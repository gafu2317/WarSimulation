import bpy
import hashlib
import json
import math
import os
import sys
from pathlib import Path
from mathutils import Matrix, Vector
import numpy as np

ROOT=Path(__file__).resolve().parents[3]
OUTPUT=ROOT/'Assets/Prototypes/TownBlock'
REVIEW=ROOT/'docs/Art/UnityTownBlock'
SOURCES={
    'KingdomBuildings_RealisticFantasy':['Fantasy_House'],
    'FantasyTownProps':['Road_Straight','Road_Corner','Paved_Plot','Produce_Stall','Cloth_Stall','Streetlamp','Bench','Crate_Closed','Barrel']}


def matrix(obj):
    return matrix(obj.parent)@obj.matrix_parent_inverse@obj.matrix_basis if obj.parent else obj.matrix_basis.copy()


def combined_mesh(source,name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    with bpy.data.libraries.load(str(source),link=False) as (available,target):
        assert name in available.collections,name
        target.collections=[name]
    collection=target.collections[0]
    copies=[]
    for original in collection.all_objects:
        if original.type!='MESH':
            continue
        duplicate=original.copy()
        duplicate.data=original.data.copy()
        transform=matrix(original)
        duplicate.parent=None
        duplicate.matrix_world=transform
        scene.collection.objects.link(duplicate)
        duplicate.select_set(True)
        copies.append(duplicate)
    bpy.context.view_layer.objects.active=copies[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name
    scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    triangulate=obj.modifiers.new('Export triangles','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    for layer in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(layer)
    obj.data.uv_layers.new(name='UVMap')
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=0.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def image(name,size,color=True):
    result=bpy.data.images.new(name,width=size,height=size,alpha=True)
    result.colorspace_settings.name='sRGB' if color else 'Non-Color'
    result.file_format='PNG'
    return result


def bake(obj,name,size):
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=16
    scene.render.bake.use_selected_to_active=False
    scene.render.bake.use_clear=True
    scene.render.bake.margin=16
    scene.render.bake.normal_space='TANGENT'
    mats=list({slot.material for slot in obj.material_slots if slot.material})
    targets={}
    for mat in mats:
        nodes=mat.node_tree.nodes
        target=nodes.new('ShaderNodeTexImage')
        nodes.active=target
        targets[mat]=target
    normal=image(name+'_Normal',size,False)
    for target in targets.values():
        target.image=normal
    bpy.ops.object.bake(type='NORMAL')
    normal.filepath_raw=str(OUTPUT/'Textures'/f'{name}_Normal.png')
    normal.save()
    base=image(name+'_BaseColor',size)
    originals={}
    for mat in mats:
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
        output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
        originals[mat]=(shader,output)
        emission=nodes.new('ShaderNodeEmission')
        color=shader.inputs['Base Color']
        if color.is_linked:
            links.new(color.links[0].from_socket,emission.inputs['Color'])
        else:
            emission.inputs['Color'].default_value=color.default_value
        links.new(emission.outputs[0],output.inputs['Surface'])
        targets[mat].image=base
        nodes.active=targets[mat]
    bpy.ops.object.bake(type='EMIT')
    base.filepath_raw=str(OUTPUT/'Textures'/f'{name}_BaseColor.png')
    base.save()
    packed=image(name+'_MetallicSmoothness',size,False)
    for mat,(shader,output) in originals.items():
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        emission=nodes.new('ShaderNodeEmission')
        emission.inputs['Color'].default_value=(shader.inputs['Metallic'].default_value,1,1-shader.inputs['Roughness'].default_value,1)
        links.new(emission.outputs[0],output.inputs['Surface'])
        targets[mat].image=packed
        nodes.active=targets[mat]
    bpy.ops.object.bake(type='EMIT')
    pixels=np.empty(size*size*4,dtype=np.float32)
    packed.pixels.foreach_get(pixels)
    pixels=pixels.reshape((-1,4))
    pixels[:,3]=pixels[:,2]
    pixels[:,2]=0
    packed.pixels.foreach_set(pixels.ravel())
    packed.filepath_raw=str(OUTPUT/'Textures'/f'{name}_MetallicSmoothness.png')
    packed.save()
    mat=bpy.data.materials.new(name+'_Baked')
    mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=base
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.material_index=0


def main():
    for directory in (OUTPUT/'Models',OUTPUT/'Textures',REVIEW):
        directory.mkdir(parents=True,exist_ok=True)
    records=[]
    for group,names in SOURCES.items():
        source=ROOT/'ArtSource/Blender'/f'{group}.blend'
        digest=hashlib.sha256(source.read_bytes()).hexdigest()
        for name in names:
            obj=combined_mesh(source,name)
            area=sum(poly.area for poly in obj.data.polygons)
            size=2**math.ceil(math.log2(math.sqrt(area)*128))
            print('BAKE_START',name,size,flush=True)
            bake(obj,name,size)
            points=[v.co for v in obj.data.vertices]
            minimum=[min(p[i] for p in points) for i in range(3)]
            maximum=[max(p[i] for p in points) for i in range(3)]
            bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'Models'/f'{name}.fbx'),use_selection=True,
                object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
                axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,
                mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
            records.append({'name':name,'source':str(source.relative_to(ROOT)),'source_sha256':digest,
                'minimum':minimum,'maximum':maximum,'triangles':len(obj.data.polygons),'atlas_size':size,
                'fbx':f'Assets/Prototypes/TownBlock/Models/{name}.fbx'})
            print('EXPORTED',name,flush=True)
        assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
    (REVIEW/'export_manifest.json').write_text(json.dumps({'models':records,'blender':bpy.app.version_string,
        'uv_texels_per_meter':128,'origin':'original asset pivot preserved'},ensure_ascii=False,indent=2))
    print('EXPORT_COMPLETE',len(records),flush=True)


if __name__=='__main__':
    main()
