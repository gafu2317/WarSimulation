using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatStatusIconTests
{
    [TestCase(-1)]
    [TestCase(16)]
    public void Catalog_ReturnsNullForOutOfRangeKind(int kind)
    {
        Assert.That(CombatStatusEffectIconCatalog.Default.GetSprite((CombatStatusIconKind)kind), Is.Null);
    }

    [Test]
    public void DebugCard_ShowsStatusIconsBelowHpAndKeepsRowWhenEffectsClear()
    {
        var hudObject = new GameObject("Debug HUD", typeof(RectTransform));
        var characterObject = new GameObject("Debug character");
        try
        {
            var hud = hudObject.AddComponent<CombatBattleHudView>();
            Character character = characterObject.AddComponent<Character>();
            character.StatusEffects.ApplyPoison(1, 5f, 1f);
            character.StatusEffects.ApplyBind(5f);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            object team = typeof(CombatBattleHudView).GetField("_debugAllies", flags).GetValue(hud);
            var refresh = typeof(CombatBattleHudView).GetMethod("RefreshDebugCharacters", flags);
            object[] arguments = { team, new[] { character }, true };
            refresh.Invoke(hud, arguments);
            var cards = (System.Collections.IList)team.GetType().GetField("Characters").GetValue(team);
            object card = cards[0];
            var root = (GameObject)card.GetType().GetField("Root").GetValue(card);
            Transform row = root.transform.Find("StatusEffectsRow");
            var layout = root.GetComponent<VerticalLayoutGroup>();
            Assert.That(root.GetComponent<LayoutElement>(), Is.Null);
            Assert.That(layout.padding.top, Is.EqualTo(layout.padding.bottom));
            Assert.That(row.GetSiblingIndex(), Is.EqualTo(root.transform.Find("HpBar").GetSiblingIndex() + 1));
            var names = new List<string>();
            foreach (Image icon in row.GetComponentsInChildren<Image>(true))
            {
                Assert.That(icon.enabled, Is.True);
                names.Add(icon.sprite.name);
            }
            CollectionAssert.AreEquivalent(new[] { "Poison", "Bind" }, names);

            var weaponText = root.transform.Find("WeaponHpRow/Weapon").GetComponent<TMPro.TMP_Text>();
            character.StatusEffects.Apply(CombatStatusEffects.StatKind.STR, 1.2f, 5f);
            refresh.Invoke(hud, arguments);
            Assert.That(weaponText.color, Is.EqualTo(Color.white));
            character.StatusEffects.Apply(CombatStatusEffects.StatKind.INT, 0.8f, 5f);
            refresh.Invoke(hud, arguments);
            Assert.That(weaponText.color, Is.EqualTo(Color.white));
            character.StatusEffects.ClearAll();
            character.StatusEffects.Apply(CombatStatusEffects.StatKind.INT, 0.8f, 5f);
            refresh.Invoke(hud, arguments);
            Assert.That(weaponText.color, Is.EqualTo(Color.white));
            character.StatusEffects.ClearAll();
            refresh.Invoke(hud, arguments);
            Assert.That(row.gameObject.activeSelf, Is.True);
            Assert.That(row.Find("StatusEffects").gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(hudObject);
            Object.DestroyImmediate(characterObject);
        }
    }

    [Test]
    public void Source_TracksSpecialEffectsAndRemovesCancelledAndExpiredEffects()
    {
        var carrierObject = new GameObject("Carrier");
        var passengerObject = new GameObject("Passenger");
        try
        {
            Character carrier = carrierObject.AddComponent<Character>();
            Character passenger = passengerObject.AddComponent<Character>();
            carrier.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            passenger.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var reflect = passengerObject.AddComponent<BibleGotsumeEffect>();
            reflect.Initialize(passenger, 1, 5f, default);
            var guard = passengerObject.AddComponent<ShieldShoulderGuardEffect>();
            guard.Initialize(carrier, passenger, 0.6f, 5f);
            var carry = carrierObject.AddComponent<BibleCarryRushEffect>();
            carry.Initialize(carrier, passenger, 1.8f, 5f);
            var icons = new List<CombatStatusIconKind>();
            CombatStatusIconSource.Collect(passenger, icons);
            CollectionAssert.AreEquivalent(new[] { CombatStatusIconKind.Reflection,
                CombatStatusIconKind.ShoulderGuard, CombatStatusIconKind.CarryRush }, icons);
            CombatStatusIconSource.Collect(carrier, icons);
            CollectionAssert.AreEqual(new[] { CombatStatusIconKind.CarryRush }, icons);
            carry.CancelImmediate();
            guard.CancelImmediate();
            reflect.Initialize(passenger, 1, 0f, default);
            CombatStatusIconSource.Collect(passenger, icons);
            Assert.That(icons, Is.Empty);
            CombatStatusIconSource.Collect(carrier, icons);
            Assert.That(icons, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(passengerObject);
            Object.DestroyImmediate(carrierObject);
        }
    }

    [Test]
    public void Source_ExcludesHealingAndNeutralEffectsAndDeduplicatesSameKind()
    {
        var go = new GameObject("Status test");
        try
        {
            Character character = go.AddComponent<Character>();
            character.StatusEffects.ApplyHealOverTime(1, 5f, 1f);
            character.StatusEffects.Apply(CombatStatusEffects.StatKind.INT, 1f, 5f);
            character.StatusEffects.Apply(CombatStatusEffects.StatKind.STR, 1.2f, 5f, "A");
            character.StatusEffects.Apply(CombatStatusEffects.StatKind.STR, 1.3f, 5f, "B");
            var icons = new List<CombatStatusIconKind>();
            CombatStatusIconSource.Collect(character, icons);
            CollectionAssert.AreEqual(new[] { CombatStatusIconKind.STRBuff }, icons);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Row_OverlapsOnlyWhenNeededAndKeepsAllIconsWithinWidth()
    {
        const float size = 28.6652f;
        const float width = 168.3551f;
        Assert.That(CombatStatusIconRow.GetStep(width, size, 3f, 3), Is.EqualTo(size + 3f));
        float step = CombatStatusIconRow.GetStep(width, size, 3f, 16);
        Assert.That(step, Is.LessThan(size));
        Assert.That(size + step * 15, Is.EqualTo(width).Within(0.001f));
        Assert.That(CombatStatusIconRow.GetStep(width, size, 3f, 0), Is.Zero);
        Assert.That(CombatStatusIconRow.GetStep(width, size, 3f, 1), Is.Zero);
    }

    [Test]
    public void Catalog_HasDistinctSpritesForEveryDisplayedKind()
    {
        var catalog = CombatStatusEffectIconCatalog.Default;
        Assert.That(catalog, Is.Not.Null);
        var rectangles = new HashSet<Rect>();
        foreach (CombatStatusIconKind kind in System.Enum.GetValues(typeof(CombatStatusIconKind)))
        {
            Sprite sprite = catalog.GetSprite(kind);
            Assert.That(sprite != null, Is.True, kind.ToString());
            Assert.That(UnityEditor.AssetDatabase.Contains(sprite), Is.True, kind.ToString());
            Assert.That(sprite.name, Is.EqualTo(kind.ToString()));
            Assert.That(catalog.GetSprite(kind), Is.SameAs(sprite));
            Assert.That(rectangles.Add(sprite.rect), Is.True, kind.ToString());
            Assert.That(sprite.rect.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(sprite.rect.xMax, Is.LessThanOrEqualTo(sprite.texture.width));
            Assert.That(sprite.rect.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(sprite.rect.yMax, Is.LessThanOrEqualTo(sprite.texture.height));
        }
    }

    [Test]
    public void WorldBar_ShowsSameEffectsAsCardSourceAndHidesAtDeath()
    {
        var go = new GameObject("World status test");
        try
        {
            Character character = go.AddComponent<Character>();
            character.Health.Initialize(10);
            character.StatusEffects.ApplyPoison(1, 5f, 1f);
            character.StatusEffects.ApplyBind(5f);
            var bar = go.AddComponent<CombatWorldHealthBar>();
            bar.Configure(character.Health);
            Transform root = go.transform.Find("HealthBar/StatusEffects");
            Assert.That(root, Is.Not.Null);
            var names = new List<string>();
            foreach (Image icon in root.GetComponentsInChildren<Image>()) names.Add(icon.sprite.name);
            CollectionAssert.AreEquivalent(new[] { "Bind", "Poison" }, names);
            character.Health.TakeDamage(10);
            Assert.That(root.gameObject.activeInHierarchy, Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
