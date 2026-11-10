"""Build a browsable deliverable from the saved models and their real renders."""
from pathlib import Path
from collections import Counter
import argparse
import csv
import hashlib
import html
import json
import math

from PIL import Image, ImageDraw, ImageFont


ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'ArtSource/Deliveries/MedievalCity_2026-10-22'
FONT_PATH=ROOT/'Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf'
LABELS={
    'Royal_Castle':'王城','Fantasy_House':'一般住宅','MerchantHouse':'商人邸','WorkshopHouse':'工房付き住宅',
    'Bakery':'パン工房','Tavern':'酒場','Blacksmith':'鍛冶屋','Stable':'厩舎','Warehouse':'倉庫','Granary':'穀物庫',
    'Guildhall':'職人組合会館','Bathhouse':'公衆浴場','Clinic':'診療所','Chapel':'礼拝堂','Church':'教会',
    'Guardhouse':'衛兵所','Barracks':'兵舎','CrimsonRowhouse':'赤灯りの町家','VelvetTerrace':'バルコニーの館',
    'LanternSpire':'塔屋付きの館','VeiledCourtyard':'中庭入口の館','Casino':'遊戯館','Museum':'美術館',
    'Library':'図書館','Observatory':'天文台','Arena':'闘技場','Plaza':'噴水広場',
    'Anvil':'金床','Barrel':'樽','Bench':'ベンチ','Cloth_Stall':'布地の露店','Produce_Stall':'青果の露店',
    'Clothesline':'物干し','Crate_Closed':'蓋付き木箱','Firewood_Rack':'薪置き','Forge':'炉','Handcart':'手押し荷車',
    'Hay_Bale':'干し草束','Noticeboard':'掲示板','Signpost':'道標','Streetlamp':'街灯','Water_Trough':'水飲み桶','Well':'井戸',
    'GroundPlant_GrassShort':'短い草','GroundPlant_GrassTuft':'草の株','GroundPlant_GrassTall':'背の高い草',
    'GroundPlant_FernPatch':'シダの群生','Flower_WildPatch':'野花','Flower_Border':'花の縁取り',
    'Tree_Street':'街路樹','Tree_Shade':'日陰の大樹','Tree_AlleyCypress':'糸杉',
    'Road_Straight':'直線道路','Road_Corner':'曲がり道','Road_T':'T字路','Road_Cross':'十字路','Road_End':'道路の終端',
    'Paved_Plot':'舗装敷地','Granite_Straight':'城壁・直線','Granite_Corner':'城壁・角','Granite_Gate':'城門',
    'Fence_Panel':'木柵','Fence_Post':'柵の支柱','Fence_Gate':'木の門扉','Stone_Wall':'低い石塀','Stone_Wall_Corner':'石塀の角',
    'Crate_Open':'開いた木箱','Grain_Sack':'穀物袋','Produce_Basket':'青果かご','Planter':'花の植木鉢',
    'Drain_Grate':'排水溝の蓋','Bollard':'車止め','Bucket':'木のバケツ','Fish_Stall':'魚の露店','Bread_Stall':'パンの露店',
    'Spice_Stall':'香辛料の露店','Flower_Stall':'花の露店','Butcher_Stall':'肉の露店','Potion_Stall':'薬の露店',
    'Book_Stall':'古書の露店','Weapon_Stall':'武器の露店','Covered_Wagon':'幌馬車','Merchant_Wagon':'商人の荷馬車',
    'Wheelbarrow':'一輪手押し車','Market_Scale':'市場の天秤','Basket_Empty':'空のかご','Pottery_Jars':'陶器の壺',
    'Sack_Stack':'積まれた穀物袋','Vegetable_Crate':'野菜の木箱','Fish_Crate':'魚の木箱','Bread_Basket':'パンのかご',
    'Rolled_Fabric':'巻いた布地','Hanging_Herbs':'干した薬草','Shop_Sign':'吊り看板','Shield_Rack':'盾掛け',
    'Weapon_Rack':'剣掛け','Training_Dummy':'訓練人形','Fountain':'噴水','Wash_Tub':'洗い桶','Street_Brazier':'街路の火鉢',
    'Hanging_Lantern':'吊りランタン','Windmill':'風車小屋','Watermill':'水車小屋','Market_Hall':'市場会館',
    'Watchtower':'見張り塔','Stone_Bridge':'石橋','Wooden_Bridge':'木橋','Dock_Section':'桟橋','Stone_Stairs':'石の階段',
    'Wooden_Stairs':'木の階段','Outhouse':'屋外便所',
}
GROUPS={'Buildings':'建物','Academies':'学校','Infrastructure':'道路・境界・橋','Market':'露店','Nature':'植栽・岩','Props':'生活小物'}


def label(name):
    if 'Academy_Lv' in name:
        role={'Warrior':'戦士学校','Arcane':'魔法学校','Spirit':'精霊使役学校'}[name.split('Academy')[0]]
        return role+' Lv'+name[-1]
    if name.startswith('NaturalTree_'): return '自然樹木 '+name[-2:]
    if name.startswith('NaturalRock_'): return '自然岩 '+name[-2:]
    return LABELS[name]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sheets(assets,view):
    directory=OUT/'Previews/Sheets';directory.mkdir(parents=True,exist_ok=True)
    paths=[];font=ImageFont.truetype(str(FONT_PATH),15);title_font=ImageFont.truetype(str(FONT_PATH),26)
    for group,ja in GROUPS.items():
        members=[a for a in assets if a['group']==group and (OUT/a[view]).exists()]
        for start in range(0,len(members),16):
            batch=members[start:start+16];rows=math.ceil(len(batch)/4)
            board=Image.new('RGB',(1200,90+rows*350),(235,233,222));d=ImageDraw.Draw(board)
            title=f'{ja} / '+('正面' if view=='front' else '斜め')+f'  {start//16+1}'
            d.text((22,20),title,font=title_font,fill=(39,55,49))
            for i,a in enumerate(batch):
                x=(i%4)*300;y=90+(i//4)*350
                im=Image.open(OUT/a[view]).convert('RGB');im.thumbnail((292,292))
                board.paste(im,(x+(300-im.width)//2,y))
                d.text((x+12,y+294),label(a['name']),font=font,fill=(33,45,40))
                d.text((x+12,y+316),a['name']+f" · {a['triangles']} tri",font=font,fill=(70,74,66))
            path=directory/f'{view}_{group}_{start//16+1:02}.jpg';board.save(path,quality=93)
            paths.append(str(path.relative_to(OUT)))
    return paths


def texture_sheets(textures):
    directory=OUT/'Previews/Sheets';paths=[]
    font=ImageFont.truetype(str(FONT_PATH),16)
    for start in range(0,len(textures),16):
        batch=textures[start:start+16];rows=math.ceil(len(batch)/4)
        board=Image.new('RGB',(1040,rows*290+60),(235,233,222));d=ImageDraw.Draw(board)
        d.text((15,15),'共有パーツ用テクスチャ / BaseColor',font=font,fill=(33,45,40))
        for i,t in enumerate(batch):
            x=i%4*260;y=60+i//4*290
            im=Image.open(OUT/t['basecolor']).convert('RGBA');im.thumbnail((250,250))
            board.paste(im,(x+5,y),im)
            d.text((x+8,y+253),t['name'],font=font,fill=(33,45,40))
        path=directory/f'textures_{start//16+1:02}.jpg';board.save(path,quality=93);paths.append(str(path.relative_to(OUT)))
    return paths


def gallery(assets):
    cards=[]
    for a in assets:
        name=a['name'];jp=label(name);top=a.get('top',a['three_quarter'])
        cards.append(f'<article data-name="{html.escape(name+" "+jp)}" data-group="{a["group"]}"><a class="picture" href="{a["front"]}" data-front="{a["front"]}" data-angle="{a["three_quarter"]}" data-top="{top}"><img loading="lazy" src="{a["front"]}" alt="{jp}"></a><div class="body"><strong>{jp}</strong><small>{name}</small><span>{a["triangles"]:,} tri · {len(a["materials"])}材質</span><a href="{a["blend"]}">.blend 原本</a></div></article>')
    options=''.join(f'<option value="{key}">{value}</option>' for key,value in GROUPS.items())
    content='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>中世ファンタジー街素材</title><style>
*{box-sizing:border-box}body{margin:0;background:#eae7dd;color:#233c32;font:16px system-ui,sans-serif}header{padding:34px 5vw 22px;background:#213d32;color:#f1ead5}h1{font-size:30px;margin:0 0 10px}header p{margin:0;line-height:1.7;color:#d7d8c9}.controls{position:sticky;top:0;padding:14px 5vw;background:#eae7dded;display:flex;gap:10px;z-index:2;flex-wrap:wrap;border-bottom:1px solid #c8c5b8}input,select,button{font:inherit;padding:10px 15px;border:1px solid #b8b8a7;border-radius:6px;background:#f8f5ed;color:#233c32}button.active{background:#345344;color:white}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(245px,1fr));gap:20px;padding:25px 5vw}article{background:#f6f2e7;border:1px solid #cfcebd;border-radius:10px;overflow:hidden}article[hidden]{display:none}.picture{display:block;background:#c9cccb}.picture img{display:block;width:100%;aspect-ratio:1}.body{padding:15px}.body strong,.body small,.body span{display:block;margin-bottom:5px}.body small{overflow-wrap:anywhere;color:#6e746a}.body span{font-size:13px}.body a{display:inline-block;margin-top:7px;color:#42634d}footer{padding:24px 5vw;font-size:14px}#count{align-self:center;margin-left:auto}
</style><header><h1>中世ファンタジーの街素材</h1><p>建物・学校・市場・道路・植栽・生活道具。部位ごとの共有PNGを組み合わせた新作。<br>正面画像は水平の正投影。平たい道路には斜め・上面も併用できる。</p></header><div class="controls"><input id="search" placeholder="名前で検索" aria-label="名前で検索"><select id="group"><option value="">すべての分類</option>OPTIONS</select><button class="active" data-view="front">正面</button><button data-view="angle">斜め</button><button data-view="top">上面・補助</button><span id="count"></span></div><main class="grid">CARDS</main><footer><a href="README.md">納品内容・使用方法</a> · <a href="asset_manifest.json">全モデル一覧</a> · <a href="texture_manifest.json">PNG一覧</a></footer><script>
const articles=[...document.querySelectorAll('article')],search=document.querySelector('#search'),group=document.querySelector('#group');function filter(){let count=0;articles.forEach(a=>{a.hidden=!a.dataset.name.toLowerCase().includes(search.value.toLowerCase())||(group.value&&a.dataset.group!==group.value);if(!a.hidden)count++});document.querySelector('#count').textContent=count+' 点'}search.oninput=filter;group.onchange=filter;document.querySelectorAll('[data-view]').forEach(button=>button.onclick=()=>{document.querySelectorAll('[data-view]').forEach(b=>b.classList.toggle('active',b===button));document.querySelectorAll('.picture').forEach(p=>{p.href=p.dataset[button.dataset.view];p.querySelector('img').src=p.href})});filter();
</script></html>'''
    (OUT/'index.html').write_text(content.replace('OPTIONS',options).replace('CARDS',''.join(cards)),encoding='utf-8')


def readme(manifest,textures):
    assets=manifest['assets'];groups=Counter(a['group'] for a in assets)
    text=f'''# 中世ヨーロッパ・ファンタジー街素材

既存の街素材{manifest['source_name_count']}点を名前から新規制作し、露店や生活小物など{manifest['added_count']}点を追加した、合計**{len(assets)}点**のセット。
旧モデルの形状・画像・生成レシピは制作の入力に使用していない。

## 開き方

- 一覧を見る: `index.html`をブラウザで開く。分類・名前で絞り込み、正面・斜めを切り替えられる。
- 全素材を編集する: `Blender/MedievalCity_Master.blend`。`00_All_Assets`シーンに一覧配置し、素材名のシーンに個別モデルと正面カメラを置いている。
- 1点を編集する: `Blender/Assets/<分類>/<モデル名>.blend`。各ファイルはそのモデルと確認用カメラ・照明を含む。
- 正面画像: `Previews/Front/`に全{len(assets)}点。斜めは`Previews/ThreeQuarter/`、道路等の補助上面は`Previews/Top/`。
- まとめ画像: `Previews/Sheets/`。モデル名と三角形数を添えている。

## 形状・テクスチャの方針

適度にデフォルメした中世ファンタジーの造形。窓の枠・奥行き・鎧戸・窓台、屋根の縁、塔、庇、車輪など形を伝える部分を立体で作った。
石・木・屋根・扉は、暖色の縁と寒色の影、広い筆の陰影を持つ、剣と魔法のRPG向けの手描き調。教会には聖紋とステンドグラス、魔法学校には星と軌道、精霊学校には蔓と花の専用部材を組み合わせている。
市場の商品は野菜・魚・パン・布・瓶などの外形を作り分け、何を売る店か判別できるようにした。
木組みの細かな傷、石目、瓦の継ぎ目、扉の金具、汚れ、商品の質感は部品ごとのPNGで表現する。
窓・扉・看板・布・壁材・屋根材は別々の部品用素材であり、建物全体の絵を投影する方式ではない。
`surface_part`プロパティと材質名で使用パーツを確認できる。原本のメッシュは部位ごとに編集できる。

- 三角形数: 全モデル合計{sum(a['triangles'] for a in assets):,}、最大{max(a['triangles'] for a in assets):,}。照明・確認用の床・一覧用インスタンスを除く。
- 表面セット: {len(textures)}種類。各部品材質にBaseColor・Normal・Roughnessの3PNG、合計{len(textures)*3}PNG。
- BaseColorはsRGB。NormalとRoughnessはNon-Color。Normalはタンジェント空間、緑チャンネルは+Y。
- 汚れ、葉・花・草のカード、鉄格子はBaseColorのアルファも使う。
- 組み込みimage_genで制作した{sum('art_source' in t for t in textures)}部材の原画像と生成プロンプトは`Sources/PaintedMaterials/`。BaseColorは生成PNGをそのまま採用し、浅いNormalとRoughnessは補助マップとして導出した。
- 壁・屋根・木材等は繰り返しUV、扉・窓・看板は部品単位のUVを使う。各部品PNGの基準寸法は`texture_manifest.json`にある。
- 単位はm、上方向は+Z、正面は-Y、接地はZ=0。
- 床タイルの正面は薄く見えるため、斜め・上面画像も用意した。

## 含まれる素材

| 分類 | 点数 |
| --- | ---: |
'''
    text+='\n'.join(f'| {GROUPS[group]} | {groups[group]} |' for group in GROUPS)
    text+='''

学校は戦士・魔法・精霊使役の3系統×Lv1〜5。主要な棟、塔、翼棟が段階ごとに増える。
市場は青果・布・魚・パン・香辛料・花・肉・薬・古書・武器の10露店。
追加の荷馬車、天秤、壺、袋、薪、道具、橋、桟橋、風車・水車小屋などで街の生活を組み立てられる。

## ファイルと検証

`asset_manifest.json`は全モデルの名前、原本パス、画像パス、三角形数、寸法、使用材質を記録する。
`Reports/library_validation.json`は保存済みマスターの実形状・UV・PNG参照・接地の検証。
`Reports/individual_validation.json`は個別.blendを開き直した検証。
`Reports/render_progress.json`は描画した画像と対応原本のSHA-256。
`Reports/delivery_validation.json`は納品ファイルと画像の照合。
`Reports/visual_review.json`は画像を目視した範囲と修正内容。

PNGは`.blend`からの相対参照。フォルダ構成を保って移動する。
`_Preview`の床・カメラ・照明は確認用であり、ゲーム用モデルのコレクションに含めていない。
原本はBlender 5.0で制作。外観用素材であり、室内、ゲーム処理、Collider、NavMeshは含まない。
Unityの既存FBX・Prefabの置換はこの納品物には含まれない。

再生成レシピはプロジェクトの`Tools/Blender/MedievalCityV2/`にある。テクスチャ、モデル、画像の順に生成する。
'''
    (OUT/'README.md').write_text(text,encoding='utf-8')


def verify(manifest,textures):
    failures=[];journal=json.loads((OUT/'Reports/render_progress.json').read_text()) if (OUT/'Reports/render_progress.json').exists() else {}
    lookup={a['name']:a for a in manifest['assets']};names=set(lookup)
    if len(names)!=len(manifest['assets']): failures.append('Duplicate model names')
    expected_blends={a['blend'] for a in manifest['assets']}
    actual_blends={str(p.relative_to(OUT)) for p in (OUT/'Blender/Assets').rglob('*.blend')}
    if expected_blends!=actual_blends: failures.append('Individual blend inventory differs from manifest')
    recipe=sha(Path(__file__).with_name('render_review.py'))
    for a in manifest['assets']:
        name=a['name'];file=OUT/a['blend']
        if not file.exists(): failures.append(name+': blend missing');continue
        state=journal.get(name,{})
        if state.get('recipe_sha256')!=recipe: failures.append(name+': render recipe is outdated')
        if state.get('blend_sha256')!=sha(file): failures.append(name+': render is not tied to current blend')
        texture_key=hashlib.sha256(''.join(sha(OUT/'Textures'/f'{m}_{suffix}.png') for m in a['materials'] for suffix in ('BaseColor','Normal','Roughness')).encode()).hexdigest()
        if state.get('texture_sha256')!=texture_key: failures.append(name+': render is not tied to current PNG textures')
        for view in ('front','three_quarter','top'):
            if view not in a: continue
            p=OUT/a[view]
            if not p.exists(): failures.append(name+': '+view+' missing');continue
            with Image.open(p) as im:
                im.verify()
            with Image.open(p) as im:
                if min(im.size)<1000: failures.append(name+': image resolution below 1000')
                if max(v[1]-v[0] for v in im.convert('RGB').getextrema())<8: failures.append(name+': render appears blank')
            if state.get(view)!=sha(p): failures.append(name+': PNG differs from render journal')
    for t in textures:
        for field in ('basecolor','normal','roughness'):
            p=OUT/t[field]
            if not p.exists(): failures.append(t['name']+': '+field+' missing')
            else:
                with Image.open(p) as im: im.verify()
        if 'source_png' in t:
            source=OUT/t['source_png']
            if not source.exists() or sha(source)!=t['source_sha256'] or sha(OUT/t['basecolor'])!=t['source_sha256']:
                failures.append(t['name']+': generated albedo provenance differs')
    expected_maps={t[field] for t in textures for field in ('basecolor','normal','roughness')}
    actual_maps={str(p.relative_to(OUT)) for p in (OUT/'Textures').glob('*.png')}
    if expected_maps!=actual_maps: failures.append('Part PNG inventory differs from manifest')
    master=OUT/'Blender/MedievalCity_Master.blend'
    for report in ('library_validation','individual_validation','visual_review'):
        p=OUT/'Reports'/(report+'.json')
        if not p.exists(): failures.append(report+': report missing');continue
        d=json.loads(p.read_text())
        if d.get('status')!='PASS': failures.append(report+': not PASS')
        if d.get('master_sha256')!=sha(master): failures.append(report+': report does not match current master')
        if report=='library_validation' and d.get('assets_checked')!=len(names): failures.append(report+': model coverage differs')
        if report=='individual_validation' and d.get('files_opened')!=len(names): failures.append(report+': file coverage differs')
        if report=='visual_review':
            if {a['name'] for a in d.get('assets',[])}!=names: failures.append(report+': visual coverage differs')
            for a in d.get('assets',[]):
                source=lookup.get(a['name'])
                if source is None: continue
                for view in ('front','three_quarter','top'):
                    if view in source and a.get(view)!=sha(OUT/source[view]):
                        failures.append(a['name']+': visual review image differs')
    result={'status':'PASS' if not failures else 'INCOMPLETE','assets':len(manifest['assets']),'source_names':manifest['source_name_count'],'added':manifest['added_count'],'front_pngs':len(list((OUT/'Previews/Front').glob('*.png'))),'part_texture_pngs':len(textures)*3,'failures':failures}
    (OUT/'Reports/delivery_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--draft',action='store_true');args=parser.parse_args()
    manifest=json.loads((OUT/'asset_manifest.json').read_text());textures=json.loads((OUT/'texture_manifest.json').read_text())
    for a in manifest['assets']: a['display_name']=label(a['name'])
    front=sheets(manifest['assets'],'front');angle=sheets(manifest['assets'],'three_quarter');tex=texture_sheets(textures)
    (OUT/'Reports/contact_sheets.json').write_text(json.dumps({'front':front,'three_quarter':angle,'textures':tex},indent=2),encoding='utf-8')
    gallery(manifest['assets']);readme(manifest,textures)
    with (OUT/'model_inventory.csv').open('w',newline='',encoding='utf-8-sig') as f:
        writer=csv.writer(f);writer.writerow(['name','display_name','group','origin','triangles','blend','front'])
        writer.writerows([[a[k] for k in ['name','display_name','group','origin','triangles','blend','front']] for a in manifest['assets']])
    if not args.draft:
        result=verify(manifest,textures)
        manifest['status']='verified' if result['status']=='PASS' else 'incomplete'
        print('DELIVERY_AUDIT',json.dumps(result),flush=True)
    (OUT/'asset_manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
    print('DELIVERY_INDEX_READY',len(manifest['assets']),flush=True)


if __name__=='__main__': main()
