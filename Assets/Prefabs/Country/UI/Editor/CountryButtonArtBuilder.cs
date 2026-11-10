using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using static CountryGovernmentBuilder;

public static class CountryButtonArtBuilder
{
    private const string Art = "Assets/Images/UI/Country/Buttons/ExpeditionParts-v1.png";
    private const string CoinArt = "Assets/Images/UI/Country/Controls/AllocationCoin-v1.png";
    private const string Records = "Artifacts/CountryUI/SliderPercent-v3";

    [MenuItem("WarSim/Country/Set Up Button Artwork")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing prefabs.");
        CountryButtonSpriteImporter.Import(CountryGovernmentControlsBuilder.TabArt, new[] {
            new SpriteRect { name = "Game-Tab", rect = new Rect(33, 84, 2032, 581), border = new Vector4(224, 160, 224, 160) }
        });
        CountryButtonSpriteImporter.Import(CountryGovernmentControlsBuilder.SchoolArt, new[] {
            new SpriteRect { name = "School-Warrior", rect = new Rect(128, 44, 482, 601) },
            new SpriteRect { name = "School-Magic", rect = new Rect(806, 44, 547, 642) },
            new SpriteRect { name = "School-Church", rect = new Rect(1528, 57, 571, 605) }
        });
        CountryButtonSpriteImporter.Import(CoinArt, new[] { new SpriteRect { name = "Allocation-Coin", rect = new Rect(362, 335, 547, 547) } });
        var coinImporter = (TextureImporter)AssetImporter.GetAtPath(CoinArt); coinImporter.maxTextureSize = 128; coinImporter.isReadable = false; coinImporter.SaveAndReimport();
        CountryButtonSpriteImporter.Import(Art, new[] {
            new SpriteRect { name = "Expedition-Map", rect = new Rect(25, 104, 1217, 675) },
            new SpriteRect { name = "Expedition-Flag", rect = new Rect(1313, 95, 432, 628) }
        });
        SavePrefab("CountryEffectStrip", CountryGovernmentControlsBuilder.Note);
        SavePrefab("CountryGovernmentAllocationRow", CountryGovernmentControlsBuilder.Allocation);
        SavePrefab("CountryGovernmentChoice", CountryGovernmentControlsBuilder.FacilityChoice);
        SavePrefab("CountryTraining", CountryGovernmentControlsBuilder.Training);
        foreach (string name in new[] { "CountryGovernment", "CountryPersonnel", "CountryRosterView", "CountryRosterConfirm" })
            SavePrefab(name, root => {
                if (name == "CountryGovernment") CountryGovernmentControlsBuilder.Government(root);
                if (name == "CountryRosterView") CountryGovernmentControlsBuilder.Roster(root);
                if (name == "CountryPersonnel") CountryGovernmentControlsBuilder.Personnel(root);
                foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (!PrefabUtility.IsPartOfPrefabInstance(button) && button.name is "Back" or "Confirm" or "Cancel" or "Close" or "Action")
                    {
                        foreach (var plate in button.GetComponentsInChildren<CountryGovernmentPlate>(true))
                        {
                            var data = new SerializedObject(plate); data.FindProperty("_cut").floatValue = 0; data.ApplyModifiedPropertiesWithoutUndo();
                        }
                        Motion(button, 3, .012f, 0);
                    }
            });
        SavePrefab("CountryButtonGroup", root => {
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                DisableEdges(button.transform.Find("Face"));
                Motion(button, 3, .025f, 0);
            }
        });
        SavePrefab("CountryHud", root => {
            foreach (var button in root.transform.Find("BottomLeftButtons").GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                foreach (var feedback in button.GetComponents<CountryButtonFeedback>())
                    if (PrefabUtility.GetCorrespondingObjectFromSource(feedback) == null) UnityEngine.Object.DestroyImmediate(feedback);
                Motion(button, 3, .025f, 0);
            }
            ApplyExpedition(root.transform.Find("BottomRightButton"));
        });
        AssetDatabase.SaveAssets();
        Debug.Log("Country button artwork updated. The open scene was not saved or reloaded.");
    }

    private static void ApplyExpedition(Transform root)
    {
        var rect = (RectTransform)root; rect.sizeDelta = new Vector2(224, 208);
        root.GetComponent<UnityEngine.UI.Image>().enabled = false;
        var faceRect = Fill(root, "Face");
        var face = faceRect.GetComponent<UnityEngine.UI.Image>();
        face.sprite = Load("Expedition-Map"); face.type = UnityEngine.UI.Image.Type.Simple;
        face.preserveAspect = true; face.color = Color.white; face.raycastTarget = true;
        face.GetComponent<UnityEngine.UI.Mask>().enabled = false;
        DisableEdges(face.transform);
        var shadowRect = Fill(root, "Shadow"); shadowRect.anchoredPosition = new Vector2(0, -4); shadowRect.SetAsFirstSibling();
        var shadow = Image(shadowRect, new Color(.12f, .15f, .2f, .45f));
        shadow.sprite = face.sprite; shadow.preserveAspect = true;
        var button = root.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = face;
        var colors = button.colors; colors.normalColor = colors.selectedColor = colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.72f, .72f, .72f); colors.disabledColor = CountryGovernmentTheme.DisabledFace;
        colors.fadeDuration = .08f; button.colors = colors;
        var icon = root.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(image => image.name == "Icon");
        icon.sprite = Load("Expedition-Flag"); icon.raycastTarget = true;
        var flag = icon.rectTransform; flag.anchorMin = flag.anchorMax = new Vector2(.65f, 0); flag.pivot = new Vector2(.15f, 0);
        flag.sizeDelta = new Vector2(80, 112); flag.anchoredPosition = new Vector2(0, 96);
        var label = root.GetComponentsInChildren<TextMeshProUGUI>(true).First(text => text.name == "Label");
        label.rectTransform.offsetMin = new Vector2(-42, 20); label.rectTransform.offsetMax = new Vector2(42, 124);
        label.fontSize = 68; label.enableAutoSizing = false; label.raycastTarget = true;
        const string materialPath = "Assets/Prefabs/Country/UI/Materials/NotoSansJP-Regular SDF - Country Expedition.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(label.fontSharedMaterial) { name = "NotoSansJP-Regular SDF - Country Expedition" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetFloat("_OutlineWidth", .5f); material.EnableKeyword("OUTLINE_ON"); EditorUtility.SetDirty(material);
        label.fontSharedMaterial = material; label.UpdateMeshPadding();
        foreach (var component in new Component[] { rect, root.GetComponent<UnityEngine.UI.Image>(), face, face.GetComponent<UnityEngine.UI.Mask>(), button, icon, flag, label.rectTransform, label })
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        if (!root.TryGetComponent<CountryButtonShape>(out var shape)) shape = root.gameObject.AddComponent<CountryButtonShape>();
        var shapeData = new SerializedObject(shape); var images = shapeData.FindProperty("_images"); images.arraySize = 2;
        images.GetArrayElementAtIndex(0).objectReferenceValue = face; images.GetArrayElementAtIndex(1).objectReferenceValue = icon;
        shapeData.FindProperty("_label").objectReferenceValue = label.rectTransform;
        shapeData.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
        AlignExpeditionArtwork(root);
        Motion(button, 5, .055f, -4);
    }

    internal static void AlignExpeditionArtwork(Transform root)
    {
        var sprite = root.Find("Face").GetComponent<UnityEngine.UI.Image>().sprite;
        float height = ((RectTransform)root).rect.width * sprite.rect.height / sprite.rect.width;
        foreach (string name in new[] { "Face", "Shadow" })
        {
            var rect = (RectTransform)root.Find(name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right; rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = name == "Shadow" ? new Vector2(0, -4) : Vector2.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
    }

    private static void DisableEdges(Transform face)
    {
        foreach (string name in new[] { "UpperEdge", "LowerEdge" })
        {
            var edge = face.Find(name);
            if (edge != null) { edge.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(edge.gameObject); }
        }
    }
    private static void Motion(UnityEngine.UI.Button button, float depth, float hover, float tilt)
    {
        CountryButtonFeedbackBuilder.Configure(button);
        var feedback = button.GetComponent<CountryButtonFeedback>();
        if (feedback == null) return;
        var data = new SerializedObject(feedback);
        data.FindProperty("_pressDepth").floatValue = depth; data.FindProperty("_hoverScale").floatValue = hover; data.FindProperty("_hoverTilt").floatValue = tilt;
        data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
    }
    private static Sprite Load(string name) => AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().First(sprite => sprite.name == name);

    [MenuItem("WarSim/Country/Button Artwork/Verify Budget Rules Without Scene Changes")]
    public static void VerifyBudgetRules()
    {
        var suite = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("CountryGovernmentDraftTests")).First(type => type != null);
        var results = new System.Collections.Generic.List<RuleReport>();
        foreach (var method in suite.GetMethods().Where(method => method.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "TestAttribute")))
        {
            var test = Activator.CreateInstance(suite); var result = new RuleReport { Name = method.Name };
            try { suite.GetMethod("SetUp").Invoke(test, null); method.Invoke(test, null); result.Passed = true; }
            catch (Exception exception) { result.Error = (exception.InnerException ?? exception).ToString(); }
            finally { suite.GetMethod("TearDown").Invoke(test, null); }
            results.Add(result);
        }
        Directory.CreateDirectory(Records); File.WriteAllText(Records + "/budget-rules.json", JsonUtility.ToJson(new RuleResults { Tests = results.ToArray() }, true));
        if (results.Any(result => !result.Passed)) throw new InvalidOperationException("Budget regression tests failed. See " + Records);
        Debug.Log($"Country budget rules: {results.Count}/{results.Count} passed. No scene was opened or saved.");
    }

    [MenuItem("WarSim/Country/Button Artwork/Capture HUD")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        Canvas.ForceUpdateCanvases(); PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        Directory.CreateDirectory(Records);
        var hud = GameObject.Find("CountryHud");
        var report = new Report {
            Width = (int)width, Height = (int)height,
            Buttons = hud.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name.StartsWith("Button") || button.name == "BottomRightButton").Select(button => new ButtonReport {
                Path = AnimationUtility.CalculateTransformPath(button.transform, hud.transform),
                FacePosition = button.targetGraphic.rectTransform.anchoredPosition, FaceScale = button.targetGraphic.transform.localScale,
                Layers = button.GetComponentsInChildren<UnityEngine.UI.Image>().Where(image => image.enabled).Select(image => image.name + ":" + (image.sprite != null ? image.sprite.name : "")).ToArray(),
                Parts = button.GetComponentsInChildren<RectTransform>().Where(part => part.name is "Icon" or "Label").Select(part => new PartReport {
                    Name = part.name, Position = part.anchoredPosition, Scale = part.localScale, Rotation = part.localEulerAngles.z
                }).ToArray()
            }).ToArray(),
            TextOverflows = hud.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing && text.GetComponentInParent<UnityEngine.UI.Button>() != null).Select(text => text.text).ToArray(),
            OtherTextOverflows = hud.GetComponentsInChildren<TextMeshProUGUI>().Where(text => text.isTextOverflowing && text.GetComponentInParent<UnityEngine.UI.Button>() == null).Select(text => text.text).ToArray()
        };
        report.Navigation = hud.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.targetGraphic is UnityEngine.UI.Image img && img.sprite != null && img.sprite.name == "Game-Tab").Select(button => new NavigationReport {
            Path = AnimationUtility.CalculateTransformPath(button.transform, hud.transform), Selected = button.transform.Find("Selected").gameObject.activeSelf,
            Color = button.colors.normalColor, Sprites = button.GetComponentsInChildren<UnityEngine.UI.Image>().Where(img => img.enabled && img.sprite != null).Select(img => img.sprite.name).ToArray()
        }).ToArray();
        report.Scales = hud.GetComponentsInChildren<CountrySliderScale>().Select(scale => {
            var slider = scale.GetComponentInParent<UnityEngine.UI.Slider>();
            return new ScaleReport { Path = AnimationUtility.CalculateTransformPath(slider.transform, hud.transform), Minimum = slider.minValue, Maximum = slider.maxValue, Value = slider.value,
                Labels = scale.GetComponentsInChildren<TextMeshProUGUI>().Select(label => new ScaleLabel { Text = label.text, Fraction = label.rectTransform.anchorMin.x }).ToArray() };
        }).ToArray();
        var expedition = hud.transform.Find("BottomRightButton");
        report.MapHit = Hits(expedition.Find("Face") as RectTransform, new Vector2(.5f, .5f), expedition);
        var flag = expedition.GetComponentsInChildren<UnityEngine.UI.Image>().First(image => image.name == "Icon").rectTransform;
        report.FlagHit = Hits(flag, new Vector2(.4f, .7f), expedition);
        report.EmptyCornerHit = Hits(expedition as RectTransform, new Vector2(.05f, .95f), expedition);
        File.WriteAllText(Records + $"/HUD-{width}x{height}.json", JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Records + $"/HUD-{width}x{height}.png"));
    }
    [MenuItem("WarSim/Country/Button Artwork/Verify Slider Pointer Drag")]
    public static void VerifySliderDrag()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode and open Budget first.");
        var root = GameObject.Find("CountryHud/Government/Window/Shell/Main/Budget/Facilities/Viewport/Content/戦士学校/Funding/Slider");
        var slider = root.GetComponent<UnityEngine.UI.Slider>();
        var draft = GameObject.Find("CountryHud/Government").GetComponent<CountryGovernmentPanel>().Draft;
        float basis = draft.FundingBasis;
        var area = (RectTransform)slider.handleRect.parent;
        Canvas.ForceUpdateCanvases();
        Vector2 ScreenPoint(RectTransform rect, Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
        var point = ScreenPoint(slider.handleRect, slider.handleRect.rect.center);
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = point, pressPosition = point };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        if (!hits[0].gameObject.transform.IsChildOf(root.transform)) throw new InvalidOperationException("The slider is covered by another UI element.");
        data.pointerPressRaycast = data.pointerCurrentRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        var steps = new System.Collections.Generic.List<DragStep>();
        foreach (float fraction in new[] { -.05f, .25f, 1.05f, 200f / basis })
        {
            var next = ScreenPoint(area, new Vector2(area.rect.xMin + area.rect.width * fraction, area.rect.center.y));
            data.delta = next - data.position; data.position = next;
            ExecuteEvents.Execute(root, data, ExecuteEvents.dragHandler); Canvas.ForceUpdateCanvases();
            float handleX = ScreenPoint(slider.handleRect, slider.handleRect.rect.center).x;
            float fillX = ScreenPoint(slider.fillRect, new Vector2(slider.fillRect.rect.xMax, slider.fillRect.rect.center.y)).x;
            float leftX = ScreenPoint(area, new Vector2(area.rect.xMin, area.rect.center.y)).x;
            float rightX = ScreenPoint(area, new Vector2(area.rect.xMax, area.rect.center.y)).x;
            steps.Add(new DragStep { Fraction = fraction, Value = slider.value, Minimum = slider.minValue, Maximum = slider.maxValue, HandleX = handleX, FillX = fillX,
                HandleFraction = Mathf.InverseLerp(leftX, rightX, handleX) });
        }
        ExecuteEvents.Execute(root, data, ExecuteEvents.pointerUpHandler); EventSystem.current.SetSelectedGameObject(null);
        var image = slider.handleRect.GetComponent<UnityEngine.UI.Image>();
        PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
        var report = new DragReport { Width = (int)width, Height = (int)height, Sprite = image.sprite.name, HandleSize = slider.handleRect.rect.size,
            ActualTreasury = PlayDataManager.Instance.Treasury, TreasuryForecast = GameObject.Find("CountryHud/Government").GetComponent<CountryGovernmentPanel>().Draft.TreasuryForecast, Steps = steps.ToArray() };
        File.WriteAllText(Records + $"/slider-pointer-{width}x{height}.json", JsonUtility.ToJson(report, true));
        if (!steps.Select(step => step.Value).SequenceEqual(new[] { 100f, Mathf.Min(Mathf.Round(basis / 40) * 10, draft.MaximumFunding(0)), draft.MaximumFunding(0), 200f }) || steps.Any(step => step.Minimum != 0 || step.Maximum != basis || Mathf.Abs(step.HandleX - step.FillX) > .1f || Mathf.Abs(step.HandleFraction - step.Value / basis) > .001f))
            throw new InvalidOperationException("Slider drag or handle/fill alignment failed. See " + Records);
    }
    [MenuItem("WarSim/Country/Button Artwork/Press Expedition")]
    public static void Press() => Pointer("BottomRightButton", true);
    [MenuItem("WarSim/Country/Button Artwork/Select Expedition")]
    public static void SelectExpedition() => EventSystem.current.SetSelectedGameObject(GameObject.Find("CountryHud/BottomRightButton"));
    [MenuItem("WarSim/Country/Button Artwork/Release Expedition")]
    public static void Release() => Pointer("BottomRightButton", false);
    [MenuItem("WarSim/Country/Button Artwork/Press Budget")]
    public static void PressBudget() => Pointer("BottomLeftButtons/Buttons/Button1", true);
    [MenuItem("WarSim/Country/Button Artwork/Release Budget")]
    public static void ReleaseBudget() => Pointer("BottomLeftButtons/Buttons/Button1", false);
    private static void Pointer(string path, bool pressed)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var button = GameObject.Find("CountryHud/" + path);
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (pressed) ExecuteEvents.Execute(button, data, ExecuteEvents.pointerDownHandler);
        else { ExecuteEvents.Execute(button, data, ExecuteEvents.pointerUpHandler); EventSystem.current.SetSelectedGameObject(null); }
    }
    private static bool Hits(RectTransform rect, Vector2 fraction, Transform button)
    {
        Vector2 local = rect.rect.min + Vector2.Scale(rect.rect.size, fraction);
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local)) };
        var results = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, results);
        return results.Any(result => result.gameObject.transform.IsChildOf(button));
    }
    [Serializable] private sealed class Report { public int Width, Height; public bool MapHit, FlagHit, EmptyCornerHit; public ButtonReport[] Buttons; public string[] TextOverflows, OtherTextOverflows; public NavigationReport[] Navigation; public ScaleReport[] Scales; }
    [Serializable] private sealed class ButtonReport { public string Path; public Vector2 FacePosition; public Vector3 FaceScale; public string[] Layers; public PartReport[] Parts; }
    [Serializable] private sealed class PartReport { public string Name; public Vector2 Position; public Vector3 Scale; public float Rotation; }
    [Serializable] private sealed class RuleResults { public RuleReport[] Tests; }
    [Serializable] private sealed class RuleReport { public string Name, Error; public bool Passed; }
    [Serializable] private sealed class DragReport { public int Width, Height, ActualTreasury, TreasuryForecast; public string Sprite; public Vector2 HandleSize; public DragStep[] Steps; }
    [Serializable] private sealed class DragStep { public float Fraction, Value, Minimum, Maximum, HandleX, FillX, HandleFraction; }
    [Serializable] private sealed class NavigationReport { public string Path; public bool Selected; public Color Color; public string[] Sprites; }
    [Serializable] private sealed class ScaleReport { public string Path; public float Minimum, Maximum, Value; public ScaleLabel[] Labels; }
    [Serializable] private sealed class ScaleLabel { public string Text; public float Fraction; }
}
