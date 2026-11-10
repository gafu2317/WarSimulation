"""Package native image generation albedos and shallow technical PBR maps."""
from pathlib import Path
import hashlib
import json
import shutil

import numpy as np
from PIL import Image, ImageFilter


ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22'
GENERATED=Path('/Users/fukutomi/.codex/generated_images/01a15ecc-6be7-7d02-b14c-cc399c79fbd6')


def main():
    sources=json.loads(Path(__file__).with_name('painted_sources.json').read_text())
    records=json.loads((OUT/'texture_manifest.json').read_text())
    source_dir=OUT/'Sources/PaintedMaterials';source_dir.mkdir(parents=True,exist_ok=True)
    provenance=source_dir/'generation_manifest.json'
    packaged={(s['name'],s['filename']) for s in json.loads(provenance.read_text())} if provenance.exists() else set()
    prompts=['# 部材用テクスチャの生成プロンプト',
             '組み込みimage_genで新規生成。現存モデル・建物画像は入力していない。',
             '石・木・屋根・扉・ステンドグラス・用途別装飾帯を別の部材用PNGとして使う。']
    for spec in sources:
        name=spec['name'];source=source_dir/(name+'.png')
        if not source.exists() or (name,spec['filename']) not in packaged:
            shutil.copyfile(GENERATED/spec['filename'],source)
        shutil.copyfile(source,OUT/'Textures'/f'{name}_BaseColor.png')
        image=Image.open(source)
        height=np.full((image.height,image.width),.5) if 'A' in image.getbands() else np.asarray(image.convert('L').filter(ImageFilter.GaussianBlur(3)),dtype=float)/255
        dy,dx=np.gradient(height)
        normal=np.dstack((-dx*4,dy*4,np.ones(height.shape)))
        normal/=np.linalg.norm(normal,axis=2)[:,:,None]
        Image.fromarray(np.clip((normal*.5+.5)*255,0,255).astype('uint8')).save(OUT/'Textures'/f'{name}_Normal.png')
        Image.fromarray(np.clip(spec['roughness']*255+(height-.5)*5,0,255).astype('uint8')).save(OUT/'Textures'/f'{name}_Roughness.png')
        record=next((r for r in records if r['name']==name),None)
        if record is None:
            record={'name':name,'part':spec['part'],'tile_meters':spec['tile_meters'],
                    'basecolor':f'Textures/{name}_BaseColor.png','normal':f'Textures/{name}_Normal.png',
                    'roughness':f'Textures/{name}_Roughness.png'}
            records.append(record)
        record.update(resolution=list(image.size),art_source='image_gen / original hand-painted part material',
                      source_png=f'Sources/PaintedMaterials/{name}.png',
                      source_sha256=hashlib.sha256(source.read_bytes()).hexdigest())
        if name in ('Grass_Tuft','Fern_Frond'):
            alpha=np.asarray(image.getchannel('A'));ys,xs=np.where(alpha>127)
            bottom=ys.max();foot=xs[ys>=bottom-3]
            record['root_uv']=[float(foot.mean()/(image.width-1)),float(1-bottom/(image.height-1))]
        prompts.extend(['## '+name,'原本: `'+spec['filename']+'`',spec['prompt']])
    (OUT/'texture_manifest.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    (source_dir/'generation_manifest.json').write_text(json.dumps(sources,indent=2),encoding='utf-8')
    prompts.append('BaseColorは生成されたPNGをそのまま使用。NormalとRoughnessは浅い補助マップとして導出し、実測の凹凸とは扱わない。')
    (source_dir/'prompts.md').write_text(chr(10).join(prompts),encoding='utf-8')
    print('PAINTED_MATERIALS_PACKAGED',len(sources),flush=True)


if __name__=='__main__':main()
