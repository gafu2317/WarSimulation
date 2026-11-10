using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class CountryTrainingView : MonoBehaviour
{
    [SerializeField] private CountryRosterView _view;
    [SerializeField] private UnityEngine.UI.Button[] _schools;
    [SerializeField] private TextMeshProUGUI _schoolName, _capacity, _empty;
    [SerializeField] private RectTransform _members;
    [SerializeField] private CountryRosterRow _tokenPrefab;
    [SerializeField] private GameObject _emptySlotPrefab;
    [SerializeField] private CountryRosterConfirm _confirmation;
    [SerializeField] private UnityEngine.UI.Button _learning;
    [SerializeField] private TextMeshProUGUI _learningLabel;
    private CountryRoster _roster;
    private List<int>[] _drafts;
    private CountryRosterRow[] _tokens;
    private GameObject[] _emptySlots;
    private int _school;
    public CountryRosterData Data => _view.Data;
    public bool CanStart => _roster != null && _drafts[_school].Count > 0 && _drafts[_school].Count + _roster.TrainingCount(_school) <= _roster.Capacity(_school);
    public bool ConfirmationOpen => _confirmation.IsOpen;
    public event Action Changed;

    public void Initialize(CountryRoster roster)
    {
        if (_roster != null) return;
        _roster = roster;
        _learning.onClick.AddListener(ShowLearning);
        _drafts = Enumerable.Range(0, Data.Schools.Length).Select(_ => new List<int>()).ToArray();
        _tokens = new CountryRosterRow[roster.Soldiers.Count];
        for (int i = 0; i < _tokens.Length; i++)
        {
            int id = i;
            _tokens[i] = Instantiate(_tokenPrefab, _members); _tokens[i].name = $"Soldier{i + 1:00}";
            _tokens[i].Bind(() => _view.SelectSoldier(id));
        }
        _emptySlots = new GameObject[7];
        for (int i = 0; i < _emptySlots.Length; i++)
        {
            _emptySlots[i] = Instantiate(_emptySlotPrefab, _members);
            _emptySlots[i].name = $"EmptySlot{i + 1}";
        }
        for (int i = 0; i < _schools.Length; i++)
        {
            int id = i;
            _schools[i].onClick.AddListener(() => { _school = id; _view.ShowSoldiers(id); Refresh(); Changed?.Invoke(); });
        }
        _view.SetMemberSelection(id => _drafts[_school].Contains(id), SelectionReason, "選択中");
        _view.Initialize(roster);
        _view.PlaceMemberControls();
        _view.EnableMemberDrag((RectTransform)_members.parent);
        for (int i = 0; i < _tokens.Length; i++)
        {
            int id = i;
            _tokens[i].EnableDrag(_view.ListViewport, () => { if (_drafts[_school].Contains(id)) { _view.SelectSoldier(id); ToggleCandidate(); } });
        }
        _view.SelectionChanged += Refresh;
        _view.ActionRequested += ToggleCandidate;
        if (isActiveAndEnabled) _roster.Changed += RosterChanged;
        _view.ShowSoldiers(_school); Refresh();
    }

    public void Refresh()
    {
        if (_roster == null) return;
        for (int i = 0; i < _schools.Length; i++)
        {
            CountryGovernmentTheme.SelectTab(_schools[i], i == _school);
        }
        var school = Data.Schools[_school];
        int level = _roster.SchoolLevel(_school);
        _learningLabel.text = $"{LearningNames(school.FirstWeapon, school.FirstSkills)}　{LearningNames(school.SecondWeapon, school.SecondSkills)}\n学ぶ技と効果 ›";
        _schoolName.text = $"{school.Name} Lv.{_roster.SchoolLevel(_school)}　{CountryRoster.WeaponName(school.FirstWeapon)}・{CountryRoster.WeaponName(school.SecondWeapon)}";
        int occupied = _roster.TrainingCount(_school), count = _drafts[_school].Count;
        int active = _roster.Soldiers.Where((soldier, id) => soldier.Training?.School == _school && _roster.IsTraining(id)).Count();
        _capacity.text = $"訓練中 {active}　予約 {occupied - active}　選択中 {count} ／ 定員 {_roster.Capacity(_school)}人";
        _empty.gameObject.SetActive(false);
        int remaining = Math.Max(0, _roster.Capacity(_school) - occupied - count);
        for (int i = 0; i < _emptySlots.Length; i++) _emptySlots[i].SetActive(i < remaining);
        for (int i = 0; i < _tokens.Length; i++)
        {
            var state = _roster.Soldiers[i];
            bool training = state.Training?.School == _school, candidate = _drafts[_school].Contains(i);
            _tokens[i].gameObject.SetActive(training || candidate);
            if (!training && !candidate) continue;
            var soldier = Data.Soldiers[i];
            string status = !training ? "選択中" : _roster.IsTraining(i) ? $"{state.Training.Due.Month}/{state.Training.Due.Day} 完了" : $"{state.Training.Start.Month}/{state.Training.Start.Day} 開始";
            _tokens[i].Refresh(soldier.Name, soldier.Icon, status, i == _view.SelectedSoldier, _roster.IsTraining(i));
        }
        int id = _view.SelectedSoldier;
        bool selectedCandidate = _drafts[_school].Contains(id);
        string reason = SelectionReason(id);
        bool canAdd = reason.Length == 0;
        string label = selectedCandidate ? "選択から外す" : _roster.Soldiers[id].Training != null ? (_roster.IsTraining(id) ? "訓練中・変更不可" : "予約済み・変更不可") : "訓練対象に加える";
        string hint = reason.Length > 0 ? reason : "2か月・確定後は変更／取消不可";
        _view.SetAction(label, selectedCandidate || canAdd, hint);
        _view.Refresh();
        string LearningNames(WeaponKind kind, SkillId[] skills)
        {
            var candidates = skills.Take(level).Where(id => id != SkillId.None).ToArray();
            return CountryRoster.WeaponName(kind) + (candidates.Length == 1 ? "：" + Data.SkillName(candidates[0]) : $" {candidates.Length}技");
        }
    }
    private void ShowLearning()
    {
        var school = Data.Schools[_school]; int level = _roster.SchoolLevel(_school);
        var text = new StringBuilder("2か月の訓練で、両方の武器の技を学ぶ。\n");
        Append(school.FirstWeapon, school.FirstSkills); Append(school.SecondWeapon, school.SecondSkills);
        _confirmation.ShowDetails(school.Name + "で学ぶ技", text.ToString());
        void Append(WeaponKind kind, SkillId[] skills)
        {
            text.AppendLine("\n" + CountryRoster.WeaponName(kind));
            for (int i = 0; i < Math.Min(level, skills.Length); i++)
            {
                var id = skills[i]; if (id == SkillId.None) continue;
                var ability = CombatSkillFactory.Create(id);
                string status = _roster.Soldiers[_view.SelectedSoldier].Learned.Contains(id) ? "習得済み" : "習得対象";
                text.AppendLine($"Lv.{i + 1}　{Data.SkillName(id)}　{status}\n{ability?.EffectDescription}　{ability?.PowerDescription}");
            }
        }
    }
    private string SelectionReason(int id)
    {
        var soldier = _roster.Soldiers[id];
        if (soldier.CP == 0) return "復活待ち";
        if (soldier.Expedition != null) return "遠征中";
        if (soldier.Training != null) return _roster.IsTraining(id) ? "訓練中" : "訓練予約済み";
        if (_drafts.Where((_, school) => school != _school).Any(draft => draft.Contains(id))) return "別の学校で選択中";
        if (!_drafts[_school].Contains(id) && _drafts[_school].Count + _roster.TrainingCount(_school) >= _roster.Capacity(_school)) return "定員いっぱい";
        return "";
    }
    private void ToggleCandidate()
    {
        int id = _view.SelectedSoldier;
        var draft = _drafts[_school];
        if (!draft.Remove(id))
        {
            if (!_roster.CanTrain(id) || draft.Count + _roster.TrainingCount(_school) >= _roster.Capacity(_school)
                || _drafts.Any(candidate => candidate.Contains(id))) return;
            draft.Add(id);
        }
        Refresh(); Changed?.Invoke();
    }
    public bool ValidateDrafts(out string reason)
    {
        reason = "";
        var seen = new HashSet<int>();
        for (int school = 0; school < _drafts.Length; school++)
        {
            if (_drafts[school].Count + _roster.TrainingCount(school) > _roster.Capacity(school))
            { reason = "学校の定員を超えている"; return false; }
            foreach (int id in _drafts[school])
                if (!seen.Add(id) || !_roster.CanTrain(id))
                { reason = "訓練できない兵士が含まれている"; return false; }
        }
        return true;
    }

    public bool CommitDrafts(out string reason)
    {
        if (!ValidateDrafts(out reason)) return false;
        var selections = _drafts.Select(draft => draft.ToArray()).ToArray();
        for (int school = 0; school < selections.Length; school++)
            if (selections[school].Length > 0 && !_roster.TryStartTraining(school, selections[school], out reason)) return false;
        foreach (var draft in _drafts) draft.Clear();
        return true;
    }

    public void ClearDrafts()
    {
        if (_drafts == null) return;
        foreach (var draft in _drafts) draft.Clear();
    }

    public void ConfirmStart()
    {
        if (!CanStart) return;
        int school = _school; int[] ids = _drafts[school].ToArray();
        var start = _roster.Today.PlanningStart;
        bool reservation = start.CompareTo(_roster.Today) > 0;
        string names = string.Join("、", ids.Select(id => Data.Soldiers[id].Name));
        string notice = reservation ? "予約を確定すると、開始前でも変更・キャンセルはできない。\n開始日までは出撃可能。開始前にCP0になると予約解除。\n" : "";
        _confirmation.Show(reservation ? "来月の訓練を予約" : "2か月の訓練を開始", $"{Data.Schools[school].Name} Lv.{_roster.SchoolLevel(school)}\n{names}\n\n開始 {start} → 完了 {start.AddMonths(2)}\n{notice}訓練中は出撃できず、途中変更・キャンセルもできない。", reservation ? "訓練を予約" : "訓練を開始", () => {
            if (_roster.TryStartTraining(school, ids, out _)) { _drafts[school].Clear(); _schools[school].Select(); }
            Refresh(); Changed?.Invoke();
        });
    }
    public void CloseConfirmation() => _confirmation.Close();
    private void RosterChanged()
    {
        foreach (var draft in _drafts) draft.RemoveAll(id => !_roster.CanTrain(id));
        Refresh(); Changed?.Invoke();
    }
    private void OnEnable() { if (_roster != null) { _roster.Changed += RosterChanged; Refresh(); } }
    private void OnDisable() { if (_roster != null) _roster.Changed -= RosterChanged; CloseConfirmation(); }
}
