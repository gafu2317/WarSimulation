"""Review native Unity captures without editing or generating the pictures."""
from pathlib import Path
import json
import html

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/RoleFacilityRebuild20261024'
design=json.loads((ROOT/'Tools/Blender/RoleFacilities/design.json').read_text())
specs={e['name']:e for e in design['assets'] if e['placed']}
priority=['Church','Library','Blacksmith','WarriorAcademy_Lv5','ArcaneAcademy_Lv5','SpiritAcademy_Lv5','Clinic','Observatory','Tavern','Arena','Museum','Casino','Guardhouse','CrimsonRowhouse','VelvetTerrace','LanternSpire','VeiledCourtyard','Plaza']
order=priority+[n for n in specs if n not in priority]
descriptions={
 'Church':'身廊・側廊・後陣・鐘楼・礼拝用の採光窓。',
 'Library':'高い閲覧室と採光屋根、低い書庫棟、蔵書と閲覧机のある回廊。',
 'Blacksmith':'開いた鍛造作業場、屋根から抜ける炉の煙突、金床・研磨輪・工具。',
 'WarriorAcademy_Lv5':'訓練棟と防備のある入口、開いた稽古場、人形・武器架・盾。',
 'ArcaneAcademy_Lv5':'研究塔と講義・実験棟、実技用の中庭、錬金術の作業台。',
 'SpiritAcademy_Lv5':'聖樹と立石を囲む開いた回廊、薬草の学習区画。',
 'Clinic':'低い病棟、病床のある回復用ベランダ、薬棚・薬草園・温室。',
 'Observatory':'実際に開口した観測ドームと望遠鏡、図表の書庫と機器工房。',
 'Tavern':'大きな酒場と宿泊・醸造棟、樽のある飲食用ポーチと卓。',
 'Arena':'開いた競技面、段状の客席、支持アーチと入場トンネル。',
 'Museum':'展示室、彫刻の柱廊と前庭、中央展示室の採光。',
 'Casino':'広い遊技室、開いた回廊、カード・チップのある遊技卓。',
 'Guardhouse':'通路を守る堡塁、見張りの歩廊と詰所、巡回用の武器・盾。',
 'CrimsonRowhouse':'演芸の広間、幕のある舞台、観客席と楽器。',
 'VelvetTerrace':'休憩用の奥棟と二層の開いたテラス、長椅子・灯り。',
 'LanternSpire':'床と屋根を持つ多層の開放亭、実際に吊った小さな灯り。',
 'VeiledCourtyard':'中庭を囲む休憩棟、幕のある入口、庭と卓。',
 'Plaza':'公共の集合場所としての段床、噴水、腰掛けと植栽。',
 'Royal_Castle':'城壁と堡塁に囲まれた内庭、大広間・主塔・開いた城門。',
 'Chapel':'小さな礼拝堂の身廊、後陣と鐘楼。',
 'Bathhouse':'浴室の大広間、採光・排気の屋根と外気浴の中庭。',
 'Barracks':'兵の宿泊棟と稽古場、武器・盾を置く中庭。',
 'Guildhall':'集会室と取引の柱廊、帳簿机と実用の小さな秤。',
 'Bakery':'焼成室と窯・煙突、屋根付きのパン売場。',
 'Warehouse':'高い搬入扉、巻上げ機と荷役床を持つ倉庫。',
 'Stable':'屋根付きの馬房、仕切り・飼葉桶と干草の上階。',
 'Granary':'防鼠用の脚で持ち上げた穀物庫、換気と荷上げ口。',
 'Market_Hall':'支持柱と屋根を持つ公共の売場、商品台と在庫。'}

def figure(phase,name):
    filename='New_'+name+'.png'
    path=OUT/phase/'Captures'/filename
    assert path.is_file(),path
    title=('作り直し前' if phase=='Before' else '作り直し後')
    front=OUT/phase/'Captures'/('New_'+name+'_Front.png')
    link=f'<a class="front" href="{phase}/Captures/{front.name}" target="_blank">正面を近くで見る</a>' if front.exists() else ''
    return f'<figure><a class="zoom" href="{phase}/Captures/{filename}" target="_blank"><img data-file="{filename}" src="{phase}/Captures/{filename}" alt="{title} {html.escape(name)}" loading="lazy" width="1920" height="1080"></a><figcaption>{title} {link}</figcaption></figure>'

cards=[]
for i,name in enumerate(order):
    cards.append(f'<section id="{name}"><h2><span>{i+1:02}</span> {html.escape(specs[name]["label"])}</h2><p>{descriptions.get(name,"")}</p><div class="pair">{figure("Before",name)}{figure("After",name)}</div></section>')
page='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>新モデルの施設 — 作り直し前後</title>
<style>
:root{font-family:system-ui,-apple-system,"Hiragino Kaku Gothic ProN",sans-serif;background:#171b1a;color:#e9e6df;color-scheme:dark}*{box-sizing:border-box}body{margin:0}header,main,nav{max-width:1800px;margin:auto;padding:24px}header{padding-bottom:8px}h1{font-size:clamp(24px,3vw,36px);margin:0 0 16px}header p,section p{color:#c2c2b7;line-height:1.8}nav{position:sticky;top:0;z-index:2;background:#171b1af5;display:flex;gap:12px;align-items:center;padding-top:12px;padding-bottom:12px;border-bottom:1px solid #424a42}button,select{font:inherit;border:1px solid #6b765f;border-radius:5px;background:#29332b;color:#eee;padding:9px 14px;cursor:pointer}button.active{background:#526044;border-color:#adbb94}nav small{margin-left:auto;color:#c2c2b7}section{padding:6px 0 38px;scroll-margin-top:100px}h2{font-size:23px;margin:20px 0 10px}h2 span{color:#c5b47e;font-size:16px;font-family:monospace}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;background:#232b25;border:1px solid #3d493d;border-radius:6px;overflow:hidden}img{width:100%;height:auto;display:block}figcaption{padding:12px 16px;color:#d4d7c8;font-size:14px}.front{float:right}a{color:#c5d5a5}summary{cursor:pointer;margin:10px 0}details{padding-bottom:12px}footer{padding:24px;color:#b5bdaf}@media(max-width:850px){.pair{grid-template-columns:1fr}header,main{padding:18px 12px}nav{padding:10px 12px;flex-wrap:wrap}nav small{display:none}h2{font-size:20px}select{max-width:45vw}}
</style><header><h1>新モデルの施設 — 作り直し前後</h1><p>用途を示す大きな看板を廃し、施設ごとの構造・屋根・窓・専用設備を新規に作成。新モデル全28施設の変更前後を比較。</p><p>すべてUnityから撮影した実際の3Dモデル。同じ配置・視点・照明で撮影している。画像を押すと原寸で開く。</p><details open><summary>街全体</summary><div class="pair">OVERVIEW</div></details></header>
<nav><select id="jump"><option value="">施設へ移動</option>OPTIONS</select><small>左：作り直し前 ／ 右：作り直し後</small></nav><main>CARDS</main>
<footer>モデル：40種類を新規作成（学校は全レベル）。街に置く学校はLv5のみ。建物や街路の位置は維持。<a href="validation.json">施設検証</a> · <a href="preservation.json">既存資産の保全</a></footer>
<script>document.getElementById('jump').onchange=e=>{if(e.target.value)document.getElementById(e.target.value).scrollIntoView({behavior:'smooth'});};</script></html>'''
page=page.replace('OVERVIEW',figure('Before','Overview')+figure('After','Overview'))
page=page.replace('OPTIONS',''.join(f'<option value="{n}">{html.escape(specs[n]["label"])}</option>' for n in order))
page=page.replace('CARDS','\n'.join(cards))
(OUT/'index.html').write_text(page)
(OUT/'gallery.json').write_text(json.dumps({'source':'Unity Camera.Render, 1920x1080, same camera and lighting, no AI images','sets':['CityV2'],'phases':['Before','After'],'facilities':[{'name':n,'label':specs[n]['label'],'identification':descriptions[n]} for n in order]},ensure_ascii=False,indent=2))
print(json.dumps({'gallery':str(OUT/'index.html'),'facilityPairs':len(order)},ensure_ascii=False))
