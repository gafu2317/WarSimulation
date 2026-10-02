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
function log(text) { state.logs.unshift({ date: dateText(), text }); state.logs = state.logs.slice(0, 200); }
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
  return `<section class="game-window facilities-window" aria-label="施設整備">
    ${windowTitle('施設整備', `<span class="tag">${state.phase === 'planning' ? '予算未決定' : '工事中'}</span>`)}
    <div class="window-content">
      <div class="ledger-labels"><span>施設</span><span>育成枠（仮）</span><span>強化予定・工事</span><span>命令</span></div>
      ${facilities.map((f, i) => `<div class="facility-row ${state.plan[i] ? 'selected-row' : ''}">
        <div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><div><h2>${f.name}</h2><small>${f.role}</small></div><span class="level">Lv.${state.levels[i]}</span></div>
        <div class="capacity"><b>${state.levels[i] * 5}</b><small>人 / 月</small></div>
        <div class="facility-work"><b>${state.plan[i] ? `選択中 Lv.${state.levels[i] + state.queues[i].length} → Lv.${targetLevel(i) - 1}` : `次の強化 Lv.${targetLevel(i)}`}</b>${costsHTML(state.plan[i] ? facilityPlanCost(i) : cost(targetLevel(i)))}${queueHTML(i)}</div>
        <div class="row-orders"><button data-action="detail" data-index="${i}" aria-label="${f.name}の詳細">詳細</button><button data-action="add" data-index="${i}" ${state.phase !== 'planning' ? 'disabled' : ''}>＋ 計画</button>${state.plan[i] ? `<small class="success">計画 ${state.plan[i]}段階</small>` : ''}</div>
      </div>`).join('')}
    </div>
    <div class="window-bottom"><small>強化工期 5日 / 同施設は順次、別施設は並行</small><button class="primary" data-action="navigate-plan">命令を確認</button></div>
  </section>`;
}
function renderPlan() {
  const total = totalCost();
  const planning = state.phase === 'planning';
  return `<section class="game-window council-window" aria-label="国家運営の計画">
    ${windowTitle('国家運営', `<span class="tag">${state.phase === 'planning' ? '予算未決定' : '今月の命令は決定済み'}</span>`)}
    <div class="window-content council-body">
      <div class="investment-ledger"><h2>施設強化の命令</h2>
        ${facilities.map((f, i) => `<div class="plan-row ${state.plan[i] ? 'selected-row' : ''}">
          <span class="facility-icon" aria-hidden="true">${f.icon}</span>
          <div class="info"><h3>${f.name}</h3><small>Lv.${state.levels[i]}${state.queues[i].length ? ` / 工事 ${state.queues[i].length}件` : ''}</small>${!planning ? queueHTML(i) : ''}${state.plan[i] ? `<div class="success">Lv.${targetLevel(i) - 1}まで / ${state.plan[i] * 5}日${state.queues[i].length ? '＋先行工事' : ''}</div>${costsHTML(facilityPlanCost(i))}` : ''}</div>
          <div class="stepper"><button aria-label="${f.name}の計画を減らす" data-action="remove" data-index="${i}" ${!state.plan[i] || state.phase !== 'planning' ? 'disabled' : ''}>−</button><b>${state.plan[i]}</b><button aria-label="${f.name}の計画を増やす" data-action="add" data-index="${i}" ${state.phase !== 'planning' ? 'disabled' : ''}>＋</button></div>
        </div>`).join('')}
        <small class="rule-note">強化1段階で育成枠（仮）が月5人増える。兵士育成・戦闘は未実装。</small>
      </div>
      <div class="treasury"><h2>国庫</h2><table><thead><tr><th>資源</th><th>${planning ? '支払い' : '翌月の収入'}</th><th>${planning ? '支払い後' : '現在'}</th></tr></thead><tbody>${resourceNames.map((name, i) => `<tr><th>${name}</th><td>${planning ? `−${total[i].toLocaleString()}` : `+${monthlyIncome[i]}`}</td><td class="${total[i] > state.resources[i] ? 'shortage' : 'success'}">${(state.resources[i] - (planning ? total[i] : 0)).toLocaleString()}</td></tr>`).join('')}</tbody></table><div class="planned-total"><small>${planning ? '強化命令' : '工事中'}</small><strong>${planning ? countPlan() : constructionCount()}<small>件</small></strong></div>${!affordable() ? `<p class="warning">${resourceNames.filter((_, i) => total[i] > state.resources[i]).join('・')}が不足</p>` : `<p class="muted">${state.phase !== 'planning' ? '費用は支払い済み。工事を進め、翌月に次の命令を選ぶ。' : countPlan() ? '命令決定時に費用を支払う。' : '強化せずに今月を進めることも可能。'}</p>`}</div>
    </div>
    <div class="window-bottom">${planning ? `<button data-action="clear-plan" ${!countPlan() ? 'disabled' : ''}>命令を取り消す</button><button class="primary" data-action="confirm" ${!affordable() ? 'disabled' : ''}>${countPlan() ? '命令を決定' : '今月は強化しない'}</button>` : `<small>${nextEventText()}</small><button class="primary" data-action="${constructionCount() ? 'next-event' : 'month'}">${constructionCount() ? '工事完了まで進む' : '翌月へ進む'}</button>`}</div>
  </section>`;
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
  document.querySelector('#resources').innerHTML = resourceNames.map((name, i) => {
    const amount = state.resources[i];
    const delta = previousResourceValues ? amount - previousResourceValues[i] : 0;
    const short = state.phase === 'planning' && countPlan() > 0 && plannedResources[i] > amount;
    return `<button type="button" class="resource resource--${resourceKinds[i]}${short ? ' is-short' : ''}${delta ? ' is-changed' : ''}" data-action="resource-detail" data-index="${i}" aria-label="${name} ${amount.toLocaleString()}、月収 +${monthlyIncome[i]}、内訳を表示">
      <span class="resource-topline"><span class="resource-symbol">${resourceIcons[i]}</span><span class="resource-name">${name}</span><small class="resource-income">+${monthlyIncome[i]}/月</small></span>
      <strong class="resource-value">${amount.toLocaleString()}</strong>
      ${delta ? `<span class="resource-delta${delta < 0 ? ' is-loss' : ''}" aria-hidden="true">${delta > 0 ? '+' : '−'}${Math.abs(delta).toLocaleString()}</span>` : ''}
    </button>`;
  }).join('');
  previousResourceValues = [...state.resources];
  const badge = document.querySelector('#plan-count');
  badge.textContent = countPlan();
  badge.hidden = !countPlan();
  const planButton = document.querySelector('.command-dock [data-route="plan"]');
  const budgetPending = state.phase === 'planning';
  planButton.querySelector('b').textContent = budgetPending ? '予算を決める' : '国家運営';
  planButton.setAttribute('aria-label', budgetPending ? '今月の予算を決める' : '国家運営を開く');
  planButton.classList.toggle('needs-action', budgetPending);
  document.querySelectorAll('[data-route]').forEach(button => {
    const active = button.dataset.route === route();
    button.classList.toggle('active', active);
    button.setAttribute('aria-pressed', active);
  });
  document.querySelector('#view').innerHTML = ({ kingdom: renderKingdom, facilities: renderFacilities, plan: renderPlan, history: renderHistory })[route()]();
  document.querySelector('#time-controls').innerHTML = state.phase === 'planning'
    ? ''
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
    stopTime(); state.phase = 'planning'; state.resources = state.resources.map((n, i) => n + monthlyIncome[i]);
    state.report = { title: `${state.year}年 ${state.month}月の月次報告`, completed: [...state.completed] }; state.completed = [];
    log('月初の収入：資金 +100、木材 +50、鉱石 +30。新しい運営計画を開始。');
  }
  save(); render(); if (newMonth) showReport();
}
function openModal(title, body, actions = '') {
  if (!modal.open) { modalOpener = document.activeElement; resumeAfterModal = Boolean(timer); stopTime(); render(); }
  document.querySelector('#modal-body').innerHTML = `<div class="modal-heading"><h2 id="modal-title">${title}</h2><button class="close" data-action="close" aria-label="閉じる">×</button></div>${resumeAfterModal ? '<p class="muted">表示中は時間を一時停止しています。閉じると再開します。</p>' : ''}${body}<div class="actions end" style="margin-top:24px">${actions}</div>`;
  if (!modal.open) modal.showModal();
}
function closeModal() { modal.close(); modal.classList.remove('resource-detail'); if (resumeAfterModal) startTime(); resumeAfterModal = false; if (modalOpener?.isConnected) modalOpener.focus(); else if (modalOpener?.dataset.action === 'resource-detail') document.querySelector(`[data-action="resource-detail"][data-index="${modalOpener.dataset.index}"]`)?.focus({ preventScroll: true }); else document.querySelector('#view').focus({ preventScroll: true }); }
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
function showDetail(i) { const f = facilities[i]; const next = targetLevel(i); openModal(f.name, `<div class="facility-heading"><span class="facility-icon" aria-hidden="true">${f.icon}</span><span class="level">Lv.${state.levels[i]}</span></div><p>${f.description}</p><div class="report"><div class="report-row"><span>次に追加する強化</span><b>Lv.${next - 1} → Lv.${next}</b></div><div class="report-row"><span>育成枠（仮）</span><b>${(next - 1) * 5} → ${next * 5}人 / 月</b></div><div class="report-row"><span>工期</span><b>5日${state.queues[i].length + state.plan[i] ? '（先行工事の完了後）' : ''}</b></div></div>${costsHTML(cost(next))}<div class="separator"></div>${queueHTML(i)}${state.queues[i].length ? `<p class="muted">予約順：${state.queues[i].map(t => `Lv.${t.level}（${t.remaining}日）`).join(' → ')}</p>` : ''}${state.plan[i] ? `<p class="success">未決定の計画：${state.plan[i]}段階</p>` : ''}`, `<button data-action="close">戻る</button><button class="primary" data-action="detail-add" data-index="${i}" ${state.phase !== 'planning' ? 'disabled' : ''}>計画に追加する</button>`); }
function showConfirm() {
  if (state.phase !== 'planning' || !affordable()) return;
  const total = totalCost();
  const nextAction = countPlan() || constructionCount() ? '工事まで' : '月次報告';
  const orders = state.plan.some(Boolean)
    ? state.plan.map((n, i) => n ? `<div class="report-row"><span>${facilities[i].name}</span><b>${n}段階 / Lv.${targetLevel(i) - 1}まで</b></div>` : '').join('')
    : '<p>今月は施設への投資を見送ります。</p>';
  const body = `<p>今月の予算を確定する。強化を選んだ場合は、表示の資源を支払い工事を予約する。</p><div class="report">${orders}</div><h3>支払い費用</h3>${costsHTML(total)}<h3 style="margin-top:22px">決定後の残高</h3>${costsHTML(state.resources.map((n, i) => n - total[i]))}<p class="muted" style="margin-top:22px">確定後は城下町へ戻る。日付横の「▶ 進行」で自動進行、「1日」で1日ずつ、「${nextAction}」で次の節目まで進められる。時間は必要なときに停止できる。</p>`;
  const confirmLabel = countPlan() ? '工事を予約して時間操作へ' : '強化なしで時間操作へ';
  openModal('今月の予算を確定', body, `<button data-action="close">計画に戻る</button><button class="primary" data-action="commit">${confirmLabel}</button>`);
}
function commit() {
  if (state.phase !== 'planning' || !affordable()) return;
  const total = totalCost();
  state.resources = state.resources.map((n, i) => n - total[i]);
  state.plan.forEach((n, i) => {
    const start = state.levels[i] + state.queues[i].length;
    for (let j = 1; j <= n; j++) state.queues[i].push({ level: start + j, remaining: 5 });
  });
  log(`運営計画を決定：施設強化 ${countPlan()}件。資金 ${total[0]}、木材 ${total[1]}、鉱石 ${total[2]}を支払い。`);
  state.plan = [0, 0, 0, 0];
  state.phase = 'running';
  closeModal();
  save();
  location.hash = 'kingdom';
  render();
  const nextAction = constructionCount() ? '工事まで' : '月次報告';
  toast(`時間停止中。日付横の「▶ 進行」か「${nextAction}」で進める`);
}
function showReport() { if (!state.report) return; openModal(escapeHTML(state.report.title), `<span class="tag">月初の収入を受け取りました</span><div class="report">${resourceNames.map((n, i) => `<div class="report-row"><span>${n}</span><b class="success">+${monthlyIncome[i]}</b></div>`).join('')}</div><h3>今回の完成結果</h3>${state.report.completed.length ? `<ul>${state.report.completed.map(t => `<li>${escapeHTML(t)}</li>`).join('')}</ul>` : '<p class="muted">完了した工事はありません。</p>'}<p class="muted">${state.queues.reduce((n, q) => n + q.length, 0)}件の工事を継続中。報告を確認したら、次の月の施設を選ぼう。</p><small>施設効果は育成枠（仮）の表示。兵士育成・戦闘は未実装。</small>`, '<button data-action="close">報告を閉じる</button><button class="primary" data-action="report-plan">次の月の施設を選ぶ →</button>'); }
function showSettings() { openModal('設定・保存', '<p class="muted">記録は随時、自動保存する。手動保存は独立した1枠に残る。読込後は時間を停止した状態で再開する。</p><div class="actions"><button data-action="save">手動保存する</button><button data-action="load">手動保存を読込</button></div><div class="separator"></div><h3>試作のルール</h3><p class="muted">初期資源：資金1,000 / 木材600 / 鉱石360<br>月収：資金100 / 木材50 / 鉱石30<br>強化費用：目標Lv. × 100 / 50 / 30<br>工期：5日。育成枠：Lv. × 5人 / 月（仮）<br>領土は1領固定。閏年は扱いません。</p>', '<button class="danger" data-action="reset-confirm">最初からやり直す</button><button data-action="close">閉じる</button>'); }
document.addEventListener('click', event => {
  const button = event.target.closest('button[data-action]'); if (!button || button.disabled) return;
  const action = button.dataset.action; const i = Number(button.dataset.index);
  if (action.startsWith('navigate-')) { location.hash = action.replace('navigate-', ''); return; }
  if (action === 'resource-detail' && Number.isInteger(i) && resourceNames[i]) showResourceDetails(i);
  if (action === 'detail') showDetail(i);
  if (['add', 'detail-add', 'remove'].includes(action) && state.phase === 'planning') { state.plan[i] = Math.max(0, state.plan[i] + (action === 'remove' ? -1 : 1)); save(); render(); if (action === 'detail-add') { closeModal(); toast(`${facilities[i].name}の強化を計画に追加しました`); } else if (action === 'add') toast(`${facilities[i].name}の強化を追加しました`); }
  if (action === 'clear-plan') { state.plan = [0, 0, 0, 0]; save(); render(); toast('未決定の計画をクリアしました'); }
  if (action === 'close') closeModal();
  if (action === 'confirm') showConfirm();
  if (action === 'commit') commit();
  if (action === 'toggle-time') { if (timer) { stopTime(); render(); } else startTime(); }
  if (action === 'day') { stopTime(); advanceDay(); }
  if (action === 'next-event') { stopTime(); const days = nextEventDays(); for (let n = 0; n < days && state.phase === 'running'; n++) advanceDay(); }
  if (action === 'month') { stopTime(); for (let n = 0; n < 31 && state.phase === 'running'; n++) advanceDay(); }
  if (action === 'report') showReport();
  if (action === 'report-plan') { closeModal(); location.hash = 'facilities'; }
  if (action === 'settings') showSettings();
  if (action === 'save') save(true);
  if (action === 'load') { try { const saved = JSON.parse(localStorage.getItem(manualSaveKey)); if (!validState(saved)) throw new Error(); stopTime(); resumeAfterModal = false; state = saved; save(); closeModal(); render(); toast('手動保存した国家状態を読み込みました'); } catch { toast('手動保存がありません。先に手動保存してください'); } }
  if (action === 'reset-confirm') openModal('王国を最初からやり直す', '<p>施設レベル、工事、資源、国家記録を初期状態へ戻します。自動保存と手動保存も初期化されます。</p>', '<button data-action="settings">戻る</button><button class="danger" data-action="reset">初期状態に戻す</button>');
  if (action === 'reset') { stopTime(); resumeAfterModal = false; state = initialState(); try { localStorage.removeItem(manualSaveKey); } catch {} save(); closeModal(); location.hash = 'kingdom'; render(); toast('王国を初期状態に戻しました'); }
});
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
