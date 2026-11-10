import { readGameSource, loadGame } from './game-test-helper.mjs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function game() {
  const source=readGameSource().split("document.addEventListener('dragstart'")[0];
  const timers=new Map(),classes=new Set();let nextId=0;
  const dialog={open:false,classList:{add:name=>classes.add(name),remove:(...names)=>names.forEach(name=>classes.delete(name))},showModal(){this.open=true;},close(){this.open=false;}};
  const nodes=new Map([['#modal',dialog]]);
  const context=vm.createContext({document:{activeElement:null,querySelector:selector=>{if(!nodes.has(selector))nodes.set(selector,{innerHTML:'',outerHTML:'',focus(){}});return nodes.get(selector);}},localStorage:{getItem:()=>null,setItem:()=>{}},location:{hash:'#kingdom'},setTimeout:(callback,delay)=>{timers.set(++nextId,{callback,delay});return nextId;},clearTimeout:id=>timers.delete(id),setInterval:()=>1,clearInterval:()=>{}});
  loadGame(context,source);
  vm.runInContext("const realOpenMonthlySettings=openMonthlySettings;let renders=0,reports=0,reviews=0,returns=0;render=()=>{renders++;};toast=()=>{};showReport=()=>{reports++;};openMonthlySettings=()=>{reviews++;};showExpeditionReport=()=>{returns++;};state.defenseChance=0;state.monthSettingsConfirmed='1-1';applyMonthBudget();budgetValues();",context);
  return {
    run:script=>{const result=vm.runInContext(script,context);return result===undefined?undefined:JSON.parse(JSON.stringify(result));},
    tick(){const [id,{callback,delay}]=timers.entries().next().value;timers.delete(id);callback();return delay;},
    timers,classes,nodes,dialog
  };
}

test('month passage advances one day per visible timer tick',()=>{
  const g=game();
  assert.deepEqual(g.run('startMonthAdvance();({day:state.day,active:Boolean(monthAdvance),timer})'),{day:1,active:true,timer:null});
  assert.ok(g.dialog.open);
  assert.ok(g.nodes.get('#modal-body').innerHTML.includes('月末まで進行'));
  assert.equal(g.tick(),400);
  assert.equal(g.run('state.day'),2);
  assert.equal(g.timers.size,1);
  assert.equal(g.tick(),250);
  assert.equal(g.run('state.day'),3);
  assert.ok(g.nodes.get('.month-passage').outerHTML.includes('2 / 31日'));
});

test('closing the popup cancels future dates and refreshes the stopped state',()=>{
  const g=game();g.run('startMonthAdvance()');g.tick();
  const before=g.run('({day:state.day,renders})');
  assert.deepEqual(g.run('closeModal();({day:state.day,active:Boolean(monthAdvance),timer})'),{day:before.day,active:false,timer:null});
  assert.equal(g.timers.size,0);
  assert.equal(g.dialog.open,false);
  assert.ok(g.run('renders')>before.renders);
  assert.ok(!g.classes.has('month-progress'));
});

test('crossing the month boundary stops ticking and opens exactly one settings review',()=>{
  const g=game();g.run('state.day=30;startMonthAdvance()');g.tick();
  assert.equal(g.run('state.day'),31);g.tick();
  assert.deepEqual(g.run('({day:state.day,month:state.month,reviews,active:Boolean(monthAdvance),timer})'),{day:1,month:2,reviews:1,active:false,timer:null});
  assert.equal(g.timers.size,0);
});

test('an attack interrupts month passage, shows an arrival, then switches to defense without advancing',()=>{
  const g=game();g.run('state.defenseChance=100;state.defenseCooldown=1;let defenseShown=0;showDefenseAttack=()=>{defenseShown++;};startMonthAdvance()');
  g.tick();assert.equal(g.run('state.day'),2);
  g.tick();
  assert.deepEqual(g.run('({day:state.day,attack:Boolean(state.defenseAttack),active:Boolean(monthAdvance),defenseShown})'),{day:3,attack:true,active:false,defenseShown:0});
  assert.ok(g.nodes.get('#modal-body').innerHTML.includes('月末への進行を一時停止'));
  assert.ok(g.classes.has('attack-cutin'));
  assert.equal(g.timers.size,1);
  assert.equal(g.tick(),1800);
  assert.deepEqual(g.run('({day:state.day,defenseShown})'),{day:3,defenseShown:1});
  assert.equal(g.timers.size,0);
});

test('dismissing an arrival keeps the attack pending without reopening a popup later',()=>{
  const g=game();g.run('state.defenseChance=100;startMonthAdvance()');g.tick();
  assert.ok(g.run('Boolean(state.defenseAttack)'));
  g.run('closeModal()');
  assert.equal(g.timers.size,0);
  assert.equal(g.dialog.open,false);
  assert.equal(g.run('state.day'),2);
  assert.ok(g.run('Boolean(state.defenseAttack)'));
});

test('an existing attack opens defense instead of scheduling month passage',()=>{
  const g=game();
  assert.deepEqual(g.run('let defenseShown=0;showDefenseAttack=()=>{defenseShown++;};state.defenseAttack={enemy:3};startMonthAdvance();({day:state.day,defenseShown,active:Boolean(monthAdvance)})'),{day:1,defenseShown:1,active:false});
  assert.equal(g.timers.size,0);
});


test('acknowledging defense resumes month passage from the interrupted date',()=>{
 const g=game();g.run('state.defenseChance=100;startMonthAdvance()');g.tick();
 assert.equal(g.run('Boolean(suspendedMonthAdvance)'),true);
 const day=g.run('state.day');
 g.run('state.defenseAttack=null;state.defenseCooldown=7;state.defenseReport={unread:true};acknowledgeDefenseResult()');
 assert.equal(g.run('state.day'),day);
 assert.equal(g.run('Boolean(monthAdvance)'),true);
 assert.equal(g.timers.size,1);
 g.tick();assert.equal(g.run('state.day'),day+1);
});

test('an expedition return stops the normal timer and displays the return report',()=>{
 const g=game();
 g.run("state.day=2;state.expeditions=[{id:1,target:0,total:1,remaining:1,elapsed:0,members:[{id:0,weapon:0,before:100}],outcome:'victory',combat:{duration:1,ratio:1,cp:1,loot:[0,0,0],testOnly:true}}];startTime();advanceDay()");
 assert.deepEqual(g.run('({day:state.day,timer,returns})'),{day:3,timer:null,returns:1});
});

test('closing the defense result also resumes the interrupted month',()=>{
 const g=game();
 g.run("state.day=10;suspendedMonthAdvance={year:1,month:1};state.defenseReport={date:'1年1月10日',enemy:3,members:[],outcome:'防衛成功',unread:true};showDefenseResult();closeModal()");
 assert.deepEqual(g.run('({day:state.day,active:Boolean(monthAdvance),unread:state.defenseReport.unread,suspended:suspendedMonthAdvance})'),{day:10,active:true,unread:false,suspended:null});
 assert.equal(g.timers.size,1);
 g.tick();assert.equal(g.run('state.day'),11);
});

test('closing a defense result from normal time keeps time stopped',()=>{
 const g=game();
 g.run("state.defenseReport={date:'1年1月1日',enemy:3,members:[],outcome:'防衛成功',unread:true};showDefenseResult();closeModal()");
 assert.deepEqual(g.run('({timer,active:Boolean(monthAdvance),unread:state.defenseReport.unread})'),{timer:null,active:false,unread:false});
 assert.equal(g.dialog.open,false);
});

test('an expedition return also ends month passage without resuming later',()=>{
 const g=game();
 g.run("state.expeditions=[{id:1,target:0,total:1,remaining:1,elapsed:0,members:[{id:0,weapon:0,before:100}],outcome:'victory',combat:{duration:1,ratio:1,cp:1,loot:[0,0,0],testOnly:true}}];startMonthAdvance()");g.tick();
 assert.deepEqual(g.run('({day:state.day,active:Boolean(monthAdvance),timer,returns,suspended:suspendedMonthAdvance})'),{day:2,active:false,timer:null,returns:1,suspended:null});
 assert.equal(g.timers.size,0);
});

test('month-end progression first asks for day-one settings and keeps the date fixed',()=>{
 const g=game();g.run('state.monthSettingsConfirmed=null;startMonthAdvance()');
 assert.deepEqual(g.run('({day:state.day,active:Boolean(monthAdvance),reviews,pending:pendingTimeAction})'),{day:1,active:false,reviews:1,pending:'month'});
 assert.equal(g.timers.size,0);
});


test('month start opens the existing budget editor without an approval modal',()=>{
 const g=game();
 g.run("state.monthSettingsConfirmed=null;state.budgetAmounts[0]=80;openMonthlySettings=realOpenMonthlySettings;openMonthlySettings()");
 assert.equal(g.run('location.hash'),'plan');
 assert.equal(g.run('budgetTabChoice'),'budget');
 assert.equal(g.run('state.budgetAmounts[0]'),14);
 assert.equal(g.dialog.open,false);
 assert.equal(g.run('timer'),null);
});

test('confirming the budget editor keeps the chosen amounts and starts time immediately',()=>{
 const g=game();
 g.run("state.monthSettingsConfirmed=null;state.budgetAmounts[0]=12;confirmMonthlySettings()");
 assert.deepEqual(g.run('({day:state.day,amount:state.monthBudget.budgets[0],route:location.hash,timer})'),{day:1,amount:12,route:'kingdom',timer:1});
 assert.equal(g.dialog.open,false);
});

test('confirmation starts queued day-one construction and continues the requested month passage',()=>{
 const g=game();
 g.run("state.monthSettingsConfirmed=null;orderConstruction(4);pendingTimeAction='month';confirmMonthlySettings()");
 assert.equal(g.run('Boolean(monthAdvance)'),true);
 assert.equal(g.run('state.queues[4][0].level'),1);
 assert.equal(g.run('state.nextConstruction.length'),0);
 assert.equal(g.run('state.resources[1]'),90);
 assert.match(g.nodes.get('#modal-body').innerHTML,/月末まで進行/);
 assert.doesNotMatch(g.nodes.get('#modal-body').innerHTML,/運営確認|month-confirm/);
});

test('saving a day-two budget schedules next month and starts the normal timer',()=>{
 const g=game();const before=g.run('state.resources[0]');
 g.run('state.day=2;state.budgetAmounts[0]=13;confirmMonthlySettings()');
 assert.equal(g.run('state.resources[0]'),before);
 assert.equal(g.run('state.nextBudget.amounts[0]'),13);
 assert.equal(g.run('timer'),1);
});

test('an infeasible confirmation stays in the editor without advancing time',()=>{
 const g=game();
 g.run("location.hash='plan';state.levels=[5,5,5,0,0,0,0];state.useTreasury=false;state.taxOverride=20");
 assert.equal(g.run('confirmMonthlySettings()'),false);
 assert.equal(g.run('location.hash'),'plan');
 assert.equal(g.run('timer'),null);
});
