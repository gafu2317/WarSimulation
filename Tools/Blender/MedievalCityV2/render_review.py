"""Render the actual Blender scenes; resume only unchanged completed images."""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import time

import bpy
from mathutils import Vector


ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def configure_device(device):
    if device=='CPU':return
    prefs=bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type=device
    prefs.get_devices()
    if not any(d.type==device for d in prefs.devices):raise RuntimeError('Requested render device is unavailable')
    for d in prefs.devices:d.use=d.type==device


def angle_direction(name):
    compound='Academy_Lv' in name or name in ('Royal_Castle','Arena','Plaza','Clinic','Market_Hall')
    return Vector((1,-1.5,1.55 if compound else 1.05)).normalized()


def frame_camera(cam, objects):
    rotation=cam.rotation_euler.to_matrix()
    inverse=rotation.transposed()
    points=[inverse@(obj.matrix_world@Vector(c)-cam.location)
            for obj in objects if obj.type=='MESH' for c in obj.bound_box]
    lo=Vector((min(p.x for p in points),min(p.y for p in points),0))
    hi=Vector((max(p.x for p in points),max(p.y for p in points),0))
    cam.location+=rotation@((lo+hi)*.5)
    cam.data.ortho_scale=max(hi.x-lo.x,hi.y-lo.y)*1.14


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--names',nargs='*')
    parser.add_argument('--resolution',type=int,default=1200)
    parser.add_argument('--device',choices=('CPU','METAL'),default='CPU')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    configure_device(args.device)
    recipe_key=digest(Path(__file__))
    manifest=json.loads((OUT/'asset_manifest.json').read_text())
    journal_path=OUT/'Reports/render_progress.json'
    journal=json.loads(journal_path.read_text()) if journal_path.exists() else {}
    start=time.time()
    for index,asset in enumerate(manifest['assets']):
        name=asset['name']
        if args.names and name not in args.names: continue
        key=digest(OUT/asset['blend'])
        texture_key=hashlib.sha256(''.join(digest(OUT/'Textures'/f'{m}_{suffix}.png') for m in asset['materials'] for suffix in ('BaseColor','Normal','Roughness')).encode()).hexdigest()
        previous=journal.get(name,{})
        scene=bpy.data.scenes[name]
        scene.cycles.device='CPU' if args.device=='CPU' else 'GPU'
        bpy.context.window.scene=scene
        scene.render.resolution_x=args.resolution;scene.render.resolution_y=args.resolution
        minimum=Vector(asset['minimum']);maximum=Vector(asset['maximum'])
        size=maximum-minimum;target=(minimum+maximum)/2
        cam=scene.camera
        views=[('front',Vector((0,-1,0)),'front'),('three_quarter',angle_direction(name),'three_quarter')]
        if 'top' in asset: views.append(('top',Vector((0,0,1)),'top'))
        for view,direction,field in views:
            path=OUT/asset[field]
            path.parent.mkdir(parents=True,exist_ok=True)
            if previous.get('blend_sha256')==key and previous.get('texture_sha256')==texture_key and previous.get('recipe_sha256')==recipe_key and previous.get('resolution')==args.resolution and path.exists() and previous.get(view)==digest(path):
                continue
            cam.location=target+direction*(max(size)*2.5+10)
            cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
            frame_camera(cam,bpy.data.collections[name].all_objects)
            # Flat infrastructure needs a shallow top view as an additional image; front remains exact.
            scene.render.filepath=str(path)
            bpy.ops.render.render(write_still=True)
            previous[view]=digest(path)
        previous['blend_sha256']=key
        previous['texture_sha256']=texture_key
        previous['recipe_sha256']=recipe_key
        previous['resolution']=args.resolution
        journal[name]=previous
        journal_path.write_text(json.dumps(journal,indent=2),encoding='utf-8')
        print('REVIEW_RENDERED',index+1,len(manifest['assets']),name,round(time.time()-start,1),'seconds',flush=True)
    print('RENDER_COMPLETE',len(journal),'assets',flush=True)


if __name__=='__main__': main()
