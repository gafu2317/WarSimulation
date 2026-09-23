"""Editable, metre-scale study kit and furnished example. No external textures."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2]
OUT=R/'ArtSource/Blender/GrandStudy.blend'
REVIEW=R/'docs/Art/GrandStudy'
random.seed(17)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.name='01 Furnished Study';scene.unit_settings.system='METRIC'
M={};assets={};current=None

def material(name,color,rough=.45,metal=0,texture=None):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    n=m.node_tree.nodes;p=next(t for t in n if t.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    if texture:
        tex=n.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=5 if texture=='wood' else 75
        coord=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(3,3,35) if texture=='wood' else (1,1,1)
        m.node_tree.links.new(coord.outputs['Generated'],mapping.inputs[0]);m.node_tree.links.new(mapping.outputs[0],tex.inputs['Vector'])
        ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(*(v*.52 for v in color),1);ramp.color_ramp.elements[1].color=(*(min(1,v*1.25) for v in color),1)
        m.node_tree.links.new(tex.outputs['Fac'],ramp.inputs[0]);m.node_tree.links.new(ramp.outputs[0],p.inputs['Base Color'])
        bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.012 if texture=='wood' else .004
        m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs[0],p.inputs['Normal'])
    M[name]=m
for args in [('Walnut',(.065,.021,.009),.32,0,'wood'),('Dark wood',(.043,.024,.016),.35,0,'wood'),('Brass',(.53,.32,.105),.3,.72,None),('Leather',(.048,.018,.011),.52,0,'leather'),('Red velvet',(.27,.016,.026),.8,0,'fabric'),('Cream',(.72,.65,.46),.72,0,'fabric'),('Piping',(.46,.39,.25),.8,0,'fabric'),('Wallpaper',(.32,.35,.22),.9,0,'fabric'),('Plaster',(.66,.62,.48),.9,0,None),('Porcelain',(.82,.79,.67),.23,0,None),('Black',(.014,.018,.019),.32,.2,None),('Paper',(.73,.64,.43),.85,0,'fabric'),('Ink',(.055,.034,.022),.9,0,None),('Green',(.035,.19,.065),.65,0,None),('Leaf light',(.13,.32,.055),.7,0,None),('Red flower',(.42,.018,.028),.6,0,None),('Blue book',(.023,.068,.10),.55,0,None),('Red book',(.20,.026,.02),.55,0,None),('Green book',(.036,.09,.061),.55,0,None),('Rug',(.19,.067,.035),.95,0,'fabric'),('Rug dark',(.026,.047,.042),.95,0,'fabric'),('Sea',(.11,.18,.19),.7,0,None),('Land',(.49,.38,.20),.8,0,None)]:material(*args)
for name,color in [('Ochre book',(.18,.10,.035)),('Brown book',(.09,.027,.012)),('Black book',(.02,.019,.015)),('Parchment book',(.36,.27,.14)),('Burgundy book',(.11,.013,.02)),('Curtain blue',(.065,.079,.095))]:material(name,color,.78,0,'fabric')
material('Sheer',(.74,.69,.55),.95)
m=M['Sheer'];nodes=m.node_tree.nodes;links=m.node_tree.links
principled=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');mix.inputs[0].default_value=.24
links.new(transparent.outputs[0],mix.inputs[1]);links.new(principled.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
BOOK_COLORS=['Red book','Blue book','Green book','Ochre book','Brown book','Black book','Parchment book','Burgundy book']
material('Window glass',(.61,.75,.78),.16)
p=next(t for t in M['Window glass'].node_tree.nodes if t.type=='BSDF_PRINCIPLED');p.inputs['Transmission Weight'].default_value=.8;p.inputs['Alpha'].default_value=.3

def asset(name,cat):
    global current
    current=bpy.data.collections.new(name);current.use_fake_user=True;assets[name]=(current,cat)
    current['category']=cat;current['units']='metres';return current

def finish(o,name,mat):
    o.name=name
    for c in list(o.users_collection):c.objects.unlink(o)
    current.objects.link(o)
    if mat:o.data.materials.append(M[mat])
    return o

def box(name,loc,size,mat='Walnut',bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=finish(bpy.context.object,name,mat);o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft edges','BEVEL');mod.width=bevel;mod.segments=2
        mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o

def mesh(name,vs,fs,mat):
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new(name,me);current.objects.link(o);o.data.materials.append(M[mat]);return o

def ball(name,loc,scale,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=loc);o=finish(bpy.context.object,name,mat);o.scale=scale
    for p in o.data.polygons:p.use_smooth=True
    return o

def curve(name,pts,r=.012,mat='Brass',closed=False):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=r;c.bevel_resolution=2
    sp=c.splines.new('POLY');sp.points.add(len(pts)-1)
    for p,v in zip(sp.points,pts):p.co=(*v,1)
    sp.use_cyclic_u=closed;o=bpy.data.objects.new(name,c);current.objects.link(o);c.materials.append(M[mat]);return o

def lathe(name,profile,loc=(0,0,0),mat='Walnut',n=32):
    vs=[(loc[0]+r*math.cos(i*math.tau/n),loc[1]+r*math.sin(i*math.tau/n),loc[2]+z) for r,z in profile for i in range(n)]
    fs=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(profile)-1) for i in range(n)]
    o=mesh(name,vs,fs,mat)
    for p in o.data.polygons:p.use_smooth=True
    return o

def ring(name,center,r,thick=.01,mat='Brass',plane='XY'):
    pts=[]
    for i in range(64):
        a=i*math.tau/64;u,v=r*math.cos(a),r*math.sin(a)
        delta=(u,v,0) if plane=='XY' else ((u,0,v) if plane=='XZ' else (0,u,v))
        pts.append(tuple(center[j]+delta[j] for j in range(3)))
    return curve(name,pts,thick,mat,True)

def frame(x,y,z,w,h,mat='Brass',r=.009):
    return curve('Panel moulding',[(x-w/2,y,z-h/2),(x+w/2,y,z-h/2),(x+w/2,y,z+h/2),(x-w/2,y,z+h/2)],r,mat,True)

def scroll(x,y,z,size=.16):
    for sign in [-1,1]:
        pts=[]
        for i in range(35):
            a=i/34*math.pi*2;rad=size*(1-i/42)
            pts.append((x+sign*(size+rad*math.cos(a)),y,z+rad*.45*math.sin(a)))
        curve('Gilded scroll',pts,.007)

def leg(x,y,h):
    lathe('Turned leg',[(0,0),(.045,0),(.052,.045),(.03,h*.26),(.047,h*.48),(.034,h*.7),(.065,h*.91),(.065,h)],(x,y,0))

def table(w,d,h=.76):
    box('Solid tabletop',(0,0,h-.04),(w,d,.08),bevel=.035)
    box('Gold table edge',(0,0,h-.065),(w+.005,d+.005,.012),'Brass',.009)
    box('Apron',(0,0,h-.17),(w-.12,d-.12,.2))
    for x in [-w/2+.12,w/2-.12]:
        for y in [-d/2+.12,d/2-.12]:leg(x,y,h-.2)
    ornament_size=min(.18,max(.08,(w-.16)/4))
    for yy in [-d/2+.051,d/2-.051]:scroll(0,yy,h-.17,ornament_size)

def book(x,y,z,w=.07,h=.26,d=.18,color='Red book',style=0,lean=0):
    before=set(current.objects)
    box('Book pages',(x,y,z+h/2),(w-.012,d-.018,h-.018),'Paper',.003)
    for xx in [-w/2,w/2]:box('Leather book cover',(x+xx,y,z+h/2),(.008,d,h),color,.003)
    box('Rounded book spine',(x,y-d/2,z+h/2),(w,.025,h),color,min(.014,w*.2))
    for zz in ([.025,h-.025] if style%3==0 else [.024,h*.22,h*.75,h-.024]):
        box('Raised spine band',(x,y-d/2-.014,z+zz),(w*.95,.009,.006),'Brass',.002)
    if style%3!=2:
        box('Title label',(x,y-d/2-.018,z+h*.64),(w*.72,.005,h*.15),'Brown book' if color!='Brown book' else 'Black book',.002)
        for j in range(3):box('Gilded title line',(x,y-d/2-.022,z+h*(.60+j*.032)),(w*(.45 if j==1 else .57),.002,.0015),'Brass',0)
    if style%3==1:
        for t in [.34,.45]:frame(x,y-d/2-.019,z+h*t,w*.44,h*.06,'Brass',.0015)
    if lean:
        from mathutils import Matrix
        rot=Matrix.Rotation(lean,4,'Y');pivot=Vector((x,y,z))
        for obj in set(current.objects)-before:
            obj.location=pivot+rot.to_3x3()@(obj.location-pivot);obj.rotation_euler.rotate(rot)

def book_stack(x,y,z,count=3):
    height=z
    for i in range(count):
        thickness=random.uniform(.035,.065);width=random.uniform(.24,.31);depth=random.uniform(.17,.23)
        box('Horizontal page block',(x,y,height+thickness/2),(width,depth,thickness-.009),'Paper',.003)
        for zz in [height,height+thickness]:box('Horizontal book cover',(x,y,zz),(width+.014,depth+.012,.006),BOOK_COLORS[i%len(BOOK_COLORS)],.003)
        box('Horizontal spine',(x,y-depth/2-.006,height+thickness/2),(width,.014,thickness),BOOK_COLORS[i%len(BOOK_COLORS)],.005)
        height+=thickness+.009

def panel_front(x,y,z,w,h):
    box('Raised wooden panel',(x,y,z),(w,.035,h),'Walnut',.015);frame(x,y-.023,z,w-.06,h-.06)

def cabinet(w,d,h,shelves=False):
    box('Back',(0,d/2-.025,h/2),(w,.05,h),'Dark wood')
    for x in [-w/2+.04,w/2-.04]:box('Upright',(x,0,h/2),(.08,d,h))
    for z in [.07,h-.06]:box('Plinth cornice',(0,0,z),(w+.1,d+.08,.12))
    if shelves:
        for z in [.57,1.12,1.67,2.22]:
            if z>h-.2:continue
            box('Shelf',(0,0,z),(w-.1,d-.03,.055))
            x=-w/2+.13;i=0
            while x<w/2-.13:
                if z==2.22 and -.39<x<.36:
                    x=.40;continue
                if i%13==7 and x+.33<w/2-.12 and z!=2.22:
                    book_stack(x+.14,-.02,z+.03,3);x+=.35;i+=1;continue
                bw=random.uniform(.032,.085);bh=random.uniform(.22,.43);bd=random.uniform(.18,.26)
                lean=math.radians(-7) if i%9==5 else 0
                reserve=bh*abs(math.sin(lean))
                book(x+bw/2+reserve,-.005+random.uniform(-.02,.025),z+.03,bw,bh,bd,random.choice(BOOK_COLORS),i,lean)
                x+=bw+.009+reserve;i+=1
        panel_front(0,-d/2-.01,.28,w-.12,.4)
    else:
        for x in [-w*.25,w*.25]:
            panel_front(x,-d/2-.025,h/2,w*.46,h-.22);ball('Brass handle',(x+(.1 if x<0 else -.1),-d/2-.07,h*.56),(.017,.025,.017),'Brass')
    for x in [-w/2+.06,w/2-.06]:frame(x,-d/2-.024,h/2,.055,h-.22)
    scroll(0,-d/2-.08,h-.10,.13)

def wallpaper_pattern(rectangles):
    # Keep one repeat and one margin across every module so adjacent walls do not
    # produce the conspicuous blank bands caused by per-panel integer rounding.
    for cx,cz,width,height in rectangles:
        columns=max(1,round(width/.24))
        rows=max(1,round(height/.28))
        step_x=width/columns
        step_z=height/rows
        half_width=min(.10,step_x*.40)
        half_height=min(.13,step_z*.43)
        for column in range(columns):
            x=cx-width/2+(column+.5)*step_x
            for row in range(rows):
                z=cz-height/2+(row+.5)*step_z
                curve('Wallpaper diamond',[(x,-.014,z-half_height),(x+half_width,-.014,z),(x,-.014,z+half_height),(x-half_width,-.014,z)],.0025,'Cream',True)

# Architecture: wall fronts face local -Y, wall pivots at the bottom centre.
for w in [1,2]:
    asset(f'Wall_Panel_{w}m','Architecture');box('Plaster wall',(0,.1,1.8),(w,.2,3.6),'Wallpaper',0)
    box('Wainscot backing',(0,-.018,.55),(w+.02,.045,1.1),'Dark wood',.004)
    for x in [-w/2+.25+i*.5 for i in range(int(w/.5))]:panel_front(x,-.046,.53,.44,.84)
    for z,h in [(.09,.15),(1.10,.07),(3.5,.16)]:box('Wall trim',(0,-.07,z),(w+.04,.12,h),'Walnut',.006)
    wallpaper_pattern([(0,2.34,w-.18,2.20)])
asset('Wall_WindowOpening_2m','Architecture')
for x in [-.925,.925]:box('Window wall pier',(x,.1,1.8),(.15,.2,3.6),'Wallpaper',0)
box('Window wall below',(0,.1,.4),(1.7,.2,.8),'Wallpaper',0)
box('Window wainscot backing',(0,-.018,.4),(2.02,.045,.8),'Dark wood',.004)
for x in [-.56,0,.56]:panel_front(x,-.046,.4,.48,.62)
for x in [-.925,.925]:panel_front(x,-.046,.4,.10,.62)
box('Window lower trim',(0,-.07,.09),(2.04,.12,.15),'Walnut',.006)
box('Window sill trim',(0,-.07,.81),(1.82,.16,.10),'Walnut',.006)
for x in [-.925,.925]:box('Window pier chair rail',(x,-.07,1.10),(.15,.12,.07),'Walnut',.006)
for x in [-.925,.925]:box('Window opening side casing',(x,-.09,2.0),(.17,.16,2.4),'Walnut',.008)
box('Window wall above',(0,.1,3.4),(1.7,.2,.4),'Wallpaper',0)
box('Window crown trim',(0,-.07,3.5),(2.04,.12,.16),'Walnut',.006)
wallpaper_pattern([(0,3.36,1.48,.28)])
asset('Window_Tall_1p7m','Architecture')
for x in [-.84,.84]:box('Window jamb',(x,0,1.2),(.1,.15,2.4))
for z in [0,2.4]:box('Window horizontal frame',(0,0,z),(1.78,.16,.1))
box('Sill',(0,-.06,-.035),(1.94,.35,.1));box('Window glass',(0,.025,1.2),(1.6,.012,2.3),'Window glass',0)
for x in [-.4,0,.4]:box('Mullion',(x,-.04,1.2),(.034,.06,2.3))
for z in [.6,1.2,1.8]:box('Transom',(0,-.04,z),(1.6,.06,.034))
asset('Door_Frame','Architecture')
for x in [-.67,.67]:box('Door jamb',(x,0,1.25),(.16,.24,2.5));frame(x,-.13,1.25,.07,2.3)
box('Door lintel',(0,0,2.55),(1.55,.28,.16));scroll(0,-.15,2.56,.18)
asset('Door_Leaf','Architecture');box('Door',(0,0,1.23),(1.16,.075,2.46))
for z,h in [(.47,.65),(1.62,1.36)]:panel_front(0,-.057,z,.92,h)
ball('Door knob',(.43,-.12,1.13),(.045,.04,.045),'Brass')
asset('Wall_DoorOpening_2m','Architecture')
for x in [-.85,.85]:box('Door wall pier',(x,.1,1.8),(.3,.2,3.6),'Wallpaper',0)
box('Above door',(0,.1,3.13),(1.4,.2,.94),'Wallpaper',0)
for x in [-.85,.85]:
    box('Door pier wainscot backing',(x,-.018,.55),(.3,.045,1.1),'Dark wood',.004)
    panel_front(x,-.046,.53,.22,.84)
box('Door lower trim',(0,-.07,.09),(2.04,.12,.15),'Walnut',.006)
box('Door crown trim',(0,-.07,3.5),(2.04,.12,.16),'Walnut',.006)
wallpaper_pattern([(-.85,2.34,.24,2.20),(.85,2.34,.24,2.20),(0,3.15,1.18,.62)])
asset('Floor_2m','Architecture')
for i in range(10):
    for j in range(2):box('Floorboard',(-.9+i*.2,-.5+j, -.045),(.196,.996,.09),'Walnut',.002)
asset('Ceiling_Coffer_2m','Architecture');box('Ceiling plaster',(0,0,.035),(2,2,.07),'Plaster',0)
for x in [-.92,.92]:box('Ceiling beam',(x,0,-.055),(.16,2,.11),'Dark wood',.012)
for y in [-.92,.92]:box('Ceiling beam',(0,y,-.055),(1.7,.16,.11),'Dark wood',.012)
curve('Ceiling gilding',[(-.81,-.81,-.05),(.81,-.81,-.05),(.81,.81,-.05),(-.81,.81,-.05)],.008,'Brass',True)
for name,size in [('Cornice_2m',(2,.18,.16)),('Skirting_2m',(2,.07,.16))]:
    asset(name,'Architecture');box(name,(0,0,size[2]/2),size);box('Gold strip',(0,-size[1]/2-.003,size[2]*.72),(2,.009,.015),'Brass',.002)
asset('Vent_Lattice','Architecture');box('Vent recess',(0,0,.18),(1.2,.055,.36),'Black')
frame(0,-.04,.18,1.24,.4,'Walnut',.035)
for i in range(11):
    x=-.5+i*.1
    curve('Lattice',[(x-.08,-.05,.04),(x+.08,-.05,.32)],.012,'Walnut')
    curve('Lattice',[(x+.08,-.05,.04),(x-.08,-.05,.32)],.012,'Walnut')
def curtain():
    box('Curtain pelmet',(0,0,2.55),(2.24,.30,.17));scroll(0,-.17,2.55,.25)
    top=2.43;tie=.53;bottom=-.69
    for side in [-1,1]:
        vs=[];nu=48;nv=64
        for j in range(nv+1):
            z=bottom+(top-bottom)*j/nv
            if z>=tie:
                q=(top-z)/(top-tie)
                center=.72+.25*q*q
                width=.58*(1-q)+.16*q
                amplitude=.036*(1-q)+.009*q
            else:
                q=(tie-z)/(tie-bottom)
                center=.97+.015*q
                width=.16+.16*q
                amplitude=.009+.022*q
            for i in range(nu+1):
                u=i/nu
                x=side*(center+(u-.5)*width)
                y=-.16+amplitude*math.cos(u*math.tau*5)-.018*math.sin((z-bottom)/(top-bottom)*math.pi)
                vs.append((x,y,z+.003*math.cos(u*math.tau*5)*(1-j/nv)))
        fs=[(j*(nu+1)+i,j*(nu+1)+i+1,(j+1)*(nu+1)+i+1,(j+1)*(nu+1)+i) for j in range(nv) for i in range(nu)]
        if side<0:fs=[tuple(reversed(f)) for f in fs]
        obj=mesh('Hanging gathered drape',vs,fs,'Curtain blue');obj.modifiers.new('Cloth thickness','SOLIDIFY').thickness=.003
        for poly in obj.data.polygons:poly.use_smooth=True
        # Cord hugs the narrow bundle; the lower fabric falls below it without swinging inward.
        curve('Tieback cord',[(side*.97+.086*math.cos(i*math.tau/48),-.17+.024*math.sin(i*math.tau/48),tie) for i in range(49)],.005)
        curve('Tieback tassel cord',[(side*.94,-.20,tie),(side*.94,-.20,tie-.16)],.004)
        lathe('Tieback tassel',[(0,0),(.015,0),(.009,.05),(0,.065)],(side*.94,-.20,tie-.22),'Brass',16)
        for i in range(6):
            x=side*(.72+(i/5-.5)*.58)
            ring('Curtain suspension ring',(x,-.14,2.455),.018,.003,'Brass','YZ')
    vs=[(-.82+i*1.64/40,.065+.008*math.cos(i*math.tau/5),.13+j*2.29/16) for j in range(17) for i in range(41)]
    mesh('Inner sheer curtain',vs,[(j*41+i,j*41+i+1,(j+1)*41+i+1,(j+1)*41+i) for j in range(16) for i in range(40)],'Sheer')
asset('Curtain_Pair','Architecture');curtain()
# Furniture.
asset('Desk_Pedestal','Furniture');box('Desk top',(0,0,.79),(1.95,.92,.10),bevel=.035)
box('Leather writing inset',(0,-.02,.843),(1.62,.64,.012),'Leather',.014)
for x in [-.7,.7]:
    box('Drawer pedestal',(x,0,.38),(.47,.76,.74))
    for z in [.18,.4,.62]:
        panel_front(x,-.398,z,.39,.18);curve('Drawer pull',[(x-.06,-.435,z),(x-.06,-.465,z-.025),(x+.06,-.465,z-.025),(x+.06,-.435,z)],.009)
box('Centre drawer',(0,0,.69),(.84,.76,.14));scroll(0,-.39,.69,.16)
for name,w,d,h in [('Meeting_Table',2.25,1.05,.78),('Coffee_Table',1.3,.72,.43),('Side_Table',.55,.5,.63)]:asset(name,'Furniture');table(w,d,h)
asset('Sideboard','Furniture');cabinet(1.65,.48,.92)
asset('Small_Cabinet','Furniture');cabinet(.56,.5,.82)
asset('Bookcase_Filled','Furniture');cabinet(2.4,.43,2.85,True)
asset('Display_Cabinet','Furniture');cabinet(.95,.43,2.7,True)
for x in [-.23,.23]:frame(x,-.25,1.61,.42,1.89,'Walnut',.025)

def tilt_parts(objects,pivot,angle=-7):
    from mathutils import Matrix
    rot=Matrix.Rotation(math.radians(angle),4,'X');pivot=Vector(pivot)
    for obj in objects:
        obj.location=pivot+rot.to_3x3()@(obj.location-pivot);obj.rotation_euler.rotate(rot)

def upholstered_back(w,h,bottom,mat='Leather',tuft=False,wing=False):
    before=set(current.objects)
    # A rigid rectangular back, softly rounded upholstery, and one common recline angle.
    box('Structural wooden back',(0,.35,bottom+h/2),(w,.075,h),'Walnut',.04)
    box('Padded back cushion',(0,.285,bottom+h/2),(w-.065,.115,h-.055),mat,.045)
    if tuft:
        for i in range(8 if w>1.4 else 3):
            count=8 if w>1.4 else 3
            for t in [.34,.68]:
                x=-w/2+.12+(i+.5)*(w-.24)/count
                ball('Leather button',(x,.222,bottom+h*t),(.009,.005,.009),mat)
    tilt_parts(set(current.objects)-before,(0,.30,bottom),-7)

def timber_between(name,a,b,width=.045,depth=.055):
    delta=Vector(b)-Vector(a);obj=box(name,(Vector(a)+Vector(b))/2,(width,depth,delta.length),'Walnut',.007)
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler()

def chair(arm=False):
    for x in [-.22,.22]:
        for y in [-.21,.21]:leg(x,y,.44)
    box('Seat frame',(0,0,.44),(.52,.50,.085));box('Seat cushion',(0,-.012,.495),(.47,.45,.09),'Red velvet',.035)
    before=set(current.objects)
    for x in [-.22,.22]:box('Solid back stile',(x,.21,.79),(.048,.065,.66),'Walnut',.012)
    for z in [.585,1.105]:box('Back frame rail',(0,.21,z),(.48,.067,.065),'Walnut',.014)
    box('Upholstery backing',(0,.22,.845),(.398,.048,.48),'Walnut',.018)
    box('Red upholstered back insert',(0,.18,.845),(.384,.062,.472),'Red velvet',.023)
    tilt_parts(set(current.objects)-before,(0,.21,.48),-7)
    if arm:
        for x in [-.285,.285]:
            timber_between('Arm support',(x,-.17,.48),(x,-.17,.69),.035,.035)
            curve('Wooden armrest',[(x,-.20,.70),(x,.02,.72),(x,.245,.73)],.023,'Walnut')
for arm in [False,True]:asset('Chair_Arms' if arm else 'Chair_Red','Furniture');chair(arm)

def sofa(w,wing=False):
    for x in [-w/2+.14,w/2-.14]:
        for y in [-.31,.31]:leg(x,y,.22)
    box('Sofa frame',(0,0,.27),(w,.88,.16),bevel=.07)
    n=3 if w>1.5 else 1
    for i in range(n):box('Leather seat',( -w/2+.17+(i+.5)*(w-.34)/n,-.07,.46),((w-.36)/n,.70,.23),'Leather',.10)
    upholstered_back(w-.12,.83 if wing else .66,.54,'Leather',True,wing)
    for x in [-w/2+.06,w/2-.06]:
        box('Upholstered arm side',(x,.015,.49),(.19,.72,.38),'Leather',.065)
        ball('Rolled arm',(x,-.015,.69),(.13,.44,.13),'Leather')
        curve('Carved arm support',[(x,-.34,.27),(x,-.4,.53),(x,-.37,.68)],.035,'Walnut')
    for x in [-w/2+.2,w/2-.2]:
        o=box('Decorative cushion',(x,.07,.67),(.34,.16,.31),'Cream',.075);o.rotation_euler.y=(-.22 if x<0 else .22)
for name,w,wing in [('Sofa_ThreeSeat',2.05,False),('Armchair_Leather',.92,True)]:asset(name,'Furniture');sofa(w,wing)
asset('Plant_Stand','Furniture');lathe('Round top',[(0,.67),(.24,.67),(.25,.70),(0,.70)])
for a in [0,math.tau/3,math.tau*2/3]:leg(.16*math.cos(a),.16*math.sin(a),.67)
asset('Tea_Trolley','Furniture')
for z in [.18,.75]:box('Trolley shelf',(0,0,z),(.86,.46,.055))
for x in [-.36,.36]:
    for y in [-.18,.18]:leg(x,y,.74)
for x in [-.36,.36]:
    for y in [-.25,.25]:
        ring('Wheel',(x,y,.16),.15,.019,'Dark wood','XZ')
        for i in range(8):
            a=i*math.tau/8;curve('Spoke',[(x,y,.16),(x+.14*math.cos(a),y,.16+.14*math.sin(a))],.006)
curve('Trolley handle',[(.36,-.2,.75),(.48,-.2,.94),(.48,.2,.94),(.36,.2,.75)],.024,'Walnut')
# Reusable small objects.
for index in range(12):
    asset(f'Book_Variant_{index+1:02d}','Objects')
    book(0,0,0,.035+(index%4)*.016,.24+(index%5)*.038,.18+(index%3)*.025,BOOK_COLORS[index%8],index)
asset('Book_Closed','Objects');book(0,0,0,.08,.27,.19)
asset('Book_Stack','Objects')
for i in range(3):
    box('Pages',(0,0,.032+i*.064),(.29,.2,.05),'Paper',.004)
    for zz in [.007,.057]:box('Book cover',(0,0,zz+i*.064),(.31,.22,.008),'Red book' if i%2 else 'Green book',.003)
asset('Book_Open','Objects')
box('Open cover',(0,0,.008),(.42,.28,.016),'Red book',.005)
for sign in [-1,1]:
    vs=[(sign*.008,-.125,.023),(sign*.19,-.125,.025),(sign*.19,.125,.025),(sign*.008,.125,.023),(sign*.06,-.125,.048),(sign*.06,.125,.048)]
    mesh('Curved open pages',vs,[(0,4,5,3),(4,1,2,5)],'Paper')
    for j in range(16):curve('Printed line',[(sign*.075,-.105+j*.014,.047),(sign*.178,-.105+j*.014,.03)],.0008,'Ink')
asset('Paper_Letter','Objects');box('Sheet',(0,0,.001),(.21,.29,.002),'Paper',0)
for j in range(18):box('Handwriting line',(-.012 if j%3 else 0,-.115+j*.012,.0025),(.16 if j%3 else .13,.001,.0006),'Ink',0)
asset('Newspaper','Objects');box('Folded newspaper',(0,0,.006),(.38,.28,.012),'Paper',.002)
for i in range(3):
    for j in range(21):box('Newsprint',(-.122+i*.122,-.105+j*.01,.0125),(.10,.0008,.0004),'Ink',0)
box('Masthead',(0,.12,.0125),(.32,.014,.0006),'Ink',0)
asset('Writing_Set','Objects');box('Pen tray',(0,0,.018),(.4,.15,.036),'Dark wood')
for x in [-.13,.13]:
    lathe('Ink well',[(0,0),(.04,0),(.04,.055),(.024,.06),(.024,.075),(0,.075)],(x,0,.037),'Brass')
    curve('Quill shaft',[(x,0,.10),(x+.07,.03,.35)],.004,'Walnut')
    mesh('Quill feather',[(x+.015,0,.16),(x+.024,-.018,.28),(x+.07,.03,.35),(x+.07,.055,.24)],[(0,1,2,3)],'Cream')
asset('Vase_Ceramic','Objects')
lathe('Hollow porcelain vase',[(0,0),(.07,0),(.085,.03),(.12,.17),(.09,.26),(.045,.33),(.042,.43),(.052,.45),(.044,.45),(.034,.42),(.038,.34),(.08,.26),(.11,.17),(.065,.025),(0,.025)],mat='Porcelain')
for z,r in [(.035,.087),(.29,.065),(.43,.046)]:ring('Vase gilding',(0,0,z),r,.004)
asset('Rose_Bouquet','Objects')
for i in range(9):
    a=i*2.4;r=.09*math.sqrt(i/8);x=r*math.cos(a);y=r*math.sin(a);z=.37+random.random()*.1
    curve('Flower stem',[(0,0,0),(x*.5,y*.5,.2),(x,y,z)],.0035,'Green')
    for j in range(7):
        a=j*2.4;rr=.017+j*.004
        ball('Rose petal',(x+rr*math.cos(a),y+rr*math.sin(a),z+j*.003),(.027,.019,.02),'Red flower')
    mesh('Rose leaf',[(x*.5,y*.5,.16),(x*.5+.07,y*.5+.018,.22),(x*.5+.025,y*.5+.03,.25)],[(0,1,2)],'Green')
asset('Teacup_Saucer','Objects')
lathe('Saucer',[(0,0),(.09,0),(.095,.01),(.08,.018),(.035,.012),(0,.012)],mat='Porcelain')
lathe('Hollow cup',[(0,.014),(.035,.014),(.047,.065),(.057,.095),(.052,.095),(.043,.065),(.03,.022),(0,.022)],mat='Porcelain')
curve('Cup handle',[(.051,0,.08),(.082,0,.085),(.087,0,.058),(.06,0,.03),(.038,0,.033)],.007,'Porcelain');ring('Cup gold rim',(0,0,.095),.055,.002)
asset('Teapot','Objects')
lathe('Teapot body',[(0,0),(.06,0),(.11,.04),(.12,.1),(.09,.16),(.055,.18),(0,.18)],mat='Porcelain')
lathe('Lid',[(0,.184),(.061,.184),(.04,.2),(0,.205)],mat='Porcelain');ball('Lid knob',(0,0,.22),(.018,.018,.02),'Brass')
curve('Teapot handle',[(-.08,0,.15),(-.17,0,.19),(-.18,0,.07),(-.09,0,.035)],.015,'Porcelain')
curve('Spout',[(.08,0,.07),(.15,0,.10),(.18,0,.19)],.021,'Porcelain')
asset('Sugar_Bowl','Objects');lathe('Sugar bowl',[(0,0),(.04,0),(.075,.05),(.065,.10),(.04,.12),(0,.12)],mat='Porcelain');ball('Sugar lid knob',(0,0,.132),(.015,.015,.017),'Brass')
asset('Serving_Tray','Objects');box('Tray base',(0,0,.01),(.45,.31,.02),'Brass',.025)
curve('Raised tray rim',[(-.21,-.14,.025),(.21,-.14,.025),(.21,.14,.025),(-.21,.14,.025)],.01,'Brass',True)
asset('Candelabra','Lighting')
lathe('Candelabra stem',[(0,0),(.09,0),(.09,.02),(.04,.04),(.018,.10),(.025,.25),(.03,.3),(0,.3)],mat='Brass')
for x in [-.15,0,.15]:
    curve('Candle branch',[(0,0,.16),(x*.6,0,.13),(x,0,.20),(x,0,.29)],.012)
    lathe('Candle socket',[(0,0),(.043,0),(.04,.018),(.018,.03),(0,.03)],(x,0,.29),'Brass')
    lathe('Unlit candle',[(0,0),(.012,0),(.012,.16),(0,.16)],(x,0,.32),'Cream');curve('Wick',[(x,0,.48),(x,0,.486)],.002,'Black')

def lampshade(x,y,z,r,h,glass=False):
    lathe('Open lampshade',[(r,0),(r*.68,h),(r*.66,h),(r-.008,0)],(x,y,z),'Cream',32)
    for zz,rr in [(z,r),(z+h,r*.68)]:ring('Shade trim',(x,y,zz),rr,.008)
    if glass:
        for i in range(16):
            a=i*math.tau/16;curve('Lead shade seam',[(x+r*math.cos(a),y+r*math.sin(a),z),(x+r*.68*math.cos(a),y+r*.68*math.sin(a),z+h)],.0035,'Black')
        ring('Lead shade band',(x,y,z+h*.5),r*.84,.004,'Black')
asset('Desk_Lamp','Lighting');lathe('Lamp stand',[(0,0),(.13,0),(.14,.015),(.08,.05),(.03,.10),(.024,.36),(.05,.42),(.02,.49),(0,.5)],mat='Brass');lampshade(0,0,.44,.25,.16,True)
asset('Table_Lamp','Lighting');lathe('Ceramic lamp base',[(0,0),(.10,0),(.1,.03),(.06,.07),(.08,.23),(.045,.31),(.025,.37),(0,.38)],mat='Porcelain');lampshade(0,0,.34,.21,.23)
asset('Wall_Sconce','Lighting');ball('Backplate',(0,.025,.25),(.075,.028,.12),'Brass');curve('Sconce arm',[(0,0,.25),(0,-.21,.06),(0,-.27,.28)],.014);lampshade(0,-.27,.28,.12,.16)
asset('Chandelier','Lighting')
lathe('Chandelier central stem',[(0,0),(.08,0),(.09,.03),(.045,.12),(.025,.52),(.06,.63),(0,.64)],mat='Brass')
for i in range(8):
    a=i*math.tau/8;x=.54*math.cos(a);y=.54*math.sin(a)
    curve('Chandelier curved arm',[(0,0,.14),(x*.65,y*.65,.04),(x,y,.14),(x,y,.35)],.018)
    lampshade(x,y,.36,.12,.2)
    curve('Hanging chain',[(0,0,.60),(x*.5,y*.5,.3),(x,y,.34)],.005)
    ball('Crystal drop',(x*.75,y*.75,.055),(.02,.02,.05),'Porcelain')
for i in range(8):ring('Suspension link',(0,0,.67+i*.05),.033,.006,plane='XZ' if i%2 else 'YZ')
lathe('Ceiling rose',[(0,1.07),(.19,1.07),(.19,1.09),(.12,1.12),(0,1.12)],mat='Brass')
asset('Wall_Clock','Objects');box('Clock case',(0,0,.51),(.36,.14,1.02));frame(0,-.083,.51,.29,.93)
# Circular face and hands lie in the XZ plane.
o=lathe('Clock face',[(0,0),(.14,0),(.14,.01),(0,.01)],mat='Cream');o.rotation_euler.x=math.pi/2;o.location=(0,-.083,.77)
for i in range(12):
    a=i*math.tau/12;curve('Clock hour',[(.112*math.sin(a),-.10,.77+.112*math.cos(a)),(.13*math.sin(a),-.10,.77+.13*math.cos(a))],.003,'Ink')
curve('Clock hands',[(-.064,-.108,.81),(0,-.108,.77),(.07,-.108,.85)],.004,'Black');curve('Pendulum rod',[(0,-.10,.55),(0,-.10,.21)],.006)
ball('Pendulum bob',(0,-.10,.21),(.062,.016,.062),'Brass')
asset('Globe_Stand','Objects');ball('Terrestrial sphere',(0,0,.95),(.35,.35,.35),'Sea')
# Stylised fictional continents follow the sphere instead of spanning it as flat ngons.
continent_random=random.Random(17)
continent_centres=[(-2.45,.42,.30),(-1.55,-.28,.22),(-.55,.62,.25),(.25,-.48,.20),(1.15,.18,.31),(2.15,-.22,.24),(2.75,.66,.18)]
for lon,lat,size in continent_centres:
    segments=14
    radii=[size*(.72+continent_random.random()*.34) for _ in range(segments)]
    vs=[]
    for ring_index,ring_scale in enumerate([0,.48,1]):
        count=1 if ring_index==0 else segments
        for j in range(count):
            if ring_index==0:
                lo,la=lon,lat
            else:
                a=j*math.tau/segments
                radial=radii[j]*ring_scale
                lo=lon+radial*math.cos(a)/max(.35,math.cos(lat))
                la=max(-1.35,min(1.35,lat+radial*.72*math.sin(a)))
            radius=.356
            vs.append((radius*math.cos(la)*math.cos(lo),radius*math.cos(la)*math.sin(lo),.95+radius*math.sin(la)))
    fs=[]
    inner_start=1;outer_start=1+segments
    for j in range(segments):
        next_index=(j+1)%segments
        fs.append((0,inner_start+j,inner_start+next_index))
        fs.append((inner_start+j,outer_start+j,outer_start+next_index,inner_start+next_index))
    land=mesh('Invented continent',vs,fs,'Land')
    for polygon in land.data.polygons:polygon.use_smooth=True
ring('Globe meridian',(0,0,.95),.385,.015,plane='XZ');ring('Equatorial support',(0,0,.94),.41,.025,'Walnut')
for i in range(3):
    a=i*math.tau/3;leg(.3*math.cos(a),.3*math.sin(a),.93)
asset('Telephone','Objects');box('Telephone plinth',(0,0,.025),(.30,.23,.05),'Dark wood',.03);box('Telephone case',(0,0,.09),(.26,.19,.09),'Brass',.02)
o=lathe('Dial',[(0,0),(.081,0),(.081,.012),(0,.012)],mat='Cream');o.rotation_euler.x=math.radians(55);o.location=(0,-.07,.13)
for i in range(10):
    a=i*math.tau/10;ball('Dial finger hole',(.059*math.cos(a),-.075-.034*math.sin(a),.142+.049*math.sin(a)),(.009,.005,.009),'Black')
for x in [-.10,.10]:curve('Receiver cradle',[(x,0,.13),(x,0,.22)],.011)
curve('Receiver grip',[(-.115,0,.245),(-.07,0,.27),(.07,0,.27),(.115,0,.245)],.018,'Walnut')
for x in [-.115,.115]:lathe('Receiver bell',[(0,0),(.045,0),(.035,.025),(.022,.04),(0,.04)],(x,0,.20),'Black')
curve('Coiled telephone cord',[(.14+.012*math.cos(i*.6),.025+.012*math.sin(i*.6),.23-i*.002) for i in range(90)],.003,'Black')
asset('Gramophone','Objects');box('Gramophone base',(0,0,.085),(.34,.32,.17));lathe('Record',[(0,0),(.135,0),(.135,.01),(0,.01)],(0,0,.175),'Black')
curve('Tone arm',[(.12,.08,.18),(.13,.04,.23),(.035,-.045,.205)],.008)
curve('Horn neck',[(.12,.10,.16),(.15,.12,.35),(.04,.1,.41)],.025)
# Flared, genuinely open horn with rolled rim.
o=lathe('Open brass horn',[(.023,0),(.04,.10),(.09,.22),(.20,.36),(.205,.37),(.195,.365),(.082,.22),(.032,.10),(.017,0)],mat='Brass');o.location=(.04,.1,.41);o.rotation_euler=(math.radians(60),math.radians(-20),0)
asset('Chess_Set','Objects');box('Chess board',(0,0,.012),(.48,.48,.024),'Walnut',.004)
for x in range(8):
    for y in range(8):box('Chess square',(-.196+x*.056,-.196+y*.056,.025),(.056,.056,.003),'Cream' if (x+y)%2 else 'Dark wood',0)
for side in [-1,1]:
    for row in [0,1]:
        for i in range(8):
            x=-.196+i*.056;y=side*(.196-row*.056);h=.065 if row else [.07,.082,.088,.10,.112,.088,.082,.07][i];mat='Cream' if side<0 else 'Dark wood'
            lathe('Chess piece',[(0,0),(.019,0),(.02,.007),(.012,.016),(.008,h*.55),(.015,h*.74),(.010,h*.88),(0,h)],(x,y,.028),mat,16)
            if row:ball('Pawn head',(x,y,.028+h),(.012,.012,.012),mat)
            elif i in [0,7]:
                for a in range(4):box('Rook crenel',(x+.012*math.cos(a*math.pi/2),y+.012*math.sin(a*math.pi/2),.028+h),(.01,.01,.014),mat,.001)
            elif i in [1,6]:ball('Knight muzzle',(x,y-.006,.032+h),(.009,.017,.011),mat)
            elif i==4:curve('King cross',[(x-.009,y,h+.037),(x+.009,y,h+.037)],.0025,mat);curve('King cross',[(x,y,h+.028),(x,y,h+.052)],.0025,mat)
for name,w,d in [('Rug_Large',2.7,1.85),('Rug_Runner',.85,2.8)]:
    asset(name,'Textiles');box('Woven rug',(0,0,.008),(w,d,.016),'Rug',.006)
    for inset in [.06,.10,.18]:curve('Rug border',[(-w/2+inset,-d/2+inset,.017),(w/2-inset,-d/2+inset,.017),(w/2-inset,d/2-inset,.017),(-w/2+inset,d/2-inset,.017)],.009,'Cream',True)
    for x in [-w/2+.3+i*.23 for i in range(max(1,int((w-.4)/.23)))]:
        for y in [-d/2+.3+i*.25 for i in range(int((d-.4)/.25))]:
            curve('Woven lozenge',[(x,y-.085,.018),(x+.065,y,.018),(x,y+.085,.018),(x-.065,y,.018)],.012,'Rug dark',True)
    for i in range(int(w/.025)):
        for sign in [-1,1]:curve('Rug fringe',[(-w/2+i*.025,sign*d/2,.01),(-w/2+i*.025,sign*(d/2+.045),.009)],.002,'Cream')
asset('Lace_Doily','Textiles')
segments=48
outline=[(.145 if i%2==0 else .137)*Vector((math.cos(i*math.tau/segments),math.sin(i*math.tau/segments))) for i in range(segments)]
vs=[(p.x,p.y,z) for z in [0,.003] for p in outline]
fs=[tuple(reversed(range(segments))),tuple(range(segments,segments*2))]
for i in range(segments):
    next_index=(i+1)%segments
    fs.append((i,next_index,segments+next_index,segments+i))
mesh('Scalloped linen doily',vs,fs,'Cream')
for radius in [.105,.126]:
    ring('Embroidered border',(0,0,.004),radius,.0007,'Piping')

def leaf(start,end,width,mat):
    a,b=Vector(start),Vector(end);mid=(a+b)/2;delta=b-a;cross=delta.cross(Vector((0,0,1))).normalized()*width
    vs=[a,mid+cross, b,mid-cross,mid+Vector((0,0,.025))]
    mesh('Leaf blade',vs,[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],mat);curve('Leaf midrib',[a,vs[4],b],.0018,'Leaf light')
for name,palm in [('Plant_Broadleaf',False),('Plant_Palm',True),('Plant_Croton',False)]:
    asset(name,'Plants');lathe('Hollow planter',[(0,0),(.15,0),(.21,.27),(.22,.29),(.195,.29),(.18,.26),(.13,.025),(0,.025)],mat='Porcelain')
    lathe('Soil',[(0,0),(.18,0),(.18,.015),(0,.015)],(0,0,.25),'Dark wood')
    for stem in range(5 if palm else 3):
        a=stem*2.4;end=(.11*math.cos(a),.11*math.sin(a),1.25+stem*.10 if palm else .8+stem*.17)
        curve('Plant stem',[(0,0,.26),(end[0]*.6,end[1]*.6,.55),end],.009 if palm else .018,'Walnut')
        for j in range(7):
            aa=a+j*2.4;z=end[2]-(j%3)*.18;start=(end[0],end[1],z)
            tip=(end[0]+.40*math.cos(aa),end[1]+.40*math.sin(aa),z-.07)
            if palm:
                curve('Palm rachis',[start,((start[0]+tip[0])/2,(start[1]+tip[1])/2,z+.10),tip],.004,'Green')
                for k in range(1,7):
                    t=k/7;c=(start[0]+(tip[0]-start[0])*t,start[1]+(tip[1]-start[1])*t,z+.08*math.sin(t*math.pi))
                    for sign in [-1,1]:leaf(c,(c[0]+.16*math.cos(aa+sign*.7),c[1]+.16*math.sin(aa+sign*.7),c[2]-.14),.012,'Green')
            else:leaf(start,tip,.075,'Leaf light' if name=='Plant_Croton' and j%2 else 'Green')
asset('Bust_Sculpture','Objects');box('Bust plinth',(0,0,.025),(.15,.12,.05),'Porcelain');lathe('Bust pedestal',[(0,0),(.055,0),(.035,.065),(.065,.075),(0,.075)],(0,0,.05),'Porcelain')
ball('Sculpture shoulders',(0,0,.18),(.10,.055,.07),'Porcelain');ball('Sculpture neck',(0,0,.24),(.028,.025,.04),'Porcelain');ball('Sculpture head',(0,0,.31),(.053,.045,.068),'Porcelain');ball('Sculpture nose',(0,-.045,.30),(.011,.018,.014),'Porcelain')
asset('Bird_Sculpture','Objects');lathe('Bird base',[(0,0),(.09,0),(.09,.025),(0,.025)],mat='Dark wood')
for x in [-.025,.025]:curve('Bird leg',[(x,0,.025),(x,-.012,.15),(x,0,.23)],.005)
ball('Bird body',(0,0,.23),(.045,.095,.055),'Brass');curve('Curved bird neck',[(0,-.04,.25),(0,-.095,.33),(0,-.065,.41)],.012);ball('Bird head',(0,-.068,.41),(.021,.03,.022),'Brass');curve('Beak',[(0,-.09,.41),(0,-.15,.4)],.005)
asset('Trophy_Cup','Objects');box('Trophy base',(0,0,.02),(.10,.1,.04),'Dark wood');lathe('Trophy',[(0,.04),(.035,.04),(.017,.06),(.015,.12),(.05,.15),(.065,.22),(.059,.22),(.042,.155),(0,.145)],mat='Brass')
for sign in [-1,1]:curve('Trophy handle',[(sign*.045,0,.20),(sign*.095,0,.21),(sign*.07,0,.145),(sign*.035,0,.13)],.007)
# Original, stylised pictures; no cropped reference photographs or outside assets.
for name,w,h,portrait in [('Picture_Landscape',1.05,.68,False),('Picture_Portrait',.54,.75,True),('Photo_Frame',.18,.24,True)]:
    asset(name,'Pictures');box('Frame backing',(0,0,h/2),(w,.025,h),'Dark wood',.005)
    box('Picture field',(0,-.018,h/2),(w-.06,.007,h-.06),'Sea' if not portrait else 'Rug dark',0)
    frame(0,-.025,h/2,w-.018,h-.018,'Walnut',.018 if w>.3 else .008);frame(0,-.035,h/2,w-.058,h-.058,'Brass',.007 if w>.3 else .003)
    if portrait:
        ball('Portrait shoulders',(0,-.027,h*.32),(w*.29,.008,h*.19),'Red book')
        ball('Portrait head',(0,-.033,h*.63),(w*.13,.009,h*.14),'Paper')
        ball('Portrait hair',(-w*.025,-.03,h*.70),(w*.14,.008,h*.10),'Walnut')
        curve('Portrait collar',[(-w*.12,-.043,h*.49),(0,-.043,h*.41),(w*.12,-.043,h*.49)],.008 if w>.3 else .003,'Cream')
    else:
        for i in range(3):
            vs=[(-w*.45,-.024,h*.12),(w*.45,-.024,h*.12),(w*.45,-.024,h*(.30+i*.13))]
            vs.extend((w*.45-j*w*.09,-.024,h*(.30+i*.13)+math.sin(j*1.6+i)*h*.06) for j in range(11))
            mesh('Landscape hills',vs,[tuple(range(len(vs)))],['Land','Green','Rug dark'][i])
        for x in [-.1,0,.1]:box('Painted castle tower',(x,-.035,h*.48),(.065,.006,.16),'Paper',0)
    if name=='Photo_Frame':curve('Frame easel',[(0,.015,h*.7),(0,.12,0)],.007,'Walnut')
# Collection instances retain editability and reuse geometry in both scenes.
room=bpy.data.collections.new('Room assembly');scene.collection.children.link(room)
def place(name,loc=(0,0,0),rot=0,scale=1,target=None):
    o=bpy.data.objects.new(name,None);o.instance_type='COLLECTION';o.instance_collection=assets[name][0];o.location=loc;o.rotation_euler.z=math.radians(rot);o.scale=(scale,)*3
    (target or room).objects.link(o);return o
for x in [-2,0,2]:
    for y in [-3,-1,1,3]:place('Floor_2m',(x,y,0));place('Ceiling_Coffer_2m',(x,y,3.6))
for x in [-2,0,2]:place('Wall_Panel_2m',(x,4,0));place('Wall_Panel_2m',(x,-4,0),180)
for y in [-3,-1,1,3]:place('Wall_DoorOpening_2m' if y==-1 else 'Wall_Panel_2m',(3,y,0),-90)
for y in [-3.5,3.5]:place('Wall_Panel_1m',(-3,y,0),90)
place('Wall_Panel_2m',(-3,0,0),90)
for y in [-2,2]:
    place('Wall_WindowOpening_2m',(-3,y,0),90)
    place('Window_Tall_1p7m',(-2.99,y,.8),90);place('Curtain_Pair',(-2.79,y,.75),90);place('Vent_Lattice',(-2.88,y,.12),90)
place('Door_Frame',(2.96,-1,0),-90);place('Door_Leaf',(3,-1,0),-90)
for x in [-1.25,1.25]:place('Bookcase_Filled',(x,3.70,0))
place('Desk_Pedestal',(0,2.05,0),180);place('Chair_Arms',(0,2.99,0))
place('Meeting_Table',(0,.42,0),90)
for x in [-.94,.94]:
    for y in [-.18,.97]:place('Chair_Red',(x,y,0),90 if x<0 else -90)
place('Rug_Large',(0,2.25,0));place('Rug_Runner',(-1.32,.42,0));place('Rug_Runner',(1.32,.42,0))
place('Sofa_ThreeSeat',(-.85,-3.48,0),180);place('Armchair_Leather',(1.19,-3.07,0),-135)
place('Rug_Large',(-.65,-2.67,0));place('Coffee_Table',(-.85,-2.35,0))
place('Tea_Trolley',(.48,-2.13,0),90)
place('Display_Cabinet',(-2.40,-3.70,0),180)
place('Side_Table',(2.45,-3.40,0));place('Vase_Ceramic',(2.45,-3.40,.63))
place('Sideboard',(2.69,1.78,0),-90)
place('Small_Cabinet',(-2.65,-.03,0),90);place('Gramophone',(-2.64,-.03,.82),90)
place('Globe_Stand',(-2.08,.69,0));place('Wall_Clock',(-2.87,-.07,1.84),90)
place('Plant_Palm',(2.15,-2.64,0));place('Plant_Broadleaf',(2.32,3.22,0));place('Plant_Stand',(-2.12,-1.22,0));place('Plant_Croton',(-2.12,-1.22,.70),scale=.65)
place('Picture_Landscape',(2.87,1.49,1.7),-90);place('Picture_Portrait',(2.87,2.66,1.65),-90)
place('Picture_Landscape',(-.85,-3.87,1.8),180);place('Picture_Portrait',(.45,-3.87,1.73),180)
for y in [.0,3.53]:place('Wall_Sconce',(2.87,y,2.1),-90)
place('Chandelier',(0,.55,2.46))
place('Desk_Lamp',(-.62,2.0,.84));place('Telephone',(.60,2.05,.84),180);place('Writing_Set',(0,2.0,.85),180)
place('Paper_Letter',(.25,1.78,.85));place('Photo_Frame',(-.32,2.3,.85),180);place('Book_Stack',(.62,2.35,.84))
place('Chess_Set',(0,-.04,.78));place('Book_Open',(0,1.0,.78),90)
place('Candelabra',(-.93,-2.42,.43));place('Newspaper',(-1.18,-2.2,.43))
for x,y in [(-.47,-2.39),(-1.15,-2.54)]:place('Lace_Doily',(x,y,.432));place('Teacup_Saucer',(x,y,.438))
place('Serving_Tray',(.48,-2.14,.78));place('Teapot',(.44,-2.14,.805));place('Sugar_Bowl',(.58,-2.25,.805))
place('Vase_Ceramic',(.48,-1.88,.78));place('Rose_Bouquet',(.48,-1.88,1.10))
place('Table_Lamp',(2.55,1.39,.92));place('Bird_Sculpture',(2.55,2.17,.92));place('Photo_Frame',(2.55,1.78,.92),-90)
for name,x in [('Bust_Sculpture',-1.25),('Trophy_Cup',1.25)]:place(name,(x,3.65,2.25))

def camera(sc,name,pos,target,lens=24,ortho=None):
    c=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,c);sc.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();c.lens=lens;c.clip_start=.03
    if ortho:c.type='ORTHO';c.ortho_scale=ortho
    sc.camera=o;return o

def area(sc,name,pos,target,energy,color,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

def setup(sc):
    sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True
    sc.render.resolution_x=1440;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
    sc.world=bpy.data.worlds.new(sc.name+' World');sc.world.use_nodes=True;next(t for t in sc.world.node_tree.nodes if t.type=='BACKGROUND').inputs[0].default_value=(.34,.40,.52,1);next(t for t in sc.world.node_tree.nodes if t.type=='BACKGROUND').inputs[1].default_value=.22
    sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG'
setup(scene)
for y in [-2,2]:area(scene,'Window daylight',(-2.6,y,2.5),(0,y,1),300,(.80,.88,1),1.7)
area(scene,'Ceiling soft fill',(0,0,3.35),(0,0,0),110,(1,.78,.52),3)
area(scene,'Camera fill',(2.6,-3.6,2.8),(0,1,1),65,(1,.87,.7),2)
cam=camera(scene,'Study overview',(2.53,-3.65,2.35),(-.20,1.1,1.35),23)
camera(scene,'Lounge view',(2.5,.1,2.05),(-.65,-2.85,1),25)
scene.camera=cam
catalog=bpy.data.scenes.new('02 Asset Catalogue');catalog.unit_settings.system='METRIC';setup(catalog)
catalog_col=bpy.data.collections.new('All individual modules');catalog.collection.children.link(catalog_col)
records=[]
for i,(name,(col,cat)) in enumerate(assets.items()):
    x=(i%8)*3.7;y=(i//8)*4
    small = cat in ['Objects','Pictures','Textiles'] and name not in ['Rug_Large','Rug_Runner','Globe_Stand','Wall_Clock','Picture_Landscape','Picture_Portrait']
    scale = 3 if small else 1
    place(name,(x,y,.03),scale=scale,target=catalog_col)
    textdata=bpy.data.curves.new(name+' label','FONT');textdata.body=name.replace('_',' ') + ('  [3x]' if small else '')
    textdata.size=.115;textdata.align_x='CENTER';textdata.materials.append(M['Cream'])
    label=bpy.data.objects.new(name+' label',textdata);catalog_col.objects.link(label);label.location=(x,y-1.6,.025)

    points=[o.matrix_world@Vector(corner) for o in col.objects if o.type=='MESH' for corner in o.bound_box]
    # Transform updates are forced below before deriving final bounds.
    records.append({'name':name,'category':cat,'parts':len(col.objects)})
next(t for t in catalog.world.node_tree.nodes if t.type=='BACKGROUND').inputs[1].default_value=.65
area(catalog,'Catalogue key',(9,-8,18),(12,13,0),4500,(1,.85,.65),12)
area(catalog,'Catalogue fill',(25,15,14),(10,12,0),3000,(.68,.82,1),10)
camera(catalog,'Catalogue overview',(27,-31,48),(13,18,0),ortho=49)
# Independent preview scene with a small, representative furniture arrangement.
detail=bpy.data.scenes.new('03 Furniture Detail');setup(detail);dc=bpy.data.collections.new('Detail display');detail.collection.children.link(dc)
for name,loc,rot in [('Desk_Pedestal',(-1.2,0,0),0),('Desk_Lamp',(-1.75,0,.84),0),('Telephone',(-.65,0,.84),0),('Book_Open',(-1.2,-.10,.85),0),('Chair_Red',(-1.2,.85,0),0),('Sofa_ThreeSeat',(1.45,.55,0),0),('Coffee_Table',(1.45,-.65,0),0),('Teacup_Saucer',(1.65,-.65,.43),0),('Candelabra',(1.2,-.65,.43),0),('Plant_Palm',(2.85,1.1,0),0),('Rug_Large',(1.45,-.10,0),0)]:place(name,loc,rot,target=dc)
area(detail,'Detail key',(-3,-4,6),(0,0,.5),1100,(1,.87,.68),5);area(detail,'Detail fill',(4,2,5),(0,0,1),850,(.7,.83,1),4)
camera(detail,'Furniture camera',(6,-9,5.3),(.3,0,.75),ortho=7.5)
# Reopen verification metadata and render cameras are kept with the asset library.
bpy.context.view_layer.update()
for rec in records:
    col=assets[rec['name']][0]
    pts=[o.matrix_world@Vector(c) for o in col.objects if o.type=='MESH' for c in o.bound_box]
    if pts:rec['mesh_bounds_m']=[[round(min(p[j] for p in pts),4) for j in range(3)],[round(max(p[j] for p in pts),4) for j in range(3)]]
    col.asset_mark();col.asset_data.description=f"{rec['category']} | metre-scale antique study | independent reusable collection"
REVIEW.mkdir(parents=True,exist_ok=True)
(REVIEW/'manifest.json').write_text(json.dumps({'asset_count':len(records),'room_dimensions_m':[6,8,3.6],'unity_modified':False,'assets':records},indent=2))
bpy.context.window.scene=scene
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT))
print('SAVED',OUT,'ASSETS',len(records),flush=True)
for sc,filename in [(scene,'Study_Overview.png'),(detail,'Furniture_Detail.png'),(catalog,'Asset_Catalogue.png')]:
    bpy.context.window.scene=sc;sc.render.filepath=str(REVIEW/filename);bpy.ops.render.render(write_still=True)
scene.camera=bpy.data.objects['Lounge view'];bpy.context.window.scene=scene;scene.render.filepath=str(REVIEW/'Study_Lounge.png');bpy.ops.render.render(write_still=True)
# Regenerate the four review angles so their filenames always show the current model.
shots=[('01_Entrance_to_Study',(2.53,-3.65,2.35),(-.20,1.1,1.35),23),('02_Window_to_Study',(-2.7,-.8,2.0),(.5,1.7,1.3),23),('03_Study_to_Lounge',(2.48,3.35,2.13),(-.3,-2.3,1.25),23),('04_Lounge_Corner',(2.5,.1,2.05),(-.65,-2.85,1),25)]
for name,pos,target,lens in shots:
    cam=scene.camera;cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
    scene.render.filepath=str(REVIEW/('Review_'+name+'.png'));bpy.ops.render.render(write_still=True)
print('ALL RENDERS COMPLETE',flush=True)
