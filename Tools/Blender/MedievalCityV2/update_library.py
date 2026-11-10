"""Merge reviewed geometry revisions while retaining unchanged asset files."""
from pathlib import Path
import json
import re

import bpy


ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22'


def main():
    manifest=json.loads((OUT/'asset_manifest.json').read_text())
    replacements=json.loads((OUT/'sample_manifest.json').read_text())['assets']
    known={a['name']:a for a in manifest['assets']}
    for asset in replacements:
        name=asset['name']
        if name not in known:raise ValueError('Not an existing library asset: '+name)
        old_scene=bpy.data.scenes[name]
        old_collection=bpy.data.collections[name]
        old_objects=list(old_scene.objects)
        old_scene.name='_Replaced_'+name
        old_collection.name='_Replaced_'+name
        with bpy.data.libraries.load(str(OUT/asset['blend']),link=False) as (available,loaded):
            if name not in available.scenes:raise ValueError('Missing replacement scene: '+name)
            loaded.scenes=[name]
        new_collection=bpy.data.collections[name]
        for obj in new_collection.objects:
            for slot in obj.material_slots:
                mat=slot.material
                base=re.sub(r'\.\d{3}$','',mat.name)
                shared=bpy.data.materials.get(base)
                if shared is not None and shared!=mat:
                    old_maps={Path(n.image.filepath).name for n in shared.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
                    new_maps={Path(n.image.filepath).name for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
                    if old_maps!=new_maps:raise ValueError('Material maps changed; a full rebuild is required')
                    slot.material=shared
        for obj in bpy.data.objects:
            if obj.instance_type=='COLLECTION' and obj.instance_collection==old_collection:
                obj.instance_collection=new_collection
        bpy.data.scenes.remove(old_scene)
        bpy.data.collections.remove(old_collection)
        for obj in old_objects:
            if obj.users==0:bpy.data.objects.remove(obj)
        known[name]=asset
        print('LIBRARY_REPLACED',name,flush=True)
    for image in bpy.data.images:
        if image.source!='FILE' or not image.filepath:continue
        filename=Path(image.filepath).name
        if not (OUT/'Textures'/filename).exists():raise FileNotFoundError(filename)
        image.filepath='//../Textures/'+filename
    bpy.data.orphans_purge(do_recursive=True)
    master=OUT/'Blender/MedievalCity_Master.blend'
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.window.scene=bpy.data.scenes['00_All_Assets']
    bpy.ops.wm.save_as_mainfile(filepath=str(master),compress=True,relative_remap=False)
    manifest['assets']=[known[a['name']] for a in manifest['assets']]
    manifest['status']='authored_pending_visual_review'
    (OUT/'asset_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')


if __name__=='__main__':main()
