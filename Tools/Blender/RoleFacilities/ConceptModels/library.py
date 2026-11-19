"""Clerestory reading hall, connected archive aisles and open-book relief."""

from math import cos, pi, radians, sin

from components import Opening, floor, gable, rectangle_walls, slab, wall
from materials import artwork


PALETTE = {
    "roof":"Library_VerdigrisTile", "wall":"Library_ClayPlaster", "trim":"Library_CutSandstone",
    "page":"Library_PageInscription", "page_edge":"Library_EdgesOfPaper",
}
MATERIALS = {
    "Library_ClayPlaster":dict(source="Plaster_Rose",color="9c7464",relief=.23),
    "Library_CutSandstone":dict(source="Limestone",color="c7ad83",relief=.16),
    "Library_EdgesOfPaper":dict(source="Paper",color="e0cba0",roughness=.80),
}

FRONT = -14.35
BACK = 14.35
NAVE = 7.70
OUTER = 15.83
AISLE_INNER_EAVE = 12.88
AISLE_OUTER_EAVE = 10.07


def book(m):
    m.landmark("OpenBook")
    m.box((0,6.02,FRONT-.164),(6.63,3.24,.34),"leather")
    for side in (-1,1):
        def p(u,v,offset=0):
            y=4.49+.33*u+.24*sin(pi*u)+v*(3.02-.17*u)
            return side*3.19*u,y,FRONT-.438-.25*sin(pi*u)-.06*u+offset

        for i in range(8):
            a,b = i/8,(i+1)/8
            uv = [(a,0),(a,1),(b,1),(b,0)]
            if side<0:
                uv.reverse()
            m.face([p(u,v) for u,v in uv],"page",uv)
            for end in (0,1):
                pts=[p(a,end),p(b,end),p(b,end,.12),p(a,end,.12)]
                m.face(pts if (side<0)==(end==0) else pts[::-1],"page_edge")
        edge=[p(1,0),p(1,1),p(1,1,.12),p(1,0,.12)]
        m.face(edge if side>0 else edge[::-1],"page_edge")
    m.feature("BookMountingBrackets")
    for x in (-2.45,2.45):
        m.box((x,4.45,FRONT-.20),(.42,.30,.53),"timber")


def aisles(m):
    front,back = FRONT+.26,BACK-.26
    width = OUTER-NAVE
    flank = [Opening(t,1.43,1.56,5.80,.67) for t in (1.67,width*.5,width-1.67)]
    side_windows = [Opening((i+.5)*(back-front)/6,1.45,2.24,5.95,.72) for i in range(6)]
    for label,left,right in (("West",-OUTER,-NAVE),("East",NAVE,OUTER)):
        side = right if label=="East" else left
        inside = left if label=="East" else right
        m.feature(label+"BookshelfGalleryBase")
        m.box(((left+right)*.5,.095,0),(width,.19,back-front),"trim")
        wall(m,label+"GalleryFront",(left,front),(right,front),.19,AISLE_OUTER_EAVE-.26,.57,openings=flank,miter=.57)
        wall(m,label+"GalleryRear",(right,back),(left,back),.19,AISLE_OUTER_EAVE-.26,.57,openings=flank,miter=.57)
        a,b = ((side,front),(side,back)) if label=="East" else ((side,back),(side,front))
        wall(m,label+"GalleryOuter",a,b,.19,AISLE_OUTER_EAVE-.26,.57,openings=side_windows,miter=.57)
        outside = side + (.39 if side>0 else -.39)
        lower_edge = AISLE_OUTER_EAVE-.39*(AISLE_INNER_EAVE-AISLE_OUTER_EAVE)/width
        slab(m,label+"AisleRoof",[(outside,lower_edge,front-.36),(outside,lower_edge,back+.36),
                               (inside,AISLE_INNER_EAVE,back+.36),(inside,AISLE_INNER_EAVE,front-.36)],.26,
             borders={0,1,3})
        m.feature(label+"AisleGableEnd")
        for z in (front,back):
            tri=[(side,AISLE_OUTER_EAVE-.26,z),(inside,AISLE_INNER_EAVE-.26,z),
                 (inside,AISLE_OUTER_EAVE-.26,z)]
            m.face(tri if (side>0)==(z==front) else tri[::-1],"wall")


def public_entrance(m):
    floor(m,"EntranceThreshold",(-1.78,FRONT-.66,1.78,FRONT),.19,"trim")
    m.feature("OpenEntranceDoors")
    angle = radians(72)
    for side in (-1,1):
        x = side*(1.514-.69*cos(angle))
        z = FRONT+.18+.69*sin(angle)
        m.box((x,1.78,z),(1.38,3.16,.11),"wood",side*angle)
    m.markers["Entrance"] = (0,.19,FRONT-.68)


def reading_room(m):
    m.feature("ReadingTables")
    for side in (-1,1):
        for z in (-10.5,-5.55,-.60):
            x = side*3.1
            m.box((x,1.40,z),(2.87,.15,1.46),"wood")
            for a,b in ((-1,-1),(-1,1),(1,-1),(1,1)):
                m.box((x+a*1.24,.748,z+b*.56),(.13,1.12,.13),"timber")
            m.box((x,1.50,z),(1.3,.053,.69),"books")
    m.feature("ArchiveShelves")
    for side in (-1,1):
        for z in (-7.93,-2.4,3.13,8.66):
            x = side*13.85
            m.box((x,1.95,z),(.62,3.52,3.11),"wood")
            for y in (.72,1.47,2.22,2.97):
                face=[(x-side*.32,y,z-1.38),(x-side*.32,y+.58,z-1.38),
                      (x-side*.32,y+.58,z+1.38),(x-side*.32,y,z+1.38)]
                uv=[(0,0),(0,1),(1,1),(1,0)]
                if side<0:
                    face.reverse()
                    uv.reverse()
                m.face(face,"books",uv)


def build(m):
    reading_room_bounds=(-NAVE,FRONT,NAVE,BACK)
    floor(m,"ReadingHallFloor",reading_room_bounds,.19,"wood")
    high = [Opening(u,9.10,2.57,6.68,.98) for u in (3.45,7.70,11.95)]
    periods = [(i+.5)*(BACK-FRONT)/6 for i in range(6)]
    opened_aisles = [Opening(u,.19,2.63,5.68,.79,None) for u in periods]
    clerestory = [Opening(u,13.87,2.32,3.72,.82) for u in periods]
    walls = {"Front":[Opening(7.70,.19,3.16,3.50,.58,None,.23),*high], "Rear":high,
             "East":[*opened_aisles,*clerestory], "West":[*opened_aisles,*clerestory]}
    rectangle_walls(m,"ReadingHall",reading_room_bounds,.19,18.44,.71,openings=walls)
    gable(m,"ReadingHallGableRoof",reading_room_bounds,18.70,24.00)
    aisles(m)
    m.feature("FacadeStonePiers")
    for x,height,z in ((-NAVE,18.42,FRONT),(NAVE,18.42,FRONT),
                       (-OUTER,9.81,FRONT+.26),(OUTER,9.81,FRONT+.26),
                       (-NAVE,18.42,BACK),(NAVE,18.42,BACK),
                       (-OUTER,9.81,BACK-.26),(OUTER,9.81,BACK-.26)):
        m.box((x,height*.5,z),(.78,height,.78),"trim")
    m.feature("FacadeStringcourses")
    for z in (FRONT,BACK):
        m.box((0,8.76,z),(14.61,.26,.34),"trim")
        for x in (-11.765,11.765):
            m.box((x,8.42,z*.982),(7.32,.23,.32),"trim")
    public_entrance(m)
    book(m)
    reading_room(m)


def materials():
    def page(u,v):
        end = .81-(int(v*16)%4==1)*.09
        return int(.17<u<end and any(abs(v-y)<.016 for y in (.38,.50,.62,.74)))
    return {**MATERIALS,"Library_PageInscription":artwork("Library_PageInscription",page,("dfcba5","695340"))}


def verify(objects):
    roofs = [o for o in objects if "AisleRoof_Library_VerdigrisTile" in o.name]
    assert len(roofs)==2
    tops = [max(v.co.z for v in obj.data.vertices) for obj in roofs]
    assert max(tops)<=12.89
    door_data = next(o.data for o in objects if o.name=="OpenEntranceDoors_Oak")
    assert min(abs(v.co.x) for v in door_data.vertices)>1.0
    sign = next(o.data for o in objects if o.name=="Landmark_OpenBook_Library_PageInscription")
    assert max(v.co.y for v in sign.vertices)<FRONT
    return {"clerestory_clearance_m":round(13.87-max(tops),3), "book_span_m":6.38,
            "open_entry_clear_width_m":round(min(abs(v.co.x) for v in door_data.vertices)*2,3)}
