"""A public square with stone inlay, seating and a nested fountain."""

from math import atan2, cos, pi, sin, sqrt, tau

from mathutils.bvhtree import BVHTree

from components import floor, pipe
from materials import artwork
from spirit_academy import leaf_crown


PALETTE = {
    "trim":"Plaza_LightDressedStone", "stone":"Plaza_SandstoneBasin", "water":"Plaza_PoolWater",
    "stream":"Plaza_FallingWater", "paving_inlay":"Plaza_CompassInlay",
    "leaf":"Plaza_DeepHedgeLeaves", "leaflight":"Plaza_NewHedgeLeaves",
}
MATERIALS = {
    "Plaza_LightDressedStone":dict(source="Limestone",color="c4baa4",relief=.20),
    "Plaza_SandstoneBasin":dict(source="Limestone",color="aaa188",relief=.22),
    "Plaza_PoolWater":dict(source="Water",color="4b9fa0",roughness=.28,metallic=.09,relief=.26),
    "Plaza_FallingWater":dict(source="Water",color="9bd3cb",roughness=.32,metallic=.15,relief=.16,emittance=.25,thin_glass=.46),
    "Plaza_DeepHedgeLeaves":dict(source="Foliage",color="728346",roughness=.85,relief=.34),
    "Plaza_NewHedgeLeaves":dict(source="Foliage",color="8d995c",roughness=.83,relief=.30),
}

PLAN = (-17.7,-13.84,17.7,13.84)
CAMERA_DIRECTIONS = {"three_quarter":(.28,-1,1.18),"front":(0,-1,.67),"rear":(-.58,1,1.18)}


def paving_and_seating(m):
    x,z,hi_x,hi_z = PLAN
    floor(m,"PlazaFlagstoneEdge",PLAN,.184,"paving")
    m.feature("InlaidPublicSquarePaving")
    m.face([(x,.187,z),(x,.187,hi_z),(hi_x,.187,hi_z),(hi_x,.187,z)],"paving_inlay",[(0,0),(0,1),(1,1),(1,0)])
    for i,(x,z) in enumerate(((-13.18,-10.36),(13.18,-10.36),(13.18,10.36),(-13.18,10.36))):
        distance=sqrt(x*x+z*z)
        inward=(-x/distance,-z/distance)
        yaw=atan2(inward[0],inward[1])
        def p(a,y,b):
            return x+a*cos(yaw)+b*sin(yaw),y,z-a*sin(yaw)+b*cos(yaw)
        m.feature(f"SeatAndGarden{i+1}_Planter")
        m.box(p(0,.41,-.50),(5.61,.45,2.04),"stone",yaw)
        m.box(p(0,.668,-.50),(5.82,.15,2.18),"trim",yaw)
        m.box(p(0,.75,-.50),(5.24,.07,1.83),"soil",yaw)
        m.feature(f"SeatAndGarden{i+1}_Leaves")
        for j,a in enumerate((-2.04,-1.27,-.39,.43,1.28,2.02)):
            leaf_crown(m,p(a,1.03,-.50),(.58,.38+(j%2)*.06,.51),i*.54+j*.81,"leaf" if j%2 else "leaflight")
        m.feature(f"SeatAndGarden{i+1}_Bench")
        for a in (-1.63,1.63):
            m.box(p(a,.513,1.87),(.16,.65,.60),"metal",yaw)
            m.beam(p(a,.37,1.54),p(a,1.24,1.41),.15,"metal",.13)
        for b in (1.59,1.85,2.10):
            m.box(p(0,.873,b),(3.88,.15,.24),"wood",yaw)
        for y in (1.13,1.43):
            m.box(p(0,y,1.44),(3.87,.22,.14),"wood",yaw)


def fountain(m):
    m.feature("FountainStonePlinth")
    m.cylinder((0,.328,0),6.75,.29,"stone",n=60)
    pipe(m,(0,0,0),(0,1,0),[(.46,6.40),(.62,6.45),(1.18,6.45),(1.30,6.49),(1.41,6.39),
                           (1.41,6.13),(1.28,6.04),(.55,6.04),(.55,6.40)],"trim",60)
    m.feature("LowerCatchBasinWater")
    m.cylinder((0,1.244,0),6.078,.018,"water",n=60)
    m.feature("FountainStackedStoneStem")
    pipe(m,(0,0,0),(0,1,0),[(.47,0),(.47,1.51),(.78,1.51),(1.31,1.26),(2.25,1.12),(2.60,1.28),
                           (2.79,1.25),(3.01,.99),(3.77,.87),(4.13,1.06),(4.60,.77),(5.69,.58),
                           (6.21,.69),(6.68,.43),(6.83,.43),(6.83,.16),(6.53,.15)],"stone",42)
    for i,(radius,height) in enumerate(((4.83,2.93),(3.13,4.63),(1.70,6.28))):
        m.feature(f"FountainBowl{i+1}")
        pipe(m,(0,0,0),(0,1,0),[(height-.77,radius*.24),(height-.73,radius*.53),
               (height-.47,radius*.78),(height-.23,radius*.95),(height-.12,radius),
               (height+.04,radius),(height+.065,radius-.23),(height-.07,radius-.31),
               (height-.45,radius*.62),(height-.47,radius*.25)],"stone",56)
        m.feature(f"FountainWaterSurface{i+1}")
        m.cylinder((0,height-.085,0),radius-.307,.020,"water",n=56)
        m.feature(f"FountainOverflows{i+1}")
        catch=(1.262,2.86,4.56)[i]
        for j in range(11-i*2):
            angle = j*tau/(11-i*2)+.25*(i+1)
            width = .15+.060*(j%3)
            def p(v,a):
                r=radius-.16+.53*sin(v*pi*.5)
                y=height+.062-(height+.062-catch)*v*v
                theta=angle+(a-.5)*width/r
                return r*cos(theta),y,r*sin(theta)
            for k in range(6):
                a,b=k/6,(k+1)/6
                m.face([p(a,0),p(a,1),p(b,1),p(b,0)],"stream",[(0,a),(1,a),(1,b),(0,b)])
    m.landmark("FountainCrownWaterJets")
    for i in range(5):
        angle=i*tau/5+.16
        def p(t):
            r=.074+.87*t**1.45
            h=6.78+sin(pi*t*.90)*(1.37+.29*cos(angle))-.96*t*t
            return r*cos(angle),h,r*sin(angle)
        for j in range(10):
            m.beam(p(j/10),p((j+1)/10),.115-.0065*j,"stream")


def materials():
    def compass(u,v):
        x,z=(u-.5)*35.4,(v-.5)*27.68
        a,b=x/.79,z/.79
        i,j=int(a+60),int(b+60)
        gap=min(abs(a-round(a)),abs(b-round(b)))
        ivory=min(abs(x),abs(z))<1.54 or abs(abs(z)-abs(x)*.74)<.51 or min(17.7-abs(x),13.84-abs(z))<.62
        if gap<.028:
            return 0 if ivory else 1
        if gap<.070:
            return 2 if ivory else 3
        return (4 if ivory else 7)+(i*37+j*13+i*j*29)%3
    return {**MATERIALS,"Plaza_CompassInlay":artwork("Plaza_CompassInlay",compass,
        ("8c7864","5d6066","b6a68b","888993","b9a48a","c3ad92","bba88e","8e9398","8b8b94","949a9e"),
         width=1024,height=800)}


def build(m):
    paving_and_seating(m)
    fountain(m)


def verify(objects):
    for i in range(1,4):
        bowl=next(o.data for o in objects if o.name==f"FountainBowl{i}_Plaza_SandstoneBasin")
        tree=BVHTree.FromPolygons([v.co for v in bowl.vertices],[p.vertices[:] for p in bowl.polygons])
        assert tree.ray_cast((0,0,9),(0,0,-1),9)[0] is None
    return {"nested_open_water_bowls":3,"plaza_paving_m":[35.4,27.68]}
