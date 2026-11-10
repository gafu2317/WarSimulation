"""Reopen every deliverable .blend, rather than trusting the master inventory."""
from pathlib import Path
import hashlib
import json

import bpy


ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22'


def main():
    manifest=json.loads((OUT/'asset_manifest.json').read_text())
    records=[];failures=[]
    for i,a in enumerate(manifest['assets']):
        path=OUT/a['blend'];name=a['name']
        bpy.ops.wm.open_mainfile(filepath=str(path))
        collection=bpy.data.collections.get(name)
        if not collection:
            failures.append(name+': no model collection');continue
        scene=bpy.data.scenes.get(name)
        if len(bpy.data.scenes)!=1: failures.append(name+': unexpected extra scenes')
        if not scene or scene.camera is None: failures.append(name+': saved preview camera missing')
        tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in collection.objects if o.type=='MESH')
        if tris!=a['triangles']: failures.append(name+': triangle count differs')
        images=[]
        for m in bpy.data.materials:
            if not m.use_nodes: continue
            for node in m.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image:
                    p=Path(bpy.path.abspath(node.image.filepath))
                    if not p.exists(): failures.append(name+': PNG path broken '+str(p))
                    if not node.image.filepath.startswith('//'): failures.append(name+': image is not portable relative path')
                    images.append(str(p.relative_to(OUT)))
        record={'name':name,'triangles':tris,'png_references_checked':len(images),'blend_sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
        records.append(record)
        print('INDIVIDUAL_OPENED',i+1,len(manifest['assets']),name,flush=True)
    result={'status':'PASS' if not failures else 'FAIL','master_sha256':hashlib.sha256((OUT/'Blender/MedievalCity_Master.blend').read_bytes()).hexdigest(),'files_opened':len(records),'failures':failures,'assets':records}
    (OUT/'Reports/individual_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print('INDIVIDUAL_AUDIT',result['status'],len(records),failures,flush=True)
    if failures: raise RuntimeError('Individual blend audit failed')


if __name__=='__main__': main()
