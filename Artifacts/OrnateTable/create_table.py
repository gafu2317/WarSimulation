import bpy, math, os
from mathutils import Vector
from math import sin, cos, pi
OUT='/Users/fukutomi/Unity/WarSimulation/Artifacts/OrnateTable'
scene=bpy.data.scenes.new('Ornate Marquetry Table')
bpy.context.window.scene=scene

def mat(name,color,metal=0,rough=.25):
 m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
 p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
 return m
wood=mat('Polished figured mahogany',(.13,.027,.008),0,.23)
n=wood.node_tree.nodes;l=wood.node_tree.links;p=next(x for x in n if x.type=='BSDF_PRINCIPLED');p.inputs['Coat Weight'].default_value=.38;p.inputs['Coat Roughness'].default_value=.17
tex=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(3,3,.45);l.new(tex.outputs['Generated'],mapping.inputs[0])
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5;noise.inputs['Detail'].default_value=4;noise.inputs['Roughness'].default_value=.7;l.new(mapping.outputs[0],noise.inputs['Vector'])
wave=n.new('ShaderNodeTexWave');wave.wave_type='BANDS';wave.bands_direction='X';wave.inputs['Scale'].default_value=5;wave.inputs['Distortion'].default_value=12;wave.inputs['Detail'].default_value=5;l.new(mapping.outputs[0],wave.inputs['Vector'])
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1]);
for i,(pos,col) in enumerate([(0,(.022,.004,.001,1)),(.3,(.065,.012,.003,1)),(.6,(.18,.042,.009,1)),(1,(.32,.095,.023,1))]):
 e=ramp.color_ramp.elements[0] if i==0 else ramp.color_ramp.elements.new(pos);e.position=pos;e.color=col
l.new(wave.outputs['Color'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
gold=mat('Warm polished ormolu',(.83,.43,.09),.82,.22)
inlay=mat('Golden satin marquetry',(.72,.38,.075),.55,.28)
dark=mat('Dark edge accent',(.035,.008,.003),0,.22)

def finish(o,name,material):
 o.name=name;o.data.materials.append(material)
 if o.type=='MESH':
  for f in o.data.polygons:f.use_smooth=True
 return o

def lathe(name,profile,material,xy=(0,0)):
 verts=[];faces=[];N=128
 for r,z in profile:
  for j in range(N):
   a=2*pi*j/N;verts.append((xy[0]+r*cos(a),xy[1]+r*sin(a),z))
 for i in range(len(profile)-1):
  for j in range(N):
   a=i*N+j;b=i*N+(j+1)%N;faces.append((a,b,b+N,a+N))
 faces += [tuple(range(N-1,-1,-1)),tuple((len(profile)-1)*N+j for j in range(N))]
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return finish(o,name,material)

def line(name,pts,r,material,closed=False):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=16;cu.bevel_depth=r;cu.bevel_resolution=3
 s=cu.splines.new('POLY');s.points.add(len(pts)-1)
 for p,co in zip(s.points,pts):p.co=(*co,1)
 s.use_cyclic_u=closed;o=bpy.data.objects.new(name,cu);scene.collection.objects.link(o);return finish(o,name,material)
def ring(name,r,z,t,material,xy=(0,0)):
 return line(name,[(xy[0]+r*cos(i*2*pi/192),xy[1]+r*sin(i*2*pi/192),z) for i in range(192)],t,material,True)
lathe('Circular solid mahogany top',[(0,1.04),(.86,1.04),(.9,1.047),(.915,1.061),(.916,1.075),(.905,1.09),(.88,1.097),(0,1.097)],wood)
lathe('Deep cylindrical apron',[(.81,.902),(.863,.902),(.874,.914),(.874,1.039),(.857,1.052),(.81,1.052)],wood)
ring('Gilded upper bullnose',.914,1.043,.018,gold)
ring('Apron lower moulding',.87,.911,.010,gold)
ring('Apron lower fine fillet',.872,.928,.0035,gold)
ring('Dark top bead',.897,1.087,.004,dark)
for r,t in [(.835,.003),(.844,.002),(.823,.0012)]:ring('Concentric inlay border',r,1.098,t,inlay)
lathe('Turned central pedestal',[(.17,.16),(.2,.18),(.19,.2),(.135,.235),(.10,.28),(.115,.31),(.155,.34),(.178,.39),(.17,.44),(.137,.49),(.092,.52),(.09,.55),(.108,.565),(.10,.585),(.075,.61),(.064,.7),(.083,.79),(.115,.88),(.13,.907)],wood)
for r,z,t in [(.10,.29,.012),(.102,.535,.012),(.107,.565,.009),(.11,.867,.008)]:ring('Pedestal gilded collar',r,z,t,gold)
for j in range(16):
 a=j*2*pi/16
 line('Pedestal fluting',[(r*cos(a),r*sin(a),z) for r,z in [(.105,.30),(.139,.33),(.17,.37),(.18,.4),(.17,.44),(.143,.48),(.099,.518)]] ,.0035,gold)

def bez(p0,p1,p2,p3,t):return tuple((1-t)**3*p0[k]+3*(1-t)**2*t*p1[k]+3*(1-t)*t*t*p2[k]+t**3*p3[k] for k in range(2))
segments=[((.55,.92),(.43,.82),(.40,.69),(.44,.54)),((.44,.54),(.47,.39),(.57,.28),(.68,.235)),((.68,.235),(.79,.20),(.79,.125),(.75,.09))]
path=[bez(*s,i/30) for s in segments for i in range(30)]+[segments[-1][-1]]
for j in range(4):
 a=pi/4+j*pi/2;rad=Vector((cos(a),sin(a),0));side=Vector((-sin(a),cos(a),0));verts=[];faces=[];accent=[]
 for i,(r,z) in enumerate(path):
  prev=Vector(path[max(0,i-1)]);nxt=Vector(path[min(len(path)-1,i+1)]);d=(nxt-prev).normalized();normal=rad*(-d.y)+Vector((0,0,d.x));center=rad*r+Vector((0,0,z));u=i/(len(path)-1);width=.066*(1-.23*u);depth=.080*(1-.3*u)
  for k in range(16):
   th=k*2*pi/16;v=center+normal*(cos(th)*depth)+side*(sin(th)*width);verts.append(v)
  accent.append(tuple(center-side*(width+.001)))
 for i in range(len(path)-1):
  for k in range(16):faces.append((i*16+k,i*16+(k+1)%16,(i+1)*16+(k+1)%16,(i+1)*16+k))
 faces.extend([tuple(range(15,-1,-1)),tuple((len(path)-1)*16+k for k in range(16))])
 me=bpy.data.meshes.new('Cabriole');me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('S-curved cabriole leg %d'%j,me);scene.collection.objects.link(o);finish(o,o.name,wood)
 sub=o.modifiers.new('Smooth carved profile','SUBSURF');sub.levels=2
 line('Leg inset gold ribbon %d'%j,accent,.009,gold)
 pts=[tuple(rad*r+Vector((0,0,z))+side*.068) for r,z in path];line('Leg reverse gold bead',pts,.004,gold)
 xy=(.75*cos(a),.75*sin(a));lathe('Round bun foot',[(.075,.018),(.085,.027),(.085,.043),(.078,.06),(.059,.076),(.048,.09)],wood,xy)
 ring('Gilt foot rim',.075,.052,.012,gold,xy);ring('Foot collar',.05,.083,.006,gold,xy)
 brace=[bez((.0,.20),(.30,.25),(.44,.13),(.69,.19),i/50) for i in range(51)]
 line('Sweeping lower stretcher',[tuple(rad*r+Vector((0,0,z))) for r,z in brace],.034,wood)
 line('Stretcher gilding',[tuple(rad*r-side*.028+Vector((0,0,z-.009))) for r,z in brace],.004,gold)

for j in range(12):
 a=j*2*pi/12;center=Vector((.878*cos(a),.878*sin(a),.975));tangent=Vector((-sin(a),cos(a),0));up=Vector((0,0,1));normal=Vector((cos(a),sin(a),0))
 for rr,th in [(.027,.004),(.020,.002)]:line('Apron rosette bezel',[tuple(center+tangent*(rr*cos(k*2*pi/64))+up*(rr*sin(k*2*pi/64))) for k in range(64)],th,gold,True)
 for k in range(12):
  b=k*2*pi/12;line('Rosette petal',[tuple(center+normal*.002+tangent*(r*cos(b))+up*(r*sin(b))) for r in [.005,.019]],.0025,gold)

# Small raised profiles keep the marquetry readable in the saved model without image textures.
def leaf(name,start,end,width):
 s=Vector(start);e=Vector(end);d=e-s;perp=Vector((-d.y,d.x,0)).normalized();verts=[tuple(s)]
 for i in range(1,13):
  t=i/13;verts.append(tuple(s+d*t+perp*(width*sin(pi*t))))
 verts.append(tuple(e))
 for i in range(12,0,-1):
  t=i/13;verts.append(tuple(s+d*t-perp*(width*sin(pi*t))))
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],[tuple(range(len(verts)))]);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);finish(o,name,inlay)
def ornament(angle,base,scale,name):
 def tr(x,y):return ((base+scale*x)*cos(angle)-scale*y*sin(angle),(base+scale*x)*sin(angle)+scale*y*cos(angle),1.099)
 line(name+' stem',[tr(x,0) for x in [0,.1,.2,.32,.44]],.0016,inlay)
 for sign in [-1,1]:
  pts=[]
  for i in range(101):
   t=i/100;theta=-pi/2+t*2*pi*1.4;r=.14*(1-t)+.009;pts.append(tr(.19+r*cos(theta),sign*(.13+r*sin(theta))))
  line(name+' scroll',pts,.0017,inlay)
  for x,y,ex,ey,w in [(.09,.015,.03,.13,.023),(.15,.03,.13,.19,.027),(.24,.04,.30,.17,.025),(.3,.02,.4,.10,.022),(.35,.0,.44,.045,.016)]:
   leaf(name+' acanthus',tr(x,sign*y),tr(ex,sign*ey),w*scale)
 leaf(name+' spear',tr(.28,0),tr(.48,0),.025*scale)
for j in range(8):ornament(j*2*pi/8,.015,.65,'Central floral inlay')
for j in range(12):ornament(j*2*pi/12,.79,-.30,'Border arabesque')
ring('Center medallion',.033,1.099,.002,inlay)
print('Table geometry complete',len(scene.objects))
