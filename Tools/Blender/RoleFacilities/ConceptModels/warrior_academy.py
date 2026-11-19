"""Training hall with attached galleries, a main gate and an open practice yard."""

from mathutils.bvhtree import BVHTree

from components import Opening, floor, gable, rectangle_walls, wall
from landmarks import outline, sword


PALETTE = {
    "roof":"Warrior_IronRedClay", "wall":"Warrior_GreyAshlar",
    "trim":"Warrior_LightGranite", "gable":"Warrior_LimedPlaster",
    "yard":"Warrior_CompactedOchre", "red":"Warrior_OxbloodCloth",
}
MATERIALS = {
    "Warrior_GreyAshlar":dict(source="Fieldstone",color="858780",roughness=.77,relief=.25),
    "Warrior_LightGranite":dict(source="Limestone",color="b3aa93",relief=.14),
    "Warrior_LimedPlaster":dict(source="Plaster",color="c6ac86",relief=.16),
    "Warrior_CompactedOchre":dict(source="Earth",color="bd935b",roughness=.88,relief=.21),
    "Warrior_OxbloodCloth":dict(source="Canvas_Red",color="9c4745",roughness=.84,relief=.13),
}

HALL = (-17.2,4.5,17.2,16.7)
FRONT = -16.2
COURT_HALF = 12.0
CAMERA_DIRECTIONS = {"three_quarter":(.40,-1,.72)}


def high_hall(m):
    left,front,right,back = HALL
    width = right-left
    floor(m,"BarracksAndInstructionHallFloor",HALL,.48,"paving")
    center = Opening(width/2,9.20,3.87,6.85,1.35)
    front_high = [center,*[Opening(width/2+x,9.20,2.79,5.44,1.05) for x in (-4.4,4.4)],
        *[Opening(width/2+x,8.40,2.46,4.65,.85) for x in (-9.21,9.21)]]
    entry = Opening(width/2,.48,4.30,6.45,1.24,"door",.30)
    rear = [Opening(x,7.82,2.54,5.76,.70) for x in (3.4,8.96,14.42,19.98,25.44,31.0)]
    side = [Opening(c,9.60,2.04,4.75,.69) for c in (2.25,6.1,9.95)]
    rectangle_walls(m,"InstructionHall",HALL,.48,17.78,.74,
                    openings={"Front":[entry,*front_high],"Rear":rear,"East":side,"West":side})
    gable(m,"InstructionHallClayRoof",HALL,18.04,24.70,.48,wall_material="gable")
    m.feature("HallFrontStonePiers")
    for x in (-7.0,7.0):
        m.box((x,8.27,front-.11),(1.03,15.51,.71),"trim")
        m.box((x,1.03,front-.11),(1.25,1.12,.89),"trim")
    m.box((0,8.55,front-.054),(11.14,.34,.40),"trim")
    for x in (-2.47,2.47):
        m.box((x,12.45,front-.038),(.61,7.54,.37),"trim")
    m.feature("GableTrussAndRidge")
    m.beam((0,24.69,front-.48),(0,24.69,back+.48),.27,"roof",.21)
    for z in (front-.09,back+.09):
        m.beam((left,17.74,z),(right,17.74,z),.30,"timber",.38)
        for side in (-1,1):
            m.beam((0,18.03,z),(side*6.48,21.83,z),.28,"timber",.34)
            m.beam((0,24.10,z),(side*16.25,17.93,z),.31,"timber",.34)
    m.feature("HallSteps")
    m.box((0,.19,front-.62),(5.5,.20,1.33),"trim")
    m.box((0,.385,front-.34),(5.16,.19,.66),"trim")


def gallery(m,side):
    label = "EastTrainingGallery" if side>0 else "WestTrainingGallery"
    inner,outer = side*COURT_HALF,side*17.2
    low,high = sorted((inner,outer))
    end = HALL[1]
    eave,ridge = 8.08,11.10
    floor(m,label+"Paving",(low,FRONT,high,end),.22,"paving")
    gable(m,label+"ClayRoof",(low,FRONT,high,end-.36),eave,ridge,.36,wall_material="gable")
    nodes = (.53,4.62,8.72,12.82,16.92,20.46)
    windows = [Opening((a+b)/2,2.28,1.72,3.60,.48) for a,b in zip(nodes,nodes[1:])]
    a,b = (outer,FRONT),(outer,end)
    if side<0:
        a,b = b,a
    outer_miters = (.53,0) if side>0 else (0,.53)
    front_miters = (0,.49) if side>0 else (.49,0)
    wall(m,label+"OuterWall",a,b,.22,eave-.26,.49,"wall",windows,outer_miters)
    wall(m,label+"FrontWall",(low,FRONT),(high,FRONT),.22,eave-.26,.53,"wall",
         [Opening((high-low)/2,2.27,1.73,3.61,.49)],front_miters)
    m.feature(label+"StonePiersAndLongBeam")
    m.beam((inner,7.56,FRONT),(inner,7.56,end),.43,"timber",.56)
    m.beam(((low+high)/2,ridge+.035,FRONT-.36),((low+high)/2,ridge+.035,end),.22,"roof",.19)
    for i,distance in enumerate(nodes):
        z = FRONT+distance
        m.box((inner,3.63,z),(.68,6.82,.70),"trim")
        for y,size in ((.53,1.02),(7.10,.89)):
            m.box((inner,y,z),(size,.42,size),"trim")
        m.beam((inner,7.76,z),(outer,7.76,z),.33,"timber",.40)
        if i>0:
            m.beam((inner,5.91,z-.19),(inner,7.40,z-1.38),.32,"wood",.33)
        if i<len(nodes)-1:
            m.beam((inner,5.91,z+.19),(inner,7.40,z+1.38),.32,"wood",.33)


def gate(m):
    width = COURT_HALF*2
    wall(m,"PracticeYardGate",(-COURT_HALF,FRONT),(COURT_HALF,FRONT),.13,6.53,.95,"wall",
         [Opening(width/2,.13,5.9,5.39,1.64,None,.25),
          *[Opening(width/2+x,1.93,1.57,2.82,.42) for x in (-8.83,-6.15,6.15,8.83)]])
    m.feature("GateCopingAndJambs")
    m.box((0,6.58,FRONT+.41),(23.57,.42,1.12),"trim")
    for x in (-4.51,4.51):
        m.box((x,3.33,FRONT+.06),(1.12,6.66,.84),"wall")
        m.box((x,6.67,FRONT+.08),(1.50,.36,1.15),"trim")
        m.box((x,.27,FRONT+.08),(1.45,.54,1.11),"trim")
    m.landmark("MountedCrossedTrainingSwords")
    m.box((0,6.46,FRONT-.092),(4.29,.72,.19),"trim")
    for direction,z in ((-1,FRONT-.28),(1,FRONT-.53)):
        x = direction*1.60
        m.box((x,6.47,(z+FRONT)/2),(.70,.36,FRONT-z),"metal")
        sword(m,x,6.61,z,4.25,direction*.73)
    m.feature("GateHangingColors")
    for x in (-4.51,4.51):
        m.beam((x-.58,5.94,FRONT-.52),(x+.58,5.94,FRONT-.52),.10,"wood")
        for offset in (-.38,.38):
            m.beam((x+offset,5.94,FRONT-.50),(x+offset,5.94,FRONT-.22),.088,"metal")
        outline(m,[(x-.48,5.95),(x+.48,5.95),(x+.48,2.69),(x,3.10),(x-.48,2.69)],
                FRONT-.54,.053,"red")
    m.markers["Entrance"]=(0,.13,FRONT)


def drill_equipment(m):
    floor(m,"EnclosedPracticeYard",(-COURT_HALF,FRONT,COURT_HALF,HALL[1]),.13,"yard")
    m.feature("PaddedTrainingDummies")
    for x,z in ((-6.41,-6.13),(0,-2.91),(6.41,-6.13)):
        m.box((x,.31,z),(1.22,.36,1.05),"timber")
        m.cylinder((x,1.51,z),.14,2.09,"wood",n=12)
        m.lathe(x,1.36,z,[(.34,0),(.44,.20),(.44,.62),(.33,.91),(.42,1.10),(.34,1.37)],"canvas",n=16)
        m.cylinder((x,2.35,z),.11,1.78,"wood",n=12,axis=(1,0,0))
        m.ellipsoid((x,2.97,z),(.25,.29,.25),"ivory",n=14,rings=8)


def build(m):
    high_hall(m)
    for side in (-1,1):
        gallery(m,side)
    drill_equipment(m)
    gate(m)


def verify(objects):
    shells = [o.data for o in objects if any(s in o.name for s in ("GalleryClayRoof", "InstructionHallClayRoof"))]
    for mesh in shells:
        tree = BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
        for x,z in ((0,-5),(-6.5,-6.3),(6.5,-6.3)):
            assert tree.ray_cast((x,z,26),(0,0,-1),26)[0] is None
    facade = next(o.data for o in objects if o.name=="PracticeYardGate_Warrior_GreyAshlar")
    tree = BVHTree.FromPolygons([v.co for v in facade.vertices],[p.vertices[:] for p in facade.polygons])
    assert tree.ray_cast((0,-19,1.6),(0,1,0),7)[0] is None
    return {"practice_yard_open_sky":True,"gate_clear_width_m":5.9,"training_dummies":3}
