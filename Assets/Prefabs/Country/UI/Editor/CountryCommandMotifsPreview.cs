using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static class CountryCommandMotifsPreview
{
    private static T Field<T>(Object target,string name) where T:Object => (T)new SerializedObject(target).FindProperty(name).objectReferenceValue;
    private static UnityEngine.UI.Button Command => Object.FindAnyObjectByType<CountryGovernmentPanel>().GetComponentsInChildren<UnityEngine.UI.Button>().First(button=>button.name is "BudgetCommand" or "TrainingCommand");

    [MenuItem("WarSim/Country/Command Motifs/Verify Setup")]
    public static void Verify()
    {
        var panel=AssetDatabase.LoadAssetAtPath<GameObject>(CountryCommandMotifsBuilder.Folder+"/CountryGovernment.prefab").GetComponent<CountryGovernmentPanel>();
        foreach(string name in new[] { "budget","training" })
        {
            var button=Field<UnityEngine.UI.Button>(panel,"_"+name+"Command");
            if(button==null || button.gameObject.activeSelf || button.targetGraphic is not UnityEngine.UI.Image)throw new InvalidOperationException("Command motif connection: "+name);
            if(Field<TextMeshProUGUI>(panel,"_"+name+"CommandLabel")==null)throw new InvalidOperationException("Command motif label: "+name);
        }
        var row=AssetDatabase.LoadAssetAtPath<GameObject>(CountryCommandMotifsBuilder.Folder+"/CountryExpeditionRow.prefab");
        if(row.GetComponentsInChildren<UnityEngine.UI.Image>().Count(image=>image.sprite!=null && image.sprite.name=="Command-ScrollRoll")!=2)throw new InvalidOperationException("Expedition scroll ends are missing.");
        Directory.CreateDirectory(CountryCommandMotifsBuilder.Records);
        File.WriteAllText(CountryCommandMotifsBuilder.Records+"/setup.json",JsonUtility.ToJson(new Setup { Project=Application.dataPath,Commands=2,SharedSprites=6,ScrollEnds=2 },true));
    }

    [MenuItem("WarSim/Country/Training Pennant/Verify Setup")]
    public static void VerifyTraining()
    {
        var panel=AssetDatabase.LoadAssetAtPath<GameObject>(CountryCommandMotifsBuilder.Folder+"/CountryGovernment.prefab").GetComponent<CountryGovernmentPanel>();
        var button=Field<UnityEngine.UI.Button>(panel,"_trainingCommand");
        var label=Field<TextMeshProUGUI>(panel,"_trainingCommandLabel");
        if(button==null || button.gameObject.activeSelf || button.targetGraphic is not UnityEngine.UI.Image face || face.sprite.name!="Training-Pennant")throw new InvalidOperationException("Training pennant connection is missing.");
        if(label==null || !label.gameObject.activeSelf || label.textWrappingMode!=TextWrappingModes.NoWrap || button.GetComponentsInChildren<TextMeshProUGUI>(true).Length!=1)throw new InvalidOperationException("Training must have one visible single-line label.");
        var staff=button.transform.Find("Surface/Staff").GetComponent<UnityEngine.UI.Image>();
        if(staff.sprite.name!="Training-Staff" || staff.raycastTarget)throw new InvalidOperationException("Training staff is missing or blocks input.");
        Directory.CreateDirectory(CountryCommandMotifsBuilder.TrainingRecords);
        File.WriteAllText(CountryCommandMotifsBuilder.TrainingRecords+"/setup.json",JsonUtility.ToJson(new Setup { Project=Application.dataPath,Commands=1,SharedSprites=2 },true));
    }

    [MenuItem("WarSim/Country/Command Motifs/Hold")]
    public static void Hold() => ExecuteEvents.Execute(Command.gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerDownHandler);
    [MenuItem("WarSim/Country/Command Motifs/Release")]
    public static void Release() => ExecuteEvents.Execute(Command.gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerUpHandler);

    [MenuItem("WarSim/Country/Command Motifs/Capture")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Capture in the existing Play session.");
        Canvas.ForceUpdateCanvases();PlayModeWindow.GetRenderingResolution(out uint width,out uint height);
        var panel=Object.FindAnyObjectByType<CountryGovernmentPanel>();
        var button=panel.IsOpen?panel.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(item=>item.name is "BudgetCommand" or "TrainingCommand" or "ConstructionCommand"):null;
        var rows=Object.FindObjectsByType<CountryExpeditionRow>().Where(row=>row.isActiveAndEnabled).ToArray();
        var texts=button!=null?panel.GetComponentsInChildren<TextMeshProUGUI>():rows.SelectMany(row=>row.GetComponentsInChildren<TextMeshProUGUI>()).ToArray();
        var report=new CaptureReport {
            Width=(int)width,Height=(int)height,Page=button!=null?button.name:"ExpeditionLog",Rows=rows.Length,
            Label=button!=null?button.transform.Find("Label").GetComponent<TextMeshProUGUI>().text:"",
            Message=Field<TextMeshProUGUI>(panel,"_status").text,
            RowPaths=rows.Select(row=>"/"+AnimationUtility.CalculateTransformPath(row.transform,null)).ToArray(),
            ExpeditionDetailsOpen=Field<CountryRosterConfirm>(Object.FindAnyObjectByType<CountryExpeditionPanel>(),"_logDetails").IsOpen,
            Interactable=button!=null && button.interactable,NormalVisible=Field<UnityEngine.UI.Button>(panel,"_confirmButton").gameObject.activeInHierarchy,
            VisibleCommands=panel.GetComponentsInChildren<UnityEngine.UI.Button>().Count(item=>item.name is "BudgetCommand" or "TrainingCommand" or "ConstructionCommand" or "Confirm"),
            FaceY=button!=null?button.targetGraphic.rectTransform.anchoredPosition.y:0,
            FaceScale=button!=null?button.targetGraphic.rectTransform.localScale.x:0,
            TextY=button!=null?((RectTransform)button.transform.Find("Label")).anchoredPosition.y:0,
            TintPartsMatch=Object.FindObjectsByType<CountryButtonTint>().Where(tint=>tint.isActiveAndEnabled).All(tint=> {
                var source=tint.GetComponent<UnityEngine.UI.Button>().targetGraphic.canvasRenderer.GetColor();
                var parts=new SerializedObject(tint).FindProperty("_parts");
                for(int i=0;i<parts.arraySize;i++)if(((UnityEngine.UI.Graphic)parts.GetArrayElementAtIndex(i).objectReferenceValue).canvasRenderer.GetColor()!=source)return false;
                return parts.arraySize>0;
            }),
            Running=DateManager.Instance.IsRunning,
            TextOverflows=texts.Where(text=>text.isTextOverflowing).Select(text=>text.name+": "+text.text).ToArray(),
            Sprites=Object.FindObjectsByType<UnityEngine.UI.Image>().Where(image=>image.isActiveAndEnabled && image.sprite!=null && (image.sprite.name.StartsWith("Command-",StringComparison.Ordinal) || image.sprite.name.StartsWith("Training-",StringComparison.Ordinal))).Select(image=>image.sprite.name).Distinct().ToArray(),
            VisibleLabels=button!=null?button.GetComponentsInChildren<TextMeshProUGUI>().Select(text=>text.text).ToArray():Array.Empty<string>(),
            VisibleTextY=button!=null?button.GetComponentsInChildren<TextMeshProUGUI>().Select(text=>text.rectTransform.anchoredPosition.y).ToArray():Array.Empty<float>()
        };
        string records=button!=null && button.name=="TrainingCommand"?CountryCommandMotifsBuilder.TrainingRecords:CountryCommandMotifsBuilder.Records;
        Directory.CreateDirectory(records);string path=$"{records}/Current-{width}x{height}";
        File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));ScreenCapture.CaptureScreenshot(Path.GetFullPath(path+".png"));
    }
    [Serializable] private sealed class Setup { public string Project;public int Commands,SharedSprites,ScrollEnds; }
    [Serializable] private sealed class CaptureReport {
        public int Width,Height,Rows,VisibleCommands;public string Page,Label,Message;public bool Interactable,NormalVisible,TintPartsMatch,Running,ExpeditionDetailsOpen;
        public float FaceY,FaceScale,TextY;public string[] TextOverflows,Sprites,RowPaths,VisibleLabels;
        public float[] VisibleTextY;
    }
}
