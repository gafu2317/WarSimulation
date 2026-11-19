"""An inn with a public veranda and a large supported ale tankard."""

from math import cos, pi, sin

from mathutils.bvhtree import BVHTree

from components import Opening, floor, pipe, rectangle_walls, slab


PALETTE = {
    "roof":"Tavern_OchreTerracotta", "wall":"Tavern_HoneyPlaster", "stone":"Tavern_BrownRubble",
    "trim":"Tavern_DressedBuffStone", "timber":"Tavern_DarkChestnut", "wood":"Tavern_AgedOak",
    "ivory":"Tavern_AleFoam", "bronze":"Tavern_WornCopperBands",
}
MATERIALS = {
    "Tavern_HoneyPlaster":dict(source="Plaster",color="c4a46e",relief=.22),
    "Tavern_BrownRubble":dict(source="Fieldstone",color="807667",relief=.25),
    "Tavern_DressedBuffStone":dict(source="Limestone",color="b29d78",relief=.18),
    "Tavern_DarkChestnut":dict(source="DarkTimber",color="6c4832",relief=.22),
    "Tavern_AgedOak":dict(source="Oak",color="a47549",relief=.21),
    "Tavern_AleFoam":dict(source="Linen",color="e1c59a",relief=.17),
    "Tavern_WornCopperBands":dict(source="Copper",color="a47443",metallic=.44,roughness=.42,relief=.08),
}

BOUNDS=(-16.25,-10.65,16.25,11.85)
EAVE,RIDGE=15.33,22.05
BASE = .25
CAMERA_DIRECTIONS = {"three_quarter":(.34,-1,.55)}


def roof(m):
    left,front,right,back=BOUNDS
    mid=(front+back)/2
    pitch=(RIDGE-EAVE)/(mid-front)
    offset=.48
    y0=EAVE-pitch*offset
    face=front-offset
    span,high=5.08,19.55
    valley=front+(high-EAVE)/pitch
    for side in (-1,1):
        edge=side*(right+offset)
        slab(m,"MainTavernRoof",[(edge,y0,face),(side*span,y0,face),(0,high,valley),
             (0,RIDGE,mid),(edge,RIDGE,mid)],.26,borders={0,4})
        slab(m,"JoinedEntryCrossGableRoof",[(side*span,y0,face),(0,high,valley),
             (0,high,front-.90),(side*span,y0,front-.90)],.26,borders={2,3})
        m.feature("RoofValleyFlashings")
        m.beam((side*span,y0+.035,face),(0,high+.035,valley),.12,"bronze",.10)
    slab(m,"MainTavernRoof",[(left-offset,RIDGE,mid),(right+offset,RIDGE,mid),
         (right+offset,y0,back+offset),(left-offset,y0,back+offset)],.26,borders={1,2,3})
    m.feature("GablesAndRidgeCaps")
    for x in (left,right):
        p=[(x,EAVE-.26,front),(x,EAVE-.26,back),(x,RIDGE-.26,mid)]
        m.face(p if x==left else list(reversed(p)),"wall")
        m.beam((x,15.00,front+.30),(x,15.00,back-.30),.28,"timber",.34)
        m.box((x,18.29,mid),(.26,5.99,.34),"timber")
    w=span*(high-EAVE)/(high-y0)
    m.face([(-w,EAVE-.26,front),(0,high-.26,front),(w,EAVE-.26,front)],"wall")
    m.beam((left-offset,RIDGE+.036,mid),(right+offset,RIDGE+.036,mid),.28,"roof",.19)
    m.beam((0,high+.032,front-.90),(0,high+.032,valley),.25,"roof",.17)
    m.feature("CrossGableFascia")
    for x in (-span,span):
        m.beam((x,y0-.12,front-.90),(0,high-.13,front-.90),.20,"timber",.29)


def shell(m):
    left,front,right,back=BOUNDS
    w=right-left
    floor(m,"InnFloor",BOUNDS,BASE,"paving",bottom=.12)
    floor(m,"InnStoneFootcourse",BOUNDS,.12,"stone")
    low=[Opening(w/2,BASE,3.94,5.37,.88,"door",.24)]
    for c in (4.14,11.05,21.45,28.36):
        low.append(Opening(c,BASE,2.25,4.30,.74,None,.12))
    high=[Opening(c,8.84,2.46,5.03,.90,"warm_glass",.20) for c in (3.57,8.28,12.58,19.92,24.22,28.93)]
    sides=[Opening(c,y,2.10,h,.62,"warm_glass",.15) for c in (3.30,8.23,14.27,19.2)
           for y,h in ((1.75,3.81),(8.52,4.86))]
    rear=[Opening(c,y,2.22,h,.61,"warm_glass",.16) for c in (3.56,8.56,13.52,18.97,23.93,28.94)
          for y,h in ((1.78,3.86),(8.74,4.88))]
    rectangle_walls(m,"InnPlasteredFrame",BOUNDS,BASE,EAVE-.26,.58,openings={
        "Front":[*low,*high],"Rear":rear,"West":sides,"East":sides})
    m.feature("InnOakFraming")
    for z in (front,back):
        for y in (7.78,14.93):
            m.beam((left+.08,y,z),(right-.08,y,z),.28,"timber",.32)
        for x in (-15.93,-10.34,-5.85,5.85,10.34,15.93):
            m.box((x,10.96,z),(.34,7.88,.32),"timber")
    for x in (left,right):
        for y in (7.76,14.94):
            m.beam((x,y,front+.10),(x,y,back-.10),.29,"timber",.32)
        for z in (front+.38,-4.83,.61,6.22,back-.38):
            m.box((x,7.61,z),(.36,14.35,.33),"timber")
    m.markers["Entrance"]=(0,BASE,front-4.79)


def veranda(m):
    left,front,right,_=BOUNDS
    entry=front-4.98
    floor(m,"AttachedVerandaFloor",(left,entry,right,front),BASE,"wood")
    slab(m,"VerandaSingleSlopeRoof",[(left-.30,6.22,entry-.32),(right+.30,6.22,entry-.32),
          (right+.30,8.31,front+.10),(left-.30,8.31,front+.10)],.26,borders={0,1,3})
    m.feature("VerandaPostsBracesAndHeader")
    m.beam((left,5.83,entry+.07),(right,5.83,entry+.07),.43,"timber",.48)
    posts=(-15.97,-10.64,-5.24,-2.76,2.76,5.24,10.64,15.97)
    for i,x in enumerate(posts):
        m.box((x,3.47,entry+.17),(.49,4.89,.52),"wood")
        m.box((x,.58,entry+.17),(.79,1.16,.81),"trim")
        for j in (i-1,i+1):
            if 0<=j<len(posts):
                inward=1 if j>i else -1
                m.beam((x,4.53,entry+.17),(x+inward*.87,5.73,entry+.16),.29,"wood",.30)
    m.feature("VerandaLowRails")
    for a,b in zip(posts,posts[1:]):
        if a<0<b:
            continue
        for y in (.51,1.23):
            m.beam((a+.28,y,entry+.16),(b-.28,y,entry+.16),.12,"timber",.17)
        for i in range(1,6):
            m.box((a+(b-a)*i/6,.87,entry+.16),(.085,.67,.11),"wood")
    for x in (-12.94,-7.99,7.99,12.94):
        m.feature("OpenAirTablesAndBenches")
        for dx in (-.46,0,.46):
            m.box((x+dx,1.36,front-2.63),(.43,.18,2.12),"wood")
        for dz in (-.71,.71):
            m.box((x,.772,front-2.63+dz),(1.13,.997,.18),"timber")
        for dx in (-.99,.99):
            m.box((x+dx,.86,front-2.63),(.34,.16,1.94),"wood")
            for dz in (-.71,.71):
                m.box((x+dx,.51,front-2.63+dz),(.26,.53,.21),"timber")
        m.feature("AleCasksUnderEaves")
        for mat,p in (("wood",[(-.48,0),(-.48,.66),(-.34,.73),(0,.79),(.34,.73),(.48,.66),(.48,0)]),
                      ("metal",[(-.33,.737),(-.21,.786),(-.08,.791)]),
                      ("metal",[(.08,.791),(.21,.786),(.33,.737)])):
            pipe(m,(x,1.14,front-.74),(0,0,1),p,mat,20)
        m.box((x,.38,front-.74),(1.12,.26,.9),"timber")


def tankard(m):
    z=BOUNDS[1]
    m.feature("TankardBracketAndStraps")
    m.box((1.75,14.63,z-.12),(.52,3.71,.39),"timber")
    m.beam((1.75,16.16,z),(0,16.16,z-1.31),.36,"wood",.40)
    m.beam((1.75,14.82,z-.12),(0,16.13,z-1.22),.31,"timber",.33)
    m.beam((0,16.17,z-1.31),(0,14.57,z-1.31),.12,"metal",.17)
    for x in (-1.46,1.46):
        m.beam((x,13.98,z-1.31),(x*.23,14.57,z-1.31),.13,"metal",.14)
    m.beam((-.34,14.57,z-1.31),(.34,14.57,z-1.31),.13,"metal",.14)
    m.landmark("CopperBandedAleMug")
    center=(0,10.68,z-1.31)
    pipe(m,center,(0,1,0),[(0,0),(0,1.94),(.22,2.02),(.52,1.96),(2.89,1.56),(3.15,1.59),
                           (3.33,1.55),(3.33,1.42),(3.07,1.35),(2.7,0)],"wood",36)
    for a,r in ((.48,1.969),(3.02,1.605)):
        pipe(m,center,(0,1,0),[(a-.17,r+.054),(a+.15,r+.033)],"bronze",36)
    for i in range(16):
        def p(j):
            a=-pi/2+pi*j/16
            return 1.46+1.25*cos(a),12.30+1.13*sin(a),z-1.30
        m.beam(p(i),p(i+1),.29,"bronze",.28)
    m.cylinder((0,14.02,z-1.31),1.53,.28,"ivory",n=32,top=1.46)
    for dx,dz in ((-.82,-.45),(-.17,-.74),(.58,-.53),(-.39,.34),(.48,.36)):
        m.ellipsoid((dx,14.16,z-1.31+dz),(.68,.32,.58),"ivory",n=14,rings=8)


def build(m):
    shell(m)
    roof(m)
    veranda(m)
    tankard(m)


def verify(objects):
    facade=next(o.data for o in objects if o.name=="InnPlasteredFrameFront_Tavern_HoneyPlaster")
    tree=BVHTree.FromPolygons([v.co for v in facade.vertices],[p.vertices[:] for p in facade.polygons])
    for x in (-12.11,-5.20,5.20,12.11):
        assert tree.ray_cast((x,-15,1.8),(0,1,0),6)[0] is None
    return {"veranda_accessible_from_inn":True,"courtyard_tables":4}
