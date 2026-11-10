"""Purpose-designed town architecture, composed from reusable surface parts."""
import math

from mathutils import Matrix, Vector


PI = math.pi
NAMES = {'Church', 'Chapel', 'Blacksmith', 'Library', 'Museum', 'Observatory'}


def identity(k, text):
    k.COLLECTION['role_identity'] = text
    k.COLLECTION['design_revision'] = 'painted-fantasy-2026-10-22'


def rotated(k, p, angle, draw):
    before = set(k.COLLECTION.objects)
    draw()
    transform = Matrix.Translation(Vector(p)) @ Matrix.Rotation(angle, 4, 'Z')
    for obj in set(k.COLLECTION.objects) - before:
        obj.matrix_basis = transform @ obj.matrix_basis


def pitched(k, name, p, w, d, rise, cover='Roof_Slate', wall='Limestone', across=False):
    rotated(k, p, PI / 2 if across else 0,
            lambda: k.roof(name, (0, 0, 0), w, d, rise, cover, end_material=wall))


def mass(k, name, p, w, d, h, wall='Limestone', cover=None, rise=2, across=False):
    x, y, z = p
    k.box(name + ' foundation', (x, y, z + .2), (w + .28, d + .28, .4), 'Fieldstone')
    k.box(name + ' walls', (x, y, z + h / 2), (w, d, h), wall)
    k.box(name + ' cornice', (x, y, z + h - .13), (w + .18, d + .18, .26), wall)
    if cover:
        pitched(k, name + ' roof', (x, y, z + h), d + .6 if across else w + .6,
                w + .6 if across else d + .6, rise, cover, wall, across)


def facade_outline(k, name, coordinates, front_y, back_y, mat='Limestone'):
    n = len(coordinates)
    vertices = [(x, yy, z) for yy in (front_y, back_y) for x, z in coordinates]
    k.mesh(name, vertices, [tuple(range(n)), tuple(reversed(range(n, n * 2)))] +
           [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)], mat)


def arch(k, p, w, h, depth=.36, stone='Limestone', side='front', trim=.2):
    x, y, z = p
    tangent = Vector((1, 0, 0)) if side == 'front' else Vector((0, 1, 0))
    normal = Vector((0, -1, 0)) if side == 'front' else Vector((1, 0, 0))
    origin = Vector(p)
    spring = h - w / 2
    for direction in (-1, 1):
        co = origin + tangent * direction * (w / 2 + trim / 2) + Vector((0, 0, spring / 2))
        size = (trim, depth, spring) if side == 'front' else (depth, trim, spring)
        k.box('Arch supporting jamb', co, size, stone)
    vertices = []
    for dep, radius in ((-depth / 2, w / 2), (-depth / 2, w / 2 + trim),
                        (depth / 2, w / 2), (depth / 2, w / 2 + trim)):
        for i in range(17):
            angle = i * PI / 16
            co = origin + tangent * (math.cos(angle) * radius) + normal * dep
            co.z += spring + math.sin(angle) * radius
            vertices.append(tuple(co))
    faces = []
    for i in range(16):
        faces.extend([(i, i + 1, 18 + i, 17 + i),
                      (34 + i, 51 + i, 52 + i, 35 + i),
                      (i, 34 + i, 35 + i, i + 1),
                      (17 + i, 18 + i, 52 + i, 51 + i)])
    faces.extend([(0, 17, 51, 34), (16, 50, 67, 33)])
    k.mesh('Load-bearing arch voussoirs', vertices, faces, stone)


def entrance(k, p, w=2, h=3, stone='Limestone'):
    x, y, z = p
    k.panel('Recessed entrance shadow', (x, y + .035, z), (w + .06, h + .03), 'Iron', arched=True)
    k.panel('Heavy oak entrance', (x, y, z), (w, h), 'Door_Oak', arched=True)
    arch(k, (x, y - .12, z), w, h, .36, stone, trim=.23)
    k.box('Entrance threshold', (x, y - .18, z + .06), (w + .55, .7, .12), stone)


def steps(k, p, w, count=3, rise=.16, tread=.38):
    x, y, z = p
    for i in range(count):
        k.box('Approach stair', (x, y + i * tread, z + (i + 1) * rise / 2),
              (w, tread + .015, (i + 1) * rise), 'Limestone')


def lean_roof(k, name, x0, x1, y0, y1, z0, z1, cover='Roof_Slate', wall='Limestone'):
    verts = [(x0, y0, z0), (x1, y0, z1), (x1, y1, z1), (x0, y1, z0),
             (x0, y0, min(z0, z1)), (x1, y0, min(z0, z1)),
             (x0, y1, min(z0, z1)), (x1, y1, min(z0, z1))]
    k.mesh(name, verts[:4], [(0, 1, 2, 3)], cover)
    if z0 > z1:
        k.mesh(name + ' gable ends', verts, [(4, 1, 0), (6, 3, 2)], wall)
    else:
        k.mesh(name + ' gable ends', verts, [(0, 1, 5), (3, 7, 2)], wall)
    for yy in (y0, y1):
        k.beam(name + ' verge', (x0, yy, z0), (x1, yy, z1), .14, 'DarkTimber')
    for xx, zz in ((x0, z0), (x1, z1)):
        k.beam(name + ' eave', (xx, y0, zz), (xx, y1, zz), .14, 'DarkTimber')


def circular_band(k, name, p, r, tube=.1, mat='Brass', vertical=False):
    rings = [(r + math.cos(i * PI / 4) * tube, math.sin(i * PI / 4) * tube) for i in range(9)]
    obj = k.lathe(name, (0, 0, 0), rings, mat, 24, caps=False)
    if vertical:
        obj.rotation_euler.x = PI / 2
    obj.location = p
    return obj


def frieze(k, name, p, w, h, mat, side='front'):
    obj = k.panel(name, p, (w, h), mat, side)
    for loop in obj.data.uv_layers.active.data:
        loop.uv.x *= w / 3
    return obj


def frieze_ring(k, name, p, r, h, mat, segments=24):
    obj = k.lathe(name, p, [(r, 0), (r, h)], mat, segments, caps=False)
    for loop in obj.data.uv_layers.active.data:
        loop.uv.y *= 2 / h
    return obj


def rose(k, p, r=1.25):
    x, y, z = p
    verts = [(x, y, z)] + [(x + math.cos(i * 2 * PI / 24) * r, y,
                           z + math.sin(i * 2 * PI / 24) * r) for i in range(24)]
    uv = [(.5, .5)] + [(.5 + math.cos(i * 2 * PI / 24) * .5,
                        .5 + math.sin(i * 2 * PI / 24) * .5) for i in range(24)]
    k.mesh('Rose stained glass', verts, [(0, i + 1, (i + 1) % 24 + 1) for i in range(24)], 'Window_Sacred', uv)
    circular_band(k, 'Rose carved stone surround', (x, y - .06, z), r + .11, .13, 'Limestone', True)
    circular_band(k, 'Rose central tracery', (x, y - .10, z), r * .3, .045, 'Brass', True)
    for i in range(12):
        a = i * PI / 6
        k.beam('Rose radial tracery', (x + math.cos(a) * r * .3, y - .12, z + math.sin(a) * r * .3),
               (x + math.cos(a) * r, y - .12, z + math.sin(a) * r), .045, 'DarkTimber')


def cross(k, p, size=2):
    x, y, z = p
    k.beam('Church cross upright', p, (x, y, z + size), .12 * size, 'Brass')
    k.beam('Church cross arms', (x - size * .34, y, z + size * .67),
           (x + size * .34, y, z + size * .67), .1 * size, 'Brass')


def belfry(k, p, w, shaft, bell_height, spire):
    x, y, z = p
    mass(k, 'Bell tower', p, w, w, shaft)
    for xx in (-w / 2 + .26, w / 2 - .26):
        for yy in (-w / 2 + .26, w / 2 - .26):
            k.box('Belfry corner pier', (x + xx, y + yy, z + shaft + bell_height / 2),
                  (.52, .52, bell_height), 'Limestone')
    opening = w - 1.04
    for yy in (-w / 2, w / 2):
        arch(k, (x, y + yy, z + shaft), opening, bell_height - .24, .28, trim=.17)
    for xx in (-w / 2, w / 2):
        arch(k, (x + xx, y, z + shaft), opening, bell_height - .24, .28, side='right', trim=.17)
    roof_z = z + shaft + bell_height
    k.box('Belfry stone crown', (x, y, roof_z), (w + .3, w + .3, .32), 'Limestone')
    cap = k.lathe('Belfry spire', (0, 0, 0), [(w * .79, 0), (w * .45, .7), (.05, spire)], 'Roof_Slate', 4, smooth=False)
    cap.rotation_euler.z = PI / 4
    cap.location = (x, y, roof_z + .17)
    k.beam('Bell suspension beam', (x - w * .35, y, z + shaft + bell_height - .4),
           (x + w * .35, y, z + shaft + bell_height - .4), .2)
    k.lathe('Hanging bronze bell', (x, y, z + shaft + .6),
            [(.57, 0), (.62, .13), (.43, .27), (.29, .84), (.12, 1.03)], 'Brass', 16)
    k.beam('Bell suspension hanger', (x, y, z + shaft + 1.62),
           (x, y, z + shaft + bell_height - .4), .1, 'Iron')
    k.beam('Bell clapper', (x, y, z + shaft + .52), (x, y, z + shaft + 1.22), .07, 'Iron')
    for zz in (2.2, 6.0):
        k.window((x, y - w / 2 - .035, z + zz), .65, 1.7, 'Window_Sacred', arched=True)
    frieze(k, 'Bell tower sacred stone frieze', (x, y - w / 2 - .012, z + shaft - .8), w, .55, 'Sacred_Trim')
    cross(k, (x, y, roof_z + .17 + spire), 1.35)


def church(k, chapel=False):
    if chapel:
        mass(k, 'Chapel nave', (0, 0, 0), 5, 9, 5.3, cover='Roof_Slate', rise=2.8)
        k.lathe('Chapel apse', (0, 4.1, 0), [(2.1, 0), (2.1, 4.4)], 'Limestone', 12)
        k.lathe('Apse roof', (0, 4.1, 4.4), [(2.4, 0), (.05, 2.4)], 'Roof_Slate', 12)
        entrance(k, (0, -4.55, .12), 1.65, 2.75)
        rose(k, (0, -4.58, 4.5), .74)
        for side, xx in (('left', -2.55), ('right', 2.55)):
            for yy in (-2.4, .2, 2.8):
                k.window((xx, yy, 1.65), .72, 2.1, 'Window_Sacred', side, True)
        for xx, side in ((-2.51, 'left'), (2.51, 'right')):
            frieze(k, 'Chapel carved lily cornice', (xx, 0, 4.72), 8.7, .42, 'Sacred_Trim', side)
        k.box('Bell gable base', (0, -3.6, 8.05), (1.7, .8, .25), 'Limestone')
        arch(k, (0, -3.6, 8.18), .85, 1.3, .55, trim=.22)
        pitched(k, 'Bell gable cap', (0, -3.6, 9.65), 1.9, 1, .75)
        k.lathe('Chapel bell', (0, -3.6, 8.45), [(.28, 0), (.31, .06), (.17, .4), (.06, .5)], 'Brass', 12)
        k.beam('Chapel bell hanger', (0, -3.6, 8.94), (0, -3.6, 9.5), .065, 'Iron')
        cross(k, (0, -3.6, 10.4), 1.2)
        for x in (-3.2, 3.2):
            k.bench((x, -2.0, 0), 1.8)
        identity(k, 'Small apsidal chapel; pierced bell gable, bronze bell, rose glass and arched portal')
        return
    mass(k, 'Basilica high nave', (0, 2.25, 0), 5.8, 17, 9.4, cover='Roof_Slate', rise=3.5)
    for direction in (-1, 1):
        xx = direction * 4.3
        mass(k, 'Low side aisle', (xx, 3.5, 0), 2.8, 14.5, 4.5)
        if direction > 0:
            lean_roof(k, 'Aisle lean roof', 2.8, 5.95, -3.9, 10.9, 6.05, 4.5)
        else:
            lean_roof(k, 'Aisle lean roof', -5.95, -2.8, -3.9, 10.9, 4.5, 6.05)
        for yy in (-2.0, 1.5, 5, 8.5):
            side = 'right' if direction > 0 else 'left'
            k.window((direction * 5.74, yy, 1.25), 1.1, 2.65, 'Window_Sacred', side, True)
            k.window((direction * 2.96, yy, 6.65), .88, 2.1, 'Window_Sacred', side, True)
        frieze(k, 'Nave sacred cornice inlay', (direction * 2.91, 2.25, 8.96), 16.8, .36,
               'Sacred_Trim', 'right' if direction > 0 else 'left')
        for yy in (-3.8, -.25, 3.25, 6.75, 10.5):
            a = direction * 5.5; b = direction * 6.35
            verts = [(a, yy - .22, 0), (b, yy - .22, 0), (b, yy + .22, 0), (a, yy + .22, 0),
                     (a, yy - .22, 4.8), (direction * 5.95, yy - .22, 3.7),
                     (direction * 5.95, yy + .22, 3.7), (a, yy + .22, 4.8)]
            k.mesh('Raking aisle buttress', verts, [(0, 3, 2, 1), (0, 1, 5, 4), (1, 2, 6, 5),
                   (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)], 'Fieldstone')
    k.lathe('Polygonal sanctuary apse', (0, 10.1, 0), [(2.85, 0), (2.85, 7.2)], 'Limestone', 12)
    k.lathe('Sanctuary conical roof', (0, 10.1, 7.2), [(3.15, 0), (.06, 3.1)], 'Roof_Slate', 12)
    belfry(k, (-4.8, -4.6, 0), 3.5, 10.7, 2.75, 4)
    belfry(k, (4.8, -4.6, 0), 3.5, 12.7, 2.75, 4.7)
    entrance(k, (0, -6.3, .5), 2.55, 3.4)
    for xx in (-2.96, 2.96):
        k.box('Joined facade masonry pier', (xx, -6.1, 4.7), (.42, .65, 9.4), 'Limestone')
    steps(k, (0, -7.65, 0), 4.8, 3)
    rose(k, (0, -6.34, 6.8), 1.35)
    cross(k, (0, -6.68, 10), 2.4)
    for x in (-2.1, 2.1):
        k.box('Portal niche base', (x, -6.52, 1.05), (.62, .56, 1.1), 'Limestone')
        statue(k, (x, -6.55, 1.6), .55)
    for x in (-7.1, 7.1):
        k.lamp((x, -5.8, 0))
        k.bench((x, -2.3, 0), 2.1)
    identity(k, 'Twin open belfries with bells; high basilica nave, low aisles, clerestory, buttresses, apse and rose window')


def table(k, p, w=2.1, d=.85):
    x, y, z = p
    k.box('Workshop bench top', (x, y, z + .95), (w, d, .16), 'Oak')
    for xx in (-w * .4, w * .4):
        for yy in (-d * .35, d * .35):
            k.box('Workbench trestle leg', (x + xx, y + yy, z + .45), (.13, .13, .9), 'DarkTimber')
    k.box('Workbench stretcher', (x, y, z + .3), (w * .88, .12, .12), 'DarkTimber')


def hammer(k, p, angle=0):
    x, y, z = p
    k.beam('Hammer ash handle', p, (x + .48 * math.cos(angle), y + .48 * math.sin(angle), z), .045, 'Oak')
    k.box('Hammer steel head', (x + .46 * math.cos(angle), y + .46 * math.sin(angle), z + .025), (.18, .14, .13), 'Iron')


def open_tub(k, p, r=.5, h=.8, water=True):
    x, y, z = p
    k.lathe('Open stave cooling tub', p, [(r * .8, 0), (r, h), (r - .07, h), (r * .8 - .07, .1)], 'Oak', 14)
    for zz in (.12, h - .15):
        circular_band(k, 'Tub iron hoop', (x, y, z + zz), r * (.8 + .2 * zz / h), .035, 'Iron')
    if water:
        k.lathe('Cooling water', (x, y, z + h * .73), [(r * .88, 0), (r * .88, .015)], 'Water', 16)


def blacksmith(k):
    k.box('Sooty work yard', (0, -.6, .045), (13, 10.4, .09), 'Cobble')
    k.box('Workshop rear wall', (0, 3.4, 2.4), (11.3, .42, 4.8), 'Fieldstone')
    k.box('Workshop west wall', (-5.45, 0, 2.05), (.4, 6.6, 4.1), 'Fieldstone')
    mass(k, 'Coal and ore store', (-4, 2, 0), 2.6, 2.4, 3.8, 'Brick')
    entrance(k, (-4, .76, .1), 1.3, 2.3, 'Brick')
    for x in (-5.4, -.7, 5.4):
        k.box('Open forge roof post', (x, -3.25, 1.9), (.25, .25, 3.8), 'DarkTimber')
    k.beam('Forge frontage tie beam', (-5.55, -3.25, 3.7), (5.55, -3.25, 3.7), .25)
    for x in (-5.4, -.7, 5.4):
        for direction in (-1, 1):
            if x + direction * .7 < -5.6 or x + direction * .7 > 5.6:
                continue
            k.beam('Forge roof knee brace', (x, -3.25, 2.95), (x + direction * .7, -3.25, 3.7), .14)
    def roof_z(y):
        return 3.72 + (y + 3.55) * .16
    for x0, x1, y0, y1 in ((-5.8, 2.2, -3.7, 3.8), (4.5, 5.8, -3.7, 3.8),
                            (2.2, 4.5, -3.7, .45), (2.2, 4.5, 2.9, 3.8)):
        k.mesh('Broad sloping forge roof', [(x0, y0, roof_z(y0)), (x1, y0, roof_z(y0)),
               (x1, y1, roof_z(y1)), (x0, y1, roof_z(y1))], [(0, 1, 2, 3)], 'Roof_Slate')
    k.beam('Forge roof front fascia', (-5.8, -3.7, roof_z(-3.7)), (5.8, -3.7, roof_z(-3.7)), .18)
    for xx in (-5.8, 5.8):
        k.beam('Forge raking fascia', (xx, -3.7, roof_z(-3.7)), (xx, 3.8, roof_z(3.8)), .18)
    x, y = 3.35, 1.65
    k.box('Furnace hearth slab', (x, y, .4), (2.8, 2.5, .8), 'Brick')
    for xx in (-1.13, 1.13):
        k.box('Furnace firebox side', (x + xx, y, 1.55), (.5, 2.5, 2.3), 'Brick')
    k.box('Furnace fireback', (x, y + 1, 1.55), (2.1, .4, 2.3), 'Brick')
    arch(k, (x, y - 1.05, .8), 1.65, 1.65, .55, 'Brick', trim=.26)
    k.box('Charcoal bed', (x, y - .15, .86), (1.65, 1.6, .12), 'Iron')
    for i in range(5):
        k.ball('Glowing charcoal', (x - .55 + i * .26, y - .65, .97), .12, 'Spice_Red', segments=6, rings=3)
    k.mesh('Tapered masonry smoke hood', [(x - 1.4, y - 1.25, 2.65), (x + 1.4, y - 1.25, 2.65),
           (x + 1.4, y + 1.25, 2.65), (x - 1.4, y + 1.25, 2.65),
           (x - 1.02, y - 1.02, 4), (x + 1.02, y - 1.02, 4),
           (x + 1.02, y + 1.02, 4), (x - 1.02, y + 1.02, 4)],
           [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], 'Brick')
    k.box('Square industrial chimney', (x, y, 6.7), (2.04, 2.04, 5.4), 'Brick')
    for zz in (4.1, 8.5, 9.35):
        k.box('Chimney stone belt', (x, y, zz), (2.3, 2.3, .2), 'Fieldstone')
    k.box('Chimney dark flue', (x, y, 9.48), (1.7, 1.7, .035), 'Iron')
    k.anvil((1.9, -2.3, .09), 1.25)
    table(k, (-2.9, -4.4, .09), 2.9, 1.05)
    for i in range(3):
        hammer(k, (-3.8 + i * .65, -4.5, 1.15), .3 + i * .8)
    k.box('Tool hanging backboard', (-2, 3.12, 2.1), (3.3, .14, 1.45), 'Oak')
    for i in range(5):
        xx = -3.25 + i * .6
        k.beam('Hanging forge tongs', (xx, 2.98, 1.8), (xx + .14, 2.98, 2.65), .04, 'Iron')
        k.beam('Tongs second arm', (xx + .23, 2.98, 1.8), (xx + .09, 2.98, 2.65), .04, 'Iron')
    open_tub(k, (4.5, -3.8, .09), .6, .95)
    k.weapon_rack((-4.1, -2.5, .09), swords=True)
    for i in range(5):
        k.box('Iron stock bar', (-.1 + i * .17, 1.75, .28 + i * .05), (.11, 2.4, .11), 'Iron')
    k.basket((-.45, 2.4, .09), .48)
    for i in range(7):
        k.ball('Lump ore', (-.7 + (i % 3) * .22, 2.2 + (i // 3) * .2, .64), .15, 'Iron', segments=5, rings=3)
    k.beam('Bellows nozzle', (.8, 1.45, 1.25), (2.1, 1.45, 1.05), .13, 'Iron')
    k.mesh('Leather forge bellows', [(-.2, 1.0, .9), (.9, 1.25, 1.1), (.9, 1.65, 1.1), (-.2, 2, .9),
           (-.2, 1, 1.6), (.9, 1.25, 1.25), (.9, 1.65, 1.25), (-.2, 2, 1.6)],
           [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], 'Canvas_Red')
    k.beam('Bellows lever', (-.25, 1.5, 1.7), (-1.3, 1.5, 1.9), .11)
    wheel = k.lathe('Grinding stone', (0, 0, 0), [(.57, 0), (.57, .23)], 'Limestone', 20)
    wheel.rotation_euler.y = PI / 2; wheel.location = (4.6, -1.45, 1.05)
    for xx in (4.45, 4.98):
        k.box('Grindstone bearing stand', (xx, -1.45, .55), (.13, .65, 1.1), 'DarkTimber')
    k.sign('Smith', (-.7, -3.5, 2.35), 1.1)
    identity(k, 'Open-front forge workshop; sloping industrial roof cut around masonry chimney; working furnace, anvil, bellows, tongs, stock, grindstone and cooling tub')


def statue(k, p, scale=1):
    x, y, z = p
    k.lathe('Sculpture draped body', p, [(.4 * scale, 0), (.27 * scale, .7 * scale),
            (.34 * scale, 1.08 * scale), (.16 * scale, 1.25 * scale)], 'Limestone', 12)
    k.ball('Sculpted head', (x, y, z + 1.52 * scale), .23 * scale, 'Limestone',
           scale=(.85, .88, 1.13), segments=12, rings=8)
    for direction in (-1, 1):
        k.beam('Sculpted arm', (x + direction * .3 * scale, y, z + 1.03 * scale),
               (x + direction * .4 * scale, y - .13 * scale, z + .56 * scale), .14 * scale, 'Limestone')


def book(k, p, s=1, upright=False):
    x, y, z = p
    if upright:
        k.box('Bound book pages', (x, y, z + .35 * s), (.12 * s, .45 * s, .7 * s), 'Paper')
        for xx in (-.08, .08):
            k.box('Bound book leather cover', (x + xx * s, y, z + .35 * s), (.035 * s, .5 * s, .75 * s), 'Canvas_Red')
    else:
        k.box('Bound book pages', (x, y, z + .07 * s), (.65 * s, .44 * s, .1 * s), 'Paper')
        for zz in (.005, .135):
            k.box('Bound book cover', (x, y, z + zz * s), (.7 * s, .49 * s, .03 * s), 'Canvas_Green')


def library(k):
    mass(k, 'Great reading hall', (0, 1, 0), 8.2, 12, 8.2, cover='Roof_Clay', rise=3.7)
    mass(k, 'Archive wing', (5.5, 4.5, 0), 5, 5, 5.2, cover='Roof_Clay', rise=2.1, across=True)
    mass(k, 'Square library stair tower', (-5.4, 2.5, 0), 3.4, 5, 11.8)
    k.roof('Stair tower hipped cap', (-5.4, 2.5, 11.8), 4, 5.6, 1.7, 'Roof_Slate', 'hip')
    for zz in (2, 5.3, 8.6):
        k.window((-5.4, -.045, zz), .75, 2, 'Window_Amber', arched=True)
        k.window((-7.15, 2.5, zz), .7, 2, 'Window_Amber', 'left', True)
    for xx in (-2.4, 0, 2.4):
        k.window((xx, -5.06, 4.2), 1.38, 3.1, 'Window_Amber', arched=True)
    for yy in (-3.4, -.2, 3, 6.2):
        if yy < 2:
            k.window((4.17, yy, 3.45), 1.25, 3.8, 'Window_Amber', 'right', True)
        if yy < 0:
            k.window((-4.17, yy, 3.45), 1.25, 3.8, 'Window_Amber', 'left', True)
    for direction in (-1, 1):
        for xx in (3.6,):
            k.box('Reading hall corner pier', (direction * xx, -5.1, 4.1), (.32, .35, 8.2), 'Limestone')
    entrance(k, (0, -5.08, .48), 2.1, 2.7)
    steps(k, (0, -6.45, 0), 4.2, 3)
    outline = [(-4.2, 8), (4.2, 8), (4.2, 8.6), (3.3, 8.6), (3.3, 9.5),
               (2.4, 9.5), (2.4, 10.4), (1.5, 10.4), (1.5, 11.3), (.6, 11.3),
               (.6, 12.1), (-.6, 12.1), (-.6, 11.3), (-1.5, 11.3), (-1.5, 10.4),
               (-2.4, 10.4), (-2.4, 9.5), (-3.3, 9.5), (-3.3, 8.6), (-4.2, 8.6)]
    facade_outline(k, 'Solid stepped collegiate gable', outline, -5.5, -5.15)
    k.sign('Book', (0, -5.7, 8.55), 2.05)
    k.window((8.06, 4.4, 1.6), 1, 2.6, 'Window_Amber', 'right', True)
    for xx in (-2.9, 2.9):
        arch(k, (xx, -5.75, .15), 1.45, 2.5, .35, trim=.18)
    k.box('Library entrance tie beam', (0, -5.9, 2.93), (7.8, 1.7, .24), 'Limestone')
    pitched(k, 'Sheltered library entrance', (0, -5.9, 3.05), 8, 2, .9, 'Roof_Slate')
    for xx in (-3.65, 3.65):
        k.box('Entrance gallery outer support', (xx, -6.5, 1.525), (.3, .3, 3.05), 'Limestone')
    table(k, (-2.9, -5.65, .15), 1.25, .6)
    book(k, (-3, -5.65, 1.18), .85)
    book(k, (-2.65, -5.68, 1.31), .6)
    k.box('Library notice board', (2.85, -5.55, 1.6), (1.15, .12, 1.15), 'Oak')
    k.panel('Reading notices', (2.85, -5.63, 1.15), (.9, .86), 'Parchment')
    k.bench((6.8, -.1, 0), 2.2)
    for xx in (-8, 7.8):
        k.lamp((xx, -4.9, 0))
    identity(k, 'Collegiate stepped-gable reading hall; tall grouped reading windows, stair tower, lower archive wing and sheltered book desk')


def column(k, p, h=4.2, r=.3):
    k.lathe('Gallery stone column', p, [(r * 1.55, 0), (r * 1.55, .2), (r, .32),
            (r * .85, h - .4), (r * 1.6, h - .25), (r * 1.6, h)], 'Limestone', 16)


def museum(k):
    k.box('Museum raised podium', (0, .1, .3), (19, 13.5, .6), 'Limestone')
    for xx in (-6.6, 6.6):
        mass(k, 'Long exhibition gallery', (xx, 2, .6), 5, 9, 5.3, cover='Roof_Slate', rise=1.8)
        for xslot in (-1.25, 1.25):
            k.window((xx + xslot, -2.55, 2), .92, 2.8, 'Window_Blue', arched=True)
        k.window((xx + (2.55 if xx > 0 else -2.55), 2, 2), 1.2, 2.8, 'Window_Blue', 'right' if xx > 0 else 'left', True)
    k.lathe('Central museum rotunda', (0, 2.6, .6), [(4, 0), (4, 6.8), (4.2, 6.8), (4.2, 7.1)], 'Limestone', 24)
    k.lathe('Museum copper dome', (0, 2.6, 7.7), [(4.3, 0), (4.15, 1.2), (3.5, 2.6),
            (2.3, 3.7), (.5, 4.4), (.18, 4.65)], 'Copper', 24)
    entrance(k, (0, -1.45, .6), 2.6, 4)
    for xx in (-4.4, -2.2, 0, 2.2, 4.4):
        column(k, (xx, -5.45, .6), 4.3, .3)
    for xx in (-4.4, 4.4):
        column(k, (xx, -2, .6), 4.3, .3)
    k.box('Museum portico entablature', (0, -3.75, 5.05), (10.4, 4.3, .4), 'Limestone')
    pitched(k, 'Museum classical pediment', (0, -3.75, 5.27), 10.7, 4.6, 1.8, 'Roof_Slate')
    k.sign('Museum', (0, -6.1, 5.45), 1.4)
    steps(k, (0, -8.05, 0), 11, 4, .15, .4)
    for xx in (-7, 7):
        k.box('Outdoor sculpture pedestal', (xx, -4.65, 1.1), (1.15, 1.15, 1), 'Limestone')
        k.box('Sculpture plinth cap', (xx, -4.65, 1.65), (1.35, 1.35, .14), 'Limestone')
        statue(k, (xx, -4.65, 1.72), 1)
        k.bench((xx, -6.45, .6), 2.3)
    identity(k, 'Domed civic rotunda with long exhibition wings, full structural colonnaded portico, broad podium stairs and outdoor sculpture exhibits')


def armillary(k, p, r=.8):
    x, y, z = p
    k.lathe('Astronomical instrument pedestal', p, [(.4, 0), (.4, .16), (.17, .3), (.17, 1.0)], 'Limestone', 14)
    center = (x, y, z + 1 + r)
    for normal in ((0, 0, 1), (0, 1, .4), (1, .3, 0)):
        obj = circular_band(k, 'Armillary bronze ring', (0, 0, 0), r, .035, 'Brass')
        obj.rotation_euler = Vector(normal).to_track_quat('Z', 'Y').to_euler()
        obj.location = center
    k.ball('Armillary central globe', center, r * .27, 'Brass', segments=12, rings=8)


def observatory(k):
    k.lathe('Observatory stepped base', (0, 1, 0), [(4.6, 0), (4.6, .3), (4.25, .3), (4.25, .6)], 'Limestone', 24)
    k.lathe('Octagonal observation tower', (0, 1, .6), [(4, 0), (4, 7.2), (4.2, 7.2), (4.2, 7.5)], 'Limestone', 8, smooth=False)
    for zz in (2.0, 5.1):
        for angle in (-PI / 4, PI / 4):
            rotated(k, (0, 1, 0), angle,
                    lambda zz=zz: k.window((0, -4.03, zz), 1, 1.8, 'Window_Blue', arched=True))
        k.window((4.04, 1, zz), 1, 1.8, 'Window_Blue', 'right', True)
    frieze_ring(k, 'Observatory celestial cornice inlay', (0, 1, 7.3), 4.02, .45, 'Arcane_Trim', 8)
    entrance(k, (0, -3.07, .6), 1.7, 2.8)
    # The dome is built with an actual observing slit, rather than a tube through its skin.
    profile = [(4.4 * math.cos(i / 16 * math.acos(.65 / 4.4)),
                4.5 * math.sin(i / 16 * math.acos(.65 / 4.4))) for i in range(17)]
    verts = []
    count = 64
    gap = .21
    for radius, height in profile:
        for i in range(count + 1):
            a = -PI / 2 + gap + i / count * (2 * PI - gap * 2)
            verts.append((math.cos(a) * radius, 1 + math.sin(a) * radius, 8.1 + height))
    dome = k.mesh('Copper dome with observing slot', verts,
           [(j * (count + 1) + i, j * (count + 1) + i + 1,
             (j + 1) * (count + 1) + i + 1, (j + 1) * (count + 1) + i)
            for j in range(len(profile) - 1) for i in range(count)], 'Copper')
    for face in dome.data.polygons:
        face.use_smooth = True
    for direction in (-1, 1):
        for i in range(len(profile) - 1):
            r0, h0 = profile[i]; r1, h1 = profile[i + 1]
            a = -PI / 2 + direction * gap
            k.beam('Dome slit rim', (math.cos(a) * r0, 1 + math.sin(a) * r0, 8.1 + h0),
                   (math.cos(a) * r1, 1 + math.sin(a) * r1, 8.1 + h1), .13, 'Brass')
    k.lathe('Dome top cap', (0, 1, 8.1 + profile[-1][1] - .015), [(.7, 0), (.06, .32)], 'Copper', 20)
    mass(k, 'Low calculation wing', (-5.0, 2.5, 0), 3.2, 7, 3.8, cover='Roof_Slate', rise=1.6)
    for yy in (0, 3.4):
        k.window((-6.65, yy, 1.6), .8, 1.6, 'Window_Amber', 'left', True)
    steps(k, (0, -4.4, 0), 3.5, 4, .15)
    armillary(k, (5.7, -2.2, 0), .95)
    k.sign('Star', (0, -3.2, 4.6), 1.6)
    identity(k, 'Octagonal observation tower, copper dome with open observing slit and rim, low calculation wing and courtyard armillary')


def flag(k, p, cloth='Canvas_Red', height=2.8):
    x, y, z = p
    k.beam('Masonry-mounted flagstaff', p, (x, y, z + height), .085, 'DarkTimber')
    k.panel('School heraldic banner', (x + .46, y, z + height - 1.75), (.85, 1.5), cloth)


def target(k, p):
    x, y, z = p
    for direction in (-1, 1):
        k.beam('Archery target trestle', (x + direction * .48, y + .3, z),
               (x + direction * .2, y, z + 1.9), .095)
    obj = k.lathe('Straw archery target', (0, 0, 0), [(.64, 0), (.64, .14)], 'Thatch', 18)
    obj.rotation_euler.x = PI / 2; obj.location = (x, y - .03, z + 1.43)
    for r, mat in ((.42, 'Canvas_Red'), (.22, 'Linen'), (.07, 'Canvas_Red')):
        obj = k.lathe('Painted target ring', (0, 0, 0), [(r, 0), (r, .012)], mat, 18)
        obj.rotation_euler.x = PI / 2; obj.location = (x, y - .19, z + 1.43)


def warrior_school(k, level):
    k.box('Fortified training precinct', (0, .4, .04), (20.4, 20, .08), 'Cobble')
    if level == 5:
        for xx in (-4.9, 4.9):
            mass(k, 'Keep side training hall', (xx, 5.7, .08), 3.8, 5.4, 5.3, cover='Roof_Clay', rise=2)
        mass(k, 'Martial academy keep', (0, 6.5, .08), 6.1, 6.8, 10.1, cover='Roof_Clay', rise=3.0)
        for xx in (-1.6, 1.6):
            for zz in (4, 7):
                k.window((xx, 3.05, zz), .65, 1.65, 'Window_Amber', arched=True)
    else:
        mass(k, 'Broad weapons training hall', (0, 5.7, .08), 13.5, 5.4, 4.8 + level * .23,
             'Fieldstone', 'Roof_Clay', 3.0)
        for xx in (-4.6, -2.7, 2.7, 4.6):
            k.window((xx, 2.96, 2.5), .85, 1.65, 'Iron_Grate', arched=True)
    entrance(k, (0, 2.95, .08), 1.7, 2.75)
    for xx in (-8, 8):
        k.box('Courtyard defensive side wall', (xx, -.4, 1.35), (.48, 13, 2.55), 'Fieldstone')
        k.box('Side wall stone coping', (xx, -.4, 2.68), (.68, 13.2, .22), 'Limestone')
    for xx in (-5.35, 5.35):
        k.box('Academy gate curtain', (xx, -6.75, 1.38), (5.25, .6, 2.6), 'Fieldstone')
        k.box('Gate curtain coping', (xx, -6.75, 2.72), (5.35, .8, .22), 'Limestone')
    for xx in (-2.04, 2.04):
        k.box('Gatehouse load-bearing pier', (xx, -6.65, 2.5), (1.12, 2.5, 5), 'Limestone')
    for yy in (-7.9, -5.4):
        arch(k, (0, yy, .08), 2.95, 3.65, .5, trim=.25)
    k.box('Gatehouse upper chamber', (0, -6.65, 4.46), (2.96, 2.5, 1.15), 'Limestone')
    pitched(k, 'Red gatehouse roof', (0, -6.65, 5.05), 5.8, 3.1, 2.0, 'Roof_Clay')
    k.crest_plate((0, -8.2, 3.8), 1.65, 1.8, 'Canvas_Red')
    k.sword_symbol((-.3, -8.26, 4.1), 1.1, .4)
    k.sword_symbol((.3, -8.28, 4.1), 1.1, -.4)
    for xx in (-4.8, 4.8):
        flag(k, (xx, -6.85, 2.65))
    if level >= 2:
        mass(k, 'West armoury range', (-6.3, -.1, .08), 3, 8, 3.9, 'Fieldstone', 'Roof_Clay', 1.8)
        k.window((-4.75, -.8, 1.8), .7, 1.3, 'Iron_Grate', 'right', True)
    if level >= 3:
        mass(k, 'East barracks range', (6.3, -.1, .08), 3, 8, 4.5, 'Fieldstone', 'Roof_Clay', 1.8)
        for yy in (-2.3, .7):
            k.window((4.75, yy, 2), .75, 1.5, 'Window_Amber', 'left', True)
    if level >= 4:
        for xx in (-8, 8):
            k.tower((xx, -6.4, .08), 1.4, 7.0 + (level - 4), 'Roof_Clay', False)
    k.lathe('Circular sparring ground', (0, -.2, .09), [(2.45, 0), (2.45, .04)], 'Earth', 32)
    for xx in (-3.25, 3.25):
        k.training_dummy((xx, .8, .08))
    target(k, (3, -4.5, .08))
    k.weapon_rack((-3.1, -4.0, .08), True)
    k.weapon_rack((-3.1, -2.4, .08), False)
    k.bench((-2.8, 2.1, .08), 2.1)
    identity(k, f'Warrior academy level {level}: fortified gate and red-roofed drill compound, open sparring yard, weapons, shields, dummies and archery target')


def round_school_tower(k, p, r, h, dome=False, crested=False):
    x, y, z = p
    k.lathe('Research tower battered plinth', p, [(r + .35, 0), (r + .35, .35), (r, .85), (r, h)], 'Limestone', 12)
    frieze_ring(k, 'Astral inlay below research cornice', (x, y, z + h - .9), r + .015, .6, 'Arcane_Trim')
    k.lathe('Research tower roof cornice', (x, y, z + h), [(r, 0), (r + .25, .23), (r + .25, .4)], 'Limestone', 12)
    if dome:
        k.lathe('Supported copper research cupola', (x, y, z + h + .4), [(r + .4, 0), (r + .3, .8),
                (r * .83, r * .74), (r * .4, r * 1.2), (.07, r * 1.4)], 'Copper', 20)
    else:
        k.lathe('Tall blue research spire', (x, y, z + h + .4), [(r + .4, 0), (r * .65, r * .65),
                (.06, r * 2.05)], 'Roof_Slate', 12)
    for zz in (3.7, 7.2, 10.7):
        if zz + 1.8 > h - .6:
            continue
        if not crested or zz + 2 < h - 3.6 or zz - .2 > h - 1.1:
            k.window((x, y - r - .035, z + zz), .8, 1.8, 'Window_Blue', arched=True)
        k.window((x + r + .035, y, z + zz), .8, 1.8, 'Window_Blue', 'right', True)


def bottle(k, p, h=.55, mat='Liquid_Green'):
    x, y, z = p
    k.lathe('Glass reagent flask', p, [(.14, 0), (.18, .12), (.16, h * .55), (.055, h * .8),
            (.055, h), (.035, h), (.035, h * .8)], 'Glass_Purple' if mat=='Liquid_Purple' else 'Glass_Green', 12)
    k.lathe('Visible reagent', (x, y, z + .02), [(.12, 0), (.14, h * .45)], mat, 10)
    k.lathe('Reagent cork', (x, y, z + h - .02), [(.05, 0), (.05, .09)], 'Cork', 8)


def telescope(k, p):
    x, y, z = p
    k.lathe('Telescope stone pier', p, [(.45, 0), (.45, .2), (.2, .4), (.2, 1.65)], 'Limestone', 12)
    direction = Vector((0, -.84, .54)).normalized()
    origin = Vector((x, y + .55, z + 1.55))
    obj = k.lathe('Brass observing telescope', (0, 0, 0), [(.14, 0), (.18, .25), (.18, 1.8), (.28, 1.8), (.28, 2.05)], 'Brass', 18)
    obj.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler(); obj.location = origin
    obj = k.lathe('Telescope objective lens', (0, 0, 0), [(.235, 0), (.235, .02)], 'Window_Blue', 18)
    obj.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler(); obj.location = origin + direction * 2.06


def arcane_school(k, level):
    k.box('Arcane campus stone forecourt', (.2, .4, .04), (21, 19, .08), 'Cobble')
    main_h = 9.5 + level * 1.15
    round_school_tower(k, (-3.4, 2.5, .08), 2.7, main_h, level >= 4, crested=True)
    entrance(k, (-3.4, -.24, .1), 1.55, 2.85)
    mass(k, 'Low lecture and demonstration hall', (2.3, -2.4, .08), 6.2, 5.4,
         4.5 + level * .16, cover='Roof_Slate', rise=2.3)
    for xx in (.65, 3.95):
        k.window((xx, -5.15, 1.6), .9, 2.65, 'Window_Blue', arched=True)
    entrance(k, (2.3, -5.15, .08), 1.45, 2.6)
    frieze(k, 'Lecture hall celestial inlay', (2.3, -5.112, 4.25 + level * .16), 6.1, .32, 'Arcane_Trim')
    k.crest_plate((-3.4, -.36, main_h - 3.4), 2.0, 2.1, 'Canvas_Blue')
    for zz in (main_h - 3.0, main_h - 1.7):
        k.beam('Tower crest masonry fixing', (-3.4, -.18, zz), (-3.4, -.37, zz), .09, 'Iron')
    vertices = []
    for i in range(16):
        a = i * PI / 8; r = .84 if i % 2 == 0 else .35
        vertices.append((-3.4 + math.cos(a) * r, -.38, main_h - 2.25 + math.sin(a) * r))
    k.mesh('Eight-point astral school crest', vertices, [tuple(range(16))], 'Brass')
    if level >= 2:
        mass(k, 'Alchemy laboratory', (6.5, 2, .08), 3.3, 5.4, 4.2, cover='Roof_Slate', rise=1.7)
        for yy in (.4, 3.6):
            k.window((8.2, yy, 1.7), .75, 1.8, 'Window_Green', 'right', True)
    if level >= 3:
        round_school_tower(k, (3, 5.4, .08), 1.9, 9 + level * .6)
        entrance(k, (3, 3.46, .08), 1.15, 2.35)
    if level >= 4:
        mass(k, 'Rear connecting scriptoria', (-.2, 6.6, .08), 4.3, 2.7, 4.6,
             cover='Roof_Slate', rise=1.4, across=True)
    if level >= 5:
        round_school_tower(k, (-7.4, 5.8, .08), 1.7, 10.5, True)
        entrance(k, (-7.4, 4.06, .08), 1.1, 2.35)
    armillary(k, (-6.6, -4.8, .08), .85)
    telescope(k, (6.8, -5.5, .08))
    table(k, (-5.2, -2.6, .08), 1.8, .85)
    for i in range(3):
        bottle(k, (-5.75 + i * .47, -2.6, 1.11), .48 + i * .09,
               'Liquid_Green' if i % 2 else 'Liquid_Purple')
    book(k, (-5.2, -2.85, 1.1), .6)
    identity(k, f'Arcane academy level {level}: tall freestanding research tower and low lecture hall, growing laboratory campus, supported cupolas, armillary, telescope and reagent workbench')


def cloister_range(k, x, h=3.6):
    direction = 1 if x > 0 else -1
    k.box('Cloister outer stone wall', (x + direction * 1.55, 1.4, h / 2), (.32, 10, h), 'Limestone')
    for yy in (-3.5, 6.3):
        k.box('Cloister end wall', (x, yy, h / 2), (3.4, .32, h), 'Limestone')
    inner = x - direction * 1.55
    for yy in (-2.0, 1.15, 4.3):
        arch(k, (inner, yy, .08), 2.6, 2.85, .32, side='right', trim=.22)
    k.box('Cloister arcade top plate', (inner, 1.4, 3.3), (.35, 10.0, .6), 'Limestone')
    pitched(k, 'Garden cloister pitched roof', (x, 1.4, h), 4, 10.4, 1.8, 'Roof_Moss')
    frieze(k, 'Cloister carved vine eave', (x + direction * 1.72, 1.4, h - .46), 9.8, .36,
           'Spirit_Trim', 'right' if direction > 0 else 'left')
    for yy in (-1.4, 2.3, 5.2):
        k.window((x + direction * 1.73, yy, 1.3), .7, 1.55, 'Window_Green',
                 'right' if direction > 0 else 'left', True)


def spirit_school(k, level):
    k.box('Garden academy ground', (0, .4, .04), (20, 20, .08), 'Earth')
    k.box('Garden approach paving', (0, -5, .09), (3.2, 8, .1), 'Cobble')
    h = 5.6 + level * .47
    k.lathe('Hexagonal communion hall', (0, 6.1, .08), [(3.75, 0), (3.75, h),
            (3.95, h), (3.95, h + .22)], 'Limestone', 6, smooth=False)
    frieze_ring(k, 'Communion hall spirit vine cornice', (0, 6.1, h - .6), 3.766, .55, 'Spirit_Trim', 6)
    k.lathe('Broad green sanctuary roof', (0, 6.1, h + .3), [(4.35, 0), (3.8, .9),
            (1.05, 3.2), (.9, 3.2), (.9, 3.8)], 'Roof_Moss', 12)
    k.lathe('Communion hall roof lantern', (0, 6.1, h + 4.1), [(.9, 0), (.9, .85)], 'Oak', 12)
    k.lathe('Lantern cap', (0, 6.1, h + 4.95), [(1.1, 0), (.045, 1.5)], 'Roof_Moss', 12)
    entrance(k, (0, 2.82, .08), 1.8, 3)
    k.sign('Spirit', (0, 2.65, 4.7), 1.7)
    for xx in (-1.4, 1.4):
        k.window((xx, 2.82, 3.3), .55, 1.35, 'Window_Green', arched=True)
    k.window((0, 5.17, h + 4.2), .38, .55, 'Window_Green', arched=True)
    k.window((.94, 6.1, h + 4.2), .38, .55, 'Window_Green', 'right', True)
    if level >= 2:
        cloister_range(k, -6.2)
    if level >= 3:
        cloister_range(k, 6.2)
    if level >= 4:
        arch(k, (0, -6.7, .08), 3.2, 3.75, .65, trim=.3)
        pitched(k, 'Garden academy entrance canopy', (0, -6.7, 4.13), 4.4, 1.8, 1.35, 'Roof_Moss')
    if level >= 5:
        for xx in (-4.2, 4.2):
            mass(k, 'Garden teaching pavilion', (xx, 7.5, .08), 3.7, 4.2, 4.6,
                 'Limestone', 'Roof_Moss', 1.9)
    k.tree((0, -.7, .08), .65 + level * .025, variant=3)
    for xx in (-3.0, 3.0):
        for yy in (-3.6, .3):
            k.planter((xx, yy, .08), .52, flowers=yy < 0)
    k.lathe('Ritual fountain basin', (0, -4.4, .09), [(1.02, 0), (1.2, .45), (1.1, .62),
            (.95, .62), (.84, .22)], 'Limestone', 18)
    k.lathe('Ritual basin water', (0, -4.4, .5), [(.96, 0), (.96, .015)], 'Water', 18)
    for xx in (-2.5, 2.5):
        k.lathe('Carved garden rune marker', (xx, 1.3, .08), [(.35, 0), (.26, 1.45), (.05, 1.65)], 'Limestone', 5, smooth=False)
        k.sign('Spirit', (xx, .99, .55), .5)
    for xx in (-4.2, 4.2):
        k.bench((xx, -5.6, .08), 1.7)
    identity(k, f'Spirit academy level {level}: garden-centred cloister compound, polygonal communion hall with green roof lantern, sacred tree, ritual water, rune markers and planted teaching courts')


def build(k, name):
    if 'Academy_Lv' in name:
        school, level = name.split('Academy_Lv')
        {'Warrior': warrior_school, 'Arcane': arcane_school, 'Spirit': spirit_school}[school](k, int(level))
    elif name in ('Church', 'Chapel'):
        church(k, name == 'Chapel')
    elif name == 'Blacksmith':
        blacksmith(k)
    elif name == 'Library':
        library(k)
    elif name == 'Museum':
        museum(k)
    elif name == 'Observatory':
        observatory(k)
    else:
        return False
    return True
