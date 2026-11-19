import json
from pathlib import Path
import sys

import bpy

ROOT = Path.cwd()
OUT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT/'Tools/World'))
from country_redesign_assets import CONCEPT
from render_country_redesign import stage, save, frame, collision_mesh
from verify_country_access import verify, PAVING
from build_country_redesign import coords

PLAN = json.loads((OUT/'town_plan.json').read_text())
SHOTS = {
    '01_academy': ((47,136,3),(.26,1,1.45),91),
    '02_ballroom': ((-21,103,4),(.58,-.78,1.65),99),
    '03_riverside': ((59,-15,4),(.13,1,1.35),150),
    '04_clinic': ((25,44,4),(.14,-1,1.16),103),
    '05_south': ((-8,-90,4),(.08,-1,1.26),211),
}
scene, bridges = stage(PLAN, CONCEPT)
file = OUT/'TownLayout.blend'
save(scene,file)
bpy.ops.wm.open_mainfile(filepath=str(file),load_ui=False)
verify(PLAN)

paving = collision_mesh(bpy.data.collections['Town ground and paved streets'])
new_lanes = {r['name']:r for r in PLAN['roads'] if r['name'] in {'Spirit academy rear lane','Ballroom rear lane'}}
lanes = list(new_lanes.values())
north_connection = next(r for r in PLAN['amenityPaths'] if r['name']=='Washing' and coords(r)[0][0]==35.93)
lanes.append(north_connection)
for lane in lanes:
    for a,b in zip(coords(lane),coords(lane)[1:]):
        for step in range(1,20):
            x,z = (start+(end-start)*step/20 for start,end in zip(a,b))
            for edge in (-.43,0,.43):
                xx = x+lane['width']*edge if a[0]==b[0] else x
                zz = z+lane['width']*edge if a[1]==b[1] else z
                floor = paving.ray_cast((xx,zz,1),(0,0,-1),1.1)[0]
                assert floor is not None and abs(floor.z-PAVING)<.003,(lane['name'],xx,zz)
print('Verified paved width on both new lanes and the north footpath.')
scene = bpy.context.scene
scene.cycles.samples = 12
scene.render.use_persistent_data = True
assert (scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage)==(2532,1170,100)
for stem,args in SHOTS.items():
    frame(scene.camera,*args)
    scene.render.filepath = str(OUT/f'{stem}.png')
    bpy.ops.render.render(write_still=True)
