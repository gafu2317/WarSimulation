"""A clear-span dance hall with a visible inlaid floor and a readable sign."""

from math import cos, radians, sin

from mathutils.bvhtree import BVHTree

from components import Opening, floor, gable, rectangle_walls
from landmarks import outline
from materials import artwork


PALETTE = {
    "roof":"Ballroom_PetrolCopper", "wall":"Ballroom_HoneyAshlar",
    "trim":"Ballroom_DressedSandstone", "parquet":"Ballroom_InlaidParquet",
    "dancers":"Ballroom_CarvedDancers", "flashing":"Ballroom_WeatheredCopper",
}
MATERIALS = {
    "Ballroom_PetrolCopper":dict(source="Copper",color="42676a",roughness=.56,metallic=.22,relief=.09),
    "Ballroom_HoneyAshlar":dict(source="Limestone",color="b7a078",relief=.14),
    "Ballroom_DressedSandstone":dict(source="Limestone",color="d2bc93",relief=.13),
    "Ballroom_CarvedDancers":dict(source="Oak",color="6b4a35",relief=.11),
    "Ballroom_WeatheredCopper":dict(source="Copper",color="657063",roughness=.44,metallic=.31,relief=.08),
}

BOUNDS = (-16.8,-14,16.8,14)
EAVES, RIDGE, OVERHANG = 14.72, 24.40, .61
CAMERA_DIRECTIONS = {"three_quarter":(.34,-1,.39)}


def carved_sign(m,front):
    m.landmark("DancingPair")
    figures = [
        [(-.14,.58),(-.33,.74),(-1.12,.29),(-1.49,.43),(-1.57,.69),(-1.33,.79),
         (-1.28,1.12),(-1.43,1.34),(-1.76,1.39),(-1.99,1.20),(-2.04,.88),(-1.91,.70),
         (-2.22,.48),(-2.39,.03),(-2.24,-.07),(-2,.16),(-1.74,-.05),(-2.30,-.91),
         (-1.77,-1.12),(-1.84,-1.45),(-1.65,-1.49),(-1.29,-1.12),(-.99,-1.13),(-.93,-1.46),
         (-.59,-1.44),(-.74,-.98),(-.48,-.87),(-1.07,-.03),(-.89,.04)],
        [(.14,.61),(.31,.76),(1.04,.37),(1.38,.65),(1.31,.85),(1.37,1.22),(1.64,1.39),
         (1.94,1.29),(2.05,1.00),(1.91,.73),(2.18,.49),(2.39,.19),(2.27,.02),
         (2,.24),(1.96,-.40),(1.70,-.57),(2.05,-1.30),(2.27,-1.41),(2.22,-1.55),
         (1.84,-1.53),(1.30,-.78),(1.09,-.59),(.82,-.97),(.84,-1.47),(.49,-1.50),
         (.48,-.94),(.71,-.41),(.87,.09)],
    ]
    for polygon in figures:
        outline(m,[(1.28*u,9.84+1.26*v) for u,v in polygon],front-.15,.26,"dancers")
    m.box((0,10.66,front-.20),(.54,.23,.23),"dancers")


def floor_and_trusses(m):
    left,front,right,back = BOUNDS
    floor(m,"GreatHallFloor",BOUNDS,.22,"wood")
    m.feature("InlaidDanceFloor")
    m.face([(left+.63,.224,front+.54),(left+.63,.224,back-.54),
            (right-.63,.224,back-.54),(right-.63,.224,front+.54)],
           "parquet",[(0,0),(0,1),(1,1),(1,0)])
    m.feature("InteriorBenches")
    for side in (-1,1):
        x = side*15.62
        for z in (-9.0,-4.5,0,4.5,9.0):
            m.box((x,.76,z),(.69,.18,2.77),"wood")
            for dz in (-1.0,1.0):
                m.box((x,.447,z+dz),(.54,.446,.28),"timber")
    m.feature("HallRoofTrusses")
    for z in (-10.58,-3.55,3.55,10.58):
        m.beam((left+.54,14.15,z),(right-.54,14.15,z),.52,"timber",.51)
        m.beam((0,14.13,z),(0,23.83,z),.44,"timber",.41)
        for side in (-1,1):
            m.beam((side*15.82,14.42,z),(0,23.73,z),.37,"timber",.41)
            m.beam((side*15.98,11.48,z),(side*12.1,14.15,z),.32,"timber",.36)


def roof_and_gable_frames(m):
    left,front,right,back = BOUNDS
    gable(m,"AssemblyHallRoof",BOUNDS,EAVES,RIDGE,OVERHANG)
    edge = right+OVERHANG
    low = EAVES-(RIDGE-EAVES)*OVERHANG/right
    m.feature("StandingSeamsAndRidgeFlashing")
    m.beam((0,RIDGE+.035,front-OVERHANG),(0,RIDGE+.035,back+OVERHANG),.24,"flashing",.19)
    for side in (-1,1):
        for i in range(20):
            z = front+.12+i*(back-front-.24)/19
            m.beam((side*edge,low+.025,z),(0,RIDGE+.025,z),.071,"flashing",.080)
        m.beam((side*edge,low-.136,front-OVERHANG),(side*edge,low-.136,back+OVERHANG),.16,"wood",.27)
        for z in (front-OVERHANG,back+OVERHANG):
            m.beam((side*edge,low-.148,z),(0,RIDGE-.148,z),.21,"wood",.31)

    m.feature("FramedGables")
    for z in (front-.09,back+.09):
        m.beam((left,14.18,z),(right,14.18,z),.27,"timber",.42)
        for side in (-1,1):
            for x in (5.8,11.4):
                top = RIDGE-(RIDGE-EAVES)*x/right-.48
                m.box((side*x,(top+14.20)*.5,z),(.36,top-14.20,.28),"wood")
            m.beam((side*5.8,19.36,z),(0,14.45,z),.32,"wood",.34)
            m.beam((side*15.9,14.75,z),(0,23.87,z),.26,"timber",.31)
        m.box((0,22.82,z),(2.42,1.22,.32),"timber")
        for x in (-.68,0,.68):
            m.box((x,22.82,z-.18 if z<0 else z+.18),(.19,1.18,.18),"wood")


def build(m):
    left,front,right,back = BOUNDS
    w,d = right-left,back-front
    facade = [Opening(c,.92,8.25,10.46,2.10,"clear_glass",.31) for c in (6.67,w-6.67)]
    facade += [Opening(w*.5,.22,6.04,7.24,1.36,None,.34)]
    side = [Opening((i+.5)*d/5,2.14,2.47,8.12,.95,"clear_glass",.20) for i in range(5)]
    rear = [Opening(c,3.10,2.94,7.42,.89,"warm_glass") for c in (5.5,10.8,16.8,22.8,28.1)]
    rectangle_walls(m,"AssemblyHall",BOUNDS,.22,EAVES-.26,.65,openings={
        "Front":facade,"East":side,"West":side,"Rear":rear})
    roof_and_gable_frames(m)
    floor_and_trusses(m)
    m.feature("FrontMasonryButtresses")
    for x in (-16.43,-5.92,5.92,16.43):
        m.box((x,6.91,front+.05),(1.17,13.82,.90),"trim")
        for y,width in ((.39,1.41),(5.88,1.25),(13.70,1.36)):
            m.box((x,y,front+.03),(width,.40,1.10),"trim")
    for x in (left,right):
        m.feature("EaveButtresses")
        for z in (-11.2,-5.6,0,5.6,11.2):
            m.box((x,6.42,z),(.63,12.84,.90),"trim")
    m.feature("LargeGlazingMullions")
    for window in facade[:2]:
        for dx in (-2.06,2.06):
            x=left+window.center+dx
            m.beam((x,window.bottom,front+.153),(x,window.crown(window.center+dx),front+.153),.104,"timber",.10)
        for y in (3.24,8.55):
            m.beam((left+window.center-window.width/2,y,front+.15),
                   (left+window.center+window.width/2,y,front+.15),.106,"timber",.12)
    floor(m,"LowPublicThreshold",(-3.45,front-1.03,3.45,front),.22,"trim")
    m.feature("OpenDoubleDoors")
    angle=radians(77)
    for side in (-1,1):
        x=side*(2.92-1.39*cos(angle))
        m.box((x,2.93,front+.34+1.39*sin(angle)),(2.78,5.42,.16),"wood",side*angle)
    m.markers["Entrance"]=(0,.22,front-1.03)
    carved_sign(m,front)


def materials():
    def parquet(u,v):
        x,y = abs(u-.5),abs(v-.5)
        diamond = x+.91*y
        if abs(diamond-.34)<.006 or abs(diamond-.295)<.004 or abs(diamond-.205)<.005:
            return 3
        if abs((x/.092)**2+(y/.105)**2-1)<.15:
            return 3
        row = int(v*77)
        column = int(u*19+.5*(row%2))
        if diamond<.34:
            return (row*127+column*53)%3
        return 4+(row*23+column*89)%2
    return {**MATERIALS,"Ballroom_InlaidParquet":artwork("Ballroom_InlaidParquet",parquet,
        ("b98855","c29361","b98356","e8c498","96704b","a67850"),width=512,height=512,emittance=.07)}


def verify(objects):
    facade = next(o.data for o in objects if o.name=="AssemblyHallFront_Ballroom_HoneyAshlar")
    tree = BVHTree.FromPolygons([v.co for v in facade.vertices],[p.vertices[:] for p in facade.polygons])
    hit,_,_,_ = tree.ray_cast((0,-17,1.6),(0,1,0),6.0)
    assert hit is None
    assert sum(o.name.startswith("InlaidDanceFloor_") for o in objects)==1
    return {"parquet_is_single_surface":True,"main_entry_open":True,"span_between_side_benches_m":30.54}
