"""A guardhouse with a connected rear watchtower and a working lookout deck."""

from mathutils.bvhtree import BVHTree

from components import Opening, floor, hip, rectangle_walls, slab
from landmarks import outline


PALETTE = {
    "wall":"Guardhouse_BlueGreyStone", "trim":"Guardhouse_WeatheredQuoins", "roof":"Guardhouse_DenimGlaze",
    "shield":"Guardhouse_IndigoEnamel", "wood":"Guardhouse_HoneyOak",
}
MATERIALS = {
    "Guardhouse_BlueGreyStone":dict(source="Fieldstone",color="626c77",roughness=.80,relief=.24),
    "Guardhouse_WeatheredQuoins":dict(source="Limestone",color="aaa79c",relief=.19),
    "Guardhouse_IndigoEnamel":dict(source="Plaster",color="345b9e",roughness=.56,relief=.07),
    "Guardhouse_HoneyOak":dict(source="Oak",color="ab7c4f",roughness=.77,relief=.20),
}

HALL = (-9.30,-14.25,9.30,12.20)
TOWER = (1.70,12.20,9.30,19.80)
DECK=.24
CAMERA_DIRECTIONS = {"three_quarter":(.63,-1,.59),"rear":(.64,1,.65)}


def shield(m,front):
    m.feature("ShieldStandoffs")
    for x in (-.96,.96):
        m.box((x,11.62,front-.14),(.45,.54,.42),"metal")
    contour=[(-2.33,2.15),(-1.24,2.03),(0,2.22),(1.24,2.03),(2.33,2.15),
             (2.25,.30),(1.95,-.92),(1.50,-1.71),(.88,-2.40),(0,-3.04),
             (-.88,-2.40),(-1.50,-1.71),(-1.95,-.92),(-2.25,.30)]
    m.landmark("MountedIndigoShield")
    outline(m,[(x,12.42+y) for x,y in contour],front-.38,.26,"steel")
    outline(m,[(x*.905,12.40+y*.915) for x,y in contour],front-.538,.14,"shield")


def main_house(m):
    left,front,right,back = HALL
    w,d = right-left,back-front
    floor(m,"GuardHallFlagstones",HALL,DECK,"paving")
    door=Opening(w/2,DECK,5.48,6.13,1.38,"door",.35)
    front_apertures=[Opening(c,1.75,1.20,3.48,.38,"warm_glass",.18) for c in (2.51,4.71,13.89,16.09)]
    side=[Opening((i+.5)*d/6,2.82,1.20,3.18,.31,"warm_glass",.16) for i in range(6)]
    link=Opening(right-(TOWER[0]+TOWER[2])/2,DECK,2.67,4.34,.60,None,0)
    rectangle_walls(m,"GuardHallMasonry",HALL,DECK,10.97,.64,openings={
        "Front":[door,*front_apertures],"East":side,"West":side,
        "Rear":[link,Opening(10.91,2.70,1.80,3.34,.47,"warm_glass"),
                Opening(15.34,2.70,1.80,3.34,.47,"warm_glass")]})
    eave,ridge,extension = 11.23,17.13,.50
    low=eave-(ridge-eave)*extension/right
    m.feature("TimberGableFrames")
    for z in (front,back):
        infill=[(left,eave-.26,z),(0,ridge-.26,z),(right,eave-.26,z)]
        m.face(infill if z==front else list(reversed(infill)),"wall")
    for sign in (-1,1):
        slab(m,"GuardHallRoof",[(sign*(right+extension),low,front-extension),
             (0,ridge,front-extension),(0,ridge,back),(sign*(right+extension),low,back)],.26,borders={0,2,3})
    m.feature("GuardHallRidgeAndRafters")
    m.beam((0,ridge+.04,front-extension),(0,ridge+.04,back),.24,"roof",.20)
    for x in (left,right):
        m.beam((x,10.78,front),(x,10.78,back),.35,"timber",.34)
        for i in range(10):
            z=front+d*(i+.5)/10
            m.beam((x*.994,9.91,z),(x*1.028,10.88,z),.24,"wood",.24)
    m.feature("GuardHallJambsAndQuoins")
    for x in (left+.18,-3.42,3.42,right-.18):
        m.box((x,5.12,front-.05),(.64,10.24,.60),"trim")
        m.box((x,.32,front-.06),(.91,.64,.84),"trim")
    for side in (-1,1):
        m.beam((side*8.78,11.23,front-.095),(0,16.66,front-.095),.28,"wood",.32)
        m.beam((side*2.65,14.74,front-.126),(0,15.78,front-.126),.22,"timber",.26)
    m.box((0,15.80,front-.12),(.31,1.96,.28),"wood")
    floor(m,"GuardhouseDoorStep",(-3.15,front-.76,3.15,front),DECK,"trim")
    m.markers["Entrance"]=(0,DECK,front-.62)
    shield(m,front)


def tower(m):
    left,front,right,back=TOWER
    x,z=(left+right)/2,(front+back)/2
    w=right-left
    start,underside=17.77,22.47
    floor(m,"WatchtowerFoundation",TOWER,DECK,"trim")
    slit = [Opening(w*.5,y,.82,1.80,.16,"warm_glass",.15) for y in (2.10,7.58,13.07)]
    opening=Opening(w*.5,DECK,2.67,4.34,.60,None,0)
    rectangle_walls(m,"WatchTowerStone",TOWER,DECK,start-.28,.56,openings={
        "Front":[opening,Opening(w*.5,15.04,.82,1.77,.16,"warm_glass",.15)],
        "East":slit,"West":slit,"Rear":slit})
    m.feature("WatchTowerQuoins")
    for tx in (left,right):
        for tz in (front,back):
            m.box((tx,8.92,tz),(.44,17.40,.44),"trim")
    layout=(left-.26,front-.26,right+.26,back+.26)
    hatch=(left+1.15,back-2.68,left+3.02,back-.92)
    for bounds in ((layout[0],layout[1],layout[2],hatch[1]),(layout[0],hatch[3],layout[2],layout[3]),
                   (layout[0],hatch[1],hatch[0],hatch[3]),(hatch[2],hatch[1],layout[2],hatch[3])):
        floor(m,"WatchPlatformTimberFloor",bounds,start,"wood",bottom=start-.31)
    m.feature("LookoutHatchLadder")
    ladder_z=hatch[3]-.31
    for tx in (hatch[0]+.26,hatch[2]-.26):
        m.beam((tx,DECK,ladder_z),(tx,start+.74,ladder_z),.13,"timber")
    for i in range(46):
        y=.55+i*.382
        m.beam((hatch[0]+.25,y,ladder_z),(hatch[2]-.25,y,ladder_z),.10,"wood")
    corners=[(left-.12,front-.12),(right+.12,front-.12),(right+.12,back+.12),(left-.12,back+.12)]
    for a,b in zip(corners,corners[1:]+corners[:1]):
        def p(t,h):
            return a[0]+(b[0]-a[0])*t,h,a[1]+(b[1]-a[1])*t
        m.feature("WatchPlatformSolidGuardboards")
        m.beam(p(.025,start+.68),p(.975,start+.68),.24,"wood",1.33)
        m.beam(p(.024,start+1.39),p(.976,start+1.39),.33,"timber",.17)
        m.feature("LookoutPostsBracesAndHeader")
        for t in (0,.50):
            m.box(p(t,(start+underside)*.5),(.39,underside-start,.39),"wood")
        m.beam(p(.004,underside-.15),p(.996,underside-.15),.39,"timber",.35)
        for t,direction in ((0,1),(.5,-1),(.5,1),(1,-1)):
            m.beam(p(t,underside-1.60),p(t+direction*.17,underside-.27),.25,"wood",.27)
    hip(m,"WatchTowerHip",(left-.12,front-.12,right+.12,back+.12),underside+.25,25.61,.43)
    m.feature("LookoutRoofFinial")
    m.cylinder((x,25.71,z),.23,.44,"trim",n=8,top=.13)


def build(m):
    main_house(m)
    tower(m)


def verify(objects):
    for label in ("GuardHallMasonryRear","WatchTowerStoneFront"):
        wall=next(o.data for o in objects if o.name==label+"_Guardhouse_BlueGreyStone")
        tree=BVHTree.FromPolygons([v.co for v in wall.vertices],[p.vertices[:] for p in wall.polygons])
        assert tree.ray_cast((5.5,10,2),(0,1,0),5)[0] is None
    boards=next(o.data for o in objects if o.name=="WatchPlatformTimberFloor_Guardhouse_HoneyOak")
    tree=BVHTree.FromPolygons([v.co for v in boards.vertices],[p.vertices[:] for p in boards.polygons])
    assert tree.ray_cast((3.75,17.9,18.5),(0,0,-1),2)[0] is None
    return {"watchtower_connected":True,"lookout_deck_m":17.77,"open_ladder_hatch":True}
