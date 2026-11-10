"""Small mesh primitives; metres, X right, Y up, front -Z."""
import math
from collections import defaultdict
import bpy
import bmesh
from mathutils import Vector


class Model:
    def __init__(self, name, aliases):
        self.name, self.aliases = name, aliases
        self.parts = defaultdict(lambda: [[], [], [], []])
        self.section = 'Foundation'
        self.features = set()
        self.landmarks = set()
        self.markers = {}
        self.wall_mapping = None

    def feature(self, name):
        self.section = name
        self.features.add(name)

    def landmark(self, name):
        self.feature('Landmark_'+name)
        self.landmarks.add(name)

    def face(self, points, material, uv=None, smooth=False):
        points = list(dict.fromkeys(tuple(round(float(n), 6) for n in p) for p in points))
        if len(points) < 3:
            return
        normal = (Vector(points[1])-Vector(points[0])).cross(Vector(points[2])-Vector(points[0]))
        if normal.length < .000001:
            return
        vertices, faces, uvs, smoothing = self.parts[(self.section, self.aliases[material])]
        start = len(vertices)
        vertices.extend(points)
        faces.append(tuple(range(start, start+len(points))))
        smoothing.append(smooth)
        axis = max(range(3), key=lambda i: abs(normal[i]))
        a, b = ((2, 1), (0, 2), (0, 1))[axis]
        scale = 3.0 if material in ('stone', 'trim', 'paving', 'brick') else 2.2
        if uv is None and self.wall_mapping and material == 'stone':
            x, z, radius = self.wall_mapping
            angles = [math.atan2(p[0]-x, z-p[2]) for p in points]
            if max(angles)-min(angles) > math.pi:
                angles = [v+math.tau if v<0 else v for v in angles]
            uv = [(angle*radius/scale, p[1]/scale) for p, angle in zip(points, angles)]
        if uv is None and material == 'roof' and abs(normal.y) > .01:
            n = normal.normalized()
            across = Vector((n.z, 0, -n.x)).normalized() if abs(n.x)+abs(n.z)>.001 else Vector((1,0,0))
            if across.z < -.001 or (abs(across.z)<.001 and across.x<0): across = -across
            uphill = n.cross(across).normalized()
            if uphill.y<0:uphill = -uphill
            uv = [(Vector(p).dot(across)/2.2, Vector(p).dot(uphill)/2.2) for p in points]
        uvs.extend(uv if uv is not None and len(uv) == len(points) else [(p[a]/scale, p[b]/scale) for p in points])

    def box(self, center, size, mat, yaw=0):
        if min(size) <= 0:
            raise ValueError((self.name, self.section, size))
        x, y, z = center
        w, h, d = (s/2 for s in size)
        co, si = math.cos(yaw), math.sin(yaw)
        def p(a, b, c): return x+a*co+c*si, y+b, z-a*si+c*co
        v = [p(-w,-h,-d),p(w,-h,-d),p(w,h,-d),p(-w,h,-d),p(-w,-h,d),p(w,-h,d),p(w,h,d),p(-w,h,d)]
        for f in ((0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)):
            self.face([v[i] for i in f], mat)

    def beam(self, start, end, width, mat, depth=None):
        a, b = Vector(start), Vector(end)
        axis = (b-a).normalized()
        ref = Vector((0,1,0)) if abs(axis.y) < .95 else Vector((1,0,0))
        u = axis.cross(ref).normalized()*width/2
        v = axis.cross(u).normalized()*(depth or width)/2
        pts = [a-u-v,a+u-v,a+u+v,a-u+v,b-u-v,b+u-v,b+u+v,b-u+v]
        for f in ((0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)):
            self.face([pts[i] for i in f], mat)

    def cylinder(self, center, radius, height, mat, n=24, top=None, axis=(0,1,0)):
        c, direction = Vector(center), Vector(axis).normalized()
        ref = Vector((1,0,0)) if abs(direction.x) < .9 else Vector((0,0,1))
        u = ref-direction*direction.dot(ref)
        u.normalize()
        v = u.cross(direction)
        lower, upper = [], []
        for i in range(n):
            ray = u*math.cos(i*math.tau/n)+v*math.sin(i*math.tau/n)
            lower.append(c-direction*height/2+ray*radius)
            upper.append(c+direction*height/2+ray*(radius if top is None else top))
        self.face(lower, mat)
        self.face(list(reversed(upper)), mat)
        for i in range(n):
            j = (i+1) % n
            self.face([lower[i],upper[i],upper[j],lower[j]], mat, smooth=n>=20)

    def ellipsoid(self, c, radii, mat, n=20, rings=12):
        def point(a,b): return c[0]+radii[0]*math.cos(b)*math.cos(a), c[1]+radii[1]*math.sin(b), c[2]+radii[2]*math.cos(b)*math.sin(a)
        for row in range(rings):
            lo, hi = -math.pi/2+row*math.pi/rings, -math.pi/2+(row+1)*math.pi/rings
            for i in range(n):
                a,b = i*math.tau/n,(i+1)*math.tau/n
                self.face([point(a,lo),point(a,hi),point(b,hi),point(b,lo)],mat,smooth=True)

    def lathe(self, x, y, z, profile, mat, n=24, caps=True):
        def p(radius, height, i):return x+radius*math.cos(i*math.tau/n),y+height,z+radius*math.sin(i*math.tau/n)
        for (r0,h0),(r1,h1) in zip(profile,profile[1:]):
            for i in range(n):self.face([p(r0,h0,i),p(r1,h1,i),p(r1,h1,i+1),p(r0,h0,i+1)],mat,smooth=True)
        if caps:
            r,h=profile[0];self.face([p(r,h,i) for i in range(n)],mat)
            r,h=profile[-1];self.face([p(r,h,i) for i in reversed(range(n))],mat)

    def ring(self, x, z, inner, outer, bottom, top, mat, n=40, start=0, end=math.tau, stretch=1):
        def p(r,y,a): return x+r*math.cos(a),y,z+r*math.sin(a)*stretch
        for i in range(n):
            a,b = start+(end-start)*i/n,start+(end-start)*(i+1)/n
            self.face([p(outer,bottom,a),p(outer,top,a),p(outer,top,b),p(outer,bottom,b)],mat)
            self.face([p(inner,bottom,b),p(inner,top,b),p(inner,top,a),p(inner,bottom,a)],mat)
            self.face([p(inner,top,a),p(inner,top,b),p(outer,top,b),p(outer,top,a)],mat)
            self.face([p(inner,bottom,b),p(inner,bottom,a),p(outer,bottom,a),p(outer,bottom,b)],mat)
        if end-start < math.tau-.001:
            for a in (start,end):
                self.face([p(inner,bottom,a),p(inner,top,a),p(outer,top,a),p(outer,bottom,a)],mat)

    def arch(self, x, y, z, width, height, thick, depth, mat, yaw=0, n=16):
        radius, spring = width/2, height-width/2
        co,si = math.cos(yaw),math.sin(yaw)
        def p(a,b,c):return x+a*co+c*si,y+b,z-a*si+c*co
        for side in (-1,1):self.box(p(side*(radius+thick/2),spring/2,0),(thick,spring,depth),mat,yaw)
        for i in range(n):
            a,b = i*math.pi/n,(i+1)*math.pi/n
            pts = [p(r*math.cos(t),spring+r*math.sin(t),d) for d in (-depth/2,depth/2) for r,t in ((radius,a),(radius,b),(radius+thick,b),(radius+thick,a))]
            for f in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
                self.face([pts[j] for j in reversed(f)],mat)

    def roof(self, x, z, width, depth, y, rise, mat='roof', kind='gable', wall='wall'):
        w,d=width/2+.22,depth/2+.22
        a,b,c,e=(x-w,y,z-d),(x+w,y,z-d),(x+w,y,z+d),(x-w,y,z+d)
        shift=0 if kind=='gable' else min(d*.8,w*.8)
        front,back=(x,y+rise,z-d+shift),(x,y+rise,z+d-shift)
        self.face([a,e,back,front],mat);self.face([b,front,back,c],mat)
        self.face([a,front,b],wall if kind=='gable' else mat)
        self.face([e,c,back],wall if kind=='gable' else mat)
        for p,q in ((a,b),(b,c),(c,e),(e,a)):
            self.beam(p,q,.16,'wood')
        self.beam(front,back,.15,mat)
        if kind=='gable':
            for p,q in ((a,front),(front,b),(e,back),(back,c)):self.beam(p,q,.15,'trim')

    def vault(self, x, z, width, depth, y, rise, mat='roof', n=16, caps=True):
        def p(a,zz):return x+math.cos(a)*width/2,y+math.sin(a)*rise,zz
        for i in range(n):
            a,b=math.pi*i/n,math.pi*(i+1)/n
            self.face([p(a,z-depth/2),p(b,z-depth/2),p(b,z+depth/2),p(a,z+depth/2)],mat)
        if caps:
            self.face([p(i*math.pi/n,z-depth/2) for i in reversed(range(n+1))],'trim')
            self.face([p(i*math.pi/n,z+depth/2) for i in range(n+1)],'trim')
        for side in (-1,1):self.box((x+side*width/2,y-.04,z),(.14,.2,depth),'trim')

    def shed(self, x0, x1, z, depth, y0, y1, mat='roof', wall='wall'):
        front,back=z-depth/2-.18,z+depth/2+.18
        self.face([(x0,y0,front),(x0,y0,back),(x1,y1,back),(x1,y1,front)],mat)
        low=min(y0,y1)
        for zz in (front,back):
            self.face([(x0,low,zz),(x0,y0,zz),(x1,y1,zz),(x1,low,zz)],wall)
            self.beam((x0,y0,zz),(x1,y1,zz),.13,'timber')
        for xx,yy in ((x0,y0),(x1,y1)):self.beam((xx,yy,front),(xx,yy,back),.16,'timber')

    def glazed_dome(self,x,z,r,y,rise):
        sectors,rings=32,12
        opening=math.pi/8
        def p(a,b,offset=0):return x+(r+offset)*math.cos(b)*math.cos(a),y+(rise+offset)*math.sin(b),z+(r+offset)*math.cos(b)*math.sin(a)
        self.feature('FramedGlassObservationDome')
        for i in range(sectors):
            a,b=i*math.tau/sectors,(i+1)*math.tau/sectors
            if abs((a+b)/2-math.pi*1.5)<opening:continue
            for j in range(rings):
                lo,hi=j*math.pi/(2*rings),(j+1)*math.pi/(2*rings)
                self.face([p(a,lo),p(a,hi),p(b,hi),p(b,lo)],'roofglass',smooth=True)
        self.feature('ObservationDomeRibsAndTrack')
        for i in range(0,sectors,2):
            a=i*math.tau/sectors
            if abs(a-math.pi*1.5)<opening-.001:continue
            for j in range(rings):self.beam(p(a,j*math.pi/(2*rings),.025),p(a,(j+1)*math.pi/(2*rings),.025),.075,'bronze')
        for j in (0,4,8):
            b=j*math.pi/(2*rings)
            for i in range(sectors):
                a,c=i*math.tau/sectors,(i+1)*math.tau/sectors
                if abs((a+c)/2-math.pi*1.5)<opening:continue
                self.beam(p(a,b,.025),p(c,b,.025),.055,'bronze')
        for a in (math.pi*1.5-opening,math.pi*1.5+opening):
            for j in range(rings):self.beam(p(a,j*math.pi/(2*rings),.04),p(a,(j+1)*math.pi/(2*rings),.04),.12,'bronze')
        self.ring(x,z,r-.12,r+.16,y-.20,y,'metal',n=64)
        self.ring(x,z,r-.08,r+.11,y,y+.10,'bronze',n=64)

    def dome(self,x,z,r,y,rise,mat='roof',slit=False,n=48,rings=14,oculus=0):
        def p(a,b):return x+r*math.cos(b)*math.cos(a),y+rise*math.sin(b),z+r*math.cos(b)*math.sin(a)
        limit=math.acos(oculus/r) if oculus else math.pi/2
        for row in range(rings):
            lo,hi=limit*row/rings,limit*(row+1)/rings
            for i in range(n):
                a,b=math.tau*i/n,math.tau*(i+1)/n
                middle=(a+b)/2
                if slit and abs(middle-math.pi*1.5)<.19:continue
                self.face([p(a,lo),p(a,hi),p(b,hi),p(b,lo)],mat,smooth=True)
        if slit:
            for angle in (math.pi*1.5-math.tau*1.5/n,math.pi*1.5+math.tau*1.5/n):
                for row in range(rings):
                    self.beam(p(angle,math.pi/2*row/rings),p(angle,math.pi/2*(row+1)/rings),.15,'metal')
        self.ring(x,z,r-.08,r+.10,y-.12,y+.08,'metal',n=n)

    def finish(self, materials):
        objects=[]
        for (section,mat),(verts,faces,uvs,smoothing) in self.parts.items():
            mesh=bpy.data.meshes.new(self.name+'_'+section+'_'+mat)
            mesh.from_pydata([(x,z,y) for x,y,z in verts],[],[tuple(reversed(f)) for f in faces])
            mesh.materials.append(materials[mat]);mesh.update()
            layer=mesh.uv_layers.new(name='UVMap')
            for i,loop in enumerate(layer.data):loop.uv=uvs[mesh.loops[i].vertex_index]
            for polygon,smooth in zip(mesh.polygons,smoothing):polygon.use_smooth=smooth
            bm=bmesh.new();bm.from_mesh(mesh)
            bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
            bm.to_mesh(mesh);bm.free();mesh.update()
            obj=bpy.data.objects.new(section+'_'+mat,mesh);bpy.context.collection.objects.link(obj);objects.append(obj)
        for name,(x,y,z) in self.markers.items():
            obj=bpy.data.objects.new(self.name+'_'+name,None);obj.location=(x,z,y);bpy.context.collection.objects.link(obj);objects.append(obj)
        return objects
