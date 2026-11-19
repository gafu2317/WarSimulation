"""A concentric stone bowl, with an open entry, stairs and two rear awnings."""

from math import asin, cos, pi, sin, sqrt, tau

from mathutils.bvhtree import BVHTree

from components import Opening, gable, wall
from landmarks import outline


PALETTE = {
    "wall":"Arena_RusticatedLimestone", "trim":"Arena_HoneySandstone", "seat":"Arena_CutStepStone",
    "sand":"Arena_PackedSand", "canopy":"Arena_OchreWeave", "laurel":"Arena_BurnishedLaurel",
}
MATERIALS = {
    "Arena_RusticatedLimestone":dict(source="Fieldstone",color="998b75",relief=.24),
    "Arena_HoneySandstone":dict(source="Limestone",color="c3a580",relief=.16),
    "Arena_CutStepStone":dict(source="Limestone",color="aba08b",relief=.19),
    "Arena_PackedSand":dict(source="Earth",color="b7996e",roughness=.89,relief=.14),
    "Arena_OchreWeave":dict(source="Canvas_Gold",color="ba9550",roughness=.85,relief=.17),
    "Arena_BurnishedLaurel":dict(source="Brass",color="a67c42",metallic=.44,roughness=.46,relief=.12),
}

RADIUS, ROW_START, CUT = 22.04,13.80,4.24
ROWS, RUN, RISE = 8,.88,.72
CAMERA_DIRECTIONS = {"three_quarter":(.11,-1,1.25),"rear":(.51,1,1.16),"front":(0,-1,.45)}


def radial(radius,angle,y):
    return radius*cos(angle),y,radius*sin(angle)


def horseshoe(m,label,inner,outer,bottom,top,material,n=92):
    m.feature(label)
    def p(radius,i,y):
        angle = -pi/2+asin(CUT/radius)
        return radial(radius,angle+(tau-2*asin(CUT/radius))*i/n,y)
    for i in range(n):
        j=i+1
        m.face([p(inner,i,top),p(inner,j,top),p(outer,j,top),p(outer,i,top)],material)
        m.face([p(outer,i,bottom),p(outer,i,top),p(outer,j,top),p(outer,j,bottom)],material)
        m.face([p(inner,j,bottom),p(inner,j,top),p(inner,i,top),p(inner,i,bottom)],material)
        m.face([p(inner,j,bottom),p(inner,i,bottom),p(outer,i,bottom),p(outer,j,bottom)],material)
    for i in (0,n):
        points=[p(inner,i,bottom),p(inner,i,top),p(outer,i,top),p(outer,i,bottom)]
        m.face(points if i==0 else list(reversed(points)),material)


def seating(m):
    m.feature("SandPitchWithinTheRing")
    m.cylinder((0,.091,0),RADIUS-.16,.182,"sand",n=112)
    for i in range(ROWS):
        r=ROW_START+i*RUN
        y=1.04+i*RISE
        horseshoe(m,"SeatingTier"+str(i+1),r,r+RUN,.18,y,"seat" if i%2==0 else "trim")
    horseshoe(m,"UpperPerimeterWalk",ROW_START+ROWS*RUN,RADIUS-.22,.18,6.55,"seat")
    for bottom,top,stone in ((0,.72,"trim"),(.72,3.11,"wall"),(3.11,3.55,"trim"),
                             (3.55,6.47,"wall"),(6.47,8.16,"wall"),(8.16,8.52,"trim")):
        horseshoe(m,"OuterRingAndParapet",RADIUS-.61,RADIUS,bottom,top,stone)
    m.feature("OuterRingButtresses")
    for i in range(1,18):
        a=i*tau/18-pi/2
        m.box(radial(RADIUS, a,3.94),(.71,7.88,.85),"trim",-a)
        m.box(radial(RADIUS+.04,a,.42),(.95,.84,1.06),"trim",-a)
    m.feature("RadialCrowdStairFlights")
    for a in (-.74,.51,pi/2,2.63,3.46,3.97):
        for i in range(ROWS):
            r=ROW_START+i*RUN
            y=1.04+i*RISE
            m.box(radial(r+RUN*.75,a,y+.18),(RUN/2,.36,1.37),"trim",-a)
        for i in range(2):
            y=.182+(1.04-.182)*(i+1)/3
            m.box(radial(ROW_START-.43*(1.5-i),a,(y+.182)/2),(.43,y-.182,1.38),"trim",-a)


def canopy(m,label,start,end):
    inner,outer = 17.18,21.65
    lo,hi = 10.96,14.35
    def p(u,v):
        return radial(inner+(outer-inner)*v,start+(end-start)*u,
                      lo+(hi-lo)*v-.42*sin(v*pi)-.23*sin(2*u*pi)**2)
    m.feature(label+"WovenFabric")
    for i in range(18):
        for j in range(5):
            a,b=i/18,(i+1)/18
            x,y=j/5,(j+1)/5
            m.face([p(a,x),p(a,y),p(b,y),p(b,x)],"canopy",[(a*4,x),(a*4,y),(b*4,y),(b*4,x)])
    m.feature(label+"PostsAndTensionRails")
    for v,foot in ((0,3.20),(1,6.54)):
        for u in (0,.50,1):
            x,y,z=p(u,v)
            m.box((x,(y+foot)/2,z),(.32,y-foot,.32),"wood")
            m.box((x,foot+.18,z),(.50,.37,.49),"trim")
            toward=.14 if u<.75 else -.14
            tip=p(u+toward,v)
            m.beam((x,y-1.47,z),tip,.19,"timber",.19)
        for i in range(18):
            m.beam(p(i/18,v),p((i+1)/18,v),.145,"wood",.14)
    for u in (0,.50,1):
        for j in range(5):
            m.beam(p(u,j/5),p(u,(j+1)/5),.104,"timber",.09)


def arched_entry(m):
    front = -sqrt(RADIUS**2-CUT**2)-.25
    m.feature("PortalPavingThreshold")
    m.box((0,.088,front+.43),(CUT*2,.176,.86),"sand")
    opening=Opening(CUT,.176,6.14,5.18,1.58,None,.37)
    wall(m,"SpectatorEntryArch",(-CUT,front),(CUT,front),.176,8.28,1.29,"trim",[opening])
    gable(m,"AttachedEntryPediment",(-CUT,front-.22,CUT,front+.92),8.54,10.23,.19,
          wall_material="trim",roof_material="trim")
    for x in (-3.78,3.78):
        m.feature("PortalStoneJambs")
        m.box((x,4.12,front-.08),(.81,8.24,.67),"trim")
        m.box((x,.33,front-.11),(1.13,.66,.93),"trim")
    m.landmark("MountedLaurelWreath")
    z=front-.234
    for side in (-1,1):
        def p(a,r):
            return side*r*sin(a),7.39-r*cos(a)
        for i in range(7):
            angle=.23+i*.37
            a,b=p(angle-.20,1.38),p(angle+.22,1.38)
            m.beam((*a,z),(*b,z),.058,"laurel")
            x,y=p(angle,1.39)
            end=p(angle+.36,1.88)
            points=[(x,y),(x+side*.30,y-.048),end,(x+side*.13,y+.32)]
            outline(m,points,z-.042,.09,"laurel")
    m.markers["Entrance"]=(0,.176,front)


def build(m):
    seating(m)
    canopy(m,"EastRearAwning",.67,1.44)
    canopy(m,"WestRearAwning",1.70,2.47)
    arched_entry(m)


def verify(objects):
    for obj in objects:
        if obj.name.startswith(("SeatingTier","OuterRingAndParapet","SpectatorEntryArch")):
            mesh=obj.data
            tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
            assert tree.ray_cast((0,-24,1.8),(0,1,0),23)[0] is None,obj.name
    assert len([o for o in objects if o.name.startswith("SeatingTier")])==ROWS
    return {"open_route_to_arena":True,"seat_tiers":ROWS,"arena_sand_diameter_m":ROW_START*2}
