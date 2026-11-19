import json
from pathlib import Path
import sys

import bpy

ROOT = Path.cwd()
sys.path.insert(0, str(ROOT/'Tools/World'))
from country_redesign_assets import CONCEPT
from render_country_redesign import stage, save, frame, VIEWS
from verify_country_access import verify

OUT = ROOT/'Artifacts/CountryStreetInfill-20261118'
PLAN = json.loads((OUT/'town_plan.json').read_text())
SHOTS = {
    '01_market': ((-53,-25,4),(.33,-1,.91),117),
    '02_smithy': ((-25,-78,4),(.22,-1,1.14),98),
    '03_clinic': ((28,42,4),(.19,-1,1.07),106),
    '04_academy': ((49,121,5),(-.23,-1,.96),125),
}
scene, bridges = stage(PLAN, CONCEPT)
(OUT/'structure_checks.json').write_text(json.dumps(bridges, indent=2)+'\n')
file = OUT/'TownLayout.blend'
save(scene, file)
bpy.ops.wm.open_mainfile(filepath=str(file), load_ui=False)
verify(PLAN)
scene = bpy.context.scene
scene.cycles.samples = 12
scene.render.use_persistent_data = True
for stem,args in SHOTS.items():
    frame(scene.camera,*args)
    scene.render.filepath = str(OUT/f'{stem}.png')
    bpy.ops.render.render(write_still=True)
