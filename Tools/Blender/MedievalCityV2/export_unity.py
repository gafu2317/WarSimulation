"""Export the delivered city kit without changing its Blender originals."""
import json
from pathlib import Path
import shutil

import bpy

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'ArtSource/Deliveries/MedievalCity_2026-10-22'
TARGET = ROOT / 'Assets/Models/Kingdom/CityV2'


def main():
    manifest = json.loads((SOURCE / 'asset_manifest.json').read_text())
    TARGET.mkdir(parents=True, exist_ok=True)
    shutil.copytree(SOURCE / 'Textures', TARGET / 'Textures', dirs_exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'Blender/MedievalCity_Master.blend'))
    scene = bpy.data.scenes.new('Unity export')
    bpy.context.window.scene = scene
    records = []
    for asset in manifest['assets']:
        copies = []
        for original in bpy.data.collections[asset['name']].all_objects:
            if original.type != 'MESH':
                continue
            obj = original.copy()
            obj.data = original.data.copy()
            scene.collection.objects.link(obj)
            obj.matrix_world = original.matrix_world.copy()
            copies.append(obj)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in copies:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = copies[0]
        bpy.ops.object.join()
        joined = bpy.context.object
        joined.name = asset['name']
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        path = TARGET / 'Models' / (asset['name'] + '.fbx')
        path.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.export_scene.fbx(
            filepath=str(path), use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
            bake_space_transform=True, mesh_smooth_type='FACE',
            use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
            path_mode='RELATIVE', embed_textures=False)
        records.append(dict(asset, fbx=str(path.relative_to(ROOT))))
        bpy.data.objects.remove(joined, do_unlink=True)
        print('UNITY_EXPORT', len(records), asset['name'], flush=True)
    (TARGET / 'export_manifest.json').write_text(json.dumps({'assets': records}, ensure_ascii=False, indent=2) + '\n')
    shutil.copy2(SOURCE / 'texture_manifest.json', TARGET / 'texture_manifest.json')
    print('UNITY_EXPORT_COMPLETE', len(records), flush=True)


if __name__ == '__main__':
    main()
