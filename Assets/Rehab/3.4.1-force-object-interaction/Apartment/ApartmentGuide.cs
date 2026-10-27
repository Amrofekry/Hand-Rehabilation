using System;
using UnityEngine;
using UnityEngine.UI;
namespace GripRehab {
/// <summary>Stage 11 apartment HUD additions on the design system:
/// (1) an input-mode strip that always says which input drives the hand and pointer, and the state of gaze,
/// voice and the assistant (off until Stages 13-15 wire them through <see cref="Set"/>); state is written in
/// words beside a dot, never by colour alone;
/// (2) first-run onboarding that coaches the basics in place, step by step with a progress bar, and never
/// blocks play. It completes from what the player actually does; H shows it again.</summary>
public sealed class ApartmentGuide:MonoBehaviour {
 public enum Channel{Input,Pointer,Gaze,Voice,Assistant}
 public const string DoneKey="GripRehab.Onboarding.Apartment";
 static readonly string[] names={"INPUT","POINTER","GAZE","VOICE","ASSISTANT"};
 static readonly string[] overrideState=new string[5];static readonly bool[] overrideOn=new bool[5];static readonly bool[] overridden=new bool[5];
 /// <summary>Later stages report their live state here (for example Set(Channel.Voice,true,"LISTENING")).</summary>
 public static void Set(Channel c,bool active,string state){overridden[(int)c]=true;overrideOn[(int)c]=active;overrideState[(int)c]=state;}
 public static void Clear(Channel c)=>overridden[(int)c]=false;

 public static readonly string[] Steps={
  "Look around: move the mouse, or turn with the camera hand.",
  "Pick up the object: look at it and press E, or close your hand.",
  "Squeeze it: hold Space or the left mouse button, or squeeze the sensor.",
  "Put it down: look at the table and press Q, or open your hand.",
  "Press TAB for options: room, object, input and the six activities."};
 public int Step{get;private set;}public bool Showing{get;private set;}
 public string ChannelText(Channel c)=>chips!=null?chipLabels[(int)c].text:"";

 ApartmentExperience app;ApartmentChoices choices;ActivityRunner runner;RehabDwellPointer pointer;
 GripRehab.UI.GripUiThemeController presentation;
 RectTransform strip,coach;Text[] chipLabels;Image[] dots;GameObject[] chips;Text coachTitle,coachBody;Image coachFill;
 float startYaw,doneUntil=-1;bool grabbed,released,squeezed;int placed;Transform hudHost;
 public RectTransform Strip=>strip;
 // Sit 12 units above the tallest card along the bottom edge (cards anchored there differ by aspect ratio).
 void PlaceStrip(){
  var canvas=strip.GetComponentInParent<Canvas>();if(canvas==null||canvas.renderMode!=RenderMode.ScreenSpaceOverlay)return;
  float top=0;var c=new Vector3[4];
  foreach(Transform child in hudHost){
   if(child==strip||child==coach||!child.gameObject.activeInHierarchy||!(child is RectTransform r)||child.GetComponent<Image>()==null)continue;
   r.GetWorldCorners(c);if(c[0].y<Screen.height*.3f&&c[1].y<Screen.height*.5f)top=Mathf.Max(top,c[1].y);
  }
  var p=strip.position;strip.position=new Vector3(Screen.width*.5f,top+12*canvas.scaleFactor,p.z);
 }

 public static ApartmentGuide Ensure(ApartmentExperience app,ApartmentChoices choices){
  var g=app.GetComponent<ApartmentGuide>();if(g==null){g=app.gameObject.AddComponent<ApartmentGuide>();g.app=app;g.choices=choices;}
  return g;
 }
 void Start(){
  runner=GetComponent<ActivityRunner>();pointer=FindAnyObjectByType<RehabDwellPointer>();presentation=FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();
  var canvas=choices!=null?choices.GetComponentInParent<Canvas>():null;if(canvas==null)return;
  var host=HudHost(canvas.transform);FitTopBar(canvas.transform);
  BuildStrip(host);BuildCoach(host);
  if(presentation!=null){presentation.RegisterPresentation(strip);presentation.RegisterPresentation(coach);}
  hudHost=host;
  app.ObjectGrabbed+=_=>grabbed=true;app.ObjectReleased+=_=>released=true;
  bool done=false;try{done=PlayerPrefs.GetInt(DoneKey,0)==1;}catch(Exception){}
  Restart(!done);
 }
 // Stage 11: at 4:3 the canvas is narrower than the 16:9 layout, and the breadcrumb and MAIN MENU (placed from
 // the bar's centre) were cut off. Pin them to the bar's own left and right edges instead.
 static void FitTopBar(Transform canvas){
  foreach(var t in canvas.GetComponentsInChildren<Text>(true)){
   if(!t.text.StartsWith("GRIP REHAB")||!t.text.Contains("/")||t.GetComponentInParent<Button>()!=null)continue;
   var bar=t.transform.parent as RectTransform;if(bar==null)continue;
   var r=t.rectTransform;float w=r.rect.width;r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(32,0);r.sizeDelta=new Vector2(w,r.rect.height);
   foreach(var b in bar.GetComponentsInChildren<Button>(true)){var br=(RectTransform)b.transform;if(br.parent!=bar)continue;float bw=br.rect.width,bh=br.rect.height;br.anchorMin=br.anchorMax=new Vector2(1,.5f);br.pivot=new Vector2(1,.5f);br.anchoredPosition=new Vector2(-16,0);br.sizeDelta=new Vector2(bw,bh);}
   return;
  }
 }
 // The HUD cards (QUICK CONTROLS and the others) share a parent that the home screen hides; use it.
 static Transform HudHost(Transform canvas){
  foreach(var t in canvas.GetComponentsInChildren<Text>(true))if(t.text.Trim()=="QUICK CONTROLS"){var card=t.transform.parent;return card!=null&&card.parent!=null?card.parent:canvas;}
  return canvas;
 }
 public void Restart(bool show){Step=0;grabbed=released=squeezed=false;startYaw=app.player.transform.eulerAngles.y;Showing=show;doneUntil=-1;}

 void Update(){
  if(app==null||strip==null)return;
  if(placed<3){PlaceStrip();placed++;}
  UpdateStrip();
  if(app.player.GameplayInputAllowed&&Input.GetKeyDown(KeyCode.H))Restart(!Showing);
  bool activity=runner!=null&&runner.Current!=null;
  if(Showing){
   switch(Step){
    case 0:if(Mathf.Abs(Mathf.DeltaAngle(startYaw,app.player.transform.eulerAngles.y))>20)Advance();break;
    case 1:if(grabbed)Advance();break;
    case 2:if(app.Held!=null&&app.ForceValid&&app.ForceNewtons>5)squeezed=true;if(squeezed)Advance();break;
    case 3:if(released)Advance();break;
    case 4:if(app.player.Paused)Finish();break;
   }
  }
  bool visible=(Showing||Time.unscaledTime<doneUntil)&&!activity&&!app.player.Paused;
  if(coach.gameObject.activeSelf!=visible)coach.gameObject.SetActive(visible);
  if(!visible)return;
  bool done=!Showing;
  coachTitle.text=done?"YOU ARE READY":"GETTING STARTED  -  STEP "+(Step+1)+" OF "+Steps.Length;
  coachBody.text=done?"Press TAB any time to choose a room, an object or an activity. H shows this guide again.":Steps[Step]+"\nH hides this guide.";
  ((RectTransform)coachFill.transform).sizeDelta=new Vector2((coach.rect.width-48)*(done?1:Step/(float)Steps.Length),0);
 }
 void Advance(){Step++;RehabAudio.Play(RehabSound.Chime);}
 void Finish(){Showing=false;doneUntil=Time.unscaledTime+4;try{PlayerPrefs.SetInt(DoneKey,1);PlayerPrefs.Save();}catch(Exception){}}

 void UpdateStrip(){
  for(int i=0;i<5;i++){
   bool on;string state;
   if(overridden[i]){on=overrideOn[i];state=overrideState[i];}
   else switch((Channel)i){
    case Channel.Input:on=true;state=app.CameraMode?"CAMERA HAND":app.Hardware?"FORCE SENSOR":"KEYBOARD DEMO";break;
    case Channel.Pointer:on=true;state=pointer!=null&&pointer.PointingDetected?"HAND POINTING":"MOUSE";break;
    default:on=false;state="OFF";break;
   }
   string text=names[i]+"  "+state;if(chipLabels[i].text!=text)chipLabels[i].text=text;
   var c=dots[i].color;float a=on?1:.28f;if(Mathf.Abs(c.a-a)>.01f){c.a=a;dots[i].color=c;}
  }
 }

 void BuildStrip(Transform host){
  // Bottom centre, just above the bottom HUD cards (placed by measurement in PlaceStrip, so it fits every aspect ratio).
  strip=Panel("Input mode strip",host,new Vector2(.5f,0),new Vector2(0,160),new Vector2(560,36));
  var row=strip.gameObject.AddComponent<HorizontalLayoutGroup>();row.padding=new RectOffset(14,14,6,6);row.spacing=14;row.childAlignment=TextAnchor.MiddleCenter;row.childForceExpandWidth=false;row.childControlWidth=true;row.childControlHeight=true;
  var fit=strip.gameObject.AddComponent<ContentSizeFitter>();fit.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
  chips=new GameObject[5];chipLabels=new Text[5];dots=new Image[5];
  for(int i=0;i<5;i++){
   var chip=new GameObject(names[i]+" chip",typeof(RectTransform));chip.transform.SetParent(strip,false);chips[i]=chip;
   var h=chip.AddComponent<HorizontalLayoutGroup>();h.spacing=6;h.childAlignment=TextAnchor.MiddleLeft;h.childControlWidth=true;h.childControlHeight=true;h.childForceExpandWidth=false;
   var dot=new GameObject("Dot",typeof(RectTransform),typeof(Image),typeof(LayoutElement));dot.transform.SetParent(chip.transform,false);
   var le=dot.GetComponent<LayoutElement>();le.preferredWidth=le.preferredHeight=10;le.minWidth=le.minHeight=10;
   dots[i]=dot.GetComponent<Image>();dots[i].color=new Color(0,.94f,1);dots[i].raycastTarget=false;
   var tag=dot.AddComponent<GripRehab.UI.GripUiRoleTag>();tag.role=GripRehab.UI.GripUiRole.Accent;
   chipLabels[i]=Label(chip.transform,12,FontStyle.Bold);chipLabels[i].horizontalOverflow=HorizontalWrapMode.Overflow;
  }
 }
 void BuildCoach(Transform host){
  coach=Panel("Onboarding coach",host,new Vector2(.5f,1),new Vector2(0,-82),new Vector2(640,124));
  coachTitle=Label(coach,17,FontStyle.Bold);Place(coachTitle.rectTransform,24,-14,592,26);
  coachBody=Label(coach,15,FontStyle.Normal);Place(coachBody.rectTransform,24,-44,592,52);
  var bar=new GameObject("Progress",typeof(RectTransform),typeof(Image));bar.transform.SetParent(coach,false);Place((RectTransform)bar.transform,24,-104,592,6);bar.GetComponent<Image>().color=new Color(1,1,1,.14f);
  var f=new GameObject("Fill",typeof(RectTransform),typeof(Image));f.transform.SetParent(bar.transform,false);
  var fr=(RectTransform)f.transform;fr.anchorMin=Vector2.zero;fr.anchorMax=new Vector2(0,1);fr.pivot=new Vector2(0,.5f);fr.sizeDelta=Vector2.zero;
  coachFill=f.GetComponent<Image>();coachFill.color=new Color(0,.94f,1);
  coach.gameObject.SetActive(false);
 }
 static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size){
  var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
  var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;
  var img=go.GetComponent<Image>();img.color=new Color(.125f,.184f,.263f,.92f);img.raycastTarget=false;
  return r;
 }
 Text Label(Transform parent,int size,FontStyle style){
  var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
  var t=go.GetComponent<Text>();t.font=presentation!=null&&presentation.Current!=null&&presentation.Current.latinFont!=null?presentation.Current.latinFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  t.fontSize=size;t.fontStyle=style;t.color=Color.white;t.raycastTarget=false;t.alignment=TextAnchor.MiddleLeft;return t;
 }
 static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
}
}
