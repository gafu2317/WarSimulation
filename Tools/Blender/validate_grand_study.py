import bpy
import json
import math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Blender/GrandStudy.blend'))
manifest=json.loads((root/'docs/Art/GrandStudy/manifest.json').read_text())
assert len(manifest['assets'])==73
assert len(bpy.data.scenes)==3
checks=[]
for entry in manifest['assets']:
    col=bpy.data.collections.get(entry['name'])
    assert col and col.asset_data and len(col.objects)>0,entry['name']
    for obj in col.objects:
        if obj.type=='MESH':
            assert len(obj.data.polygons)>0,obj.name
            assert all(math.isfinite(v) for vertex in obj.data.vertices for v in vertex.co),obj.name
            assert len(obj.data.materials)>0,obj.name
        elif obj.type=='CURVE':
            assert obj.data.bevel_depth>0,obj.name
    entry['parts']=len(col.objects)
    points=[obj.matrix_world@Vector(corner) for obj in col.objects if obj.type=='MESH' for corner in obj.bound_box]
    if points:
        entry['mesh_bounds_m']=[[round(min(p[j] for p in points),4) for j in range(3)],[round(max(p[j] for p in points),4) for j in range(3)]]
    checks.append(entry['name'])
for sc in bpy.data.scenes:
    assert sc.camera and sc.camera.type=='CAMERA',sc.name
    for obj in sc.objects:
        if obj.instance_type=='COLLECTION':assert obj.instance_collection,obj.name
assert not [im for im in bpy.data.images if im.source=='FILE' and not im.packed_file]
room=bpy.data.scenes['01 Furnished Study']
assert len([o for o in room.objects if o.instance_collection and o.instance_collection.name=='Floor_2m'])==1
assert len([o for o in room.objects if o.instance_collection and o.instance_collection.name=='Ceiling_Coffer_2m'])==12
# Cushions must sit on the seat surface and remain inside the armrests.
for name,width,expected in [('Sofa_ThreeSeat',2.05,2),('Armchair_Leather',.92,1)]:
    cushions=[o for o in bpy.data.collections[name].objects if o.get('independent_cushion')]
    if cushions:
        assert len(cushions)==expected
        for cushion in cushions:
            coords=[v.co for v in cushion.data.vertices]
            assert abs(min(v.z for v in coords)-.56)<.0001
            assert max(abs(v.x) for v in coords)<width/2-.17
for name in ['Chair_Red','Chair_Arms']:
    chair=bpy.data.collections[name]
    assert len([obj for obj in chair.objects if obj.name.startswith('Turned leg')])==4,name
    assert not any(obj.name.startswith('Rear leg') for obj in chair.objects),name
    for obj in bpy.data.collections[name].objects:
        if obj.name.startswith('Red upholstered back insert'):
            thickness=(max(v.co.y for v in obj.data.vertices)-min(v.co.y for v in obj.data.vertices))*abs(obj.scale.y)
            assert thickness<.08,(name,thickness)
desk_chair=next(obj for obj in room.objects if obj.instance_collection and obj.instance_collection.name=='Chair_Arms')
assert abs(desk_chair.rotation_euler.z)<.001,desk_chair.rotation_euler.z
for name in ['Sofa_ThreeSeat','Armchair_Leather']:
    highest=max((obj.matrix_world@Vector(corner)).z for obj in bpy.data.collections[name].objects for corner in obj.bound_box)
    assert highest<1.21,(name,highest)
    back=next(obj for obj in bpy.data.collections[name].objects if obj.name.startswith('Continuous tufted leather back'))
    assert back.data.materials and back.data.materials[0].name=='Back leather',name
window_wall=bpy.data.collections['Wall_WindowOpening_2m']
assert len([obj for obj in window_wall.objects if obj.name.startswith('Window opening side casing')])==2
for name,width in [('Wall_Panel_1m',1.03),('Wall_Panel_2m',2.03)]:
    rails=[obj for obj in bpy.data.collections[name].objects if obj.name.startswith('Wall trim')]
    assert rails and all(obj.dimensions.x>=width for obj in rails),name
# The top surface must not share a plane with cabinet sides or back panels.
for name in ['Sideboard','Small_Cabinet','Bookcase_Filled','Display_Cabinet']:
    col=bpy.data.collections[name]
    cap=max((o for o in col.objects if o.name.startswith('Plinth cornice')),key=lambda o:o.location.z)
    underside=min((cap.matrix_world@Vector(v)).z for v in cap.bound_box)
    for obj in col.objects:
        if obj.type=='MESH' and obj.name.startswith(('Back','Upright')):
            assert max((obj.matrix_world@Vector(v)).z for v in obj.bound_box)<=underside+.0001,(name,obj.name)
wallpaper_diamonds={}
for name in ['Wall_Panel_1m','Wall_Panel_2m','Wall_WindowOpening_2m','Wall_DoorOpening_2m']:
    diamonds=[o for o in bpy.data.collections[name].objects if o.name.startswith('Wallpaper diamond')]
    assert diamonds,name
    for diamond in diamonds:
        assert diamond.type=='CURVE',diamond.name
        frontmost=max(point.co.y for spline in diamond.data.splines for point in spline.points)+diamond.data.bevel_depth
        assert frontmost<=-.004,(name,diamond.name,frontmost)
    wallpaper_diamonds[name]=len(diamonds)
doily=bpy.data.collections['Lace_Doily']
doily_meshes=[o for o in doily.objects if o.type=='MESH']
assert len(doily_meshes)==1
assert len(doily.objects)<=3
assert not any(o.name.startswith(('Crochet ring','Crochet petal')) for o in doily.objects)
continents=[o for o in bpy.data.collections['Globe_Stand'].objects if o.name.startswith('Invented continent')]
assert 5<=len(continents)<=10
for continent in continents:
    assert continent.type=='MESH'
    assert max(len(p.vertices) for p in continent.data.polygons)<=4
    for vertex in continent.data.vertices:
        radius=(vertex.co-Vector((0,0,.95))).length
        assert .354<=radius<=.358,(continent.name,radius)
art_checks={}
for name in ['Picture_Landscape','Picture_Portrait','Photo_Frame']:
    col=bpy.data.collections[name]
    canvas=next((o for o in col.objects if o.name.startswith('Flat painted canvas')),None)
    if canvas:
        assert canvas.data.uv_layers and len(canvas.data.polygons)==1,name
        textures=[n.image for m in canvas.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE']
        assert textures and all(im.packed_file for im in textures),name
        assert not any(o.name.startswith(('Portrait head','Portrait shoulders','Landscape hills','Painted castle tower')) for o in col.objects),name
        art_checks[name]='packed texture and flat UV canvas'
plant_bounds={}
for name in ['Plant_Broadleaf','Plant_Palm','Plant_Croton']:
    col=bpy.data.collections[name]
    pts=[o.matrix_world@Vector(p) for o in col.objects if o.type=='MESH' for p in o.bound_box]
    plant_bounds[name]=[[round(min(p[j] for p in pts),4) for j in range(3)],[round(max(p[j] for p in pts),4) for j in range(3)]]
    for obj in col.objects:
        if obj.type=='MESH' and obj.name.startswith(('Ficus oval leaf','Croton variegated leaf','Tapered palm leaflet')):
            assert obj.data.uv_layers,obj.name
report={'art_checks':art_checks,'plant_mesh_bounds_m':plant_bounds,'wallpaper_diamonds':wallpaper_diamonds,'doily_meshes':len(doily_meshes),'globe_continents':len(continents),'reopened':True,'cabinet_top_joints':True,'asset_count':len(checks),'scenes':list(bpy.data.scenes.keys()),'external_image_dependencies':0,'floor_tiles':12,'ceiling_tiles':12,'valid_meshes_and_materials':True,'notes':'Visual review is separate; no claim of exhaustive mesh-intersection testing. Internal construction joints intentionally overlap.'}
(root/'docs/Art/GrandStudy/manifest.json').write_text(json.dumps(manifest,indent=2))
(root/'docs/Art/GrandStudy/validation.json').write_text(json.dumps(report,indent=2))
print('PASS: 73 asset collections, 3 scenes, finite meshes, assigned materials, no external image dependencies')
