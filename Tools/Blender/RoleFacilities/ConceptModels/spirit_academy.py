"""Two levels of teaching galleries around a mature courtyard tree."""

from math import cos, pi, sin, tau

from mathutils import Vector
from mathutils.bvhtree import BVHTree

from components import Opening, courtyard_roof, floor, gable, rectangle_walls
from landmarks import outline


PALETTE = {
    "roof":"Spirit_ForestCeramic", "stone":"Spirit_HoneyStone", "wall":"Spirit_WarmLime",
    "trim":"Spirit_LightSandstone", "timber":"Spirit_ChestnutTimber", "bark":"Spirit_OldBark",
    "leaf":"Spirit_ShadedCanopy", "leaflight":"Spirit_SunlitCanopy", "token":"Spirit_PaleJade",
    "medallion":"Spirit_DarkJade", "spiral":"Spirit_IvoryEnamel",
}
MATERIALS = {
    "Spirit_HoneyStone":dict(source="Limestone",color="b6a07d",relief=.20),
    "Spirit_WarmLime":dict(source="Plaster",color="d0b890",relief=.16),
    "Spirit_LightSandstone":dict(source="Limestone",color="bfad8b",relief=.16),
    "Spirit_ChestnutTimber":dict(source="DarkTimber",color="73563f",roughness=.78,relief=.22),
    "Spirit_OldBark":dict(source="Bark",color="977857",roughness=.88,relief=.62),
    "Spirit_ShadedCanopy":dict(source="Foliage",color="687a43",roughness=.84,relief=.42),
    "Spirit_SunlitCanopy":dict(source="Foliage",color="879652",roughness=.82,relief=.32),
    "Spirit_PaleJade":dict(color="99c9ac",roughness=.31,metallic=.19,emittance=.36),
    "Spirit_DarkJade":dict(source="Oak",color="375f50",roughness=.69,relief=.10),
    "Spirit_IvoryEnamel":dict(color="c7d4aa",roughness=.41),
}

OUTER = (-18.25,-16.4,18.25,16.4)
COURT = (-10.92,-9.90,10.92,9.87)
EAVE, UPPER = 13.22,6.56
CAMERA_DIRECTIONS = {"three_quarter":(.30,-1,.94),"rear":(-.42,1,1.55)}


def training_galleries(m):
    left,front,right,back = OUTER
    x0,z0,x1,z1 = COURT
    floor(m,"CourtyardAndSchoolPaving",OUTER,.22,"paving")
    windows = [Opening(x,1.51,2.07,3.74,.73) for x in (3.22,8.26,13.39,23.11,28.24,33.28)]
    short = [Opening(x,1.51,1.89,3.74,.74) for x in (3.66,8.74,13.87,18.93,24.06,29.14)]
    entry = Opening((right-left)/2,.22,5.32,5.46,1.54,None,.30)
    rectangle_walls(m,"SandstoneSchoolWalls",OUTER,.22,UPPER-.23,.64,"stone",{
        "Front":[entry,*windows],"Rear":[*windows,Opening(18.25,1.51,2.07,3.74,.73)],"East":short,"West":short})
    centers = [opening.center for opening in short]
    for x in (left,right):
        m.feature("ExposedTimberAndSills")
        m.beam((x,UPPER+.04,front),(x,UPPER+.04,back),.36,"timber",.29)
        for distance in ((.36,)+tuple((a+b)/2 for a,b in zip(centers,centers[1:]))+(32.44,)):
            m.box((x,9.72,front+distance),(.29,6.39,.30),"timber")
    high_side = [Opening(o.center,8.55,1.98,3.22,.12) for o in short]
    high_long = [Opening(o.center,8.55,2.08,3.22,.12) for o in windows]
    rectangle_walls(m,"UpperTimberSchoolWalls",OUTER,UPPER-.23,EAVE-.26,.64,openings={
        "Front":high_long,"Rear":[*high_long,Opening(18.25,8.55,2.08,3.22,.12)],"East":high_side,"West":high_side})
    m.feature("UpperFrontAndRearTimberFrames")
    for z in (front,back):
        m.beam((left,UPPER+.06,z),(right,UPPER+.06,z),.33,"timber",.30)
        for x in (-17.7,-12.51,-7.43,-2.43,2.43,7.43,12.51,17.7):
            m.box((x,9.71,z),(.31,6.42,.27),"timber")
    side_arches = [Opening((z1-z0)*(i+.5)/5,.22,2.91,4.70,1.44,None,.16) for i in range(5)]
    rear_arches = [Opening((x1-x0)*(i+.5)/5,.22,3.10,4.70,1.54,None,.17) for i in range(5)]
    inner_gate = Opening((x1-x0)/2,.22,5.32,5.46,1.54,None,.26)
    front_arches = [Opening(c,.22,2.74,4.70,1.40,None,.17) for c in (2.66,6.3,15.54,19.18)]
    rectangle_walls(m,"CourtyardStoneArcades",COURT,.22,UPPER-.24,.52,"stone",{
        "Front":[inner_gate,*front_arches],"Rear":rear_arches,"East":side_arches,"West":side_arches},inward=True)
    x_min,x_max = left+.19,right-.19
    spans = [(x_min,front+.19,x_max,z0-.06),(x_min,z0-.06,x0-.06,z1+.06),
             (x1+.06,z0-.06,x_max,z1+.06),(x_min,z1+.06,x_max,12.92),
             (x_min,15.64,x_max,back-.19),(x_min,12.92,-14.33,15.64),(-3.21,12.92,x_max,15.64)]
    for bounds in spans:
        floor(m,"ContinuousUpperGalleryFloor",bounds,UPPER,"wood",bottom=UPPER-.24)

    m.feature("UpperCourtyardPostsAndRailings")
    corners = [(x0,z0),(x1,z0),(x1,z1),(x0,z1)]
    for a,b in zip(corners,corners[1:]+corners[:1]):
        def p(t,y):
            return a[0]+(b[0]-a[0])*t,y,a[1]+(b[1]-a[1])*t
        for h,w in ((6.75,.22),(8.16,.23),(12.81,.43)):
            m.beam(p(.006,h),p(.994,h),w,"timber",w+.05)
        for i in range(5):
            t=(i+.015)/5
            m.box(p(t,9.72),(.34,6.36,.34),"timber")
            for low,high in ((.013,.187),(.187,.013)):
                m.beam(p(i/5+low,6.88),p(i/5+high,8.01),.14,"timber",.14)
    courtyard_roof(m,"JoinedSchoolCourtyardRoofs",OUTER,COURT,EAVE,17.06)


def stair_and_portal(m):
    start,end = -14.31,-3.22
    width,z = end-start,14.28
    m.feature("UpperGalleryStairStringers")
    for zz in (z-1.12,z+1.12):
        m.beam((start,.27,zz),(end,UPPER-.17,zz),.24,"timber",.39)
    m.feature("UpperGalleryStairTreads")
    for i in range(28):
        y=.22+(UPPER-.22)*(i+1)/28
        m.box((start+width*(i+.5)/28,y-.087,z),(width/28,.174,2.65),"wood")
    m.feature("StairAndLandingGuards")
    for zz in (z-1.34,z+1.34):
        m.beam((start,1.11,zz),(end,UPPER+1.12,zz),.14,"timber",.18)
        for i in (0,7,14,21,28):
            m.box((start+width*i/28,.77+(UPPER-.22)*i/28,zz),(.15,1.10,.15),"timber")
        m.beam((start,UPPER+1.12,zz),(end,UPPER+1.12,zz),.14,"timber",.17)
        for i in (0,7,14,21,28):
            m.box((start+width*i/28,UPPER+.52,zz),(.14,1.05,.14),"timber")

    front=OUTER[1]
    floor(m,"EntryThreshold",(-3.34,front-1.10,3.34,front),.22,"trim")
    m.feature("AttachedGatehousePilastersAndHeader")
    for x in (-3.32,3.32):
        m.box((x,4.8,front-.52),(.90,9.60,1.00),"trim")
        m.box((x,.46,front-.56),(1.18,.92,1.14),"trim")
    m.box((0,9.68,front-.55),(7.62,.39,1.13),"timber")
    gable(m,"GatehousePediment",(-3.72,front-1.13,3.72,front+.02),10.13,12.33,.27,wall_material="wall")
    m.markers["Entrance"]=(0,.22,front-1.1)
    m.landmark("SpiritEmblemOverGate")
    m.cylinder((0,7.77,front-.24),1.88,.48,"trim",axis=(0,0,1),n=32)
    m.cylinder((0,7.77,front-.51),1.72,.18,"medallion",axis=(0,0,1),n=32)
    swirl=[(-.08,-1.25),(-.62,-1.02),(-.94,-.63),(-.94,-.21),(-.55,.22),(.05,.52),
           (.31,.85),(.12,1.11),(-.17,1.29),(.33,1.16),(.68,.86),(.72,.52),(.50,.19),
           (-.05,-.12),(-.25,-.39),(-.17,-.69),(.14,-.67),(.22,-.44),(.49,-.62),
           (.46,-.90),(.22,-1.16)]
    outline(m,[(x,7.77+y) for x,y in swirl],front-.65,.16,"spiral")


def bough(m,points,radii,sides=14):
    nodes = [Vector(p) for p in points]
    rings = []
    for i,(p,radius) in enumerate(zip(nodes,radii)):
        axis = (nodes[min(i+1,len(nodes)-1)]-nodes[max(0,i-1)]).normalized()
        u=axis.cross((0,0,1)).normalized()
        v=u.cross(axis)
        rings.append([p+radius*(1+.085*cos(j*tau/sides*5+i*.94))*(u*cos(j*tau/sides)+v*sin(j*tau/sides))
                      for j in range(sides)])
    m.face(rings[0],"bark")
    for i,(a,b) in enumerate(zip(rings,rings[1:])):
        for j in range(sides):
            k=(j+1)%sides
            u,v=j/sides*2,(j+1)/sides*2
            y,h=(nodes[i]-nodes[0]).length*.36,(nodes[i+1]-nodes[0]).length*.36
            m.face([a[j],b[j],b[k],a[k]],"bark",[(u,y),(u,h),(v,h),(v,y)],smooth=True)
    m.face(list(reversed(rings[-1])),"bark")


def leaf_crown(m,xyz,size,phase,material):
    def p(a,b):
        depth=1+.10*sin(a*5+phase)*cos(b*3)+.062*cos(b*7+3*a)*cos(b)
        return (xyz[0]+depth*size[0]*cos(b)*cos(a),xyz[1]+depth*size[1]*sin(b),
                xyz[2]+depth*size[2]*cos(b)*sin(a))
    for row in range(8):
        a,b = (row/8-.5)*pi,((row+1)/8-.5)*pi
        for i in range(18):
            left,right = i*tau/18,(i+1)*tau/18
            u,v = i/6,(i+1)/6
            y,h = row/4,(row+1)/4
            if row==0:
                m.face([p(left,a),p(left,b),p(right,b)],material,[((u+v)/2,y),(u,h),(v,h)],smooth=True)
            elif row==7:
                m.face([p(left,a),p(left,b),p(right,a)],material,[(u,y),((u+v)/2,h),(v,y)],smooth=True)
            else:
                m.face([p(left,a),p(left,b),p(right,b),p(right,a)],material,[(u,y),(u,h),(v,h),(v,y)],smooth=True)


def courtyard_tree(m):
    x,z=-.34,1.48
    m.feature("RaisedRootBed")
    m.ring(x,z,4.77,5.13,.22,.63,"trim",n=44)
    m.cylinder((x,.39,z),4.78,.34,"soil",n=44)
    m.landmark("CourtyardTreeTrunk")
    bough(m,[(x,.43,z),(x-.04,1.50,z+.09),(x+.32,4.48,z-.09),(x-.25,8.94,z+.27),
             (x+.74,12.13,z+.07),(x+.40,16.38,z+.31),(x+.97,20.52,z-.09),(x+.74,22.75,z+.42)],
             (1.82,1.50,1.16,.97,.70,.49,.26,.08),20)
    for i in range(7):
        a=(i+.36)*tau/7
        bough(m,[(x+.73*cos(a),1.72,z+.77*sin(a)),(x+2.04*cos(a),.90,z+2.08*sin(a)),
                 (x+4.24*cos(a),.57,z+3.93*sin(a))],(.73,.41,.035),11)
    anchors = []
    for i in range(7):
        a=(i+.35)*tau/7
        spread = 5.25+.38*sin(i*3.6)
        y=12.66+(i%3)*1.23
        trunk = (x+.28,8.80+(i%3)*1.12,z+.12)
        knee = (x+3.28*cos(a),y,z+3.00*sin(a))
        fork = (x+spread*cos(a),y+2.65,z+.84*spread*sin(a))
        tip = (x+(spread+.84)*cos(a),y+6.14,z+(spread+.38)*.81*sin(a))
        m.feature("MainTreeBoughs")
        bough(m,[trunk,knee,fork,tip],(.72,.56,.32,.045))
        spur=(tip[0]-2.22*sin(a),tip[1]+.79,tip[2]+1.58*cos(a))
        bough(m,[knee,fork,spur],(.44,.25,.04))
        m.feature("TexturedLeafClusters")
        leaf_crown(m,tip,(2.73,1.96,2.41),a,"leaf" if i%2 else "leaflight")
        leaf_crown(m,spur,(2.42,1.75,2.32),1.38*a,"leaflight" if i%2 else "leaf")
        for j in (-1,1):
            angle=a+j*.68
            lobe=(tip[0]+2.09*cos(angle),tip[1]+.38+.36*j,tip[2]+2.09*sin(angle))
            leaf_crown(m,lobe,(1.16,.98,1.08),2.05*angle,"leaflight" if j>0 else "leaf")
        if sin(a)<-.20:
            anchors.append(Vector(knee).lerp(Vector(fork),.44))
    for j,(cx,y,cz) in enumerate(((-.36,21.73,.89),(.77,23.35,1.25),(2.03,21.93,3.31))):
        leaf_crown(m,(cx,y,cz),(3.03,2.22,2.49),1.2+j*.87,"leaflight" if j!=2 else "leaf")
    m.feature("JadeTokensOnBoughs")
    for i,(cx,y,cz) in enumerate(anchors):
        drop=1.16+(i%2)*.45
        m.beam((cx,y-.12,cz),(cx,y-drop,cz),.049,"wood")
        m.cylinder((cx,y-drop-.31,cz),.39,.62,"token",n=8,top=.07)
        m.cylinder((cx,y-drop-.83,cz),.038,.42,"token",n=8,top=.39)


def build(m):
    training_galleries(m)
    stair_and_portal(m)
    courtyard_tree(m)


def verify(objects):
    def tree(mesh):
        return BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
    roof = next(o.data for o in objects if o.name=="JoinedSchoolCourtyardRoofs_Spirit_ForestCeramic")
    roof_tree = tree(roof)
    for obj in objects:
        if obj.name.startswith(("Landmark_CourtyardTreeTrunk_","MainTreeBoughs_","TexturedLeafClusters_")):
            assert not roof_tree.overlap(tree(obj.data)),obj.name
        if obj.name.startswith(("SandstoneSchoolWallsFront_Spirit_HoneyStone","CourtyardStoneArcadesFront_Spirit_HoneyStone")):
            assert tree(obj.data).ray_cast((0,-19,1.7),(0,1,0),13)[0] is None
    return {"tree_clear_of_cloister_roof":True,"gateway_clear_width_m":5.32,"upper_gallery_stair":True}
