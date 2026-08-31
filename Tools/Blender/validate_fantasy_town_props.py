import bpy
import bmesh
import hashlib
import json
import math
import os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
REVIEW=os.path.join(ROOT,'docs','Art','FantasyTownProps')
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT,'ArtSource','Blender','FantasyTownProps.blend'))
with open(os.path.join(REVIEW,'model_manifest.json')) as f:
    manifest=json.load(f)
expected=set('Road_Straight Road_Corner Road_T Road_Cross Road_End Paved_Plot Fence_Panel Fence_Post Fence_Gate Stone_Wall Stone_Wall_Corner Produce_Stall Cloth_Stall Handcart Crate_Closed Crate_Open Grain_Sack Produce_Basket Barrel Well Water_Trough Firewood_Rack Hay_Bale Clothesline Signpost Noticeboard Anvil Forge Bench Streetlamp Planter Drain_Grate Bollard Bucket'.split())
errors=[]
assert {r['name'] for r in manifest['models']}==expected
records=[]
road_trees={}
for record in manifest['models']:
    name=record['name']
    collection=bpy.data.collections[name]
    root=bpy.data.objects[name]
    points,faces=[],[]
    triangles=0
    meshes=[o for o in collection.objects if o.type=='MESH']
    assert meshes and tuple(root.location)==(0,0,0)
    for obj in meshes:
        if obj.parent!=root or not obj.material_slots or any(not slot.material for slot in obj.material_slots):
            errors.append([name,obj.name,'hierarchy or materials'])
        transform=obj.matrix_parent_inverse@obj.matrix_basis
        offset=len(points)
        points.extend(transform@v.co for v in obj.data.vertices)
        faces.extend(tuple(offset+i for i in face.vertices) for face in obj.data.polygons)
        obj.data.calc_loop_triangles()
        triangles+=len(obj.data.loop_triangles)
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        open_edges=sum(not edge.is_manifold for edge in bm.edges)
        zero_faces=sum(face.calc_area()<=0 for face in bm.faces)
        if open_edges or zero_faces:
            errors.append([name,obj.name,'mesh surface',open_edges,zero_faces])
        bm.free()
    assert all(math.isfinite(v) for p in points for v in p)
    low=[min(p[i] for p in points) for i in range(3)]
    high=[max(p[i] for p in points) for i in range(3)]
    epsilon=max(abs(v) for v in low+high)*2**-23
    datum=-0.18 if record['category']=='Roads' else 0
    if abs(low[2]-datum)>epsilon:
        errors.append([name,'ground datum',low[2],datum])
    records.append({'name':name,'mesh_parts':len(meshes),'triangles':triangles,'minimum':low,'maximum':high})
    if record.get('ports'):
        road_trees[name]=BVHTree.FromPolygons(points,faces)
        assert all(abs(low[i]+4)<=epsilon and abs(high[i]-4)<=epsilon for i in (0,1))
        for port,(x,y) in {'N':(0,4),'S':(0,-4),'E':(4,0),'W':(-4,0)}.items():
            sample_x=x-math.copysign(epsilon,x) if x else 0
            sample_y=y-math.copysign(epsilon,y) if y else 0
            hit=road_trees[name].ray_cast(Vector((sample_x,sample_y,1)),Vector((0,0,-1)),2)[0]
            surface=0 if port in record['ports'] else 0.12
            if hit is None or abs(hit.z-surface)>epsilon:
                errors.append([name,'road edge height',port,list(hit) if hit else None,surface])
town=bpy.data.scenes['00 Town Example']
placements=[o for o in town.objects if 'asset_name' in o]
roads=[o for o in placements if o['asset_name'] in road_trees]
lookup={(round(o.location.x),round(o.location.y)):o for o in roads}
ports={r['name']:r.get('ports','') for r in manifest['models']}
joined=0
for obj in roads:
    turn=round(math.degrees(obj.rotation_euler.z)/90)
    for port in ports[obj['asset_name']]:
        directions='NESW'
        world_port=directions[(directions.index(port)-turn)%4]
        dx,dy={'N':(0,8),'E':(8,0),'S':(0,-8),'W':(-8,0)}[world_port]
        other=lookup.get((round(obj.location.x+dx),round(obj.location.y+dy)))
        assert other is not None,(obj.name,world_port,'connected module')
        other_turn=round(math.degrees(other.rotation_euler.z)/90)
        opposite=directions[(directions.index(world_port)+2+other_turn)%4]
        assert opposite in ports[other['asset_name']],(obj.name,other.name,'matching ports')
        assert obj.location.z==other.location.z
        joined+=1
previewed={o['asset_name'] for s in bpy.data.scenes if s!=town for o in s.objects if 'asset_name' in o}
assert previewed==expected
assert expected <= {o['asset_name'] for o in placements}|previewed
assert not bpy.data.libraries,'embedded existing building sources'
assert not [im for im in bpy.data.images if im.source=='FILE' and not im.packed_file]
frames=[]
for scene in bpy.data.scenes:
    bpy.context.window.scene=scene
    bpy.context.view_layer.update()
    projected=[]
    inverse=scene.camera.matrix_basis.inverted()
    width=scene.camera.data.ortho_scale
    height=width*scene.render.resolution_y/scene.render.resolution_x
    for obj in scene.objects:
        if obj.instance_type!='COLLECTION':
            continue
        transform=obj.matrix_basis@Matrix.Translation(-obj.instance_collection.instance_offset)
        for part in obj.instance_collection.all_objects:
            if part.type!='MESH':
                continue
            basis=part.parent.matrix_basis@part.matrix_parent_inverse@part.matrix_basis if part.parent else part.matrix_basis
            for vertex in part.data.vertices:
                point=inverse@transform@basis@vertex.co
                projected.append(Vector((point.x/width+0.5,point.y/height+0.5,0)))
    frame=[min(v.x for v in projected),max(v.x for v in projected),min(v.y for v in projected),max(v.y for v in projected)]
    if not (0<=frame[0]<frame[1]<=1 and 0<=frame[2]<frame[3]<=1):
        errors.append([scene.name,'preview clipping',frame])
    frames.append({'scene':scene.name,'normalized_frame_bounds':frame})
original_file='/private/tmp/town-props-original-hashes.json'
preserved=[]
if os.path.exists(original_file):
    with open(original_file) as f:
        originals=json.load(f)
    for path,digest in originals.items():
        with open(path,'rb') as f:
            actual=hashlib.file_digest(f,'sha256').hexdigest()
        if actual!=digest:
            errors.append([path,'original asset changed'])
        else:
            preserved.append(path)
report={'status':'PASS' if not errors else 'FAIL','environment':'Blender '+bpy.app.version_string,
    'blend_reopen':'PASS','asset_count':len(records),'models':records,'road_join_count':joined//2,
    'town_instances':len(placements),'all_assets_previewed':True,'external_dependencies':False,
    'unchanged_original_files':preserved,'preview_frames':frames,'errors':errors}
with open(os.path.join(REVIEW,'validation.json'),'w') as f:
    json.dump(report,f,indent=2,ensure_ascii=False)
print(json.dumps(report,ensure_ascii=False,indent=2))
assert not errors,errors
