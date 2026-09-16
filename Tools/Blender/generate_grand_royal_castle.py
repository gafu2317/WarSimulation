"""Royal castle designed for a 60 x 54 metre plot."""
import bpy
import sys
import math
import json
import hashlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
import generate_school_levels as s

ROOT=Path(__file__).resolve().parents[2]
OUTPUT=ROOT/'ArtSource/Blender/GrandRoyalCastle.blend'
REVIEW=ROOT/'docs/Art/GrandRoyalCastle'
SOURCE=ROOT/'ArtSource/Blender/KingdomBuildings_RealisticFantasy.blend'

def place(b,recipe,x,y,z=0,angle=0):
    before={k:len(v[0]) for k,v in b.groups.items()}
    recipe()
    c=math.cos(angle);sn=math.sin(angle)
    for k,(vs,_) in b.groups.items():
        for i in range(before.get(k,0),len(vs)):
            a,d,h=vs[i];vs[i]=(x+a*c-d*sn,y+a*sn+d*c,z+h)

def hip(b,x,y,w,d,z,h):
    vs=[(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x+w/2,y+d/2,z),(x-w/2,y+d/2,z),(x,y-d*.2,z+h),(x,y+d*.2,z+h)]
    b.mesh('Palace slate roof',vs,[(0,1,4),(1,2,5,4),(2,3,5),(3,0,4,5)],'roof')
    for a,c in [(0,4),(1,4),(2,5),(3,5),(4,5)]:b.beam('Roof stone ridges',vs[a],vs[c],.1,'shade')
    for t in [.2,.4,.6,.8]:
        a=w/2*(1-t);d2=d/2*(1-t)+d*.2*t;zz=z+h*t+.025
        for sign in [-1,1]:b.beam('Slate courses',(x-a,y+sign*d2,zz),(x+a,y+sign*d2,zz),.035,'iron')
        for sign in [-1,1]:b.beam('Slate courses',(x+sign*a,y-d2,zz),(x+sign*a,y+d2,zz),.035,'iron')


def crenels(b,x,y,w,d,z,role='Battlements'):
    # Each edge uses one regular pitch; corner blocks belong only to the front/back edges.
    for yy in [-d/2,d/2]:
        b.box(role+' parapet',(x,y+yy,z+.38),(w,.45,.75),'stone')
    for xx in [-w/2,w/2]:
        b.box(role+' parapet',(x+xx,y,z+.38),(.45,d,.75),'stone')
    nx=max(2,round(w/1.25));ny=max(2,round(d/1.25))
    for i in range(nx+1):
        for yy in [-d/2,d/2]:
            b.box(role+' merlon',(x-w/2+i*w/nx,y+yy,z+1.04),(.6,.6,.62),'stone')
    for i in range(1,ny):
        for xx in [-w/2,w/2]:
            b.box(role+' merlon',(x+xx,y-d/2+i*d/ny,z+1.04),(.6,.6,.62),'stone')

def opening(b,x,y,z,w=.5,h=1.45,angle=0):
    place(b,lambda:s.aperture(b,0,0,z,w,h,style='military'),x,y,angle=angle)

def shaft(b,name,x,y,w,d,h):
    b.box(name+' masonry',(x,y,.4+h/2),(w,d,h),'stone')
    b.box(name+' plinth',(x,y,.7),(w+.24,d+.24,.6),'shade')
    for z in [1.2,h*.5,h+.2]:
        b.box(name+' belt',(x,y,z),(w+.16,d+.16,.18),'trim')
    for xx in [-w/2+.16,w/2-.16]:
        for yy in [-d/2+.16,d/2-.16]:
            b.box(name+' quoin',(x+xx,y+yy,.4+h/2),(.38,.38,h),'trim')

def tower(b,name,x,y,w,d,h,front=True,rear=True,left=True,right=True,crown_roof=False):
    shaft(b,name,x,y,w,d,h)
    b.box(name+' corbelled crown',(x,y,h+.42),(w+.55,d+.55,.32),'trim')
    crenels(b,x,y,w+.3,d+.3,h+.6,name)
    # Narrow defensive openings below; larger chambers only near the top.
    for z in [2.5,6,9.5,13,16.5,20,23.5]:
        if z+1.8>h:continue
        if name=='Royal keep' and z<4:continue
        ww=.22 if z<h-5 else .65
        for yy,enabled,a in [(y-d/2-.09,front,0),(y+d/2+.09,rear,math.pi)]:
            if enabled or (name=='Rear corner tower' and yy<y and z>=16.5):opening(b,x,yy,z,ww,1.55,a)
        for xx,enabled,a in [(x-w/2-.09,left,-math.pi/2),(x+w/2+.09,right,math.pi/2)]:
            if enabled or (name=='Rear corner tower' and z>=20):opening(b,xx,y,z,ww,1.55,a)
    for xx in [-w*.3,0,w*.3]:
        for yy in [y-d/2-.12,y+d/2+.12]:
            b.box(name+' crown corbel',(x+xx,yy,h-.12),(.32,.5,.75),'trim')
    if crown_roof:hip(b,x,y,w-1.5,d-1.5,h+.6,4.8)

def gallery(b,x):
    # An inhabited curtain joins the front and rear towers at flat end faces.
    w=8;d=26;y=-1;h=10
    shaft(b,'Residential curtain',x,y,w,d,h)
    hip(b,x,y,6.3,25.8,h+.4,3.2)
    for outer in [-1,1]:
        xx=x+outer*4
        b.box('Side wall parapet',(xx,y,h+.75),(.45,d,.7),'stone')
        for i in range(21):b.box('Side wall merlon',(xx,y-d/2+(i+.5)*d/21,h+1.35),(.6,.6,.55),'stone')
    inward=-1 if x>0 else 1
    for yy in [-10,-5,0,5,10]:
        for z in [2.2,5.6,8]:
            opening(b,x+inward*4.1,yy,z,.65,1.5,inward*math.pi/2)
        for z in [3.2,7]:
            opening(b,x-inward*4.1,yy,z,.2,1.3,-inward*math.pi/2)
    # Roof does not continue through the corner towers.

def rear_hall(b,x):
    shaft(b,'Great hall',x,16,11,10,15)
    hip(b,x,16,10.8,10.4,15.4,4)
    for xx in [-3.6,0,3.6]:
        for z in [2,5.3,8.6,11.9]:
            opening(b,x+xx,10.9,z,.7,1.7)
        for z in [3,7,11]:
            opening(b,x+xx,21.1,z,.3,1.5,math.pi)

def front_wall(b,x):
    shaft(b,'Front curtain',x,-19,11,2.4,8)
    crenels(b,x,-19,10.9,2.3,8.4,'Front curtain')
    for xx in [-3,0,3]:opening(b,x+xx,-20.3,4,.2,1.2)

def gatehouse(b):
    # A real open passage, surrounded by two substantial gatehouse towers.
    for side in [-1,1]:
        tower(b,'Gatehouse tower',side*5,-19,4,6,15,front=True,rear=True,left=side<0,right=side>0)
        b.box('Gate passage pier',(side*2.5,-19,4.2),(1,6,7.6),'stone')
        s.crest(b,side*6.2,-22.16,13.8,.8,2.6)
    profile=[(-2,8),(2,8)]+[(2*math.cos(i*math.pi/20),3+2*math.sin(i*math.pi/20)) for i in range(21)]
    b.prism('Open entrance vault',profile,-19,6,'stone')
    for y in [-22.09,-15.91]:
        b.curve('Entrance arch stones',[(2*math.cos(i*math.pi/20),y,3+2*math.sin(i*math.pi/20)) for i in range(21)],.2,'trim')
        for side in [-1,1]:b.box('Entrance jamb',(side*2,y,1.7),(.4,.3,2.6),'trim')
    b.box('Gate upper chamber',(0,-19,9.7),(6,6,3.4),'stone')
    for xx in [-1.7,0,1.7]:opening(b,xx,-22.1,8.8,.5,1.5)
    hip(b,0,-19,5.8,5.8,11.4,2.6)
    for xx in [-1.5,-1,-.5,0,.5,1,1.5]:
        b.box('Raised portcullis',(xx,-21.4,5.5),(.07,.09,1.2),'iron')
    for y in [-24,-22.5]:b.box('Gate approach paving',(0,y,.43),(4,1.4,.06),'trim')

def grounds(b):
    b.box('Castle stone terrace',(0,0,.2),(60,54,.4),'paving')
    # The inhabited wings ARE the defensive perimeter: there is no second enclosure.
    for sign in [-1,1]:
        tower(b,'Front corner tower',sign*22,-18,8,8,15,front=True,rear=False,left=sign<0,right=sign>0)
        tower(b,'Rear corner tower',sign*22,17,8,10,22,front=False,rear=True,left=sign<0,right=sign>0,crown_roof=True)
        gallery(b,sign*22)
        rear_hall(b,sign*12.5)
        front_wall(b,sign*12.5)
    # Keep and halls meet at vertical planes; roofs terminate before those planes.
    tower(b,'Royal keep',0,16,14,12,26,front=True,rear=True,left=False,right=False,crown_roof=True)
    s.aperture(b,0,9.86,.65,2,3,'door')
    for i in range(3):b.box('Keep entry steps',(0,9.5-i*.3,.55-i*.06),(3.4,.4,.16),'trim')
    for x in [-4,4]:
        for z in [5,9,13,17,21]:
            opening(b,x,9.9,z,.75,1.9)
        s.crest(b,math.copysign(5.4,x),9.8,24.5,1,2.3)
    b.beam('Royal standard pole',(0,16,31.4),(0,16,34),.065,'gold')
    b.prism('Royal banner',[(.1,33.9),(2.4,33.7),(2,32.6),(.1,32.75)],16,.05,'red')
    gatehouse(b)
    b.box('Courtyard central paving',(0,-2.5,.425),(4,24,.05),'trim')

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    for key,color in dict(s.PALETTE,glass='26383D',stone='ABA18C',trim='C4B89C',roof='34414D',paving='7C7B70').items():
        mapping={'stone':'Stone','trim':'StoneTrim','shade':'StoneShade','roof':'Roof','iron':'Iron','gold':'Brass','door':'Door','glass':'Window','paving':'Paving','wood':'Timber'}
        m=bpy.data.materials.get('Fantasy_'+mapping.get(key,''))
        if m is None:
            m=bpy.data.materials.new(key);m.diffuse_color=(*(int(color[i:i+2],16)/255 for i in [0,2,4]),1)
        s.MATS[key]=m
    b=s.Builder('Grand_Royal_Castle_Grounds');grounds(b);col,root=b.finish((0,0,0))
    root['plot_size_m']='60 x 54';root['plot_area_multiplier']=4;root['storey_height_m']=3.2
    scene.name='Fortified Royal Castle';scene.unit_settings.system='METRIC'
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=1600;scene.render.resolution_y=1300;scene.render.resolution_percentage=100
    sh=scene.display.shading;sh.color_type='MATERIAL';sh.light='STUDIO';sh.show_shadows=True;sh.show_cavity=True;sh.cavity_type='BOTH';sh.background_type='WORLD'
    scene.world=bpy.data.worlds.new('Castle World');scene.world.color=(.13,.15,.16)
    cam=bpy.data.objects.new('Castle Camera',bpy.data.cameras.new('Castle Camera'));scene.collection.objects.link(cam);scene.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=88;cam.data.clip_end=1000
    target=Vector((0,0,11));cam.location=target+Vector((70,-105,80));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    REVIEW.mkdir(parents=True,exist_ok=True);scene.render.filepath=str(REVIEW/'GrandRoyalCastle_Preview.png')
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT))
    bpy.ops.render.render(write_still=True)
    cam.location=(0,0,120);cam.rotation_euler=(0,0,0);cam.data.ortho_scale=78
    scene.render.filepath=str(REVIEW/'GrandRoyalCastle_Top.png');bpy.ops.render.render(write_still=True)
    target=Vector((0,0,11));cam.location=target+Vector((-70,105,70));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=88
    scene.render.filepath=str(REVIEW/'GrandRoyalCastle_Rear.png');bpy.ops.render.render(write_still=True)
    (REVIEW/'validation.json').write_text(json.dumps(dict(design='integrated fortified royal castle',plot_m=[60,54],area_ratio=4,storey_height_m=3.2,independent_annexes=0,perimeter_wall_rings=1,source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),unity_updated=False),indent=2))

if __name__=='__main__':main()
