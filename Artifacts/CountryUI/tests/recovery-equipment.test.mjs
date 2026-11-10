import { readGameSource, loadGame } from './game-test-helper.mjs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function game() {
  const source=readGameSource().split("document.addEventListener('dragstart'")[0];
  const context=vm.createContext({document:{querySelector:()=>({classList:{add(){},remove(){}}})},localStorage:{getItem:()=>null,setItem(){}},location:{hash:'#kingdom'},setTimeout:()=>1,clearTimeout(){},setInterval:()=>1,clearInterval(){}});
  loadGame(context,source);
  vm.runInContext("render=()=>{};toast=()=>{};showReport=()=>{};showDefenseResult=()=>{};maybeDefenseAttack=()=>{};closeModal=()=>{};openModal=(title,body)=>{globalThis.dialog={title,body};};initializeRecovery();",context);
  return script=>{const result=vm.runInContext(script,context);return result===undefined?undefined:JSON.parse(JSON.stringify(result));};
}

test('initial CP0 characters spend three full days recovering before school placement',()=>{
  const run=game();
  assert.equal(run('characterStatus(state.characters[2])'),'療養中・あと3日');
  assert.equal(run('state.trainingDraft[2]'),null);
  for(let day=1;day<=3;day++) {
    run(`state.day=${day};advanceRecovery();placeTraining(2,0,0);`);
    assert.equal(run('state.trainingDraft[2]'),null);
    assert.equal(run('state.characters[2].cp'),0);
  }
  run('state.day=4;advanceRecovery();placeTraining(2,0,0);');
  assert.equal(run('characterStatus(state.characters[2])'),'療養完了・復帰訓練可能');
  assert.equal(run('state.trainingDraft[2].purpose'),'復帰訓練');
  assert.equal(run('state.characters[2].cp'),0);
});

test('a CP0 transition does not consume its first recovery day immediately or twice in one date',()=>{
  const run=game();
  const before=run('state.day=30;state.characters[0].cp=0;startRecovery(state.characters[0]);advanceRecovery();({remaining:state.characters[0].recoveryDays,cp:state.characters[0].cp})');
  assert.deepEqual(before,{remaining:3,cp:0});
  assert.equal(run('state.day=31;advanceRecovery();advanceRecovery();state.characters[0].recoveryDays'),2);
  assert.equal(run('state.month=2;state.day=1;advanceRecovery();state.characters[0].recoveryDays'),1);
  assert.equal(run('state.day=2;advanceRecovery();state.characters[0].recoveryDays'),0);
  assert.equal(run('state.characters[0].cp'),0);
});

test('recovering characters cannot enter confirmed training and full CP returns only after recovery training',()=>{
  const run=game();
  run("state.trainingPolicy[2]={school:0,slot:0,purpose:'復帰訓練',skill:'戦士訓練 1'};activateTraining();");
  assert.equal(run('state.characters[2].task'),null);
  assert.equal(run('state.trainingDraft[2]'),null);
  run('state.day=4;advanceRecovery();placeTraining(2,0,0);state.trainingPolicy=copyTraining(state.trainingDraft);state.month=2;state.day=1;activateTraining();');
  assert.equal(run('state.characters[2].task.purpose'),'復帰訓練');
  assert.equal(run('state.characters[2].cp'),0);
  run('state.day=4;advanceTraining();');
  assert.equal(run('state.characters[2].cp'),0);
  run('advanceTraining();');
  assert.equal(run('state.characters[2].cp'),0);
  run('state.day=5;advanceTraining();');
  assert.equal(run('state.characters[2].cp'),100);
  assert.equal(run('state.characters[2].task'),null);
});

test('legacy CP0 saves start recovery while already active recovery training keeps its progress',()=>{
  const run=game();
  run("state.characters[0].cp=0;delete state.characters[0].recoveryDays;delete state.characters[0].recoveryDate;state.characters[1].cp=0;state.characters[1].task={school:0,slot:1,purpose:'復帰訓練',skill:'戦士訓練 1'};initializeRecovery();");
  assert.equal(run('characterStatus(state.characters[0])'),'療養中・あと3日');
  assert.equal(run('state.characters[1].task.purpose'),'復帰訓練');
  assert.equal(run('recovering(state.characters[1])'),false);
  run('state.day=2;advanceRecovery();initializeRecovery();');
  assert.equal(run('state.characters[0].recoveryDays'),2);
});

test('completing recovery training leaves the next month review empty and the recovered character available',()=>{
  const run=game();
  run('commit();state.day=4;advanceRecovery();placeTraining(2,0,0);commit();state.day=31;advanceDay();confirmMonthlySettings();state.day=5;advanceTraining();state.day=28;advanceDay();');
  assert.equal(run('state.month'),3);
  assert.equal(run('state.characters[2].cp'),100);
  assert.equal(run('state.trainingPolicy[2]'),null);
  assert.equal(run('state.trainingDraft.filter(Boolean).length'),0);
  assert.equal(run('expeditionEligible(state.characters[2])'),true);
  run('confirmMonthlySettings();');
  assert.equal(run('state.characters[2].task'),null);
  assert.equal(run('expeditionEligible(state.characters[2])'),true);
});

test('finishing current training preserves a confirmed reservation for a different school next month',()=>{
  const run=game();
  run('placeTraining(0,0,0);commit();state.day=2;placeTraining(0,1,0);commit();state.day=31;advanceDay();');
  assert.equal(run('state.characters[0].task'),null);
  assert.equal(run('state.trainingPolicy[0].school'),1);
  run('confirmMonthlySettings();');
  assert.equal(run('state.characters[0].task.school'),1);
});

test('old confirmed policies cannot retrain an already learned school skill after recovery',()=>{
  const run=game();
  run("state.characters[0].skills.push('戦士訓練 1');state.trainingPolicy[0]={school:0,slot:0,purpose:'復帰訓練',skill:'戦士訓練 1'};activateTraining();");
  assert.equal(run('state.characters[0].task'),null);
  assert.equal(run('state.trainingPolicy[0]'),null);
  assert.equal(run('expeditionEligible(state.characters[0])'),true);
});

test('defense and expedition CP exhaustion both begin three days of recovery',()=>{
  const run=game();
  run("state.characters[0].cp=1;state.characters[0].defending=true;state.defenseWeapons={0:0};state.defenseAttack={enemy:1,date:'1年1月1日',combat:{difficulty:'normal',enemyPower:90,cp:1,roll:1}};resolveDefenseAttack();");
  assert.equal(run('state.characters[0].recoveryDays'),3);
  assert.equal(run('state.characters[0].defending'),false);
  run("state.characters[1].cp=1;state.expeditions=[{id:1,target:0,total:6,elapsed:5,remaining:1,outcome:'victory',combat:{difficulty:'normal',ratio:1,cp:1,loot:[0,0,0]},members:[{id:1,weapon:0,before:1}]}];advanceExpedition();");
  assert.equal(run('state.characters[1].recoveryDays'),3);
  assert.equal(run('state.characters[1].cp'),0);
});

test('all six weapon types dim only the two attack abilities they do not use',()=>{
  const run=game();
  for(const [weapon,used] of [[0,'STR'],[1,'STR'],[2,'INT'],[3,'FAI'],[4,'INT'],[5,'FAI']]) {
    const html=run(`expeditionStats(state.characters[0],0,${weapon})`);
    for(const key of ['HP','STR','INT','FAI','AGI']) {
      const unused=['STR','INT','FAI'].includes(key) && key!==used;
      assert.ok(html.includes(`class="${unused?'stat-unused':''}" data-stat="${key}"`),`${weapon}: ${key}`);
    }
  }
  assert.ok(!run("expeditionStats(state.characters[0],0,'')").includes('stat-unused'));
});

test('equipment preview reflects the current and candidate weapons independently',()=>{
  const run=game();
  run("state.characters[0].defending=true;state.defenseWeapons={0:0};equipmentPicker(0,'defense',2);");
  const html=run('dialog.body');
  const str=html.match(/<div class="equipment-stat" data-stat="STR">(.*?)<\/div>/)[1];
  const int=html.match(/<div class="equipment-stat" data-stat="INT">(.*?)<\/div>/)[1];
  assert.match(str,/<b class="">\d+<\/b>/);
  assert.match(str,/<b class="stat-unused /);
  assert.match(int,/<b class="stat-unused">/);
  assert.match(int,/<b class=" stat-up">/);
  assert.ok(!html.match(/<div class="equipment-stat" data-stat="(?:HP|AGI)">(.*?)<\/div>/g).join('').includes('stat-unused'));
});

test('character details follow the weapon actually in use, including during an expedition',()=>{
  const run=game();
  run('state.defenseWeapons={0:0};');
  assert.ok(!run("characterDetails(0,'training')").includes('stat-unused'));
  run("state.expeditions=[{members:[{id:0,weapon:3}]}];");
  const html=run("characterDetails(0,'training')");
  assert.match(html,/class="stat-unused" data-stat="STR"/);
  assert.match(html,/class="stat-unused" data-stat="INT"/);
  assert.match(html,/class="" data-stat="FAI"/);
});

test('unequipped characters show an explicit warning in selected slots, details and equipment comparison',()=>{
  const run=game();
  run("formationDraft={0:''};");
  for(const script of ["assignedCharacter(0,'expedition')","characterDetails(0,'expedition')"]) {
    const html=run(script);assert.match(html,/weapon-missing/);assert.match(html,/武器が未設定！/);
  }
  run("equipmentPicker(0,'expedition');");
  assert.match(run('dialog.body'),/equipment-current weapon-missing/);
  assert.match(run('dialog.body'),/武器が未設定！/);
});

test('school details use this month on day one and next month after day one',()=>{
  const run=game();
  assert.match(run('renderTraining()'),/1年1月の訓練予定/);
  assert.match(run('state.day=2;renderTraining()'),/1年2月の訓練予定/);
});
