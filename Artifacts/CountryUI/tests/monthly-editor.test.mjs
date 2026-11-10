import { readGameSource, loadGame, loadEditorEvents } from './game-test-helper.mjs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function game(){
 const source=readGameSource(),listeners={},intervals=new Map(),timeouts=new Map();let nextId=0,hash='#kingdom';
 const location={get hash(){return hash;},set hash(value){hash=value.startsWith('#')?value:`#${value}`;}};
 const node={innerHTML:'',outerHTML:'',focus(){},scrollIntoView(){},classList:{add(){},remove(){}}};
 const modal={...node,open:false,showModal(){this.open=true;},close(){this.open=false;}};
 const context=vm.createContext({
  document:{activeElement:null,querySelector:s=>s==='#modal'?modal:s==='.game-window'?null:node,addEventListener:(name,callback)=>listeners[name]=callback},
  window:{addEventListener:(name,callback)=>listeners[name]=callback},
  history:{replaceState:(state,title,url)=>location.hash=url},location,
  localStorage:{getItem:()=>null,setItem(){}},
  setInterval:callback=>{intervals.set(++nextId,callback);return nextId;},clearInterval:id=>intervals.delete(id),
  setTimeout:callback=>{timeouts.set(++nextId,callback);return nextId;},clearTimeout:id=>timeouts.delete(id)
 });
 loadGame(context,source);
 vm.runInContext('render=()=>{};toast=()=>{};maybeDefenseAttack=()=>{};state.defenseChance=0;budgetValues();',context);
 loadEditorEvents(context,source);
 const run=script=>{const value=vm.runInContext(script,context);return value===undefined?undefined:JSON.parse(JSON.stringify(value));};
 return {
  run,modal,intervals,timeouts,
  click(action,data={}){const button={dataset:{action,...data},disabled:false};listeners.click({target:{closest:()=>button}});},
  hash(value){location.hash=value;listeners.hashchange();},
  space(){listeners.keydown({code:'Space',key:' ',target:{tagName:'DIV',matches:()=>false},preventDefault(){}});},
  tick(){const entry=intervals.values().next();if(entry.done)return false;entry.value();return true;}
 };
}

test('every monthly editor entry pauses a running timer before editing',()=>{
 for(const [action,data] of [['open-monthly-menu',{menuTab:'budget'}],['open-monthly-menu',{menuTab:'construction'}],['open-monthly-menu',{menuTab:'training'}],['navigate-plan',{}],['navigate-budget',{index:'0'}],['navigate-training',{}],['training-open',{index:'0'}],['weapon-budget',{}]]){
  const g=game();g.run('confirmMonthlySettings()');assert.equal(g.intervals.size,1);
  g.click(action,data);
  assert.equal(g.run('route()'),'plan',action);assert.equal(g.intervals.size,0,action);assert.equal(g.run('timer'),null,action);
 }
 const g=game();g.run('confirmMonthlySettings()');g.hash('#plan');assert.equal(g.intervals.size,0);
});

test('day-one edits cannot silently become next-month reservations after a timer tick',()=>{
 const g=game();g.run('confirmMonthlySettings()');g.click('open-monthly-menu',{menuTab:'budget'});
 g.run('state.budgetAmounts[0]=12');assert.equal(g.tick(),false);
 assert.deepEqual(g.run('({day:state.day,amount:state.budgetAmounts[0],target:monthSettingsDate()})'),{day:1,amount:12,target:'1年1月'});
 g.click('commit');
 assert.deepEqual(g.run('({active:state.monthBudget.budgets[0],next:state.nextBudget||null,route:route()})'),{active:12,next:null,route:'kingdom'});
 assert.equal(g.intervals.size,1);
});

test('opening the editor at month end preserves the unconfirmed amount until confirmation',()=>{
 const g=game();g.run('confirmMonthlySettings();state.day=31');g.click('open-monthly-menu',{menuTab:'budget'});
 g.run('state.budgetAmounts[0]=13');assert.equal(g.tick(),false);
 assert.deepEqual(g.run('({month:state.month,day:state.day,amount:state.budgetAmounts[0]})'),{month:1,day:31,amount:13});
 g.click('commit');assert.equal(g.run('state.nextBudget.amounts[0]'),13);assert.equal(g.intervals.size,1);
 g.tick();assert.deepEqual(g.run('({month:state.month,day:state.day,amount:state.budgetAmounts[0],timer})'),{month:2,day:1,amount:13,timer:null});
});

test('entering training from a modal does not restart its previously running timer',()=>{
 const g=game();g.run('confirmMonthlySettings();openModal("施設","学校")');assert.equal(g.run('resumeAfterModal'),true);
 g.click('training-open',{index:'0'});
 assert.equal(g.modal.open,false);assert.equal(g.run('route()'),'plan');assert.equal(g.intervals.size,0);assert.equal(g.run('resumeAfterModal'),false);
});

test('switching monthly tabs keeps the draft and time paused',()=>{
 const g=game();g.run('confirmMonthlySettings()');g.click('open-monthly-menu',{menuTab:'budget'});g.run('state.budgetAmounts[0]=12');
 for(const tab of ['construction','training','budget']){
  g.click('budget-tab',{tab});assert.equal(g.run('budgetTabChoice'),tab);assert.equal(g.run('state.budgetAmounts[0]'),12);assert.equal(g.intervals.size,0);assert.equal(g.timeouts.size,0);
 }
});

test('time controls in the editor retain the draft and wait for confirmation before the requested operation',()=>{
 for(const [action,pending] of [['toggle-time','time'],['day','day'],['month','month']]){
  const g=game();g.run('confirmMonthlySettings()');g.click('open-monthly-menu',{menuTab:'budget'});g.run('state.budgetAmounts[0]=12');g.click(action);
  assert.deepEqual(g.run('({day:state.day,amount:state.budgetAmounts[0],pending:pendingTimeAction})'),{day:1,amount:12,pending});
  assert.equal(g.intervals.size,0);assert.equal(g.timeouts.size,0);g.click('commit');
  assert.equal(g.run('state.monthBudget.budgets[0]'),12);
  assert.equal(g.run('state.day'),pending==='day'?2:1);
  assert.equal(g.intervals.size,pending==='time'?1:0);
  assert.equal(g.run('Boolean(monthAdvance)'),pending==='month');
 }
 const g=game();g.run('confirmMonthlySettings()');g.hash('#plan');g.run('state.budgetAmounts[0]=12');g.space();
 assert.equal(g.intervals.size,0);assert.equal(g.run('state.budgetAmounts[0]'),12);assert.equal(g.run('pendingTimeAction'),'time');
});

test('direct editor navigation pauses month passage and confirmation resumes it after starting queued construction',()=>{
 const g=game();g.run('commit();startMonthAdvance()');assert.equal(g.timeouts.size,1);
 g.hash('#plan');assert.equal(g.timeouts.size,0);assert.equal(g.run('Boolean(monthAdvance)'),false);assert.equal(g.run('pendingTimeAction'),'month');
 g.run('state.day=2;orderConstruction(4);state.day=1');g.click('commit');
 assert.equal(g.run('Boolean(monthAdvance)'),true);assert.equal(g.run('state.queues[4][0].level'),1);assert.equal(g.run('state.resources[1]'),90);
 assert.equal(g.timeouts.size,1);
});
