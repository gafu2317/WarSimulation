"""Open forge hall with a working clerestory and a chimney over the hearth."""

from math import cos, pi, sin

from mathutils.bvhtree import BVHTree

from components import Opening, floor, gable, rectangle_walls, slab, wall
from landmarks import outline
from materials import artwork


PALETTE = {
    "roof":"Forge_BlackenedCeramic", "wall":"Forge_SmokedLimewash", "stone":"Forge_GraniteRubble",
    "trim":"Forge_DressedGranite", "brick":"Forge_FiredBrick", "coal":"Forge_GlowingCoalBed",
    "metal":"Forge_BurnishedCastIron",
}
MATERIALS = {
    "Forge_SmokedLimewash":dict(source="Plaster",color="b8a285",relief=.18),
    "Forge_GraniteRubble":dict(source="Fieldstone",color="8a8175",relief=.22),
    "Forge_DressedGranite":dict(source="Limestone",color="a99b85",relief=.19),
    "Forge_FiredBrick":dict(source="Brick",color="a26246",relief=.30),
    "Forge_BurnishedCastIron":dict(source="Iron",color="6e6862",roughness=.48,metallic=.40,relief=.17),
}

HALL = (-9.8,-12.8,9.8,13.2)
CHIMNEY = (4.95,1.15)
HALF_FLUE = 1.45
RIDGE, EAVE = 17.42,11.23
CAMERA_DIRECTIONS = {"three_quarter":(.177,-1,.37),"rear":(.65,1,.69)}


def workshop_shell(m):
    left,front,right,back=HALL
    floor(m,"WorkingHallPaving",HALL,.23,"paving")
    side=[Opening(c,.23,2.16,4.30,.40,None,.13) for c in (5.45,20.46)]
    rectangle_walls(m,"WorkHallMasonry",HALL,.23,EAVE-.26,.62,"stone",{
        "Front":[Opening(-left,.23,17.92,9.58,.34,None,0)],"East":side,"West":side,
        "Rear":[Opening(c,2.68,2.29,4.51,.61,"warm_glass") for c in (3.24,7.62,12.00,16.37)]})
    m.feature("TimberPortalAndTies")
    m.beam((left+.48,10.01,front-.09),(right-.48,10.01,front-.09),.59,"timber",.62)
    for x in (-8.69,8.69):
        m.box((x,5.32,front-.06),(.63,9.02,.68),"wood")
        m.box((x,.52,front-.05),(.94,1.04,.98),"trim")
        m.beam((x,7.38,front-.05),(x*.752,9.79,front-.05),.40,"timber",.43)
    for z in (front,back):
        m.feature("WorkshopGableInfill")
        coords=[(left,EAVE-.26,z),(0,RIDGE-.26,z),(right,EAVE-.26,z)]
        m.face(coords if z==front else list(reversed(coords)),"wall")
        zz=z-.11 if z==front else z+.11
        m.feature("WorkshopGableFrame")
        m.beam((left+1.58,11.68,zz),(right-1.58,11.68,zz),.28,"timber",.37)
        m.beam((0,12.01,zz),(0,16.98,zz),.36,"wood",.33)
        for s in (-1,1):
            m.beam((s*9.25,EAVE-.09,zz),(0,RIDGE-.51,zz),.31,"wood",.33)
            m.beam((0,14.23,zz),(s*4.42,14.27,zz),.21,"wood",.28)
    m.markers["Entrance"]=(0,.23,front)


def roof_and_vent(m):
    left,front,right,back=HALL
    cx,cz=CHIMNEY
    monitor=2.23
    near,far=front+2.76,back-2.33
    x_cuts=[left-.51,-monitor,0,monitor,cx-HALF_FLUE,cx+HALF_FLUE,right+.51]
    z_cuts=[front-.51,near,cz-HALF_FLUE,cz+HALF_FLUE,far,back+.51]
    def p(x,z):
        return x,RIDGE-(RIDGE-EAVE)*abs(x)/right,z
    for a,b in zip(x_cuts,x_cuts[1:]):
        for f,r in zip(z_cuts,z_cuts[1:]):
            if (a>=-monitor and b<=monitor and f>=near and r<=far) or (a==cx-HALF_FLUE and f==cz-HALF_FLUE):
                continue
            ends = {i for i,test in enumerate((f==z_cuts[0],b==x_cuts[-1],r==z_cuts[-1],a==x_cuts[0])) if test}
            slab(m,"MainRoofWithFlueAndClerestoryOpenings",[p(a,f),p(b,f),p(b,r),p(a,r)],.26,borders=ends)
    bounds=(-monitor,near,monitor,far)
    bottom=p(monitor,near)[1]-.26
    windows=[Opening((far-near)*(i+.5)/6,bottom+.36,2.08,1.44,.0,"clear_glass",.08) for i in range(6)]
    rectangle_walls(m,"ClerestoryWalls",bounds,bottom,18.40,.21,"wall",{"West":windows,"East":windows})
    gable(m,"ClerestoryRoof",bounds,18.66,20.81,.29)
    m.feature("ClerestoryOakFraming")
    for x in (-monitor,monitor):
        for i in range(7):
            z=near+(far-near)*i/6
            m.box((x,(bottom+18.40)/2,z),(.23,18.40-bottom,.24),"timber")


def side_stores(m):
    left,front,right,back=HALL
    for side in (-1,1):
        edge=right*side
        x_end=16.20*side
        low,high=sorted((edge,x_end))
        label="FuelStore" if side>0 else "IronAndToolStore"
        bounds=(low,front+.16,high,back)
        floor(m,label+"Floor",bounds,.23,"paving")
        north=[Opening(x,1.37,1.69,3.54,.63,"warm_glass") for x in (1.88,4.52)]
        a,b=(x_end,front+.16),(x_end,back)
        if side<0:
            a,b=b,a
        wall(m,label+"OuterWall",a,b,.23,7.34,.51,"stone",
             [Opening(c,2.0,2.15,3.87,.62,"warm_glass") for c in (3.52,9.72,15.98,22.07)],.51)
        for z in (front+.16,back):
            a,b=((low,z),(high,z)) if z<0 else ((high,z),(low,z))
            limits=(0,.51) if (side>0)==(z<0) else (.51,0)
            wall(m,label+"EndWall",a,b,.23,7.34,.51,"brick" if side>0 else "stone",north,limits)
            m.feature(label+"SlopeEndClosure")
            points=[(x_end,7.34,z),(edge,7.34,z),(edge,10.40,z)]
            m.face(points if (side>0)==(z>0) else list(reversed(points)),"stone")
        slab(m,label+"SingleSlopeRoof",[(x_end+side*.40,7.37,front-.12),(x_end+side*.40,7.37,back+.35),
             (edge,10.66,back+.35),(edge,10.66,front-.12)],.26,borders={0,1,3})


def chimney_and_hearth(m):
    x,z=CHIMNEY
    front=z-2.95
    width=5.30
    opening=Opening(width/2,1.04,3.76,3.38,1.04,None,.25)
    wall(m,"FireboxArch",(x-width/2,front),(x+width/2,front),.23,5.06,.52,"brick",[opening])
    m.feature("FirebrickWallsAndHearth")
    for sign in (-1,1):
        m.box((x+sign*2.18,2.60,z-.22),(.49,4.74,4.67),"brick")
    m.box((x,2.60,z+1.88),(4.39,4.74,.45),"brick")
    m.box((x,.64,z-.42),(4.88,.80,4.48),"stone")
    m.feature("EmberGrateWithCoalTexture")
    m.face([(x-1.76,1.045,front+.23),(x-1.76,1.045,z+1.64),
            (x+1.76,1.045,z+1.64),(x+1.76,1.045,front+.23)],"coal",[(0,0),(0,1),(1,1),(1,0)])
    flames=[(-1.73,1.06),(1.73,1.06),(1.49,1.67),(1.18,1.63),(.93,2.0),(.50,1.74),
            (.29,2.32),(-.09,1.89),(-.40,2.13),(-.65,1.68),(-1.03,1.85),(-1.46,1.45)]
    m.face([(x+a,y,z+1.634) for a,y in flames],"coal",[(a/3.55+.5,(y-1.06)/1.32) for a,y in flames])
    def square(y,r,skirt=False):
        face = [(x-r,y,z-r),(x+r,y,z-r),(x+r,y,z+r),(x-r,y,z+r)]
        if skirt:
            face = [(a,b,c-.52 if c<z else c+.30) for a,b,c in face]
        return face
    def seams(label,profile,material):
        m.feature(label)
        for a,b in zip(profile,profile[1:]):
            for i in range(4):
                j=(i+1)%4
                m.face([a[i],b[i],b[j],a[j]],material)
    seams("ForgeHood",[square(4.89,2.32,True),square(5.27,2.27,True),square(7.28,1.31)],"metal")
    seams("BrickExhaustStack",[square(7.27,1.30),square(9.38,1.45),square(15.90,1.45),
          square(21.05,1.18),square(23.90,1.11)],"brick")
    seams("ChimneyCopingAndOpenFlue",[square(23.62,1.11),square(23.72,1.26),square(24.02,1.29),
          square(24.15,1.24),square(24.15,.76),square(22.98,.76)],"trim")
    seams("ChimneyShoulderBelt",[square(15.76,1.47),square(15.76,1.56),square(16.08,1.56),square(16.12,1.44)],"trim")


def anvil(m,x,y,z,scale,depth):
    profile=[(-1.01,0),(-1.15,.25),(-.49,.36),(-.39,.65),(-.68,1.0),(-1.18,1.20),
             (-1.68,1.56),(-1.91,1.86),(.86,1.86),(1.82,2.0),(1.75,1.69),(1.35,1.30),
             (.65,1.02),(.42,.77),(.55,.39),(.99,.20),(.99,0),(.49,0),(.23,.16),(-.30,.16),(-.58,0)]
    outline(m,[(x+a*scale,y+b*scale) for a,b in profile],z,depth,"metal")


def fixtures(m):
    m.feature("AnvilAndStoneWorkingStand")
    m.box((-1.6,.59,-5.42),(2.63,.72,1.55),"trim")
    anvil(m,-1.6,.95,-5.42,.75,.83)
    m.feature("WorkbenchAndToolRack")
    for x in (-5.87,6.75):
        m.box((x,1.24,-2.75),(1.36,.23,3.82),"wood")
        for dz in (-1.47,1.47):
            m.box((x,.68,-2.75+dz),(1.0,.91,.25),"timber")
    front=HALL[1]
    m.feature("SignMountingPlate")
    m.box((0,12.57,front-.15),(4.54,1.11,.32),"wood")
    m.landmark("CastAnvilSign")
    anvil(m,0,11.91,front-.417,1.46,.28)


def materials():
    def embers(u,v):
        hot=.59+.28*cos(pi*(u-.5))*cos(pi*.68*v)-.18*v
        seams=.24*sin(78*u+13*sin(19*v))*sin(56*v+16*sin(17*u))
        grain=.083*sin(301*u+76*sin(66*v))*sin(231*v)
        heat=hot+seams+grain
        return max(0,min(4,int(heat*4.35)))
    return {**MATERIALS,"Forge_GlowingCoalBed":artwork("Forge_GlowingCoalBed",embers,
         ("4e241c","9f351b","e06929","e99b38","f9ce68"),width=384,height=256,emittance=1.7)}


def build(m):
    workshop_shell(m)
    roof_and_vent(m)
    side_stores(m)
    chimney_and_hearth(m)
    fixtures(m)


def verify(objects):
    mesh=next(o.data for o in objects if o.name=="MainRoofWithFlueAndClerestoryOpenings_Forge_BlackenedCeramic")
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
    for x,z in ((0,0),CHIMNEY):
        assert tree.ray_cast((x,z,27),(0,0,-1),18)[0] is None
    assert tree.ray_cast((3.0,1.1,27),(0,0,-1),18)[0] is not None
    mesh=next(o.data for o in objects if o.name=="ChimneyCopingAndOpenFlue_Forge_DressedGranite")
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
    assert tree.ray_cast((*CHIMNEY,26),(0,0,-1),7)[0] is None
    return {"open_chimney_coping":True,"main_roof_cuts_for_stack_and_clerestory":True}
