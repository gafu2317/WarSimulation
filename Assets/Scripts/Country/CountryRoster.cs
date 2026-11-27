using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CountryRoster
{
    public static IReadOnlyList<SkillId> BasicSkills { get; } = Array.AsReadOnly(new[] {
        SkillId.Sword_Slash, SkillId.Shield_Slash, SkillId.Wand_Bolt,
        SkillId.Grimoire_Bolt, SkillId.Bible_Smite, SkillId.Rosary_Strike
    });
    private readonly CountryRosterData _data;
    private readonly CountrySoldierState[] _soldiers;
    private readonly List<int> _owners = new();
    private readonly List<CountryWeaponItem> _weapons = new();
    private readonly Func<string, int> _facilityLevel;
    private readonly Random _forgeRandom = new();
    private readonly Random _personalityRandom;
    private int _lastForgeMonth = -1;
    private int _spiritCPBonus;
    private readonly List<string> _battleNotices = new();
    public int WeaponCount => _weapons.Count;
    public CountryRosterData Data => _data;
    public IReadOnlyList<CountrySoldierState> Soldiers => _soldiers;
    public CountryDate Today { get; private set; }
    public event Action Changed;
    internal event Action<int> WeaponBroken;

    public CountryRoster(CountryRosterData data, CountryDate today, Func<string, int> facilityLevel = null)
        : this(data, today, facilityLevel, new Random()) { }

    internal CountryRoster(CountryRosterData data, CountryDate today, Func<string, int> facilityLevel, Random personalityRandom)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Today = today;
        _facilityLevel = facilityLevel;
        _personalityRandom = personalityRandom;
        var ids = new HashSet<string>();
        foreach (var weapon in data.Weapons)
            if (string.IsNullOrEmpty(weapon.Id) || !ids.Add(weapon.Id) || weapon.Kind == WeaponKind.Unarmed)
                throw new ArgumentException("Weapons require distinct IDs and a weapon kind.");
        foreach (var weapon in data.Weapons)
            for (int i = 0; i < (weapon.Tier == 1 ? 3 : weapon.Tier == 0 ? 1 : 0); i++)
                AddWeapon(weapon);
        ids.Clear();
        _soldiers = new CountrySoldierState[data.Soldiers.Length];
        for (int i = 0; i < _soldiers.Length; i++)
        {
            var definition = data.Soldiers[i];
            if (string.IsNullOrEmpty(definition.Id) || !ids.Add(definition.Id) || definition.MaxCP < 1)
                throw new ArgumentException("Soldiers require distinct IDs and positive CP.");
            _soldiers[i] = new CountrySoldierState(definition.MaxCP, BasicSkills, data.Schools.Length);
            if (string.IsNullOrEmpty(definition.InitialWeaponId)) continue;
            int weapon = _weapons.FindIndex(item => item.Template.Id == definition.InitialWeaponId);
            if (weapon < 0 || _owners[weapon] >= 0) throw new ArgumentException("Initial equipment must exist and have one owner.");
            _owners[weapon] = i;
        }
    }

    internal string SoldierId(int index) => _data.Soldiers[index].Id;

    internal int SoldierIndex(string id)
    {
        int index = Array.FindIndex(_data.Soldiers, soldier => soldier.Id == id);
        CountryCheckpoint.Require(index >= 0, "soldier ID");
        return index;
    }

    internal CountryCheckpoint.RosterState Capture()
    {
        return new CountryCheckpoint.RosterState {
            Soldiers = _soldiers.Select((soldier, index) => new CountryCheckpoint.Soldier {
                Id = SoldierId(index),
                CP = soldier.CP,
                Personality = soldier.Personality,
                RevivalDue = new CountryCheckpoint.Date(soldier.RevivalDue.GetValueOrDefault()),
                Learned = soldier.Learned.ToArray(),
                Completed = soldier.BestSchoolLevels.Select((level, school) => new CountryCheckpoint.SchoolLevel {
                    School = _data.Schools[school].Name, Level = level
                }).Where(item => item.Level > 0).ToArray(),
                TrainingSchool = soldier.Training != null ? _data.Schools[soldier.Training.School].Name : null,
                TrainingStart = new CountryCheckpoint.Date(soldier.Training?.Start ?? default),
                TrainingLevel = soldier.Training?.Level ?? 0,
                TrainingSkills = soldier.Training?.Skills.ToArray()
            }).ToArray(),
            Weapons = _weapons.Select((weapon, index) => new CountryCheckpoint.Weapon {
                Template = weapon.Template.Id, Durability = weapon.Durability,
                Owner = _owners[index] >= 0 ? SoldierId(_owners[index]) : null
            }).ToArray(),
            LastForgeMonth = _lastForgeMonth,
            BattleNotices = _battleNotices.ToArray()
        };
    }

    internal void Restore(CountryCheckpoint.RosterState save, bool legacyCP = false)
    {
        CountryCheckpoint.Require(save.Soldiers != null && save.Soldiers.Length > 0 && save.Weapons != null &&
            save.BattleNotices != null && save.LastForgeMonth >= -1 && save.LastForgeMonth <= Today.MonthIndex, "roster");
        var restored = new HashSet<int>();
        foreach (var entry in save.Soldiers)
        {
            int id = SoldierIndex(entry.Id);
            var revival = entry.RevivalDue.OptionalDate();
            CountryCheckpoint.Require(restored.Add(id) && entry.CP >= 0 && (entry.CP == 0) == revival.HasValue &&
                (!revival.HasValue || revival.Value.CompareTo(Today) > 0) &&
                CombatAiPersonalityProfile.IsSelectable(entry.Personality) &&
                entry.Completed != null && ValidSkills(entry.Learned), "soldier state");
            var soldier = _soldiers[id];
            int bonus = legacyCP && entry.CP > 0 ? _spiritCPBonus : 0;
            soldier.CP = (int)Math.Min(MaxCP(id), (long)entry.CP + bonus);
            soldier.Personality = entry.Personality;
            soldier.RevivalDue = revival;
            soldier.ResetSkills(BasicSkills.Concat(entry.Learned).Distinct());
            foreach (var completion in entry.Completed)
            {
                int school = SchoolIndex(completion.School);
                CountryCheckpoint.Require(soldier.BestSchoolLevels[school] == 0 && completion.Level is >= 1 and <= 5, "completed training");
                soldier.BestSchoolLevels[school] = completion.Level;
            }
            if (string.IsNullOrEmpty(entry.TrainingSchool)) continue;
            CountryCheckpoint.Require(entry.CP > 0 && entry.TrainingLevel is >= 1 and <= 5 && ValidSkills(entry.TrainingSkills), "training");
            var training = new CountryTraining(SchoolIndex(entry.TrainingSchool), entry.TrainingLevel, entry.TrainingStart.ToDate(), entry.TrainingSkills);
            CountryCheckpoint.Require(training.Due.CompareTo(Today) > 0, "training due date");
            soldier.Training = training;
        }

        _weapons.Clear();
        _owners.Clear();
        var templates = _data.Weapons.ToDictionary(weapon => weapon.Id);
        var equipped = new HashSet<int>();
        foreach (var entry in save.Weapons)
        {
            CountryCheckpoint.Require(entry.Template != null && templates.ContainsKey(entry.Template) && entry.Durability >= 0, "weapon");
            int owner = string.IsNullOrEmpty(entry.Owner) ? -1 : SoldierIndex(entry.Owner);
            CountryCheckpoint.Require(owner < 0 || equipped.Add(owner), "weapon owner");
            AddWeapon(templates[entry.Template]);
            _weapons[^1].Durability = Math.Min(_weapons[^1].Durability, entry.Durability);
            _owners[^1] = owner;
        }
        _lastForgeMonth = save.LastForgeMonth;
        _battleNotices.AddRange(save.BattleNotices);
    }

    private int SchoolIndex(string name)
    {
        int index = Array.FindIndex(_data.Schools, school => school.Name == name);
        CountryCheckpoint.Require(index >= 0, "school name");
        return index;
    }

    private static bool ValidSkills(SkillId[] skills) => skills != null &&
        skills.All(skill => skill != SkillId.None && Enum.IsDefined(typeof(SkillId), skill));

    public int SchoolLevel(int school) => Math.Clamp(_facilityLevel?.Invoke(_data.Schools[school].Name) ?? 1, 0, 5);
    public int Capacity(int school)
    {
        int level = SchoolLevel(school);
        return level > 0 ? level + 2 : 0;
    }

    private float[] ForgeWeights(int level, int funding, int minimum)
    {
        int highest = Math.Min(level, Math.Min(4, _data.ForgeWeights.Length));
        if (highest == 0) return Array.Empty<float>();
        var weights = new float[highest];
        for (int i = 0; i < highest; i++)
            if (_data.Weapons.Any(weapon => weapon.Tier == i + 1))
                weights[i] = Math.Max(0, _data.ForgeWeights[highest - 1][i]);
        if (highest == 1 || minimum <= 0) return weights;
        float extra = Math.Clamp((funding - minimum) / (float)minimum, 0, 1);
        float boost = Math.Min(weights[0], extra * weights[highest - 1]);
        weights[0] -= boost;
        weights[highest - 1] += boost;
        return weights;
    }

    public float HighestTierChance(int level, int funding, int minimum)
    {
        if (level <= 0 || funding < minimum) return 0;
        var weights = ForgeWeights(level, funding, minimum);
        double total = weights.Sum(weight => (double)weight);
        return total > 0 ? (float)(weights[^1] / total) : 0;
    }

    public IReadOnlyList<string> Forge(CountryDate date, int level, int funding, int minimum)
    {
        if (date.Day != 1 || date.MonthIndex <= _lastForgeMonth) return Array.Empty<string>();
        _lastForgeMonth = date.MonthIndex;
        if (level <= 0 || funding < minimum) return Array.Empty<string>();
        var weights = ForgeWeights(level, funding, minimum);
        double total = weights.Sum(weight => (double)weight);
        if (total <= 0) return Array.Empty<string>();
        var produced = new string[3];
        for (int item = 0; item < produced.Length; item++)
        {
            double roll = _forgeRandom.NextDouble() * total;
            int tier = 0;
            while (tier < weights.Length - 1 && (weights[tier] == 0 || roll >= weights[tier])) roll -= weights[tier++];
            var templates = _data.Weapons.Where(weapon => weapon.Tier == tier + 1).ToArray();
            var template = templates[_forgeRandom.Next(templates.Length)];
            AddWeapon(template);
            produced[item] = template.Name;
        }
        Changed?.Invoke();
        return produced;
    }
    public int TrainingCount(int school)
    {
        int count = 0;
        foreach (var soldier in _soldiers) if (soldier.Training?.School == school) count++;
        return count;
    }
    private void AddWeapon(CountryWeaponDefinition template)
    {
        _weapons.Add(new CountryWeaponItem(template));
        _owners.Add(-1);
    }

    public CountryWeaponDefinition Weapon(int id) => _weapons[id].Template;
    public int Durability(int id) => _weapons[id].Durability;
    public bool IsUsableWeapon(int id) => id >= 0 && id < _weapons.Count && Durability(id) > 0;

    public IReadOnlyList<string> TakeBattleNotices()
    {
        var notices = _battleNotices.ToArray();
        _battleNotices.Clear();
        return notices;
    }

    public string BattleWarning(int soldier, int weapon, int cost)
    {
        string name = _data.Soldiers[soldier].Name;
        var warnings = new List<string>();
        if (_soldiers[soldier].CP <= cost) warnings.Add($"{name}のCPが0になる");
        if (IsUsableWeapon(weapon) && Durability(weapon) == 1) warnings.Add($"{name}の{Weapon(weapon).Name}が破損する");
        return string.Join("\n", warnings);
    }

    public int WeaponOwner(int weapon) => weapon < 0 ? -1 : _owners[weapon];
    public int EquippedWeapon(int soldier) => _owners.IndexOf(soldier);
    public WeaponKind Profession(int soldier)
    {
        int weapon = EquippedWeapon(soldier);
        return !IsUsableWeapon(weapon) ? WeaponKind.Unarmed : Weapon(weapon).Kind;
    }
    public CountrySoldierStats Stats(int soldier) => Stats(soldier, EquippedWeapon(soldier));
    public CountrySoldierStats Stats(int soldier, int weapon, int previewSchool = -1, int previewLevel = 0)
    {
        CountrySoldierStats percent = default;
        for (int i = 0; i < _data.Schools.Length; i++)
        {
            int level = _soldiers[soldier].BestSchoolLevels[i];
            if (i == previewSchool) level = Math.Max(level, previewLevel);
            percent += _data.Schools[i].BonusAt(level);
        }
        return _data.Soldiers[soldier].InitialStats.Grown(percent) + (!IsUsableWeapon(weapon) ? default : Weapon(weapon).Bonus);
    }

    internal CountrySoldierStats BattleStats(int soldier, int weapon, int previewSchool = -1, int previewLevel = 0)
    {
        var stats = Stats(soldier, weapon, previewSchool, previewLevel);
        var personality = _soldiers[soldier].Personality;
        return new CountrySoldierStats {
            HP = Adjust(stats.HP, CombatStat.HP), STR = Adjust(stats.STR, CombatStat.STR),
            INT = Adjust(stats.INT, CombatStat.INT), FAI = Adjust(stats.FAI, CombatStat.FAI),
            AGI = Adjust(stats.AGI, CombatStat.AGI)
        };

        int Adjust(int value, CombatStat stat) => CombatAiPersonalityProfile.ApplyStatMultiplier(value,
            CombatAiPersonalityProfile.GetStatMultiplier(personality, stat));
    }
    public bool IsTraining(int soldier) => _soldiers[soldier].Training is { } training && Today.CompareTo(training.Start) >= 0;
    public bool CanDeploy(int soldier) => _soldiers[soldier].CP > 0 && !IsTraining(soldier) && _soldiers[soldier].Expedition == null;
    public bool CanTrain(int soldier) => _soldiers[soldier].CP > 0 && _soldiers[soldier].Training == null && _soldiers[soldier].Expedition == null;
    public bool CanUse(int soldier, SkillId skill) => _soldiers[soldier].Learned.Contains(skill)
        && _data.Skills != null && _data.Skills.TryGetDefinition(skill, out var definition) && definition.RequiredWeaponKind == Profession(soldier);

    public bool TryEquip(int soldier, int weapon)
    {
        if (soldier < 0 || soldier >= _soldiers.Length || weapon < -1 || (weapon >= 0 && !IsUsableWeapon(weapon))) return false;
        if (_soldiers[soldier].Expedition != null || (weapon >= 0 && _owners[weapon] >= 0 && _soldiers[_owners[weapon]].Expedition != null)) return false;
        int old = EquippedWeapon(soldier);
        if (old == weapon) return true;
        if (old >= 0) _owners[old] = -1;
        if (weapon >= 0) _owners[weapon] = soldier;
        Changed?.Invoke();
        return true;
    }

    internal void Depart(CountryExpeditionTrip trip)
    {
        foreach (int soldier in trip.Members) _soldiers[soldier].Expedition = trip;
        Changed?.Invoke();
    }

    internal void ConsumeBattleCP(IReadOnlyList<int> members, int cost, CountryDate battleDate)
    {
        foreach (int id in members)
        {
            var soldier = _soldiers[id];
            int weapon = EquippedWeapon(id);
            if (IsUsableWeapon(weapon) && --_weapons[weapon].Durability == 0)
            {
                _battleNotices.Add($"{Weapon(weapon).Name}（{_data.Soldiers[id].Name}装備）が破損した");
                if (soldier.Expedition == null) _owners[weapon] = -1;
                WeaponBroken?.Invoke(id);
            }
            soldier.CP = Math.Max(0, soldier.CP - cost);
            if (soldier.CP > 0 || soldier.RevivalDue.HasValue) continue;
            bool hadReservation = soldier.Training != null;
            soldier.Training = null;
            soldier.RevivalDue = battleDate.AddMonths(1);
            _battleNotices.Add($"{_data.Soldiers[id].Name}のCPが0になった。{soldier.RevivalDue.Value}に復活する。" +
                (hadReservation ? "訓練予約は解除された。" : ""));
        }
        Changed?.Invoke();
    }

    internal void Return(CountryExpeditionTrip trip)
    {
        foreach (int soldier in trip.Members)
        {
            if (_soldiers[soldier].Expedition != trip) continue;
            _soldiers[soldier].Expedition = null;
            int weapon = EquippedWeapon(soldier);
            if (weapon >= 0 && Durability(weapon) == 0) _owners[weapon] = -1;
        }
        Changed?.Invoke();
    }

    public bool TryStartTraining(int school, IReadOnlyList<int> soldiers, out string reason)
    {
        reason = "";
        if (school < 0 || school >= _data.Schools.Length || soldiers == null || soldiers.Count == 0) { reason = "訓練する兵士を選ぶ"; return false; }
        if (TrainingCount(school) + soldiers.Count > Capacity(school)) { reason = "学校の定員を超えている"; return false; }
        var unique = new HashSet<int>();
        foreach (int id in soldiers)
            if (id < 0 || id >= _soldiers.Length || !unique.Add(id) || !CanTrain(id)) { reason = "訓練中・復活待ち・遠征中の兵士は選べない"; return false; }
        int level = SchoolLevel(school);
        var skills = TrainingSkills(school);
        foreach (int id in soldiers)
            _soldiers[id].Training = new CountryTraining(school, level, Today.PlanningStart, skills);
        Changed?.Invoke();
        return true;
    }

    public SkillId[] TrainingSkills(int school)
    {
        var definition = _data.Schools[school];
        var skills = new List<SkillId>();
        Add(definition.FirstSkills); Add(definition.SecondSkills);
        return skills.ToArray();
        void Add(SkillId[] slots)
        {
            for (int i = 0; i < Math.Min(SchoolLevel(school), slots.Length); i++)
                if (slots[i] != SkillId.None && !skills.Contains(slots[i]) && _data.Skills != null && _data.Skills.TryGetDefinition(slots[i], out _)) skills.Add(slots[i]);
        }
    }

    public int MaxCP(int soldier) => (int)Math.Min(int.MaxValue, (long)_data.Soldiers[soldier].MaxCP + _spiritCPBonus);

    internal void SetSpiritSchoolLevel(int level)
    {
        int bonus = CountryGovernmentData.SpiritCPBonus(level);
        if (bonus == _spiritCPBonus) return;
        int increase = Math.Max(0, bonus - _spiritCPBonus);
        _spiritCPBonus = bonus;
        for (int i = 0; i < _soldiers.Length; i++)
            if (_soldiers[i].CP > 0)
                _soldiers[i].CP = (int)Math.Min(MaxCP(i), (long)_soldiers[i].CP + increase);
        Changed?.Invoke();
    }

    public bool TrySetCP(int soldier, int cp)
    {
        if (soldier < 0 || soldier >= _soldiers.Length || cp < 0 || cp > MaxCP(soldier)) return false;
        var state = _soldiers[soldier];
        if (IsTraining(soldier) || state.RevivalDue.HasValue) return false;
        state.CP = cp;
        if (cp == 0) { state.Training = null; state.RevivalDue = Today.AddMonths(1); }
        Changed?.Invoke();
        return true;
    }

    public void AdvanceTo(CountryDate today)
    {
        if (today.CompareTo(Today) <= 0) return;
        Today = today;
        foreach (var soldier in _soldiers)
        {
            if (soldier.RevivalDue.HasValue && today.CompareTo(soldier.RevivalDue.Value) >= 0)
            {
                int id = Array.IndexOf(_soldiers, soldier);
                soldier.CP = MaxCP(id);
                Array.Clear(soldier.BestSchoolLevels, 0, soldier.BestSchoolLevels.Length);
                soldier.ResetSkills(BasicSkills);
                var personalities = CombatAiPersonalityProfile.BuiltInKinds;
                soldier.Personality = personalities[_personalityRandom.Next(personalities.Length)];
                soldier.RevivalDue = null;
            }
            var training = soldier.Training;
            if (training == null || today.CompareTo(training.Due) < 0) continue;
            soldier.BestSchoolLevels[training.School] = Math.Max(soldier.BestSchoolLevels[training.School], training.Level);
            foreach (var skill in training.Skills) soldier.Learn(skill);
            soldier.Training = null;
        }
        Changed?.Invoke();
    }

    public static string WeaponName(WeaponKind kind) => kind switch {
        WeaponKind.Sword => "剣", WeaponKind.Shield => "盾", WeaponKind.Wand => "杖",
        WeaponKind.Grimoire => "魔導書", WeaponKind.Bible => "聖書", WeaponKind.Rosary => "ロザリオ", _ => "未装備"
    };
}

public sealed class CountrySoldierState
{
    private readonly List<SkillId> _learned;
    private readonly IReadOnlyList<SkillId> _learnedView;
    public int CP { get; internal set; }
    public CombatAiPersonalityKind Personality { get; internal set; } = CombatAiPersonalityKind.Neutral;
    internal int[] BestSchoolLevels { get; }
    public int BestSchoolLevel(int school) => BestSchoolLevels[school];
    public CountryTraining Training { get; internal set; }
    public CountryExpeditionTrip Expedition { get; internal set; }
    public CountryDate? RevivalDue { get; internal set; }
    public IReadOnlyList<SkillId> Learned => _learnedView;
    internal CountrySoldierState(int cp, IEnumerable<SkillId> skills, int schools)
    { CP = cp; BestSchoolLevels = new int[schools]; _learned = new List<SkillId>(skills); _learnedView = _learned.AsReadOnly(); }
    internal void Learn(SkillId skill) { if (!_learned.Contains(skill)) _learned.Add(skill); }
    internal void ResetSkills(IEnumerable<SkillId> skills) { _learned.Clear(); _learned.AddRange(skills); }
}

public sealed class CountryTraining
{
    public readonly int School, Level;
    public readonly CountryDate Start, Due;
    public readonly IReadOnlyList<SkillId> Skills;
    public CountryTraining(int school, int level, CountryDate start, SkillId[] skills)
    { School = school; Level = level; Start = start; Due = start.AddMonths(2); Skills = Array.AsReadOnly((SkillId[])skills.Clone()); }
    public float Progress(CountryDate today) => today.CompareTo(Start) < 0 ? 0 : Math.Clamp((float)Start.DaysUntil(today) / Start.DaysUntil(Due), 0, 1);
}

public sealed class CountryWeaponItem
{
    public CountryWeaponDefinition Template { get; }
    public int Durability { get; internal set; }

    internal CountryWeaponItem(CountryWeaponDefinition template)
    {
        Template = template;
        Durability = Math.Max(1, template.MaxDurability);
    }
}
