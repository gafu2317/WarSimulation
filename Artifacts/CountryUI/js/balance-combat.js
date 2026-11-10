(() => {
  const clamp=(n,min,max)=>Math.max(min,Math.min(max,n));
  const destinations=[
    {duration:6,enemies:8,terrain:1.1,loot:[18,0,5]},
    {duration:4,enemies:5,terrain:1.05,loot:[14,6,0]},
    {duration:2,enemies:3,terrain:1,loot:[10,0,0]}
  ];
  const primary=type=>({剣:'STR',盾:'STR',杖:'INT',魔導書:'INT',聖書:'FAI',ロザリオ:'FAI'})[type];
  function power(stats,weapon,skills=[]) {
    const key=primary(weapon?.type);if(!key)return 0;
    const school={STR:'戦士',INT:'魔法使い',FAI:'僧侶'}[key];
    const level=Math.max(0,...skills.map(s=>s.startsWith(`${school}訓練 `)?Number(s.match(/(\d+)$/)?.[1]||0):0));
    return Math.max(0,(stats.HP/12+1.4*stats[key]+.3*stats.AGI+weapon.power)*(1+.04*Math.min(5,level)));
  }
  function estimate(power,enemyPower) {
    const ratio=enemyPower>0?Math.max(0,power/enemyPower):power>0?10:0;
    const chance=power>0?clamp(ratio**4/(1+ratio**4),.02,.98):0;
    return {power,enemyPower,ratio,chance};
  }
  function cpLoss({duration=1,ratio=1,cp=1,outcome='victory',kind='expedition',elapsed=duration,maxCP=100}) {
    const base=outcome==='retreat'?.5*Math.max(1,elapsed)*cp:(kind==='defense'?4:1.25*duration)*cp/Math.sqrt(clamp(ratio,.5,2))*(outcome==='defeat'?1.8:1);
    return Math.max(1,Math.ceil(base*maxCP/100));
  }
  function trainingDays(purpose,level,difficulty='normal',supported=false) {
    const adjustment={easy:-1,normal:0,hard:1}[difficulty]??0;
    const days=purpose==='復帰訓練'?4+adjustment-Math.floor((level-1)/2):purpose==='初回訓練'?6+adjustment:6+2*level+adjustment;
    return Math.max(2,days-Number(supported));
  }
  const enemyPower=(target,profile)=>destinations[target].enemies*90*destinations[target].terrain*(profile.enemy??1);
  const reward=(target,profile)=>destinations[target].loot.map((n,i)=>Math.round(n*(i?(profile.materials??1):(profile.reward??1))));
  const supplies=(count,duration,profile)=>Math.ceil((.5*count+.5*duration)*(profile.supplies??1));
  const raidLoss=(tax,balance,difficulty='normal')=>Math.min(balance,Math.ceil(tax*.1*({easy:.5,normal:1,hard:1.5}[difficulty]??1)));
  globalThis.CountryCombat={destinations,primary,power,estimate,cpLoss,trainingDays,enemyPower,reward,supplies,raidLoss,cooldown:12,recoveryDays:3};
})();
