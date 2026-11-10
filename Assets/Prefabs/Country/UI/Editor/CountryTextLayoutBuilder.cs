using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;

public static class CountryTextLayoutBuilder
{
    [MenuItem("WarSim/Country/Set Up Text Layout")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before changing text layout.");
        foreach(string name in new[] { "CountryRosterRow","CountryRosterView","CountryRosterSelection","CountryTraining","CountryPersonnel","CountryGovernment","CountryExpeditionRow","CountryTopBar","CountryMinimap","CountryFacilityBanner","CountryBudgetCommand","CountryTrainingCommand","CountryConstructionCommand","CountryDefense" })
            SavePrefab(name,root=>Apply(name,root));
    }

    internal static void Apply(string name,GameObject root)
    {
        switch(name)
        {
            case "CountryRosterRow":
                Layout((RectTransform)root.transform,152);
                TopFill(root.transform,"Name",24,54,120,28);
                var state=TopFill(root.transform,"State",78,44,120,28).GetComponent<TextMeshProUGUI>();state.textWrappingMode=TextWrappingModes.Normal;
                var portrait=(RectTransform)root.transform.Find("Portrait");portrait.anchoredPosition=new Vector2(12,-32);Record(portrait);
                Record(root.GetComponent<UnityEngine.UI.LayoutElement>());Record(root.transform.Find("Name"));Record(state);Record(state.transform);break;
            case "CountryRosterView": Roster(root.transform);break;
            case "CountryTraining":
                foreach(Transform school in root.transform.Find("Schools"))
                    if(school.GetComponent<UnityEngine.UI.Button>()!=null)Group(school,"Label",16,16);
                break;
            case "CountryRosterSelection": Inset(root.transform.Find("Body/Roster"),20,12,20,12);break;
            case "CountryPersonnel": Inset(root.transform.Find("Window/Shell/Main/Roster"),20,12,20,12);break;
            case "CountryExpeditionRow":
                var title=(RectTransform)root.transform.Find("Name");title.anchoredPosition=new Vector2(title.anchoredPosition.x,-20);title.sizeDelta=new Vector2(title.sizeDelta.x,54);Record(title);
                break;
            case "CountryFacilityBanner": Banner(root);break;
            case "CountryTopBar": TopBar(root.transform);break;
            case "CountryMinimap": Minimap(root.transform);break;
            case "CountryGovernment":
                foreach(Transform tab in root.transform.Find("Window/Shell/Header/Navigation"))Group(tab,"TabLabel",24,24);
                var symbol=new SerializedObject(root.GetComponent<CountryGovernmentPanel>()).FindProperty("_constructionSymbol").objectReferenceValue as CountryFacilitySymbol;
                var rect=Top(symbol.transform.parent,symbol.name,8,-28,72,72);Record(rect);
                break;
            case "CountryBudgetCommand": Group(root.transform,"Label",32,68);break;
            case "CountryTrainingCommand": Group(root.transform,"Label",32,48);break;
            case "CountryConstructionCommand": Group(root.transform,"Label",64,64);break;
            case "CountryDefense":
                var notice=(RectTransform)root.transform.Find("Window/Shell/Alert/Card/Info/Text");
                notice.offsetMax=new Vector2(-24,notice.offsetMax.y);Record(notice);break;
        }
    }

    private static void Roster(Transform root)
    {
        var footer=BottomFill(root.Find("Right"),"Actions",120);footer.anchorMin=new Vector2(.3f,0);footer.offsetMin=new Vector2(16,0);Record(footer);
        var button=Right(footer,"Action",0,4,488,112);Record(button);
        var label=button.Find("Label").GetComponent<TextMeshProUGUI>();label.rectTransform.offsetMin=new Vector2(92,12);label.rectTransform.offsetMax=new Vector2(-28,-12);Record(label.rectTransform);
        Inset(footer.Find("Hint"),16,16,512,16);
        var body=(RectTransform)root.Find("Right/Body");body.offsetMax=new Vector2(body.offsetMax.x,-112);Record(body);
        var details=(RectTransform)root.Find("Right/Body/Details");details.offsetMin=new Vector2(details.offsetMin.x,112);Record(details);
        var content=root.Find("Right/Body/Details/Viewport/Content").GetComponent<UnityEngine.UI.VerticalLayoutGroup>();content.padding=new RectOffset(12,12,8,16);Record(content);
        Group(button,"Label",28,28);
    }

    private static void Group(Transform button,string label,float left,float right)
    {
        var component=Add<CountryButtonContent>(button.gameObject);
        Set(component,("_icon",button.Find("Icon") as RectTransform),("_label",button.Find(label).GetComponent<TextMeshProUGUI>()));
        var data=new SerializedObject(component);data.FindProperty("_sidePadding").vector2Value=new Vector2(left,right);data.ApplyModifiedPropertiesWithoutUndo();Record(component);
    }

    internal static void Banner(GameObject root)
    {
        var label=root.transform.Find("Face/Label").GetComponent<TextMeshProUGUI>();
        label.rectTransform.offsetMin=new Vector2(88,0);label.rectTransform.offsetMax=new Vector2(-24,0);Record(label.rectTransform);
    }

    private static void TopBar(Transform root)
    {
        var controls=root.Find("Content/DateControls");
        CountryTimeBuilder.ConfigureLayout(controls);
        var kingdom=root.Find("Content/KingdomName").GetComponent<UnityEngine.UI.LayoutElement>();kingdom.minWidth=kingdom.preferredWidth=284;Record(kingdom);
        foreach(string name in new[] { "Population","TaxIncome","Wood","Ore" })
        {
            var item=root.Find("Content/"+name);var width=item.GetComponent<UnityEngine.UI.LayoutElement>();width.minWidth=width.preferredWidth=236;width.flexibleWidth=0;Record(width);
            var icon=item.Find("Icon").GetComponent<UnityEngine.UI.LayoutElement>();icon.minWidth=icon.preferredWidth=56;Record(icon);
            var gap=Child(root.Find("Content"),name+"Spacing");
            var space=Add<UnityEngine.UI.LayoutElement>(gap.gameObject);space.minWidth=space.preferredWidth=0;space.flexibleWidth=1;Record(space);
            gap.SetSiblingIndex(item.GetSiblingIndex()+1);Record(gap);
        }
    }

    internal static void Minimap(Transform root)
    {
        var map=root.Find("MapFrame/MapArea/Map");var north=(RectTransform)map.Find("North");
        north.anchoredPosition=new Vector2(-8,-8);north.sizeDelta=new Vector2(32,40);Record(north);
        var arrow=(RectTransform)map.Find("NorthArrow");arrow.anchoredPosition=new Vector2(-24,-54);Record(arrow);
    }

    private static void Inset(Transform transform,float left,float top,float right,float bottom)
    {
        var rect=(RectTransform)transform;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);Record(rect);
    }
    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
