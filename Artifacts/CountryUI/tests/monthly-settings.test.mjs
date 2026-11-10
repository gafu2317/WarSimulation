import { readGameSource, loadGame } from './game-test-helper.mjs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function game(){
  const source=readGameSource().split("document.addEventListener('dragstart'")[0];
  const context=vm.createContext({document:{querySelector:()=>({classList:{remove(){}}})},localStorage:{getItem:()=>null,setItem(){}},location:{hash:'#plan'},setTimeout:()=>1,clearTimeout(){},setInterval:()=>1,clearInterval(){}});
  loadGame(context,source);
  vm.runInContext('render=()=>{};toast=()=>{};',context);
  return script=>{const value=vm.runInContext(script,context);return value===undefined?undefined:JSON.parse(JSON.stringify(value));};
}

test('every slider minimum reaches the exact upkeep amount despite percentage rounding',()=>{
  for(const tax of [31,103,1000]){
    const run=game();
    const result=run(`state.taxOverride=${tax};state.useTreasury=true;state.budgetAmounts=[50,10,10,0,0,0,0];const minimum=budgetValues().minimums[0];changeBudget(0,minimum);({amount:state.budget[0],html:renderPlan()})`);
    assert.equal(result.amount,4);
    assert.match(result.html,/最低配分額 4/);
    assert.doesNotMatch(result.html,/最低 \d+％/);
  }
});

test('a day-one confirmation applies this month and repeated confirmation never charges twice',()=>{
  const run=game();
  const first=run('state.taxOverride=100;state.budgetAmounts=[12,10,10,0,0,0,0];({ok:commit(),treasury:state.resources[0],active:state.monthBudget.budgets[0],confirmed:state.monthSettingsConfirmed,pending:state.nextBudget||null})');
  assert.deepEqual(first,{ok:true,treasury:368,active:12,confirmed:'1-1',pending:null});
  assert.equal(run('commit();state.resources[0]'),368);
  assert.equal(run('state.budgetAmounts[0]=14;commit();state.resources[0]'),366);
  assert.equal(run('state.monthBudget.opening'),300);
});

test('reconfirming treasury allocations uses the original available balance',()=>{
  const run=game();
  const result=run('state.levels=[5,5,5,0,5,5,5];state.taxOverride=100;state.useTreasury=true;state.budgetAmounts=[58,58,58,0,100,40,50];commit();const first=state.resources[0];commit();({first,second:state.resources[0],allocation:state.monthBudget.allocation})');
  assert.deepEqual(result,{first:36,second:36,allocation:364});
  assert.equal(run('state.budgetAmounts[4]=90;commit();state.resources[0]'),46);
});

test('day-two confirmation stores next-month settings and preserves active finance and training',()=>{
  const run=game();
  const result=run('state.taxOverride=100;commit();state.day=2;const funds=state.resources[0];state.budgetAmounts[0]=13;state.trainingDraft[0]={school:1,slot:0,purpose:"スキル習得",skill:"魔法使い訓練 1"};const active=JSON.stringify(state.characters[0].task);({ok:commit(),funds,after:state.resources[0],budget:state.monthBudget.budgets[0],next:state.nextBudget.amounts[0],unchanged:JSON.stringify(state.characters[0].task)===active,nextSchool:state.nextTraining[0].school})');
  assert.equal(result.ok,true);assert.equal(result.after,result.funds);
  assert.equal(result.budget,14);assert.equal(result.next,13);assert.equal(result.unchanged,true);assert.equal(result.nextSchool,1);
});

test('reopening monthly settings discards edits and restores confirmed values for the target month',()=>{
  const run=game();
  assert.deepEqual(run('budgetValues();state.budgetAmounts[0]=12;commit();state.budgetAmounts[0]=40;state.trainingDraft[0]={school:1,slot:0};restoreMonthlyDraft();({amount:state.budgetAmounts[0],training:state.trainingDraft[0]})'),{amount:12,training:null});
  assert.equal(run('state.day=2;state.budgetAmounts[0]=13;commit();state.budgetAmounts[0]=50;restoreMonthlyDraft();state.budgetAmounts[0]'),13);
  assert.equal(run('state.nextBudget=null;state.budgetAmounts[0]=60;restoreMonthlyDraft();state.budgetAmounts[0]'),12);
});

test('construction ordered after day one waits for monthly approval without spending resources',()=>{
  const run=game();
  assert.deepEqual(run('state.day=2;const before=[...state.resources];({ok:orderConstruction(4),before,after:state.resources,queue:state.queues[4],reserved:state.nextConstruction[0]})'),{ok:true,before:[300,150,90],after:[300,150,90],queue:[],reserved:{index:4,level:1,price:[140,60,36]}});
  assert.equal(run('orderConstruction(4)'),false);
  assert.deepEqual(run('state.day=1;({started:applyPlannedConstruction(),resources:state.resources,queue:state.queues[4].map(j=>j.level),pending:state.nextConstruction})'),{started:1,resources:[160,90,54],queue:[1],pending:[]});
  assert.equal(run('applyPlannedConstruction();state.resources[0]'),160);
});

test('day-one construction starts immediately and cannot create duplicate new buildings',()=>{
  const run=game();
  assert.equal(run("state.monthSettingsConfirmed='1-1';orderConstruction(4)"),true);
  assert.equal(run('state.resources[0]'),160);
  assert.equal(run('state.queues[4][0].remaining'),5);
  assert.equal(run('orderConstruction(4)'),false);
});

test('sequential upgrade reservations retain their levels and costs',()=>{
  const run=game();
  run('state.resources=[600,300,180]');
  assert.deepEqual(run('state.day=2;orderConstruction(0);orderConstruction(0);state.nextConstruction.map(j=>({level:j.level,price:j.price[0]}))'),[{level:2,price:198},{level:3,price:242}]);
  assert.deepEqual(run('state.day=1;applyPlannedConstruction();({levels:state.queues[0].map(j=>j.level),resources:state.resources})'),{levels:[2,3],resources:[160,111,67]});
});

test('unaffordable planned construction stays pending and is visibly deferred',()=>{
  const run=game();
  const result=run('state.day=2;state.resources=[0,0,0];orderConstruction(4);state.day=1;({started:applyPlannedConstruction(),pending:state.nextConstruction.length,html:renderConstruction()})');
  assert.equal(result.started,0);assert.equal(result.pending,1);
  assert.match(result.html,/資源不足で着工待ち/);
  assert.equal(run('state.resources=[140,60,36];applyPlannedConstruction()'),1);
});

test('construction management has no cancellation control or cancellation implementation',()=>{
  const run=game();
  const result=run("state.monthSettingsConfirmed='1-1';const started=orderConstruction(0);({started,queue:state.queues[0].length,html:renderConstruction(),cancel:typeof cancelBuilding,confirm:typeof confirmCancelBuilding})");
  assert.equal(result.started,true);assert.equal(result.queue,1);
  assert.match(result.html,/Lv\.2へ強化中/);
  assert.doesNotMatch(result.html,/cancel-build|工事をキャンセル/);
  assert.equal(result.cancel,'undefined');assert.equal(result.confirm,'undefined');
});


test('unapproved day-one construction preserves the minimum operating reserve',()=>{
 const run=game();
 assert.equal(run('state.monthSettingsConfirmed=null;state.levels=[4,4,4,0,0,0,0];state.resources=[500,250,150];state.taxOverride=100;state.useTreasury=true;orderConstruction(0)'),true);
 assert.deepEqual(run('state.resources'),[500,250,150]);
 assert.equal(run('state.queues[0].length'),0);
 assert.equal(run('state.nextConstruction[0].level'),5);
 assert.equal(run('budgetValues().feasible'),true);
});

test('the monthly preview leaves state untouched and predicts the combined budget and construction balance',()=>{
 const run=game();
 const preview=run('state.resources=[500,150,90];state.taxOverride=20;state.useTreasury=true;state.budgetAmounts=[12,10,8,0,0,0,0];state.nextConstruction=[{index:4,level:1,price:[100,50,30]},{index:0,level:2,price:[200,100,60]}];const b=budgetValues(),before=JSON.stringify(state),preview=budgetOutcomePreview(b);({unchanged:JSON.stringify(state)===before,resources:preview.resources,statuses:preview.orders.map(o=>o.status)})');
 assert.equal(preview.unchanged,true);
 assert.deepEqual(preview.resources,[190,0,0]);assert.deepEqual(preview.statuses,['発注','発注']);
 assert.deepEqual(run('commit();applyPlannedConstruction();state.resources'),preview.resources);
 assert.deepEqual(run('restoreMonthlyDraft();const repeat=budgetOutcomePreview(budgetValues());commit();applyPlannedConstruction();({preview:repeat.resources,actual:state.resources})'),{preview:preview.resources,actual:preview.resources});
});

test('monthly construction checks each reservation against the budget balance and preceding payments',()=>{
 const run=game();
 const preview=run('state.taxOverride=100;state.resources=[60,600,360];state.useTreasury=true;state.budgetAmounts=[14,14,14,0,0,0,0];state.nextConstruction=[{index:4,level:1,price:[100,50,30]},{index:0,level:2,price:[200,100,60]},{index:5,level:1,price:[100,50,30]}];const preview=budgetOutcomePreview(budgetValues());({resources:preview.resources,statuses:preview.orders.map(o=>o.status),before:preview.orders.map(o=>o.before[0])})');
 assert.deepEqual(preview,{resources:[18,550,330],statuses:['発注','資源不足','資源不足'],before:[118,18,18]});
 assert.deepEqual(run('commit();applyPlannedConstruction();({resources:state.resources,pending:state.nextConstruction.map(j=>j.index)})'),{resources:preview.resources,pending:[0,5]});
});

test('a reservation missing its preceding upgrade has the same waiting status in preview and application',()=>{
 const run=game();
 const result=run('state.nextConstruction=[{index:0,level:3,price:[300,150,90]},{index:4,level:1,price:[100,50,30]}];const preview=budgetOutcomePreview(budgetValues());const statuses=preview.orders.map(o=>o.status);commit();applyPlannedConstruction();({statuses,preview:preview.resources,actual:state.resources,deferred:state.nextConstruction[0].deferred})');
 assert.deepEqual(result.statuses,['先行工事待ち','発注']);assert.deepEqual(result.actual,result.preview);assert.equal(result.deferred,'先行工事待ち');
});
