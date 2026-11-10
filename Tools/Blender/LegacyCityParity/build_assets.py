"""Build and bake only the fifty missing models in the legacy city style."""
import argparse
import hashlib
import importlib.util
import json
import math
import shutil
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0,str(Path(__file__).resolve().parent))
import recipes

SOURCE = ROOT/'ArtSource/Blender/Kingdom/LegacyParityAdditions.blend'
OUT = ROOT/'ArtSource/Exports/LegacyCityParity'
REVIEW = ROOT/'Artifacts/CountryComparison/LegacyParity'
EXISTING = ['Fence_Panel','Fence_Post','Fence_Gate','Stone_Wall','Stone_Wall_Corner',
            'Crate_Open','Grain_Sack','Produce_Basket','Planter','Drain_Grate','Bollard','Bucket']
CATALOG = json.loads((ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22/asset_manifest.json').read_text())
ASSETS = {a['name']:a for a in CATALOG['assets'] if a['name'] in set(EXISTING)|set(recipes.RECIPES)}
assert len(ASSETS)==50 and len(recipes.RECIPES)==38


def load_module(name,path):
    spec=importlib.util.spec_from_file_location(name,ROOT/path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result)
    return result


shared=load_module('legacy_city_bake','Tools/Blender/UnityTownBlock/export_block.py')
shared.OUTPUT=OUT


def bounds(objects):
    points=[shared.matrix(obj)@v.co for obj in objects if obj.type=='MESH' for v in obj.data.vertices]
    low=[min(p[i] for p in points) for i in range(3)]
    high=[max(p[i] for p in points) for i in range(3)]
    return low,high


def generate():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(ROOT/'ArtSource/Blender/Kingdom/FantasyTownProps.blend'),link=False) as (available,target):
        assert set(EXISTING)<=set(available.collections)
        target.collections=list(EXISTING)
    collections={name:collection for name,collection in zip(EXISTING,target.collections)}
    recipes.materials()
    for name,recipe in recipes.RECIPES.items():
        collection,_=recipes.g.make_asset(name,recipe)
        collections[name]=collection
        print('LEGACY_MODEL_BUILT',name,flush=True)
    bpy.context.view_layer.update()
    records=[]
    for name,collection in collections.items():
        meshes=[obj for obj in collection.all_objects if obj.type=='MESH']
        assert meshes and all(obj.data.materials for obj in meshes),name
        low,high=bounds(meshes)
        materials=sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
        records.append(dict(name=name,group=ASSETS[name]['group'],display_name=ASSETS[name]['display_name'],
                            minimum=low,maximum=high,mesh_parts=len(meshes),materials=materials,
                            triangles=sum(len(poly.vertices)-2 for obj in meshes for poly in obj.data.polygons),
                            origin='legacy_source_collection' if name in EXISTING else 'legacy_recipe'))
        collection.use_fake_user=True
        collection.asset_mark()
        collection.asset_data.description=ASSETS[name]['display_name']+' | Original legacy construction and materials'
        if collection.name in bpy.context.scene.collection.children:
            bpy.context.scene.collection.children.unlink(collection)
    bpy.context.scene.name='Legacy parity source assets'
    for i,record in enumerate(sorted(records,key=lambda r:(r['group'],r['name']))):
        instance=bpy.data.objects.new(record['name']+' preview',None)
        instance.instance_type='COLLECTION';instance.instance_collection=collections[record['name']]
        bpy.context.scene.collection.objects.link(instance)
        instance.location=((i%8)*24,(i//8)*24,0)
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'source_manifest.json').write_text(json.dumps(dict(blender=bpy.app.version_string,models=records),ensure_ascii=False,indent=2)+'\n')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),check_existing=False)
    print('LEGACY_GENERATION_COMPLETE',len(records),flush=True)


def preview(obj,path):
    scene=bpy.context.scene
    scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=700;scene.render.resolution_y=700;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(path)
    scene.world=bpy.data.worlds.new('Legacy review world');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.55,.55,.55,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
    vertices=[obj.matrix_world@v.co for v in obj.data.vertices]
    low,high=bounds([obj]);center=Vector([(a+b)/2 for a,b in zip(low,high)])
    extent=max(b-a for a,b in zip(low,high))
    data=bpy.data.cameras.new('Review camera');camera=bpy.data.objects.new(data.name,data)
    scene.collection.objects.link(camera);camera.location=center+Vector((1.25,-2,1.05))*extent*3
    rotation=(center-camera.location).to_track_quat('-Z','Y');camera.rotation_euler=rotation.to_euler()
    data.type='ORTHO';local=[rotation.inverted()@(p-center) for p in vertices]
    data.ortho_scale=max(max(p.x for p in local)-min(p.x for p in local),max(p.y for p in local)-min(p.y for p in local))*1.16
    scene.camera=camera
    for index,(position,power) in enumerate([((-1,-2,3),1800),((2,1,2),1000)]):
        light_data=bpy.data.lights.new('Review key '+str(index),'AREA')
        light_data.energy=power*extent**2/12;light_data.shape='DISK';light_data.size=extent*1.6
        light=bpy.data.objects.new(light_data.name,light_data);scene.collection.objects.link(light)
        light.location=center+Vector(position)*extent
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.render.render(write_still=True)


def repair(names):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    recipes.materials()
    records=json.loads((OUT/'source_manifest.json').read_text())['models']
    for name in names:
        previous=bpy.data.collections[name]
        instances=[o for o in bpy.data.objects if o.instance_collection==previous]
        previous.name=name+' superseded'
        collection,_=recipes.g.make_asset(name,recipes.RECIPES[name])
        for instance in instances:instance.instance_collection=collection
        for obj in list(previous.all_objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(previous)
        meshes=[o for o in collection.all_objects if o.type=='MESH']
        bpy.context.view_layer.update()
        record=next(r for r in records if r['name']==name)
        record['minimum'],record['maximum']=bounds(meshes)
        record['mesh_parts']=len(meshes)
        record['triangles']=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
        collection.use_fake_user=True;collection.asset_mark()
        bpy.context.scene.collection.children.unlink(collection)
    (OUT/'source_manifest.json').write_text(json.dumps(dict(blender=bpy.app.version_string,models=records),ensure_ascii=False,indent=2)+'\n')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),check_existing=False)
    print('LEGACY_REPAIR_COMPLETE',sorted(names),flush=True)


def export(names):
    for directory in [OUT/'Models',OUT/'Textures',REVIEW/'Production']:
        directory.mkdir(parents=True,exist_ok=True)
    source_records=json.loads((OUT/'source_manifest.json').read_text())['models']
    for record in source_records:
        name=record['name']
        if names and name not in names:continue
        obj=shared.combined_mesh(SOURCE,name)
        area=sum(poly.area for poly in obj.data.polygons)
        size=min(2048,max(512,2**math.ceil(math.log2(max(1,math.sqrt(area)*32)))))
        preview(obj,REVIEW/'Production'/(name+'.png'))
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
        shared.bake(obj,name,size)
        assert obj.data.uv_layers and len(obj.data.materials)==1
        fbx=OUT/'Models'/(name+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',
            bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',
            add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
        record['atlas_size']=size
        record['fbx']='Assets/Models/Kingdom/City/Models/'+name+'.fbx'
        record['source']=str(SOURCE.relative_to(ROOT))
        record['source_sha256']=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
        (OUT/(name+'.json')).write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
        print('LEGACY_EXPORT_COMPLETE',name,size,flush=True)
    complete=[json.loads((OUT/(name+'.json')).read_text()) for name in ASSETS if (OUT/(name+'.json')).exists()]
    manifest=dict(status='PASS' if len(complete)==50 else 'INCOMPLETE',blender=bpy.app.version_string,
                  expected_models=50,models=complete)
    (OUT/'export_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    if manifest['status']=='PASS':
        destination=ROOT/'Assets/Models/Kingdom/City'
        for record in complete:
            name=record['name']
            shutil.copy2(OUT/'Models'/(name+'.fbx'),destination/'Models'/(name+'.fbx'))
            for suffix in ('BaseColor','Normal','MetallicSmoothness'):
                filename=name+'_'+suffix+'.png'
                shutil.copy2(OUT/'Textures'/filename,destination/'Textures'/filename)
        shutil.copy2(OUT/'export_manifest.json',destination/'legacy_parity_manifest.json')
    print('LEGACY_EXPORT_BATCH',len(complete),'/ 50',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--phase',choices=['generate','repair','export'],required=True)
    parser.add_argument('--only',nargs='*')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.phase=='generate':generate()
    elif args.phase=='repair':repair(set(args.only or []))
    else:export(set(args.only or []))
