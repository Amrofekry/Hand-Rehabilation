using UnityEngine;
using UnityEngine.UI;
namespace GripRehab {
/// <summary>Stage 11: lays out the session options (pause) panel on the design-system grid: a title, four
/// numbered sections (room, object, input and hand, activity) each with its label above its row, and RESUME.
/// Sizes come from the theme tokens; nothing overlaps at any of the tested resolutions.</summary>
public static class ApartmentOptionsLayout {
 public const float Pad=32,Gap=12,LabelHeight=20,ChipHeight=40,ButtonHeight=48,ResumeHeight=52,SectionGap=20;
 public static void Apply(ApartmentChoices c,Button[] activities,GripRehab.UI.GripUiThemeController presentation){
  var panel=(RectTransform)c.inputButton.transform.parent;float w=panel.rect.width>10?panel.rect.width:panel.sizeDelta.x;
  Text label01=null,label02=null,label03=null,title=null;
  foreach(var t in panel.GetComponentsInChildren<Text>(true)){
   if(t.GetComponentInParent<Button>()!=null)continue;
   var s=t.text.Trim();
   if(s.StartsWith("01"))label01=t;else if(s.StartsWith("02"))label02=t;else if(s.StartsWith("03"))label03=t;else if(s=="SESSION OPTIONS")title=t;
  }
  Text label04=null;
  if(label03!=null){label04=Object.Instantiate(label03,label03.transform.parent);label04.name="04 activity label";label04.text="04  /  ACTIVITY (STARTS AT ONCE)";if(presentation!=null)presentation.RegisterPresentation(label04.transform);}
  float y=-Pad;
  if(title!=null){Place(title.rectTransform,Pad,y,w-2*Pad,36,true);title.alignment=TextAnchor.MiddleLeft;y-=36+SectionGap;}
  y=Section(label01,new Button[]{c.livingButton,c.kitchenButton,c.labButton,c.materialLabButton},ChipHeight,14,y,w,presentation);
  y=Section(label02,c.objectButtons,ButtonHeight,16,y,w,presentation);
  y=Section(label03,new[]{c.inputButton,c.handButton},ChipHeight+4,15,y,w,presentation);
  activityRowY=y-(label04!=null?LabelHeight+8:0);
  y=Section(label04,activities,ChipHeight+4,15,y,w,presentation);
  y-=4;
  Row(new[]{c.resumeButton},ResumeHeight,17,y,w,presentation);y-=ResumeHeight+Pad;
  panel.sizeDelta=new Vector2(panel.sizeDelta.x,-y);
 }
 static float activityRowY=float.NaN;
 /// <summary>Lays out the activity row again after a room change shows or hides a button (only the shown ones share the row).</summary>
 public static void LayoutActivities(ApartmentChoices c,Button[] activities){
  if(float.IsNaN(activityRowY)||c==null||c.inputButton==null)return;
  var panel=(RectTransform)c.inputButton.transform.parent;float w=panel.rect.width>10?panel.rect.width:panel.sizeDelta.x;
  Row(activities,ChipHeight+4,15,activityRowY,w,Object.FindAnyObjectByType<GripRehab.UI.GripUiThemeController>());
 }
 static float Section(Text label,Button[] row,float height,int font,float y,float w,GripRehab.UI.GripUiThemeController presentation){
  if(label!=null){Place(label.rectTransform,Pad,y,w-2*Pad,LabelHeight,true);label.alignment=TextAnchor.LowerLeft;label.resizeTextForBestFit=false;if(presentation!=null)presentation.SetBaseFontSize(label,13);else label.fontSize=13;y-=LabelHeight+8;}
  Row(row,height,font,y,w,presentation);return y-height-SectionGap;
 }
 static void Row(Button[] row,float height,int font,float y,float w,GripRehab.UI.GripUiThemeController presentation){
  int n=0;foreach(var b in row)if(b!=null&&b.gameObject.activeSelf)n++;if(n==0)return;
  float bw=(w-2*Pad-(n-1)*Gap)/n;int i=0;
  foreach(var b in row){
   if(b==null||!b.gameObject.activeSelf)continue;
   Place((RectTransform)b.transform,Pad+i*(bw+Gap),y,bw,height,true);i++;
   GripRehab.UI.GripUiControlFeedback.Attach(Object.FindAnyObjectByType<GripRehab.UI.GripUiAccessibility>(),b);
   var t=b.GetComponentInChildren<Text>(true);if(t==null)continue;
   var r=t.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=new Vector2(8,4);r.offsetMax=new Vector2(-8,-4);
   t.alignment=TextAnchor.MiddleCenter;t.resizeTextForBestFit=true;t.resizeTextMinSize=11;t.fontStyle=FontStyle.Bold;
   if(presentation!=null)presentation.SetBaseFontSize(t,font);else t.fontSize=font;t.resizeTextMaxSize=t.fontSize;
  }
 }
 // Top-left anchored placement inside the panel, x and y measured from its top-left corner.
 static void Place(RectTransform r,float x,float y,float width,float height,bool topLeft){
  r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);
 }
}
}
