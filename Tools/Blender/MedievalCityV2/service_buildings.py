"""Town service buildings with layouts and equipment determined by their jobs."""
import math

from facility_buildings import (arch, armillary, bottle, circular_band, column, entrance, flag, hammer, identity,
                               lean_roof, mass, open_tub, pitched, rotated, steps, table)


PI = math.pi


def timber_bay(k, p, w, h):
    x, y, z = p
    for xx in (-w / 2, w / 2):
        k.box('Facade timber upright', (x + xx, y, z + h / 2), (.18, .2, h), 'DarkTimber')
    for zz in (0, h):
        k.box('Facade timber horizontal', (x, y, z + zz), (w + .18, .2, .18), 'DarkTimber')
    k.beam('Facade diagonal brace', (x - w * .42, y, z + .15), (x + w * .42, y, z + h - .15), .12)


def storage(k, granary=False):
    if granary:
        for xx in (-3.1, 0, 3.1):
            for yy in (-2.4, 2.4):
                k.lathe('Rodent-proof staddle stone', (xx, yy, 0), [(.42, 0), (.32, 1.3),
                        (.7, 1.3), (.7, 1.48), (.42, 1.58)], 'Fieldstone', 12)
        k.box('Granary suspended timber floor', (0, 0, 1.65), (8.6, 6.8, .3), 'DarkTimber')
        k.box('Raised grain store walls', (0, 0, 3.8), (8.1, 6.4, 4), 'Oak')
        for xx in (-3.95, 3.95):
            k.box('Granary corner timber', (xx, -3.25, 3.7), (.18, .18, 3.8), 'DarkTimber')
        pitched(k, 'Granary steep thatch roof', (0, 0, 5.8), 8.9, 7.2, 3.3, 'Thatch', 'Oak')
        entrance(k, (0, -3.25, 1.8), 1.8, 2.6, 'DarkTimber')
        for xx in (-2.7, 2.7):
            k.window((xx, -3.27, 3.8), .75, .85, 'Iron_Grate')
        steps(k, (0, -6.0, 0), 2.3, 9, .2, .32)
        k.sign('Grain', (0, -3.76, 6.1), 1.6)
        for xx in (-2.8, 2.8):
            k.lathe('Open grain sack',(xx,-4.4,0),[(.35,0),(.48,.3),(.4,.75),(.32,.9),(.28,.9),(.3,.75)],'Linen',14)
            k.lathe('Stored grain kernels',(xx,-4.4,.78),[(.3,0),(.3,.025)],'Grain',16)
        identity(k, 'Elevated timber granary on mushroom staddle stones, ventilated grain loft, thatched roof and access stair')
        return
    mass(k, 'High freight warehouse', (0, 0, 0), 10.5, 9, 7.4, 'Plaster', 'Roof_Slate', 3.9)
    for xx in (-3.6, 3.6):
        timber_bay(k, (xx, -4.55, .4), 3.35, 6.8)
    k.box('Raised loading dock', (0, -5.35, .4), (11, 1.7, .8), 'Fieldstone')
    entrance(k, (0, -4.72, .8), 3.25, 4.1, 'DarkTimber')
    k.panel('Loft loading door', (0, -4.84, 6.0), (1.9, 2.3), 'Door_Oak', arched=True)
    for direction in (-1, 1):
        k.beam('Warehouse gable hoist anchor', (direction * .9, -4.93, 8.1), (0, -4.93, 8.9), .18)
    k.beam('Warehouse projecting hoist', (0, -4.7, 8.8), (0, -6.45, 8.8), .2)
    k.beam('Hoist diagonal support', (0, -4.93, 8.4), (0, -6.0, 8.8), .14)
    k.beam('Hoist rope', (0, -6.4, 8.7), (0, -6.4, 5.55), .035, 'Linen')
    for xx in (-3.7, 3.7):
        k.crate((xx, -5.3, .8), 1.1)
        k.crate((xx, -5.3, 1.68), .75)
    for yy in (-2, 1.6):
        k.window((5.3, yy, 4.8), .75, 1.5, 'Iron_Grate', 'right')
    steps(k, (0, -7.7, 0), 3.6, 4, .2, .4)
    k.sign('Storage', (3.7, -4.95, 5.2), 1.3)
    identity(k, 'Freight warehouse with tall double loading doors, loft hatch, supported timber hoist, raised cargo dock and stacked shipping crates')


def stable(k):
    k.box('Stable straw yard', (0, 0, .04), (16, 10.5, .08), 'Earth')
    k.box('Stable rear wall', (0, 2.9, 1.9), (14, .35, 3.8), 'Oak')
    for xx in (-7, 7):
        k.box('Stable end masonry', (xx, 0, 1.9), (.35, 6, 3.8), 'Fieldstone')
    for xx in (-6, -3, 0, 3, 6):
        k.box('Horse stall partition', (xx, 0, 1), (.12, 5.7, 2), 'Oak')
        k.box('Stable aisle roof post', (xx, -2.9, 1.9), (.2, .2, 3.8), 'DarkTimber')
    k.box('Stable front tie beam', (0, -2.9, 3.7), (14.3, .24, .24), 'DarkTimber')
    pitched(k, 'Long transverse stable roof', (0, 0, 3.8), 6.8, 14.8, 2.5, 'Thatch', 'Oak', True)
    for xx in (-4.5, -1.5, 1.5, 4.5):
        k.panel('Stall half door', (xx, -3.03, .1), (2.7, 1.1), 'Door_Oak')
        for yy in (-1.0, 1.3):
            k.box('Fresh stable hay', (xx, yy, .22), (1.6, 1.3, .32), 'Thatch')
    k.trough((-4.7, -4.1, .08), 3.2)
    for xx in (3.5, 5.2):
        k.box('Bound feed bale', (xx, -4.1, .55), (1.4, .9, .94), 'Thatch')
    k.sign('Stable', (0, -3.23, 2.6), 1.0)
    k.box('Tack hanging board', (7.22, .1, 2), (.12, 2.5, 1.2), 'DarkTimber')
    for yy in (-.6, .4, 1.2):
        k.beam('Bridle leather strap', (7.32, yy, 1.45), (7.32, yy, 2.4), .04, 'Canvas_Red')
    identity(k, 'Long open stable range under a transverse thatch roof, divided horse stalls with half doors, hay bedding, feed bales, tack and drinking trough')


def bakery(k):
    mass(k, 'Stone bakehouse', (-1.5, 1, 0), 6.8, 6.3, 4.15, 'Plaster', 'Roof_Clay', 2.5)
    mass(k, 'Flour store', (-5.8, 2.1, 0), 2.7, 4.1, 3.0, 'Brick', 'Roof_Clay', 1.5)
    k.lathe('Round brick bread oven', (3.3, 1.25, 0), [(1.75, 0), (1.75, 1.4),
            (1.5, 2.3), (.8, 3.0), (.38, 3.2)], 'Brick', 20)
    k.lathe('Bread oven chimney', (3.3, 1.25, 3.1), [(.42, 0), (.42, 2.9), (.6, 2.9), (.6, 3.1)], 'Brick', 12)
    k.panel('Oven dark mouth', (3.3, -.52, .7), (1.0, 1.0), 'Iron', arched=True)
    arch(k, (3.3, -.58, .7), 1, 1.0, .25, 'Brick', trim=.18)
    entrance(k, (-3.4, -2.2, .1), 1.25, 2.55)
    k.window((-.1, -2.22, 1.3), 2.1, 2, 'Window_Amber', arched=True)
    k.canopy((-.4, -3.3, 0), 4.1, 2.0, 'Canvas_Gold', 2.65)
    table(k, (-.4, -3.9, 0), 3.6, .85)
    for i in range(8):
        k.ball('Fresh scored bread loaf', (-1.65 + (i % 4) * .75, -4.0 + (i // 4) * .3, 1.15),
               .23, 'Bread_Crust', scale=(1.15, .72, .6), segments=12, rings=7)
    k.sign('Bread', (-.4, -4.45, 1.5), .85)
    for xx in (-5.0, -4.1):
        k.basket((xx, -3.2, 0), .4)
    k.beam('Oven peel handle', (4.1, -.9, .1), (4.1, 1.0, 2.9), .06, 'Oak')
    k.box('Wooden oven peel', (4.1, .97, 2.65), (.5, .08, .68), 'Oak')
    identity(k, 'Low bakehouse and flour store with large round masonry oven, projecting bread counter, visible loaves and baking peel')


def bathhouse(k):
    k.lathe('Round bathing hall', (0, 2, 0), [(4.2, 0), (4.2, 4.8)], 'Plaster', 24)
    k.lathe('Bath hall copper dome', (0, 2, 4.8), [(4.55, 0), (4.3, 1.3), (3.4, 2.6),
            (1.8, 3.45), (.1, 3.8)], 'Copper', 24)
    for xx in (-5.2, 5.2):
        mass(k, 'Low changing range', (xx, 1.7, 0), 3.2, 7.4, 3.1, 'Plaster', 'Roof_Clay', 1.5)
        k.window((xx, -2.05, 1.4), 1.0, 1.3, 'Window_Amber', arched=True)
    entrance(k, (0, -2.27, .08), 2.0, 2.9)
    for xx in (-1.65, 1.65):
        column(k, (xx, -3.7, 0), 3.1, .18)
    k.box('Bath entrance canopy', (0, -3.1, 3.2), (4, 2.1, .25), 'Limestone')
    pitched(k, 'Bath portico roof', (0, -3.1, 3.33), 4.3, 2.4, 1.1, 'Roof_Clay')
    k.sign('Bath', (0, -4.41, 3.2), .95)
    for xx in (-3.5, 3.5):
        open_tub(k, (xx, -3.8, 0), .6, .6)
        k.bench((xx, -5.1, 0), 1.8)
    k.box('Bath furnace chimney', (-2.5, 4.8, 3.8), (1.15, 1.15, 7.6), 'Brick')
    identity(k, 'Domed communal bathing hall with low changing wings, sheltered stone portico, water basins and furnace chimney')


def clinic(k):
    mass(k, 'Apothecary treatment hall', (0, 3, 0), 9.5, 5.5, 4.8, 'Plaster', 'Roof_Moss', 2.6)
    for xx in (-4.0, 4.0):
        mass(k, 'Clinic quiet side range', (xx, -1, 0), 2.8, 4, 3.25, 'Plaster', 'Roof_Moss', 1.6)
        k.window((xx, -3.05, 1.35), .9, 1.5, 'Window_Green', arched=True)
    entrance(k, (0, .2, .08), 1.6, 2.7)
    for xx in (-1.85, 1.85):
        k.window((xx, .2, 1.4), .75, 2, 'Window_Green', arched=True)
    k.sign('Herb', (0, -.25, 4.9), 1.8)
    for xx in (-1.7, 1.7):
        for yy in (-2.8, -.7):
            k.box('Medicinal herb bed', (xx, yy, .2), (1.1, 1.5, .4), 'Oak')
            for i in range(3):
                k.planter((xx, yy + (i - 1) * .4, .4), .22, False)
    k.canopy((4, -3.85, 0), 2.6, 1.5, 'Canvas_Green', 3.05)
    table(k, (4, -3.85, 0), 2.0, .65)
    for i in range(4):
        bottle(k, (3.3 + i * .42, -3.85, 1.04), .5 + i % 2 * .12)
    k.bench((-4, -4.3, 0), 2.3)
    identity(k, 'Low treatment compound enclosing a medicinal herb court, apothecary bottle counter and sheltered patient waiting bench')


def guardhouse(k):
    for xx, h in ((-3.5, 8.8), (3.5, 6.3)):
        mass(k, 'Gate guard tower', (xx, 0, 0), 3.3, 5.2, h, 'Fieldstone')
        k.box('Guard tower parapet', (xx, 0, h + .2), (3.7, 5.6, .4), 'Limestone')
        for yy in (-2.6, 2.6):
            for offset in (-1.4, 0, 1.4):
                k.box('Tower defensive merlon', (xx + offset, yy, h + .8), (.65, .65, 1), 'Limestone')
        for zz in (2.6, 5.3):
            if zz + 1.4 < h:
                k.window((xx, -2.65, zz), .55, 1.3, 'Iron_Grate', arched=True)
    for yy in (-1.6, 1.6):
        arch(k, (0, yy, 0), 3.55, 4.7, .55, 'Fieldstone', trim=.32)
    k.box('Guard gate bridge chamber', (0, 0, 5.0), (3.7, 3.2, 1.1), 'Fieldstone')
    pitched(k, 'Guard gate slate roof', (0, 0, 5.55), 4.3, 3.8, 2.0)
    k.sign('Guard', (0, -1.98, 5.6), 1.2)
    entrance(k, (-3.5, -2.65, 0), 1.25, 2.4)
    k.weapon_rack((4.0, -3.9, 0), False)
    flag(k, (-3.5, 0, 9.2))
    identity(k, 'Asymmetric fortified guard gate, tall watch platform and lower guardroom tower, open arched passage and shield rack')


def barracks(k):
    k.box('Barracks drill square', (0, .5, .04), (19, 17, .08), 'Cobble')
    mass(k, 'Long military dormitory', (0, 3.6, .08), 14, 5.5, 6.1, 'Plaster', 'Roof_Clay', 2.5, True)
    for xx in (-5.3, -2.8, 2.8, 5.3):
        for zz in (1.5, 4.0):
            k.window((xx, .8, zz), .85, 1.6, 'Window_Amber', arched=True)
    entrance(k, (0, .78, .08), 2.0, 2.8)
    for xx in (-7.1, 7.1):
        k.tower((xx, 3.6, .08), 1.35, 8.5, 'Roof_Clay', False)
    k.sign('Warrior', (0, .55, 3.6), 1.8)
    for xx in (-4.5, -1.5, 1.5, 4.5):
        k.training_dummy((xx, -3.2, .08))
    k.weapon_rack((-6.6, -5.5, .08), True)
    k.weapon_rack((6.6, -5.5, .08), False)
    for xx in (-7.5, 7.5):
        flag(k, (xx, -1.0, .08), height=4.5)
    identity(k, 'Long two-storey barracks with transverse roof, end watch bastions, dormitory windows, drill square, dummies and military equipment')


def guildhall(k):
    for xx in (-6.0, 6.0):
        k.box('Guild ground side wall', (xx, 0, 1.65), (.38, 7, 3.3), 'Limestone')
    k.box('Guild ground rear wall', (0, 3.35, 1.65), (12, .3, 3.3), 'Limestone')
    for xx in (-4.8, -2.4, 0, 2.4, 4.8):
        arch(k, (xx, -3.5, 0), 2.03, 2.85, .55, trim=.22)
    k.box('Guild arcade entablature', (0, -3.5, 3.18), (12.5, .65, .3), 'Limestone')
    mass(k, 'Guild assembly chamber', (0, 0, 3.33), 12, 7, 4.0, 'Plaster', 'Roof_Clay', 3.2)
    for xx in (-4.45, -1.5, 1.5, 4.45):
        k.window((xx, -3.55, 4.25), 1.25, 2.0, 'Window_Amber', arched=True)
    for xx in (-5.8, -3, 0, 3, 5.8):
        k.box('Guild chamber timber pier', (xx, -3.56, 5.33), (.2, .2, 4), 'DarkTimber')
    mass(k, 'Guild clock tower', (0, 1.6, 0), 3, 3, 13, 'Limestone')
    pitched(k, 'Guild clock tower cap', (0, 1.6, 13), 3.7, 3.7, 2.9, 'Roof_Slate')
    face = k.lathe('Ivory guild clock dial', (0, 0, 0), [(.83, 0), (.83, .055)], 'Linen', 24)
    face.rotation_euler.x = PI / 2; face.location = (0, .035, 11.5)
    circular_band(k, 'Guild clock brass bezel', (0, -.035, 11.5), .83, .055, 'Brass', True)
    k.beam('Guild clock minute hand', (0, -.1, 11.5), (.12, -.1, 12.13), .045, 'Iron')
    k.beam('Guild clock hour hand', (0, -.1, 11.5), (-.4, -.1, 11.7), .065, 'Iron')
    k.sign('Tools', (0, -3.92, 7.7), 1.9)
    for xx in (-4.8, 4.8):
        flag(k, (xx, -3.95, 5.8), 'Canvas_Red', 2.8)
    k.box('Guild public notices', (-4.7, -.9, 1.65), (1.4, .12, 1.3), 'Oak')
    k.panel('Guild parchment commissions', (-4.7, -.99, 1.1), (1.15, 1.05), 'Parchment')
    table(k, (4.6, -1.2, 0), 2, .85)
    identity(k, 'Civic guild assembly hall above a five-bay open arcade, exposed upper timber structure, clock tower, guild banners and public commission desk')


def tavern(k):
    mass(k, 'Public feasting hall', (0, 1, 0), 11.5, 7, 5.6, 'Plaster', 'Roof_Clay', 3.4)
    mass(k, 'Tall inn cross wing', (-4.1, -1.0, 0), 3.6, 10, 7.1, 'Plaster', 'Roof_Slate', 2.25)
    for xx in (-5.8, -4.1, -2.4):
        k.box('Inn frontage timber post', (xx, -6.08, 3.55), (.18, .2, 7.1), 'DarkTimber')
    for zz in (2.9, 7):
        k.box('Inn frontage timber plate', (-4.1, -6.08, zz), (3.6, .2, .18), 'DarkTimber')
    for xx in (-4.95, -3.25):
        k.window((xx, -6.12, 3.8), .7, 1.9)
    entrance(k, (1.7, -2.56, .08), 2, 2.75, 'DarkTimber')
    for xx in (-.7, 4.2):
        k.window((xx, -2.57, 1.2), 1.3, 2.2, 'Window_Amber', arched=True)
    for xx in (-1.9, .1, 3.1, 5.5):
        k.box('Feasting hall front timber', (xx, -2.58, 4.2), (.17, .18, 2.8), 'DarkTimber')
    k.sign('Tavern', (-4.1, -6.5, 7.2), 1.45)
    k.canopy((1.6, -4.55, 0), 6, 2.8, 'Canvas_Gold', 3.0)
    for xx in (-.3, 3.4):
        table(k, (xx, -4.4, 0), 2.1, .85)
        k.bench((xx, -5.3, 0), 2.1)
        k.lathe('Tavern ale jug', (xx + .5, -4.4, 1.04), [(.12, 0), (.17, .15), (.1, .34), (.1, .38)], 'Terracotta', 12)
    for xx in (-1.5, 5.2):
        k.barrel((xx, -3.4, 0), .4, 1.05)
    identity(k, 'Large public feasting hall with tall cross-wing inn, sheltered communal beer terrace, ale barrels and prominent drinking sign')


def market_hall(k):
    k.box('Covered market paving', (0, 0, .04), (16.8, 11.5, .08), 'Cobble')
    for xx in (-7.3, 7.3):
        for yy in (-4.4, 0, 4.4):
            k.box('Market roof load-bearing post', (xx, yy, 2.5), (.28, .28, 5), 'DarkTimber')
            k.box('Market post stone shoe', (xx, yy, .3), (.58, .58, .6), 'Limestone')
    for yy in (-4.4, 0, 4.4):
        k.beam('Market full-span tie beam', (-7.3, yy, 4.85), (7.3, yy, 4.85), .26)
        k.beam('Market king post', (0, yy, 4.85), (0, yy, 8.75), .2)
        for direction in (-1, 1):
            k.beam('Market triangular roof truss', (direction * 7.3, yy, 4.9), (0, yy, 8.75), .2)
    pitched(k, 'Great trading hall roof', (0, 0, 5), 15.3, 9.6, 3.8, 'Roof_Clay', 'Oak')
    for xx, kind in ((-5.1, 'Produce'), (-1.7, 'Bread'), (1.7, 'Potion'), (5.1, 'Cloth')):
        k.box('Market counter frontage', (xx, -3.8, .6), (2.9, 1.0, 1.2), 'Oak')
        k.box('Market counter worktop', (xx, -3.8, 1.22), (3.0, 1.1, .14), 'Oak')
        rotated(k, (xx, -3.4, .03), 0, lambda kind=kind: k.market_goods(kind))
    for xx in (-4.3, 0, 4.3):
        k.crate((xx, 2.7, .08), 1.1)
    k.sign('Trade', (0, -4.98, 5.8), 2)
    identity(k, 'Open timber trading hall with real full-span roof trusses, stone-shod posts, four stocked counters, rear cargo aisle and trading crest')


def casino(k):
    k.box('Casino stone forecourt', (0, -.3, .12), (15, 13, .24), 'Limestone')
    k.lathe('Octagonal games pavilion', (0, 2.0, .24), [(4.4, 0), (4.4, 5.8)], 'Plaster_Rose', 8, smooth=False)
    k.lathe('Games pavilion tiered roof', (0, 2, 6.04), [(4.85, 0), (4.25, 1.2), (1.25, 3.4),
            (1.1, 3.4), (1.1, 4)], 'Roof_Clay', 16)
    k.lathe('Casino roof finial', (0, 2, 10.04), [(1.3, 0), (.05, 1.5)], 'Copper', 16)
    for xx in (-5.1, 5.1):
        mass(k, 'Games club side salon', (xx, 1.1, .24), 3.1, 6.8, 3.8, 'Plaster_Rose', 'Roof_Clay', 1.7)
    entrance(k, (0, -2.45, .24), 2.1, 3.25)
    for xx in (-2.1, 2.1):
        column(k, (xx, -4.4, .24), 3.5, .23)
    k.roof('Casino entrance pavilion', (0, -3.8, 3.74), 5.2, 3.6, 1.4, 'Roof_Clay', 'hip')
    k.sign('Casino', (0, -2.5, 4.5), 1.8)
    for xx in (-5, 5):
        table(k, (xx, -3.75, .24), 1.6, 1.1)
        for j in range(3):
            k.lathe('Gaming brass counters', (xx + (j - 1) * .32, -3.75, 1.28), [(.1, 0), (.1, .04)], 'Brass', 12)
        k.bench((xx, -4.7, .24), 1.7)
        k.lamp((xx, -5.7, .24))
    identity(k, 'Octagonal rose-plaster games pavilion with tiered roof, side salons, columned entrance and visible gaming tables and counters')


def workshop_house(k):
    mass(k, 'Craftsman dwelling', (-2.6, .6, 0), 5, 7, 6.4, 'Plaster', 'Roof_Clay', 2.8)
    entrance(k, (-2.6, -2.95, .08), 1.45, 2.65, 'DarkTimber')
    for xx in (-3.8, -1.4):
        k.window((xx, -2.98, 3.75), .85, 1.75)
    for xx in (-5.0, -.2):
        k.box('Dwelling upper timber corner', (xx, -2.98, 4.5), (.18, .18, 3.8), 'DarkTimber')
    for zz in (2.6, 6.3):
        k.box('Dwelling front timber plate', (-2.6, -2.98, zz), (5, .18, .18), 'DarkTimber')
    k.box('Carpentry workshop rear wall', (2.6, 3.5, 1.65), (5.4, .25, 3.3), 'Oak')
    for yy in (-2.8, 3.4):
        k.box('Carpentry shed roof post', (5.3, yy, 1.67), (.22, .22, 3.34), 'DarkTimber')
    lean_roof(k, 'Attached carpentry shed roof', -.15, 5.65, -3.2, 3.8, 4.2, 3.3, 'Roof_Slate', 'Oak')
    table(k, (2.4, -2.35, 0), 3.7, 1.2)
    for i in range(3):
        hammer(k, (1.2 + i * .8, -2.35, 1.05), .3 + i * .8)
    k.box('Carpentry bench vise jaw', (3.75, -2.95, .99), (.4, .22, .38), 'Iron')
    k.beam('Bench vise screw', (3.75, -3.02, .99), (3.75, -3.5, .99), .07, 'Iron')
    for i in range(6):
        k.box('Seasoned carpentry stock', (6.3, .7, .2 + i * .16), (.9, 3.4, .13), 'Oak')
    k.box('Joinery wall tool board', (2.5, 3.3, 2.0), (3.5, .14, 1.3), 'DarkTimber')
    for xx in (1.2, 2, 2.8, 3.6):
        k.beam('Hanging wood chisel', (xx, 3.16, 1.5), (xx, 3.16, 2.25), .055, 'Iron')
        k.beam('Chisel wooden grip', (xx, 3.16, 2.25), (xx, 3.16, 2.55), .085, 'Oak')
    k.sign('Tools', (2.5, -3.3, 2.6), .95)
    k.beam('Workshop sign suspension', (2.5, -3.2, 3.79), (2.5, -3.2, 3.63), .06, 'Iron')
    identity(k, 'Craftsman dwelling attached to a low open carpentry workshop, sloping supported shed roof, vise workbench, chisels, hand tools and seasoned timber stack')


RECIPES = {'Warehouse': storage, 'Granary': lambda k: storage(k, True), 'Stable': stable,
           'Bakery': bakery, 'Bathhouse': bathhouse, 'Clinic': clinic, 'Guardhouse': guardhouse,
           'Barracks': barracks, 'Guildhall': guildhall, 'Tavern': tavern,
           'Market_Hall': market_hall, 'Casino': casino, 'WorkshopHouse': workshop_house}


def build(k, name):
    recipe = RECIPES.get(name)
    if recipe is None:
        return False
    recipe(k)
    return True
