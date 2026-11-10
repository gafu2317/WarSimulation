"""Purpose-built facilities with large, use-specific equipment and silhouettes."""
import math
from furnishings import *
from landmarks import *


def aliases(style,name):
    result=dict(stone='Fieldstone',trim='Carved_Limestone',wall='Plaster',wood='Oak',timber='DarkTimber',door='Door_Oak',
        glass='Glazing_Blue',amber='Window_Amber',blueglass='Glazing_Blue',roofglass='Architectural_Glass',metal='Iron',bronze='Bronze_Fittings',gold='Brass',
        brick='Brick',paving='Cobble',soil='Earth',leaf='Foliage',leaflight='Cypress_Foliage',
        linen='Linen',paper='Paper',red='Canvas_Red',green='Canvas_Green',blue='Canvas_Blue',
        clay='Terracotta',water='Water',ember='Glowing_Coals',coal='Coal',bread='Bread_Crust',roof='Roof_Slate',straw='Thatch',leather='Leather',
        steel='Identity_Steel',ivory='Identity_Ivory',ink='Identity_Ink',healing='Identity_Healing',
        crystal='Identity_Crystal',spiritleaf='Identity_SpiritLeaf',spiritlight='Identity_SpiritLight',lantern='Identity_Lantern')
    result['glass']='Window_Sacred' if name in ('Church','Chapel') else 'Glazing_Amber' if name in ('Library','Tavern') else 'Glazing_Sage' if name=='Clinic' else 'Glazing_Plum' if name=='Casino' else result['glass']
    return result


def footing(m,w,d):
    m.feature('ContinuousFoundation')
    m.box((0,.10,0),(w,.20,d),'stone')
    m.box((0,.215,0),(w-.08,.03,d-.08),'paving')
    m.markers['Entrance']=(0,.23,-d/2)


def aperture(m,x,y,z,w,h,yaw=0,glass='glass',arched=True,door=False):
    co,si=math.cos(yaw),math.sin(yaw)
    def p(a,b,c):return x+a*co+c*si,y+b,z-a*si+c*co
    if arched:
        radius=w/2;spring=h-radius
        outline=[(-w/2,0),(w/2,0)]+[(radius*math.cos(i*math.pi/16),spring+radius*math.sin(i*math.pi/16)) for i in range(17)]
        m.face([p(a,b,.06) for a,b in reversed(outline)], 'door' if door else glass,
            [(a/w+.5,b/h) for a,b in reversed(outline)])
        arch_center=p(0,0,-.02)
        m.arch(arch_center[0],y,arch_center[2],w,h,.14,.25,'trim',yaw)
        for side in (-1,1):
            for i in range(8):
                a=math.pi/2+i*math.pi/16 if side<0 else i*math.pi/16
                b=a+math.pi/16
                pts=[(radius*math.cos(a),spring+radius*math.sin(a)),(radius*math.cos(b),spring+radius*math.sin(b))]
                edge=-radius if side<0 else radius
                m.face([p(pts[0][0],pts[0][1],.13),p(pts[1][0],pts[1][1],.13),p(edge,pts[1][1],.13),p(edge,pts[0][1],.13)],'wall')
    else:
        m.box(p(0,h/2,.06),(w,h,.10),'door' if door else glass,yaw)
        for s in (-1,1):m.box(p(s*(w/2+.06),h/2,-.035),(.12,h+.14,.24),'trim',yaw)
        for yy in (0,h):m.box(p(0,yy,-.04),(w+.22,.14,.25),'trim',yaw)
    if door:
        m.box(p(0,.025,-.12),(w+.23,.05,.48),'trim',yaw)
        for yy in (.45,min(h-.3,1.8)):m.box(p(0,yy,-.035),(w*.88,.07,.05),'metal',yaw)
        for xx in (-w*.11,w*.11):m.box(p(xx,1.0,-.08),(.07,.14,.06),'bronze',yaw)
    else:
        for xx in ([-w/6,w/6] if w>1.3 else [0]):m.beam(p(xx,.08,-.07),p(xx,h-(w*.18 if arched else .07),-.07),.055,'metal')
        for yy in (.4*h,.7*h):
            half=min(w/2, math.sqrt(max(0,(w/2)**2-(yy-(h-w/2))**2))) if arched and yy>h-w/2 else w/2
            m.beam(p(-half,yy,-.07),p(half,yy,-.07),.045,'metal')
        m.box(p(0,-.08,-.09),(w+.28,.12,.4),'trim',yaw)


def room(m,label,x,z,w,d,h,style,roof=True,shape=None,window_height=1.9,spacing=2.8,entry=True,wall=None,base=.23,windows=True,door_width=1.8,door_height=3.0,ceiling=True,side_sill=None,upper_doors=()):
    m.feature(label)
    wall=wall or ('wall' if style==2 else 'stone')
    thickness=.28 if style==2 else .42
    m.box((x,base+.10,z),(w+.15,.20,d+.15),'stone')
    levels=max(1,round((h-base)/3.4))
    for face,(length,depth,yaw) in enumerate(((w,z-d/2,0),(w,z+d/2,math.pi),(d,x-w/2,math.pi/2),(d,x+w/2,-math.pi/2))):
        horizontal=face<2
        holes=[]
        count=max(1,int((length-.8)/spacing))
        ww=min(1.4,(length-.8)/max(count,1)*.50)
        hh=min(window_height,(h-base)/levels*.72)
        if windows:
            for level in range(levels):
                if face>=2 and side_sill is not None and level<levels-1:continue
                for i in range(count):
                    pos=(i-(count-1)/2)*(length-.9)/max(count,1)
                    yy=base+.9+level*(h-base)/levels
                    if face>=2 and side_sill is not None:
                        yy=max(yy,side_sill);hh=min(hh,h-.24-yy)
                    if face==0 and abs(pos)<(door_width+ww)*.5+.18 and (entry and level==0 or any(yy<floor+door_height and yy+hh>floor for floor in upper_doors)):continue
                    if yy+hh>h-.16:yy=h-.16-hh
                    holes.append((pos,yy,ww,hh,False))
        if face==0 and entry:holes.append((0,base,door_width,min(door_height,h-base-.25),True))
        if face==0:
            for floor in upper_doors:holes.append((0,floor,door_width,min(door_height,h-floor-.25),True))
        xs=sorted(set([-length/2,length/2]+[p[0]+s*p[2]/2 for p in holes for s in (-1,1)]))
        ys=sorted(set([base,h]+[v for p in holes for v in (p[1],p[1]+p[3])]))
        for a,b in zip(xs,xs[1:]):
            for lo,hi in zip(ys,ys[1:]):
                if lo<base-.001 or hi>h+.001:continue
                if any(abs((a+b)/2-p[0])<p[2]/2-.001 and p[1]-.001<(lo+hi)/2<p[1]+p[3]+.001 for p in holes):continue
                c=(x+(a+b)/2,(lo+hi)/2,depth) if horizontal else (depth,(lo+hi)/2,z+(a+b)/2)
                size=(b-a,hi-lo,thickness) if horizontal else (thickness,hi-lo,b-a)
                m.box(c,size,wall)
        for pos,yy,ww,hh,door in holes:
            wx,wz=(x+pos,depth-.16 if face==0 else depth+.16) if horizontal else (depth-.22 if face==2 else depth+.22,z+pos)
            aperture(m,wx,yy,wz,ww,hh,yaw,arched=style==3 or window_height>2.6,door=door)
    for a in (-1,1):
        for b in (-1,1):m.box((x+a*w/2,(base+h)/2,z+b*d/2),(.34,h-base,.34),'trim')
    for zz in (-1,1):m.box((x,h-.12,z+zz*d/2),(w+.22,.24,.30),'trim')
    for xx in (-1,1):m.box((x+xx*w/2,h-.12,z),(.30,.24,d),'trim')
    if ceiling:m.box((x,h-.14,z),(w-.3,.12,d-.3),'wood')
    m.box((x,base+.07,z),(w-.28,.12,d-.28),'wood')
    if roof:
        m.feature(label+'_Roof')
        rise=min(w*(.56 if style==2 else .29),4.0)
        if shape=='vault':m.vault(x,z,w+.4,d+.4,h,rise)
        else:m.roof(x,z,w,d,h,rise,kind=shape or ('gable' if style==2 else 'hip'))


def arcade(m,label,x,z,w,d,height,style,roof=True,fronts=3):
    m.feature(label)
    timber=label in ('OpenForgeWorkshop','OpenHorseStalls','CoveredPublicMarketHall','OpenBreadCounter','ShelteredDrinkingPorch','OpenGamingArcade','CurtainedPerformancePorch','OpenTradingLoggia')
    rows=max(1,int(d/3.0))
    for i in range(fronts+1):
        xx=x-w/2+w*i/fronts
        for row in range(rows+1):
            zz=z-d/2+d*row/rows
            m.box((xx,.32,zz),(.50,.2,.50),'trim')
            if timber:
                m.box((xx,(height+.4)/2,zz),(.23,height-.4,.23),'timber')
                for side in (-1,1):
                    if 0<=i+side<=fronts:m.beam((xx,height-.65,zz),(xx+side*min(.75,w/fronts*.35),height,zz),.12,'timber')
            else:
                m.cylinder((xx,(height+.4)/2,zz),.18,height-.4,'trim',n=16)
                m.box((xx,height-.20,zz),(.46,.22,.46),'trim')
    for zz in (-1,1):m.box((x,height+.02,z+zz*d/2),(w+.5,.30,.36),'timber' if timber else 'trim')
    for xx in (-1,1):m.box((x+xx*w/2,height,z),(.25,.24,d),'timber' if timber else 'trim')
    if not timber:
        for i in range(fronts):
            xx=x-w/2+w*(i+.5)/fronts
            m.arch(xx,.8,z-d/2,w/fronts-.42,height-.80,.13,.32,'trim')
    if roof:
        m.feature(label+'_ShelterRoof')
        m.roof(x,z,w+.1,d+.3,height+.24,min(1.1,d*.5),kind='hip' if style==3 else 'gable')


def square_tower(m,label,x,z,w,d,height,style,roof=True,battle=False):
    room(m,label,x,z,w,d,height,style,roof=False,entry=False,window_height=1.6,spacing=3,wall='stone')
    m.feature(label+'_Top')
    if battle:
        m.box((x,height+.1,z),(w+.5,.25,d+.5),'trim')
        for side in (-1,1):
            for j in range(4):
                m.box((x+(j-1.5)*w/3,height+.75,z+side*d/2),(.55,1.1,.45),'stone')
                m.box((x+side*w/2,height+.75,z+(j-1.5)*d/3),(.45,1.1,.55),'stone')
    elif roof:m.roof(x,z,w+.2,d+.2,height+.2,w*(.8 if style==2 else .45),kind='hip')


def round_tower(m,label,x,z,r,height,style,cap=True,sides=16,entry=False):
    m.feature(label)
    m.cylinder((x,.4,z),r+.22,.4,'trim',n=sides)
    m.wall_mapping=(x,z,r)
    # Faceted wall segments leave true rectangular window openings.
    for i in range(sides):
        a=math.tau*i/sides
        xx,zz=x+math.sin(a)*r,z-math.cos(a)*r
        width=2*r*math.tan(math.pi/sides)
        yaw=-a
        if i==0 and width>.72 and entry:
            opening=min(1.8,width-.18)
            height_door=min(2.6,height*.40)
            win=min(1.5,width*.62);low,high=height*.53,height*.76
            holes=[(opening,.4,.4+height_door),(win,low,high)]
            xs=sorted(set([-width/2,width/2,-opening/2,opening/2,-win/2,win/2]))
            ys=sorted(set([.4,height,.4+height_door,low,high]))
            for a,b in zip(xs,xs[1:]):
                for lo,hi in zip(ys,ys[1:]):
                    if any(abs((a+b)/2)<ww/2-.001 and bottom<(lo+hi)/2<top for ww,bottom,top in holes):continue
                    m.box((xx+(a+b)/2,(lo+hi)/2,zz),(b-a,hi-lo,.3),'stone')
            aperture(m,xx,.4,zz-.17,opening,height_door,arched=True,door=True)
            aperture(m,xx,low,zz-.17,win,high-low,arched=True)
        elif i%2==0 and width>.8:
            for lo,hi in ((.4,height*.53),(height*.76,height)):
                m.box((xx,(lo+hi)/2,zz),(width,hi-lo,.3),'stone',yaw)
            opening=min(.75,width*.55)
            for side in (-1,1):
                dx=side*(opening/2+(width-opening)/4)
                m.box((xx+dx*math.cos(yaw),height*.645,zz-dx*math.sin(yaw)),((width-opening)/2,height*.23,.3),'stone',yaw)
            aperture(m,xx,height*.53,zz,opening,height*.23,yaw,arched=style==3)
        else:m.box((xx,(height+.4)/2,zz),(width,height-.4,.3),'stone',yaw)
    m.wall_mapping=None
    for yy in (.5,height*.46,height):m.ring(x,z,r-.05,r+.22,yy,yy+.17,'trim',n=sides)
    if cap:
        m.feature(label+'_Roof')
        m.cylinder((x,height+r*.75,z),r+.34,r*1.5,'roof',n=sides,top=.03)


def court(m,label,x,z,w,d):
    m.feature(label)
    m.box((x,.235,z),(w,.03,d),'soil')
    for side in (-1,1):
        m.box((x+side*w/2,.29,z),(.13,.12,d+.13),'trim')
        m.box((x,.29,z+side*d/2),(w,.12,.13),'trim')


def church(m,w,d,s,chapel=False):
    nave=w*(.42 if not chapel else .62)
    length=d*.62
    room(m,'HighNave',0,0,nave,length,8 if not chapel else 5.7,s,window_height=3.3,spacing=2.8,shape='gable' if s==2 else 'vault',side_sill=6.5 if not chapel else None)
    if not chapel:
        for side in (-1,1):
            room(m,'SideAisle',side*w*.32,.2,w*.22,length*.95,4.3,s,entry=False,window_height=2.3)
            for i in range(4):m.box((side*w*.455,2.1,-length*.36+i*length*.24),(.55,4.0,.48),'trim')
        room(m,'TransverseChapel',0,d*.16,w*.86,d*.18,5.3,s,entry=False,window_height=2.4)
    m.feature('RoundedApse')
    rear=length/2+.12
    apse=min(nave*.48,d*.16)
    m.cylinder((0,3.3,rear),apse,6.0,'stone',n=20)
    m.cylinder((0,6.85,rear),apse+.20,1.15,'roof',n=20,top=.03)
    towers=[(-w*.35,-d*.29),(w*.35,-d*.29)] if s==2 and not chapel else [(-w*.33,-d*.13)]
    for i,(x,z) in enumerate(towers):
        height=(10.8 if not chapel else 7.0)+(i*.8)
        square_tower(m,'BellTower',x,z,w*.20,w*.20,height,s,roof=False)
        m.landmark('GreatOpenBelfryAndCross')
        span=w*.075
        for xx in (-1,1):
            for zz in (-1,1):m.box((x+xx*span,height+1.45,z+zz*span),(.25,2.9,.25),'trim')
        r=min(.98,w*.06)
        m.lathe(x,height+.48,z,[(r,0),(r,.16),(r*.73,.35),(r*.55,1.3),(r*.32,1.62)],'bronze',n=32,caps=False)
        m.ellipsoid((x,height+.44,z),(r*.22,r*.25,r*.22),'metal',n=16,rings=10)
        m.beam((x,height+1.8,z),(x,height+2.9,z),.12,'metal')
        m.roof(x,z,w*.22,w*.22,height+3.0,1.9,kind='hip')
        m.beam((x,height+4.6,z),(x,height+7.0,z),.24,'gold')
        m.beam((x-.85,height+6.3,z),(x+.85,height+6.3,z),.24,'gold')
    if not chapel:
        m.landmark('CathedralRoseWindow')
        z=-length/2-.20
        m.cylinder((0,6.3,z),1.35,.08,'glass',n=48,axis=(0,0,1))
        hoop(m,(0,6.3,z-.07),1.43,.20,'trim')
        for i in range(8):
            a=i*math.tau/8
            m.beam((0,6.3,z-.14),(math.cos(a)*1.25,6.3+math.sin(a)*1.25,z-.14),.08,'gold')
    m.feature('CongregationEntrance')
    stairs(m,0,-length/2-.65,3.1)
    for side in (-1,1):
        bench(m,side*w*.29,-d*.40,2.2)
        planter(m,side*w*.29,-d*.46,1.5,.7)


def library(m,w,d,s):
    if s==2:
        room(m,'TallReadingHall',0,d*.09,w*.44,d*.69,8.4,s,roof=False,window_height=3.1,spacing=2.7,ceiling=False,side_sill=6.7)
        for side in (-1,1):
            room(m,'ArchiveWing',side*w*.34,d*.13,w*.24,d*.61,5.2,s,entry=False,window_height=2.4,roof=False)
            m.feature('ArchiveWing_LeanRoof')
            m.shed(side*w*.475,side*w*.22,d*.13,d*.61,5.25,6.5)
    else:
        room(m,'VaultedReadingHall',0,d*.13,w*.67,d*.58,7.1,s,roof=False,window_height=3.3,spacing=2.8)
        for side in (-1,1):room(m,'LowArchiveWing',side*w*.37,-d*.08,w*.19,d*.62,4.2,s,entry=False,window_height=2.1)
    m.feature('GlazedClerestory')
    if s==2:
        z=d*.09;depth=d*.69;outer=w*.235;inner=w*.085
        for side in (-1,1):
            points=[(side*outer,8.4,z-depth/2),(side*outer,8.4,z+depth/2),(side*inner,10.2,z+depth/2),(side*inner,10.2,z-depth/2)]
            m.face(list(reversed(points)) if side==1 else points,'roof')
            m.box((side*inner,10.60,z),(.045,.80,depth),'roofglass')
        for end in (-1,1):
            points=[(-outer,8.4,z+end*depth/2),(-inner,10.2,z+end*depth/2),(inner,10.2,z+end*depth/2),(outer,8.4,z+end*depth/2)]
            m.face(list(reversed(points)) if end==1 else points,'wall')
            m.box((0,10.6,z+end*depth/2),(inner*2,.8,.045),'roofglass')
        for i in range(9):
            zz=z+(i-4)*depth/8
            for side in (-1,1):
                m.beam((side*inner,10.2,zz),(side*inner,11.02,zz),.07,'bronze')
                m.beam((side*outer,8.27,zz),(side*inner,10.07,zz),.10,'timber')
        for side in (-1,1):
            for yy in (10.2,11.02):m.beam((side*inner,yy,z-depth/2),(side*inner,yy,z+depth/2),.10,'bronze')
        m.roof(0,z,inner*2,depth,11.02,.75,kind='hip')
    else:
        radius=w*.35;depth=d*.61;z=d*.13
        for i in range(18):
            a=math.pi*i/18;b=math.pi*(i+1)/18
            def p(t,zz):return radius*math.cos(t),7.1+3.0*math.sin(t),zz
            m.face([p(a,z-depth/2),p(b,z-depth/2),p(b,z+depth/2),p(a,z+depth/2)],'blueglass' if 7<=i<=10 else 'roof')
        for end in (-1,1):
            zz=z+end*depth/2
            m.face([p(i*math.pi/18,zz) for i in (reversed(range(19)) if end<0 else range(19))],'blueglass')
            for j in range(18):m.beam(p(j*math.pi/18,zz),p((j+1)*math.pi/18,zz),.16,'trim')
            for xx in (-radius*.5,0,radius*.5):
                top=7.1+3.0*math.sqrt(1-(xx/radius)**2)
                m.beam((xx,7.1,zz),(xx,top,zz),.11,'bronze')
            m.beam((-radius,7.1,zz),(radius,7.1,zz),.18,'trim')
        for i in range(8):
            zz=z+(i-3.5)*depth/8
            for j in range(18):m.beam(p(j*math.pi/18,zz),p((j+1)*math.pi/18,zz),.065,'bronze')
    arcade(m,'OpenReadingLoggia',0,-d*.34,w*.78,d*.19,5.0,s,fronts=3)
    m.feature('VisibleBookStacks')
    for side in (-1,1):shelf(m,side*w*.27,-d*.35,w=2.7,h=4.0)
    open_book(m,0,7.6,-d*.27-.32,w=4.7,h=2.8)
    m.feature('InteriorReadingFurniture')
    for side in (-1,1):
        for zz in (d*.04,d*.26):
            reading_desk(m,side*w*.12,zz,w=1.6)
        shelf(m,side*w*.14,d*.39,w=1.8,h=2.8)
    m.feature('OutdoorReadingDesks')
    for side in (-1,1):reading_desk(m,side*w*.20,-d*.40,w=1.7)
    stairs(m,0,-d*.46,2.5,.24,.65)


def warrior(m,w,d,s,level):
    h=4.3+level*.4
    court(m,'OpenSparringCourt',0,-d*.11,w*.57,d*.48)
    if s==2:
        room(m,'DrillHall',0,d*.29,w*.80,d*.21,h,s,window_height=2.3)
        for side in (-1,1):room(m,'TrainingWing',side*w*.36,-d*.07,w*.19,d*.51,h-.7,s,entry=False,shape='hip')
        if level>=3:
            for side in (-1,1):square_tower(m,'GateFlank',side*w*.26,-d*.35,w*.17,d*.17,h+1.6,s)
    else:
        room(m,'LongDrillHall',-w*.14,d*.29,w*.60,d*.25,h,s,window_height=2.4,shape='vault')
        room(m,'ArmoryWing',w*.34,d*.03,w*.19,d*.74,h-.6,s,entry=False)
        arcade(m,'CoveredTrainingStand',-w*.36,-d*.04,w*.17,d*.58,3.3,s,fronts=1)
        if level>=3:square_tower(m,'GateWatch',-w*.26,-d*.35,w*.18,d*.18,h+2.0,s,battle=True)
    m.feature('PracticeEquipment')
    for x in (-w*.17,0,w*.17):dummy(m,x,-d*.05)
    weapon_rack(m,-w*.16,d*.12,2.6)
    weapon_rack(m,w*.16,d*.12,2.6,shields=True)
    for x in (-w*.15,w*.15):bench(m,x,-d*.29,2.0)
    m.feature('CourtEntry')
    for side in (-1,1):m.box((side*w*.34,.7,-d*.43),(w*.18,1.0,.35),'stone')
    training_gate(m,w,d,level)


def arcane(m,w,d,s,level):
    if s==2:
        room(m,'ArcaneLectureHall',0,-d*.03,w*.54,d*.39,6.4,s,window_height=3.0)
        positions=[(-w*.31,d*.15,w*.145,9+level*.6),(w*.31,d*.15,w*.145,8.3+level*.5)]
        if level>=4:positions.append((0,d*.31,w*.16,10.5+level*.7))
        for i,(x,z,r,h) in enumerate(positions):
            round_tower(m,'ResearchTurret',x,z,r,h,s,cap=i!=0,sides=20)
            if i==0:arcane_focus(m,x,z,r,h)
    else:
        round_tower(m,'OctagonalResearchKeep',0,d*.17,w*.26,10+level*.6,s,cap=False,sides=8)
        m.feature('ResearchKeepRoof')
        m.cylinder((0,12+level*.6,d*.17),w*.30,3.7,'roof',n=8,top=w*.05)
        room(m,'WestLaboratory',-w*.34,d*.01,w*.18,d*.56,5.5,s,entry=False,shape='vault')
        room(m,'EastLaboratory',w*.34,d*.10,w*.18,d*.47,6.5,s,entry=False,shape='vault')
        arcade(m,'DemonstrationLoggia',0,-d*.19,w*.46,d*.15,4.0,s)
    court(m,'OpenPracticalMagicCourt',0,-d*.32,w*.57,d*.23)
    m.feature('BuiltRitualBasin')
    m.ring(0,-d*.32,1.2,1.45,.26,.38,'trim',n=28)
    m.ring(0,-d*.32,1.36,1.42,.38,.42,'bronze',n=28)
    m.cylinder((0,.27,-d*.32),1.18,.04,'blueglass',n=28)
    m.feature('PracticalSpellFocus')
    m.cylinder((0,1.7,-d*.32),.36,.75,'blueglass',n=6,top=.03)
    m.cylinder((0,1.15,-d*.32),.03,.35,'blueglass',n=6,top=.36)
    m.feature('AlchemyWorkstations')
    for side in (-1,1):
        table(m,side*w*.23,-d*.33,1.6,.8)
        for i in range(3):vessel(m,side*w*.23+(i-1)*.32,1.13,-d*.33,r=.1,h=.45,color='blueglass')
        shelf(m,side*w*.27,-d*.13,1.7,2.5,books=False)


def spirit(m,w,d,s,level):
    court(m,'SacredPlantedCourt',0,-d*.02,w*.53,d*.55)
    if s==2:
        room(m,'SpiritInstructionHall',0,d*.30,w*.65,d*.20,5.4+level*.2,s,window_height=2.7)
        for side in (-1,1):arcade(m,'OpenNatureCloister',side*w*.35,-d*.02,w*.18,d*.61,3.6,s,fronts=1)
    else:
        room(m,'LowSpiritHall',-w*.17,d*.28,w*.55,d*.24,5.3,s,window_height=2.7,shape='vault')
        room(m,'HerbalStudyWing',w*.33,d*.10,w*.19,d*.56,4.2,s,entry=False)
        arcade(m,'StoneTreeCloister',-w*.35,-d*.08,w*.17,d*.58,3.6,s,fronts=1)
    sacred_tree(m,w,d,level)
    m.feature('StandingStoneCircle')
    radius=min(w,d)*.22
    for i in range(7):
        a=i*math.tau/7
        x,z=math.cos(a)*radius,math.sin(a)*radius-d*.02
        m.cylinder((x,1.1,z),.28,1.7,'stone',n=6,top=.17)
        m.cylinder((x,2.03,z),.15,.22,'blueglass',n=8,top=.04)
    m.feature('HerbTeachingBeds')
    for side in (-1,1):
        planter(m,side*w*.28,-d*.35,2.2,1.2)
        reading_desk(m,side*w*.22,d*.17,1.5)


def tavern(m,w,d,s):
    if s==2:
        room(m,'GuestRoomWing',-w*.16,d*.13,w*.49,d*.61,7.1,s,window_height=1.7)
        room(m,'BrewingHall',w*.23,d*.24,w*.30,d*.38,4.5,s,entry=False)
    else:
        room(m,'CommunalMeadHall',0,d*.18,w*.77,d*.46,5.9,s,window_height=2.0,shape='gable')
        room(m,'Brewhouse',-w*.29,-d*.15,w*.24,d*.34,3.6,s,entry=False)
    arcade(m,'ShelteredDrinkingPorch',0,-d*.28,w*.77,d*.22,3.1,s,fronts=3)
    m.feature('AleCasksAndTap')
    for i in range(3):barrel(m,w*.21+i*.88,-d*.24)
    m.box((w*.21,.8,-d*.24-.43),(.08,.13,.25),'bronze')
    m.feature('OutdoorTrestleTables')
    for side in (-1,1):
        table(m,side*w*.20,-d*.40,1.7,.8)
        bench(m,side*w*.20,-d*.47,1.7)
        for i in (-1,1):vessel(m,side*w*.20+i*.30,1.12,-d*.4,.08,.25,'clay')
    m.feature('BrewingFlue')
    m.box((w*.28,4.2,d*.29),(.8,4.8,.8),'brick')
    m.box((w*.28,6.65,d*.29),(1,.2,1),'trim')
    ale_mug(m,w,d)


def observatory(m,w,d,s):
    radius=w*.275;height=7.8;x,z=0,d*.14
    round_tower(m,'ObservatoryDrum',x,z,radius,height,s,cap=False,sides=16,entry=True)
    m.feature('ObservationDeck')
    m.cylinder((x,height-.10,z),radius-.15,.20,'wood',n=48)
    m.glazed_dome(x,z,radius+.12,height+.18,radius*.92)
    m.feature('WorkingSlitTelescope')
    telescope(m,x,z-radius*.18,y=height+.05,length=radius*1.25)
    for side,label in ((-1,'ChartArchive'),(1,'InstrumentWorkshop')):
        room(m,label,side*w*.29,-d*.20,w*.24,d*.28,3.5,s,entry=False,roof=False)
        m.feature(label+'_LeadFlashedRoof')
        m.shed(side*w*.43,side*w*.15,-d*.20,d*.28,3.58,4.65)
        m.beam((side*w*.15,4.68,-d*.35),(side*w*.15,4.68,-d*.05),.12,'metal')
    m.feature('AstronomersEntrance')
    arcade(m,'InstrumentPorch',0,-d*.345,w*.30,d*.13,2.9,s,fronts=1)
    reading_desk(m,-w*.24,-d*.36,w=1.5)
    table(m,w*.24,-d*.36,1.5,.75)
    m.feature('OutdoorQuadrant')
    qx=w*.43
    for side in (-1,1):m.beam((qx+side*.55,.23,-d*.40),(qx,1.6,-d*.40),.07,'bronze')
    for i in range(16):
        a=i*math.pi/32;b=(i+1)*math.pi/32
        m.beam((qx+math.cos(a)*.8,1.4+math.sin(a)*.8,-d*.40),(qx+math.cos(b)*.8,1.4+math.sin(b)*.8,-d*.40),.06,'bronze')


def museum(m,w,d,s):
    if s==2:
        room(m,'HighCentralGallery',0,d*.08,w*.36,d*.58,8.1,s,window_height=3.6,side_sill=6.95)
        for side in (-1,1):
            room(m,'SideExhibitionGallery',side*w*.305,d*.10,w*.25,d*.54,5.3,s,window_height=2.6,spacing=2.5,entry=False,roof=False)
            m.feature('SideGallery_LeanRoof')
            m.shed(side*w*.45,side*w*.18,d*.10,d*.54,5.4,6.8)
    else:
        round_tower(m,'OctagonalSculptureHall',0,d*.10,w*.33,7.0,s,cap=False,sides=8)
        m.feature('GalleryLanternRoof')
        m.cylinder((0,8.0,d*.10),w*.37,2.0,'roof',n=8,top=w*.13)
        m.cylinder((0,9.22,d*.10),w*.13,.5,'glass',n=8)
        m.cylinder((0,9.7,d*.10),w*.16,.5,'roof',n=8,top=.04)
        for side in (-1,1):room(m,'CollectionWing',side*w*.34,d*.08,w*.21,d*.53,4.5,s,entry=False,window_height=2.4)
    arcade(m,'SculpturePortico',0,-d*.30,w*.70,d*.20,4.0,s,roof=False,fronts=4)
    m.feature('SculptureTerraceRoof')
    m.box((0,4.4,-d*.30),(w*.74,.38,d*.25),'trim')
    monumental_muse(m,w,d)
    m.feature('SculptureCourt')
    for side in (-1,1):statue(m,side*w*.30,-d*.41,2.7)
    for side in (-1,1):
        m.box((side*w*.33,.40,-d*.28),(1.0,.34,.85),'trim')
        vessel(m,side*w*.33,.57,-d*.28,.31,.85,'clay')
    stairs(m,0,-d*.46,3.2,.24,.7)


def clinic(m,w,d,s):
    if s==2:
        for side in (-1,1):room(m,'InfirmaryWard',side*w*.31,d*.04,w*.24,d*.69,4.1,s,entry=False,window_height=2.2)
        room(m,'TreatmentWing',0,d*.29,w*.43,d*.23,4.7,s,window_height=2.2)
    else:
        room(m,'MainInfirmaryWard',-w*.21,d*.11,w*.42,d*.61,4.7,s,window_height=2.4,shape='vault')
        room(m,'ApothecaryWard',w*.29,d*.18,w*.26,d*.48,3.8,s,entry=False,window_height=2.2)
    arcade(m,'OpenRecoveryVeranda',0,-d*.26,w*.66,d*.18,2.9,s,fronts=3)
    m.feature('VisibleRecoveryBeds')
    for side in (-1,1):bed(m,side*w*.19,-d*.26)
    m.feature('MedicinalHerbBeds')
    for side in (-1,1):planter(m,side*w*.22,-d*.42,2.0,1.0)
    m.feature('ApothecaryShelves')
    shelf(m,w*.26,d*.02,1.5,2.2,books=False)
    m.feature('TreatmentSuppliesTable')
    table(m,w*.35,-d*.42,.9,.6)
    for i in range(3):vessel(m,w*.35+(i-1)*.22,1.12,-d*.42,r=.065,h=.30,color='glass')
    m.cylinder((w*.35,1.16,-d*.45),.09,.24,'linen',n=12,axis=(1,0,0))
    m.feature('HerbConservatory')
    m.box((w*.46,1.85,d*.14),(w*.07,2.6,d*.40),'roofglass')
    for i in range(5):m.box((w*.495,1.85,d*.14+(i-2)*d*.09),(.055,2.7,.065),'wood')
    m.roof(w*.46,d*.14,w*.06,d*.40,3.2,.4,mat='roofglass',kind='gable',wall='roofglass')
    clinic_cross(m,w,d)


def casino(m,w,d,s):
    if s==2:
        room(m,'BroadGamingSaloon',0,d*.08,w*.83,d*.61,5.2,s,window_height=2.4,roof=False)
        m.feature('GamingHallMansard')
        m.roof(0,d*.08,w*.88,d*.65,5.3,2.0,kind='hip')
        room(m,'RoofLantern',0,d*.08,w*.30,d*.22,8.8,s,base=7.30,entry=False,window_height=.65)
    else:
        round_tower(m,'OctagonalGamingHall',0,d*.10,w*.35,5.1,s,cap=False,sides=8)
        m.feature('GamingHallCopperRoof')
        m.dome(0,d*.1,w*.39,5.25,2.4)
        for side in (-1,1):room(m,'GamingParlor',side*w*.34,-d*.03,w*.20,d*.44,3.6,s,entry=False)
    arcade(m,'OpenGamingArcade',0,-d*.30,w*.73,d*.23,3.1,s,fronts=3)
    m.feature('RealCardTablesAndChips')
    for side in (-1,1):game_table(m,side*w*.19,-d*.27)
    m.feature('EntranceLanterns')
    for side in (-1,1):lantern(m,side*w*.34,-d*.40,2.1)
    casino_cards(m,w,d)


def guard(m,w,d,s):
    if s==2:
        for side in (-1,1):square_tower(m,'GuardGateBastion',side*w*.30,d*.02,w*.25,d*.53,6.4,s,battle=True)
        m.feature('CoveredGuardWatchRooms')
        for side in (-1,1):m.roof(side*w*.30,d*.02,w*.17,d*.32,6.65,.85,kind='hip')
        arcade(m,'GuardedEntryPassage',0,-d*.08,w*.26,d*.46,3.9,s,roof=False,fronts=1)
        m.feature('GateWalkway')
        m.box((0,4.1,-d*.08),(w*.36,.4,d*.47),'stone')
        for end in (-1,1):m.box((0,4.7,-d*.08+end*d*.23),(w*.36,.9,.3),'stone')
    else:
        square_tower(m,'SingleWatchBastion',-w*.25,d*.06,w*.34,d*.66,7.4,s,battle=True)
        room(m,'GuardReadyRoom',w*.22,d*.04,w*.35,d*.62,4.5,s,window_height=1.3)
        arcade(m,'PatrolEntry',w*.17,-d*.32,w*.40,d*.17,2.8,s,fronts=1)
    m.feature('PatrolEquipmentRacks')
    weapon_rack(m,-w*.22,-d*.36,w=1.5,shields=True)
    weapon_rack(m,w*.23,-d*.36,w=1.5)
    m.feature('ArrowSlitParapet')
    for i in range(5):m.box(((i-2)*w*.15,1.0,d*.40),(.5,1.4,.35),'stone')
    guard_crest(m,w,d)


def blacksmith(m,w,d,s):
    room(m,'ToolStoreAndBellowsRoom',-w*.25,d*.19,w*.30,d*.43,5.0 if s==2 else 4.2,s,entry=False,wall='brick')
    room(m,'IronStockStore',w*.24,d*.27,w*.32,d*.24,4.2,s,entry=False,wall='stone')
    arcade(m,'OpenForgeWorkshop',0,-d*.13,w*.80,d*.38,4.4,s,roof=False,fronts=3)
    m.feature('SeparatedForgeShelterRoofs')
    # The hearth flue passes through the open rear gap, not through a roof face.
    for side in (-1,1):m.shed(side*w*.42,side*w*.16,-d*.24,d*.24,4.65,5.5)
    m.roof(w*.26,d*.03,w*.29,d*.24,4.65,1.0,kind='gable')
    m.feature('WorkingHearthAndMasonryFlue')
    furnace(m,0,d*.10,width=2.0)
    m.feature('AnvilsAndForgeTools')
    for side in (-1,1):anvil(m,side*w*.18,-d*.22)
    table(m,-w*.28,-d*.34,2.0,.75)
    for i in range(4):m.box((-w*.28+(i-1.5)*.25,1.15,-d*.34),(.1,.07,.52),'metal')
    weapon_rack(m,w*.30,-d*.06,w=2.1)
    m.feature('GrindingWheel')
    m.cylinder((w*.28,1.0,-d*.34),.55,.22,'stone',n=24,axis=(1,0,0))
    for side in (-1,1):m.beam((w*.28+side*.35,.23,-d*.34),(w*.28+side*.15,1.0,-d*.34),.12,'wood')
    m.feature('CoalAndIronStocks')
    for i in range(5):m.box((-w*.30, .38+i*.15,d*.05),(.9,.13,.4),'metal')
    for i in range(12):
        m.ellipsoid((w*.32+(i%4-1.5)*.18,.32+(i//4)*.12,d*.10),(.15,.13,.12),'coal',n=8,rings=5)
    m.feature('BellowsAndQuenchingTub')
    m.ellipsoid((-1.65,1.05,d*.10),(.67,.20,.38),'leather',n=16,rings=8)
    m.beam((-1.45,1.05,d*.10),(-.85,.9,d*.10),.12,'metal')
    for yy in (.85,1.24):m.box((-1.7,yy,d*.10),(1.1,.07,.55),'wood')
    m.ring(-w*.30,-d*.10,.40,.49,.23,1.02,'wood',n=24)
    m.ring(-w*.30,-d*.10,.48,.52,.83,.89,'metal',n=24)
    m.cylinder((-w*.30,.87,-d*.10),.39,.025,'water',n=24)
    forge_emblem(m,w,d)


def arena(m,w,d,s):
    radius=min(w,d)*.42
    m.feature('OpenArenaFloor')
    m.cylinder((0,.24,0),radius*.60,.08,'soil',n=48)
    m.feature('TieredSpectatorSeating')
    for level in range(5):
        inner=radius*(.60+.07*level)
        if s==2:m.ring(0,0,inner,inner+radius*.078,.20,.6+level*.5,'trim',n=64,stretch=d/w)
        else:m.ring(0,0,inner,inner+radius*.078,.20,.6+level*.5,'stone',n=56,start=-.20,end=math.pi+math.pi*.80,stretch=d/w)
    m.feature('SupportedArenaArcades')
    for i in range(18):
        a=math.tau*(i+.5)/18
        if abs(a-math.pi*1.5)<.22:continue
        x,z=math.cos(a)*radius,math.sin(a)*radius*d/w
        yaw=-a-math.pi/2
        m.arch(x,.23,z,1.55,2.0,.18,.6,'stone',yaw)
    m.ring(0,0,radius-.23,radius+.35,2.15,2.42,'trim',n=64,stretch=d/w)
    m.feature('ArenaEntryTunnel')
    m.arch(0,.23,-radius*d/w,2.6,2.45,.32,.95,'trim')
    m.landmark('TournamentGateAndBanners')
    z=-radius*d/w
    m.arch(0,.23,z,3.6,5.8,.60,1.25,'trim')
    for side in (-1,1):
        x=side*3.1
        m.box((x,3.95,z),(.16,7.4,.16),'wood')
        outline(m,[(x-.9,7.2),(x+.9,7.2),(x+.9,4.4),(x,3.85),(x-.9,4.4)],z,.06,'red')
    m.feature('TournamentEquipment')
    weapon_rack(m,-w*.16,-d*.40,w=1.7)
    weapon_rack(m,w*.16,-d*.40,w=1.7,shields=True)
    m.feature('SpectatorCanopy')
    m.roof(0,d*.34,w*.40,d*.13,5.8,1.4,mat='red',kind='hip')
    for side in (-1,1):m.box((side*w*.20,3.0,d*.34),(.20,5.6,.20),'wood')


def nightlife(m,name,w,d,s):
    if name=='CrimsonRowhouse':
        room(m,'RearPerformanceHall',0,d*.18,w*.78,d*.40,6.1 if s==2 else 5.4,s,window_height=2.3,shape='gable' if s==2 else 'vault')
        arcade(m,'CurtainedPerformancePorch',0,-d*.22,w*.78,d*.28,3.8,s,fronts=3)
        m.feature('SmallRaisedStage')
        m.box((0,.48,-d*.16),(w*.51,.5,d*.19),'wood')
        for side in (-1,1):m.box((side*w*.27,2.2,-d*.18),(.40,3.4,.09),'red')
        for xx in (-w*.20,0,w*.20):bench(m,xx,-d*.41,1.1)
        m.feature('LuteAndDrum')
        m.ellipsoid((-.7,1.1,-d*.18),(.28,.37,.16),'wood',n=16,rings=8)
        m.beam((-.7,1.3,-d*.18),(-.5,2.0,-d*.18),.08,'wood')
        m.cylinder((.8,.9,-d*.15),.35,.5,'clay',n=16)
        m.cylinder((.8,1.17,-d*.15),.36,.04,'linen',n=16)
    elif name=='VelvetTerrace':
        room(m,'RearLounge',0,d*.24,w*.78,d*.30,6.4,s,window_height=2.0,shape='hip',upper_doors=(3.375,))
        for floor in (0,3.0):
            m.feature('OpenLoungeTerrace')
            m.box((0,.25+floor,-d*.10),(w*.82,.25,d*.42),'trim')
            for x in (-w*.37,0,w*.37):m.box((x,1.7+floor,-d*.26),(.18,2.9,.18),'wood')
            for side in (-1,1):
                m.box((side*w*.38,.9+floor,-d*.08),(.12,1.2,d*.40),'wood')
                m.box((side*w*.23,.70+floor,-d*.10),(1.5,.3,.65),'red')
                lantern(m,side*w*.35,-d*.24,2.1+floor)
            if floor>0:
                m.feature('TerraceFrontBalustrade')
                for i in range(17):m.box(((i-8)*w*.045,floor+.83,-d*.29),(.055,.9,.065),'wood')
                m.box((0,floor+1.30,-d*.29),(w*.76,.10,.12),'wood')
        m.feature('TerraceInternalStair')
        for i in range(15):m.box((-w*.23,.43+i*.20,d*.12+i*d*.015),(.9,.16,.27),'wood')
        for side in (-1,1):m.beam((-w*.23+side*.4,.3,d*.11),(-w*.23+side*.4,3.25,d*.34),.13,'timber')
        m.feature('TerraceCanopy')
        m.roof(0,-d*.10,w*.84,d*.43,6.25,1.1,kind='hip')
    elif name=='LanternSpire':
        height=3.3
        for level in range(3):
            y=.23+level*height
            m.feature('OpenLanternPavilionTier')
            width=w*(.75-level*.07)
            depth=d*(.70-level*.06)
            if level==0:m.box((0,y,0),(width,.25,depth),'wood')
            else:
                m.feature('StairwellOpening')
                for side in (-1,1):
                    m.box((side*(width/4+.45),y,0),(width/2-.9,.25,depth),'wood')
                    m.box((0,y,side*(depth/4+.45)),(1.8,.25,depth/2-.9),'wood')
            m.feature('PavilionBalustrade')
            for side in (-1,1):
                for end in (-1,1):
                    m.box((side*width*.44,y+1.49,end*depth*.44),(.22,2.85,.22),'wood')
                    lantern(m,side*width*.39,end*depth*.41,y+1.65)
            for side in (-1,1):
                m.box((side*width*.44,y+1.08,0),(.12,.12,depth*.88),'wood')
                for i in range(10):m.box((side*width*.44,y+.60,(i-4.5)*depth*.088),(.06,.84,.06),'wood')
            for end in (-1,1):
                for i in range(13):
                    xx=(i-6)*width*.069
                    if level==0 and end==-1 and abs(xx)<.75:continue
                    m.box((xx,y+.60,end*depth*.44),(.06,.84,.06),'wood')
                if level>0 or end==1:m.box((0,y+1.08,end*depth*.44),(width*.88,.12,.12),'wood')
            if level==2:m.roof(0,0,width+.1,depth+.1,y+2.85,.65 if s==2 else .9,kind='hip')
            else:
                outerx,outerz=width/2+.3,depth/2+.3
                innerx,innerz=w*(.75-(level+1)*.07)/2,d*(.70-(level+1)*.06)/2
                for side in (-1,1):
                    points=[(side*outerx,y+2.85,-outerz),(side*outerx,y+2.85,outerz),(side*innerx,y+height,innerz),(side*innerx,y+height,-innerz)]
                    m.face(list(reversed(points)) if side==1 else points,'roof')
                    points=[(-outerx,y+2.85,side*outerz),(outerx,y+2.85,side*outerz),(innerx,y+height,side*innerz),(-innerx,y+height,side*innerz)]
                    m.face(list(reversed(points)) if side==-1 else points,'roof')
        m.feature('ContinuousSpiralStair')
        m.cylinder((0,3.7,0),.12,6.9,'timber',n=16)
        for i in range(36):
            a=i*math.tau/18;b=(i+1)*math.tau/18;y=.355+(i+1)*height/18
            m.ring(0,0,.12,.85,y-.12,y,'wood',n=3,start=a,end=b)
            m.beam((.85*math.cos(a),y,.85*math.sin(a)),(.85*math.cos(a),y+.9,.85*math.sin(a)),.035,'metal')
            m.beam((.85*math.cos(a),y+.9,.85*math.sin(a)),(.85*math.cos(b),y+.9+height/18,.85*math.sin(b)),.04,'metal')
    else:
        for side in (-1,1):room(m,'EnclosingLoungeWing',side*w*.32,d*.04,w*.22,d*.66,4.3,s,entry=False,shape='hip')
        room(m,'RearCourtyardLounge',0,d*.32,w*.48,d*.20,5.0,s,entry=False,shape='vault' if s==3 else 'hip')
        court(m,'PrivateInnerCourt',0,-d*.07,w*.44,d*.43)
        arcade(m,'VeiledCourtEntrance',0,-d*.32,w*.63,d*.16,3.1,s,fronts=3)
        m.feature('CurtainedOpenBays')
        for side in (-1,1):
            m.box((side*w*.30,1.9,-d*.25),(.35,2.7,.06),'red')
            planter(m,side*w*.18,-d*.17,1.0,.65)
            lantern(m,side*w*.25,-d*.34,2.0)
            table(m,side*w*.13,d*.10,w=1.0,d=.6)
    entertainment(m,name,w,d)


def plaza(m,w,d,s):
    m.feature('PublicGatheringSteps')
    if s==2:
        for i in range(3):m.cylinder((0,.23+i*.12,0),w*(.37-i*.035),.13,'trim',n=48)
    else:
        for i in range(3):m.box((0,.24+i*.13,0),(w*(.76-i*.07),.14,d*(.62-i*.05)),'trim')
    m.landmark('GrandTieredPublicFountain')
    m.ring(0,0,2.45,2.80,.55,1.25,'trim',n=48)
    m.cylinder((0,.96,0),2.43,.05,'water',n=48)
    m.lathe(0,.65,0,[(.8,0),(.52,.35),(.32,2.0),(.62,2.3)],'trim',n=32)
    m.ring(0,0,.30,1.60,2.85,3.12,'trim',n=40)
    m.cylinder((0,3.02,0),1.55,.04,'water',n=40)
    m.cylinder((0,3.68,0),.20,1.3,'bronze',n=24)
    m.ring(0,0,.18,.85,4.25,4.43,'bronze',n=32)
    m.cylinder((0,4.35,0),.82,.04,'water',n=32)
    m.ellipsoid((0,4.85,0),(.27,.48,.27),'gold',n=20,rings=12)
    for i in range(8):
        a=i*math.tau/8
        for j in range(8):
            def p(t):return math.cos(a)*(1.25+t*.95),3.0+.5*t-2.5*t*t,math.sin(a)*(1.25+t*.95)
            m.beam(p(j/8),p((j+1)/8),.055,'water')
    m.feature('CivicSeatingAndPlanting')
    for x,z in ((-w*.31,-d*.28),(w*.31,-d*.28),(-w*.31,d*.28),(w*.31,d*.28)):
        bench(m,x,z,2.0)
        planter(m,x,z+.6,2,.7)


def castle(m,w,d,s):
    court(m,'FortifiedInnerWard',0,0,w*.78,d*.76)
    for side in (-1,1):
        m.feature('CurtainWall')
        m.box((side*w*.40,2.3,0),(.65,4.2,d*.79),'stone')
        if side==1:m.box((0,2.3,side*d*.39),(w*.80,4.2,.65),'stone')
        else:
            for flank in (-1,1):m.box((flank*w*.245,2.3,side*d*.39),(w*.31,4.2,.65),'stone')
    m.feature('WallWalkAndCrenellations')
    for side in (-1,1):
        for i in range(14):
            m.box(((i-6.5)*w*.055,4.8,side*d*.39),(.85,1.0,.8),'trim')
        for i in range(12):m.box((side*w*.40,4.8,(i-5.5)*d*.061),(.8,1.0,.85),'trim')
    for side in (-1,1):
        for end in (-1,1):
            if s==2:round_tower(m,'CornerKeep',side*w*.39,end*d*.38,w*.075,8.7,s)
            else:square_tower(m,'SquareCornerBastion',side*w*.38,end*d*.37,w*.14,d*.14,7.4,s,battle=True)
    room(m,'RoyalGreatHall',0,-d*.10,w*.47,d*.38,9.5 if s==2 else 8.0,s,window_height=3.5,shape='gable' if s==2 else 'vault')
    if s==2:square_tower(m,'CentralRoyalKeep',0,d*.21,w*.22,d*.24,14.0,s)
    else:square_tower(m,'AsymmetricRoyalKeep',-w*.18,d*.21,w*.25,d*.26,13.2,s,battle=True)
    m.feature('OpenGatehousePassage')
    for side in (-1,1):square_tower(m,'GatehouseFlank',side*w*.13,-d*.39,w*.12,d*.14,7.2,s)
    m.arch(0,.23,-d*.40,w*.12,3.6,.4,.9,'trim')
    m.box((0,4.5,-d*.40),(w*.16,1.3,d*.11),'stone')
    m.feature('RoyalGuardCourt')
    for side in (-1,1):
        weapon_rack(m,side*w*.19,-d*.23,2.4,shields=True)
        planter(m,side*w*.30,d*.01,2.5,1.4)


def services(m,name,w,d,s):
    if name=='Bathhouse':
        room(m,'BathingHall',0,d*.14,w*.74,d*.55,4.7,s,entry=True,roof=False,ceiling=False)
        m.feature('BathingDome')
        m.ring(0,d*.14,w*.25,w*.28,4.7,5.15,'trim',n=48)
        m.dome(0,d*.14,w*.28,5.15,2.7,mat='bronze',oculus=.55)
        m.feature('BathHallRoofShoulders')
        m.box((0,4.78,d*.14),(w*.78,.16,d*.59),'metal')
        m.feature('BathingHallRoofLantern')
        m.cylinder((0,8.12,d*.14),.55,.60,'roofglass',n=20)
        m.cylinder((0,8.64,d*.14),.73,.45,'bronze',n=20,top=.06)
        for i in range(8):
            a=i*math.tau/8
            m.beam((math.cos(a)*.56,7.82,d*.14+math.sin(a)*.56),(math.cos(a)*.56,8.42,d*.14+math.sin(a)*.56),.06,'bronze')
        arcade(m,'OpenBathCourt',0,-d*.26,w*.65,d*.24,3.0,s,fronts=3)
        m.feature('BathPoolsAndBenches')
        for side in (-1,1):
            m.ring(side*w*.17,-d*.27,.9,1.15,.28,.8,'trim',n=24)
            m.cylinder((side*w*.17,.55,-d*.27),.88,.04,'water',n=24)
            bench(m,side*w*.25,-d*.41,1.7)
    elif name=='Barracks':
        for side in (-1,1):room(m,'SoldiersDormitory',side*w*.28,d*.14,w*.28,d*.57,5.0,s,entry=True,window_height=1.5,door_width=1.5,door_height=2.6)
        court(m,'BarracksDrillYard',0,-d*.19,w*.36,d*.40)
        m.feature('SoldiersDrillEquipment')
        for side in (-1,1):
            weapon_rack(m,side*w*.21,-d*.20,2.0,shields=side<0)
            dummy(m,side*w*.11,-d*.34)
    elif name=='Guildhall':
        room(m,'MerchantsAssemblyHall',0,d*.10,w*.74,d*.55,7.0,s,window_height=2.6,shape='gable' if s==2 else 'vault')
        arcade(m,'OpenTradingLoggia',0,-d*.29,w*.78,d*.23,3.5,s,fronts=4)
        m.feature('TradeDesksAndGoodsScales')
        for side in (-1,1):
            reading_desk(m,side*w*.22,-d*.30,1.8)
            barrel(m,side*w*.35,-d*.25,.35)
        table(m,0,-d*.30,1.6,.7)
        m.beam((0,1.15,-d*.30),(0,2.0,-d*.30),.04,'bronze')
        m.beam((-.45,1.90,-d*.30),(.45,1.90,-d*.30),.04,'bronze')
        for side in (-1,1):m.cylinder((side*.42,1.54,-d*.30),.19,.06,'bronze',n=16)
    elif name=='Bakery':
        room(m,'Bakehouse',-w*.17,d*.08,w*.48,d*.63,4.7,s,window_height=1.4)
        arcade(m,'OpenBreadCounter',w*.19,-d*.17,w*.39,d*.29,3.0,s,fronts=2)
        m.feature('MasonryBreadOvens')
        m.dome(w*.21,d*.14,1.25,.25,1.8,mat='brick')
        m.arch(w*.21,.25,d*.14-1.20,.85,1.0,.22,.25,'brick')
        m.box((w*.21,2.7,d*.28),(.65,4.4,.65),'brick')
        m.feature('BreadCounterAndTrays')
        table(m,w*.20,-d*.27,2.7,.9)
        for i in range(8):m.ellipsoid((w*.20+(i%4-1.5)*.48,1.25,-d*.27+(i//4-.5)*.35),(.24,.12,.13),'bread',n=12,rings=6)
    elif name=='Warehouse':
        room(m,'TallGoodsWarehouse',0,d*.04,w*.82,d*.73,7.5,s,window_height=1.3,entry=True,door_width=3.8,door_height=4.0,spacing=3.4,shape='gable')
        m.feature('WideGoodsLoadingDoors')
        m.feature('HoistAndLoadingPlatform')
        m.box((0,.40,-d*.39),(w*.60,.34,d*.16),'wood')
        m.beam((0,6.8,-d*.28),(0,6.8,-d*.47),.18,'wood')
        m.beam((0,6.8,-d*.45),(0,2.4,-d*.45),.035,'metal')
        for side in (-1,1):
            barrel(m,side*w*.26,-d*.37,.48)
            m.box((side*w*.32,.73,-d*.38),(1.1,1.0,1.0),'wood')
    elif name=='Granary':
        m.feature('RaisedRatProofGranary')
        for x in (-w*.29,0,w*.29):
            for z in (-d*.26,d*.26):
                m.cylinder((x,.71,z),.22,1.0,'stone',n=10)
                m.cylinder((x,1.28,z),.52,.14,'trim',n=12)
        room(m,'VentilatedGrainStore',0,d*.04,w*.73,d*.65,6.4,s,base=1.4,entry=True,window_height=.8,wall='wood')
        m.feature('GrainLoadingChute')
        for i in range(5):m.box((0,.25+i*.24,-d*.43+i*.22),(1.7,.22,.4),'wood')
        for side in (-1,1):m.ellipsoid((side*w*.26,.6,-d*.38),(.4,.45,.34),'linen',n=12,rings=8)
    elif name=='Stable':
        room(m,'HayLoftAndTackRoom',0,d*.24,w*.82,d*.26,5.4,s,entry=False,window_height=1.0,wall='wood')
        arcade(m,'OpenHorseStalls',0,-d*.08,w*.80,d*.42,3.3,s,fronts=4)
        m.feature('StallPartitionsAndFeedTroughs')
        for i in range(5):
            x=(i-2)*w*.19
            m.box((x,1.1,-d*.05),(.1,1.7,d*.37),'wood')
        for i in range(4):
            x=(i-1.5)*w*.19
            m.box((x,.6,-d*.32),(w*.15,.75,.6),'wood')
            m.box((x,1.0,-d*.32),(w*.13,.05,.48),'straw')
            m.box((x,.75,d*.08),(1.1,1.0,.8),'straw')
    elif name=='Market_Hall':
        arcade(m,'CoveredPublicMarketHall',0,d*.01,w*.84,d*.64,4.4,s,fronts=4)
        m.feature('PublicGoodsCounters')
        for side in (-1,1):
            table(m,side*w*.26,-d*.14,2.7,1.0)
            for i in range(6):
                m.ellipsoid((side*w*.26+(i%3-1)*.45,1.20,-d*.14+(i//3-.5)*.36),(.19,.12,.15),'bread',n=10,rings=6)
            barrel(m,side*w*.29,d*.19,.45)
    else:raise ValueError(name)


def build(m,name,w,d,style):
    if style!='CityV2':raise ValueError(style)
    s=2
    footing(m,w,d)
    if name.startswith('WarriorAcademy'):warrior(m,w,d,s,int(name[-1]))
    elif name.startswith('ArcaneAcademy'):arcane(m,w,d,s,int(name[-1]))
    elif name.startswith('SpiritAcademy'):spirit(m,w,d,s,int(name[-1]))
    elif name in ('Church','Chapel'):church(m,w,d,s,name=='Chapel')
    elif name=='Library':library(m,w,d,s)
    elif name=='Tavern':tavern(m,w,d,s)
    elif name=='Observatory':observatory(m,w,d,s)
    elif name=='Museum':museum(m,w,d,s)
    elif name=='Clinic':clinic(m,w,d,s)
    elif name=='Casino':casino(m,w,d,s)
    elif name=='Guardhouse':guard(m,w,d,s)
    elif name=='Blacksmith':blacksmith(m,w,d,s)
    elif name=='Arena':arena(m,w,d,s)
    elif name in ('CrimsonRowhouse','VelvetTerrace','LanternSpire','VeiledCourtyard'):nightlife(m,name,w,d,s)
    elif name=='Plaza':plaza(m,w,d,s)
    elif name=='Royal_Castle':castle(m,w,d,s)
    else:services(m,name,w,d,s)
