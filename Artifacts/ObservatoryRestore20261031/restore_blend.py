import bpy, sys, json, hashlib
from pathlib import Path
root=Path('/Users/fukutomi/Unity/WarSimulation')
sys.path.insert(0,str(root/'Tools/Blender/RoleFacilities'))
from mesh import Model
from architecture import build,aliases
m=Model('Observatory',aliases('CityV2','Observatory'))
build(m,'Observatory',13,13,'CityV2')
geometry_hash=hashlib.sha256(json.dumps([p[0:2] for p in m.parts.values()]).encode()).hexdigest()
old=json.loads((root/'Artifacts/CityV2Identity20261030/Before/Assets/Models/Kingdom/CityV2/export_manifest.json').read_text())
expected=next(e['geometryHash'] for e in old['assets'] if e['name']=='Observatory')
assert geometry_hash==expected,(geometry_hash,expected)
collection=bpy.data.collections['Observatory']
for obj in list(collection.objects):
 mesh=obj.data if obj.type=='MESH' else None
 bpy.data.objects.remove(obj,do_unlink=True)
 if mesh and mesh.users==0:bpy.data.meshes.remove(mesh)
bpy.data.collections.remove(collection)
backup=root/'Artifacts/CityV2Identity20261030/Before/Art/RoleFacilities/CityV2_Facilities.blend'
with bpy.data.libraries.load(str(backup),link=False) as (source,target):
 target.collections=['Observatory']
for collection in target.collections:
 bpy.context.scene.collection.children.link(collection)
assert len(bpy.context.scene.collection.children)==41,len(bpy.context.scene.collection.children)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Art/RoleFacilities/CityV2_Facilities.blend'))
(root/'Artifacts/ObservatoryRestore20261031/source_validation.json').write_text(json.dumps({'status':'PASS','geometryHash':geometry_hash,'matchesOriginalGeometry':True,'collections':len(bpy.context.scene.collection.children)},indent=2))
print('OBSERVATORY_RESTORED',geometry_hash,flush=True)
