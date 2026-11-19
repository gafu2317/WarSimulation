"""Measure and load the Blender and CityV2 assets used by the town layout."""

import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
CONCEPT = ROOT / 'Art/RoleFacilities/ConceptModels'
CITY = ROOT / 'Assets/Models/Kingdom/CityV2'
TOOLS = ROOT / 'Tools/Blender/RoleFacilities'
sys.path[:0] = [str(Path(__file__).resolve().parent), str(TOOLS / 'ConceptModels'), str(TOOLS)]
from build import MODEL_NAMES
from country_prop_models import RECIPES as PROP_RECIPES, make_prop
from country_street_life import CATALOG

ENTRY_PASSAGES = {'Bakery':-2.21, 'Guildhall':1.55, 'Market_Hall':1.55, 'WarriorAcademy_Lv5':-1.62}


def mesh_bounds(objects):
    corners = [obj.matrix_world @ Vector(corner) for obj in objects
               if obj.type == 'MESH' for corner in obj.bound_box]
    return ([min(v[i] for v in corners) for i in range(3)],
            [max(v[i] for v in corners) for i in range(3)])


def bounds(collection):
    return mesh_bounds(collection.all_objects)


def entrance(name, collection, low):
    marker = collection.objects.get(f'{name}_Entrance')
    if marker:
        x,y,z = marker.location
        if name in ENTRY_PASSAGES:
            x = ENTRY_PASSAGES[name] * collection.get('layout_reference_ratio',1)
        return [x,y,z]
    if name == 'Plaza':
        return [0,low[1]+.12,low[2]]
    feature = 'Doorstep' if name in {'Fantasy_House','MerchantHouse'} else 'Entrance threshold'
    threshold = next(obj for obj in collection.all_objects
                     if obj.type=='MESH' and obj.name.split('.')[0]==feature)
    bottom,top = mesh_bounds((threshold,))
    return [(bottom[0]+top[0])/2,bottom[1]+.09,top[2]]


def load(name, *, link=False, concept_dir=CONCEPT):
    if name in PROP_RECIPES:
        return make_prop(name)
    if name in MODEL_NAMES:
        with bpy.data.libraries.load(str(concept_dir / f'{name}.blend'), link=link) as (source, output):
            assert name in source.collections, name
            output.collections = [name]
        collection = output.collections[0]
        if collection.hide_render or collection.hide_viewport:
            raise ValueError(f'{name} is hidden in its Blender source')
        return collection

    path = CITY / 'Models' / f'{name}.fbx'
    existing = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    collection = bpy.data.collections.new(f'CityV2 {name}')
    for obj in sorted(set(bpy.data.objects) - existing,key=lambda o:o.name):
        if name == 'Watermill' and obj.type == 'MESH' and (
            'millrace' in obj.name.casefold() or any(slot.name.split('.')[0]=='Water' for slot in obj.material_slots)
        ):
            bpy.data.objects.remove(obj,do_unlink=True)
            continue
        collection.objects.link(obj)
        for parent in tuple(obj.users_collection):
            if parent != collection:
                parent.objects.unlink(obj)
    return collection


def measure(output, concept_dir):
    plan = json.loads((ROOT / 'Assets/Data/CountryComparison/town_plan.json').read_text())
    buildings = ({entry['name'] for entry in plan['buildings']} | set(MODEL_NAMES)) - {'LanternSpire', 'VelvetTerrace'}
    props = {'Well', 'Bucket', 'Bench', 'Streetlamp', 'Signpost', 'Noticeboard', 'Planter', 'Flower_Border',
             'Tree_Shade', 'Tree_Street', 'Tree_AlleyCypress', 'Clothesline',
             'Barrel', 'Crate_Closed', 'Covered_Wagon', 'Produce_Basket', 'Water_Trough'}
    props.update(entry['name'] for entry in plan['props'] if entry['name'].endswith('_Stall'))
    props.update(CATALOG | PROP_RECIPES.keys())
    infrastructure = {'Granite_Straight', 'Granite_Gate', 'Watchtower', 'Stone_Bridge',
                      'Wooden_Bridge', 'Dock_Section', 'Stone_Wall', 'Fence_Panel', 'Wooden_Stairs'}
    result = {}
    for name in sorted(buildings | props | infrastructure):
        collection = load(name,concept_dir=concept_dir)
        low,high = bounds(collection)
        record = {'low':low,'high':high}
        if name in buildings:
            record['entry'] = entrance(name,collection,low)
        if name == 'CrimsonRowhouse':
            foyer = next(obj for obj in collection.objects if obj.name.startswith('FoyerFrontWall_Openings_Door_Oak'))
            a,b = mesh_bounds((foyer,))
            record['secondaryEntry'] = [(a[0]+b[0])/2,a[1]-.17,a[2]]
        result[name] = record
        print(name, [round(hi-lo, 3) for lo,hi in zip(low,high)], flush=True)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--concept-dir',type=Path,default=CONCEPT)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    measure(args.output,args.concept_dir.resolve())
