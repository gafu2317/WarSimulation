"""A sheltered social court with galleries, awnings and a reflecting pool."""

from math import cos, pi, sin

from mathutils.bvhtree import BVHTree

from components import Opening, floor, gable, slab, wall
from materials import artwork
from spirit_academy import leaf_crown


PALETTE = {
    "roof":"Courtyard_CinnabarTiles", "wall":"Courtyard_OchreStone", "trim":"Courtyard_CreamSandstone",
    "fabric":"Courtyard_CopperStripedCanvas", "water":"Courtyard_StillGreenWater", "ironwork":"Courtyard_BlackBronze",
    "leaf":"Courtyard_OliveShrubs", "leaflight":"Courtyard_Herbs", "flower":"Courtyard_RustFlowers",
}
MATERIALS = {
    "Courtyard_OchreStone":dict(source="Limestone",color="bca170",relief=.22),
    "Courtyard_CreamSandstone":dict(source="Limestone",color="bcac8f",relief=.17),
    "Courtyard_StillGreenWater":dict(source="Water",color="567f74",roughness=.26,metallic=.16,relief=.18),
    "Courtyard_BlackBronze":dict(source="Iron",color="504932",roughness=.57,metallic=.37,relief=.14),
    "Courtyard_OliveShrubs":dict(source="Foliage",color="677747",roughness=.83,relief=.36),
    "Courtyard_Herbs":dict(source="Foliage",color="849359",roughness=.83,relief=.28),
    "Courtyard_RustFlowers":dict(source="Foliage",color="ba8050",roughness=.88,relief=.22),
}

W,FRONT,BACK=16.85,-15.27,15.65
INNER,LodgeFront=10.65,8.78
CAMERA_DIRECTIONS = {"three_quarter":(.20,-1,1.02),"rear":(-.44,1,1.26)}


def gallery(m,s):
    label="EastGallery" if s>0 else "WestGallery"
    x,z=s*INNER,FRONT
    outer = s*W
    l,r=sorted((x,outer))
    length=LodgeFront-z
    top=12.44
    lower=[Opening((i+.5)*length/5,.24,3.22,4.67,1.48,None,.19) for i in range(5)]
    upper=[Opening((i+.5)*length/5,7.22,2.19,3.79,.66,"warm_glass",.20) for i in range(5)]
    upper_court=upper[1:] if s<0 else upper[:-1]
    oa,ob=((outer,z),(outer,LodgeFront)) if s>0 else ((outer,LodgeFront),(outer,z))
    ia,ib=((x,LodgeFront),(x,z)) if s>0 else ((x,z),(x,LodgeFront))
    wall(m,label+"CourtArcade",ia,ib,.24,top-.26,.54,"wall",[*lower,*upper_court],(.54,0) if s<0 else (0,.54))
    exterior=[Opening(o.center,1.47,1.77,3.75,.68,"glass",.16) for o in lower]
    wall(m,label+"Exterior",oa,ob,.24,top-.26,.54,"wall",[*exterior,*upper],(.54,0) if s>0 else (0,.54))
    wall(m,label+"StreetGable",(l,z),(r,z),.24,top-.26,.54,"wall",
         [Opening((r-l)/2,y,1.78,h,.64,"warm_glass",.20) for y,h in ((1.53,3.63),(7.25,4.01))],.54)
    floor(m,label+"AttachedMezzanine",(l+.20,z+.19,r-.20,LodgeFront),6.16,"wood",bottom=5.87)
    gable(m,label+"TiledRoof",(l,z,r,LodgeFront-.39),top,16.28,.39)
    m.feature(label+"TimberFraming")
    for xx in (x,outer):
        m.beam((xx,6.09,z+.10),(xx,6.09,LodgeFront-.10),.27,"timber",.33)
        for i in range(1,5):
            zz=z+length*i/5
            m.box((xx,8.94,zz),(.23,6.24,.27),"timber")
    for zz in (-7.75,0.41,5.44):
        m.feature(label+"CoveredConversationSeats")
        inner_x=x+s*.73
        m.box((inner_x,.79,zz),(.53,.16,2.50),"wood")
        m.box((inner_x+s*.23,1.20,zz),(.16,.50,2.47),"wood")
        for dz in (-.91,.91):
            m.box((inner_x,.52,zz+dz),(.42,.56,.17),"timber")


def lodge(m):
    width=2*W
    low=[Opening(c,.24,3.02,4.69,1.26,None,.18) for c in (8.11,12.45,16.85,21.25,25.59)]
    links=[Opening(c,.24,2.45,4.24,.41,None,.15) for c in (3.10,30.60)]
    upper=[Opening(c,y,1.91,3.81,.75,"warm_glass",.17) for c in (3.13,8.08,12.46,16.85,21.23,25.61,30.57)
           for y in (6.70,12.31)]
    upper_court = [opening for opening in upper if 4<opening.center<width-4]
    eave,ridge=17.43,21.72
    wall(m,"RearLodgeCourtWall",(-W,LodgeFront),(W,LodgeFront),.24,eave-.26,.61,"wall",[*low,*links,*upper_court],.61)
    wall(m,"RearLodgeRearWall",(W,BACK),(-W,BACK),.24,eave-.26,.61,"wall",[*upper,
         *[Opening(c,1.54,1.94,3.91,.75) for c in (3.13,8.08,12.46,16.85,21.23,25.61,30.57)]],.61)
    mid=(BACK+LodgeFront)/2
    for s in (-1,1):
        x=s*W
        a,b=((x,LodgeFront),(x,BACK)) if s>0 else ((x,BACK),(x,LodgeFront))
        wall(m,"RearLodgeGableWall",a,b,.24,eave-.26,.61,"wall",[Opening(3.45,9.67,2.10,4.96,.56)],.61)
        m.feature("LongLodgeGableEnds")
        gable_side=[(x,eave-.26,LodgeFront),(x,ridge-.26,mid),(x,eave-.26,BACK)]
        m.face(gable_side if s>0 else list(reversed(gable_side)),"wall")
    edge=.32
    e=eave-(ridge-eave)*edge/((BACK-LodgeFront)/2)
    for z in (LodgeFront-edge,BACK+edge):
        slab(m,"RearLodgeLongRoof",[(-W-edge,e,z),(W+edge,e,z),(W+edge,ridge,mid),(-W-edge,ridge,mid)],.26,borders={0,1,3})
    m.feature("LodgeRoofCrestAndGalleryBands")
    m.beam((-W-edge,ridge+.018,mid),(W+edge,ridge+.018,mid),.23,"roof",.18)
    for z in (LodgeFront,BACK):
        for y in (5.98,11.41,17.04):
            m.beam((-W,y,z),(W,y,z),.31,"timber",.31)
    floor(m,"LodgeUpperStoreyFloor",(-W+.22,LodgeFront+.19,W-.22,BACK-.19),6.05,"wood",bottom=5.80)


def gate(m):
    left,right=-INNER,INNER
    f,b=FRONT,FRONT+2.68
    portal=Opening(INNER,.24,6.28,5.52,1.39,None,.31)
    front=[portal,*[Opening(INNER+x,1.42,1.60,3.62,.74,"warm_glass",.16) for x in (-8.00,-5.48,5.48,8.00)]]
    wall(m,"GardenBreezewayStreetWall",(left,f),(right,f),.24,8.55,.54,"wall",front)
    wall(m,"GardenBreezewayCourtWall",(right,b),(left,b),.24,8.77,.54,"wall",[portal])
    slab(m,"LowGardenEntryRoof",[(left-.09,8.77,f-.27),(right+.09,8.77,f-.27),
           (right+.09,9.03,b+.32),(left-.09,9.03,b+.32)],.26,borders={0,2})
    m.feature("GardenBreezewayJambs")
    for x in (-3.64,3.64):
        m.box((x,3.67,f-.084),(.55,7.34,.59),"trim")
    m.feature("IronworkMountingPlate")
    m.box((0,7.20,f-.108),(6.19,2.08,.28),"wood")
    m.landmark("OpenworkFanOverTheCourtyard")
    for s in (-1,1):
        for i in range(1,5):
            angle=i*.29
            a=(s*.34*sin(angle),6.35,f-.30)
            b=(s*(.41+i*.46),7.98-.15*i,f-.30)
            m.beam(a,b,.105,"ironwork")
    for scale in (.62,1.20,2.22):
        for i in range(10):
            def p(t):
                return scale*sin(t),6.43+.82*scale*cos(t),f-.316
            m.beam(p((i-5)*.28),p((i-4)*.28),.100,"ironwork")
    m.markers["Entrance"]=(0,.24,f)


def outdoor_rooms(m):
    m.feature("InsetReflectingPoolBasin")
    m.ring(0,-.88,0,3.56,.24,.45,"trim",n=8,start=pi/8,end=pi/8+2*pi)
    m.ring(0,-.88,2.95,3.39,.45,1.16,"trim",n=8,start=pi/8,end=pi/8+2*pi)
    m.ring(0,-.88,2.91,3.51,1.16,1.32,"trim",n=8,start=pi/8,end=pi/8+2*pi)
    m.feature("StillPoolWaterSurface")
    m.ring(0,-.88,0,2.96,.96,.99,"water",n=8,start=pi/8,end=pi/8+2*pi)
    for s in (-1,1):
        for i,z in enumerate((-7.86,-2.54,3.93,6.97)):
            m.feature("GreeneryBeneathTheGallery")
            x=s*(8.17 if i<3 else 6.76)
            m.box((x,.42,z),(1.36,.36,2.68),"trim")
            m.box((x,.622,z),(1.25,.069,2.53),"soil")
            for j in range(5):
                leaf_crown(m,(x+(.23 if j%2 else -.19),.80+(j%2)*.16,z+(j-2)*.45),
                          (.41,.28,.39),z+j*.37,("leaf","leaflight","flower")[j%3])
        for i in range(4):
            f=FRONT+2.3+i*5.51
            m.feature("DeepShadeCanvas")
            def p(t,span):
                return s*(INNER-1.24*t),6.17-.66*t-.12*sin(pi*t),f+span*3.19
            for j in range(6):
                u,v=j/6,(j+1)/6
                m.face([p(u,0),p(u,1),p(v,1),p(v,0)],"fabric",[(0,u),(1,u),(1,v),(0,v)])
            m.feature("AwningTensionRods")
            m.beam(p(1,0),p(1,1),.12,"timber")
            for u in (0,1):
                m.beam((s*INNER,5.79,f+u*3.19),p(1,u),.09,"ironwork")


def build(m):
    floor(m,"CourtyardPavingWithinBuilding",(-W,FRONT,W,BACK),.24,"paving")
    for side in (-1,1):
        gallery(m,side)
    lodge(m)
    gate(m)
    outdoor_rooms(m)


def materials():
    def stripes(u,v):
        band=(u*7)%1
        return 0 if band<.68 else 1 if band<.90 else 2
    return {**MATERIALS,"Courtyard_CopperStripedCanvas":artwork("Courtyard_CopperStripedCanvas",stripes,
           ("9c583c","b17a55","a36748"),width=192,height=96)}


def verify(objects):
    for obj in objects:
        if obj.name.startswith(("GardenBreezewayStreetWall_Courtyard_OchreStone","GardenBreezewayCourtWall_Courtyard_OchreStone")):
            mesh=obj.data
            tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
            assert tree.ray_cast((0,-17,1.84),(0,1,0),8)[0] is None
        if obj.name.endswith("_Courtyard_CinnabarTiles"):
            mesh=obj.data
            tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
            assert tree.ray_cast((0,-.88,25),(0,0,-1),25)[0] is None
    return {"open_archway_to_garden":True,"reflecting_pool_open_to_sky":True}
