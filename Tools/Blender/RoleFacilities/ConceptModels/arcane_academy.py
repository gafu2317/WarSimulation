"""A teaching hall connected to an eight-sided focusing tower."""

from math import atan2, cos, pi, sin, tan, tau

from mathutils.bvhtree import BVHTree

from components import Opening, floor, gable, round_wall, slab, wall
from materials import artwork


PALETTE = {
    "roof":"Arcane_AubergineClay", "wall":"Arcane_LavenderGranite", "trim":"Arcane_SilverAshlar",
    "stained":"Arcane_AstralStainedGlass", "mount":"Arcane_AntiqueBronze",
    "crystal_light":"Arcane_SeaGlassBright", "crystal":"Arcane_SeaGlass",
    "crystal_dark":"Arcane_SeaGlassShadow",
}
MATERIALS = {
    "Arcane_LavenderGranite":dict(source="Limestone",color="8b8992",roughness=.75,relief=.22),
    "Arcane_SilverAshlar":dict(source="Limestone",color="b5b1ab",relief=.17),
    "Arcane_AntiqueBronze":dict(source="Brass",color="64604a",roughness=.43,metallic=.37,relief=.11),
    "Arcane_SeaGlassBright":dict(color="64d8dd",roughness=.27,metallic=.17,emittance=.43),
    "Arcane_SeaGlass":dict(color="39b5c3",roughness=.25,metallic=.17,emittance=.53),
    "Arcane_SeaGlassShadow":dict(color="239ba9",roughness=.24,metallic=.19,emittance=.31),
}

HALL = (-8.85,-11.4,18.77,11.0)
TOWER = (-14.04,-6.20)
APOTHEM = 5.19
RADIUS = APOTHEM/cos(pi/8)
FLOOR, DECK = .48,19.44
CAMERA_DIRECTIONS = {"three_quarter":(.42,-1,.48)}


def lecture_hall(m):
    left,front,right,back = HALL
    w,d = right-left,back-front
    floor(m,"LectureHallPaving",HALL,FLOOR,"paving")
    top_windows = [Opening(w*(i+.5)/6,8.04,2.05,5.22,1.34,"stained",.21) for i in range(6)]
    lower_windows = [Opening(w*(i+.5)/6,1.32,1.97,4.62,1.15,"stained",.20) for i in (0,1,4,5)]
    entry = Opening(w/2,FLOOR,4.05,5.58,1.03,"door",.27)
    side_high = [Opening(x,8.04,2.05,5.22,1.34,"stained",.19) for x in (3.6,8.7,13.7,18.82)]
    side_low = [Opening(x,1.36,1.93,4.60,1.13,"stained",.18) for x in (3.6,8.7,13.7,18.82)]
    link = Opening(back-TOWER[1],FLOOR,2.70,4.17,.48,None,.0)
    bottom_west = [o for o in side_low if d-o.center>11.5]
    top_west = [o for o in side_high if d-o.center>11.5]
    edges = [((left,front),(right,front),[entry,*lower_windows,*top_windows]),
             ((right,front),(right,back),[*side_low,*side_high]),
             ((right,back),(left,back),[*lower_windows,*top_windows]),
             ((left,back),(left,front),[link,*bottom_west,*top_west])]
    for i,(a,b,openings) in enumerate(edges):
        wall(m,"LectureHallWall"+str(i),a,b,FLOOR,15.30,.62,"wall",openings,.62)

    z = (front+back)/2
    a,b = left,right+.43
    eave,high = 15.56,21.63
    edge_y = eave-(high-eave)*.43/(d/2)
    for edge in (front-.43,back+.43):
        slab(m,"LongLectureHallRoof",[(a,edge_y,edge),(b,edge_y,edge),
              (b,high,z),(a,high,z)],.26,borders={0,1,3})
    m.feature("EndGablesAndRidgeCoping")
    for x in (left,right):
        points = [(x,eave-.26,front),(x,eave-.26,back),(x,high-.26,z)]
        m.face(points if x==left else list(reversed(points)),"wall")
    m.beam((left,high+.042,z),(right+.43,high+.042,z),.27,"roof",.19)
    m.feature("BaysAndBeltCourses")
    for zz in (front,back):
        for i in range(7):
            if zz==front and i==3:
                continue
            x=left+i*w/6
            m.box((x,7.64,zz),(.51,15.28,.46),"trim")
            m.box((x,14.17,zz),(.70,.45,.56),"trim")
        m.beam((left,6.81,zz),(right,6.81,zz),.41,"trim",.29)
    for distance in (1.11,6.20,11.18,16.23,21.31):
        m.box((right,7.64,front+distance),(.53,15.28,.51),"trim")

    x=(left+right)/2
    m.feature("RaisedEntryPilasters")
    for side in (-1,1):
        m.box((x+side*2.61,3.64,front-.24),(.84,7.28,.83),"trim")
        m.box((x+side*2.61,6.65,front-.28),(1.13,.43,.94),"trim")
    m.box((x,7.15,front-.50),(6.30,.46,1.24),"trim")
    gable(m,"PortalPediment",(x-3.2,front-1.06,x+3.2,front+.09),7.40,8.86,.19,
          wall_material="trim",roof_material="trim")
    floor(m,"DoorLanding",(x-2.33,front-.73,x+2.33,front),FLOOR,"trim")
    floor(m,"LowerEntryStep",(x-2.74,front-1.31,x+2.74,front-.73),.32,"trim")
    floor(m,"FirstEntryStep",(x-2.91,front-1.89,x+2.91,front-1.31),.16,"trim")
    m.markers["Entrance"]=(x,FLOOR,front-.73)


def tower(m):
    x,z = TOWER

    def apertures(i,c):
        if i==2:
            return [Opening(c,FLOOR,2.70,4.17,.48,None,0)]
        return [Opening(c,y,1.57,4.35,1.04,"stained",.19) for y in (1.48,7.4,13.25)]

    m.feature("TowerBasaltPlinth")
    m.ring(x,z,0,RADIUS,0,FLOOR,"trim",n=8,start=pi/8,end=tau+pi/8)
    round_wall(m,"FocusTowerMasonry",RADIUS,FLOOR,DECK,.75,"wall",apertures,n=8,center=TOWER)
    m.feature("TowerHorizontalBeltCourses")
    for y in (6.21,12.14,18.73):
        m.ring(x,z,RADIUS-.24,RADIUS+.20,y,y+.32,"trim",n=8,start=pi/8,end=tau+pi/8)
    m.feature("FocusingDeckSlab")
    m.ring(x,z,0,RADIUS+.14,DECK-.42,DECK,"trim",n=8,start=pi/8,end=tau+pi/8)
    m.feature("TowerParapetAndCoping")
    m.ring(x,z,RADIUS-.57,RADIUS+.12,DECK,20.39,"wall",n=8,start=pi/8,end=tau+pi/8)
    m.ring(x,z,RADIUS-.70,RADIUS+.26,20.39,20.63,"trim",n=8,start=pi/8,end=tau+pi/8)
    for i in range(8):
        angle = tau*(i-.5)/8
        m.box((x+RADIUS*sin(angle),20.09,z-RADIUS*cos(angle)),(.49,1.29,.49),"trim",-angle)


def focusing_crystal(m):
    x,z = TOWER
    m.feature("CrystalPedestal")
    m.cylinder((x,DECK+.29,z),2.10,.58,"trim",n=20)
    m.cylinder((x,DECK+.69,z),1.46,.22,"mount",n=20,top=1.29)
    facets = [(.87,20.24,.06),(1.89,21.79,.03),(2.37,24.20,.13),(1.69,26.20,.22)]
    rings = []
    for j,(radius,y,twist) in enumerate(facets):
        rings.append([(x+radius*cos(a+twist),y+.13*sin(a*3+j),z+radius*sin(a+twist))
                      for a in (tau*i/8 for i in range(8))])
    top = (x-.23,27.84,z+.045)
    tints = ("crystal_light","crystal","crystal_dark")
    m.landmark("TowerFocusingCrystal")
    m.face(list(reversed(rings[0])),"crystal_dark")
    for row,(low,high) in enumerate(zip(rings,rings[1:])):
        for i in range(8):
            j=(i+1)%8
            m.face([low[i],high[i],high[j]],tints[(2*i+row)%3])
            m.face([low[i],high[j],low[j]],tints[(i+2*row+1)%3])
    for i in range(8):
        m.face([rings[-1][i],top,rings[-1][(i+1)%8]],tints[i%3])
    m.feature("FourCrystalRetainers")
    for i in range(4):
        a = i*pi/2
        def p(r,y):
            return x+r*cos(a),y,z+r*sin(a)
        m.box(p(3.82,DECK+.18),(.90,.36,.91),"mount",-a)
        profile = [p(r,y) for r,y in ((3.84,19.66),(3.79,20.69),(3.42,21.72),
                                      (2.88,22.38),(2.12,22.95))]
        for start,end in zip(profile,profile[1:]):
            m.beam(start,end,.41,"mount",.48)
        m.box(p(2.15,22.95),(.40,.58,.64),"mount",-a)
        m.box(p(2.30,23.14),(.32,.093,.76),"bronze",-a)


def build(m):
    lecture_hall(m)
    tower(m)
    focusing_crystal(m)


def materials():
    def astral_panes(u,v):
        x,y = u-.5,v-.70
        r = (x*x+2.8*y*y)**.5
        if r<.318:
            a = atan2(y*1.67,x)
            if abs(r-.290)<.013 or (r>.061 and abs(sin(a*4))<.17):
                return 0
            return 1+int((a+pi)*4/pi+1e-5)%4
        x,y=(u+.35*v)*7,(v-.025)*13
        if abs(x-round(x))<.067 or abs(y-round(y))<.081 or abs(u-.5)<.024:
            return 0
        return 1+(int(x)+3*int(y))%4
    return {**MATERIALS,"Arcane_AstralStainedGlass":artwork("Arcane_AstralStainedGlass",astral_panes,
        ("363940","bd8e48","d6a762","4a87a6","735f95"),width=192,height=336,emittance=.35)}


def verify(objects):
    links = [next(o.data for o in objects if o.name==f"{name}_Arcane_LavenderGranite")
             for name in ("LectureHallWall3","FocusTowerMasonry")]
    for mesh in links:
        tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
        assert tree.ray_cast((-11,TOWER[1],2.3),(1,0,0),5)[0] is None
    return {"open_link_to_tower":True,"crystal_apex_m":27.84,"crystal_mount_footprint_m":8.55}
