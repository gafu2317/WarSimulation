using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CountryBrightnessTests
{
    private GameObject _root;

    [SetUp] public void SetUp() { }
    [TearDown] public void TearDown() { if (_root != null) PrefabUtility.UnloadPrefabContents(_root); }

    [Test]
    public void WorkboardLightDoesNotReceiveInput()
    {
        _root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Country/UI/CountryWorkboard.prefab");
        var light = _root.transform.Find("Light").GetComponent<CountryGovernmentPlate>();
        Assert.That(light.raycastTarget, Is.False);
    }

}
