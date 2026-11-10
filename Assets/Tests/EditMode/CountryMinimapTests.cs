using NUnit.Framework;
using UnityEngine;

public sealed class CountryMinimapTests
{
    private GameObject _root;
    private Camera _camera;
    private readonly Vector2[] _polygon = new Vector2[16];
    private readonly Vector2[] _scratch = new Vector2[16];
    private readonly Rect _bounds = new(-200, -200, 400, 400);

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Minimap camera", typeof(Camera));
        _camera = _root.GetComponent<Camera>();
        _camera.aspect = 16f / 9;
        _camera.fieldOfView = 40;
        _camera.transform.SetPositionAndRotation(new Vector3(0, 100, -100), Quaternion.Euler(45, 0, 0));
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    private int Project(Rect bounds) => CountryMinimapViewport.ProjectViewport(_camera, bounds, _polygon, _scratch);

    [Test]
    public void MovingEast_ShiftsTheVisibleAreaEast()
    {
        Assert.That(Project(_bounds), Is.EqualTo(4));
        Vector2 first = _polygon[0];
        _camera.transform.position += Vector3.right * 20;
        Assert.That(Project(_bounds), Is.EqualTo(4));
        Assert.That(_polygon[0].x - first.x, Is.EqualTo(.05f).Within(.0001f));
        Assert.That(_polygon[0].y, Is.EqualTo(first.y).Within(.0001f));
    }

    [Test]
    public void ZoomingIn_ReducesTheVisibleArea()
    {
        int count = Project(_bounds);
        float before = Area(count);
        _camera.transform.position *= .5f;
        Assert.That(Area(Project(_bounds)), Is.LessThan(before * .3f));
    }

    [Test]
    public void SwitchingCountries_PreservesRelativeMapPosition()
    {
        int count = Project(_bounds);
        var before = (Vector2[])_polygon.Clone();
        _camera.transform.position += Vector3.right * 308;
        Rect moved = _bounds;
        moved.x += 308;
        Assert.That(Project(moved), Is.EqualTo(count));
        for (int i = 0; i < count; i++)
            Assert.That(Vector2.Distance(_polygon[i], before[i]), Is.LessThan(.0001f));
    }

    [Test]
    public void ZoomingOut_ClipsTheFrameToTheMap()
    {
        int count = Project(new Rect(-10, -10, 20, 20));
        Assert.That(count, Is.EqualTo(4));
        Assert.That(Area(count), Is.EqualTo(1).Within(.0001f));
        for (int i = 0; i < count; i++)
        {
            Assert.That(_polygon[i].x, Is.InRange(-.0001f, 1.0001f));
            Assert.That(_polygon[i].y, Is.InRange(-.0001f, 1.0001f));
        }
    }

    [Test]
    public void LookingAwayFromTheCountry_HidesTheFrame()
    {
        _camera.transform.position += Vector3.right * 1000;
        Assert.That(Project(_bounds), Is.Zero);
        _camera.transform.rotation = Quaternion.Euler(-45, 0, 0);
        Assert.That(Project(_bounds), Is.Zero);
    }

    private float Area(int count)
    {
        float area = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = _polygon[i], b = _polygon[(i + 1) % count];
            area += a.x * b.y - b.x * a.y;
        }
        return Mathf.Abs(area) * .5f;
    }
}
