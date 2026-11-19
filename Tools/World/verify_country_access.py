"""Check the street-to-door walking lines in a saved Blender town."""

from collections import defaultdict
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_country_redesign import Box, coords, route_boxes
from country_redesign_assets import MODEL_NAMES, bounds
from render_country_redesign import collision_mesh


PAVING = .028
# Local waypoints stop on the final tread or just inside an open passage.
# This keeps closed leaves out of the sweep while catching blockers before them.
WAYPOINTS = {
    'ArcaneAcademy_Lv5': [(3.968,-9.10)], 'Arena': [(0,-11.7)], 'Bakery': [(-2.21,-3.48)],
    'Ballroom': [(0,-10.4)], 'Barracks': [(0,-3.3)], 'Bathhouse': [(0,-2.60)],
    'Blacksmith': [(0,-8.9)], 'Casino': [(2.192,-10.8)], 'Chapel': [(0,-3.64)],
    'Church': [(0,-9.91)], 'Clinic': [(0,-7.6)], 'CrimsonRowhouse': [(0,-9.05)],
    'Granary': [(0,-4.285)], 'Guardhouse': [(0,-11.36)],
    'Guildhall': [(1.55,-3.48),(0,-3.48),(0,-3.09)],
    'Library': [(0,-10.1)], 'Market_Hall': [(1.55,-2.5)], 'MasqueradeSalon': [(9.85,-7.45)],
    'Museum': [(0,-11.62)], 'Observatory': [(0,-10.54)], 'Plaza': [(0,-8.25)],
    'Royal_Castle': [(0,1.20)], 'SpiritAcademy_Lv5': [(0,-10.5)],
    'Stable': [(0,-5.48),(4.2,-5.48)], 'Tavern': [(0,-8.49)], 'VeiledCourtyard': [(0,-7.6)],
    'Warehouse': [(0,-5.14)], 'WarriorAcademy_Lv5': [(-1.62,1.62),(0,1.62),(0,3.58)],
}


def joined_mesh(collection, additions, inverse):
    vertices,faces = [],[]
    for obj,space in [(o,o.matrix_world) for o in collection.all_objects]+[
        (o,inverse @ o.matrix_world) for o in additions
    ]:
        if obj.type != 'MESH':
            continue
        base = len(vertices)
        vertices.extend(space@v.co for v in obj.data.vertices)
        faces.extend(tuple(base+i for i in face.vertices) for face in obj.data.polygons)
    return BVHTree.FromPolygons(vertices,faces)


def walk(mesh, waypoints, ground, scale):
    floor = ground
    for a,b in zip(waypoints,waypoints[1:]):
        delta = b-a
        if delta.length < .001:
            continue
        normal = delta.normalized()
        perp = Vector((normal.y,-normal.x))*.29/scale
        count = math.ceil(delta.length*scale/.08)
        prev = a
        for i in range(1,count+1):
            at = a.lerp(b,i/count)
            hit = mesh.ray_cast((*at,floor+.281/scale),(0,0,-1),.31/scale+floor-ground)[0]
            next_floor = max(ground,hit.z) if hit is not None else ground
            if (floor-next_floor)*scale > .285:
                raise ValueError(f'step drops {(floor-next_floor)*scale:.2f} m at {tuple(at)}')
            for offset in (-perp,perp,Vector((0,0))):
                for height in (.30,1.02,1.72):
                    ray = (*(prev+offset),max(floor,next_floor)+height/scale)
                    obstacle = mesh.ray_cast(ray,(*normal,0),(at-prev).length)[0]
                    if obstacle is not None:
                        raise ValueError(f'blocked at {tuple(round(v,3) for v in obstacle)}')
            prev,floor = at,next_floor


def instance_map(plan):
    instances = defaultdict(list)
    for obj in bpy.context.scene.objects:
        if not obj.get('model'):
            continue
        low,high = bounds(obj.instance_collection)
        cx,cy = ((a+b)/2 for a,b in zip(low[:2],high[:2]))
        x,y,_ = obj.matrix_world @ Vector((cx,cy,low[2]))
        instances[obj['model']].append(((x,y),obj))
    assert sum(map(len,instances.values()))==len(plan['buildings']), 'Saved plot count'
    return instances


def verify(plan):
    bpy.context.view_layer.update()
    instances = instance_map(plan)
    access = bpy.data.collections['Public entrances and landings']
    paving = collision_mesh(bpy.data.collections['Town ground and paved streets'])
    meshes = {}
    count = 0
    extras = {p['name'].removesuffix(' foyer door'):p for p in plan.get('secondaryAccessPaths',[])}
    for plot,path in zip(plan['buildings'],plan['accessPaths'],strict=True):
        name = plot.get('modernModel',plot['name'])
        position = plot['x'],plot['z']
        center,obj = min(instances[name],key=lambda candidate:math.dist(candidate[0],position))
        assert math.dist(center,position)<.003,(name,'placement mismatch')
        inverse = obj.matrix_world.inverted()
        collection = obj.instance_collection
        if collection not in meshes:
            detail = {'Granary':'Granary landing','WarriorAcademy_Lv5':'Training yard passage',
                      'Warehouse':'Warehouse loading tread'}.get(name)
            added = [o for o in access.objects if detail and o.name.startswith(detail)]
            meshes[collection] = joined_mesh(collection,added,inverse)
        secondary = extras.get(name)
        for route in [path,secondary] if secondary else [path]:
            entrance,street = coords(route)
            for f in (.003,.1,.25,.5,.75,.9,.997):
                x,z = (b+(a-b)*f for a,b in zip(entrance,street))
                slab = paving.ray_cast((x,z,1),(0,0,-1),1.1)[0]
                assert slab is not None and abs(slab.z-PAVING)<.003,(name,'discontinuous paving',x,z)
            local = [inverse @ Vector((*p,PAVING)) for p in (street,entrance)]
            x,y,z = local[-1]
            ratio = collection.get('layout_reference_ratio',1)
            approach = [(-12.96,-8.73)] if route is secondary else WAYPOINTS.get(name)
            approach = [Vector(p)*ratio for p in approach] if approach else [Vector((x,y+.28))]
            waypoints = [Vector(p[:2]) for p in local]+approach
            try:
                walk(meshes[collection],waypoints,z,obj.scale.x)
            except ValueError as error:
                raise ValueError(f'{name} at {plot["x"]}, {plot["z"]}: {error}') from error
            count += 1

    for path in plan['amenityPaths']:
        for a,b in zip(coords(path),coords(path)[1:]):
            for fraction in (.05,.25,.5,.75,.95):
                x,z = (start+(end-start)*fraction for start,end in zip(a,b))
                floor = paving.ray_cast((x,z,1),(0,0,-1),1.1)[0]
                assert floor is not None and abs(floor.z-PAVING)<.003,(path['name'],'discontinuous paving',x,z)

    paths = plan['accessPaths']+plan['amenityPaths']+plan.get('secondaryAccessPaths',[])
    corridors = [shape for route in paths for shape in route_boxes(route)]
    furniture = bpy.data.collections['Market, wells, seats and planting']
    shapes = []
    for obj in furniture.objects:
        low,high = bounds(obj.instance_collection)
        w,d = ((b-a)*s for a,b,s in zip(low[:2],high[:2],obj.scale[:2]))
        x,y,_ = obj.matrix_world @ Vector(((low[0]+high[0])/2,(low[1]+high[1])/2,low[2]))
        shape = Box.around(x,y,w,d,round(-math.degrees(obj.rotation_euler.z)))
        assert not any(shape.overlaps(b) for b in corridors), (obj.name,'furnishing in a walking route')
        shapes.append(shape)
    assert len(shapes)==len(plan['props']), 'Saved furnishing count'
    linked = {Path(bpy.path.abspath(lib.filepath)).stem for lib in bpy.data.libraries
              if Path(bpy.path.abspath(lib.filepath)).is_file()}
    assert set(MODEL_NAMES) <= linked, 'Missing linked facility model'
    print(f'Verified {count} entrances and {len(shapes)} furnishings in the saved town')


if __name__ == '__main__':
    plan = Path(sys.argv[sys.argv.index('--')+1])
    verify(json.loads(plan.read_text()))
