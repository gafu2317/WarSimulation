"""Compare native Unity renders of the facility silhouettes at fixed distances."""
import html
import json
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from review_city_v2_quality import comparison, FONT

ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Artifacts/CityV2Identity20261030'
QUALITY=ROOT/'Artifacts/CityV2Quality20261030'


def main():
    specs=[e for e in json.loads((ROOT/'Tools/Blender/RoleFacilities/design.json').read_text())['assets'] if e['placed'] and e.get('identity')]
    cards=[]
    for e in specs:
        name,label=e['name'],e['label']
        current='新モデル・元の姿を維持' if e.get('preserveIdentity') else '新モデル・大きな目印を追加'
        for distance in ('Medium','Near'):
            comparison(REPORT/f'Before/Streets/{name}_{distance}.png',REPORT/f'After/Streets/{name}_{distance}.png',REPORT/f'{name}_{distance}.jpg',
                       ('新モデル・変更前',current),label+' ／ '+e['identity'])
        cards.append(f'<article id="{name}"><h2>{html.escape(label)}</h2><p>{html.escape(e["identity"])}</p>'
                     f'<a class="shot" href="{name}_Medium.jpg" data-name="{name}"><img loading="lazy" src="{name}_Medium.jpg" alt="{html.escape(label)}の変更前後"></a>'
                     f'<p><a href="After/Streets/{name}_Medium.png">中距離の原寸</a> ／ <a href="After/Streets/{name}_Near.png">近距離の原寸</a></p></article>')
    comparison(REPORT/'Before/Streets/New_Overview.png',REPORT/'After/Streets/New_Overview.png',REPORT/'Overview_BeforeAfter.jpg',
               ('新モデル・変更前','新モデル・現在'),'国の全景 ／ 同じカメラで比較')
    comparison(REPORT/'After/Streets/Old_Overview.png',REPORT/'After/Streets/New_Overview.png',REPORT/'Overview_OldNew.jpg',
               ('旧モデル','新モデル・現在'),'新旧2国の現在の比較')
    font=ImageFont.truetype(FONT,23)
    for page,start in enumerate(range(0,len(specs),8),1):
        group=specs[start:start+8]
        canvas=Image.new('RGB',(1600,340*((len(group)+3)//4)),'#eeece5')
        draw=ImageDraw.Draw(canvas)
        for i,e in enumerate(group):
            source=QUALITY/f'After/Models/{e["name"]}.png'
            if not source.exists():continue
            x,y=i%4*400,i//4*340
            with Image.open(source) as image:canvas.paste(image.convert('RGB').resize((400,300),Image.Resampling.LANCZOS),(x,y))
            draw.text((x+10,y+305),e['label'],font=font,fill='#222')
        canvas.save(REPORT/f'Facilities_{page}.jpg',quality=95)
    nav=' ／ '.join(f'<a href="#{e["name"]}">{html.escape(e["label"])}</a>' for e in specs)
    document='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>施設の大きな目印 — 変更前後</title>
<style>body{margin:0;background:#eeeae1;color:#292b29;font:16px system-ui}main{max-width:1560px;padding:30px;margin:auto}h1{font-size:30px}h2{font-size:24px}a{color:#315e71}nav{line-height:2}article{margin:42px 0}img{display:block;width:100%;height:auto}.controls{position:sticky;top:0;padding:12px;background:#eeeae1ed;border-bottom:1px solid #bbb;z-index:1}button{font:inherit;border:1px solid #70857c;border-radius:7px;padding:10px 20px;margin-right:10px;background:#f9f8f2;color:#293d35;cursor:pointer}button[aria-pressed=true]{background:#31584b;color:white}p{line-height:1.6}.sub{color:#5c625a}</style>
<main><h1>施設の大きな目印</h1><p>学校の結晶と大樹、酒場のジョッキなど、施設ごとに大きな形を設けた。天文台は元の姿を維持。左が変更前、右が現在。</p><p class="sub">Unityの実際の街を同じ位置・画角で撮影。中距離では周りの街並みと一緒に、近距離では取付けや設備を確認できる。</p>'''
    document+='<p><a href="../Layout/">現在の街区配置と道の引き方の比較へ</a></p>'
    document+=f'<nav>{nav}</nav><article><h2>国の全景</h2><a href="Overview_BeforeAfter.jpg"><img src="Overview_BeforeAfter.jpg"></a></article>'
    document+='<div class="controls"><button aria-pressed="true" data-distance="Medium">少し引いて見る</button><button aria-pressed="false" data-distance="Near">近くで見る</button></div>'
    document+=''.join(cards)+'<article><h2>旧モデルとの比較</h2><a href="Overview_OldNew.jpg"><img loading="lazy" src="Overview_OldNew.jpg"></a></article><p><a href="../">全141種類と街並みの比較へ戻る</a></p></main>'
    document+='''<script>document.querySelectorAll('[data-distance]').forEach(button=>button.addEventListener('click',()=>{document.querySelectorAll('[data-distance]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));document.querySelectorAll('.shot').forEach(a=>{const src=a.dataset.name+'_'+button.dataset.distance+'.jpg?v=__CAPTURE_REVISION__';a.href=src;a.querySelector('img').src=src;});}));</script></html>'''
    revision=str((REPORT/'After/identity_capture.json').stat().st_mtime_ns)
    document=re.sub(r'((?:src|href)="[^"]+\.(?:png|jpg))"',lambda m:f'{m[1]}?v={revision}"',document)
    document=document.replace('__CAPTURE_REVISION__',revision)
    (REPORT/'index.html').write_text(document)
    link=QUALITY/'Identity'
    if not link.exists():link.symlink_to('../CityV2Identity20261030',target_is_directory=True)
    print('Gallery:',REPORT/'index.html','facilities:',len(specs))


if __name__=='__main__':main()
