using NUnit.Framework;
using UnityEngine;

public sealed class CountryFacilityLayoutTests
{
    private readonly Rect _area = new(0, 0, 600, 400);

    [Test]
    public void NearbyBanners_KeepSeparateClickAreas()
    {
        var first = new Rect(100, 100, 180, 112);
        bool shown = CountryFacilityLayer.TryPlace(_area, new Rect(150, 120, 180, 112),
            new Rect[0], new[] { first }, 1, out Rect second);
        Assert.That(shown, Is.True);
        Assert.That(second.Overlaps(first), Is.False);
        Assert.That(second.x, Is.EqualTo(150));
        Assert.That(second.y, Is.GreaterThan(first.yMax));
    }

    [Test]
    public void BannerBehindFixedHud_IsHidden()
    {
        bool shown = CountryFacilityLayer.TryPlace(_area, new Rect(10, 150, 150, 112),
            new[] { new Rect(0, 100, 200, 300) }, new Rect[0], 0, out _);
        Assert.That(shown, Is.False);
    }

    [Test]
    public void OffscreenBanner_IsHiddenInsteadOfPinnedToTheEdge()
    {
        bool shown = CountryFacilityLayer.TryPlace(_area, new Rect(-30, 100, 150, 112),
            new Rect[0], new Rect[0], 0, out _);
        Assert.That(shown, Is.False);
    }

    [Test]
    public void CrowdedBanners_DoNotOverflowTheScreen()
    {
        var first = new Rect(100, 260, 180, 112);
        bool shown = CountryFacilityLayer.TryPlace(_area, first, new Rect[0], new[] { first }, 1, out _);
        Assert.That(shown, Is.False);
    }
}
