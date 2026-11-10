using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using static CountryGovernmentBuilder;
using Object = UnityEngine.Object;
using Theme = CountryGovernmentTheme;

public static class CountryRosterArtBuilder
{
    internal const string Atlas = "Assets/Images/UI/Country/Roster/RosterParts-v1.png";
    internal static bool Available => AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().Count() == 5;
    private static Sprite Part(string name) => AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().Single(sprite => sprite.name == name);

    [MenuItem("WarSim/Country/Set Up Roster Artwork")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating roster artwork.");
        Import();
        SavePrefab("CountryRosterRow", Row);
        SavePrefab("CountrySoldierToken", Token);
        SavePrefab("CountryTrainingEmptySlot", Empty);
        SavePrefab("CountryRosterView", View);
        Debug.Log("Shared roster artwork updated without saving or reloading the scene.");
    }

    private static void Import()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.isReadable = true; importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
        int width=texture.width,height=texture.height;
        var pixels = texture.GetPixels32();
        var windows = new[] { new Rect(0,.71f,.5f,.29f), new Rect(.5f,.71f,.5f,.29f), new Rect(0,0,.33f,.70f), new Rect(.34f,0,.33f,.70f), new Rect(.68f,0,.32f,.70f) };
        string[] names = { "Roster-RowFace", "Roster-RowFrame", "Roster-IconRing", "Roster-EmptySlot", "Roster-PortraitFrame" };
        var regions = new SpriteRect[names.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            var window = windows[i];
            int minX=texture.width, minY=texture.height, maxX=0, maxY=0;
            for (int y=Mathf.FloorToInt(window.yMin*texture.height);y<Mathf.FloorToInt(window.yMax*texture.height);y++)
                for (int x=Mathf.FloorToInt(window.xMin*texture.width);x<Mathf.FloorToInt(window.xMax*texture.width);x++)
                    if (pixels[y*texture.width+x].a > 64)
                    { minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y); }
            if (minX>maxX || minY>maxY) throw new InvalidOperationException("Missing roster sprite: "+names[i]);
            var rect = Rect.MinMaxRect(Mathf.Max(0,minX-4),Mathf.Max(0,minY-4),Mathf.Min(texture.width,maxX+5),Mathf.Min(texture.height,maxY+5));
            byte centerAlpha = pixels[(int)rect.center.y*texture.width+(int)rect.center.x].a;
            if (i>0 && centerAlpha>32) throw new InvalidOperationException("Roster frames require transparent centers: "+names[i]);
            regions[i] = new SpriteRect { name=names[i],rect=rect,border=i<2 ? Vector4.one*48 : i==4 ? Vector4.one*64 : Vector4.zero };
        }
        CountryButtonSpriteImporter.Import(Atlas,regions);
        Directory.CreateDirectory("Artifacts/CountryUI/RosterArt-v1");
        File.WriteAllText("Artifacts/CountryUI/RosterArt-v1/slicing.json",JsonUtility.ToJson(new SliceReport { Width=width,Height=height,Sprites=regions.Select(region=>new Slice { Name=region.name,Rect=region.rect,Border=region.border }).ToArray() },true));
    }

    internal static void Row(GameObject root)
    {
        var button = root.GetComponent<UnityEngine.UI.Button>();
        Face(button);
        var surface = button.targetGraphic.rectTransform;
        var selected = (RectTransform)root.transform.Find("Selected");
        Match(selected,surface,2);
        Picture(selected,"Roster-RowFrame",Theme.Accent,true);
        var divider = root.transform.Find("Divider"); if (divider!=null) divider.gameObject.SetActive(false);
        var portrait = (RectTransform)root.transform.Find("Portrait");
        var clip = (RectTransform)portrait.Find("Clip");clip.offsetMin=Vector2.one*8;clip.offsetMax=Vector2.one*-8;
        Picture(Fill(portrait,"Edge"),"Roster-IconRing",Theme.Border);
        Focus(button);
        CountryButtonFeedbackBuilder.Configure(button);
    }

    internal static void Token(GameObject root)
    {
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(128,128);Layout(rect,128);Column(rect,128,0);
        root.transform.Find("Surface").gameObject.SetActive(false);
        root.transform.Find("Name").gameObject.SetActive(false);
        root.transform.Find("Outline").gameObject.SetActive(false);
        root.transform.Find("Divider").gameObject.SetActive(false);
        var portrait=Top(rect,"Portrait",20,4,88,88);
        var clip=(RectTransform)portrait.Find("Clip");clip.offsetMin=Vector2.one*8;clip.offsetMax=Vector2.one*-8;
        Picture(Fill(portrait,"Edge"),"Roster-IconRing",Theme.Border);
        var button=root.GetComponent<UnityEngine.UI.Button>();
        var background=portrait.GetComponent<CountryCircleGraphic>();background.color=Color.white;button.targetGraphic=background;
        var selected=Top(rect,"Selected",20,4,88,88);Picture(selected,"Roster-IconRing",Theme.Accent);
        var focus=Top(rect,"Focus",17,1,94,94);var focusImage=Picture(focus,"Roster-IconRing",Theme.Border);focusImage.enabled=false;
        focus.SetAsLastSibling();selected.SetAsLastSibling();
        var caption=root.transform.Find("State").GetComponent<TMPro.TextMeshProUGUI>();
        TopFill(rect,"State",92,36,4,4);caption.fontSize=22;caption.alignment=TMPro.TextAlignmentOptions.Center;
        caption.overflowMode=TMPro.TextOverflowModes.Ellipsis;caption.gameObject.SetActive(false);
        CountryButtonFeedbackBuilder.Configure(button);
    }

    internal static void Empty(GameObject root)
    {
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(128,128);Layout(rect,128);Column(rect,128,0);
        Picture(Top(rect,"Empty",20,4,88,88),"Roster-EmptySlot",Theme.Border);
    }

    internal static void View(GameObject root)
    {
        var frame = root.transform.Find("Right/Body/Portrait/Frame");
        Picture(Fill(frame,"Surface"),"Roster-RowFace",Theme.Recess,true);
        foreach (string name in new[] { "Art", "Placeholder" })
        {
            var content=(RectTransform)frame.Find(name);content.offsetMin=Vector2.one*24;content.offsetMax=Vector2.one*-24;
        }
        var rim = Picture(Fill(frame,"Rim"),"Roster-PortraitFrame",Theme.Border,true);rim.pixelsPerUnitMultiplier=3;
        rim.transform.SetAsLastSibling();
        foreach (var edge in root.GetComponentsInChildren<CountryCircleGraphic>(true).Where(graphic=>graphic.name=="Edge" && graphic.transform.parent.name=="WeaponIcon").ToArray())
            Picture((RectTransform)edge.transform,"Roster-IconRing",Theme.Border);
        var action = root.transform.Find("Right/Actions/Action").GetComponent<UnityEngine.UI.Button>();
        Picture((RectTransform)action.transform.Find("Frame"),"Roster-RowFrame",Theme.Accent,true);
        Picture((RectTransform)action.transform.Find("Shadow"),"Roster-RowFace",Theme.Recess,true);
        Face(action);Focus(action);CountryButtonFeedbackBuilder.Configure(action);
    }

    private static void Face(UnityEngine.UI.Button button)
    {
        var rect = button.targetGraphic.rectTransform;
        button.targetGraphic = Picture(rect,"Roster-RowFace",Color.white,true);
    }
    private static void Focus(UnityEngine.UI.Button button)
    {
        var rect = (RectTransform)button.transform.Find("Focus");
        Match(rect,button.targetGraphic.rectTransform,3);
        var image = Picture(rect,"Roster-RowFrame",Theme.Border,true);image.enabled=false;
    }
    private static void Match(RectTransform rect,RectTransform target,float extra)
    {
        rect.anchorMin=target.anchorMin;rect.anchorMax=target.anchorMax;rect.pivot=target.pivot;
        rect.offsetMin=target.offsetMin-Vector2.one*extra;rect.offsetMax=target.offsetMax+Vector2.one*extra;
    }
    private static UnityEngine.UI.Image Picture(RectTransform rect,string part,Color color,bool sliced=false)
    {
        foreach (var graphic in rect.GetComponents<Graphic>().Where(graphic=>graphic is not UnityEngine.UI.Image).ToArray()) Object.DestroyImmediate(graphic);
        var image=Add<UnityEngine.UI.Image>(rect.gameObject);image.sprite=Part(part);image.color=color;image.raycastTarget=false;
        if (part is "Roster-RowFace" or "Roster-RowFrame")
        {
            if (rect.name == "Shadow") image.color = CountryButtonBorderBuilder.Shadow;
            else image.material = CountryButtonBorderBuilder.Material;
        }
        image.type=sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect=!sliced;image.pixelsPerUnitMultiplier=2;
        return image;
    }
    [Serializable] private sealed class Slice { public string Name;public Rect Rect;public Vector4 Border; }
    [Serializable] private sealed class SliceReport { public int Width,Height;public Slice[] Sprites; }
}
