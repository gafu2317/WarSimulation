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
    public void CombatPartyMemberView_RefreshesPersonalityAfterBind()
    {
        Character character = CreateCharacter("Target", CombatTeam.Ally);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();
        var profile = ScriptableObject.CreateInstance<CombatAiPersonalityProfile>();
        CombatEditModeTestUtil.SetPrivateField(profile, "_displayNameJapanese", "慎重");

        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);
        CombatEditModeTestUtil.SetPrivateField(character, "_personalityProfile", profile);
        view.Tick(0f);

        Assert.That(view.CurrentPersonalityText, Is.EqualTo("慎重"));

        Object.DestroyImmediate(profile);
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
    public void CombatPartyMemberView_ShowsFirstThreeStatusEffectsAsColoredBlankIcons()
    {
        Character character = CreateCharacter("Target", CombatTeam.Ally);
        character.StatusEffects.Apply(CombatStatusEffects.StatKind.STR, 1.25f, 5f);
        character.StatusEffects.ApplyPoison(2, 5f, 1f);
        character.StatusEffects.ApplyInvulnerable(5f);
        character.StatusEffects.ApplyRoot(5f);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();

        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);

        Transform icons = viewObject.transform.Find("BuffDebuffRoot");
        Assert.That(view.ActiveStatusIconCount, Is.EqualTo(3));
        Assert.That(icons.GetChild(0).GetComponent<Image>().sprite, Is.Null);
        Assert.That(icons.GetChild(0).GetComponent<Image>().color, Is.EqualTo(Color.cyan));
        Assert.That(icons.GetChild(1).GetComponent<Image>().color, Is.EqualTo(Color.red));
        Assert.That(icons.GetChild(2).GetComponent<Image>().color, Is.EqualTo(Color.cyan));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
    }

    [Test]
    public void CombatPartyMemberView_SelectsNestedWeaponIcon()
    {
        Character character = CreateCharacter("Target", CombatTeam.Ally);
        character.EquipWeapon(new Sword());
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();

        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);

        Transform mask = viewObject.transform.Find("WeaponIconRoot/Mask");
        Assert.That(mask.Find("SwordIcon").gameObject.activeSelf, Is.True);
        Assert.That(mask.Find("ShieldIcon").gameObject.activeSelf, Is.False);

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
    }

    [Test]
    public void CombatPartyMemberView_ClickTogglesCharacterFocusAndHighlight()
    {
        CombatPartyFocus.Clear();
        Character character = CreateCharacter("Target", CombatTeam.Ally);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();
        Image background = viewObject.transform.Find("Background").GetComponent<Image>();
        Color idleColor = background.color;
        view.Bind(character, CombatCharacterAppearanceView.Facing.FrontLeft);

        viewObject.GetComponent<Button>().onClick.Invoke();

        Assert.That(CombatPartyFocus.Selected, Is.EqualTo(character));
        Assert.That(background.color, Is.Not.EqualTo(idleColor));

        viewObject.GetComponent<Button>().onClick.Invoke();
        Assert.That(CombatPartyFocus.Selected, Is.Null);
        Assert.That(background.color, Is.EqualTo(idleColor));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(character.gameObject);
        Object.DestroyImmediate(GameObject.Find("CombatFocusMarkerOverlayCanvas"));
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
    public void CombatPartyMemberView_ShowSkillDisplaysAndExpires()
    {
        Character ally = CreateCharacter("Ally", CombatTeam.Ally);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();
        view.Bind(ally, CombatCharacterAppearanceView.Facing.FrontLeft);
        view.ShowSkill("Slash", 0f);

        Assert.That(view.CurrentSkillText, Is.EqualTo("Slash"));

        view.Tick(3f);
        Assert.That(view.CurrentSkillText, Is.Empty);

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(ally.gameObject);
    }

    [Test]
    public void CombatPartyMemberView_ShowsCastingSkill()
    {
        Character ally = CreateCharacter("Ally", CombatTeam.Ally);
        var viewObject = CreateMemberViewObject("MemberView");
        var view = viewObject.GetComponent<CombatPartyMemberView>();
        var skill = new PartyViewCastTestSkill();
        view.Bind(ally, CombatCharacterAppearanceView.Facing.FrontLeft);

        ally.SkillCaster.TryStartCast(skill, SkillExecutionContext.None);
        view.Tick(0f);

        Assert.That(view.CurrentSkillText, Is.EqualTo("Bolt詠唱中"));

        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(ally.gameObject);
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
        CreateTmpText(root.transform, "PersonalityText");
        CreateTmpText(root.transform, "ObjectiveText");
        CreateTmpText(root.transform, "BuffDebuffText");
        CreateTmpText(root.transform, "WeaponText");
        GameObject weaponIconRoot = CreateChild(root.transform, "WeaponIconRoot", typeof(RectTransform));
        weaponIconRoot.SetActive(false);
        GameObject weaponMask = CreateChild(weaponIconRoot.transform, "Mask", typeof(RectTransform));
        CreateChild(weaponMask.transform, "SwordIcon", typeof(RectTransform), typeof(Image));
        CreateChild(weaponMask.transform, "ShieldIcon", typeof(RectTransform), typeof(Image));
        CreateChild(weaponMask.transform, "WandIcon", typeof(RectTransform), typeof(Image));
        CreateChild(weaponMask.transform, "GrimoireIcon", typeof(RectTransform), typeof(Image));
        CreateChild(weaponMask.transform, "BibleIcon", typeof(RectTransform), typeof(Image));
        CreateChild(weaponMask.transform, "RosaryIcon", typeof(RectTransform), typeof(Image));
        GameObject buffDebuffRoot = CreateChild(root.transform, "BuffDebuffRoot", typeof(RectTransform));
        CreateChild(buffDebuffRoot.transform, "Image", typeof(RectTransform), typeof(Image));
        CreateChild(buffDebuffRoot.transform, "Image (1)", typeof(RectTransform), typeof(Image));
        CreateChild(buffDebuffRoot.transform, "Image (2)", typeof(RectTransform), typeof(Image));
        CreateTmpText(root.transform, "HpText");
        GameObject skillBackground = CreateChild(root.transform, "SkillBackground", typeof(RectTransform), typeof(Image));
        skillBackground.SetActive(false);
        GameObject skillText = CreateTmpText(skillBackground.transform, "SkillText");
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

    private sealed class PartyViewCastTestSkill : SkillBase
    {
        public override string Name => "Bolt";
        public override float CastTimeSeconds => 1f;

        public override void Execute(Character self, SkillExecutionContext context)
        {
        }
    }
}
