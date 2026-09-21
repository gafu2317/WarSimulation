"""Rebuild the study's focal props; run after refine_grand_study_details.py."""
import ast, math, sys
from pathlib import Path
import bpy
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/Art/GrandStudy'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/GrandStudy.blend'))
M={m.name:m for m in bpy.data.materials};current=None
for node in ast.parse((ROOT/'Tools/Blender/generate_grand_study.py').read_text()).body:
    if isinstance(node,ast.FunctionDef) and node.name in ['mesh','curve','lathe','ring','camera','area','setup']:
        exec(compile(ast.Module(body=[node],type_ignores=[]),'helpers','exec'),globals())

def block(name,loc,size,mat='Walnut',bevel=.003):
    vs=[(loc[0]+x*size[0]/2,loc[1]+y*size[1]/2,loc[2]+z*size[2]/2) for x,y,z in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    o=mesh(name,vs,[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)],mat)
    m=o.modifiers.new('Millimetre edge rounding','BEVEL');m.width=bevel;m.segments=3
    o.modifiers.new('Face normals','WEIGHTED_NORMAL');return o

def clear(name):
    global current
    current=bpy.data.collections[name]
    for obj in list(current.objects):bpy.data.objects.remove(obj,do_unlink=True)

def bezier(name,points,r=.004,mat='Brass'):
    pts=[]
    a,b,c,d=map(Vector,points)
    for i in range(49):
        t=i/48;pts.append((1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d)
    return curve(name,pts,r,mat)

def disk(name,r,h,loc,mat='Brass'):
    return lathe(name,[(0,0),(r,0),(r,h),(0,h)],loc,mat,64)

# A continuous hemispherical shell, with small separate glass cells and lead came.
clear('Desk_Lamp')
lathe('Cast lamp foot',[(0,0),(.12,0),(.139,.009),(.139,.016),(.126,.024),(.10,.033),(.074,.052),(.047,.081),(.027,.125),(.022,.31),(.036,.355),(.035,.37),(.020,.395),(.017,.465),(0,.47)],mat='Brass',n=64)
for z,r in [(.016,.135),(.036,.09),(.324,.025),(.371,.033)]:ring('Turned base bead',(0,0,z),r,.002)
shade=M.get('Opal glass') or bpy.data.materials.new('Opal glass');M[shade.name]=shade;shade.use_nodes=True
p=next(n for n in shade.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
p.inputs['Base Color'].default_value=(.72,.65,.43,1);p.inputs['Roughness'].default_value=.24;p.inputs['Transmission Weight'].default_value=.12
p.inputs['Subsurface Weight'].default_value=.10
R=.248;z0=.438
lathe('Opal glass continuous shell',[(R*math.sin(a),z0+.205*math.cos(a)) for a in [math.pi/2-i*math.pi/2/40 for i in range(41)]]+[(0,z0+.202)]+[((R-.003)*math.sin(a),z0+.202*math.cos(a)) for a in [i*math.pi/2/40 for i in range(1,41)]],(0,0,0),'Opal glass',96)
# Staggered cell boundaries follow the curved shell, not floating straight spokes.
for row in range(5):
    t1=.12+row*(math.pi/2-.12)/5;t2=.12+(row+1)*(math.pi/2-.12)/5
    count=[8,12,16,20,24][row];offset=(row%2)*math.pi/count
    for i in range(count):
        a=i*math.tau/count+offset
        pts=[((R+.0008)*math.sin(t)*math.cos(a),(R+.0008)*math.sin(t)*math.sin(a),z0+.2058*math.cos(t)) for t in [t1+(t2-t1)*j/12 for j in range(13)]]
        curve('Glass cell vertical came',pts,.0015,'Dark wood')
    ring('Glass cell horizontal came',(0,0,z0+.2058*math.cos(t2)),(R+.0008)*math.sin(t2),.0016,'Dark wood')
ring('Rolled shade rim',(0,0,z0),R,.0035)
lathe('Shade cap and finial',[(0,.638),(.037,.638),(.035,.646),(.015,.654),(.009,.663),(.013,.673),(.006,.687),(0,.693)],mat='Brass',n=48)
for a in [0,math.tau/3,2*math.tau/3]:
    bezier('Shade internal support',[(0,0,.405),(.03*math.cos(a),.03*math.sin(a),.47),(.16*math.cos(a),.16*math.sin(a),.47),(.235*math.cos(a),.235*math.sin(a),.442)],.003)

clear('Telephone')
block('Ebonised telephone foot',(0,0,.022),(.32,.255,.044),'Dark wood',.013)
block('Brass lower step',(0,0,.050),(.29,.227,.019),'Brass',.005)
block('Telephone enclosed case',(0,.016,.101),(.255,.181,.084),'Brass',.008)
block('Case shoulder',(0,.016,.15),(.276,.196,.016),'Brass',.006)
lathe('Central receiver pedestal',[(0,.157),(.04,.157),(.043,.171),(.024,.18),(.015,.224),(.028,.235),(.022,.247),(0,.249)],(0,.033,0),'Brass',48)
for x in [-.108,.108]:
    bezier('Curved cradle fork',[(0,.033,.215),(x*.5,.033,.205),(x,.033,.218),(x,.033,.271)],.006)
# Dial local Z points outwards and upwards; its rear remains clear of the case.
start=set(current.objects)
disk('Dial backing',.092,.011,(0,0,0),'Dark wood')
disk('Ivory numeral ring',.087,.006,(0,0,.011),'Cream')
ring('Dial outer brass lip',(0,0,.018),.089,.0025)
disk('Rotating dial plate',.076,.005,(0,0,.018),'Brass')
for i in range(10):
    a=math.radians(35+i*29);x=.059*math.cos(a);y=.059*math.sin(a)
    disk('Recessed finger socket',.009,.0006,(x,y,.0231),'Black')
    ring('Finger socket bevel',(x,y,.024),.009,.001,'Brass')
    data=bpy.data.curves.new('Dial numeral','FONT');data.body=str((i+1)%10);data.size=.011;data.align_x='CENTER';data.align_y='CENTER'
    obj=bpy.data.objects.new('Dial number '+data.body,data);current.objects.link(obj);data.materials.append(M['Ink']);obj.location=(.080*math.cos(a),.080*math.sin(a),.018)
disk('Dial centre label',.032,.002,(0,0,.024),'Cream');ring('Label bezel',(0,0,.027),.033,.0015)
curve('Dial finger stop',[(.067,-.055,.025),(.084,-.071,.027),(.083,-.075,.041)],.003)
rot=Matrix.Rotation(math.radians(58),4,'X');translation=Vector((0,-.142,.116))
for obj in set(current.objects)-start:obj.matrix_world=Matrix.Translation(translation)@rot@obj.matrix_world
# Turned wooden handgrip joins the metal receiver bells.
o=lathe('Receiver wood grip',[(0,-.088),(.016,-.088),(.020,-.075),(.016,-.055),(.015,.055),(.020,.075),(.016,.088),(0,.088)],mat='Walnut',n=48)
o.rotation_euler.y=math.pi/2;o.location=(0,.033,.292)
for x in [-.113,.113]:
    lathe('Receiver bell housing',[(0,0),(.046,0),(.047,.009),(.039,.016),(.031,.035),(.015,.052),(0,.055)],(x,.033,.242),'Black',48)
    ring('Receiver brass band',(x,.033,.251),.044,.003)
    bezier('Receiver neck',[(x,.033,.29),(x,.033,.31),(x*.85,.033,.303),(x*.72,.033,.292)],.011)
curve('Braided receiver cable',[(.137+.009*math.cos(i*.65),.036+.009*math.sin(i*.65),.265-i*.0015) for i in range(125)],.0025,'Black')

clear('Gramophone')
block('Gramophone bottom moulding',(0,0,.015),(.36,.335,.03),'Walnut',.006)
block('Gramophone cabinet',(0,0,.089),(.332,.303,.119),'Walnut',.005)
block('Gramophone top moulding',(0,0,.157),(.36,.335,.018),'Walnut',.004)
for z in [.037,.143]:
    curve('Cabinet fine brass inlay',[(-.168,-.155,z),(.168,-.155,z),(.168,.155,z),(-.168,.155,z)],.0012,'Brass',True)
disk('Turntable felt',.141,.007,(0,0,.168),'Dark wood');disk('Shellac record',.133,.003,(0,0,.177),'Black')
for i in range(24):ring('Record groove',(0,0,.1803),.047+i*.0034,.00018,'Dark wood')
disk('Record paper label',.043,.0005,(0,0,.1805),'Cream');disk('Spindle',.003,.011,(0,0,.181))
bezier('Continuous tone arm',[(.134,.083,.174),(.178,.019,.261),(.08,-.013,.246),(.048,-.069,.203)],.008)
o=disk('Sound box',.020,.01,(0,0,0),'Brass');o.rotation_euler.x=math.radians(65);o.location=(.048,-.069,.202)
curve('Record needle',[(.048,-.073,.195),(.045,-.077,.181)],.001,'Black')
bezier('Curved acoustic neck',[(.139,.107,.169),(.183,.13,.35),(.097,.13,.407),(.055,.104,.394)],.024)
# Thin open bell, radial facets and matching rolled rim, with no solid front cap.
profile=[]
for i in range(33):
    t=i/32;profile.append((.024+.181*t**2.1,.35*t))
profile+= [(r-.0025,z) for r,z in reversed(profile)]
o=lathe('Spun brass open horn',profile,mat='Brass',n=96)
rotation=Matrix.Rotation(math.radians(61),4,'X')@Matrix.Rotation(math.radians(-18),4,'Y')
o.matrix_world=Matrix.Translation((.055,.104,.394))@rotation
r=ring('Horn rolled lip',(0,0,.35),.205,.0035);r.matrix_world=o.matrix_world.copy()
for i in range(8):
    a=i*math.tau/8
    obj=curve('Horn shallow rib',[(r*math.cos(a),r*math.sin(a),z) for r,z in profile[:33]],.00065)
    obj.matrix_world=o.matrix_world.copy()
curve('Winding crank',[(.168,.02,.095),(.214,.02,.095),(.214,.02,.061),(.248,.02,.061)],.005)
o=lathe('Crank wooden grip',[(0,0),(.009,0),(.012,.01),(.01,.037),(0,.043)],mat='Walnut');o.rotation_euler.y=math.pi/2;o.location=(.24,.02,.061)

# Bake only primitive scales into cabinet caps, so bevel widths stay in metres.
for col in bpy.data.collections:
    if not col.get('category'):continue
    for obj in col.objects:
        if obj.type=='MESH' and obj.name.startswith('Plinth cornice'):
            obj.data=obj.data.copy();obj.data.transform(Matrix.Diagonal((*obj.scale,1)));obj.scale=(1,1,1)
            for mod in obj.modifiers:
                if mod.type=='BEVEL':mod.width=.005;mod.segments=3

# Terminate cabinet sides and back beneath the cap; coincident top faces flicker.
for name in ['Sideboard','Small_Cabinet','Bookcase_Filled','Display_Cabinet']:
    col=bpy.data.collections[name]
    caps=[o for o in col.objects if o.name.startswith('Plinth cornice')]
    cap=max(caps,key=lambda o:o.location.z)
    underside=min((cap.matrix_world@Vector(v)).z for v in cap.bound_box)
    for obj in col.objects:
        if obj.type!='MESH' or not obj.name.startswith(('Back','Upright')):continue
        corners=[obj.matrix_world@Vector(v) for v in obj.bound_box]
        lo=min(p.z for p in corners);hi=max(p.z for p in corners)
        if hi>underside+.0001:
            factor=(underside-lo)/(hi-lo)
            obj.location.z=lo+(obj.location.z-lo)*factor
            obj.scale.z*=factor
    print('CABINET CAP JOINT',name,round(underside,4),flush=True)

# Fine casting marks should not read as coarse hammered metal.
for node in M['Brass'].node_tree.nodes:
    if node.type=='BUMP':
        node.inputs['Strength'].default_value=.08
        node.inputs['Distance'].default_value=.00012

room=bpy.data.scenes['01 Furnished Study'];bpy.context.window.scene=room
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Blender/GrandStudy.blend'))
# Temporary contact sheet scene is not saved into the reusable asset file.
preview=bpy.data.scenes.new('Focal props review');setup(preview);preview.cycles.samples=48
current=bpy.data.collections.new('Preview surface');preview.collection.children.link(current)
block('Review table',(0,0,-.035),(2.3,1.7,.07),'Walnut',.005)
for name,x in [('Desk_Lamp',-.65),('Telephone',0),('Gramophone',.64)]:
    o=bpy.data.objects.new(name+' review',None);o.instance_type='COLLECTION';o.instance_collection=bpy.data.collections[name];o.location.x=x;preview.collection.objects.link(o)
area(preview,'Large softbox',(-1,-2,2),(0,0,.3),100,(1,.88,.7),2)
area(preview,'Edge light',(1,1,1.6),(0,0,.3),80,(.75,.85,1),1.4)
camera(preview,'Focal props',(1.1,-2.8,1.5),(0,0,.32),ortho=2.08)
bpy.context.window.scene=preview;preview.render.filepath=str(OUT/'Focal_Props_Closeup.png');bpy.ops.render.render(write_still=True)
print('FOCAL PROPS PREVIEW COMPLETE',flush=True)
if '--preview-only' in sys.argv:raise SystemExit
bpy.context.window.scene=room
shots=[('01_Entrance_to_Study',(2.53,-3.65,2.35),(-.20,1.1,1.35),23),('02_Window_to_Study',(-2.7,-.8,2.0),(.5,1.7,1.3),23),('03_Study_to_Lounge',(2.48,3.35,2.13),(-.3,-2.3,1.25),23),('04_Lounge_Corner',(2.5,.1,2.05),(-.65,-2.85,1),25)]
for name,pos,target,lens in shots:
    cam=room.camera;cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
    room.render.filepath=str(OUT/('Review_'+name+'.png'));bpy.ops.render.render(write_still=True)
    if name.startswith('01'):bpy.data.images['Render Result'].save_render(str(OUT/'Study_Overview.png'),scene=room)
    if name.startswith('04'):bpy.data.images['Render Result'].save_render(str(OUT/'Study_Lounge.png'),scene=room)
for name,file in [('02 Asset Catalogue','Asset_Catalogue.png'),('03 Furniture Detail','Furniture_Detail.png')]:
    sc=bpy.data.scenes[name];bpy.context.window.scene=sc;sc.render.filepath=str(OUT/file);bpy.ops.render.render(write_still=True)
print('FOCAL PROP PASS COMPLETE',flush=True)
