"""Additional city models assembled with the original fantasy construction kit."""
import importlib.util
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT/'Tools/Blender'/filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


p = load('legacy_parity_props', 'generate_fantasy_town_props.py')
g, r, m, c, t = p.g, p.r, p.m, p.c, p.t
school = load('legacy_parity_school', 'generate_fantasy_schools.py')
school.g = g
PI = math.pi


def materials():
    p.materials()
    for name,color,kind in [
        ('OchrePlaster','A98C67','plain'),('WarriorRed','7C2830','plain'),
        ('Bread','B89359','plain'),('BreadCut','D0B67D','plain'),('Fish','8C9F9D','metal'),
        ('Meat','8F4C46','plain'),('Fat','CEB694','plain'),('Bone','C8BFA8','plain'),
        ('PotionBlue','456978','plain'),('PotionGreen','597553','plain'),
        ('PotionPurple','756080','plain'),('Pottery','9F7657','plain'),
        ('Herb','617347','plain'),('HerbDark','3F5233','plain'),('Soil','40382C','plain')]:
        g.MATERIALS[name] = r.surface('Parity_'+name,color,kind)


def place(recipe, position=(0,0,0), scale=1, angle=0):
    before = set(g.PARTS.objects)
    recipe()
    bpy.context.view_layer.update()
    transform = Matrix.Translation(Vector(position)) @ Matrix.Rotation(angle,4,'Z') @ Matrix.Scale(scale,4)
    for obj in set(g.PARTS.objects)-before:
        obj.matrix_world = transform @ obj.matrix_world


def lathe(name, profile, material, sides=24):
    verts = [(radius*math.cos(2*PI*i/sides),radius*math.sin(2*PI*i/sides),z)
             for radius,z in profile for i in range(sides)]
    faces = []
    for row in range(len(profile)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    faces += [tuple(reversed(range(sides))),tuple((len(profile)-1)*sides+i for i in range(sides))]
    return g.mesh(name,verts,faces,material)


def fish():
    c.ellipsoid('Silver fish body',(0,0,.12),(.32,.11,.105),'Fish')
    g.mesh('Forked fish tail',[(-.25,0,.12),(-.48,-.15,.12),(-.42,0,.12),(-.48,.15,.12)],
           [(0,1,2),(0,2,3)],'Fish')
    g.mesh('Fish dorsal fin',[(-.1,0,.2),(.16,0,.2),(-.07,0,.3)],[(0,1,2)],'Fish')
    for side in (-1,1):
        c.ellipsoid('Fish eye',(.21,side*.092,.16),(.022,.013,.022),'Ink')


def bread():
    c.ellipsoid('Baked oval loaf',(0,0,.13),(.28,.18,.13),'Bread')
    for x in (-.12,0,.12):
        m.rod('Loaf scored crust',(x-.025,-.08,.235),(x+.025,.08,.235),.018,'BreadCut')


def bottle(material='PotionBlue'):
    lathe('Potion glass bottle',[(.11,0),(.14,.04),(.14,.25),(.07,.31),(.055,.43),(.065,.44)],material,16)
    g.cylinder('Bottle cork',(0,0,.465),.053,.065,'Door',.004,12)
    g.box('Potion parchment label',(0,-.14,.17),(.14,.012,.12),'Ivory',.002)
    g.box('Bottle label mark',(0,-.151,.17),(.025,.012,.08),'Ink',0)


def book(color='Banner'):
    g.box('Bound book pages',(0,0,.07),(.27,.36,.1),'Ivory',.006)
    for z in (.009,.129):
        g.box('Book leather cover',(0,0,z),(.3,.39,.025),color,.006)
    g.box('Book leather spine',(-.145,0,.07),(.025,.39,.14),color,.006)
    for yy in (-.11,.11):
        g.box('Book binding band',(-.162,yy,.07),(.015,.025,.14),'Brass',.003)


def empty_basket():
    p.bowl('Empty wicker basket',.44,.38,'Basket')
    for z,radius in ((.08,.35),(.18,.38),(.29,.414),(.39,.44)):
        c.torus('Woven basket hoop',(0,0,z),radius,.022,(0,0,0),'Rope')
    for i in range(12):
        a=2*PI*i/12
        m.rod('Basket woven stave',(.34*math.cos(a),.34*math.sin(a),.02),
              (.44*math.cos(a),.44*math.sin(a),.38),.026,'Basket')


def vegetable():
    c.ellipsoid('Cabbage heart',(0,0,.13),(.16,.15,.13),'Leaf')
    for i in range(5):
        a=2*PI*i/5
        c.ellipsoid('Folded cabbage leaf',(.095*math.cos(a),.095*math.sin(a),.12),(.11,.085,.09),'Foliage')


def rack(weapons=True):
    for x in (-.87,.87):
        g.box('Rack spreading foot',(x,0,.08),(.3,.75,.16),'Timber',.01)
        g.box('Rack upright',(x,.14,.98),(.13,.13,1.88),'Timber',.008)
    for z in (.48,1.57):
        g.box('Equipment rack rail',(0,.14,z),(1.9,.13,.14),'Door',.008)
    if weapons:
        for i,x in enumerate((-.55,0,.55)):
            school.sword('Displayed sword',(x,-.05,1.04),1.45,(i-1)*.12)
    else:
        for x in (-.48,.48):
            place(lambda:school.shield_emblem(0,0,0),(x,-.06,1.05),.25)


def stall(kind):
    for x in (-1.48,1.48):
        for y,z in ((-1.03,2.48),(1.03,2.85)):
            g.box('Stall post shoe',(x,y,.09),(.24,.24,.18),'Iron',.008)
            g.box('Stall load-bearing upright',(x,y,z/2),(.105,.105,z),'Timber',.008)
    for y,z in ((-1.03,2.48),(1.03,2.85)):
        m.rod('Stall transverse beam',(-1.65,y,z),(1.65,y,z),.1,'Timber')
    for x in (-1.48,1.48):
        m.rod('Stall awning bearer',(x,-1.03,2.48),(x,1.03,2.85),.075,'Timber')
    cloth={'Fish':'BlueCloth','Bread':'Canvas','Spice':'Ochre','Flower':'Leaf',
           'Butcher':'Crimson','Potion':'BlueCloth','Book':'Canvas','Weapon':'Crimson'}[kind]
    outline=[(-1.78,-1.3,2.46),(1.78,-1.3,2.46),(1.78,1.3,2.94),(-1.78,1.3,2.94)]
    verts=outline+[(x,y,z-.05) for x,y,z in outline]
    g.mesh('Supported canvas canopy',verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],cloth,.004)
    for i in range(7):
        x=-1.78+(i+.5)*3.56/7
        g.extrude('Scalloped canvas valance',[(x-.245,2.44),(x+.245,2.44),(x+.18,2.28),(x,2.23),(x-.18,2.28)],
                  -1.33,-1.29,'Canvas' if i%2 else cloth,.003)
    for x in (-1.22,1.22):
        g.box('Market counter foot',(x,-.68,.48),(.12,.65,.96),'Timber',.008)
    g.box('Solid display counter',(0,-.69,.99),(3.2,.96,.14),'Door',.01)
    g.box('Display counter front',(0,-1.15,.77),(3.2,.07,.4),'Door',.007)
    goods(kind)


def goods(kind):
    if kind=='Fish':
        for x in (-1.04,0,1.04):
            place(lambda:p.crate(True),(x,-.68,1.07),.75)
            for yy in (-.85,-.56):
                place(fish,(x,yy,1.3),.8)
    elif kind=='Bread':
        for x in (-1.04,0,1.04):
            place(empty_basket,(x,-.7,1.07),.75)
            for y in (-.84,-.59):place(bread,(x,y,1.35),.9)
    elif kind=='Spice':
        for i,x in enumerate((-1.15,-.58,0,.58,1.15)):
            place(lambda:p.bowl('Open spice jar',.22,.35,'Pottery'),(x,-.7,1.07))
            g.cylinder('Coloured spice mound',(x,-.7,1.41),.17,.03,['Ochre','Crimson','Leaf'][i%3],0,16)
    elif kind=='Flower':
        for x in (-1.05,0,1.05):
            place(lambda:p.bowl('Florist clay pot',.24,.35,'Pottery'),(x,-.65,1.07))
            for i in range(5):
                a=i*2*PI/5
                xx,yy=x+.12*math.cos(a),-.65+.12*math.sin(a)
                m.rod('Cut flower stem',(xx,yy,1.23),(xx,yy,1.95),.018,'Leaf')
                for j in range(5):
                    b=j*2*PI/5
                    c.ellipsoid('Flower petal',(xx+.07*math.cos(b),yy+.07*math.sin(b),1.95),(.07,.05,.025),'Crimson' if i%2 else 'Ochre')
    elif kind=='Butcher':
        for x in (-.8,.25):
            c.ellipsoid('Butcher cut roast',(x,-.7,1.22),(.36,.25,.15),'Meat')
            c.ellipsoid('Pale cut face',(x,-.92,1.25),(.23,.035,.09),'Fat')
        for x in (-.9,.9):
            m.rod('Hanging meat hook',(x,.75,2.56),(x,.75,2.0),.03,'Iron')
            c.ellipsoid('Suspended cured ham',(x,.75,1.78),(.25,.22,.37),'Meat')
            g.cylinder('Ham exposed bone',(x,.75,2.12),.07,.26,'Bone',.008,12)
    elif kind=='Potion':
        for row,y in enumerate((-.9,-.5)):
            for i,x in enumerate((-1.15,-.58,0,.58,1.15)):
                place(lambda i=i,row=row:bottle(['PotionBlue','PotionGreen','PotionPurple'][(i+row)%3]),(x,y,1.07),1.1)
    elif kind=='Book':
        for i,x in enumerate((-1.05,-.35,.35,1.05)):
            for layer in range(2+i%2):
                place(lambda i=i:book(['Banner','Door','BlueCloth','Leaf'][i]),(x,-.7,1.07+layer*.14),1.1)
    elif kind=='Weapon':
        for x in (-.9,0,.9):school.sword('Weapon stall sword',(x,-.7,1.55),.95,.6)
        place(lambda:school.shield_emblem(0,0,0),(0,.7,1.3),.36)


def wheel(x,y,z,radius=.62):
    c.torus('Iron wagon wheel rim',(x,y,z),radius,.065,(0,PI/2,0),'Iron')
    hub=g.cylinder('Wooden wheel hub',(x,y,z),.13,.22,'Timber',.008,16)
    hub.rotation_euler.y=PI/2
    for i in range(8):
        a=i*PI/4
        m.rod('Wooden wheel spoke',(x,y,z),(x,y+(radius-.05)*math.cos(a),z+(radius-.05)*math.sin(a)),.065,'Door')


def wagon(covered=False):
    for x in (-.8,.8):
        g.box('Underfloor wagon bearer',(x,0,.74),(.16,3.8,.22),'Timber',.009)
    for x in (-.96,-.64,-.32,0,.32,.64,.96):
        g.box('Wagon bed plank',(x,0,.92),(.3,3.5,.16),'Door',.009)
    for y in (-1.18,1.18):
        m.rod('Iron wagon axle',(-1.43,y,.66),(1.43,y,.66),.13,'Iron')
        for x in (-1.35,1.35):wheel(x,y,.66)
    for x in (-1.13,1.13):
        for y in (-1.68,1.68):
            g.box('Wagon corner stake',(x,y,1.35),(.12,.12,.9),'Timber',.008)
        for z in (1.15,1.39,1.63):
            g.box('Wagon side board',(x,0,z),(.11,3.5,.2),'Door',.007)
    for z in (1.15,1.39,1.63):
        g.box('Wagon tail board',(0,1.71,z),(2.22,.11,.2),'Door',.007)
    for x in (-.67,.67):
        m.rod('Wagon connected drawbar',(x,-1.65,.8),(x,-3.65,.65),.11,'Timber')
    if covered:
        rows=[-1.6,0,1.6]
        arc=[(-1.15*math.cos(i*PI/12),1.72+1.02*math.sin(i*PI/12)) for i in range(13)]
        for y in rows:
            m.curve_tube('Timber wagon canopy hoop',[(x,y,z) for x,z in arc],.045,'Timber')
        verts=[(x,y,z+.045) for y in (-1.6,1.6) for x,z in arc]
        faces=[(i,i+1,i+14,i+13) for i in range(12)]
        faces += [tuple(reversed(face)) for face in faces]
        g.mesh('Linen wagon weather cover',verts,faces,'Canvas')
    else:
        for x,y in [(-.55,.2),(.55,.65),(-.5,-.8)]:
            place(p.crate,(x,y,1.0),.7)


def wheelbarrow():
    wheel(0,-.95,.43,.39)
    for x in (-.42,.42):
        m.rod('Barrow shaft',(x,-.9,.52),(x,1.8,.85),.09,'Timber')
        m.rod('Barrow parking leg',(x,.65,.64),(x,.7,.04),.075,'Timber')
    g.box('Barrow wooden floor',(0,0,.62),(1.1,1.45,.12),'Door',.009)
    for x in (-.54,.54):
        g.box('Barrow raised side',(x,0,.87),(.09,1.5,.45),'Door',.009)
    for y in (-.7,.7):
        g.box('Barrow front and rear',(0,y,.87),(1.05,.09,.45),'Door',.009)


def market_scale():
    g.box('Balance scale foot',(0,0,.09),(.85,.65,.18),'Timber',.01)
    g.cylinder('Brass scale column',(0,0,.75),.065,1.4,'Brass',.006,16)
    m.rod('Balanced horizontal beam',(-.72,0,1.3),(.72,0,1.3),.045,'Brass')
    for x in (-.65,.65):
        place(lambda:p.bowl('Suspended brass scale pan',.28,.08,'Brass'),(x,0,.65))
        for y in (-.2,.2):m.rod('Scale pan suspension',(x,0,1.3),(x,y,.73),.016,'Iron')
    g.cylinder('Balance central finial',(0,0,1.52),.09,.16,'Brass',.006,16)


def pottery():
    profiles=[[(.22,0),(.34,.13),(.36,.5),(.2,.73),(.16,.86),(.2,.9),(.14,.9),(.12,.85)],
              [(.18,0),(.29,.12),(.3,.4),(.2,.57),(.16,.65),(.12,.65)]]
    for i,(x,y) in enumerate([(-.34,0),(.35,.17)]):
        place(lambda i=i:lathe('Earthenware storage jar',profiles[i],'Pottery'),(x,y,0))


def sack_stack():
    for x,y in [(-.38,-.25),(.38,-.25),(0,.35)]:
        place(p.sack,(x,y,0),.85)
    place(p.sack,(0,0,.72),.78,angle=.4)


def filled_container(kind):
    place(empty_basket if kind=='Bread' else lambda:p.crate(True))
    for i in range(4):
        x=-.25+(i%2)*.5
        y=-.19+(i//2)*.38
        recipe={'Bread':bread,'Fish':fish,'Vegetable':vegetable}[kind]
        place(recipe,(x,y,.29 if kind=='Bread' else .85),.75)


def rolled_fabric():
    for x,color in [(-.33,'Banner'),(0,'Canvas'),(.33,'BlueCloth')]:
        obj=g.cylinder('Rolled cloth bolt',(x,0,.55),.15,1.1,color,.008,20)
        obj.rotation_euler.x=PI/2
        tie=g.cylinder('Fabric binding tie',(x,-.05,.55),.155,.035,'Rope',0,16)
        tie.rotation_euler.x=PI/2
    g.box('Fabric roll storage board',(0,0,.05),(1.05,1.25,.1),'Door',.008)


def hanging_herbs():
    for x in (-1.05,1.05):
        g.box('Drying frame foot',(x,0,.08),(.28,.65,.16),'Timber',.009)
        g.box('Drying frame upright',(x,0,.9),(.1,.1,1.7),'Timber',.006)
    m.rod('Herb drying rail',(-1.1,0,1.72),(1.1,0,1.72),.07,'Door')
    for i,x in enumerate((-.7,-.23,.23,.7)):
        m.curve_tube('Tied herb suspension',[(x,0,1.75),(x,0,1.55),(x,.02,1.42)],.014,'Rope')
        place(lambda i=i:t.herb_plant(.65,i*.8),(x,0,1.45),angle=PI)
        for j in range(3):
            a=j*2*PI/3
            m.rod('Hanging herb stem',(x,0,1.43),(x+.14*math.cos(a),.1*math.sin(a),.85),.018,'Leaf')
            c.ellipsoid('Drying herb leaf',(x+.12*math.cos(a),.09*math.sin(a),1.03),(.09,.045,.17),'Herb')


def shop_sign():
    before=set(g.PARTS.objects)
    g.box('Sign wall mounting plate',(0,.38,1.24),(.18,.1,.48),'Iron',.006)
    m.rod('Sign cantilever',(0,.38,1.45),(0,-.65,1.45),.075,'Iron')
    m.rod('Sign cantilever brace',(0,.38,1.1),(0,-.45,1.45),.05,'Iron')
    for x in (-.4,.4):
        m.rod('Short sign suspension',(x,-.5,1.45),(x,-.5,1.1),.025,'Iron')
    g.box('Wooden hanging trade sign',(0,-.5,.74),(1.2,.12,.7),'Door',.025)
    for z in (.44,1.04):
        g.box('Trade sign frame',(0,-.58,z),(1.16,.04,.055),'Brass',.004)
    g.box('Merchant emblem vertical',(0,-.59,.74),(.045,.035,.4),'Brass',.004)
    g.box('Merchant emblem balance',(0,-.59,.85),(.55,.035,.045),'Brass',.004)
    for obj in set(g.PARTS.objects)-before:obj.location.z+=1.1
    g.box('Hanging sign timber post',(0,.38,1.4),(.16,.16,2.8),'Timber',.01)


def training_dummy():
    g.box('Training dummy broad foot',(0,0,.1),(1.3,1.0,.2),'Timber',.014)
    school.training_dummy(0,0)


def wash_tub():
    place(lambda:p.bowl('Wide wooden washing tub',.75,.43,'Door'))
    for z,radius in ((.08,.61),(.37,.73)):
        c.torus('Washing tub iron hoop',(0,0,z),radius,.025,(0,0,0),'Iron')
    g.cylinder('Washing water',(0,0,.32),.65,.025,'Water',0,32)
    g.box('Wash board',(0,.25,.52),(.35,.75,.08),'Timber',.008)


def brazier():
    for i in range(3):
        a=i*2*PI/3
        m.rod('Brazier iron foot',(.4*math.cos(a),.4*math.sin(a),.025),(.23*math.cos(a),.23*math.sin(a),.7),.07,'Iron')
    place(lambda:p.bowl('Iron brazier fire bowl',.5,.3,'Iron'),(0,0,.6))
    for x,y in [(-.18,0),(.12,.13),(.12,-.15)]:
        c.ellipsoid('Glowing charcoal',(x,y,.82),(.16,.12,.07),'Ember')


def hanging_lantern():
    t.lantern(0,0,1.0)


def watchtower():
    t.square_watchtower()
    bpy.context.view_layer.update()
    prefixes=('Carved lancet','Recessed window','Stone sill','Window center','Window transom')
    for obj in g.PARTS.objects:
        if obj.type!='MESH' or not obj.name.startswith(prefixes):continue
        verts=[obj.matrix_world@v.co for v in obj.data.vertices]
        if max(v.z for v in verts)<3.4 and max(v.y for v in verts)<-2:
            obj.location.x-=1.2
    r.door(0,-2.25,1.2,2.4,.15)
    g.box('Watchtower threshold',(0,-2.6,.1),(1.8,.7,.2),'StoneTrim',.012)
    g.box('Roof access hatch',(0,0,7.38),(1.0,1.3,.07),'Door',.01)
    for y in (-.4,.4):g.box('Hatch hinge',(.38,y,7.43),(.2,.065,.035),'Iron',.004)


def windmill():
    g.cylinder('Windmill octagonal foundation',(0,0,.2),2.95,.4,'StoneShade',.015,16)
    bpy.ops.mesh.primitive_cone_add(vertices=32,radius1=2.8,radius2=1.9,depth=7.6,location=(0,0,4.1))
    g.finish(bpy.context.object,'Tapered old masonry mill tower','Stone',.01)
    bpy.ops.mesh.primitive_cone_add(vertices=32,radius1=2.45,radius2=.035,depth=2.6,location=(0,0,9.2))
    g.finish(bpy.context.object,'Slate mill rotating cap','Roof',.008)
    g.box('Mill entrance stone portal',(0,-2.55,1.5),(1.95,.62,2.7),'StoneTrim',.014)
    r.door(0,-2.9,1.35,2.4,.15)
    r.lancet(0,-2.31,3.7,.65,1.45)
    m.pipe('Mill windshaft bearing',(0,-1.8,7.35),(0,-3.5,7.35),.22,'Iron')
    c.ellipsoid('Wooden sail hub',(0,-3.53,7.35),(.35,.3,.35),'Timber')
    for i in range(4):
        a=.4+i*PI/2
        m.rod('Mill sail structural spar',(0,-3.55,7.35),(5*math.cos(a),-3.55,7.35+5*math.sin(a)),.14,'Timber')
        outline=[(math.cos(a)*rr-math.sin(a)*tt,-3.59,7.35+math.sin(a)*rr+math.cos(a)*tt)
                 for rr,tt in [(1.0,0),(4.9,0),(4.9,.85),(1.0,.85)]]
        verts=outline+[(x,y-.035,z) for x,y,z in outline]
        g.mesh('Framed linen mill sail',verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'Canvas')
        for j in range(7):
            rr=1.1+j*.62
            m.rod('Sail transverse batten',(math.cos(a)*rr,-3.64,7.35+math.sin(a)*rr),
                  (math.cos(a)*rr-math.sin(a)*.85,-3.64,7.35+math.sin(a)*rr+math.cos(a)*.85),.04,'Door')
    for x in (-3.45,3.45):
        place(p.sack,(x,.6,0),1.1)


def watermill():
    g.box('Mill stone foundation',(0,0,.3),(7.4,9.4,.6),'StoneShade',.018)
    g.box('Mill plaster processing hall',(0,0,3.0),(6.8,9,4.8),'Plaster',.018)
    r.roof(7.7,9.9,5.4,2.65)
    for x in (-3.2,0,3.2):
        g.box('Front mill timber upright',(x,-4.54,3.0),(.22,.18,4.8),'Timber',.01)
    g.box('Mill horizontal front bearer',(0,-4.57,3.25),(6.7,.2,.23),'Timber',.01)
    r.door(0,-4.65,1.65,2.65,.3)
    for x in (-2.1,2.1):r.lancet(x,-4.55,1.7,.7,1.4)
    g.box('Mill loading step',(0,-4.95,.15),(2.5,1.0,.3),'StoneTrim',.01)
    for x in (4.35,5.45):
        c.torus('Old waterwheel timber rim',(x,0,2.7),2.45,.13,(0,PI/2,0),'Timber')
    m.pipe('Connected mill axle',(3.2,0,2.7),(6.5,0,2.7),.2,'Timber')
    for i in range(16):
        a=i*2*PI/16
        yy,zz=2.45*math.cos(a),2.7+2.45*math.sin(a)
        m.rod('Waterwheel structural spoke',(4.9,0,2.7),(4.9,yy,zz),.13,'Door')
        obj=g.box('Waterwheel radial paddle',(4.9,yy,zz),(1.25,.22,.6),'Door',.009)
        obj.rotation_euler.x=a-PI/2
    for x in (3.65,6.15):
        g.box('Stone millrace bank',(x,0,.25),(.3,12,.5),'Stone',.008)
    g.box('Millrace water',(4.9,0,.145),(2.2,12,.025),'Water',0)
    g.box('External axle bearing pier',(6.55,0,1.3),(.65,.85,2.6),'Stone',.012)
    place(p.sack,(-2.5,-5.3,0))


def market_hall():
    g.box('Market hall paved foundation',(0,0,.18),(18.2,13.6,.36),'StoneShade',.018)
    for x in (-8,-4,4,8):
        for y in (-5.9,5.9):
            g.box('Market hall post stone shoe',(x,y,.52),(.7,.7,.68),'StoneTrim',.01)
            g.box('Market hall load-bearing post',(x,y,3.25),(.32,.32,5.5),'Timber',.012)
    for x in (-8,8):
        for y in (-2,2):
            g.box('Side gallery supporting post',(x,y,3.25),(.32,.32,5.5),'Timber',.012)
    for y in (-5.9,5.9):
        g.box('Hall transverse roof bearer',(0,y,6.05),(16.6,.36,.36),'Timber',.012)
        for side in (-1,1):
            m.rod('Hall triangular truss',(side*8,y,6.15),(0,y,9.45),.22,'Timber')
        for x in (-8,-4,4,8):
            direction=1 if x<0 else -1
            m.rod('Connected gallery knee brace',(x,y,4.9),(x+direction*.8,y,5.95),.14,'Timber')
    for x in (-8,8):g.box('Hall longitudinal roof bearer',(x,0,6.05),(.36,12.2,.36),'Timber',.012)
    r.roof(19.0,14.0,6.25,3.5)
    g.box('Market rear counting room',(0,4.65,2.75),(14,2.2,4.8),'Plaster',.016)
    r.door(0,3.48,1.7,2.75,.36)
    for x in (-5,-2.7,2.7,5):r.lancet(x,3.49,1.6,.7,1.5)
    g.box('Market great trade emblem',(0,-6.17,7.35),(2.6,.15,1.8),'Banner',.04)
    m.rod('Emblem balance post',(0,-6.29,6.8),(0,-6.29,7.95),.075,'Brass')
    m.rod('Emblem balance arm',(-.85,-6.29,7.7),(.85,-6.29,7.7),.075,'Brass')
    for x in (-.75,.75):
        m.rod('Emblem scale suspension',(x,-6.3,7.7),(x,-6.3,7.1),.04,'Brass')
        g.box('Emblem scale pan',(x,-6.3,7.08),(.55,.08,.07),'Brass',.01)
    for x in (-1.0,1.0):
        for y in (1.8,3.6):
            g.box('Bell turret pillar',(x,y,9.9),(.17,.17,2.4),'Timber',.008)
    place(lambda:r.roof(2.8,2.7,11.1,1.0),(0,2.7,0))
    place(lambda:lathe('Market hanging bronze bell',[(.48,0),(.5,.08),(.3,.22),(.22,.7),(.08,.82)],'Brass'),(0,2.7,9.9))
    g.box('Bell suspension beam',(0,2.7,11.0),(2.1,.2,.2),'Timber',.009)
    for x in (-5.8,5.8):
        place(p.crate,(x,1.4,.36),.8)


def outhouse():
    g.box('Privy stone footing',(0,0,.1),(2.0,2.1,.2),'StoneShade',.012)
    for x in (-.84,.84):
        g.box('Privy side timber wall',(x,0,1.25),(.12,1.9,2.3),'Door',.01)
    g.box('Privy rear wall',(0,.88,1.25),(1.8,.12,2.3),'Door',.01)
    for x in (-.78,.78):
        g.box('Privy front door jamb',(x,-.9,1.25),(.16,.16,2.3),'Timber',.008)
    g.box('Privy boarded door',(0,-.93,1.18),(1.35,.1,2.0),'Door',.012)
    for x in (-.45,-.15,.15,.45):
        g.box('Privy door plank seam',(x,-.99,1.18),(.014,.015,1.96),'Timber',0)
    for z in (.48,1.75):
        g.box('Privy door batten',(0,-1.015,z),(1.23,.06,.09),'Timber',.006)
    g.box('Simple privy door latch',(.43,-1.06,1.1),(.2,.06,.06),'Iron',.004)
    t.shed_roof(2.1,2.25,2.45,2.75,'Roof')
    moon=[(.21*math.cos(i*PI/8),2.03+.21*math.sin(i*PI/8)) for i in range(9)]
    moon += [(.16*math.cos(i*PI/8),2.03+.13*math.sin(i*PI/8)) for i in reversed(range(9))]
    g.extrude('Crescent privy ventilation',moon,-1.002,-.995,'Window')
    for x in (-.91,.91):
        g.box('Side ventilation opening',(x,.3,2.16),(.018,.45,.1),'Window',0)


def extrusion(name, outline, x0, x1, material):
    count=len(outline)
    verts=[(x,y,z) for x in (x0,x1) for y,z in outline]
    faces=[tuple(reversed(range(count))),tuple(range(count,count*2))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    return g.mesh(name,verts,faces,material)


def bridge(stone=True):
    count=20 if stone else 14
    profile=[(-4+i*8/count,.38+math.sin(i*PI/count)*(1.55 if stone else .3)) for i in range(count+1)]
    if stone:
        outline=profile+[(y,z-.25) for y,z in reversed(profile)]
        extrusion('Masonry arch deck',outline,-1.7,1.7,'Stone')
        for side in (-1,1):
            rail=[(y,z+.78) for y,z in profile]+[(y,z) for y,z in reversed(profile)]
            extrusion('Continuous old bridge parapet',rail,side*1.9-.13,side*1.9+.13,'StoneTrim')
    else:
        for i in range(count):
            y0,z0=profile[i];y1,z1=profile[i+1]
            g.mesh('Fitted bridge deck board',[(-1.7,y0,z0),(1.7,y0,z0),(1.7,y1,z1),(-1.7,y1,z1)],
                   [(0,1,2,3),(3,2,1,0)],'Door')
            for x in (-1.25,1.25):m.rod('Underdeck timber girder',(x,y0,z0-.13),(x,y1,z1-.13),.2,'Timber')
        for i in range(0,count+1,2):
            y,z=profile[i]
            for x in (-1.7,1.7):g.box('Bridge handrail post',(x,y,z+.47),(.12,.12,.95),'Timber',.008)
        for x in (-1.7,1.7):
            for (y0,z0),(y1,z1) in zip(profile,profile[1:]):
                m.rod('Continuous timber handrail',(x,y0,z0+.92),(x,y1,z1+.92),.085,'Door')
    for y in (-3.85,3.85):
        g.box('Bridge bank bearing',(0,y,.09),(3.4,.3,.18),'StoneShade' if stone else 'Timber',.008)


def dock():
    for i in range(13):
        g.box('Individual dock floorboard',(0,-2.4+i*.4,.8),(3,.38,.2),'Door',.008)
    for x in (-1.14,1.14):
        g.box('Dock load-bearing stringer',(x,0,.64),(.16,5,.2),'Timber',.008)
        for y in (-2,2):
            g.cylinder('Driven dock timber pile',(x,y,.65),.16,1.3,'Bark',.008,12)
            c.torus('Mooring rope collar',(x,y,1.1),.17,.025,(0,0,0),'Rope')


def stairs(wood=False):
    if not wood:
        outline=[(0,0),(2.38,0)]
        for i in reversed(range(7)):
            outline += [((i+1)*.34,(i+1)*.18),(i*.34,(i+1)*.18)]
        extrusion('Seven old masonry steps',outline,-1,1,'Stone')
    else:
        for i in range(7):
            g.box('Wood stair tread',(0,(i+.5)*.34,(i+1)*.18-.05),(2,.35,.1),'Door',.008)
        for x in (-.78,.78):
            m.rod('Connected stair stringer',(x,.03,.04),(x,2.31,1.18),.16,'Timber')
            g.box('Stair upper bearing leg',(x,2.25,.6),(.15,.15,1.2),'Timber',.008)
        for x in (-1,1):
            for i in (0,3,6):
                g.box('Stair handrail support',(x,(i+.5)*.34,(i+1)*.18+.38),(.1,.1,.8),'Timber',.006)
            m.rod('Sloping old wood handrail',(x,.17,.98),(x,2.21,2.06),.075,'Door')


RECIPES = {
    'Windmill':windmill,'Watermill':watermill,'Market_Hall':market_hall,'Watchtower':watchtower,'Outhouse':outhouse,
    'Covered_Wagon':lambda:wagon(True),'Merchant_Wagon':wagon,'Wheelbarrow':wheelbarrow,
    'Market_Scale':market_scale,'Basket_Empty':empty_basket,'Pottery_Jars':pottery,'Sack_Stack':sack_stack,
    'Vegetable_Crate':lambda:filled_container('Vegetable'),'Fish_Crate':lambda:filled_container('Fish'),
    'Bread_Basket':lambda:filled_container('Bread'),'Rolled_Fabric':rolled_fabric,'Hanging_Herbs':hanging_herbs,
    'Shop_Sign':shop_sign,'Shield_Rack':lambda:rack(False),'Weapon_Rack':rack,'Training_Dummy':training_dummy,
    'Fountain':m.fountain,'Wash_Tub':wash_tub,'Street_Brazier':brazier,'Hanging_Lantern':hanging_lantern,
    'Stone_Bridge':bridge,'Wooden_Bridge':lambda:bridge(False),'Dock_Section':dock,
    'Stone_Stairs':stairs,'Wooden_Stairs':lambda:stairs(True),
}
for kind in ['Fish','Bread','Spice','Flower','Butcher','Potion','Book','Weapon']:
    RECIPES[kind+'_Stall'] = lambda kind=kind:stall(kind)
