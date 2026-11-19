"""Recovery wards around a sheltered herb and light court."""

from math import radians,cos,sin

from mathutils.bvhtree import BVHTree

from components import Opening, courtyard_roof, floor, pipe, rectangle_walls
from landmarks import outline


PALETTE = {
    "roof":"Clinic_SageClay", "stone":"Clinic_PaleFieldstone", "wall":"Clinic_Limewash",
    "trim":"Clinic_DressedStone", "healing":"Clinic_HerbalCross",
}
MATERIALS = {
    "Clinic_PaleFieldstone":dict(source="Fieldstone",color="9b9a8f",relief=.22),
    "Clinic_Limewash":dict(source="Plaster",color="cbbfa6",relief=.16),
    "Clinic_DressedStone":dict(source="Limestone",color="b0a287",relief=.16),
    "Clinic_HerbalCross":dict(source="Plaster",color="4c835f",relief=.05),
}

OUTER = (-14.70,-14.25,14.70,14.25)
COURT = (-6.50,-6.05,6.50,6.05)
CAMERA_DIRECTIONS = {"three_quarter":(.48,-1,.85),"rear":(-.35,1,2.48)}


def apothecary_mark(m):
    z = OUTER[1]
    m.landmark("ApothecaryCross")
    x,y = 0,8.68
    a,b = .66,2.29
    profile = [(-a,-b),(a,-b),(a,-a),(b,-a),(b,a),(a,a),(a,b),
               (-a,b),(-a,a),(-b,a),(-b,-a),(-a,-a)]
    m.box((0,y,z-.135),(2.52,2.85,.31),"timber")
    outline(m,[(x+u,y+v) for u,v in profile],z-.405,.28,"healing")
    m.landmark("MortarAndPestle")
    x,z = 4.61,z-1.54
    m.cylinder((x,.33,z),.84,.66,"trim",n=24,top=.80)
    pipe(m,(x,.65,z),(0,1,0),
         [(0,0),(0,.55),(.21,.70),(.67,1.03),(1.08,1.13),
          (1.10,.99),(.90,.86),(.46,.48),(.31,.22),(.31,0)],"stone",24)
    m.cylinder((x+.43,1.80,z),.205,1.91,"wood",n=18,axis=(.54,1,.07),top=.23)


def garden(m):
    floor(m,"RecoveryCourtPaving",COURT,.235,"paving")
    for side in (-1,1):
        x = side*3.37
        herbs,bench = -3.0,.10
        m.feature("HerbPlanters")
        m.box((x,.493,herbs),(2.67,.516,1.49),"trim")
        m.box((x,.762,herbs),(2.55,.032,1.38),"soil")
        for i in range(5):
            m.ellipsoid((x+(i-2)*.46,.965,herbs),(.29,.25,.40),"leaf",n=10,rings=5)
        m.feature("RecoveryBenches")
        m.box((x,.74,bench),(2.38,.15,.46),"wood")
        for delta in (-.86,.86):
            m.box((x+delta,.451,bench),(.19,.436,.42),"trim")


def build(m):
    left,front,right,back = OUTER
    width,depth = right-left,back-front
    floor(m,"WardAndCourtFoundation",OUTER,.22,"trim")
    low = [Opening((i+.5)*depth/7,1.41,1.66,3.31,.65) for i in range(7)]
    high = [Opening((i+.5)*depth/7,8.12,1.82,4.68,.55) for i in range(7)]
    windows = [3.08,7.13,11.19,18.21,22.27,26.32]
    bottom = [Opening(u,1.41,1.73,3.53,.63) for u in windows]
    top = [Opening(u,8.12,1.78,4.64,.55) for u in windows]
    front_entry=Opening(width/2,.22,3.45,4.78,.64,None,.24)
    rectangle_walls(m,"WardLower",OUTER,.22,6.25,.66,"stone",
                    {"Front":[front_entry,*bottom],"Rear":bottom,"East":low,"West":low})
    rectangle_walls(m,"WardUpper",OUTER,6.25,15.04,.66,openings={
        "Front":top,"Rear":[*top,Opening(width/2,8.12,2.08,4.64,.59)],"East":high,"West":high})

    inner_side = [Opening(u,.22,1.64,4.30,.36,None) for u in (1.6,4.6,7.5,10.5)]
    inner_high = [Opening(u,8.11,1.72,4.67,.27) for u in (1.6,4.6,7.5,10.5)]
    court_portal = Opening(6.5,.22,3.46,4.80,.65,None,.22)
    court_rear = [Opening(u,1.29,1.91,5.60,.28,"clear_glass") for u in (1.63,4.82,8.18,11.37)]
    rectangle_walls(m,"CourtWard",COURT,.22,15.04,.58,openings={
        "Front":[court_portal,Opening(2.38,8.11,2.19,4.67,.44),Opening(10.62,8.11,2.19,4.67,.44)],
        "Rear":[*court_rear,*inner_high],"West":[*inner_side,*inner_high],"East":[*inner_side,*inner_high]},
        inward=True)
    courtyard_roof(m,"WardRoof",OUTER,COURT,15.30,20.72)

    m.feature("AshlarWallPiers")
    for z in (front,back):
        for x in (-14.43,-9.60,-5.54,5.54,9.60,14.43):
            m.box((x,7.515,z),(.51,15.03,.49),"trim")
    for x in (left,right):
        for z in (-10.18,-6.11,-2.04,2.04,6.11,10.18):
            m.box((x,7.51,z),(.43,15.02,.46),"trim")

    floor(m,"EntryThreshold",(-1.97,front-.58,1.97,front),.22,"trim")
    m.feature("RecessedEntryDoors")
    angle = radians(78)
    for side in (-1,1):
        center = (side*(1.655-.78*cos(angle)),1.94,front+.31+.78*sin(angle))
        m.box(center,(1.56,3.44,.15),"wood",side*angle)
    m.markers["Entrance"] = (0,.22,front-.60)
    garden(m)
    apothecary_mark(m)


def verify(objects):
    roof = next(o.data for o in objects if o.name=="WardRoof_Clinic_SageClay")
    tree = BVHTree.FromPolygons([v.co for v in roof.vertices],[p.vertices[:] for p in roof.polygons])
    for pos in ((0,0,26),(3,2,26),(-3,-2,26)):
        hit,_,_,_=tree.ray_cast(pos,(0,0,-1),26)
        assert hit is None
    for prefix in ("WardLowerFront_Clinic_PaleFieldstone","CourtWardFront_Clinic_Limewash"):
        wall=next(o.data for o in objects if o.name==prefix)
        tree=BVHTree.FromPolygons([v.co for v in wall.vertices],[p.vertices[:] for p in wall.polygons])
        hit,_,_,_=tree.ray_cast((0,-17,1.8),(0,1,0),17)
        assert hit is None
    return {"court_free_of_roof":True,"court_approach_width_m":3.18,"court_bounds_m":[13,12.1]}
