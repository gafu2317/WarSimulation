"""Exercise legacy recipes and export into review artifacts without changing game assets."""
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
SOURCE = ROOT / 'ArtSource/Blender/Kingdom'
EXISTING = ['Fence_Panel', 'Fence_Post', 'Fence_Gate', 'Stone_Wall', 'Stone_Wall_Corner',
            'Crate_Open', 'Grain_Sack', 'Produce_Basket', 'Planter', 'Drain_Grate', 'Bollard', 'Bucket']


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def points(objects):
    return [obj.matrix_world @ vertex.co for obj in objects if obj.type == 'MESH' for vertex in obj.data.vertices]


def bounds(vertices):
    low = [min(p[i] for p in vertices) for i in range(3)]
    high = [max(p[i] for p in vertices) for i in range(3)]
    return dict(minimum=low, maximum=high, size=[b-a for a,b in zip(low,high)])


def preview(obj, path):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 800
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(path)
    scene.world = bpy.data.worlds.new('Review studio')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.55,.55,.55,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
    vertices = points([obj])
    center = sum(vertices,Vector()) / len(vertices)
    extents = bounds(vertices)['size']
    center.z = (min(v.z for v in vertices)+max(v.z for v in vertices))/2
    distance = max(extents)*3
    camera_data = bpy.data.cameras.new('Review camera')
    camera = bpy.data.objects.new('Review camera',camera_data)
    scene.collection.objects.link(camera)
    camera.location = center+Vector((1.25,-2,1.05))*distance
    rotation = (center-camera.location).to_track_quat('-Z','Y')
    camera.rotation_euler = rotation.to_euler()
    camera_data.type = 'ORTHO'
    projected = [rotation.inverted()@(p-center) for p in vertices]
    camera_data.ortho_scale = max(max(p.x for p in projected)-min(p.x for p in projected),
                                 max(p.y for p in projected)-min(p.y for p in projected))*1.2
    scene.camera = camera
    for index,(offset,power) in enumerate([((-1,-2,3),1800),((2,1,2),1000)]):
        data = bpy.data.lights.new('Review light '+str(index),'AREA')
        data.energy = power*max(extents)**2/12
        data.shape = 'DISK'
        data.size = max(extents)*1.6
        light = bpy.data.objects.new(data.name,data)
        scene.collection.objects.link(light)
        light.location = center+Vector(offset)*max(extents)
        light.rotation_euler = (center-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.render.render(write_still=True)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    protected = [ROOT/'Assets/Scenes/Country.unity',SOURCE/'FantasyTownProps.blend',
                 SOURCE/'FantasyTownFacilities.blend',SOURCE/'FantasyCivicFacilities.blend']
    for folder in ['Assets/Prefabs/Kingdom/City/Prefabs','Assets/Prefabs/Kingdom/CityV2/Prefabs']:
        protected.extend(sorted((ROOT/folder).glob('*.prefab')))
    before = {str(path.relative_to(ROOT)):digest(path) for path in protected}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(SOURCE/'FantasyTownProps.blend'),link=False) as (available,target):
        assert set(EXISTING) <= set(available.collections)
        target.collections = list(EXISTING)
    original_assets = []
    for name,collection in zip(EXISTING,target.collections):
        meshes = [obj for obj in collection.all_objects if obj.type=='MESH']
        assert meshes
        materials = sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
        original_assets.append(dict(name=name,mesh_parts=len(meshes),materials=materials))
    print('LEGACY_COLLECTIONS_CONFIRMED',len(original_assets),flush=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    props = module('legacy_props_probe','Tools/Blender/generate_fantasy_town_props.py')
    props.materials()
    def watchtower():
        props.t.square_watchtower()
        bpy.context.view_layer.update()
        for obj in props.g.PARTS.objects:
            if obj.type!='MESH' or not obj.name.startswith(('Carved lancet','Recessed window','Stone sill','Window center','Window transom')):
                continue
            vertices=points([obj])
            if max(v.z for v in vertices)<3.4 and max(v.y for v in vertices)<-2:
                obj.location.x-=1.2
        props.r.door(0,-2.25,1.2,2.4,.15)
        props.g.box('Standalone tower doorstep',(0,-2.6,.1),(1.8,.7,.2),'StoneTrim',.012)
    recipes = [('Bucket',props.bucket),('Watchtower',watchtower),('Fountain',props.m.fountain)]
    generated = []
    for name,recipe in recipes:
        collection,_ = props.g.make_asset(name,recipe)
        bpy.context.view_layer.update()
        meshes = [obj for obj in collection.all_objects if obj.type=='MESH']
        assert meshes and all(obj.data.materials for obj in meshes)
        generated.append(dict(name=name,mesh_parts=len(meshes),**bounds(points(meshes))))
    probe_source = OUT/'LegacyParity_Probe.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(probe_source),check_existing=False)

    shared = module('legacy_export_probe','Tools/Blender/UnityTownBlock/export_block.py')
    shared.OUTPUT = OUT
    (OUT/'Textures').mkdir(exist_ok=True)
    (OUT/'Models').mkdir(exist_ok=True)
    exports = []
    for name,_ in recipes:
        obj = shared.combined_mesh(probe_source,name)
        expected = bounds(points([obj]))
        preview(obj,OUT/(name+'_LegacyProbe.png'))
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        shared.bake(obj,name,512)
        fbx = OUT/'Models'/(name+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',
            bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',
            add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=False)
        imported = [obj for obj in bpy.context.scene.objects if obj.type=='MESH']
        actual = bounds(points(imported))
        error = max(abs(a-b) for a,b in zip(expected['size'],actual['size']))
        textures = [OUT/'Textures'/(name+'_'+suffix+'.png') for suffix in ['BaseColor','Normal','MetallicSmoothness']]
        assert error<.002 and all(path.is_file() for path in textures)
        assert all(obj.data.uv_layers for obj in imported)
        exports.append(dict(name=name,status='PASS',dimension_error_m=error,expected=expected,
                            imported=actual,textures=[str(p.relative_to(ROOT)) for p in textures],
                            fbx=str(fbx.relative_to(ROOT))))
        print('LEGACY_EXPORT_ROUNDTRIP_PASS',name,'error_m',error,flush=True)
    unchanged = all(path.is_file() and digest(path)==before[str(path.relative_to(ROOT))] for path in protected)
    assert unchanged
    report = dict(status='PASS',blender=bpy.app.version_string,existing_unimported=original_assets,
                  generated_recipes=generated,exports=exports,protected_files=len(protected),
                  existing_assets_and_scene_unchanged=unchanged,source_sha256=before)
    (OUT/'probe_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('LEGACY_PARITY_PROBE_PASS',len(exports),'exports',len(protected),'protected files',flush=True)


if __name__=='__main__':
    main()
