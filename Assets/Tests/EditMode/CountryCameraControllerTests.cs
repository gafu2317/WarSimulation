using NUnit.Framework;
using UnityEngine;

public sealed class CountryCameraControllerTests
{
    private GameObject _root;
    private Camera _camera;
    private CountryCameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Country camera test", typeof(Camera));
        _camera = _root.GetComponent<Camera>();
        _camera.enabled = false;
        _camera.pixelRect = new Rect(0f, 0f, 844f, 390f);
        _root.transform.SetPositionAndRotation(new Vector3(0f, 100f, -100f), Quaternion.Euler(45f, 0f, 0f));
        _controller = _root.AddComponent<CountryCameraController>();
        _controller.SyncStateFromTransform();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void MovementKeepsHeightAndViewingAngle()
    {
        Vector3 before = _root.transform.position;
        Quaternion rotation = _root.transform.rotation;

        _controller.Move(new Vector2(1f, 1f), 1f);

        Assert.That(_root.transform.position.x, Is.GreaterThan(before.x));
        Assert.That(_root.transform.position.z, Is.GreaterThan(before.z));
        Assert.That(_root.transform.position.y, Is.EqualTo(before.y).Within(0.001f));
        Assert.That(Quaternion.Angle(_root.transform.rotation, rotation), Is.LessThan(0.001f));
    }

    [Test]
    public void MovementDoesNotDependOnFrameCount()
    {
        Vector3 start = _root.transform.position;
        _controller.Move(Vector2.up, 1f);
        Vector3 oneStep = _root.transform.position;
        _root.transform.position = start;
        _controller.SyncStateFromTransform();

        for (int i = 0; i < 60; i++) _controller.Move(Vector2.up, 1f / 60f);

        Assert.That(Vector3.Distance(_root.transform.position, oneStep), Is.LessThan(0.001f));
    }

    [Test]
    public void DiagonalMovementIsNotFaster()
    {
        Vector3 start = _root.transform.position;
        _controller.Move(Vector2.up, 1f);
        float straightDistance = Vector3.Distance(start, _root.transform.position);
        _root.transform.position = start;
        _controller.SyncStateFromTransform();

        _controller.Move(Vector2.one, 1f);

        Assert.That(Vector3.Distance(start, _root.transform.position),
            Is.EqualTo(straightDistance).Within(0.001f));
    }

    [Test]
    public void ZoomKeepsGroundFocusAndViewingAngle()
    {
        Vector3 before = _root.transform.position;
        Vector3 focus = GroundFocus();
        Quaternion rotation = _root.transform.rotation;

        _controller.Zoom(2f);

        Assert.That(Vector3.Distance(_root.transform.position, focus),
            Is.LessThan(Vector3.Distance(before, focus)));
        Assert.That(Vector3.Distance(GroundFocus(), focus), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(_root.transform.rotation, rotation), Is.LessThan(0.001f));
    }

    [Test]
    public void FractionalWheelInputAccumulatesWithoutDependingOnFrameCount()
    {
        Vector3 start = _root.transform.position;
        _controller.Zoom(1f);
        Vector3 oneTick = _root.transform.position;
        _root.transform.position = start;
        _controller.SyncStateFromTransform();
        for (int i = 0; i < 10; i++) _controller.Zoom(.1f);
        Assert.That(Vector3.Distance(_root.transform.position, oneTick), Is.LessThan(.001f));
    }

    [Test]
    public void ZoomStopsAtBothLimits()
    {
        Vector3 focus = GroundFocus();
        _controller.Zoom(10000f);
        Vector3 closest = _root.transform.position;
        _controller.Zoom(10000f);
        Assert.That(Vector3.Distance(_root.transform.position, closest), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(closest, focus), Is.EqualTo(35f).Within(0.001f));

        _controller.Zoom(-10000f);
        Vector3 furthest = _root.transform.position;
        _controller.Zoom(-10000f);
        Assert.That(Vector3.Distance(_root.transform.position, furthest), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(furthest, focus), Is.EqualTo(600f).Within(0.001f));
    }

    [Test]
    public void DragKeepsTheGrabbedGroundPointUnderThePointer()
    {
        Vector2 from = new(422f, 195f);
        Vector2 to = new(550f, 245f);
        Ray ray = _camera.ScreenPointToRay(from);
        new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance);
        Vector3 grabbed = ray.GetPoint(distance);
        float height = _root.transform.position.y;

        _controller.Pan(from, to);

        Vector3 projected = _camera.WorldToScreenPoint(grabbed);
        Assert.That(Vector2.Distance(new Vector2(projected.x, projected.y), to), Is.LessThan(0.01f));
        Assert.That(_root.transform.position.y, Is.EqualTo(height).Within(0.001f));
    }

    [Test]
    public void SpreadingFingersZoomsInAroundTheGestureCenter()
    {
        Vector2 center = new(560f, 220f);
        Ray ray = _camera.ScreenPointToRay(center);
        new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance);
        Vector3 grabbed = ray.GetPoint(distance);
        float height = _root.transform.position.y;
        Quaternion rotation = _root.transform.rotation;

        _controller.Pinch(center, 100f, 150f);

        Vector3 projected = _camera.WorldToScreenPoint(grabbed);
        Assert.That(Vector2.Distance(new Vector2(projected.x, projected.y), center), Is.LessThan(0.01f));
        Assert.That(_root.transform.position.y, Is.LessThan(height));
        Assert.That(Quaternion.Angle(_root.transform.rotation, rotation), Is.LessThan(0.001f));
    }

    [Test]
    public void ClosingFingersZoomsOut()
    {
        float height = _root.transform.position.y;

        _controller.Pinch(new Vector2(422f, 195f), 150f, 100f);

        Assert.That(_root.transform.position.y, Is.GreaterThan(height));
    }

    [Test]
    public void PinchStopsAtBothZoomLimits()
    {
        Vector2 center = new(422f, 195f);
        _controller.Pinch(center, 1f, 10000f);
        Assert.That(Vector3.Distance(_root.transform.position, GroundFocus()), Is.EqualTo(35f).Within(0.001f));

        _controller.Pinch(center, 10000f, 1f);

        Assert.That(Vector3.Distance(_root.transform.position, GroundFocus()), Is.EqualTo(600f).Within(0.001f));
    }

    [Test]
    public void SyncKeepsTheNewCityPositionWhenZooming()
    {
        _controller.Move(Vector2.right, 0.5f);
        _controller.Zoom(1f);
        Vector3 oldFocus = GroundFocus();
        Vector3 offset = new(308f, 0f, 0f);
        _root.transform.position += offset;
        _controller.SyncStateFromTransform();

        _controller.Zoom(1f);

        Assert.That(Vector3.Distance(GroundFocus(), oldFocus + offset), Is.LessThan(0.001f));
    }

    private Vector3 GroundFocus()
    {
        Ray ray = new(_root.transform.position, _root.transform.forward);
        new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance);
        return ray.GetPoint(distance);
    }
}
