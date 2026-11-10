"""Contact sheets containing only renders tied to the current saved sources."""
from pathlib import Path
import argparse
import hashlib
import json
import math

from PIL import Image, ImageDraw, ImageFont

from assemble_delivery import OUT, ROOT, FONT_PATH, label


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--start',type=int,default=1)
    parser.add_argument('--count',type=int,default=12);parser.add_argument('--top',action='store_true')
    args=parser.parse_args()
    assets=json.loads((OUT/'asset_manifest.json').read_text())['assets']
    journal=json.loads((OUT/'Reports/render_progress.json').read_text())
    cache={}
    def sha(path):
        if path not in cache:cache[path]=hashlib.sha256(path.read_bytes()).hexdigest()
        return cache[path]
    recipe=sha(Path(__file__).with_name('render_review.py'))
    ready=[]
    for asset in assets:
        state=journal.get(asset['name'],{})
        textures=hashlib.sha256(''.join(sha(OUT/'Textures'/f'{m}_{s}.png') for m in asset['materials'] for s in ('BaseColor','Normal','Roughness')).encode()).hexdigest()
        views=['top'] if args.top else ['front','three_quarter']
        if any(v not in asset for v in views):continue
        if state.get('blend_sha256')!=sha(OUT/asset['blend']) or state.get('texture_sha256')!=textures or state.get('recipe_sha256')!=recipe:continue
        if any(not (OUT/asset[v]).exists() or state.get(v)!=sha(OUT/asset[v]) for v in views):continue
        ready.append(asset)
    candidates=[a for a in assets if all(v in a for v in views)]
    finished={a['name'] for a in ready}
    prefix=next((i for i,a in enumerate(candidates) if a['name'] not in finished),len(candidates))
    ready=candidates[:prefix]
    batch=ready[args.start-1:args.start-1+args.count]
    if not batch:print('CURRENT_RENDER_COUNT',len(ready));return
    directory=ROOT/'Artifacts/MedievalCity_2026-10-22/WorkingReview/Fantasy';directory.mkdir(parents=True,exist_ok=True)
    font=ImageFont.truetype(str(FONT_PATH),20)
    for view in views:
        board=Image.new('RGB',(1440,50+math.ceil(len(batch)/3)*525),(235,233,225));draw=ImageDraw.Draw(board)
        draw.text((15,10),f'Current sources / {view} / {args.start}–{args.start+len(batch)-1}',font=font,fill=(40,50,52))
        for i,a in enumerate(batch):
            x=i%3*480;y=50+i//3*525
            im=Image.open(OUT/a[view]).convert('RGB');im.thumbnail((472,470))
            board.paste(im,(x+(480-im.width)//2,y))
            draw.text((x+8,y+471),label(a['name']),font=font,fill=(35,45,48))
            draw.text((x+8,y+497),a['name'],font=font,fill=(65,70,75))
        path=directory/f'{view}_{args.start:03}_{args.start+len(batch)-1:03}.jpg';board.save(path,quality=95)
        print(path)
    print('CURRENT_RENDER_COUNT',len(ready),'BATCH_NAMES',','.join(a['name'] for a in batch))


if __name__=='__main__':main()
