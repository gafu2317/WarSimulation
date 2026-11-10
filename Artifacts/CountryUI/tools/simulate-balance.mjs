import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
import vm from 'node:vm';
import { createHash } from 'node:crypto';

const root = fileURLToPath(new URL('../', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => args.includes(name) ? args[args.indexOf(name) + 1] : fallback;
const seedCount = Number(option('--seeds', 64));
const months = Number(option('--months', 12));
if (!Number.isInteger(seedCount) || seedCount < 1 || !Number.isInteger(months) || months < 1) throw new Error('seeds and months must be positive integers');
const output = resolve(root, option('--output', 'reports/balance-simulation.json'));
const sourceFiles = ['js/balance.js', 'js/balance-combat.js', 'js/country-v131.js'].map(path => ({ path, text: readFileSync(resolve(root, path), 'utf8') }));
const source = sourceFiles.map(({ path, text }) => new vm.Script(text.split("document.addEventListener('dragstart'")[0], { filename: path }));

function browserShell() {
  const element = () => ({ innerHTML: '', outerHTML: '', textContent: '', open: false, isConnected: true, classList: { add() {}, remove() {}, toggle() {} }, focus() {}, setAttribute() {}, showModal() { this.open = true; }, close() { this.open = false; }, querySelector: () => element(), querySelectorAll: () => [] });
  const nodes = new Map();
  return vm.createContext({
    document: { activeElement: null, querySelector(selector) { if (!nodes.has(selector)) nodes.set(selector, element()); return nodes.get(selector); }, querySelectorAll: () => [], createElement: () => element() },
    localStorage: { getItem: () => null, setItem() {} }, location: { hash: '#kingdom' },
    setTimeout: () => 1, clearTimeout() {}, setInterval: () => 1, clearInterval() {}
  });
}

function simulationDriver({ difficulty, seed, strategy, months, scenario }) {
  // UI rendering and wall-clock scheduling are excluded; every economy/combat/day function remains the game implementation.
  render = toast = save = showExpeditionReport = showDefenseResult = showAttackArrival = showDefenseAttack = () => {};
  openModal = closeModal = startTime = () => {};
  openMonthlySettings = () => restoreMonthlyDraft();
  state = initialState(difficulty, seed);
  if (scenario === 'low-stock') state.resources = state.resources.map(n => Math.floor(n / 4));
  if (scenario === 'no-wood') state.resources[1] = 0;
  if (scenario === 'frequent-raids') state.defenseChance = 25;
  initializeBalance(); extendBuildings(); initializeRecovery(); initializeBudgetPolicy(); syncWeapons(); initializeCombatState(); restoreMonthlyDraft();
  const initialResources = [...state.resources], firstDate = recoveryDate(), targetMonth = months + 1;
  const main = [...Array(12).keys()];
  const metrics = { difficulty, seed, strategy, scenario, days: 0, completedMonths: 0, resources: [], minimumResources: [...state.resources], levels: [], builds: 0, trainingCompleted: 0, recoveryCompleted: 0, cpZeroEvents: 0, mainAvailable: 0, rosterAvailable: 0, raids: 0, defenseWins: 0, defenseLosses: 0, expeditions: 0, expeditionWins: 0, expeditionLosses: 0, supplies: 0, rewards: [0, 0, 0], production: 0, tax: 0, allocation: 0, withdrawals: 0, marketIncome: 0, productionRefund: 0, ledger: {}, failures: [], monthly: [] };
  let eventDelta = [0, 0, 0], completedMonth = 0;
  const addDelta = (name, before) => {
    const delta = state.resources.map((n, i) => n - before[i]);
    const total = metrics.ledger[name] ||= [0, 0, 0];
    delta.forEach((n, i) => { total[i] += n; eventDelta[i] += n; });
    metrics.minimumResources = state.resources.map((n, i) => Math.min(n, metrics.minimumResources[i]));
    if (state.resources.some(n => !Number.isSafeInteger(n) || n < 0)) metrics.failures.push(`invalid-resource:${name}:${dateText()}`);
    return delta;
  };
  const originalBudget = applyMonthBudget, originalBuild = startConstruction, originalExpedition = advanceExpedition, originalDefense = resolveDefenseAttack, originalProduction = produceWeapons, originalRecovery = startRecovery;
  applyMonthBudget = (...args) => { const before = [...state.resources], previous = state.monthBudget; const result = originalBudget(...args); addDelta('budget', before); if (state.monthBudget !== previous) { metrics.tax += state.monthBudget.tax; metrics.allocation += state.monthBudget.allocation; metrics.withdrawals += state.monthBudget.withdrawal; } return result; };
  startConstruction = job => { const before = [...state.resources]; const result = originalBuild(job); addDelta('construction', before); if (result) metrics.builds++; return result; };
  advanceExpedition = () => { const before = [...state.resources], previous = new Set(state.expeditionReports || []); const result = originalExpedition(); addDelta('expeditionReturn', before); for (const report of state.expeditionReports || []) if (!previous.has(report)) { if (report.outcome === 'victory') metrics.expeditionWins++; else if (report.outcome === 'defeat') metrics.expeditionLosses++; report.loot.forEach((n, i) => metrics.rewards[i] += n); } return result; };
  resolveDefenseAttack = () => { const before = [...state.resources], attack = state.defenseAttack; const result = originalDefense(); addDelta('defense', before); if (attack && !state.defenseAttack) { metrics.raids++; if (['victory', '防衛成功'].includes(state.defenseReport.outcome)) metrics.defenseWins++; else metrics.defenseLosses++; } return result; };
  produceWeapons = () => { const before = [...state.resources], expected = productionSettlement(); const made = originalProduction(); const delta = addDelta('production', before); metrics.production += made.length; metrics.productionRefund += delta[0]; if (made.length !== expected.count || delta[0] !== expected.refund || delta[1] !== -made.length * CountryBalance.rules.productionWood || delta[2] !== -made.length * CountryBalance.rules.productionOre) metrics.failures.push(`production-mismatch:${dateText()}`); return made; };
  startRecovery = c => { if (c.cp === 0) metrics.cpZeroEvents++; return originalRecovery(c); };
  const upkeepTotal = () => state.levels.reduce((n, level) => n + CountryBalance.upkeep(level), 0);
  const weaponRanking = new Map();
  const weaponFor = id => {
    const key = `${weapons.length}:${state.characters[id].skills.join('|')}`;
    if (weaponRanking.get(id)?.key !== key) weaponRanking.set(id, { key, weapons: weapons.map((_, weapon) => ({ weapon, power: memberCombatPower({ id, weapon }) })).sort((a, b) => b.power - a.power || a.weapon - b.weapon).map(item => item.weapon) });
    return weaponRanking.get(id).weapons.find(weapon => weaponAvailable(weapon, id)) ?? 0;
  };
  function chooseDefense() {
    for (const c of state.characters) c.defending = false;
    state.defenseWeapons = {};
    const count = scenario === 'overextended' ? 4 : strategy === 'stability' ? 10 : 8;
    const candidates = state.characters.map((c, id) => ({ c, id })).filter(({ c, id }) => (scenario !== 'overextended' || id < 4) && c.qualified && c.cp > 0 && !c.task && !onExpedition(c)).sort((a, b) => Number(b.id < 12) - Number(a.id < 12) || b.c.cp - a.c.cp || a.id - b.id).slice(0, count);
    for (const { c, id } of candidates) { c.defending = true; state.defenseWeapons[id] = weaponFor(id); }
  }
  function chooseBudget() {
    const desired = state.levels.map((level, i) => CountryBalance.upkeep(level));
    for (let i = 0; i < 3; i++) desired[i] += Math.min(10 * Math.min(5, state.levels[i]), strategy === 'stability' ? 6 : strategy === 'growth' ? 4 : 8);
    desired[5] = CountryBalance.budgetCap(5, state.levels[5]);
    desired[6] = CountryBalance.budgetCap(6, state.levels[6]);
    desired[4] = CountryBalance.budgetCap(4, state.levels[4]);
    const reserve = upkeepTotal() * (strategy === 'stability' ? 2 : 1);
    const pool = taxIncome() + Math.max(0, state.resources[0] - reserve);
    let excess = Math.max(0, desired.reduce((a, b) => a + b, 0) - pool);
    for (const i of [6, 5, 2, 1, 0, 4]) { const trim = Math.min(excess, desired[i] - CountryBalance.upkeep(state.levels[i])); desired[i] -= trim; excess -= trim; }
    state.budgetAmounts = desired;
    state.useTreasury = desired.reduce((a, b) => a + b, 0) > taxIncome();
    return budgetValues();
  }
  function chooseTraining() {
    state.trainingDraft = state.characters.map(() => null);
    const occupied = new Set(state.characters.filter(c => c.task).map(c => `${c.task.school}:${c.task.slot}`));
    const candidates = state.characters.map((c, id) => ({ c, id })).filter(({ c }) => !c.task && !recovering(c) && !onExpedition(c)).sort((a, b) => Number(b.c.cp === 0) - Number(a.c.cp === 0) || Number(b.id < 12) - Number(a.id < 12) || a.id - b.id);
    for (const { c, id } of candidates) {
      const preferred = { STR: 0, INT: 1, FAI: 2 }[CountryCombat.primary(weapons[weaponFor(id)].type)];
      const schools = c.cp === 0 ? [preferred, ...[0, 1, 2].filter(i => i !== preferred)] : [preferred];
      for (const school of schools) {
        if (c.cp > 0 && c.skills.includes(trainingSkill(school))) continue;
        let slot = 0; while (occupied.has(`${school}:${slot}`)) slot++;
        if (slot >= trainingLimit(school)) continue;
        placeTraining(id, school, slot); occupied.add(`${school}:${slot}`); break;
      }
    }
  }
  function chooseConstruction(budget) {
    const priorities = strategy === 'growth' ? [[4, 1], [4, 2], [5, 1], [4, 3], [6, 1], [4, 4], [4, 5], [0, 2], [1, 2], [2, 2], [5, 2], [6, 2]] : strategy === 'expedition' ? [[6, 1], [4, 1], [5, 1], [6, 2], [0, 2], [1, 2], [2, 2], [4, 2], [6, 3], [5, 2], [4, 3]] : [[4, 1], [5, 1], [6, 1], [0, 2], [4, 2], [1, 2], [2, 2], [5, 2], [4, 3], [6, 2]];
    const next = priorities.find(([i, level]) => state.levels[i] + state.queues[i].length + plannedConstruction(i).length < level);
    if (!next) return;
    const [i] = next, price = cost(constructionLevel(i)), available = [...state.resources];
    available[0] += budget.deposit - budget.withdrawal;
    const reserve = upkeepTotal() * (strategy === 'stability' ? 2 : 1);
    if (price.every((n, j) => n <= available[j] - (j === 0 ? reserve : 0))) orderConstruction(i);
  }
  function planMonth() {
    restoreMonthlyDraft();
    const budget = chooseBudget();
    if (!budget.feasible) { metrics.failures.push(`maintenance-infeasible:${dateText()}`); return false; }
    chooseTraining(); chooseConstruction(budget);
    const preview = budgetOutcomePreview(budget, false).resources;
    if (!commit()) { metrics.failures.push(`budget-rejected:${dateText()}`); return false; }
    applyPlannedConstruction();
    if (preview.some((n, i) => n !== state.resources[i])) metrics.failures.push(`budget-preview-mismatch:${dateText()}`);
    return true;
  }
  function chooseExpeditions() {
    if (strategy === 'stability') return;
    for (const target of strategy === 'growth' ? [2] : [2, 1, 0]) {
      if (expeditionBlockReason(target)) continue;
      const available = state.characters.map((c, id) => ({ c, id })).filter(({ c, id }) => (scenario !== 'overextended' || id >= 4 && id < 12) && expeditionEligible(c)).sort((a, b) => Number(b.id < 12) - Number(a.id < 12) || b.c.cp - a.c.cp || a.id - b.id);
      const members = []; formationDraft = {};
      for (const { id } of available) {
        const weapon = weaponFor(id); members.push({ id, weapon }); formationDraft[id] = String(weapon);
        if (scenario === 'overextended' && members.length >= 2) break;
        if (expeditionEstimate(members, target).chance >= (strategy === 'growth' ? .85 : .9)) break;
      }
      const estimate = expeditionEstimate(members, target);
      if (!members.length || scenario !== 'overextended' && estimate.chance < (strategy === 'growth' ? .85 : .9) || state.resources[0] - estimate.supplies < upkeepTotal()) { formationDraft = {}; continue; }
      formationDraft = Object.fromEntries(members.map(m => [m.id, String(m.weapon)])); expeditionTarget = target;
      const before = [...state.resources], count = state.expeditions.length;
      departExpedition(); const delta = addDelta('expeditionDeparture', before);
      if (state.expeditions.length > count) { metrics.expeditions++; metrics.supplies -= delta[0]; }
      formationDraft = {};
    }
  }
  for (let guard = 0; guard < 366 * months; guard++) {
    if ((state.year - 1) * 12 + state.month >= targetMonth && state.day === 1) break;
    if (state.day === 1 && monthlySettingsPending() && !planMonth()) break;
    chooseDefense(); chooseExpeditions();
    const before = [...state.resources], previousDate = recoveryDate(), previousMonth = state.month, previousTasks = state.characters.map(c => c.task ? { ...c.task } : null), previousCP = state.characters.map(c => c.cp), monthBudget = state.monthBudget && { ...state.monthBudget };
    eventDelta = [0, 0, 0];
    advanceDay();
    if (state.defenseAttack) { chooseDefense(); resolveDefenseAttack(); if (state.defenseAttack) { metrics.failures.push(`unresolved-attack:${dateText()}`); break; } }
    if (recoveryDate() === previousDate) { metrics.failures.push(`date-stalled:${dateText()}`); break; }
    const rollover = state.month !== previousMonth;
    const direct = state.resources.map((n, i) => n - before[i] - eventDelta[i]);
    direct.forEach((n, i) => (metrics.ledger[rollover ? 'monthSettlement' : 'unclassified'] ||= [0, 0, 0])[i] += n);
    if (!rollover && direct.some(n => n !== 0)) metrics.failures.push(`unclassified-delta:${dateText()}:${direct}`);
    if (rollover) {
      completedMonth++;
      const market = monthBudget ? CountryBalance.marketIncome(monthBudget.marketLevel || 0, monthBudget.budgets[4], monthBudget.difficulty) : 0;
      metrics.marketIncome += market;
      const material = CountryBalance.materialIncome(monthBudget?.difficulty || difficulty);
      const refund = direct[0] - market;
      if (refund < 0 || refund % CountryBalance.rules.productionCost !== 0 || direct[1] !== material[1] || direct[2] !== material[2]) metrics.failures.push(`settlement-mismatch:${dateText()}:${direct}`);
      metrics.monthly.push({ month: completedMonth, resources: [...state.resources], levels: [...state.levels], tax: monthBudget?.tax, allocation: monthBudget?.allocation, market, productionRefund: state.productionRefund || 0, cpZero: state.characters.filter(c => c.cp === 0).length });
    }
    state.characters.forEach((c, id) => {
      if (previousTasks[id] && !c.task && (c.cp > previousCP[id] || c.skills.includes(previousTasks[id].skill))) { metrics.trainingCompleted++; if (previousTasks[id].purpose === '復帰訓練') metrics.recoveryCompleted++; }
    });
    metrics.days++;
    metrics.mainAvailable += main.filter(id => state.characters[id].cp > 0 && !state.characters[id].task).length / main.length;
    metrics.rosterAvailable += state.characters.filter(c => c.cp > 0 && !c.task).length / state.characters.length;
    metrics.minimumResources = state.resources.map((n, i) => Math.min(n, metrics.minimumResources[i]));
    if (state.resources.some(n => !Number.isSafeInteger(n) || n < 0)) { metrics.failures.push(`invalid-resource:${dateText()}`); break; }
  }
  metrics.completedMonths = completedMonth;
  metrics.resources = [...state.resources]; metrics.levels = [...state.levels];
  metrics.expeditionsInProgress = state.expeditions.length;
  metrics.mainAvailability = metrics.days ? metrics.mainAvailable / metrics.days : 0;
  metrics.rosterAvailability = metrics.days ? metrics.rosterAvailable / metrics.days : 0;
  delete metrics.mainAvailable; delete metrics.rosterAvailable;
  const accounted = Object.values(metrics.ledger).reduce((sum, delta) => sum.map((n, i) => n + delta[i]), [0, 0, 0]);
  metrics.accountingResidual = state.resources.map((n, i) => n - initialResources[i] - accounted[i]);
  if (metrics.accountingResidual.some(n => n !== 0)) metrics.failures.push(`accounting-residual:${metrics.accountingResidual}`);
  if (metrics.days !== recoveryDate() - firstDate) metrics.failures.push('day-count-mismatch');
  if (completedMonth !== months) metrics.failures.push(`incomplete-months:${completedMonth}`);
  return metrics;
}

function run(options) {
  const context = browserShell();
  for (const script of source) script.runInContext(context);
  context.simulationOptions = options;
  return JSON.parse(JSON.stringify(vm.runInContext(`(${simulationDriver.toString()})(simulationOptions)`, context)));
}

const rows = [], replayFailures = [];
for (const difficulty of ['easy', 'normal', 'hard']) {
  for (const strategy of ['stability', 'growth', 'expedition']) {
    for (let seed = 1; seed <= seedCount; seed++) {
      const options = { difficulty, strategy, seed, months, scenario: 'baseline' }, result = run(options);
      rows.push(result);
      if (seed === 1 && JSON.stringify(result) !== JSON.stringify(run(options))) replayFailures.push(`${difficulty}/${strategy}`);
    }
  }
}
const stress = [];
if (!args.includes('--skip-stress')) for (const scenario of ['low-stock', 'no-wood', 'frequent-raids', 'overextended']) for (const difficulty of ['easy', 'normal', 'hard']) for (let seed = 1; seed <= Math.min(8, seedCount); seed++) stress.push(run({ difficulty, strategy: 'expedition', seed, months, scenario }));
const mean = (list, get) => list.reduce((n, row) => n + get(row), 0) / list.length;
const summarize = list => ({ runs: list.length, failures: list.filter(row => row.failures.length).length, completedMonths: [Math.min(...list.map(r => r.completedMonths)), Math.max(...list.map(r => r.completedMonths))], finalResourcesMean: [0, 1, 2].map(i => +mean(list, row => row.resources[i]).toFixed(2)), minimumCash: Math.min(...list.map(row => row.minimumResources[0])), buildsMean: +mean(list, row => row.builds).toFixed(2), mainAvailabilityMean: +mean(list, row => row.mainAvailability).toFixed(4), rosterAvailabilityMean: +mean(list, row => row.rosterAvailability).toFixed(4), cpZeroEventsMean: +mean(list, row => row.cpZeroEvents).toFixed(2), recoveriesMean: +mean(list, row => row.recoveryCompleted).toFixed(2), raidsMean: +mean(list, row => row.raids).toFixed(2), defenseWins: list.reduce((n, row) => n + row.defenseWins, 0), defenseLosses: list.reduce((n, row) => n + row.defenseLosses, 0), expeditionsMean: +mean(list, row => row.expeditions).toFixed(2), expeditionWins: list.reduce((n, row) => n + row.expeditionWins, 0), expeditionLosses: list.reduce((n, row) => n + row.expeditionLosses, 0), suppliesMean: +mean(list, row => row.supplies).toFixed(2), productionMean: +mean(list, row => row.production).toFixed(2) });
const summary = {};
for (const difficulty of ['easy', 'normal', 'hard']) for (const strategy of ['stability', 'growth', 'expedition']) summary[`${difficulty}/${strategy}`] = summarize(rows.filter(row => row.difficulty === difficulty && row.strategy === strategy));
const stressSummary = {};
for (const scenario of ['low-stock', 'no-wood', 'frequent-raids', 'overextended']) for (const difficulty of ['easy', 'normal', 'hard']) { const selected = stress.filter(row => row.scenario === scenario && row.difficulty === difficulty); if (selected.length) stressSummary[`${scenario}/${difficulty}`] = summarize(selected); }
const report = { months, seedCount, source: 'Game VM: country-v131.js + balance.js + balance-combat.js; no resource injection after initial state', sourceFiles: sourceFiles.map(({ path, text }) => ({ path, sha256: createHash('sha256').update(text).digest('hex') })), strategies: { stability: '2 months of upkeep reserved; 10 defenders; no expeditions; market/clinic first', growth: '1 month of upkeep reserved; market investment first; plains when success estimate >=85%', expedition: '1 month of upkeep reserved; workshop first; all destinations when success estimate >=90%' }, availability: 'Fraction of days with CP>0 and no school task, including troops on missions; main is roster IDs 0..11; roster is all 30. This measures recovery downtime, not idle-at-home availability.', replayFailures, summary, stressSummary, rows, stress };
mkdirSync(dirname(output), { recursive: true }); writeFileSync(output, `${JSON.stringify(report, null, 2)}\n`);
const csv = ['difficulty,strategy,seed,scenario,days,completedMonths,cash,wood,ore,builds,mainAvailability,rosterAvailability,cpZeroEvents,recoveries,raids,defenseWins,defenseLosses,expeditions,expeditionWins,expeditionLosses,supplies,production,failures', ...[...rows, ...stress].map(r => [r.difficulty, r.strategy, r.seed, r.scenario, r.days, r.completedMonths, ...r.resources, r.builds, r.mainAvailability, r.rosterAvailability, r.cpZeroEvents, r.recoveryCompleted, r.raids, r.defenseWins, r.defenseLosses, r.expeditions, r.expeditionWins, r.expeditionLosses, r.supplies, r.production, JSON.stringify(r.failures.join('|'))].join(','))].join('\n');
writeFileSync(output.replace(/\.json$/, '.csv'), `${csv}\n`);
console.log(JSON.stringify({ output, summary, stressSummary, replayFailures, failureExamples: [...rows, ...stress].filter(r => r.failures.length).slice(0, 8).map(({ difficulty, strategy, seed, scenario, failures }) => ({ difficulty, strategy, seed, scenario, failures })) }, null, 2));
if (replayFailures.length || rows.some(row => row.failures.length) || stress.some(row => row.failures.length)) process.exitCode = 1;
