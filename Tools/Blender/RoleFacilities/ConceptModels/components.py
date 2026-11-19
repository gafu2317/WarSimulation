"""Components measured in metres using X right, Y up and a -Z front."""

from dataclasses import dataclass
from math import cos, hypot, pi, sin, sqrt, tan, tau

from mathutils import Vector


@dataclass(frozen=True)
class Opening:
    center: float
    bottom: float
    width: float
    height: float
    arch: float = 0
    fill: str | None = "glass"
    rim: float = .16

    def crown(self, u):
        if not self.arch:
            return self.bottom + self.height
        t = (u - self.center) / (self.width / 2)
        return self.bottom + self.height - self.arch + self.arch * sqrt(max(0, 1 - t*t))

    def head(self):
        half = self.width / 2
        if self.arch:
            return [self.center - half*cos(pi*i/10) for i in range(11)]
        return [self.center - half, self.center + half]

    def outline(self):
        head = self.head()
        return [(head[0], self.bottom), (head[-1], self.bottom),
                *((u, self.crown(u)) for u in reversed(head))]


def wall(m, label, start, end, base, top, depth, material="wall", openings=(), miter=0):
    """Build a wall with recessed infill and continuous reveals at real apertures."""
    m.feature(label)
    x,z = start
    dx,dz = end[0]-x,end[1]-z
    length = hypot(dx,dz)
    ux,uz = dx/length,dz/length
    start_miter,end_miter = miter if isinstance(miter,tuple) else (miter,miter)
    middles = [(opening,opening.head()) for opening in openings]
    assert all(0 < head[0] < head[-1] < length and
               base <= opening.bottom < opening.bottom+opening.height < top
               for opening,head in middles), label

    def point(u, y, inset):
        if abs(u)<1e-7:
            u += start_miter*inset/depth
        elif abs(u-length)<1e-7:
            u -= end_miter*inset/depth
        return x+ux*u-uz*inset,y,z+uz*u+ux*inset

    def face(uvs, inset, inward=False):
        coords = [point(u,y,inset) for u,y in uvs]
        # An unbroken texture direction keeps masonry coherent around openings.
        offset = x*ux+z*uz
        scale = 2.6 if material in ("stone","trim","brick") else 3.3
        uv = [((u+offset)/scale,y/scale) for u,y in uvs]
        if inward:
            coords.reverse()
            uv.reverse()
        m.face(coords,material,uv)

    cuts = sorted({0,length,*(u for _,head in middles for u in head)})
    for a,b in zip(cuts,cuts[1:]):
        mid = (a+b)/2
        holes = sorted((o for o,head in middles if head[0]<mid<head[-1]),key=lambda o:o.bottom)
        left,right = base,base
        for opening in [*holes,None]:
            limit = opening.bottom if opening else top
            assert min(limit-left,limit-right)>=-1e-6, label
            if min(limit-left,limit-right)>1e-6:
                solid = [(a,left),(a,limit),(b,limit),(b,right)]
                face(solid,0)
                face(solid,depth,True)
            if opening:
                left,right=opening.crown(a),opening.crown(b)
        if not holes or holes[0].bottom>base+1e-6:
            m.face([point(a,base,0),point(b,base,0),
                    point(b,base,depth),point(a,base,depth)],material)
        m.face([point(a,top,0),point(a,top,depth),
                point(b,top,depth),point(b,top,0)],material)
    if not start_miter:
        m.face([point(0,base,0),point(0,base,depth),point(0,top,depth),point(0,top,0)],material)
    if not end_miter:
        m.face([point(length,base,0),point(length,top,0),
                point(length,top,depth),point(length,base,depth)],material)

    for opening in openings:
        rim = opening.outline()
        for (u,y),(v,height) in zip(rim,rim[1:]+rim[:1]):
            if max(y,height)<=base+1e-6:
                continue
            m.face([point(u,y,0),point(u,y,depth),point(v,height,depth),point(v,height,0)],material)
        m.feature(label+"_Openings")
        if opening.rim:
            lip = opening.rim
            below = lip if opening.bottom-base>=lip else 0
            border = Opening(opening.center,opening.bottom-below,opening.width+2*lip,
                             opening.height+lip+below,opening.arch+lip if opening.arch else 0).outline()
            for i,j in zip(range(len(rim)),[*range(1,len(rim)),0]):
                u,y = rim[i]
                v,height = rim[j]
                if not below and max(y,height)<=base+1e-6:
                    continue
                a,lo = border[i]
                b,hi = border[j]
                m.face([point(u,y,-.09),point(v,height,-.09),point(b,hi,-.09),point(a,lo,-.09)],"trim")
                m.face([point(a,lo,-.09),point(b,hi,-.09),point(b,hi,.025),point(a,lo,.025)],"trim")
                m.face([point(v,height,-.09),point(u,y,-.09),point(u,y,0),point(v,height,0)],"trim")
        if opening.fill:
            back = min(.20,depth*.4)
            uv = [((u-opening.center)/opening.width+.5,(y-opening.bottom)/opening.height) for u,y in rim]
            m.face([point(u,y,back) for u,y in reversed(rim)],opening.fill,list(reversed(uv)))
            m.face([point(u,y,back+.05) for u,y in rim],opening.fill,uv)
            if opening.fill in ("glass","warm_glass","clear_glass"):
                def bar(u,lo,v,hi):
                    m.beam(point(u,lo,back-.027),point(v,hi,back-.027),.085,"timber",.070)
                half = opening.width/2
                rail = opening.bottom+opening.height*.53
                bar(opening.center,opening.bottom,opening.center,opening.crown(opening.center))
                bar(opening.center-half,rail,opening.center+half,rail)
        m.feature(label)


def rectangle_walls(m,label,bounds,base,top,depth,material="wall",openings=None,inward=False):
    left,front,right,back = bounds
    corners = [(left,front),(right,front),(right,back),(left,back)]
    for side,a,b in zip(("Front","East","Rear","West"),corners,corners[1:]+corners[:1]):
        start,end = (b,a) if inward else (a,b)
        wall(m,label+side,start,end,base,top,depth,material,(openings or {}).get(side,()),
             -depth if inward else depth)


def round_wall(m,label,radius,base,top,depth,material,openings,n=32,center=(0,0)):
    x,z = center
    corners = [(x+radius*sin((i-.5)*tau/n),z-radius*cos((i-.5)*tau/n)) for i in range(n)]
    panel = 2*radius*sin(pi/n)
    for i,a,b in zip(range(n),corners,corners[1:]+corners[:1]):
        wall(m,label,a,b,base,top,depth,material,openings(i,panel*.5),depth*tan(pi/n))


def floor(m,label,bounds,level=.18,material="paving",bottom=0):
    left,front,right,back = bounds
    m.feature(label)
    m.box(((left+right)/2,(bottom+level)/2,(front+back)/2),
          (right-left,level-bottom,back-front),material)


def slab(m, label, top, thickness, material="roof", underside="timber", borders=None):
    """Clad an upward-facing polygon and give its ceiling and exposed edges thickness."""
    m.feature(label)
    if (Vector(top[1])-Vector(top[0])).cross(Vector(top[2])-Vector(top[0])).y<0:
        top = list(reversed(top))
        if borders is not None:
            borders = {len(top)-2-i if i<len(top)-1 else i for i in borders}
    lower = [(x,y-thickness,z) for x,y,z in top]
    m.face(top,material)
    m.face(list(reversed(lower)),underside)
    for i in range(len(top)) if borders is None else borders:
        j = (i+1)%len(top)
        m.face([top[j],top[i],lower[i],lower[j]],underside)


def gable(m,label,bounds,eave,ridge,overhang=.36,wall_material="wall",roof_material="roof"):
    left,front,right,back = bounds
    middle = (left+right)/2
    y_edge = eave-(ridge-eave)*overhang/(middle-left)
    f,b = front-overhang,back+overhang
    slab(m,label,[(left-overhang,y_edge,f),(left-overhang,y_edge,b),(middle,ridge,b),(middle,ridge,f)],
         .26,roof_material,borders={0,1,3})
    slab(m,label,[(middle,ridge,f),(middle,ridge,b),(right+overhang,y_edge,b),(right+overhang,y_edge,f)],
         .26,roof_material,borders={1,2,3})
    m.feature(label+"Gables")
    at_wall = ridge-.26
    for z in (front,back):
        profile = [(left,eave-.26,z),(middle,at_wall,z),(right,eave-.26,z)]
        m.face(profile if z==front else list(reversed(profile)),wall_material)


def hip(m,label,bounds,eave,ridge,overhang=.30,roof_material="roof"):
    left,front,right,back = bounds
    w,d = right-left,back-front
    dx,dz = max(0,(w-d)*.5),max(0,(d-w)*.5)
    x,z = (left+right)/2,(front+back)/2
    high = [(x-dx,ridge,z-dz),(x+dx,ridge,z+dz)]
    y = eave-(ridge-eave)*overhang/(min(w,d)*.5)
    low = [(left-overhang,y,front-overhang),(right+overhang,y,front-overhang),
           (right+overhang,y,back+overhang),(left-overhang,y,back+overhang)]
    ridge_points = [[high[0],high[1]],[high[1]],[high[1],high[0]],[high[0]]]
    if d>w:
        ridge_points = [[high[0]],[high[0],high[1]],[high[1]],[high[1],high[0]]]
    for i in range(4):
        poly = list(dict.fromkeys([low[i],low[(i+1)%4],*reversed(ridge_points[i])]))
        slab(m,label,poly,.25,roof_material,borders={0})


def convex_hip(m,label,footprint,eave,ridge,overhang=.30):
    """Set out each hip on the bisector between adjacent roof planes."""
    corners = [Vector(p) for p in footprint]
    edges = [b-a for a,b in zip(corners,corners[1:]+corners[:1])]
    normals = [Vector((-edge.y,edge.x)).normalized() for edge in edges]
    rim = [p-overhang*(a+b)/(1+a.dot(b))
           for p,a,b in zip(corners,normals[-1:]+normals[:-1],normals)]

    def clip(poly,normal,cut):
        result = []
        for a,b in zip(poly,poly[1:]+poly[:1]):
            da,db = normal.dot(a)-cut,normal.dot(b)-cut
            if da<=1e-5:
                result.append(a)
            if da*db < 0 and min(abs(da),abs(db))>1e-5:
                result.append(a.lerp(b,da/(da-db)))
        return result

    offsets = [n.dot(p) for n,p in zip(normals,rim)]
    faces = []
    for i,(normal,offset) in enumerate(zip(normals,offsets)):
        poly = rim
        for j,(other,cut) in enumerate(zip(normals,offsets)):
            if i!=j:
                poly = clip(poly,normal-other,offset-cut)
        faces.append([(p,float(normal.dot(p)-offset)) for p in poly])
    rise_from_rim = max(d for face in faces for _,d in face)
    pitch = (ridge-eave)/(rise_from_rim-overhang)
    for face in faces:
        top = [(p.x,eave+(d-overhang)*pitch,p.y) for p,d in face]
        borders = {i for i,((_,a),(_,b)) in enumerate(zip(face,face[1:]+face[:1]))
                   if max(abs(a),abs(b))<1e-5}
        slab(m,label,top,.25,borders=borders)


def courtyard_roof(m,label,outer,inner,eave,ridge):
    def corners(bounds,y,extension=0):
        left,front,right,back = bounds
        return [(left-extension,y,front-extension),(right+extension,y,front-extension),
                (right+extension,y,back+extension),(left-extension,y,back+extension)]
    width = min(inner[0]-outer[0],inner[1]-outer[1],outer[2]-inner[2],outer[3]-inner[3])
    y = eave-(ridge-eave)*.26/(width*.5)
    outside = corners(outer,y,.26)
    inside = corners(inner,y,-.26)
    crest = corners(tuple((a+b)*.5 for a,b in zip(outer,inner)),ridge)
    for i in range(4):
        j = (i+1)%4
        slab(m,label,[outside[i],outside[j],crest[j],crest[i]],.26,borders={0})
        slab(m,label,[crest[i],crest[j],inside[j],inside[i]],.26,borders={2})


def pipe(m,center,axis,profile,material,sides=24):
    """A solid or open axial profile, including the lip of recessed lenses."""
    center,axis = Vector(center),Vector(axis).normalized()
    u = axis.cross((0,1,0))
    if u.length<.001:
        u = axis.cross((1,0,0))
    u.normalize()
    v = axis.cross(u)

    def point(i,dist,radius):
        a = tau*i/sides
        return center+axis*dist+(u*cos(a)+v*sin(a))*radius

    for (a,r),(b,s) in zip(profile,profile[1:]):
        for i in range(sides):
            dist = (b-a)**2+(s-r)**2
            assert dist>0
            m.face([point(i,a,r),point(i+1,a,r),point(i+1,b,s),point(i,b,s)],
                   material,[(i/sides,a),((i+1)/sides,a),((i+1)/sides,b),(i/sides,b)],smooth=r!=0 and s!=0)
