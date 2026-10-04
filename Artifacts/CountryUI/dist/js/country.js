const facilities = [
  { id: 'warrior', name: '戦士学校', icon: '⚔', role: '戦士の育成', description: '王国を守る戦士たちの学び舎。訓練場を拡張し、より多くの戦士を育成する。' },
  { id: 'magic', name: '魔法学校', icon: '✧', role: '魔法使いの育成', description: '魔術の知識と実践を伝える学校。研究室を拡張し、魔法使いの育成枠を増やす。' },
  { id: 'church', name: '教会', icon: '✝', role: '僧侶の育成', description: '祈りと癒やしを学ぶ聖堂。修練の場を整え、僧侶の育成枠を増やす。' },
  { id: 'spirit', name: '精霊使役学校', icon: '❋', role: '精霊使いの育成', description: '自然と精霊の声を聞く学び舎。契約の場を拡張し、精霊使いの育成枠を増やす。' },
  { id: 'market', name: '市場', icon: '⚖', role: '商業・交易', description: '商人と住民が品物を売買する場所。王国の商業を支える。' },
  { id: 'clinic', name: '診療所', icon: '✚', role: '住民の医療', description: '病気やけがをした住民を診療する施設。王国の暮らしを支える。' },
  { id: 'workshop', name: '工房', icon: '⚒', role: '道具・装備の生産', description: '職人が道具や装備を製作する施設。王国の生産を支える。' }
];
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
const initialState = () => ({ version: 1, year: 1, month: 1, day: 1, phase: 'planning', resources: [1000, 600, 360], levels: [1, 1, 1, 1, 0, 0, 0], queues: facilities.map(() => []), plan: facilities.map(() => 0), logs: [{ date: '1年 1月1日', text: '王国の運営を開始。施設を選び、強化命令を決めよう。' }], report: null, completed: [] });
let state = initialState();
let timer = null;
let budgetTabChoice = 'budget';
let constructionChoice = 4;
let toastTimer;
let modalOpener;
let resumeAfterModal = false;
let previousResourceValues = null;
const modal = document.querySelector('#modal');
const validState = s => s?.version === 1 && Number.isInteger(s.year) && s.year > 0 && Number.isInteger(s.month) && s.month >= 1 && s.month <= 12 && Number.isInteger(s.day) && s.day >= 1 && s.day <= daysInMonth(s.month) && ['planning', 'running'].includes(s.phase) && Array.isArray(s.resources) && s.resources.length === 3 && s.resources.every(n => Number.isSafeInteger(n) && n >= 0) && ['levels', 'plan'].every(key => Array.isArray(s[key]) && [4, facilities.length].includes(s[key].length) && s[key].every(n => Number.isSafeInteger(n) && n >= 0)) && Array.isArray(s.queues) && s.queues.length === s.levels.length && s.queues.every(q => Array.isArray(q) && q.every(t => Number.isInteger(t.level) && t.level > 0 && Number.isInteger(t.remaining) && t.remaining > 0 && t.remaining <= 5)) && Array.isArray(s.logs) && s.logs.every(l => typeof l.date === 'string' && typeof l.text === 'string') && Array.isArray(s.completed) && s.completed.every(v => typeof v === 'string') && (s.report === null || (typeof s.report.title === 'string' && Array.isArray(s.report.completed) && s.report.completed.every(v => typeof v === 'string')));
try { const saved = JSON.parse(localStorage.getItem(storageKey)); if (validState(saved)) state = saved; } catch {}
function extendBuildings() {
  for (const key of ['levels', 'plan', 'budgetShare', 'budget']) {
    if (state[key]) while (state[key].length < facilities.length) state[key].push(0);
  }
  while (state.queues.length < facilities.length) state.queues.push([]);
}
extendBuildings();
function populationCount() { return state.population ?? 12560; }
function taxIncome() { return Math.floor(populationCount() / 125); }
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
  return ['kingdom', 'facilities', 'plan', 'history'].includes(name) ? name : 'kingdom';
}
function constructionCount() { return state.queues.reduce((n, q) => n + q.length, 0); }
function nextEventDays() { return Math.min(daysInMonth(state.month) - state.day + 1, ...state.queues.filter(q => q.length).map(q => q[0].remaining)); }
function nextEventText() {
  const days = nextEventDays();
  const names = facilities.filter((_, i) => state.queues[i][0]?.remaining === days).map(f => f.name);
  return names.length ? `${names.join('・')}の工事完了まであと${days}日。` : `次の月次報告まであと${days}日。`;
}
function windowTitle(title, extra = '') {
  return `<div class="window-title"><h1>${title}</h1><div class="actions">${extra}<button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div></div>`;
}
function renderKingdom() {
  return `<div class="settlement" aria-label="王国の城下町">
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
  const minimums = state.levels.map(level => !level ? 0 : pool ? Math.ceil(level * 10 / pool * 100) : 100);
  const feasible = minimums.reduce((a,b) => a+b, 0) <= 100;
  state.budgetShare ??= facilities.map(() => 0);
  state.budgetShare = state.budgetShare.map((n,i) => Math.max(minimums[i], Math.min(100, Math.round(n))));
  if (feasible) {
    let excess = Math.max(0, state.budgetShare.reduce((a,b) => a+b, 0) - 100);
    state.budgetShare = state.budgetShare.map((n,i) => { const trim = Math.min(excess, n-minimums[i]); excess -= trim; return n-trim; });
  }
  state.budget = state.budgetShare.map(n => Math.floor(pool * n / 100));
  const allocation = state.budget.reduce((a,b) => a+b, 0);
  const share = state.budgetShare.reduce((a,b) => a+b, 0);
  const deposit = Math.max(0, tax-allocation);
  const withdrawal = Math.max(0, allocation-tax);
  return { tax, treasury, pool, minimums, share, allocation, deposit, withdrawal, feasible };
}
function changeBudget(i, value) {
  const b = budgetValues();
  const used = b.share-state.budgetShare[i];
  if (!b.feasible) return;
  state.budgetShare[i] = Math.max(b.minimums[i], Math.min(100-used, Math.round(value)));
  budgetValues(); save(); render();
}
function budgetTabs() {
  return `<div class="window-title budget-tab-header"><div class="budget-tabs" role="tablist" aria-label="予算と建設">${[['budget','今月の予算'],['construction','建設']].map(([tab,name])=>`<button role="tab" id="tab-${tab}" aria-controls="panel-${tab}" aria-selected="${budgetTabChoice === tab}" tabindex="${budgetTabChoice === tab ? 0 : -1}" data-action="budget-tab" data-tab="${tab}">${name}</button>`).join('')}</div><button class="close" data-action="navigate-kingdom" aria-label="城下町へ戻る">×</button></div>`;
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
    <div class="construction-wallet">${['国庫','木材','鉱石'].map((name,i)=>`<div><span class="resource-symbol">${resourceIcons[i]}</span><span>${name}</span><strong>${state.resources[i].toLocaleString()}</strong></div>`).join('')}</div>
    <section class="new-construction"><h2>新規建設</h2><div class="construction-picker"><div class="construction-catalog" role="group" aria-label="建設する建物を選択">${facilities.slice(4).map((building,n)=>{const i=n+4; return `<button class="construction-option ${i===constructionChoice ? 'is-selected' : ''}" data-action="construction-select" data-index="${i}" aria-pressed="${i===constructionChoice}"><span class="building-preview preview-${building.id}" aria-hidden="true"></span><span><b>${building.name}</b><small>${building.role}</small></span><span class="catalog-status">${state.levels[i] ? '建設済み' : state.queues[i].length ? '建設中' : '未建設'}</span></button>`;}).join('')}</div><section class="construction-inspector" aria-label="選択した建物の詳細"><div class="inspector-heading"><span class="facility-icon">${f.icon}</span><div><h3>${f.name}</h3><small>${f.role}</small></div></div><p>${f.description}</p>${constructionPrices(cost(1))}<div class="construction-order"><span>工期 <b>5日</b></span><button class="primary" data-action="new-build-confirm" data-index="${constructionChoice}" ${built || pending || !canBuild ? 'disabled' : ''}>${built ? '建設済み' : pending ? '建設中' : !canBuild ? '資源不足' : '建設を決める'}</button></div></section></div></section>
    <section class="existing-construction"><h2>既存の建物を強化</h2><div class="construction-columns"><span>建物</span><span>強化に必要な資源</span><span>工期</span><span></span></div>
    ${facilities.map((f,i)=>{ if (!state.levels[i]) return ''; const next = state.levels[i]+state.queues[i].length+1; const price = cost(next); const canBuild = price.every((n,j)=>n<=state.resources[j]); return `<article class="construction-row"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${next-1} → Lv.${next}</small></div></div>${constructionPrices(price)}<div class="construction-duration"><b>5日</b><small>${state.queues[i].length ? '先行工事の後に着工' : 'すぐに着工'}</small></div><button class="primary" data-action="build" data-index="${i}" aria-label="${f.name}をLv.${next}へ強化" ${canBuild ? '' : 'disabled'}>${canBuild ? '強化する' : '資源不足'}</button></article>`; }).join('')}</section>
    <div class="construction-queues"><h2>工事状況 <small>${constructionCount()}件</small></h2><div>${constructionCount() ? facilities.map((f,i)=>state.queues[i].length ? `<section><h3>${f.name}</h3>${queueHTML(i)}</section>` : '').join('') : '<p class="muted">進行中の工事はありません。</p>'}</div></div>
    </div><div class="window-bottom"><small>建設・強化の費用は国庫から支払う。</small><button data-action="construction-work">工事状況を見る</button><button data-action="budget-tab" data-tab="budget">今月の予算へ戻る</button></div></section>`;
}
function confirmNewBuilding(i) {
  const f = facilities[i];
  if (!f || i < 4 || state.levels[i] || state.queues[i].length) return;
  openModal(`${f.name}を建設`, `<p>${f.description}</p>${constructionPrices(cost(1))}<div class="construction-confirm-line"><span>工期</span><b>5日</b></div><p class="muted">着工時に費用を支払い、工事状況に追加する。</p>`, `<button data-action="close">戻る</button><button class="primary" data-action="new-build" data-index="${i}">着工する</button>`);
}
function renderPlan() {
  if (budgetTabChoice === 'construction') return renderConstruction();
  const b = budgetValues(); const locked = state.phase !== 'planning';
  const money = n => n.toLocaleString();
  return `<section class="game-window budget-window" aria-label="今月の予算">
    ${budgetTabs()}
    <div class="window-content budget-content" role="tabpanel" id="panel-budget" aria-labelledby="tab-budget" tabindex="0">
      <div class="budget-source"><div class="budget-source-toolbar"><span>予算の財源</span><label class="treasury-option ${state.useTreasury ? 'is-enabled' : ''}"><input type="checkbox" data-use-treasury aria-label="国庫を配分に使う" ${state.useTreasury ? 'checked' : ''} ${locked ? 'disabled' : ''}><span>国庫も配分に含める</span></label></div><div class="budget-source-tax"><small>税収</small><strong>${money(b.tax)}</strong><span class="source-note">今月の収入</span></div><div class="budget-source-treasury"><small>国庫</small><strong>${money(b.treasury)}</strong><span class="source-note">${state.useTreasury ? '配分に含める' : '今回は使わない'}</span></div><div class="budget-source-total"><small>配分できる合計</small><strong>${money(b.pool)}</strong><span class="source-note">${state.useTreasury ? '税収と国庫を含む' : '税収のみ'}</span></div></div>
      <div class="allocation-bar" aria-label="建物へ${b.share}％、未配分${Math.max(0,100-b.share)}％">${state.budgetShare.map((n,i)=>n ? `<span class="allocation-segment allocation-${i}" style="flex:${n}" title="${facilities[i].name} ${n}％"></span>` : '').join('')}<span class="allocation-segment allocation-saved" style="flex:${Math.max(0,100-b.share)}" title="未配分 ${Math.max(0,100-b.share)}％"></span></div>
      <div class="budget-layout"><div class="building-budgets"><div class="budget-columns"><span>建築物</span><span>予算の配分</span></div>
      ${facilities.map((f,i)=> state.levels[i] && (!locked || state.monthBudget?.shares[i]) ? `<article class="building-budget"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${state.levels[i]}</small></div></div><div class="allocation-control"><div class="allocation-top"><div class="budget-adjust tax-accent"><button data-action="budget-down" data-index="${i}" aria-label="${f.name}の配分率を1％減らす" ${locked || !b.feasible || state.budgetShare[i] <= b.minimums[i] ? 'disabled' : ''}>−</button><strong>${state.budgetShare[i]}<small>％</small></strong><button data-action="budget-up" data-index="${i}" aria-label="${f.name}の配分率を1％増やす" ${locked || !b.feasible || b.share >= 100 ? 'disabled' : ''}>＋</button></div><div class="allocation-amount"><small>配分額</small><b>${money(state.budget[i])}</b></div></div><input type="range" min="${b.minimums[i]}" max="100" step="1" value="${state.budgetShare[i]}" data-budget-index="${i}" aria-label="${f.name}の配分率" aria-valuetext="${state.budgetShare[i]}％、最低${b.minimums[i]}％" ${locked || !b.feasible ? 'disabled' : ''}><div class="allocation-minimum"><span>最低 ${b.minimums[i]}％</span><span>必要な運営費を含む</span></div></div></article>` : '').join('')}</div>
      <aside class="budget-reserve"><h2>国庫の見込み</h2><div><span>現在の国庫</span><b>${money(state.resources[0])}</b></div><div class="reserve-accent"><span>配分に使う国庫</span><b>−${money(b.withdrawal)}</b></div><div class="tax-accent"><span>税収から積立</span><b>＋${money(b.deposit)}</b></div><div class="budget-balance"><span>${locked ? '予算確定後の国庫' : '配分後に残る国庫'}</span><strong>${money(locked ? state.resources[0] : state.resources[0]+b.deposit-b.withdrawal)}</strong></div><p>未配分の税収は国庫へ。使わなかった国庫はそのまま残る。</p>${!b.feasible ? '<p class="reserve-accent">最低配分を満たすには、国庫も使う必要があります。</p>' : ''}</aside></div>
    </div><div class="window-bottom"><div class="budget-footer"><span>建物へ配分 <b>${b.share}％</b></span><span>未配分 <b>${Math.max(0,100-b.share)}％</b></span></div><button class="primary" data-action="commit" ${locked || !b.feasible ? 'disabled' : ''}>${locked ? '今月の予算は確定済み' : '今月の予算を確定'}</button></div></section>`;
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
  const budgetScroll = document.querySelector('.budget-content')?.scrollTop || 0;
  const constructionScroll = document.querySelector('.construction-content')?.scrollTop || 0;
  const dateState = state.phase === 'planning'
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
  document.querySelector('#view').innerHTML = ({ kingdom: renderKingdom, facilities: renderFacilities, plan: renderPlan, history: renderHistory })[route()]();
  document.querySelector('#time-controls').innerHTML = state.phase === 'planning'
    ? `<button class="primary budget-command" data-action="navigate-plan" data-route="plan" aria-label="今月の予算を決める" aria-pressed="${route() === 'plan'}"><span aria-hidden="true">⚑</span>予算を決める</button>`
    : `<button class="time-auto" aria-label="${timer ? '自動進行を一時停止' : '自動進行を開始'}" title="${timer ? '自動進行を停止' : '自動進行を開始'}" data-action="toggle-time"><span aria-hidden="true">${timer ? 'Ⅱ' : '▶'}</span><small>${timer ? '停止' : '進行'}</small></button><button aria-label="1日進める" title="1日進める" data-action="day">1日</button><button class="primary" aria-label="${constructionCount() ? '次の工事完了まで進む' : '月次報告まで進む'}" data-action="${constructionCount() ? 'next-event' : 'month'}">${constructionCount() ? '工事まで' : '月次報告'}</button>`;
  const budgetContent = document.querySelector('.budget-content');
  if (budgetContent) budgetContent.scrollTop = budgetScroll;
  const constructionContent = document.querySelector('.construction-content');
  if (constructionContent) constructionContent.scrollTop = constructionScroll;
  if (!modal.open && focusBudget !== undefined) document.querySelector(`[data-budget-index="${focusBudget}"]`)?.focus({ preventScroll: true });
  if (!modal.open && focusTreasury) document.querySelector('[data-use-treasury]')?.focus({ preventScroll: true });
  if (focusAction && !modal.open) {
    const replacement = [...document.querySelectorAll('[data-action]')].find(el => el.dataset.action === focusAction && el.dataset.index === focusIndex && el.dataset.tab === focusTab && !el.disabled);
    replacement?.focus({ preventScroll: true });
  }
}
function stopTime() { clearInterval(timer); timer = null; }
function startTime() { if (!timer && state.phase === 'running') timer = setInterval(advanceDay, 2000); render(); }
function advanceDay() {
  if (state.phase !== 'running') return;
  state.day++;
  const newMonth = state.day > daysInMonth(state.month);
  if (newMonth) { state.day = 1; state.month++; if (state.month > 12) { state.month = 1; state.year++; } }
  state.queues.forEach((q, i) => { if (!q.length) return; q[0].remaining--; if (!q[0].remaining) { const task = q.shift(); state.levels[i] = task.level; const text = task.level === 1 ? `${facilities[i].name}の新規建設が完了` : `${facilities[i].name}がLv.${task.level}に到達`; state.completed.push(text); log(`${text}。`); } });
  if (newMonth) {
    updatePopulation();
    stopTime(); state.phase = 'planning'; state.resources = state.resources.map((n, i) => n + (i ? monthlyIncome[i] : 0)); state.boost = facilities.map(() => 0);
    state.report = { finance: state.monthBudget ? { ...state.monthBudget, building: state.monthBuilding || 0, closing: state.resources[0] } : null, title: `${state.year}年 ${state.month}月の月次報告`, completed: [...state.completed] }; state.completed = []; state.monthBudget = null; state.monthBuilding = 0; state.monthOpening = state.resources[0];
    log(`新しい月の税収${taxIncome()}を配分できる。木材50、鉱石30を受け取り。`);
  }
  save(); render(); if (newMonth) showReport();
}
function openModal(title, body, actions = '') {
  if (!modal.open) { modalOpener = document.activeElement; resumeAfterModal = Boolean(timer); stopTime(); render(); }
  document.querySelector('#modal-body').innerHTML = `<div class="modal-heading"><h2 id="modal-title">${title}</h2><button class="close" data-action="close" aria-label="閉じる">×</button></div>${resumeAfterModal ? '<p class="muted">表示中は時間を一時停止しています。閉じると再開します。</p>' : ''}${body}<div class="actions end" style="margin-top:24px">${actions}</div>`;
  if (!modal.open) modal.showModal();
}
function closeModal() { modal.close(); modal.classList.remove('resource-detail', 'monthly-report'); if (resumeAfterModal) startTime(); resumeAfterModal = false; if (modalOpener?.isConnected) modalOpener.focus(); else if (modalOpener?.dataset.action === 'resource-detail') document.querySelector(`[data-action="resource-detail"][data-index="${modalOpener.dataset.index}"]`)?.focus({ preventScroll: true }); else document.querySelector('#view').focus({ preventScroll: true }); }
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
function showDetail(i) { const f = facilities[i]; const next = targetLevel(i); openModal(f.name, `<div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><span class="level">Lv.${state.levels[i]}</span></div><p>${f.description}</p><div class="report"><div class="report-row"><span>次に追加する強化</span><b>Lv.${next - 1} → Lv.${next}</b></div>${i < 4 ? `<div class="report-row"><span>育成枠（仮）</span><b>${(next - 1) * 5} → ${next * 5}人 / 月</b></div>` : `<div class="report-row"><span>用途</span><b>${f.role}</b></div>`}<div class="report-row"><span>工期</span><b>5日${state.queues[i].length + state.plan[i] ? '（先行工事の完了後）' : ''}</b></div></div>${costsHTML(cost(next))}<div class="separator"></div>${queueHTML(i)}${state.queues[i].length ? `<p class="muted">予約順：${state.queues[i].map(t => `Lv.${t.level}（${t.remaining}日）`).join(' → ')}</p>` : ''}${state.plan[i] ? `<p class="success">未決定の計画：${state.plan[i]}段階</p>` : ''}`, `<button data-action="close">戻る</button><button class="primary" data-action="navigate-budget" data-index="${i}" ${state.phase !== 'planning' ? 'disabled' : ''}>運営予算を配分する</button><button data-action="build" data-index="${i}" ${cost(next).some((n,j)=>n>state.resources[j]) ? 'disabled' : ''}>国庫で建物を強化</button>`); }
function commit() {
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
    ${f.pool !== undefined ? `<div class="month-ledgers"><section><h3>今月の予算配分</h3><div class="month-line"><span>税収</span><b>${money(f.tax)}</b></div><div class="month-line"><span>建物への配分</span><b>${money(f.allocation)}</b></div><div class="month-line tax-accent"><span>税収から積立</span><b>＋${money(f.deposit)}</b></div></section><section><h3>国庫からの支出</h3><div class="month-line"><span>予算への配分</span><b>${money(f.withdrawal)}</b></div><div class="month-line"><span>建築・強化</span><b>${money(f.building)}</b></div></section></div><section class="month-buildings unified-month"><h3>建物への配分記録</h3><div class="month-building-head"><span>建物</span><span>配分率</span><span>配分額</span></div>${facilities.map((building,i)=> f.shares[i] ? `<div class="month-building-row"><span>${building.name}</span><b class="tax-accent">${f.shares[i]}％</b><span>${money(f.budgets[i])}</span></div>` : '').join('')}</section>` : `    <div class="month-ledgers"><section><h3>税収の使い道</h3><div class="month-line month-total"><span>税収</span><b>${money(f.tax)}</b></div><div class="month-line"><span>建物の運営費</span><b>${money(f.maintenance - f.shortfall)}</b></div><div class="month-line"><span>建物への追加予算</span><b>${money(f.allocation)}</b></div><div class="month-line tax-accent"><span>国庫へ積立</span><b>＋${money(f.deposit)}</b></div></section><section><h3>国庫からの支出</h3><div class="month-line"><span>建築・強化</span><b>${money(f.building)}</b></div><div class="month-line reserve-accent"><span>運営ブースト</span><b>${money(f.boost)}</b></div><div class="month-line"><span>維持費の不足を補填</span><b>${money(f.shortfall)}</b></div></section></div>
    <section class="month-buildings"><h3>建物への配分記録</h3><div class="month-building-head"><span>建物</span><span>配分率</span><span>配分額</span><span>ブースト</span></div>${facilities.map((building,i)=> f.shares[i] ? `<div class="month-building-row"><span>${building.name}</span><b class="tax-accent">${f.shares[i]}％</b><span>${money(f.budgets[i])}</span><span class="reserve-accent">${money(f.boosts[i])}</span></div>` : '').join('')}</section>`}` : '<p class="muted">この月の収支記録はありません。新しい予算を確定した月から収支を記録します。</p>';
  openModal('月次報告', `${body}<div class="month-completed"><h3>完成した工事</h3>${state.report.completed.length ? state.report.completed.map(t=>`<p>${escapeHTML(t)}</p>`).join('') : '<p class="muted">今月の完成工事はありません。</p>'}</div>`, '<button data-action="close">城下町へ戻る</button><button class="primary" data-action="report-plan">次月の予算を配分</button>');
}
function showSettings() { openModal('設定・保存', '<p class="muted">記録は随時、自動保存する。手動保存は独立した1枠に残る。読込後は時間を停止した状態で再開する。</p><div class="actions"><button data-action="save">手動保存する</button><button data-action="load">手動保存を読込</button></div><div class="separator"></div><h3>試作のルール</h3><p class="muted">初期資源：資金1,000 / 木材600 / 鉱石360<br>月収：資金100 / 木材50 / 鉱石30<br>強化費用：目標Lv. × 100 / 50 / 30<br>工期：5日。育成枠：Lv. × 5人 / 月（仮）<br>領土は1領固定。閏年は扱いません。</p>', '<button class="danger" data-action="reset-confirm">最初からやり直す</button><button data-action="close">閉じる</button>'); }
document.addEventListener('click', event => {
  if (modal.open && event.target === modal && event.clientY <= document.querySelector('.hud').getBoundingClientRect().bottom) { closeModal(); return; }
  if (!modal.open && document.querySelector('.game-window') && !event.target.closest('.game-window')) { location.hash = 'kingdom'; return; }
  const button = event.target.closest('button[data-action]'); if (!button || button.disabled) return;
  const action = button.dataset.action; const i = Number(button.dataset.index);
  if (action === 'construction-select') { constructionChoice = i; render(); return; }
  if (action === 'construction-work') { document.querySelector('.construction-queues')?.scrollIntoView({ block: 'start', behavior: 'smooth' }); return; }
  if (action === 'new-build-confirm') { confirmNewBuilding(i); return; }
  if (action === 'new-build') {
    if (!facilities[i] || i < 4 || state.levels[i] || state.queues[i].length || cost(1).some((n,j)=>n>state.resources[j])) return;
    state.monthOpening ??= state.resources[0]; state.monthBuilding = (state.monthBuilding || 0) + cost(1)[0];
    state.resources = state.resources.map((n,j)=>n-cost(1)[j]); state.queues[i].push({level:1,remaining:5});
    log(`${facilities[i].name}の新規建設を発注。`); closeModal(); save(); render();
    document.querySelector('.construction-queues')?.scrollIntoView({ block:'start' }); toast(`${facilities[i].name}を着工。工事状況に追加しました`); return;
  }
  if (action === 'budget-tab') { budgetTabChoice = button.dataset.tab; render(); document.querySelector(`[role="tab"][data-tab="${budgetTabChoice}"]`)?.focus({ preventScroll: true }); return; }
  if (action === 'navigate-budget') { budgetTabChoice = 'budget'; closeModal(); location.hash = 'plan'; render(); const target = document.querySelector(`[data-budget-index="${i}"]`); target?.scrollIntoView({ block: 'nearest' }); target?.focus({ preventScroll: true }); return; }
  if (action === 'navigate-plan') budgetTabChoice = 'budget';
  if (action.startsWith('navigate-')) { location.hash = action.replace('navigate-', ''); return; }
  if (action === 'population-detail') showPopulation();
  if (action === 'resource-detail' && Number.isInteger(i) && resourceNames[i]) showResourceDetails(i);
  if (action === 'build') { if (!facilities[i] || !state.levels[i]) return; const next = state.levels[i] + state.queues[i].length + 1; const price = cost(next); if (price.every((n,j)=>n<=state.resources[j])) { state.monthOpening ??= state.resources[0]; state.monthBuilding = (state.monthBuilding || 0) + price[0]; state.resources = state.resources.map((n,j)=>n-price[j]); state.queues[i].push({level:next,remaining:5}); log(`${facilities[i].name}の強化を国庫で発注。資金 ${price[0]}。`); closeModal(); save(); render(); } return; }
  if (action === 'detail') showDetail(i);
  if (['add', 'remove'].includes(action) && state.phase === 'planning') { state.plan[i] = Math.max(0, state.plan[i] + (action === 'remove' ? -1 : 1)); save(); render(); if (action === 'add') toast(`${facilities[i].name}の強化を追加しました`); }
  if (action === 'clear-plan') { state.plan = facilities.map(() => 0); save(); render(); toast('未決定の計画をクリアしました'); }
  if (action === 'close') closeModal();

  if (/^budget-(up|down)$/.test(action) && state.phase === 'planning') { budgetValues(); changeBudget(i, state.budgetShare[i] + (action.endsWith('up') ? 1 : -1)); return; }
  if (action === 'commit') commit();
  if (action === 'toggle-time') { if (timer) { stopTime(); render(); } else startTime(); }
  if (action === 'day') { stopTime(); advanceDay(); }
  if (action === 'next-event') { stopTime(); const days = nextEventDays(); for (let n = 0; n < days && state.phase === 'running'; n++) advanceDay(); }
  if (action === 'month') { stopTime(); for (let n = 0; n < 31 && state.phase === 'running'; n++) advanceDay(); }
  if (action === 'report') showReport();
  if (action === 'report-plan') { budgetTabChoice = 'budget'; closeModal(); location.hash = 'plan'; }
  if (action === 'settings') showSettings();
  if (action === 'save') save(true);
  if (action === 'load') { try { const saved = JSON.parse(localStorage.getItem(manualSaveKey)); if (!validState(saved)) throw new Error(); stopTime(); resumeAfterModal = false; state = saved; extendBuildings(); save(); closeModal(); render(); toast('手動保存した国家状態を読み込みました'); } catch { toast('手動保存がありません。先に手動保存してください'); } }
  if (action === 'reset-confirm') openModal('王国を最初からやり直す', '<p>施設レベル、工事、資源、国家記録を初期状態へ戻します。自動保存と手動保存も初期化されます。</p>', '<button data-action="settings">戻る</button><button class="danger" data-action="reset">初期状態に戻す</button>');
  if (action === 'reset') { stopTime(); resumeAfterModal = false; state = initialState(); try { localStorage.removeItem(manualSaveKey); } catch {} save(); closeModal(); location.hash = 'kingdom'; render(); toast('王国を初期状態に戻しました'); }
});
document.addEventListener('change', event => {
  if (state.phase !== 'planning') return;
  if (event.target.matches('[data-use-treasury]')) { state.useTreasury = event.target.checked; budgetValues(); save(); render(); return; }
  const i = event.target.dataset.budgetIndex;
  if (i !== undefined) changeBudget(Number(i), Number(event.target.value));
});
modal.addEventListener('click', event => { const r = modal.getBoundingClientRect(); if (event.target === modal && (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom)) closeModal(); });
modal.addEventListener('cancel', event => { event.preventDefault(); closeModal(); });
window.addEventListener('hashchange', () => { if (modal.open) closeModal(); render(); document.querySelector('#view').focus({ preventScroll: true }); });
document.addEventListener('keydown', event => {
  if (modal.open || event.ctrlKey || event.metaKey || event.altKey) return;
  if (event.target.matches('[role="tab"]') && ['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) { event.preventDefault(); budgetTabChoice = event.key === 'Home' ? 'budget' : event.key === 'End' ? 'construction' : budgetTabChoice === 'budget' ? 'construction' : 'budget'; render(); document.querySelector(`[role="tab"][data-tab="${budgetTabChoice}"]`)?.focus({ preventScroll: true }); return; }
  if (event.key === 'Escape') { location.hash = 'kingdom'; return; }
  if (/^[1-4]$/.test(event.key)) location.hash = ['kingdom', 'facilities', 'plan', 'history'][Number(event.key) - 1];
  if (event.code === 'Space' && state.phase === 'running' && event.target.tagName !== 'BUTTON') {
    event.preventDefault();
    if (timer) { stopTime(); render(); } else startTime();
  }
});
window.addEventListener('pagehide', () => { stopTime(); save(); });
render();
