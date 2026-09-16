import bpy
import math
import json
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'ArtSource/Blender/FantasySchoolLevels.blend'
REVIEW = ROOT / 'docs/Art/FantasySchoolLevels'
KINDS = ['WarriorAcademy', 'ArcaneAcademy', 'SpiritAcademy']
PALETTE = {'stone':'B5AA92','trim':'DED1B2','shade':'877D68','wood':'71553A','oak':'B29461',
           'roof':'414752','blue':'315371','green':'365C4D','tile':'476B57','glass':'275C70',
           'leafglass':'507E64','door':'4B382A','iron':'33393B','gold':'B99A5B','red':'963F32',
           'white':'E6DBC2','ground':'686B54','paving':'B7B09B'}
MATS={}

class Builder:
    def __init__(self,name):self.name=name;self.groups={}
    def mesh(self,role,vs,fs,mat):
        vertices,faces=self.groups.setdefault((role,mat),([],[]));offset=len(vertices)
        vertices.extend(tuple(v) for v in vs);faces.extend(tuple(i+offset for i in f) for f in fs)
    def box(self,role,c,d,mat):
        x,y,z=c;a,b,h=[v/2 for v in d]
        self.mesh(role,[(x+sx*a,y+sy*b,z+sz*h) for sz in [-1,1] for sy in [-1,1] for sx in [-1,1]],[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],mat)
    def prism(self,role,profile,y,depth,mat):
        n=len(profile);vs=[(x,yy,z) for yy in [y-depth/2,y+depth/2] for x,z in profile]
        self.mesh(role,vs,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
    def beam(self,role,a,b,r,mat,sides=6):
        a,b=Vector(a),Vector(b);direction=(b-a).normalized();u=direction.cross(Vector((0,0,1)))
        if u.length<.01:u=direction.cross(Vector((0,1,0)))
        u.normalize();v=direction.cross(u);vs=[p+r*(u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides)) for p in [a,b] for i in range(sides)]
        self.mesh(role,vs,[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)],mat)
    def curve(self,role,points,r,mat):
        for a,b in zip(points,points[1:]):self.beam(role,a,b,r,mat)
    def cylinder(self,role,x,y,z,r,h,mat,n=16,r2=None):
        r2=r if r2 is None else r2
        vs=[(x+rr*math.cos(i*math.tau/n),y+rr*math.sin(i*math.tau/n),zz) for zz,rr in [(z,r),(z+h,r2)] for i in range(n)]
        self.mesh(role,vs,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
    def finish(self,offset):
        col=bpy.data.collections.new(self.name);bpy.context.scene.collection.children.link(col)
        root=bpy.data.objects.new(self.name,None);col.objects.link(root);root.location=offset;root['asset_root']=True;root['plot_size_m']=26
        for (role,mat),(vs,fs) in self.groups.items():
            mesh=bpy.data.meshes.new(self.name+' '+role);mesh.from_pydata(vs,[],fs);mesh.update()
            obj=bpy.data.objects.new(role,mesh);col.objects.link(obj);obj.data.materials.append(MATS[mat]);obj.parent=root
        return col,root


def arch(x,z,w,h):
    return [(x-w/2,z),(x+w/2,z)]+[(x+w/2*math.cos(i*math.pi/12),z+h-w/2+w/2*math.sin(i*math.pi/12)) for i in range(13)]

def pointed(x,z,w,h):
    return [(x-w/2,z),(x+w/2,z),(x+w/2,z+h*.65),(x+w*.32,z+h*.85),(x,z+h),(x-w*.32,z+h*.85),(x-w/2,z+h*.65)]

def aperture(b,x,y,z,w,h,kind='window',style='stone',side=False):
    before={k:(len(v[0]),len(v[1])) for k,v in b.groups.items()}
    profile=arch if style=='military' or kind=='door' else pointed
    trim='oak' if style=='spirit' else 'trim';glass='leafglass' if style=='spirit' else 'glass'
    b.prism('Door frame' if kind=='door' else 'Window frame',profile(x,z-.13,w+.32,h+.26),y,.16,trim)
    b.prism('Door panel' if kind=='door' else 'Window glass',profile(x,z,w,h),y-.11,.04,'door' if kind=='door' else glass)
    if kind=='window':
        b.beam('Window tracery',(x,y-.16,z+.05),(x,y-.16,z+h-.15),.045,trim)
        b.beam('Window transom',(x-w*.46,y-.16,z+h*.43),(x+w*.46,y-.16,z+h*.43),.045,trim)
        b.box('Window sill',(x,y-.04,z-.19),(w+.5,.4,.16),trim)
    else:
        for xx in [-w*.32,0,w*.32]:b.beam('Door joinery',(x+xx,y-.16,z+.1),(x+xx,y-.16,z+h*.7),.025,'oak')
        for zz in [.65,1.45]:b.box('Door iron',(x,y-.17,z+zz),(w*.92,.05,.1),'iron')
    if side:
        for k,(vs,_) in b.groups.items():
            start=before.get(k,(0,0))[0]
            for i in range(start,len(vs)):
                xx,yy,zz=vs[i];vs[i]=(-yy,xx,zz)

def roof(b,x,y,w,d,z,h,mat,curved=False):
    segments=12
    def height(t):return z+h*(1-abs(t))**(1.5 if curved else 1)+(.55*abs(t)**8 if curved else 0)
    for i in range(segments):
        a=-1+2*i/segments;bb=-1+2*(i+1)/segments
        xa=x+a*w/2;xb=x+bb*w/2;za=height(a);zb=height(bb)
        b.mesh('Roof surfaces',[(xa,y-d/2,za),(xb,y-d/2,zb),(xb,y+d/2,zb),(xa,y+d/2,za)],[(0,1,2,3)],mat if i%3 else ('tile' if curved else mat))
        for row in range(1,5):
            t=a+(bb-a)*row/5;xx=x+t*w/2;zz=height(t)+.015
            b.beam('Roof courses',(xx,y-d/2,zz),(xx,y+d/2,zz),.015,'oak' if curved else 'shade',4)
    trim='oak' if curved else 'trim'
    for yy in [y-d/2,y+d/2]:
        b.curve('Gable verge',[(x+(-1+2*i/segments)*w/2,yy,height(-1+2*i/segments)+.04) for i in range(segments+1)],.11,trim)
        if curved:
            b.prism('Gable infill',[(x-w/2,z-.05),(x+w/2,z-.05)]+[(x+(1-2*i/segments)*w/2,height(1-2*i/segments)-.07) for i in range(segments+1)],yy+(.12 if yy<y else -.12),.14,'wood')
        else:
            face_y=yy+(.12 if yy<y else -.12)
            apex=(x,face_y,z+h-.07)
            left=(x-w/2,face_y,z-.05)
            right=(x+w/2,face_y,z-.05)
            b.mesh('Gable infill',[left,right,apex],[(0,1,2),(2,1,0)],'stone')
    b.beam('Roof ridge',(x,y-d/2-.2,z+h+.08),(x,y+d/2+.2,z+h+.08),.12,trim)
    for yy in [y-d/2,y+d/2]:b.beam('Roof finial',(x,yy,z+h),(x,yy,z+h+.6),.065,'gold')

def hip(b,x,y,w,d,z,h):
    vs=[(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x+w/2,y+d/2,z),(x-w/2,y+d/2,z),(x,y-d*.28,z+h),(x,y+d*.28,z+h)]
    b.mesh('Slate hip roof',vs,[(0,1,4),(1,2,5,4),(2,3,5),(3,0,4,5)],'roof')
    for a,bb in [(0,4),(1,4),(2,5),(3,5),(4,5)]:b.beam('Hip roof seam',vs[a],vs[bb],.06,'shade')

def crest(b,x,y,z,w,h):
    b.prism('Red school standards',[(x-w/2,z),(x+w/2,z),(x+w/2,z-h+.25),(x,z-h),(x-w/2,z-h+.25)],y,.04,'red')
    b.beam('Banner rail',(x-w*.6,y-.03,z+.1),(x+w*.6,y-.03,z+.1),.055,'gold')
    b.beam('White sword emblem',(x,y-.06,z-h*.2),(x,y-.06,z-h*.76),.055,'white')
    b.beam('White sword guard',(x-w*.22,y-.06,z-h*.56),(x+w*.22,y-.06,z-h*.56),.05,'white')

def crown(b,x,y,w,d,z):
    b.box('Battlement cornice',(x,y,z),(w+.35,d+.35,.3),'trim')
    nx=max(2,round(w/1.15));ny=max(2,round(d/1.15))
    for i in range(nx+1):
        for yy in [-d/2,d/2]:
            b.box('Merlons',(x-w/2+w*i/nx,y+yy,z+.43),(.55,.55,.65),'stone')
    for i in range(1,ny):
        for xx in [-w/2,w/2]:
            b.box('Merlons',(x+xx,y-d/2+d*i/ny,z+.43),(.55,.55,.65),'stone')


def base(b,w,d,h,style,level=1):
    b.box('Fixed 26m plot',(0,0,.15),(26,26,.3),'paving')
    b.box('School foundation',(0,0,.48),(w+.6,d+.6,.36),'shade')
    b.box('Main school walls',(0,0,.6+h/2),(w,d,h),'stone' if style!='spirit' else 'wood')
    front=-d/2
    b.box('Projecting entrance bay',(0,front-.45,.6+h/2),(5.4,1.5,h),'wood' if style=='spirit' else 'stone')
    for z in [.82,h+.48]+([4.9] if h>7 else []):b.box('Stone stringcourse',(0,0,z),(w+.2,d+.2,.16),'oak' if style=='spirit' else 'trim')
    for x in [-w*.34,-w*.19,w*.19,w*.34]:
        for z in ([1.3,5.55] if h>7 else [1.2]):aperture(b,x,front-.12,z,1.02,min(2.65,h+.25-z) if h>7 else 2.45,style=style)
    for side in [-1,1]:
        for yy in [-d*.28,0,d*.28]:
            for z in ([1.3,5.55] if h>7 else [1.2]):
                if style=='spirit' and level==5 and z<3 and yy==0:continue
                before={k:len(v[0]) for k,v in b.groups.items()}
                aperture(b,yy,-w/2-.12,z,1.05,min(2.5,h+.25-z),style=style,side=True)
                if side<0:
                    for k,(vs,_) in b.groups.items():
                        for i in range(before.get(k,0),len(vs)):xx,y,zv=vs[i];vs[i]=(-xx,-y,zv)
    for x in ([-w*.27,0,w*.27] if (style=='military' and level>=3) or (style=='arcane' and level>=4) else [-w*.4,-w*.23,0,w*.23,w*.4]):
        for z in ([1.3,5.55] if h>7 else [1.2]):
            before={k:len(v[0]) for k,v in b.groups.items()};aperture(b,x,front-.12,z,1,min(2.4,h+.25-z),style=style)
            for k,(vs,_) in b.groups.items():
                for i in range(before.get(k,0),len(vs)):xx,y,zv=vs[i];vs[i]=(-xx,-y,zv)
    aperture(b,0,front-1.3,.65,2,3.3,'door',style)
    if h>7:aperture(b,0,front-1.3,5.2,1.45,min(2.8,h-4.95),style=style)
    for side in [-1,1]:b.box('Portal stone piers',(side*2.6,front-1.25,.6+h/2),(.24,.25,h),'oak' if style=='spirit' else 'trim')
    for i in range(3):b.box('Entrance stairs',(0,front-1.45-i*.35,.5-i*.13),(3.4,.55,.24),'trim')
    b.box('Entrance path',(0,(front-2.4-13)/2,.32),(3,front-2.4+13,.04),'paving')
    for side in [-1,1]:
        b.box('Grass edging',(side*(w/2+.75),0,.33),(.6,d,.06),'ground')
        if style=='spirit':b.box('Facade pilasters',(side*w/2,front-.15,.6+h/2),(.25,.32,h),'oak')

def military(b,lv):
    w=17+lv;d=15+lv*.7;h=[5.3,7,8.4,9.3,10.1][lv-1];base(b,w,d,h,'military',lv);hip(b,0,0,w-.4,d-.4,h+.6,1.5)
    if lv<3:
        for side in [-1,1]:crest(b,side*w*.43,-d/2-.18,h+.1,1.3,min(3.8,h-1.5))
    if lv>=2:
        tw=4.2+lv*.2;th=h+[0,4.5,5.6,6.5,8][lv-1]
        b.box('Central command tower',(0,2.3,.6+th/2),(tw,4.8,th),'stone');crown(b,0,2.3,tw,4.8,th+.65)
        aperture(b,0,-.23,max(h+2.4,th-3),1.1,2.1,style='military')
        if lv>=4:crest(b,tw*.32,-.28,th-.2,.65,2.8)
    if lv>=3:
        crown(b,0,-d/2-.45,5.6,1.6,h+.8)
        for x in [-w/2+1.3,w/2-1.3]:
            for y in [-d/2+1.3,d/2-1.3]:
                b.box('Corner bastion',(x,y,.6+(h+.6)/2),(2.7,2.7,h+.6),'stone');crown(b,x,y,2.8,2.8,h+1.3)
                for z in [1,4.6]:b.box('Bastion belt',(x,y,z),(2.85,2.85,.18),'trim')
        for side in [-1,1]:crest(b,side*(w/2-1.3),-d/2-.13,h+.45,1.05,3.7)
    if lv==5:
        for side in [-1,1]:
            x=side*3.5;b.beam('Ceremonial flagstaff',(x,2.3,h+1),(x,2.3,h+7.5),.055,'gold');crest(b,x,2.3,h+7,1,3.4)

def dome(b,x,y,z,r,h):
    rings=10;n=32;vs=[]
    for j in range(rings+1):
        t=j/rings*math.pi/2;rr=r*max(.035,math.cos(t))
        vs.extend((x+rr*math.cos(i*math.tau/n),y+rr*math.sin(i*math.tau/n),z+h*math.sin(t)) for i in range(n))
    b.mesh('Blue central dome',vs,[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(rings) for i in range(n)],'blue')
    for i in range(8):
        a=i*math.tau/8;b.curve('Dome gilded ribs',[(x+(r*math.cos(j/rings*math.pi/2)+.025)*math.cos(a),y+(r*math.cos(j/rings*math.pi/2)+.025)*math.sin(a),z+h*math.sin(j/rings*math.pi/2)) for j in range(rings+1)],.055,'gold')
    b.cylinder('Dome lantern',x,y,z+h,.24,.75,'trim',8);b.cylinder('Lantern cap',x,y,z+h+.75,.4,.7,'blue',8,.025)

def rose(b,y,z,spirit=False):
    material='oak' if spirit else 'trim'
    points=[(1.12*math.cos(i*math.tau/32),y,z+1.12*math.sin(i*math.tau/32)) for i in range(33)]
    b.curve('Rose window rim',points,.12,material)
    b.prism('Rose window glazing',[(math.cos(i*math.tau/24),z+math.sin(i*math.tau/24)) for i in range(24)],y+.06,.04,'leafglass' if spirit else 'glass')
    for i in range(8):
        a=i*math.tau/8;b.beam('Rose tracery',(0,y-.06,z),(.95*math.cos(a),y-.06,z+.95*math.sin(a)),.055,material)

def arcane(b,lv):
    w=15.5+lv*1.3;d=15+lv*.8;h=[5.6,6,7.1,8.5,9.6][lv-1];base(b,w,d,h,'arcane',lv);roof(b,0,0,w+.4,d+.6,h+.6,4,'blue');roof(b,0,-d/2,6,3,h+.6,3.2,'blue');rose(b,-d/2-1.55,h+1.65)
    if lv>=2:
        rad=1.1+lv*.44;dh=1.8+lv*.36;top=h+6.3
        b.cylinder('Octagonal observatory drum',0,2.2,h+2.6,rad,3.7,'stone',8)
        for x in [-rad*.45,rad*.45]:aperture(b,x,2.2-rad-.08,h+4.15,.64,1.8,style='arcane')
        b.cylinder('Dome cornice',0,2.2,top,rad+.2,.22,'trim',32);dome(b,0,2.2,top+.15,rad+.12,dh)
    if lv>=4:
        for x in [-w/2+.6,w/2-.6]:
            for y in ([-d/2+.7] if lv==4 else [-d/2+.7,d/2-.7]):
                z=h+2.3;b.cylinder('Corner turret',x,y,.6,1.05,z,'stone',8)
                b.cylinder('Turret crown',x,y,z+.6,1.18,.25,'trim',16);b.cylinder('Turret spire',x,y,z+.85,1.3,2.7,'blue',16,.02)
                b.beam('Turret finial',(x,y,z+3.4),(x,y,z+4.2),.045,'gold')
                aperture(b,x,y-1.07,h,.55,1.7,style='arcane')
    if lv>=3:
        for x in [-w*.3,w*.3]:
            b.box('Roof dormer',(x,-d*.27,h+2.8),(2,2,1.6),'stone');roof(b,x,-d*.27,2.4,2.4,h+3.65,1.1,'blue');aperture(b,x,-d*.27-1.08,h+2.35,.6,1.05,style='arcane')

def spirit(b,lv):
    w=16.5+lv*1.05;d=15+lv*.65;h=[4.7,5.4,6.4,7.2,8.2][lv-1];base(b,w,d,h,'spirit',lv);roof(b,0,0,w+1.1,d+.8,h+.55,4.2,'green',True);roof(b,0,-d/2,6,3,h+.6,3.3,'green',True);rose(b,-d/2-1.55,h+1.6,True)
    front=-d/2-.27
    for x in [-w*.43,-w*.27,w*.27,w*.43]:
        b.beam('Exposed timber posts',(x,front,.6),(x,front,h+.6),.12,'oak')
        for side in [-1,1]:b.curve('Branching timber braces',[(x,front,h-2),(x+side*.2,front,h-.8),(x+side*.55,front,h+.5)],.1,'oak')
    if lv>=2:
        th=h+[0,2.5,4.6,6.4,8][lv-1];tw=3.6+lv*.18
        b.box('Tree shaped bell tower',(0,2.4,(h+th)/2),(tw,4.2,th-h),'wood')
        for x in [-tw/2,tw/2]:b.beam('Tower timber posts',(x,.25,h),(x,.25,th+.4),.14,'oak')
        if lv>=3:aperture(b,0,.18,th-2.6,1.1,2.2,style='spirit')
        roof(b,0,2.4,tw+1.1,5.1,th+.2,3.8,'green',True)
    if lv>=3:
        for x in [-w*.3,w*.3]:
            b.box('Leaf dormer',(x,-d*.23,h+2.4),(1.8,1.8,1.8),'wood');roof(b,x,-d*.23,2.3,2.2,h+3.3,1.4,'green',True);aperture(b,x,-d*.23-.98,h+2,.65,1.1,style='spirit')
    if lv==5:
        for side in [-1,1]:
            x=side*(w/2-.8);b.box('Side garden vestibule',(x,1,2.05),(3,8,2.9),'wood');roof(b,x,1,3.7,8.7,3.6,1.6,'green',True)


def shield(b,x,y,z,size):
    outline=[(-.52,.55),(.52,.55),(.46,-.12),(0,-.65),(-.46,-.12)]
    b.prism('Decor shield stone border',[(x+a*size,z+c*size) for a,c in outline],y,.16,'trim')
    b.prism('Decor shield red enamel',[(x+a*size*.78,z+c*size*.78) for a,c in outline],y-.11,.06,'red')
    for sign in [-1,1]:
        b.beam('Decor crossed blades',(x-sign*.26*size,y-.18,z-.28*size),(x+sign*.26*size,y-.18,z+.32*size),.035*size,'white')

def star(b,x,y,z,r):
    points=[]
    for i in range(16):
        a=math.pi/2+i*math.tau/16;rr=r if i%2==0 else r*.3
        points.append((x+rr*math.cos(a),z+rr*math.sin(a)))
    b.prism('Decor celestial stars',points,y,.065,'gold')

def leaf(b,x,y,z,size,sign=1):
    b.prism('Decor carved leaves',[(x,z),(x+sign*size*.5,z+size*.17),(x+sign*size*.6,z+size*.55),(x+sign*size*.22,z+size*.47)],y,.065,'oak')
    b.beam('Decor leaf veins',(x,y-.05,z),(x+sign*size*.5,y-.05,z+size*.46),.025,'gold')

def ornaments(b,kind,lv):
    if kind=='WarriorAcademy':
        w=17+lv;d=15+lv*.7;h=[5.3,7,8.4,9.3,10.1][lv-1]
    elif kind=='ArcaneAcademy':
        w=15.5+lv*1.3;d=15+lv*.8;h=[5.6,6,7.1,8.5,9.6][lv-1]
    else:
        w=16.5+lv*1.05;d=15+lv*.65;h=[4.7,5.4,6.4,7.2,8.2][lv-1]
    front=-d/2
    spirit=kind=='SpiritAcademy';mat='oak' if spirit else 'trim'
    # Added ornament stays outside wall faces and between existing openings.
    for side in [-1,1]:
        for yy in [-d*.42,-d*.14,d*.14,d*.42]:
            x=side*(w/2+.12)
            if spirit and lv==5 and abs(yy)<4:continue
            if abs(yy)>d*.4 and ((kind=='WarriorAcademy' and lv>=3) or (kind=='ArcaneAcademy' and lv>=4)):continue
            b.box('Decor side pilasters',(x,yy,.65+h/2),(.32,.3,h-.15),mat)
            b.box('Decor pilaster foot',(x,yy,.95),(.48,.55,.55),mat)
            b.box('Decor pilaster capital',(x,yy,h+.23),(.48,.62,.28),mat)
        if not spirit and not ((kind=='WarriorAcademy' and lv>=3) or (kind=='ArcaneAcademy' and lv>=4)):
            for z in [1.25+i*.65 for i in range(int((h-1)/.65))]:
                b.box('Decor corner quoins',(side*(w/2-.12),front-.12,z),(.65,.3,.3),'trim')
                b.box('Decor rear quoins',(side*(w/2-.12),-front+.12,z),(.65,.3,.3),'trim')
    if kind=='WarriorAcademy':
        shield(b,0,front-1.48,4.52,.62)
        if lv>=2:
            for side in [-1,1]:
                shield(b,side*w*.265,front-.16,4.4,.62)
        if lv>=3:
            for i in range(15):
                x=-w*.43+i*w*.86/14
                if 3<abs(x)<w/2-2.9:b.box('Decor cornice dentils',(x,front-.25,h+.2),(.22,.35,.28),'trim')
    elif kind=='ArcaneAcademy':
        star(b,0,front-1.5,4.52,.32)
        for side in [-1,1]:
            star(b,side*w*.27,front-.29,h+1.2,.4+lv*.025)
            if lv>=2:
                x=side*w*.265
                b.prism('Decor blue heraldic panel',pointed(x,4.05,.64,.65),front-.12,.1,'blue')
                star(b,x,front-.21,4.38,.2)
        if lv>=4:
            for i in range(17):
                x=-w*.44+i*w*.88/16
                if 3<abs(x)<w/2-1.8:
                    b.box('Decor blue cornice inlay',(x,front-.17,h+.28),(.35,.1,.16),'blue')
    else:
        for side in [-1,1]:
            x=side*2.1;y=front-1.27
            b.curve('Decor portal vine',[(x+side*.1*math.sin(i*.8),y,1+i*.18) for i in range(20)],.055,'oak')
            for i in range(2+lv):leaf(b,x,y-.05,1.3+i*.39,.44,side*(-1 if i%2 else 1))
        if lv>=3:
            for side in [-1,1]:
                x=side*w*.27
                leaf(b,x,front-.43,4.15,.5,side)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.name='School Levels - Editable Assets';scene.unit_settings.system='METRIC'
    for key,color in PALETTE.items():
        m=bpy.data.materials.new(key);m.diffuse_color=(*(int(color[i:i+2],16)/255 for i in [0,2,4]),1);m.use_nodes=True
        n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');n.inputs['Base Color'].default_value=m.diffuse_color;n.inputs['Roughness'].default_value=.76;n.inputs['Emission Strength'].default_value=0;MATS[key]=m
    records=[]
    for k,recipe in zip(KINDS,[military,arcane,spirit]):
        for lv in range(1,6):
            b=Builder(k+f'_Lv{lv}');recipe(b,lv);ornaments(b,k,lv);col,rt=b.finish(((lv-1)*32,KINDS.index(k)*38,0));rt['level']=lv;rt['school_type']=k
            points=[v for vs,_ in b.groups.values() for v in vs];lo=[min(v[i] for v in points) for i in range(3)];hi=[max(v[i] for v in points) for i in range(3)]
            records.append({'name':col.name,'minimum':lo,'maximum':hi,'parts':len(col.objects)-1});print('CREATED',col.name,flush=True)
    REVIEW.mkdir(parents=True,exist_ok=True)
    (REVIEW/'model_manifest.json').write_text(json.dumps({'design':'Reference-led integrated school architecture','plotSize':26,'models':records},indent=2))
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT))

if __name__=='__main__':main()
