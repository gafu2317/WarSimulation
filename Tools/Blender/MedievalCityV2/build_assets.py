"""Name-driven stylized town kit. Existing geometry is never imported."""
from pathlib import Path
import argparse
import hashlib
import json
import math
import random
import sys

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import facility_buildings
import service_buildings
import landmark_structures
import quality_details


ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'ArtSource/Deliveries/MedievalCity_2026-10-22'
TEXTURE_INFO = {t['name']:t for t in json.loads((OUT/'texture_manifest.json').read_text())}
MATS = {}
COLLECTION = None
RAND = random.Random(0)
PI = math.pi

ADDITIONS = [
    'Fence_Panel', 'Fence_Post', 'Fence_Gate', 'Stone_Wall', 'Stone_Wall_Corner',
    'Crate_Open', 'Grain_Sack', 'Produce_Basket', 'Planter', 'Drain_Grate', 'Bollard', 'Bucket',
    'Fish_Stall', 'Bread_Stall', 'Spice_Stall', 'Flower_Stall', 'Butcher_Stall', 'Potion_Stall', 'Book_Stall', 'Weapon_Stall',
    'Covered_Wagon', 'Merchant_Wagon', 'Wheelbarrow', 'Market_Scale', 'Basket_Empty', 'Pottery_Jars',
    'Sack_Stack', 'Vegetable_Crate', 'Fish_Crate', 'Bread_Basket', 'Rolled_Fabric', 'Hanging_Herbs',
    'Shop_Sign', 'Shield_Rack', 'Weapon_Rack', 'Training_Dummy', 'Fountain', 'Wash_Tub',
    'Street_Brazier', 'Hanging_Lantern', 'Windmill', 'Watermill', 'Market_Hall',
    'Watchtower', 'Stone_Bridge', 'Wooden_Bridge', 'Dock_Section', 'Stone_Stairs', 'Wooden_Stairs', 'Outhouse',
]

BUILDING_SPECS = {'Fantasy_House': (5.6, 6.8, 5.5, 'HalfTimber', 'Roof_Clay', None, 'house'),
 'MerchantHouse': (8, 8, 7.5, 'HalfTimber_White', 'Roof_Slate', 'Trade', 'merchant'),
 'CrimsonRowhouse': (5.1, 8, 8, 'Plaster_Rose', 'Roof_Clay', 'Wine', 'rowhouse'),
 'VelvetTerrace': (8.6, 8, 6, 'HalfTimber_White', 'Roof_Slate', 'Wine', 'terrace'),
 'LanternSpire': (5, 6, 9, 'Plaster', 'Roof_Slate', 'Star', 'spire'),
 'VeiledCourtyard': (11, 10, 5, 'Plaster_Rose', 'Roof_Moss', 'Flower', 'courtyard'),
 'Outhouse': (1.7, 1.8, 2.2, 'Oak', 'Thatch', None, 'outhouse')}


def material(name):
    if name in MATS:
        return MATS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    output = nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    dedicated={'Carved_Limestone':(.58,.58,.52,1),'Wicker':(.40,.28,.13,1),'Leather':(.24,.12,.065,1),'Coal':(.045,.043,.04,1),'Glowing_Coals':(.20,.045,.008,1)}
    if name in dedicated:
        bsdf.inputs['Base Color'].default_value=dedicated[name]
        bsdf.inputs['Roughness'].default_value=.72
        MATS[name]=mat
        return mat
    for suffix, socket in [('BaseColor', 'Base Color'), ('Roughness', 'Roughness')]:
        tex = nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(OUT / 'Textures' / f'{name}_{suffix}.png'), check_existing=True)
        tex.interpolation = 'Linear'
        if suffix != 'BaseColor':
            tex.image.colorspace_settings.name = 'Non-Color'
        mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs[socket])
        if suffix=='BaseColor': color_tex=tex
        if suffix=='BaseColor' and name in ('Weathering','Fern_Frond','Leaf_Card','Canopy_Card','Cypress_Card','Flower_Card_Gold','Flower_Card_Pink','Grass_Tuft','Iron_Grate'):
            mat.node_tree.links.new(tex.outputs['Alpha'],bsdf.inputs['Alpha'])
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(str(OUT / 'Textures' / f'{name}_Normal.png'), check_existing=True)
    tex.image.colorspace_settings.name = 'Non-Color'
    normal = nodes.new('ShaderNodeNormalMap')
    normal.inputs['Strength'].default_value = .24
    mat.node_tree.links.new(tex.outputs['Color'], normal.inputs['Color'])
    mat.node_tree.links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
    if name in ('Fern_Frond','Leaf_Card','Canopy_Card','Cypress_Card','Flower_Card_Gold','Flower_Card_Pink','Grass_Tuft'):
        translucent=nodes.new('ShaderNodeBsdfTranslucent')
        mat.node_tree.links.new(color_tex.outputs['Color'],translucent.inputs['Color'])
        mix=nodes.new('ShaderNodeMixShader');mix.inputs[0].default_value=.28
        mat.node_tree.links.new(bsdf.outputs['BSDF'],mix.inputs[1])
        transparent=nodes.new('ShaderNodeBsdfTransparent')
        cutout=nodes.new('ShaderNodeMixShader')
        mat.node_tree.links.new(color_tex.outputs['Alpha'],cutout.inputs[0])
        mat.node_tree.links.new(transparent.outputs[0],cutout.inputs[1])
        mat.node_tree.links.new(translucent.outputs[0],cutout.inputs[2])
        mat.node_tree.links.new(cutout.outputs[0],mix.inputs[2])
        mat.node_tree.links.new(mix.outputs[0],output.inputs['Surface'])
    bsdf.inputs['Metallic'].default_value = .65 if name in ('Iron', 'Brass', 'Copper') else 0
    if name.startswith('Window_'):
        bsdf.inputs['Roughness'].default_value=.3
        bsdf.inputs['Metallic'].default_value=.12
        bsdf.inputs['Transmission Weight'].default_value=.08
        bsdf.inputs['IOR'].default_value=1.46
        mat.node_tree.links.new(color_tex.outputs['Color'],bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value=.18 if name=='Window_Sacred' else .08
    if name.startswith(('Glass_','Liquid_')):
        bsdf.inputs['Transmission Weight'].default_value=.72 if name=='Glass_Clear' else .18
        bsdf.inputs['Coat Weight'].default_value=.25
        bsdf.inputs['IOR'].default_value=1.46
        if name!='Glass_Clear':
            mat.node_tree.links.new(color_tex.outputs['Color'],bsdf.inputs['Emission Color'])
            bsdf.inputs['Emission Strength'].default_value=.08
    MATS[name] = mat
    return mat


def mesh(name, vertices, faces, mat, uvs=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.materials.append(material(mat))
    data.update()
    uv = data.uv_layers.new(name='UVMap')
    scale = 3 if mat.startswith('HalfTimber') else 2.4 if mat in ('Limestone', 'Fieldstone') else 2
    for face in data.polygons:
        normal = face.normal
        axis = max(range(3), key=lambda i: abs(normal[i]))
        axes = (0, 1) if axis == 2 else (0, 2) if axis == 1 else (1, 2)
        for i in face.loop_indices:
            vi = data.loops[i].vertex_index
            co = data.vertices[vi].co
            uv.data[i].uv = uvs[vi] if uvs else (co[axes[0]] / scale, co[axes[1]] / scale)
    obj = bpy.data.objects.new(name, data)
    COLLECTION.objects.link(obj)
    obj['surface_part'] = mat
    return obj


def box(name, p, size, mat='Oak'):
    x,y,z=p;w,d,h=[v*.5 for v in size]
    verts=[(x+a*w,y+b*d,z+c*h) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    obj=mesh(name,verts,[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],mat)
    if min(size)>=.10:obj['edge_bevel']=min(.035,min(size)*.12)
    return obj


def beam(name, a, b, width=.12, mat='DarkTimber'):
    mid=(Vector(a)+Vector(b))*.5
    obj=box(name,(0,0,0),(width,width,(Vector(b)-Vector(a)).length),mat)
    obj.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler()
    obj.location=mid
    if width<.04 and 'edge_bevel' in obj: del obj['edge_bevel']
    return obj


def lathe(name, p, rings, mat='Oak', segments=12, caps=True, smooth=True):
    x,y,z=p
    if segments>=8: segments*=2
    stride=segments+1
    verts=[(x+r*math.cos(i*2*PI/segments),y+r*math.sin(i*2*PI/segments),z+zz) for r,zz in rings for i in range(stride)]
    faces=[]
    for row in range(len(rings)-1):
        for i in range(segments):
            a=row*stride; b=(row+1)*stride
            faces.append((a+i,a+i+1,b+i+1,b+i))
    if caps:
        faces.extend([tuple(reversed(range(segments))),tuple((len(rings)-1)*stride+i for i in range(segments))])
    uv=[]
    circumference=max(1,2*PI*max(r for r,zz in rings)/2)
    for r,zz in rings:
        vv=zz/max(v for _,v in rings) if mat.startswith(('Glass_','Liquid_')) else zz/2
        uv.extend([(i/segments*circumference,vv) for i in range(stride)])
    obj=mesh(name,verts,faces,mat,uv)
    for face in obj.data.polygons:
        if abs(face.normal.z)>.999:
            for loop in face.loop_indices:
                co=obj.data.vertices[obj.data.loops[loop].vertex_index].co
                obj.data.uv_layers.active.data[loop].uv=((co.x-x)/2,(co.y-y)/2)
    if smooth:
        for f in obj.data.polygons:
            f.use_smooth=len(f.vertices)==4 and abs(f.normal.z)<.999
    return obj


def ball(name,p,r,mat='Foliage',scale=(1,1,1),segments=9,rings=5):
    profiles=[(r*max(.018,math.sin(PI*i/rings)),-r*math.cos(PI*i/rings)) for i in range(rings+1)]
    obj=lathe(name,(0,0,0),profiles,mat,segments)
    obj.scale=scale;obj.location=p
    if mat in ('Foliage','Foliage_Gold','Rock'):
        for face in obj.data.polygons:
            axis=max(range(3),key=lambda i:abs(face.normal[i]))
            axes=(0,1) if axis==2 else (0,2) if axis==1 else (1,2)
            for loop in face.loop_indices:
                co=obj.data.vertices[obj.data.loops[loop].vertex_index].co
                obj.data.uv_layers.active.data[loop].uv=(co[axes[0]]/2,co[axes[1]]/2)
    return obj


def roof(name, p, width, depth, rise, mat='Roof_Slate', style='gable',end_material='DarkTimber'):
    x,y,z=p;w=width*.5;d=depth*.5
    if style=='hip':
        verts=[(x-w,y-d,z),(x+w,y-d,z),(x+w,y+d,z),(x-w,y+d,z),(x,y-d*.48,z+rise),(x,y+d*.48,z+rise)]
        faces=[(0,1,4),(1,2,5,4),(2,3,5),(3,0,4,5)]
    else:
        verts=[(x-w,y-d,z),(x+w,y-d,z),(x+w,y+d,z),(x-w,y+d,z),(x,y-d,z+rise),(x,y+d,z+rise)]
        faces=[(0,4,5,3),(1,2,5,4),(0,1,4),(2,3,5)]
    obj=mesh(name,verts,faces,mat)
    if style=='gable':
        obj.data.materials.append(material(end_material))
        for face in list(obj.data.polygons)[2:]: face.material_index=1
    if width>3:
        beam('Eaves fascia',(x-w,y-d,z-.06),(x-w,y+d,z-.06),.16,'DarkTimber')
        beam('Eaves fascia',(x+w,y-d,z-.06),(x+w,y+d,z-.06),.16,'DarkTimber')
        if style=='gable':
            for yy in (y-d,y+d):
                beam('Gable bargeboard',(x-w,yy,z),(x,yy,z+rise),.14,'DarkTimber')
                beam('Gable bargeboard',(x,yy,z+rise),(x+w,yy,z),.14,'DarkTimber')
    return obj


def panel(name,p,size,mat='Window_Amber',side='front',arched=False):
    w,h=size
    coords=[(-w/2,0),(w/2,0)]
    if arched:
        coords.extend([(math.cos(i*PI/8)*w/2,h-w/2+math.sin(i*PI/8)*w/2) for i in range(9)])
    else:
        coords.extend([(w/2,h),(-w/2,h)])
    tangent={'front':(1,0,0),'right':(0,1,0),'back':(-1,0,0),'left':(0,-1,0)}[side]
    verts=[(p[0]+tangent[0]*u,p[1]+tangent[1]*u,p[2]+v) for u,v in coords]
    return mesh(name,verts,[tuple(range(len(verts)))],mat,[(u/w+.5,v/h) for u,v in coords])


def horizontal(name,p,w,d,mat='Produce'):
    x,y,z=p
    return mesh(name,[(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x+w/2,y+d/2,z),(x-w/2,y+d/2,z)],[(0,1,2,3)],mat,[(0,0),(1,0),(1,1),(0,1)])


def sign(motif,p,size=1.15):
    panel('Painted shop sign',p,(size,size),'Sign_'+motif)
    beam('Sign bracket',(p[0],p[1]+.3,p[2]+size+.08),(p[0],p[1]-.06,p[2]+size+.08),.07,'Iron')


def window(p,w=.85,h=1.4,mat='Window_Amber',side='front',arched=False):
    normal=Vector({'front':(0,-1,0),'right':(1,0,0),'back':(0,1,0),'left':(-1,0,0)}[side])
    tangent=Vector({'front':(1,0,0),'right':(0,1,0),'back':(-1,0,0),'left':(0,-1,0)}[side])
    origin=Vector(p);up=Vector((0,0,1))
    panel('Window recess',origin-normal*.035,(w+.04,h+.04),'Iron',side,arched)
    panel('Glass pane',origin,(w,h),mat,side,arched)
    frame='Limestone' if arched else 'DarkTimber'
    jamb_h=h-w/2 if arched else h
    for sign in (-1,1):
        center=origin+tangent*sign*(w/2+.07)+up*jamb_h/2+normal*.075
        size=(.15,.23,jamb_h+.1) if side in ('front','back') else (.23,.15,jamb_h+.1)
        box('Window jamb',center,size,frame)
    if arched:
        verts=[]
        for depth,r in ((.19,w/2+.15),(.19,w/2),(-.015,w/2+.15),(-.015,w/2)):
            for i in range(17):
                a=i*PI/16;co=origin+tangent*(math.cos(a)*r)+up*(jamb_h+math.sin(a)*r)+normal*depth
                verts.append(tuple(co))
        faces=[]
        for i in range(16):
            faces.extend([(i,i+1,18+i,17+i),(i,34+i,35+i,i+1),(17+i,18+i,52+i,51+i)])
        mesh('Arched stone reveal',verts,faces,frame)
    else:
        center=origin+up*(h+.025)+normal*.075
        box('Window lintel',center,(w+.3,.23,.15) if side in ('front','back') else (.23,w+.3,.15),frame)
    center=origin-up*.09+normal*.13
    box('Projecting window sill',center,(w+.42,.44,.18) if side in ('front','back') else (.44,w+.42,.18),'Limestone')
    mullion_h=h-.12
    beam('Casement mullion',origin+up*.04+normal*.045,origin+up*mullion_h+normal*.045,.047,'DarkTimber')
    beam('Casement crossbar',origin-tangent*w*.46+up*h*.52+normal*.045,origin+tangent*w*.46+up*h*.52+normal*.045,.042,'DarkTimber')
    if not arched:
        for sign in (-1,1):
            center=origin+tangent*sign*(w/2+.28)+up*h*.5+normal*.02
            box('Open timber shutter',center,(.29,.065,h-.1) if side in ('front','back') else (.065,.29,h-.1),'Oak')


def hall(p,w,d,h,wall='HalfTimber',cover='Roof_Clay',motif=None,style='gable',windows=True,door=True,right_window_slots=(-.28,.28),center_post=True):
    x,y,z=p
    box('Stone footing',(x,y,z+.22),(w+.24,d+.24,.44),'Fieldstone')
    lower=min(2.6,h)
    box('Ground floor',(x,y,z+.42+(lower-.42)/2),(w,d,lower-.42),'Limestone' if wall.startswith('HalfTimber') else wall)
    if h>lower:
        box('Upper wall panels',(x,y,z+lower+(h-lower)/2),(w+.14,d+.1,h-lower),'Plaster' if wall.startswith('HalfTimber') else wall)
        box('Structural sill',(x,y,z+lower),(w+.29,d+.21,.16),'DarkTimber')
        if wall.startswith('HalfTimber'):
            for xx in (-w/2,0,w/2) if w>=6 and center_post else (-w/2,w/2):
                for yy in (-d/2-.07,d/2+.07): box('Structural timber post',(x+xx,y+yy,z+(lower+h)/2),(.17,.18,h-lower),'DarkTimber')
            if d>=6:
                for xx in (-w/2-.085,w/2+.085):
                    box('Side frame post',(x+xx,y,z+(lower+h)/2),(.17,.17,h-lower),'DarkTimber')
            for yy in (-d/2-.08,d/2+.08):
                box('Upper timber plate',(x,y+yy,z+h-.08),(w+.2,.17,.17),'DarkTimber')
                for direction in (-1,1):
                    beam('Timber corner knee brace',(x+direction*w/2,y+yy,z+lower+.8),
                         (x+direction*(w/2-min(w*.18,.9)),y+yy,z+lower+.06),.13,'DarkTimber')
    rise=min(w*.5,3.5)
    roof('Main roof',(x,y,z+h),w+.75,d+.75,rise,cover,style,wall)
    if door:
        panel('Entrance door',(x,y-d/2-.025,z+.1),(min(1.65,w*.32),2.25 if h>3 else h*.8),'Door_Oak',arched=True)
        box('Doorstep',(x,y-d/2-.26,z+.08),(1.9,.65,.16),'Limestone')
    if windows:
        levels=range(max(1,round(h/2.8)))
        for level in levels:
            zz=z+1.0+level*2.65
            if zz+1.35>z+h-.15: continue
            if w>=6 or level>0 or not door:
                for xx in (-w*.31,w*.31) if w>=6 else (0,): window((x+xx,y-d/2-.10,zz),.9,1.35)
            for slot in right_window_slots if d>=6 else (0,): window((x+w/2+.085,y+d*slot,zz),.85,1.35,side='right')
            for xx in (-w*.27,w*.27) if w>=6 else (0,): window((x+xx,y+d/2+.08,zz),.85,1.35,side='back')
            for yy in (-d*.28,d*.28) if d>=6 else (0,): window((x-w/2-.085,y+yy,zz),.85,1.35,side='left')
    if motif and w>3:
        size=1.9 if w>=7 else 1.1
        marker_z=z+h+.1 if style=='gable' else z+max(2.55,h-size-.25)
        marker_y=y-d/2-.52 if style=='gable' else y-d/2-.25
        sign(motif,(x,marker_y,marker_z),size)
    panel('Weathered wall foot',(x,y-d/2-.13,z+.25),(w,.82),'Weathering')
    panel('Weathered wall foot',(x+w/2+.13,y,z+.25),(d,.82),'Weathering','right')
    panel('Weathered wall foot',(x-w/2-.13,y,z+.25),(d,.82),'Weathering','left')
    panel('Weathered wall foot',(x,y+d/2+.13,z+.25),(w,.82),'Weathering','back')
    if h>3.8 and motif!='Smith':
        box('Chimney',(x+w*.28,y+d*.22,z+h+rise*.6),(.65,.65,2.1),'Brick')
        box('Chimney cap',(x+w*.28,y+d*.22,z+h+rise*.6+1.07),(.82,.82,.13),'Limestone')


def canopy(p,w,d,cloth='Canvas_Stripe',height=2.6):
    x,y,z=p
    for xx in (-w/2,w/2):
        for yy in (-d/2,d/2): box('Canopy post',(x+xx,y+yy,z+height/2),(.1,.1,height),'DarkTimber')
    verts=[];uv=[]
    for j in range(9):
        yy=(j/8-.5)*d*1.2
        for i in range(13):
            xx=(i/12-.5)*w*1.12
            zz=z+height+.34*math.cos(yy/(d*.6)*PI/2)-.08*math.sin(i/12*PI)
            verts.append((x+xx,y+yy,zz));uv.append((i/12,j/8))
    obj=mesh('Draped stitched awning',verts,[(j*13+i,j*13+i+1,(j+1)*13+i+1,(j+1)*13+i) for j in range(8) for i in range(12)],cloth,uv)
    for f in obj.data.polygons:f.use_smooth=True
    vals=[];uv=[]
    for i in range(49):
        xx=(i/48-.5)*w*1.12
        zz=z+height-.08*math.sin(i/48*PI)+.015
        vals.extend([(x+xx,y-d*.6,zz),(x+xx,y-d*.6,zz-.19-.055*math.sin(i/48*PI*8)**2)])
        uv.extend([(i/48,1),(i/48,0)])
    mesh('Scalloped canvas hem',vals,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(48)],cloth,uv)
    for xx in (-w/2,w/2):
        for i in range(16):
            a=(i/16-.5)*d*1.2;b=((i+1)/16-.5)*d*1.2
            def support(yy):return (x+xx,y+yy,z+height+.34*math.cos(yy/(d*.6)*PI/2)-.08*math.sin((xx/(w*1.12)+.5)*PI)-.055)
            beam('Curved awning support',support(a),support(b),.075,'DarkTimber')
    for yy in (-d/2,d/2):beam('Awning cross rail',(x-w/2,y+yy,z+height-.06),(x+w/2,y+yy,z+height-.06),.1)


def porch(p,w,d,cover='Roof_Clay'):
    x,y,z=p
    for xx in (-w/2,w/2): box('Porch post',(x+xx,y-d/2,z+1.35),(.16,.16,2.7),'DarkTimber')
    roof('Porch roof',(x,y,z+2.65),w+.5,d+.4,.7,cover,'hip')


def standard_building(name):
    w,d,h,wall,cover,motif,kind=BUILDING_SPECS[name]
    if kind=='courtyard':
        hall((-4,1,0),3.5,9,5,wall,cover,None)
        hall((4,1,0),3.5,9,5,wall,cover,None)
        hall((0,4,0),5,3.5,4.8,wall,cover,None)
        for yy in (-4.5,-3):facility_buildings.arch(sys.modules[__name__],(0,yy,0),3.2,3,.45,trim=.25)
        box('Covered court gateway tie',(0,-3.75,3.3),(4.1,2.1,.24),'Limestone')
        roof('Covered court gateway roof',(0,-3.75,3.42),4.4,2.4,1.1,cover,'hip')
        for xx in (-2,2):box('Court entrance boundary',(xx,-4,.7),(.6,1.2,1.4),'Limestone')
        fountain((0,.5,0),.65)
        for xx in (-2.5,2.5):lamp((xx,-5.4,0))
        for xx in (-5.5,5.5):planter((xx,-4.4,0),.35)
        COLLECTION['role_identity']='Secluded three-wing residence around a fountain court, supported vaulted gateway and planted entrance'
        return
    annex=kind in ('house','merchant','rowhouse')
    balcony=kind in ('terrace','merchant')
    hall((0,0,0),w,d,h,wall,cover,motif,windows=kind!='outhouse',
         right_window_slots=(-.28,) if annex else (-.28,.28),center_post=not balcony)
    if annex:
        hall((w*.43,d*.28,0),w*.45,d*.66,max(3.4,h-1.5),wall,cover,None,windows=False,door=False)
    if balcony:
        box('Accessible balcony platform',(0,-d/2-.7,3.1),(w*.68,1.5,.2),'Oak')
        for xx in (-w*.34,w*.34):
            box('Balcony foundation pier',(xx,-d/2-1.3,1.5),(.23,.23,3),'DarkTimber')
        for xx in [-w*.3+i*w*.6/6 for i in range(7)]:box('Balcony baluster',(xx,-d/2-1.4,3.65),(.07,.07,.9),'DarkTimber')
        box('Balcony continuous handrail',(0,-d/2-1.4,4.12),(w*.72,.13,.13),'Oak')
        facility_buildings.entrance(sys.modules[__name__],(0,-d/2-.17,3.2),1.2,2.15,'DarkTimber')
        if kind=='terrace':
            for xx in (-w*.34,w*.34):box('Loggia roof post',(xx,-d/2-1.3,4.15),(.17,.17,1.9),'DarkTimber')
            facility_buildings.rotated(sys.modules[__name__],(0,0,0),PI/2,
                lambda:facility_buildings.lean_roof(sys.modules[__name__],'Covered terrace roof',-d/2-1.6,-d/2+.1,-w*.38,w*.38,5.1,5.8,cover,'DarkTimber'))
    if kind=='spire':
        tower((3.7,1.3,0),1.5,11.8,cover)
        for xx in (2.8,4.6):
            lamp((xx,-.2,8.3),post=False)
            beam('Tower lantern wall bracket',(xx,.16,9.3),(xx,-.2,9.3),.06,'Iron')
    if kind=='outhouse':panel('Privy ventilation',(0,-d/2-.04,1.87),(.4,.18),'Iron_Grate')
    COLLECTION['role_identity']='Residential '+kind+'; coherent timber construction, framed windows, fitted doors and functional extensions'


def tower(p,r,h,cover='Roof_Slate',spire=True):
    x,y,z=p
    lathe('Tower masonry',p,[(r+.15,0),(r+.15,.3),(r,.3),(r,h)],'Limestone',12)
    lathe('Tower cornice',(x,y,z+h-.2),[(r,0),(r+.2,.15),(r+.2,.4)],'Limestone',12)
    if spire:
        lathe('Spire',(x,y,z+h+.2),[(r+.35,0),(.045,r*2.4)],cover,12)
    else:
        for i in range(8):
            a=i*PI/4;box('Crenel',(x+math.sin(a)*r,y+math.cos(a)*r,z+h+.5),(.5,.5,.85),'Limestone')
    for zz in (h*.34,h*.7):
        window((x,y-r-.03,z+zz),.55,1.3,'Window_Blue',arched=True)


def wheel(p,r=.65):
    x,y,z=p
    obj=lathe('Wagon wheel',(0,0,0),[(r,0),(r,.13),(r*.84,.13),(r*.84,0),(r,0)],'DarkTimber',12,caps=False)
    obj.rotation_euler.y=PI/2;obj.location=p
    for a in range(0,180,45):
        c=math.cos(math.radians(a))*r;s=math.sin(math.radians(a))*r
        beam('Wheel spoke',(x+.07,y-c,z-s),(x+.07,y+c,z+s),.055,'Oak')


def wagon(covered=False,cart=False):
    w,d=(1.5,2.2) if cart else (2.1,4.0)
    box('Wagon bed',(0,0,.9),(w,d,.22),'Oak')
    box('Wagon left side',(-w/2,0,1.25),(.12,d,.7),'Oak')
    box('Wagon right side',(w/2,0,1.25),(.12,d,.7),'Oak')
    box('Wagon tail',(0,d/2,1.25),(w,.12,.7),'Oak')
    for y in ([.2] if cart else [-1.15,1.15]):
        beam('Axle',(-w/2-.3,y,.65),(w/2+.3,y,.65),.12,'Iron')
        for x in (-w/2-.16,w/2+.16): wheel((x,y,.65),.62)
    for x in (-.52,.52): beam('Draw shaft',(x,-d/2,.8),(x,-d/2-1.8,.7),.11,'Oak')
    if covered:
        verts=[]
        for y in (-d/2,d/2):
            verts.extend([(math.cos(i*PI/8)*w*.56,y,1.65+math.sin(i*PI/8)*1.1) for i in range(9)])
        mesh('Wagon canvas',verts,[(i,i+1,i+10,i+9) for i in range(8)],'Linen')
    else:
        crate((-.45,.55,1.02),.75)
        lathe('Cargo barrel',(.45,-.55,1.02),[(.35,0),(.4,.45),(.35,.9)],'Oak',10)


def crate(p=(0,0,0),size=1,closed=True,contents=None):
    return quality_details.crate(sys.modules[__name__],p,size,closed,contents)


def barrel(p=(0,0,0),r=.45,h=1.2):
    x,y,z=p
    lathe('Barrel staves',p,[(r*.85,0),(r,h*.15),(r*1.08,h*.5),(r,h*.85),(r*.85,h)],'Oak',12)
    for zz in (.13,h*.5,h-.15): lathe('Barrel iron hoop',(x,y,z+zz),[(r*(1.08 if zz==h*.5 else .99),0),(r*(1.08 if zz==h*.5 else .99),.07)],'Iron',12)


def basket(p=(0,0,0),r=.45,contents=None):
    return quality_details.basket(sys.modules[__name__],p,r,contents)


def trough(p=(0,0,0),length=2.2):
    x,y,z=p
    box('Trough bottom',(x,y,z+.2),(length,.8,.2),'Fieldstone')
    for yy in (-.43,.43): box('Trough rim',(x,y+yy,z+.5),(length,.16,.6),'Fieldstone')
    for xx in (-length/2,length/2): box('Trough end',(x+xx,y,z+.5),(.16,.8,.6),'Fieldstone')
    horizontal('Water',(x,y,z+.62),length-.2,.7,'Water')


def planter(p=(0,0,0),r=.6,flowers=True):
    x,y,z=p
    h=r*1.25
    lathe('Terracotta planter',p,[(r*.65,0),(r,h*.92),(r*1.04,h),(r*.88,h),(r*.75,h*.82)],'Terracotta',14)
    lathe('Planter soil',(x,y,z+h*.85),[(r*.83,0),(r*.83,.015)],'Earth',12)
    for i in range(4):
        a=i*PI/2
        plant_card('Growing leaf sprig',(x+math.cos(a)*r*.18,y+math.sin(a)*r*.18,z+h+r*.5),r*1.1,r*1.4,'Leaf_Card',(math.cos(a),math.sin(a),.15))
    if flowers:
        for i in range(5):
            a=i*2*PI/5;flower((x+math.cos(a)*r*.45,y+math.sin(a)*r*.45,z+h*.9),r*.95,i%2==0)


def plant_card(name,p,w,h,mat,normal=(0,-1,.2),rooted=False):
    n=Vector(normal).normalized()
    tangent=n.cross(Vector((0,0,1)))
    if tangent.length<.01:tangent=Vector((1,0,0))
    tangent.normalize();up=tangent.cross(n).normalized();center=Vector(p)
    if rooted:
        u,v=TEXTURE_INFO[mat]['root_uv']
        uv=[(0,v),(1,v),(1,1),(0,1)]
        verts=[tuple(center+tangent*(a-u)*w+up*(b-v)/(1-v)*h) for a,b in uv]
    else:
        uv=[(0,0),(1,0),(1,1),(0,1)]
        verts=[tuple(center+tangent*xx*w/2+up*yy*h/2) for xx,yy in ((-1,-1),(1,-1),(1,1),(-1,1))]
    return mesh(name,verts,[(0,1,2,3)],mat,uv)


def flower(p,scale=1,pink=False):
    x,y,z=p;top=z+.57*scale
    beam('Flower stem',p,(x+.0305*scale,y+.0195*scale,top-.0228*scale),.018*scale,'Foliage')
    plant_card('Flower leaves',(x,y,z+.25*scale),.37*scale,.38*scale,'Leaf_Card')
    plant_card('Textured flower petals',(x+.035*scale,y,top),.4*scale,.4*scale,
               'Flower_Card_Pink' if pink else 'Flower_Card_Gold',(.15,-.65,.76))


def anvil(p=(0,0,0),scale=1):
    x,y,z=p;s=scale
    lathe('Round anvil timber stump',p,[(.5*s,0),(.48*s,.65*s)],'Bark',14)
    box('Anvil foot',(x,y,z+.7*s),(.64*s,.46*s,.12*s),'Iron')
    lathe('Anvil narrow waist',(x,y,z+.75*s),[(.23*s,0),(.15*s,.13*s),(.24*s,.25*s)],'Iron',4,smooth=False)
    box('Anvil flat forging face',(x-.05*s,y,z+1.02*s),(.9*s,.4*s,.14*s),'Iron')
    horn=lathe('Tapered anvil horn',(0,0,0),[(.17*s,0),(.14*s,.23*s),(.015*s,.57*s)],'Iron',14)
    horn.rotation_euler.y=PI/2;horn.location=(x+.39*s,y,z+1.02*s)


def forge(p=(0,0,0),scale=1):
    return quality_details.forge(sys.modules[__name__],p,scale)


def training_dummy(p=(0,0,0)):
    x,y,z=p
    box('Dummy foot',(x,y,z+.09),(.9,.7,.18),'Oak')
    beam('Dummy post',(x,y,z),(x,y,z+2),.13)
    beam('Dummy arms',(x-.8,y,z+1.4),(x+.8,y,z+1.4),.13)
    ball('Straw torso',(x,y,z+1.25),.43,'Thatch',scale=(.8,.6,1.3),segments=8)
    ball('Straw head',(x,y,z+1.95),.25,'Linen',segments=8,rings=4)


def bench(p=(0,0,0),length=2):
    x,y,z=p
    box('Bench seat',(x,y,z+.5),(length,.55,.12),'Oak')
    for xx in (-length*.36,length*.36):
        box('Bench leg',(x+xx,y,z+.25),(.15,.43,.5),'DarkTimber')
        box('Bench back supporting stile',(x+xx,y+.22,z+.76),(.11,.13,.96),'DarkTimber')
    box('Bench back',(x,y+.22,z+.95),(length,.1,.55),'Oak')


def lamp(p=(0,0,0),post=True):
    x,y,z=p
    if post:
        lathe('Lamp post',p,[(.22,0),(.15,.3),(.065,.3),(.065,2.5)],'Iron',8)
        z+=2.5
    box('Lantern body',(x,y,z+.25),(.37,.37,.5),'Window_Amber')
    for xx in (-.2,.2):
        for yy in (-.2,.2): beam('Lantern corner',(x+xx,y+yy,z),(x+xx,y+yy,z+.52),.035,'Iron')
    cap=lathe('Lantern cap',(0,0,0),[(.32,0),(.06,.3)],'Iron',4)
    cap.rotation_euler.z=PI/4;cap.location=(x,y,z+.5)
    base=lathe('Lantern base',(0,0,0),[(.28,0),(.28,.08)],'Iron',4)
    base.rotation_euler.z=PI/4;base.location=(x,y,z-.05)
    if not post:facility_buildings.circular_band(sys.modules[__name__],'Lantern suspension loop',(x,y,z+.9),.1,.025,'Iron',True)


def stall(kind):
    motif,goods,cloth={
        'Produce':('Fruit','Produce','Canvas_Stripe'), 'Cloth':('Trade','Linen','Canvas_Blue'),
        'Fish':('Fish','Fish','Canvas_Blue'), 'Bread':('Bread','Bread','Canvas_Gold'),
        'Spice':('Spice','Produce','Canvas_Red'), 'Flower':('Flower','Foliage','Canvas_Green'),
        'Butcher':('Meat','Linen','Canvas_Red'), 'Potion':('Potion','Window_Green','Canvas_Blue'),
        'Book':('Book','Books','Canvas_Green'), 'Weapon':('Smith','Iron','Canvas_Red'),
    }[kind]
    canopy((0,0,0),3.0,2,cloth,2.6)
    box('Counter top',(0,-.45,1.1),(2.95,1.05,.15),'Oak')
    box('Counter front',(0,-.93,.65),(2.8,.1,.9),'Oak')
    for x in (-1.3,1.3):
        for y in (-.8,-.1):box('Counter leg',(x,y,.53),(.12,.12,1.06),'DarkTimber')
    for x in (-.92,0,.92):
        box('Display tray',(x,-.4,1.21),(.84,.78,.12),'DarkTimber')
    market_goods(kind)
    sign(motif,(-.92,-1.25,1.64),.64)
    if kind=='Flower':
        for x in (-1.85,1.85): planter((x,.2,0),.28)
    elif kind=='Potion':
        box('Apothecary rear bottle shelf',(0,.75,1.75),(3.0,.5,.12),'Oak')
        for x in (-.8,0,.8):facility_buildings.bottle(sys.modules[__name__],(x,.75,1.81),.5)
    elif kind=='Weapon':
        weapon_rack((0,.65,0),True)
    elif kind=='Butcher':
        hanging_rail('Butcher')
        for x in (-.55,.25):
            ball('Hanging cured ham',(x,.7,1.8),.26,'Meat',scale=(1,.8,1.6),segments=12,rings=8)
            beam('Ham bone',(x,.7,2),(x,.7,2.35),.08,'Linen')
            beam('Ham suspension tie',(x,.7,2.32),(x,.75,2.4),.025,'Linen')
    elif kind=='Cloth':
        for x in (-.6,0,.6): lathe('Fabric bolt',(x,.5,0),[(.18,0),(.18,1.4)],cloth,8)
    else: basket((1,.55,0),.36,goods if kind in ('Produce','Bread','Fish') else None)


def hanging_rail(name):
    beam(name+' hanging rail',(-1.5,.75,2.4),(1.5,.75,2.4),.09,'DarkTimber')
    for x in (-1.5,1.5):beam(name+' rail post bracket',(x,.75,2.4),(x,1,2.4),.09,'DarkTimber')


def market_goods(kind):
    if kind=='Produce':
        for tray in range(3):
            x=(tray-1)*.92
            for row in range(3):
                for col in range(3):
                    xx=x+(col-1)*.21;yy=-.4+(row-1)*.22
                    if tray<2:
                        ball('Fresh apple',(xx,yy,1.4),.125,'Apple_Red' if tray==0 else 'Apple_Green',scale=(1,1,.94),segments=8,rings=6)
                        beam('Fruit stalk',(xx,yy,1.5),(xx+.02,yy,1.56),.017,'DarkTimber')
                    else:
                        obj=lathe('Carrot',(0,0,0),[(.015,0),(.07,.32),(.06,.38)],'Carrot',10)
                        obj.rotation_euler.x=PI/2;obj.location=(xx,yy+.12,1.34)
                        for a in (-.4,0,.4):beam('Carrot greens',(xx,yy+.12,1.34),(xx+a*.12,yy+.36,1.48),.02,'Foliage')
        for x in (-.47,.46):ball('Leafy cabbage',(x,.52,1.38),.25,'Cabbage',segments=12,rings=8)
    elif kind=='Fish':
        for i in range(6):
            x=(i%3-1)*.9;y=-.7+(i//3)*.42
            fish((x,y,1.39))
        hanging_rail('Fish')
        for x in (-.7,0,.7):
            beam('Fish tie',(x,.75,2.4),(x,.75,2.16),.022,'Linen')
            fish((x,.75,1.95),hanging=True)
    elif kind=='Bread':
        for i in range(8):
            x=(i%4-1.5)*.57;y=-.62+i//4*.42
            ball('Baked loaf',(x,y,1.41),.22,'Bread_Crust',scale=(1.12,.7,.65),segments=12,rings=8)
        for x in (-.8,0,.8):ball('Long bread',(x,.42,1.4),.19,'Bread_Crust',scale=(.55,2.25,.65),segments=12,rings=8)
    elif kind=='Spice':
        for i,mat in enumerate(('Spice_Red','Spice_Ochre','Spice_Salt')):
            x=(i-1)*.92
            lathe('Spice bowl',(x,-.4,1.27),[(.18,0),(.32,.23),(.28,.26),(.22,.1)],'Terracotta',14)
            lathe('Spice mound',(x,-.4,1.46),[(.25,0),(.2,.08),(.015,.15)],mat,14)
        for x in (-.8,0,.8): basket((x,.62,0),.3)
    elif kind=='Potion':
        for i in range(9):
            x=(i%3-1)*.76;y=-.68+i//3*.28;h=.43+(i%3)*.1
            lathe('Glass medicine bottle',(x,y,1.28),[(.12,0),(.14,.12),(.13,h*.55),(.055,h*.75),(.05,h),(.035,h),(.035,h*.72)],'Glass_Green' if i%2 else 'Glass_Purple',14)
            lathe('Colored potion',(x,y,1.30),[(.105,0),(.115,.1),(.1,h*.55)],'Liquid_Green' if i%2 else 'Liquid_Purple',12)
            lathe('Bottle cork',(x,y,1.28+h-.02),[(.047,0),(.05,.08)],'Cork',10)
    elif kind=='Book':
        for i in range(12):
            x=(i%3-1)*.87;y=-.45+(i//3%2)*.32;z=1.3+(i//6)*.18
            box('Book pages',(x,y,z),(.61,.3,.10),'Paper')
            for zz in (-.06,.06):box('Leather book cover',(x,y,z+zz),(.65,.34,.025),'Canvas_Red' if i%2 else 'Canvas_Green')
    elif kind=='Cloth':
        for i,mat in enumerate(('Canvas_Red','Canvas_Blue','Canvas_Gold')):
            obj=lathe('Cloth bolt',(0,0,0),[(.15,0),(.15,.7)],mat,14);obj.rotation_euler.x=PI/2;obj.location=((i-1)*.9,-.05,1.43)
    elif kind=='Butcher':
        for i in range(5):
            x=(i-2)*.45
            ball('Butcher cut',(x,-.4,1.4),.22,'Meat',scale=(1,.7,.45),segments=12,rings=7)
    elif kind=='Weapon':
        for i in range(3):
            x=(i-1)*.8
            beam('Laid sword blade',(x,-.76,1.31),(x,.0,1.31),.045,'Iron')
            beam('Laid sword guard',(x-.15,-.67,1.34),(x+.15,-.67,1.34),.045,'Brass')
            beam('Laid sword grip',(x,-.91,1.34),(x,-.69,1.34),.06,'DarkTimber')
    elif kind=='Flower':
        for i in range(3):
            x=(i-1)*.91
            lathe('Flower vendor water vase',(x,-.6,1.28),[(.15,0),(.19,.12),(.14,.31),(.16,.35),(.12,.35),(.11,.28)],'Terracotta',12)
            for j in range(5):
                a=j*2*PI/5
                flower((x+math.cos(a)*.12,-.6+math.sin(a)*.12,1.53),.55+j%2*.08,(i+j)%2==0)


def fish(p,hanging=False):
    profiles=[(-.36,.015),(-.27,.10),(-.11,.155),(.09,.145),(.25,.067),(.34,.022)]
    segments=16;verts=[];uv=[]
    for xx,r in profiles:
        for i in range(segments+1):
            a=i*2*PI/segments
            verts.append((xx,math.cos(a)*r*.62,math.sin(a)*r*.85))
            uv.append(((xx+.36)/.7,i/segments))
    faces=[(row*17+i,row*17+i+1,(row+1)*17+i+1,(row+1)*17+i) for row in range(5) for i in range(16)]
    body=mesh('Fish skin and gills',verts,faces,'Fish_Skin',uv)
    for f in body.data.polygons:f.use_smooth=True
    tail=mesh('Forked fish tail',[(.29,0,0),(.5,-.15,0),(.43,0,0),(.5,.15,0)],[(0,1,2),(0,2,3)],'Fish_Skin',[(.9,.5),(1,0),(.95,.5),(1,1)])
    dorsal=mesh('Dorsal fin',[(-.12,0,.12),(.10,0,.21),(.18,0,.09)],[(0,1,2)],'Fish_Skin',[(.4,.5),(.6,.8),(.7,.5)])
    for obj in (body,tail,dorsal):
        obj.location=p
        if hanging:obj.rotation_euler.y=PI/2
    if hanging:tail.rotation_euler.x=PI/2


def weapon_rack(p=(0,0,0),swords=False):
    return quality_details.weapon_rack(sys.modules[__name__],p,swords)


def tree(p=(0,0,0),scale=1,variant=1,cypress=False):
    x,y,z=p
    scale*=.75 if variant==8 else 1.2 if variant==9 else 1
    h=(5.5+variant*.17)*scale
    lean=0 if cypress else ({4:-.85,5:.85,10:.6}.get(variant,(variant%3-1)*.2))*scale
    trunk=lathe('Tapered living tree trunk',(0,0,0),[(.32*scale,0),(.23*scale,h*.32),(.13*scale,h*.66),(.04*scale,h*.94)],'Bark',12)
    for v in trunk.data.vertices:
        v.co.x+=lean*(v.co.z/h)**1.2
        v.co.y+=math.sin(v.co.z/h*PI)*scale*.12
    trunk.location=p
    for i in range(4):
        a=i*PI/2+.3
        branch=lathe('Tree root flare',(0,0,0),[(.13*scale,0),(.035*scale,.65*scale)],'Bark',8)
        start=Vector((x,y,z+.28*scale));end=Vector((x+math.cos(a)*.65*scale,y+math.sin(a)*.65*scale,z+.04))
        branch.rotation_euler=(end-start).to_track_quat('Z','Y').to_euler();branch.location=start
    if cypress:
        lathe('Layered cypress foliage',(x,y,z+h*.18),[(.45*scale,0),(.9*scale,h*.16),(1*scale,h*.35),(.65*scale,h*.65),(.02,h*.95)],'Cypress_Foliage',16)
        for i in range(18):
            a=i*2.4;zz=z+h*(.25+(i%6)*.11);rr=scale*(1-abs(.45-(zz-z)/h))
            plant_card('Cypress leaf edge',(x+math.cos(a)*rr,y+math.sin(a)*rr,zz),.65*scale,.9*scale,'Cypress_Card',(math.cos(a),math.sin(a),.1))
        return
    count=7 if variant==6 else 3 if variant==7 else 5
    for i in range(count):
        a=i*2*PI/count+.25*variant
        r=(1.8 if variant==2 else 1.7 if variant==7 else .85 if variant==3 else 1.1)*scale
        pos=(x+lean+math.cos(a)*r*RAND.uniform(.85,1.15),y+math.sin(a)*r,z+h*.67+(i%2)*scale*.8)
        start=Vector((x+lean*.6,y,z+h*.48));end=Vector(pos)
        branch=lathe('Tapered tree bough',(0,0,0),[(.13*scale,0),(.045*scale,(end-start).length)],'Bark',8)
        branch.rotation_euler=(end-start).to_track_quat('Z','Y').to_euler();branch.location=start
        radius=(1.4+(i%3)*.12)*scale
        canopy=ball('Painted leaf canopy',pos,radius,'Foliage',scale=(1.15 if variant==2 else 1,.95,.67 if variant==2 else .85),segments=12,rings=7)
        for v in canopy.data.vertices:
            co=v.co
            co*=1+.075*math.sin(co.x*3.7+co.z*2.1+i)+.06*math.cos(co.y*4.1-i)
        for j in range(9):
            angle=j*2*PI/9+i*.7
            center=(pos[0]+math.cos(angle)*radius*.85,pos[1]+math.sin(angle)*radius*.8,pos[2]+(j%2-.3)*radius*.45)
            plant_card('Textured canopy leaf edge',center,radius*1.15,radius*1.25,'Canopy_Card',(math.cos(angle),math.sin(angle),.25))
    ball('Upper leaf canopy',(x+lean,y,z+h*.95),1.25*scale,'Foliage',segments=12,rings=7)


def nature(name):
    if name.startswith('NaturalRock_'):
        v=int(name.rsplit('_',1)[1]);r=1.2+v*.035
        obj=ball('Weathered natural boulder',(0,0,r*.68),r,'Rock',scale=(1.2+v%3*.12,.85,.7+v%4*.1),segments=10+v%3,rings=7)
        for pt in obj.data.vertices:
            co=pt.co
            co*=1+.065*math.sin(co.x*3.7+v)+.045*math.cos(co.y*4.2+co.z*2.8)
        if v in (4,8): ball('Companion natural rock',(r*.9,.2,.35),r*.5,'Rock',scale=(1,.8,.8),segments=10,rings=6)
    elif name.startswith('NaturalTree_'):
        tree(variant=int(name.rsplit('_',1)[1]))
    elif name in ('Tree_Street','Tree_Shade','Tree_AlleyCypress'):
        tree(scale=.8 if name=='Tree_Street' else 1.05,variant=3 if name=='Tree_Street' else 4,cypress=name=='Tree_AlleyCypress')
    elif name.startswith('GroundPlant_') or name.startswith('Flower_'):
        if name=='GroundPlant_FernPatch':
            for i in range(7):
                a=i*2*PI/7
                plant_card('Arched painted fern frond',(0,0,0),.65,.9,'Fern_Frond',
                           (math.cos(a),math.sin(a),.45),rooted=True)
            return
        tall=.65 if 'Tall' in name or 'Fern' in name else .26
        count=12 if name=='Flower_Border' else 8
        for i in range(count):
            x=(i%4-.5)*.35;y=(i//4-.5)*.35
            if name.startswith('Flower_'):
                flower((x,y,0),.75+RAND.random()*.25,i%2==0)
            else:
                for j in range(3):
                    a=j*PI/3
                    plant_card('Textured grass cluster',(x,y,0),tall*.9,tall,'Grass_Tuft',(math.cos(a),math.sin(a),0),rooted=True)
    else: raise ValueError(name)


def road(name):
    if name=='Plaza':
        box('Plaza paving',(0,0,.06),(14,14,.12),'Cobble')
        fountain((0,0,.12),1.5)
        for x in (-4.8,4.8):
            bench((x,3,.12),2.5)
            planter((x,-3,.12),.7)
        return
    if name.startswith('Granite_') or name.startswith('Stone_Wall'):
        h=4.2 if name.startswith('Granite') else 1.6
        if 'Gate' in name:
            for x in (-2.1,2.1): box('Gate pier',(x,0,h*.5),(1.1,1.1,h),'Fieldstone')
            box('Gate lintel',(0,0,h-.35),(3.2,.9,.7),'Limestone')
            panel('Portcullis',(0,-.05,.05),(3.1,h-.7),'Iron_Grate',arched=True)
        else:
            if 'Corner' in name:
                body=[(-2,-.325),(1.975,-.325),(1.975,3.6),(1.325,3.6),(1.325,.325),(-2,.325)]
                cap=[(-2.075,-.425),(2.075,-.425),(2.075,3.625),(1.225,3.625),(1.225,.425),(-2.075,.425)]
                for label,outline,bottom,top,mat in [('Joined corner wall',body,0,h,'Fieldstone'),('Joined corner coping',cap,h,h+.16,'Limestone')]:
                    n=len(outline);verts=[(x,y,z) for z in (bottom,top) for x,y in outline]
                    mesh(label,verts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+
                         [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
                if h>3:
                    for y in (1.0,2.1,3.2):box('Return wall merlon',(1.65,y,h+.45),(.7,.55,.75),'Limestone')
            else:
                box('Wall section',(0,0,h/2),(4,.65,h),'Fieldstone')
                box('Wall coping',(0,0,h+.08),(4.15,.85,.16),'Limestone')
            if h>3:
                for x in (-1.6,-.53,.53,1.6): box('Merlon',(x,0,h+.45),(.55,.7,.75),'Limestone')
        return
    box('Road substrate',(0,0,.025),(8,8,.05),'Earth')
    polygons={
        'Paved_Plot':[(-4,-4),(4,-4),(4,4),(-4,4)],
        'Road_Straight':[(-1.9,-4),(1.9,-4),(1.9,4),(-1.9,4)],
        'Road_Corner':[(-1.9,-4),(1.9,-4),(1.9,-1.9),(4,-1.9),(4,1.9),(-1.9,1.9)],
        'Road_T':[(-1.9,-4),(1.9,-4),(1.9,-1.9),(4,-1.9),(4,1.9),(-4,1.9),(-4,-1.9),(-1.9,-1.9)],
        'Road_Cross':[(-1.9,-4),(1.9,-4),(1.9,-1.9),(4,-1.9),(4,1.9),(1.9,1.9),(1.9,4),(-1.9,4),(-1.9,1.9),(-4,1.9),(-4,-1.9),(-1.9,-1.9)],
    }
    if name=='Road_End':
        angle=PI-math.asin(1.9/3)
        outline=[(-1.9,-4),(1.9,-4)]+[(math.sin(angle-i*angle*2/36)*3,math.cos(angle-i*angle*2/36)*3) for i in range(37)]
    else:outline=polygons[name]
    count=len(outline);verts=[(x,y,z) for z in (.05,.11) for x,y in outline]
    mesh('Single joined paving outline',verts,[tuple(reversed(range(count))),tuple(range(count,count*2))]+
         [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)],'Cobble')
    if name!='Paved_Plot':
        for a,b in zip(outline,outline[1:]+outline[:1]):
            if (abs(a[0])==4 and abs(b[0])==4) or (abs(a[1])==4 and abs(b[1])==4):continue
            beam('Low paving curb',(a[0],a[1],.14),(b[0],b[1],.14),.11,'Limestone')


def fountain(p=(0,0,0),scale=1):
    x,y,z=p;s=scale
    lathe('Fountain basin',p,[(1.3*s,0),(1.5*s,.15*s),(1.5*s,.5*s),(1.3*s,.5*s),(1.15*s,.18*s)],'Limestone',16)
    lathe('Fountain water',(x,y,z+.22*s),[(1.2*s,0),(1.2*s,.01)],'Water',16)
    lathe('Fountain pedestal',p,[(.38*s,0),(.28*s,1.2*s),(.6*s,1.4*s),(.15*s,1.5*s),(.15*s,2*s)],'Limestone',12)


def fence(name):
    for x in (-2,2) if name!='Fence_Post' else (0,):
        box('Fence post',(x,0,.8),(.2,.2,1.6),'Oak')
        lathe('Post cap',(x,0,1.6),[(.18,0),(.02,.2)],'DarkTimber',4)
    if name!='Fence_Post':
        for z in (.55,1.25): box('Fence rail',(0,0,z),(4,.12,.14),'Oak')
        if name=='Fence_Gate': beam('Gate brace',(-1.85,-.08,.6),(1.85,-.08,1.2),.11)


def bridge(name):
    stone=name=='Stone_Bridge';count=24 if stone else 12
    profile=[(-4+i*8/count,.4+math.sin(i/count*PI)*(1.9 if stone else .35)) for i in range(count+1)]
    if stone:
        verts=[(x,y,z) for x in (-1.75,1.75) for y,z in profile]
        stride=count+1
        mesh('Continuous cambered bridge paving',verts,[(i,i+1,stride+i+1,stride+i) for i in range(count)],'Cobble')
        for direction in (-1,1):
            xx=direction*1.75
            verts=[(xx,y,z) for y,z in profile]+[(xx,y,.035+math.sin(i/count*PI)*1.65) for i,(y,z) in enumerate(profile)]
            mesh('True stone bridge arch spandrel',verts,[(i,i+1,stride+i+1,stride+i) for i in range(count)],'Limestone')
            outer=xx+direction*.26
            verts=[(x,y,z+dz) for x,dz in ((xx,0),(outer,0),(xx,.9),(outer,.9)) for y,z in profile]
            faces=[]
            for i in range(count):
                faces.extend([(i,i+1,2*stride+i+1,2*stride+i),(stride+i,3*stride+i,3*stride+i+1,stride+i+1),
                              (2*stride+i,2*stride+i+1,3*stride+i+1,3*stride+i)])
            faces.extend([(0,2*stride,3*stride,stride),(count,stride+count,3*stride+count,2*stride+count)])
            mesh('Continuous stone bridge parapet',verts,faces,'Limestone')
        underside=[(x,y,.035+math.sin(i/count*PI)*1.65) for x in (-1.75,1.75) for i,(y,z) in enumerate(profile)]
        mesh('Bridge arch intrados',underside,[(i,stride+i,stride+i+1,i+1) for i in range(count)],'Limestone')
        for yy in (-4,4):mesh('Bridge masonry abutment end',[(-1.75,yy,.035),(1.75,yy,.035),(1.75,yy,.4),(-1.75,yy,.4)],[(0,1,2,3)],'Limestone')
    else:
        for i in range(count):
            y0,z0=profile[i];y1,z1=profile[i+1]
            mesh('Fitted wooden bridge deck', [(-1.7,y0,z0),(1.7,y0,z0),(1.7,y1,z1),(-1.7,y1,z1)],[(0,1,2,3)],'Oak')
        for xx in (-1.35,1.35):
            for i in range(count):
                y0,z0=profile[i];y1,z1=profile[i+1]
                beam('Under-deck load-bearing girder',(xx,y0,z0-.12),(xx,y1,z1-.12),.22,'DarkTimber')
        for i in range(0,count+1,3):
            yy,zz=profile[i]
            for xx in (-1.7,1.7):box('Bridge balustrade post',(xx,yy,zz+.5),(.13,.13,1.15),'Oak')
        for xx in (-1.7,1.7):
            for i in range(count):
                y0,z0=profile[i];y1,z1=profile[i+1]
                beam('Continuous bridge handrail',(xx,y0,z0+1.04),(xx,y1,z1+1.04),.1,'DarkTimber')
        for yy in (-3.8,3.8):box('Bridge abutment sill',(0,yy,.14),(3.7,.45,.28),'Fieldstone')


def props(name):
    if name.endswith('_Stall'): stall(name.split('_')[0])
    elif name.startswith('Fence_'): fence(name)
    elif name in ('Crate_Closed','Crate_Open','Vegetable_Crate','Fish_Crate'):
        crate(size=1.1,closed=name=='Crate_Closed')
        if name=='Vegetable_Crate':
            for i in range(6):
                ball('Crated fresh apple',((i%3-1)*.28,(i//3-.5)*.3,.91),.15,'Apple_Red' if i%2 else 'Apple_Green',segments=10,rings=6)
        elif name=='Fish_Crate':
            for yy in (-.19,.19):fish((-.07,yy,.94))
    elif name in ('Produce_Basket','Bread_Basket','Basket_Empty'):
        basket()
        if name=='Produce_Basket':
            for i in range(5):
                a=i*PI*2/5
                ball('Basket apple',(math.cos(a)*.23,math.sin(a)*.23,.63),.14,'Apple_Green' if i%2 else 'Apple_Red',segments=10,rings=6)
        elif name=='Bread_Basket':
            for xx in (-.15,.15):ball('Basket bread loaf',(xx,0,.65),.22,'Bread_Crust',scale=(.7,1.35,.65),segments=12,rings=7)
    elif name=='Barrel': barrel()
    elif name in ('Grain_Sack','Sack_Stack'):
        for x,y,z in [(0,0,0)] if name=='Grain_Sack' else [(-.4,0,0),(.4,.1,0),(0,.1,.8)]:
            ball('Linen grain sack',(x,y,z+.48),.46,'Linen',scale=(1,.8,1.3),segments=10)
            lathe('Sack tied mouth',(x,y,z+1),[(.15,0),(.08,.15),(.18,.25)],'Linen',8)
    elif name in ('Handcart','Merchant_Wagon','Covered_Wagon'): wagon(name=='Covered_Wagon',name=='Handcart')
    elif name=='Wheelbarrow':
        crate((0,0,.55),1.0,False)
        wheel((0,-.7,.35),.34)
        for x in (-.45,.45):
            beam('Barrow handle',(x,-.6,.8),(x,1.6,.8),.1)
            beam('Barrow leg',(x,.5,0),(x,.5,.65),.09)
    elif name=='Anvil': anvil()
    elif name=='Forge': forge()
    elif name=='Bench': bench()
    elif name=='Streetlamp': lamp()
    elif name=='Hanging_Lantern': lamp(post=False)
    elif name in ('Planter','Garden_Planter'): planter()
    elif name=='Flower_Border': nature(name)
    elif name=='Water_Trough': trough()
    elif name=='Fountain': fountain()
    elif name=='Well':
        lathe('Well shaft',(0,0,0),[(1.05,0),(1.05,1),(.78,1),(.78,.18)],'Fieldstone',12)
        for x in (-1.05,1.05): box('Well upright',(x,0,1.65),(.15,.15,3.3),'Oak')
        roof('Well roof',(0,0,3.1),2.8,2.3,.9,'Roof_Clay')
        beam('Well axle',(-1.1,0,2),(1.1,0,2),.14,'DarkTimber')
        beam('Well rope',(0,0,.9),(0,0,2),.035,'Linen')
        drum=lathe('Well rope winding drum',(0,0,0),[(.16,0),(.16,.55)],'Linen',10)
        drum.rotation_euler.y=PI/2;drum.location=(-.275,0,2)
        beam('Well hand crank',(1.17,0,2),(1.17,0,1.65),.065,'Iron')
        beam('Well crank grip',(1.17,0,1.65),(1.42,0,1.65),.065,'Oak')
    elif name in ('Bucket','Wash_Tub'):
        r=.35 if name=='Bucket' else .8;h=.6 if name=='Bucket' else .55
        lathe('Wooden tub',(0,0,0),[(r*.8,0),(r,h),(r*.87,h),(r*.69,.08)],'Oak',12)
        for zz in (.1,h-.1): lathe('Tub hoop',(0,0,zz),[(r*(.88 if zz==.1 else .99),0),(r*(.88 if zz==.1 else .99),.05)],'Iron',12)
        if name=='Bucket':
            for i in range(16):
                a=i*PI/16;b=(i+1)*PI/16
                beam('Bucket iron carrying handle',(math.cos(a)*r,0,h+math.sin(a)*r),
                     (math.cos(b)*r,0,h+math.sin(b)*r),.025,'Iron')
        else:horizontal('Wash tub water',(0,0,h*.7),r*1.3,r*1.3,'Water')
    elif name=='Hay_Bale':
        box('Hay bale',(0,0,.48),(1.5,1,.96),'Thatch')
        for x in (-.5,.5): box('Bale binding',(x,0,.48),(.04,1.015,.97),'Linen')
    elif name=='Firewood_Rack':
        for x in (-.9,.9):
            for y in (-.35,.35): box('Rack leg',(x,y,.65),(.12,.12,1.3),'DarkTimber')
        for z in (.2,.5,.8):
            for y in (-.2,.2):
                ob=lathe('Split firewood',(0,0,0),[(.15,0),(.15,1.7)],'Bark',6);ob.rotation_euler.y=PI/2;ob.location=(-.85,y,z)
    elif name=='Clothesline':
        for x in (-1.8,1.8): box('Clothesline pole',(x,0,1.3),(.12,.12,2.6),'Oak')
        beam('Washing line',(-1.8,0,2.4),(1.8,0,2.4),.025,'Linen')
        for x in (-1,0,1): panel('Linen washing',(x,-.01,1.1),(.7,1.3),'Linen')
    elif name in ('Noticeboard','Signpost','Shop_Sign'):
        box('Sign post',(0,0,1.4),(.15,.15,2.8),'Oak')
        if name=='Signpost':
            for x,z,w,direction in [(.3,2.3,1.5,1),(-.2,1.85,1.3,-1)]:
                outline=[(-w/2,-.15),(w/2-.23,-.15),(w/2,0),(w/2-.23,.15),(-w/2,.15)]
                verts=[(x+direction*u,y,z+v) for y in (-.09,.06) for u,v in outline]
                faces=[(0,1,2,3,4),(9,8,7,6,5)]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)]
                mesh('Carved arrow direction board',verts,faces if direction>0 else [tuple(reversed(f)) for f in faces],'Oak')
        elif name=='Shop_Sign': sign('Trade',(0,-.12,1.45),1.1)
        else:
            box('Notice board',(0,0,1.6),(1.8,.16,1.4),'DarkTimber')
            for x,z in [(-.4,1.1),(.3,1.3)]: panel('Parchment notices',(x,-.09,z),(.55,.8),'Parchment')
            roof('Noticeboard shelter',(0,0,2.4),2.05,.7,.4,'Roof_Clay')
    elif name=='Drain_Grate':
        for x in (-.55,.55):box('Drain stone side curb',(x,0,.045),(.1,1.2,.09),'Fieldstone')
        for y in (-.55,.55):box('Drain stone end curb',(0,y,.045),(1,.1,.09),'Fieldstone')
        horizontal('Recessed dark drain interior',(0,0,.005),1,1,'Iron')
        horizontal('Iron drainage grille',(0,0,.096),1.0,1.0,'Iron_Grate')
    elif name=='Bollard': lathe('Stone bollard',(0,0,0),[(.2,0),(.2,.75),(.24,.82),(.13,.95)],'Limestone',8)
    elif name=='Pottery_Jars':
        for x,y,r,h in [(-.4,0,.35,.9),(.4,.1,.24,.65),(0,.5,.28,.8)]: lathe('Pottery jar',(x,y,0),[(r*.65,0),(r,.3*h),(r*.7,h*.75),(r*.43,h),(r*.35,h),(r*.35,h*.88)],'Terracotta',12)
    elif name=='Rolled_Fabric':
        for x,mat in [(-.4,'Canvas_Red'),(0,'Canvas_Blue'),(.4,'Canvas_Gold')]: lathe('Rolled fabric',(x,0,0),[(.19,0),(.19,1.45)],mat,10)
    elif name=='Hanging_Herbs':
        beam('Herb drying rail',(-1.2,0,1.8),(1.2,0,1.8),.08)
        for xx in (-1.1,1.1):beam('Herb rail wall bracket',(xx,0,1.8),(xx,.3,1.8),.08,'Iron')
        for x in (-.8,-.25,.3,.8):
            beam('Herb stalk',(x,0,1.75),(x,0,.9),.025,'Linen')
            for a in (0,PI/3,2*PI/3):plant_card('Drying herb leaves',(x,0,1.15),.45,.85,'Leaf_Card',(math.cos(a),math.sin(a),0))
    elif name in ('Shield_Rack','Weapon_Rack'): weapon_rack(swords=name=='Weapon_Rack')
    elif name=='Training_Dummy': training_dummy()
    elif name=='Market_Scale':
        box('Scale foot',(0,0,.1),(.8,.6,.2),'Oak')
        beam('Scale column',(0,0,.15),(0,0,1.5),.08,'Brass')
        beam('Balance arm',(-.65,0,1.3),(.65,0,1.3),.06,'Brass')
        for x in (-.6,.6):
            for y in (-.17,.17): beam('Pan suspension',(x,0,1.3),(x,y,.7),.018,'Iron')
            lathe('Scale pan',(x,0,.65),[(.26,0),(.29,.07)],'Brass',10)
    elif name=='Street_Brazier':
        for i in range(3):
            a=i*2*PI/3;beam('Brazier leg',(math.cos(a)*.4,math.sin(a)*.4,0),(math.cos(a)*.25,math.sin(a)*.25,.7),.07,'Iron')
        lathe('Brazier bowl',(0,0,.6),[(.25,0),(.48,.3),(.5,.5),(.4,.5),(.18,.15)],'Iron',10)
    elif name in ('Stone_Bridge','Wooden_Bridge'): bridge(name)
    elif name=='Dock_Section':
        box('Dock deck',(0,0,.8),(3,5,.2),'Oak')
        for x in (-1.3,1.3):
            for y in (-2,2): lathe('Dock pile',(x,y,0),[(.16,0),(.16,1.3)],'Bark',8)
    elif name.endswith('_Stairs'):
        if name.startswith('Stone'):
            outline=[(0,0),(2.38,0)]
            for i in reversed(range(7)):outline.extend([((i+1)*.34,(i+1)*.18),(i*.34,(i+1)*.18)])
            n=len(outline);verts=[(xx,yy,zz) for xx in (-1,1) for yy,zz in outline]
            mesh('Continuous carved stone staircase',verts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+
                 [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],'Limestone')
        else:
            for i in range(7):box('Wooden stair plank',(0,(i+.5)*.34,(i+1)*.18-.05),(2,.36,.1),'Oak')
            for xx in (-.8,.8):
                beam('Stair supporting stringer',(xx,.06,.04),(xx,2.31,1.18),.17,'DarkTimber')
                box('Stair upper support',(xx,2.25,.6),(.15,.15,1.2),'DarkTimber')
            for xx in (-1,1):
                for i in (0,3,6):box('Stair handrail post',(xx,(i+.5)*.34,(i+1)*.18+.425),(.1,.1,.85),'Oak')
                beam('Sloping stair handrail',(xx,.17,1.03),(xx,2.21,2.11),.09,'DarkTimber')
    else: raise ValueError('No authoring recipe for '+name)


def build(name):
    if facility_buildings.build(sys.modules[__name__], name): pass
    elif service_buildings.build(sys.modules[__name__], name): pass
    elif landmark_structures.build(sys.modules[__name__], name): pass
    elif name in BUILDING_SPECS: standard_building(name)
    elif name.endswith('_Stall'):stall(name.split('_')[0])
    elif name.startswith(('Natural','Tree_','GroundPlant_','Flower_')): nature(name)
    elif name.startswith(('Road_','Granite_','Stone_Wall')) or name in ('Paved_Plot','Plaza'): road(name)
    else: props(name)
    quality_details.finish(sys.modules[__name__],name)


def crest_plate(p,w,h,mat='Canvas_Blue'):
    x,y,z=p
    coords=[(-w/2,h),(-w/2,.36*h),(0,0),(w/2,.36*h),(w/2,h)]
    verts=[(x+a,y+depth,z+b) for depth in (0,.12) for a,b in coords]
    faces=[(0,1,2,3,4),(9,8,7,6,5)]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)]
    mesh('Heraldic backing',verts,faces,mat)


def sword_symbol(p,size=2.5,angle=0):
    x,y,z=p
    tangent=Vector((math.sin(angle),0,math.cos(angle)))
    cross=Vector((math.cos(angle),0,-math.sin(angle)))
    origin=Vector(p)
    beam('Emblem sword blade',origin+tangent*.23*size,origin+tangent*size,.10*size,'Brass')
    beam('Emblem sword hilt',origin-tangent*.04*size,origin+tangent*.23*size,.10*size,'DarkTimber')
    beam('Emblem sword guard',origin+tangent*.23*size-cross*.18*size,origin+tangent*.23*size+cross*.18*size,.075*size,'Brass')


def finish_edges():
    for obj in COLLECTION.objects:
        if obj.type!='MESH' or not ('edge_bevel' in obj or 'grain_axis' in obj):continue
        if 'edge_bevel' in obj:
            bm=bmesh.new();bm.from_mesh(obj.data)
            bmesh.ops.bevel(bm,geom=list(bm.edges),offset=obj['edge_bevel'],segments=1,affect='EDGES',profile=.5)
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
            bm.to_mesh(obj.data);bm.free();obj.data.update()
        uv=obj.data.uv_layers.active
        for face in obj.data.polygons:
            axis=max(range(3),key=lambda i:abs(face.normal[i]))
            axes=(0,1) if axis==2 else (0,2) if axis==1 else (1,2)
            grain=obj.get('grain_axis',-1)
            if grain>=0 and axis!=grain:axes=(next(a for a in range(3) if a not in (axis,grain)),grain)
            for i in face.loop_indices:
                co=obj.data.vertices[obj.data.loops[i].vertex_index].co
                uv.data[i].uv=(co[axes[0]]/2,co[axes[1]]/2)


def bounds(objects):
    pts=[obj.matrix_world@Vector(c) for obj in objects if obj.type=='MESH' for c in obj.bound_box]
    return Vector([min(p[i] for p in pts) for i in range(3)]),Vector([max(p[i] for p in pts) for i in range(3)])


def camera(scene, target, size, view='front'):
    data=bpy.data.cameras.new('Camera_'+view);obj=bpy.data.objects.new('Camera_'+view,data)
    scene.collection.objects.link(obj)
    from render_review import angle_direction,frame_camera
    direction=Vector((0,-1,0)) if view=='front' else angle_direction(scene.name)
    distance=max(size)*2.5+10
    obj.location=target+direction*distance
    obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=max(size.x,size.z)*1.2 if view=='front' else max(size)*1.65
    data.lens=50;data.clip_end=1000
    frame_camera(obj,COLLECTION.objects)
    scene.camera=obj
    return obj


def preview_setup(scene,minimum,maximum):
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.cycles.transparent_max_bounces=16
    scene.cycles.device='CPU'
    scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    scene.view_settings.view_transform='AgX'
    world=bpy.data.worlds.new('Studio world');world.use_nodes=True
    background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    background.inputs[0].default_value=(.49,.56,.62,1)
    background.inputs[1].default_value=.55
    scene.world=world
    size=maximum-minimum;center=(minimum+maximum)/2
    camera(scene,center,size)
    # Separate preview rig is excluded from model topology and library collections.
    mat=bpy.data.materials.get('Studio floor') or bpy.data.materials.new('Studio floor')
    mat.diffuse_color=(.20,.23,.22,1)
    data=bpy.data.meshes.new('Studio floor')
    span=max(size)*10+30
    data.from_pydata([(-span,-span,minimum.z-.001),(span,-span,minimum.z-.001),(span,span,minimum.z-.001),(-span,span,minimum.z-.001)],[],[(0,1,2,3)])
    data.materials.append(mat)
    ground=bpy.data.objects.new('_Preview floor',data);scene.collection.objects.link(ground)
    for name,direction,energy in [('Key',(-.6,-1,1),3.0),('Rim',(1,.5,.8),1.3)]:
        light=bpy.data.lights.new(name,'SUN');light.energy=energy;light.angle=.15
        obj=bpy.data.objects.new('_Preview '+name,light);scene.collection.objects.link(obj)
        obj.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()


def group_for(name):
    if name in BUILDING_SPECS or name in facility_buildings.NAMES or name in service_buildings.RECIPES or name in landmark_structures.RECIPES: return 'Buildings'
    if 'Academy_Lv' in name: return 'Academies'
    if name.endswith('_Stall'): return 'Market'
    if name.startswith(('Natural','Tree_','GroundPlant_','Flower_')): return 'Nature'
    if name.startswith(('Road_','Granite_','Stone_Wall','Fence_')) or name in ('Paved_Plot','Plaza','Stone_Bridge','Wooden_Bridge','Dock_Section','Stone_Stairs','Wooden_Stairs'): return 'Infrastructure'
    return 'Props'


def main():
    global COLLECTION,RAND
    parser=argparse.ArgumentParser();parser.add_argument('--names',nargs='*');parser.add_argument('--sample',action='store_true')
    parser.add_argument('--device',choices=('CPU','METAL'),default='CPU')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    from render_review import configure_device
    configure_device(args.device)
    bpy.context.preferences.filepaths.save_version=0
    source=sorted(p.stem for p in (ROOT/'Assets/Prefabs/Kingdom/City/Prefabs').glob('*.prefab'))
    names=args.names or source+ADDITIONS
    assert len(names)==len(set(names))
    for folder in ('Blender/Assets','Previews/Front','Previews/ThreeQuarter','Reports'):
        (OUT/folder).mkdir(parents=True,exist_ok=True)
    records=[];grid=bpy.data.scenes.new('00_All_Assets')
    for index,name in enumerate(names):
        RAND=random.Random(int(hashlib.sha256(name.encode()).hexdigest()[:8],16))
        scene=bpy.data.scenes.new(name)
        COLLECTION=bpy.data.collections.new(name)
        scene.collection.children.link(COLLECTION)
        build(name)
        finish_edges()
        bpy.context.view_layer.update()
        for obj in COLLECTION.objects:
            if obj.type=='MESH': obj.data.update()
            obj.update_tag()
        # Evaluate object matrices outside the active scene without importing source meshes.
        for obj in COLLECTION.objects:
            obj.matrix_world=obj.matrix_basis
        minimum,maximum=bounds(COLLECTION.objects)
        if abs(minimum.z) > .001:
            for obj in COLLECTION.objects: obj.location.z-=minimum.z
            for obj in COLLECTION.objects: obj.matrix_world=obj.matrix_basis
            minimum,maximum=bounds(COLLECTION.objects)
        COLLECTION.asset_mark();COLLECTION.asset_data.description='Name-driven medieval fantasy kit; independent reusable part textures.'
        preview_setup(scene,minimum,maximum)
        group=group_for(name)
        path=OUT/'Blender/Assets'/group/(name+'.blend');path.parent.mkdir(parents=True,exist_ok=True)
        bpy.data.libraries.write(str(path),{scene},path_remap='RELATIVE_ALL',fake_user=True,compress=True)
        triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in COLLECTION.objects if o.type=='MESH')
        inst=bpy.data.objects.new(name,None);inst.instance_type='COLLECTION';inst.instance_collection=COLLECTION
        inst.location=((index%12)*32,(index//12)*32,0);grid.collection.objects.link(inst)
        record={'name':name,'group':group,'origin':'existing_name' if name in source else 'added','blend':str(path.relative_to(OUT)),'front':f'Previews/Front/{name}.png','three_quarter':f'Previews/ThreeQuarter/{name}.png','triangles':triangles,'mesh_parts':len(COLLECTION.objects),'materials':sorted({m.name for o in COLLECTION.objects if o.type=='MESH' for m in o.data.materials}),'minimum':[round(v,4) for v in minimum],'maximum':[round(v,4) for v in maximum]}
        record['role_identity']=COLLECTION.get('role_identity','')
        if name.startswith('Road_') or group=='Academies' or name in ('Paved_Plot','Plaza','Drain_Grate','Royal_Castle','Arena','Clinic'):
            record['top']=f'Previews/Top/{name}.png'
        records.append(record)
        print('AUTHORED',index+1,len(names),name,triangles,'triangles',flush=True)
        if args.sample:
            bpy.context.window.scene=scene
            scene.cycles.device='CPU' if args.device=='CPU' else 'GPU'
            scene.render.resolution_x=1200;scene.render.resolution_y=1200
            scene.render.filepath=str(OUT/record['front']);bpy.ops.render.render(write_still=True)
            camera(scene,(minimum+maximum)/2,maximum-minimum,'three_quarter')
            scene.render.filepath=str(OUT/record['three_quarter']);bpy.ops.render.render(write_still=True)
    master=OUT/'Blender/MedievalCity_Master.blend'
    if not args.names:
        grid.world=bpy.data.worlds.get('Studio world')
        bpy.context.window.scene=grid
        if bpy.context.screen:
            for area in bpy.context.screen.areas:
                if area.type=='VIEW_3D':
                    area.spaces.active.region_3d.view_location=(176,176,4)
                    area.spaces.active.region_3d.view_distance=460
                    area.spaces.active.clip_end=2500
        for image in bpy.data.images:
            if image.source=='FILE' and image.filepath:
                image.filepath=bpy.path.relpath(bpy.path.abspath(image.filepath),start=str(master.parent))
        bpy.ops.wm.save_as_mainfile(filepath=str(master),compress=True,relative_remap=False)
    manifest={'status':'authored_pending_visual_review','source_name_count':len(source),'added_count':len([x for x in records if x['origin']=='added']),'asset_count':len(records),'units':'meters','up_axis':'+Z','front_axis':'-Y','design_basis':'Asset names only; no original meshes, images or modeling recipes read.','material_policy':'Reusable part PNGs: BaseColor, tangent normal (+Y), roughness. No whole-building projection.','assets':records}
    manifest_name='sample_manifest.json' if args.names else 'asset_manifest.json'
    (OUT/manifest_name).write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print('LIBRARY_SAVED',len(records),str(master),flush=True)


if __name__=='__main__': main()
