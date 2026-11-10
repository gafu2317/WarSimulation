using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;

public static class CountryCommandMotifsBuilder
{
    internal const string Folder = "Assets/Prefabs/Country/UI";
    internal const string Atlas = "Assets/Images/UI/Country/Buttons/CommandMotifParts-v1.png";
    internal const string Records = "Artifacts/CountryUI/CommandMotifs-v1";
    internal const string TrainingAtlas = "Assets/Images/UI/Country/Buttons/TrainingPennantParts-v1.png";
    internal const string TrainingRecords = "Artifacts/CountryUI/TrainingPennant-v1";
    private static bool Available => AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().Count() == 6;
    private static Sprite Part(string name,string atlas=Atlas) => AssetDatabase.LoadAllAssetsAtPath(atlas).OfType<Sprite>().Single(sprite => sprite.name == name);

    [MenuItem("WarSim/Country/Set Up Command Motifs")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating command motifs.");
        CountryButtonSpriteImporter.Import(Atlas,new[] {
            new SpriteRect { name="Command-Paper",rect=new Rect(80,717,786,255),border=new Vector4(56,32,72,32) },
            new SpriteRect { name="Command-Seal",rect=new Rect(1074,708,290,284) },
            new SpriteRect { name="Command-BookCover",rect=new Rect(61,434,823,223),border=new Vector4(96,48,96,48) },
            new SpriteRect { name="Command-BookPage",rect=new Rect(993,418,465,265),border=new Vector4(56,56,28,56) },
            new SpriteRect { name="Command-ScrollRoll",rect=new Rect(402,40,167,354) },
            new SpriteRect { name="Command-Bookmark",rect=new Rect(1163,36,135,357) }
        });
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);importer.isReadable=false;importer.SaveAndReimport();
        ImportTraining();
        SavePrefab("CountryBudgetCommand",Budget);
        SavePrefab("CountryTrainingCommand",Training);
        SavePrefab("CountryExpeditionRow",ApplyExpeditionRow);
        using var scope=new PrefabUtility.EditPrefabContentsScope(Folder+"/CountryGovernment.prefab");
        ApplyGovernment(scope.prefabContentsRoot);
    }

    [MenuItem("WarSim/Country/Set Up Training Pennant")]
    public static void BuildTraining()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before updating the training pennant.");
        ImportTraining();
        SavePrefab("CountryTrainingCommand",Training);
        using var scope=new PrefabUtility.EditPrefabContentsScope(Folder+"/CountryGovernment.prefab");
        ApplyGovernment(scope.prefabContentsRoot);
    }

    private static void ImportTraining()
    {
        CountryButtonSpriteImporter.Import(TrainingAtlas,new[] {
            new SpriteRect { name="Training-Pennant",rect=new Rect(38,442,1925,301),border=new Vector4(56,32,180,32) },
            new SpriteRect { name="Training-Staff",rect=new Rect(964,33,69,389) }
        });
        var importer=(TextureImporter)AssetImporter.GetAtPath(TrainingAtlas);importer.isReadable=false;importer.SaveAndReimport();
    }

    internal static void ApplyGovernment(GameObject root)
    {
        var panel=root.GetComponent<CountryGovernmentPanel>();var footer=root.transform.Find("Window/Shell/Footer");
        Place("CountryBudgetCommand","BudgetCommand","_budgetCommand","_budgetCommandLabel");
        Place("CountryTrainingCommand","TrainingCommand","_trainingCommand","_trainingCommandLabel");
        void Place(string prefab,string name,string buttonField,string labelField)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+prefab+".prefab");if(asset==null)return;
            var child=footer.Find(name);
            if(child==null) { child=((GameObject)PrefabUtility.InstantiatePrefab(asset,footer)).transform;child.name=name; }
            var rect=(RectTransform)child;var original=(RectTransform)footer.Find("Confirm");
            rect.anchorMin=original.anchorMin;rect.anchorMax=original.anchorMax;rect.pivot=original.pivot;
            rect.anchoredPosition=original.anchoredPosition;rect.sizeDelta=original.sizeDelta;
            child.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(rect);PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            Set(panel,(buttonField,child.GetComponent<UnityEngine.UI.Button>()),(labelField,child.Find("Label").GetComponent<TextMeshProUGUI>()));
        }
    }

    private static void Budget(GameObject root)
    {
        var button=Base(root,"今月の配分を確定","Command-Paper");var face=button.targetGraphic.rectTransform;
        Picture(Right(face,"Seal",8,48,56,56),"Command-Seal",Color.white);
        var icon=Image(Top(root.transform,"Icon",24,28,56,56),Color.white);
        icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/budget-v1.png");icon.preserveAspect=true;
        var label=root.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        label.rectTransform.offsetMin=new Vector2(96,8);label.rectTransform.offsetMax=new Vector2(-68,-8);
    }

    private static void Training(GameObject root)
    {
        var button=Base(root,"今月の訓練を開始","Training-Pennant",TrainingAtlas);var face=button.targetGraphic.rectTransform;
        foreach(string path in new[] { "Surface/LeftPage","Surface/RightPage","Surface/Bookmark","When","ActionLabel" })
            if(root.transform.Find(path) is Transform old)Object.DestroyImmediate(old.gameObject);
        var staff=Picture(Top(face,"Staff",-4,-8,20,120),"Training-Staff",Color.white,false,TrainingAtlas);
        var tint=Add<CountryButtonTint>(root);SetArray(tint,"_parts",new UnityEngine.Object[] { staff });
        var icon=Image(Top(root.transform,"Icon",32,28,56,56),Color.white);
        icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/training-v1.png");icon.preserveAspect=true;
        var label=root.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        label.gameObject.SetActive(true);
        label.rectTransform.offsetMin=new Vector2(100,12);label.rectTransform.offsetMax=new Vector2(-44,-12);
        label.textWrappingMode=TextWrappingModes.NoWrap;
        CountryButtonFeedbackBuilder.Configure(button);
    }

    private static UnityEngine.UI.Button Base(GameObject root,string text,string part,string atlas=Atlas)
    {
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(440,112);
        var button=Button(rect,text,Color.white,true);root.transform.Find("Frame").gameObject.SetActive(false);
        var shadow=Picture(Fill(rect,"Shadow",new Vector2(4,0),new Vector2(-4,-8)),part,new Color(0,0,0,.35f),true,atlas);
        var focus=Picture(Fill(rect,"Focus"),part,CountryGovernmentTheme.Accent,true,atlas);
        var face=Picture(Fill(rect,"Surface",Vector2.one*4,Vector2.one*-4),part,Color.white,true,atlas);
        shadow.transform.SetAsFirstSibling();focus.transform.SetSiblingIndex(1);face.transform.SetSiblingIndex(2);focus.enabled=false;
        button.targetGraphic=face;
        var label=root.transform.Find("Label").GetComponent<TextMeshProUGUI>();label.fontSize=32;label.color=CountryGovernmentTheme.Recess;
        CountryButtonFeedbackBuilder.Configure(button);return button;
    }

    internal static void ApplyExpeditionRow(GameObject root)
    {
        if(!Available)return;
        var button=root.GetComponent<UnityEngine.UI.Button>();var rect=(RectTransform)root.transform;
        var face=Picture(Fill(rect,"Surface",new Vector2(6,8),new Vector2(-6,-8)),"Command-Paper",Color.white,true);
        var focus=Picture(Fill(rect,"Focus",new Vector2(2,4),new Vector2(-2,-4)),"Command-Paper",CountryGovernmentTheme.Accent,true);focus.enabled=false;
        focus.transform.SetAsFirstSibling();face.transform.SetSiblingIndex(1);button.targetGraphic=face;CountryGovernmentTheme.Colors(button,Color.white);
        var left=Picture(Top(face.transform,"LeftRoll",2,0,48,128),"Command-ScrollRoll",Color.white);
        var right=Picture(Right(face.transform,"RightRoll",2,0,48,128),"Command-ScrollRoll",Color.white);
        right.rectTransform.pivot=Vector2.one*.5f;right.rectTransform.anchoredPosition=new Vector2(-26,-64);right.rectTransform.localScale=new Vector3(-1,1,1);
        var tint=Add<CountryButtonTint>(root);SetArray(tint,"_parts",new UnityEngine.Object[] { left,right });
        root.transform.Find("Divider").gameObject.SetActive(false);
        var castle=(RectTransform)root.transform.Find("Castle");castle.anchoredPosition=new Vector2(64,-28);
        foreach(string name in new[] { "Name","Detail","Status" })root.transform.Find(name).GetComponent<TextMeshProUGUI>().color=CountryGovernmentTheme.Recess;
        var title=(RectTransform)root.transform.Find("Name");title.offsetMin=new Vector2(172,title.offsetMin.y);
        var detail=(RectTransform)root.transform.Find("Detail");detail.offsetMin=new Vector2(172,detail.offsetMin.y);
        var status=(RectTransform)root.transform.Find("Status");status.anchoredPosition=new Vector2(-72,status.anchoredPosition.y);
        CountryButtonFeedbackBuilder.Configure(button);
    }

    private static UnityEngine.UI.Image Picture(RectTransform rect,string part,Color color,bool sliced=false,string atlas=Atlas)
    {
        foreach(var graphic in rect.GetComponents<UnityEngine.UI.Graphic>().Where(graphic=>graphic is not UnityEngine.UI.Image).ToArray())Object.DestroyImmediate(graphic);
        var image=Image(rect,color);image.sprite=Part(part,atlas);image.raycastTarget=false;
        image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.preserveAspect=!sliced;image.pixelsPerUnitMultiplier=2;
        return image;
    }
}
