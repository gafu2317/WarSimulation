"""Publish matching native Unity views of the revised street layout."""
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from review_city_v2_quality import FONT

ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Artifacts/TownLayout20261031'
SHOTS=[('Overview','国の全景','城を北の奥に据え、市場と橋を軸に街区をつなぐ。'),
       ('Market','市場と商店街','南門と東門から来た通りが、露店の集まる広場に合流する。'),
       ('Residential','西の住宅街','家の玄関を曲がる生活道路へ向け、狭い路地と裏庭をつなぐ。'),
       ('Cathedral','教会と診療所','教会前の広場を中心に、診療所と浴場が近隣住民を支える。'),
       ('Palace','城と北の街区','城の前庭に大通りが至り、脇の通りから兵舎や住宅へ入る。'),
       ('Fountain','噴水広場','大通りが曲がる場所に、複数の街区から歩いて入れる広場を置く。'),
       ('Colleges','学院と天文台','学院・図書館・美術館が集まる北東の街区。天文台は元の姿を維持。'),
       ('Freight','東門と工房街','倉庫・鍛冶場・穀物庫・厩舎を荷運び道と作業庭で結ぶ。'),
       ('River','川と水車小屋','生活道路が橋に集まり、水車は本流へ接続する。'),
       ('Gate','南門から街へ','衛兵所から商店街へ入り、路地の先に暮らしの街区が続く。')]


def pair(left,right,target,labels,caption):
    canvas=Image.new('RGB',(2400,760),'#eeeae0');draw=ImageDraw.Draw(canvas)
    font=ImageFont.truetype(FONT,30);small=ImageFont.truetype(FONT,25)
    for i,path in enumerate((left,right)):
        with Image.open(path) as source:canvas.paste(source.convert('RGB').resize((1200,675),Image.Resampling.LANCZOS),(i*1200,45))
        draw.text((i*1200+16,4),labels[i],font=font,fill='#292d2b')
    draw.text((16,725),caption,font=small,fill='#292d2b');canvas.save(target,quality=95)


def main():
    plan=json.loads((ROOT/'Assets/Data/CountryComparison/town_plan.json').read_text())
    cards=[]
    revision=str((REPORT/'After/Streets/New_Overview.png').stat().st_mtime_ns)
    for name,label,description in SHOTS:
        pair(REPORT/f'After/Streets/Old_{name}.png',REPORT/f'After/Streets/New_{name}.png',REPORT/f'{name}_OldNew.jpg',('旧モデル','新モデル'),label+' ／ 新旧で共通の街路と配置')
        pair(REPORT/f'Before/Streets/New_{name}.png',REPORT/f'After/Streets/New_{name}.png',REPORT/f'{name}_BeforeAfter.jpg',('配置変更前','配置変更後'),label+' ／ 同じカメラから比較')
        cards.append(f'<article id="{name}"><h2>{html.escape(label)}</h2><p>{html.escape(description)}</p><a class="shot" data-name="{name}" href="{name}_OldNew.jpg?v={revision}"><img src="{name}_OldNew.jpg?v={revision}" loading="lazy" alt="{html.escape(label)}の比較"></a><p class="originals"><a href="After/Streets/Old_{name}.png?v={revision}">旧モデルの原寸</a> · <a href="After/Streets/New_{name}.png?v={revision}">新モデルの原寸</a></p></article>')
    document='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>街路と街区の組み直し</title><style>body{margin:0;background:#eeeae0;color:#292d2b;font:16px system-ui}main{max-width:1600px;padding:28px;margin:auto}h1{font-size:30px}h2{font-size:24px}p{line-height:1.7}a{color:#315e71}nav{line-height:2.1}article{margin:38px 0}img{display:block;width:100%;height:auto}.controls{position:sticky;top:0;z-index:1;padding:12px;background:#eeeae0f2;border-block:1px solid #c3bfae}button{font:inherit;padding:10px 18px;margin:3px 10px 3px 0;border:1px solid #657d75;border-radius:6px;color:#274a3c;background:#f8f6f1;cursor:pointer}button[aria-pressed=true]{background:#31584b;color:#fff}.sub,.originals{font-size:14px;color:#61685f}</style><main><h1>街路と街区の組み直し</h1><p>門から市場と橋へ、橋から教会・噴水広場・城へ。通りの役割に合わせて、商店・住宅・工房・公共施設をまとめた。</p>'''
    document+=f'<p class="sub">Unityの実際のCountryシーンを撮影。両国とも129種類、主要建物{len(plan["buildings"])}棟、接続した街路{len(plan["roads"])}本。学校はLv5のみ。撮影日：2026年11月1日。</p>'
    document+='<nav>'+' ／ '.join(f'<a href="#{n}">{l}</a>' for n,l,_ in SHOTS)+'</nav>'
    document+='<div class="controls"><button aria-pressed="true" data-mode="OldNew">旧・新モデルを比較</button><button aria-pressed="false" data-mode="BeforeAfter">配置の変更前後</button></div>'+''.join(cards)
    document+='<p><a href="../">モデル一覧と材質の比較へ</a> ／ <a href="../Identity/">施設の目印の変更記録へ</a></p></main>'
    document+='''<script>document.querySelectorAll('[data-mode]').forEach(button=>button.addEventListener('click',()=>{document.querySelectorAll('[data-mode]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));document.querySelectorAll('.shot').forEach(a=>{const src=a.dataset.name+'_'+button.dataset.mode+'.jpg?v=REV';a.href=src;a.querySelector('img').src=src;});}));</script></html>'''.replace('REV',revision)
    (REPORT/'index.html').write_text(document)
    link=ROOT/'Artifacts/CityV2Quality20261030/Layout'
    if not link.exists():link.symlink_to('../TownLayout20261031',target_is_directory=True)
    print(REPORT/'index.html')


if __name__=='__main__':main()
