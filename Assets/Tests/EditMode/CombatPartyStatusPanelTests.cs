using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public sealed class CombatPartyStatusPanelTests
{
    [Test]
    public void ForceSyncNow_MatchesCurrentTeamCounts()
    {
        var systemObject = new GameObject("CharacterSystem");
        var system = systemObject.AddComponent<CombatCharacterSystem>();
        system.AllyCharacters.Add(CreateCharacter("AllyA", CombatTeam.Ally));
        system.AllyCharacters.Add(CreateCharacter("AllyB", CombatTeam.Ally));
        system.EnemyCharacters.Add(CreateCharacter("EnemyA", CombatTeam.Enemy));
        system.EnemyCharacters.Add(CreateCharacter("EnemyB", CombatTeam.Enemy));
        system.EnemyCharacters.Add(CreateCharacter("EnemyC", CombatTeam.Enemy));

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CombatPartyStatusPanel));
        CreateColumnWithTemplate(panelObject.transform, "AlliesColumn");
        CreateColumnWithTemplate(panelObject.transform, "EnemiesColumn");
        var panel = panelObject.GetComponent<CombatPartyStatusPanel>();
        panel.Initialize(system);
        panel.ForceSyncNow();

        Assert.That(panel.AllyViewCount, Is.EqualTo(2));
        Assert.That(panel.EnemyViewCount, Is.EqualTo(3));

        Object.DestroyImmediate(panelObject);
        DestroyCharacters(system);
        Object.DestroyImmediate(systemObject);
    }

    [Test]
    public void ForceSyncNow_TracksListChanges()
    {
        var systemObject = new GameObject("CharacterSystem");
        var system = systemObject.AddComponent<CombatCharacterSystem>();
        Character ally = CreateCharacter("Ally", CombatTeam.Ally);
        Character enemy = CreateCharacter("Enemy", CombatTeam.Enemy);
        system.AllyCharacters.Add(ally);
        system.EnemyCharacters.Add(enemy);

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CombatPartyStatusPanel));
        CreateColumnWithTemplate(panelObject.transform, "AlliesColumn");
        CreateColumnWithTemplate(panelObject.transform, "EnemiesColumn");
        var panel = panelObject.GetComponent<CombatPartyStatusPanel>();
        panel.Initialize(system);
        panel.ForceSyncNow();

        Character newEnemy = CreateCharacter("Enemy2", CombatTeam.Enemy);
        system.EnemyCharacters.Add(newEnemy);
        panel.ForceSyncNow();

        Assert.That(panel.EnemyViewCount, Is.EqualTo(2));

        system.EnemyCharacters.RemoveAt(0);
        panel.ForceSyncNow();

        Assert.That(panel.EnemyViewCount, Is.EqualTo(1));
        Assert.That(panel.FindView(newEnemy), Is.Not.Null);

        Object.DestroyImmediate(panelObject);
        DestroyCharacters(system);
        Object.DestroyImmediate(systemObject);
    }

    [Test]
    public void CombatPartyMemberView_UpdatesHpAndKeepsZeroHpVisible()
    {
        Character character = CreateCharacter("Target", CombatTeam.Ally);
        CombatHealth health = character.Health;
        health.Initialize(12, 12);

        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();
        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);

        health.TakeDamage(12);

        Assert.That(view.CurrentHpRatio, Is.EqualTo(0f).Within(0.001f));
        Assert.That(view.BoundCharacter, Is.EqualTo(character));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
    }

    [Test]
    public void CombatPartyMemberView_ShowsCharacterDisplayName()
    {
        Character character = CreateCharacter("TargetName", CombatTeam.Ally);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();

        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);

        Assert.That(view.CurrentNameText, Is.EqualTo("TargetName"));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
    }

    [Test]
    public void CombatPartyMemberView_ShowsBuffDebuffText()
    {
        Character character = CreateCharacter("Target", CombatTeam.Ally);
        character.StatusEffects.Apply(CombatStatusEffects.StatKind.STR, 1.25f, 5f);
        character.StatusEffects.ApplyPoison(2, 5f, 1f);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();

        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);
        view.Tick(0f);

        Assert.That(view.CurrentBuffDebuffText, Does.Contain("STRバフ"));
        Assert.That(view.CurrentBuffDebuffText, Does.Contain("毒"));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
    }

    [Test]
    public void CombatCharacterAppearanceView_BuildsImagesFromDirectionSprites()
    {
        Character character = CreateCharacter("Visual", CombatTeam.Ally, withAppearance: false);
        AttachAppearance(character.transform, "CharacterFrontLeft", "Body", CreateTestSprite(Color.red), new Vector3(0f, 1f, 0f), 0);
        AttachAppearance(character.transform, "CharacterFrontLeft", "Weapon", CreateTestSprite(Color.blue), new Vector3(0.5f, 0.5f, 0f), 2);

        var viewObject = new GameObject("AppearanceView", typeof(RectTransform), typeof(CombatCharacterAppearanceView));
        var view = viewObject.GetComponent<CombatCharacterAppearanceView>();
        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);

        Assert.That(view.PartCount, Is.EqualTo(2));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
    }

    [Test]
    public void SkillUsed_ShowsAndExpiresOnMatchingView()
    {
        var systemObject = new GameObject("CharacterSystem");
        var system = systemObject.AddComponent<CombatCharacterSystem>();
        Character ally = CreateCharacter("Ally", CombatTeam.Ally);
        system.AllyCharacters.Add(ally);

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CombatPartyStatusPanel));
        CreateColumnWithTemplate(panelObject.transform, "AlliesColumn");
        CreateColumnWithTemplate(panelObject.transform, "EnemiesColumn");
        var panel = panelObject.GetComponent<CombatPartyStatusPanel>();
        panel.Initialize(system);
        panel.ForceSyncNow();

        CombatSkillUseEvents.RaiseSkillUsed(ally, "Slash");
        CombatPartyMemberView view = panel.FindView(ally);

        Assert.That(view, Is.Not.Null);
        Assert.That(view.CurrentSkillText, Is.EqualTo("Slash"));

        view.ShowSkill("Slash", 0f);
        panel.TickNow(3f);
        Assert.That(view.CurrentSkillText, Is.Empty);

        Object.DestroyImmediate(panelObject);
        DestroyCharacters(system);
        Object.DestroyImmediate(systemObject);
    }

    private static Character CreateCharacter(string name, CombatTeam team, bool withAppearance = true)
    {
        var characterObject = new GameObject(name);
        characterObject.AddComponent<NavMeshAgent>();
        Character character = characterObject.AddComponent<Character>();
        character.SetTeam(team);
        character.Health.Initialize(10, 10);

        if (withAppearance)
        {
            AttachAppearance(character.transform, "CharacterFrontLeft", "Body", CreateTestSprite(Color.white), new Vector3(0f, 1f, 0f), 0);
            AttachAppearance(character.transform, "CharacterFrontRight", "Body", CreateTestSprite(Color.white), new Vector3(0f, 1f, 0f), 0);
        }

        return character;
    }

    private static void AttachAppearance(
        Transform characterRoot,
        string directionRootName,
        string partName,
        Sprite sprite,
        Vector3 localPosition,
        int sortingOrder)
    {
        Transform spriteRoot = FindOrCreateChild(characterRoot, "SpriteRoot");
        Transform directionRoot = FindOrCreateChild(spriteRoot, directionRootName);
        directionRoot.gameObject.SetActive(false);

        var part = new GameObject(partName, typeof(SpriteRenderer));
        part.transform.SetParent(directionRoot, false);
        part.transform.localPosition = localPosition;

        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child;
        }

        var childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Sprite CreateTestSprite(Color color)
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 8f);
    }

    private static void DestroyCharacters(CombatCharacterSystem system)
    {
        for (int i = 0; i < system.AllyCharacters.Count; i++)
        {
            if (system.AllyCharacters[i] != null)
            {
                Object.DestroyImmediate(system.AllyCharacters[i].gameObject);
            }
        }

        for (int i = 0; i < system.EnemyCharacters.Count; i++)
        {
            if (system.EnemyCharacters[i] != null)
            {
                Object.DestroyImmediate(system.EnemyCharacters[i].gameObject);
            }
        }
    }

    private static void CreateColumnWithTemplate(Transform parent, string name)
    {
        var column = new GameObject(name, typeof(RectTransform));
        column.transform.SetParent(parent, false);
        CreateMemberViewObject($"{name}_Template").transform.SetParent(column.transform, false);
    }

    private static GameObject CreateMemberViewObject(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CombatPartyMemberView));
        CreateChild(root.transform, "Background", typeof(RectTransform), typeof(Image));

        var appearance = CreateChild(root.transform, "Appearance", typeof(RectTransform), typeof(CombatCharacterAppearanceView));
        CreateChild(appearance.transform, "Content", typeof(RectTransform));

        CreateTmpText(root.transform, "NameText");
        CreateTmpText(root.transform, "ObjectiveText");
        CreateTmpText(root.transform, "BuffDebuffText");
        CreateTmpText(root.transform, "WeaponText");
        CreateTmpText(root.transform, "HpText");
        GameObject skillText = CreateTmpText(root.transform, "SkillText");
        skillText.SetActive(false);

        var hpBarBackground = CreateChild(root.transform, "HpBarBackground", typeof(RectTransform), typeof(Image));
        GameObject hpBarFill = CreateChild(hpBarBackground.transform, "HpBarFill", typeof(RectTransform), typeof(Image));
        Image hpFillImage = hpBarFill.GetComponent<Image>();
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        return root;
    }

    private static GameObject CreateTmpText(Transform parent, string name)
    {
        GameObject child = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return child;
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        var child = new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }
}
