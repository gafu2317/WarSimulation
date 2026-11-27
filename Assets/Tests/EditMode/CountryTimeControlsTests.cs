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
    private Button[] _buttons;
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
        _buttons = Field<Button[]>("_modeButtons");
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
    public void EachButtonSelectsItsOwnSpeedFromTheCurrentMode()
    {
        Click(2);
        _date.AdvanceTime(1); Assert.That(_date.CurrentDay, Is.EqualTo(3));
        Click(2);
        Click(0);
        _date.AdvanceTime(10); Assert.That(_date.CurrentDay, Is.EqualTo(3));
        Click(1);
        _date.AdvanceTime(1); Assert.That(_date.CurrentDay, Is.EqualTo(4));
    }

    [Test]
    public void ExternalPauseImmediatelySelectsStop()
    {
        _date.ResumeDoubleSpeed(); AssertMode(2);
        _date.Pause(); AssertMode(0);
        Click(1);
    }

    [Test]
    public void MonthChangeUpdatesTheDateAndReturnsToStop()
    {
        var state = new SerializedObject(_date);
        state.FindProperty("_currentDay").intValue = 31;
        state.ApplyModifiedPropertiesWithoutUndo();
        _date.OnMonthChanged += _ => _date.Pause();
        _date.ResumeDoubleSpeed(); _date.AdvanceTime(10);
        AssertMode(0);
        Assert.That(Field<TextMeshProUGUI>("_dateText").text, Is.EqualTo("2月1日"));
    }

    [Test]
    public void MonthEndFastForwardHasItsOwnStatusUntilRegularSpeedIsSelected()
    {
        _date.AdvanceToMonthEnd();
        var dateText = Field<TextMeshProUGUI>("_dateText");
        Assert.That(dateText.text, Does.Contain("早送り中"));
        foreach (var frame in Field<GameObject[]>("_selectionFrames"))
            Assert.That(frame.activeSelf, Is.False);
        Click(1);
        Assert.That(_date.IsAdvancingToMonthEnd, Is.False);
        Assert.That(dateText.text, Is.EqualTo("1月1日"));
    }

    [Test]
    public void DisablingAndReenablingDoesNotDuplicateTheModeChanges()
    {
        Click(1);
        Invoke("OnDisable"); AssertMode(0);
        Invoke("OnEnable");
        Click(1);
        Click(2);
    }

    private T Field<T>(string name) => (T)typeof(CountryTimeControls).GetField(name, Private).GetValue(_controls);
    private void Invoke(string name) => typeof(CountryTimeControls).GetMethod(name, Private).Invoke(_controls, null);

    private void Click(int speed) { _buttons[speed].onClick.Invoke(); AssertMode(speed); }

    private void AssertMode(int speed)
    {
        Assert.That(_date.SpeedMultiplier, Is.EqualTo(speed));
        var frames = Field<GameObject[]>("_selectionFrames");
        for (int i = 0; i < frames.Length; i++)
            Assert.That(frames[i].activeSelf, Is.EqualTo(speed == i));
    }
}
