import bpy
import importlib.util
import json
import math
import os
import sys
from types import SimpleNamespace
from mathutils import Vector, Matrix

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
SOURCE=os.path.join(ROOT,'ArtSource','Blender','FantasyTownProps.blend')
REVIEW=os.path.join(ROOT,'docs','Art','FantasyTownProps')
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('civic_props',os.path.join(os.path.dirname(__file__),'generate_fantasy_civic_facilities.py'))
m=importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
c,t,r,g=m.c,m.t,m.r,m.g
r.REVIEW=REVIEW
ASSETS={}
RECORDS=[]
PREVIEWS=[]


def bowl(name, radius, height, material):
    sides=24
    rings=((radius*0.77,0),(radius,height),(radius-0.055,height),(radius*0.77-0.055,0.085))
    vertices=[(rr*math.cos(math.tau*i/sides),rr*math.sin(math.tau*i/sides),z) for rr,z in rings for i in range(sides)]
    faces=[tuple(range(sides-1,-1,-1)),tuple(range(3*sides,4*sides))]
    for row in range(3):
        faces += [(row*sides+i,row*sides+(i+1)%sides,(row+1)*sides+(i+1)%sides,(row+1)*sides+i) for i in range(sides)]
    return g.mesh(name,vertices,faces,material)


def road(ports):
    g.box('Road base below grade',(0,0,-0.09),(8,8,0.18),'Cobble',0)
    coords=(-4,-2,2,4)
    walk=set()
    for ix in range(3):
        for iy in range(3):
            is_road=(ix==1 and iy==1) or (ix==1 and iy==2 and 'N' in ports) or (ix==1 and iy==0 and 'S' in ports) or (ix==2 and iy==1 and 'E' in ports) or (ix==0 and iy==1 and 'W' in ports)
            if not is_road:
                walk.add((ix,iy))
                x0,x1,y0,y1=coords[ix],coords[ix+1],coords[iy],coords[iy+1]
                g.box('Raised paving',( (x0+x1)/2,(y0+y1)/2,0.06),(x1-x0,y1-y0,0.12),'PavingGrid',0)
    for ix,iy in walk:
        for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
            nx,ny=ix+dx,iy+dy
            if 0<=nx<3 and 0<=ny<3 and (nx,ny) not in walk:
                x0,x1,y0,y1=coords[ix],coords[ix+1],coords[iy],coords[iy+1]
                if dx:
                    x=(x0+0.055) if dx<0 else (x1-0.055)
                    g.box('Road curb',(x,(y0+y1)/2,0.127),(0.11,y1-y0,0.025),'StoneTrim',0)
                else:
                    y=(y0+0.055) if dy<0 else (y1-0.055)
                    g.box('Road curb',((x0+x1)/2,y,0.127),(x1-x0,0.11,0.025),'StoneTrim',0)


def paving_tile():
    g.box('Plot paving tile',(0,0,-0.03),(8,8,0.3),'PavingGrid',0)


def fence_post():
    g.box('Fence foot',(0.14,0,0.1),(0.28,0.28,0.2),'StoneShade',0.007)
    g.box('Oak fence post',(0.14,0,0.76),(0.18,0.18,1.36),'Timber',0.009)
    bpy.ops.mesh.primitive_cone_add(vertices=4,radius1=0.17,radius2=0.02,depth=0.18,location=(0.14,0,1.52),rotation=(0,0,math.pi/4))
    g.finish(bpy.context.object,'Fence pointed cap','Door',0.005)


def fence_panel(gate=False):
    fence_post()
    for z in (0.48,1.14):
        g.box('Gate rail' if gate else 'Fence rail',(2.1,0,z),(3.8,0.105,0.13),'Door',0.008)
    if gate:
        for i in range(10):
            g.box('Gate upright slat',(0.48+i*0.35,-0.04,0.82),(0.16,0.11,1.04),'Door',0.009)
        m.rod('Gate diagonal brace',(0.34,-0.135,0.36),(3.87,-0.135,1.22),0.11,'Timber')
        for z in (0.48,1.14):
            g.box('Gate iron hinge',(0.45,-0.135,z),(0.48,0.04,0.065),'Iron',0.004)
        g.box('Gate iron latch',(3.77,-0.15,0.91),(0.3,0.04,0.07),'Iron',0.004)
    else:
        for x in (0.76,1.47,2.18,2.89,3.6):
            g.box('Fence picket',(x,-0.022,0.88),(0.12,0.14,1.3),'Door',0.008)


def stone_wall(corner=False):
    if not corner:
        g.box('Low garden masonry',(2,0,0.57),(4,0.4,1.14),'Stone',0)
        g.box('Garden wall coping',(2,0,1.21),(4,0.53,0.14),'StoneTrim',0)
    else:
        def layer(name,width,bottom,top,material):
            p=[(-width/2,-width/2),(4,-width/2),(4,width/2),(width/2,width/2),(width/2,4),(-width/2,4)]
            v=[(x,y,z) for z in (bottom,top) for x,y in p]
            f=[tuple(range(5,-1,-1)),tuple(range(6,12))]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)]
            g.mesh(name,v,f,material)
        layer('L shaped garden wall',0.4,0,1.14,'Stone')
        layer('L shaped coping',0.53,1.14,1.28,'StoneTrim')


def crate(open_top=False):
    g.box('Crate bottom',(0,0,0.06),(1.12,0.88,0.12),'Door',0.008)
    for x in (-0.5,0.5):
        for y in (-0.38,0.38):
            g.box('Crate corner',(x,y,0.52),(0.115,0.115,0.96),'Timber',0.007)
    for z in (0.24,0.47,0.7,0.93):
        for y in (-0.42,0.42):
            g.box('Crate long plank',(0,y,z),(1.12,0.08,0.2),'Door',0.006)
        for x in (-0.52,0.52):
            g.box('Crate end plank',(x,0,z),(0.08,0.77,0.2),'Door',0.006)
    if not open_top:
        for x in (-0.42,-0.14,0.14,0.42):
            g.box('Crate lid slat',(x,0,1.08),(0.265,0.88,0.1),'Door',0.006)
        for y in (-0.27,0.27):
            g.box('Crate lid batten',(0,y,1.16),(1.12,0.08,0.07),'Timber',0.005)
    m.rod('Crate diagonal brace',(-0.45,-0.475,0.17),(0.44,-0.475,0.99),0.073,'Timber')


def sack():
    rings=((0,0.28),(0.12,0.37),(0.48,0.42),(0.74,0.29),(0.86,0.14),(0.95,0.19))
    n=16
    v=[(rr*math.cos(math.tau*i/n),rr*math.sin(math.tau*i/n),z) for z,rr in rings for i in range(n)]
    f=[tuple(range(n-1,-1,-1)),tuple(range((len(rings)-1)*n,len(rings)*n))]
    f += [(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(rings)-1) for i in range(n)]
    g.mesh('Full linen grain sack',v,f,'Sacking',0.008)
    c.torus('Sack rope tie',(0,0,0.86),0.148,0.024,(0,0,0),'Rope')
    m.curve_tube('Sack tied cord',[(0.14,-0.02,0.87),(0.22,-0.03,0.8),(0.2,-0.04,0.7)],0.014,'Rope')


def basket():
    bowl('Wicker produce basket',0.44,0.38,'Basket')
    for z,rr in ((0.08,0.35),(0.18,0.38),(0.29,0.414),(0.39,0.44)):
        c.torus('Woven basket hoop',(0,0,z),rr,0.022,(0,0,0),'Rope')
    for i in range(16):
        a=math.tau*i/16
        m.rod('Basket wicker stave',(0.34*math.cos(a),0.34*math.sin(a),0.02),
              (0.44*math.cos(a),0.44*math.sin(a),0.38),0.028,'Basket')
    for i in range(9):
        a=math.tau*i/9
        rr=0.25 if i<7 else 0.11
        c.ellipsoid('Market apple',(rr*math.cos(a),rr*math.sin(a),0.36+(i%3)*0.033),(0.105,0.1,0.1),'Apple' if i%3 else 'Leaf')


def market(cloth=False):
    for x in (-1.48,1.48):
        for y,z in ((-1.03,2.48),(1.03,2.85)):
            g.box('Stall post shoe',(x,y,0.09),(0.24,0.24,0.18),'Iron',0.008)
            g.box('Stall timber upright',(x,y,z/2),(0.105,0.105,z),'Timber',0.008)
    for y,z in ((-1.03,2.48),(1.03,2.85)):
        m.rod('Stall crossbeam',(-1.65,y,z),(1.65,y,z),0.1,'Timber')
    for x in (-1.48,1.48):
        m.rod('Stall awning brace',(x,-1.03,2.48),(x,1.03,2.85),0.075,'Timber')
    vertices=[(x,y,z+dz) for dz in (0,-0.05) for x,y,z in ((-1.78,-1.3,2.46),(1.78,-1.3,2.46),(1.78,1.3,2.94),(-1.78,1.3,2.94))]
    g.mesh('Merchant fabric awning',vertices,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'BlueCloth' if cloth else 'Crimson',0.005)
    for i in range(7):
        x=-1.78+(i+0.5)*3.56/7
        g.extrude('Canvas scalloped edge',[(x-0.245,2.44),(x+0.245,2.44),(x+0.18,2.28),(x,2.23),(x-0.18,2.28)],-1.33,-1.29,'Canvas' if i%2==0 else ('BlueCloth' if cloth else 'Crimson'),0.003)
    for x in (-1.22,1.22):
        g.box('Market counter leg',(x,-0.68,0.48),(0.12,0.65,0.96),'Timber',0.008)
    g.box('Market wooden counter',(0,-0.69,0.99),(3.2,0.96,0.14),'Door',0.01)
    g.box('Counter front board',(0,-1.15,0.77),(3.2,0.07,0.4),'Door',0.007)
    if cloth:
        for i,(x,color) in enumerate(((-0.96,'Crimson'),(-0.48,'Canvas'),(0,'BlueCloth'),(0.48,'Ochre'),(0.96,'Leaf'))):
            obj=g.cylinder('Rolled cloth bolt',(x,-0.71,1.19),0.13,0.69,color,0.008,16)
            obj.rotation_euler.x=math.pi/2
            g.cylinder('Cloth bolt tie',(x,-0.72,1.19),0.135,0.038,'Rope',0,16).rotation_euler.x=math.pi/2
        for x,color in ((-0.65,'Crimson'),(0.4,'BlueCloth')):
            g.box('Hanging fabric sample',(x,0.94,1.63),(0.82,0.035,1.69),color,0.005)
            m.rod('Fabric display pole',(x-0.5,0.94,2.48),(x+0.5,0.94,2.48),0.046,'Timber')
    else:
        for x in (-1.04,0,1.04):
            r.shifted(basket,(x,-0.7,1.07))
        for x in (-0.8,0.6):
            r.shifted(sack,(x,0.59,0))


def handcart():
    for side in (-1,1):
        x=side*1.01
        c.torus('Handcart iron wheel rim',(x,0,0.69),0.62,0.07,(0,math.pi/2,0),'Iron')
        g.cylinder('Handcart wheel hub',(x,0,0.69),0.125,0.24,'Timber',0.008,16).rotation_euler.y=math.pi/2
        for i in range(8):
            a=math.tau*i/8
            m.rod('Cart wooden wheel spoke',(x,0,0.69),(x,0.57*math.cos(a),0.69+0.57*math.sin(a)),0.065,'Door')
    m.rod('Handcart axle',(-1.15,0,0.69),(1.15,0,0.69),0.14,'Iron')
    for x in (-0.63,-0.31,0,0.31,0.63):
        g.box('Cart bed floor',(x,0.12,0.88),(0.3,2.24,0.14),'Door',0.007)
    for z in (1.04,1.25,1.46):
        for x in (-0.82,0.82):
            g.box('Cart side plank',(x,0.12,z),(0.1,2.28,0.18),'Door',0.007)
        g.box('Cart tail plank',(0,1.24,z),(1.68,0.1,0.18),'Door',0.007)
    for x in (-0.81,0.81):
        for y in (-0.93,1.18):
            g.box('Cart corner stake',(x,y,1.17),(0.115,0.115,0.92),'Timber',0.006)
    for x in (-0.57,0.57):
        m.rod('Cart long handle',(x,-0.86,0.83),(x,-3.12,1.03),0.095,'Timber')
    g.box('Cart parking prop',(0,-0.82,0.39),(0.16,0.16,0.78),'Timber',0.007)


def bucket():
    bowl('Wood bucket staves',0.3,0.54,'Door')
    for z,rr in ((0.08,0.243),(0.45,0.29)):
        c.torus('Bucket iron band',(0,0,z),rr,0.026,(0,0,0),'Iron')
    m.curve_tube('Bucket arched iron handle',[(-0.29,0,0.44),(-0.24,0,0.91),(0,0,1.05),(0.24,0,0.91),(0.29,0,0.44)],0.022,'Iron')


def well():
    g.cylinder('Well octagonal footing',(0,0,0.12),1.38,0.24,'StoneShade',0.01,8)
    m.ring('Well masonry ring',0.8,1.12,0.2,1.14,'Stone')
    m.ring('Well coping stones',0.77,1.22,1.12,1.32,'StoneTrim')
    g.cylinder('Well dark water',(0,0,0.31),0.79,0.04,'Water',0,48)
    for x in (-1.21,1.21):
        g.box('Well canopy post foot',(x,0,0.14),(0.34,0.36,0.28),'StoneTrim',0.012)
        g.box('Well oak upright',(x,0,1.75),(0.17,0.17,3.4),'Timber',0.01)
    g.cylinder('Well windlass axle',(0,0,2.1),0.14,2.72,'Door',0.008,24).rotation_euler.y=math.pi/2
    for x in (-0.1,0,0.1):
        c.torus('Windlass wrapped rope',(x,0,2.1),0.156,0.025,(0,math.pi/2,0),'Rope')
    m.rod('Well descending rope',(0,-0.17,2.1),(0,-0.17,0.99),0.025,'Rope')
    r.shifted(bucket,(0,-0.17,0.3))
    m.rod('Windlass crank',(1.44,0,2.1),(1.44,0,1.68),0.065,'Iron')
    m.rod('Windlass crank grip',(1.44,0,1.68),(1.74,0,1.68),0.076,'Timber')
    r.roof(3.23,2.71,3.38,1.05)


def trough():
    g.box('Trough stone base',(0,0,0.11),(2.8,0.99,0.22),'StoneShade',0.012)
    for y in (-0.43,0.43):
        g.box('Trough long stone wall',(0,y,0.43),(2.8,0.17,0.64),'Stone',0.01)
    for x in (-1.3,1.3):
        g.box('Trough end stone',(x,0,0.43),(0.2,0.73,0.64),'Stone',0.01)
    g.box('Water in trough',(0,0,0.58),(2.42,0.66,0.04),'Water',0.003)


def firewood():
    for x in (-1.27,1.27):
        for y in (-0.39,0.39):
            g.box('Firewood rack post',(x,y,0.78),(0.12,0.12,1.56),'Timber',0.008)
        g.box('Firewood rack runner',(x,0,0.08),(0.2,1.04,0.16),'Timber',0.007)
    for row in range(4):
        count=6-row
        for i in range(count):
            x=(i-(count-1)/2)*0.35
            z=0.33+row*0.31
            obj=g.cylinder('Stacked firewood bark',(x,0,z),0.174,1.03,'Bark',0.006,12)
            obj.rotation_euler.x=math.pi/2
            for y in (-0.523,0.523):
                obj=g.cylinder('Cut log end',(x,y,z),0.145,0.014,'CutWood',0,12)
                obj.rotation_euler.x=math.pi/2
    r.shifted(lambda:t.shed_roof(2.98,1.28,1.65,1.81),(0,0,0))


def hay():
    g.box('Bound hay bale',(0,0,0.4),(1.48,0.88,0.8),'Hay',0.09)
    for x in (-0.43,0.43):
        for y in (-0.45,0.45):
            g.box('Hay rope binding',(x,y,0.4),(0.035,0.025,0.67),'Rope',0.003)
        for z in (0.05,0.8):
            g.box('Hay upper and lower tie',(x,0,z),(0.035,0.82,0.025),'Rope',0.003)


def clothesline():
    for x in (-2.05,2.05):
        g.box('Clothesline post foot',(x,0,0.11),(0.28,0.28,0.22),'StoneShade',0.009)
        g.box('Clothesline oak pole',(x,0,1.24),(0.085,0.085,2.48),'Timber',0.008)
    m.curve_tube('Sagging washing rope',[(-2.05,0,2.4),(0,0,2.15),(2.05,0,2.4)],0.017,'Rope')
    for x,color,h in ((-1.19,'Canvas',1.04),(0,'BlueCloth',0.9),(1.17,'Canvas',1.11)):
        top=2.15+0.25*(x/2.05)**2
        n=8
        vertices=[(x-0.43+0.86*i/n,0.03*math.cos(i*math.pi)+dy,z)
                  for dy in (-0.012,0.012) for z in (top-h,top) for i in range(n+1)]
        stride=n+1
        faces=[]
        for i in range(n):
            faces += [(i,i+1,stride+i+1,stride+i),(2*stride+i,3*stride+i,3*stride+i+1,2*stride+i+1),
                      (i,2*stride+i,2*stride+i+1,i+1),(stride+i,stride+i+1,3*stride+i+1,3*stride+i)]
        faces += [(0,stride,3*stride,2*stride),(n,2*stride+n,3*stride+n,stride+n)]
        g.mesh('Hanging washed linen',vertices,faces,color)
        for dx in (-0.31,0.31):
            g.box('Wooden clothespin',(x+dx,-0.027,top+0.035),(0.039,0.053,0.15),'Door',0.005)


def signpost():
    g.box('Signpost stone socket',(0,0,0.16),(0.46,0.46,0.32),'StoneShade',0.014)
    g.box('Road sign oak post',(0,0,1.47),(0.16,0.16,2.94),'Timber',0.008)
    for z,direction in ((2.14,1),(2.67,-1)):
        outline=[(-0.63,z-0.14),(0.65,z-0.14),(0.9,z),(0.65,z+0.14),(-0.63,z+0.14)]
        g.extrude('Directional sign board',[(x*direction,zz) for x,zz in outline],-0.18,-0.09,'Door',0.008)
        for dz in (-0.05,0.05):
            g.box('Sign carved lettering',(0,-0.192,z+dz),(0.74,0.018,0.017),'Ivory',0)


def noticeboard():
    for x in (-1.06,1.06):
        g.box('Notice board foot',(x,0,0.12),(0.34,0.48,0.24),'StoneShade',0.012)
        g.box('Notice board oak post',(x,0,1.35),(0.16,0.18,2.7),'Timber',0.008)
    g.box('Notice board back',(0,0,1.79),(2.16,0.14,1.46),'Door',0.009)
    for z in (1.07,2.53):
        g.box('Notice board frame',(0,-0.055,z),(2.32,0.14,0.11),'Timber',0.007)
    for x,z,w,h in ((-0.61,1.95,0.45,0.7),(0,1.81,0.48,0.78),(0.63,2.02,0.43,0.56)):
        g.box('Parchment notice',(x,-0.082,z),(w,0.016,h),'Canvas',0)
        for row in range(4):
            g.box('Notice ink line',(x,-0.096,z+h*0.19-row*0.1),(w*0.7,0.01,0.016),'Ink',0)
        g.cylinder('Notice wax seal',(x+w*0.26,-0.103,z-h*0.3),0.05,0.014,'Crimson',0,12).rotation_euler.x=math.pi/2
    t.shed_roof(2.65,0.86,2.73,2.88)


def anvil():
    g.cylinder('Anvil oak stump',(0,0,0.32),0.46,0.64,'Bark',0.013,14)
    g.cylinder('Stump end grain',(0,0,0.649),0.43,0.022,'CutWood',0,14)
    g.box('Anvil foot',(0,0,0.74),(0.85,0.46,0.17),'Iron',0.016)
    g.box('Anvil waist',(0,0,0.94),(0.42,0.33,0.28),'Iron',0.02)
    g.box('Anvil working face',(-0.04,0,1.15),(1.04,0.47,0.2),'Iron',0.016)
    bpy.ops.mesh.primitive_cone_add(vertices=12,radius1=0.18,radius2=0.016,depth=0.56,location=(0.72,0,1.12),rotation=(0,math.pi/2,0))
    g.finish(bpy.context.object,'Anvil tapered horn','Iron',0.004)
    m.rod('Smith hammer handle',(-0.38,-0.1,1.29),(-0.01,0.17,1.29),0.045,'Door')
    g.box('Smith hammer head',(-0.39,-0.11,1.34),(0.13,0.24,0.16),'Iron',0.01)


def forge():
    g.box('Forge stone foundation',(0,0,0.12),(2.06,1.57,0.24),'StoneShade',0.014)
    for x in (-0.77,0.77):
        g.box('Forge masonry leg',(x,0,0.65),(0.48,1.3,1.06),'Stone',0.01)
    g.box('Forge hearth slab',(0,0,1.2),(2.06,1.57,0.22),'StoneTrim',0.012)
    g.box('Forge charcoal bed',(0,0.18,1.33),(1.27,0.88,0.08),'Coal',0.016)
    for x,y in ((-0.3,0.1),(0.05,0),(0.34,0.27),(-0.14,0.43)):
        c.ellipsoid('Banked glowing charcoal',(x,y,1.41),(0.13,0.11,0.07),'Ember')
    for x in (-0.78,0.78):
        g.box('Forge chimney pillar',(x,0.58,1.97),(0.36,0.32,1.38),'Stone',0.01)
    g.box('Forge back wall',(0,0.67,1.66),(1.28,0.17,0.74),'Stone',0.008)
    g.mesh('Forge tapered smoke hood',[(-1,-0.45,2.55),(1,-0.45,2.55),(1,0.8,2.55),(-1,0.8,2.55),
        (-0.35,0.13,3.12),(0.35,0.13,3.12),(0.35,0.8,3.12),(-0.35,0.8,3.12)],
        [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'Iron',0.01)
    g.box('Forge chimney stack',(0,0.47,3.57),(0.68,0.67,0.94),'Stone',0.008)
    g.box('Chimney black throat',(0,0.47,4.048),(0.49,0.48,0.018),'Coal',0)
    for x in (-0.35,0.35):
        g.box('Chimney cap side',(x,0.47,4.06),(0.16,0.87,0.14),'StoneTrim',0.005)
    for y in (0.115,0.825):
        g.box('Chimney cap end',(0,y,4.06),(0.54,0.16,0.14),'StoneTrim',0.005)


def drain():
    for x in (-0.54,0.54):
        g.box('Drain outer rim',(x,0,0.033),(0.09,0.76,0.066),'Iron',0.005)
    for y in (-0.335,0.335):
        g.box('Drain rim end',(0,y,0.033),(0.99,0.09,0.066),'Iron',0.005)
    for x in (-0.4,-0.2,0,0.2,0.4):
        g.box('Drain transverse bar',(x,0,0.027),(0.038,0.6,0.048),'Iron',0.003)


def bollard():
    g.cylinder('Bollard stone foot',(0,0,0.09),0.26,0.18,'StoneShade',0.008,8)
    g.cylinder('Iron street bollard',(0,0,0.55),0.12,0.94,'Iron',0.009,12)
    g.cylinder('Bollard bronze collar',(0,0,0.92),0.153,0.07,'Brass',0.004,16)
    c.ellipsoid('Rounded bollard head',(0,0,1.04),(0.155,0.155,0.14),'Iron')


def materials():
    m.materials()
    for name,color,kind in [('Sacking','A18A64','plain'),('Rope','89714A','plain'),('Basket','806041','wood'),
        ('Apple','963D2D','plain'),('Leaf','69764B','plain'),('BlueCloth','415663','plain'),
        ('Crimson','743539','plain'),('Canvas','D4C4A4','plain'),('Ochre','B38A45','plain'),
        ('Bark','4B3A29','wood'),('CutWood','AB855A','wood'),('Hay','A99858','wood'),
        ('Coal','282628','stone'),('Ember','B95522','plain')]:
        g.MATERIALS[name]=r.surface(name,color,kind)
    mat=g.MATERIALS['PavingGrid'].copy()
    mat.name='Fantasy_TownCobble'
    brick=next(n for n in mat.node_tree.nodes if n.type=='TEX_BRICK')
    brick.inputs['Brick Width'].default_value=0.44
    brick.inputs['Row Height'].default_value=0.31
    for key,color in [('Color1','777A76'),('Color2','93918A'),('Mortar','565A54')]:
        brick.inputs[key].default_value=(*r.rgb(color),1)
    g.MATERIALS['Cobble']=mat


def specifications():
    return [
        ('Road_Straight','直線道路','Roads',lambda:road('NS'),{'ports':'NS'}),
        ('Road_Corner','曲がり道路','Roads',lambda:road('NE'),{'ports':'NE'}),
        ('Road_T','T字路','Roads',lambda:road('NEW'),{'ports':'NEW'}),
        ('Road_Cross','十字路','Roads',lambda:road('NSEW'),{'ports':'NSEW'}),
        ('Road_End','行き止まり','Roads',lambda:road('S'),{'ports':'S'}),
        ('Paved_Plot','敷地舗装','Roads',paving_tile,{}),
        ('Fence_Panel','木柵','Boundaries',fence_panel,{'pitch_m':4}),
        ('Fence_Post','終端柱','Boundaries',fence_post,{}),
        ('Fence_Gate','木門','Boundaries',lambda:fence_panel(True),{'pitch_m':4}),
        ('Stone_Wall','低い石塀','Boundaries',stone_wall,{'pitch_m':4}),
        ('Stone_Wall_Corner','石塀の角','Boundaries',lambda:stone_wall(True),{'pitch_m':4}),
        ('Produce_Stall','青果露店','Market',market,{}),
        ('Cloth_Stall','布露店','Market',lambda:market(True),{}),
        ('Handcart','荷車','Market',handcart,{}),
        ('Crate_Closed','木箱・蓋付き','Market',crate,{}),
        ('Crate_Open','木箱・開放','Market',lambda:crate(True),{}),
        ('Grain_Sack','麻袋','Market',sack,{}),
        ('Produce_Basket','青果かご','Market',basket,{}),
        ('Barrel','樽','Market',t.barrel,{'origin':'existing tavern prop'}),
        ('Well','井戸','Life',well,{}),('Water_Trough','水槽','Life',trough,{}),
        ('Firewood_Rack','薪置き場','Life',firewood,{}),('Hay_Bale','干し草','Life',hay,{}),
        ('Clothesline','物干し','Life',clothesline,{}),
        ('Signpost','道標','Civic',signpost,{}),('Noticeboard','掲示板','Civic',noticeboard,{}),
        ('Anvil','金床','Civic',anvil,{}),('Forge','小型の炉','Civic',forge,{}),
        ('Bench','ベンチ','Civic',m.bench,{'origin':'existing plaza prop'}),
        ('Streetlamp','街灯','Civic',m.plaza_lamp,{'origin':'existing plaza prop'}),
        ('Planter','植え込み','Civic',m.planter,{'origin':'existing plaza prop'}),
        ('Drain_Grate','排水格子','Details',drain,{}),('Bollard','車止め','Details',bollard,{}),
        ('Bucket','木桶','Details',bucket,{})]


def object_matrix(obj):
    return object_matrix(obj.parent)@obj.matrix_parent_inverse@obj.matrix_basis if obj.parent else obj.matrix_basis


def bounds(collection):
    return [object_matrix(obj)@v.co for obj in collection.all_objects if obj.type=='MESH' for v in obj.data.vertices]


def instance(scene,name,position=(0,0,0),angle=0):
    obj=bpy.data.objects.new(name+' placement',None)
    obj.instance_type='COLLECTION'
    obj.instance_collection=ASSETS[name]
    scene.collection.objects.link(obj)
    obj.location=position
    obj.rotation_euler.z=math.radians(angle)
    obj['asset_name']=name
    return obj


def stage_instances(scene,filename,camera,size):
    bpy.context.window.scene=scene
    bpy.context.view_layer.update()
    points=[]
    for obj in scene.objects:
        if obj.instance_type=='COLLECTION':
            transform=object_matrix(obj)@Matrix.Translation(-obj.instance_collection.instance_offset)
            points += [transform@p for p in bounds(obj.instance_collection)]
    proxy=SimpleNamespace(type='MESH',matrix_world=Matrix.Identity(4),bound_box=points)
    r.stage(scene,[proxy],camera,(0,0,2),filename,size)
    scene.view_settings.exposure=0.35
    PREVIEWS.append(scene)


def frame_scene(scene):
    bpy.context.window.scene=scene
    bpy.context.view_layer.update()
    inverse=scene.camera.matrix_basis.inverted()
    points=[]
    for obj in scene.objects:
        if obj.instance_type=='COLLECTION':
            transform=inverse@object_matrix(obj)@Matrix.Translation(-obj.instance_collection.instance_offset)
            points.extend(transform@p for p in bounds(obj.instance_collection))
    left,right=min(p.x for p in points),max(p.x for p in points)
    bottom,top=min(p.y for p in points),max(p.y for p in points)
    scene.camera.location+=scene.camera.rotation_euler.to_quaternion()@Vector(((left+right)/2,(bottom+top)/2,0))
    scene.camera.data.ortho_scale=max(right-left,(top-bottom)*scene.render.resolution_x/scene.render.resolution_y)*1.12


def category_scenes():
    for index,(category,spacing,columns) in enumerate([('Roads',10,3),('Boundaries',6,3),('Market',5,3),('Life',6,3),('Civic',5,4),('Details',2.7,3)],1):
        scene=bpy.data.scenes.new(f'{index:02d} {category}')
        names=[rec['name'] for rec in RECORDS if rec['category']==category]
        rows=math.ceil(len(names)/columns)
        for i,name in enumerate(names):
            x=(i%columns-(columns-1)/2)*spacing
            y=((rows-1)/2-i//columns)*spacing
            if category=='Boundaries':
                x-=2
            instance(scene,name,(x,y,0))
        stage_instances(scene,category+'_Preview.png',(13,-25,25),(2000,1400))


def load_existing(filename,names):
    path=os.path.join(ROOT,'ArtSource','Blender',filename)
    with bpy.data.libraries.load(path,link=False) as (source,target):
        assert all(name in source.collections for name in names),(filename,names)
        target.collections=list(names)
    for name,collection in zip(names,target.collections):
        ASSETS[name]=collection


def town_scene():
    load_existing('KingdomBuildings_RealisticFantasy.blend',['Fantasy_House','Royal_Castle'])
    load_existing('FantasyTownFacilities.blend',['Tavern','Clinic','Guardhouse'])
    load_existing('FantasyCivicFacilities.blend',['Plaza','Library'])
    load_existing('FantasyNightlifeBuildings.blend',['CrimsonRowhouse','VelvetTerrace'])
    scene=bpy.data.scenes.new('00 Town Example')
    occupied={(i,0) for i in range(-4,5)}|{(0,j) for j in range(-4,4)}|{(i,-4) for i in range(-4,5)}|{(i,2) for i in range(-3,4)}|{(-3,j) for j in range(0,3)}|{(3,j) for j in range(0,3)}
    direction={'N':(0,1),'E':(1,0),'S':(0,-1),'W':(-1,0)}
    patterns=[(rec['name'],set(rec['ports'])) for rec in RECORDS if rec.get('ports')]
    def rotated(ports,turn):
        sequence='NESW'
        return {sequence[(sequence.index(p)-turn)%4] for p in ports}
    for i,j in sorted(occupied):
        ports={p for p,(dx,dy) in direction.items() if (i+dx,j+dy) in occupied}
        for name,base in patterns:
            match=next((turn for turn in range(4) if rotated(base,turn)==ports),None)
            if match is not None:
                instance(scene,name,(i*8,j*8,0),match*90)
                break
        else:
            raise ValueError(('road topology',i,j,ports))
    for i in (-2,-1,1,2):
        instance(scene,'Paved_Plot',(i*8,8,0))
    instance(scene,'Plaza',(-33,30,0))
    instance(scene,'Well',(-13,8,0.12))
    for x in (-18,-8):
        instance(scene,'Bench',(x,8,0.12),90)
        instance(scene,'Planter',(x,12,0.12))
    instance(scene,'Produce_Stall',(7,9,0.12))
    instance(scene,'Cloth_Stall',(13,9,0.12))
    instance(scene,'Produce_Stall',(19,9,0.12))
    instance(scene,'Well',(11,5.8,0.12))
    for name,pos,angle in [('Handcart',(18.5,5.7,0.12),90),('Barrel',(6,6,0.12),0),('Grain_Sack',(5,6,0.12),0),
        ('Crate_Closed',(15,10.5,0.12),0),('Crate_Open',(16.3,10.5,0.12),0),('Produce_Basket',(15,9.4,0.12),0),
        ('Noticeboard',(6,13.5,0.12),0),('Signpost',(3,3,0.12),0)]:
        instance(scene,name,pos,angle)
    instance(scene,'Royal_Castle',(0,40,0))
    for x in (-18,18):
        instance(scene,'Fantasy_House',(x,26,0))
    instance(scene,'Tavern',(-17,-9,0),180)
    instance(scene,'Clinic',(17,-9,0),180)
    instance(scene,'Guardhouse',(-31,9,0),-90)
    instance(scene,'Library',(33,9,0),90)
    instance(scene,'CrimsonRowhouse',(-17,-25,0))
    instance(scene,'VelvetTerrace',(17,-25,0))
    for x in (-32,-25,25,32):
        instance(scene,'Fantasy_House',(x,-25,0))
    for x,y in [(-20,-3),(20,-3),(-20,19),(20,19),(3,-13),(-3,-21),(27,3),(-27,3),(3,21)]:
        instance(scene,'Streetlamp',(x,y,0.12))
    for x in (-11,11):
        instance(scene,'Bench',(x,-3,0.12))
    for x in (-29,29):
        instance(scene,'Planter',(x,-3,0.12))
    for x in (-3,3):
        instance(scene,'Bollard',(x,-26.5,0.12))
    for x in (-2.45,2.45):
        instance(scene,'Drain_Grate',(x,-8,0.12))
    for x in (-23,-19):
        instance(scene,'Fence_Panel',(x,-17,0))
    instance(scene,'Fence_Gate',(-15,-17,0))
    instance(scene,'Fence_Post',(-11,-17,0))
    instance(scene,'Clothesline',(-19,-15,0))
    instance(scene,'Firewood_Rack',(-13,-15,0))
    instance(scene,'Hay_Bale',(-22,-14,0))
    instance(scene,'Water_Trough',(-7,-13,0))
    instance(scene,'Bucket',(-8.8,-13,0))
    for x in (10,14):
        instance(scene,'Stone_Wall',(x,-18,0))
    instance(scene,'Stone_Wall_Corner',(18,-18,0))
    instance(scene,'Forge',(15,-15,0))
    instance(scene,'Anvil',(12.3,-15.5,0))
    stage_instances(scene,'Town_Preview.png',(85,-110,105),(2800,2200))
    return scene


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials()
    source_scene=bpy.context.scene
    source_scene.name='Source construction'
    for name,label,category,recipe,metadata in specifications():
        collection,root=g.make_asset(name,recipe)
        root['label_ja']=label
        collection.asset_mark()
        collection.asset_data.description=label+' | Fantasy town modular prop'
        points=[object_matrix(obj)@v.co for obj in collection.objects if obj.type=='MESH' for v in obj.data.vertices]
        RECORDS.append({'name':name,'label_ja':label,'category':category,'origin':'new',
            'parts':sum(o.type=='MESH' for o in collection.objects),
            'minimum':[min(p[i] for p in points) for i in range(3)],
            'maximum':[max(p[i] for p in points) for i in range(3)],**metadata})
        ASSETS[name]=collection
        source_scene.collection.children.unlink(collection)
        print('PROP_CREATED',name,flush=True)
    category_scenes()
    town=town_scene()
    for scene in PREVIEWS:
        frame_scene(scene)
    bpy.context.window.scene=town
    bpy.data.scenes.remove(source_scene)
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
    with open(os.path.join(REVIEW,'model_manifest.json'),'w') as f:
        json.dump({'blender':bpy.app.version_string,'models':RECORDS,
            'previews':[{'scene':s.name,'file':os.path.basename(s.render.filepath)} for s in PREVIEWS],
            'town_instances':[{'name':o['asset_name'],'position':list(o.location),'angle_degrees':round(math.degrees(o.rotation_euler.z))} for o in town.objects if 'asset_name' in o]},f,indent=2,ensure_ascii=False)
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    if '--skip-render' not in sys.argv:
        for scene in PREVIEWS:
            bpy.ops.render.render(write_still=True,scene=scene.name)
    print('TOWN_PROPS_COMPLETE',flush=True)


if __name__=='__main__':
    main()
