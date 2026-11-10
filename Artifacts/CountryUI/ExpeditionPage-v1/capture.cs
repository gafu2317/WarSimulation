var type=System.Reflection.Assembly.Load("EditModeTests").GetType("CountryExpeditionPageTests",true);
var fixture=System.Activator.CreateInstance(type);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var reports=new System.Collections.Generic.List<object>();
try {
 type.GetMethod("SetUp").Invoke(fixture,null);
 var panel=(CountryExpeditionPanel)type.GetField("_panel",flags).GetValue(fixture);
 var camera=(Camera)type.GetField("_camera",flags).GetValue(fixture);
 var hud=(GameObject)type.GetField("_hud",flags).GetValue(fixture);
 var page=(UnityEngine.UI.ScrollRect)type.GetField("_page",flags).GetValue(fixture);
 var view=(CountryRosterView)type.GetField("_view",flags).GetValue(fixture);
 var list=(UnityEngine.UI.ScrollRect)type.GetField("_list",flags).GetValue(fixture);
 var target=(RenderTexture)type.GetField("_target",flags).GetValue(fixture);
 var destination=(int)type.GetField("_destination",flags).GetValue(fixture);
 System.Action<string> shot=name=>{
  type.GetMethod("Layout",flags).Invoke(fixture,null);
  foreach(var label in hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>())label.ForceMeshUpdate();
  camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
  var image=new Texture2D(844,390,TextureFormat.RGB24,false);
  try{image.ReadPixels(new Rect(0,0,844,390),0,0);image.Apply();System.IO.File.WriteAllBytes("Artifacts/CountryUI/ExpeditionPage-v1/"+name+"-844x390.png",image.EncodeToPNG());}
  finally {UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
  reports.Add(new {name,page=page.verticalNormalizedPosition,target=panel.Draft.Target,members=panel.Draft.Members.ToArray(),overflow=panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Where(t=>t.isTextOverflowing && t.overflowMode!=TMPro.TextOverflowModes.Ellipsis).Select(t=>new {t.name,t.text}).ToArray()});
 };
 shot("Map-Unselected");panel.SelectRegion(destination);shot("Map-Selected");
 var map=(RectTransform)page.content.Find("Map");var plane=(RectTransform)map.Find("MapArea/Plane");var formation=(RectTransform)page.content.Find("Formation");
 var geometry=new {mapPlane=new[]{plane.rect.width,plane.rect.height},mapSection=new[]{map.rect.width,map.rect.height},formationSection=new[]{formation.rect.width,formation.rect.height}};
 System.IO.File.WriteAllText("Artifacts/CountryUI/ExpeditionPage-v1/after-layout.json",Newtonsoft.Json.JsonConvert.SerializeObject(geometry,Newtonsoft.Json.Formatting.Indented));
 page.verticalNormalizedPosition=.5f;shot("Page-Middle");
 page.verticalNormalizedPosition=0;
 var action=(UnityEngine.UI.Button)typeof(CountryRosterView).GetField("_action",flags).GetValue(view);
 foreach(int id in new[]{0,7,25}){view.SelectSoldier(id);action.onClick.Invoke();}
 shot("Formation-Selected");
 var equipment=(UnityEngine.UI.Button)typeof(CountryExpeditionPanel).GetField("_equipment",flags).GetValue(panel);
 equipment.onClick.Invoke();shot("Equipment");equipment.onClick.Invoke();
 var depart=(UnityEngine.UI.Button)typeof(CountryExpeditionPanel).GetField("_depart",flags).GetValue(panel);
 depart.onClick.Invoke();shot("Departure-Confirmation");
 System.IO.File.WriteAllText("Artifacts/CountryUI/ExpeditionPage-v1/captures.json",Newtonsoft.Json.JsonConvert.SerializeObject(reports,Newtonsoft.Json.Formatting.Indented));
 return new {geometry,captures=reports};
} finally{type.GetMethod("TearDown").Invoke(fixture,null);}
