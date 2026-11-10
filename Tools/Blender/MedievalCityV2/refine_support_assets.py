"""Regenerate only reviewed support assets, preserving the delivered archive."""
import hashlib
import json
from pathlib import Path
import random
import sys
import bpy
import bmesh

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build_assets as kit
from quality_details import SUPPORT_ASSETS


def main():
    root=HERE.parents[2]
    out=root/'Artifacts/CityV2Quality20261030/Generated/Support'
    out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    records=[]
    for name in sorted(SUPPORT_ASSETS):
        kit.RAND=random.Random(int(hashlib.sha256(name.encode()).hexdigest()[:8],16))
        kit.COLLECTION=bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(kit.COLLECTION)
        kit.build(name);kit.finish_edges()
        for obj in kit.COLLECTION.objects:
            if obj.type!='MESH':continue
            mesh=bmesh.new();mesh.from_mesh(obj.data)
            bmesh.ops.triangulate(mesh,faces=list(mesh.faces))
            empty=[face for face in mesh.faces if face.calc_area()<1e-7]
            if empty:bmesh.ops.delete(mesh,geom=empty,context='FACES')
            mesh.to_mesh(obj.data);mesh.free();obj.data.update()
        bpy.context.view_layer.update()
        minimum,maximum=kit.bounds(kit.COLLECTION.objects)
        for obj in kit.COLLECTION.objects:obj.location.z-=minimum.z
        bpy.context.view_layer.update()
        minimum,maximum=kit.bounds(kit.COLLECTION.objects)
        bpy.ops.object.select_all(action='DESELECT')
        objects=list(kit.COLLECTION.objects)
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        path=out/(name+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
            apply_unit_scale=True,bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=True,
            add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
        records.append(dict(name=name,staged=str(path.relative_to(root)),fbx=f'Assets/Models/Kingdom/CityV2/Models/{name}.fbx',
            group=kit.group_for(name),materials=sorted({m.name for o in objects for m in o.data.materials}),
            triangles=sum(len(f.vertices)-2 for o in objects for f in o.data.polygons),mesh_parts=len(objects),
            minimum=list(minimum),maximum=list(maximum),coordinateSystem='X right, Z up, front -Y',
            blend='Art/CityV2Quality/CityV2_Support.blend'))
        for obj in objects:obj.select_set(False)
        kit.COLLECTION.hide_viewport=True;kit.COLLECTION.hide_render=True
        print('SUPPORT',name,len(objects),flush=True)
    source=root/'Art/CityV2Quality';source.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/'CityV2_Support.blend'))
    (out.parent/'support_generation.json').write_text(json.dumps({'assets':records},ensure_ascii=False,indent=2))
    print('SUPPORT_COMPLETE',len(records),flush=True)


if __name__=='__main__':main()
