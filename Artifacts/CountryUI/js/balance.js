(() => {
  const maxLevel=5,constructionDays=5;
  const profiles=Object.freeze({
    easy:Object.freeze({id:'easy',name:'やさしい',income:1.2,materials:1.2,enemy:.85,cp:.85,reward:1.2,raidChance:.04,raidCooldown:12,recoveryTrainingDays:3}),
    normal:Object.freeze({id:'normal',name:'ふつう',income:1,materials:1,enemy:1,cp:1,reward:1,raidChance:.06,raidCooldown:10,recoveryTrainingDays:4}),
    hard:Object.freeze({id:'hard',name:'むずかしい',income:.85,materials:.85,enemy:1.2,cp:1.2,reward:.85,raidChance:.08,raidCooldown:8,recoveryTrainingDays:5})
  });
  const rules=Object.freeze({
    initialPopulation:12560,taxDivisor:125,populationCapacity:16000,
    upkeepBase:3,schoolSlotsPerLevel:5,schoolCost:2,
    marketActivityPerLevel:45,marketGrossTenths:18,
    clinicPatientsPerLevel:4,clinicCost:2,clinicMortalityReduction:.3,
    productionCost:10,productionWood:3,productionOre:2,inventoryCap:60,
    weaponBasePower:10,weaponPowerPerLevel:8,weaponPowerRandomMax:4
  });
  const effects=Object.freeze({
    school:'維持費を除く活動予算2で訓練枠1人。上限は施設Lv×5人。',
    market:'活動予算を月初に払い、活動額×1.8×難易度の売上を翌月初に受け取る。',
    clinic:'活動予算2で支援1人、上限Lv×4人。支援対象の復帰訓練を1日短縮し、住民の自然死亡も最大30％抑える。',
    workshop:'活動予算10と木材3・鉱石2で武器1本。月Lv本、保管60本まで。'
  });
  const rationale=Object.freeze({
    construction:'新築費140は初期税収100から最低運営費12を引いた88の約1.6か月分。素材60・36は月収の1.2か月分。',
    upkeep:'全6施設Lv5でも維持費48。初期税収に対しnormal48％、hard約57％で、活動予算と建築費の余地を残す。',
    market:'normalのLv1最大運営は予算49、売上81、純益32。新築費140の回収は約4.4か月。',
    growth:'Lv上限、施設別活動予算上限、人口上限で、再投資による無制限の収益増幅を防ぐ。',
    production:'維持費は製作費にならない。製作能力・材料・空き保管枠の最小値で、本数と実消費を決める。'
  });
  const level=n=>Math.min(maxLevel,Math.max(0,Math.floor(Number(n)||0)));
  const profile=id=>profiles[id]||profiles.normal;
  const upkeep=n=>level(n)?level(n)+rules.upkeepBase:0;
  const initialResources=()=>[300,150,90];
  const cost=n=>[140,60,36].map(amount=>Math.round(amount*Math.sqrt(level(n))));
  function budgetCap(index,n){
    const l=level(n);if(!l || index===3)return 0;
    const activity=index<3?rules.schoolSlotsPerLevel*rules.schoolCost:index===4?rules.marketActivityPerLevel:index===5?rules.clinicPatientsPerLevel*rules.clinicCost:index===6?rules.productionCost:0;
    return activity?upkeep(l)+activity*l:0;
  }
  const activity=(n,budget,cap)=>Math.min(cap,Math.max(0,Math.floor(Number(budget)||0)-upkeep(n)));
  const schoolCapacity=(n,budget)=>Math.floor(activity(n,budget,level(n)*rules.schoolSlotsPerLevel*rules.schoolCost)/rules.schoolCost);
  const clinicCapacity=(n,budget)=>Math.floor(activity(n,budget,level(n)*rules.clinicPatientsPerLevel*rules.clinicCost)/rules.clinicCost);
  function marketIncome(n,budget,id='normal'){
    const funded=activity(n,budget,level(n)*rules.marketActivityPerLevel);
    return Math.floor(funded*rules.marketGrossTenths*Math.round(profile(id).income*100)/1000);
  }
  function taxIncome(pop,id='normal'){
    return Math.floor(Math.max(0,pop)*Math.round(profile(id).income*100)/(rules.taxDivisor*100));
  }
  function materialIncome(id='normal'){
    const multiplier=profile(id).materials;
    return [0,Math.round(50*multiplier),Math.round(30*multiplier)];
  }
  function populationChange(pop,clinicLevel=0,clinicBudget=0){
    const before=Math.max(0,Math.floor(pop)),births=Math.floor(before*.004),naturalDeaths=Math.floor(before*.002),arrivals=Math.floor(before*.003);
    const treated=clinicCapacity(clinicLevel,clinicBudget),preventedDeaths=Math.min(treated,Math.floor(naturalDeaths*rules.clinicMortalityReduction));
    const deaths=naturalDeaths-preventedDeaths;
    const departures=Math.floor(before*.001)+Math.max(0,before+births-deaths+arrivals-Math.floor(before*.001)-Math.max(before,rules.populationCapacity));
    return {before,births,deaths,arrivals,departures,after:before+births-deaths+arrivals-departures,treated,preventedDeaths};
  }
  function productionCount(n,budget,resources,inventoryCount=0){
    const l=level(n),funded=Math.floor(activity(l,budget,l*rules.productionCost)/rules.productionCost);
    const materials=resources||[0,0,0];
    return Math.max(0,Math.min(l,funded,Math.floor(materials[1]/rules.productionWood),Math.floor(materials[2]/rules.productionOre),rules.inventoryCap-Math.max(0,Math.floor(inventoryCount))));
  }
  function productionMaterials(count){
    const n=Math.max(0,Math.floor(count));return [0,n*rules.productionWood,n*rules.productionOre];
  }
  globalThis.CountryBalance=Object.freeze({maxLevel,constructionDays,profiles,rules,effects,rationale,profile,initialResources,upkeep,cost,budgetCap,schoolCapacity,marketIncome,clinicCapacity,populationChange,taxIncome,materialIncome,productionCount,productionMaterials});
})();
