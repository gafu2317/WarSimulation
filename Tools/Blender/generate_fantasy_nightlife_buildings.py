import bpy
import bmesh
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SOURCE = os.path.join(ROOT, 'ArtSource', 'Blender', 'FantasyNightlifeBuildings.blend')
REVIEW = os.path.join(ROOT, 'docs', 'Art', 'FantasyNightlifeBuildings')
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('fantasy_materials', os.path.join(os.path.dirname(__file__), 'generate_realistic_fantasy_buildings.py'))
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)
g = r.g
r.REVIEW = REVIEW


def rod(name, start, end, width, material):
    delta=Vector(end)-Vector(start)
    obj=g.box(name,(Vector(start)+Vector(end))/2,(width,width,delta.length),material,min(width/8,0.006))
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    return obj


def tube(name, points, radius, material):
    data=bpy.data.curves.new(name,'CURVE')
    data.dimensions='3D'
    data.resolution_u=8
    data.bevel_depth=radius
    data.bevel_resolution=2
    data.use_fill_caps=True
    spline=data.splines.new('BEZIER')
    spline.bezier_points.add(len(points)-1)
    for point,position in zip(spline.bezier_points,points):
        point.co=position
        point.handle_left_type=point.handle_right_type='AUTO'
    obj=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    obj=bpy.context.object
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    epsilon=max(abs(v) for vertex in bm.verts for v in vertex.co)*2**-23
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=epsilon)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    return g.finish(obj,name,material)


def sphere(name, center, size, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,radius=1,location=center)
    bpy.context.object.scale=size
    return g.finish(bpy.context.object,name,material)


def hip(name,width,depth,base,rise,material='Slate'):
    vertices=[(-width/2,-depth/2,base),(width/2,-depth/2,base),(width/2,depth/2,base),(-width/2,depth/2,base),
              (0,-depth*0.22,base+rise),(0,depth*0.22,base+rise)]
    g.mesh(name,vertices,[(0,3,2,1),(0,1,4),(1,2,5,4),(2,3,5),(3,0,4,5)],material,0.014)
    g.box(name+' eaves',(0,0,base-0.03),(width+0.06,depth+0.06,0.16),'RoofEdge',0.009)
    rod(name+' ridge',(0,-depth*0.22,base+rise+0.025),(0,depth*0.22,base+rise+0.025),0.09,'RoofEdge')
    for i in range(1,6):
        f=i/6
        x=width/2*(1-f)
        y=depth/2*(1-f)+depth*0.22*f
        z=base+rise*f+0.013
        for side in (-1,1):
            rod(name+' slate seam',(-x,side*y,z),(x,side*y,z),0.018,'RoofEdge')
            rod(name+' slate seam',(side*x,-y,z),(side*x,y,z),0.018,'RoofEdge')


def gable(width,depth,base,rise,wall,roof):
    g.extrude('Tall gable plaster',[(-width/2+0.23,base),(width/2-0.23,base),(0,base+rise-0.15)],
              -depth/2+0.19,depth/2-0.19,wall,0.01)
    for side in (-1,1):
        g.extrude('Steep roof panel',[(side*width/2,base),(0,base+rise),(0,base+rise-0.13),(side*width/2,base-0.13)],
                  -depth/2,depth/2,roof,0.01)
        for y in (-depth/2-0.02,depth/2+0.02):
            rod('Gable rake trim',(side*width/2,y,base),(0,y,base+rise),0.1,'RoofEdge')
        for i in range(1,7):
            f=i/7
            x=side*width/2*(1-f)
            rod('Gable slate row',(x,-depth/2,base+rise*f+0.01),(x,depth/2,base+rise*f+0.01),0.018,'RoofEdge')
    rod('Gable ridge',(0,-depth/2-0.08,base+rise),(0,depth/2+0.08,base+rise),0.1,'RoofEdge')


def lantern(x,y,z):
    rod('Lantern wall arm',(x,y+0.32,z+0.46),(x,y,z+0.46),0.045,'Iron')
    rod('Lantern suspension',(x,y,z+0.45),(x,y,z+0.29),0.023,'Brass')
    g.cylinder('Ruby lantern glass',(x,y,z),0.145,0.47,'RubyGlass',0.006,8)
    for dz in (-0.26,0.26):
        g.cylinder('Lantern bronze rim',(x,y,z+dz),0.19,0.065,'Brass',0.006,8)
    for i in range(8):
        a=math.tau*i/8
        rod('Lantern iron rib',(x+0.15*math.cos(a),y+0.15*math.sin(a),z-0.24),
            (x+0.15*math.cos(a),y+0.15*math.sin(a),z+0.24),0.022,'Iron')
    bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=0.2,radius2=0.055,depth=0.15,location=(x,y,z+0.36))
    g.finish(bpy.context.object,'Lantern faceted hood','Iron',0.005)
    sphere('Lantern lower tassel bead',(x,y,z-0.38),(0.038,0.038,0.07),'Brass')


def window(x,y,bottom,width,height,curtain='Crimson',frame='StoneTrim'):
    g.extrude('Arched window surround',g.arch_profile(x,bottom-0.1,width+0.24,height+0.2),y,y+0.13,frame,0.008)
    g.extrude('Warm recessed glazing',g.arch_profile(x,bottom,width,height),y-0.027,y-0.002,'WarmGlass',0.003)
    for side in (-1,1):
        for fold in range(3):
            px=x+side*(width*0.3+fold*width*0.06)
            g.cylinder('Folded window drapery',(px,y-0.073,bottom+height*0.38),width*0.044,height*0.73,curtain,0.005,10)
        g.box('Drapery gold tie',(x+side*width*0.36,y-0.123,bottom+height*0.31),(width*0.25,0.03,0.056),'Brass',0.003)
    rod('Window central mullion',(x,y-0.06,bottom+0.03),(x,y-0.06,bottom+height-0.04),0.034,'Iron')
    g.box('Deep window sill',(x,y-0.035,bottom-0.13),(width+0.36,0.29,0.14),frame,0.01)


def door(x,y,bottom,width,height):
    g.extrude('Polished double entrance door',g.arch_profile(x,bottom,width,height),y-0.04,y+0.05,'DarkWood',0.009)
    g.arch_trim('Entry carved stone arch',x,y-0.06,bottom,width,height,0.19,'StoneTrim')
    for dx in (-width*0.36,-width*0.12,width*0.12,width*0.36):
        g.box('Door raised panel',(x+dx,y-0.106,bottom+height*0.36),(width*0.2,0.055,height*0.56),'Door',0.008)
    for side in (-1,1):
        rod('Paired bronze door handle',(x+side*0.09,y-0.162,bottom+height*0.4),
            (x+side*0.09,y-0.162,bottom+height*0.54),0.035,'Brass')


def canopy(width,depth,front,back,material):
    vertices=[(x,y,z+dz) for dz in (0,-0.085) for x,y,z in
              ((-width/2,-depth/2,front),(width/2,-depth/2,front),(width/2,depth/2,back),(-width/2,depth/2,back))]
    g.mesh('Fabric entrance canopy',vertices,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],material,0.006)
    sections=math.ceil(width/0.5)
    step=width/sections
    for i in range(sections):
        x=-width/2+(i+0.5)*step
        g.extrude('Canopy scalloped valance',[(x-step*0.48,front),(x+step*0.48,front),
                  (x+step*0.32,front-0.19),(x,front-0.25),(x-step*0.32,front-0.19)],-depth/2-0.025,-depth/2+0.025,material,0.003)
    for side in (-1,1):
        rod('Canopy bronze edge',(side*width/2,-depth/2,front),(side*width/2,depth/2,back),0.05,'Brass')


def stairs(x,front,back,width,height):
    count=math.ceil(height/0.18)
    for i in range(count):
        h=height*(i+1)/count
        depth=(back-front)/count
        g.box('Entrance stone tread',(x,front+depth*(i+0.5),h/2),(width,depth,h),'StoneTrim',0)


def rail(start,end,bottom,height=0.94):
    a,b=Vector(start),Vector(end)
    count=math.ceil((b-a).length/0.32)
    for i in range(count+1):
        p=a+(b-a)*i/count
        rod('Iron balcony baluster',(p.x,p.y,bottom),(p.x,p.y,bottom+height),0.029,'Iron')
    for z,material,width in ((bottom+0.16,'Iron',0.035),(bottom+height,'Brass',0.053)):
        rod('Balcony horizontal rail',(a.x,a.y,z),(b.x,b.y,z),width,material)


def projecting_bay():
    profile=[(-1.62,0.08),(1.62,0.08),(1.62,-0.59),(1.09,-1.13),(-1.09,-1.13),(-1.62,-0.59)]
    vertices=[(x,y,z) for z in (3.45,5.92) for x,y in profile]
    faces=[tuple(range(5,-1,-1)),tuple(range(6,12))]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)]
    g.mesh('Three face projecting oriel',vertices,faces,'MutedRose',0.01)
    window(0,-1.16,3.87,1.45,1.65)
    for side in (-1,1):
        r.shifted(lambda:window(0,-0.06,3.87,0.52,1.65),(side*1.36,-0.87,0),side*math.pi/4)
    g.box('Oriel lower sill',(0,-0.62,3.5),(3.34,1.34,0.19),'StoneTrim',0.01)
    r.shifted(lambda:hip('Oriel hipped cap',3.63,1.5,5.94,0.67,'WineRoof'),(0,-0.57,0))
    for x in (-1.06,1.06):
        rod('Oriel bracket',(x,-0.07,2.92),(x,-0.93,3.4),0.12,'StoneTrim')


def crimson_rowhouse():
    g.box('Rowhouse stone foundation',(0,0,0.23),(5.42,6.65,0.46),'StoneShade',0.018)
    g.box('Rowhouse lower walls',(0,0,1.64),(5.08,6.28,3.08),'Stone',0.014)
    g.box('Rowhouse rose upper walls',(0,0,4.65),(5.08,6.28,2.95),'MutedRose',0.014)
    for z in (0.65,3.2,6.16):
        g.box('Rowhouse stone stringcourse',(0,0,z),(5.26,6.46,0.19),'StoneTrim',0.01)
    gable(5.72,6.94,6.2,2.75,'MutedRose','WineRoof')
    r.shifted(projecting_bay,(0,-3.16,0))
    door(1.17,-3.27,0.25,1.32,2.34)
    window(-1.14,-3.19,0.91,1.55,1.82)
    r.shifted(lambda:canopy(2.15,1.15,2.77,3.13,'Crimson'),(1.13,-3.65,0))
    stairs(1.17,-4.3,-3.27,1.82,0.26)
    for x in (-2.13,-0.14,2.19):
        lantern(x,-3.55,2.44)
    for angle in (-math.pi/2,math.pi/2):
        r.shifted(lambda:[window(x,-2.59,z,0.98,1.47) for x in (-1.89,1.89) for z in (1.02,4.02)],angle=angle)
    r.shifted(lambda:[window(x,-3.18,z,1.1,1.54) for x in (-1.21,1.21) for z in (0.95,4.0)],angle=math.pi)
    window(0,-3.3,6.61,0.86,1.23,'Crimson')
    g.cylinder('Rooftop bronze stem',(0,-3.45,9.1),0.035,0.42,'Brass',0.003,12)
    sphere('Rooftop bronze bead',(0,-3.45,9.34),(0.085,0.085,0.13),'Brass')


def velvet_terrace():
    g.box('Terrace main foundation',(-1.25,0,0.25),(7.45,7.03,0.5),'StoneShade',0.018)
    g.box('Terrace villa walls',(-1.25,0,3.22),(7.04,6.62,5.95),'PaleMauve',0.014)
    for z in (0.69,3.28,6.22):
        g.box('Villa limestone cornice',(-1.25,0,z),(7.28,6.86,0.22),'StoneTrim',0.012)
    r.shifted(lambda:hip('Villa low hipped roof',7.82,7.44,6.27,2.12),(-1.25,0,0))
    g.box('Low terrace wing foundation',(3.59,0.72,0.24),(2.37,5.54,0.48),'StoneShade',0.016)
    g.box('Low terrace wing',(3.52,0.72,1.91),(2.5,5.18,3.54),'Stone',0.012)
    g.box('Open rooftop terrace',(3.63,0.72,3.72),(2.77,5.54,0.2),'StoneTrim',0.012)
    rail((4.93,-1.94),(4.93,3.39),3.83)
    rail((2.42,-1.94),(4.93,-1.94),3.83)
    rail((2.42,3.39),(4.93,3.39),3.83)
    g.box('Broad front balcony',(-1.25,-3.88,3.4),(7.05,1.2,0.24),'StoneTrim',0.012)
    rail((-4.65,-4.43),(2.15,-4.43),3.53)
    rail((-4.65,-4.43),(-4.65,-3.35),3.53)
    rail((2.15,-4.43),(2.15,-3.35),3.53)
    for x in (-4.22,1.72):
        g.box('Balcony support footing',(x,-4.05,0.11),(0.42,0.48,0.22),'StoneShade',0.01)
        g.box('Balcony stone support',(x,-4.05,1.78),(0.24,0.3,3.32),'StoneTrim',0.014)
    for x in (-3.39,-1.25,0.89):
        window(x,-3.35,3.94,1.35,1.88,'Plum')
    for x in (-3.49,0.99):
        window(x,-3.35,1.02,1.4,1.7,'Plum')
    door(-1.25,-3.43,0.25,1.6,2.63)
    r.shifted(lambda:canopy(3.25,1.38,2.77,3.18,'Plum'),(-1.25,-3.92,0))
    stairs(-1.25,-4.86,-3.41,2.3,0.26)
    for x in (-2.36,-0.14):
        lantern(x,-3.7,2.35)
    for x in (-3.94,1.42):
        rod('Balcony lantern upright',(x,-4.1,3.53),(x,-4.1,5.22),0.039,'Iron')
    lantern(-3.94,-4.42,4.7)
    lantern(1.42,-4.42,4.7)
    window(3.55,-1.92,0.92,1.3,1.82,'Plum')
    r.shifted(lambda:[window(x,-4.84,1.0,1.12,1.86,'Plum') for x in (-0.87,0.84,2.55)],angle=math.pi/2)
    r.shifted(lambda:[window(x,-4.81,z,1.12,1.81,'Plum') for x in (-1.96,0.2,2.28) for z in (0.98,3.95)],angle=-math.pi/2)
    r.shifted(lambda:[window(-x,-3.36,z,1.12,1.78,'Plum') for x in (-3.4,-1.25,0.9) for z in (1.0,3.93)],angle=math.pi)


def lantern_spire():
    g.box('Slender house foundation',(-0.43,0.38,0.23),(4.83,6.45,0.46),'StoneShade',0.018)
    g.box('Slender house tall walls',(-0.43,0.38,4.76),(4.46,6.09,9.05),'DuskPlaster',0.014)
    for z in (0.66,3.32,6.23,9.25):
        g.box('Slender house storey cornice',(-0.43,0.38,z),(4.67,6.3,0.18),'StoneTrim',0.009)
    r.shifted(lambda:hip('Tall house roof',5.12,6.74,9.31,1.75),(-0.43,0.38,0))
    g.cylinder('Octagonal corner tower base',(1.23,-2.21,0.22),1.39,0.44,'StoneShade',0.012,8)
    g.cylinder('Octagonal corner tower',(1.23,-2.21,5.21),1.23,10.02,'DuskPlaster',0.008,8)
    for z in (0.67,3.33,6.25,9.31,10.22):
        g.cylinder('Tower ring moulding',(1.23,-2.21,z),1.33,0.15,'StoneTrim',0.007,8)
    for z in (1.11,4.07,7.05):
        window(1.23,-3.46,z,0.77,1.91,'Crimson')
        r.shifted(lambda z=z:window(0,-1.26,z,0.72,1.91,'Crimson'),(1.23,-2.21,0),math.pi/2)
    for z in (4.07,7.05):
        window(-1.41,-2.7,z,0.84,1.91,'Crimson')
    bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=1.55,radius2=0.035,depth=2.14,location=(1.23,-2.21,11.39))
    g.finish(bpy.context.object,'Octagonal slate tower cap','Slate',0.005)
    for i in range(8):
        a=math.tau*i/8
        rod('Tower roof seam',(1.23+1.555*math.cos(a),-2.21+1.555*math.sin(a),10.33),
            (1.23+0.035*math.cos(a),-2.21+0.035*math.sin(a),12.46),0.025,'RoofEdge')
    g.cylinder('Tower bronze finial',(1.23,-2.21,12.55),0.038,0.31,'Brass',0.004,12)
    sphere('Tower finial bead',(1.23,-2.21,12.74),(0.074,0.074,0.1),'Brass')
    door(-1.19,-2.85,0.24,1.23,2.54)
    r.shifted(lambda:canopy(1.91,1.01,2.94,3.29,'Crimson'),(-1.19,-3.18,0))
    stairs(-1.19,-3.85,-2.84,1.64,0.25)
    g.box('Small upper Juliet balcony',(-1.35,-3.01,6.34),(1.75,0.72,0.14),'StoneTrim',0.008)
    rail((-2.17,-3.32),(-0.53,-3.32),6.42,0.78)
    rail((-2.17,-3.32),(-2.17,-2.69),6.42,0.78)
    rail((-0.53,-3.32),(-0.53,-2.69),6.42,0.78)
    for x,y,z in ((-2.46,-3.04,2.52),(2.23,-3.03,2.65),(1.23,-3.7,6.43)):
        lantern(x,y,z)
    for angle,position in ((-math.pi/2,(-0.43,0.38,0)),(math.pi/2,(-0.43,0.38,0))):
        r.shifted(lambda:[window(x,-2.28,z,0.86,1.77,'Crimson') for x in (-0.04,1.91) for z in (1.19,4.1,7.06)],position,angle)
    r.shifted(lambda:[window(x,-3.11,z,0.91,1.81,'Crimson') for x in (-1.14,1.14) for z in (1.16,4.08,7.04)],(-0.43,0.38,0),math.pi)


def courtyard_wing():
    g.box('Courtyard wing stone foot',(0,0,0.21),(3.34,7.4,0.42),'StoneShade',0.015)
    g.box('Courtyard wing walls',(0,0,2.9),(3.03,7.07,5.44),'SandPlaster',0.013)
    for z in (0.67,3.1,5.68):
        g.box('Courtyard wing moulding',(0,0,z),(3.23,7.27,0.19),'StoneTrim',0.01)
    hip('Courtyard wing wine roof',3.63,7.82,5.73,1.47,'WineRoof')
    for z in (0.99,3.65):
        window(0,-3.58,z,1.3,1.63,'Crimson')
        r.shifted(lambda z=z:window(0,-3.58,z,1.3,1.63,'Crimson'),angle=math.pi)
    for angle in (-math.pi/2,math.pi/2):
        r.shifted(lambda:[window(x,-1.56,z,0.86,1.54,'Crimson') for x in (-2.3,0,2.3) for z in (1.02,3.68)],angle=angle)


def veiled_courtyard():
    g.box('Private courtyard paving',(0,0.74,0.125),(10.93,9.06,0.25),'CourtyardPaving',0.016)
    r.shifted(courtyard_wing,(-3.77,0.03,0))
    r.shifted(courtyard_wing,(3.77,0.03,0))
    g.box('Rear gallery foundation',(0,4.42,0.2),(4.5,2.03,0.4),'StoneShade',0.012)
    g.box('Rear gallery connecting wall',(0,4.39,2.58),(4.34,1.99,4.93),'SandPlaster',0.012)
    g.box('Rear gallery cornice',(0,4.39,5.09),(4.57,2.18,0.19),'StoneTrim',0.01)
    r.shifted(lambda:hip('Rear gallery wine roof',4.85,2.59,5.15,1.04,'WineRoof'),(0,4.39,0))
    for x in (-1.07,1.07):
        window(x,3.35,2.78,1.07,1.76,'Plum')
        r.shifted(lambda x=x:window(x,-5.44,2.78,1.07,1.76,'Plum'),angle=math.pi)
    door(0,3.29,0.25,1.43,2.25)
    for x in (-1.77,1.77):
        g.box('Courtyard gate pedestal',(x,-3.66,0.25),(0.64,0.74,0.5),'StoneShade',0.012)
        g.box('Courtyard gate pier',(x,-3.62,1.97),(0.44,0.54,3.58),'StoneTrim',0.012)
        g.box('Gate pier capital',(x,-3.65,3.79),(0.62,0.73,0.19),'StoneTrim',0.012)
    radius,spring=1.51,2.4
    outline=[(-radius,4.08),(radius,4.08)]+[(radius*math.cos(math.pi*i/20),spring+radius*math.sin(math.pi*i/20)) for i in range(21)]
    g.extrude('Open courtyard arch header',outline,-3.94,-3.26,'SandPlaster',0)
    g.arch_trim('Courtyard dressed arch',0,-3.99,0.25,3.02,spring+radius-0.25,0.18,'StoneTrim')
    g.box('Gate horizontal crown',(0,-3.63,4.1),(3.85,0.95,0.18),'StoneTrim',0.012)
    for side in (-1,1):
        for fold in range(3):
            x=side*(1.13+fold*0.085)
            g.cylinder('Gate drawn velvet drape',(x,-3.87,1.69),0.073,2.77,'Crimson',0.007,12)
        g.box('Gate drapery gold tie',(side*1.22,-3.96,1.51),(0.34,0.055,0.1),'Brass',0.005)
        lantern(side*1.86,-4.17,2.93)
        lantern(side*3.77,-3.91,3.08)
    g.box('Courtyard threshold',(0,-3.65,0.17),(2.89,0.91,0.34),'StoneTrim',0.006)
    stairs(0,-4.66,-4.07,2.82,0.25)
    for x in (-1.62,1.62):
        g.box('Courtyard low planter',(x,1.1,0.46),(0.72,1.16,0.43),'StoneTrim',0.012)
        g.box('Courtyard clipped shrub',(x,1.1,0.87),(0.61,1.05,0.54),'Foliage',0.07)
    lantern(-1.75,3.03,2.4)
    lantern(1.75,3.03,2.4)


def materials():
    r.setup_materials()
    for name,color,kind in (
        ('MutedRose','AA847E','plain'),('PaleMauve','B5A39F','plain'),('DuskPlaster','8C8390','plain'),
        ('SandPlaster','B9AC97','plain'),('Slate','323B46','plain'),('WineRoof','51373D','plain'),
        ('Crimson','762D39','plain'),('Plum','512B46','plain'),('DarkWood','46312D','wood'),
        ('WarmGlass','94714F','plain'),('RubyGlass','A73539','plain'),('Foliage','4E6049','plain'),
        ('CourtyardPaving','989083','plain')):
        g.MATERIALS[name]=r.surface(name,color,kind)
    for name,color,strength in (('WarmGlass',(1,0.48,0.22,1),0.32),('RubyGlass',(1,0.11,0.055,1),1.7)):
        shader=g.MATERIALS[name].node_tree.nodes.get('Principled BSDF')
        shader.inputs['Emission Color'].default_value=color
        shader.inputs['Emission Strength'].default_value=strength


def gallery_copy(asset, position, scene):
    collection,source=asset
    target=bpy.data.collections.new('Display '+source.name)
    scene.collection.children.link(target)
    root=bpy.data.objects.new('Display '+source.name,None)
    root.location=position
    target.objects.link(root)
    for obj in collection.objects:
        if obj.type=='MESH':
            copy=obj.copy()
            copy.parent=root
            target.objects.link(copy)
    return target


def twilight(scene, objects):
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(0.24,0.29,0.4,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=0.34
    for obj in scene.objects:
        if obj.type=='LIGHT':
            if obj.data.type=='SUN':
                obj.data.energy=0.55
                obj.data.color=(0.73,0.79,1)
            else:
                obj.data.energy*=0.52
                obj.data.color=(0.8,0.85,1)
    lights=bpy.data.collections.new('Red lantern lighting')
    scene.collection.children.link(lights)
    bpy.context.view_layer.update()
    for obj in objects:
        if obj.type=='MESH' and any(slot.material==g.MATERIALS['RubyGlass'] for slot in obj.material_slots):
            lamp=bpy.data.objects.new('Warm ruby light',bpy.data.lights.new('Warm ruby light','POINT'))
            lights.objects.link(lamp)
            lamp.location=obj.matrix_world.translation+Vector((0,-0.19,0))
            lamp.data.energy=24
            lamp.data.color=(1,0.27,0.15)
            lamp.data.shadow_soft_size=0.22
    scene.view_settings.exposure=0.6


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials()
    assets,scenes=[],[]
    specs=(('CrimsonRowhouse','赤灯りの町家',crimson_rowhouse),('VelvetTerrace','バルコニーの館',velvet_terrace),
           ('LanternSpire','塔屋付きの建物',lantern_spire),('VeiledCourtyard','中庭入口の館',veiled_courtyard))
    for index,(name,label,recipe) in enumerate(specs,start=1):
        scene=bpy.context.scene if index==1 else bpy.data.scenes.new(name)
        scene.name=f'0{index} {name}'
        bpy.context.window.scene=scene
        asset=g.make_asset(name,recipe)
        asset[1]['facility_type']=label
        asset[1]['design']='New independent nightlife building; no existing building recipe reused'
        assets.append(asset)
        scenes.append(scene)
        r.stage(scene,list(asset[0].objects),(23,-37,25),(0,0,4),name+'_Preview.png',(1600,1600))
        scene.view_settings.exposure=0.3
        print('NIGHTLIFE_BUILDING_CREATED',name,flush=True)
    overview=bpy.data.scenes.new('00 Four Nightlife Buildings')
    bpy.context.window.scene=overview
    displays=[gallery_copy(asset,position,overview) for asset,position in zip(assets,((-20,0,0),(-7,0,0),(6,0,0),(18.8,0,0)))]
    objects=[obj for collection in displays for obj in collection.objects]
    r.stage(overview,objects,(14,-70,40),(0,0,4),'Buildings_Preview.png',(3000,1400))
    twilight(overview,objects)
    records=[]
    for collection,root in assets:
        points=[obj.matrix_parent_inverse@obj.matrix_basis@Vector(v) for obj in collection.objects if obj.type=='MESH' for v in obj.bound_box]
        records.append({'name':root.name,'facility_type':root['facility_type'],
                        'parts':sum(obj.type=='MESH' for obj in collection.objects),
                        'minimum':[min(p[i] for p in points) for i in range(3)],'maximum':[max(p[i] for p in points) for i in range(3)]})
    with open(os.path.join(REVIEW,'model_manifest.json'),'w') as handle:
        json.dump({'blender':bpy.app.version_string,'models':records,'geometry':'Four new building recipes; shared primitives and materials only'},handle,indent=2,ensure_ascii=False)
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    for scene in [overview]+scenes:
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print('NIGHTLIFE_BUILDINGS_COMPLETE',flush=True)


if __name__=='__main__':
    main()
