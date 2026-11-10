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
    private readonly int[] _owners, _levels;
    public CountryRosterData Data => _data;
    public IReadOnlyList<CountrySoldierState> Soldiers => _soldiers;
    public CountryDate Today { get; private set; }
    public event Action Changed;

    public CountryRoster(CountryRosterData data, CountryDate today)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Today = today;
        _owners = new int[data.Weapons.Length]; Array.Fill(_owners, -1);
        var ids = new HashSet<string>();
        foreach (var weapon in data.Weapons)
            if (string.IsNullOrEmpty(weapon.Id) || !ids.Add(weapon.Id) || weapon.Kind == WeaponKind.Unarmed)
                throw new ArgumentException("Weapons require distinct IDs and a weapon kind.");
        ids.Clear();
        _soldiers = new CountrySoldierState[data.Soldiers.Length];
        for (int i = 0; i < _soldiers.Length; i++)
        {
            var definition = data.Soldiers[i];
            if (string.IsNullOrEmpty(definition.Id) || !ids.Add(definition.Id) || definition.MaxCP < 1)
                throw new ArgumentException("Soldiers require distinct IDs and positive CP.");
            _soldiers[i] = new CountrySoldierState(definition.MaxCP, BasicSkills);
            if (string.IsNullOrEmpty(definition.InitialWeaponId)) continue;
            int weapon = Array.FindIndex(data.Weapons, item => item.Id == definition.InitialWeaponId);
            if (weapon < 0 || _owners[weapon] >= 0) throw new ArgumentException("Initial equipment must exist and have one owner.");
            _owners[weapon] = i;
        }
        _levels = new int[data.Schools.Length];
        for (int i = 0; i < _levels.Length; i++) _levels[i] = Math.Clamp(data.Schools[i].InitialLevel, 1, 5);
    }

    public int SchoolLevel(int school) => _levels[school];
    public int Capacity(int school) => _levels[school] + 2;
    public int TrainingCount(int school)
    {
        int count = 0;
        foreach (var soldier in _soldiers) if (soldier.Training?.School == school) count++;
        return count;
    }
    public void SetSchoolLevel(FacilityType facility, int level)
    {
        for (int i = 0; i < _levels.Length; i++)
            if (_data.Schools[i].Facility == facility && _levels[i] != Math.Clamp(level, 1, 5))
            { _levels[i] = Math.Clamp(level, 1, 5); Changed?.Invoke(); return; }
    }
    public int WeaponOwner(int weapon) => _owners[weapon];
    public int EquippedWeapon(int soldier) => Array.IndexOf(_owners, soldier);
    public WeaponKind Profession(int soldier)
    {
        int weapon = EquippedWeapon(soldier);
        return weapon < 0 ? WeaponKind.Unarmed : _data.Weapons[weapon].Kind;
    }
    public CountrySoldierStats Stats(int soldier)
    {
        int weapon = EquippedWeapon(soldier);
        return _data.Soldiers[soldier].InitialStats + _soldiers[soldier].Growth + (weapon < 0 ? default : _data.Weapons[weapon].Bonus);
    }
    public bool IsTraining(int soldier) => _soldiers[soldier].Training is { } training && Today.CompareTo(training.Start) >= 0;
    public bool CanDeploy(int soldier) => _soldiers[soldier].CP > 0 && !IsTraining(soldier) && _soldiers[soldier].Expedition == null;
    public bool CanTrain(int soldier) => _soldiers[soldier].CP > 0 && _soldiers[soldier].Training == null && _soldiers[soldier].Expedition == null;
    public bool CanUse(int soldier, SkillId skill) => _soldiers[soldier].Learned.Contains(skill)
        && _data.Skills != null && _data.Skills.TryGetDefinition(skill, out var definition) && definition.RequiredWeaponKind == Profession(soldier);

    public bool TryEquip(int soldier, int weapon)
    {
        if (soldier < 0 || soldier >= _soldiers.Length || weapon < -1 || weapon >= _owners.Length) return false;
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
            soldier.CP = Math.Max(0, soldier.CP - cost);
            if (soldier.CP > 0 || soldier.RevivalDue.HasValue) continue;
            soldier.Training = null;
            soldier.RevivalDue = battleDate.AddMonths(1);
        }
        Changed?.Invoke();
    }

    internal void Return(CountryExpeditionTrip trip)
    {
        foreach (int soldier in trip.Members)
            if (_soldiers[soldier].Expedition == trip) _soldiers[soldier].Expedition = null;
        Changed?.Invoke();
    }

    public bool TryStartTraining(int school, IReadOnlyList<int> soldiers, out string reason)
    {
        reason = "";
        if (school < 0 || school >= _levels.Length || soldiers == null || soldiers.Count == 0) { reason = "訓練する兵士を選ぶ"; return false; }
        if (TrainingCount(school) + soldiers.Count > Capacity(school)) { reason = "学校の定員を超えている"; return false; }
        var unique = new HashSet<int>();
        foreach (int id in soldiers)
            if (id < 0 || id >= _soldiers.Length || !unique.Add(id) || !CanTrain(id)) { reason = "訓練中・復活待ち・遠征中の兵士は選べない"; return false; }
        var definition = _data.Schools[school];
        var skills = TrainingSkills(school);
        foreach (int id in soldiers)
            _soldiers[id].Training = new CountryTraining(school, _levels[school], Today.PlanningStart, definition.Gains, skills);
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
            for (int i = 0; i < Math.Min(_levels[school], slots.Length); i++)
                if (slots[i] != SkillId.None && !skills.Contains(slots[i]) && _data.Skills != null && _data.Skills.TryGetDefinition(slots[i], out _)) skills.Add(slots[i]);
        }
    }

    public bool TrySetCP(int soldier, int cp)
    {
        if (soldier < 0 || soldier >= _soldiers.Length || cp < 0 || cp > _data.Soldiers[soldier].MaxCP) return false;
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
                soldier.CP = _data.Soldiers[id].MaxCP;
                soldier.Growth = default;
                soldier.ResetSkills(BasicSkills);
                soldier.RevivalDue = null;
            }
            var training = soldier.Training;
            if (training == null || today.CompareTo(training.Due) < 0) continue;
            soldier.Growth += training.Gains;
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
    public CountrySoldierStats Growth { get; internal set; }
    public CountryTraining Training { get; internal set; }
    public CountryExpeditionTrip Expedition { get; internal set; }
    public CountryDate? RevivalDue { get; internal set; }
    public IReadOnlyList<SkillId> Learned => _learnedView;
    internal CountrySoldierState(int cp, IEnumerable<SkillId> skills)
    { CP = cp; _learned = new List<SkillId>(skills); _learnedView = _learned.AsReadOnly(); }
    internal void Learn(SkillId skill) { if (!_learned.Contains(skill)) _learned.Add(skill); }
    internal void ResetSkills(IEnumerable<SkillId> skills) { _learned.Clear(); _learned.AddRange(skills); }
}

public sealed class CountryTraining
{
    public readonly int School, Level;
    public readonly CountryDate Start, Due;
    public readonly CountrySoldierStats Gains;
    public readonly IReadOnlyList<SkillId> Skills;
    public CountryTraining(int school, int level, CountryDate start, CountrySoldierStats gains, SkillId[] skills)
    { School = school; Level = level; Start = start; Due = start.AddMonths(2); Gains = gains; Skills = Array.AsReadOnly((SkillId[])skills.Clone()); }
    public float Progress(CountryDate today) => today.CompareTo(Start) < 0 ? 0 : Math.Clamp((float)Start.DaysUntil(today) / Start.DaysUntil(Due), 0, 1);
}
