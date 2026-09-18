from pathlib import Path
import json
import re

project=Path(__file__).resolve().parents[2]
rows=[]
for line in (project/'docs/スキル演出.md').read_text().splitlines():
 if not line.startswith('|') or '`' not in line:continue
 parts=[s.strip() for s in line.strip('|').split('|')]
 if len(parts)<4 or not parts[2].startswith('`'):continue
 skill=parts[2].strip('`')
 if skill=='SkillId':continue
 rows.append(dict(id=skill,name=parts[1],english=parts[0],concept=parts[3],family=skill.split('_')[0]))
design={}
for line in (project/'docs/Vfx/演出刷新設計.md').read_text().splitlines():
 if line.startswith('|'):
  parts=[s.strip() for s in line.strip('|').split('|')]
  if len(parts)==2:design[parts[0]]=parts[1]
for row in rows:row['change']=design.get(row['id'],'')
html='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>WarSimulation — Skill VFX review</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#101923;color:#eef3f4;font:15px/1.7 system-ui,sans-serif}header,main,footer{max-width:1100px;margin:auto;padding:32px 24px}header{padding-top:56px}h1{font-size:36px;line-height:1.3;margin:12px 0}h2{margin:0;font-size:21px}.eyebrow{color:#7ddcc0;font-size:12px;letter-spacing:.2em}p{max-width:820px;color:#b8c8d2}nav{display:flex;flex-wrap:wrap;gap:10px;position:sticky;top:0;background:#101923ed;backdrop-filter:blur(10px);padding:16px 24px;z-index:2;border-block:1px solid #293744}button,select{border:1px solid #465561;background:#1d2b38;color:white;padding:10px 14px;border-radius:6px;font:inherit;cursor:pointer}button.active{background:#d9f6e8;color:#132b24}.grid{display:grid;gap:24px}article{background:#182532;border:1px solid #324451;border-radius:12px;overflow:hidden}.copy{padding:24px}.id{font:12px/1.4 monospace;color:#8da7b9}video{display:block;width:100%;max-width:960px;margin:auto;background:#080d13}.mobile video{max-width:480px}.concept{color:#f0dfad}details{margin-top:10px}summary{cursor:pointer;color:#b8d9e6}a{color:#82dec4}.meta{font-size:13px;color:#9caeb9}.count{margin-left:auto;align-self:center;color:#9caeb9}
</style>
<header><div class="eyebrow">WAR SIMULATION / VISUAL EFFECTS</div><h1>32スキルの演出レビュー</h1><p>Unityで実際に再生して記録した映像です。実戦は既存の戦闘カメラとキャラクターを使用し、検証用にAIの自動判断を停止して全スキルを順番に発動しています。EffectTestは形と時間変化を見やすい検証視点です。</p><p class="meta">Unity 6000.4.3f1 · URP 17.4 · Apple M3 Pro / Metal · 960×540収録・約10fps<br>480px表示は縮小時の視認性確認用です。スマートフォン実機の性能は未検証です。</p></header>
<nav><button class="active" id="combat">実戦</button><button id="preview">EffectTest</button><button id="size">480pxで見る</button><select id="family" aria-label="武器系統"><option value="">すべての系統</option><option>Sword</option><option>Shield</option><option>Wand</option><option>Grimoire</option><option>Bible</option><option>Rosary</option></select><span class="count">32 SKILLS</span></nav>
<main class="grid" id="cards"></main><footer>記録時にUIを除いた戦闘カメラ出力を使用しています。音・キャラクターアニメーション・カメラ演出の追加はありません。<br><a href="../../docs/Vfx/検証報告.md">変更内容・検証報告</a> · <a href="Combat/results.csv">実戦の記録</a> · <a href="EffectTest/results.csv">EffectTestの記録</a></footer>
<script>
const rows=DATA;let mode='Combat';
const cards=document.querySelector('#cards');
function show(){cards.textContent='';for(const r of rows){let family=r.family==='StatDebuff'?'Grimoire':r.family;if(document.querySelector('#family').value&&family!==document.querySelector('#family').value)continue;let a=document.createElement('article');a.innerHTML=`<div class="copy"><div class="id">${r.id}</div><h2>${r.name}</h2><div class="concept">${r.concept}</div><details><summary>演出の変更点</summary><p>${r.change}</p></details></div><video controls muted loop playsinline preload="none" poster="${mode}/${r.id}.jpg" src="${mode}/${r.id}.mp4"></video>`;let v=a.querySelector('video');v.onplay=()=>{for(const other of document.querySelectorAll('video'))if(other!==v)other.pause()};cards.append(a)}document.querySelector('.count').textContent=cards.children.length+' SKILLS'}
function setMode(next){mode=next;document.querySelector('#combat').classList.toggle('active',mode==='Combat');document.querySelector('#preview').classList.toggle('active',mode==='EffectTest');show()}
document.querySelector('#combat').onclick=()=>setMode('Combat');document.querySelector('#preview').onclick=()=>setMode('EffectTest');document.querySelector('#family').onchange=show;document.querySelector('#size').onclick=function(){let small=document.body.classList.toggle('mobile');this.textContent=small?'全幅で見る':'480pxで見る';this.classList.toggle('active',small)};show();
</script></html>'''.replace('DATA',json.dumps(rows,ensure_ascii=False))
(project/'Captures/Vfx/index.html').write_text(html)
print(len(rows),'skills written')
