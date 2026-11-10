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
function monthlyMaterials(){return CountryBalance.materialIncome(state.nextDifficulty||state.difficulty);}
const resourceIcons = [
  '<svg viewBox="0 0 24 24" aria-hidden="true"><ellipse cx="12" cy="5.5" rx="7" ry="2.8"/><path d="M5 5.5v4.2c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8V5.5M5 9.7v4.2c0 1.5 3.1 2.8 7 2.8 1.1 0 2.2-.1 3.1-.3"/><path d="M12 3v6m-2.2-1.8c.4-.5 1.1-.8 2.2-.8 1.4 0 2.3.7 2.3 1.7 0 2.3-4.6 1.3-4.6 3.8 0 1 1 1.7 2.4 1.7 1 0 1.8-.3 2.2-.8"/></svg>',
  '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4.5h13a2.5 2.5 0 0 1 0 5H5a2.5 2.5 0 0 1 0-5Zm1 8h13a2.5 2.5 0 0 1 0 5H6a2.5 2.5 0 0 1 0-5Z"/><ellipse cx="5" cy="7" rx="1.4" ry="2.5"/><ellipse cx="6" cy="15" rx="1.4" ry="2.5"/></svg>',
  '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 3 8-1 6 6-3 13H6L3 8l4-5Z"/><path d="m7 3 5 6 3-7M12 9 6 21m6-12 6 12m-6-12 9-1"/></svg>'
];
const storageKey = 'war-simulation-country-v1';
const manualSaveKey = `${storageKey}-manual`;
const initialState = (difficulty='normal',seed=Date.now()>>>0) => ({ version: 1, difficulty, balanceSeed:seed, balanceRulesVersion:1, randomCounters:{}, budgetPolicy:{amounts:facilities.map((_,i)=>i<3?CountryBalance.upkeep(1)+10:0),useTreasury:false}, useTreasury:false, treasuryDefaultOff:true, trainingStateSeparated:true, trainingDraft:characterNames.map((_,id)=>id===2?{school:0,slot:0,purpose:'復帰訓練',skill:'戦士訓練 1'}:null), trainingPolicy:characterNames.map(()=>null), nextTraining:null, expeditions: [], nextExpeditionId: 1, year: 1, month: 1, day: 1, phase: 'planning', resources: CountryBalance.initialResources(difficulty), levels: [1, 1, 1, 0, 0, 0, 0], queues: facilities.map(() => []), plan: facilities.map(() => 0), logs: [{ date: '1年 1月1日', text: '王国の運営を開始。施設を選び、強化命令を決めよう。' }], report: null, completed: [], training: [null, null, null, null], soldiers: [0, 0, 0, 0], characters: initialCharacters(), trainingUsed: [1,0,0,0], cpTrainingSeeded:true });
let state;
let timer = null;
let monthAdvance = null;
let suspendedMonthAdvance = null;
let pendingTimeAction = null;
let monthAdvanceTimer = null;
let attackArrivalTimer = null;
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
function initialCharacters() { return characterNames.map((name,id)=>({name,cp:id===2 ? 0 : 100,maxCP:100,qualified:true,skills:id<6 ? ["基本訓練 I"] : [],task:null})); }
state = initialState();
let toastTimer;
let modalOpener;
let resumeAfterModal = false;
let showingDefenseResult = false;
let previousResourceValues = null;
const modal = document.querySelector('#modal');
const validState = s => s?.version === 1 && Number.isInteger(s.year) && s.year > 0 && Number.isInteger(s.month) && s.month >= 1 && s.month <= 12 && Number.isInteger(s.day) && s.day >= 1 && s.day <= daysInMonth(s.month) && ['planning', 'running'].includes(s.phase) && Array.isArray(s.resources) && s.resources.length === 3 && s.resources.every(n => Number.isSafeInteger(n) && n >= 0) && ['levels', 'plan'].every(key => Array.isArray(s[key]) && [4, facilities.length].includes(s[key].length) && s[key].every(n => Number.isSafeInteger(n) && n >= 0)) && Array.isArray(s.queues) && s.queues.length === s.levels.length && s.queues.every(q => Array.isArray(q) && q.every(t => Number.isInteger(t.level) && t.level > 0 && Number.isInteger(t.remaining) && t.remaining > 0 && t.remaining <= 5)) && Array.isArray(s.logs) && s.logs.every(l => typeof l.date === 'string' && typeof l.text === 'string') && Array.isArray(s.completed) && s.completed.every(v => typeof v === 'string') && (s.training === undefined || (Array.isArray(s.training) && s.training.length === 4 && s.training.every(t => t === null || (Number.isSafeInteger(t.count) && t.count > 0 && Number.isInteger(t.remaining) && t.remaining >= 0 && t.remaining <= 5)))) && (s.soldiers === undefined || (Array.isArray(s.soldiers) && s.soldiers.length === 4 && s.soldiers.every(n => Number.isSafeInteger(n) && n >= 0))) && (s.characters === undefined || (Array.isArray(s.characters) && s.characters.length===30 && s.characters.every(c=>typeof c.name==='string' && Number.isInteger(c.cp) && c.cp>=0 && Number.isInteger(c.maxCP) && c.cp<=c.maxCP && typeof c.qualified==='boolean' && Array.isArray(c.skills) && c.skills.every(v=>typeof v==='string') && (!c.task || (Number.isInteger(c.task.school) && c.task.school>=0 && c.task.school<4 && typeof c.task.skill==='string' && typeof c.task.purpose==='string'))))) && (s.trainingUsed===undefined || (Array.isArray(s.trainingUsed) && s.trainingUsed.length===4 && s.trainingUsed.every(n=>Number.isInteger(n) && n>=0))) && (s.report === null || (typeof s.report.title === 'string' && Array.isArray(s.report.completed) && s.report.completed.every(v => typeof v === 'string')));
try { const saved = JSON.parse(localStorage.getItem(storageKey)); if (validState(saved)) state = saved; } catch {}
initializeBalance();
function initializeBalance(){
  state.difficulty=CountryBalance.profiles[state.difficulty]?state.difficulty:'normal';
  state.balanceSeed??=1357911;state.randomCounters??={};
  if(!CountryBalance.profiles[state.nextDifficulty])delete state.nextDifficulty;
  if(!state.balanceRulesVersion){delete state.defenseChance;delete state.mockExpeditionOutcome;state.balanceRulesVersion=1;}
}
function gameRandom(channel,serial,date=recoveryDate()){
  const counters=state.randomCounters;
  if(counters.date!==date){counters.date=date;counters.counts={};}
  if(serial===undefined){serial=counters.counts[channel]||0;counters.counts[channel]=serial+1;}
  let value=state.balanceSeed>>>0;
  for(const char of `${date}:${channel}:${serial}`)value=Math.imul(value^char.charCodeAt(0),16777619);
  value=Math.imul(value^(value>>>16),2246822507);value=Math.imul(value^(value>>>13),3266489909);
  return ((value^(value>>>16))>>>0)/4294967296;
}
function extendBuildings() {
  if(!state.treasuryDefaultOff){state.useTreasury=false;state.treasuryDefaultOff=true;}
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
  if(!state.trainingStateSeparated){
    // Legacy saves have no confirmation snapshot, so retain assignments as drafts rather than start them silently.
    state.trainingDraft=state.characters.map(c=>c.task?{...c.task}:null);
    state.trainingPolicy=state.characters.map(()=>null);state.nextTraining=null;
    state.characters.forEach(c=>c.task=null);state.trainingStateSeparated=true;
  }
  updateTrainingUsed();
}
extendBuildings();
initializeRecovery();
formationDraft=Object.fromEntries(Object.entries(state.expeditionPlan||{}).filter(([id])=>expeditionEligible(state.characters[id])));expeditionTarget=state.expeditionTarget??null;
function populationCount() { return state.population ?? 12560; }
function taxIncome() { return state.taxOverride ?? CountryBalance.taxIncome(populationCount(),state.difficulty); }
function updatePopulation() {
  const b=state.monthBudget;
  state.populationChange=CountryBalance.populationChange(populationCount(),b?.clinicLevel??state.levels[5],b?.budgets[5]||0);
  state.population=state.populationChange.after;
  log(`人口 ${state.population.toLocaleString()}人。翌月の税収 ${taxIncome()}。`);
}
function showPopulation() {
  const c = state.populationChange;
  const delta = c ? c.after-c.before : 0;
  openModal('人口', `<div class="population-overview"><small>現在の人口</small><strong>${populationCount().toLocaleString()}<small>人</small></strong><span class="tax-accent">${c ? `前月から ${delta >= 0 ? '＋' : '−'}${Math.abs(delta)}人` : 'まだ月次の増減記録はありません'}</span></div><h3>前月の増減</h3>${c ? `<div class="population-breakdown"><div><span>出生</span><b class="tax-accent">＋${c.births}</b></div><div><span>死亡</span><b>−${c.deaths}</b></div><div><span>転入</span><b class="tax-accent">＋${c.arrivals}</b></div><div><span>転出</span><b>−${c.departures}</b></div></div>` : '<p class="muted">次の月へ進むと、内訳を確認できる。</p>'}<div class="population-tax"><span>今月の税収</span><strong>${taxIncome()}</strong><small>人口に応じて税収が変わる。</small></div><p class="muted">試作ルール：人口は月初に更新。出生0.4％・死亡0.2％・転入0.3％・転出0.1％。人口上限16,000人。診療所の活動予算で自然死亡を最大30％減らす。税収は人口÷125×難易度倍率、端数切り捨て。</p>`, '<button data-action="close">閉じる</button>');
}
function daysInMonth(month) { return [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1]; }
function dateText() { return `${state.year}年${state.month}月${state.day}日`; }
function escapeHTML(value) { return String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); }
function save(manual = false) { try { localStorage.setItem(manual ? manualSaveKey : storageKey, JSON.stringify(state)); if (manual) toast('手動保存しました。後でこの状態に戻せます'); } catch { toast('記録を保存できません。保存領域へのアクセスを確認してください'); } }
function log(text) { state.logs.unshift({ date: dateText(), text }); state.logs = state.logs.slice(0, 200); state.recordsUnread = true; }
function toast(message) { const el = document.querySelector('#toast'); el.textContent = message; el.classList.add('visible'); clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.remove('visible'), 3200); }
function cost(level) { return CountryBalance.cost(level,state.difficulty); }
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
    ${windowTitle('建築物一覧', '<span class="tag">建物の管理</span>')}
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
function budgetValuesFor(amounts, useTreasury, treasury=state.resources[0]) {
  const tax=state.taxOverride??CountryBalance.taxIncome(populationCount(),state.day!==1?(state.nextDifficulty||state.difficulty):state.difficulty),pool=tax+(useTreasury?treasury:0);
  const minimums=state.levels.map(level=>!level?0:pool?Math.round(CountryBalance.upkeep(level)/pool*100):100);
  const budgets=facilities.map((_,i)=>state.levels[i]?Math.min(CountryBalance.budgetCap(i,state.levels[i]),Math.max(CountryBalance.upkeep(state.levels[i]),Math.round(amounts?.[i]||0))):0);
  const required=state.levels.reduce((sum,level)=>sum+CountryBalance.upkeep(level),0);
  let excess=Math.max(0,budgets.reduce((a,b)=>a+b,0)-pool);
  if(required<=pool)budgets.forEach((amount,i)=>{const trim=Math.min(excess,amount-CountryBalance.upkeep(state.levels[i]));excess-=trim;budgets[i]-=trim;});
  const allocation=budgets.reduce((a,b)=>a+b,0);
  const weights=[...budgets,Math.max(0,pool-allocation)].map(n=>pool?n/pool*100:0);
  const rounded=weights.map(Math.floor);
  if(pool && required<=pool){
    const order=weights.map((n,i)=>({i,remainder:n-rounded[i]})).sort((a,b)=>b.remainder-a.remainder||a.i-b.i);
    const remaining=100-rounded.reduce((a,b)=>a+b,0);
    for(let n=0;n<remaining;n++)rounded[order[n].i]++;
  }
  const shares=rounded.slice(0,facilities.length),share=shares.reduce((a,b)=>a+b,0);
  minimums.forEach((n,i)=>{minimums[i]=Math.min(n,shares[i]);});
  return {tax,treasury,pool,minimums,minimumAmounts:state.levels.map(level=>CountryBalance.upkeep(level)),maximumAmounts:state.levels.map((level,i)=>CountryBalance.budgetCap(i,level)),budgets,shares,share,allocation,deposit:Math.max(0,tax-allocation),withdrawal:Math.max(0,allocation-tax),feasible:required<=pool};
}
function budgetTreasury(){
  const previous=state.day===1 && state.monthBudget?.year===state.year && state.monthBudget.month===state.month?state.monthBudget:null;
  return Math.max(0,state.resources[0]-(previous?.deposit||0)+(previous?.withdrawal||0));
}
function budgetValues() {
  state.budgetAmounts ??= [...(state.budgetPolicy?.amounts||facilities.map((_,i)=>Math.floor((taxIncome()+(state.useTreasury?budgetTreasury():0))*(state.budgetShare?.[i]||0)/100)))];
  const b=budgetValuesFor(state.budgetAmounts,state.useTreasury,budgetTreasury());
  state.budgetAmounts=[...b.budgets];state.budget=[...b.budgets];state.budgetShare=[...b.shares];
  return b;
}
const percent=n=>Math.round(n).toLocaleString();
function nextBudgetDate(){return state.month===12?`${state.year+1}年1月`:`${state.year}年${state.month+1}月`;}
function monthSettingsDate(){return state.day===1?`${state.year}年${state.month}月`:nextBudgetDate();}
function budgetPolicy(){return {amounts:[...state.budgetAmounts],useTreasury:Boolean(state.useTreasury)};}
function restoreMonthlyDraft(){
  if(monthAdvance)pendingTimeAction='month';else if(timer)pendingTimeAction='time';
  stopTime();stopMonthAdvance();resumeAfterModal=false;
  const policy=state.day===1?state.budgetPolicy:state.nextBudget||state.budgetPolicy;
  state.budgetAmounts=[...policy.amounts];state.useTreasury=policy.useTreasury;
  state.trainingDraft=copyTraining(state.day===1?state.trainingPolicy:state.nextTraining||state.trainingPolicy);
  budgetValues();updateTrainingUsed();
}
function initializeBudgetPolicy(){
  if(state.budgetPolicy)return;
  const amounts=state.monthBudget?.budgets||state.budgetAmounts||facilities.map((_,i)=>Math.floor(taxIncome()*(state.budgetShare?.[i]||0)/100));
  state.budgetPolicy={amounts:[...amounts],useTreasury:state.monthBudget?state.monthBudget.withdrawal>0:Boolean(state.useTreasury)};
}
initializeBudgetPolicy();
function applyMonthBudget(reapply=false){
  const previous=state.monthBudget?.year===state.year && state.monthBudget.month===state.month?state.monthBudget:null;
  if(previous && !reapply)return;
  const treasury=state.resources[0]-(previous?.deposit||0)+(previous?.withdrawal||0);
  const b=budgetValuesFor(state.budgetPolicy.amounts,state.budgetPolicy.useTreasury,Math.max(0,treasury));
  state.monthBudget={year:state.year,month:state.month,opening:previous?.opening??state.resources[0],tax:b.tax,treasury:b.treasury,pool:b.pool,allocation:b.allocation,withdrawal:b.withdrawal,deposit:b.deposit,shares:b.shares,budgets:b.budgets,workshopLevel:state.levels[6],marketLevel:state.levels[4],clinicLevel:state.levels[5],difficulty:state.difficulty};
  state.resources[0]=treasury+b.deposit-b.withdrawal;state.phase='running';
}

function changeBudget(i,value,live=false) {
  const b=budgetValues();if(!b.feasible)return;
  const requested=Math.round(value)<=b.minimums[i]?b.minimumAmounts[i]:Math.round(b.pool*Math.round(value)/100);
  const amount=Math.max(b.minimumAmounts[i],Math.min(b.maximumAmounts[i],b.pool-b.allocation+state.budget[i],requested));
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
  return `<div class="window-title budget-tab-header"><div class="budget-tabs" role="tablist" aria-label="国家運営">${[['budget',state.day===1?'今月の予算':'来月の予算'],['construction','建設'],['training','兵士育成']].map(([tab,name])=>`<button role="tab" id="tab-${tab}" aria-controls="panel-${tab}" aria-selected="${budgetTabChoice === tab}" tabindex="${budgetTabChoice === tab ? 0 : -1}" data-action="budget-tab" data-tab="${tab}">${name}</button>`).join('')}</div><button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div>`;
}
function monthlyFooter() {
  const b=budgetValues(),policy=state.day===1?state.budgetPolicy:state.nextBudget;
  const saved=policy && JSON.stringify(policy)===JSON.stringify(budgetPolicy()) && trainingDraftSaved() && (state.day!==1 || state.monthSettingsConfirmed===`${state.year}-${state.month}`);
  const trainees=state.trainingDraft.filter(Boolean).length;
  const label=state.day===1?'今月':'来月';
  return `<div class="window-bottom"><div class="budget-footer"><span>予算配分 <b>${percent(b.share)}％</b></span><span>訓練予定 <b>${trainees}人</b></span><small>${monthSettingsDate()}から反映 · 確定すると時間を進める</small></div><button class="primary" data-action="commit" ${!b.feasible?'disabled':''}>${saved?'確定済みの内容で時間を進める':`${label}の予算・育成を確定して進行`}</button></div>`;
}
const flowArrow='<svg viewBox="0 0 32 24" aria-hidden="true"><path d="M3 12h24M19 4l8 8-8 8"/></svg>';
const spendClock='<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 6v6l4 2"/></svg>';
const cpIcon='<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 4H4v16h16V4h-5M9 2h6v4H9Z"/><path d="m13 8-5 6h4l-1 4 5-6h-4Z"/></svg>';
const lossGlyph='<svg viewBox="0 0 20 20" aria-hidden="true"><path d="M4 10h12"/></svg>';
const gainGlyph='<svg viewBox="0 0 20 20" aria-hidden="true"><path d="M4 10h12M10 4v12"/></svg>';
function resourceChanges(changes,indices=changes.map((_,i)=>i),balances=state.resources) {
  return `<div class="resource-changes">${changes.map((change,n)=>{
    const i=indices[n];
    const before=balances[i],after=before+change,gain=change>0,scale=Math.max(before,after,1),remaining=Math.max(0,Math.min(before,after))/scale*100,changed=Math.min(Math.abs(change),scale)/scale*100;
    return `<div class="resource-change resource--${resourceKinds[i]} ${gain?'is-gain':'is-spend'} ${after<0?'is-short':''}" aria-label="${resourceNames[i]}：${before}から${after}、${change===0?'変化なし':gain?'増加':'消費'}${Math.abs(change)}"><span class="resource-symbol">${resourceIcons[i]}</span><div class="change-body"><div class="change-heading"><small>${['国庫','木材','鉱石'][i]}</small><strong class="change-amount"><span class="change-glyph">${change===0?'':gain?gainGlyph:lossGlyph}</span>${Math.abs(change).toLocaleString()}</strong></div><div class="change-values"><span>${before.toLocaleString()}</span><span class="flow-arrow">${flowArrow}</span><b>${after.toLocaleString()}</b>${after<0?'<span class="short-warning" aria-label="資源不足">△ !</span>':''}</div><div class="change-track" aria-hidden="true"><span class="change-retained" style="width:${Math.round(remaining)}%"></span><span class="change-used" style="width:${Math.round(changed)}%"></span></div></div></div>`;
  }).join('')}</div>`;
}
function constructionPrices(price,available=state.resources) {
  return `<div class="construction-cost">${price.map((n,j)=>`<div class="resource--${resourceKinds[j]} ${n>available[j]?'cost-short':''}"><span class="resource-symbol">${resourceIcons[j]}</span><small>${['国庫','木材','鉱石'][j]}</small><b><span class="change-glyph">${lossGlyph}</span>${n.toLocaleString()}</b></div>`).join('')}</div>`;
}
function plannedConstruction(i){return (state.nextConstruction||[]).filter(job=>job.index===i);}
function constructionLevel(i){return state.levels[i]+state.queues[i].length+plannedConstruction(i).length+1;}
function startConstruction(job){
  const {index:i,level,price}=job;
  if(price.some((n,j)=>n>state.resources[j]))return false;
  state.monthOpening ??= state.resources[0];state.monthBuilding=(state.monthBuilding||0)+price[0];
  state.resources=state.resources.map((n,j)=>n-price[j]);
  state.queues[i].push({level,remaining:5,price:[...price],paidYear:state.year,paidMonth:state.month});
  log(`${facilities[i].name}の${level===1?'新規建設':'強化'}を着工。資金 ${price[0]}。`);
  return true;
}
function orderConstruction(i){
  if(!facilities[i] || i===3 || constructionLevel(i)>CountryBalance.maxLevel || (!state.levels[i] && (state.queues[i].length || plannedConstruction(i).length)))return false;
  if(state.day===1 && !monthlySettingsPending() && plannedConstruction(i).length)return false;
  const level=constructionLevel(i),job={index:i,level,price:cost(level)};
  if(state.day===1 && !monthlySettingsPending()){if(!startConstruction(job))return false;}
  else{state.nextConstruction??=[];state.nextConstruction.push(job);log(`${monthSettingsDate()}の月初に${facilities[i].name}の${level===1?'新規建設':'強化'}を予定。`);}
  save();return true;
}
function constructionSchedule(resources){
  const remaining=[...resources],levels=state.levels.map((level,i)=>level+state.queues[i].length);
  const orders=(state.nextConstruction||[]).map(job=>{
    const before=[...remaining],short=job.price.some((n,j)=>n>remaining[j]);
    const status=short?'資源不足':job.level!==levels[job.index]+1?'先行工事待ち':'発注';
    if(status==='発注'){job.price.forEach((n,j)=>remaining[j]-=n);levels[job.index]++;}
    return {job,before,status};
  });
  return {orders,resources:remaining};
}
function applyPlannedConstruction(){
  const pending=[];let started=0;
  for(const {job,status} of constructionSchedule(state.resources).orders){
    if(status==='発注'){startConstruction(job);started++;}
    else{job.deferred=status;pending.push(job);log(`${facilities[job.index].name}は${status}のため着工待ち。次の確定時に再確認。`);}
  }
  state.nextConstruction=pending;return started;
}
function constructionImpact(i,level,price) {
  const f=facilities[i];
  return `<section class="spending-preview" aria-label="支払いと工事の変化"><div class="spending-resources"><h3>${state.day===1?'今月の着工時に支払う資源':`${nextBudgetDate()}の着工時に支払う資源`}</h3>${resourceChanges(price.map(n=>-n))}</div><span class="flow-arrow spending-arrow">${flowArrow}</span><div class="spending-result"><span class="spending-building" aria-hidden="true"><svg viewBox="0 0 80 72"><path d="M7 29 40 7l33 22H7ZM13 29v37h54V29M28 66V43h24v23M7 66h66"/><path d="M19 35h8m26 0h8"/></svg><span class="facility-icon">${f.icon}</span></span><h3>${f.name}</h3><div class="result-levels"><span>Lv.${level-1}</span><span class="flow-arrow">${flowArrow}</span><b>Lv.${level}</b></div><div class="result-level-pips" aria-hidden="true">${Array.from({length:Math.min(level,8)},(_,n)=>`<i class="${n===Math.min(level,8)-1?'new-level':''}"></i>`).join('')}</div><span class="spend-timing">${spendClock}${monthSettingsDate()}の月初 · 工期 5日${state.queues[i].length?' · 先行工事の後':''}</span></div></section>`;
}
function budgetSpending(b,preview=null) {
  const previous=state.day===1 && state.monthBudget?.year===state.year && state.monthBudget.month===state.month?state.monthBudget:null;
  const change=b.deposit-b.withdrawal-(previous?.deposit||0)+(previous?.withdrawal||0);
  return `<section class="budget-spending" aria-label="${preview?'予算・建築反映後の資源':'予算配分による変化'}"><span class="spend-timing">${spendClock}${monthSettingsDate()}の月初</span><div class="budget-money-flow"><span class="resource-symbol resource--funds">${resourceIcons[0]}</span><strong>${b.allocation.toLocaleString()}</strong><span class="flow-arrow">${flowArrow}</span><div class="budget-recipients">${facilities.map((f,i)=>b.budgets[i]?`<span class="facility-icon" title="${f.name}に${b.budgets[i]}">${f.icon}</span>`:'').join('')}</div></div><small>${preview?'予算と発注可能な工事を反映':'建物の運営・育成へ配分'}</small>${resourceChanges(preview?preview.resources.map((n,i)=>n-state.resources[i]):[change])}${preview&&state.day!==1?'<small>次の月初の木材・鉱石収入を含む。税収は現在値で試算。</small>':''}</section>`;
}
function renderConstruction() {
  const f = facilities[constructionChoice];
  const pending = state.queues[constructionChoice].length > 0 || plannedConstruction(constructionChoice).length > 0;
  const built = state.levels[constructionChoice] > 0;
  const canBuild = state.day!==1 || monthlySettingsPending() || cost(1).every((n,j)=>n<=state.resources[j]);
  return `<section class="game-window budget-window construction-window" aria-label="建設">${budgetTabs()}<div class="window-content construction-content" role="tabpanel" id="panel-construction" aria-labelledby="tab-construction" tabindex="0">
    <div class="construction-wallet">${['人口','税収','国庫','木材','鉱石'].map((name,i)=>`<div><span class="resource-symbol">${i<2?'':resourceIcons[i-2]}</span><span>${name}</span><strong>${[populationCount(),taxIncome(),...state.resources][i].toLocaleString()}</strong></div>`).join('')}</div>
    <section class="new-construction"><h2>新規建設</h2><div class="construction-picker"><div class="construction-catalog" role="group" aria-label="建設する建物を選択">${facilities.slice(4).map((building,n)=>{const i=n+4; return `<button class="construction-option ${i===constructionChoice ? 'is-selected' : ''}" data-action="construction-select" data-index="${i}" aria-pressed="${i===constructionChoice}"><span class="building-preview preview-${building.id}" aria-hidden="true"></span><span><b>${building.name}</b><small>${building.role}</small></span><span class="catalog-status">${state.levels[i] ? '建設済み' : state.queues[i].length ? '建設中' : plannedConstruction(i).length ? `${state.day===1?'今月':'来月'}着工` : '未建設'}</span></button>`;}).join('')}</div><section class="construction-inspector" aria-label="選択した建物の詳細"><div class="inspector-heading"><span class="facility-icon">${f.icon}</span><div><h3>${f.name}</h3><small>${f.role}</small></div></div><p>${f.description}</p>${constructionPrices(cost(1))}<div class="construction-order"><span>${monthSettingsDate()}に着工 · 工期 <b>5日</b></span><button class="primary" data-action="new-build-confirm" data-index="${constructionChoice}" ${built || pending || !canBuild ? 'disabled' : ''}>${built ? '建設済み' : pending ? plannedConstruction(constructionChoice).length?`${state.day===1?'今月':'来月'}着工予定`:'建設中' : !canBuild ? '資源不足' : state.day===1?'建設を決める':'来月の建設を予約'}</button></div></section></div></section>
    <section class="existing-construction"><div class="construction-list-heading"><h2>既存の建物を強化</h2><button data-action="navigate-facilities">建築物一覧を確認</button></div><div class="construction-columns"><span>建物</span><span>強化に必要な資源</span><span>工期</span><span></span></div>
    ${facilities.map((f,i)=>{ if (!state.levels[i]) return ''; const next=constructionLevel(i),price=cost(next),atMax=next>CountryBalance.maxLevel,canBuild=!atMax && (state.day!==1 || monthlySettingsPending() || price.every((n,j)=>n<=state.resources[j])); return `<article class="construction-row"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${next-1} → Lv.${next}</small></div></div>${constructionPrices(price)}<div class="construction-duration"><b>5日</b><small>${state.day!==1?'来月の月初から':state.queues[i].length ? '先行工事の後に着工' : '今月の月初に着工'}</small></div><button class="primary" data-action="build" data-index="${i}" aria-label="${f.name}をLv.${next}へ強化" ${canBuild ? '' : 'disabled'}>${atMax?'最大Lv':canBuild ? state.day===1?'強化する':'来月の強化を予約' : '資源不足'}</button></article>`; }).join('')}</section>
    <div class="construction-queues"><h2>工事状況 <small>${constructionCount()}件</small></h2><div>${constructionCount() ? facilities.map((f,i)=>state.queues[i].length ? `<section><h3>${f.name}</h3>${queueHTML(i)}</section>` : '').join('') : '<p class="muted">進行中の工事はありません。</p>'}</div><h2>${state.day===1?'今月':'来月'}の着工予定 <small>${(state.nextConstruction||[]).length}件</small></h2>${(state.nextConstruction||[]).map(job=>`<section><h3>${facilities[job.index].name} · Lv.${job.level}</h3>${job.deferred?`<p class="reserve-accent">${job.deferred}で着工待ち · 次の確定時に再確認</p>`:''}${constructionPrices(job.price)}</section>`).join('')||'<p class="muted">来月の着工予定はありません。</p>'}</div>
    </div>${monthlyFooter()}</section>`;
}
function confirmNewBuilding(i) {
  const f = facilities[i];
  if (!f || i < 4 || state.levels[i] || state.queues[i].length || plannedConstruction(i).length) return;
  openModal(`${f.name}を建設`, `${constructionImpact(i,1,cost(1))}<p>${f.description}</p><p class="muted">${state.day===1?monthlySettingsPending()?'予算の確定時に資源を支払って着工し、工事完了後に使える。':'資源は着工時に支払い、建物は工事完了後に使える。':'今は資源を消費せず、来月の予算確定後に資源を支払って着工する。'}</p>`, `<button data-action="close">戻る</button><button class="primary" data-action="new-build" data-index="${i}" ${state.day===1 && !monthlySettingsPending() && cost(1).some((n,j)=>n>state.resources[j])?'disabled':''}>${state.day===1?!monthlySettingsPending()?'資源を支払って着工':'今月の建設を予約':'来月の建設を予約'}</button>`);
}
function renderPlan() {
  if (budgetTabChoice === 'construction') return renderConstruction();
  if (budgetTabChoice === 'training') return renderTraining();
  const b = budgetValues(),preview=budgetOutcomePreview(b);
  const money = n => n.toLocaleString();
  return `<section class="game-window budget-window" aria-label="${state.day===1?'今月':'来月'}の予算">
    ${budgetTabs()}
    <div class="window-content budget-content" role="tabpanel" id="panel-budget" aria-labelledby="tab-budget" tabindex="0">
      <div class="budget-effective"><span class="budget-effective-label">${monthSettingsDate()}の予算</span><span>${state.day===1?'確定すると今月の予算・育成へ反映。':'2日以降の確定は来月から反映。'}</span></div><div class="budget-source"><div class="budget-source-toolbar"><span>予算の財源</span><label class="treasury-option ${state.useTreasury ? 'is-enabled' : ''}"><input type="checkbox" data-use-treasury aria-label="国庫を配分に使う" ${state.useTreasury ? 'checked' : ''}><span>国庫も配分に含める</span></label></div><div class="budget-source-tax"><small>税収</small><strong>${money(b.tax)}</strong><span class="source-note">${state.day===1?'今月':'来月'}の見込み</span></div><div class="budget-source-treasury"><small>国庫</small><strong>${money(b.treasury)}</strong><span class="source-note">${state.useTreasury ? '配分に含める' : '今回は使わない'}</span></div><div class="budget-source-total"><small>配分できる合計</small><strong>${money(b.pool)}</strong><span class="source-note">${state.useTreasury ? '税収と国庫を含む' : '税収のみ'}</span></div></div>
      <div class="allocation-bar" aria-label="建物へ${percent(b.share)}％、未配分${percent(Math.max(0,100-b.share))}％">${state.budgetShare.map((n,i)=>n ? `<span class="allocation-segment allocation-${i}" style="flex:${n}" title="${facilities[i].name} ${percent(n)}％"></span>` : '').join('')}<span class="allocation-segment allocation-saved" style="flex:${Math.max(0,100-b.share)}" title="未配分 ${percent(Math.max(0,100-b.share))}％"></span></div>
      <div class="budget-layout"><div class="building-budgets"><div class="budget-columns"><span>建築物</span><span>予算の配分</span></div>
      ${facilities.map((f,i)=> i!==3 && state.levels[i] ? `<article class="building-budget"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${state.levels[i]}</small><p class="facility-purpose">${facilityPurposes[i]}</p><div class="allocation-effect">${facilityEffect(i)}</div></div></div><div class="allocation-control"><div class="allocation-top"><div class="budget-adjust tax-accent"><button data-action="budget-down" data-index="${i}" aria-label="${f.name}の配分率を1％減らす" ${!b.feasible || state.budget[i]<=CountryBalance.upkeep(state.levels[i]) ? 'disabled' : ''}>−</button><strong><small>配分率</small>${percent(state.budgetShare[i])}<small>％</small></strong><button data-action="budget-up" data-index="${i}" aria-label="${f.name}の配分率を1％増やす" ${!b.feasible || b.allocation>=b.pool || state.budget[i]>=b.maximumAmounts[i] ? 'disabled' : ''}>＋</button></div><div class="allocation-amount"><small>配分額</small><b>${money(state.budget[i])}</b></div></div><input type="range" min="${b.minimums[i]}" max="${Math.min(100,Math.ceil(b.maximumAmounts[i]/b.pool*100))}" step="1" value="${state.budgetShare[i]}" data-budget-index="${i}" aria-label="${f.name}の配分率" aria-valuetext="${percent(state.budgetShare[i])}％、最低配分額${money(b.minimumAmounts[i])}" ${!b.feasible ? 'disabled' : ''}><div class="allocation-minimum"><span>最低配分額 ${money(b.minimumAmounts[i])}</span><span>必要な運営費を含む</span></div></div></article>` : '').join('')}</div>
      <aside class="budget-reserve"><h2>確定後の資源</h2>${budgetSpending(b,preview)}${constructionSummary(preview)}<p>未配分の税収は国庫へ。待機中の工事は支払わない。</p>${!b.feasible ? '<p class="reserve-accent">最低配分を満たすには、国庫も使う必要があります。</p>' : ''}</aside></div>
    </div>${monthlyFooter()}</section>`;
}
const facilityPurposes=['兵士の訓練を支える','魔術の訓練を支える','回復と支援の訓練を支える','','商業を支える','住民の健康を守る','一点ものの武器を生産する'];
function facilityEffect(i){
 const amount=state.budget[i]||0,level=state.levels[i],upkeep=CountryBalance.upkeep(level);
 if(i<3)return `訓練枠 <b>${CountryBalance.schoolCapacity(level,amount)} / ${Math.min(5,level)*5}人</b><small>維持${upkeep}＋1枠につき活動予算2</small>`;
 if(i===4)return `翌月初の売上 <b>＋${CountryBalance.marketIncome(level,amount,state.nextDifficulty||state.difficulty)}</b><small>維持${upkeep}を除く活動額×1.8×収入倍率</small>`;
 if(i===5)return `訓練支援 <b>${CountryBalance.clinicCapacity(level,amount)}人</b><small>支援対象は所要日数−1日 · 自然死亡を最大30％抑制</small>`;
 if(i===6)return `${state.day===1?'今月':'来月'}の生産上限 <b>${productionCount(amount)}点</b><small>維持${upkeep}＋1本につき資金10・木材3・鉱石2 · 月末完成</small>`;
 return '';
}
function skillLink(skill){return `<button class="skill-link" data-action="skill-info" data-skill="${escapeHTML(skill)}">${escapeHTML(skill)}</button>`;}
function showCharacterSkills(id){
 characterChoice=id;const c=state.characters[id];
 openModal(`${escapeHTML(c.name)}のスキル`,`<section class="character-skill-overview"><h3>習得済み <small>${c.skills.length}件</small></h3>${learnedSkills(c)}${c.task?`<h3>今月の訓練で習得</h3>${skillLink(c.task.skill)}<p class="muted">${facilities[c.task.school].name} · ${trainingProgressText(c.task)}</p>`:''}</section>`,'<button data-action="close">戻る</button>');
 modal.classList.add('skill-dialog');
 modal.querySelectorAll('[data-action="skill-info"]').forEach(button=>button.dataset.backCharacter=id);
}
function skillData(skill){
 const school=skill.includes('魔法')?1:skill.includes('僧侶')?2:0,level=Number(skill.match(/(\d+)$/)?.[1]||0);
 const data=[{kind:'近接訓練',description:'剣・盾を使う兵士の戦力を高める。',stat:'STR'},{kind:'魔法訓練',description:'杖・魔導書を使う兵士の戦力を高める。',stat:'INT'},{kind:'祈祷訓練',description:'聖書・ロザリオを使う兵士の戦力を高める。',stat:'FAI'}][school];
 return {...data,school,level,bonus:skill.includes('訓練 ')?4*Math.min(5,level):0};
}
function showSkillInfo(skill,backCharacter){
 const d=skillData(skill),c=state.characters[characterChoice],known=c.skills.includes(skill),scheduled=c.task?.skill===skill,planned=state.trainingDraft[characterChoice]?.skill===skill;
 openModal(skill,`<section class="skill-detail"><div class="skill-kind"><span>${facilities[d.school].icon} ${d.kind}</span><small>Lv.${d.level}</small></div><p class="skill-description">${d.description}</p><dl class="skill-facts"><div><dt>部隊戦力</dt><dd>＋${d.bonus}％</dd></div><div><dt>参照能力</dt><dd>${d.stat}</dd></div></dl><div class="skill-learning"><span>${escapeHTML(c.name)}</span><b>${known?'習得済み':scheduled?'今月の訓練で習得予定':planned?'来月の訓練で習得予定':'未習得'}</b></div><p class="skill-source">${facilities[d.school].name} Lv.${d.level}の訓練で習得。<br>学校の強化だけでは、習得済みスキルは変わらない。</p><small class="muted">対応武器を装備した時、最も高い習得Lvだけを使う。国家UIの戦力計算へ反映。</small></section>`,backCharacter===undefined?'<button data-action="close">戻る</button>':`<button data-action="character-skills" data-index="${backCharacter}">スキル一覧へ戻る</button>`);
 modal.classList.add('skill-dialog');
}
function learnedSkills(c){return c.skills.length?`<ul class="learned-skills">${c.skills.map(skill=>{const d=skillData(skill);return `<li>${skillLink(skill)}<small>${d.bonus?`対応武器の戦力＋${d.bonus}％`:'基礎課程'}</small></li>`;}).join('')}</ul>`:'<p class="muted">習得済みスキルなし</p>';}
function upgradeEffects(i,level){
 return `<section class="upgrade-effects"><h3>強化で変わること</h3>${i<3?`<div><span>毎月の訓練枠</span><b>${(level-1)*5}人 <em>→</em> ${level*5}人</b></div><div><span>習得可能スキル</span><span>${soldierTypes[i]}訓練 ${level-1} <em>→</em> ${soldierTypes[i]}訓練 ${level}</span></div><p>強いスキルの習得には、強化後にもう一度訓練が必要。</p>`:`<div><span>${['交易規模','診療能力','生産武器の強さ'][i-4]||'施設能力'}</span><b>${level-1}段階 <em>→</em> ${level}段階</b></div>`}<small>施設Lv上限5 · 学校は活動予算2につき1枠</small></section>`;
}
function trainingDraftSaved(){const confirmed=state.day===1?state.trainingPolicy:state.nextTraining;return Boolean(confirmed) && (state.day!==1 || state.monthSettingsConfirmed===`${state.year}-${state.month}`) && JSON.stringify(confirmed)===JSON.stringify(state.trainingDraft);}
function copyTraining(plan){return plan.map(t=>t?{...t}:null);}
function cancelTraining(id){state.trainingDraft[id]=null;updateTrainingUsed();save();render();}
function recoveryDate(){return (state.year-1)*365+Array.from({length:state.month-1},(_,i)=>daysInMonth(i+1)).reduce((sum,n)=>sum+n,0)+state.day;}
function recovering(c){return c.cp===0 && c.recoveryDays>0;}
function recoveryStatus(c){return recovering(c)?`療養中・あと${c.recoveryDays}日`:'療養完了・復帰訓練可能';}
function startRecovery(c){
  if(c.cp!==0)return;
  c.recoveryDays=CountryCombat.recoveryDays;c.recoveryDate=recoveryDate();c.task=null;c.defending=false;
  const id=state.characters.indexOf(c);
  for(const key of ['trainingDraft','trainingPolicy','nextTraining'])if(state[key] && id>=0)state[key][id]=null;
  if(state.trainingDraft)updateTrainingUsed();
}
function initializeRecovery(){
  state.characters.forEach(c=>{
    if(c.cp>0){delete c.recoveryDays;delete c.recoveryDate;return;}
    if(c.task){c.recoveryDays=0;c.recoveryDate=recoveryDate();return;}
    if(!Number.isInteger(c.recoveryDays) || c.recoveryDays<0 || c.recoveryDays>3 || !Number.isInteger(c.recoveryDate) || c.recoveryDate>recoveryDate())startRecovery(c);
  });
}
function advanceRecovery(){
  state.characters.forEach(c=>{
    if(!recovering(c))return;
    const elapsed=Math.max(0,recoveryDate()-c.recoveryDate);if(!elapsed)return;
    c.recoveryDays=Math.max(0,c.recoveryDays-elapsed);c.recoveryDate=recoveryDate();
    if(!c.recoveryDays){const text=`${c.name}の療養が完了。学校で復帰訓練が可能`;state.completed.push(text);log(text);}
  });
}
function combatProfile(){return CountryBalance.profile(state.difficulty);}
function initializeCombatState(){
  state.expeditionReadyOn??=[0,0,0];
  state.characters.forEach(c=>{
    if(!c.task || Number.isInteger(c.task.requiredDays))return;
    c.task.requiredDays=CountryCombat.trainingDays(c.task.purpose,state.levels[c.task.school],state.difficulty);
    c.task.progress=Math.min(c.task.requiredDays-1,state.day-1);c.task.lastTrainingDate=recoveryDate();
  });
  for(const m of state.expeditions)if(!m.combat)m.combat={difficulty:state.difficulty,ratio:1,chance:0,cp:combatProfile().cp,loot:[0,0,0],testOnly:true,supplies:0};
  if(state.defenseAttack && !state.defenseAttack.combat)state.defenseAttack.combat={difficulty:state.difficulty,enemyPower:state.defenseAttack.enemy*90*combatProfile().enemy,cp:combatProfile().cp,roll:gameRandom('defense')};
}
function trainingProgressText(t){return `残り${Math.max(0,(t.requiredDays??CountryCombat.trainingDays(t.purpose,state.levels[t.school],state.difficulty))-(t.progress||0))}訓練日`;}
function activateTraining(){
  const budgets=state.monthBudget?.budgets||state.budgetAmounts;
  state.characters.forEach(c=>{if(c.task?.startedOn===recoveryDate() && !c.task.progress)c.task=null;});
  const supported=new Set(state.characters.map((c,id)=>({c,id,t:state.trainingPolicy[id]})).filter(({c,t})=>t && !c.task && !recovering(c) && !onExpedition(c) && t.slot<trainingLimit(t.school,budgets) && !(c.qualified && c.cp>0 && c.skills.includes(trainingSkill(t.school)))).sort((a,b)=>Number(b.c.cp===0)-Number(a.c.cp===0)||a.id-b.id).slice(0,CountryBalance.clinicCapacity(state.levels[5],budgets?.[5]||0)).map(({id})=>id));
  const occupied=new Set(state.characters.filter(c=>c.task).map(c=>`${c.task.school}:${c.task.slot}`));
  state.characters.forEach((c,id)=>{
    if(c.task)return;
    const t=state.trainingPolicy[id];
    if(recovering(c) || (t && c.qualified && c.cp>0 && c.skills.includes(trainingSkill(t.school)))){state.trainingPolicy[id]=null;c.task=null;return;}
    if(!t || t.slot>=trainingLimit(t.school,budgets) || occupied.has(`${t.school}:${t.slot}`) || onExpedition(c))return;
    const purpose=trainingPurpose(c);
    c.task={...t,purpose,skill:trainingSkill(t.school),requiredDays:CountryCombat.trainingDays(purpose,state.levels[t.school],state.difficulty,supported.has(id)),progress:0,lastTrainingDate:recoveryDate(),startedOn:recoveryDate(),difficulty:state.difficulty,supported:supported.has(id)};
    occupied.add(`${t.school}:${t.slot}`);
  });
  state.trainingDraft=copyTraining(state.trainingPolicy);updateTrainingUsed();
}
function trainingLimit(i,budgets=state.budgetAmounts||state.budgetPolicy?.amounts){return CountryBalance.schoolCapacity(state.levels[i],budgets?.[i]??CountryBalance.upkeep(state.levels[i])+10*Math.min(5,state.levels[i]));}
function trainingPurpose(c) { return !c.qualified ? '初回訓練' : c.cp===0 ? '復帰訓練' : 'スキル習得'; }
function trainingSkill(i) { return `${soldierTypes[i]}訓練 ${state.levels[i]}`; }
function characterStatus(c) { return c.task ? `${facilities[c.task.school].name}で訓練中` : c.cp===0 ? recoveryStatus(c) : !c.qualified ? '未訓練' : '使用可能'; }
function rosterSelected(id,mode){const c=state.characters[id];return mode==='training'?state.trainingDraft[id]?.school===trainingChoice:mode==='defense'?c.defending:Object.hasOwn(formationDraft,id);}
function combatBlockReason(c){return [c.cp===0?'CP0・戦闘不能':!c.qualified?'未訓練':'',c.task?'訓練中':'',c.cp===0 && !c.task?recoveryStatus(c):'',onExpedition(c)?'遠征中':''].filter(Boolean).join(' / ');}
function rosterReason(id,mode){
  const c=state.characters[id];
  if(onExpedition(c))return '遠征中';
  if(mode==='training')return recovering(c)?recoveryStatus(c):c.qualified && c.cp>0 && c.skills.includes(trainingSkill(trainingChoice)) && state.trainingDraft[id]?.school!==trainingChoice?'習得済み':'';
  return [combatBlockReason(c),mode==='expedition' && c.defending?'防衛中':''].filter(Boolean).join(' / ');
}
function rosterStatus(id,mode){const c=state.characters[id];return rosterReason(id,mode)|| (rosterSelected(id,mode)?'配置済み':mode==='training' && c.cp===0?recoveryStatus(c):mode==='training' && state.trainingDraft[id]?'他の学校に配置中':'配置可能');}
function rosterPortrait(id){const c=state.characters[id];return `<span class="round-avatar ${c.cp===0?'portrait-cp-zero':''}">${expeditionImage(id%6,c.name)}</span>`;}
function equippedWeapon(id){const member=state.expeditions.flatMap(m=>m.members).find(m=>m.id===id);return member?.weapon??(state.characters[id].defending?state.defenseWeapons?.[id]:'');}
function rosterButton(id,mode,action='roster-place',extra=''){
  const c=state.characters[id],selected=rosterSelected(id,mode),reason=rosterReason(id,mode),weapon=mode==='expedition' && selected?formationDraft[id]:equippedWeapon(id);
  return `<article class="roster-entry"><button class="roster-character ${selected?'is-selected':''} ${reason?'roster-unavailable':''} ${c.cp===0?'exhausted-character':''}" data-action="${action}" data-mode="${mode}" data-index="${id}" ${extra} draggable="${!reason}" data-drag-character="${id}" aria-label="${c.name}：${rosterStatus(id,mode)}" ${reason?'disabled':''}><span class="roster-person">${rosterPortrait(id)}<span><b>${c.name}</b><span class="roster-cp ${c.cp===0?'cp-empty':''}">CP <strong>${c.cp}</strong> / ${c.maxCP}</span><small>${escapeHTML(c.personality||'')}</small>${reason?`<span class="roster-blocked-reason">${reason}</span>`:''}</span><span class="roster-mark">${selected?'✓':'＋'}</span></span>${expeditionStats(c,id,weapon)}${reason?'':`<span class="roster-status">${rosterStatus(id,mode)}</span>`}</button><button class="roster-skill-open" data-action="character-skills" data-index="${id}" aria-label="${c.name}のスキルを見る">スキル <b>${c.skills.length}</b> <span>詳細 ›</span></button></article>`;
}
function rosterList(mode){
  const ids=state.characters.map((c,id)=>id).sort((a,b)=>Number(!!rosterReason(a,mode))-Number(!!rosterReason(b,mode)) || (mode==='training'?Number(state.characters[a].cp===0)-Number(state.characters[b].cp===0):0) || (mode==='defense' && soldierSort==='personality'?(state.characters[a].personality||'').localeCompare(state.characters[b].personality||'','ja'):0) || a-b);
  return ids.map(id=>rosterButton(id,mode)).join('');
}
function assignedCharacter(id,mode){
  const c=state.characters[id],weapon=mode==='defense'?state.defenseWeapons?.[id]:mode==='expedition'?formationDraft[id]:'',w=weapons[weapon];
  const planned=state.trainingDraft[id];
  const reason=mode==='training'?[c.cp===0?recoveryStatus(c):'',trainingDraftSaved()?'訓練予定・確定済み':'訓練予定・未確定'].filter(Boolean).join(' / '):rosterReason(id,mode);
  const unavailable=mode==='training'?Boolean(combatBlockReason(c)):Boolean(reason);
  const remove=mode==='training'?'character-cancel':mode==='defense'?'defense-remove':'roster-remove',equip=mode==='defense'?'defense-weapon':'expedition-pick-weapon';
  return `<article class="assigned-character ${mode==='training'?'training-seat':''} ${c.cp===0?'exhausted-character':''} ${unavailable?'assigned-unavailable':''}" ${mode==='training'?`data-drop-slot="${planned.slot}" data-drop-school="${planned.school}"`:''}><button class="assigned-person" draggable="${!onExpedition(c)}" data-drag-character="${id}" data-action="character-detail" data-index="${id}" aria-label="${c.name}の詳細${reason?`：${reason}`:''}"><span class="roster-person">${rosterPortrait(id)}<span><b>${c.name}</b><span class="roster-cp ${c.cp===0?'cp-empty':''}">CP <strong>${c.cp}</strong> / ${c.maxCP}</span>${reason?`<span class="assigned-status ${c.cp===0?'status-critical':''}">${reason}</span>`:''}</span></span></button><button class="assigned-remove" data-action="${remove}" data-mode="${mode}" data-index="${id}" aria-label="${c.name}を配置から外す">×</button>${mode!=='training'?`<button class="assigned-weapon ${w?'':'unassigned weapon-missing'}" data-action="${equip}" data-index="${id}" aria-label="${c.name}の武器を設定">${w?expeditionImage(6+weaponTypes.indexOf(w.type),w.name,'weapon-photo'):'<span class="weapon-empty" aria-hidden="true">!</span>'}<span>${w?w.name:'武器が未設定！'}</span></button>`:''}${mode==='training'?'':expeditionStats(c,id,weapon)}</article>`;
}
function characterDetails(id,mode){
  const c=state.characters[id],weapon=mode==='expedition'?formationDraft[id]??equippedWeapon(id):equippedWeapon(id);
  return `<header class="roster-detail-header">${rosterPortrait(id)}<div><h2>${c.name}</h2><small>${rosterStatus(id,mode)}</small></div></header><p>${escapeHTML(c.personality||'')}</p><div class="details-cp"><span>CP</span><strong class="${c.cp===0?'cp-empty':''}">${c.cp}<small> / ${c.maxCP}</small></strong></div><div class="details-cp-bar"><span style="width:${c.cp/c.maxCP*100}%"></span></div>${c.cp===0?`<p class="recovery-status ${recovering(c)?'is-recovering':'recovery-ready'}">${c.task?'復帰訓練中・完了するとCP全回復':recoveryStatus(c)}</p>`:''}${mode!=='training' && !weapons[weapon]?'<p class="weapon-missing"><span aria-hidden="true">△ !</span> 武器が未設定！</p>':''}${expeditionStats(c,id,weapon)}<h3>習得済みスキル</h3>${learnedSkills(c)}`;
}
function placeRoster(id,mode){
  characterChoice=id;if(rosterReason(id,mode))return;
  if(rosterSelected(id,mode)){render();return;}
  if(mode==='defense')addDefense(id);
  else if(mode==='expedition'){formationDraft[id]='';render();}
  else {const slot=Array.from({length:trainingLimit(trainingChoice)},(_,n)=>n).find(n=>!state.trainingDraft.some(t=>t?.school===trainingChoice && t.slot===n));if(slot!==undefined)placeTraining(id,trainingChoice,slot);else render();}
}

function renderTraining() {
 const i=trainingChoice,c=state.characters[characterChoice],task=state.trainingDraft[characterChoice],limit=trainingLimit(i);
 const trainees=state.trainingDraft.map((task,id)=>({task,id})).filter(t=>t.task?.school===i);
 const skill=task?.skill||trainingSkill(i),saved=trainingDraftSaved();
 return `<section class="game-window budget-window training-window" aria-label="兵士育成">${budgetTabs()}<div class="training-body" role="tabpanel" id="panel-training" aria-labelledby="tab-training" tabindex="0">
 <nav class="school-tabs" aria-label="訓練する学校">${facilities.slice(0,3).map((f,n)=>`<button class="school-tab ${n===i?'selected-school':''}" data-action="training-select" data-index="${n}" aria-pressed="${n===i}"><span class="facility-icon">${f.icon}</span><span><b>${f.name}</b><small>Lv.${state.levels[n]}</small></span><span class="school-tab-count"><b>${state.trainingUsed[n]}</b> / ${trainingLimit(n)}<small>予定枠</small></span></button>`).join('')}</nav>
 <section class="training-board" aria-label="${state.day===1?'今月':'来月'}の訓練予定"><header><h2>${monthSettingsDate()}の訓練予定 <small>${saved?state.day===1?'確定済み · 今月の訓練中':'確定済み · 対象月に開始':state.day===1?'未確定 · 確定後、今月に開始':'未確定 · 確定後、対象月に開始'}</small></h2><strong>残り ${limit-trainees.length}<small> / ${limit}枠</small></strong></header><div class="training-slot-list">${Array.from({length:limit},(_,n)=>{const t=trainees.find(t=>t.task.slot===n);return t?assignedCharacter(t.id,'training'):`<button class="round-slot empty-slot" data-drop-slot="${n}" data-drop-school="${i}" data-action="training-slot-pick" data-index="${i}" aria-label="訓練の空き枠${n+1}に配置"><span class="round-avatar">＋</span><small>空き枠 ${n+1}</small></button>`;}).join('')}</div></section>
 <div class="character-area"><section class="candidate-panel" aria-label="訓練候補"><header><h2>キャラクター <small>30人</small></h2></header><div class="candidate-list roster-grid" data-drop-roster="true">${rosterList('training')}</div></section>
 <aside class="character-details" aria-label="キャラクター詳細">${characterDetails(characterChoice,'training')}${c.task?`<div class="details-training"><h3>今月は訓練中</h3><p>${facilities[c.task.school].name} · ${trainingProgressText(c.task)}</p></div>`:''}<div class="details-training">${recovering(c)?`<h3>学校への復帰準備</h3><b>${recoveryStatus(c)}</b><p>3日間の療養を終えると復帰訓練に配置できる。</p>`:`<h3>${task?`${state.day===1?'今月':'来月'}の訓練予定`:'この学校での訓練'}</h3><b>${task?.purpose||trainingPurpose(c)}</b><p>${facilities[task?.school??i].name}${task?` · 枠${task.slot+1}`:''}<br>${skillLink(skill)}</p>${task?`<small>${saved?'確定済み':'未確定'} · ${monthSettingsDate()}に開始</small>`:''}${(task?.purpose||trainingPurpose(c))==='復帰訓練'?'<small>完了するとCP全回復・使用可能に復帰</small>':''}`}</div></aside></div>
 </div>${monthlyFooter()}</section>`;
}
function updateTrainingUsed(){state.trainingUsed=[0,1,2,3].map(i=>state.trainingDraft.filter(t=>t?.school===i).length);}
function placeTraining(id,school,slot){
 const c=state.characters[id];if(!c || recovering(c) || onExpedition(c) || school<0 || school>2 || slot<0 || slot>=trainingLimit(school))return;
 if(c.qualified && c.cp>0 && c.skills.includes(trainingSkill(school)) && state.trainingDraft[id]?.school!==school){toast('この学校のスキルは習得済みです');return;}
 const target=state.trainingDraft.findIndex(t=>t?.school===school && t.slot===slot);if(target===id)return;
 const old=state.trainingDraft[id];
 if(target>=0)state.trainingDraft[target]=old?{...old,purpose:trainingPurpose(state.characters[target]),skill:trainingSkill(old.school)}:null;
 state.trainingDraft[id]={school,slot,purpose:trainingPurpose(c),skill:trainingSkill(school)};characterChoice=id;updateTrainingUsed();save();render();
}
function startTraining(i,slot){
 if(i===3 || state.trainingDraft[characterChoice])return;
 if(slot===undefined){slot=0;while(state.trainingDraft.some(t=>t?.school===i && t.slot===slot))slot++;}
 placeTraining(characterChoice,i,slot);
}

function advanceTraining() {
 if(state.characters.some(c=>c.task && !Number.isInteger(c.task.requiredDays)))initializeCombatState();
 state.characters.forEach((c,id)=>{
   if(!c.task || recovering(c))return;
   const t=c.task,elapsed=Math.max(0,recoveryDate()-(t.lastTrainingDate??recoveryDate()));
   t.progress=(t.progress||0)+elapsed;t.lastTrainingDate=recoveryDate();if(t.progress<t.requiredDays)return;
   if(!c.skills.includes(t.skill))c.skills.push(t.skill);
   if(t.purpose==='復帰訓練'){c.cp=c.maxCP;delete c.recoveryDays;delete c.recoveryDate;}
   c.qualified=true;c.task=null;
   if(state.trainingPolicy[id]?.school===t.school && state.trainingPolicy[id]?.skill===t.skill)state.trainingPolicy[id]=null;
   if(state.trainingDraft[id]?.school===t.school && state.trainingDraft[id]?.skill===t.skill && !state.nextTraining?.[id])state.trainingDraft[id]=null;
   const text=`${c.name}の${t.purpose}が完了`;state.completed.push(text);log(text);
 });updateTrainingUsed();
}

const weaponTypes=['剣','盾','杖','聖書','魔導書','ロザリオ'];
const weaponBonuses=[{STR:4},{HP:12},{INT:4},{FAI:4},{INT:5,AGI:-1},{FAI:3,AGI:1}];
const weapons=[];
function syncWeapons(){
 if(!state.weaponInventory){
  state.weaponInventory=weaponTypes.map((type,n)=>({name:['暁の剣','守護の盾','星詠みの杖','黎明の聖書','深蒼の魔導書','祈りのロザリオ'][n],type,level:1,power:18+n,bonus:Object.fromEntries(Object.entries(weaponBonuses[n]).map(([key,value])=>[key,value*2])),description:'表示確認用のユニーク装備。',sample:true}));
 }
 weapons.splice(0,weapons.length,...weaponTypes.map((type,n)=>({name:`訓練用${type}`,type,power:10,bonus:weaponBonuses[n],training:true,level:0,description:'人数に制限なく装備できる基本の訓練用武器。'})),...state.weaponInventory);
 if(!state.weaponRulesVersion){
  const convert=n=>[0,0,0,0,2][Number(n)]??0;
  for(const key of ['defenseWeapons','expeditionPlan'])if(state[key])for(const id in state[key])if(state[key][id]!=='')state[key][id]=convert(state[key][id]);
  for(const mission of [...state.expeditions,...(state.expeditionReports||[])])mission.members.forEach(m=>m.weapon=convert(m.weapon));
  for(const id in formationDraft)if(formationDraft[id]!=='')formationDraft[id]=String(convert(formationDraft[id]));
  state.weaponRulesVersion=1;
 }
}
syncWeapons();
initializeCombatState();
function weaponUsers(index){
 if(weapons[index]?.training)return [];
 const users=[];
 for(const [id,weapon] of Object.entries(state.defenseWeapons||{}))if(state.characters[id]?.defending && String(weapon)===String(index))users.push({id:Number(id),place:'防衛'});
 for(const mission of state.expeditions)for(const member of mission.members)if(String(member.weapon)===String(index))users.push({id:member.id,place:'遠征中'});
 for(const [id,weapon] of Object.entries(formationDraft))if(weapon!=='' && String(weapon)===String(index))users.push({id:Number(id),place:'遠征準備'});
 return users;
}
function weaponUseText(index){return weapons[index].training?'無制限':weaponUsers(index).map(u=>`${state.characters[u.id].name} · ${u.place}`).join(' / ')||'未装備';}
function weaponAvailable(index,id){return weapons[index] && (weapons[index].training || !weaponUsers(index).some(u=>u.id!==id));}
function productionCount(amount=state.budget?.[6]||0){return CountryBalance.productionCount(state.levels[6],amount,state.resources,state.weaponInventory.length);}
function productionSettlement(){
 const b=state.monthBudget,level=b?.workshopLevel??state.levels[6],amount=b?.budgets[6]||0;
 const count=CountryBalance.productionCount(level,amount,state.resources,state.weaponInventory.length);
 return {count,materials:CountryBalance.productionMaterials(count),refund:Math.max(0,amount-CountryBalance.upkeep(level)-count*CountryBalance.rules.productionCost)};
}
function monthEarnings(){
 const b=state.monthBudget,production=productionSettlement();
 return {production,market:CountryBalance.marketIncome(b?.marketLevel??0,b?.budgets[4]||0,b?.difficulty||state.difficulty)};
}
function produceWeapons(){
 const {count,materials,refund}=productionSettlement(),level=Math.min(5,state.monthBudget?.workshopLevel??state.levels[6]);
 state.resources=state.resources.map((n,i)=>n-materials[i]+(i?0:refund));
 state.productionRefund=refund;const made=[];
 for(let n=0;n<count;n++){
  const type=Math.floor(gameRandom('weapon')*6),serial=state.weaponInventory.length+1;
  const item={name:`${['暁','霧','星','月','蒼天','銀光'][Math.floor(gameRandom('weapon')*6)]}の${weaponTypes[type]}・${serial}`,type:weaponTypes[type],level,power:10+level*8+Math.floor(gameRandom('weapon')*5),bonus:Object.fromEntries(Object.entries(weaponBonuses[type]).map(([key,value])=>[key,value*(level+1)+Math.floor(gameRandom('weapon')*3)])),description:'工房で生産された一点ものの装備。'};
  state.weaponInventory.push(item);made.push(6+state.weaponInventory.length-1);
 }
 syncWeapons();if(made.length)state.completed.push(`工房で装備${made.length}点が完成（木材${materials[1]}・鉱石${materials[2]}）`);
 if(refund)state.completed.push(`未生産の活動予算${refund}を国庫へ返還`);return made;
}
function weaponRow(w,i,action='weapon-select',extra=''){
 return `<button class="arsenal-row ${i===weaponChoice?'is-selected':''}" data-action="${action}" data-index="${i}" ${extra}>${expeditionImage(6+weaponTypes.indexOf(w.type),w.name,'weapon-photo')}<span><b>${w.name}</b><small>${w.type} · ${w.training?'訓練用':`製作Lv.${w.level}`}</small></span><span class="arsenal-bonus">${Object.entries(w.bonus).map(([key,value])=>`${key} ${value>0?'+':''}${value}`).join(' · ')}</span><small>${weaponUseText(i)}</small></button>`;
}
function soldierTabs() {
  return `<div class="window-title budget-tab-header"><div class="budget-tabs" role="tablist" aria-label="兵士と武器">${[['members','兵士一覧'],['weapons','武器一覧']].map(([key,name])=>`<button role="tab" aria-selected="${soldierPage===key}" data-action="soldier-page" data-page="${key}">${name}</button>`).join('')}</div><button class="${returnToDefense?'defense-return':'close'}" data-action="navigate-kingdom" aria-label="${returnToDefense?'防衛戦へ戻る':'城下町へ戻る'}">${returnToDefense?'防衛戦へ戻る':'×'}</button></div>`;
}
function renderWeapons() {
 const w=weapons[weaponChoice]||weapons[0],count=productionCount();
 return `<section class="game-window budget-window soldiers-window" aria-label="武器一覧">${soldierTabs()}<div class="arsenal-production"><div><small>工房 ${state.levels[6]?`Lv.${state.levels[6]}`:'未建設'}</small><b>今月の生産予定 <strong>${count}</strong>点</b></div><span>活動予算10＋木材3＋鉱石2で1本 · 月Lv本・保管60本まで<br><small>材料不足分の活動予算は返還 · 月末に完成</small></span><button data-action="weapon-budget">生産予算を確認</button></div><div class="weapon-body"><section class="weapon-roster arsenal-list" aria-label="所持武器"><h3>ユニーク装備 <small>${state.weaponInventory.length}点 · １点につき１人</small></h3>${weapons.slice(6).map((item,n)=>weaponRow(item,n+6)).join('')}<h3>訓練用武器 <small>６種すべて無制限</small></h3>${weapons.slice(0,6).map((item,n)=>weaponRow(item,n)).join('')}</section><aside class="character-details arsenal-detail" aria-label="武器の詳細">${expeditionImage(6+weaponTypes.indexOf(w.type),w.name,'weapon-photo')}<h2>${w.name}</h2><p>${w.type} · ${w.training?'訓練用':`製作Lv.${w.level}`}</p><p>${w.description}</p>${abilityKeys.map(key=>`<div class="weapon-stat"><span>${key}</span><b>${w.bonus[key]>0?'+':''}${w.bonus[key]||'—'}</b></div>`).join('')}<p class="tax-accent">${weaponUseText(weaponChoice)}</p><small>${w.training?'複数の兵士に同時に装備できる。':'この装備を同時に持てるのは１人だけ。'}${w.sample?' 初期装備は確認用の仮データ。':''}</small></aside></div></section>`;
}
const expeditionDestinations = [
  {name:'北の砦',description:'丘陵に築かれた砦',terrain:'丘陵・城壁',feature:'高低差が大きく、城壁がある'},
  {name:'東の森林',description:'街道沿いの深い森',terrain:'森林',feature:'遮蔽物が多く、見通しが悪い'},
  {name:'南の平原',description:'見通しのよい草原',terrain:'平原',feature:'遮蔽物が少ない開けた戦場'}
].map((d,i)=>({...d,duration:CountryCombat.destinations[i].duration,enemy:`${CountryCombat.destinations[i].enemies}人`,days:`往復 ${CountryCombat.destinations[i].duration}日`,get loot(){return CountryCombat.reward(i,combatProfile());},get reward(){return expeditionRewardText(this.loot);}}));
function expeditionRewardText(loot){return loot.map((n,i)=>n?`${['国庫','木材','鉱石'][i]} ＋${n}`:'').filter(Boolean).join('・')||'なし';}
function memberCombatPower({id,weapon}){const c=state.characters[id];return CountryCombat.power(Object.fromEntries(abilityKeys.map((key,n)=>[key,expeditionAbilities(c,id,weapon)[n]])),weapons[weapon],c.skills);}
function expeditionEstimate(members,target){return {...CountryCombat.estimate(members.reduce((sum,m)=>sum+memberCombatPower(m),0),CountryCombat.enemyPower(target,combatProfile())),difficulty:state.difficulty,cp:combatProfile().cp,loot:CountryCombat.reward(target,combatProfile()),supplies:CountryCombat.supplies(members.length,CountryCombat.destinations[target].duration,combatProfile())};}
function expeditionBlockReason(target){return state.expeditions.some(m=>m.target===target)?'遠征隊が進軍中':Math.max(0,(state.expeditionReadyOn?.[target]||0)-recoveryDate())?`再出発まであと${state.expeditionReadyOn[target]-recoveryDate()}日`:'';}
function combatEstimateHTML(combat){return Number.isFinite(combat.power)?`<div class="mission-info"><span>味方戦力 <b>${Math.round(combat.power)}</b></span><span>敵戦力 <b>${Math.round(combat.enemyPower)}</b></span><span>国家UIの試算成功率 <b>${Math.round(combat.chance*100)}%</b></span></div><small class="muted">国家UI独自の抽象試算。Unity本戦の勝率とは異なる。</small>`:'<p class="muted">旧記録の遠征・報酬なし</p>';}
function defenseMembers(){return state.characters.map((c,id)=>({c,id})).filter(({c,id})=>c.defending && c.qualified && c.cp>0 && !c.task && !onExpedition(c) && weapons[state.defenseWeapons?.[id]]).map(({id})=>({id,weapon:state.defenseWeapons[id],before:state.characters[id].cp}));}
function defenseCombat(attack,members){
  const profile=combatProfile();
  attack.combat??={difficulty:state.difficulty,enemyPower:attack.enemy*90*profile.enemy,cp:profile.cp,roll:gameRandom('raid',2),loss:CountryCombat.raidLoss(taxIncome(),Number.MAX_SAFE_INTEGER,state.difficulty)};
  return {...attack.combat,...CountryCombat.estimate(members.reduce((sum,m)=>sum+memberCombatPower(m),0),attack.combat.enemyPower),kind:'defense'};
}
function maybeDefenseAttack(){
  if(state.defenseAttack)return;
  if((state.defenseCooldown||0)>0){state.defenseCooldown--;save();return;}
  if(gameRandom('raid',0)<(state.defenseChance===undefined?combatProfile().raidChance:state.defenseChance/100))triggerDefenseAttack();
}
function triggerDefenseAttack(){
  if(state.defenseAttack){showDefenseAttack();return;}
  const interruptedMonth=Boolean(monthAdvance);
  if(interruptedMonth)suspendedMonthAdvance={year:state.year,month:state.month};
  stopTime();stopMonthAdvance();resumeAfterModal=false;
  const profile=combatProfile(),enemy=3+Math.floor(gameRandom('raid',1)*6);
  state.defenseAttack={enemy,date:dateText(),combat:{difficulty:state.difficulty,enemyPower:enemy*90*profile.enemy,cp:profile.cp,roll:gameRandom('raid',2),loss:CountryCombat.raidLoss(taxIncome(),Number.MAX_SAFE_INTEGER,state.difficulty)}};state.defenseCooldown=profile.raidCooldown;
  log('城下町に敵が接近。防衛戦が発生。');save();render();showAttackArrival(interruptedMonth);
}
function showDefenseAttack(){
  clearTimeout(attackArrivalTimer);attackArrivalTimer=null;modal.classList.remove('attack-cutin');
  const attack=state.defenseAttack;if(!attack)return;stopTime();resumeAfterModal=false;
  const members=defenseMembers(),ready=new Set(members.map(m=>m.id));
  const combat=defenseCombat(attack,members);
  const assigned=state.characters.map((c,id)=>({c,id})).filter(({c})=>c.defending);
  modal.classList.add('defense-dialog');
  const roster=assigned.map(({c,id})=>{const w=weapons[state.defenseWeapons?.[id]],reason=ready.has(id)?'':[combatBlockReason(c),!w?'武器が未設定！':''].filter(Boolean).join(' / ');return `<div class="mission-member ${reason?'defense-unavailable':''}">${expeditionImage(id%6,c.name)}<span><b>${c.name}</b><small class="${w?'':'weapon-missing'}">${w?.name||'武器が未設定！'}</small>${reason?`<small class="defense-warning ${w?'':'weapon-missing'}">${reason}</small>`:''}</span>${ready.has(id)?combatCPPreview(c,combat):`<span class="mission-cp"><small>CP</small><b>${c.cp} / ${c.maxCP}</b></span>`}</div>`;}).join('');
  const loss=Math.min(state.resources[0],combat.loss??CountryCombat.raidLoss(taxIncome(),state.resources[0],combat.difficulty));
  openModal('城下町に敵が接近',`<div class="defense-alert-heading"><span aria-hidden="true">⚔</span><div><small>${attack.date} · 時間停止中</small><h3>城下町を防衛せよ</h3></div></div><div class="defense-forces"><div><small>敵の人数</small><strong>${attack.enemy}<small>人</small></strong></div><div><small>防衛メンバー</small><strong>${assigned.length}<small>人</small></strong></div></div>${combatEstimateHTML(combat)}<p class="spend-timing">${spendClock}戦闘終了時にCPを消費 · 敗北時 ${resourceIcons[0]}国庫 −${loss}</p><h3 class="defense-roster-title">防衛メンバー <small>出撃可能 ${members.length}人</small></h3>${assigned.length?`<div class="mission-members" aria-label="防衛メンバー一覧">${roster}</div>`:'<p class="defense-warning">防衛メンバーが未配置。敗北を受け入れるか、メンバーを編集しておくれ。</p>'}${!members.length && assigned.length?'<p class="defense-warning">出撃できる兵士がいない。敗北を受け入れるか、メンバーと装備を確認しておくれ。</p>':''}<p class="muted">斜線部分のCP消費は勝敗で変わる。防衛戦が終わるまで時間は停止する。</p>`, `<button data-action="defense-organize">メンバーを編集</button><button class="primary" data-action="defense-fight">${members.length?'戦闘':'敗北を受け入れる'}</button>`);
}
function resolveDefenseAttack(){
  const attack=state.defenseAttack;if(!attack)return false;
  const selected=defenseMembers(),combat=defenseCombat(attack,selected);
  const victory=selected.length>0 && combat.roll<combat.chance,outcome=victory?'victory':'defeat';
  const members=selected.map(m=>{const c=state.characters[m.id];c.cp=Math.max(0,c.cp-CountryCombat.cpLoss({...combat,outcome,maxCP:c.maxCP}));if(!c.cp)startRecovery(c);return {...m,after:c.cp};});
  const resourceBefore=[...state.resources],loss=victory?0:Math.min(state.resources[0],combat.loss??CountryCombat.raidLoss(taxIncome(),state.resources[0],combat.difficulty));state.resources[0]-=loss;
  state.defenseReport={...attack,combat,members,loss,resourceBefore,outcome:victory?'防衛成功':'防衛失敗',unread:true};state.defenseAttack=null;
  log(`城下町の防衛戦：${state.defenseReport.outcome}。`);save();render();showDefenseResult();
  return true;
}
function showDefenseResult(){
  const r=state.defenseReport;if(!r)return;modal.classList.add('defense-dialog');
  openModal('防衛戦の結果',`<header class="mission-result"><small>${r.date}</small><h3>${r.outcome}</h3></header><p>敵 ${r.enemy}人 · 防衛 ${r.members.length}人</p>${r.loss?resourceChanges([-r.loss],[0],r.resourceBefore||[state.resources[0]+r.loss,...state.resources.slice(1)]):'<p class="tax-accent">国庫の損失なし</p>'}${r.members.length?`<h3>兵士のCP変化</h3><div class="mission-members">${expeditionMemberRows(r.members,true)}</div>`:''}<p class="muted">${suspendedMonthAdvance?'確認すると月末までの進行を再開する。':'確認後、時間は手動で再開できる。'}</p>`, `<button class="primary" data-action="defense-report-read">${suspendedMonthAdvance?'結果を確認して月末まで再開':'結果を確認'}</button>`);
  showingDefenseResult=true;
}

function selectedExpedition(){return expeditionPage==='status'?state.expeditions.find(m=>m.id===expeditionChoice):null;}
function unreadExpeditionReports(){return (state.expeditionReports||[]).filter(r=>!r.read).length;}
function expeditionStage(m){return m.retreating?'撤退・帰還中':m.elapsed<Math.ceil(m.total/2)?'進軍中':m.elapsed===Math.ceil(m.total/2)?'戦闘中':'帰還中';}
function expeditionHeader(){return `<div class="window-title budget-tab-header expedition-main-header"><div class="budget-tabs" role="tablist" aria-label="遠征"><button role="tab" aria-selected="${expeditionPage==='status'}" data-action="expedition-overview">遠征状況 <small>${state.expeditions.length}部隊</small></button><button role="tab" aria-selected="${expeditionPage==='new'}" data-action="expedition-new">＋ 新しい遠征</button></div><button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div>`;}
function renderExpeditionOverview(){
  const reports=(state.expeditionReports||[]).map((r,index)=>({...r,index})).filter(r=>!r.read);
  return `<section class="game-window budget-window expedition-window expedition-overview" aria-label="全部隊の遠征状況">${expeditionHeader()}<div class="expedition-overview-body"><h2>進行中の遠征</h2>${state.expeditions.length?state.expeditions.map(m=>`<div class="expedition-status-row"><div><b>${expeditionDestinations[m.target].name}</b><small>${state.characters[m.members[0].id].name}隊 · ${m.members.length}人</small></div><div><span class="expedition-stage">${expeditionStage(m)}</span><div class="mission-track"><span style="width:${m.elapsed/m.total*100}%"></span></div><small>進軍 → 戦闘 → 帰還</small></div><div class="expedition-days"><small>帰還まで</small><strong>${m.remaining}</strong> 日</div><button data-action="expedition-detail" data-index="${m.id}">部隊を確認 →</button></div>`).join(''):'<p class="muted">遠征中の部隊はない。「＋ 新しい遠征」から部隊を編成できる。</p>'}${reports.length?`<h2 class="expedition-reports-heading">帰還報告 <small>未確認 ${unreadExpeditionReports()}件</small></h2>${reports.map((r,i)=>`<div class="expedition-status-row expedition-return-row"><div><b>${expeditionDestinations[r.target].name}</b><small>${r.date} · ${r.members.length}人</small></div><div><span class="${r.read?'muted':'expedition-unread'}">${r.read?'確認済み':'帰還報告あり'}</span><small>兵士は帰還済み</small></div><div><b>${{victory:'勝利',defeat:'敗北',retreat:'撤退'}[r.outcome]}</b></div><button data-action="expedition-report" data-report="${r.index}">報告を確認 →</button></div>`).join('')}`:''}</div><div class="window-bottom"><span>遠征中 ${state.expeditions.length}部隊 · ${state.expeditions.reduce((n,m)=>n+m.members.length,0)}人</span><button class="primary" data-action="expedition-new">＋ 新しい遠征</button></div></section>`;
}

function onExpedition(c) {return state.expeditions.some(e=>e.members.some(m=>state.characters[m.id]===c));}

function cpChange(before,after,max,uncertain=0) {
  const lost=Math.max(0,before-after-uncertain),empty=after===0;
  return `<span class="cp-change ${empty?'cp-risk':''}" aria-label="CP ${before}から${after}${uncertain?`〜${after+uncertain}`:''}"><span class="cp-change-heading"><span class="cp-icon">${cpIcon}</span><b>${before} <span class="flow-arrow">${flowArrow}</span> ${after}${uncertain?`〜${after+uncertain}`:''}</b><strong class="change-amount"><span class="change-glyph">${lost||uncertain?lossGlyph:''}</span>${lost}${uncertain?`〜${before-after}`:''}</strong></span><span class="change-track" aria-hidden="true"><span class="change-retained" style="width:${Math.round(after/max*100)}%"></span>${uncertain?`<span class="change-uncertain" style="width:${Math.round(uncertain/max*100)}%"></span>`:''}<span class="change-used" style="width:${Math.round(lost/max*100)}%"></span></span>${empty?`<small class="cp-empty-warning"><span class="short-warning" aria-hidden="true">△ !</span>${uncertain?'CP0になる可能性あり・3日療養後に復帰訓練':'CP0で戦闘不能・訓練が必要（3日療養後）'}</small>`:''}</span>`;
}
function combatCPPreview(c,combat){
  if(combat.outcome==='retreat')return cpChange(c.cp,Math.max(0,c.cp-CountryCombat.cpLoss({...combat,maxCP:c.maxCP})),c.maxCP);
  const after=Math.max(0,c.cp-CountryCombat.cpLoss({...combat,outcome:'defeat',maxCP:c.maxCP}));
  const best=Math.max(0,c.cp-CountryCombat.cpLoss({...combat,outcome:'victory',maxCP:c.maxCP}));
  return cpChange(c.cp,after,c.maxCP,best-after);
}
function expeditionMemberRows(members,report=false,preview=null) {
  return members.map(m=>{
    const c=state.characters[m.id],w=weapons[m.weapon],before=report?m.before:c.cp;
    return `<div class="mission-member ${report && m.after===0?'member-exhausted':''}">${expeditionImage(m.id%6,c.name)}<span><b>${c.name}</b><small class="${w?'':'weapon-missing'}">${w?.name||'武器が未設定！'}</small></span>${report?cpChange(before,m.after,c.maxCP):preview?combatCPPreview(c,preview):`<span class="mission-cp"><small>CP</small><b>${c.cp} / ${c.maxCP}</b></span>`}</div>`;
  }).join('');
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
  const d=expeditionDestinations[expeditionTarget],party=members.map(id=>({id,weapon:Number(formationDraft[id])})),combat={...expeditionEstimate(party,expeditionTarget),duration:d.duration},blocked=expeditionBlockReason(expeditionTarget),testOnly=['victory','defeat'].includes(state.mockExpeditionOutcome);
  const loot=testOnly?[0,0,0]:combat.loot;
  openModal('遠征の出発確認', `<div class="mission-destination"><h3>${d.name}</h3><span>${d.days} · 敵 ${d.enemy}</span></div>${combatEstimateHTML(combat)}<div class="mission-reward"><small>${testOnly?'開発用の勝敗指定・報酬なし':'勝利時の報酬'}</small><b>${expeditionRewardText(loot)}</b></div><p class="spend-timing">${spendClock}出発時に補給費 ${resourceIcons[0]}国庫 −${combat.supplies}</p>${resourceChanges([-combat.supplies],[0])}<p class="spend-timing">${spendClock}帰還時にCPを消費 · 斜線部分は勝敗で変動</p><div class="mission-members">${expeditionMemberRows(party,false,combat)}</div><p class="muted">出発後は帰還まで、防衛・訓練・他の遠征に配置できない。同じ場所への再出発は帰還から${CountryCombat.cooldown}日後。補給費は撤退時も戻らない。</p>${blocked?`<p class="stat-down">${blocked}</p>`:state.resources[0]<combat.supplies?'<p class="stat-down">補給費の国庫が不足</p>':''}`, `<button data-action="close">編成に戻る</button><button class="primary" data-action="expedition-depart" ${blocked || state.resources[0]<combat.supplies?'disabled':''}>遠征に出発</button>`);
}
function departExpedition(){
  if(expeditionTarget===null || expeditionBlockReason(expeditionTarget))return false;
  const members=Object.keys(formationDraft).map(Number);
  if(!members.length || members.some(id=>!expeditionEligible(state.characters[id]) || !weapons[formationDraft[id]]))return false;
  const d=expeditionDestinations[expeditionTarget],party=members.map(id=>({id,weapon:Number(formationDraft[id]),before:state.characters[id].cp}));
  const combat={...expeditionEstimate(party,expeditionTarget),duration:d.duration,roll:gameRandom('expedition'),testOnly:['victory','defeat'].includes(state.mockExpeditionOutcome)};
  if(state.resources[0]<combat.supplies)return false;
  state.resources[0]-=combat.supplies;
  if(combat.testOnly)combat.loot=[0,0,0];
  const outcome=combat.testOnly?state.mockExpeditionOutcome:combat.roll<combat.chance?'victory':'defeat';
  state.expeditionPlan={...formationDraft};state.expeditionTarget=expeditionTarget;
  state.expeditions.push({id:state.nextExpeditionId++,target:expeditionTarget,total:d.duration,remaining:d.duration,elapsed:0,members:party,combat,outcome,departed:`${state.year}年${state.month}月${state.day}日`});
  formationDraft={};expeditionChoice=null;expeditionPage="status";
  log(`${d.name}へ${members.length}人が出発。補給費 ${combat.supplies}を支出。`);save();closeModal();render();toast('遠征に出発。日付を進めると帰還まで進行する');return true;
}
function renderExpeditionProgress(){
  const m=selectedExpedition();if(!m)return renderExpeditionOverview();const d=expeditionDestinations[m.target],stage=m.retreating?'撤退・帰還中':m.elapsed===0?'進軍中':m.elapsed<Math.ceil(m.total/2)?'進軍中':m.elapsed===Math.ceil(m.total/2)?'戦闘中':'帰還中';
  const percent=m.elapsed/m.total*100,loot=m.retreating || m.combat.testOnly?[0,0,0]:m.combat.loot;
  return `<section class="game-window budget-window expedition-window" aria-label="遠征状況">${expeditionHeader()}<button class="expedition-back" data-action="expedition-overview">← 全部隊の状況</button><div class="mission-content"><header class="mission-status"><div><small>${m.departed} 出発</small><h2>${d.name}</h2><b>${stage}</b></div><div class="mission-remaining"><small>帰還まで</small><strong>${m.remaining}<small>日</small></strong></div></header><div class="mission-track" aria-label="遠征の進行 ${Math.round(percent)}％"><span style="width:${percent}%"></span></div><div class="mission-stages"><span>出発</span><span>戦闘</span><span>帰還</span></div><div class="mission-info"><span>敵 ${d.enemy}</span><span>${d.terrain}</span><span>勝利報酬 ${expeditionRewardText(loot)}</span><span>補給費 ${m.combat.supplies}（支払済み）</span></div>${combatEstimateHTML(m.combat)}<h3>遠征メンバー ${m.members.length}人</h3><div class="mission-members">${expeditionMemberRows(m.members,false,{duration:m.total,...m.combat,...(m.retreating?{outcome:'retreat',elapsed:m.retreatElapsed}:{})})}</div><p class="muted">上の時間操作で日付を進めると遠征も進行する。帰還時に時間が停止する。勝率・報酬・CP消費は出発時の難易度で確定済み。</p></div><div class="window-bottom"><button data-action="navigate-kingdom">城下町へ戻る</button><div class="actions"><button data-action="expedition-map">マップ情報</button><button data-action="expedition-retreat" ${m.retreating?'disabled':''}>${m.retreating?'撤退中':'撤退を指示'}</button></div></div></section>`;
}
function confirmExpeditionRetreat(){
  const m=selectedExpedition();if(!m || m.retreating)return;
  openModal('遠征から撤退', `<p>報酬を受け取らず帰還へ切り替える。出発時の補給費は戻らない。兵士は帰還まで配置できない。</p><p class="spend-timing">${spendClock}帰還時のCP消費</p><div class="mission-members">${expeditionMemberRows(m.members,false,{...m.combat,outcome:'retreat',elapsed:m.elapsed})}</div>`, '<button data-action="close">遠征を続ける</button><button data-action="expedition-retreat-submit">撤退する</button>');
}
function retreatExpedition(){
  const m=selectedExpedition();if(!m || m.retreating)return;
  m.retreating=true;m.outcome='retreat';m.retreatElapsed=m.elapsed;m.remaining=Math.max(1,Math.min(m.remaining,Math.ceil(m.elapsed/2)));m.total=m.elapsed+m.remaining;
  log(`${expeditionDestinations[m.target].name}から撤退を開始。`);save();closeModal();render();
}
function advanceExpedition(){
  let returned=0;
  for(const m of [...state.expeditions]) {
    m.elapsed++;m.remaining--;
    if(m.remaining>0)continue;
    returned++;
    const d=expeditionDestinations[m.target],combat={duration:m.total,...m.combat},loot=m.outcome==='victory' && !combat.testOnly?combat.loot:[0,0,0];
    const members=m.members.map(member=>{const c=state.characters[member.id],loss=CountryCombat.cpLoss({...combat,outcome:m.outcome,elapsed:m.retreatElapsed??m.elapsed,maxCP:c.maxCP});c.cp=Math.max(0,c.cp-loss);if(!c.cp)startRecovery(c);return {...member,after:c.cp};});
    const resourceBefore=[...state.resources];state.resources=state.resources.map((v,i)=>v+loot[i]);
    state.expeditionReadyOn[m.target]=recoveryDate()+CountryCombat.cooldown;
    const report={read:false,target:m.target,outcome:m.outcome,members,loot,combat,resourceBefore,date:`${state.year}年${state.month}月${state.day}日`};
    state.expeditionReports=[report,...(state.expeditionReports||[])].slice(0,12);state.expeditionUnread=true;state.expeditions=state.expeditions.filter(e=>e.id!==m.id);
    const label={victory:'勝利',defeat:'敗北',retreat:'撤退'}[m.outcome];state.completed.push(`${d.name}の遠征隊が帰還（${label}）`);log(`${d.name}から帰還。${label}、国庫＋${loot[0]}。`);toast('遠征隊が帰還。遠征状況から報告を確認できる');
  }
  return returned;
}
function showExpeditionReport(index=0){
  const r=state.expeditionReports?.[index];if(!r)return;expeditionReportChoice=index;
  const label={victory:'勝利',defeat:'敗北',retreat:'撤退'}[r.outcome];
  openModal('遠征の帰還報告', `<header class="mission-result"><small>${r.date} 帰還</small><h3>${expeditionDestinations[r.target].name}</h3><strong class="${r.outcome==='defeat'?'stat-down':'tax-accent'}">${label}</strong></header><div class="mission-reward"><small>獲得報酬${r.combat?.testOnly?' · 開発用の勝敗指定':''}</small><b>${expeditionRewardText(r.loot)}</b></div>${resourceChanges(r.loot,[0,1,2],r.resourceBefore||state.resources.map((n,i)=>n-r.loot[i]))}<p class="muted">出発時の補給費 ${resourceIcons[0]}国庫 −${r.combat?.supplies||0}（支払済み）</p><h3>兵士のCP変化</h3><div class="mission-members">${expeditionMemberRows(r.members,true)}</div><p class="muted">帰還した兵士は再配置できる。CP0の兵士は3日間の療養後、学校で復帰訓練を完了するとCPが全回復する。同じ場所への再出発は帰還から${CountryCombat.cooldown}日後。</p>`, '<button class="primary" data-action="expedition-report-read">報告を確認</button>');
}

function expeditionEligible(c) {return c.qualified && c.cp>0 && !c.defending && !c.task && !onExpedition(c);}
function expeditionBlockedReason(c) {return onExpedition(c)?'遠征中':c.cp===0?`CP0・${recoveryStatus(c)}`:c.defending?'防衛中':c.task?'訓練中':'戦闘不能';}
function expeditionImage(cell,label,kind='portrait-photo') {
  if(kind==='weapon-photo')return `<img class="weapon-photo weapon-art" src="assets/weapon-${(cell-6)%6}.svg" alt="${escapeHTML(label)}">`;
  return `<span class="asset-photo ${kind}" role="img" aria-label="${escapeHTML(label)}" style="background-position:${cell%6*20}% ${cell<6?0:100}%"></span>`;
}
const abilityKeys=['HP','STR','INT','FAI','AGI'];
function expeditionAbilities(c,id,weapon='') {
  const mock=[180+id*13%121,24+id*7%37,18+id*11%43,16+id*5%41,22+id*9%39];
  return abilityKeys.map((key,n)=>(c.stats?.[key] ?? mock[n])+(weapons[weapon]?.bonus?.[key]||0));
}
function unusedWeaponStat(key,weapon){const used={剣:'STR',盾:'STR',杖:'INT',魔導書:'INT',聖書:'FAI',ロザリオ:'FAI'}[weapons[weapon]?.type];return Boolean(used) && ['STR','INT','FAI'].includes(key) && key!==used;}
function expeditionStats(c,id,weapon='') {
  const values=expeditionAbilities(c,id,weapon);
  return `<span class="expedition-stats" aria-label="能力値">${abilityKeys.map((key,n)=>`<span class="${unusedWeaponStat(key,weapon)?'stat-unused':''}" data-stat="${key}" ${unusedWeaponStat(key,weapon)?'aria-label="'+key+' '+values[n]+'：この武器では不使用"':''}><small>${key}</small><strong class="${weapons[weapon]?.bonus?.[key]>0?'stat-up':''}">${values[n]}</strong></span>`).join('')}</span>`;
}
function equipmentPicker(id,context,candidate=null) {
  const c=state.characters[id],current=(context==='defense'?state.defenseWeapons?.[id]:formationDraft[id])??'';
  const selected=candidate ?? (weapons[current]?Number(current):0),w=weapons[selected];
  const before=expeditionAbilities(c,id,current),after=expeditionAbilities(c,id,selected);
  modal.classList.add('expedition-equipment');
  openModal(`${c.name}の装備`, `<div class="equipment-layout"><section class="equipment-weapons" aria-label="武器を選ぶ"><h3>武器を選ぶ</h3><div class="expedition-weapon-picker">${weapons.map((item,n)=>`<button data-action="equipment-preview" data-context="${context}" data-index="${id}" data-weapon="${n}" aria-pressed="${n===selected}" ${weaponAvailable(n,id)?'':'disabled'}>${expeditionImage(6+weaponTypes.indexOf(item.type),item.name,'weapon-photo')}<span><b>${item.name}</b><small>${item.type} · 威力 ${item.power}</small><small>${Number(current)===n && current!==''?'装備中':weaponUseText(n)}</small></span></button>`).join('')}</div></section><section class="equipment-comparison" aria-label="装備による能力値の変化"><header>${expeditionImage(id%6,c.name)}<span><b>${c.name}</b><small>CP ${c.cp} / ${c.maxCP}</small></span></header><div class="equipment-selected">${expeditionImage(6+weaponTypes.indexOf(w.type),w.name,'weapon-photo')}<span><b>${w.name}</b><small>威力 ${w.power}</small></span></div><div class="equipment-columns"><span>能力</span><span>現在</span><span></span><span>装備後</span><span>増減</span></div>${abilityKeys.map((key,n)=>{const delta=after[n]-before[n],unused=unusedWeaponStat(key,selected)?'stat-unused':'';return `<div class="equipment-stat" data-stat="${key}"><span class="${unused}">${key}</span><b class="${unusedWeaponStat(key,current)?'stat-unused':''}">${before[n]}</b><span class="equipment-arrow">→</span><b class="${unused} ${delta>0?'stat-up':delta<0?'stat-down':''}">${after[n]}</b><strong class="${unused} ${delta>0?'stat-up':delta<0?'stat-down':''}">${delta>0?'+':''}${delta||'—'}</strong></div>`;}).join('')}<small class="equipment-current ${weapons[current]?'':'weapon-missing'}">${weapons[current]?`現在の武器：${weapons[current].name}`:'武器が未設定！'}</small></section></div>`, `<button data-action="close">戻る</button><button class="primary" data-action="equipment-apply" data-context="${context}" data-index="${id}" data-weapon="${selected}" ${weaponAvailable(selected,id)?'':'disabled'}>${w.name}を装備</button>`);
}
function renderExpedition() {
  if(expeditionPage==='status')return selectedExpedition()?renderExpeditionProgress():renderExpeditionOverview();
  const members=state.characters.map((c,id)=>({c,id})).sort((a,b)=>Number(!expeditionEligible(a.c))-Number(!expeditionEligible(b.c)) || a.id-b.id);
  const chosen=Object.keys(formationDraft);
  const blocked=chosen.filter(id=>!expeditionEligible(state.characters[id]));
  const unarmed=chosen.filter(id=>!weapons[formationDraft[id]]);
  const combat=expeditionTarget===null?null:expeditionEstimate(chosen.map(id=>({id:Number(id),weapon:formationDraft[id]})),expeditionTarget),destinationBlocked=expeditionTarget===null?'':expeditionBlockReason(expeditionTarget);
  const complete=Boolean(combat && chosen.length && !blocked.length && !unarmed.length && !destinationBlocked && state.resources[0]>=combat.supplies);
  const departureHint=expeditionTarget===null?'<span class="departure-hint">目的地を選んでおくれ</span>':!chosen.length?'<span class="departure-hint">遠征メンバーを選んでおくれ</span>':blocked.length?`<span class="departure-hint stat-down">出撃不可 ${blocked.length}人：メンバーを確認</span>`:unarmed.length?`<button class="departure-hint needs-equipment weapon-missing" data-action="expedition-pick-weapon" data-index="${unarmed[0]}"><span aria-hidden="true">△ !</span> 武器が未設定！ ${unarmed.length}人 <span>武器を選ぶ →</span></button>`:destinationBlocked?`<span class="departure-hint stat-down">${destinationBlocked}</span>`:state.resources[0]<combat.supplies?'<span class="departure-hint stat-down">補給費の国庫が不足</span>':'<span class="departure-hint departure-ready">✓ 出発準備完了</span>';
  const title=`<div class="expedition-steps"><div class="budget-tabs" role="tablist" aria-label="遠征の準備">${[['destination','目的地'],['members',`メンバー ${chosen.length}人`]].map(([key,name])=>`<button role="tab" id="expedition-tab-${key}" aria-controls="expedition-panel-${key}" aria-selected="${expeditionTab===key}" data-action="expedition-tab" data-tab="${key}"><span class="step-number">${key==='destination'?'1':'2'}</span>${name}</button>`).join('')}</div></div>`;
  const body=expeditionTab==='destination' ? `<div class="expedition-destination-panel" role="tabpanel" id="expedition-panel-destination" aria-labelledby="expedition-tab-destination"><div class="destination-heading"><h2>遠征する場所を選ぶ</h2><small>同じ場所へは1部隊ずつ · 帰還から${CountryCombat.cooldown}日後に再出発</small></div><div class="destination-list">${expeditionDestinations.map((d,i)=>`<button data-action="expedition-target" data-index="${i}" aria-pressed="${expeditionTarget===i}"><span class="destination-scene scene-${i}" aria-hidden="true"></span><span class="destination-intel"><span class="destination-name"><b>${d.name}</b><small>${expeditionBlockReason(i)}</small></span><span class="destination-facts"><span><small>敵戦力</small><b>${Math.round(CountryCombat.enemyPower(i,combatProfile()))}</b><small>${d.enemy}</small></span><span><small>マップ情報</small><b>${d.terrain}</b><small>${d.feature}</small></span><span><small>獲得報酬</small><b>${d.reward}</b><small>${d.days}</small></span></span></span><strong class="destination-select">${expeditionTarget===i?'✓ 選択中':'選択'}</strong></button>`).join('')}</div></div>` : `<div class="expedition-members-panel" role="tabpanel" id="expedition-panel-members" aria-labelledby="expedition-tab-members"><aside class="expedition-party"><header><h2>遠征メンバー <b>${chosen.length}人</b></h2><small>能力値は装備補正込み</small></header><div class="expedition-party-list" data-drop-expedition="true">${chosen.map(id=>assignedCharacter(Number(id),'expedition')).join('')}<button class="round-slot empty-slot" data-action="expedition-add" aria-label="遠征の空き枠に配置"><span class="round-avatar">＋</span><small>兵士を追加</small></button></div></aside><div class="character-area expedition-character-area"><section class="expedition-roster"><header><h2>兵士一覧 <small>30人</small></h2><span>クリックで配置 · ドラッグで移動 · ×で解除</span></header><div class="expedition-roster-list roster-grid" data-drop-expedition-roster="true">${rosterList('expedition')}</div></section><aside class="character-details" aria-label="兵士の詳細">${characterDetails(characterChoice,'expedition')}</aside></div></div>`;
  return `<section class="game-window budget-window soldiers-window expedition-window" aria-label="遠征の編成">${expeditionHeader()}${title}${body}<div class="window-bottom"><div class="expedition-summary"><span>目的地 <b>${expeditionTarget===null?'未選択':expeditionDestinations[expeditionTarget].name}</b></span><span>メンバー <b>${chosen.length}人</b></span>${combat?`<span>出発時 ${resourceIcons[0]}国庫 <b class="stat-down">−${combat.supplies}</b></span><span>試算成功率 <b>${Math.round(combat.chance*100)}%</b></span>`:''}</div>${expeditionTab==='destination'?`<div class="actions"><button data-action="expedition-map" ${expeditionTarget===null?'disabled':''}>マップ情報</button><button class="primary" data-action="expedition-tab" data-tab="members" ${expeditionTarget===null?'disabled':''}>メンバーを選ぶ →</button></div>`:`<div class="departure-actions">${departureHint}<button class="primary" data-action="expedition-save" ${!complete?'disabled':''}>出発内容を確認</button></div>`}</div></section>`;
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
  openModal('武器が未設定！', `<p class="weapon-missing"><span aria-hidden="true">△ !</span> 防衛メンバー${members.length}人の武器が未設定！</p><p>このまま閉じると、防衛メンバーから外れる。</p><ul>${members.map(({c})=>`<li>${escapeHTML(c.name)}</li>`).join('')}</ul>`, '<button data-action="close">戻って武器を設定</button><button class="primary" data-action="defense-exit">外して閉じる</button>');
}
function chooseDefenseWeapon(id) {
  const c=state.characters[id];if(!c.qualified || c.cp===0 || c.task || onExpedition(c))return;
  equipmentPicker(id,'defense');
}
function applyEquipment(id,context,weapon){
  const c=state.characters[id];if(!weapons[weapon] || !c)return;
  if(!weaponAvailable(weapon,id)){toast('この装備は別の兵士が使用中');return;}
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
  return `<section class="game-window budget-window soldiers-window" aria-label="兵士管理">${soldierTabs()}<div class="soldier-toolbar"><div class="soldier-legend">${['戦闘不能','防衛','攻撃可能','遠征中'].map(name=>`<span class="soldier-state state-${['戦闘不能','防衛','攻撃可能','遠征中'].indexOf(name)}">${name} <b>${state.characters.filter(c=>soldierState(c)===name).length}</b></span>`).join('')}</div><label>並べ替え <select data-soldier-sort aria-label="兵士の並べ替え">${[['roster','名簿順'],['personality','性格順']].map(([key,name])=>`<option value="${key}" ${soldierSort===key?'selected':''}>${name}</option>`).join('')}</select></label></div><div class="soldier-defense-board">${defenseRow()}</div><div class="character-area soldier-body"><section class="candidate-panel" aria-label="兵士一覧"><header><h2>兵士一覧 <small>30人</small></h2></header><div class="candidate-list roster-grid" data-drop-defense-roster="true">${rosterList('defense')}</div></section><aside class="character-details" aria-label="兵士の詳細">${characterDetails(characterChoice,'defense')}<div class="details-training"><h3>今月の訓練</h3>${c.task?`<b>${c.task.purpose}</b><p>${facilities[c.task.school].name} · 枠${c.task.slot+1}<br>${escapeHTML(c.task.skill)}</p>`:'<p>予定なし</p>'}</div></aside></div></section>`;
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
  const expeditionScroll = document.querySelector('.expedition-roster-list')?.scrollTop || 0;
  const constructionScroll = document.querySelector('.construction-content')?.scrollTop || 0;
  const mobileScroll=['.training-body','.soldier-body','.soldiers-window','.expedition-members-panel'].map(selector=>[selector,document.querySelector(selector)?.scrollTop||0]);
  document.querySelector('.expedition-command').innerHTML=`<span aria-hidden="true">⚑</span><span class="expedition-command-label">遠征</span> ${state.expeditions.length?`<b>${state.expeditions.length}部隊</b>`:''} ${unreadExpeditionReports()?`<small class="expedition-unread">報告 ${unreadExpeditionReports()}</small>`:''}`;
  const dateState = state.defenseAttack ? ['paused','襲撃・停止中'] : monthAdvance ? ['running','月末まで進行中'] : timer ? ['running','時間進行中'] : ['paused','時間停止中'];
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
    const active = button.dataset.route === route() && (!button.dataset.menuTab || button.dataset.menuTab === budgetTabChoice);
    button.classList.toggle('active', active);
    button.setAttribute('aria-pressed', active);
  });
  document.querySelector('.budget-bottom')?.classList.toggle('needs-action',!state.nextBudget);
  document.querySelector('#view').innerHTML = ({ kingdom: renderKingdom, facilities: renderFacilities, plan: renderPlan, history: renderHistory, training: renderTraining, soldiers: renderSoldiers, expedition: renderExpedition })[route()]();
  document.querySelector('#time-controls').innerHTML = state.defenseAttack ? '<button class="attack-command" data-action="defense-notice">⚔ 襲撃発生</button>'
    : `<button class="time-auto" aria-label="${timer?'自動進行を一時停止':'自動進行を開始'}" title="${timer?'自動進行を停止':'自動進行を開始'}" data-action="toggle-time"><span aria-hidden="true">${timer?'Ⅱ':'▶'}</span><small>${timer?'停止':'進行'}</small></button><button aria-label="1日進める" title="1日進める" data-action="day">1日</button><button aria-label="月末まで進める" data-action="month">月末</button>`;
  const defenseNotice=document.querySelector('.defense-notice');
  defenseNotice.hidden=!state.defenseAttack && !state.defenseReport?.unread;
  defenseNotice.classList.toggle('defense-active',Boolean(state.defenseAttack));
  defenseNotice.setAttribute('aria-label',state.defenseAttack?'襲撃中・時間停止中。防衛戦へ戻る':'防衛戦の結果を確認');
  defenseNotice.innerHTML=state.defenseAttack?`<span class="defense-notice-icon" aria-hidden="true">⚔</span><span class="defense-notice-message"><strong>城下町が襲撃されている</strong><small>敵 ${state.defenseAttack.enemy}人 · 時間停止中</small></span><span class="defense-notice-action">防衛戦へ戻る <span aria-hidden="true">→</span></span>`:'⚔ 防衛戦 <small>結果を確認</small>';
  const expeditionRoster = document.querySelector('.expedition-roster-list');
  if (expeditionRoster) expeditionRoster.scrollTop = expeditionScroll;
  const budgetContent = document.querySelector('.budget-content');
  if (budgetContent) budgetContent.scrollTop = budgetScroll;
  const constructionContent = document.querySelector('.construction-content');
  if (constructionContent) constructionContent.scrollTop = constructionScroll;
  const trainingContent = document.querySelector('.training-content');
  if (trainingContent) trainingContent.scrollTop = trainingScroll;
  const candidates=document.querySelector('.candidate-list'); if(candidates)candidates.scrollTop=candidateScroll;
  const slots=document.querySelector('.training-slot-list'); if(slots)slots.scrollTop=slotScroll;
  mobileScroll.forEach(([selector,scroll])=>{const node=document.querySelector(selector);if(node)node.scrollTop=scroll;});
  if (!modal.open && focusBudget !== undefined) document.querySelector(`[data-budget-index="${focusBudget}"]`)?.focus({ preventScroll: true });
  if (!modal.open && focusTraining !== undefined) document.querySelector(`[data-training-count="${focusTraining}"][type="${focusTrainingType}"]`)?.focus({ preventScroll:true });
  if (!modal.open && focusTreasury) document.querySelector('[data-use-treasury]')?.focus({ preventScroll: true });
  if (focusAction && !modal.open) {
    const replacement = [...document.querySelectorAll('[data-action]')].find(el => el.dataset.action === focusAction && el.dataset.index === focusIndex && el.dataset.tab === focusTab && !el.disabled);
    replacement?.focus({ preventScroll: true });
  }
}
function stopMonthAdvance(){
  clearTimeout(monthAdvanceTimer);monthAdvanceTimer=null;monthAdvance=null;modal.classList.remove('month-progress');
}
function monthProgressBody(){
  const run=monthAdvance,progress=Math.round(run.days/run.total*100);
  return `<section class="month-passage" aria-label="月末までの日付進行"><div class="passage-orbit" aria-hidden="true"><span>☀</span></div><p class="passage-month">${state.year}年 ${state.month}月</p><div class="passage-calendar" aria-hidden="true"><span>${state.day>1?state.day-1:'—'}</span><strong>${state.day}<small>日</small></strong><span>${state.day<daysInMonth(state.month)?state.day+1:'翌月'}</span></div><p class="passage-status" role="status">${dateText()} · 1日ずつ進行中</p><div class="passage-track" role="progressbar" aria-label="月末までの進行" aria-valuemin="0" aria-valuemax="${run.total}" aria-valuenow="${run.days}"><span style="width:${progress}%"></span></div><div class="passage-count"><span>${run.days} / ${run.total}日</span><span>襲撃で一時停止 · 防衛後に再開</span></div><div class="passage-events">${run.events.length?run.events.slice(-2).map(text=>`<p>✓ ${escapeHTML(text)}</p>`).join(''):'<p>街の活動を進めています</p>'}</div></section>`;
}
function startMonthAdvance(){
  if(state.defenseAttack){showDefenseAttack();return;}
  if(requestMonthlyTime('month'))return;
  if(monthAdvance)return;
  stopTime();resumeAfterModal=false;
  monthAdvance={year:state.year,month:state.month,total:daysInMonth(state.month)-state.day+1,days:0,events:[]};
  modal.classList.add('month-progress');openModal('月末まで進行',monthProgressBody(),'<button data-action="month-stop">進行を中断</button>');
  render();monthAdvanceTimer=setTimeout(tickMonthAdvance,400);
}
function tickMonthAdvance(){
  const run=monthAdvance;if(!run)return;
  if(state.defenseAttack){suspendedMonthAdvance={year:state.year,month:state.month};stopMonthAdvance();showDefenseAttack();return;}
  const completed=state.completed.length;
  advanceDay();
  if(monthAdvance!==run)return;
  run.days++;run.events.push(...state.completed.slice(completed));
  document.querySelector('.month-passage').outerHTML=monthProgressBody();
  monthAdvanceTimer=setTimeout(tickMonthAdvance,250);
}
function showAttackArrival(interruptedMonth=false){
  clearTimeout(attackArrivalTimer);
  const attack=state.defenseAttack;if(!attack)return;
  modal.classList.add('attack-cutin');
  openModal('襲撃発生',`<section class="attack-arrival" role="alert"><div class="attack-arrival-emblem" aria-hidden="true">⚔</div><small>${dateText()} · 時間停止</small><h3>城下町に敵が接近</h3><p>${interruptedMonth?'月末への進行を一時停止。防衛結果を確認すると再開する。':'日付の進行を停止しました。'}<br>防衛メンバーで街を守ろう。</p><div class="attack-arrival-forces"><span>敵の人数</span><strong>${attack.enemy}<small>人</small></strong></div></section>`,'<button class="primary" data-action="defense-notice">防衛戦へ</button>');
  attackArrivalTimer=setTimeout(()=>{attackArrivalTimer=null;if(modal.open&&state.defenseAttack)showDefenseAttack();},1800);
}
function stopTime() { clearInterval(timer); timer = null; }
function startTime() { if(state.defenseAttack){showDefenseAttack();return;} if(requestMonthlyTime('time'))return; applyMonthBudget();if(!timer)timer=setInterval(advanceDay,2000);save();render(); }
function monthlySettingsPending(){return state.day===1 && state.monthSettingsConfirmed!==`${state.year}-${state.month}`;}
function requestMonthlyTime(action){
  if(route()!=='plan' && !monthlySettingsPending())return false;
  pendingTimeAction=action;
  if(route()==='plan'){stopTime();stopMonthAdvance();resumeAfterModal=false;render();}
  else openMonthlySettings();
  return true;
}
function budgetOutcomePreview(b,nextMonth=state.day!==1){
  const previous=!nextMonth && state.monthBudget?.year===state.year && state.monthBudget.month===state.month?state.monthBudget:null;
  const resources=[...state.resources];resources[0]+=b.deposit-b.withdrawal-(previous?.deposit||0)+(previous?.withdrawal||0);
  if(nextMonth){const e=monthEarnings(),materials=monthlyMaterials();resources[0]+=e.market+e.production.refund;resources[1]+=materials[1]-e.production.materials[1];resources[2]+=materials[2]-e.production.materials[2];}
  return constructionSchedule(resources);
}
function constructionSummary(preview){
  if(!preview.orders.length)return '';
  return `<section class="budget-construction-summary"><h3><span class="facility-icon">⚒</span> ${state.day===1?'今月':'来月'}の工事 <small>${preview.orders.length}件</small></h3>${preview.orders.map(({job,before,status})=>{
    const ready=status==='発注',f=facilities[job.index],symbol=ready?'<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="m6 12 4 4 8-8"/></svg>':status==='資源不足'?'<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m12 2 11 20H1L12 2Zm0 6v6m0 3v1"/></svg>':spendClock;
    return `<article class="budget-construction-order ${ready?'order-ready':'order-waiting'}" aria-label="${f.name} Lv.${job.level}：${ready?'資源を支払い工事へ追加':`${status}・支払いなし`}"><div class="budget-order-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><b>${f.name}</b><span class="budget-order-status">${symbol}${ready?'発注・支払い':`${status}・支払いなし`}</span></div>${constructionPrices(job.price,before)}<div class="budget-order-result"><span class="flow-arrow" aria-hidden="true">${flowArrow}</span><span class="budget-order-building" aria-hidden="true">⚒</span><span>Lv.${job.level-1} <span aria-hidden="true">→</span> <b>Lv.${job.level}</b></span><div class="result-level-pips" aria-hidden="true">${Array.from({length:Math.min(job.level,8)},(_,n)=>`<i class="${n===Math.min(job.level,8)-1?'new-level':''}"></i>`).join('')}</div><small>${ready?'工期5日':status}</small></div></article>`;
  }).join('')}</section>`;
}
function openMonthlySettings(){
  stopTime();stopMonthAdvance();resumeAfterModal=false;restoreMonthlyDraft();
  if(modal.open)closeModal();
  budgetTabChoice='budget';location.hash='plan';render();
}
function confirmMonthlySettings(){
  if(!commit())return false;
  if(state.day===1)applyPlannedConstruction();
  save();const action=pendingTimeAction;pendingTimeAction=null;
  resumeAfterModal=false;if(modal.open)closeModal();location.hash='kingdom';render();
  if(action==='month')startMonthAdvance();else if(action==='day')advanceDay();else startTime();
  return true;
}
function acknowledgeDefenseResult(){
  showingDefenseResult=false;
  if(state.defenseReport)state.defenseReport.unread=false;
  const resume=suspendedMonthAdvance && suspendedMonthAdvance.year===state.year && suspendedMonthAdvance.month===state.month;
  suspendedMonthAdvance=null;resumeAfterModal=false;save();closeModal();render();
  if(resume && !state.defenseAttack)startMonthAdvance();
}
function advanceDay() {
  if(state.defenseAttack)return;
  if(requestMonthlyTime('day'))return;
  applyMonthBudget();
  state.day++;
  const newMonth = state.day > daysInMonth(state.month);
  if (newMonth) { state.day = 1; state.month++; if (state.month > 12) { state.month = 1; state.year++; } }
  advanceRecovery();
  const returned=advanceExpedition();
  state.queues.forEach((q, i) => { if (!q.length) return; q[0].remaining--; if (!q[0].remaining) { const task = q.shift(); state.levels[i] = task.level; const text = task.level === 1 ? `${facilities[i].name}の新規建設が完了` : `${facilities[i].name}がLv.${task.level}に到達`; state.completed.push(text); log(`${text}。`); } });
  advanceTraining();
  if (newMonth) {
    const earnings=monthEarnings(),produced=produceWeapons();
    state.resources[0]+=earnings.market;
    updatePopulation();
    if(state.nextTraining){state.trainingPolicy=copyTraining(state.nextTraining);state.nextTraining=null;}
    if(state.nextDifficulty){state.difficulty=state.nextDifficulty;delete state.nextDifficulty;}
    const materials=monthlyMaterials();
    stopTime();state.resources=state.resources.map((n,i)=>n+materials[i]);state.boost=facilities.map(()=>0);
    state.report={produced,finance:state.monthBudget?{...state.monthBudget,building:state.monthBuilding||0,market:earnings.market,productionRefund:earnings.production.refund,productionMaterials:earnings.production.materials,closing:state.resources[0]}:null,title:`${state.year}年 ${state.month}月の月次報告`,completed:[...state.completed]};state.completed=[];state.monthBudget=null;state.monthBuilding=0;state.monthOpening=state.resources[0];
    if(state.nextBudget){state.budgetPolicy=state.nextBudget;state.nextBudget=null;}
    state.monthSettingsConfirmed=null;restoreMonthlyDraft();
    log(`月初の予算決定。木材${materials[1]}、鉱石${materials[2]}、市場売上${earnings.market}を受け取り。`);
  }
  if(newMonth || returned){stopTime();stopMonthAdvance();suspendedMonthAdvance=null;pendingTimeAction=null;resumeAfterModal=false;}
  save();render();
  if(newMonth)openMonthlySettings();else if(returned)showExpeditionReport(0);else maybeDefenseAttack();
}
function openModal(title, body, actions = '') {
  showingDefenseResult=false;
  if (!modal.open) { modalOpener = document.activeElement; resumeAfterModal = Boolean(timer); stopTime(); render(); }
  document.querySelector('#modal-body').innerHTML = `<div class="modal-heading"><h2 id="modal-title">${title}</h2><button class="close" data-action="close" aria-label="閉じる">×</button></div>${resumeAfterModal ? '<p class="muted">表示中は時間を一時停止しています。閉じると再開します。</p>' : ''}${body}<div class="actions end" style="margin-top:24px">${actions}</div>`;
  if (!modal.open) modal.showModal();
}
function closeModal() { if(showingDefenseResult){acknowledgeDefenseResult();return;} const wasAdvancing=Boolean(monthAdvance);stopMonthAdvance();clearTimeout(attackArrivalTimer);attackArrivalTimer=null;modal.classList.remove('attack-cutin');modal.close(); modal.classList.remove('resource-detail', 'monthly-report', 'expedition-equipment', 'defense-dialog', 'member-selection', 'skill-dialog'); if(wasAdvancing)render(); if (resumeAfterModal) startTime(); resumeAfterModal = false; if (modalOpener?.isConnected) modalOpener.focus(); else if (modalOpener?.dataset.action === 'resource-detail') document.querySelector(`[data-action="resource-detail"][data-index="${modalOpener.dataset.index}"]`)?.focus({ preventScroll: true }); else document.querySelector('#view').focus({ preventScroll: true }); }
function resourceForecast(){
  const nextMonth=state.day!==1 || !monthlySettingsPending();
  const policy=nextMonth?state.nextBudget||state.budgetPolicy:state.budgetPolicy;
  const budget=budgetValuesFor(policy.amounts,policy.useTreasury,nextMonth?state.resources[0]:budgetTreasury());
  const preview=budgetOutcomePreview(budget,nextMonth),construction=[0,0,0];
  preview.orders.filter(order=>order.status==='発注').forEach(({job})=>job.price.forEach((n,i)=>construction[i]+=n));
  const previous=!nextMonth && state.monthBudget?.year===state.year && state.monthBudget.month===state.month?state.monthBudget:null;
  return {nextMonth,budget,preview,construction,adjustment:previous?(previous.withdrawal||0)-(previous.deposit||0):0};
}
function showResourceDetails(i) {
  const f=resourceForecast(),income=i?(f.nextMonth?monthlyMaterials()[i]:0):f.budget.tax;
  const e=f.nextMonth?monthEarnings():null;
  const rows=[['月初収入',income],...(e?(i?[['工房の材料消費',-e.production.materials[i]]]:[['市場の売上',e.market],['未生産予算の返還',e.production.refund]]):[]),...(!i?[['運営・育成の予算',-f.budget.allocation],...(f.adjustment?[['計上済み予算の調整',f.adjustment]]:[])]:[]),['着工する建築',-f.construction[i]]];
  const signed=n=>`${n<0?'−':n>0?'+':''}${Math.abs(n).toLocaleString()}`;
  const waiting=f.preview.orders.filter(order=>order.status!=='発注').length;
  const body=`<div class="resource-ledger resource--${resourceKinds[i]}"><span class="resource-symbol">${resourceIcons[i]}</span><span class="resource-ledger-copy"><small>現在の所持</small><strong>${state.resources[i].toLocaleString()}</strong></span></div>
    <p class="spend-timing">${spendClock}${f.nextMonth?nextBudgetDate():monthSettingsDate()}の予算確定時</p>
    <div class="resource-ledger-lines">${rows.map(([name,n])=>`<div class="resource-ledger-row"><span>${name}</span><strong class="${n<0?'resource-ledger-loss':n>0?'resource-ledger-gain':''}">${signed(n)}</strong></div>`).join('')}</div>
    ${resourceChanges([f.preview.resources[i]-state.resources[i]],[i])}<small class="muted">確定済みの予算・建築予約から試算。${f.nextMonth?'来月の税収・運営費は現在の値で計算。':''}${waiting?`未着工の${waiting}件は支払いなし。`:''}</small>`;
  modal.classList.add('resource-detail');
  openModal(`${resourceNames[i]}の内訳`,body,'<button data-action="close">閉じる</button>');
}

function showDetail(i) { if(i===3)return; const f = facilities[i]; const next = constructionLevel(i); openModal(f.name, `<div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><span class="level">Lv.${state.levels[i]}</span></div><p>${f.description}</p><div class="report"><div class="report-row"><span>次に追加する強化</span><b>Lv.${next - 1} → Lv.${next}</b></div>${i < 4 ? `<div class="report-row"><span>育成枠（仮）</span><b>${(next - 1) * 5} → ${next * 5}人 / 月</b></div>` : `<div class="report-row"><span>用途</span><b>${f.role}</b></div>`}<div class="report-row"><span>工期</span><b>5日${state.queues[i].length + state.plan[i] ? '（先行工事の完了後）' : ''}</b></div></div>${costsHTML(cost(next))}<div class="separator"></div>${queueHTML(i)}${state.queues[i].length ? `<p class="muted">予約順：${state.queues[i].map(t => `Lv.${t.level}（${t.remaining}日）`).join(' → ')}</p>` : ''}${state.plan[i] ? `<p class="success">未決定の計画：${state.plan[i]}段階</p>` : ''}`, `<button data-action="close">戻る</button><button data-action="build" data-index="${i}" ${next>CountryBalance.maxLevel||cost(next).some((n,j)=>n>state.resources[j]) ? 'disabled' : ''}>${next>CountryBalance.maxLevel?'最大Lv':'国庫で建物を強化'}</button>`); }
function commit() {
  const b=budgetValues();
  const previous=state.day===1 && state.monthBudget?.year===state.year && state.monthBudget.month===state.month?state.monthBudget:null;
  if(!b.feasible || b.allocation>b.pool || b.withdrawal>b.treasury || (state.day===1 && state.resources[0]+b.deposit-b.withdrawal-(previous?.deposit||0)+(previous?.withdrawal||0)<0))return false;
  if(state.day===1){
    state.budgetPolicy=budgetPolicy();state.trainingPolicy=copyTraining(state.trainingDraft);
    applyMonthBudget(true);activateTraining();state.monthSettingsConfirmed=`${state.year}-${state.month}`;
  }else{state.nextBudget=budgetPolicy();state.nextTraining=copyTraining(state.trainingDraft);}
  log(`${monthSettingsDate()}の予算・育成を確定：建物へ ${b.allocation}。`);
  save();render();toast(`予算・育成を保存。${monthSettingsDate()}から反映します`);return true;
}
function acknowledgeExpeditionReport(index){
  if(!state.expeditionReports?.[index])return;
  state.expeditionReports.splice(index,1);state.expeditionUnread=unreadExpeditionReports()>0;save();
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
  openModal('月次報告', `${body}${state.report.produced?.length?`<section class="month-produced"><h3>完成した武器 <small>${state.report.produced.length}点</small></h3>${state.report.produced.map(i=>weaponRow(weapons[i],i,'produced-weapon')).join('')}</section>`:''}<div class="month-completed">${f?`<p class="muted">市場売上＋${f.market||0} · 未生産予算返還＋${f.productionRefund||0}</p>`:''}<h3>完了した活動</h3>${state.report.completed.length ? state.report.completed.map(t=>`<p>${escapeHTML(t)}</p>`).join('') : '<p class="muted">今月の完成工事はありません。</p>'}</div>`, '<button data-action="close">城下町へ戻る</button><button class="primary" data-action="report-plan">次月の予算を配分</button>');
}
function setDifficulty(id){
  if(!CountryBalance.profiles[id])return;
  if(monthlySettingsPending()){state.difficulty=id;delete state.nextDifficulty;budgetValues();}
  else if(id===state.difficulty)delete state.nextDifficulty;
  else state.nextDifficulty=id;
  save();render();showSettings();
}
function showSettings() {
  const selected=state.nextDifficulty||state.difficulty,current=CountryBalance.profile(state.difficulty);
  const cards=Object.values(CountryBalance.profiles).map(p=>`<button class="difficulty-card" data-action="difficulty" data-difficulty="${p.id}" aria-pressed="${selected===p.id}"><b>${p.name}</b><span>収入 ${Math.round(p.income*100)}% · 敵戦力 ${Math.round(p.enemy*100)}%</span><span>CP消費 ${Math.round(p.cp*100)}% · 襲撃 ${Math.round(p.raidChance*100)}% / 日</span><small>襲撃後${p.raidCooldown}日間は発生なし</small></button>`).join('');
  openModal('設定・保存', `<h3>難易度</h3><p class="muted">現在：${current.name}。${monthlySettingsPending()?'今月の予算確定前なので、今月から反映。':'変更は来月から反映。進行中の遠征・訓練は開始時の条件を保つ。'}${state.nextDifficulty?`<b> 来月：${CountryBalance.profile(state.nextDifficulty).name}</b>`:''}</p><div class="difficulty-options">${cards}</div><details class="balance-rules"><summary>資源・施設のルール</summary><p>初期資源はどの難易度も資金300・木材150・鉱石90。税収は人口125人につき1×収入倍率。</p><p>月初の素材収入は木材50・鉱石30×難易度倍率。施設は最大Lv5、月の維持費はLv＋3。</p><p>学校は活動予算2で訓練枠1人。市場は活動額の1.8倍×収入倍率を翌月初に売上として受け取る。</p><p>診療所は活動予算2で訓練支援1人、所要日数を1日短縮。工房は1本に活動予算10・木材3・鉱石2を消費。</p><p>CP0は3日療養し、月初に学校へ配置して復帰訓練。標準Lv1では4訓練日で全回復。</p></details><div class="separator"></div><p class="muted">記録は自動保存。手動保存は独立した1枠に残る。</p><div class="actions"><button data-action="save">手動保存する</button><button data-action="load">手動保存を読込</button></div><details class="balance-rules developer-settings"><summary>開発用の数値・動作確認</summary><p class="muted">変更した記録は難易度比較の対象外。結果を固定した遠征の報酬は0。</p><label class="mock-expedition-setting">次に出発する遠征 <select data-expedition-outcome aria-label="遠征結果のプレビュー">${[['auto','戦力と抽選で決定'],['victory','勝利を確認（報酬なし）'],['defeat','敗北を確認（報酬なし）']].map(([id,label])=>`<option value="${id}" ${(state.mockExpeditionOutcome||'auto')===id?'selected':''}>${label}</option>`).join('')}</select></label><label class="mock-expedition-setting">日ごとの襲撃発生率 <select data-defense-chance aria-label="襲撃発生率"><option value="auto" ${state.defenseChance===undefined?'selected':''}>難易度に従う</option>${[0,5,10,25,100].map(n=>`<option value="${n}" ${state.defenseChance===n?'selected':''}>${n}%</option>`).join('')}</select></label><button data-action="defense-preview">今すぐ襲撃を発生させる</button><h3>人口・税収・資源を直接変更</h3><p class="muted">税収を直接変更した場合は、その値を毎月使う。</p><div class="resource-editor">${['人口','税収','国庫','木材','鉱石'].map((name,i)=>`<label><span>${name}</span><input type="number" min="0" step="1" max="999999999" data-resource-edit="${i}" aria-label="${name}の数値" value="${[populationCount(),taxIncome(),...state.resources][i]}"></label>`).join('')}</div><p id="resource-edit-error" class="stat-down" role="alert"></p><div class="actions end"><button data-action="resource-edit-save">資源の変更を反映</button></div></details>`, '<button class="danger" data-action="reset-confirm">最初からやり直す</button><button data-action="close">閉じる</button>');
}
function applyResourceEdits(){
  const values=[...document.querySelectorAll('[data-resource-edit]')].map(el=>el.value.trim()===''?NaN:Number(el.value));
  if(values.length!==5 || values.some(n=>!Number.isSafeInteger(n)||n<0||n>999999999)){document.querySelector('#resource-edit-error').textContent='0以上の整数を入力してください';return;}
  state.balanceModified=true;state.population=values[0];state.taxOverride=values[1];state.resources=values.slice(2);save();render();showSettings();toast('人口・税収・資源の数値を変更しました');
}
function memberPicker(mode,title,action,extra='',filter=()=>true){
  const ids=state.characters.map((c,id)=>id).filter(id=>filter(state.characters[id],id)).sort((a,b)=>Number(!!rosterReason(a,mode))-Number(!!rosterReason(b,mode)) || Number(state.characters[a].cp===0)-Number(state.characters[b].cp===0) || a-b);
  openModal(title,`<div class="member-pick-list roster-grid">${ids.map(id=>rosterButton(id,mode,action,extra)).join('')||'<p>配置できる兵士はいない。</p>'}</div>`,'<button data-action="close">戻る</button>');
  modal.classList.add('member-selection');
}
function pickTrainingSlot(school,slot){memberPicker('training',`訓練枠${slot+1}に配置`,'training-place',`data-school="${school}" data-slot="${slot}"`,(c,id)=>!state.trainingDraft[id]);}
function pickDefenseMember(){memberPicker('defense','防衛メンバーに配置','defense-add','',c=>!c.defending);}
function pickExpeditionMember(){memberPicker('expedition','遠征メンバーに配置','expedition-place','',(c,id)=>!Object.hasOwn(formationDraft,id));}
function confirmUpgrade(i){
  if(!facilities[i]||!state.levels[i]||constructionLevel(i)>CountryBalance.maxLevel)return;
  const level=constructionLevel(i),price=cost(level);
  openModal(`${facilities[i].name}を強化`, `${constructionImpact(i,level,price)}${upgradeEffects(i,level)}<p class="muted">${state.day===1?state.queues[i].length?'先行工事が終わってから着工。':'確認後に着工。':'今は資源を消費せず、来月の予算確定後に着工。'}費用は着工時に支払い、工事状況へ追加する。</p>`, `<button data-action="close">戻る</button><button class="primary" data-action="build-submit" data-index="${i}" ${state.day===1 && !monthlySettingsPending() && price.some((n,j)=>n>state.resources[j])?'disabled':''}>${state.day===1?!monthlySettingsPending()?'資源を支払って強化':'今月の強化を予約':'来月の強化を予約'}</button>`);
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
 else if(target.hasAttribute('data-drop-roster')){cancelTraining(id);toast('来月の訓練予定から外しました');}
 else placeTraining(id,Number(target.dataset.dropSchool),Number(target.dataset.dropSlot));
 document.querySelectorAll('.drop-hover,.dragging').forEach(el=>el.classList.remove('drop-hover','dragging'));
});
document.addEventListener('dragend',()=>{draggedCharacter=null;document.body.classList.remove('training-dragging');document.querySelectorAll('.drop-hover,.dragging').forEach(el=>el.classList.remove('drop-hover','dragging'));});
document.addEventListener('change',event=>{if(event.target.matches('[data-defense-chance]')){if(event.target.value==='auto')delete state.defenseChance;else state.defenseChance=Number(event.target.value);state.balanceModified=true;save();}if(event.target.matches('[data-expedition-outcome]')){state.mockExpeditionOutcome=event.target.value;save();}});
document.addEventListener('click', event => {
  if (modal.open && event.target === modal && event.clientY <= document.querySelector('.hud').getBoundingClientRect().bottom) { closeModal(); return; }
  if (!modal.open && document.querySelector('.game-window') && !event.target.closest('.game-window,.hud button[data-action],.defense-notice')) { location.hash = 'kingdom'; return; }
  const button = event.target.closest('button[data-action]'); if (!button || button.disabled) return;
  const action = button.dataset.action; const i = Number(button.dataset.index);
  if(action==='character-detail'){characterChoice=i;render();return;}
  if(action==='roster-place'){placeRoster(i,button.dataset.mode);return;}
  if(action==='roster-remove'){delete formationDraft[i];render();return;}
  if(action==='weapon-budget'){restoreMonthlyDraft();budgetTabChoice='budget';location.hash='plan';render();document.querySelector('[data-budget-index="6"]')?.scrollIntoView({block:'center'});return;}
  if(action==='produced-weapon'){weaponChoice=i;soldierPage='weapons';closeModal();location.hash='soldiers';render();return;}
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
  if(action==='defense-report-read'){acknowledgeDefenseResult();return;}
  if(action==='expedition-map'){showExpeditionMap();return;}
  if(action==='expedition-depart'){departExpedition();return;}
  if(action==='expedition-retreat'){confirmExpeditionRetreat();return;}
  if(action==='expedition-retreat-submit'){retreatExpedition();return;}
  if(action==='expedition-report'){showExpeditionReport(Number(button.dataset.report||0));return;}
  if(action==='expedition-report-read'){acknowledgeExpeditionReport(expeditionReportChoice);closeModal();render();return;}
  if(action==='expedition-target'){expeditionTarget=i;render();return;}
  if(action==='expedition-toggle'){if(!expeditionEligible(state.characters[i]))return;if(Object.hasOwn(formationDraft,i))delete formationDraft[i];else formationDraft[i]='';render();return;}
  if(action==='expedition-save'){
    const ids=Object.keys(formationDraft);if(expeditionTarget===null || !ids.length || ids.some(id=>!expeditionEligible(state.characters[id]) || formationDraft[id]===''))return;
    confirmExpeditionDeparture();return;
  }

  if (action === 'training-open') {restoreMonthlyDraft();trainingChoice = i; budgetTabChoice='training'; closeModal(); location.hash = 'plan'; render(); return; }
  if (action === 'training-select') { trainingChoice = i; render(); return; }
  if (action === 'training-start') { startTraining(i,Number(button.dataset.dropSlot)); return; }
  if (action === 'character-select') {
    characterChoice=i;
    if(route()==='plan' && budgetTabChoice==='training' && !state.trainingDraft[i]){startTraining(trainingChoice);return;}
    if(route()==='soldiers' && !state.characters[i].defending && state.characters[i].cp>0 && !state.characters[i].task){addDefense(i);return;}
    render();return;
  }
  if(action==='training-slot-pick'){pickTrainingSlot(i,Number(button.dataset.dropSlot));return;}
  if(action==='training-place'){closeModal();placeTraining(i,Number(button.dataset.school),Number(button.dataset.slot));return;}
  if(action==='defense-pick'){pickDefenseMember();return;}
  if(action==='expedition-add'){pickExpeditionMember();return;}
  if(action==='expedition-place'){if(expeditionEligible(state.characters[i]))formationDraft[i]='';closeModal();render();return;}
  if (action === 'character-cancel') {cancelTraining(i);return;}

  if (action === 'construction-select') { constructionChoice = i; render(); return; }
  if (action === 'construction-work') { document.querySelector('.construction-queues')?.scrollIntoView({ block: 'start', behavior: 'smooth' }); return; }
  if (action === 'new-build-confirm') {confirmNewBuilding(i);return;}
  if (action === 'new-build') {
    if (!facilities[i] || i < 4 || state.levels[i] || state.queues[i].length) return;
    if(orderConstruction(i)){closeModal();render();toast(state.day===1?monthlySettingsPending()?`${facilities[i].name}を予算確定時の着工予定に追加しました`:`${facilities[i].name}を着工しました`:`${facilities[i].name}を来月の着工予定に追加しました`);}return;
  }
  if (action === 'budget-tab') { budgetTabChoice = button.dataset.tab; render(); document.querySelector(`[role="tab"][data-tab="${budgetTabChoice}"]`)?.focus({ preventScroll: true }); return; }
  if (action === 'navigate-budget') {restoreMonthlyDraft();budgetTabChoice = 'budget'; closeModal(); location.hash = 'plan'; render(); const target = document.querySelector(`[data-budget-index="${i}"]`); target?.scrollIntoView({ block: 'nearest' }); target?.focus({ preventScroll: true }); return; }
  if (action === 'navigate-training') {restoreMonthlyDraft();budgetTabChoice='training';location.hash='plan';render();return;}
  if(action==='open-monthly-menu'){restoreMonthlyDraft();budgetTabChoice=button.dataset.menuTab;location.hash='plan';render();return;}
  if(action==='open-soldier-menu'){soldierPage='members';location.hash='soldiers';render();return;}
  if (action === 'navigate-plan') {restoreMonthlyDraft();budgetTabChoice = 'budget';}
  if (action.startsWith('navigate-')) { location.hash = action.replace('navigate-', ''); return; }
  if (action === 'population-detail') showPopulation();
  if (action === 'resource-detail' && Number.isInteger(i) && resourceNames[i]) showResourceDetails(i);
  if (action === 'build') {confirmUpgrade(i);return;}
  if (action === 'build-submit') {if(facilities[i] && state.levels[i] && orderConstruction(i)){closeModal();render();toast(state.day===1?monthlySettingsPending()?`${facilities[i].name}の強化を予算確定時の着工予定に追加しました`:`${facilities[i].name}の強化を着工しました`:`${facilities[i].name}の強化を来月に予約しました`);}return;}
  if (action === 'detail') showDetail(i);
  if (['add', 'remove'].includes(action) && state.phase === 'planning') { state.plan[i] = Math.max(0, state.plan[i] + (action === 'remove' ? -1 : 1)); save(); render(); if (action === 'add') toast(`${facilities[i].name}の強化を追加しました`); }
  if (action === 'clear-plan') { state.plan = facilities.map(() => 0); save(); render(); toast('未決定の計画をクリアしました'); }
  if (action === 'close') closeModal();

  if (/^budget-(up|down)$/.test(action)) { budgetValues(); changeBudget(i, state.budgetShare[i] + (action.endsWith('up') ? 1 : -1)); return; }
  if(action==='commit'){confirmMonthlySettings();return;}
  if (action === 'toggle-time') { if (timer) { stopTime(); render(); } else startTime(); }
  if (action === 'day') { stopTime(); advanceDay(); }
  if (action === 'next-event') { stopTime(); const days = nextEventDays(); for (let n = 0; n < days && state.phase === 'running' && !state.defenseAttack; n++) advanceDay(); }
  if(action==='month'){startMonthAdvance();return;}
  if(action==='month-stop'){suspendedMonthAdvance=null;pendingTimeAction=null;closeModal();render();return;}
  if (action === 'report') showReport();
  if (action === 'report-plan') {restoreMonthlyDraft();budgetTabChoice = 'budget'; closeModal(); location.hash = 'plan'; }
  if(action==='resource-edit-save'){applyResourceEdits();return;}
  if(action==='difficulty'){setDifficulty(button.dataset.difficulty);return;}
  if (action === 'settings') showSettings();
  if (action === 'save') save(true);
  if (action === 'load') { try { const saved = JSON.parse(localStorage.getItem(manualSaveKey)); if (!validState(saved)) throw new Error(); stopTime();stopMonthAdvance();suspendedMonthAdvance=null;pendingTimeAction=null;resumeAfterModal = false; state = saved; initializeBalance();extendBuildings();initializeRecovery();syncWeapons();initializeCombatState();initializeBudgetPolicy();restoreMonthlyDraft();save(); closeModal(); render();if(monthlySettingsPending())openMonthlySettings();else toast('手動保存した国家状態を読み込みました'); } catch { toast('手動保存がありません。先に手動保存してください'); } }
  if (action === 'reset-confirm') openModal('王国を最初からやり直す', '<p>施設レベル、工事、資源、国家記録を初期状態へ戻します。自動保存と手動保存も初期化されます。</p>', '<button data-action="settings">戻る</button><button class="danger" data-action="reset">初期状態に戻す</button>');
  if (action === 'reset') { stopTime();stopMonthAdvance();suspendedMonthAdvance=null;pendingTimeAction=null;resumeAfterModal = false; state = initialState(state.nextDifficulty||state.difficulty); initializeBalance();extendBuildings();initializeRecovery();syncWeapons();initializeCombatState();initializeBudgetPolicy();restoreMonthlyDraft(); try { localStorage.removeItem(manualSaveKey); } catch {} save(); closeModal(); location.hash = 'kingdom'; render();openMonthlySettings(); }
});
document.addEventListener('input',event=>{
  if(event.target.matches('[data-budget-index]'))changeBudget(Number(event.target.dataset.budgetIndex),Number(event.target.value),true);
});
document.addEventListener('change', event => {
  if(event.target.matches('[data-formation-weapon]')){formationDraft[event.target.dataset.formationWeapon]=event.target.value;render();return;}
  if(event.target.matches('[data-soldier-sort]')){soldierSort=event.target.value;render();return;}
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
  if(lastRoute!=='plan' && route()==='plan')restoreMonthlyDraft();
  if(lastRoute==='soldiers' && route()!=='soldiers' && unequippedDefenders().length){const target=route();history.replaceState(null,'','#soldiers');if(modal.open)closeModal();render();confirmSoldierExit(target);return;}
  const showAttack=lastRoute==='soldiers' && route()==='kingdom' && returnToDefense && state.defenseAttack;
  if(lastRoute==='soldiers' && route()!=='soldiers')returnToDefense=false;
  lastRoute=route();if(modal.open && (!monthAdvance || route()!=='kingdom'))closeModal();render();
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
  if (event.code === 'Space' && event.target.tagName !== 'BUTTON') {
    event.preventDefault();
    if (timer) { stopTime(); render(); } else startTime();
  }
});
window.addEventListener('pagehide',()=>{stopTime();stopMonthAdvance();suspendedMonthAdvance=null;clearTimeout(attackArrivalTimer);save();});
restoreMonthlyDraft();
render();
if(monthlySettingsPending())openMonthlySettings();

new ResizeObserver(()=>document.documentElement.style.setProperty('--hud-bottom',`${document.querySelector('.hud').getBoundingClientRect().bottom}px`)).observe(document.querySelector('.hud'));
