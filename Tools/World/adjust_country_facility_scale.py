"""Bake facility widths and storey heights into review copies of the Blender models."""

import argparse
from bisect import bisect_right
import json
from pathlib import Path
import shutil
import sys

import bpy
from mathutils import Matrix

sys.path.insert(0, str(Path(__file__).resolve().parent))
from country_redesign_assets import CONCEPT, MODEL_NAMES, bounds, measure


WIDTH_METRES = {
    'Observatory': 20.5,
    'Library': 23.0,
    'Museum': 26.2,
    'Clinic': 18.7,
    'WarriorAcademy_Lv5': 25.5,
    'ArcaneAcademy_Lv5': 27.3,
    'SpiritAcademy_Lv5': 25.4,
    'Guardhouse': 10.7,
    'Blacksmith': 16.0,
    'Tavern': 18.0,
    'Casino': 20.1,
    'CrimsonRowhouse': 20.3,
    'VeiledCourtyard': 19.9,
    'Ballroom': 21.3,
    'MasqueradeSalon': 19.3,
}
REFERENCE_MODELS = {'Arena', 'Church', 'Plaza'}

# Ground floor, upper floor or framing band, and upper wall in recipe metres.
STOREYS = {
    'Clinic': ((.22, 6.25, 15.04), (3.30, 3.20)),
    'Tavern': ((.25, 7.78, 14.93), (3.25, 3.10)),
    'SpiritAcademy_Lv5': ((.22, 6.56, 13.22), (3.45, 3.45)),
    'Casino': ((.28, 10.04, 18.27), (4.50, 3.75)),
    'CrimsonRowhouse': ((.23, 6.36, 10.40), (3.15, 3.15)),
    'VeiledCourtyard': ((.24, 6.16, 12.44), (3.15, 3.10)),
    'MasqueradeSalon': ((.27, 8.20, 15.96), (3.60, 3.20)),
}


def dimensions(collection):
    low, high = bounds(collection)
    return [b - a for a, b in zip(low, high)]


def storey_mapping(model, scene, factor):
    if model not in STOREYS:
        return lambda height: height * factor, {}
    levels, storeys = STOREYS[model]
    measured_levels = scene.get('storey_levels_metres')
    if measured_levels is None:
        measured_levels = [level * scene['geometry_factor'] for level in levels]
    source = [0, *measured_levels]
    after = [0, source[1] * factor]
    for height in storeys:
        after.append(after[-1] + height)
    slopes = [(b - a) / (d - c) for a, b, c, d in zip(after, after[1:], source, source[1:])]
    slopes.append(factor)

    def height_at(height):
        i = max(0, bisect_right(source, height) - 1)
        return after[i] + (height - source[i]) * slopes[i]

    return height_at, {'old_levels': source[1:], 'new_levels': after[1:]}


def scale_mesh(mesh, factor, height_at):
    mesh.transform(Matrix.Scale(factor, 4))
    for vertex in mesh.vertices:
        vertex.co.z = height_at(vertex.co.z / factor)
    if any(material and 'tile_repeat_metres' in material for material in mesh.materials):
        for uv in mesh.uv_layers.active.uv:
            uv.vector *= factor
    mesh.update()


def prepare(output):
    assert set(MODEL_NAMES) == WIDTH_METRES.keys() | REFERENCE_MODELS
    models = output / 'models'
    models.mkdir(parents=True, exist_ok=True)
    report = {}
    for name in MODEL_NAMES:
        source = CONCEPT / f'{name}.blend'
        destination = models / source.name
        bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
        scene = bpy.context.scene
        collection = bpy.data.collections[name]
        before = dimensions(collection)
        target = WIDTH_METRES.get(name)
        factor = target / before[0] if target is not None else 1
        height_at, levels = storey_mapping(name, scene, factor)
        if target is None:
            shutil.copyfile(source, destination)
        else:
            meshes = set()
            for obj in collection.all_objects:
                assert all(abs(v - 1) < 1e-6 for v in obj.scale), obj.name
                if obj.type == 'MESH':
                    assert obj.matrix_world.is_identity, obj.name
                    meshes.add(obj.data)
                else:
                    x, y, z = obj.location
                    obj.location = (x * factor, y * factor, height_at(z))
            for mesh in meshes:
                scale_mesh(mesh, factor, height_at)
            collection['layout_reference_ratio'] = collection.get('layout_reference_ratio', 1) * factor
            bpy.context.view_layer.update()
            measured = dimensions(collection)
            assert abs(measured[0] - target) < .001 and abs(measured[2] - height_at(before[2])) < .001, name
            scene['bounds_metres'] = [round(v, 3) for v in measured]
            if levels:
                scene['storey_levels_metres'] = levels['new_levels']
            bpy.context.preferences.filepaths.save_version = 0
            bpy.ops.wm.save_as_mainfile(filepath=str(destination), compress=True)
        report[name] = {'before': before, 'after': dimensions(collection), 'ratio': factor, **levels}
        print(name, *(f'{v:.2f} m' for v in report[name]['after']), flush=True)
    (output / 'scale_measurements.json').write_text(json.dumps(report, indent=2) + '\n')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    measure(output / 'dimensions.json', models)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    prepare(args.output.resolve())
