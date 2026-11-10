using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CountryButtonFeedbackTests
{
    private GameObject _root;
    private Button _button;
    private CountryButtonFeedback _feedback;

    [SetUp]
    public void SetUp()
    {
        _root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Country/UI/CountryRosterView.prefab");
        var view = _root.GetComponent<CountryRosterView>();
        _button = (Button)new SerializedObject(view).FindProperty("_action").objectReferenceValue;
        _feedback = _button.GetComponent<CountryButtonFeedback>();
        Invoke("Awake"); Invoke("OnEnable");
    }

    [TearDown] public void TearDown() => PrefabUtility.UnloadPrefabContents(_root);
    private void Invoke(string name) => typeof(CountryButtonFeedback).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_feedback, null);

    [Test]
    public void DisablingAndReenablingRestoresTheButtonContentColors()
    {
        _button.interactable = true; Invoke("UpdateDisabledAppearance");
        var graphics = (Graphic[])typeof(CountryButtonFeedback)
            .GetField("_graphics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_feedback);
        var original = graphics.Select(graphic => graphic.color).ToArray();

        _button.interactable = false; Invoke("UpdateDisabledAppearance");
        Assert.That(graphics.Where((graphic, i) => graphic.color != original[i]), Is.Not.Empty);
        _button.interactable = true; Invoke("UpdateDisabledAppearance");
        Assert.That(graphics.Select(graphic => graphic.color), Is.EqualTo(original));
        _button.interactable = false; Invoke("UpdateDisabledAppearance"); Invoke("OnDisable");
        Assert.That(graphics.Select(graphic => graphic.color), Is.EqualTo(original));
    }
}
