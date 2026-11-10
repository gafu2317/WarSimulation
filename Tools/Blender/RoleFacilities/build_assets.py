"""Factory-empty authoring. Existing FBX, prefabs and building recipes are never read."""
from pathlib import Path
import sys
import json
import hashlib
import argparse
import bpy

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
sys.path.insert(0,str(HERE))
from mesh import Model
from architecture import build, aliases


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--style',choices=['CityV2'],default='CityV2')
    parser.add_argument('--only',nargs='*')
    arguments=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    design=json.loads((HERE/'design.json').read_text())
    report=ROOT/'Artifacts/RoleFacilityRebuild20261024'
    entries=[]
    for style in [arguments.style]:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        out=report/'Generated'/style
        out.mkdir(parents=True,exist_ok=True)
        specs=[e for e in design['assets'] if not arguments.only or e['name'] in arguments.only]
        for i,spec in enumerate(specs):
            name=spec['name'];plot=spec['plot'];palette=aliases(style,name)
            materials={}
            for name_mat in set(palette.values()):
                mat=bpy.data.materials.get(name_mat) or bpy.data.materials.new(name_mat)
                mat.diffuse_color=(.58,.53,.44,1)
                materials[name_mat]=mat
            m=Model(name,palette)
            level=int(name[-1]) if '_Lv' in name else 5
            scale=.64+level*.072 if '_Lv' in name else 1
            build(m,name,plot['width']*scale,plot['depth']*scale,style)
            objects=m.finish(materials)
            bpy.ops.object.select_all(action='DESELECT')
            for obj in objects:obj.select_set(True)
            bpy.context.view_layer.objects.active=objects[0]
            fbx=out/(name+'.fbx')
            bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,
                apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=True,
                mesh_smooth_type='FACE',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
            vertices=[v for part in m.parts.values() for v in part[0]]
            faces=[f for part in m.parts.values() for f in part[1]]
            record=dict(style=style,name=name,label=spec['label'],staged=str(fbx.relative_to(ROOT)),fbx=f'Assets/Models/Kingdom/{style}/Models/{name}.fbx',
                materials=sorted({key[1] for key in m.parts}),min=[min(v[k] for v in vertices) for k in range(3)],
                max=[max(v[k] for v in vertices) for k in range(3)],vertices=len(vertices),faces=len(faces),features=sorted(m.features),markers=m.markers,
                triangles=sum(len(f)-2 for f in faces),mesh_parts=len(m.parts),blend=f'Art/RoleFacilities/{style}_Facilities.blend',
                coordinateSystem='X right, Y up, Z back',
                geometryHash=hashlib.sha256(json.dumps([p[0:2] for p in m.parts.values()]).encode()).hexdigest(),
                fromScratch=True,landmarks=sorted(m.landmarks),sourceInputs=['facility function','plot dimensions','style brief','distance-readable identity'])
            entries.append(record)
            for obj in objects:obj.select_set(False)
            collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
            for obj in objects:
                for old in list(obj.users_collection):old.objects.unlink(obj)
                collection.objects.link(obj)
            collection.hide_viewport=True;collection.hide_render=True
            print(f'ROLE {style} {i+1}/{len(specs)} {name} {len(vertices)} vertices {len(m.features)} functional groups',flush=True)
        source=ROOT/'Art/RoleFacilities';source.mkdir(parents=True,exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(source/(style+'_Facilities.blend')))
    manifest=report/('generation_'+arguments.style+('.preview' if arguments.only else '')+'.json')
    manifest.write_text(json.dumps({'authoring':'Purpose-built architecture with large equipment, sculptural shop signs and readable silhouettes.',
        'existingModelsRead':False,'existingImagesRead':False,'designHash':hashlib.sha256((HERE/'design.json').read_bytes()).hexdigest(),
        'sources':{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in HERE.glob('*.py')},'assets':entries},ensure_ascii=False,indent=2))
    print('ROLE_COMPLETE '+str(len(entries))+' '+str(manifest),flush=True)


if __name__=='__main__':main()
