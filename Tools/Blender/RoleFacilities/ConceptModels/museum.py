"""Exhibition hall with visible galleries and textured paintings."""

from math import pi, sin

from mathutils.bvhtree import BVHTree

from components import Opening, floor, hip, rectangle_walls
from materials import artwork


PALETTE = {
    "wall":"Museum_RussetBrick", "trim":"Museum_PaleSandstone", "roof":"Museum_BronzeTile",
    "painting_sign":"Museum_MorningHills", "painting_west":"Museum_SnowAndWater",
    "painting_east":"Museum_GoldFields", "matting":"Museum_DisplayLinen",
}
MATERIALS = {
    "Museum_RussetBrick":dict(source="Brick",color="a56f53",relief=.17),
    "Museum_PaleSandstone":dict(source="Limestone",color="c0aa89",relief=.16),
    "Museum_DisplayLinen":dict(source="Linen",color="d7c9a6",roughness=.85),
}

BOUNDS = (-18.1,-14.65,18.1,14.65)


def picture(m,x,y,z,w,h,material,frame=.19):
    m.box((x,y,z+.08),(w+2*frame,h+2*frame,.22),"timber")
    face=[(x-w*.5,y-h*.5,z-.041),(x-w*.5,y+h*.5,z-.041),
          (x+w*.5,y+h*.5,z-.041),(x+w*.5,y-h*.5,z-.041)]
    m.face(face,material,[(0,0),(0,1),(1,1),(1,0)])
    for side in (-1,1):
        m.box((x+side*(w+frame)*.5,y,z-.035),(frame,h,.16),"wood")
        m.box((x,y+side*(h+frame)*.5,z-.035),(w+2*frame,frame,.16),"wood")


def gallery(m,x,label,painting):
    m.feature(label+"DisplayRoom")
    m.box((x,5.82,-10.10),(10.75,11.2,.45),"trim")
    for side in (-1,1):
        m.box((x+side*5.58,5.59,-12.36),(.39,10.74,4.32),"trim")
    m.feature(label+"Painting")
    picture(m,x,5.93,-10.35,7.76,4.86,painting)
    m.feature(label+"DisplayCase")
    m.box((x,.76,-12.73),(4.65,1.05,1.27),"timber")
    m.box((x,1.31,-12.73),(4.69,.12,1.37),"wood")
    m.box((x,1.382,-12.73),(4.42,.022,1.11),"matting")
    for side in (-1,1):
        m.box((x+side*1.42,1.456,-12.73),(.93,.129,.68),"ivory")
    m.box((x,1.66,-12.73),(4.66,.44,1.32),"clear_glass")


def build(m):
    left,front,right,back = BOUNDS
    w = right-left
    floor(m,"ExhibitionHallFloor",BOUNDS,.24,"trim")
    galleries = [Opening(u,.24,9.64,10.46,1.62,None,.28) for u in (7.42,w-7.42)]
    doors = [Opening(w*.50,.24,3.24,3.7,.23,"door",.26)]
    side_high = [Opening((back-front)*(i+.5)/6,13.08,1.96,3.26,.60) for i in range(6)]
    front_high = [Opening(u,13.08,1.97,3.26,.50) for u in (3.25,7.42,11.2,15.54,20.66,25,28.78,32.95)]
    flank = [Opening((back-front)*(i+.5)/6,2.98,2.21,6.75,.58) for i in range(6)]
    rear_low = [Opening(u,3.15,2.35,6.6,.6) for u in (4.3,11.2,18.1,25,31.9)]
    openings = {"Front":[*galleries,*doors,*front_high],"Rear":[*rear_low,*front_high],
                "East":[*flank,*side_high],"West":[*flank,*side_high]}
    rectangle_walls(m,"Museum",BOUNDS,.24,18.10,.81,openings=openings)
    m.feature("EngagedStonePiers")
    for z in (front,back):
        for x in (-17.8,-5,5,17.8):
            m.box((x,9.06,z),(1.11,18.12,.84),"trim")
        m.box((0,12.1,z),(36.72,.31,.41),"trim")
    for x in (left,right):
        m.box((x,12.1,0),(.41,.31,28.87),"trim")
        for z in (-9.74,-4.87,0,4.87,9.74):
            m.box((x,9.06,z),(.65,18.12,.57),"trim")
    hip(m,"ContinuousHippedRoof",BOUNDS,18.36,22.36,.49)
    floor(m,"RecessedEntranceStep",(-1.97,front-.55,1.97,front),.24,"trim")
    m.markers["Entrance"] = (0,.24,front-.55)
    m.landmark("FramedSunrise")
    picture(m,0,7.98,front-.20,4.96,4.25,"painting_sign",.24)
    gallery(m,-10.68,"WestGallery","painting_west")
    gallery(m,10.68,"EastGallery","painting_east")


def landscape(name,colors,offset,river=False):
    def picture_pixel(u,v):
        horizon = .46+.105*sin((u+offset)*pi*3)+.06*sin((u+offset)*pi*7)
        rear = .62+.078*sin((u+offset)*pi*6)+.058*cosine_peak(u+offset)
        if ((u-.63)*1.16)**2+(v-.77)**2<.073**2 and v>rear:
            return 1
        if v>rear:
            return 0
        if v>horizon:
            return 2 if v<rear-.04 else 3
        if v>.28+.033*sin(u*12):
            return 4
        curve = .58-.27*sin(4*v+.09)-.34*(.28-v)
        if river and abs(u-curve)<.11+.37*(.28-v):
            return 5
        return 6 if sin(26*v+7*u)>.16 else 4
    return artwork(name,picture_pixel,colors,width=352,height=224,emittance=.16)


def cosine_peak(u):
    return max(0,1-abs(u-.39)*10)+max(0,1-abs(u-.72)*17)


def materials():
    return {
        **MATERIALS,
        "Museum_MorningHills":landscape("Museum_MorningHills",
            ("d29457","f6d68d","615f67","c1a183","6a724f","97b0aa","8d884b"),.15,True),
        "Museum_SnowAndWater":landscape("Museum_SnowAndWater",
            ("7196a7","e5ded0","657997","d5d7ce","51694c","7caaae","2f4d47"),.0,True),
        "Museum_GoldFields":landscape("Museum_GoldFields",
            ("95adb4","f5d3a1","697656","9e9b73","b58d41","8fb1b6","cfab4c"),-.12),
    }


def verify(objects):
    facade = next(o.data for o in objects if o.name=="MuseumFront_Museum_RussetBrick")
    tree = BVHTree.FromPolygons([v.co for v in facade.vertices],[p.vertices[:] for p in facade.polygons])
    for x in (-10.68,10.68):
        hit,_,_,_ = tree.ray_cast((x,-19,5.6),(0,1,0),8.5)
        assert hit is None
    return {"street_facing_exhibition_bays":2,"textured_artworks":3,"gallery_opening_width_m":9.64}
