from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = Path.cwd()
sys.path.insert(0, str(ROOT / 'Tools/World'))
from render_country_redesign import Assets
from country_redesign_assets import CONCEPT, bounds, load
from materials import surface
from preview import setup

REVIEW = ROOT / 'Artifacts/CountryFacilityScale-20261117'
SHEETS = (
    ('01_storeys', ('Fantasy_House', 'Tavern', 'Clinic')),
    ('02_schools', ('WarriorAcademy_Lv5', 'ArcaneAcademy_Lv5', 'SpiritAcademy_Lv5')),
    ('03_landmarks', ('Church', 'Observatory', 'Guardhouse')),
    ('04_public', ('Library', 'Museum', 'Arena')),
    ('05_venues', ('Ballroom', 'Casino', 'MasqueradeSalon')),
    ('06_small_scale', ('Blacksmith', 'CrimsonRowhouse', 'VeiledCourtyard')),
)
WIDTH, HEIGHT, SPAN = 2532, 1170, 158
COLUMNS = (438, 1266, 2094)
BASELINES = (515, 1038)
ANGLE = Vector((.38, -1, .23)).normalized()
ROTATION = (-ANGLE).to_track_quat('-Z', 'Y')
RIGHT, UP = (ROTATION @ Vector(a) for a in ((1, 0, 0), (0, 1, 0)))
UNPROJECT = Matrix((RIGHT.xy, UP.xy)).inverted()


def at_pixels(x, y):
    return UNPROJECT @ (Vector((x - WIDTH / 2, HEIGHT / 2 - y)) * (SPAN / WIDTH))


def create_person(collection, position, tint):
    x, y = position
    for coords, dimensions in (
        ((0, 0, 1.17), (.24, .16, .50)),
        ((-.12, 0, .43), (.10, .12, .81)), ((.10, 0, .43), (.10, .12, .81)),
        ((-.21, 0, 1.12), (.07, .09, .44)), ((.21, 0, 1.12), (.07, .09, .44)),
    ):
        a, b, z = coords
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x+a, y+b, z))
        obj = bpy.context.object
        obj.dimensions = dimensions
        obj.data.materials.append(tint)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.14, location=(x, y, 1.56))
    bpy.context.object.data.materials.append(tint)


def place(source, x, y, scene, scale, person_tint):
    lo, hi = bounds(source)
    corners = [Vector((a*scale,b*scale,lo[2]*scale)) for a in (lo[0],hi[0]) for b in (lo[1],hi[1])]
    u = [RIGHT.dot(p) for p in corners]
    v = [UP.dot(p) for p in corners]
    origin = at_pixels(x,y) - UNPROJECT @ Vector(((min(u)+max(u))/2,min(v)))
    instance = bpy.data.objects.new(source.name, None)
    scene.collection.objects.link(instance)
    instance.instance_type = 'COLLECTION'
    instance.instance_collection = source
    instance.location = (*origin, -lo[2]*scale)
    instance.scale = (scale,) * 3
    if source.name.startswith('Fantasy_House') or 'CityV2 Fantasy_House' in source.name:
        px, py = -1.8*scale, -4.0*scale
    else:
        entry = next((o for o in source.all_objects if o.type == 'EMPTY' and o.name.split('.')[0].endswith('_Entrance')),None)
        px = (entry.location.x if entry else (lo[0]+hi[0])*.5) + 1.55
        py = lo[1] - .52
    create_person(instance.users_collection[0], (origin.x+px,origin.y+py), person_tint)


def render_sheet(stem, names):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Review world')
    camera = setup(scene)
    camera.location = ANGLE * 460
    camera.data.clip_end = 2300
    camera.rotation_euler = ROTATION.to_euler()
    camera.data.ortho_scale = SPAN
    scene.cycles.samples = 10
    scene.cycles.adaptive_threshold = .08
    tint = surface('1.70 m person', color='186691', roughness=.88)
    assets = Assets(CONCEPT)
    for name, x in zip(names, COLUMNS):
        if name == 'Fantasy_House':
            house = assets.collection(name, 'Terracotta')
            bpy.context.view_layer.update()
            for y in BASELINES:
                place(house, x, y, scene, .98, tint)
            continue
        for source_dir, y in zip((CONCEPT, REVIEW/'models'), BASELINES):
            source = load(name, link=True, concept_dir=source_dir)
            bpy.context.view_layer.update()
            place(source, x, y, scene, 1, tint)
    scene.render.filepath = str(REVIEW / f'{stem}_raw.png')
    bpy.ops.render.render(write_still=True)


requested = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for stem, names in SHEETS:
    if not requested or stem in requested:
        render_sheet(stem, names)
