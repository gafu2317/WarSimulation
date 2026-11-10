import { readGameSource, loadGame } from './game-test-helper.mjs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function game() {
  const source=readGameSource().split("document.addEventListener('dragstart'")[0];
  const context=vm.createContext({document:{querySelector:()=>({classList:{remove(){}}})},localStorage:{getItem:()=>null,setItem(){}},location:{hash:'#plan'},setTimeout:()=>1,clearTimeout(){},setInterval:()=>1,clearInterval(){}});
  loadGame(context,source);
  return script=>vm.runInContext(script,context);
}

test('upgrade preview shows all three costs and remaining balances without spending them',()=>{
  const run=game(),before=run('JSON.stringify(state.resources)');
  const html=run('constructionImpact(0,2,[200,100,60])');
  assert.match(html,/資金：300から100、消費200/);
  assert.match(html,/木材：150から50、消費100/);
  assert.match(html,/鉱石：90から30、消費60/);
  assert.match(html,/Lv\.1/);assert.match(html,/Lv\.2/);assert.match(html,/工期 5日/);
  assert.equal(run('JSON.stringify(state.resources)'),before);
});

test('insufficient resources show the actual deficit and never draw negative bars',()=>{
  const html=game()('state.resources=[50,600,360];constructionImpact(0,2,[200,100,60])');
  assert.match(html,/資金：50から-150、消費200/);
  assert.match(html,/is-short/);assert.match(html,/資源不足/);
  assert.doesNotMatch(html,/width:-/);
});

test('next-month allocation previews treasury use without changing the current balance',()=>{
  const run=game(),html=run('state.day=2;state.taxOverride=20;state.useTreasury=true;state.budgetAmounts=[12,10,8,0,0,0,0];budgetSpending(budgetValues())');
  assert.match(html,/1年2月の月初/);assert.match(html,/資金：300から290、消費10/);
  assert.equal(run('state.resources[0]'),300);
});

test('the existing budget page shows resource icons, combined balances and construction payment results',()=>{
 const run=game();
 const html=run('state.resources=[500,150,90];state.taxOverride=20;state.useTreasury=true;state.budgetAmounts=[12,10,8,0,0,0,0];state.nextConstruction=[{index:4,level:1,price:[100,50,30]},{index:0,level:2,price:[200,100,60]}];renderPlan()');
 assert.match(html,/予算・建築反映後の資源/);
 assert.match(html,/資金：500から190、消費310/);assert.match(html,/木材：150から0、消費150/);assert.match(html,/鉱石：90から0、消費90/);
 assert.match(html,/budget-construction-order order-ready/);assert.match(html,/市場 Lv\.1：資源を支払い工事へ追加/);
 assert.match(html,/resource--funds/);assert.match(html,/resource--wood/);assert.match(html,/resource--ore/);assert.match(html,/budget-order-status.*<svg/);
 assert.doesNotMatch(html,/month-review|この内容で今月を開始/);
 assert.equal(run('state.resources[0]'),500);assert.equal(run('state.queues[4].length'),0);
});

test('unfunded monthly construction is visibly waiting and contributes no predicted payment',()=>{
 const run=game();
 const html=run('state.taxOverride=20;state.resources=[100,60,36];state.useTreasury=true;state.budgetAmounts=[12,10,8,0,0,0,0];state.nextConstruction=[{index:4,level:1,price:[100,50,30]}];constructionSummary(budgetOutcomePreview(budgetValues()))');
 assert.match(html,/order-waiting/);assert.match(html,/資源不足・支払いなし/);assert.match(html,/cost-short/);assert.match(html,/viewBox="0 0 24 24"/);
 assert.equal(run('budgetOutcomePreview(budgetValues()).resources[0]'),90);
});

test('the budget page omits construction summaries when no construction is reserved',()=>{
 const html=game()('renderPlan()');
 assert.match(html,/確定後の資源/);assert.doesNotMatch(html,/budget-construction-summary/);
});

test('next-month combined balances include new material income without reversing the current budget',()=>{
 const run=game();
 const result=run('render=()=>{};toast=()=>{};state.taxOverride=100;state.budgetAmounts=[12,10,10,0,0,0,0];commit();state.day=2;state.budgetAmounts=[14,10,10,0,0,0,0];JSON.stringify(budgetOutcomePreview(budgetValues()).resources)');
 assert.deepEqual(JSON.parse(result),[434,200,120]);
 assert.equal(run('state.resources[0]'),368);
});

test('defense CP preview clamps at zero and shows the resulting combat restriction',()=>{
  const html=game()('cpChange(10,0,100)');
  assert.match(html,/CP 10から0/);assert.match(html,/CP0で戦闘不能・訓練が必要/);
  assert.match(html,/width:10%/);
});

test('expedition CP uncertainty is distinct from a completed loss',()=>{
  const run=game(),html=run("state.characters[0].cp=8;expeditionMemberRows([{id:0,weapon:0}],false,{duration:4,ratio:1,cp:1})");
  assert.match(html,/CP 8から0〜3/);assert.match(html,/change-uncertain/);
  assert.match(html,/CP0になる可能性あり/);assert.equal(run('state.characters[0].cp'),8);
  const result=run('expeditionMemberRows([{id:0,weapon:0,before:8,after:0}],true)');
  assert.match(result,/CP0で戦闘不能・訓練が必要/);assert.doesNotMatch(result,/change-uncertain/);
});


test('resource breakdown uses confirmed budgets, current tax and reserved construction without mutating state',()=>{
 const run=game();
 run('state.taxOverride=103;state.budgetPolicy={amounts:[12,10,10,0,0,0,0],useTreasury:false};state.budgetAmounts=[90,10,10,0,0,0,0];state.nextConstruction=[{index:4,level:1,price:[100,50,30]}]');
 const before=run('JSON.stringify(state)');
 assert.deepEqual(JSON.parse(run('JSON.stringify(resourceForecast().preview.resources)')),[271,100,60]);
 assert.equal(run('resourceForecast().budget.tax'),103);
 assert.equal(run('resourceForecast().budget.allocation'),32);
 assert.equal(run('JSON.stringify(state)'),before);
 const html=run('let ledger="";openModal=(title,body)=>ledger=body;modal.classList.add=()=>{};showResourceDetails(0);ledger');
 assert.match(html,/1年1月の予算確定時/);assert.match(html,/資金：300から271、消費29/);
 assert.match(html,/運営・育成の予算/);assert.match(html,/着工する建築/);assert.doesNotMatch(html,/予定支出.*なし/);
});

test('an already confirmed day-one ledger forecasts the next month without undoing current payments',()=>{
 const run=game();
 run('render=()=>{};toast=()=>{};state.taxOverride=100;state.budgetAmounts=[12,10,10,0,0,0,0];commit()');
 assert.equal(run('state.resources[0]'),368);
 assert.equal(run('resourceForecast().nextMonth'),true);
 assert.deepEqual(JSON.parse(run('JSON.stringify(resourceForecast().preview.resources)')),[436,200,120]);
 assert.equal(run('resourceForecast().adjustment'),0);
});

test('day-two resource forecasts use the confirmed next-month budget and exclude unaffordable construction',()=>{
 const run=game();
 run('state.day=2;state.resources=[100,0,0];state.taxOverride=103;state.nextBudget={amounts:[14,14,14,0,0,0,0],useTreasury:true};state.nextConstruction=[{index:0,level:2,price:[200,100,60]}]');
 assert.deepEqual(JSON.parse(run('JSON.stringify(resourceForecast().preview.resources)')),[161,50,30]);
 assert.deepEqual(JSON.parse(run('JSON.stringify(resourceForecast().construction)')),[0,0,0]);
 assert.equal(run('resourceForecast().preview.orders[0].status'),'資源不足');
 const html=run('let ledger="";openModal=(title,body)=>ledger=body;modal.classList.add=()=>{};showResourceDetails(2);ledger');
 assert.match(html,/鉱石：0から30、増加30/);assert.match(html,/resource-change resource--ore/);assert.doesNotMatch(html,/resource-change resource--funds/);
 assert.match(html,/未着工の1件は支払いなし/);
});

test('legacy current-month payments are adjusted only once in an unconfirmed day-one forecast',()=>{
 const run=game();
 run('render=()=>{};toast=()=>{};state.taxOverride=100;state.budgetAmounts=[12,10,10,0,0,0,0];commit();state.monthSettingsConfirmed=null');
 assert.equal(run('resourceForecast().preview.resources[0]'),368);
 assert.equal(run('resourceForecast().adjustment'),-68);
 assert.equal(run('state.resources[0]'),368);
});
