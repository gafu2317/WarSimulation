using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static class CountryConstructionSignPreview
{
    private static CountryGovernmentPanel Panel => Object.FindAnyObjectByType<CountryGovernmentPanel>();
    private static T Field<T>(Object target, string name) where T : Object => (T)new SerializedObject(target).FindProperty(name).objectReferenceValue;
    private static UnityEngine.UI.Button Command => Field<UnityEngine.UI.Button>(Panel,"_constructionCommand");

    [MenuItem("WarSim/Country/Construction Sign/Verify Setup")]
    public static void Verify()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountryConstructionSignBuilder.Prefab);
        var button = prefab.GetComponent<UnityEngine.UI.Button>();
        if (button.targetGraphic is not UnityEngine.UI.Image face || face.sprite.name != "Construction-Planks") throw new InvalidOperationException("Construction sign face is missing.");
        if (prefab.GetComponentsInChildren<UnityEngine.UI.Image>(true).Count(image => image.sprite != null && image.sprite.name == "Construction-Brace") != 2) throw new InvalidOperationException("Construction sign requires two shared braces.");
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryGovernment.prefab").GetComponent<CountryGovernmentPanel>();
        var sign = Field<UnityEngine.UI.Button>(panel,"_constructionCommand");
        if (sign == null || sign == Field<UnityEngine.UI.Button>(panel,"_confirmButton") || sign.gameObject.activeSelf) throw new InvalidOperationException("Construction command connection is invalid.");
        Directory.CreateDirectory(CountryConstructionSignBuilder.Records);
        File.WriteAllText(CountryConstructionSignBuilder.Records+"/setup.json",JsonUtility.ToJson(new Setup {
            Project=Application.dataPath, Face=face.sprite.name, Width=((RectTransform)button.transform).rect.width,
            Height=((RectTransform)button.transform).rect.height, SharedBraces=2, SeparateConstructionCommand=true
        },true));
    }

    [MenuItem("WarSim/Country/Construction Sign/Hold")]
    public static void Hold() => ExecuteEvents.Execute(Command.gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerDownHandler);
    [MenuItem("WarSim/Country/Construction Sign/Release")]
    public static void Release() => ExecuteEvents.Execute(Command.gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerUpHandler);

    [MenuItem("WarSim/Country/Construction Sign/Capture")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying || !Panel.IsOpen) throw new InvalidOperationException("Open government UI in the existing Play session.");
        Canvas.ForceUpdateCanvases(); PlayModeWindow.GetRenderingResolution(out uint width,out uint height);
        var button = Command; var label = Field<TextMeshProUGUI>(Panel,"_constructionCommandLabel");
        var report = new CaptureReport {
            Width=(int)width,Height=(int)height,Label=label.text,SignVisible=button.gameObject.activeInHierarchy,
            NormalVisible=Field<UnityEngine.UI.Button>(Panel,"_confirmButton").gameObject.activeInHierarchy,
            Interactable=button.interactable,LabelOverflow=label.isTextOverflowing,
            FaceY=button.targetGraphic.rectTransform.anchoredPosition.y,FaceScale=button.targetGraphic.rectTransform.localScale.x,
            IconY=((RectTransform)button.transform.Find("Icon")).anchoredPosition.y,
            TextY=label.rectTransform.anchoredPosition.y,Running=DateManager.Instance.IsRunning,
            TextOverflows=Panel.GetComponentsInChildren<TextMeshProUGUI>().Where(text=>text.isTextOverflowing).Select(text=>text.name+": "+text.text).ToArray()
        };
        Directory.CreateDirectory(CountryConstructionSignBuilder.Records);
        string path=$"{CountryConstructionSignBuilder.Records}/Current-{width}x{height}";
        File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(path+".png"));
    }
    [Serializable] private sealed class Setup { public string Project,Face; public float Width,Height; public int SharedBraces; public bool SeparateConstructionCommand; }
    [Serializable] private sealed class CaptureReport {
        public int Width,Height; public string Label; public bool SignVisible,NormalVisible,Interactable,LabelOverflow,Running;
        public float FaceY,FaceScale,IconY,TextY; public string[] TextOverflows;
    }
}
