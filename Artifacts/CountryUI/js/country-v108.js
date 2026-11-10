const facilities = [
  { id: 'warrior', name: '戦士学校', icon: '⚔', role: '戦士の育成', description: '王国を守る戦士たちの学び舎。訓練場を拡張し、より多くの戦士を育成する。' },
  { id: 'magic', name: '魔法学校', icon: '✧', role: '魔法使いの育成', description: '魔術の知識と実践を伝える学校。研究室を拡張し、魔法使いの育成枠を増やす。' },
  { id: 'church', name: '教会', icon: '✝', role: '僧侶の育成', description: '祈りと癒やしを学ぶ聖堂。修練の場を整え、僧侶の育成枠を増やす。' },
  { id: 'spirit', name: '精霊使役学校', icon: '❋', role: '精霊使いの育成', description: '自然と精霊の声を聞く学び舎。契約の場を拡張し、精霊使いの育成枠を増やす。' },
  { id: 'market', name: '市場', icon: '⚖', role: '商業・交易', description: '商人と住民が品物を売買する場所。王国の商業を支える。' },
  { id: 'clinic', name: '診療所', icon: '✚', role: '住民の医療', description: '病気やけがをした住民を診療する施設。王国の暮らしを支える。' },
  { id: 'workshop', name: '工房', icon: '⚒', role: '道具・装備の生産', description: '職人が道具や装備を製作する施設。王国の生産を支える。' }
];
const soldierTypes = ['戦士', '魔法使い', '僧侶', '精霊使い'];
const soldierRoles = ['前衛・近接戦闘', '魔術による攻撃', '回復・支援', '精霊による支援'];
const resourceNames = ['資金', '木材', '鉱石'];
const resourceKinds = ['funds', 'wood', 'ore'];
const monthlyIncome = [100, 50, 30];
const resourceIcons = [
  '<svg viewBox="0 0 24 24" aria-hidden="true"><ellipse cx="12" cy="5.5" rx="7" ry="2.8"/><path d="M5 5.5v4.2c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8V5.5M5 9.7v4.2c0 1.5 3.1 2.8 7 2.8 1.1 0 2.2-.1 3.1-.3"/><path d="M12 3v6m-2.2-1.8c.4-.5 1.1-.8 2.2-.8 1.4 0 2.3.7 2.3 1.7 0 2.3-4.6 1.3-4.6 3.8 0 1 1 1.7 2.4 1.7 1 0 1.8-.3 2.2-.8"/></svg>',
  '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4.5h13a2.5 2.5 0 0 1 0 5H5a2.5 2.5 0 0 1 0-5Zm1 8h13a2.5 2.5 0 0 1 0 5H6a2.5 2.5 0 0 1 0-5Z"/><ellipse cx="5" cy="7" rx="1.4" ry="2.5"/><ellipse cx="6" cy="15" rx="1.4" ry="2.5"/></svg>',
  '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 3 8-1 6 6-3 13H6L3 8l4-5Z"/><path d="m7 3 5 6 3-7M12 9 6 21m6-12 6 12m-6-12 9-1"/></svg>'
];
const storageKey = 'war-simulation-country-v1';
const manualSaveKey = `${storageKey}-manual`;
const initialState = () => ({ version: 1, expeditions: [], nextExpeditionId: 1, year: 1, month: 1, day: 1, phase: 'planning', resources: [1000, 600, 360], levels: [1, 1, 1, 0, 0, 0, 0], queues: facilities.map(() => []), plan: facilities.map(() => 0), logs: [{ date: '1年 1月1日', text: '王国の運営を開始。施設を選び、強化命令を決めよう。' }], report: null, completed: [], training: [null, null, null, null], soldiers: [0, 0, 0, 0], characters: initialCharacters(), trainingUsed: [1,0,0,0], cpTrainingSeeded:true });
let state;
let timer = null;
let budgetTabChoice = 'budget';
let constructionChoice = 4;
let trainingChoice = 0;
let characterChoice = 0;
let soldierSort = "roster";
let soldierPage = "members";
let weaponChoice = 0;
let formationDraft = {};
let expeditionTarget = null;
let expeditionTab = "destination";
let expeditionPage = "status";
let expeditionChoice = null;
let expeditionReportChoice = 0;
let pendingSoldierRoute = "kingdom";
let returnToDefense = false;
let draggedCharacter = null;
const characterNames = "イングリッド マーニャ アリス イリア ミレディ メフィナ ミオ テレス モナ ドミニク エレノア ロン ケント レン アラン シータ ヴィスタ ライアン ガラム ゼオ ルールー モルゲン シャンク アスティナ シャロ レイネー ノエル ミシェル ユーイ ウルシャ".split(" ");
function initialCharacters() { return characterNames.map((name,id)=>({name,cp:id===2 ? 0 : 100,maxCP:100,qualified:true,skills:id<6 ? ["基本訓練 I"] : [],task:id===2 ? {school:0,slot:0,purpose:"復帰訓練",skill:"戦士訓練 1"} : null})); }
state = initialState();
let toastTimer;
let modalOpener;
let resumeAfterModal = false;
let previousResourceValues = null;
const modal = document.querySelector('#modal');
const validState = s => s?.version === 1 && Number.isInteger(s.year) && s.year > 0 && Number.isInteger(s.month) && s.month >= 1 && s.month <= 12 && Number.isInteger(s.day) && s.day >= 1 && s.day <= daysInMonth(s.month) && ['planning', 'running'].includes(s.phase) && Array.isArray(s.resources) && s.resources.length === 3 && s.resources.every(n => Number.isSafeInteger(n) && n >= 0) && ['levels', 'plan'].every(key => Array.isArray(s[key]) && [4, facilities.length].includes(s[key].length) && s[key].every(n => Number.isSafeInteger(n) && n >= 0)) && Array.isArray(s.queues) && s.queues.length === s.levels.length && s.queues.every(q => Array.isArray(q) && q.every(t => Number.isInteger(t.level) && t.level > 0 && Number.isInteger(t.remaining) && t.remaining > 0 && t.remaining <= 5)) && Array.isArray(s.logs) && s.logs.every(l => typeof l.date === 'string' && typeof l.text === 'string') && Array.isArray(s.completed) && s.completed.every(v => typeof v === 'string') && (s.training === undefined || (Array.isArray(s.training) && s.training.length === 4 && s.training.every(t => t === null || (Number.isSafeInteger(t.count) && t.count > 0 && Number.isInteger(t.remaining) && t.remaining >= 0 && t.remaining <= 5)))) && (s.soldiers === undefined || (Array.isArray(s.soldiers) && s.soldiers.length === 4 && s.soldiers.every(n => Number.isSafeInteger(n) && n >= 0))) && (s.characters === undefined || (Array.isArray(s.characters) && s.characters.length===30 && s.characters.every(c=>typeof c.name==='string' && Number.isInteger(c.cp) && c.cp>=0 && Number.isInteger(c.maxCP) && c.cp<=c.maxCP && typeof c.qualified==='boolean' && Array.isArray(c.skills) && c.skills.every(v=>typeof v==='string') && (!c.task || (Number.isInteger(c.task.school) && c.task.school>=0 && c.task.school<4 && typeof c.task.skill==='string' && typeof c.task.purpose==='string'))))) && (s.trainingUsed===undefined || (Array.isArray(s.trainingUsed) && s.trainingUsed.length===4 && s.trainingUsed.every(n=>Number.isInteger(n) && n>=0))) && (s.report === null || (typeof s.report.title === 'string' && Array.isArray(s.report.completed) && s.report.completed.every(v => typeof v === 'string')));
try { const saved = JSON.parse(localStorage.getItem(storageKey)); if (validState(saved)) state = saved; } catch {}
function extendBuildings() {
  state.expeditions ??= state.activeExpedition ? [{...state.activeExpedition,id:1}] : [];
  delete state.activeExpedition;
  state.nextExpeditionId ??= Math.max(0,...state.expeditions.map(m=>m.id))+1;
  (state.expeditionReports||[]).forEach(r=>r.read ??= !state.expeditionUnread);
  state.characters ??= initialCharacters();
  if(!state.rosterReadySeeded){state.characters.forEach(c=>c.qualified=true);state.rosterReadySeeded=true;}
  state.characters.forEach((c,i)=>{c.name=characterNames[i];c.defending ??= false;delete c.weapon;c.personality ??= ["勇敢","冷静","標準","温厚"][i%4];});
  state.trainingUsed ??= [0,0,0,0];
  state.training = [null,null,null,null];
  state.soldiers ??= [0, 0, 0, 0];
  for (const key of ['levels', 'plan', 'budgetShare', 'budget']) {
    if (state[key]) while (state[key].length < facilities.length) state[key].push(0);
  }
  while (state.queues.length < facilities.length) state.queues.push([]);
  state.excludedSpirit ??= { level:state.levels[3], queue:state.queues[3], plan:state.plan[3] };
  state.levels[3]=0; state.queues[3]=[]; state.plan[3]=0;
  for (const key of ['budgetShare','budget']) if(state[key]) state[key][3]=0;
  state.characters.forEach(c=>{if(c.task?.school===3)c.task=null;}); state.trainingUsed[3]=0;
  for(let school=0;school<3;school++) {
    const assigned=state.characters.filter(c=>c.task?.school===school); const slots=new Set();
    assigned.forEach(c=>{if(!Number.isInteger(c.task.slot) || slots.has(c.task.slot)){let slot=0;while(slots.has(slot))slot++;c.task.slot=slot;}slots.add(c.task.slot);});
  }
  if(!state.cpTrainingSeeded) {
    state.characters.filter(c=>c.cp===0 && !c.task).forEach(c=>{let slot=0;while(state.characters.some(ch=>ch.task?.school===0 && ch.task.slot===slot))slot++;if(slot<trainingLimit(0))c.task={school:0,slot,purpose:'復帰訓練',skill:trainingSkill(0)};});
    state.cpTrainingSeeded=true;
  }
  updateTrainingUsed();
}
extendBuildings();
formationDraft=Object.fromEntries(Object.entries(state.expeditionPlan||{}).filter(([id])=>expeditionEligible(state.characters[id])));expeditionTarget=state.expeditionTarget??null;
function populationCount() { return state.population ?? 12560; }
function taxIncome() { return state.taxOverride ?? Math.floor(populationCount() / 125); }
function updatePopulation() {
  const before = populationCount();
  const births = Math.floor(before * .004); const deaths = Math.floor(before * .002);
  const arrivals = Math.floor(before * .003); const departures = Math.floor(before * .001);
  state.population = before + births - deaths + arrivals - departures;
  state.populationChange = { before, births, deaths, arrivals, departures, after: state.population };
  log(`人口 ${state.population.toLocaleString()}人。前月から＋${state.population-before}人。翌月の税収 ${taxIncome()}。`);
}
function showPopulation() {
  const c = state.populationChange;
  const delta = c ? c.after-c.before : 0;
  openModal('人口', `<div class="population-overview"><small>現在の人口</small><strong>${populationCount().toLocaleString()}<small>人</small></strong><span class="tax-accent">${c ? `前月から ${delta >= 0 ? '＋' : '−'}${Math.abs(delta)}人` : 'まだ月次の増減記録はありません'}</span></div><h3>前月の増減</h3>${c ? `<div class="population-breakdown"><div><span>出生</span><b class="tax-accent">＋${c.births}</b></div><div><span>死亡</span><b>−${c.deaths}</b></div><div><span>転入</span><b class="tax-accent">＋${c.arrivals}</b></div><div><span>転出</span><b>−${c.departures}</b></div></div>` : '<p class="muted">予算を確定して次の月へ進むと、内訳を確認できる。</p>'}<div class="population-tax"><span>今月の税収</span><strong>${taxIncome()}</strong><small>人口に応じて税収が変わる。</small></div><p class="muted">試作ルール：人口は月初に更新。出生0.4％・死亡0.2％・転入0.3％・転出0.1％。税収は125人につき1。端数は切り捨て。</p>`, '<button data-action="close">閉じる</button>');
}
function daysInMonth(month) { return [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1]; }
function dateText() { return `${state.year}年${state.month}月${state.day}日`; }
function escapeHTML(value) { return String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); }
function save(manual = false) { try { localStorage.setItem(manual ? manualSaveKey : storageKey, JSON.stringify(state)); if (manual) toast('手動保存しました。後でこの状態に戻せます'); } catch { toast('記録を保存できません。保存領域へのアクセスを確認してください'); } }
function log(text) { state.logs.unshift({ date: dateText(), text }); state.logs = state.logs.slice(0, 200); state.recordsUnread = true; }
function toast(message) { const el = document.querySelector('#toast'); el.textContent = message; el.classList.add('visible'); clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.remove('visible'), 3200); }
function cost(level) { return [100 * level, 50 * level, 30 * level]; }
function targetLevel(i) { return state.levels[i] + state.queues[i].length + state.plan[i] + 1; }
function facilityPlanCost(i) {
  const total = [0, 0, 0];
  for (let n = 1; n <= state.plan[i]; n++) cost(state.levels[i] + state.queues[i].length + n).forEach((c, j) => total[j] += c);
  return total;
}
function totalCost() { return facilities.reduce((total, _, i) => facilityPlanCost(i).map((cost, j) => total[j] + cost), [0, 0, 0]); }
function affordable() { return totalCost().every((n, i) => n <= state.resources[i]); }
function countPlan() { return state.plan.reduce((a, b) => a + b, 0); }
function costsHTML(values) { return `<div class="costs">${values.map((n, i) => `<span>${resourceNames[i]} <b>${n.toLocaleString()}</b></span>`).join('')}</div>`; }
function queueHTML(i) { const q = state.queues[i]; return q.length ? `<div class="queue">${q[0].level === 1 ? "新規建設中" : `Lv.${q[0].level}へ強化中`} · あと${q[0].remaining}日${q.length > 1 ? ` / 待ち ${q.length - 1}件` : ''}<div class="progress" aria-label="強化の進捗"><span style="width:${(5 - q[0].remaining) * 20}%"></span></div></div>` : '<div class="queue">工事の予定はありません</div>'; }
function route() {
  const name = location.hash.slice(1);
  return ['kingdom', 'facilities', 'plan', 'history', 'training', 'soldiers', 'expedition'].includes(name) ? name : 'kingdom';
}
function constructionCount() { return state.queues.reduce((n, q) => n + q.length, 0); }
function nextEventDays() { return Math.min(daysInMonth(state.month) - state.day + 1, ...state.expeditions.map(m=>m.remaining), ...state.queues.filter(q => q.length).map(q => q[0].remaining), ...state.training.filter(t => t?.remaining > 0).map(t => t.remaining)); }
function trainingCount() { return state.characters.filter(c=>c.task).length; }
function readySoldiers() { return 0; }
function hasWork() { return constructionCount() + trainingCount() > 0 || state.expeditions.length>0; }
function nextEventText() {
  const days = nextEventDays();
  const names = facilities.filter((_, i) => state.queues[i][0]?.remaining === days).map(f => f.name);
  return names.length ? `${names.join('・')}の工事完了まであと${days}日。` : `次の月次報告まであと${days}日。`;
}
function windowTitle(title, extra = '') {
  return `<div class="window-title"><h1>${title}</h1><div class="actions">${extra}<button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div></div>`;
}
function renderKingdom() {
  return `<div class="settlement" aria-label="王国の城下町"><figure class="city-minimap" aria-label="城下町の全体像"><div class="minimap-image"><img src="assets/city-minimap.svg" alt="真上から見た城下町の簡略図"><span class="minimap-viewport" role="img" aria-label="現在の表示範囲（仮）"></span></div><figcaption>全体図</figcaption></figure>
    ${facilities.map((f, i) => state.levels[i] || state.queues[i].length ? `<button class="building-marker marker-${f.id} ${state.plan[i] ? 'selected' : state.queues[i].length ? 'building' : ''}" data-action="detail" data-index="${i}">
      <span class="marker-emblem" aria-hidden="true">${f.icon}</span>
      <span class="marker-name">${f.name}<small>${state.levels[i] ? `Lv.${state.levels[i]}` : '建設中'}${state.plan[i] ? ` · 計画 ${state.plan[i]}` : ''}</small></span>
      ${state.queues[i].length ? `<span class="construction">⚒ あと${state.queues[i][0].remaining}日</span>` : ''}
    </button>` : '').join('')}
  </div>`;
}
function renderFacilities() {
  return `<section class="game-window facilities-window" aria-label="建築物一覧">
    ${windowTitle('建築物一覧', `<span class="tag">${state.phase === 'planning' ? '予算未決定' : '工事中'}</span>`)}
    <div class="window-content">
      <div class="ledger-labels"><span>施設</span><span>用途</span><span>工事・予算計画</span><span>確認</span></div>
      ${facilities.map((f, i) => state.levels[i] || state.queues[i].length ? `<div class="facility-row ${state.plan[i] ? 'selected-row' : ''}">
        <div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><div><h2>${f.name}</h2><small>${f.role}</small></div><span class="level">${state.levels[i] ? `Lv.${state.levels[i]}` : '建設中'}</span></div>
        <div class="capacity">${i < 4 ? `<b>${state.levels[i] * 5}</b><small>人 / 月</small>` : `<small>${f.role}</small>`}</div>
        <div class="facility-work">${queueHTML(i)}${state.plan[i] ? `<small class="success">予算案：強化 ${state.plan[i]}段階</small>` : ''}</div>
        <div class="row-orders"><button data-action="detail" data-index="${i}" aria-label="${f.name}の詳細">詳細</button></div>
      </div>` : '').join('')}
    </div>
    <div class="window-bottom"><small>建物の状態を確認。強化への投資は予算で決める。</small><button data-action="navigate-plan">予算を確認</button></div>
  </section>`;
}
function budgetValues() {
  const locked = state.phase !== 'planning' && state.monthBudget?.pool !== undefined;
  const tax = locked ? state.monthBudget.tax : taxIncome();
  const treasury = locked ? state.monthBudget.treasury : state.resources[0];
  const pool = tax + (state.useTreasury ? treasury : 0);
  if (locked) {
    const f = state.monthBudget;
    state.budgetShare = facilities.map((_,i) => f.shares[i] || 0);
    state.budget = facilities.map((_,i) => f.budgets[i] || 0);
    return { tax:f.tax, treasury:f.treasury, pool:f.pool, minimums:state.budgetShare, share:state.budgetShare.reduce((a,b)=>a+b,0), allocation:f.allocation, deposit:f.deposit, withdrawal:f.withdrawal, feasible:true };
  }
  const minimums=state.levels.map(level=>!level?0:pool?level*10/pool*100:100);
  state.budgetAmounts ??= facilities.map((_,i)=>Math.floor(pool*(state.budgetShare?.[i]||0)/100));
  state.budget=state.budgetAmounts.map((amount,i)=>state.levels[i]?Math.max(state.levels[i]*10,Math.round(amount||0)):0);
  const required=state.levels.reduce((sum,level)=>sum+level*10,0);
  let excess=Math.max(0,state.budget.reduce((a,b)=>a+b,0)-pool);
  if(required<=pool)state.budget=state.budget.map((amount,i)=>{const trim=Math.min(excess,amount-state.levels[i]*10);excess-=trim;return amount-trim;});
  state.budgetAmounts=[...state.budget];
  state.budgetShare=state.budget.map(amount=>pool?amount/pool*100:0);
  const allocation=state.budget.reduce((a,b)=>a+b,0),share=state.budgetShare.reduce((a,b)=>a+b,0);
  return {tax,treasury,pool,minimums,share,allocation,deposit:Math.max(0,tax-allocation),withdrawal:Math.max(0,allocation-tax),feasible:required<=pool};
}
const percent=n=>Number(n.toFixed(1)).toLocaleString();
function changeBudget(i,value,live=false) {
  const b=budgetValues();if(!b.feasible)return;
  const amount=Math.max(state.levels[i]*10,Math.min(b.pool-b.allocation+state.budget[i],Math.round(b.pool*value/100)));
  state.budgetAmounts[i]=amount;budgetValues();save();
  if(!live){render();return;}
  const fresh=document.createElement('div');fresh.innerHTML=renderPlan();
  for(const selector of ['.allocation-bar','.budget-adjust strong','.allocation-amount','.allocation-effect','.budget-reserve','.budget-footer']){
    const targets=document.querySelectorAll(selector),sources=fresh.querySelectorAll(selector);
    targets.forEach((el,n)=>{if(sources[n])el.innerHTML=sources[n].innerHTML;});
  }
  document.querySelectorAll('[data-budget-index]').forEach(el=>{el.value=state.budgetShare[Number(el.dataset.budgetIndex)];el.setAttribute('aria-valuetext',`${percent(Number(el.value))}％`);});
  for(const action of ['budget-down','budget-up'])document.querySelectorAll(`[data-action="${action}"]`).forEach(el=>{el.disabled=fresh.querySelector(`[data-action="${action}"][data-index="${el.dataset.index}"]`).disabled;});
}
function toggleTreasury(checked) {
  const b=budgetValues();
  if(!checked && b.allocation>b.tax){toast('配分額が税収を超えています。先に配分を減らしてください');render();return;}
  state.budgetAmounts=[...state.budget];state.useTreasury=checked;budgetValues();save();render();
}
function budgetTabs() {
  return `<div class="window-title budget-tab-header"><div class="budget-tabs" role="tablist" aria-label="今月の国家運営">${[['budget','今月の予算'],['construction','建設'],['training','兵士育成']].map(([tab,name])=>`<button role="tab" id="tab-${tab}" aria-controls="panel-${tab}" aria-selected="${budgetTabChoice === tab}" tabindex="${budgetTabChoice === tab ? 0 : -1}" data-action="budget-tab" data-tab="${tab}">${name}</button>`).join('')}</div><button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div>`;
}
function monthlyFooter() {
  const b = budgetValues(), locked = state.phase !== 'planning';
  const trainees = state.characters.filter(c => c.task).length;
  return `<div class="window-bottom"><div class="budget-footer"><span>予算配分 <b>${percent(b.share)}％</b></span><span>訓練予定 <b>${trainees}人</b></span><small>${locked ? '今月は進行中' : '確定すると時間が進む'}</small></div><button class="primary" data-action="commit" ${locked || !b.feasible ? 'disabled' : ''}>${locked ? '今月の予算は確定済み' : '予算を確定して今月を開始'}</button></div>`;
}
function constructionPrices(price) {
  return `<div class="construction-cost">${price.map((n,j)=>`<div class="${n>state.resources[j] ? 'cost-short' : ''}"><small>${['国庫','木材','鉱石'][j]}</small><b>${n.toLocaleString()}</b></div>`).join('')}</div>`;
}
function renderConstruction() {
  const f = facilities[constructionChoice];
  const pending = state.queues[constructionChoice].length > 0;
  const built = state.levels[constructionChoice] > 0;
  const canBuild = cost(1).every((n,j)=>n<=state.resources[j]);
  return `<section class="game-window budget-window construction-window" aria-label="建設">${budgetTabs()}<div class="window-content construction-content" role="tabpanel" id="panel-construction" aria-labelledby="tab-construction" tabindex="0">
    <div class="construction-wallet">${['人口','税収','国庫','木材','鉱石'].map((name,i)=>`<div><span class="resource-symbol">${i<2?'':resourceIcons[i-2]}</span><span>${name}</span><strong>${[populationCount(),taxIncome(),...state.resources][i].toLocaleString()}</strong></div>`).join('')}</div>
    <section class="new-construction"><h2>新規建設</h2><div class="construction-picker"><div class="construction-catalog" role="group" aria-label="建設する建物を選択">${facilities.slice(4).map((building,n)=>{const i=n+4; return `<button class="construction-option ${i===constructionChoice ? 'is-selected' : ''}" data-action="construction-select" data-index="${i}" aria-pressed="${i===constructionChoice}"><span class="building-preview preview-${building.id}" aria-hidden="true"></span><span><b>${building.name}</b><small>${building.role}</small></span><span class="catalog-status">${state.levels[i] ? '建設済み' : state.queues[i].length ? '建設中' : '未建設'}</span></button>`;}).join('')}</div><section class="construction-inspector" aria-label="選択した建物の詳細"><div class="inspector-heading"><span class="facility-icon">${f.icon}</span><div><h3>${f.name}</h3><small>${f.role}</small></div></div><p>${f.description}</p>${constructionPrices(cost(1))}<div class="construction-order"><span>工期 <b>5日</b></span><button class="primary" data-action="new-build-confirm" data-index="${constructionChoice}" ${built || pending || !canBuild ? 'disabled' : ''}>${built ? '建設済み' : pending ? '建設中' : !canBuild ? '資源不足' : '建設を決める'}</button></div></section></div></section>
    <section class="existing-construction"><h2>既存の建物を強化</h2><div class="construction-columns"><span>建物</span><span>強化に必要な資源</span><span>工期</span><span></span></div>
    ${facilities.map((f,i)=>{ if (!state.levels[i]) return ''; const next = state.levels[i]+state.queues[i].length+1; const price = cost(next); const canBuild = price.every((n,j)=>n<=state.resources[j]); return `<article class="construction-row"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${next-1} → Lv.${next}</small></div></div>${constructionPrices(price)}<div class="construction-duration"><b>5日</b><small>${state.queues[i].length ? '先行工事の後に着工' : 'すぐに着工'}</small></div><button class="primary" data-action="build" data-index="${i}" aria-label="${f.name}をLv.${next}へ強化" ${canBuild ? '' : 'disabled'}>${canBuild ? '強化する' : '資源不足'}</button></article>`; }).join('')}</section>
    <div class="construction-queues"><h2>工事状況 <small>${constructionCount()}件</small></h2><div>${constructionCount() ? facilities.map((f,i)=>state.queues[i].length ? `<section><h3>${f.name}</h3>${queueHTML(i)}${state.queues[i].map((t,n)=>`<button class="cancel-construction" data-action="cancel-build" data-index="${i}" data-position="${n}">Lv.${t.level}の工事をキャンセル</button>`).join('')}</section>` : '').join('') : '<p class="muted">進行中の工事はありません。</p>'}</div></div>
    </div>${monthlyFooter()}</section>`;
}
function confirmNewBuilding(i) {
  const f = facilities[i];
  if (!f || i < 4 || state.levels[i] || state.queues[i].length) return;
  openModal(`${f.name}を建設`, `<p>${f.description}</p>${constructionPrices(cost(1))}<div class="construction-confirm-line"><span>工期</span><b>5日</b></div><p class="muted">着工時に費用を支払い、工事状況に追加する。</p>`, `<button data-action="close">戻る</button><button class="primary" data-action="new-build" data-index="${i}">着工する</button>`);
}
function renderPlan() {
  if (budgetTabChoice === 'construction') return renderConstruction();
  if (budgetTabChoice === 'training') return renderTraining();
  const b = budgetValues(); const locked = state.phase !== 'planning';
  const money = n => n.toLocaleString();
  return `<section class="game-window budget-window" aria-label="今月の予算">
    ${budgetTabs()}
    <div class="window-content budget-content" role="tabpanel" id="panel-budget" aria-labelledby="tab-budget" tabindex="0">
      <div class="budget-source"><div class="budget-source-toolbar"><span>予算の財源</span><label class="treasury-option ${state.useTreasury ? 'is-enabled' : ''}"><input type="checkbox" data-use-treasury aria-label="国庫を配分に使う" ${state.useTreasury ? 'checked' : ''} ${locked ? 'disabled' : ''}><span>国庫も配分に含める</span></label></div><div class="budget-source-tax"><small>税収</small><strong>${money(b.tax)}</strong><span class="source-note">今月の収入</span></div><div class="budget-source-treasury"><small>国庫</small><strong>${money(b.treasury)}</strong><span class="source-note">${state.useTreasury ? '配分に含める' : '今回は使わない'}</span></div><div class="budget-source-total"><small>配分できる合計</small><strong>${money(b.pool)}</strong><span class="source-note">${state.useTreasury ? '税収と国庫を含む' : '税収のみ'}</span></div></div>
      <div class="allocation-bar" aria-label="建物へ${percent(b.share)}％、未配分${percent(Math.max(0,100-b.share))}％">${state.budgetShare.map((n,i)=>n ? `<span class="allocation-segment allocation-${i}" style="flex:${n}" title="${facilities[i].name} ${percent(n)}％"></span>` : '').join('')}<span class="allocation-segment allocation-saved" style="flex:${Math.max(0,100-b.share)}" title="未配分 ${percent(Math.max(0,100-b.share))}％"></span></div>
      <div class="budget-layout"><div class="building-budgets"><div class="budget-columns"><span>建築物</span><span>予算の配分</span></div>
      ${facilities.map((f,i)=> i!==3 && state.levels[i] && (!locked || state.monthBudget?.shares[i]) ? `<article class="building-budget"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${state.levels[i]}</small><p class="facility-purpose">${facilityPurposes[i]}</p></div></div><div class="allocation-control"><div class="allocation-top"><div class="budget-adjust tax-accent"><button data-action="budget-down" data-index="${i}" aria-label="${f.name}の配分率を1％減らす" ${locked || !b.feasible || state.budgetShare[i] <= b.minimums[i] ? 'disabled' : ''}>−</button><strong><small>配分率</small>${percent(state.budgetShare[i])}<small>％</small></strong><button data-action="budget-up" data-index="${i}" aria-label="${f.name}の配分率を1％増やす" ${locked || !b.feasible || b.share >= 100 ? 'disabled' : ''}>＋</button></div><div class="allocation-amount"><small>配分額</small><b>${money(state.budget[i])}</b></div></div><input type="range" min="${b.minimums[i]}" max="100" step="any" value="${state.budgetShare[i]}" data-budget-index="${i}" aria-label="${f.name}の配分率" aria-valuetext="${percent(state.budgetShare[i])}％、最低${percent(b.minimums[i])}％" ${locked || !b.feasible ? 'disabled' : ''}><div class="allocation-minimum"><span>最低 ${percent(b.minimums[i])}％</span><span>必要な運営費を含む</span></div><div class="allocation-effect">${facilityEffect(i)}<small>効果は仮設定</small></div></div></article>` : '').join('')}</div>
      <aside class="budget-reserve"><h2>国庫の見込み</h2><div><span>現在の国庫</span><b>${money(state.resources[0])}</b></div><div class="reserve-accent"><span>配分に使う国庫</span><b>−${money(b.withdrawal)}</b></div><div class="tax-accent"><span>税収から積立</span><b>＋${money(b.deposit)}</b></div><div class="budget-balance"><span>${locked ? '予算確定後の国庫' : '配分後に残る国庫'}</span><strong>${money(locked ? state.resources[0] : state.resources[0]+b.deposit-b.withdrawal)}</strong></div><p>未配分の税収は国庫へ。使わなかった国庫はそのまま残る。</p>${!b.feasible ? '<p class="reserve-accent">最低配分を満たすには、国庫も使う必要があります。</p>' : ''}</aside></div>
    </div>${monthlyFooter()}</section>`;
}
const facilityPurposes=['兵士の訓練を支える','魔術の訓練を支える','回復と支援の訓練を支える','','商業を支える','住民の健康を守る','装備の生産を支える'];
function facilityEffect(i){
 const amount=state.budget[i]||0,level=state.levels[i];
 return i<3?`訓練支援 <b>${Math.round(amount/(level*10)*100)}％</b>`:i===4?`交易量 <b>${amount*2}</b>`:i===5?`診療人数 <b>${amount*3}人</b>`:`生産支援 <b>${amount*2}％</b>`;
}
function skillLink(skill){return `<button class="skill-link" data-action="skill-info" data-skill="${escapeHTML(skill)}">${escapeHTML(skill)}</button>`;}
function showCharacterSkills(id){
 characterChoice=id;const c=state.characters[id];
 openModal(`${escapeHTML(c.name)}のスキル`,`<section class="character-skill-overview"><h3>習得済み <small>${c.skills.length}件</small></h3>${learnedSkills(c)}${c.task?`<h3>今月の訓練で習得</h3>${skillLink(c.task.skill)}<p class="muted">${facilities[c.task.school].name} · 月末に完了</p>`:''}</section>`,'<button data-action="close">戻る</button>');
 modal.classList.add('skill-dialog');
 modal.querySelectorAll('[data-action="skill-info"]').forEach(button=>button.dataset.backCharacter=id);
}
function skillData(skill){
 const school=skill.includes('魔法')?1:skill.includes('僧侶')?2:0,level=Number(skill.match(/(\d+)$/)?.[1]||1);
 const data=[{kind:'近接攻撃',description:'敵１人に剣による強い攻撃を行う。',target:'敵１人',range:'1マス',power:100,stat:'STR'},{kind:'魔法攻撃',description:'離れた敵１人に魔力による攻撃を行う。',target:'敵１人',range:'3マス',power:90,stat:'INT'},{kind:'回復',description:'味方１人のHPを回復する。',target:'味方１人',range:'2マス',power:80,stat:'FAI'}][school];
 return {...data,school,level,power:data.power+(level-1)*20};
}
function showSkillInfo(skill,backCharacter){
 const d=skillData(skill),c=state.characters[characterChoice],known=c.skills.includes(skill),scheduled=c.task?.skill===skill;
 openModal(skill,`<section class="skill-detail"><div class="skill-kind"><span>${facilities[d.school].icon} ${d.kind}</span><small>Lv.${d.level}</small></div><p class="skill-description">${d.description}</p><dl class="skill-facts"><div><dt>対象</dt><dd>${d.target}</dd></div><div><dt>射程</dt><dd>${d.range}</dd></div><div><dt>${d.kind==='回復'?'回復力':'威力'}</dt><dd>${d.power}</dd></div><div><dt>参照能力</dt><dd>${d.stat}</dd></div></dl><div class="skill-learning"><span>${escapeHTML(c.name)}</span><b>${known?'習得済み':scheduled?'今月の訓練で習得予定':'未習得'}</b></div><p class="skill-source">${facilities[d.school].name} Lv.${d.level}の訓練で習得。<br>学校の強化だけでは、習得済みスキルは変わらない。</p><small class="muted">効果・数値は表示確認用の仮設定。</small></section>`,backCharacter===undefined?'<button data-action="close">戻る</button>':`<button data-action="character-skills" data-index="${backCharacter}">スキル一覧へ戻る</button>`);
 modal.classList.add('skill-dialog');
}
function learnedSkills(c){return c.skills.length?`<ul class="learned-skills">${c.skills.map(skill=>{const d=skillData(skill);return `<li>${skillLink(skill)}<small>${d.kind} · ${d.target}</small></li>`;}).join('')}</ul>`:'<p class="muted">習得済みスキルなし</p>';}
function upgradeEffects(i,level){
 return `<section class="upgrade-effects"><h3>強化で変わること</h3>${i<3?`<div><span>毎月の訓練枠</span><b>${(level-1)*5}人 <em>→</em> ${level*5}人</b></div><div><span>習得可能スキル</span><span>${soldierTypes[i]}訓練 ${level-1} <em>→</em> ${soldierTypes[i]}訓練 ${level}</span></div><p>強いスキルの習得には、強化後にもう一度訓練が必要。</p>`:`<div><span>${['交易規模','診療能力','生産能力'][i-4]||'施設能力'}</span><b>${level-1}段階 <em>→</em> ${level}段階</b></div>`}<small>効果・数値は仮設定</small></section>`;
}
function trainingLimit(i) { return state.levels[i] * 5; }
function trainingPurpose(c) { return !c.qualified ? '初回訓練' : c.cp===0 ? '復帰訓練' : 'スキル習得'; }
function trainingSkill(i) { return `${soldierTypes[i]}訓練 ${state.levels[i]}`; }
function characterStatus(c) { return c.task ? `${facilities[c.task.school].name}で訓練予定` : !c.qualified ? '未訓練' : c.cp===0 ? '使用不可' : '使用可能'; }
function rosterSelected(id,mode){const c=state.characters[id];return mode==='training'?c.task?.school===trainingChoice:mode==='defense'?c.defending:Object.hasOwn(formationDraft,id);}
function rosterReason(id,mode){
  const c=state.characters[id];
  if(onExpedition(c))return '遠征中';
  if(mode==='training')return c.qualified && c.cp>0 && c.skills.includes(trainingSkill(trainingChoice)) && c.task?.school!==trainingChoice?'習得済み':'';
  return !c.qualified || c.cp===0?'戦闘不能':c.task?'訓練中':mode==='expedition' && c.defending?'防衛中':'';
}
function rosterStatus(id,mode){const c=state.characters[id];return rosterReason(id,mode)|| (rosterSelected(id,mode)?'配置済み':mode==='training' && c.task?'他の学校に配置中':'配置可能');}
function rosterPortrait(id){const c=state.characters[id];return `<span class="round-avatar ${c.cp===0?'portrait-cp-zero':''}">${expeditionImage(id%6,c.name)}</span>`;}
function rosterButton(id,mode,action='roster-place',extra=''){
  const c=state.characters[id],selected=rosterSelected(id,mode),reason=rosterReason(id,mode),weapon=mode==='defense' && selected?state.defenseWeapons?.[id]:mode==='expedition' && selected?formationDraft[id]:'';
  return `<article class="roster-entry"><button class="roster-character ${selected?'is-selected':''} ${reason?'roster-unavailable':''} ${c.cp===0?'exhausted-character':''}" data-action="${action}" data-mode="${mode}" data-index="${id}" ${extra} draggable="${!reason}" data-drag-character="${id}" aria-label="${c.name}：${rosterStatus(id,mode)}" ${reason?'disabled':''}><span class="roster-person">${rosterPortrait(id)}<span><b>${c.name}</b><span class="roster-cp ${c.cp===0?'cp-empty':''}">CP <strong>${c.cp}</strong> / ${c.maxCP}</span><small>${escapeHTML(c.personality||'')}</small></span><span class="roster-mark">${selected?'✓':'＋'}</span></span>${expeditionStats(c,id,weapon)}<span class="roster-status">${rosterStatus(id,mode)}</span></button><button class="roster-skill-open" data-action="character-skills" data-index="${id}" aria-label="${c.name}のスキルを見る">スキル <b>${c.skills.length}</b> <span>詳細 ›</span></button></article>`;
}
function rosterList(mode){
  const ids=state.characters.map((c,id)=>id).sort((a,b)=>Number(!!rosterReason(a,mode))-Number(!!rosterReason(b,mode)) || (mode==='training'?Number(state.characters[a].cp===0)-Number(state.characters[b].cp===0):0) || (mode==='defense' && soldierSort==='personality'?(state.characters[a].personality||'').localeCompare(state.characters[b].personality||'','ja'):0) || a-b);
  return ids.map(id=>rosterButton(id,mode)).join('');
}
function assignedCharacter(id,mode){
  const c=state.characters[id],weapon=mode==='defense'?state.defenseWeapons?.[id]:mode==='expedition'?formationDraft[id]:'',w=weapons[weapon];
  const remove=mode==='training'?'character-cancel':mode==='defense'?'defense-remove':'roster-remove',equip=mode==='defense'?'defense-weapon':'expedition-pick-weapon';
  return `<article class="assigned-character ${c.cp===0?'exhausted-character':''}" ${mode==='training'?`data-drop-slot="${c.task.slot}" data-drop-school="${c.task.school}"`:''}><button class="assigned-person" draggable="${!onExpedition(c)}" data-drag-character="${id}" data-action="character-detail" data-index="${id}" aria-label="${c.name}の詳細"><span class="roster-person">${rosterPortrait(id)}<span><b>${c.name}</b><span class="roster-cp ${c.cp===0?'cp-empty':''}">CP <strong>${c.cp}</strong> / ${c.maxCP}</span></span></span></button><button class="assigned-remove" data-action="${remove}" data-mode="${mode}" data-index="${id}" aria-label="${c.name}を配置から外す">×</button>${mode!=='training'?`<button class="assigned-weapon ${w?'':'unassigned'}" data-action="${equip}" data-index="${id}" aria-label="${c.name}の武器を設定">${w?expeditionImage(Number(weapon)+6,w.name,'weapon-photo'):'<span class="weapon-empty">＋</span>'}<span>${w?w.name:'武器未設定'}</span></button>`:''}${expeditionStats(c,id,weapon)}</article>`;
}
function characterDetails(id,mode){
  const c=state.characters[id],weapon=mode==='defense' && c.defending?state.defenseWeapons?.[id]:mode==='expedition'?formationDraft[id]:'';
  return `<header class="roster-detail-header">${rosterPortrait(id)}<div><h2>${c.name}</h2><small>${rosterStatus(id,mode)}</small></div></header><p>${escapeHTML(c.personality||'')}</p><div class="details-cp"><span>CP</span><strong class="${c.cp===0?'cp-empty':''}">${c.cp}<small> / ${c.maxCP}</small></strong></div><div class="details-cp-bar"><span style="width:${c.cp/c.maxCP*100}%"></span></div>${expeditionStats(c,id,weapon)}<h3>習得済みスキル</h3>${learnedSkills(c)}`;
}
function placeRoster(id,mode){
  characterChoice=id;if(rosterReason(id,mode))return;
  if(rosterSelected(id,mode)){render();return;}
  if(mode==='defense')addDefense(id);
  else if(mode==='expedition'){formationDraft[id]='';render();}
  else {const slot=Array.from({length:trainingLimit(trainingChoice)},(_,n)=>n).find(n=>!state.characters.some(c=>c.task?.school===trainingChoice && c.task.slot===n));if(slot!==undefined)placeTraining(id,trainingChoice,slot);else render();}
}

function renderTraining() {
 const i=trainingChoice,c=state.characters[characterChoice],limit=trainingLimit(i);
 const trainees=state.characters.map((ch,id)=>({ch,id})).filter(({ch})=>ch.task?.school===i);
 const skill=trainingSkill(i),known=c.skills.includes(skill),available=!c.task && trainees.length<limit && !(c.qualified && c.cp>0 && known);
 return `<section class="game-window budget-window training-window" aria-label="兵士育成">${budgetTabs()}<div class="training-body" role="tabpanel" id="panel-training" aria-labelledby="tab-training" tabindex="0">
 <nav class="school-tabs" aria-label="訓練する学校">${facilities.slice(0,3).map((f,n)=>`<button class="school-tab ${n===i?'selected-school':''}" data-action="training-select" data-index="${n}" aria-pressed="${n===i}"><span class="facility-icon">${f.icon}</span><span><b>${f.name}</b><small>Lv.${state.levels[n]}</small></span><span class="school-tab-count"><b>${state.trainingUsed[n]}</b> / ${trainingLimit(n)}<small>枠使用</small></span></button>`).join('')}</nav>
 <div class="school-explanation"><span>毎月 <b>${limit}人</b> を訓練</span><span>習得 ${skillLink(skill)}</span></div>
 <section class="training-board" aria-label="今月の訓練枠"><header><h2>今月の訓練枠 <small>月末に完了</small></h2><strong>残り ${limit-trainees.length}<small> / ${limit}枠</small></strong></header><div class="training-slot-list">${Array.from({length:limit},(_,n)=>{const t=trainees.find(t=>t.ch.task.slot===n);return t?assignedCharacter(t.id,'training'):`<button class="round-slot empty-slot" data-drop-slot="${n}" data-drop-school="${i}" data-action="training-slot-pick" data-index="${i}" aria-label="訓練の空き枠${n+1}に配置"><span class="round-avatar">＋</span><small>空き枠 ${n+1}</small></button>`;}).join('')}</div></section>
 <div class="character-area"><section class="candidate-panel" aria-label="訓練候補"><header><h2>キャラクター <small>30人</small></h2><span>クリックで空き枠へ配置 · ドラッグで入れ替え</span></header><div class="candidate-list roster-grid" data-drop-roster="true">${rosterList('training')}</div></section>
 <aside class="character-details" aria-label="キャラクター詳細">${characterDetails(characterChoice,'training')}<div class="details-training"><h3>${c.task?'今月の訓練':'この学校での訓練'}</h3><b>${c.task?c.task.purpose:trainingPurpose(c)}</b><p>${c.task?`${facilities[c.task.school].name} · 枠${c.task.slot+1}`:facilities[i].name}<br>${skillLink(c.task?.skill||skill)}<small class="training-skill-status">${c.skills.includes(c.task?.skill||skill)?'習得済み':c.task?'訓練完了時に習得':'この学校で習得可能'}</small></p>${(c.task?.purpose||trainingPurpose(c))==='復帰訓練'?'<small>完了するとCP全回復・使用可能に復帰</small>':!c.qualified?'<small>完了すると兵士として使用可能</small>':''}</div></aside></div>
 </div>${monthlyFooter()}</section>`;
}

function updateTrainingUsed() { state.trainingUsed=[0,1,2,3].map(i=>state.characters.filter(c=>c.task?.school===i).length); }
function placeTraining(id,school,slot) {
 const c=state.characters[id]; if(!c || onExpedition(c) || school<0 || school>2 || slot<0 || slot>=trainingLimit(school))return;
 if(c.qualified && c.cp>0 && c.skills.includes(trainingSkill(school)) && c.task?.school!==school){toast('この学校のスキルは習得済みです');return;}
 const target=state.characters.find(ch=>ch.task?.school===school && ch.task.slot===slot); if(target===c)return;
 const old=c.task;
 if(target)target.task=old ? {school:old.school,slot:old.slot,purpose:trainingPurpose(target),skill:trainingSkill(old.school)} : null;
 c.task={school,slot,purpose:trainingPurpose(c),skill:trainingSkill(school)};characterChoice=id;updateTrainingUsed();save();render();toast(`${c.name}を訓練枠${slot+1}に配置しました`);
}
function startTraining(i,slot) {
 if(i===3)return;
 const c=state.characters[characterChoice];if(c.task)return;
 if(slot===undefined){slot=0;while(state.characters.some(ch=>ch.task?.school===i && ch.task.slot===slot))slot++;}
 placeTraining(characterChoice,i,slot);
}

function advanceTraining() {
 if(state.day!==1) return;
 state.characters.forEach(c=>{if(!c.task)return;const t=c.task;if(!c.skills.includes(t.skill))c.skills.push(t.skill);if(t.purpose==='復帰訓練')c.cp=c.maxCP;c.qualified=true;c.task=null;const text=`${c.name}の${t.purpose}が完了`;state.completed.push(text);log(text);});state.trainingUsed=[0,0,0,0];
}

const weapons = [
  {name:'鉄の剣',bonus:{STR:8,AGI:2},type:'剣',power:24,count:8,description:'扱いやすい標準的な片手剣。'},
  {name:'鋼の槍',bonus:{HP:10,STR:10,AGI:-2},type:'槍',power:30,count:5,description:'長い間合いで戦うための槍。'},
  {name:'戦斧',bonus:{STR:16,AGI:-6},type:'斧',power:36,count:3,description:'一撃の威力を重視した斧。'},
  {name:'長弓',bonus:{STR:4,AGI:8},type:'弓',power:22,count:6,description:'離れた相手を狙う弓。'},
  {name:'魔術の杖',bonus:{STR:-2,INT:12,FAI:8},type:'杖',power:28,count:4,description:'魔術の使用を助ける杖。'}
];
function soldierTabs() {
  return `<div class="window-title budget-tab-header"><div class="budget-tabs" role="tablist" aria-label="兵士と武器">${[['members','兵士一覧'],['weapons','武器一覧']].map(([key,name])=>`<button role="tab" aria-selected="${soldierPage===key}" data-action="soldier-page" data-page="${key}">${name}</button>`).join('')}</div><button class="${returnToDefense?'defense-return':'close'}" data-action="navigate-kingdom" aria-label="${returnToDefense?'防衛戦へ戻る':'城下町へ戻る'}">${returnToDefense?'防衛戦へ戻る':'×'}</button></div>`;
}
function renderWeapons() {
  const w=weapons[weaponChoice];
  return `<section class="game-window budget-window soldiers-window" aria-label="武器一覧">${soldierTabs()}<div class="weapon-body"><section class="weapon-roster" aria-label="所持武器"><div class="weapon-columns"><span>武器</span><span>威力</span><span>所持数</span></div>${weapons.map((w,i)=>`<button class="weapon-row ${i===weaponChoice?'is-selected':''}" data-action="weapon-select" data-index="${i}" aria-pressed="${i===weaponChoice}"><span><b>${w.name}</b><small>${w.type}</small></span><strong>${w.power}</strong><strong>${w.count}</strong></button>`).join('')}</section><aside class="character-details" aria-label="武器の詳細"><header><h2>${w.name}</h2><small>${w.type}</small></header><p>${w.description}</p><div class="weapon-stat"><span>威力</span><b>${w.power}</b></div><div class="weapon-stat"><span>所持数</span><b>${w.count}</b></div></aside></div></section>`;
}
const expeditionDestinations = [
  {name:'北の砦',description:'丘陵に築かれた砦',duration:6,loot:[300,0,0],enemy:'8人',terrain:'丘陵・城壁',feature:'高低差が大きく、城壁がある',reward:'国庫 ＋300',days:'往復 6日'},
  {name:'東の森林',description:'街道沿いの深い森',duration:4,loot:[180,80,0],enemy:'5人',terrain:'森林',feature:'遮蔽物が多く、見通しが悪い',reward:'国庫 ＋180・木材 ＋80',days:'往復 4日'},
  {name:'南の平原',description:'見通しのよい草原',duration:2,loot:[100,0,0],enemy:'3人',terrain:'平原',feature:'遮蔽物が少ない開けた戦場',reward:'国庫 ＋100',days:'往復 2日'}
];
function defenseMembers(){return state.characters.map((c,id)=>({c,id})).filter(({c,id})=>c.defending && c.qualified && c.cp>0 && !c.task && !onExpedition(c) && weapons[state.defenseWeapons?.[id]]).map(({id})=>({id,weapon:state.defenseWeapons[id],before:state.characters[id].cp}));}
function maybeDefenseAttack(){
  if(state.defenseAttack)return;
  if((state.defenseCooldown||0)>0){state.defenseCooldown--;save();return;}
  if(Math.random()*100<(state.defenseChance??10))triggerDefenseAttack();
}
function triggerDefenseAttack(){
  if(state.defenseAttack){showDefenseAttack();return;}
  stopTime();resumeAfterModal=false;
  state.defenseAttack={enemy:3+Math.floor(Math.random()*6),date:dateText()};state.defenseCooldown=7;
  log('城下町に敵が接近。防衛戦が発生。');save();render();showDefenseAttack();
}
function showDefenseAttack(){
  const attack=state.defenseAttack;if(!attack)return;stopTime();resumeAfterModal=false;
  const members=defenseMembers(),ready=new Set(members.map(m=>m.id));
  const assigned=state.characters.map((c,id)=>({c,id})).filter(({c})=>c.defending);
  modal.classList.add('defense-dialog');
  const roster=assigned.map(({c,id})=>{const w=weapons[state.defenseWeapons?.[id]],reason=ready.has(id)?'':onExpedition(c)?'遠征中':c.task?'訓練中':!c.qualified || c.cp===0?'戦闘不能':'武器未設定';return `<div class="mission-member ${reason?'defense-unavailable':''}">${expeditionImage(id%6,c.name)}<span><b>${c.name}</b><small>${w?.name||'武器未設定'}</small>${reason?`<small class="defense-warning">${reason}</small>`:''}</span><span class="mission-cp"><small>CP</small><b>${c.cp} / ${c.maxCP}</b></span></div>`;}).join('');
  openModal('城下町に敵が接近',`<div class="defense-alert-heading"><span aria-hidden="true">⚔</span><div><small>${attack.date} · 時間停止中</small><h3>城下町を防衛せよ</h3></div></div><div class="defense-forces"><div><small>敵の人数</small><strong>${attack.enemy}<small>人</small></strong></div><div><small>防衛メンバー</small><strong>${assigned.length}<small>人</small></strong></div></div><h3 class="defense-roster-title">防衛メンバー <small>出撃可能 ${members.length}人</small></h3>${assigned.length?`<div class="mission-members" aria-label="防衛メンバー一覧">${roster}</div>`:'<p class="defense-warning">防衛メンバーが未配置。メンバーを編集して配置しておくれ。</p>'}${!members.length && assigned.length?'<p class="defense-warning">出撃できる兵士がいない。メンバーと装備を確認しておくれ。</p>':''}<p class="muted">防衛戦が終わるまで時間は停止する。</p><small class="muted">戦闘はUI確認用の仮処理。</small>`, `<button data-action="defense-organize">メンバーを編集</button><button class="primary" data-action="defense-fight" ${members.length?'':'disabled'}>戦闘</button>`);
}
function resolveDefenseAttack(){
  const attack=state.defenseAttack;if(!attack || !defenseMembers().length)return;
  const members=defenseMembers().map(m=>{const c=state.characters[m.id];c.cp=Math.max(0,c.cp-20);if(!c.cp)c.defending=false;return {...m,after:c.cp};});
  state.defenseReport={...attack,members,outcome:'防衛成功',unread:true};state.defenseAttack=null;
  log(`城下町の防衛戦：${state.defenseReport.outcome}。`);save();render();showDefenseResult();
}
function showDefenseResult(){
  const r=state.defenseReport;if(!r)return;modal.classList.add('defense-dialog');
  openModal('防衛戦の結果',`<header class="mission-result"><small>${r.date}</small><h3>${r.outcome}</h3></header><p>敵 ${r.enemy}人 · 防衛 ${r.members.length}人</p>${r.members.length?`<h3>兵士のCP変化</h3><div class="mission-members">${expeditionMemberRows(r.members,true)}</div>`:''}<p class="muted">戦闘結果とCP消費は仮処理。施設・資源の損失はなし。確認後、時間は手動で再開できる。</p>`, '<button class="primary" data-action="defense-report-read">結果を確認</button>');
}

function selectedExpedition(){return expeditionPage==='status'?state.expeditions.find(m=>m.id===expeditionChoice):null;}
function unreadExpeditionReports(){return (state.expeditionReports||[]).filter(r=>!r.read).length;}
function expeditionStage(m){return m.retreating?'撤退・帰還中':m.elapsed<Math.ceil(m.total/2)?'進軍中':m.elapsed===Math.ceil(m.total/2)?'戦闘中':'帰還中';}
function expeditionHeader(){return `<div class="window-title budget-tab-header expedition-main-header"><div class="budget-tabs" role="tablist" aria-label="遠征"><button role="tab" aria-selected="${expeditionPage==='status'}" data-action="expedition-overview">遠征状況 <small>${state.expeditions.length}部隊</small></button><button role="tab" aria-selected="${expeditionPage==='new'}" data-action="expedition-new">＋ 新しい遠征</button></div><button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div>`;}
function renderExpeditionOverview(){
  const reports=state.expeditionReports||[];
  return `<section class="game-window budget-window expedition-window expedition-overview" aria-label="全部隊の遠征状況">${expeditionHeader()}<div class="expedition-overview-body"><h2>進行中の遠征</h2>${state.expeditions.length?state.expeditions.map(m=>`<div class="expedition-status-row"><div><b>${expeditionDestinations[m.target].name}</b><small>${state.characters[m.members[0].id].name}隊 · ${m.members.length}人</small></div><div><span class="expedition-stage">${expeditionStage(m)}</span><div class="mission-track"><span style="width:${m.elapsed/m.total*100}%"></span></div><small>進軍 → 戦闘 → 帰還</small></div><div class="expedition-days"><small>帰還まで</small><strong>${m.remaining}</strong> 日</div><button data-action="expedition-detail" data-index="${m.id}">部隊を確認 →</button></div>`).join(''):'<p class="muted">遠征中の部隊はない。「＋ 新しい遠征」から部隊を編成できる。</p>'}${reports.length?`<h2 class="expedition-reports-heading">帰還報告 <small>未確認 ${unreadExpeditionReports()}件</small></h2>${reports.map((r,i)=>`<div class="expedition-status-row expedition-return-row"><div><b>${expeditionDestinations[r.target].name}</b><small>${r.date} · ${r.members.length}人</small></div><div><span class="${r.read?'muted':'expedition-unread'}">${r.read?'確認済み':'帰還報告あり'}</span><small>兵士は帰還済み</small></div><div><b>${{victory:'勝利',defeat:'敗北',retreat:'撤退'}[r.outcome]}</b></div><button data-action="expedition-report" data-report="${i}">報告を確認 →</button></div>`).join('')}`:''}</div><div class="window-bottom"><span>遠征中 ${state.expeditions.length}部隊 · ${state.expeditions.reduce((n,m)=>n+m.members.length,0)}人</span><button class="primary" data-action="expedition-new">＋ 新しい遠征</button></div></section>`;
}

function onExpedition(c) {return state.expeditions.some(e=>e.members.some(m=>state.characters[m.id]===c));}

function expeditionMemberRows(members,report=false) {
  return members.map(m=>{const c=state.characters[m.id],w=weapons[m.weapon];return `<div class="mission-member">${expeditionImage(m.id%6,c.name)}<span><b>${c.name}</b><small>${w.name}</small></span><span class="mission-cp"><small>CP</small><b>${report?`${m.before} → ${m.after}`:`${c.cp} / ${c.maxCP}`}</b>${report?`<small class="${m.after===0?'stat-down':''}">${m.after===0?'訓練で復帰が必要':m.before===m.after?'変化なし':`−${m.before-m.after}`}</small>`:''}</span></div>`;}).join('');
}

function showExpeditionMap(){
  const target=selectedExpedition()?.target??expeditionTarget;if(target===null)return;
  const d=expeditionDestinations[target],forest=target===1;
  const trees=Array.from({length:forest?36:10},(_,i)=>`<circle cx="${90+(i*127)%540}" cy="${70+(i*79)%230}" r="${forest?17:12}" fill="#526e69"/>`).join('');
  openModal(`${d.name}のマップ情報`, `<div class="mission-map"><svg viewBox="0 0 720 370" role="img" aria-label="${d.name}の仮マップ。左下に出撃地点、右上に敵陣。"><defs><pattern id="mission-grid" width="40" height="40" patternUnits="userSpaceOnUse"><path d="M40 0H0V40" fill="none" stroke="#c9d6dd" stroke-opacity=".12"/></pattern></defs><rect width="720" height="370" fill="#3e5459"/><path d="M0 260Q160 70 320 190T720 90V0H0Z" fill="#687575" opacity=".5"/>${trees}<path d="M85 300Q240 310 350 200T620 80" fill="none" stroke="#b6aa8a" stroke-width="24" opacity=".7"/>${target===0?'<path d="M480 40H670V180H480Z" fill="#7c8790" stroke="#b6bdc0" stroke-width="10"/><path d="M480 130H670" stroke="#c5cbd0" stroke-width="9"/>':''}<rect width="720" height="370" fill="url(#mission-grid)"/><circle cx="90" cy="300" r="20" fill="#aacddd" stroke="#e0edf2" stroke-width="2"/><circle cx="610" cy="85" r="20" fill="#b58d88" stroke="#e8c7be" stroke-width="2"/><g fill="#edf0eb" font-size="18" font-family="sans-serif"><text x="48" y="345">出撃地点</text><text x="570" y="50">敵陣 ${d.enemy}</text></g></svg></div><div class="mission-info"><span>${d.terrain}</span><span>${d.feature}</span></div><small class="muted">地形と配置は表示確認用の仮マップ。</small>`, '<button data-action="close">戻る</button>');
}

function confirmExpeditionDeparture(){
  const members=Object.keys(formationDraft).map(Number).filter(id=>expeditionEligible(state.characters[id]));
  if(expeditionTarget===null || !members.length || members.some(id=>!weapons[formationDraft[id]]))return;
  const d=expeditionDestinations[expeditionTarget];
  openModal('遠征の出発確認', `<div class="mission-destination"><h3>${d.name}</h3><span>${d.days} · 敵 ${d.enemy}</span></div><div class="mission-reward"><small>勝利時の報酬</small><b>${d.reward}</b></div><div class="mission-members">${expeditionMemberRows(members.map(id=>({id,weapon:formationDraft[id]})))}</div><p class="muted">出発後は帰還まで、防衛・訓練・他の遠征に配置できない。日付を進めると遠征が進行する。</p><p class="muted">戦闘結果・報酬・CP消費はUI確認用の仮処理。</p>`, '<button data-action="close">編成に戻る</button><button class="primary" data-action="expedition-depart">遠征に出発</button>');
}
function departExpedition(){
  if(expeditionTarget===null)return;
  const members=Object.keys(formationDraft).map(Number);
  if(!members.length || members.some(id=>!expeditionEligible(state.characters[id]) || !weapons[formationDraft[id]]))return;
  const d=expeditionDestinations[expeditionTarget];
  state.expeditionPlan={...formationDraft};state.expeditionTarget=expeditionTarget;
  state.expeditions.push({id:state.nextExpeditionId++,target:expeditionTarget,total:d.duration,remaining:d.duration,elapsed:0,members:members.map(id=>({id,weapon:Number(formationDraft[id]),before:state.characters[id].cp})),outcome:state.mockExpeditionOutcome==='defeat'?'defeat':'victory',departed:`${state.year}年${state.month}月${state.day}日`});
  formationDraft={};expeditionChoice=null;expeditionPage="status";
  log(`${d.name}へ${members.length}人が出発。`);save();closeModal();render();toast('遠征に出発。日付を進めると帰還まで進行します');
}
function renderExpeditionProgress(){
  const m=selectedExpedition();if(!m)return renderExpeditionOverview();const d=expeditionDestinations[m.target],stage=m.retreating?'撤退・帰還中':m.elapsed===0?'進軍中':m.elapsed<Math.ceil(m.total/2)?'進軍中':m.elapsed===Math.ceil(m.total/2)?'戦闘中':'帰還中';
  const percent=m.elapsed/m.total*100;
  return `<section class="game-window budget-window expedition-window" aria-label="遠征状況">${expeditionHeader()}<button class="expedition-back" data-action="expedition-overview">← 全部隊の状況</button><div class="mission-content"><header class="mission-status"><div><small>${m.departed} 出発</small><h2>${d.name}</h2><b>${stage}</b></div><div class="mission-remaining"><small>帰還まで</small><strong>${m.remaining}<small>日</small></strong></div></header><div class="mission-track" aria-label="遠征の進行 ${Math.round(percent)}％"><span style="width:${percent}%"></span></div><div class="mission-stages"><span>出発</span><span>戦闘</span><span>帰還</span></div><div class="mission-info"><span>敵 ${d.enemy}</span><span>${d.terrain}</span><span>${m.retreating?'報酬なし':`勝利報酬 ${d.reward}`}</span></div><h3>遠征メンバー ${m.members.length}人</h3><div class="mission-members">${expeditionMemberRows(m.members)}</div><p class="muted">${state.phase==='planning'?'上の日付の隣にある「時間を進める」で、今月の予算を確定して進行。':'上の時間操作で日付を進めると、遠征も進行する。'}</p><small class="muted">戦闘はUI確認用の仮処理。</small></div><div class="window-bottom"><button data-action="navigate-kingdom">城下町へ戻る</button><div class="actions"><button data-action="expedition-map">マップ情報</button><button data-action="expedition-retreat" ${m.retreating?'disabled':''}>${m.retreating?'撤退中':'撤退を指示'}</button></div></div></section>`;
}
function confirmExpeditionRetreat(){
  if(!selectedExpedition() || selectedExpedition().retreating)return;
  openModal('遠征から撤退', '<p>報酬を受け取らず、帰還へ切り替える。兵士は帰還まで配置できない。</p>', '<button data-action="close">遠征を続ける</button><button data-action="expedition-retreat-submit">撤退する</button>');
}
function retreatExpedition(){
  const m=selectedExpedition();if(!m || m.retreating)return;
  m.retreating=true;m.outcome='retreat';m.remaining=Math.max(1,Math.min(m.remaining,Math.ceil(m.elapsed/2)));m.total=m.elapsed+m.remaining;
  log(`${expeditionDestinations[m.target].name}から撤退を開始。`);save();closeModal();render();
}
function advanceExpedition(){
  for(const m of [...state.expeditions]) {
    m.elapsed++;m.remaining--;
    if(m.remaining>0)continue;
    const d=expeditionDestinations[m.target],loot=m.outcome==='victory'?d.loot:[0,0,0];
    const members=m.members.map((member,n)=>{const c=state.characters[member.id],loss=m.outcome==='defeat'?60+n%3*20:m.outcome==='retreat'?0:20+n%3*10;c.cp=Math.max(0,c.cp-loss);return {...member,after:c.cp};});
    state.resources=state.resources.map((v,i)=>v+loot[i]);
    const report={read:false,target:m.target,outcome:m.outcome,members,loot,date:`${state.year}年${state.month}月${state.day}日`};
    state.expeditionReports=[report,...(state.expeditionReports||[])].slice(0,12);state.expeditionUnread=true;state.expeditions=state.expeditions.filter(e=>e.id!==m.id);
    const label={victory:'勝利',defeat:'敗北',retreat:'撤退'}[m.outcome];state.completed.push(`${d.name}の遠征隊が帰還（${label}）`);log(`${d.name}から帰還。${label}、国庫＋${loot[0]}。`);toast('遠征隊が帰還。遠征状況から報告を確認できます');
  }
}
function showExpeditionReport(index=0){
  const r=state.expeditionReports?.[index];if(!r)return;expeditionReportChoice=index;
  const label={victory:'勝利',defeat:'敗北',retreat:'撤退'}[r.outcome];
  openModal('遠征の帰還報告', `<header class="mission-result"><small>${r.date} 帰還</small><h3>${expeditionDestinations[r.target].name}</h3><strong class="${r.outcome==='defeat'?'stat-down':'tax-accent'}">${label}</strong></header><div class="mission-reward"><small>獲得報酬</small><b>${r.loot.some(n=>n)?r.loot.map((n,i)=>n?`${['国庫','木材','鉱石'][i]} ＋${n}`:'').filter(Boolean).join(' · '):'なし'}</b></div><h3>兵士のCP変化</h3><div class="mission-members">${expeditionMemberRows(r.members,true)}</div><p class="muted">帰還した兵士は再配置できる。CP0の兵士は学校での訓練が必要。</p><small class="muted">戦闘結果・CP消費はUI確認用の仮処理。</small>`, '<button class="primary" data-action="expedition-report-read">報告を確認</button>');
}

function expeditionEligible(c) {return c.qualified && c.cp>0 && !c.defending && !c.task && !onExpedition(c);}
function expeditionBlockedReason(c) {return onExpedition(c)?'遠征中':c.cp===0?'CP0':c.defending?'防衛中':c.task?'訓練中':'戦闘不能';}
function expeditionImage(cell,label,kind='portrait-photo') {
  return `<span class="asset-photo ${kind}" role="img" aria-label="${escapeHTML(label)}" style="background-position:${cell%6*20}% ${cell<6?0:100}%"></span>`;
}
const abilityKeys=['HP','STR','INT','FAI','AGI'];
function expeditionAbilities(c,id,weapon='') {
  const mock=[180+id*13%121,24+id*7%37,18+id*11%43,16+id*5%41,22+id*9%39];
  return abilityKeys.map((key,n)=>(c.stats?.[key] ?? mock[n])+(weapons[weapon]?.bonus?.[key]||0));
}
function expeditionStats(c,id,weapon='') {
  const values=expeditionAbilities(c,id,weapon);
  return `<span class="expedition-stats" aria-label="能力値">${abilityKeys.map((key,n)=>`<span><small>${key}</small><strong class="${weapons[weapon]?.bonus?.[key]>0?'stat-up':''}">${values[n]}</strong></span>`).join('')}</span>`;
}
function equipmentPicker(id,context,candidate=null) {
  const c=state.characters[id],current=(context==='defense'?state.defenseWeapons?.[id]:formationDraft[id])??'';
  const selected=candidate ?? (weapons[current]?Number(current):0),w=weapons[selected];
  const before=expeditionAbilities(c,id,current),after=expeditionAbilities(c,id,selected);
  modal.classList.add('expedition-equipment');
  openModal(`${c.name}の装備`, `<div class="equipment-layout"><section class="equipment-weapons" aria-label="武器を選ぶ"><h3>武器を選ぶ</h3><div class="expedition-weapon-picker">${weapons.map((item,n)=>`<button data-action="equipment-preview" data-context="${context}" data-index="${id}" data-weapon="${n}" aria-pressed="${n===selected}">${expeditionImage(n+6,item.name,'weapon-photo')}<span><b>${item.name}</b><small>${item.type} · 威力 ${item.power}</small><small>${Number(current)===n && current!==''?'装備中':''}</small></span></button>`).join('')}</div></section><section class="equipment-comparison" aria-label="装備による能力値の変化"><header>${expeditionImage(id%6,c.name)}<span><b>${c.name}</b><small>CP ${c.cp} / ${c.maxCP}</small></span></header><div class="equipment-selected">${expeditionImage(selected+6,w.name,'weapon-photo')}<span><b>${w.name}</b><small>威力 ${w.power}</small></span></div><div class="equipment-columns"><span>能力</span><span>現在</span><span></span><span>装備後</span><span>増減</span></div>${abilityKeys.map((key,n)=>{const delta=after[n]-before[n];return `<div class="equipment-stat"><span>${key}</span><b>${before[n]}</b><span class="equipment-arrow">→</span><b class="${delta>0?'stat-up':delta<0?'stat-down':''}">${after[n]}</b><strong class="${delta>0?'stat-up':delta<0?'stat-down':''}">${delta>0?'+':''}${delta||'—'}</strong></div>`;}).join('')}<small class="equipment-current">現在の武器：${weapons[current]?.name||'未装備'}</small></section></div>`, `<button data-action="close">戻る</button><button class="primary" data-action="equipment-apply" data-context="${context}" data-index="${id}" data-weapon="${selected}">${w.name}を装備</button>`);
}
function renderExpedition() {
  if(expeditionPage==='status')return selectedExpedition()?renderExpeditionProgress():renderExpeditionOverview();
  const members=state.characters.map((c,id)=>({c,id})).sort((a,b)=>Number(!expeditionEligible(a.c))-Number(!expeditionEligible(b.c)) || a.id-b.id);
  const chosen=Object.keys(formationDraft).filter(id=>expeditionEligible(state.characters[id]));
  const unarmed=chosen.filter(id=>!weapons[formationDraft[id]]);
  const complete=expeditionTarget!==null && chosen.length>0 && !unarmed.length;
  const departureHint=expeditionTarget===null?'<span class="departure-hint">目的地を選んでおくれ</span>':!chosen.length?'<span class="departure-hint">遠征メンバーを選んでおくれ</span>':unarmed.length?`<button class="departure-hint needs-equipment" data-action="expedition-pick-weapon" data-index="${unarmed[0]}">武器未設定：${unarmed.length}人 <span>武器を選ぶ →</span></button>`:'<span class="departure-hint departure-ready">✓ 出発準備完了</span>';
  const title=`<div class="window-title budget-tab-header"><div class="budget-tabs" role="tablist" aria-label="遠征の準備">${[['destination','目的地'],['members',`メンバー ${chosen.length}人`]].map(([key,name])=>`<button role="tab" id="expedition-tab-${key}" aria-controls="expedition-panel-${key}" aria-selected="${expeditionTab===key}" data-action="expedition-tab" data-tab="${key}">${name}</button>`).join('')}</div><button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div>`;
  const body=expeditionTab==='destination' ? `<div class="expedition-destination-panel" role="tabpanel" id="expedition-panel-destination" aria-labelledby="expedition-tab-destination"><div class="destination-heading"><h2>遠征する場所を選ぶ</h2><small>敵情報・報酬・日数は表示確認用の仮データ</small></div><div class="destination-list">${expeditionDestinations.map((d,i)=>`<button data-action="expedition-target" data-index="${i}" aria-pressed="${expeditionTarget===i}"><span class="destination-scene scene-${i}" aria-hidden="true"></span><span class="destination-intel"><span class="destination-name"><b>${d.name}</b></span><span class="destination-facts"><span><small>敵戦力</small><b>${d.enemy}</b></span><span><small>マップ情報</small><b>${d.terrain}</b><small>${d.feature}</small></span><span><small>獲得報酬</small><b>${d.reward}</b><small>${d.days}</small></span></span></span><strong class="destination-select">${expeditionTarget===i?'✓ 選択中':'選択'}</strong></button>`).join('')}</div></div>` : `<div class="expedition-members-panel" role="tabpanel" id="expedition-panel-members" aria-labelledby="expedition-tab-members"><aside class="expedition-party"><header><h2>遠征メンバー <b>${chosen.length}人</b></h2><small>能力値は装備補正込み</small></header><div class="expedition-party-list" data-drop-expedition="true">${chosen.map(id=>assignedCharacter(Number(id),'expedition')).join('')}<button class="round-slot empty-slot" data-action="expedition-add" aria-label="遠征の空き枠に配置"><span class="round-avatar">＋</span><small>兵士を追加</small></button></div></aside><div class="character-area expedition-character-area"><section class="expedition-roster"><header><h2>兵士一覧 <small>30人</small></h2><span>クリックで配置 · ドラッグで移動 · ×で解除</span></header><div class="expedition-roster-list roster-grid" data-drop-expedition-roster="true">${rosterList('expedition')}</div></section><aside class="character-details" aria-label="兵士の詳細">${characterDetails(characterChoice,'expedition')}</aside></div></div>`;
  return `<section class="game-window budget-window soldiers-window expedition-window" aria-label="遠征の編成">${expeditionHeader()}${title}${body}<div class="window-bottom"><div class="expedition-summary"><span>目的地 <b>${expeditionTarget===null?'未選択':expeditionDestinations[expeditionTarget].name}</b></span><span>メンバー <b>${chosen.length}人</b></span></div>${expeditionTab==='destination'?`<div class="actions"><button data-action="expedition-map" ${expeditionTarget===null?'disabled':''}>マップ情報</button><button class="primary" data-action="expedition-tab" data-tab="members" ${expeditionTarget===null?'disabled':''}>メンバーを選ぶ →</button></div>`:`<div class="departure-actions">${departureHint}<button class="primary" data-action="expedition-save" ${!complete?'disabled':''}>出発内容を確認</button></div>`}</div></section>`;
}
function soldierState(c) { return onExpedition(c) ? '遠征中' : !c.qualified || c.cp===0 ? '戦闘不能' : c.defending ? '防衛' : '攻撃可能'; }
function defenseRow() {
  const defenders=state.characters.map((c,id)=>({c,id})).filter(({c})=>c.defending);
  return `<section class="defense-row" aria-label="防衛メンバー" data-drop-defense="true"><header><h2>防衛メンバー <small>${defenders.length}人</small></h2><span>クリックで配置 · ドラッグで移動 · ×で解除</span></header><div class="defense-member-list">${defenders.map(({id})=>assignedCharacter(id,'defense')).join('')}<button class="round-slot empty-slot" data-action="defense-pick" aria-label="防衛の空き枠に配置"><span class="round-avatar">＋</span><small>兵士を追加</small></button></div></section>`;
}
function addDefense(id) {
  const c=state.characters[id];if(!c.qualified || c.cp===0 || c.task || onExpedition(c))return;
  c.defending=true;characterChoice=id;if(state.expeditionPlan)delete state.expeditionPlan[id];save();render();
}
function unequippedDefenders() {return state.characters.map((c,id)=>({c,id})).filter(({c,id})=>c.defending && !weapons[state.defenseWeapons?.[id]]);}
function confirmSoldierExit(target) {
  pendingSoldierRoute=target;
  const members=unequippedDefenders();
  openModal('武器未設定の防衛メンバー', `<p>武器を設定していないメンバーが${members.length}人いる。このまま閉じると、防衛メンバーから外れる。</p><ul>${members.map(({c})=>`<li>${escapeHTML(c.name)}</li>`).join('')}</ul>`, '<button data-action="close">戻って武器を設定</button><button class="primary" data-action="defense-exit">外して閉じる</button>');
}
function chooseDefenseWeapon(id) {
  const c=state.characters[id];if(!c.qualified || c.cp===0 || c.task || onExpedition(c))return;
  equipmentPicker(id,'defense');
}
function applyEquipment(id,context,weapon){
  const c=state.characters[id];if(!weapons[weapon] || !c)return;
  if(context==='defense'){
    if(!c.qualified || c.cp===0 || c.task || onExpedition(c))return;
    c.defending=true;state.defenseWeapons??={};state.defenseWeapons[id]=weapon;
    if(state.expeditionPlan)delete state.expeditionPlan[id];characterChoice=id;save();
  }else{
    if(!expeditionEligible(c) || !Object.hasOwn(formationDraft,id))return;
    formationDraft[id]=String(weapon);
  }
  closeModal();render();
}
function removeDefense(id) {state.characters[id].defending=false;if(state.defenseWeapons)delete state.defenseWeapons[id];save();render();}
function renderSoldiers() {
  if(soldierPage==='weapons')return renderWeapons();
  const members = state.characters.map((c,id)=>({c,id}));
  members.sort((a,b)=>Number(a.c.cp===0 || !!a.c.task || onExpedition(a.c))-Number(b.c.cp===0 || !!b.c.task || onExpedition(b.c)) || (soldierSort==='personality' ? (a.c.personality||'').localeCompare(b.c.personality||'','ja') : 0) || a.id-b.id);
  const c=state.characters[characterChoice], status=soldierState(c);
  return `<section class="game-window budget-window soldiers-window" aria-label="兵士管理">${soldierTabs()}<div class="soldier-toolbar"><div class="soldier-legend">${['戦闘不能','防衛','攻撃可能','遠征中'].map(name=>`<span class="soldier-state state-${['戦闘不能','防衛','攻撃可能','遠征中'].indexOf(name)}">${name} <b>${state.characters.filter(c=>soldierState(c)===name).length}</b></span>`).join('')}</div><label>並べ替え <select data-soldier-sort aria-label="兵士の並べ替え">${[['roster','名簿順'],['personality','性格順']].map(([key,name])=>`<option value="${key}" ${soldierSort===key?'selected':''}>${name}</option>`).join('')}</select></label></div><div class="character-area soldier-body"><section class="candidate-panel" aria-label="兵士一覧">${defenseRow()}<div class="candidate-list roster-grid" data-drop-defense-roster="true">${rosterList('defense')}</div></section><aside class="character-details" aria-label="兵士の詳細">${characterDetails(characterChoice,'defense')}<div class="details-training"><h3>今月の訓練予定</h3>${c.task?`<b>${c.task.purpose}</b><p>${facilities[c.task.school].name} · 枠${c.task.slot+1}<br>${escapeHTML(c.task.skill)}</p>`:'<p>予定なし</p>'}</div></aside></div></section>`;
}
function renderHistory() {
  return `<section class="game-window chronicle-window" aria-label="国家記録">${windowTitle('国家記録', `<button data-action="report" ${!state.report ? 'disabled' : ''}>月次報告</button>`)}<div class="window-content"><ul class="history-list">${state.logs.map(l => `<li><time>${escapeHTML(l.date)}</time><span>${escapeHTML(l.text)}</span></li>`).join('')}</ul></div></section>`;
}
function render() {
  const populationButton = document.querySelector('.population');
  populationButton.querySelector('.resource-value').textContent = populationCount().toLocaleString();
  populationButton.setAttribute('aria-label', `人口 ${populationCount().toLocaleString()}人、詳細を表示`);
  const focus = document.activeElement;
  const focusAction = focus?.dataset.action;
  const focusIndex = focus?.dataset.index;
  const focusTab = focus?.dataset.tab;
  const focusBudget = focus?.dataset.budgetIndex;
  const focusTreasury = focus?.matches('[data-use-treasury]');
  const focusTraining = focus?.dataset.trainingCount;
  const focusTrainingType = focus?.type;
  const budgetScroll = document.querySelector('.budget-content')?.scrollTop || 0;
  const trainingScroll = document.querySelector('.training-content')?.scrollTop || 0;
  const candidateScroll = document.querySelector('.candidate-list')?.scrollTop || 0;
  const slotScroll = document.querySelector('.training-slot-list')?.scrollTop || 0;
  const constructionScroll = document.querySelector('.construction-content')?.scrollTop || 0;
  document.querySelector('.expedition-command').innerHTML=`<span aria-hidden="true">⚑</span>遠征 ${state.expeditions.length?`<b>${state.expeditions.length}部隊</b>`:''} ${unreadExpeditionReports()?`<small class="expedition-unread">報告 ${unreadExpeditionReports()}</small>`:''}`;
  const dateState = state.defenseAttack ? ['paused','襲撃・停止中'] : state.phase === 'planning'
    ? ['budget-pending', '予算未決定']
    : timer ? ['running', '時間進行中'] : ['paused', '時間停止中'];
  document.querySelector('#date').innerHTML = `<b>${dateText()}</b><small class="date-state date-state--${dateState[0]}">${dateState[1]}</small>`;
  const plannedResources = totalCost();
  document.querySelector('#resources').innerHTML = `<div class="resource resource--funds" aria-label="税収 ${taxIncome()}、月の収入" title="税収：月の収入"><span class="resource-symbol">${resourceIcons[0]}</span><span class="resource-name">税収</span><strong class="resource-value">${taxIncome().toLocaleString()}</strong></div>` + resourceNames.map((name, i) => {
    const amount = state.resources[i];
    const delta = previousResourceValues ? amount - previousResourceValues[i] : 0;
    const short = state.phase === 'planning' && countPlan() > 0 && plannedResources[i] > amount;
    return `<button type="button" class="resource resource--${resourceKinds[i]}${short ? ' is-short' : ''}${delta ? ' is-changed' : ''}" data-action="resource-detail" data-index="${i}" aria-label="${i === 0 ? '国庫' : name} ${amount.toLocaleString()}、内訳を表示">
      <span class="resource-topline"><span class="resource-symbol">${resourceIcons[i]}</span><span class="resource-name">${i === 0 ? '国庫' : name}</span></span>
      <strong class="resource-value">${amount.toLocaleString()}</strong>
      ${delta ? `<span class="resource-delta${delta < 0 ? ' is-loss' : ''}" aria-hidden="true">${delta > 0 ? '+' : '−'}${Math.abs(delta).toLocaleString()}</span>` : ''}
    </button>`;
  }).join('');
  previousResourceValues = [...state.resources];
  if (route() === 'history' && state.recordsUnread) { state.recordsUnread = false; save(); }
  const recordsButton = document.querySelector('.realm');
  recordsButton.querySelector('.record-notice').hidden = !state.recordsUnread;
  recordsButton.setAttribute('aria-label', state.recordsUnread ? '国家記録を開く・新しい記録あり' : '国家記録を開く');
  recordsButton.title = state.recordsUnread ? '国家記録・新しい記録あり' : '国家記録';
  document.querySelectorAll('[data-route]').forEach(button => {
    const active = button.dataset.route === route();
    button.classList.toggle('active', active);
    button.setAttribute('aria-pressed', active);
  });
  document.querySelector('#view').innerHTML = ({ kingdom: renderKingdom, facilities: renderFacilities, plan: renderPlan, history: renderHistory, training: renderTraining, soldiers: renderSoldiers, expedition: renderExpedition })[route()]();
  document.querySelector('#time-controls').innerHTML = state.defenseAttack ? '<button class="primary" data-action="defense-notice">⚔ 防衛を確認</button>' : state.phase === 'planning'
    ? `<button class="primary" data-action="start-month" aria-label="現在の予算で時間を進める" title="現在の予算・育成設定を確定して今月を開始">▶ 時間を進める</button>`
    : `<button class="time-auto" aria-label="${timer ? '自動進行を一時停止' : '自動進行を開始'}" title="${timer ? '自動進行を停止' : '自動進行を開始'}" data-action="toggle-time"><span aria-hidden="true">${timer ? 'Ⅱ' : '▶'}</span><small>${timer ? '停止' : '進行'}</small></button><button aria-label="1日進める" title="1日進める" data-action="day">1日</button><button class="primary" aria-label="${hasWork() ? '次の工事完了・月末まで進む' : '月次報告まで進む'}" data-action="${hasWork() ? 'next-event' : 'month'}">${hasWork() ? '次の完了' : '月次報告'}</button>`;
  const defenseNotice=document.querySelector('.defense-notice');
  defenseNotice.hidden=!state.defenseAttack && !state.defenseReport?.unread;
  defenseNotice.innerHTML=state.defenseAttack?'⚔ 襲撃発生 <small>防衛を確認</small>':'⚔ 防衛戦 <small>結果を確認</small>';
  const budgetContent = document.querySelector('.budget-content');
  if (budgetContent) budgetContent.scrollTop = budgetScroll;
  const constructionContent = document.querySelector('.construction-content');
  if (constructionContent) constructionContent.scrollTop = constructionScroll;
  const trainingContent = document.querySelector('.training-content');
  if (trainingContent) trainingContent.scrollTop = trainingScroll;
  const candidates=document.querySelector('.candidate-list'); if(candidates)candidates.scrollTop=candidateScroll;
  const slots=document.querySelector('.training-slot-list'); if(slots)slots.scrollTop=slotScroll;
  if (!modal.open && focusBudget !== undefined) document.querySelector(`[data-budget-index="${focusBudget}"]`)?.focus({ preventScroll: true });
  if (!modal.open && focusTraining !== undefined) document.querySelector(`[data-training-count="${focusTraining}"][type="${focusTrainingType}"]`)?.focus({ preventScroll:true });
  if (!modal.open && focusTreasury) document.querySelector('[data-use-treasury]')?.focus({ preventScroll: true });
  if (focusAction && !modal.open) {
    const replacement = [...document.querySelectorAll('[data-action]')].find(el => el.dataset.action === focusAction && el.dataset.index === focusIndex && el.dataset.tab === focusTab && !el.disabled);
    replacement?.focus({ preventScroll: true });
  }
}
function stopTime() { clearInterval(timer); timer = null; }
function startTime() { if(state.defenseAttack){showDefenseAttack();return;} if (!timer && state.phase === 'running') timer = setInterval(advanceDay, 2000); render(); }
function advanceDay() {
  if (state.phase !== 'running' || state.defenseAttack) return;
  state.day++;
  const newMonth = state.day > daysInMonth(state.month);
  if (newMonth) { state.day = 1; state.month++; if (state.month > 12) { state.month = 1; state.year++; } }
  advanceExpedition();
  state.queues.forEach((q, i) => { if (!q.length) return; q[0].remaining--; if (!q[0].remaining) { const task = q.shift(); state.levels[i] = task.level; const text = task.level === 1 ? `${facilities[i].name}の新規建設が完了` : `${facilities[i].name}がLv.${task.level}に到達`; state.completed.push(text); log(`${text}。`); } });
  const previousTraining=newMonth?state.characters.map(c=>c.task?{...c.task}:null):null;
  advanceTraining();
  if (newMonth) {
    updatePopulation();
    if(state.monthBudget){state.budgetAmounts=[...state.monthBudget.budgets];state.budgetShare=[...state.monthBudget.shares];}
    state.characters.forEach((c,id)=>{const t=previousTraining[id];if(t && t.school<3 && t.slot<trainingLimit(t.school))c.task={...t,purpose:trainingPurpose(c),skill:trainingSkill(t.school)};});updateTrainingUsed();
    stopTime(); state.phase = 'planning'; state.resources = state.resources.map((n, i) => n + (i ? monthlyIncome[i] : 0)); state.boost = facilities.map(() => 0);
    state.report = { finance: state.monthBudget ? { ...state.monthBudget, building: state.monthBuilding || 0, closing: state.resources[0] } : null, title: `${state.year}年 ${state.month}月の月次報告`, completed: [...state.completed] }; state.completed = []; state.monthBudget = null; state.monthBuilding = 0; state.monthOpening = state.resources[0];
    log(`新しい月の税収${taxIncome()}を配分できる。木材50、鉱石30を受け取り。`);
  }
  save(); render(); if (newMonth) showReport(); else maybeDefenseAttack();
}
function openModal(title, body, actions = '') {
  if (!modal.open) { modalOpener = document.activeElement; resumeAfterModal = Boolean(timer); stopTime(); render(); }
  document.querySelector('#modal-body').innerHTML = `<div class="modal-heading"><h2 id="modal-title">${title}</h2><button class="close" data-action="close" aria-label="閉じる">×</button></div>${resumeAfterModal ? '<p class="muted">表示中は時間を一時停止しています。閉じると再開します。</p>' : ''}${body}<div class="actions end" style="margin-top:24px">${actions}</div>`;
  if (!modal.open) modal.showModal();
}
function closeModal() { modal.close(); modal.classList.remove('resource-detail', 'monthly-report', 'expedition-equipment', 'defense-dialog', 'member-selection', 'skill-dialog'); if (resumeAfterModal) startTime(); resumeAfterModal = false; if (modalOpener?.isConnected) modalOpener.focus(); else if (modalOpener?.dataset.action === 'resource-detail') document.querySelector(`[data-action="resource-detail"][data-index="${modalOpener.dataset.index}"]`)?.focus({ preventScroll: true }); else document.querySelector('#view').focus({ preventScroll: true }); }
function showResourceDetails(i) {
  const income = monthlyIncome[i];
  const planned = state.phase === 'planning' ? totalCost()[i] : 0;
  const balance = state.resources[i] - planned;
  const expenseText = state.phase !== 'planning' ? '支払済み' : planned ? `−${planned.toLocaleString()}` : 'なし';
  const body = `<div class="resource-ledger resource--${resourceKinds[i]}"><span class="resource-symbol">${resourceIcons[i]}</span><span class="resource-ledger-copy"><small>現在の所持</small><strong>${state.resources[i].toLocaleString()}</strong></span></div>
    <div class="resource-ledger-lines">
      <div class="resource-ledger-row"><span>今月の予定支出</span><strong class="${planned ? 'resource-ledger-loss' : ''}">${expenseText}</strong></div>
      <div class="resource-ledger-row"><span>次回の月初収入</span><strong class="resource-ledger-gain">+${income.toLocaleString()}</strong></div>
      <div class="resource-ledger-row"><span>収支反映後の見込み</span><strong>${(balance + income).toLocaleString()}</strong></div>
    </div>`;
  modal.classList.add('resource-detail');
  openModal(`${resourceNames[i]}の内訳`, body, '<button data-action="close">閉じる</button>');
}
function showDetail(i) { if(i===3)return; const f = facilities[i]; const next = targetLevel(i); openModal(f.name, `<div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><span class="level">Lv.${state.levels[i]}</span></div><p>${f.description}</p><div class="report"><div class="report-row"><span>次に追加する強化</span><b>Lv.${next - 1} → Lv.${next}</b></div>${i < 4 ? `<div class="report-row"><span>育成枠（仮）</span><b>${(next - 1) * 5} → ${next * 5}人 / 月</b></div>` : `<div class="report-row"><span>用途</span><b>${f.role}</b></div>`}<div class="report-row"><span>工期</span><b>5日${state.queues[i].length + state.plan[i] ? '（先行工事の完了後）' : ''}</b></div></div>${costsHTML(cost(next))}<div class="separator"></div>${queueHTML(i)}${state.queues[i].length ? `<p class="muted">予約順：${state.queues[i].map(t => `Lv.${t.level}（${t.remaining}日）`).join(' → ')}</p>` : ''}${state.plan[i] ? `<p class="success">未決定の計画：${state.plan[i]}段階</p>` : ''}`, `<button data-action="close">戻る</button><button data-action="build" data-index="${i}" ${cost(next).some((n,j)=>n>state.resources[j]) ? 'disabled' : ''}>国庫で建物を強化</button>`); }
function commit() {
  if(state.defenseAttack){showDefenseAttack();return;}
  const b = budgetValues();
  if (state.phase !== 'planning' || !b.feasible || b.allocation > b.pool || b.withdrawal > state.resources[0]) return;
  state.monthBudget = { year: state.year, month: state.month, opening: state.monthOpening ?? state.resources[0], tax: b.tax, treasury: b.treasury, pool: b.pool, allocation: b.allocation, withdrawal: b.withdrawal, deposit: b.deposit, shares: [...state.budgetShare], budgets: [...state.budget] };
  state.resources[0] += b.deposit - b.withdrawal;
  log(`今月の予算を確定：建物へ ${b.allocation}、国庫から ${b.withdrawal}、税収から積立 ${b.deposit}。`);
  state.phase = 'running'; save(); location.hash = 'kingdom'; startTime(); toast('予算を確定。時間を自動で進めています');
}
function showReport() {
  if (!state.report) return;
  const f = state.report.finance;
  const money = n => n.toLocaleString();
  const change = f ? f.closing - f.opening : 0;
  modal.classList.add('monthly-report');
  const body = f ? `<div class="month-result"><span>${f.year}年${f.month}月の運営結果</span><div><small>国庫の変化</small><strong class="${change >= 0 ? 'tax-accent' : 'reserve-accent'}">${change >= 0 ? '＋' : '−'}${money(Math.abs(change))}</strong></div><p>月初 ${money(f.opening)} <span>→</span> 月末 <b>${money(f.closing)}</b></p></div>
    ${f.pool !== undefined ? `<div class="month-ledgers"><section><h3>今月の予算配分</h3><div class="month-line"><span>税収</span><b>${money(f.tax)}</b></div><div class="month-line"><span>建物への配分</span><b>${money(f.allocation)}</b></div><div class="month-line tax-accent"><span>税収から積立</span><b>＋${money(f.deposit)}</b></div></section><section><h3>国庫からの支出</h3><div class="month-line"><span>予算への配分</span><b>${money(f.withdrawal)}</b></div><div class="month-line"><span>建築・強化</span><b>${money(f.building)}</b></div></section></div><section class="month-buildings unified-month"><h3>建物への配分記録</h3><div class="month-building-head"><span>建物</span><span>配分率</span><span>配分額</span></div>${facilities.map((building,i)=> i!==3 && f.shares[i] ? `<div class="month-building-row"><span>${building.name}</span><b class="tax-accent">${percent(f.shares[i])}％</b><span>${money(f.budgets[i])}</span></div>` : '').join('')}</section>` : `    <div class="month-ledgers"><section><h3>税収の使い道</h3><div class="month-line month-total"><span>税収</span><b>${money(f.tax)}</b></div><div class="month-line"><span>建物の運営費</span><b>${money(f.maintenance - f.shortfall)}</b></div><div class="month-line"><span>建物への追加予算</span><b>${money(f.allocation)}</b></div><div class="month-line tax-accent"><span>国庫へ積立</span><b>＋${money(f.deposit)}</b></div></section><section><h3>国庫からの支出</h3><div class="month-line"><span>建築・強化</span><b>${money(f.building)}</b></div><div class="month-line reserve-accent"><span>運営ブースト</span><b>${money(f.boost)}</b></div><div class="month-line"><span>維持費の不足を補填</span><b>${money(f.shortfall)}</b></div></section></div>
    <section class="month-buildings"><h3>建物への配分記録</h3><div class="month-building-head"><span>建物</span><span>配分率</span><span>配分額</span><span>ブースト</span></div>${facilities.map((building,i)=> i!==3 && f.shares[i] ? `<div class="month-building-row"><span>${building.name}</span><b class="tax-accent">${percent(f.shares[i])}％</b><span>${money(f.budgets[i])}</span><span class="reserve-accent">${money(f.boosts[i])}</span></div>` : '').join('')}</section>`}` : '<p class="muted">この月の収支記録はありません。新しい予算を確定した月から収支を記録します。</p>';
  openModal('月次報告', `${body}<div class="month-completed"><h3>完了した活動</h3>${state.report.completed.length ? state.report.completed.map(t=>`<p>${escapeHTML(t)}</p>`).join('') : '<p class="muted">今月の完成工事はありません。</p>'}</div>`, '<button data-action="close">城下町へ戻る</button><button class="primary" data-action="report-plan">次月の予算を配分</button>');
}
function showSettings() {
  openModal('設定・保存', `<p class="muted">記録は自動保存。手動保存は独立した1枠に残る。</p><div class="actions"><button data-action="save">手動保存する</button><button data-action="load">手動保存を読込</button></div><div class="separator"></div><label class="mock-expedition-setting">次に出発する遠征の結果（UI確認用） <select data-expedition-outcome aria-label="遠征結果のプレビュー"><option value="victory" ${state.mockExpeditionOutcome!=='defeat'?'selected':''}>勝利</option><option value="defeat" ${state.mockExpeditionOutcome==='defeat'?'selected':''}>敗北</option></select></label><div class="separator"></div><h3>防衛戦（UI確認用）</h3><label class="mock-expedition-setting">日ごとの襲撃発生率 <select data-defense-chance aria-label="襲撃発生率">${[0,5,10,25,100].map(n=>`<option value="${n}" ${(state.defenseChance??10)===n?'selected':''}>${n===0?'発生しない':`${n}%`}</option>`).join('')}</select></label><p class="muted">頻度は仮設定。襲撃後は7日間発生しない。</p><button data-action="defense-preview">今すぐ襲撃を発生させる</button><div class="separator"></div><h3>人口・税収・資源を直接変更</h3><p class="muted">税収は変更後の数値を毎月使う。</p><div class="resource-editor">${['人口','税収','国庫','木材','鉱石'].map((name,i)=>`<label><span>${name}</span><input type="number" min="0" step="1" max="999999999" data-resource-edit="${i}" aria-label="${name}の数値" value="${[populationCount(),taxIncome(),...state.resources][i]}"></label>`).join('')}</div><p id="resource-edit-error" class="stat-down" role="alert"></p><div class="actions end"><button data-action="resource-edit-save">資源の変更を反映</button></div>`, '<button class="danger" data-action="reset-confirm">最初からやり直す</button><button data-action="close">閉じる</button>');
}
function applyResourceEdits(){
  const values=[...document.querySelectorAll('[data-resource-edit]')].map(el=>el.value.trim()===''?NaN:Number(el.value));
  if(values.length!==5 || values.some(n=>!Number.isSafeInteger(n)||n<0||n>999999999)){document.querySelector('#resource-edit-error').textContent='0以上の整数を入力してください';return;}
  state.population=values[0];state.taxOverride=values[1];state.resources=values.slice(2);save();render();showSettings();toast('人口・税収・資源の数値を変更しました');
}
function memberPicker(mode,title,action,extra='',filter=()=>true){
  const ids=state.characters.map((c,id)=>id).filter(id=>filter(state.characters[id],id)).sort((a,b)=>Number(!!rosterReason(a,mode))-Number(!!rosterReason(b,mode)) || Number(state.characters[a].cp===0)-Number(state.characters[b].cp===0) || a-b);
  openModal(title,`<div class="member-pick-list roster-grid">${ids.map(id=>rosterButton(id,mode,action,extra)).join('')||'<p>配置できる兵士はいない。</p>'}</div>`,'<button data-action="close">戻る</button>');
  modal.classList.add('member-selection');
}
function pickTrainingSlot(school,slot){memberPicker('training',`訓練枠${slot+1}に配置`,'training-place',`data-school="${school}" data-slot="${slot}"`,(c)=>!c.task);}
function pickDefenseMember(){memberPicker('defense','防衛メンバーに配置','defense-add','',c=>!c.defending);}
function pickExpeditionMember(){memberPicker('expedition','遠征メンバーに配置','expedition-place','',(c,id)=>!Object.hasOwn(formationDraft,id));}
function confirmUpgrade(i){
  if(!facilities[i]||!state.levels[i])return;
  const level=state.levels[i]+state.queues[i].length+1,price=cost(level);
  openModal(`${facilities[i].name}を強化`, `<div class="construction-confirm-line"><span>施設レベル</span><b>Lv.${level-1} → Lv.${level}</b></div>${upgradeEffects(i,level)}${constructionPrices(price)}<div class="construction-confirm-line"><span>工期</span><b>5日</b></div><p class="muted">${state.queues[i].length?'先行工事が終わってから着工。':'確認後に着工。'}費用を支払い、工事状況へ追加する。</p>`, `<button data-action="close">キャンセル</button><button class="primary" data-action="build-submit" data-index="${i}" ${price.some((n,j)=>n>state.resources[j])?'disabled':''}>強化を確定</button>`);
}
function confirmCancelBuilding(i,position){
  const jobs=state.queues[i]?.slice(position);if(!jobs?.length)return;
  const refund=jobs.reduce((sum,t)=>(t.price||cost(t.level)).map((n,j)=>sum[j]+n),[0,0,0]);
  openModal('工事をキャンセル', `<p>${facilities[i].name}のLv.${jobs[0].level}${jobs.length>1?'以降の予約も含めて':''}の工事を取り消す。</p>${constructionPrices(refund)}<p class="muted">上の資源を全額返却する。</p>`, `<button data-action="close">工事を続ける</button><button data-action="cancel-build-submit" data-index="${i}" data-position="${position}">工事を取り消す</button>`);
}
function cancelBuilding(i,position){
  const jobs=state.queues[i]?.splice(position);if(!jobs?.length)return;
  jobs.forEach(t=>{const price=t.price||cost(t.level);state.resources=state.resources.map((n,j)=>n+price[j]);if(!t.paidYear || (t.paidYear===state.year&&t.paidMonth===state.month))state.monthBuilding=Math.max(0,(state.monthBuilding||0)-price[0]);});
  log(`${facilities[i].name}の工事を取り消し、資源を返却。`);save();closeModal();render();
}
document.addEventListener('dragstart',event=>{
 const source=event.target.closest('[data-drag-character]');if(!source)return;
 draggedCharacter=Number(source.dataset.dragCharacter);event.dataTransfer.setData('text/plain',String(draggedCharacter));event.dataTransfer.effectAllowed='move';
 const avatar=source.querySelector('.round-avatar');const size=avatar.getBoundingClientRect();event.dataTransfer.setDragImage(avatar,size.width/2,size.height/2);source.classList.add('dragging');document.body.classList.add('training-dragging');
});
document.addEventListener('dragover',event=>{
 if(draggedCharacter===null)return;const target=event.target.closest('[data-drop-slot],[data-drop-roster],[data-drop-defense],[data-drop-defense-roster],[data-drop-expedition],[data-drop-expedition-roster]');if(!target)return;
 event.preventDefault();event.dataTransfer.dropEffect='move';document.querySelectorAll('.drop-hover').forEach(el=>el.classList.remove('drop-hover'));target.classList.add('drop-hover');
});
document.addEventListener('drop',event=>{
 if(draggedCharacter===null)return;const target=event.target.closest('[data-drop-slot],[data-drop-roster],[data-drop-defense],[data-drop-defense-roster],[data-drop-expedition],[data-drop-expedition-roster]');if(!target)return;event.preventDefault();
 const id=draggedCharacter;draggedCharacter=null;document.body.classList.remove('training-dragging');
 if(target.hasAttribute('data-drop-expedition')){placeRoster(id,'expedition');}
 else if(target.hasAttribute('data-drop-expedition-roster')){delete formationDraft[id];render();}
 else if(target.hasAttribute('data-drop-defense')){addDefense(id);}
 else if(target.hasAttribute('data-drop-defense-roster')){if(state.characters[id].defending)removeDefense(id);}
 else if(target.hasAttribute('data-drop-roster')){state.characters[id].task=null;updateTrainingUsed();save();render();toast('訓練枠から外しました');}
 else placeTraining(id,Number(target.dataset.dropSchool),Number(target.dataset.dropSlot));
 document.querySelectorAll('.drop-hover,.dragging').forEach(el=>el.classList.remove('drop-hover','dragging'));
});
document.addEventListener('dragend',()=>{draggedCharacter=null;document.body.classList.remove('training-dragging');document.querySelectorAll('.drop-hover,.dragging').forEach(el=>el.classList.remove('drop-hover','dragging'));});
document.addEventListener('change',event=>{if(event.target.matches('[data-defense-chance]')){state.defenseChance=Number(event.target.value);save();}if(event.target.matches('[data-expedition-outcome]')){state.mockExpeditionOutcome=event.target.value;save();}});
document.addEventListener('click', event => {
  if (modal.open && event.target === modal && event.clientY <= document.querySelector('.hud').getBoundingClientRect().bottom) { closeModal(); return; }
  if (!modal.open && document.querySelector('.game-window') && !event.target.closest('.game-window,.hud button[data-action],.defense-notice')) { location.hash = 'kingdom'; return; }
  const button = event.target.closest('button[data-action]'); if (!button || button.disabled) return;
  const action = button.dataset.action; const i = Number(button.dataset.index);
  if(action==='character-detail'){characterChoice=i;render();return;}
  if(action==='roster-place'){placeRoster(i,button.dataset.mode);return;}
  if(action==='roster-remove'){delete formationDraft[i];render();return;}
  if (action === 'soldier-page') { soldierPage=button.dataset.page;render();return; }
  if (action === 'weapon-select') {weaponChoice=i;render();return;}
  if (action === 'defense-add') {if(modal.open)closeModal();addDefense(i);return;}
  if (action === 'defense-exit') {unequippedDefenders().forEach(({id})=>{state.characters[id].defending=false;if(state.defenseWeapons)delete state.defenseWeapons[id];});save();closeModal();location.hash=pendingSoldierRoute;return;}
  if (action === 'defense-weapon') {chooseDefenseWeapon(i);return;}
  if (action === 'defense-remove') {removeDefense(i);return;}

  if(action==='expedition-tab'){expeditionTab=button.dataset.tab;render();return;}
  if(action==='expedition-pick-weapon'){equipmentPicker(i,'expedition');return;}
  if(action==='equipment-preview'){equipmentPicker(i,button.dataset.context,Number(button.dataset.weapon));return;}
  if(action==='equipment-apply'){applyEquipment(i,button.dataset.context,Number(button.dataset.weapon));return;}
  if(action==='navigate-expedition'){expeditionPage='status';expeditionChoice=null;location.hash='expedition';render();return;}
  if(action==='expedition-overview'){expeditionPage='status';expeditionChoice=null;render();return;}
  if(action==='expedition-new'){expeditionPage='new';expeditionChoice=null;expeditionTab='destination';formationDraft=Object.fromEntries(Object.entries(formationDraft).filter(([id])=>expeditionEligible(state.characters[id])));render();return;}
  if(action==='expedition-detail'){expeditionPage='status';expeditionChoice=i;render();return;}
  if(action==='defense-preview'){resumeAfterModal=false;closeModal();triggerDefenseAttack();return;}
  if(action==='defense-notice'){state.defenseAttack?showDefenseAttack():showDefenseResult();return;}
  if(action==='defense-organize'){closeModal();returnToDefense=true;soldierPage='members';location.hash='soldiers';render();return;}
  if(action==='character-skills'){showCharacterSkills(i);return;}
  if(action==='skill-info'){showSkillInfo(button.dataset.skill,button.dataset.backCharacter);return;}
  if(action==='defense-fight'){resolveDefenseAttack();return;}
  if(action==='defense-report-read'){if(state.defenseReport)state.defenseReport.unread=false;save();closeModal();render();return;}
  if(action==='expedition-map'){showExpeditionMap();return;}
  if(action==='expedition-depart'){departExpedition();return;}
  if(action==='expedition-retreat'){confirmExpeditionRetreat();return;}
  if(action==='expedition-retreat-submit'){retreatExpedition();return;}
  if(action==='expedition-report'){showExpeditionReport(Number(button.dataset.report||0));return;}
  if(action==='expedition-report-read'){if(state.expeditionReports?.[expeditionReportChoice])state.expeditionReports[expeditionReportChoice].read=true;state.expeditionUnread=unreadExpeditionReports()>0;save();closeModal();render();return;}
  if(action==='expedition-target'){expeditionTarget=i;render();return;}
  if(action==='expedition-toggle'){if(!expeditionEligible(state.characters[i]))return;if(Object.hasOwn(formationDraft,i))delete formationDraft[i];else formationDraft[i]='';render();return;}
  if(action==='expedition-save'){
    const ids=Object.keys(formationDraft);if(expeditionTarget===null || !ids.length || ids.some(id=>!expeditionEligible(state.characters[id]) || formationDraft[id]===''))return;
    confirmExpeditionDeparture();return;
  }

  if (action === 'training-open') { trainingChoice = i; budgetTabChoice='training'; closeModal(); location.hash = 'plan'; render(); return; }
  if (action === 'training-select') { trainingChoice = i; render(); return; }
  if (action === 'training-start') { startTraining(i,Number(button.dataset.dropSlot)); return; }
  if (action === 'character-select') {
    characterChoice=i;
    if(route()==='plan' && budgetTabChoice==='training' && !state.characters[i].task){startTraining(trainingChoice);return;}
    if(route()==='soldiers' && !state.characters[i].defending && state.characters[i].cp>0 && !state.characters[i].task){addDefense(i);return;}
    render();return;
  }
  if(action==='training-slot-pick'){pickTrainingSlot(i,Number(button.dataset.dropSlot));return;}
  if(action==='training-place'){closeModal();placeTraining(i,Number(button.dataset.school),Number(button.dataset.slot));return;}
  if(action==='defense-pick'){pickDefenseMember();return;}
  if(action==='expedition-add'){pickExpeditionMember();return;}
  if(action==='expedition-place'){if(expeditionEligible(state.characters[i]))formationDraft[i]='';closeModal();render();return;}
  if (action === 'character-cancel') { const c=state.characters[i];if(c.task){state.trainingUsed[c.task.school]--;c.task=null;save();render();}return; }

  if (action === 'construction-select') { constructionChoice = i; render(); return; }
  if (action === 'construction-work') { document.querySelector('.construction-queues')?.scrollIntoView({ block: 'start', behavior: 'smooth' }); return; }
  if (action === 'new-build-confirm') { confirmNewBuilding(i); return; }
  if (action === 'new-build') {
    if (!facilities[i] || i < 4 || state.levels[i] || state.queues[i].length || cost(1).some((n,j)=>n>state.resources[j])) return;
    state.monthOpening ??= state.resources[0]; state.monthBuilding = (state.monthBuilding || 0) + cost(1)[0];
    state.resources = state.resources.map((n,j)=>n-cost(1)[j]); state.queues[i].push({level:1,remaining:5,price:cost(1),paidYear:state.year,paidMonth:state.month});
    log(`${facilities[i].name}の新規建設を発注。`); closeModal(); save(); render();
    document.querySelector('.construction-queues')?.scrollIntoView({ block:'start' }); toast(`${facilities[i].name}を着工。工事状況に追加しました`); return;
  }
  if (action === 'budget-tab') { budgetTabChoice = button.dataset.tab; render(); document.querySelector(`[role="tab"][data-tab="${budgetTabChoice}"]`)?.focus({ preventScroll: true }); return; }
  if (action === 'navigate-budget') { budgetTabChoice = 'budget'; closeModal(); location.hash = 'plan'; render(); const target = document.querySelector(`[data-budget-index="${i}"]`); target?.scrollIntoView({ block: 'nearest' }); target?.focus({ preventScroll: true }); return; }
  if (action === 'navigate-training') {budgetTabChoice='training';location.hash='plan';render();return;}
  if (action === 'navigate-plan') budgetTabChoice = 'budget';
  if (action.startsWith('navigate-')) { location.hash = action.replace('navigate-', ''); return; }
  if (action === 'population-detail') showPopulation();
  if (action === 'resource-detail' && Number.isInteger(i) && resourceNames[i]) showResourceDetails(i);
  if (action === 'build') {confirmUpgrade(i);return;}
  if(action==='cancel-build'){confirmCancelBuilding(i,Number(button.dataset.position));return;}
  if(action==='cancel-build-submit'){cancelBuilding(i,Number(button.dataset.position));return;}
  if (action === 'build-submit') { if (!facilities[i] || !state.levels[i]) return; const next = state.levels[i] + state.queues[i].length + 1; const price = cost(next); if (price.every((n,j)=>n<=state.resources[j])) { state.monthOpening ??= state.resources[0]; state.monthBuilding = (state.monthBuilding || 0) + price[0]; state.resources = state.resources.map((n,j)=>n-price[j]); state.queues[i].push({level:next,remaining:5,price:[...price],paidYear:state.year,paidMonth:state.month}); log(`${facilities[i].name}の強化を国庫で発注。資金 ${price[0]}。`); closeModal(); save(); render(); } return; }
  if (action === 'detail') showDetail(i);
  if (['add', 'remove'].includes(action) && state.phase === 'planning') { state.plan[i] = Math.max(0, state.plan[i] + (action === 'remove' ? -1 : 1)); save(); render(); if (action === 'add') toast(`${facilities[i].name}の強化を追加しました`); }
  if (action === 'clear-plan') { state.plan = facilities.map(() => 0); save(); render(); toast('未決定の計画をクリアしました'); }
  if (action === 'close') closeModal();

  if (/^budget-(up|down)$/.test(action) && state.phase === 'planning') { budgetValues(); changeBudget(i, state.budgetShare[i] + (action.endsWith('up') ? 1 : -1)); return; }
  if (action === 'commit' || action === 'start-month') commit();
  if (action === 'toggle-time') { if (timer) { stopTime(); render(); } else startTime(); }
  if (action === 'day') { stopTime(); advanceDay(); }
  if (action === 'next-event') { stopTime(); const days = nextEventDays(); for (let n = 0; n < days && state.phase === 'running' && !state.defenseAttack; n++) advanceDay(); }
  if (action === 'month') { stopTime(); for (let n = 0; n < 31 && state.phase === 'running' && !state.defenseAttack; n++) advanceDay(); }
  if (action === 'report') showReport();
  if (action === 'report-plan') { budgetTabChoice = 'budget'; closeModal(); location.hash = 'plan'; }
  if(action==='resource-edit-save'){applyResourceEdits();return;}
  if (action === 'settings') showSettings();
  if (action === 'save') save(true);
  if (action === 'load') { try { const saved = JSON.parse(localStorage.getItem(manualSaveKey)); if (!validState(saved)) throw new Error(); stopTime(); resumeAfterModal = false; state = saved; extendBuildings(); save(); closeModal(); render(); toast('手動保存した国家状態を読み込みました'); } catch { toast('手動保存がありません。先に手動保存してください'); } }
  if (action === 'reset-confirm') openModal('王国を最初からやり直す', '<p>施設レベル、工事、資源、国家記録を初期状態へ戻します。自動保存と手動保存も初期化されます。</p>', '<button data-action="settings">戻る</button><button class="danger" data-action="reset">初期状態に戻す</button>');
  if (action === 'reset') { stopTime(); resumeAfterModal = false; state = initialState(); try { localStorage.removeItem(manualSaveKey); } catch {} save(); closeModal(); location.hash = 'kingdom'; render(); toast('王国を初期状態に戻しました'); }
});
document.addEventListener('input',event=>{
  if(state.phase==='planning' && event.target.matches('[data-budget-index]'))changeBudget(Number(event.target.dataset.budgetIndex),Number(event.target.value),true);
});
document.addEventListener('change', event => {
  if(event.target.matches('[data-formation-weapon]')){formationDraft[event.target.dataset.formationWeapon]=event.target.value;render();return;}
  if(event.target.matches('[data-soldier-sort]')){soldierSort=event.target.value;render();return;}
  if (state.phase !== 'planning') return;
  if (event.target.matches('[data-use-treasury]')) { toggleTreasury(event.target.checked);return; }
  const i = event.target.dataset.budgetIndex;
  if (i !== undefined) changeBudget(Number(i), Number(event.target.value));
});
modal.addEventListener('click', event => { const r = modal.getBoundingClientRect(); if (event.target === modal && (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom)) { event.stopPropagation(); closeModal(); } });
modal.addEventListener('cancel', event => { event.preventDefault(); closeModal(); });
function migrateTrainingRoute() { if(location.hash==='#training'){budgetTabChoice='training';history.replaceState(null,'','#plan');} }
migrateTrainingRoute();
let lastRoute=route();
window.addEventListener('hashchange', () => {
  migrateTrainingRoute();
  if(lastRoute==='soldiers' && route()!=='soldiers' && unequippedDefenders().length){const target=route();history.replaceState(null,'','#soldiers');if(modal.open)closeModal();render();confirmSoldierExit(target);return;}
  const showAttack=lastRoute==='soldiers' && route()==='kingdom' && returnToDefense && state.defenseAttack;
  if(lastRoute==='soldiers' && route()!=='soldiers')returnToDefense=false;
  lastRoute=route();if(modal.open)closeModal();render();
  if(showAttack){showDefenseAttack();return;}
  document.querySelector('#view').focus({preventScroll:true});
});
document.addEventListener('keydown', event => {
  if (modal.open || event.ctrlKey || event.metaKey || event.altKey) return;
  if (event.target.matches('[data-action="expedition-tab"][role="tab"]') && ['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) {event.preventDefault();expeditionTab=event.key==='Home'?'destination':event.key==='End'?'members':expeditionTab==='destination'?'members':'destination';render();document.querySelector(`#expedition-tab-${expeditionTab}`)?.focus();return;}
  if (event.target.matches('[data-action="budget-tab"][role="tab"]') && ['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) { event.preventDefault(); const tabs=['budget','construction','training'];budgetTabChoice = event.key==='Home' ? tabs[0] : event.key==='End' ? tabs[2] : tabs[(tabs.indexOf(budgetTabChoice)+(event.key==='ArrowRight'?1:2))%3]; render(); document.querySelector(`[role="tab"][data-tab="${budgetTabChoice}"]`)?.focus({ preventScroll: true }); return; }
  if (event.target.matches('input, select, textarea')) return;
  if (event.key === 'Escape') { location.hash = 'kingdom'; return; }
  if (/^[1-4]$/.test(event.key)) location.hash = ['kingdom', 'facilities', 'plan', 'history'][Number(event.key) - 1];
  if (event.code === 'Space' && state.phase === 'running' && event.target.tagName !== 'BUTTON') {
    event.preventDefault();
    if (timer) { stopTime(); render(); } else startTime();
  }
});
window.addEventListener('pagehide', () => { stopTime(); save(); });
render();

new ResizeObserver(()=>document.documentElement.style.setProperty('--hud-bottom',`${document.querySelector('.hud').getBoundingClientRect().bottom}px`)).observe(document.querySelector('.hud'));
