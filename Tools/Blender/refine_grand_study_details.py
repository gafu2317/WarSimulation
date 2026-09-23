"""Detail pass on the editable study: structural upholstery and restrained PBR materials."""
import bpy, math, random, ast, json, sys
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'docs/Art/GrandStudy'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/GrandStudy.blend'))
source=ast.parse((ROOT/'Tools/Blender/generate_grand_study.py').read_text())
M={m.name:m for m in bpy.data.materials};current=None
for node in source.body:
    if isinstance(node,ast.FunctionDef) and node.name in ['finish','box','mesh','ball','curve','lathe','ring','frame','scroll','leg','camera','area','setup']:
        exec(compile(ast.Module(body=[node],type_ignores=[]),'helpers','exec'),globals())

def surface(name,color,rough,kind,metal=0):
    mat=M.get(name) or bpy.data.materials.new(name);M[name]=mat
    mat.use_nodes=True;mat.diffuse_color=(*color,1);nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    out=nodes.new('ShaderNodeOutputMaterial');p=nodes.new('ShaderNodeBsdfPrincipled');links.new(p.outputs[0],out.inputs[0])
    p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    coord=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY'
    mapping.inputs[1].default_value=(3,55,3) if kind=='wood' else (1,1,1)
    links.new(coord.outputs['Object'],mapping.inputs[0])
    noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5 if kind=='wood' else 160 if kind=='leather' else 330
    noise.inputs['Detail'].default_value=3;links.new(mapping.outputs[0],noise.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.18;ramp.color_ramp.elements[1].position=.82
    ramp.color_ramp.elements[0].color=(*(c*.50 for c in color),1);ramp.color_ramp.elements[1].color=(*(c*1.3 for c in color),1)
    links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])
    roughmap=nodes.new('ShaderNodeMapRange');roughmap.inputs['To Min'].default_value=max(.1,rough-.10);roughmap.inputs['To Max'].default_value=min(.95,rough+.12)
    links.new(noise.outputs['Fac'],roughmap.inputs[0]);links.new(roughmap.outputs[0],p.inputs['Roughness'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.22;bump.inputs['Distance'].default_value=.00025 if kind=='wood' else .00065
    links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
    if kind=='wood':p.inputs['Coat Weight'].default_value=.23;p.inputs['Coat Roughness'].default_value=.26
    if kind in ['cloth','brocade']:p.inputs['Sheen Weight'].default_value=.32
    if kind=='brocade':
        uv=nodes.new('ShaderNodeTexCoord');sep=nodes.new('ShaderNodeSeparateXYZ');links.new(uv.outputs['Generated'],sep.inputs[0])
        waves=[]
        for channel in ['X','Z']:
            mult=nodes.new('ShaderNodeMath');mult.operation='MULTIPLY';mult.inputs[1].default_value=math.tau*5
            links.new(sep.outputs[channel],mult.inputs[0]);sin=nodes.new('ShaderNodeMath');sin.operation='SINE';links.new(mult.outputs[0],sin.inputs[0]);waves.append(sin)
        prod=nodes.new('ShaderNodeMath');prod.operation='MULTIPLY'
        for i,w in enumerate(waves):links.new(w.outputs[0],prod.inputs[i])
        motif=nodes.new('ShaderNodeValToRGB');motif.color_ramp.elements[0].position=.08;motif.color_ramp.elements[0].color=(.025,.016,.009,1);motif.color_ramp.elements[1].position=.28;motif.color_ramp.elements[1].color=(.42,.31,.13,1)
        links.new(prod.outputs[0],motif.inputs[0]);links.new(motif.outputs[0],p.inputs['Base Color'])
    return mat
surface('Walnut',(.041,.013,.006),.34,'wood')
surface('Dark wood',(.018,.012,.009),.4,'wood')
surface('Leather',(.030,.013,.009),.43,'leather')
surface('Back leather',(.025,.011,.008),.68,'leather')
surface('Red velvet',(.14,.006,.012),.75,'cloth')
surface('Curtain blue',(.037,.042,.049),.82,'cloth')
surface('Brass',(.33,.22,.09),.35,'metal',1)
surface('Brocade',(.4,.3,.13),.78,'brocade')
surface('Piping',(.065,.033,.009),.72,'cloth')
# Reduce applied ornament to shallow relief while keeping it clear of the wall face.
for name in ['Wall_Panel_1m','Wall_Panel_2m','Wall_WindowOpening_2m','Wall_DoorOpening_2m']:
    for obj in bpy.data.collections[name].objects:
        if obj.name.startswith('Wallpaper diamond'):
            obj.data.bevel_depth=.0025
            for spline in obj.data.splines:
                for point in spline.points:point.co.y=-.014
# Overlap modular rails slightly so bevels cannot expose seams at joins.
for name,width in [('Wall_Panel_1m',1.04),('Wall_Panel_2m',2.04)]:
    for obj in bpy.data.collections[name].objects:
        if obj.name.startswith('Wall trim'):obj.dimensions.x=width
for name in ['Wall_WindowOpening_2m','Wall_DoorOpening_2m']:
    for obj in bpy.data.collections[name].objects:
        if obj.name.startswith(('Window lower trim','Window crown trim','Door lower trim','Door crown trim')):obj.dimensions.x=2.04
current=bpy.data.collections['Wall_WindowOpening_2m']
for obj in list(current.objects):
    if obj.name.startswith('Window opening side casing'):bpy.data.objects.remove(obj,do_unlink=True)
for x in [-.925,.925]:box('Window opening side casing',(x,-.09,2.0),(.17,.16,2.4),'Walnut',.008)
for col in bpy.data.collections:
    if not col.get('category'):continue
    for obj in col.objects:
        if obj.type=='CURVE' and obj.name.startswith(('Gilded scroll','Panel moulding')):obj.data.bevel_depth=min(obj.data.bevel_depth,.003)
        if obj.type=='MESH':
            for mod in obj.modifiers:
                if mod.type=='BEVEL':mod.segments=4

def pillow(x,y,z,w=.40,h=.40):
    n=32;vs=[]
    for side in [-1,1]:
        for j in range(n+1):
            v=-1+2*j/n
            for i in range(n+1):
                u=-1+2*i/n
                xx=u*w/2*(1-.075*v*v);zz=v*h/2*(1-.075*u*u)
                depth=.012+.077*(max(0,1-u*u)*max(0,1-v*v))**.65
                vs.append((xx,side*depth,zz))
    count=(n+1)**2;fs=[]
    for j in range(n):
        for i in range(n):
            a=j*(n+1)+i;fs.extend([(a,a+1,a+n+2,a+n+1),(a+count,a+n+1+count,a+n+2+count,a+1+count)])
    edge=list(range(n+1))+[j*(n+1)+n for j in range(1,n+1)]+[n*(n+1)+i for i in range(n-1,-1,-1)]+[j*(n+1) for j in range(n-1,0,-1)]
    for a,b in zip(edge,edge[1:]+edge[:1]):fs.append((a,b,b+count,a+count))
    obj=mesh('Separate brocade cushion',vs,fs,'Brocade')
    for p in obj.data.polygons:p.use_smooth=True
    rot=Matrix.Rotation(math.radians(-10),3,'X');rotated=[rot@Vector(v) for v in vs];lift=z-min(v.z for v in rotated)
    for v,p in zip(obj.data.vertices,rotated):v.co=p+Vector((x,y,lift))
    pts=[]
    for i in edge:
        a=Vector(vs[i]);a.y=0;pts.append(rot@a+Vector((x,y,lift)))
    curve('Cushion edge seam',pts,.0025,'Piping',True)
    obj['bottom_contact_z']=z;obj['independent_cushion']=True

def padded_back(w,h):
    bottom=.52;n=64;m=32;vs=[];buttons=[]
    columns=9 if w>1.5 else 3
    for j,t in enumerate([.30,.62]):
        for i in range(columns):
            u=(i+.5+(j%2)*.20)/columns
            if u<.95:buttons.append((u,t))
    for rear in [False,True]:
        for j in range(m+1):
            t=j/m
            for i in range(n+1):
                u=i/n
                height=h-.055+.055*math.sin(u*math.pi)
                taper=1-.055*t*t
                x=(u-.5)*(w-.10)*taper
                dent=sum(.022*math.exp(-((u-bu)/.028)**2-((t-bt)/.06)**2) for bu,bt in buttons)
                bulge=.025*math.sin(math.pi*t)*math.sin(math.pi*u)
                y=.285+.055*t+(.105 if rear else -bulge+dent)
                vs.append((x,y,bottom+t*height))
    count=(n+1)*(m+1);fs=[]
    for j in range(m):
        for i in range(n):
            a=j*(n+1)+i;fs.extend([(a,a+n+1,a+n+2,a+1),(a+count,a+1+count,a+n+2+count,a+n+1+count)])
    edge=list(range(n+1))+[j*(n+1)+n for j in range(1,m+1)]+[m*(n+1)+i for i in range(n-1,-1,-1)]+[j*(n+1) for j in range(m-1,0,-1)]
    for a,b in zip(edge,edge[1:]+edge[:1]):fs.append((a,b,b+count,a+count))
    obj=mesh('Continuous tufted leather back',vs,fs,'Back leather')
    for p in obj.data.polygons:p.use_smooth=True
    curve('Back welt seam',[vs[i] for i in edge],.0025,'Back leather',True)
    for u,t in buttons:
        index=round(t*m)*(n+1)+round(u*n);x,y,z=vs[index]
        ball('Recessed covered button',(x,y-.001,z),(.007,.003,.007),'Back leather')

def seating(w,armchair=False):
    for x in [-w/2+.12,w/2-.12]:
        for y in [-.30,.30]:leg(x,y,.18)
    box('Carved sofa apron',(0,0,.235),(w,.83,.11),'Walnut',.025)
    box('Upholstered seat support',(0,.01,.31),(w-.13,.76,.08),'Leather',.025)
    n=1 if armchair else 3
    for i in range(n):
        width=(w-.34)/n;cx=-w/2+.17+(i+.5)*width
        box('Separate seat pad',(cx,-.045,.455),(width-.012,.67,.21),'Leather',.07)
        curve('Seat welt',[(cx-width/2+.03,-.352,.46),(cx+width/2-.03,-.352,.46),(cx+width/2-.03,.26,.46),(cx-width/2+.03,.26,.46)],.0025,'Leather',True)
    padded_back(w,.64 if armchair else .56)
    for side in [-1,1]:
        x=side*(w/2-.065)
        box('Leather arm body',(x,.01,.49),(.20,.72,.37),'Leather',.075)
        ball('Leather arm roll',(x,-.02,.685),(.12,.415,.105),'Leather')
        curve('Arm front wood trim',[(x,-.355,.28),(x,-.40,.45),(x,-.385,.62)],.022,'Walnut')
    if not armchair:
        for x in [-w/2+.46,w/2-.46]:pillow(x,-.05,.56)
    else:pillow(0,-.06,.56,.34,.35)

for name,w,arm in [('Sofa_ThreeSeat',2.05,False),('Armchair_Leather',.92,True)]:
    current=bpy.data.collections[name]
    for o in list(current.objects):bpy.data.objects.remove(o,do_unlink=True)
    seating(w,arm)
# Chair upholstery is seated inside the rails, with less proud padding.
for name in ['Chair_Red','Chair_Arms']:
    current=bpy.data.collections[name]
    for obj in current.objects:
        if obj.name.startswith('Red upholstered back insert'):
            obj.scale.y=.045/(max(v.co.y for v in obj.data.vertices)-min(v.co.y for v in obj.data.vertices))
        if obj.name.startswith('Wooden armrest'):
            end=obj.data.splines[0].points[-1];end.co.x=math.copysign(.22,end.co.x)
# The grotesquely thick top trim came from copy-pasted cabinet cornices: use a flat solid top.
for name in ['Sideboard','Small_Cabinet','Display_Cabinet','Bookcase_Filled']:
    current=bpy.data.collections[name]
    for obj in list(current.objects):
        if obj.name.startswith('Plinth cornice') and obj.location.z>.2:
            for mod in obj.modifiers:
                if mod.type=='BEVEL':mod.width=.008
            obj['detail_audited']=True
# Woven motifs should not protrude as bars above a textile.
for colname in ['Rug_Large','Rug_Runner']:
    for obj in bpy.data.collections[colname].objects:
        if obj.type=='CURVE' and obj.name.startswith(('Rug border','Woven lozenge')):
            obj.data.bevel_depth=.0006
            for spline in obj.data.splines:
                for point in spline.points:point.co.z=.0166
# A six-petal floral repeat with nested rings, rather than a checkerboard.
mat=M['Brocade'];nodes=mat.node_tree.nodes;links=mat.node_tree.links
p=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
def mathnode(op,a,b=None):
    n=nodes.new('ShaderNodeMath');n.operation=op
    for i,value in enumerate([a,b] if b is not None else [a]):
        if isinstance(value,(int,float)):n.inputs[i].default_value=value
        else:links.new(value,n.inputs[i])
    return n.outputs[0]
coord=nodes.new('ShaderNodeTexCoord');sep=nodes.new('ShaderNodeSeparateXYZ');links.new(coord.outputs['Generated'],sep.inputs[0])
u=mathnode('SUBTRACT',mathnode('FRACT',mathnode('MULTIPLY',sep.outputs['X'],3)),.5)
v=mathnode('SUBTRACT',mathnode('FRACT',mathnode('MULTIPLY',sep.outputs['Z'],3)),.5)
r=mathnode('SQRT',mathnode('ADD',mathnode('MULTIPLY',u,u),mathnode('MULTIPLY',v,v)))
a=mathnode('ARCTAN2',v,u)
fl=mathnode('ADD',.235,mathnode('MULTIPLY',mathnode('COSINE',mathnode('MULTIPLY',a,6)),.055))
petals=mathnode('LESS_THAN',mathnode('ABSOLUTE',mathnode('SUBTRACT',r,fl)),.014)
inner=mathnode('LESS_THAN',mathnode('ABSOLUTE',mathnode('SUBTRACT',r,.09)),.012)
diamond=mathnode('LESS_THAN',mathnode('ABSOLUTE',mathnode('SUBTRACT',mathnode('ADD',mathnode('ABSOLUTE',u),mathnode('ABSOLUTE',v)),.47)),.01)
mask=mathnode('MAXIMUM',petals,mathnode('MAXIMUM',inner,diamond))
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.30,.23,.12,1);ramp.color_ramp.elements[1].color=(.023,.014,.006,1)
links.new(mask,ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])

# Lower global fill; preserve daylight direction and material contrast.
room=bpy.data.scenes['01 Furnished Study']
for obj in room.objects:
    if obj.type=='LIGHT' and 'Window daylight' in obj.name:obj.data.energy=240
    if obj.type=='LIGHT' and 'Ceiling soft' in obj.name:obj.data.energy=75
for sc in bpy.data.scenes:
    sc.cycles.samples=40
bpy.context.window.scene=room;bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Blender/GrandStudy.blend'))
if '--geometry-only' in sys.argv:raise SystemExit
# Close-ups first, before full-room renders.
detail=bpy.data.scenes['03 Furniture Detail'];bpy.context.window.scene=detail;original_camera=detail.camera
camera(detail,'Upholstery detail',(3.7,-4.8,2.5),(1.42,.25,.72),ortho=3.4)
detail.render.filepath=str(OUT/'Upholstery_Closeup.png');bpy.ops.render.render(write_still=True)
camera(detail,'Desk material detail',(-3,-3.8,2.1),(-1.2,0,.55),ortho=2.7)
detail.render.filepath=str(OUT/'Desk_Closeup.png');bpy.ops.render.render(write_still=True)
detail.camera=original_camera
for sc,file in [(detail,'Furniture_Detail.png'),(bpy.data.scenes['02 Asset Catalogue'],'Asset_Catalogue.png')]:
    bpy.context.window.scene=sc;sc.render.filepath=str(OUT/file);bpy.ops.render.render(write_still=True)
print('CLOSEUPS DONE',flush=True)
bpy.context.window.scene=room
shots=[('01_Entrance_to_Study',(2.53,-3.65,2.35),(-.20,1.1,1.35),23),('02_Window_to_Study',(-2.7,-.8,2.0),(.5,1.7,1.3),23),('03_Study_to_Lounge',(2.48,3.35,2.13),(-.3,-2.3,1.25),23),('04_Lounge_Corner',(2.5,.1,2.05),(-.65,-2.85,1),25)]
for name,pos,target,lens in shots:
    cam=room.camera;cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
    room.render.filepath=str(OUT/('Review_'+name+'.png'));bpy.ops.render.render(write_still=True)
    if name.startswith('01'):bpy.data.images['Render Result'].save_render(str(OUT/'Study_Overview.png'),scene=room)
    if name.startswith('04'):bpy.data.images['Render Result'].save_render(str(OUT/'Study_Lounge.png'),scene=room)
print('DETAIL PASS COMPLETE',flush=True)
