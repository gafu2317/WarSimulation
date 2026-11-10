using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CountryTextLayoutPreview
{
    internal const string Records = "Artifacts/CountryUI/TextLayout-v1";
    private const string RequestPath = "/private/tmp/country-text-layout.json";

    [MenuItem("WarSim/Country/Text Layout/Capture")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Capture the existing Play session.");
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        var root=GameObject.Find(request.Scope);
        if(root==null)throw new InvalidOperationException("Review scope is not active: "+request.Scope);
        Canvas.ForceUpdateCanvases();
        PlayModeWindow.GetRenderingResolution(out uint width,out uint height);
        var report=new CaptureReport {
            Scope=request.Scope,Width=(int)width,Height=(int)height,
            Texts=root.GetComponentsInChildren<TextMeshProUGUI>().Where(text=>text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)).Select(Read).ToArray(),
            Scrolls=root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Select(scroll=>new ScrollInfo {
                Path=Path(scroll.transform),Position=scroll.normalizedPosition,Viewport=Bounds(scroll.viewport),Content=Bounds(scroll.content)
            }).ToArray()
        };
        string folder=Records+"/"+request.Phase;Directory.CreateDirectory(folder);
        string path=$"{folder}/{request.Name}-{width}x{height}";
        File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));
        ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(path+".png"));
    }

    [MenuItem("WarSim/Country/Text Layout/Scroll")]
    public static void Scroll()
    {
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        var scroll=GameObject.Find(request.Scope).GetComponent<UnityEngine.UI.ScrollRect>();
        if(scroll==null)throw new InvalidOperationException("No scroll at "+request.Scope);
        scroll.StopMovement();scroll.normalizedPosition=new Vector2(request.X,request.Y);Canvas.ForceUpdateCanvases();
    }

    [MenuItem("WarSim/Country/Text Layout/Hold")]
    public static void Hold() => Pointer(true);
    [MenuItem("WarSim/Country/Text Layout/Release")]
    public static void Release() => Pointer(false);
    private static void Pointer(bool down)
    {
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        var target=GameObject.Find(request.Scope);
        var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button=UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        if(down)UnityEngine.EventSystems.ExecuteEvents.Execute(target,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        else UnityEngine.EventSystems.ExecuteEvents.Execute(target,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
    }

    [MenuItem("WarSim/Country/Text Layout/Inventory Prefabs")]
    public static void Inventory()
    {
        var rows=AssetDatabase.FindAssets("t:Prefab",new[] { "Assets/Prefabs/Country/UI" }).Select(AssetDatabase.GUIDToAssetPath).Select(path=>new PrefabInfo {
            Asset=path,Texts=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TextMeshProUGUI>(true).Select(text=>new PrefabText {
                Path=AnimationUtility.CalculateTransformPath(text.transform,text.transform.root),Text=text.text,Active=text.gameObject.activeSelf,Size=text.fontSize,
                Alignment=text.alignment.ToString(),Margin=text.margin,Position=text.rectTransform.anchoredPosition,Dimensions=text.rectTransform.sizeDelta
            }).ToArray()
        }).ToArray();
        Directory.CreateDirectory(Records);File.WriteAllText(Records+"/prefab-inventory.json",JsonUtility.ToJson(new InventoryReport { Prefabs=rows },true));
    }

    private static TextInfo Read(TextMeshProUGUI text)
    {
        var rect=Bounds(text.rectTransform);var clip=new Rect(0,0,Screen.width,Screen.height);
        foreach(var mask in text.GetComponentsInParent<UnityEngine.UI.RectMask2D>())
            if(mask.isActiveAndEnabled)clip=Intersect(clip,Bounds((RectTransform)mask.transform));
        foreach(var mask in text.GetComponentsInParent<UnityEngine.UI.Mask>())
            if(mask.isActiveAndEnabled)clip=Intersect(clip,Bounds((RectTransform)mask.transform));
        var button=text.GetComponentInParent<UnityEngine.UI.Button>();
        return new TextInfo {
            Path=Path(text.transform),Text=text.text,Size=text.fontSize,Alignment=text.alignment.ToString(),Margin=text.margin,
            Color=text.color,RendererColor=text.canvasRenderer.GetColor(),Opacity=text.canvasRenderer.GetInheritedAlpha(),Interactable=button==null || button.IsInteractable(),
            Rect=rect,Clip=clip,Visible=Intersect(rect,clip).width>1 && Intersect(rect,clip).height>1,
            Overflow=text.isTextOverflowing,Lines=text.textInfo.lineCount,
            PreferredWidth=text.preferredWidth,PreferredHeight=text.preferredHeight,
            Button=button!=null?Path(button.transform):"",ButtonRect=button!=null?Bounds((RectTransform)button.transform):default
        };
    }
    private static Rect Bounds(RectTransform rect)
    {
        var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    private static Rect Intersect(Rect a,Rect b) => Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
    private static string Path(Transform transform) => "/"+AnimationUtility.CalculateTransformPath(transform,null);
    [MenuItem("WarSim/Country/Text Layout/Inspect Top Bar")]
    public static void InspectTopBar()
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryTopBar.prefab");
        var rows=root.GetComponentsInChildren<UnityEngine.UI.LayoutElement>(true).Select(item=>new LayoutInfo {
            Path=AnimationUtility.CalculateTransformPath(item.transform,root.transform),Minimum=item.minWidth,Preferred=item.preferredWidth,Flexible=item.flexibleWidth
        }).ToArray();
        File.WriteAllText(Records+"/topbar-layout.json",JsonUtility.ToJson(new LayoutReport { Elements=rows },true));
    }

    [MenuItem("WarSim/Country/Text Layout/Focus Facility")]
    public static void FocusFacility()
    {
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        var layer=Object.FindAnyObjectByType<CountryFacilityLayer>();
        var targets=layer.Targets.Where(target=>target.building!=null && target.building.gameObject.activeInHierarchy).ToArray();
        var target=targets[request.Index];
        layer.GetComponentInChildren<CountryFacilityPopup>(true).Close();
        var camera=(Camera)new SerializedObject(layer).FindProperty("_viewCamera").objectReferenceValue;
        var controller=camera.GetComponent<CountryCameraController>();
        float ground=new SerializedObject(controller).FindProperty("_groundHeight").floatValue;
        var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));var plane=new Plane(Vector3.up,new Vector3(0,ground,0));
        if(!plane.Raycast(ray,out float distance))throw new InvalidOperationException("The country camera must face the ground.");
        var focus=ray.GetPoint(distance);
        var position=target.building.position;
        camera.transform.position+=new Vector3(position.x-focus.x,0,position.z-focus.z);controller.SyncStateFromTransform();
        Directory.CreateDirectory(Records);File.WriteAllText(Records+"/facilities.json",JsonUtility.ToJson(new FacilityReport { Names=targets.Select(item=>item.name).ToArray() },true));
    }

    [MenuItem("WarSim/Country/Text Layout/Open Facility")]
    public static void OpenFacility()
    {
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        var layer=Object.FindAnyObjectByType<CountryFacilityLayer>();
        var targets=layer.Targets.Where(target=>target.building!=null && target.building.gameObject.activeInHierarchy).ToArray();
        layer.GetComponentInChildren<CountryFacilityPopup>(true).Open(targets[request.Index]);
    }

    [MenuItem("WarSim/Country/Text Layout/Prepare Training States")]
    public static void PrepareTrainingStates()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Use only the current Play session.");
        var data=PlayDataManager.Instance;data.SetRaidsEnabled(false);
        foreach(var school in data.Roster.Data.Schools)data.Roster.SetSchoolLevel(school.Facility,5);
        if(!data.Roster.TryStartTraining(0,new[] { 0 },out string reason))throw new InvalidOperationException(reason);
        data.Roster.TrySetCP(25,0);
    }

    [MenuItem("WarSim/Country/Text Layout/Open Return Details")]
    public static void OpenReturnDetails()
    {
        var report=Object.FindAnyObjectByType<CountryExpeditionReportView>();
        report.GetComponentInChildren<CountryExpeditionRow>().GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        if(!report.DetailsOpen)throw new InvalidOperationException("Return details did not open.");
    }

    [MenuItem("WarSim/Country/Text Layout/Select Region")]
    public static void SelectRegion()
    {
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        Object.FindAnyObjectByType<CountryExpeditionPanel>().SelectRegion(request.Index);
    }

    [MenuItem("WarSim/Country/Text Layout/Advance To Year End")]
    public static void YearEnd()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Use only the current Play session.");
        PlayDataManager.Instance.SetRaidsEnabled(false);
        var date=DateManager.Instance;var government=Object.FindAnyObjectByType<CountryGovernmentPanel>();
        float interval=new SerializedObject(date).FindProperty("_secondsPerDay").floatValue;
        while(date.CurrentMonth!=12 || date.CurrentDay!=31)
        {
            government.Close();date.Resume();date.AdvanceTime(interval);date.Pause();
        }
        government.Close();
    }

    [MenuItem("WarSim/Country/Text Layout/Verify Prefabs")]
    public static void VerifyPrefabs()
    {
        int groups=0;
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[] { "Assets/Prefabs/Country/UI" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var group in root.GetComponentsInChildren<CountryButtonContent>(true))
            {
                var data=new SerializedObject(group);
                if(data.FindProperty("_icon").objectReferenceValue==null || data.FindProperty("_label").objectReferenceValue==null)
                    throw new InvalidOperationException("Unbound button content: "+path);
                groups++;
            }
        }
        File.WriteAllText(Records+"/prefab-verification.json",JsonUtility.ToJson(new PrefabVerification { Groups=groups,Passed=true },true));
    }
    [Serializable] private sealed class Request { public string Phase,Name,Scope;public float X,Y;public int Index; }
    [Serializable] private sealed class LayoutInfo { public string Path;public float Minimum,Preferred,Flexible; }
    [Serializable] private sealed class LayoutReport { public LayoutInfo[] Elements; }
    [Serializable] private sealed class FacilityReport { public string[] Names; }
    [Serializable] private sealed class PrefabVerification { public int Groups;public bool Passed; }
    [Serializable] private sealed class CaptureReport { public string Scope;public int Width,Height;public TextInfo[] Texts;public ScrollInfo[] Scrolls; }
    [Serializable] private sealed class TextInfo {
        public string Path,Text,Alignment,Button;public float Size,PreferredWidth,PreferredHeight,Opacity;public Color Color,RendererColor;public Vector4 Margin;public Rect Rect,Clip,ButtonRect;public bool Visible,Overflow,Interactable;public int Lines;
    }
    [Serializable] private sealed class ScrollInfo { public string Path;public Vector2 Position;public Rect Viewport,Content; }
    [Serializable] private sealed class InventoryReport { public PrefabInfo[] Prefabs; }
    [Serializable] private sealed class PrefabInfo { public string Asset;public PrefabText[] Texts; }
    [Serializable] private sealed class PrefabText { public string Path,Text,Alignment;public bool Active;public float Size;public Vector4 Margin;public Vector2 Position,Dimensions; }
}
