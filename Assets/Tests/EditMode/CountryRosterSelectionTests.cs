using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CountryRosterSelectionTests
{
    private GameObject _root;
    private CountryRosterView _view;
    private ScrollRect _list;
    private bool _member, _unavailable;

    [SetUp]
    public void SetUp()
    {
        _member = _unavailable = false;
        _root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Country/UI/CountryRosterView.prefab");
        _view = _root.GetComponent<CountryRosterView>();
        _list = (ScrollRect)new SerializedObject(_view).FindProperty("_list").objectReferenceValue;
        _view.SetMemberSelection(id => id == 0 && _member, id => id == 0 && _unavailable ? "訓練中" : "", "選択中");
        _view.Initialize(new CountryRoster(_view.Data, new CountryDate(0, 1, 1)));
        _view.ShowSoldiers(0);
    }

    [TearDown] public void TearDown() => PrefabUtility.UnloadPrefabContents(_root);
    private Transform Row(int id) => _list.content.Find($"Row{id + 1:00}");
    private string State(int id) => Row(id).Find("State").GetComponent<TextMeshProUGUI>().text;
    private float Opacity(int id)
    {
        var group = Row(id).GetComponent<CanvasGroup>();
        return group != null ? group.alpha : 1;
    }

    [Test]
    public void MembershipRemainsVisibleWhenAnotherSoldierHasFocus()
    {
        _member = true; _view.Refresh(); _view.SelectSoldier(1);
        Assert.That(State(0), Is.EqualTo("選択中"));
        Assert.That(Row(0).Find("State").GetComponent<TextMeshProUGUI>().color, Is.EqualTo(CountryGovernmentTheme.Positive));
        Assert.That(Row(0).Find("Selected").gameObject.activeSelf, Is.False);
        Assert.That(Row(1).Find("Selected").gameObject.activeSelf, Is.True);
        _member = false; _view.Refresh();
        Assert.That(State(0), Is.Not.EqualTo("選択中"));
        Assert.That(Row(0).Find("State").GetComponent<TextMeshProUGUI>().color, Is.EqualTo(CountryGovernmentTheme.Muted));
    }

    [Test]
    public void UnavailableMembersStayWithTheSelectionAndExplainWhy()
    {
        _member = _unavailable = true; _view.Refresh();
        Assert.That(Row(0).GetSiblingIndex(), Is.Zero);
        Assert.That(Opacity(0), Is.EqualTo(1));
        Assert.That(Row(0).GetComponent<Button>().interactable, Is.True);
        Assert.That(State(0), Is.EqualTo("選択中・訓練中"));
        Assert.That(Row(0).Find("State").GetComponent<TextMeshProUGUI>().color, Is.EqualTo(CountryGovernmentTheme.Positive));
        Row(0).GetComponent<Button>().onClick.Invoke();
        Assert.That(_view.SelectedSoldier, Is.Zero);
        _member = false; _view.Refresh();
        Assert.That(Row(0).GetSiblingIndex(), Is.EqualTo(_view.Data.Soldiers.Length - 1));
        _unavailable = false; _view.Refresh();
        Assert.That(Row(0).GetSiblingIndex(), Is.Zero);
        Assert.That(Opacity(0), Is.EqualTo(1));
    }

    [Test]
    public void SelectedMembersComeFirstAndRemovingThemRestoresTheOriginalOrder()
    {
        var members = new HashSet<int> { 25, 7 };
        _view.SetMemberSelection(members.Contains, _ => "", "選択中");
        _view.Refresh();
        Assert.That(Row(7).GetSiblingIndex(), Is.Zero);
        Assert.That(Row(25).GetSiblingIndex(), Is.EqualTo(1));
        Assert.That(Row(0).GetSiblingIndex(), Is.EqualTo(2));
        _view.SelectSoldier(1);
        Assert.That(State(7), Is.EqualTo("選択中"));
        Assert.That(Row(7).Find("State").GetComponent<TextMeshProUGUI>().color, Is.EqualTo(CountryGovernmentTheme.Positive));
        members.Remove(7); _view.Refresh();
        Assert.That(Row(25).GetSiblingIndex(), Is.Zero);
        Assert.That(State(7), Is.Not.EqualTo("選択中"));
        members.Clear(); _view.Refresh();
        Assert.That(Row(0).GetSiblingIndex(), Is.Zero);
        Assert.That(Row(7).GetSiblingIndex(), Is.EqualTo(7));
        Assert.That(Row(25).GetSiblingIndex(), Is.EqualTo(25));
    }

    [Test]
    public void WeaponListRestoresOriginalOrderAndVisibility()
    {
        _unavailable = true; _view.Refresh(); _view.ShowWeapons();
        Assert.That(Row(0).GetSiblingIndex(), Is.Zero);
        Assert.That(Opacity(0), Is.EqualTo(1));
        _view.ShowSoldiers(0);
        Assert.That(Row(0).GetSiblingIndex(), Is.EqualTo(_view.Data.Soldiers.Length - 1));
    }
}

public sealed class CountryTrainingSelectionTests
{
    private GameObject _root;
    private CountryTrainingView _training;
    private CountryRosterView _view;
    private ScrollRect _list;
    private Button _action;
    private Button[] _schools;

    [SetUp]
    public void SetUp()
    {
        _root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Country/UI/CountryTraining.prefab");
        _training = _root.GetComponent<CountryTrainingView>();
        var properties = new SerializedObject(_training);
        _view = (CountryRosterView)properties.FindProperty("_view").objectReferenceValue;
        var confirm = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryRosterConfirm.prefab"), _root.transform).GetComponent<CountryRosterConfirm>();
        properties.FindProperty("_confirmation").objectReferenceValue = confirm;
        properties.ApplyModifiedPropertiesWithoutUndo();
        var schools = properties.FindProperty("_schools");
        _schools = new Button[schools.arraySize];
        for (int i = 0; i < schools.arraySize; i++) _schools[i] = (Button)schools.GetArrayElementAtIndex(i).objectReferenceValue;
        var viewProperties = new SerializedObject(_view);
        _list = (ScrollRect)viewProperties.FindProperty("_list").objectReferenceValue;
        _action = (Button)viewProperties.FindProperty("_action").objectReferenceValue;
        _training.Initialize(new CountryRoster(_view.Data, new CountryDate(0, 1, 1)));
    }

    [TearDown] public void TearDown() => PrefabUtility.UnloadPrefabContents(_root);
    private Transform Row(int id) => _list.content.Find($"Row{id + 1:00}");
    private string State(int id) => Row(id).Find("State").GetComponent<TextMeshProUGUI>().text;
    private float Opacity(int id)
    {
        var group = Row(id).GetComponent<CanvasGroup>();
        return group != null ? group.alpha : 1;
    }
    private void Toggle(int id) { _view.SelectSoldier(id); _action.onClick.Invoke(); }

    [Test]
    public void DraftMembershipSurvivesFocusAndSchoolChangesAndCanBeRemoved()
    {
        Toggle(0); _view.SelectSoldier(1);
        Assert.That(State(0), Is.EqualTo("選択中"));
        _schools[1].onClick.Invoke();
        Assert.That(State(0), Is.EqualTo("別の学校で選択中"));
        Assert.That(Opacity(0), Is.EqualTo(1));
        _schools[0].onClick.Invoke();
        Assert.That(State(0), Is.EqualTo("選択中"));
        Toggle(0);
        Assert.That(State(0), Is.Not.EqualTo("選択中"));
    }

    [Test]
    public void FullSchoolMovesUnselectedCandidatesDownWithoutBlockingRemoval()
    {
        var roster = new CountryRoster(_view.Data, new CountryDate(0, 1, 1));
        int capacity = roster.Capacity(0);
        for (int i = 0; i < capacity; i++) Toggle(i);
        Assert.That(State(capacity), Is.EqualTo("定員いっぱい"));
        Assert.That(Opacity(capacity), Is.EqualTo(1));
        Assert.That(State(0), Is.EqualTo("選択中"));
        Toggle(0);
        Assert.That(State(capacity), Is.Not.EqualTo("定員いっぱい"));
        Assert.That(Opacity(capacity), Is.EqualTo(1));
    }
}
