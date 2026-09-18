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


CUTOUTS=[]
def clip(poly,axis,bound,positive):
    result=[]
    for a,c in zip(poly,poly[1:]+poly[:1]):
        da=(a[axis]-bound)*(1 if positive else -1);dc=(c[axis]-bound)*(1 if positive else -1)
        if da>=-1e-8:result.append(a)
        if (da>0 and dc<0) or (da<0 and dc>0):
            t=da/(da-dc);result.append(tuple(a[i]+t*(c[i]-a[i]) for i in range(3)))
    return result

def cut_rectangle(poly,box):
    remaining=poly;outside=[]
    for axis,value,inside_positive in [(0,box[0],True),(0,box[1],False),(1,box[2],True),(1,box[3],False)]:
        piece=clip(remaining,axis,value,not inside_positive)
        if len(piece)>2:outside.append(piece)
        remaining=clip(remaining,axis,value,inside_positive)
        if len(remaining)<3:break
    return outside

def cut_circle(poly,circle):
    x,y,r=circle
    remaining=poly;outside=[]
    for i in range(32):
        a=(i+.5)*math.tau/32;nx=math.cos(a);ny=math.sin(a);limit=r*math.cos(math.pi/32)
        def split(points,keep_inside):
            result=[]
            for u,v in zip(points,points[1:]+points[:1]):
                du=(u[0]-x)*nx+(u[1]-y)*ny-limit
                dv=(v[0]-x)*nx+(v[1]-y)*ny-limit
                if (du<=1e-8 if keep_inside else du>=-1e-8):result.append(u)
                if du*dv<0:
                    t=du/(du-dv);result.append(tuple(u[j]+t*(v[j]-u[j]) for j in range(3)))
            return result
        part=split(remaining,False)
        if len(part)>2:outside.append(part)
        remaining=split(remaining,True)
        if len(remaining)<3:break
    return outside

def roof(b,x,y,w,d,z,h,holes=()):
    vs=[(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x+w/2,y+d/2,z),(x-w/2,y+d/2,z),(x,y-d*.2,z+h),(x,y+d*.2,z+h)]
    faces=[(0,1,4),(1,2,5,4),(2,3,5),(3,0,4,5)]
    for f in faces:
        polys=[[vs[i] for i in f]]
        for hole in holes:polys=[p for poly in polys for p in (cut_circle(poly,hole) if len(hole)==3 else cut_rectangle(poly,hole))]
        for poly in polys:b.mesh('Slate roofs',poly,[tuple(range(len(poly)))],'roof')
    # Courses are clipped against the same holes as the roof surfaces.
    for t in [.16,.32,.48,.64,.8]:
        a=w/2*(1-t);dd=d/2*(1-t)+d*.2*t;zz=z+h*t+.025
        segments=[((x-a,y-dd,zz),(x+a,y-dd,zz)),((x-a,y+dd,zz),(x+a,y+dd,zz)),((x-a,y-dd,zz),(x-a,y+dd,zz)),((x+a,y-dd,zz),(x+a,y+dd,zz))]
        for start,end in segments:
            pieces=[(start,end)]
            for hole in holes:
                xmin,xmax,ymin,ymax=(hole[0]-hole[2],hole[0]+hole[2],hole[1]-hole[2],hole[1]+hole[2]) if len(hole)==3 else hole
                next_parts=[]
                for a1,a2 in pieces:
                    axis=0 if abs(a2[0]-a1[0])>.001 else 1;other=1-axis
                    low,high=(xmin,xmax) if axis==0 else (ymin,ymax)
                    ol,oh=(ymin,ymax) if other==1 else (xmin,xmax)
                    if not ol<=a1[other]<=oh:next_parts.append((a1,a2));continue
                    mn,mx=sorted([a1[axis],a2[axis]])
                    for lo,hi in [(mn,min(mx,low)),(max(mn,high),mx)]:
                        if hi-lo>.01:
                            p=list(a1);q=list(a2);p[axis]=lo;q[axis]=hi;next_parts.append((tuple(p),tuple(q)))
                pieces=next_parts
            for a1,a2 in pieces:b.beam('Slate courses',a1,a2,.022,'iron',4)

def blocks(b,x,y,w,h,z=.45,side=False):
    def draw():
        for row in range(int(h/.65)):
            zz=z+row*.65
            b.beam('Stone horizontal joints',(x-w/2,y,zz),(x+w/2,y,zz),.012,'mortar',4)
            for i in range(int(w/1.3)):
                xx=x-w/2+(i+.5*(row%2))*1.3
                if xx>x-w/2+.1:b.beam('Stone vertical joints',(xx,y,zz),(xx,y,min(zz+.65,z+h)),.011,'mortar',4)
    if side:
        place(b,draw,0,0,angle=math.pi/2)
    else:draw()

def masonry(b,name,x,y,w,d,h):
    b.box(name,(x,y,.4+h/2),(w,d,h),'stone')
    b.box('Foundation courses',(x,y,.65),(w+.18,d+.18,.5),'shade')
    for z in [1,h*.5,h+.25]:b.box('Building stringcourses',(x,y,z),(w+.12,d+.12,.16),'trim')
    for yy in [y-d/2-.012,y+d/2+.012]:blocks(b,x,yy,w,h)
    for xx in [x-w/2-.012,x+w/2+.012]:
        for row in range(int(h/.65)):
            z=.45+row*.65
            b.beam('Stone side joints',(xx,y-d/2,z),(xx,y+d/2,z),.012,'mortar',4)

def window(b,x,y,z,w=.6,h=1.65,a=0):
    place(b,lambda:s.aperture(b,0,0,z,w,h,style='arcane'),x,y,angle=a)

def round_tower(b,name,x,y,r,h,spire,angles=(0,math.pi/2,math.pi,3*math.pi/2),windows_start=2.3):
    b.cylinder(name,x,y,.4,r,h,'stone',32)
    b.cylinder('Tower splayed footing',x,y,.4,r+.18,.7,'shade',32,r)
    for row in range(1,int(h/.65)):
        z=.4+row*.65
        pts=[(x+(r+.012)*math.cos(i*math.tau/32),y+(r+.012)*math.sin(i*math.tau/32),z) for i in range(33)]
        b.curve('Tower masonry courses',pts,.012,'mortar')
    for z in [1,h*.5,h+.15]:b.cylinder('Tower carved belts',x,y,z,r+.1,.18,'trim',32)
    for z in [windows_start+i*3.5 for i in range(9)]:
        if z+1.8>=h:break
        for a in angles:
            place(b,lambda:window(b,0,-r-.07,z,.4,1.5),x,y,angle=a)
    b.cylinder('Tower corbel cornice',x,y,h+.35,r+.36,.45,'trim',32)
    for i in range(12):
        a=i*math.tau/12
        place(b,lambda:b.box('Tower corbels',(0,0,h-.05),(.3,.5,.75),'trim'),x+r*math.cos(a),y+r*math.sin(a),angle=a)
    # Open circular parapet, with its roof set inside the wall-walk.
    n=32;rr=r+.25;inner=r-.15;z=h+.57
    for i in range(n):
        a=i*math.tau/n;c=(i+1)*math.tau/n
        pts=[(x+rad*math.cos(t),y+rad*math.sin(t),zz) for zz in [z,z+.65] for rad,t in [(inner,a),(rr,a),(rr,c),(inner,c)]]
        b.mesh('Circular parapets',pts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'stone')
    for i in range(12):
        a=i*math.tau/12
        place(b,lambda:b.box('Circular merlons',(0,0,z+.95),(.55,.55,.65),'stone'),x+(r+.04)*math.cos(a),y+(r+.04)*math.sin(a),angle=a)
    b.cylinder('Conical slate spire',x,y,h+.6,r-.2,spire,'roof',32,.04)
    for frac in [.2,.4,.6,.8]:
        b.cylinder('Spire slate bands',x,y,h+.6+spire*frac,(r-.2)*(1-frac)+.018,.045,'iron',32)
    b.beam('Gilt finials',(x,y,h+.6+spire),(x,y,h+spire+1.4),.06,'gold')

def parapet(b,x,y,length,z):
    b.box('Curtain parapet',(x,y,z+.35),(length,.45,.7),'stone')
    count=round(length/1.3)
    for i in range(count):b.box('Curtain merlons',(x-length/2+(i+.5)*length/count,y,z+.95),(.62,.55,.6),'stone')

def wall(b,length):
    masonry(b,'Outer curtain masonry',0,0,length,1.7,7)
    for y in [-.7,.7]:parapet(b,0,y,length,7.4)
    for xx in [-length*.3,0,length*.3]:window(b,xx,-.95,3.7,.2,1.2)

def gate(b):
    for sign in [-1,1]:
        b.box('Gateway jamb masonry',(sign*2.8,-22,3.8),(1.6,3.8,6.8),'stone')
    profile=[(-2,7.2),(2,7.2)]+[(2*math.cos(i*math.pi/20),3+2*math.sin(i*math.pi/20)) for i in range(21)]
    b.prism('Open gateway vault',profile,-22,3.8,'stone')
    for y in [-24.02,-19.98]:
        b.curve('Gate arch stone ring',[(2*math.cos(i*math.pi/20),y,3+2*math.sin(i*math.pi/20)) for i in range(21)],.22,'trim')
        for sign in [-1,1]:b.box('Gate jamb cut stones',(sign*2,y,1.65),(.4,.3,2.6),'trim')
    b.box('Gate upper room',(0,-22,8.1),(7.2,3.8,1.8),'stone')
    parapet(b,0,-23.85,7.2,9)
    for xx in [-1.2,1.2]:window(b,xx,-24,7.45,.4,1.1)
    for sign in [-1,1]:
        round_tower(b,'Gate round tower',sign*4.5,-22,1.8,10.5,4.8,angles=(0,math.pi),windows_start=2.2)
        s.crest(b,sign*4.5,-23.98,9.8,.75,2.3)
    for xx in [-1.5,-1,-.5,0,.5,1,1.5]:b.box('Raised portcullis bars',(xx,-23,5.6),(.055,.07,.9),'iron')

def grounds(b):
    b.box('Castle stone terrace',(0,0,.2),(60,54,.4),'paving')
    for sign in [-1,1]:
        place(b,lambda:wall(b,38.7),sign*26,0,angle=sign*math.pi/2)
        for yy in [-22,22]:round_tower(b,'Outer corner tower',sign*26,yy,2.7,11.2,5.6)
    place(b,lambda:wall(b,46.8),0,22,angle=math.pi)
    for sign in [-1,1]:place(b,lambda:wall(b,17.2),sign*14.85,-22)
    gate(b)
    # Castle body: broad central hall with attached wings, not a ring of separate buildings.
    masonry(b,'Central great hall masonry',0,6.5,18,19,18)
    masonry(b,'Upper keep masonry',0,8,8,8,27)
    holes=[(-4.08,4.08,3.92,12.08)]
    for sign in [-1,1]:
        masonry(b,'Attached wing masonry',sign*13,2,8,28,10.6)
        for yy,hh in [(-3,21),(16,23.5)]:
            round_tower(b,'Body round tower',sign*9,yy,2,hh,6,angles=(0 if yy<0 else math.pi,),windows_start=2.2)
            holes.append((sign*9,yy,2.015))
        roof(b,sign*13,2,8.4,28.4,11,3.8,holes)
        for xx in [sign*12.5,sign*15.5]:
            for z in [2,5.5,8.5]:window(b,xx,-12.1,z,.6,1.5)
        for yy in [-9,-5,-1,3,7,11,14]:
            for z in [2,5.5,8.5]:window(b,sign*17.1,yy,z,.6,1.5,sign*math.pi/2)
    for sign in [-1,1]:
        for z in [2,5.5,8.5]:
            for xx in [sign*12.5,sign*15.5]:window(b,xx,16.1,z,.6,1.5,math.pi)
        for z in [3,7,11,15]:window(b,sign*6,16.1,z,.55,1.55,math.pi)
        for z in [13.5,16.5]:
            for yy in [1,6]:window(b,sign*9.1,yy,z,.55,1.5,sign*math.pi/2)
        for z in [3,7,11,15]:window(b,sign*1.8,16.1,z,.55,1.55,math.pi)
        for yy in [-9,-5]:
            for z in [2,5.5,8.5]:window(b,sign*8.9,yy,z,.6,1.5,-sign*math.pi/2)
    roof(b,0,6.5,18.4,19.4,18.4,5.6,holes)
    roof(b,0,8,8.6,8.6,27.4,7)
    for z in [5,8.5,12,15.5]:
        for xx in [-4,0,4]:window(b,xx,-3.1,z,.85,2.05)
    s.aperture(b,0,-3.14,.7,2,3,'door')
    for i in range(3):b.box('Main entry steps',(0,-3.5-i*.3,.58-i*.07),(3.4,.4,.16),'trim')
    for sign in [-1,1]:s.crest(b,sign*6,-3.17,16.7,1.2,4.5)
    for a in [0,math.pi/2,math.pi,3*math.pi/2]:
        for z in [25]:
            place(b,lambda:window(b,0,-4.1,z,.75,1.55),0,8,angle=a)
    for yy in [3.95,12.05]:parapet(b,0,yy,8.2,27.25)
    for xx in [-4.05,4.05]:place(b,lambda:parapet(b,0,0,8.2,27.25),xx,8,angle=math.pi/2)
    # Front-facing corbelled battlement below the principal roof.
    parapet(b,0,-3.16,14,18.5)
    for xx in range(-6,7):
        b.box('Great hall corbels',(xx,-3.16,18.2),(.35,.55,.7),'trim')
    b.beam('Royal flag pole',(0,8,34.4),(0,8,36.5),.065,'gold')
    b.prism('Royal red flag',[(.1,36.4),(2.4,36.15),(2,35.25),(.1,35.4)],8,.045,'red')
    b.box('Central approach',(0,-14,.425),(4,21,.05),'trim')
    # Shallow planting beds sit outside the circulation route.
    for sign in [-1,1]:
        b.box('Side garden stone rim',(sign*20,2,.62),(1.6,5,.35),'trim')
        b.box('Side garden soil',(sign*20,2,.82),(1.3,4.7,.08),'shade')
        for yy in [.4,1.2,2,2.8,3.6]:
            b.cylinder('Low garden shrubs',sign*20,yy,.85,.48,.55,'ground',8,.35)
    for x in [-21,-20]:
        b.box('Rear service crate',(x,12,.85),(.8,.8,.9),'wood')
        for z in [.55,1.15]:b.box('Crate iron band',(x,12,z),(.83,.83,.06),'iron')



def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    for key,color in dict(s.PALETTE,glass='26383D',stone='ABA18C',trim='C4B89C',roof='34414D',paving='7C7B70',mortar='8D8677').items():
        mapping={'stone':'Stone','trim':'StoneTrim','shade':'StoneShade','roof':'Roof','iron':'Iron','gold':'Brass','door':'Door','glass':'Window','paving':'Paving','wood':'Timber'}
        m=bpy.data.materials.get('Fantasy_'+mapping.get(key,''))
        if m is None:
            m=bpy.data.materials.new(key);m.diffuse_color=(*(int(color[i:i+2],16)/255 for i in [0,2,4]),1)
        m.use_nodes=True
        shader=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value=m.diffuse_color
        shader.inputs['Roughness'].default_value=.85
        shader.inputs['Emission Strength'].default_value=0
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
    (REVIEW/'validation.json').write_text(json.dumps(dict(design='user reference: round spires, central castle, separate single curtain',plot_m=[60,54],area_ratio=4,storey_height_m=3.2,independent_annexes=0,perimeter_wall_rings=1,source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),unity_updated=False),indent=2))

if __name__=='__main__':main()
