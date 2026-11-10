using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;
using Theme = CountryGovernmentTheme;

public static class CountryUsabilityBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI/";

    [MenuItem("WarSim/Country/Apply Usability Fixes")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Update prefab assets in Edit mode.");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryRosterConfirm.prefab")) ApplyConfirmation(scope.prefabContentsRoot);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTraining.prefab")) ApplyTraining(scope.prefabContentsRoot);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryGovernmentAllocationRow.prefab"))
        {
            var root = scope.prefabContentsRoot.transform;
            root.Find("Effect/Details/Caption").GetComponent<TextMeshProUGUI>().text = "施設の役割";
            TopFill(root.Find("Effect/Details"), "Caption", 8, 40, 24, 136);
            TopFill(root.Find("Effect/Details"), "EffectText", 48, 80, 24, 136);
            root.Find("Funding/Maximum").GetComponent<TextMeshProUGUI>().fontSize = 24;
        }
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryGovernment.prefab"))
        {
            var root = scope.prefabContentsRoot;
            var caption = root.transform.Find("Window/Shell/Header/Treasury/Caption").GetComponent<TextMeshProUGUI>();
            caption.text = "計画後国庫"; caption.fontSize = 26;
            Set(root.GetComponent<CountryGovernmentPanel>(), ("_treasuryCaption", caption),
                ("_fundingBasis", root.transform.Find("Window/Shell/Main/Budget/Guide/Instruction").GetComponent<TextMeshProUGUI>()));
            var effect = root.transform.Find("Window/Shell/Main/Construction/Content/Right/Detail/Effect");
            TopFill(effect, "Description", 48, 80, 24, 136);
        }
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryTopBar.prefab")) TintSettings(scope.prefabContentsRoot);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "CountryHud.prefab"))
        { ApplyHud(scope.prefabContentsRoot); TintSettings(scope.prefabContentsRoot); }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
        string glyphs = string.Concat(Directory.GetFiles("Assets/Scripts/Country", "*.cs").Select(File.ReadAllText));
        glyphs += string.Concat(Enum.GetValues(typeof(SkillId)).Cast<SkillId>().Select(id => CombatSkillFactory.Create(id)?.EffectDescription));
        glyphs = string.Concat(glyphs.Replace("弹", "弾").Where(c => c >= '\u3000' && c <= '\u9fff' || c is '→' or '↓' or '〜' or '＝').Distinct());
        if (!font.HasCharacters(glyphs))
        {
            if (!font.TryAddCharacters(glyphs, out string missing)) throw new InvalidOperationException("Missing UI characters: " + missing);
        }
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        Debug.Log("Country usability fixes applied to prefab assets. No scene was opened, saved or reloaded.");
    }

    internal static void ApplyConfirmation(GameObject root)
    {
        var controller = root.GetComponent<CountryRosterConfirm>();
        var properties = new SerializedObject(controller);
        var body = (TextMeshProUGUI)properties.FindProperty("_body").objectReferenceValue;
        var card = root.transform.Find("Window/Card");
        var scroll = Scroll(Fill(card, "BodyScroll", new Vector2(48, 160), new Vector2(-48, -136)));
        body.transform.SetParent(scroll.content, false);
        body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.sizeDelta = Vector2.zero;
        Set(controller, ("_bodyScroll", scroll));
    }

    internal static void ApplyTraining(GameObject root)
    {
        var selection = root.transform.Find("Selection");
        var members = (RectTransform)selection.Find("Members"); members.anchorMax = new Vector2(.54f, 1);
        var rect = TopFill(selection, "Learning", 48, 128, 12, 12); rect.anchorMin = new Vector2(.56f, 1);
        var button = Button(rect, "", Theme.Panel);
        CountryGovernmentControlsBuilder.Command(button, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/training-v1.png"));
        var label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>(); label.fontSize = 28;
        label.text = "学ぶ技と効果 ›";
        Set(root.GetComponent<CountryTrainingView>(), ("_learning", button), ("_learningLabel", label));
    }

    internal static void ApplyHud(GameObject root)
    {
        var panel = root.GetComponentInChildren<CountryExpeditionPanel>(true);
        var open = new SerializedObject(panel).FindProperty("_open").objectReferenceValue as UnityEngine.UI.Button;
        var badge = Child(open.transform, "ActiveExpeditions");
        badge.anchorMin = badge.anchorMax = new Vector2(.5f, 1); badge.pivot = new Vector2(.5f, 0);
        badge.anchoredPosition = new Vector2(0, 10); badge.sizeDelta = new Vector2(220, 48);
        var oldText = badge.GetComponent<TextMeshProUGUI>(); if (oldText != null) UnityEngine.Object.DestroyImmediate(oldText);
        Image(badge, Theme.Recess).raycastTarget = false;
        var count = Label(Fill(badge, "Label"), "遠征中 0隊", 28, Theme.Text); count.alignment = TextAlignmentOptions.Center;
        count.raycastTarget = false;
        var notice = Child(root.transform, "DepartureNotice");
        notice.anchorMin = notice.anchorMax = new Vector2(1, 0); notice.pivot = Vector2.one;
        notice.anchoredPosition = new Vector2(-36, 340); notice.sizeDelta = new Vector2(480, 112);
        var oldNotice = notice.GetComponent<TextMeshProUGUI>(); if (oldNotice != null) UnityEngine.Object.DestroyImmediate(oldNotice);
        Image(notice, Theme.Recess).raycastTarget = false;
        var message = Label(Fill(notice, "Label", new Vector2(16, 8), new Vector2(-16, -8)), "", 30, Theme.Text); message.alignment = TextAlignmentOptions.Center;
        message.raycastTarget = false;
        Set(panel, ("_hudCount", count), ("_departureNotice", message));
        PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        count.gameObject.SetActive(true); message.gameObject.SetActive(true);
        badge.gameObject.SetActive(false); notice.gameObject.SetActive(false);
    }

    private static void TintSettings(GameObject root)
    {
        foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if (image.sprite != null && image.sprite.texture.name == "settings-v1")
            { image.color = Theme.Ink; PrefabUtility.RecordPrefabInstancePropertyModifications(image); }
    }
}
