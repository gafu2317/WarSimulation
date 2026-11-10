using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class CountryInitialViewTests
{
    GameObject root;
    CountryComparisonView view;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Country initial view test");
        root.SetActive(false);
        view = root.AddComponent<CountryComparisonView>();
        view.viewCamera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        view.viewCamera.transform.SetParent(root.transform);
        view.centers = new[] { new Vector3(-154, 0, 0), new Vector3(154, 0, 0) };
        view.cities = new[] { new GameObject("Old"), new GameObject("New") };
        foreach (var city in view.cities) city.transform.SetParent(root.transform);
        view.Show(2);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.Destroy(root);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Rendering_UsesATemporaryPipelineAndRestoresTheOriginal()
    {
        var previous = QualitySettings.renderPipeline;
        var source = GraphicsSettings.currentRenderPipeline;
        var serialized = JsonUtility.ToJson(source);
        root.SetActive(true);
        yield return null;

        var temporary = QualitySettings.renderPipeline;
        Assert.That(temporary, Is.Not.SameAs(source));
        Assert.That(temporary.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
        Assert.That(JsonUtility.ToJson(source), Is.EqualTo(serialized));
        root.SetActive(false);
        yield return null;
        Assert.That(QualitySettings.renderPipeline, Is.SameAs(previous));
        Assert.That(temporary == null, Is.True);
    }

    [UnityTest]
    public IEnumerator AfterStartup_ComparisonSelectionAndCameraPosePersist()
    {
        root.SetActive(true);
        yield return null;

        view.Show(0);
        var position = view.viewCamera.transform.position + new Vector3(7, -8, 11);
        view.viewCamera.transform.position = position;
        yield return null;
        yield return null;

        Assert.That(view.viewIndex, Is.Zero);
        Assert.That(view.cities[0].activeSelf, Is.True);
        Assert.That(view.cities[1].activeSelf, Is.False);
        Assert.That(Vector3.Distance(view.viewCamera.transform.position, position), Is.LessThan(.001f));
    }
}
