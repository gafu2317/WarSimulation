using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;

public static class CountryConstructionSignBuilder
{
    internal const string Atlas = "Assets/Images/UI/Country/Buttons/ConstructionSignParts-v1.png";
    internal const string Prefab = "Assets/Prefabs/Country/UI/CountryConstructionCommand.prefab";
    internal const string Records = "Artifacts/CountryUI/ConstructionSign-v1";
    private static Sprite Part(string name) => AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().Single(sprite => sprite.name == name);

    [MenuItem("WarSim/Country/Set Up Construction Sign")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating construction artwork.");
        CountryButtonSpriteImporter.Import(Atlas, new[] {
            new SpriteRect { name = "Construction-Planks", rect = new Rect(52,325,1967,375), border = new Vector4(256,48,256,48) },
            new SpriteRect { name = "Construction-Brace", rect = new Rect(956,32,146,292) }
        });
        var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.maxTextureSize = 4096; importer.isReadable = false; importer.SaveAndReimport();
        SavePrefab("CountryConstructionCommand", Command);
        using var scope = new PrefabUtility.EditPrefabContentsScope("Assets/Prefabs/Country/UI/CountryGovernment.prefab");
        Apply(scope.prefabContentsRoot);
        Debug.Log("Construction sign connected without saving or reloading the open scene.");
    }

    internal static void Apply(GameObject root)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (asset == null) return;
        var footer = root.transform.Find("Window/Shell/Footer");
        var command = footer.Find("ConstructionCommand");
        if (command == null)
        {
            command = ((GameObject)PrefabUtility.InstantiatePrefab(asset, footer)).transform;
            command.name = "ConstructionCommand";
        }
        var rect = (RectTransform)command; var normal = (RectTransform)footer.Find("Confirm");
        rect.anchorMin = normal.anchorMin; rect.anchorMax = normal.anchorMax; rect.pivot = normal.pivot;
        rect.anchoredPosition = normal.anchoredPosition; rect.sizeDelta = normal.sizeDelta;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        Set(root.GetComponent<CountryGovernmentPanel>(),
            ("_constructionCommand", command.GetComponent<UnityEngine.UI.Button>()),
            ("_constructionCommandLabel", command.Find("Label").GetComponent<TextMeshProUGUI>()));
        command.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(command.gameObject);
    }

    private static void Command(GameObject root)
    {
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(440,112);
        var button = Button(rect, "建設する", Color.white, true);
        root.transform.Find("Frame").gameObject.SetActive(false);
        var shadow = Picture(Fill(rect,"Shadow",new Vector2(4,0),new Vector2(-4,-8)), "Construction-Planks", new Color(0,0,0,.4f), true);
        var focus = Picture(Fill(rect,"Focus"), "Construction-Planks", CountryGovernmentTheme.Accent, true);
        var face = Picture(Fill(rect,"Surface",Vector2.one*4,Vector2.one*-4), "Construction-Planks", Color.white, true);
        shadow.transform.SetAsFirstSibling(); focus.transform.SetSiblingIndex(1); face.transform.SetSiblingIndex(2);
        focus.enabled = false; button.targetGraphic = face;
        Picture(Top(face.transform,"LeftBrace",14,10,36,84),"Construction-Brace",Color.white);
        var right = Picture(Right(face.transform,"RightBrace",14,10,36,84),"Construction-Brace",Color.white);
        right.rectTransform.pivot = Vector2.one*.5f;
        right.rectTransform.anchoredPosition = new Vector2(-32,-52);
        right.transform.localScale = new Vector3(-1,1,1);
        var icon = Image(Top(rect,"Icon",60,24,64,64),Color.white);
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/construction-v1.png"); icon.preserveAspect = true;
        var label = root.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        label.rectTransform.offsetMin = new Vector2(136,8); label.rectTransform.offsetMax = new Vector2(-56,-8);
        label.fontSize = 32; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        CountryButtonFeedbackBuilder.Configure(button);
        var data = new SerializedObject(button.GetComponent<CountryButtonFeedback>());
        data.FindProperty("_hoverTilt").floatValue = -5; data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UnityEngine.UI.Image Picture(RectTransform rect, string name, Color color, bool sliced = false)
    {
        foreach (var graphic in rect.GetComponents<UnityEngine.UI.Graphic>().Where(graphic => graphic is not UnityEngine.UI.Image).ToArray()) Object.DestroyImmediate(graphic);
        var image = Image(rect,color); image.sprite = Part(name); image.raycastTarget = false;
        image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = !sliced; image.pixelsPerUnitMultiplier = 4;
        return image;
    }
}
