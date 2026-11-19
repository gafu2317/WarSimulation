"""A corner salon with an angled entry and furnished projecting booths."""

from math import atan2, cos, sin, tan, tau

from mathutils import Vector
from mathutils.bvhtree import BVHTree

from components import Opening, convex_hip, pipe, slab, wall


PALETTE = {
    "wall":"Salon_OxideBrick", "roof":"Salon_CharcoalSlate", "trim":"Salon_CreamStone",
    "bay_roof":"Salon_SmokyZinc", "panel":"Salon_BottleGreenOak", "mask":"Salon_Pewter",
    "lining":"Salon_WarmOakPaneling",
    "rose":"Salon_DamaskRose", "moss":"Salon_MossDamask", "navy":"Salon_IndigoDamask",
}
MATERIALS = {
    "Salon_OxideBrick":dict(source="Brick",color="a45d48",relief=.27),
    "Salon_CreamStone":dict(source="Limestone",color="c2aa8c",relief=.15),
    "Salon_SmokyZinc":dict(source="Copper",color="655c59",metallic=.34,relief=.10),
    "Salon_BottleGreenOak":dict(source="Oak",color="294642",relief=.13),
    "Salon_WarmOakPaneling":dict(source="Oak",color="a87649",relief=.14,emittance=.14),
    "Salon_Pewter":dict(source="Iron",color="b4b8bc",metallic=.55,roughness=.38,relief=.07),
    "Salon_DamaskRose":dict(source="Canvas_Red",color="ac6a70",relief=.16,emittance=.09),
    "Salon_MossDamask":dict(source="Canvas_Green",color="87945a",relief=.16,emittance=.09),
    "Salon_IndigoDamask":dict(source="Canvas_Blue",color="5b8197",relief=.16,emittance=.09),
}

PLAN = [(-15,-12),(9.20,-12),(15,-6.20),(15,12),(-15,12)]
EAVES = 15.96
CAMERA_DIRECTIONS = {"three_quarter":(.78,-1,.50),"front":(.94,-1,.19)}


def along(a,b):
    delta = Vector(b)-Vector(a)
    u,v = delta.normalized()

    def map_point(span,height,inset):
        return (a[0]+u*span-v*inset,height,a[1]+v*span+u*inset)

    return map_point,atan2(-v,u),delta.length


def mask(m,a,b):
    p,yaw,length = along(a,b)
    c = length/2
    m.feature("MaskPanelAndBrackets")
    m.box(p(c,8.40,-.07),(6.15,2.86,.44),"trim",yaw)
    m.box(p(c,8.40,-.30),(5.75,2.48,.17),"panel",yaw)
    m.landmark("PewterMask")
    for side in (-1,1):
        def loop(i,inside,inset):
            t = tau*i/20
            x = 1.24+(1.28 if not inside else .87)*cos(t)
            y = .07+(.91 if not inside else .42)*sin(t)*(.87-.16*cos(t))+.28*cos(t)
            return p(c+side*x,8.38+y,inset)
        for i in range(20):
            j = (i+1)%20
            faces = ([loop(i,False,-.62),loop(j,False,-.62),loop(j,True,-.66),loop(i,True,-.66)],
                     [loop(j,False,-.62),loop(i,False,-.62),loop(i,False,-.365),loop(j,False,-.365)],
                     [loop(i,True,-.66),loop(j,True,-.66),loop(j,True,-.365),loop(i,True,-.365)])
            for face in faces:
                m.face(face,"mask")
    m.box(p(c,8.29,-.49),(.98,.47,.32),"mask",yaw)


def bay(m,a,b,center,width,label,fabric):
    p,yaw,_ = along(a,b)
    left,right = center-width/2,center+width/2
    tip,back = -1.48,.015
    m.feature(label+"PlinthAndParapet")
    m.box(p(center,.18,(tip+back)/2),(width,.36,back-tip),"trim",yaw)
    m.box(p(center,.82,tip+.13),(width,.92,.26),"timber",yaw)
    side_start = tip+.26
    for edge in (left+.11,right-.11):
        m.box(p(edge,.82,(side_start+back)/2),(.22,.92,back-side_start),"timber",yaw)
    m.box(p(center,1.33,tip+.11),(width+.11,.20,.26),"wood",yaw)
    m.feature(label+"WindowAndTimberJambs")
    columns = [left+width*i/4 for i in range(5)]
    for span in columns:
        m.box(p(span,3.92,tip+.15),(.16,5.32,.23),"timber",yaw)
    for height in (5.52,6.53):
        m.box(p(center,height,tip+.14),(width+.10,.16,.22),"timber",yaw)
    def lens(u,v,u_end,v_end):
        face = [p(u,1.38,v),p(u,6.54,v),p(u_end,6.54,v_end),p(u_end,1.38,v_end)]
        m.face(face,"clear_glass")
    lens(left,tip+.17,right,tip+.17)
    for span in (left,right):
        lens(span,tip+.17,span,.0)
        m.box(p(span,3.92,-.04),(.22,5.31,.21),"timber",yaw)
        m.box(p(span,5.52,(tip+back)/2),(.16,.16,back-tip),"timber",yaw)
        face = [p(span,6.48,tip+.12),p(span,6.77,tip+.12),p(span,7.62,.07),p(span,6.48,.07)]
        m.face(face if span==left else list(reversed(face)),"timber")
    slab(m,label+"StandingSeamRoof",[p(left-.18,6.79,tip-.15),p(right+.18,6.79,tip-.15),
         p(right+.18,7.86,.08),p(left-.18,7.86,.08)],.22,"bay_roof")
    m.feature(label+"RoofEdgeFlashing")
    m.beam(p(left-.18,7.84,.065),p(right+.18,7.84,.065),.17,"metal",.18)
    for span in columns:
        m.beam(p(span,6.81,tip-.19),p(span,7.87,.075),.054,"bay_roof",.045)
    m.feature(label+"ConversationBooth")
    m.box(p(center,.46,.55),(width-.17,.20,3.83),"wood",yaw)
    m.box(p(center,2.11,2.36),(width-.16,3.11,.21),"lining",yaw)
    for position in (left+.78,right-.78):
        sign = -1 if position<center else 1
        m.box(p(position,.84,.28),(1.08,.58,2.22),"timber",yaw)
        m.box(p(position,.99,.28),(1.03,.27,2.25),fabric,yaw)
        m.box(p(position+sign*.43,1.51,.28),(.25,.88,2.25),fabric,yaw)
        for v in (-.79,1.34):
            m.box(p(position,1.18,v),(1.13,.56,.17),fabric,yaw)
    m.cylinder(p(center,1.26,.38),.77,.14,"lining",n=20)
    m.cylinder(p(center,.90,.38),.13,.62,"timber",n=12)


def build(m):
    edges = list(zip(PLAN,PLAN[1:]+PLAN[:1]))
    angles = [along(a,b)[1] for a,b in edges]
    miters = [.64*tan((angles[i-1]-angle)%tau*.5) for i,angle in enumerate(angles)]
    fixtures = {0:((6.04,7.63,"rose"),(18.10,7.59,"moss")),
                2:((4.64,5.86,"navy"),(13.62,5.82,"rose"))}
    slab(m,"SalonMasonrySlab",[(x,.27,z) for x,z in PLAN],.27,"trim",underside="stone")
    for i,(a,b) in enumerate(edges):
        p,yaw,length = along(a,b)
        bays = fixtures.get(i,())
        centers = ([c+k for c,w,_ in bays for k in (-w*.235,w*.235)] if bays else
                   [length*k for k in ((.50,) if i==1 else (.20,.40,.60,.80))])
        upper = [Opening(c,10.35,1.78,3.94,.81,"warm_glass",.18) for c in centers]
        lower = [Opening(c,.27,w-.47,7.05,.0,None,0) for c,w,_ in bays]
        if i==1:
            lower = [Opening(length/2,.27,3.72,5.63,1.18,"door",.33)]
            m.markers["Entrance"] = p(length/2,.27,-.52)
            m.feature("CornerEntranceStep")
            m.box(p(length/2,.135,-.27),(4.73,.27,.54),"trim",yaw)
        elif not bays:
            lower = [Opening(c,2.67,1.78,3.94,.81,"warm_glass",.18) for c in centers]
        wall(m,"BrickElevation"+str(i),a,b,.27,EAVES-.25,.64,"wall",[*lower,*upper],
             (miters[i],miters[(i+1)%len(miters)]))
        m.feature("ContinuousCornice")
        m.beam(p(.14,15.37,-.06),p(length-.14,15.37,-.06),.57,"trim",.42)
        m.feature("BrickPilasters")
        pilasters = (.55,length-.55)
        if bays:
            pilasters += ((bays[0][0]+bays[1][0])/2,)
        for c in pilasters:
            m.box(p(c,7.91,-.036),(.72,14.66,.44),"wall",yaw)
            m.box(p(c,.66,-.039),(1.0,1.32,.60),"trim",yaw)
            m.box(p(c,14.38,-.051),(.91,.32,.58),"trim",yaw)
        for j,(c,w,fabric) in enumerate(bays):
            bay(m,a,b,c,w,f"ProjectingBay{i}_{j}",fabric)
    mask(m,*edges[1])
    convex_hip(m,"ChamferedSlateHipRoof",PLAN,EAVES,21.88,.43)
    m.feature("SharedHearthChimney")
    m.box((-6.88,10.6,6.77),(2.85,20.68,1.41),"wall")
    m.box((-6.88,20.92,6.77),(3.10,.38,1.68),"trim")
    for x in (-7.70,-6.88,-6.06):
        pipe(m,(x,21.10,6.77),(0,1,0),[(0,.28),(.92,.254),(1.02,.28),(1.03,.21),(.03,.20)],"wall",18)


def verify(objects):
    roof = next(o.data for o in objects if o.name=="ChamferedSlateHipRoof_Salon_CharcoalSlate")
    tree = BVHTree.FromPolygons([v.co for v in roof.vertices],[p.vertices[:] for p in roof.polygons])
    assert all(tree.ray_cast((x,z,25),(0,0,-1),12)[0] is not None for x,z in ((0,0),(12,-8),(13.3,-7.5)))
    assert tree.ray_cast((14.7,-11.7,25),(0,0,-1),12)[0] is None
    assert len([o for o in objects if o.name.startswith("ProjectingBay") and o.name.endswith("Clear_Glazing")])==4
    return {"roof_follows_chamfer":True,"furnished_window_bays":4}
