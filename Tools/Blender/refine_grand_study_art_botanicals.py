"""Paintings, botanical meshes and small sculptures. Run after the focal-props pass."""
import ast, math, random, sys
from pathlib import Path
import bpy
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'docs/Art/GrandStudy';TEX=ROOT/'ArtSource/Blender/Textures/GrandStudy'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/GrandStudy.blend'))
M={m.name:m for m in bpy.data.materials};current=None;random.seed(93)
for path,names in [('generate_grand_study.py',['mesh','curve','lathe','ring','camera','area','setup']),('refine_grand_study_focal_props.py',['block','clear','bezier','disk'])]:
    for node in ast.parse((ROOT/'Tools/Blender'/path).read_text()).body:
        if isinstance(node,ast.FunctionDef) and node.name in names:exec(compile(ast.Module(body=[node],type_ignores=[]),'helpers','exec'),globals())

def smooth(obj):
    for p in obj.data.polygons:p.use_smooth=True
    return obj

def ellipsoid(name,loc,radii,mat,n=40,m=24):
    vs=[]
    for j in range(m+1):
        a=-math.pi/2+math.pi*j/m
        for i in range(n):
            b=math.tau*i/n;vs.append((loc[0]+radii[0]*math.cos(a)*math.sin(b),loc[1]+radii[1]*math.cos(a)*math.cos(b),loc[2]+radii[2]*math.sin(a)))
    fs=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(m) for i in range(n)]
    return smooth(mesh(name,vs,fs,mat))

def material(name,color,rough=.45,metal=0):
    mat=M.get(name) or bpy.data.materials.new(name);M[name]=mat;mat.use_nodes=True
    mat.diffuse_color=(*color,1);nodes=mat.node_tree.nodes;nodes.clear();p=nodes.new('ShaderNodeBsdfPrincipled');out=nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(p.outputs[0],out.inputs[0])
    p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    return mat,p

# UV-space veins follow every individual leaf, regardless of its rotation in the plant.
for name,col in [('Ficus deep',(.025,.092,.017)),('Ficus young',(.063,.17,.027)),('Palm jade',(.028,.12,.034)),('Palm olive',(.08,.16,.025)),('Croton burgundy',(.16,.03,.007)),('Croton green',(.036,.075,.013))]:
    mat,p=material(name,col,.41);p.inputs['Subsurface Weight'].default_value=.05;p.inputs['Sheen Weight'].default_value=.10;p.inputs['Specular IOR Level'].default_value=.28
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    uv=nodes.new('ShaderNodeTexCoord');sep=nodes.new('ShaderNodeSeparateXYZ');links.new(uv.outputs['UV'],sep.inputs[0])
    def calc(op,a,b=0):
        q=nodes.new('ShaderNodeMath');q.operation=op
        for idx,v in enumerate([a,b]):
            if isinstance(v,(float,int)):q.inputs[idx].default_value=v
            else:links.new(v,q.inputs[idx])
        return q.outputs[0]
    cross=calc('ABSOLUTE',calc('SUBTRACT',sep.outputs['Y'],.5))
    centre=calc('LESS_THAN',cross,.011)
    phase=calc('SUBTRACT',calc('MULTIPLY',sep.outputs['X'],12),calc('MULTIPLY',cross,5))
    side=calc('LESS_THAN',calc('ABSOLUTE',calc('SUBTRACT',calc('FRACT',phase),.5)),.018)
    veins=calc('MAXIMUM',centre,side)
    mix=nodes.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(*col,1)
    veincol=(.42,.19,.021) if 'Croton' in name else tuple(v*1.9 for v in col)
    mix.inputs[2].default_value=(*veincol,1);links.new(veins,mix.inputs[0]);links.new(mix.outputs[0],p.inputs['Base Color'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.0003
    links.new(veins,bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
material('Plant bark',(.071,.040,.017),.8);material('Potting soil',(.023,.014,.007),.95)
mat,p=material('Ivory glazed planter',(.43,.39,.28),.27);p.inputs['Coat Weight'].default_value=.3
material('Aged bronze',(.16,.105,.052),.39,.83)
mat,p=material('Carved ivory stone',(.61,.57,.47),.40);p.inputs['Subsurface Weight'].default_value=.05
nodes=mat.node_tree.nodes;links=mat.node_tree.links;n=nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=45;n.inputs['Detail'].default_value=2
b=nodes.new('ShaderNodeBump');b.inputs['Strength'].default_value=.08;b.inputs['Distance'].default_value=.00010;links.new(n.outputs['Fac'],b.inputs['Height']);links.new(b.outputs[0],p.inputs['Normal'])
for name,c in [('Rose wine',(.21,.005,.012)),('Rose velvet',(.32,.014,.018)),('Rose edges',(.43,.026,.022))]:
    mat,p=material(name,c,.57);p.inputs['Subsurface Weight'].default_value=.11;p.inputs['Sheen Weight'].default_value=.28

# Leaves have curved outlines, a folded midrib and a drooping tip, not triangular blades.
def leaf(name,start,end,width,mat,arch=.018,twist=0,segments=20):
    a,b=Vector(start),Vector(end);d=b-a;side=d.cross(Vector((0,0,1))).normalized()
    if side.length<.1:side=Vector((1,0,0))
    normal=side.cross(d).normalized()
    if normal.z<0:normal=-normal
    cols=6;vs=[]
    for j in range(segments+1):
        t=j/segments;c=a+d*t+Vector((0,0,arch*math.sin(math.pi*t)))
        w=width*(math.sin(math.pi*t)**.78)*(.85+.15*t)
        if 'Croton' in name:w*=1+.08*math.sin(t*math.pi*6)
        for i in range(cols+1):
            u=-1+2*i/cols
            v=c+side*(w*u/2)+normal*(w*(.15*u*u+.11*u*math.sin(t*math.pi+twist)))
            vs.append(v)
    fs=[(j*(cols+1)+i,j*(cols+1)+i+1,(j+1)*(cols+1)+i+1,(j+1)*(cols+1)+i) for j in range(segments) for i in range(cols)]
    o=smooth(mesh(name,vs,fs,mat));uv=o.data.uv_layers.new(name='Leaf veins')
    for poly in o.data.polygons:
        for idx in poly.loop_indices:
            v=o.data.loops[idx].vertex_index;uv.data[idx].uv=(v//(cols+1)/segments,v%(cols+1)/cols)
    mod=o.modifiers.new('Leaf thickness','SOLIDIFY');mod.thickness=.00035
    return o

def planter():
    lathe('Glazed ceramic planter',[(0,0),(.135,0),(.15,.025),(.17,.06),(.19,.19),(.207,.265),(.218,.27),(.219,.287),(.199,.29),(.191,.266),(.169,.12),(.122,.03),(0,.03)],mat='Ivory glazed planter',n=64)
    for z,r in [(.027,.15),(.065,.17),(.264,.207),(.286,.217)]:ring('Pot raised bead',(0,0,z),r,.0022,'Ivory glazed planter')
    disk('Visible potting soil',.19,.011,(0,0,.256),'Potting soil')
    for i in range(16):
        a=i*2.4;r=.17*math.sqrt(random.random());ellipsoid('Soil particle',(r*math.cos(a),r*math.sin(a),.266),(.009,.007,.004),'Plant bark',12,6)

for name in ['Plant_Broadleaf','Plant_Croton']:
    clear(name);planter();croton=name=='Plant_Croton'
    for s in range(3):
        a=s*2.3;off=Vector((.047*math.cos(a),.047*math.sin(a),.263));h=(.84+s*.16) if croton else (1.00+s*.17)
        top=Vector((.13*math.cos(a),.13*math.sin(a),h))
        bezier('Woody main stem',[off,off+Vector((0,0,.3)),top-Vector((.02,0,.25)),top],.009 if croton else .011,'Plant bark')
        for j in range(7):
            t=.35+j*.095;root=off.lerp(top,t);az=a+j*2.399
            length=random.uniform(.15,.24);tip=root+Vector((length*math.cos(az),length*math.sin(az),.05+random.random()*.07))
            bezier('Supported side branch',[root,root+Vector((0,0,.05)),tip-Vector((.015,0,.02)),tip],.0034,'Plant bark')
            for k in range(3):
                start=root.lerp(tip,.52+k*.23);ang=az+[-.68,.68,0][k];L=random.uniform(.17,.25) if croton else random.uniform(.20,.29)
                end=start+Vector((L*math.cos(ang),L*math.sin(ang),-.05+random.random()*.03))
                m=('Croton burgundy' if (j+k+s)%3==0 else 'Croton green') if croton else ('Ficus young' if j>4 else 'Ficus deep')
                leaf('Croton variegated leaf' if croton else 'Ficus oval leaf',start,end,.090 if croton else .105,m,.037,random.random()*2)

        leaf('Terminal young leaf',top-Vector((0,0,.012)),top+Vector((.08*math.cos(a),.08*math.sin(a),.09)),.04,'Croton green' if croton else 'Ficus young',.01)

clear('Plant_Palm');planter()
for s in range(3):
    az=s*2.4;root=Vector((.055*math.cos(az),.055*math.sin(az),.263));crown=Vector((.16*math.cos(az),.16*math.sin(az),1.11+s*.17))
    curve('Palm cane',[root.lerp(crown,i/40) for i in range(41)],.010,'Plant bark')
    for j in range(10):
        c=root.lerp(crown,.15+j*.075);ring('Cane growth ring',c,.0102,.001,'Palm olive')
    for f in range(6):
        a=az+f*2.399;L=.47+random.random()*.15
        def path(t):return crown+Vector((L*t*math.cos(a),L*t*math.sin(a),.23*math.sin(math.pi*t)-.21*t*t))
        curve('Arching palm frond',[path(i/40) for i in range(41)],.0025,'Palm olive')
        for j in range(1,13):
            t=.10+j*.063;c=path(t);ll=.13*(math.sin(math.pi*t)**.45)+.035
            for sign in [-1,1]:
                ang=a+sign*1.0;end=c+Vector((ll*math.cos(ang),ll*math.sin(ang),-.07-.025*t))
                leaf('Tapered palm leaflet',c,end,.018*(1-.45*t),'Palm jade' if (j+f)%3 else 'Palm olive',.009,sign*.4,12)
        leaf('Palm terminal leaflet',path(.90),path(1.08),.018,'Palm jade',.002,0,12)

# Individual curled rose petals replace clusters of intersecting balls.
clear('Rose_Bouquet')
for flower in range(9):
    a=flower*2.399;r=.085*math.sqrt(flower/8);centre=Vector((r*math.cos(a),r*math.sin(a),.40+.044*math.sin(flower*1.7)))
    bezier('Rose stem',[(0,0,0),(centre.x*.5,centre.y*.5,.20),centre-Vector((0,0,.10)),centre],.0026,'Ficus deep')
    for layer,count in enumerate([4,5,7,9]):
        rr=.004+layer*.009
        for k in range(count):
            angle=k*math.tau/count+layer*.61;vs=[];nu=12;nv=12
            for j in range(nv+1):
                t=j/nv
                for i in range(nu+1):
                    u=-1+2*i/nu;theta=angle+u*(.68 if layer else .78)
                    radius=.003+rr*(math.sin(t*math.pi/2)**.8)
                    z=-.020+.047*t-.014*t**5*(layer/3)+.005*u*u*math.sin(math.pi*t)
                    vs.append(centre+Vector((radius*math.cos(theta),radius*math.sin(theta),z)))
            fs=[(j*(nu+1)+i,j*(nu+1)+i+1,(j+1)*(nu+1)+i+1,(j+1)*(nu+1)+i) for j in range(nv) for i in range(nu)]
            o=smooth(mesh('Curled rose petal',vs,fs,['Rose wine','Rose wine','Rose velvet','Rose edges'][layer]));o.modifiers.new('Petal thickness','SOLIDIFY').thickness=.0003
    for k in range(2):
        z=.17+k*.09;start=Vector((centre.x*z/centre.z,centre.y*z/centre.z,z));ang=a+k*2
        leaf('Rose serrated leaf',start,start+Vector((.065*math.cos(ang),.065*math.sin(ang),.035)),.031,'Ficus deep',.006)

# Anatomical head surface with nose bridge, chin, eye sockets and mouth relief.
clear('Bust_Sculpture');stone='Carved ivory stone'
block('Stone bust square plinth',(0,0,.016),(.165,.128,.032),stone,.003)
lathe('Turned bust socle',[(0,.032),(.063,.032),(.064,.042),(.046,.052),(.035,.072),(.043,.082),(.072,.092),(0,.094)],mat=stone,n=64)
# Draped shoulders form a continuous cropped chest, tapering into the pedestal.
vs=[];N=128;rows=96
for j in range(rows+1):
    t=j/rows;z=.086+.139*t
    w=.052+.050*math.sin(math.pi*t)-.028*t**4
    dep=.036+.019*math.sin(math.pi*t)-.012*t**5
    for i in range(N):
        a=i*math.tau/N;zz=z
        fold=.0014*math.sin(a*8+t*7)*(math.sin(math.pi*t)**.5)
        vs.append(((w+fold)*math.sin(a),-(dep+fold)*math.cos(a),zz))
fs=[(j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i) for j in range(rows) for i in range(N)]
fs.extend([tuple(reversed(range(N))),tuple(rows*N+i for i in range(N))]);smooth(mesh('Carved draped shoulders',vs,fs,stone))
ellipsoid('Anatomical neck',(0,.006,.229),(.023,.026,.038),stone)
vs=[];N=144;rows=96
for j in range(rows+1):
    phi=-math.pi/2+math.pi*j/rows;z=.306+.079*math.sin(phi)
    for i in range(N):
        a=math.tau*i/N;x=.051*math.cos(phi)*math.sin(a);y=-.043*math.cos(phi)*math.cos(a)
        if z<.286:x*=.86+.14*max(0,(z-.227)/.059)
        front=max(0,math.cos(a))**12
        def g(xx,zz,sx,sz):return math.exp(-((x-xx)/sx)**2-((z-zz)/sz)**2)
        relief=.010*g(0,.301,.010,.025)+.012*g(0,.291,.009,.008)
        relief+=.003*g(-.014,.32,.014,.005)+.003*g(.014,.32,.014,.005)
        relief-=.004*(g(-.021,.309,.011,.007)+g(.021,.309,.011,.007))
        relief+=.003*g(0,.270,.017,.006)+.005*g(0,.249,.019,.009)
        relief-=.002*g(0,.266,.015,.0015)
        y-=front*relief;vs.append((x,y,z))
fs=[(j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i) for j in range(rows) for i in range(N)]
smooth(mesh('Sculpted facial anatomy',vs,fs,stone))
def head_front(x,z):
    ph=math.asin(max(-1,min(1,(z-.306)/.079)));cp=math.cos(ph)
    y=-.043*math.sqrt(max(0,cp*cp-(x/.051)**2))
    front=(y/(-.043*cp))**12 if cp>.001 else 0
    def g(xx,zz,sx,sz):return math.exp(-((x-xx)/sx)**2-((z-zz)/sz)**2)
    relief=.010*g(0,.301,.010,.025)+.012*g(0,.291,.009,.008)
    relief+=.003*g(-.014,.32,.014,.005)+.003*g(.014,.32,.014,.005)
    relief-=.004*(g(-.021,.309,.011,.007)+g(.021,.309,.011,.007))
    relief+=.003*g(0,.270,.017,.006)+.005*g(0,.249,.019,.009)-.002*g(0,.266,.015,.0015)
    return y-front*relief
for sign in [-1,1]:
    ellipsoid('Carved ear',(sign*.049,.001,.300),(.008,.009,.017),stone)
    # Narrow eyelids lie inside the recessed sockets; no separate round eyeballs.
    x=sign*.021
    for upper in [True,False]:
        pts=[]
        for u in [-1+i/12 for i in range(25)]:
            xx=x+.009*u;zz=.309+(.0024 if upper else -.0015)*math.sqrt(max(0,1-u*u))
            pts.append((xx,head_front(xx,zz)-.00035,zz))
        curve('Carved eyelid',pts,.00065,stone)
    curve('Nostril crease',[(sign*.004,-.058,.287),(sign*.007,-.055,.285)],.0007,stone)
# Wavy cropped hair cap, with its own continuous carved surface.
vs=[];N=120;rows=40
for j in range(rows+1):
    t=j/rows
    for i in range(N):
        a=i*math.tau/N;bottom=.13+.37*max(0,math.cos(a))+.035*math.sin(a*7);phi=bottom+(math.pi/2-bottom)*t
        wav=.0016*math.sin(a*18+phi*13)*(1-t)
        vs.append(((.053+wav)*math.cos(phi)*math.sin(a),-(.046+wav)*math.cos(phi)*math.cos(a),.306+.082*math.sin(phi)))
fs=[(j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i) for j in range(rows) for i in range(N)];hair=smooth(mesh('Carved wavy hair cap',vs,fs,stone));hair.modifiers.new('Hair joins skull','SOLIDIFY').thickness=.008
for row,(phi0,count) in enumerate([(.55,14),(.83,13),(1.10,10),(1.32,6)]):
    for k in range(count):
        a0=k*math.tau/count+row*.37;pts=[]
        for j in range(32):
            t=j/31;angle=t*math.pi*1.6;shrink=1-.7*t
            a=a0+.12*math.cos(angle)*shrink;phi=phi0+.10*math.sin(angle)*shrink
            if phi<.13+.37*max(0,math.cos(a))+.035*math.sin(a*7):continue
            pts.append((.0545*math.cos(phi)*math.sin(a),-.0475*math.cos(phi)*math.cos(a),.306+.0835*math.sin(phi)))
        if len(pts)>2:curve('Carved curling hair lock',pts,.0012,stone)
# Merge the carved head, hair, ears and body into one continuous stone surface.
# This removes the open hair-cap edge and isolated primitive seams.
bust_parts=[o for o in current.objects if not o.name.startswith(('Stone bust square plinth','Turned bust socle'))]
scene=bpy.context.scene
for obj in bust_parts:
    scene.collection.objects.link(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in bust_parts:obj.select_set(True)
bpy.context.view_layer.objects.active=bust_parts[0]
bpy.ops.object.convert(target='MESH')
bpy.ops.object.join();obj=bpy.context.object;obj.name='Unified carved portrait bust'
obj.data.remesh_voxel_size=.00065
bpy.ops.object.voxel_remesh()
mod=obj.modifiers.new('Carving finish','SMOOTH');mod.factor=.6;mod.iterations=3
bpy.ops.object.modifier_apply(modifier=mod.name)
for poly in obj.data.polygons:poly.use_smooth=True
for col in list(obj.users_collection):
    if col!=current:col.objects.unlink(obj)
if obj.name not in current.objects:current.objects.link(obj)


clear('Bird_Sculpture');bronze='Aged bronze'
lathe('Oval bird sculpture base',[(0,0),(.093,0),(.096,.01),(.09,.022),(0,.022)],mat='Dark wood',n=64)
ring('Bird base bronze rim',(0,0,.019),.09,.0018,bronze)
for x in [-.019,.019]:
    bezier('Heron long leg',[(x,.024,.027),(x,.032,.12),(x,-.004,.15),(x,.006,.227)],.0037,bronze)
    for k in [-1,0,1]:bezier('Heron toe',[(x,.024,.03),(x+k*.015,.001,.028),(x+k*.017,-.022,.028),(x+k*.019,-.033,.027)],.0018,bronze)
ellipsoid('Heron torso',(0,.021,.242),(.042,.074,.046),bronze,64,40)
# Swept continuous S-neck with a variable section radius.
controls=[Vector((0,-.031,.254)),Vector((0,-.107,.30)),Vector((0,.013,.36)),Vector((0,-.055,.400))]
vs=[];N=24;rows=64
for j in range(rows+1):
    t=j/rows;a,b,c,d=controls;p=(1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d
    tangent=(-3*(1-t)**2*a+(3*(1-t)**2-6*(1-t)*t)*b+(6*(1-t)*t-3*t*t)*c+3*t*t*d).normalized()
    u=Vector((1,0,0));v=tangent.cross(u).normalized();r=.014*(1-t)+.008*t
    for i in range(N):
        aa=i*math.tau/N;vs.append(p+r*(math.cos(aa)*u+math.sin(aa)*v))
fs=[(j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i) for j in range(rows) for i in range(N)];smooth(mesh('Continuous S shaped heron neck',vs,fs,bronze))
ellipsoid('Heron head',(0,-.061,.402),(.013,.025,.016),bronze)
mesh('Tapered heron beak',[(-.007,-.078,.400),(.007,-.078,.400),(0,-.078,.408),(0,-.154,.397),(0,-.078,.394)],[(0,2,3),(2,1,3),(1,4,3),(4,0,3),(0,4,1,2)],bronze)
for sign in [-1,1]:
    ellipsoid('Heron inset eye',(sign*.012,-.070,.406),(.0014,.002,.002),'Dark wood',16,10)
    ellipsoid('Folded bronze wing',(sign*.033,.028,.25),(.013,.057,.031),bronze)
    for k in range(7):
        bezier('Incised feather edge',[(sign*.04,-.012+k*.006,.266),(sign*.048,.018+k*.004,.26),(sign*.039,.049+k*.004,.24),(sign*.017,.088+k*.003,.226)],.0009,bronze)
leaf('Heron tapered tail',(0,.07,.241),(0,.135,.207),.037,bronze,.004)

# Trophy handles become smooth cast loops as part of the sculpture-detail pass.
current=bpy.data.collections['Trophy_Cup']
for obj in list(current.objects):
    if obj.name.startswith('Trophy handle'):bpy.data.objects.remove(obj,do_unlink=True)
for sign in [-1,1]:bezier('Cast trophy scroll handle',[(sign*.05,0,.206),(sign*.115,0,.235),(sign*.103,0,.126),(sign*.030,0,.146)],.0055,'Brass')

# Painting textures are packed into the blend, with real wood and gilt mouldings.
if '--geometry-only' not in sys.argv:
    for name,w,h,file in [('Picture_Landscape',1.05,.68,'landscape.png'),('Picture_Portrait',.54,.75,'portrait.png'),('Photo_Frame',.18,.24,'portrait.png')]:
        clear(name);small=w<.3;border=.017 if small else .037
        block('Frame wooden backing',(0,.005,h/2),(w,.022,h),'Dark wood',.003)
        iw=w-2*border;ih=h-2*border
        mat,p=material(name+' oil on canvas',(.3,.25,.17),.48)
        nodes=mat.node_tree.nodes;links=mat.node_tree.links;tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(TEX/file),check_existing=True);tex.image.pack();tex.interpolation='Linear';links.new(tex.outputs['Color'],p.inputs['Base Color'])
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=650
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.00012;links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal']);p.inputs['Coat Weight'].default_value=.12
        o=mesh('Flat painted canvas',[(-iw/2,-.01,border),(iw/2,-.01,border),(iw/2,-.01,h-border),(-iw/2,-.01,h-border)],[(0,1,2,3)],mat.name)
        uv=o.data.uv_layers.new(name='Painting UV')
        image_aspect=tex.image.size[0]/tex.image.size[1];aspect=iw/ih
        u0=(1-aspect/image_aspect)/2 if aspect<image_aspect else 0
        v0=(1-image_aspect/aspect)/2 if aspect>image_aspect else 0
        for i,co in enumerate([(u0,v0),(1-u0,v0),(1-u0,1-v0),(u0,1-v0)]):uv.data[i].uv=co
        for x in [-w/2+border/2,w/2-border/2]:block('Wood frame vertical',(x,-.008,h/2),(border,.040,h),'Dark wood',.004 if not small else .001)
        for z in [border/2,h-border/2]:block('Wood frame horizontal',(0,-.008,z),(w-2*border,.040,border),'Dark wood',.004 if not small else .001)
        for offset,thick in [(border*.78,.002 if not small else .0008),(border*.27,.0013 if not small else .0006)]:
            curve('Gilt picture moulding',[(-w/2+offset,-.031,offset),(w/2-offset,-.031,offset),(w/2-offset,-.031,h-offset),(-w/2+offset,-.031,h-offset)],thick,'Brass',True)
        if not small:
            for k in range(int(w/.019)):
                for z in [border*.45,h-border*.45]:ellipsoid('Gilt frame bead',(-w/2+border+k*(w-2*border)/max(1,int(w/.019)-1),-.030,z),(.0016,.001,.0016),'Brass',8,6)
        else:
            block('Picture easel rear foot',(0,.055,.065),(.018,.012,.14),'Dark wood',.001).rotation_euler.x=math.radians(-28)

room=bpy.data.scenes['01 Furnished Study'];bpy.context.window.scene=room
for obj in room.objects:
    if obj.instance_collection and obj.instance_collection.name=='Plant_Broadleaf':obj.location=(1.80,2.95,0)

bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Blender/GrandStudy.blend'))
if '--no-preview' in sys.argv:raise SystemExit
# Transient review scene, excluded from the saved three-scene project.
preview=bpy.data.scenes.new('Botanical and art inspection');setup(preview);preview.cycles.samples=40
current=bpy.data.collections.new('Review ground');preview.collection.children.link(current)
block('Review ground',(0,0,-.04),(5,4,.08),'Walnut',.005)
for name,loc,scale,rot in [('Plant_Broadleaf',(-1.25,.3,0),1,0),('Plant_Palm',(0,.65,0),1,0),('Plant_Croton',(1.23,.4,0),1,0),('Bust_Sculpture',(-.73,-.74,0),1.65,0),('Bird_Sculpture',(.27,-.65,0),1.6,0),('Rose_Bouquet',(1.06,-.70,.0),1.5,0)]:
    o=bpy.data.objects.new(name+' review',None);o.instance_type='COLLECTION';o.instance_collection=bpy.data.collections[name];o.location=loc;o.scale=(scale,)*3;o.rotation_euler.z=rot;preview.collection.objects.link(o)
area(preview,'Softbox',(-2,-3,4),(0,0,.65),230,(1,.9,.75),3)
area(preview,'Rim light',(2,2,3),(0,0,.7),180,(.80,.90,1),2)
camera(preview,'Botanical closeup',(2.1,-5.5,3.1),(0,0,.72),ortho=3.75)
bpy.context.window.scene=preview;preview.render.filepath=str(OUT/'Art_Botanicals_Closeup.png');bpy.ops.render.render(write_still=True)
camera(preview,'Sculptures closeup',(.10,-2.50,.92),(-.31,-.69,.33),ortho=1.65)
preview.render.filepath=str(OUT/'Sculptures_Closeup.png');bpy.ops.render.render(write_still=True)
if '--geometry-only' not in sys.argv:
    paintings=bpy.data.scenes.new('Painting inspection');setup(paintings);paintings.cycles.samples=40
    current=bpy.data.collections.new('Painting review wall');paintings.collection.children.link(current)
    block('Painting wall',(0,.08,.65),(2.4,.08,1.45),'Dark wood',.005)
    for name,loc in [('Picture_Landscape',(-.48,0,.32)),('Picture_Portrait',(.59,0,.28)),('Photo_Frame',(.17,-.045,.08))]:
        obj=bpy.data.objects.new(name+' review',None);obj.instance_type='COLLECTION';obj.instance_collection=bpy.data.collections[name];obj.location=loc;paintings.collection.objects.link(obj)
    area(paintings,'Painting diffuse light',(-1,-2,2),(0,0,.7),80,(1,.94,.83),2)
    camera(paintings,'Painting detail',(.17,-2.8,.94),(0,0,.65),ortho=2.2)
    bpy.context.window.scene=paintings;paintings.render.filepath=str(OUT/'Paintings_Closeup.png');bpy.ops.render.render(write_still=True)
print('ART BOTANICAL PREVIEWS COMPLETE',flush=True)
if '--preview-only' in sys.argv or '--geometry-only' in sys.argv:raise SystemExit
bpy.context.window.scene=room
shots=[('01_Entrance_to_Study',(2.53,-3.65,2.35),(-.20,1.1,1.35),23),('02_Window_to_Study',(-2.7,-.8,2.0),(.5,1.7,1.3),23),('03_Study_to_Lounge',(2.48,3.35,2.13),(-.3,-2.3,1.25),23),('04_Lounge_Corner',(2.5,.1,2.05),(-.65,-2.85,1),25)]
for name,pos,target,lens in shots:
    cam=room.camera;cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
    room.render.filepath=str(OUT/('Review_'+name+'.png'));bpy.ops.render.render(write_still=True)
    if name.startswith('01'):bpy.data.images['Render Result'].save_render(str(OUT/'Study_Overview.png'),scene=room)
    if name.startswith('04'):bpy.data.images['Render Result'].save_render(str(OUT/'Study_Lounge.png'),scene=room)
for name,file in [('02 Asset Catalogue','Asset_Catalogue.png'),('03 Furniture Detail','Furniture_Detail.png')]:
    sc=bpy.data.scenes[name];bpy.context.window.scene=sc;sc.render.filepath=str(OUT/file);bpy.ops.render.render(write_still=True)
print('ART BOTANICAL PASS COMPLETE',flush=True)
