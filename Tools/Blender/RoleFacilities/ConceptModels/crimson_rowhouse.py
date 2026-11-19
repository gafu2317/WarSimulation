"""An open-front theater with built-in stage, curtains and an attached foyer."""

from math import cos, pi, sin

from mathutils.bvhtree import BVHTree

from components import Opening, floor, gable, rectangle_walls, slab, wall
from landmarks import outline


PALETTE = {
    "roof":"Theater_OxbloodCeramic", "wall":"Theater_BurgundyLimewash", "stone":"Theater_CharcoalFieldstone",
    "trim":"Theater_DressedIvoryStone", "stage":"Theater_HoneyStagePlanks", "fabric":"Theater_RubyWool",
    "bronze":"Theater_GiltLyre",
}
MATERIALS = {
    "Theater_BurgundyLimewash":dict(source="Plaster",color="9c5b59",relief=.18),
    "Theater_CharcoalFieldstone":dict(source="Fieldstone",color="8d827a",relief=.23),
    "Theater_DressedIvoryStone":dict(source="Limestone",color="bca892",relief=.17),
    "Theater_HoneyStagePlanks":dict(source="Oak",color="b98a51",roughness=.72,relief=.20,emittance=.047),
    "Theater_RubyWool":dict(source="Canvas_Red",color="9d3545",roughness=.89,relief=.22),
    "Theater_GiltLyre":dict(source="Brass",color="bc974f",metallic=.44,roughness=.36,relief=.08),
}

HALL=(-12.60,-12.32,12.60,12.71)
WING=(-19.80,-10.92,-12.60,9.58)
STAGE=1.06
CAMERA_DIRECTIONS = {"three_quarter":(-.29,-1,.53),"rear":(.57,1,.66)}


def hall_and_stage(m):
    left,front,right,back=HALL
    width,depth=right-left,back-front
    floor(m,"TheaterGroundSlab",HALL,.23,"paving")
    opening=Opening(width/2,STAGE,15.25,11.03,2.16,None,.30)
    front_low=[Opening(c,1.99,1.91,4.21,.82,"warm_glass") for c in (2.52,22.67)]
    front_high=[Opening(c,8.00,1.93,4.87,.91,"warm_glass") for c in (2.52,22.67)]
    wall(m,"StoneProscenium",(left,front),(right,front),.23,15.26,.64,"stone",[opening,*front_low,*front_high],.64)
    sides=[Opening((i+.5)*depth/5,y,2.12,h,.71,"warm_glass",.17) for i in range(5)
           for y,h in ((1.75,3.86),(8.46,4.99))]
    rear=[Opening(c,y,2.12,3.93,.65,"warm_glass") for c in (3.16,9.45,15.75,22.05) for y in (2.04,8.74)]
    side_gate=Opening(back-(WING[1]+WING[3])/2,.23,2.60,4.29,.48,None,.0)
    wall(m,"EastTheaterWall",(right,front),(right,back),.23,15.26,.64,"wall",sides,.64)
    wall(m,"RearTheaterWall",(right,back),(left,back),.23,15.26,.64,"wall",rear,.64)
    wall(m,"WestTheaterWall",(left,back),(left,front),.23,15.26,.64,"wall",[side_gate],.64)
    gable(m,"TheaterDeepClayGable",HALL,15.52,24.28,.54)
    m.feature("ProsceniumStoneFrame")
    for x in (-12.24,-8.46,8.46,12.24):
        m.box((x,7.61,front-.07),(.64,15.22,.59),"trim")
        for y,w in ((.49,.88),(7.32,.79),(14.93,1.01)):
            m.box((x,y,front-.08),(w,.44,.70),"trim")
    for z in (front,back):
        m.beam((left,14.92,z),(right,14.92,z),.38,"timber",.30)
    floor(m,"RaisedWoodenStage",(-7.62,front+.64,7.62,5.85),STAGE,"stage",bottom=.23)
    m.feature("ThreeTreadStageApproach")
    landing=front
    for i in range(4):
        m.box((0,(i+1)*STAGE/8,landing-(3.5-i)*.38),(3.57,(i+1)*STAGE/4,.38),"trim")
    m.feature("StageBackdropAndScenicTimbers")
    m.box((0,5.87,5.78),(15.36,9.60,.34),"wood")
    for x in (-7.29,-3.63,0,3.63,7.29):
        m.box((x,5.74,5.53),(.35,9.35,.32),"timber")
    for y in (1.28,5.79,10.49):
        m.beam((-7.4,y,5.52),(7.4,y,5.52),.29,"timber",.44)
    m.markers["Entrance"]=(0,0,front-1.52)


def curtains(m):
    front=HALL[1]
    profile=[(0,1.13),(.14,.93),(.28,.62),(.48,1.35),(.71,2.29),(1,3.48)]
    def fold(u,t,s,inset=0):
        a,b=next((a,b) for a,b in zip(profile,profile[1:]) if a[0]<=t<=b[0])
        q=(t-a[0])/(b[0]-a[0])
        width=a[1]+(b[1]-a[1])*(.5-.5*cos(q*pi))
        return s*(7.37-u*width),1.08+9.94*t,front+1.12+(.13+.045*sin(t*8))*cos(u*28+.54*t)+inset
    m.feature("StageCurtainRail")
    m.beam((-7.9,11.02,front+1.16),(7.9,11.02,front+1.16),.19,"timber")
    for s in (-1,1):
        m.feature("StageCurtainLeft" if s<0 else "StageCurtainRight")
        for i in range(28):
            for j in range(16):
                a,b=i/28,(i+1)/28
                c,d=j/16,(j+1)/16
                m.face([fold(c,a,s),fold(d,a,s),fold(d,b,s),fold(c,b,s)],"fabric",
                       [(c*1.6,a*4),(d*1.6,a*4),(d*1.6,b*4),(c*1.6,b*4)],smooth=True)
        m.feature("GildedCurtainTieBacks")
        for i in range(12):
            c,d=i/12,(i+1)/12
            m.face([fold(c,.256,s,-.052),fold(d,.256,s,-.052),
                    fold(d,.297,s,-.052),fold(c,.297,s,-.052)],"bronze")


def foyer(m):
    left,front,right,back=WING
    width,depth=right-left,back-front
    floor(m,"AttachedFoyerGround",WING,.23,"paving")
    front_low=[Opening(width/2,.23,2.62,4.33,.59,"door",.24)]
    front_high=[Opening(c,6.35,1.25,3.03,.71,None,.13) for c in (1.42,3.60,5.78)]
    wall(m,"FoyerFrontWall",(left,front),(right,front),.23,10.40,.54,"wall",[*front_low,*front_high],(.54,0))
    west=[Opening(c,y,2.01,3.27,.49,"warm_glass") for c in (3.41,10.25,17.06) for y in (1.56,6.33)]
    wall(m,"FoyerWestWall",(left,back),(left,front),.23,10.40,.54,"wall",west,.54)
    wall(m,"FoyerRearWall",(right,back),(left,back),.23,10.40,.54,"wall",[Opening(c,2.16,1.56,3.91,.56) for c in (2.05,5.15)],(0,.54))
    floor(m,"AttachedFoyerBalconyFloor",(left+.20,front+.19,right,back-.20),6.36,"wood",bottom=6.03)
    m.feature("FoyerBalconyStoneSill")
    m.box(((left+right)/2,6.32,front-.09),(width-.32,.29,.76),"trim")
    m.feature("FoyerBalconyRail")
    m.beam((left+.38,7.19,front-.12),(right-.39,7.19,front-.12),.18,"timber",.16)
    for i in range(13):
        m.box((left+.51+i*(width-1.02)/12,6.83,front-.12),(.095,.70,.13),"wood")
    slab(m,"AttachedFoyerLeanTo",[(left-.34,10.49,front-.34),(left-.34,10.49,back+.34),
           (right,13.42,back+.34),(right,13.42,front-.34)],.26,borders={0,1,3})
    m.feature("FoyerSideRoofEnds")
    m.face([(left,10.40,front),(right,13.16,front),(right,10.40,front)],"wall")
    m.face([(left,10.40,back),(right,10.40,back),(right,13.16,back)],"wall")


def lyre(m):
    z=HALL[1]
    m.feature("LyreMountingPins")
    for x in (-.77,.77):
        m.box((x,16.16,z-.14),(.51,.48,.38),"metal")
    m.landmark("GildedLyreAboveStage")
    y=15.70
    side=[(.38,0),(.83,.10),(1.20,.35),(1.48,.83),(1.52,1.34),(1.40,1.85),(1.07,2.42),(.91,2.76),
          (.94,3.05),(1.14,3.19),(1.31,3.11),(1.36,2.89),(1.61,2.85),(1.84,3.09),(1.82,3.37),
          (1.57,3.60),(1.17,3.64),(.80,3.44),(.62,3.05),(.63,2.52),(.91,1.95),(1.07,1.43),
          (1.03,.91),(.80,.53),(.37,.43)]
    for s in (-1,1):
        outline(m,[(s*x*1.22,y+v*1.24) for x,v in side],z-.40,.28,"bronze")
    m.box((0,y+.21,z-.40),(1.43,.40,.29),"bronze")
    m.beam((-.91,y+3.39,z-.40),(.91,y+3.39,z-.40),.115,"bronze")
    for x in (-.62,-.31,0,.31,.62):
        m.beam((x,y+.37,z-.438),(x,y+3.55,z-.438),.050,"bronze")


def build(m):
    hall_and_stage(m)
    curtains(m)
    foyer(m)
    lyre(m)


def verify(objects):
    mesh=next(o.data for o in objects if o.name=="StoneProscenium_Theater_CharcoalFieldstone")
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
    assert tree.ray_cast((0,-15,2.15),(0,1,0),8)[0] is None
    assert tree.ray_cast((0,-15,.68),(0,1,0),8)[0] is not None
    return {"clear_open_stage_width_m":15.25,"stage_deck_height_m":STAGE,"curtains_are_separate_surfaces":2}
