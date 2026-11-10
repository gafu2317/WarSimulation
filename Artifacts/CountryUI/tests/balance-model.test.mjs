import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';
import { loadGame } from './game-test-helper.mjs';

function model() {
  const context=vm.createContext({});
  for(const file of ['balance.js','balance-combat.js'])vm.runInContext(readFileSync(new URL(`../js/${file}`,import.meta.url),'utf8'),context);
  return {B:context.CountryBalance,C:context.CountryCombat};
}
const plain=value=>JSON.parse(JSON.stringify(value));
function game(saved) {
  const node={classList:{add(){},remove(){}}};
  const context=vm.createContext({document:{querySelector:()=>node},localStorage:{getItem:()=>saved?JSON.stringify(saved):null,setItem(){}},location:{hash:'#kingdom'},setTimeout:()=>1,clearTimeout(){},setInterval:()=>1,clearInterval(){}});
  loadGame(context);
  vm.runInContext('render=()=>{};toast=()=>{};openMonthlySettings=()=>{};showReport=()=>{};maybeDefenseAttack=()=>{};showSettings=()=>{};closeModal=()=>{};showDefenseResult=()=>{};showExpeditionReport=()=>{};',context);
  return script=>{const value=vm.runInContext(script,context);return value===undefined?undefined:plain(value);};
}

test('difficulty changes recurring income and pressure while sharing a finite initial stock',()=>{
  const {B}=model();
  for(const id of ['easy','normal','hard'])assert.deepEqual(plain(B.initialResources(id)),[300,150,90]);
  assert.deepEqual(['easy','normal','hard'].map(id=>B.taxIncome(12560,id)),[120,100,85]);
  assert.deepEqual(['easy','normal','hard'].map(id=>plain(B.materialIncome(id))),[[0,60,36],[0,50,30],[0,43,26]]);
  assert.ok(B.profile('easy').raidChance<B.profile('normal').raidChance);
  assert.ok(B.profile('normal').raidChance<B.profile('hard').raidChance);
  assert.ok(B.profile('easy').enemy<B.profile('normal').enemy);
  assert.ok(B.profile('normal').enemy<B.profile('hard').enemy);
  assert.equal(B.profile('missing').id,'normal');
});

test('construction price has diminishing marginal growth and facility effects stop at level five',()=>{
  const {B}=model();
  assert.deepEqual(plain(B.cost(1)),[140,60,36]);
  assert.deepEqual(plain(B.cost(2)),[198,85,51]);
  assert.deepEqual(plain(B.cost(5)),[313,134,80]);
  assert.deepEqual(plain(B.cost(99)),plain(B.cost(5)));
  assert.equal(B.constructionDays,5);
  assert.equal(B.upkeep(0),0);
  assert.equal(B.upkeep(1),4);
  assert.equal(B.upkeep(99),8);
  assert.equal(B.upkeep(5)*6,48);
  assert.ok(B.upkeep(5)*6<B.taxIncome(12560,'hard'));
});

test('school funding buys complete seats after upkeep and cannot bypass facility capacity',()=>{
  const {B}=model();
  assert.equal(B.schoolCapacity(1,4),0);
  assert.equal(B.schoolCapacity(1,5),0);
  assert.equal(B.schoolCapacity(1,6),1);
  assert.equal(B.schoolCapacity(1,14),5);
  assert.equal(B.schoolCapacity(1,1000),5);
  assert.equal(B.schoolCapacity(5,1000),25);
  assert.equal(B.schoolCapacity(0,1000),0);
  assert.deepEqual([0,1,2,4,5,6].map(i=>B.budgetCap(i,1)),[14,14,14,49,12,14]);
});

test('market gross sales include its activity budget and normal level-one capital pays back in about 4.4 months',()=>{
  const {B}=model();
  assert.equal(B.marketIncome(1,4),0);
  assert.equal(B.marketIncome(1,49),81);
  assert.equal(B.marketIncome(1,1000),81);
  assert.equal(B.marketIncome(0,1000),0);
  assert.deepEqual(['easy','normal','hard'].map(id=>B.marketIncome(1,49,id)),[97,81,68]);
  assert.equal(B.marketIncome(1,49)-49,32);
  assert.ok(B.cost(1)[0]/32>4 && B.cost(1)[0]/32<5);
});

test('clinic support is funded per person and population growth preserves legacy inhabitants',()=>{
  const {B}=model();
  assert.equal(B.clinicCapacity(1,5),0);
  assert.equal(B.clinicCapacity(1,12),4);
  const untreated=B.populationChange(12560),treated=B.populationChange(12560,1,12);
  assert.deepEqual(plain(untreated),{before:12560,births:50,deaths:25,arrivals:37,departures:12,after:12610,treated:0,preventedDeaths:0});
  assert.equal(treated.preventedDeaths,4);
  assert.equal(treated.after,12614);
  assert.equal(B.populationChange(16000).after,16000);
  assert.equal(B.populationChange(20000,5,1000).after,20000);
});

test('production is limited independently by activity funds, materials, monthly level and storage',()=>{
  const {B}=model();
  assert.equal(B.productionCount(2,5,[100,100,100],0),0);
  assert.equal(B.productionCount(2,24,[100,100,100],0),1);
  assert.equal(B.productionCount(2,25,[100,100,100],0),2);
  assert.equal(B.productionCount(5,1000,[100,8,100],0),2);
  assert.equal(B.productionCount(5,1000,[100,100,3],0),1);
  assert.equal(B.productionCount(5,1000,[100,100,100],59),1);
  assert.equal(B.productionCount(5,1000,[100,100,100],70),0);
  assert.deepEqual(plain(B.productionMaterials(2)),[0,6,4]);
});

test('weapon primary attribute changes combat power while unused attack attributes do not',()=>{
  const {C}=model(),stats={HP:120,STR:20,INT:30,FAI:40,AGI:10};
  for(const [type,key] of [['剣','STR'],['盾','STR'],['杖','INT'],['魔導書','INT'],['聖書','FAI'],['ロザリオ','FAI']]){
    const weapon={type,power:10},base=C.power(stats,weapon);
    assert.equal(base,23+1.4*stats[key]);
    for(const unused of ['STR','INT','FAI'].filter(k=>k!==key))assert.equal(C.power({...stats,[unused]:9999},weapon),base);
    assert.ok(C.power({...stats,[key]:stats[key]+1},weapon)>base);
  }
  assert.equal(C.power(stats,null),0);
  assert.equal(C.power(stats,{type:'剣',power:10},['戦士訓練 5']),C.power(stats,{type:'剣',power:10})*1.2);
});

test('success chance follows symmetric combat ratios and has no guaranteed outcome for an armed force',()=>{
  const {C}=model();
  assert.equal(C.estimate(100,100).chance,.5);
  assert.equal(C.estimate(50,100).chance,1/17);
  assert.equal(C.estimate(200,100).chance,16/17);
  assert.equal(C.estimate(0,100).chance,0);
  assert.equal(C.estimate(1,10000).chance,.02);
  assert.equal(C.estimate(10000,1).chance,.98);
});

test('CP loss scales with travel, strength, difficulty and personal capacity',()=>{
  const {C}=model();
  assert.equal(C.cpLoss({duration:4}),5);
  assert.equal(C.cpLoss({duration:4,outcome:'defeat'}),9);
  assert.equal(C.cpLoss({duration:4,ratio:2}),4);
  assert.equal(C.cpLoss({duration:4,cp:1.2}),6);
  assert.equal(C.cpLoss({duration:4,maxCP:120}),6);
  assert.equal(C.cpLoss({kind:'defense'}),4);
  assert.equal(C.cpLoss({outcome:'retreat',elapsed:3}),2);
});

test('recovery requires three convalescence days plus difficulty and school based training',()=>{
  const {C}=model();
  assert.equal(C.recoveryDays,3);
  assert.deepEqual(['easy','normal','hard'].map(id=>C.trainingDays('復帰訓練',1,id)),[3,4,5]);
  assert.equal(C.trainingDays('復帰訓練',3,'normal'),3);
  assert.equal(C.trainingDays('復帰訓練',5,'easy',true),2);
  assert.equal(C.trainingDays('初回訓練',1,'normal'),6);
  assert.equal(C.trainingDays('スキル習得',1,'normal'),8);
  assert.equal(C.trainingDays('スキル習得',5,'normal'),16);
});

test('expedition supply and cooldown limit repeated revenue and defeat never removes unavailable cash',()=>{
  const {B,C}=model();
  assert.equal(C.cooldown,12);
  assert.equal(C.supplies(6,2,B.profile('normal')),4);
  assert.deepEqual(plain(C.reward(0,B.profile('normal'))),[18,0,5]);
  assert.equal(C.enemyPower(2,B.profile('normal')),270);
  assert.deepEqual(['easy','normal','hard'].map(id=>C.raidLoss(100,100,id)),[5,10,15]);
  assert.equal(C.raidLoss(100,3,'hard'),3);
});

test('new-game school budgets fund five seats each without editing',()=>{
  const run=game();
  assert.deepEqual(run('budgetValues().budgets'),[14,14,14,0,0,0,0]);
  assert.deepEqual(run('[0,1,2].map(i=>trainingLimit(i))'),[5,5,5]);
  assert.equal(run('state.resources[0]'),300);
});

test('market income is credited once at the following month boundary and matches its confirmed budget',()=>{
  const run=game();
  run('state.levels[4]=1;state.taxOverride=100;state.budgetAmounts=[4,4,4,0,49,0,0];commit()');
  assert.equal(run('state.resources[0]'),339);
  run('state.day=31;advanceDay()');
  assert.deepEqual(run('state.resources'),[420,200,120]);
  assert.equal(run('state.report.finance.market'),81);
  run('advanceDay()');
  assert.deepEqual(run('state.resources'),[420,200,120]);
});

test('workshop consumes only produced materials and refunds the unproduced activity budget',()=>{
  const run=game();
  run('state.resources=[300,3,2];state.levels[6]=2;state.taxOverride=100;state.budgetAmounts=[4,4,4,0,0,0,25];commit()');
  assert.equal(run('state.resources[0]'),363);
  assert.deepEqual(run('productionSettlement()'),{count:1,materials:[0,3,2],refund:10});
  const before=run('state.weaponInventory.length');
  run('state.day=31;advanceDay()');
  assert.equal(run('state.weaponInventory.length'),before+1);
  assert.deepEqual(run('state.resources'),[373,50,30]);
  assert.deepEqual(run('state.report.finance.productionMaterials'),[0,3,2]);
  assert.equal(run('state.report.finance.productionRefund'),10);
});

test('construction cannot queue levels above five even from a legacy high-level save',()=>{
  const run=game();
  assert.equal(run('state.levels[0]=5;orderConstruction(0)'),false);
  assert.equal(run('state.levels[0]=9;orderConstruction(0)'),false);
  assert.equal(run('state.levels[0]'),9);
});

test('seeded random channels reproduce and production cannot reroll a day raid',()=>{
  const run=game();
  run('state.balanceSeed=123456;state.randomCounters={}');
  const expected=run("[gameRandom('raid',0),gameRandom('weapon'),gameRandom('weapon')]");
  run('state.randomCounters={}');
  assert.deepEqual(run("[gameRandom('raid',0),gameRandom('weapon'),gameRandom('weapon')]"),expected);
  const raid=run("gameRandom('raid',0)");
  run("for(let i=0;i<100;i++)gameRandom('weapon')");
  assert.equal(run("gameRandom('raid',0)"),raid);
  assert.notEqual(run("state.day=2;gameRandom('raid',0)"),raid);
});

test('unconfirmed first-day difficulty applies now and confirmed changes apply at the next month',()=>{
  const run=game();
  run("setDifficulty('hard')");
  assert.deepEqual(run('({difficulty:state.difficulty,next:state.nextDifficulty||null,resources:state.resources,tax:taxIncome()})'),{difficulty:'hard',next:null,resources:[300,150,90],tax:85});
  run("commit();setDifficulty('easy')");
  assert.deepEqual(run('({difficulty:state.difficulty,next:state.nextDifficulty,tax:taxIncome()})'),{difficulty:'hard',next:'easy',tax:85});
  run('state.day=31;advanceDay()');
  assert.deepEqual(run('({difficulty:state.difficulty,next:state.nextDifficulty||null,materials:state.resources.slice(1),pending:monthlySettingsPending()})'),{difficulty:'easy',next:null,materials:[210,126],pending:true});
});

test('selecting the current difficulty cancels a reservation without resetting the economy',()=>{
  const run=game();
  run("commit();state.day=2;setDifficulty('hard');setDifficulty('normal')");
  assert.equal(run('state.difficulty'),'normal');
  assert.equal(run('state.nextDifficulty||null'),null);
  assert.equal(run('state.resources[0]'),358);
});

test('training and a returning expedition keep their original difficulty conditions',()=>{
  const run=game();
  run('placeTraining(1,0,1);commit()');
  const training=run('state.characters[1].task');
  run("setDifficulty('hard')");
  assert.deepEqual(run('state.characters[1].task'),training);
  run('state.day=30;formationDraft={0:0};expeditionTarget=0;departExpedition()');
  const snapshot=run('state.expeditions[0].combat');
  run('state.day=31;advanceDay()');
  assert.equal(run('state.difficulty'),'hard');
  assert.deepEqual(run('state.expeditions[0].combat'),snapshot);
  run("setDifficulty('easy')");
  assert.equal(run('state.difficulty'),'easy');
  assert.deepEqual(run('state.expeditions[0].combat'),snapshot);
  assert.equal(snapshot.difficulty,'normal');
});

test('legacy migration preserves stock, population, high levels and inventory while clearing old forced outcomes',()=>{
  const original=game(),legacy=original('({...state,resources:[901,611,377],population:20000,levels:[8,1,1,0,0,0,0],defenseChance:100,mockExpeditionOutcome:"victory"})');
  for(const key of ['difficulty','balanceSeed','randomCounters','balanceRulesVersion'])delete legacy[key];
  const run=game(legacy);
  assert.deepEqual(run('state.resources'),[901,611,377]);
  assert.deepEqual(run('state.levels'),[8,1,1,0,0,0,0]);
  assert.equal(run('state.population'),20000);
  assert.deepEqual(run('state.weaponInventory'),legacy.weaponInventory);
  assert.equal(run('state.difficulty'),'normal');
  assert.equal(run('state.balanceSeed'),1357911);
  assert.equal(run('state.defenseChance??null'),null);
  assert.equal(run('state.mockExpeditionOutcome??null'),null);
  assert.equal(run('state.characters[0].cp'),100);
});

test('a legacy expedition receives a no-reward snapshot without changing its remaining days or CP',()=>{
  const original=game(),legacy=original('({...state,expeditions:[{id:1,target:2,total:2,remaining:1,elapsed:1,outcome:"victory",members:[{id:0,weapon:0,before:100}]}]})');
  delete legacy.balanceRulesVersion;
  const run=game(legacy);
  assert.equal(run('state.expeditions[0].remaining'),1);
  assert.equal(run('state.characters[0].cp'),100);
  assert.equal(run('state.expeditions[0].combat.testOnly'),true);
  assert.deepEqual(run('state.expeditions[0].combat.loot'),[0,0,0]);
});

test('a raid with no usable defenders accepts defeat, clamps loss to available cash and releases time',()=>{
  const run=game();
  run("commit();state.resources[0]=3;state.defenseAttack={enemy:3,date:'1年1月1日',combat:{difficulty:'normal',enemyPower:270,cp:1,roll:.5,loss:10}}");
  assert.equal(run('resolveDefenseAttack()'),true);
  assert.equal(run('state.defenseAttack'),null);
  assert.equal(run('state.resources[0]'),0);
  assert.equal(run('state.defenseReport.loss'),3);
  assert.deepEqual(run('state.defenseReport.members'),[]);
  assert.equal(run('state.defenseReport.outcome'),'防衛失敗');
  assert.equal(run('advanceDay();state.day'),2);
});

test('each army pays supplies on the same date and an active destination rejects duplicate departure',()=>{
  const run=game();
  assert.equal(run('formationDraft={0:0};expeditionTarget=2;departExpedition()'),true);
  assert.equal(run('state.resources[0]'),298);
  assert.equal(run('formationDraft={1:0};expeditionTarget=0;departExpedition()'),true);
  assert.equal(run('state.resources[0]'),294);
  assert.deepEqual(run('state.expeditions.map(m=>m.combat.supplies)'),[2,4]);
  assert.equal(run('formationDraft={3:0};expeditionTarget=0;departExpedition()'),false);
  assert.equal(run('state.resources[0]'),294);
  assert.equal(run('state.expeditions.length'),2);
});

test('insufficient departure supplies keep cash, member availability and destination unchanged',()=>{
  const run=game();
  assert.equal(run('state.resources[0]=1;formationDraft={0:0};expeditionTarget=2;departExpedition()'),false);
  assert.equal(run('state.resources[0]'),1);
  assert.equal(run('state.expeditions.length'),0);
  assert.equal(run('expeditionEligible(state.characters[0])'),true);
});

test('developer forced victories consume supplies and CP but provide no reward',()=>{
  const run=game();
  run("state.mockExpeditionOutcome='victory';formationDraft={0:0};expeditionTarget=2;departExpedition()");
  assert.equal(run('state.expeditions[0].combat.testOnly'),true);
  assert.deepEqual(run('state.expeditions[0].combat.loot'),[0,0,0]);
  run('advanceExpedition();advanceExpedition()');
  assert.equal(run('state.resources[0]'),298);
  assert.ok(run('state.characters[0].cp')<100);
  assert.deepEqual(run('state.expeditionReports[0].loot'),[0,0,0]);
});

test('destination cooldown lasts twelve dates across a month boundary and reopening a save',()=>{
  const run=game();
  run('commit();state.day=30;formationDraft={0:0};expeditionTarget=2;departExpedition();advanceDay();advanceDay()');
  assert.deepEqual(run('({month:state.month,day:state.day})'),{month:2,day:1});
  assert.match(run('expeditionBlockReason(2)'),/あと12日/);
  const saved=run('state'),resumed=game(saved);
  assert.match(resumed('expeditionBlockReason(2)'),/あと12日/);
  assert.equal(resumed('state.day=12;formationDraft={1:0};expeditionTarget=2;departExpedition()'),false);
  assert.equal(resumed('state.day=13;departExpedition()'),true);
});
