import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SOURCE = os.path.join(ROOT, 'ArtSource', 'Blender', 'FantasyEntertainmentDistrict.blend')
REVIEW = os.path.join(ROOT, 'docs', 'Art', 'FantasyEntertainmentDistrict')
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('civic', os.path.join(os.path.dirname(__file__), 'generate_fantasy_civic_facilities.py'))
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
c, t, r, g = m.c, m.t, m.r, m.g
r.REVIEW = REVIEW


def warm_window(x, y, bottom, width, height, curtains=False):
    t.casement(x,y,bottom,width,height,frame='Timber',glass='WarmGlass')
    if curtains:
        for side in (-1,1):
            g.box('Velvet window curtain',(x+side*width*0.34,y-0.13,bottom+height/2),
                  (width*0.23,0.065,height*0.94),'Velvet',0.01)
            g.box('Curtain gold tie',(x+side*width*0.34,y-0.172,bottom+height*0.38),
                  (width*0.25,0.035,0.063),'Brass',0.004)


def crescent(x,y,z,radius):
    obj=g.cylinder('Gilded crescent disc',(x,y,z),radius,0.035,'Brass',0,32)
    obj.rotation_euler.x=math.pi/2
    obj=g.cylinder('Crescent inset',(x+radius*0.35,y-0.025,z+radius*0.18),radius*0.83,0.025,'Velvet',0,32)
    obj.rotation_euler.x=math.pi/2


def balcony(width, y, bottom):
    g.box('Stone balcony slab',(0,y,bottom),(width,1.23,0.22),'StoneTrim',0.012)
    for x in (-width/2+0.16,width/2-0.16):
        g.box('Balcony end pier',(x,y-0.5,bottom+0.6),(0.2,0.2,1.09),'StoneTrim',0.01)
    count=math.ceil(width/0.39)
    for i in range(count+1):
        x=-width/2+0.25+(width-0.5)*i/count
        m.rod('Balcony wrought iron baluster',(x,y-0.51,bottom+0.13),(x,y-0.51,bottom+1.1),0.038,'Iron')
        if i%2==0:
            c.ellipsoid('Balcony brass bead',(x,y-0.51,bottom+0.59),(0.058,0.047,0.08),'Brass')
    m.rod('Balcony brass top rail',(-width/2,y-0.51,bottom+1.13),(width/2,y-0.51,bottom+1.13),0.065,'Brass')
    m.rod('Balcony lower rail',(-width/2,y-0.51,bottom+0.27),(width/2,y-0.51,bottom+0.27),0.052,'Iron')
    for side in (-1,1):
        x=side*(width/2-0.15)
        m.rod('Balcony side handrail',(x,y-0.51,bottom+1.13),(x,y+0.55,bottom+1.13),0.063,'Brass')
        for offset in (-0.19,0.15,0.49):
            m.rod('Balcony side baluster',(x,y+offset,bottom+0.14),(x,y+offset,bottom+1.1),0.038,'Iron')
        m.rod('Balcony diagonal support',(side*width*0.37,y+0.5,bottom-0.93),
              (side*width*0.37,y-0.4,bottom-0.1),0.16,'StoneTrim')


def salon():
    g.box('Salon foundation',(0,0,0.22),(9.75,6.9,0.44),'StoneShade',0.02)
    g.box('Salon stone ground floor',(0,0,1.64),(9.3,6.45,3.08),'Stone',0.016)
    g.box('Salon rose plaster upper floors',(0,0,5.85),(9.3,6.45,5.38),'RosePlaster',0.015)
    for z,height in ((3.18,0.2),(5.94,0.16),(8.53,0.27)):
        g.box('Salon limestone stringcourse',(0,0,z),(9.58,6.73,height),'StoneTrim',0.012)
    for x in (-4.46,4.46):
        for y in (-3.24,3.24):
            g.box('Salon corner pilaster',(x,y,4.57),(0.3,0.36,7.88),'StoneTrim',0.01)
    for x in (-3.04,-1.02,1.02,3.04):
        warm_window(x,-3.27,3.76,1.23,1.77,True)
        warm_window(x,-3.27,6.52,1.03,1.32,True)
    for angle in (-math.pi/2,math.pi/2):
        r.shifted(lambda:[warm_window(x,-4.7,z,1.12,1.58,True) for x in (-1.93,0,1.93) for z in (3.85,6.44)],angle=angle)
    r.shifted(lambda:[warm_window(x,-3.27,z,1.16,1.61) for x in (-3.0,0,3.0) for z in (1.02,3.85,6.44)],angle=math.pi)
    for x in (-3.03,3.03):
        warm_window(x,-3.29,0.91,1.35,1.48)
    r.door(0,-3.51,1.78,2.57,0.24)
    c.stairs(2.9,-4.43,-3.48,0.25)
    balcony(7.8,-3.82,3.29)
    r.shifted(lambda:t.shed_roof(3.68,1.36,2.78,3.11,'Velvet'),(0,-3.84,0))
    for i in range(7):
        x=-1.84+(i+0.5)*3.68/7
        g.extrude('Salon awning scallop',[(x-0.25,2.76),(x+0.25,2.76),(x+0.17,2.58),(x,2.53),(x-0.17,2.58)],
                  -4.55,-4.50,'Velvet',0.004)
    for x in (-1.45,1.45):
        t.lantern(x,-3.69,2.2)
    r.roof(10.15,7.3,8.61,2.72)
    g.box('Salon roof dormer',(0,-2.57,9.05),(2.29,1.32,1.18),'RosePlaster',0.012)
    r.shifted(lambda:t.gable(2.79,1.74,9.59,1.13,'RosePlaster'),(0,-2.59,0))
    g.box('Salon moon sign',(0,-3.28,9.16),(1.08,0.12,0.89),'Velvet',0.025)
    crescent(0,-3.37,9.16,0.3)
    for x in (-4.06,4.06):
        g.cylinder('Salon roof finial',(x,0,9.27),0.048,0.62,'Brass',0.004,12)
        c.ellipsoid('Salon finial orb',(x,0,9.62),(0.093,0.093,0.12),'Brass')


def theatre_mask(x,y,z,angle,smile):
    outline=[(-0.43,0.46),(0,0.57),(0.43,0.46),(0.48,0.05),(0.33,-0.41),
             (0,-0.62),(-0.33,-0.41),(-0.48,0.05)]
    def face():
        g.extrude('Theatre carved mask',[(px,pz) for px,pz in outline],-0.06,0.06,'Ivory',0.01)
        for side in (-1,1):
            c.ellipsoid('Mask eye',(side*0.19,-0.084,0.1),(0.1,0.025,0.045),'Iron')
        g.extrude('Mask nose',[(-0.057,0.04),(0.057,0.04),(0,-0.19)],-0.12,-0.065,'StoneTrim',0.004)
        middle=-0.34 if smile else -0.21
        edge=-0.21 if smile else -0.34
        m.curve_tube('Mask mouth',[(-0.2,-0.087,edge),(0,-0.09,middle),(0.2,-0.087,edge)],0.027,'Iron')
    before=set(g.PARTS.objects)
    face()
    rotation=__import__('mathutils').Matrix.Rotation(angle,4,'Y')
    for obj in set(g.PARTS.objects)-before:
        obj.matrix_world=rotation@obj.matrix_world
        obj.location+=Vector((x,y,z))


def theatre():
    g.box('Theatre foundation',(0,0,0.24),(6.96,7.26,0.48),'StoneShade',0.02)
    g.box('Theatre walls',(0,0,3.16),(6.5,6.8,5.94),'TheatrePlaster',0.018)
    for z in (0.73,3.42,6.1):
        g.box('Theatre cornice',(0,0,z),(6.75,7.05,0.21),'StoneTrim',0.012)
    for x in (-3.01,-1.3,1.3,3.01):
        g.box('Theatre entrance pilaster',(x,-3.45,3.25),(0.28,0.25,5.53),'StoneTrim',0.012)
        g.box('Theatre column capital',(x,-3.51,5.98),(0.44,0.38,0.2),'Brass',0.007)
    r.door(0,-3.63,1.96,2.96,0.33)
    c.stairs(3.5,-4.57,-3.56,0.34)
    for x in (-2.13,2.13):
        g.box('Theatre crimson drape',(x,-3.61,2.45),(1.04,0.07,2.49),'Velvet',0.008)
        m.rod('Theatre banner rod',(x-0.6,-3.63,3.78),(x+0.6,-3.63,3.78),0.065,'Brass')
        g.extrude('Theatre gold diamond',[(x,2.86),(x+0.22,2.48),(x,2.1),(x-0.22,2.48)],-3.665,-3.649,'Brass',0)
        t.lantern(x,-3.79,1.51)
    r.shifted(lambda:t.shed_roof(5.23,1.34,3.73,4.15,'Velvet'),(0,-3.78,0))
    theatre_mask(-0.5,-3.61,5.1,-0.16,True)
    theatre_mask(0.5,-3.72,5.1,0.16,False)
    for angle in (-math.pi/2,math.pi/2):
        r.shifted(lambda:[g.window(x,-3.31,1.45,1.0,2.07) for x in (-2.03,0.01,2.05)],angle=angle)
    r.shifted(lambda:[warm_window(x,-3.45,1.53,1.0,1.8) for x in (-1.94,0,1.94)],angle=math.pi)
    r.roof(7.45,7.7,6.18,2.55)


def hanging_lantern(x,y,z,ruby=True):
    material='RubyGlass' if ruby else 'Lantern'
    g.box('Hanging lantern panes',(x,y,z),(0.27,0.27,0.45),material,0.009)
    for dx in (-0.15,0.15):
        for dy in (-0.15,0.15):
            g.box('Hanging lantern corner',(x+dx,y+dy,z),(0.033,0.033,0.49),'Iron',0.003)
    for dz in (-0.26,0.26):
        g.box('Hanging lantern cap',(x,y,z+dz),(0.38,0.38,0.07),'Iron',0.005)
    g.cylinder('Hanging lantern ring',(x,y,z+0.39),0.028,0.2,'Brass',0.003,10)


def entrance_arch():
    for x in (-3.2,3.2):
        g.box('Lane arch stone footing',(x,-8.85,0.27),(0.78,0.78,0.54),'StoneShade',0.016)
        g.box('Lane arch pedestal',(x,-8.85,0.78),(0.49,0.49,0.56),'StoneTrim',0.012)
        g.cylinder('Lane arch iron post',(x,-8.85,2.82),0.078,3.58,'Iron',0.006,16)
        g.cylinder('Lane arch post collar',(x,-8.85,4.45),0.16,0.13,'Brass',0.006,16)
    m.curve_tube('Arched iron lane gateway',[(-3.2,-8.85,4.49),(-2.3,-8.85,5.1),(0,-8.85,5.58),
                 (2.3,-8.85,5.1),(3.2,-8.85,4.49)],0.075,'Iron')
    m.curve_tube('Gateway gold trim',[(-3.2,-8.85,4.27),(-2.3,-8.85,4.85),(0,-8.85,5.32),
                 (2.3,-8.85,4.85),(3.2,-8.85,4.27)],0.034,'Brass')
    for x in (-2.39,2.39):
        m.rod('Gate lantern chain',(x,-8.85,4.84),(x,-8.85,4.36),0.029,'Iron')
        hanging_lantern(x,-8.85,3.98,False)
    g.extrude('Velvet moon quarter sign',[(-0.82,4.99),(0.82,4.99),(0.82,4.31),(0,4.13),(-0.82,4.31)],
              -8.93,-8.82,'Velvet',0.009)
    crescent(0,-9.01,4.6,0.25)
    for x in (-0.5,0.5):
        m.rod('Quarter sign hanger',(x,-8.87,5.3),(x,-8.87,4.99),0.035,'Brass')


def table():
    g.cylinder('Outdoor tavern tabletop',(0,0,0.86),0.61,0.12,'Door',0.012,24)
    g.cylinder('Table iron pedestal',(0,0,0.43),0.065,0.8,'Iron',0.005,12)
    for angle in (0,math.tau/3,2*math.tau/3):
        m.rod('Table splayed foot',(0,0,0.3),(0.43*math.cos(angle),0.43*math.sin(angle),0.05),0.065,'Iron')
    for x in (-1.0,1.0):
        g.cylinder('Outdoor stool seat',(x,0,0.56),0.27,0.12,'Door',0.01,16)
        for dx,dy in ((-0.14,-0.14),(0.14,-0.14),(0,0.14)):
            g.box('Stool leg',(x+dx,dy,0.25),(0.065,0.065,0.5),'Timber',0.007)
    for x,y in ((-0.21,-0.13),(0.22,0.14)):
        g.cylinder('Tankard on table',(x,y,1.02),0.074,0.2,'Brass',0.006,12)
        c.torus('Tankard handle',(x+0.092,y,1.02),0.047,0.012,(math.pi/2,0,0),'Brass')


def street():
    outline=[(-10.1,-11.25),(10.1,-11.25),(11.85,-9.5),(11.85,9.5),(10.1,11.25),(-10.1,11.25),(-11.85,9.5),(-11.85,-9.5)]
    vertices=[(x,y,z) for z in (0,0.25) for x,y in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    g.mesh('Entertainment quarter cobbled base',vertices,faces,'PavingGrid',0.012)
    for i,a in enumerate(outline):
        b=outline[(i+1)%8]
        m.rod('Quarter stone border',(a[0],a[1],0.23),(b[0],b[1],0.23),0.2,'StoneTrim')
    for x in (-1.58,1.58):
        g.box('Central lane paving band',(x,-3.52,0.262),(0.17,14.85,0.025),'StoneShade',0.003)
    r.shifted(entrance_arch,(0,0,0.25))
    for position,angle in (((-3.52,-2.1,0.25),math.pi/2),((5.92,-7.15,0.25),0)):
        r.shifted(table,position,angle)
    for x,y in ((-9.4,4.65),(9.65,5.45)):
        r.shifted(m.planter,(x,y,0.25))
    for position in ((-5.85,-8.65,0.25),(8.65,-7.45,0.25)):
        r.shifted(m.plaza_lamp,position)
    m.curve_tube('Lantern suspension rope',[(-3.78,-4.1,5.6),(0,-4.1,4.98),(3.78,-4.1,5.6)],0.025,'Timber')
    for i,x in enumerate((-3.12,-1.57,0,1.57,3.12)):
        z=4.98+0.62*(x/3.78)**2
        m.rod('Suspended lantern hanger',(x,-4.1,z),(x,-4.1,z-0.25),0.023,'Iron')
        hanging_lantern(x,-4.1,z-0.61,i%2==0)
    for x in (-3.82,3.82):
        g.box('Rope post stone foot',(x,-4.1,0.45),(0.43,0.43,0.4),'StoneShade',0.012)
        g.box('Lantern rope oak post',(x,-4.1,3.03),(0.12,0.12,5.4),'Timber',0.008)
    for x,y in ((-2.35,1.7),(2.35,1.7)):
        r.shifted(m.planter,(x,y,0.25))


def materials():
    m.materials()
    for name,color,kind in (('RosePlaster','A68B80','plain'),('TheatrePlaster','AAA48F','plain'),
                           ('Velvet','642E42','plain'),('OchrePlaster','B19C79','plain'),
                           ('WarmGlass','A88A57','plain'),('RubyGlass','9E3D35','plain')):
        g.MATERIALS[name]=r.surface(name,color,kind)
    for name,color,strength in (('WarmGlass',(1,0.48,0.17,1),0.38),('Lantern',(1,0.51,0.18,1),2.0),
                               ('RubyGlass',(1,0.16,0.07,1),1.3)):
        shader=g.MATERIALS[name].node_tree.nodes.get('Principled BSDF')
        shader.inputs['Emission Color'].default_value=color
        shader.inputs['Emission Strength'].default_value=strength


def evening_lighting(scene, objects):
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(0.16,0.22,0.37,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=0.21
    for obj in scene.objects:
        if obj.type=='LIGHT':
            if obj.data.type=='SUN':
                obj.data.energy=0.19
                obj.data.color=(0.52,0.65,1)
            else:
                obj.data.energy*=0.23
                obj.data.color=(0.57,0.68,1)
    lighting=bpy.data.collections.new(scene.name+' Lantern light')
    scene.collection.children.link(lighting)
    bpy.context.view_layer.update()
    for obj in objects:
        if obj.type!='MESH' or not any(slot.material in (g.MATERIALS['Lantern'],g.MATERIALS['RubyGlass']) for slot in obj.material_slots):
            continue
        lamp=bpy.data.objects.new('Warm lantern pool',bpy.data.lights.new('Warm lantern pool','POINT'))
        lighting.objects.link(lamp)
        lamp.location=obj.matrix_world.translation+Vector((0,-0.21,0))
        lamp.data.energy=43
        lamp.data.color=(1,0.4,0.12)
        lamp.data.shadow_soft_size=0.24
    scene.view_settings.exposure=0.65


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials()
    day=bpy.context.scene
    day.name='01 Quarter Daylight'
    assets=[]
    for name,label,recipe,position,angle in (
        ('VelvetSalon','遊興館',salon,(0,6.5,0.25),0),
        ('CopperTankard','酒場',t.tavern,(-7.45,-1.9,0.25),math.pi/2),
        ('LanternTheatre','小劇場',theatre,(7.3,-1.6,0.25),0),
        ('LanternLane','路地と街灯',street,(0,0,0),0)):
        asset=g.make_asset(name,recipe)
        asset[1].location=position
        asset[1].rotation_euler.z=angle
        asset[1]['facility_type']=label
        assets.append(asset)
        print('DISTRICT_COMPONENT_CREATED',name,flush=True)
    objects=[obj for collection,root in assets for obj in collection.objects if obj.type=='MESH']
    scenes=[]
    for scene_name,camera,filename,evening in (
        ('01 Quarter Daylight',(33,-46,35),'Daylight_Preview.png',False),
        ('02 Quarter Dusk',(33,-46,35),'Dusk_Preview.png',True),
        ('03 Lantern Street',(10,-37,18),'Street_Preview.png',True)):
        scene=day if not scenes else bpy.data.scenes.new(scene_name)
        bpy.context.window.scene=scene
        if scene!=day:
            for collection,root in assets:
                scene.collection.children.link(collection)
        bpy.context.view_layer.update()
        r.stage(scene,objects,camera,(0,0,4.4),filename,(2200,1800))
        scene.view_settings.exposure=0.3
        if evening:
            evening_lighting(scene,objects)
        scenes.append(scene)
    records=[]
    for collection,root in assets:
        points=[root.matrix_basis@obj.matrix_parent_inverse@obj.matrix_basis@Vector(v)
                for obj in collection.objects if obj.type=='MESH' for v in obj.bound_box]
        records.append({'name':root.name,'facility_type':root['facility_type'],
                        'parts':sum(obj.type=='MESH' for obj in collection.objects),
                        'ground_z':float(root.location.z),'minimum':[min(p[i] for p in points) for i in range(3)],
                        'maximum':[max(p[i] for p in points) for i in range(3)]})
    with open(os.path.join(REVIEW,'model_manifest.json'),'w') as handle:
        json.dump({'blender':bpy.app.version_string,'facility':'歓楽街','models':records},handle,indent=2,ensure_ascii=False)
    bpy.context.window.scene=scenes[0]
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    for scene in scenes:
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print('ENTERTAINMENT_DISTRICT_COMPLETE',flush=True)


if __name__=='__main__':
    main()
