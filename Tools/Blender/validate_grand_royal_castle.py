import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(p/'ArtSource/Blender/GrandRoyalCastle.blend'))
bpy.context.view_layer.update()
assert 'Royal_Castle' not in bpy.data.collections
vs=[];fs=[]
for o in bpy.data.objects:
 if o.type=='MESH' and (o.name.endswith(' masonry') or o.name in ['Outer corner tower','Gate round tower','Body round tower','Gate upper room','Open gateway vault']):
  n=len(vs);vs.extend(o.matrix_world@v.co for v in o.data.vertices);fs.extend(tuple(n+i for i in f.vertices) for f in o.data.polygons)
tree=BVHTree.FromPolygons(vs,fs)
obj=bpy.data.objects['Window glass'];mesh=obj.data
adj=[set() for _ in mesh.vertices]
for e in mesh.edges:
 a,b=e.vertices;adj[a].add(b);adj[b].add(a)
remaining=set(range(len(adj)));count=0
while remaining:
 seed=remaining.pop();part={seed};stack=[seed]
 while stack:
  for i in adj[stack.pop()]:
   if i in remaining:remaining.remove(i);part.add(i);stack.append(i)
 coords=[obj.matrix_world@mesh.vertices[i].co for i in part]
 lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
 c=(lo+hi)/2
 hits=[tree.ray_cast(c,Vector(d),.5)[0] for d in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0)]]
 assert any(v is not None for v in hits),tuple(c)
 count+=1
t=bpy.data.objects['Castle stone terrace'];assert tuple(round(v,2) for v in t.dimensions[:2])==(60,54)
v=p/'docs/Art/GrandRoyalCastle/validation.json';r=json.loads(v.read_text());r.update(saved_file_reopened=True,window_glass_components_checked=count,windows_have_wall_support=True,old_inner_castle_removed=True);v.write_text(json.dumps(r,indent=2))

roof=bpy.data.objects['Slate roofs'].data
checked=0
for face in roof.polygons:
 points=[roof.vertices[i].co for i in face.vertices]
 points.append(sum(points,Vector())/len(points))
 for point in points:
  x,y,z=point
  assert not (-3.999<x<3.999 and 4.001<y<11.999 and z<27.39),('keep roof penetration',tuple(point))
  for xx in [-9,9]:
   for yy,hh in [(-3,21),(16,23.5)]:
    assert not ((x-xx)**2+(y-yy)**2<1.999**2 and z<hh+.39),('round tower roof penetration',tuple(point))
 checked+=1
deps=bpy.context.evaluated_depsgraph_get()
for y in range(-25,-4):
 hit,*rest=bpy.context.scene.ray_cast(deps,Vector((0,y,2)),Vector((0,1,0)),distance=.9)
 assert not hit,(y,rest)
r.update(roof_faces_checked=checked,roof_cutouts_clear_of_towers=True,gate_approach_clear_at_2m=True)
v.write_text(json.dumps(r,indent=2))
print('PASS',count,'supported windows,',checked,'roof faces clear of towers, open gate approach')
