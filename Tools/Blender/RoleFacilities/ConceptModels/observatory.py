"""Round civic observatory, with a usable split dome and a mounted telescope."""

from math import asin, cos, pi, radians, sin

from mathutils import Vector
from mathutils.bvhtree import BVHTree

from components import Opening, pipe, round_wall


PALETTE = {
    "stone":"Observatory_DrumStone", "trim":"Observatory_Sandstone",
    "roof":"Observatory_IndigoMetal", "lens":"Observatory_ObjectiveLens",
}
MATERIALS = {
    "Observatory_DrumStone": dict(source="Fieldstone",color="82868d",relief=.22),
    "Observatory_Sandstone": dict(source="Limestone",color="b6a588",relief=.16),
    "Observatory_IndigoMetal": dict(source="Iron",color="384d6d",roughness=.51,metallic=.24,relief=.08),
    "Observatory_ObjectiveLens": dict(color="517e95",roughness=.12,metallic=.36),
}

DOME_RADIUS = 10.48
DOME_RISE = 9.83
DOME_BASE = 15.22
SLIT_WIDTH = 2.83
SLIT_END = radians(105)
CAMERA_DIRECTIONS = {"three_quarter":(.43,-1,.53)}


def dome(m):
    cut = asin(SLIT_WIDTH/DOME_RADIUS)
    sweep = [i*SLIT_END/12 for i in range(13)] + [SLIT_END+i*(pi-SLIT_END)/8 for i in range(1,9)]
    sections = [
        ("EastShutter",cut,pi/2,10,0),
        ("WestShutter",-pi/2,-cut,10,0),
        ("RearDomeBridge",-cut,cut,6,12),
    ]

    def point(a,t,depth=0):
        return Vector(((DOME_RADIUS-depth)*sin(a),DOME_BASE+(DOME_RISE-depth)*cos(a)*sin(t),
                       -(DOME_RADIUS-depth)*cos(a)*cos(t)))

    def panel(a,b,s,t,depth,material,inside=False):
        coords = [point(a,s,depth),point(a,t,depth),point(b,t,depth),point(b,s,depth)]
        uv = [(x*DOME_RADIUS/4.6,y*DOME_RADIUS/4.6) for x,y in ((a,s),(a,t),(b,t),(b,s))]
        if inside:
            coords.reverse()
            uv.reverse()
        m.face(coords,material,uv,smooth=True)

    for label,a,b,n,start in sections:
        m.feature(label)
        across = [a+(b-a)*i/n for i in range(n+1)]
        for left,right in zip(across,across[1:]):
            for near,far in zip(sweep[start:],sweep[start+1:]):
                panel(left,right,near,far,0,"roof")
                panel(left,right,near,far,.29,"timber",True)
            for t in sweep[start],sweep[-1]:
                quad=[point(left,t),point(right,t),point(right,t,.29),point(left,t,.29)]
                m.face(quad if t==pi else quad[::-1],"metal")

    m.feature("ContinuousShutterEdging")
    for a in (-cut,cut):
        for near,far in zip(sweep[:12],sweep[1:13]):
            quad=[point(a,near),point(a,far),point(a,far,.29),point(a,near,.29)]
            m.face(quad if a>0 else quad[::-1],"metal")
    m.feature("StandingSeams")
    for a in (-.83,-.50,-cut,cut,.50,.83):
        for near,far in zip(sweep,sweep[1:]):
            m.beam(point(a,near,-.034),point(a,far,-.034),.075,"roof",.070)


def telescope(m):
    pivot = Vector((0,18.30,-1.72))
    axis = Vector((-.125,.485,-.869)).normalized()
    m.feature("AzimuthTurntable")
    m.cylinder((0,15.49,-1.72),1.80,.40,"metal",n=32,top=1.67)
    m.cylinder((0,15.91,-1.72),1.33,.44,"bronze",n=28,top=1.15)
    m.feature("PierAndPivotFork")
    m.box((0,16.235,-1.72),(2.14,.21,1.24),"metal")
    for side in (-1,1):
        foot,head = (side*.97,16.28,-1.57),(side*.88,18.34,-1.72)
        m.beam(foot,head,.43,"metal",.45)
        m.cylinder((side*.92,18.30,-1.72),.49,.48,"bronze",n=22,axis=(1,0,0))
    m.cylinder(pivot,.34,2.24,"metal",n=24,axis=(1,0,0))
    m.landmark("WorkingTelescope")
    profile = [(-2.06,0),(-2.06,.39),(-1.84,.45),(-.22,.45),(-.01,.60),(2.6,.60),
               (2.79,.72),(5.78,.72),(5.98,.90),(9.46,.90),(9.62,1.07),(9.82,1.07),
               (9.82,.81),(9.49,.81)]
    pipe(m,pivot,axis,profile,"bronze",28)
    for a,radius in ((-.12,.60),(2.58,.72),(5.78,.86)):
        pipe(m,pivot,axis,[(a-.075,radius-.16),(a-.075,radius+.035),
                          (a+.09,radius+.035),(a+.09,radius-.16)],"metal",28)
    pipe(m,pivot,axis,[(9.47,0),(9.47,.811),(9.51,.811),(9.55,.46),(9.56,0)],"lens",32)


def build(m):
    m.feature("GroundedFoundation")
    m.cylinder((0,.135,0),13.44,.27,"trim",n=32)
    round_wall(m,"PublicRingHall",13.30,.27,5.00,.64,"trim",lambda i,c:
               (Opening(c,.27,2.10,2.86,.62,"door",.19),) if i==0 else
               (Opening(c,1.13,1.47,2.23,.34),) if min(i,32-i) in (3,7,11,15) else ())
    m.feature("TerraceFloorAndCornice")
    m.ring(0,0,9.72,13.43,4.99,5.28,"trim",n=32,start=-pi/2-pi/32,end=3*pi/2-pi/32)
    round_wall(m,"SurveyDrum",10.35,5.26,15.08,.62,"stone",lambda i,c:
               (Opening(c,6.26,1.35,1.85),Opening(c,10.22,1.66,3.53,.76)) if i%4==0 else ())
    m.feature("SurveyDrumBelt")
    m.ring(0,0,10.20,10.46,9.12,9.45,"trim",n=32,start=-pi/2-pi/32,end=3*pi/2-pi/32)
    m.feature("DomeBearingRing")
    m.ring(0,0,9.64,10.55,14.89,15.22,"trim",n=48)
    m.ring(0,0,9.98,10.53,15.215,15.30,"metal",n=48)
    m.feature("ObservationDeck")
    m.cylinder((0,15.17,0),10.03,.24,"wood",n=48)
    m.feature("ThresholdSteps")
    m.box((0,.066,-13.49),(2.66,.132,.74),"trim")
    m.box((0,.200,-13.38),(2.52,.136,.49),"trim")
    m.markers["Entrance"]=(0,.268,-13.87)
    dome(m)
    telescope(m)


def verify(objects):
    mesh_objects = [o for o in objects if o.type=="MESH"]
    grounded = [o for o in mesh_objects if o.name.startswith(("PublicRingHall_","SurveyDrum_"))]
    for obj in grounded:
        points = {tuple(round(x,4) for x in v.co) for v in obj.data.vertices}
        assert all((-x,y,z) in points for x,y,z in points), obj.name
    shutter_parts = [o for o in mesh_objects if o.name.startswith(
        ("EastShutter_","WestShutter_","RearDomeBridge_","ContinuousShutterEdging_","StandingSeams_"))]
    instruments = [o for o in mesh_objects if o.name.startswith("Landmark_WorkingTelescope_")]
    for shell in shutter_parts:
        tree = BVHTree.FromPolygons([v.co for v in shell.data.vertices],
                                    [p.vertices[:] for p in shell.data.polygons])
        for tube in instruments:
            optic = BVHTree.FromPolygons([v.co for v in tube.data.vertices],
                                        [p.vertices[:] for p in tube.data.polygons])
            assert not tree.overlap(optic), (shell.name,tube.name)
        hit,_,_,_ = tree.ray_cast((0,-13,19.4),(0,1,0),13)
        assert hit is None, shell.name
    return {"survey_tower_symmetry":"verified", "dome_aperture_m":2*SLIT_WIDTH,
            "telescope_shell_intersections":0}
