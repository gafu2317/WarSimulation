using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CountryTimeControlsTests
{
    private GameObject _hud, _clock;
    private Scene _scene;
    private DateManager _date;
    private CountryTimeControls _controls;
    private Button _button;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _clock = new GameObject("Time control test");
        SceneManager.MoveGameObjectToScene(_clock, _scene);
        _date = _clock.AddComponent<DateManager>();
        _hud = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Country/UI/CountryHud.prefab");
        _controls = _hud.GetComponent<CountryTimeControls>();
        _button = Field<Button>("_modeButton");
        var feedback = _button.GetComponent<CountryButtonFeedback>();
        typeof(CountryButtonFeedback).GetMethod("Awake", Private).Invoke(feedback, null);
        typeof(CountryButtonFeedback).GetMethod("OnEnable", Private).Invoke(feedback, null);
        Invoke("Awake");
        typeof(CountryTimeControls).GetField("_date", Private).SetValue(_controls, _date);
        Invoke("Subscribe");
    }

    [TearDown]
    public void TearDown()
    {
        PrefabUtility.UnloadPrefabContents(_hud);
        Object.DestroyImmediate(_clock);
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    [Test]
    public void ClickingCyclesThroughStopNormalDoubleAndStop()
    {
        _button.onClick.Invoke(); AssertIcon(1);
        _date.AdvanceTime(1); Assert.That(_date.CurrentDay, Is.EqualTo(2));
        _button.onClick.Invoke(); AssertIcon(2);
        _date.AdvanceTime(1); Assert.That(_date.CurrentDay, Is.EqualTo(4));
        _button.onClick.Invoke(); AssertIcon(0);
        _date.AdvanceTime(10); Assert.That(_date.CurrentDay, Is.EqualTo(4));
        _button.onClick.Invoke(); AssertIcon(1);
    }

    [Test]
    public void ExternalPauseImmediatelyShowsTheStopMark()
    {
        _date.ResumeDoubleSpeed(); AssertIcon(2);
        _date.Pause(); AssertIcon(0);
        _button.onClick.Invoke(); AssertIcon(1);
    }

    [Test]
    public void MonthChangeUpdatesTheDateAndReturnsToStop()
    {
        var state = new SerializedObject(_date);
        state.FindProperty("_currentDay").intValue = 31;
        state.ApplyModifiedPropertiesWithoutUndo();
        _date.OnMonthChanged += _ => _date.Pause();
        _date.ResumeDoubleSpeed(); _date.AdvanceTime(10);
        AssertIcon(0);
        Assert.That(Field<TextMeshProUGUI>("_dateText").text, Is.EqualTo("2月1日"));
    }

    [Test]
    public void DisablingAndReenablingDoesNotDuplicateTheModeChanges()
    {
        _button.onClick.Invoke(); AssertIcon(1);
        Invoke("OnDisable"); AssertIcon(0);
        Invoke("OnEnable");
        _button.onClick.Invoke(); AssertIcon(1);
        _button.onClick.Invoke(); AssertIcon(2);
    }

    private T Field<T>(string name) => (T)typeof(CountryTimeControls).GetField(name, Private).GetValue(_controls);
    private void Invoke(string name) => typeof(CountryTimeControls).GetMethod(name, Private).Invoke(_controls, null);

    private void AssertIcon(int speed)
    {
        Assert.That(_date.SpeedMultiplier, Is.EqualTo(speed));
        var names = new[] { "_stopIcon", "_playIcon", "_doubleSpeedIcon" };
        for (int i = 0; i < names.Length; i++)
        {
            var icon = Field<GameObject>(names[i]);
            Assert.That(icon.activeSelf, Is.EqualTo(speed == i));
        }
    }
}
