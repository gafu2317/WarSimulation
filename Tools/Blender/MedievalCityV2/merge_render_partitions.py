"""Merge only completed partition renders matching their actual saved inputs."""
from pathlib import Path
import hashlib
import json

from assemble_delivery import OUT, ROOT


def main():
    cache={}
    def sha(path):
        if path not in cache:cache[path]=hashlib.sha256(path.read_bytes()).hexdigest()
        return cache[path]
    assets=json.loads((OUT/'asset_manifest.json').read_text())['assets']
    path=OUT/'Reports/render_progress.json';journal=json.loads(path.read_text())
    recipe=sha(Path(__file__).with_name('render_review.py'))
    completed=0
    for job in (0,1):
        source=ROOT/f'Artifacts/MedievalCity_2026-10-22/RenderJobs/{job}/Reports/render_progress.json'
        try:rows=json.loads(source.read_text())
        except (FileNotFoundError,json.JSONDecodeError):continue
        for index,asset in enumerate(assets):
            if index%2!=job:continue
            state=rows.get(asset['name'],{})
            key=hashlib.sha256(''.join(sha(OUT/'Textures'/f'{m}_{s}.png') for m in asset['materials'] for s in ('BaseColor','Normal','Roughness')).encode()).hexdigest()
            if state.get('blend_sha256')!=sha(OUT/asset['blend']) or state.get('texture_sha256')!=key or state.get('recipe_sha256')!=recipe or state.get('resolution')!=1200:continue
            if any(not (OUT/asset[v]).exists() or state.get(v)!=sha(OUT/asset[v]) for v in ('front','three_quarter','top') if v in asset):continue
            journal[asset['name']]=state;completed+=1
    path.write_text(json.dumps(journal,indent=2))
    print('PARTITION_IMAGES_CURRENT',completed,'/',len(assets),flush=True)


if __name__=='__main__':main()
