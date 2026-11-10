using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryRosterArtPreview
{
    private const string Records="Artifacts/CountryUI/MemberIcons-v1";
    private const string Folder="Assets/Prefabs/Country/UI/";
    private static CountryRosterView View => Object.FindObjectsByType<CountryRosterView>().First(view=>view.isActiveAndEnabled);
    private static T Field<T>(Object target,string name) where T:Object => (T)new SerializedObject(target).FindProperty(name).objectReferenceValue;

    [MenuItem("WarSim/Country/Roster Artwork Preview/Verify Setup")]
    public static void VerifySetup()
    {
        foreach (string name in new[] { "CountryRosterRow","CountrySoldierToken" })
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab");
            bool token=name=="CountrySoldierToken";
            if (!token) Require(root.transform.Find("Surface").GetComponent<UnityEngine.UI.Image>().sprite.name=="Roster-RowFace",name+" face");
            Require(root.transform.Find("Selected").GetComponent<UnityEngine.UI.Image>().sprite.name==(token ? "Roster-IconRing" : "Roster-RowFrame"),name+" selection");
            Require(root.transform.Find("Portrait/Edge").GetComponent<UnityEngine.UI.Image>().sprite.name=="Roster-IconRing",name+" circle");
            Require(root.transform.Find("Portrait/Clip").GetComponent<UnityEngine.UI.Mask>()!=null,name+" mask");
            var feedback=root.GetComponent<CountryButtonFeedback>();var fields=new SerializedObject(feedback);
            Require(fields.FindProperty("_contents").arraySize==(token ? 2 : 3),name+" animation groups");
            Require(Field<RectTransform>(feedback,"_face")==root.transform.Find(token ? "Portrait" : "Surface"),name+" animated face");
            if (token) Require(!root.transform.Find("Surface").gameObject.activeSelf && !root.transform.Find("Name").gameObject.activeSelf && !root.transform.Find("State").gameObject.activeSelf,"Compact icon token");
        }
        var empty=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"CountryTrainingEmptySlot.prefab");
        Require(empty.transform.Find("Empty").GetComponent<UnityEngine.UI.Image>().sprite.name=="Roster-EmptySlot","Empty slot image");
        var view=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"CountryRosterView.prefab");
        Require(view.transform.Find("Right/Body/Portrait/Frame/Rim").GetComponent<UnityEngine.UI.Image>().sprite.name=="Roster-PortraitFrame","Standing portrait frame");
        Require(Mathf.Abs(view.transform.Find("Right/Body/Portrait/Frame").GetComponent<AspectRatioFitter>().aspectRatio-.5f)<.001f,"Standing portrait ratio");
        Debug.Log("Shared roster faces, frames, circle masks, empty slots, portrait and animation references are valid.");
    }
    private static void Require(bool condition,string name) { if (!condition) throw new InvalidOperationException("Roster artwork: "+name); }

    [MenuItem("WarSim/Country/Roster Artwork Preview/Hold First Row")]
    public static void Hold() => ExecuteEvents.Execute(View.GetComponentsInChildren<CountryRosterRow>().First().gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerDownHandler);
    [MenuItem("WarSim/Country/Roster Artwork Preview/Release First Row")]
    public static void Release() => ExecuteEvents.Execute(View.GetComponentsInChildren<CountryRosterRow>().First().gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerUpHandler);

    [MenuItem("WarSim/Country/Roster Artwork Preview/Verify Scroll")]
    public static void Scroll()
    {
        var scroll=Field<ScrollRect>(View,"_list");Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
        var before=scroll.content.anchoredPosition;
        scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta=new Vector2(0,-1) });
        float step=scroll.content.anchoredPosition.y-before.y;Require(Mathf.Abs(step-18)<.1f,"Scroll step");
        PlayModeWindow.GetRenderingResolution(out uint width,out uint height);
        Directory.CreateDirectory(Records);File.WriteAllText($"{Records}/scroll-{width}x{height}.json",JsonUtility.ToJson(new ScrollReport { Step=step },true));
        scroll.verticalNormalizedPosition=1;
    }

    [MenuItem("WarSim/Country/Roster Artwork Preview/Capture")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture the current Play session only.");
        Canvas.ForceUpdateCanvases();PlayModeWindow.GetRenderingResolution(out uint width,out uint height);
        var view=View;var row=view.GetComponentsInChildren<CountryRosterRow>().First();var button=row.GetComponent<UnityEngine.UI.Button>();
        var frame=Field<AspectRatioFitter>(view,"_portraitFrame");
        var personnel=Object.FindAnyObjectByType<CountryPersonnelPanel>();var expeditions=Object.FindAnyObjectByType<CountryExpeditionPanel>();
        var images=Object.FindObjectsByType<UnityEngine.UI.Image>().Where(image=>image.isActiveAndEnabled && image.sprite!=null && image.sprite.name.StartsWith("Roster-",StringComparison.Ordinal)).ToArray();
        var report=new CaptureReport {
            Page=expeditions.IsOpen ? "Expedition" : personnel.IsOpen ? "Personnel" : "Training",Width=(int)width,Height=(int)height,
            DetailName=Field<TextMeshProUGUI>(view,"_name").text,SelectedSoldier=view.SelectedSoldier,SelectedWeapon=view.SelectedWeapon,
            SelectedRows=view.GetComponentsInChildren<CountryRosterRow>().Count(item=>Field<GameObject>(item,"_selected").activeSelf),
            EmptySlots=images.Count(image=>image.sprite.name=="Roster-EmptySlot"),
            Sprites=images.GroupBy(image=>image.sprite.name).Select(group=>new SpriteUse { Name=group.Key,Count=group.Count() }).ToArray(),
            FaceY=button.targetGraphic.rectTransform.anchoredPosition.y,FaceScale=button.targetGraphic.rectTransform.localScale.x,
            PortraitY=((RectTransform)row.transform.Find("Portrait")).anchoredPosition.y,
            PortraitRatio=frame.GetComponent<RectTransform>().rect.width/frame.GetComponent<RectTransform>().rect.height,
            IndividualImageAssigned=Field<UnityEngine.UI.Image>(view,"_portrait").sprite!=null,
            Tokens=Object.FindObjectsByType<CountryRosterRow>().Where(item=>item.isActiveAndEnabled && item.transform.parent.parent.parent.name=="Members").Select(item=>new TokenReport {
                ObjectName=item.name,NameVisible=Field<TextMeshProUGUI>(item,"_name").gameObject.activeSelf,
                CaptionVisible=Field<TextMeshProUGUI>(item,"_state").gameObject.activeSelf,Caption=Field<TextMeshProUGUI>(item,"_state").text,
                CardVisible=item.transform.Find("Surface").gameObject.activeSelf,Width=((RectTransform)item.transform).rect.width
            }).ToArray(),
            Running=DateManager.Instance.IsRunning,CameraInput=Object.FindAnyObjectByType<CountryGovernmentPanel>().CameraControls.InputEnabled,
            TextOverflows=Object.FindObjectsByType<TextMeshProUGUI>().Where(text=>text.isActiveAndEnabled && text.isTextOverflowing).Select(text=>text.name+": "+text.text).ToArray()
        };
        Directory.CreateDirectory(Records);string path=$"{Records}/Current-{width}x{height}";
        File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));ScreenCapture.CaptureScreenshot(Path.GetFullPath(path+".png"));
    }
    [Serializable] private sealed class SpriteUse { public string Name;public int Count; }
    [Serializable] private sealed class ScrollReport { public float Step; }
    [Serializable] private sealed class TokenReport { public string ObjectName,Caption;public bool NameVisible,CaptionVisible,CardVisible;public float Width; }
    [Serializable] private sealed class CaptureReport {
        public string Page,DetailName;public int Width,Height,SelectedSoldier,SelectedWeapon,SelectedRows,EmptySlots;
        public float FaceY,FaceScale,PortraitY,PortraitRatio;public bool IndividualImageAssigned,Running,CameraInput;
        public SpriteUse[] Sprites;public string[] TextOverflows;
        public TokenReport[] Tokens;
    }
}
