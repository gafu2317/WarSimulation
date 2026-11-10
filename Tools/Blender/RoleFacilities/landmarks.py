"""Large, supported silhouettes with a different visual language for each use."""
import math


def outline(m, points, z, depth, material):
    area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(points,points[1:]+points[:1]))
    if area>0:points=list(reversed(points))
    front=[(x,y,z-depth/2) for x,y in points]
    back=[(x,y,z+depth/2) for x,y in points]
    m.face(front,material)
    m.face(list(reversed(back)),material)
    for i in range(len(points)):
        j=(i+1)%len(points)
        m.face([front[i],back[i],back[j],front[j]],material)


def hoop(m, center, radius, thickness, material, tilt=0):
    x,y,z=center
    def p(a):return x+radius*math.cos(a),y+radius*math.sin(a)*math.cos(tilt),z+radius*math.sin(a)*math.sin(tilt)
    for i in range(48):m.beam(p(i*math.tau/48),p((i+1)*math.tau/48),thickness,material)


def sword(m,x,y,z,length,angle=0):
    co,si=math.cos(angle),math.sin(angle)
    def p(a,b):return x+a*co-b*si,y+a*si+b*co
    w=length*.09
    outline(m,[p(-w,0),p(w,0),p(w,length*.68),p(0,length*.86),p(-w,length*.68)],z,length*.04,'steel')
    a,b=p(0,0),p(0,length*.75)
    m.beam((*a,z-.08),(*b,z-.08),length*.022,'ivory')
    a,b=p(-length*.24,0),p(length*.24,0)
    m.beam((*a,z),(*b,z),length*.075,'gold')
    a,b=p(0,-length*.25),p(0,-length*.035)
    m.beam((*a,z),(*b,z),length*.085,'leather')
    a,b=p(0,-length*.29)
    m.ellipsoid((a,b,z),(length*.08,length*.08,length*.06),'gold',n=16,rings=8)


def training_gate(m,w,d,level):
    m.landmark('CrossedTrainingSwords')
    z=-d*.35;h=6.9+level*.24;span=w*.35
    for side in (-1,1):
        m.box((side*span/2,h/2,z),(.60,h,.70),'stone')
        m.box((side*span/2,h,z),(1.0,.32,1.0),'trim')
    m.box((0,h-.30,z),(span,.52,.70),'timber')
    m.box((0,h+.02,z),(span*.68,.26,.85),'trim')
    size=min(4.7,w*.25)
    for side in (-1,1):sword(m,side*.72,h+.62,z-.18,size,side*.65)
    for side in (-1,1):
        x=side*w*.37
        m.cylinder((x,5.1,-d*.37),.075,9.5,'wood',n=12)
        outline(m,[(x-side*.12,8.8),(x-side*1.65,8.8),(x-side*1.65,5.6),(x-side*.88,5.05),(x-side*.12,5.6)],-d*.37,.06,'red')


def arcane_focus(m,x,z,r,h):
    m.landmark('TowerCrownSpellCrystal')
    m.cylinder((x,h+.16,z),r+.30,.32,'trim',n=24)
    for i in range(6):
        a=i*math.tau/6
        start=(x+math.cos(a)*r*.83,h+.30,z+math.sin(a)*r*.83)
        end=(x+math.cos(a)*r*.47,h+2.15,z+math.sin(a)*r*.47)
        m.beam(start,end,.22,'gold')
    radius=r*.47
    m.cylinder((x,h+2.75,z),radius,3.4,'crystal',n=6,top=.03)
    m.cylinder((x,h+.65,z),.04,.82,'crystal',n=6,top=radius)
    hoop(m,(x,h+2.35,z),r*.82,.11,'gold',.62)
    hoop(m,(x,h+2.35,z),r*.82,.11,'gold',-.62)


def sacred_tree(m,w,d,level):
    m.landmark('GreatSpiritTreeAndWisps')
    height=10+level*.82;z=-d*.015
    m.cylinder((0,.23+height*.32,z),height*.072,height*.64,'timber',n=18,top=height*.045)
    for i in range(7):
        a=i*math.tau/7+.30
        tip=(math.cos(a)*height*.27,.23+height*(.66+(i%2)*.09),z+math.sin(a)*height*.24)
        m.beam((0,height*.42,z),tip,height*.065,'timber')
        m.ellipsoid(tip,(height*.23,height*.21,height*.20),'spiritleaf' if i%2 else 'leaflight',n=20,rings=12)
    m.ellipsoid((0,height*.88,z),(height*.28,height*.22,height*.26),'spiritleaf',n=20,rings=12)
    for i in range(5):
        a=i*math.tau/5
        x,zz=math.cos(a)*w*.24,z+math.sin(a)*d*.20
        yy=height*(.50+(i%2)*.14)
        m.ellipsoid((x,yy,zz),(.45,.70,.45),'spiritlight',n=20,rings=12)
        m.cylinder((x,yy+.78,zz),.30,.65,'spiritlight',n=10,top=.01)


def open_book(m,x,y,z,w=4.3,h=2.7):
    m.landmark('CarvedOpenBookOverReadingHall')
    for side in (-1,1):
        pts=[(x,y-h/2,z-.18),(x+side*w/2,y-h/2+.20,z+.24),(x+side*w/2,y+h/2,z+.24),(x,y+h/2-.20,z-.18)]
        m.face(list(reversed(pts)) if side>0 else pts,'ivory')
        for a,b in zip(pts,pts[1:]+pts[:1]):m.beam(a,b,.11,'gold')
        for i in range(5):
            yy=y-h*.29+i*h*.13
            m.beam((x+side*w*.08,yy,z-.14),(x+side*w*.42,yy+.15,z+.14),.035,'ink')
        m.box((x+side*w*.25,y,z+.42),(w*.5+.1,h+.10,.15),'leather')
    m.beam((x,y-h*.56,z-.23),(x,y+h*.47,z-.23),.12,'gold')
    for side in (-1,1):m.box((x+side*w*.34,y,z+.54),(.24,h*.7,.34),'trim')


def ale_mug(m,w,d):
    m.landmark('SuspendedAleTankard')
    x=w*.24;z=-d*.455;y=5.25
    pole=x+1.65
    m.box((pole,3.95,z),(.25,7.45,.28),'timber')
    m.beam((pole,7.65,z),(x-.65,7.65,z),.25,'timber')
    m.beam((pole,6.1,z),(x+.25,7.65,z),.16,'timber')
    for side in (-1,1):m.beam((x+side*.48,7.65,z),(x+side*.48,y+1.02,z),.045,'metal')
    m.lathe(x,y-.95,z,[(.70,0),(.82,.18),(.82,1.75),(.76,1.98)],'wood',n=32)
    for yy in (y-.88,y+.78):m.ring(x,z,.78,.89,yy,yy+.16,'gold',n=32)
    m.cylinder((x,y+1.02,z),.72,.12,'ivory',n=32)
    for i in range(5):m.ellipsoid((x+(i-2)*.29,y+1.10,z-.10),(.25,.18,.30),'ivory',n=16,rings=8)
    hoop(m,(x+1.02,y,z),.61,.19,'gold')


def clinic_cross(m,w,d):
    m.landmark('ApothecaryCrossAndMortar')
    z=-d*.36
    for side in (-1,1):m.box((side*1.7,2.85,z),(.30,5.25,.35),'trim')
    m.box((0,5.52,z),(3.85,.38,.55),'trim')
    m.box((0,6.93,z),(.87,3.0,.38),'healing')
    m.box((0,6.93,z),(2.85,.86,.38),'healing')
    x=w*.29;zz=-d*.40
    m.cylinder((x,.53,zz),.70,.60,'trim',n=20)
    m.lathe(x,.83,zz,[(.55,0),(.82,.18),(1.0,.80),(.99,.99),(.80,1.00),(.69,.40),(.40,.26)],'ivory',n=28,caps=False)
    m.beam((x-.24,1.34,zz),(x+.76,2.79,zz+.06),.30,'wood')


def casino_cards(m,w,d):
    m.landmark('FanOfGamingCards')
    z=-d*.335;y=6.8
    m.box((0,4.8,z),(w*.42,.35,.80),'gold')
    for side in (-1,1):m.box((side*w*.19,4.05,z),(.18,1.5,.20),'gold')
    for j,angle in enumerate((-.38,.38,0)):
        x=(j-1)*.48 if j<2 else 0;zz=z-j*.13
        co,si=math.cos(angle),math.sin(angle)
        def p(a,b):return x+a*co-b*si,y+a*si+b*co
        outline(m,[p(-.94,-1.85),p(.94,-1.85),p(.94,1.85),p(-.94,1.85)],zz,.13,'ivory')
        for a,b in (((-.94,-1.85),(.94,-1.85)),((.94,-1.85),(.94,1.85)),((.94,1.85),(-.94,1.85)),((-.94,1.85),(-.94,-1.85))):
            pa,pb=p(*a),p(*b);m.beam((*pa,zz-.09),(*pb,zz-.09),.075,'gold')
        outline(m,[p(0,-.8),p(.57,0),p(0,.8),p(-.57,0)],zz-.10,.04,'red' if j!=1 else 'ink')
    for side in (-1,1):
        x=side*w*.31;zz=-d*.44
        m.box((x,.83,zz),(.70,1.2,.70),'trim')
        m.box((x,2.03,zz),(1.40,1.40,1.40),'ivory',yaw=side*.12)
        for xx,yy in ((-.30,-.30),(.30,-.30),(0,0),(-.30,.30),(.30,.30)):
            m.cylinder((x+xx,2.03+yy,zz-.75),.12,.06,'ink',n=16,axis=(0,0,1))


def guard_crest(m,w,d):
    m.landmark('GateShieldAndPolearms')
    z=-d*.32;y=6.05
    for side in (-1,1):
        x=side*1.28
        m.beam((x*.65,4.40,z+.14),(x*1.18,8.45,z+.14),.12,'timber')
        outline(m,[(x*1.18-.30,8.15),(x*1.18,9.10),(x*1.18+.30,8.15)],z+.14,.12,'steel')
    points=[(-1.35,y+1.35),(1.35,y+1.35),(1.2,y-.55),(0,y-1.55),(-1.2,y-.55)]
    outline(m,points,z-.07,.28,'gold')
    outline(m,[(x*.88,y+(yy-y)*.88) for x,yy in points],z-.27,.12,'blue')
    m.box((0,y,z-.37),(.24,2.45,.08),'ivory')
    m.box((0,y+.38,z-.37),(2.27,.23,.08),'ivory')
    m.ellipsoid((0,y+.38,z-.49),(.30,.30,.15),'gold',n=20,rings=12)


def forge_emblem(m,w,d):
    m.landmark('ForgedAnvilAndHammer')
    x=-w*.20;y=6.25;z=-d*.37
    for side in (-1,1):m.beam((x+side*1.1,4.55,z),(x+side*1.1,y+1.05,z),.15,'metal')
    points=[(x-1.30,y-.70),(x+.70,y-.70),(x+.46,y-.37),(x+.30,y+.15),(x+1.68,y+.45),(x+2.00,y+.75),(x-1.30,y+.75),(x-1.30,y+.25),(x-.55,y+.15),(x-.80,y-.37)]
    outline(m,points,z,.44,'steel')
    m.beam((x+.35,y+.82,z),(x+1.45,y+2.40,z),.18,'wood')
    m.box((x+1.45,y+2.40,z),(1.20,.60,.65),'steel')


def monumental_muse(m,w,d):
    m.landmark('MonumentalGallerySculpture')
    x=0;z=-d*.30;y=4.70
    m.box((x,y-.17,z),(2.30,.34,1.80),'trim')
    m.box((x,y+.13,z),(1.3,.26,1.1),'trim')
    m.lathe(x,y+.26,z,[(.77,0),(.67,1.1),(.44,1.9),(.65,2.9),(.30,3.20)],'trim',n=24)
    m.ellipsoid((x,y+3.86,z),(.43,.56,.40),'trim',n=24,rings=16)
    m.cylinder((x,y+3.28,z),.21,.45,'trim',n=20)
    for side in (-1,1):
        m.beam((x+side*.51,y+2.93,z),(x+side*1.02,y+2.35,z-.16),.29,'trim')
        m.beam((x+side*1.02,y+2.35,z-.16),(x+side*1.36,y+2.88,z-.24),.25,'trim')
        m.ellipsoid((x+side*1.38,y+2.93,z-.24),(.18,.25,.16),'trim',n=16,rings=10)
    for i in range(7):
        a=i*math.tau/7
        m.beam((x+math.cos(a)*.45,y+1.9,z+math.sin(a)*.45),(x+math.cos(a)*.75,y+.32,z+math.sin(a)*.75),.055,'ivory')
    for side in (-1,1):
        pts=[(x+side*.38,y+2.6),(x+side*1.40,y+3.75),(x+side*2.15,y+4.3),(x+side*2.45,y+4.65),(x+side*2.36,y+3.15),(x+side*1.45,y+2.35),(x+side*.4,y+1.75)]
        outline(m,pts,z+.20,.24,'trim')
        for i in range(6):m.beam((x+side*.58,y+2.35,z+.02),(x+side*(1.05+i*.25),y+2.9+i*.26,z+.02),.13,'ivory')


def tall_lantern(m,x,y,z,h=2.1):
    r=h*.28
    m.lathe(x,y-h/2,z,[(r*.48,0),(r*.91,h*.20),(r,h*.50),(r*.91,h*.8),(r*.48,h)],'lantern',n=24)
    for yy in (y-h/2,y+h/2):m.cylinder((x,yy,z),r*.57,.12,'gold',n=24)
    for i in range(8):
        a=i*math.tau/8
        points=[(x+math.cos(a)*r*rr,y-h/2+h*hh,z+math.sin(a)*r*rr) for rr,hh in ((.5,0),(.93,.2),(1.015,.5),(.93,.8),(.5,1))]
        for a,b in zip(points,points[1:]):m.beam(a,b,.045,'gold')
    m.beam((x,y+h/2,z),(x,y+h/2+.48,z),.045,'metal')
    m.beam((x,y-h/2,z),(x,y-h/2-.42,z),.08,'red')


def entertainment(m,name,w,d):
    m.landmark('EntertainmentLanternsAndDrapery')
    if name=='LanternSpire':
        for i in range(3):tall_lantern(m,0,1.95+i*3.3,-d*(.355-i*.03),2.15)
    else:
        for side in (-1,1):
            x=side*w*.31;z=-d*.41
            m.box((x,2.85,z),(.19,5.25,.19),'timber')
            m.beam((x,5.47,z),(x-side*.7,5.47,z),.14,'gold')
            tall_lantern(m,x-side*.58,3.85,z,2.1)
        if name=='VelvetTerrace':
            for side in (-1,1):m.box((side*w*.25,4.55,-d*.27),(.70,2.25,.10),'red')
        if name=='CrimsonRowhouse':
            m.landmark('GildedPerformanceLyre')
            z=-d*.245;y=6.05
            m.box((0,5.20,z),(1.9,.24,.65),'gold')
            for side in (-1,1):m.box((side*.55,5.43,z),(.18,.45,.24),'metal')
            for side in (-1,1):m.beam((side*.62,y-.45,z),(side*1.22,y+1.85,z),.24,'gold')
            m.box((0,y-.40,z),(1.5,.62,.42),'wood')
            m.beam((-1.22,y+1.85,z),(1.22,y+1.85,z),.24,'gold')
            for i in range(7):m.beam(((i-3)*.20,y-.15,z-.14),((i-3)*.29,y+1.74,z-.14),.035,'ivory')
