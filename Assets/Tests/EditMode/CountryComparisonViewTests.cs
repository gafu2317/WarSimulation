using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CountryComparisonViewTests
{
    GameObject root;
    CountryComparisonView view;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Country comparison test");
        root.SetActive(false);
        view = root.AddComponent<CountryComparisonView>();
        view.viewCamera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        view.viewCamera.transform.SetParent(root.transform);
        view.cities = new[] { new GameObject("Old"), new GameObject("New") };
        view.centers = new[] { new Vector3(-154, 0, 0), new Vector3(154, 0, 0) };
        foreach (var city in view.cities) city.transform.SetParent(root.transform);
        view.hiddenCountryGround = new[] { new GameObject("Old ground"), new GameObject("New ground") };
        foreach (var ground in view.hiddenCountryGround) ground.transform.SetParent(root.transform);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void EditingScene_DoesNotOverrideOrModifyTheSharedPipeline()
    {
        var previous = QualitySettings.renderPipeline;
        var source = GraphicsSettings.currentRenderPipeline;
        var serialized = JsonUtility.ToJson(source);

        root.SetActive(true);

        Assert.That(QualitySettings.renderPipeline, Is.SameAs(previous));
        Assert.That(JsonUtility.ToJson(source), Is.EqualTo(serialized));
        root.SetActive(false);
        Assert.That(QualitySettings.renderPipeline, Is.SameAs(previous));
    }

    [TestCase(0)]
    [TestCase(2)]
    public void InitialView_ShowsTheNewCastleFromOtherSelections(int previousSelection)
    {
        view.initialFocus = new Vector3(0, 6, 137);
        view.Show(previousSelection);

        view.ShowInitialView();

        Assert.That(view.viewIndex, Is.EqualTo(1));
        Assert.That(view.cities[1].activeSelf, Is.True);
        Assert.That(view.cities[0].activeSelf, Is.False);
        Assert.That(view.hiddenCountryGround[1].activeSelf, Is.False);
        Assert.That(view.hiddenCountryGround[0].activeSelf, Is.True);
        var camera = view.viewCamera;
        var focus = view.centers[1] + view.initialFocus;
        Assert.That(Vector3.Distance(camera.transform.position + camera.transform.forward * 130, focus), Is.LessThan(.001f));
        Assert.That(camera.fieldOfView, Is.EqualTo(40));
    }

    [Test]
    public void WithOnlyTheNewCountry_ItStaysVisibleWhenComparisonIsRequested()
    {
        Object.DestroyImmediate(view.cities[0]);
        view.ShowInitialView();
        var position = view.viewCamera.transform.position;

        view.Show(0);
        view.Show(2);
        view.Swap();

        Assert.That(view.viewIndex, Is.EqualTo(1));
        Assert.That(view.cities[1].activeSelf, Is.True);
        Assert.That(view.viewCamera.transform.position, Is.EqualTo(position));
    }

    [Test]
    public void PairView_ShowsBothCountriesAndHidesTheirBackdrop()
    {
        view.Show(2);
        Assert.That(view.cities[0].activeSelf && view.cities[1].activeSelf, Is.True);
        Assert.That(view.hiddenCountryGround[0].activeSelf || view.hiddenCountryGround[1].activeSelf, Is.False);
    }

    [Test]
    public void SingleViews_ShowOnlyTheSelectedCountry()
    {
        foreach (var selected in new[] { 0, 1 })
        {
            view.Show(selected);
            Assert.That(view.cities[selected].activeSelf, Is.True);
            Assert.That(view.cities[1-selected].activeSelf, Is.False);
            Assert.That(view.hiddenCountryGround[selected].activeSelf, Is.False);
            Assert.That(view.hiddenCountryGround[1-selected].activeSelf, Is.True);
        }
    }

    [Test]
    public void CyclingTwoCountries_PreservesRelativeCameraPoseAndZoom()
    {
        view.Show(0);
        var offset = new Vector3(17, 35, -42);
        var rotation = Quaternion.Euler(42, 12, 0);
        view.viewCamera.transform.SetPositionAndRotation(view.centers[0] + offset, rotation);
        view.viewCamera.fieldOfView = 31;
        foreach (var next in new[] { 1, 0, 1, 0 })
        {
            view.Swap();
            Assert.That(Vector3.Distance(view.viewCamera.transform.position, view.centers[next] + offset), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(view.viewCamera.transform.rotation, rotation), Is.LessThan(.001f));
            Assert.That(view.viewCamera.fieldOfView, Is.EqualTo(31));
            Assert.That(view.viewIndex, Is.EqualTo(next));
        }
    }

    [Test]
    public void SwappingFromPairView_SelectsTheNewCountry()
    {
        view.Show(2);
        view.Swap();
        Assert.That(view.viewIndex, Is.EqualTo(1));
        Assert.That(view.cities[1].activeSelf, Is.True);
        Assert.That(view.cities[0].activeSelf, Is.False);
    }

    [TestCase(3)]
    [TestCase(4)]
    public void RemovedViewIndices_FallBackToThePairView(int index)
    {
        view.Show(index);
        Assert.That(view.viewIndex, Is.EqualTo(2));
        Assert.That(view.cities[0].activeSelf && view.cities[1].activeSelf, Is.True);
    }
}
