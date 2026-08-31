import bpy
import bmesh
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SOURCE = os.path.join(ROOT, 'ArtSource', 'Blender', 'FantasyCivicFacilities.blend')
REVIEW = os.path.join(ROOT, 'docs', 'Art', 'FantasyCivicFacilities')
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('culture', os.path.join(os.path.dirname(__file__), 'generate_fantasy_culture_facilities.py'))
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)
t, r, g = c.t, c.r, c.g
r.REVIEW = REVIEW


def rod(name, start, end, width, material):
    direction = Vector(end)-Vector(start)
    obj = g.box(name, (Vector(start)+Vector(end))/2, (width,width,direction.length), material, min(width/8,0.008))
    obj.rotation_euler = direction.to_track_quat('Z','Y').to_euler()
    return obj


def pipe(name, start, end, radius, material):
    direction = Vector(end)-Vector(start)
    obj = g.cylinder(name, (Vector(start)+Vector(end))/2, radius, direction.length, material, 0.006, 32)
    obj.rotation_euler = direction.to_track_quat('Z','Y').to_euler()
    return obj


def ring(name, inner, outer, bottom, top, material):
    for start,end in ((0,math.pi),(math.pi,math.tau)):
        c.ring_section(name,(inner,inner),(outer,outer),bottom,top,start,end,material)


def curve_tube(name, points, radius, material):
    data = bpy.data.curves.new(name,'CURVE')
    data.dimensions = '3D'
    data.resolution_u = 8
    data.bevel_depth = radius
    data.bevel_resolution = 2
    data.use_fill_caps = True
    spline = data.splines.new('BEZIER')
    spline.bezier_points.add(len(points)-1)
    for point,position in zip(spline.bezier_points,points):
        point.co = position
        point.handle_left_type = point.handle_right_type = 'AUTO'
    obj = bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.object
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    epsilon = max(abs(value) for vertex in bm.verts for value in vertex.co)*2**-23
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=epsilon)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    return g.finish(bpy.context.object,name,material)


def book_emblem():
    outline = [(-1.12,5.36),(-1.12,6.69),(-0.18,6.53),(0,6.43),(0.18,6.53),(1.12,6.69),
               (1.12,5.36),(0.17,5.19),(0,5.12),(-0.17,5.19)]
    g.extrude('Open book bronze binding',outline,-5.25,-5.12,'Brass',0.008)
    for side in (-1,1):
        profile = [(side*0.1,5.3),(side*1.0,5.46),(side*1.0,6.53),(side*0.1,6.38)]
        g.extrude('Carved ivory book pages',profile,-5.28,-5.255,'Ivory',0.003)
        for z in (5.63,5.86,6.09):
            rod('Engraved book line',(side*0.24,-5.296,z),(side*0.86,-5.296,z+0.1),0.018,'Ink')
    rod('Book central spine',(0,-5.31,5.16),(0,-5.31,6.4),0.06,'Brass')


def library():
    g.box('Library foundation',(0,0,0.25),(14.15,8.95,0.5),'StoneShade',0.02)
    g.box('Library portal foundation',(0,-4.46,0.25),(4.9,1.75,0.5),'StoneShade',0.02)
    g.box('Library reading hall',(0,0,3.45),(13.6,8.4,6.3),'LibraryPlaster',0.015)
    for z,h in ((0.72,0.28),(6.5,0.26)):
        g.box('Library limestone cornice',(0,0,z),(13.9,8.7,h),'StoneTrim',0.012)
    r.roof(14.5,9.3,6.61,2.7)
    g.box('Library projecting entrance',(0,-4.36,3.85),(4.6,1.5,7.2),'Stone',0.012)
    g.box('Library entrance eaves',(0,-4.36,7.46),(4.77,1.66,0.24),'StoneTrim',0.012)
    r.shifted(lambda:t.gable(5.18,2.05,7.52,2.2,'LibraryPlaster'),(0,-4.36,0))
    for x in (-6.5,-2.65,2.65,6.5):
        g.box('Reading hall buttress',(x,-4.3,3.1),(0.4,0.55,5.66),'StoneTrim',0.014)
        g.box('Buttress cap',(x,-4.32,5.94),(0.55,0.66,0.2),'StoneShade',0.012)
    for x in (-5.42,-3.8,3.8,5.42):
        r.lancet(x,-4.25,1.39,1.0,3.99)
        g.box('Reading window transom',(x,-4.37,3.24),(0.98,0.065,0.085),'StoneTrim',0.008)
    for angle in (math.pi/2,-math.pi/2):
        r.shifted(lambda:[r.lancet(x,-6.85,1.42,1.22,3.9) for x in (-2.65,0,2.65)],angle=angle)
    for x in (-5.3,-2.65,0,2.65,5.3):
        r.shifted(lambda x=x:r.lancet(x,-4.25,1.42,1.1,3.9),angle=math.pi)
    for x in (-2.12,2.12):
        g.box('Portal edge pier',(x,-5.14,3.83),(0.29,0.28,7.08),'StoneTrim',0.01)
    r.door(0,-5.22,2.0,3.35,0.49)
    g.box('Portal header',(0,-5.2,4.16),(4.54,0.36,0.2),'StoneTrim',0.012)
    c.stairs(3.55,-6.47,-5.19,0.5)
    book_emblem()
    c.torus('Library upper oculus',(0,-5.12,8.14),0.32,0.065,(math.pi/2,0,0),'StoneTrim')
    pane=g.cylinder('Library oculus glass',(0,-5.11,8.14),0.27,0.045,'Window',0,32)
    pane.rotation_euler.x=math.pi/2
    for x in (-1.54,1.54):
        t.lantern(x,-5.47,2.75)
    g.box('Library chimney',(-4.1,1.9,7.4),(0.86,0.91,3.65),'Stone',0.012)
    g.box('Library chimney cap',(-4.1,1.9,9.22),(1.06,1.11,0.23),'StoneTrim',0.012)
    for x in (-4.32,-3.88):
        g.cylinder('Library chimney pot',(x,1.9,9.55),0.14,0.46,'StoneShade',0.006,12)


def bench():
    for x in (-1.1,1.1):
        g.box('Bench stone support',(x,0,0.36),(0.3,0.64,0.72),'StoneTrim',0.015)
        rod('Bench back support',(x,0.23,0.65),(x,0.33,1.43),0.08,'Iron')
    for y in (-0.21,0,0.21):
        g.box('Oak bench seat slat',(0,y,0.78),(2.8,0.18,0.13),'Door',0.014)
    for z in (1.07,1.33):
        g.box('Oak bench back slat',(0,0.32,z),(2.8,0.11,0.2),'Door',0.012)
    for x in (-1.29,1.29):
        rod('Bench arm',(x,-0.22,1.08),(x,0.29,1.08),0.065,'Iron')


def plaza_lamp():
    g.box('Lamp stone footing',(0,0,0.14),(0.6,0.6,0.28),'StoneShade',0.015)
    g.cylinder('Bronze plaza lamp post',(0,0,1.79),0.085,3.45,'Iron',0.006,12)
    g.cylinder('Lamp collar',(0,0,3.38),0.16,0.15,'Brass',0.006,16)
    g.box('Plaza lantern amber panes',(0,0,3.74),(0.38,0.38,0.6),'Lantern',0.012)
    for x in (-0.21,0.21):
        for y in (-0.21,0.21):
            g.box('Lantern corner frame',(x,y,3.74),(0.045,0.045,0.68),'Iron',0.004)
    for z in (3.39,4.08):
        g.box('Lantern square cap',(0,0,z),(0.55,0.55,0.1),'Iron',0.008)
    bpy.ops.mesh.primitive_cone_add(vertices=4,radius1=0.43,radius2=0.06,depth=0.3,location=(0,0,4.27),rotation=(0,0,math.pi/4))
    g.finish(bpy.context.object,'Pyramidal lantern roof','Iron',0.008)


def planter():
    g.box('Stone planter',(0,0,0.32),(1.9,1.9,0.64),'Stone',0.014)
    g.box('Planter rim',(0,0,0.66),(2.05,2.05,0.17),'StoneTrim',0.012)
    g.box('Planter soil',(0,0,0.76),(1.75,1.75,0.055),'Soil',0.004)
    g.box('Clipped evergreen hedge',(0,0,1.13),(1.6,1.6,0.7),'Foliage',0.12)


def fountain():
    g.cylinder('Octagonal fountain plinth',(0,0,0.14),3.23,0.28,'StoneShade',0.012,8)
    g.cylinder('Fountain basin floor',(0,0,0.3),2.94,0.25,'Stone',0.012,48)
    ring('Fountain basin wall',2.56,2.94,0.34,0.96,'Stone')
    ring('Fountain coping',2.51,3.02,0.92,1.12,'StoneTrim')
    g.cylinder('Fountain pool water',(0,0,0.8),2.57,0.06,'Water',0,64)
    g.cylinder('Fountain central pedestal',(0,0,1.45),0.49,2.1,'StoneTrim',0.012,16)
    for z,radius in ((1.0,0.64),(2.35,0.6)):
        g.cylinder('Carved fountain collar',(0,0,z),radius,0.18,'StoneTrim',0.01,24)
    bpy.ops.mesh.primitive_cone_add(vertices=48,radius1=0.49,radius2=1.34,depth=0.45,location=(0,0,2.61))
    g.finish(bpy.context.object,'Upper fountain bowl','StoneTrim',0.008)
    ring('Upper bowl lip',1.19,1.36,2.8,3.0,'StoneTrim')
    g.cylinder('Upper bowl water',(0,0,2.9),1.2,0.04,'Water',0,48)
    g.cylinder('Fountain bronze nozzle',(0,0,3.1),0.13,0.4,'Brass',0.006,16)
    c.ellipsoid('Fountain nozzle crown',(0,0,3.37),(0.2,0.2,0.21),'Brass')
    for angle in (0,math.pi/2,math.pi,math.pi*1.5):
        dx,dy=math.cos(angle),math.sin(angle)
        curve_tube('Fountain falling water',[(1.22*dx,1.22*dy,2.92),(1.61*dx,1.61*dy,2.7),
                   (1.83*dx,1.83*dy,1.96),(1.9*dx,1.9*dy,0.84)],0.038,'WaterLight')
        for radius in (0.14,0.27):
            c.torus('Pool ripple',(1.9*dx,1.9*dy,0.84),radius,0.012,(0,0,0),'WaterLight')


def plaza():
    outline=[(-6.6,-8.6),(6.6,-8.6),(8.6,-6.6),(8.6,6.6),(6.6,8.6),(-6.6,8.6),(-8.6,6.6),(-8.6,-6.6)]
    vertices=[(x,y,z) for z in (0,0.25) for x,y in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    g.mesh('Octagonal paved public square',vertices,faces,'PavingGrid',0.015)
    for i in range(8):
        a,b=outline[i],outline[(i+1)%8]
        rod('Plaza border stone',(a[0],a[1],0.23),(b[0],b[1],0.23),0.22,'StoneTrim')
    r.shifted(fountain,(0,0,0.25))
    for angle in (0,math.pi/2,math.pi,math.pi*1.5):
        r.shifted(bench,(6.05*math.sin(angle),-6.05*math.cos(angle),0.25),angle+math.pi)
    for x in (-5.75,5.75):
        for y in (-5.75,5.75):
            r.shifted(planter,(x,y,0.25))
    for x in (-7.45,7.45):
        for y in (-4.0,4.0):
            r.shifted(plaza_lamp,(x,y,0.25))
    for i in range(8):
        angle=math.tau*i/8
        points=[(-0.18,3.65),(0.18,3.65),(0.1,4.55),(0,5.04),(-0.1,4.55)]
        xy=[(x*math.cos(angle)-y*math.sin(angle),x*math.sin(angle)+y*math.cos(angle)) for x,y in points]
        vertices=[(x,y,z) for z in (0.253,0.272) for x,y in xy]
        faces=[tuple(range(4,-1,-1)),tuple(range(5,10))]+[(j,(j+1)%5,(j+1)%5+5,j+5) for j in range(5)]
        g.mesh('Compass paving inlay',vertices,faces,'StoneShade')


def dome():
    columns,rows=72,20
    gap=math.pi/8
    start,end=-math.pi/2+gap,3*math.pi/2-gap
    vertices=[]
    for radius in (4.18,4.04):
        for j in range(rows+1):
            polar=0.045+(math.pi/2-0.045)*j/rows
            for i in range(columns+1):
                azimuth=start+(end-start)*i/columns
                vertices.append((radius*math.sin(polar)*math.cos(azimuth),radius*math.sin(polar)*math.sin(azimuth),7.4+radius*math.cos(polar)))
    stride=columns+1
    count=stride*(rows+1)
    faces=[]
    for j in range(rows):
        for i in range(columns):
            a=j*stride+i
            faces.extend(((a,a+1,a+stride+1,a+stride),(count+a+stride,count+a+stride+1,count+a+1,count+a)))
    for j in range(rows):
        for i in (0,columns):
            a=j*stride+i
            faces.append((a,a+stride,count+a+stride,count+a))
    for i in range(columns):
        for j in (0,rows):
            a=j*stride+i
            faces.append((a,count+a,count+a+1,a+1))
    shell=g.mesh('Slotted copper observing dome',vertices,faces,'Patina')
    for face in shell.data.polygons:
        face.use_smooth=True
    for i in range(9):
        azimuth=start+(end-start)*i/8
        points=[]
        for j in range(11):
            polar=0.048+(math.pi/2-0.048)*j/10
            points.append((4.2*math.sin(polar)*math.cos(azimuth),4.2*math.sin(polar)*math.sin(azimuth),7.4+4.2*math.cos(polar)))
        curve_tube('Dome bronze meridian rib',points,0.025,'Brass')


def telescope():
    g.cylinder('Telescope mounting plinth',(0,-0.25,7.61),0.63,0.34,'StoneTrim',0.012,24)
    g.cylinder('Telescope pedestal',(0,-0.25,8.1),0.2,0.82,'Iron',0.008,24)
    direction=Vector((0,-0.8,0.6))
    origin=Vector((0,-0.4,8.68))
    for x in (-0.48,0.48):
        rod('Telescope fork arm',(x,-0.25,8.0),(x,-0.4,8.75),0.13,'Brass')
    pipe('Telescope altitude axle',(-0.65,-0.4,8.68),(0.65,-0.4,8.68),0.11,'Brass')
    pipe('Brass refractor barrel',origin-direction*0.7,origin+direction*3.85,0.41,'Brass')
    for distance in (-0.64,0.52,2.91,3.8):
        center=origin+direction*distance
        pipe('Telescope barrel collar',center-direction*0.075,center+direction*0.075,0.47,'Iron')
    front=origin+direction*3.91
    pipe('Telescope recessed objective',front-direction*0.02,front+direction*0.01,0.35,'Lens')
    pipe('Telescope eyepiece',origin-direction*1.1,origin-direction*0.7,0.12,'Iron')


def observatory():
    g.cylinder('Observatory foundation',(0,0,0.24),4.88,0.48,'StoneShade',0.018,48)
    g.cylinder('Observatory stone rotunda',(0,0,3.8),4.5,6.98,'Stone',0.01,64)
    for z,height,radius in ((0.73,0.22,4.66),(4.78,0.17,4.6),(7.27,0.28,4.73)):
        g.cylinder('Rotunda stone cornice',(0,0,z),radius,height,'StoneTrim',0.012,64)
    ring('Copper dome base rail',4.06,4.39,7.38,7.57,'Brass')
    for angle in (-math.pi/3,math.pi/3,-2*math.pi/3,2*math.pi/3,math.pi):
        r.shifted(lambda:g.window(0,-4.54,2.0,0.95,2.3),angle=angle)
    r.door(0,-4.61,1.72,2.92,0.4)
    c.stairs(2.8,-6.04,-4.75,0.41)
    for x in (-1.39,1.39):
        t.lantern(x,-4.54,2.85)
    c.torus('Observatory star medallion',(0,-4.58,5.96),0.69,0.046,(math.pi/2,0,0),'Brass')
    star=[]
    for i in range(16):
        a=math.tau*i/16
        radius=0.58 if i%2==0 else 0.21
        star.append((radius*math.sin(a),5.96+radius*math.cos(a)))
    g.extrude('Eight point astronomer star',star,-4.63,-4.595,'Brass',0)
    g.box('Astronomer study foundation',(-5.1,0.3,0.21),(4.45,5.8,0.42),'StoneShade',0.018)
    g.box('Astronomer study walls',(-5.1,0.3,1.91),(4.05,5.4,3.4),'LibraryPlaster',0.015)
    g.box('Study eaves cornice',(-5.1,0.3,3.61),(4.3,5.65,0.21),'StoneTrim',0.012)
    r.shifted(lambda:r.roof(4.65,6.0,3.69,1.67),(-5.1,0.3,0))
    g.window(-5.3,-2.46,1.0,1.2,1.88)
    r.shifted(lambda:g.window(0,-7.19,1.0,1.16,1.9),angle=-math.pi/2)
    dome()
    telescope()


def materials():
    r.setup_materials()
    for name,color,kind in (('LibraryPlaster','B8B29F','plain'),('Ivory','E0D7BE','plain'),('Ink','78664C','plain'),
                            ('Lantern','D3AB62','plain'),('Patina','557C75','metal'),('Foliage','52654B','plain'),
                            ('Soil','514B3D','plain'),('Water','426A70','metal'),('WaterLight','789E9C','metal'),('Lens','263F49','metal')):
        g.MATERIALS[name]=r.surface(name,color,kind)
    for name in ('Water','WaterLight','Lens'):
        g.MATERIALS[name].node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=0.22
    mat=g.material('Fantasy_PavingGrid','A6A294')
    shader=mat.node_tree.nodes.get('Principled BSDF')
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    geom=nodes.new('ShaderNodeNewGeometry')
    brick=nodes.new('ShaderNodeTexBrick')
    links.new(geom.outputs['Position'],brick.inputs['Vector'])
    brick.inputs['Scale'].default_value=1
    brick.inputs['Brick Width'].default_value=1.1
    brick.inputs['Row Height'].default_value=0.75
    brick.inputs['Mortar Size'].default_value=0.018
    for key,color in (('Color1','A6A294'),('Color2','B8B09E'),('Mortar','79796E')):
        brick.inputs[key].default_value=(*r.rgb(color),1)
    links.new(brick.outputs['Color'],shader.inputs['Base Color'])
    bump=nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value=0.25
    bump.inputs['Distance'].default_value=0.025
    bump.invert=True
    links.new(brick.outputs['Fac'],bump.inputs['Height'])
    links.new(bump.outputs['Normal'],shader.inputs['Normal'])
    g.MATERIALS['PavingGrid']=mat


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials()
    assets,scenes=[],[]
    for index,(name,label,recipe,camera) in enumerate((('Library','図書館',library,(24,-38,23)),
            ('Plaza','広場',plaza,(25,-35,31)),('Observatory','天文台',observatory,(23,-36,24))),start=1):
        scene=bpy.context.scene if index==1 else bpy.data.scenes.new(name)
        scene.name=f'0{index} {name}'
        bpy.context.window.scene=scene
        asset=g.make_asset(name,recipe)
        asset[1]['facility_type']=label
        assets.append(asset)
        scenes.append(scene)
        r.stage(scene,list(asset[0].objects),camera,(0,0,4),name+'_Preview.png',(1900,1500))
        scene.view_settings.exposure=0.3
        print('FACILITY_CREATED',name,flush=True)
    overview=bpy.data.scenes.new('00 All Civic Facilities')
    bpy.context.window.scene=overview
    displays=[t.gallery_copy(asset,position,overview) for asset,position in zip(assets,((-20,1,0),(0,-1,0),(21,1,0)))]
    r.stage(overview,[obj for collection in displays for obj in collection.objects],(19,-64,43),(0,0,4),
            'Facilities_Preview.png',(2800,1300))
    overview.view_settings.exposure=0.3
    records=[]
    for collection,root in assets:
        points=[obj.matrix_parent_inverse@obj.matrix_basis@Vector(v) for obj in collection.objects if obj.type=='MESH' for v in obj.bound_box]
        records.append({'name':root.name,'facility_type':root['facility_type'],
                        'parts':sum(obj.type=='MESH' for obj in collection.objects),
                        'minimum':[min(p[i] for p in points) for i in range(3)],'maximum':[max(p[i] for p in points) for i in range(3)]})
    with open(os.path.join(REVIEW,'model_manifest.json'),'w') as handle:
        json.dump({'blender':bpy.app.version_string,'models':records},handle,indent=2,ensure_ascii=False)
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    for scene in [overview]+scenes:
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print('CIVIC_FACILITIES_COMPLETE',flush=True)


if __name__=='__main__':
    main()
