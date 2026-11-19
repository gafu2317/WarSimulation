"""A polygonal card hall with real front openings and the gaming tables in view."""

from math import cos, pi, sin, tau

from mathutils.bvhtree import BVHTree

from components import Opening, floor, round_wall, slab
from landmarks import outline
from materials import artwork


PALETTE = {
    "roof":"Casino_RoseClay", "wall":"Casino_PowderedMauve", "trim":"Casino_QuartzSandstone",
    "ivory":"Casino_CardEdge", "spade":"Casino_SpadePrint", "diamond":"Casino_DiamondPrint",
    "felt":"Casino_GamingFelt", "leather":"Casino_OxbloodSeats",
}
MATERIALS = {
    "Casino_PowderedMauve":dict(source="Plaster",color="b29ba7",relief=.20),
    "Casino_QuartzSandstone":dict(source="Limestone",color="b1a489",relief=.16),
    "Casino_CardEdge":dict(source="Oak",color="c3aa80",relief=.11),
    "Casino_OxbloodSeats":dict(source="Canvas_Red",color="935551",relief=.15,emittance=.058),
}

LOWER,UPPER=15.2,10.94
RAD0,RAD1=LOWER/cos(pi/8),UPPER/cos(pi/8)
FIRST,SECOND=.28,10.04
CAMERA_DIRECTIONS = {"three_quarter":(.32,-1,.54)}


def point(radius,i,height):
    angle=tau*(i-.5)/8
    return radius*sin(angle),height,-radius*cos(angle)


def halls(m):
    m.feature("OctagonalGamingFloor")
    m.ring(0,0,0,RAD0,0,FIRST,"paving",n=8,start=pi/8,end=tau+pi/8)
    def ground_openings(i,c):
        if i==0:
            return [Opening(c+d,FIRST,4.66,5.40,1.45,None,.27) for d in (-2.74,2.74)]
        if i==3:
            return [Opening(c,FIRST,2.73,4.64,.72,"door",.18)]
        return [Opening(c+d,1.54,1.89,4.61,.75,"warm_glass",.20) for d in (-3.03,0,3.03)]
    round_wall(m,"LowerCasinoWalls",RAD0,FIRST,8.26,.64,"wall",ground_openings,n=8)
    def high_openings(i,c):
        if i==0:
            return []
        return [Opening(c+d,SECOND+1.45,1.89,5.08,1.04,"warm_glass",.20) for d in (-2.12,2.12)]
    round_wall(m,"UpperCasinoWalls",RAD1,SECOND,18.18,.58,"wall",high_openings,n=8)
    m.feature("UpperOctagonalHallFloor")
    m.ring(0,0,0,RAD1,SECOND-.34,SECOND,"wood",n=8,start=pi/8,end=tau+pi/8)
    for i in range(8):
        m.feature("StoneColumnsUnderUpperHall")
        x,_,z = point(RAD1-.42,i,FIRST)
        m.box((x,4.99,z),(.83,9.42,.83),"trim")
        for y in (.55,9.51):
            m.box((x,y,z),(1.01,.37,1.01),"trim")
        slab(m,"LowOctagonalAisleRoof",[point(RAD0+.54,i,8.15),point(RAD0+.54,i+1,8.15),
             point(RAD1,i+1,10.30),point(RAD1,i,10.30)],.26,borders={0})
        slab(m,"UpperOctagonalHipRoof",[point(RAD1+.38,i,18.27),point(RAD1+.38,i+1,18.27),
             (0,23.74,0)],.27,borders={0})
        m.feature("OctagonalStoneQuoins")
        for rad,y,h,size in ((RAD0,3.99,7.98,.67),(RAD1,14.12,8.16,.58)):
            m.box(point(rad-.12,i,y),(size,h,size),"trim",-tau*(i-.5)/8)
        m.feature("RaisedClayHipCaps")
        m.beam(point(RAD1+.34,i,18.31),(0,23.78,0),.20,"roof",.19)
    m.feature("OctagonalRoofCrown")
    m.cylinder((0,23.85,0),.50,.36,"trim",n=12,top=.34)
    floor(m,"MainHallEntryTread",(-5.67,-LOWER-.67,5.67,-LOWER),FIRST,"trim")
    m.markers["Entrance"]=(2.74,FIRST,-LOWER-.67)


def cards(m):
    w,h=2.72,4.46
    zfront=-UPPER
    m.feature("CardSignBackingBrackets")
    m.box((0,13.63,zfront-.14),(6.89,.42,.43),"timber")
    m.box((0,15.09,zfront-.16),(5.45,.30,.50),"timber")
    for direction in (-1,1):
        m.box((direction*1.87,14.11,zfront-.21),(.33,1.48,.21),"metal")
        m.box((direction*.47,14.39,zfront-.34),(.24,1.72,.67),"metal")
    m.landmark("FanOfPlayingCards")
    for x,y,a,z,material in ((-2.04,13.69,.32,zfront-.42,"diamond"),
                             (2.04,13.69,-.32,zfront-.38,"diamond"),
                             (0,14.53,0,zfront-.76,"spade")):
        def p(u,v):
            return x+u*cos(a)-v*sin(a),y+u*sin(a)+v*cos(a)
        corners=[(-w/2+.15,-h/2),(w/2-.15,-h/2),(w/2,-h/2+.15),(w/2,h/2-.15),
                 (w/2-.15,h/2),(-w/2+.15,h/2),(-w/2,h/2-.15),(-w/2,-h/2+.15)]
        outline(m,[p(u,v) for u,v in corners],z,.22,"ivory")
        face=[(u*.96,v*.97) for u,v in reversed(corners)]
        m.face([(*p(u,v),z-.113) for u,v in face],material,[(u/w+.5,v/h+.5) for u,v in face])


def game_tables(m):
    for x,z,wide in ((-2.78,-11.94,False),(2.81,-11.79,True)):
        a,b=(1.58,1.08) if wide else (1.35,1.28)
        m.feature("StableGamingTables")
        m.cylinder((x,.71,z),.23,.88,"timber",n=14,top=.27)
        m.cylinder((x,.38,z),.69,.20,"timber",n=18,top=.51)
        def ring(i,margin=0,y=1.36):
            t=i*tau/28
            return x+(a-margin)*cos(t),y,z+(b-margin)*sin(t)
        for i in range(28):
            m.face([ring(i,y=1.16),ring(i),ring(i+1),ring(i+1,y=1.16)],"wood")
            m.face([ring(i),ring(i,.11),ring(i+1,.11),ring(i+1)],"wood")
        m.feature("PrintedTableFelt")
        coords=[ring(i,.11,1.363) for i in reversed(range(28))]
        m.face(coords,"felt",[((px-x)/a*.5+.5,(pz-z)/b*.5+.5) for px,_,pz in coords])
        m.feature("TableStools")
        for i in range(6):
            t=tau*i/6+pi/6
            u,v=x+1.78*cos(t),z+1.59*sin(t)
            m.cylinder((u,.44,v),.11,.32,"timber",n=12,top=.094)
            m.cylinder((u,.63,v),.33,.12,"leather",n=16,top=.31)


def materials():
    def card(spade):
        def pixel(u,v):
            x,y=abs(u-.5),v-.50
            if min(u,1-u,v,1-v)<.056:
                return 0
            if spade:
                emblem=(x/(.287*(1-.73*max(y,0)/.24)+1e-6) < 1 and 0<=y<.29
                        or ((x-.128)/.153)**2+((y+.012)/.119)**2<1
                        or -0.205<y<.012 and x<.056+.27*abs(y))
            else:
                emblem=x/.31+abs(y)/.28<1
            return 2 if emblem else 1
        return pixel
    def felt(u,v):
        x,y=u-.5,v-.5
        r=(x*x+y*y)**.5
        if abs(r-.305)<.013 or abs(r-.386)<.010 or abs(x)<.16 and abs(y)<.082 and min(abs(abs(x)-.16),abs(abs(y)-.08))<.012:
            return 1
        return 2 if r<.299 else 0
    return {**MATERIALS,
        "Casino_SpadePrint":artwork("Casino_SpadePrint",card(True),("b89866","dabe88","272731"),width=192,height=300),
        "Casino_DiamondPrint":artwork("Casino_DiamondPrint",card(False),("b89866","dabe88","a04648"),width=192,height=300),
        "Casino_GamingFelt":artwork("Casino_GamingFelt",felt,("527254","c4a66c","658c64"),emittance=.29)}


def build(m):
    halls(m)
    cards(m)
    game_tables(m)


def verify(objects):
    wall=next(o.data for o in objects if o.name=="LowerCasinoWalls_Casino_PowderedMauve")
    tree=BVHTree.FromPolygons([v.co for v in wall.vertices],[p.vertices[:] for p in wall.polygons])
    assert all(tree.ray_cast((x,-18,2.1),(0,1,0),7)[0] is None for x in (-2.74,2.74))
    assert tree.ray_cast((0,-18,2.1),(0,1,0),7)[0] is not None
    return {"eight_sided_halls":2,"clear_gaming_hall_openings":2,"card_symbols_in_textures":True}
