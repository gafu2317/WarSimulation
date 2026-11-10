"""Audit the saved authored library and independently measured geometry."""
from pathlib import Path
import hashlib
import json
import math

import bpy
from mathutils import Vector


ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22'


def main():
    manifest=json.loads((OUT/'asset_manifest.json').read_text())
    expected={p.stem for p in (ROOT/'Assets/Prefabs/Kingdom/City/Prefabs').glob('*.prefab')}
    rebuilt={a['name'] for a in manifest['assets'] if a['origin']=='existing_name'}
    failures=[];records=[]
    if expected!=rebuilt: failures.append('Current source names differ: '+str(expected^rebuilt))
    for a in manifest['assets']:
        name=a['name'];collection=bpy.data.collections.get(name)
        if collection is None: failures.append(name+': no collection');continue
        if name.endswith('_Stall') and not any(o.name.startswith('Draped stitched awning') for o in collection.objects):
            failures.append(name+': stall has no modeled market awning')
        triangles=0;uv_faces=0;zero_area=0;mesh_parts=0;materials=set()
        for obj in collection.objects:
            if obj.type!='MESH': continue
            mesh_parts+=1;data=obj.data
            triangles+=sum(len(f.vertices)-2 for f in data.polygons)
            if not data.uv_layers: failures.append(name+': missing UVs in '+obj.name);continue
            for v in data.vertices:
                if not all(math.isfinite(c) for c in v.co): failures.append(name+': non-finite vertex')
            for face in data.polygons:
                uv=[data.uv_layers.active.data[i].uv for i in face.loop_indices]
                area=abs(sum(uv[i].x*uv[(i+1)%len(uv)].y-uv[(i+1)%len(uv)].x*uv[i].y for i in range(len(uv)))*.5)
                uv_faces+=1
                if area<1e-9: zero_area+=1
            for m in data.materials:
                if not m or not m.use_nodes: failures.append(name+': non-textured material');continue
                materials.add(m.name)
                images=[n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
                if len(images)!=3: failures.append(name+': expected 3 PNG maps for '+m.name)
                for im in images:
                    path=Path(bpy.path.abspath(im.filepath))
                    if not path.exists() or path.suffix.lower()!='.png': failures.append(name+': missing external PNG '+str(path))
                    if not im.filepath.startswith('//'): failures.append(name+': master image path is not portable')
        if triangles!=a['triangles']: failures.append(name+': topology differs from manifest')
        if zero_area: failures.append(name+': '+str(zero_area)+' UV faces have zero area')
        pts=[obj.matrix_world@Vector(c) for obj in collection.objects if obj.type=='MESH' for c in obj.bound_box]
        minimum=[min(v[i] for v in pts) for i in range(3)];maximum=[max(v[i] for v in pts) for i in range(3)]
        if abs(minimum[2])>.001: failures.append(name+': asset is not grounded')
        if any(abs(x-y)>.001 for x,y in zip(minimum+maximum,a['minimum']+a['maximum'])): failures.append(name+': bounds differ')
        if not (OUT/a['blend']).exists(): failures.append(name+': missing individual blend')
        scene=bpy.data.scenes.get(name)
        if not scene or not scene.camera: failures.append(name+': no saved front camera')
        elif abs(scene.camera.rotation_euler.x-math.pi/2)>.001: failures.append(name+': camera is not horizontal')
        records.append({'name':name,'triangles_measured':triangles,'mesh_parts':mesh_parts,'textured_materials':len(materials),'uv_faces':uv_faces,'zero_area_uv_faces':zero_area,'ground_z':minimum[2]})
    report={'status':'PASS' if not failures else 'FAIL','master_sha256':hashlib.sha256((OUT/'Blender/MedievalCity_Master.blend').read_bytes()).hexdigest(),'source_names':len(expected),'rebuilt_source_names':len(rebuilt),'assets_checked':len(records),'triangles_total':sum(x['triangles_measured'] for x in records),'max_asset_triangles':max(x['triangles_measured'] for x in records),'failures':failures,'assets':records,'scope':'Saved master scene data: current-name coverage, measured topology, UV area, finite geometry, external PNG nodes, grounding, individual file existence and front camera. Visual review is recorded separately.'}
    (OUT/'Reports/library_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('LIBRARY_AUDIT',json.dumps({k:v for k,v in report.items() if k!='assets'}),flush=True)
    if failures: raise RuntimeError('Library validation failed')


if __name__=='__main__': main()
