const facilities = [
  { id: 'warrior', name: '戦士学校', icon: '⚔', role: '戦士の育成', description: '王国を守る戦士たちの学び舎。訓練場を拡張し、より多くの戦士を育成する。' },
  { id: 'magic', name: '魔法学校', icon: '✧', role: '魔法使いの育成', description: '魔術の知識と実践を伝える学校。研究室を拡張し、魔法使いの育成枠を増やす。' },
  { id: 'church', name: '教会', icon: '✝', role: '僧侶の育成', description: '祈りと癒やしを学ぶ聖堂。修練の場を整え、僧侶の育成枠を増やす。' },
  { id: 'spirit', name: '精霊使役学校', icon: '❋', role: '精霊使いの育成', description: '自然と精霊の声を聞く学び舎。契約の場を拡張し、精霊使いの育成枠を増やす。' }
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
const initialState = () => ({ version: 1, year: 1, month: 1, day: 1, phase: 'planning', resources: [1000, 600, 360], levels: [1, 1, 1, 1], queues: [[], [], [], []], plan: [0, 0, 0, 0], logs: [{ date: '1年 1月1日', text: '王国の運営を開始。施設を選び、強化命令を決めよう。' }], report: null, completed: [] });
let state = initialState();
let timer = null;
let toastTimer;
let modalOpener;
let resumeAfterModal = false;
let previousResourceValues = null;
const modal = document.querySelector('#modal');
const validState = s => s?.version === 1 && Number.isInteger(s.year) && s.year > 0 && Number.isInteger(s.month) && s.month >= 1 && s.month <= 12 && Number.isInteger(s.day) && s.day >= 1 && s.day <= daysInMonth(s.month) && ['planning', 'running'].includes(s.phase) && Array.isArray(s.resources) && s.resources.length === 3 && s.resources.every(n => Number.isSafeInteger(n) && n >= 0) && ['levels', 'plan'].every(key => Array.isArray(s[key]) && s[key].length === 4 && s[key].every(n => Number.isSafeInteger(n) && n >= 0)) && Array.isArray(s.queues) && s.queues.length === 4 && s.queues.every(q => Array.isArray(q) && q.every(t => Number.isInteger(t.level) && t.level > 0 && Number.isInteger(t.remaining) && t.remaining > 0 && t.remaining <= 5)) && Array.isArray(s.logs) && s.logs.every(l => typeof l.date === 'string' && typeof l.text === 'string') && Array.isArray(s.completed) && s.completed.every(v => typeof v === 'string') && (s.report === null || (typeof s.report.title === 'string' && Array.isArray(s.report.completed) && s.report.completed.every(v => typeof v === 'string')));
try { const saved = JSON.parse(localStorage.getItem(storageKey)); if (validState(saved)) state = saved; } catch {}
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
function queueHTML(i) { const q = state.queues[i]; return q.length ? `<div class="queue">Lv.${q[0].level}へ強化中 · あと${q[0].remaining}日${q.length > 1 ? ` / 待ち ${q.length - 1}件` : ''}<div class="progress" aria-label="強化の進捗"><span style="width:${(5 - q[0].remaining) * 20}%"></span></div></div>` : '<div class="queue">工事の予定はありません</div>'; }
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
    ${facilities.map((f, i) => `<button class="building-marker marker-${f.id} ${state.plan[i] ? 'selected' : state.queues[i].length ? 'building' : ''}" data-action="detail" data-index="${i}">
      <span class="marker-emblem" aria-hidden="true">${f.icon}</span>
      <span class="marker-name">${f.name}<small>Lv.${state.levels[i]}${state.plan[i] ? ` · 計画 ${state.plan[i]}` : ''}</small></span>
      ${state.queues[i].length ? `<span class="construction">⚒ あと${state.queues[i][0].remaining}日</span>` : ''}
    </button>`).join('')}
  </div>`;
}
function renderFacilities() {
  return `<section class="game-window facilities-window" aria-label="建築物一覧">
    ${windowTitle('建築物一覧', `<span class="tag">${state.phase === 'planning' ? '予算未決定' : '工事中'}</span>`)}
    <div class="window-content">
      <div class="ledger-labels"><span>施設</span><span>育成枠（仮）</span><span>工事・予算計画</span><span>確認</span></div>
      ${facilities.map((f, i) => `<div class="facility-row ${state.plan[i] ? 'selected-row' : ''}">
        <div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><div><h2>${f.name}</h2><small>${f.role}</small></div><span class="level">Lv.${state.levels[i]}</span></div>
        <div class="capacity"><b>${state.levels[i] * 5}</b><small>人 / 月</small></div>
        <div class="facility-work">${queueHTML(i)}${state.plan[i] ? `<small class="success">予算案：強化 ${state.plan[i]}段階</small>` : ''}</div>
        <div class="row-orders"><button data-action="detail" data-index="${i}" aria-label="${f.name}の詳細">詳細</button></div>
      </div>`).join('')}
    </div>
    <div class="window-bottom"><small>建物の状態を確認。強化への投資は予算で決める。</small><button data-action="navigate-plan">予算を確認</button></div>
  </section>`;
}
function budgetValues() {
  state.budget ??= [0, 0, 0, 0]; state.boost ??= [0, 0, 0, 0];
  state.maintenance ??= state.levels.map(level => level * 10);
  let remaining = monthlyIncome[0];
  state.maintenance = state.maintenance.map(n => { const paid = Math.min(remaining, Math.max(0, n)); remaining -= paid; return paid; });
  const maintenance = state.maintenance.reduce((a,b) => a+b, 0);
  const available = Math.max(0, monthlyIncome[0] - maintenance);
  state.budgetShare ??= state.budget.map(n => available ? Math.floor(n / available * 100) : 0);
  state.budget = state.budgetShare.map(n => Math.floor(available * n / 100));
  const allocation = state.budget.reduce((a, b) => a + b, 0);
  const boost = state.boost.reduce((a, b) => a + b, 0);
  const shortfall = Math.max(0, maintenance - monthlyIncome[0]);
  return { maintenance, available, allocation, boost, shortfall, deposit: Math.max(0, available - allocation), withdrawal: boost + shortfall };
}
function renderPlan() {
  const b = budgetValues(); const share = state.budgetShare.reduce((a,b)=>a+b,0); const locked = state.phase !== 'planning';
  const valid = b.allocation <= b.available && b.withdrawal <= state.resources[0];
  return `<section class="game-window budget-window" aria-label="今月の予算">
    ${windowTitle('今月の予算', '<span class="tag">建物の運営費を配分</span>')}
    <div class="window-content budget-content">
      <div class="allocation-summary"><div><h2>今月の配分</h2><p>運営費を0にすると稼働停止。残りの税収を100％として配分する。</p></div><div class="allocation-saving"><small>国庫へ積立</small><strong>${100-share}％</strong></div></div>
      <div class="allocation-bar" aria-label="予算配分">${state.budgetShare.map((n,i)=>n ? `<span class="allocation-segment allocation-${i}" style="flex:${n}" title="${facilities[i].name} ${n}％"></span>` : '').join('')}<span class="allocation-segment allocation-saved" style="flex:${100-share}" title="国庫へ積立 ${100-share}％"></span></div>
      <div class="budget-layout"><div class="building-budgets"><div class="budget-columns"><span>建築物・維持費</span><span>税収の配分率</span><span>国庫からブースト</span></div>
      ${facilities.map((f,i)=>`<article class="building-budget"><div class="budget-building"><span class="facility-icon">${f.icon}</span><div><h2>${f.name}</h2><small>Lv.${state.levels[i]} · ${state.maintenance[i] ? '稼働中' : '稼働停止・効果0'}</small><div class="maintenance-adjust"><span>運営費</span><button data-action="maintenance-down" data-index="${i}" aria-label="${f.name}の運営費を減らす" ${locked || !state.maintenance[i] ? 'disabled' : ''}>−</button><b>${state.maintenance[i]}</b><button data-action="maintenance-up" data-index="${i}" aria-label="${f.name}の運営費を増やす" ${locked || b.maintenance + 5 > monthlyIncome[0] ? 'disabled' : ''}>＋</button></div></div></div><div class="allocation-control"><div class="budget-adjust tax-accent"><button data-action="budget-down" data-index="${i}" aria-label="${f.name}の配分率を減らす" ${locked || !state.budgetShare[i] ? 'disabled' : ''}>−</button><strong>${state.budgetShare[i]}<small>％</small></strong><button data-action="budget-up" data-index="${i}" aria-label="${f.name}の配分率を増やす" ${locked || share + 5 > 100 ? 'disabled' : ''}>＋</button></div><input type="range" min="0" max="${100-share+state.budgetShare[i]}" step="1" value="${state.budgetShare[i]}" data-budget-index="${i}" aria-label="${f.name}の配分率" ${locked ? 'disabled' : ''}><small>配分額 ${state.budget[i]}</small></div><div class="budget-adjust reserve-accent"><button data-action="boost-down" data-index="${i}" aria-label="${f.name}のブーストを減らす" ${locked || !state.boost[i] ? 'disabled' : ''}>−</button><strong>${state.boost[i]}</strong><button data-action="boost-up" data-index="${i}" aria-label="${f.name}のブーストを増やす" ${locked || b.withdrawal + 5 > state.resources[0] ? 'disabled' : ''}>＋</button></div></article>`).join('')}</div>
      <aside class="budget-reserve"><h2>国庫の動き</h2><div><span>現在の国庫</span><b>${state.resources[0].toLocaleString()}</b></div><div class="reserve-accent"><span>今月のブースト</span><b>−${b.boost}</b></div>${b.shortfall ? `<div class="reserve-accent"><span>維持費の不足を補填</span><b>−${b.shortfall}</b></div>` : ''}<div class="tax-accent"><span>未配分を積立</span><b>＋${b.deposit}</b></div><div class="budget-balance"><span>予算確定後の国庫</span><strong>${(state.resources[0]+b.deposit-b.withdrawal).toLocaleString()}</strong></div><p>運営費0の建物は稼働停止。残った税収は国庫へ。ブーストは今月だけの追加支援。</p><small>建築・強化費用は別途、国庫から支払う。</small></aside></div>

    </div><div class="window-bottom"><div class="budget-footer"><span>建物へ配分 <b>${share}％</b></span><span>国庫へ積立 <b>${100-share}％</b></span></div><button class="primary" data-action="commit" ${locked || !valid ? 'disabled' : ''}>${locked ? '今月の予算は確定済み' : '今月の予算を確定'}</button></div></section>`;
}
function renderHistory() {
  return `<section class="game-window chronicle-window" aria-label="国家記録">${windowTitle('国家記録', `<button data-action="report" ${!state.report ? 'disabled' : ''}>月次報告</button>`)}<div class="window-content"><ul class="history-list">${state.logs.map(l => `<li><time>${escapeHTML(l.date)}</time><span>${escapeHTML(l.text)}</span></li>`).join('')}</ul></div></section>`;
}
function render() {
  const focus = document.activeElement;
  const focusAction = focus?.dataset.action;
  const focusIndex = focus?.dataset.index;
  const dateState = state.phase === 'planning'
    ? ['budget-pending', '予算未決定']
    : timer ? ['running', '時間進行中'] : ['paused', '時間停止中'];
  document.querySelector('#date').innerHTML = `<b>${dateText()}</b><small class="date-state date-state--${dateState[0]}">${dateState[1]}</small>`;
  const plannedResources = totalCost();
  document.querySelector('#resources').innerHTML = `<div class="resource resource--funds" aria-label="税収 ${monthlyIncome[0]}、月の収入" title="税収：月の収入"><span class="resource-symbol">${resourceIcons[0]}</span><span class="resource-name">税収</span><strong class="resource-value">${monthlyIncome[0].toLocaleString()}</strong></div>` + resourceNames.map((name, i) => {
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
  if (focusAction && !modal.open) {
    const replacement = [...document.querySelectorAll('[data-action]')].find(el => el.dataset.action === focusAction && el.dataset.index === focusIndex && !el.disabled);
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
  state.queues.forEach((q, i) => { if (!q.length) return; q[0].remaining--; if (!q[0].remaining) { const task = q.shift(); state.levels[i] = task.level; const text = `${facilities[i].name}がLv.${task.level}に到達`; state.completed.push(text); log(`${text}。育成枠（仮）は月${task.level * 5}人に拡張。`); } });
  if (newMonth) {
    stopTime(); state.phase = 'planning'; state.resources = state.resources.map((n, i) => n + (i ? monthlyIncome[i] : 0)); state.boost = [0, 0, 0, 0];
    state.report = { finance: state.monthBudget ? { ...state.monthBudget, building: state.monthBuilding || 0, closing: state.resources[0] } : null, title: `${state.year}年 ${state.month}月の月次報告`, completed: [...state.completed] }; state.completed = []; state.monthBudget = null; state.monthBuilding = 0; state.monthOpening = state.resources[0];
    log('新しい月の税収100を配分できる。木材50、鉱石30を受け取り。');
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
function showDetail(i) { const f = facilities[i]; const next = targetLevel(i); openModal(f.name, `<div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><span class="level">Lv.${state.levels[i]}</span></div><p>${f.description}</p><div class="report"><div class="report-row"><span>次に追加する強化</span><b>Lv.${next - 1} → Lv.${next}</b></div><div class="report-row"><span>育成枠（仮）</span><b>${(next - 1) * 5} → ${next * 5}人 / 月</b></div><div class="report-row"><span>工期</span><b>5日${state.queues[i].length + state.plan[i] ? '（先行工事の完了後）' : ''}</b></div></div>${costsHTML(cost(next))}<div class="separator"></div>${queueHTML(i)}${state.queues[i].length ? `<p class="muted">予約順：${state.queues[i].map(t => `Lv.${t.level}（${t.remaining}日）`).join(' → ')}</p>` : ''}${state.plan[i] ? `<p class="success">未決定の計画：${state.plan[i]}段階</p>` : ''}`, `<button data-action="close">戻る</button><button class="primary" data-action="navigate-budget" data-index="${i}" ${state.phase !== 'planning' ? 'disabled' : ''}>運営予算を配分する</button><button data-action="build" data-index="${i}" ${cost(next).some((n,j)=>n>state.resources[j]) ? 'disabled' : ''}>国庫で建物を強化</button>`); }
function commit() {
  const b = budgetValues();
  if (state.phase !== 'planning' || b.allocation > b.available || b.withdrawal > state.resources[0]) return;
  state.monthBudget = { year: state.year, month: state.month, opening: state.monthOpening ?? state.resources[0], tax: monthlyIncome[0], maintenance: b.maintenance, allocation: b.allocation, boost: b.boost, shortfall: b.shortfall, deposit: b.deposit, shares: [...state.budgetShare], budgets: [...state.budget], boosts: [...state.boost] };
  state.resources[0] += b.deposit - b.withdrawal;
  log(`今月の予算を確定：維持費 ${b.maintenance}、追加予算 ${b.allocation}、国庫ブースト ${b.boost}、積立 ${b.deposit}。`);
  state.phase = 'running'; save(); location.hash = 'kingdom'; startTime(); toast('予算を確定。時間を自動で進めています');
}
function showReport() {
  if (!state.report) return;
  const f = state.report.finance;
  const money = n => n.toLocaleString();
  const change = f ? f.closing - f.opening : 0;
  modal.classList.add('monthly-report');
  const body = f ? `<div class="month-result"><span>${f.year}年${f.month}月の運営結果</span><div><small>国庫の変化</small><strong class="${change >= 0 ? 'tax-accent' : 'reserve-accent'}">${change >= 0 ? '＋' : '−'}${money(Math.abs(change))}</strong></div><p>月初 ${money(f.opening)} <span>→</span> 月末 <b>${money(f.closing)}</b></p></div>
    <div class="month-ledgers"><section><h3>税収の使い道</h3><div class="month-line month-total"><span>税収</span><b>${money(f.tax)}</b></div><div class="month-line"><span>建物の運営費</span><b>${money(f.maintenance - f.shortfall)}</b></div><div class="month-line"><span>建物への追加予算</span><b>${money(f.allocation)}</b></div><div class="month-line tax-accent"><span>国庫へ積立</span><b>＋${money(f.deposit)}</b></div></section><section><h3>国庫からの支出</h3><div class="month-line"><span>建築・強化</span><b>${money(f.building)}</b></div><div class="month-line reserve-accent"><span>運営ブースト</span><b>${money(f.boost)}</b></div><div class="month-line"><span>維持費の不足を補填</span><b>${money(f.shortfall)}</b></div></section></div>
    <section class="month-buildings"><h3>建物への配分記録</h3><div class="month-building-head"><span>建物</span><span>配分率</span><span>配分額</span><span>ブースト</span></div>${facilities.map((building,i)=>`<div class="month-building-row"><span>${building.name}</span><b class="tax-accent">${f.shares[i]}％</b><span>${money(f.budgets[i])}</span><span class="reserve-accent">${money(f.boosts[i])}</span></div>`).join('')}</section>` : '<p class="muted">この月の収支記録はありません。新しい予算を確定した月から収支を記録します。</p>';
  openModal('月次報告', `${body}<div class="month-completed"><h3>完成した工事</h3>${state.report.completed.length ? state.report.completed.map(t=>`<p>${escapeHTML(t)}</p>`).join('') : '<p class="muted">今月の完成工事はありません。</p>'}</div>`, '<button data-action="close">城下町へ戻る</button><button class="primary" data-action="report-plan">次月の予算を配分</button>');
}
function showSettings() { openModal('設定・保存', '<p class="muted">記録は随時、自動保存する。手動保存は独立した1枠に残る。読込後は時間を停止した状態で再開する。</p><div class="actions"><button data-action="save">手動保存する</button><button data-action="load">手動保存を読込</button></div><div class="separator"></div><h3>試作のルール</h3><p class="muted">初期資源：資金1,000 / 木材600 / 鉱石360<br>月収：資金100 / 木材50 / 鉱石30<br>強化費用：目標Lv. × 100 / 50 / 30<br>工期：5日。育成枠：Lv. × 5人 / 月（仮）<br>領土は1領固定。閏年は扱いません。</p>', '<button class="danger" data-action="reset-confirm">最初からやり直す</button><button data-action="close">閉じる</button>'); }
document.addEventListener('click', event => {
  if (modal.open && event.target === modal && event.clientY <= document.querySelector('.hud').getBoundingClientRect().bottom) { closeModal(); return; }
  if (!modal.open && document.querySelector('.game-window') && !event.target.closest('.game-window')) { location.hash = 'kingdom'; return; }
  const button = event.target.closest('button[data-action]'); if (!button || button.disabled) return;
  const action = button.dataset.action; const i = Number(button.dataset.index);
  if (action === 'navigate-budget') { closeModal(); location.hash = 'plan'; render(); const target = document.querySelector(`[data-action="add"][data-index="${i}"]`); target?.scrollIntoView({ block: 'nearest' }); target?.focus({ preventScroll: true }); return; }
  if (action.startsWith('navigate-')) { location.hash = action.replace('navigate-', ''); return; }
  if (action === 'resource-detail' && Number.isInteger(i) && resourceNames[i]) showResourceDetails(i);
  if (action === 'build') { const next = state.levels[i] + state.queues[i].length + 1; const price = cost(next); if (price.every((n,j)=>n<=state.resources[j])) { state.monthOpening ??= state.resources[0]; state.monthBuilding = (state.monthBuilding || 0) + price[0]; state.resources = state.resources.map((n,j)=>n-price[j]); state.queues[i].push({level:next,remaining:5}); log(`${facilities[i].name}の強化を国庫で発注。資金 ${price[0]}。`); closeModal(); save(); render(); } return; }
  if (action === 'detail') showDetail(i);
  if (['add', 'remove'].includes(action) && state.phase === 'planning') { state.plan[i] = Math.max(0, state.plan[i] + (action === 'remove' ? -1 : 1)); save(); render(); if (action === 'add') toast(`${facilities[i].name}の強化を追加しました`); }
  if (action === 'clear-plan') { state.plan = [0, 0, 0, 0]; save(); render(); toast('未決定の計画をクリアしました'); }
  if (action === 'close') closeModal();

  if (action === 'maintenance-up' || action === 'maintenance-down') { if (state.phase !== 'planning') return; const b = budgetValues(); const up = action.endsWith('up'); if (!up || b.maintenance + 5 <= monthlyIncome[0]) state.maintenance[i] = Math.max(0, state.maintenance[i] + (up ? 5 : -5)); budgetValues(); save(); render(); return; }
  if (/^(budget|boost)-(up|down)$/.test(action) && state.phase === 'planning') { const b = budgetValues(); const up = action.endsWith('up'); if (action.startsWith('budget')) { const used = state.budgetShare.reduce((a,b)=>a+b,0); if (!up || used + 5 <= 100) state.budgetShare[i] = Math.max(0, state.budgetShare[i] + (up ? 5 : -5)); } else if (!up || b.withdrawal + 5 <= state.resources[0]) state.boost[i] = Math.max(0, state.boost[i] + (up ? 5 : -5)); save(); render(); return; }
  if (action === 'commit') commit();
  if (action === 'toggle-time') { if (timer) { stopTime(); render(); } else startTime(); }
  if (action === 'day') { stopTime(); advanceDay(); }
  if (action === 'next-event') { stopTime(); const days = nextEventDays(); for (let n = 0; n < days && state.phase === 'running'; n++) advanceDay(); }
  if (action === 'month') { stopTime(); for (let n = 0; n < 31 && state.phase === 'running'; n++) advanceDay(); }
  if (action === 'report') showReport();
  if (action === 'report-plan') { closeModal(); location.hash = 'plan'; }
  if (action === 'settings') showSettings();
  if (action === 'save') save(true);
  if (action === 'load') { try { const saved = JSON.parse(localStorage.getItem(manualSaveKey)); if (!validState(saved)) throw new Error(); stopTime(); resumeAfterModal = false; state = saved; save(); closeModal(); render(); toast('手動保存した国家状態を読み込みました'); } catch { toast('手動保存がありません。先に手動保存してください'); } }
  if (action === 'reset-confirm') openModal('王国を最初からやり直す', '<p>施設レベル、工事、資源、国家記録を初期状態へ戻します。自動保存と手動保存も初期化されます。</p>', '<button data-action="settings">戻る</button><button class="danger" data-action="reset">初期状態に戻す</button>');
  if (action === 'reset') { stopTime(); resumeAfterModal = false; state = initialState(); try { localStorage.removeItem(manualSaveKey); } catch {} save(); closeModal(); location.hash = 'kingdom'; render(); toast('王国を初期状態に戻しました'); }
});
document.addEventListener('change', event => { const i = event.target.dataset.budgetIndex; if (i === undefined || state.phase !== 'planning') return; const used = state.budgetShare.reduce((a,b)=>a+b,0)-state.budgetShare[i]; state.budgetShare[i] = Math.min(100-used, Math.max(0, Number(event.target.value))); budgetValues(); save(); render(); });
modal.addEventListener('click', event => { const r = modal.getBoundingClientRect(); if (event.target === modal && (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom)) closeModal(); });
modal.addEventListener('cancel', event => { event.preventDefault(); closeModal(); });
window.addEventListener('hashchange', () => { if (modal.open) closeModal(); render(); document.querySelector('#view').focus({ preventScroll: true }); });
document.addEventListener('keydown', event => {
  if (modal.open || event.ctrlKey || event.metaKey || event.altKey) return;
  if (event.key === 'Escape') { location.hash = 'kingdom'; return; }
  if (/^[1-4]$/.test(event.key)) location.hash = ['kingdom', 'facilities', 'plan', 'history'][Number(event.key) - 1];
  if (event.code === 'Space' && state.phase === 'running' && event.target.tagName !== 'BUTTON') {
    event.preventDefault();
    if (timer) { stopTime(); render(); } else startTime();
  }
});
window.addEventListener('pagehide', () => { stopTime(); save(); });
render();
