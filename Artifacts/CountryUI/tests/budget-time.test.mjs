import { readGameSource, loadGame } from './game-test-helper.mjs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function game() {
  const source=readGameSource().split("document.addEventListener('dragstart'")[0];
  const context=vm.createContext({document:{querySelector:()=>({classList:{remove:()=>{}}})},localStorage:{getItem:()=>null,setItem:()=>{}},location:{hash:'#kingdom'},setTimeout:()=>1,clearTimeout:()=>{},setInterval:()=>1,clearInterval:()=>{}});
  loadGame(context,source);
  vm.runInContext("render=()=>{};toast=()=>{};showReport=()=>{};openMonthlySettings=()=>{};maybeDefenseAttack=()=>{};",context);
  return script=>JSON.parse(JSON.stringify(vm.runInContext(script,context)));
}

test('percentage labels and sliders use integers while treasury switching preserves amounts',()=>{
  const run=game();
  for(const tax of [30,31,103,1000]){
    const value=run(`state.taxOverride=${tax};state.resources[0]=380;state.useTreasury=false;state.budgetAmounts=[10,10,10,0,0,0,0];const b${tax}=budgetValues();toggleTreasury(true);({before:b${tax}.budgets,after:state.budget,shares:state.budgetShare,html:renderPlan()})`);
    assert.deepEqual(value.after,value.before);
    assert.ok(value.shares.every(Number.isInteger));
    assert.ok(value.html.includes('step="1"'));
    assert.ok(!/\d[.,]\d+[％%]/.test(value.html));
  }
});

test('1 percent buttons change the selected rate by one and respect upkeep',()=>{
  const run=game();
  assert.deepEqual(run('state.taxOverride=100;budgetValues();changeBudget(0,11);({rate:state.budgetShare[0],amount:state.budget[0]})'),{rate:11,amount:11});
  assert.equal(run('changeBudget(0,0);state.budget[0]'),4);
});

test('saving next month during an attack changes neither date, current budget, treasury nor timer',()=>{
  const run=game();
  const before=run('state.day=2;applyMonthBudget();state.defenseAttack={enemy:3};state.budgetAmounts=[12,10,10,0,0,0,0];({day:state.day,resources:state.resources,active:state.monthBudget})');
  const after=run('commit();({day:state.day,resources:state.resources,active:state.monthBudget,timer,next:state.nextBudget})');
  assert.equal(after.day,before.day);
  assert.deepEqual(after.resources,before.resources);
  assert.deepEqual(after.active,before.active);
  assert.equal(after.timer,null);
  assert.equal(after.next.amounts[0],12);
  assert.equal(run('advanceDay();state.day'),before.day);
});

test('unconfirmed day-one settings stop time until the monthly review is approved',()=>{
  const run=game();
  assert.deepEqual(run('startTime();({day:state.day,timer,pending:pendingTimeAction})'),{day:1,timer:null,pending:'time'});
  assert.equal(run('commit()'),true);
  assert.equal(run('advanceDay();state.day'),2);
});

test('midmonth budgets wait for the next month review before any payment',()=>{
  const run=game();
  const before=run('commit();state.day=2;state.budgetAmounts=[13,10,10,0,0,0,0];commit();({treasury:state.resources[0],active:state.monthBudget.budgets[0]})');
  assert.equal(before.active,14);
  const first=run('state.day=31;advanceDay();({month:state.month,day:state.day,budget:state.monthBudget,treasury:state.resources[0],policy:state.budgetPolicy.amounts[0],pending:monthlySettingsPending()})');
  assert.deepEqual(first,{month:2,day:1,budget:null,treasury:before.treasury,policy:13,pending:true});
  const paid=run('commit();({treasury:state.resources[0],active:state.monthBudget.budgets[0]})');
  assert.equal(paid.active,13);
  assert.equal(run('advanceDay();state.resources[0]'),paid.treasury);
});

test('December budgets activate in January of the next year',()=>{
  const run=game();
  assert.equal(run('state.month=12;nextBudgetDate()'),'2年1月');
  assert.deepEqual(run('state.day=31;commit();advanceDay();({year:state.year,month:state.month,day:state.day})'),{year:2,month:1,day:1});
});

test('acknowledging one report removes only it and old acknowledged reports stay out of the list',()=>{
  const run=game();
  const count=run("state.expeditionReports=[{target:0,read:true,date:'old',members:[],outcome:'victory'},{target:1,read:false,date:'first',members:[],outcome:'victory'},{target:2,read:false,date:'second',members:[],outcome:'defeat'}];renderExpeditionOverview()");
  assert.ok(count.includes('data-report="1"'));
  assert.ok(!count.includes('data-report="0"'));
  assert.deepEqual(run('acknowledgeExpeditionReport(1);({targets:state.expeditionReports.map(r=>r.target),unread:unreadExpeditionReports()})'),{targets:[0,2],unread:1});
  assert.equal(run('acknowledgeExpeditionReport(1);unreadExpeditionReports()'),0);
});


test('blocked selected members show all reasons in defense and expedition slots',()=>{
  const run=game();
  for(const mode of ['defense','expedition']){
    const html=run(`state.characters[0].cp=0;state.characters[0].task={school:0,slot:1};assignedCharacter(0,'${mode}')`);
    assert.ok(html.includes('assigned-unavailable'));
    assert.ok(html.includes('CP0・戦闘不能 / 訓練中'));
    assert.ok(html.includes('aria-label="イングリッドを配置から外す"'));
  }
  const training=run("state.characters[0].cp=100;assignedCharacter(0,'defense')");
  assert.ok(training.includes('assigned-status '));
  assert.ok(training.includes('訓練中'));
  const ready=run("state.characters[0].task=null;assignedCharacter(0,'defense')");
  assert.ok(!ready.includes('assigned-status'));
});

test('unavailable expedition members remain visible and block departure',()=>{
  const run=game();
  const html=run("formationDraft={0:0};expeditionPage='new';expeditionTab='members';expeditionTarget=0;state.characters[0].task={school:0,slot:1};renderExpedition()");
  assert.ok(html.includes('訓練中'));
  assert.ok(html.includes('出撃不可 1人'));
  assert.ok(html.includes('data-action="expedition-save" disabled'));
});

test('reopening budget settings restores the confirmed treasury choice and amounts',()=>{
  const run=game();
  assert.equal(run('state.useTreasury'),false);
  assert.equal(run('state.taxOverride=100;toggleTreasury(true);state.budgetAmounts=[12,10,8,0,0,0,0];commit()'),true);
  assert.deepEqual(run('state.useTreasury=false;state.budgetAmounts[0]=10;restoreMonthlyDraft();({use:state.useTreasury,amount:state.budget[0]})'),{use:true,amount:12});
});

test('a training draft does not block combat, finish early, or change active training',()=>{
 const run=game();
 assert.deepEqual(run("state.day=2;placeTraining(0,0,1);({task:state.characters[0].task,eligible:expeditionEligible(state.characters[0]),draft:state.trainingDraft[0].slot,confirmed:trainingDraftSaved()})"),{task:null,eligible:true,draft:1,confirmed:false});
 assert.ok(run("renderTraining()").includes('訓練予定・未確定'));
 assert.ok(!run("renderTraining()").includes('訓練中'));
 const result=run("state.day=31;advanceDay();({task:state.characters[0].task,skills:state.characters[0].skills})");
 assert.equal(result.task,null);assert.deepEqual(result.skills,['基本訓練 I']);
});

test('training confirmed on day one starts now and finishes after its required days',()=>{
 const run=game();
 assert.equal(run('placeTraining(0,0,1);commit();trainingDraftSaved()'),true);
 assert.equal(run('state.characters[0].task.school'),0);
 assert.ok(run('renderTraining()').includes('訓練予定・確定済み'));
 const days=run('state.characters[0].task.requiredDays');
 run(`state.day=${days};advanceTraining();true`);
 assert.notEqual(run('state.characters[0].task'),null);
 run(`state.day=${days+1};advanceTraining();true`);
 assert.equal(run('state.characters[0].task'),null);
 assert.ok(run("state.characters[0].skills.includes('戦士訓練 1')"));
});

test('editing next month during active training preserves the current task',()=>{
 const run=game();
 run('placeTraining(0,0,1);commit();state.day=2;true');
 const before=run('state.characters[0].task');
 run('placeTraining(0,1,0);commit();true');
 assert.deepEqual(run('state.characters[0].task'),before);
 assert.equal(run('state.nextTraining[0].school'),1);
});

test('legacy assignments migrate to unconfirmed plans without changing CP or learned skills',()=>{
 const run=game();
 const migrated=run("state.characters[0].task={school:0,slot:1,purpose:'スキル習得',skill:'戦士訓練 1'};delete state.trainingStateSeparated;extendBuildings();({active:state.characters[0].task,draft:state.trainingDraft[0],confirmed:trainingDraftSaved(),cp:state.characters[0].cp})");
 assert.equal(migrated.active,null);assert.equal(migrated.draft.slot,1);assert.equal(migrated.confirmed,false);assert.equal(migrated.cp,100);
});


test('CP0 must finish convalescence before reserving recovery training',()=>{
 const run=game();
 assert.equal(run('state.characters[2].recoveryDays'),3);
 assert.equal(run('placeTraining(2,0,0);state.trainingDraft[2]'),null);
 run('commit();advanceDay();advanceDay();advanceDay();true');
 assert.equal(run('state.characters[2].recoveryDays'),0);
 assert.equal(run('state.characters[2].cp'),0);
 run('placeTraining(2,0,0);commit();state.day=31;advanceDay();true');
 assert.equal(run('state.characters[2].task'),null);
 assert.equal(run('commit();state.characters[2].task.purpose'),'復帰訓練');
 run('state.day=4;advanceTraining();true');
 assert.equal(run('state.characters[2].cp'),0);
 run('state.day=5;advanceTraining();true');
 assert.equal(run('state.characters[2].cp'),100);
});
