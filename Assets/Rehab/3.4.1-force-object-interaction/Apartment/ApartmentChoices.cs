using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace GripRehab {
// Saved uGUI controls; subscriptions are paired across every Play/enable cycle.
public sealed class ApartmentChoices:MonoBehaviour {
 public ApartmentExperience experience;
 public Button handButton;
 public Button livingButton,kitchenButton,resumeButton,inputButton;
 public Button[] objectButtons;
 public Text heading,inputLabel;
 public CanvasGroup controls;
 public Transform kitchenStation;
 public SqueezableObject[] livingObjects,kitchenObjects;
 public bool Kitchen{get;private set;}
 // Stage 8 Grip Lab: one code-built prop per grip of the grip chart, on the living-room table.
 [NonSerialized] public SqueezableObject[] labObjects=new SqueezableObject[0];
 [NonSerialized] public Button labButton,materialLabButton;
 // Stage 9 Material Lab: the deformation chart's materials and forms, at the same table.
 [NonSerialized] public SqueezableObject[] materialObjects=new SqueezableObject[0];
 public bool Lab{get;private set;}
 public bool MaterialLab{get;private set;}
 public int LabSelection{get;private set;}
 public int MaterialSelection{get;private set;}
 SqueezableObject[] CurrentLab=>MaterialLab?materialObjects:labObjects;
 int CurrentLabSelection=>MaterialLab?MaterialSelection:LabSelection;
 public int LivingSelection{get;private set;}
 public int KitchenSelection{get;private set;}
 public SqueezableObject SelectedObject=>Lab&&CurrentLab.Length>0?CurrentLab[CurrentLabSelection]:Kitchen?kitchenObjects[KitchenSelection]:livingObjects[LivingSelection];
 private void Awake(){
  if(experience==null||livingObjects==null||livingObjects.Length==0||livingObjects[0]==null)return;
  GripLab.AssignStationGrips(experience.objects);
  var ball=livingObjects[0];var renderer=ball.visual!=null?ball.visual.GetComponentInChildren<Renderer>(true):null;
  var source=renderer!=null?renderer.sharedMaterial:null;
  if(source==null)return;
  // The table top under the station ball: its lowest collider point.
  var col=ball.GetComponent<Collider>();float drop=col is SphereCollider sphere?sphere.radius*ball.transform.lossyScale.y:col!=null?col.bounds.extents.y:.07f;
  var tableTop=ball.transform.position+Vector3.down*drop;
  labObjects=GripLab.Build(ball.transform.parent,tableTop,source);
  materialObjects=GripLab.BuildMaterials(ball.transform.parent,tableTop,source);
  var all=new SqueezableObject[experience.objects.Length+labObjects.Length+materialObjects.Length];experience.objects.CopyTo(all,0);
  labObjects.CopyTo(all,experience.objects.Length);materialObjects.CopyTo(all,experience.objects.Length+labObjects.Length);experience.objects=all;
  // Station buttons: LIVING ROOM, KITCHEN, GRIP LAB, MATERIAL LAB.
  labButton=Instantiate(kitchenButton,kitchenButton.transform.parent);labButton.name="GRIP LAB";
  materialLabButton=Instantiate(kitchenButton,kitchenButton.transform.parent);materialLabButton.name="MATERIAL LAB";
  var buttons=new[]{livingButton,kitchenButton,labButton,materialLabButton};
  for(int i=0;i<buttons.Length;i++){
   var rect=(RectTransform)buttons[i].transform;rect.sizeDelta=new Vector2(100,35);rect.anchoredPosition=new Vector2(-159+106*i,rect.anchoredPosition.y);
   var label=buttons[i].GetComponentInChildren<Text>();var labelRect=label.rectTransform;labelRect.sizeDelta=new Vector2(96,31);labelRect.anchoredPosition=new Vector2(0,-17.5f);label.fontSize=13;
  }
  labButton.GetComponentInChildren<Text>().text="GRIP LAB";materialLabButton.GetComponentInChildren<Text>().text="MATERIAL LAB";
  // Stage 10: the six multi-step activities (living room and kitchen).
  ActivityRunner.Ensure(experience,this,source);
  // Stage 11: input-mode strip and first-run onboarding.
  ApartmentGuide.Ensure(experience,this);
 }
 private UnityAction[] callbacks;
 private RehabAppShell shell;
 private GripRehab.UI.GripUiThemeController presentation;
 private GripRehab.UI.GripUiTheme lastPalette;
 private void OnEnable(){
  if(labButton!=null)labButton.onClick.AddListener(SelectLab);
  if(materialLabButton!=null)materialLabButton.onClick.AddListener(SelectMaterialLab);
  if(handButton!=null)handButton.onClick.AddListener(ToggleHand);livingButton.onClick.AddListener(Living);kitchenButton.onClick.AddListener(KitchenRoom);resumeButton.onClick.AddListener(Resume);inputButton.onClick.AddListener(ToggleInput);
  callbacks=new UnityAction[objectButtons.Length];for(int i=0;i<objectButtons.Length;i++){int choice=i;callbacks[i]=()=>SelectObject(choice);objectButtons[i].onClick.AddListener(callbacks[i]);}
 }
 private void OnDisable(){
  if(labButton!=null)labButton.onClick.RemoveListener(SelectLab);
  if(materialLabButton!=null)materialLabButton.onClick.RemoveListener(SelectMaterialLab);
  if(handButton!=null)handButton.onClick.RemoveListener(ToggleHand);livingButton.onClick.RemoveListener(Living);kitchenButton.onClick.RemoveListener(KitchenRoom);resumeButton.onClick.RemoveListener(Resume);inputButton.onClick.RemoveListener(ToggleInput);
  if(callbacks!=null)for(int i=0;i<objectButtons.Length;i++)objectButtons[i].onClick.RemoveListener(callbacks[i]);
 }
 private void Start(){shell=FindAnyObjectByType<RehabAppShell>();presentation=FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();if(presentation!=null)presentation.PresentationApplied+=Refresh;experience.SelectHand(false);experience.player.SetPaused(true);Refresh();}
 private void OnDestroy(){if(presentation!=null)presentation.PresentationApplied-=Refresh;}
 private void ToggleHand(){experience.SelectHand(!experience.LeftSelected);Refresh();}
 private void Update(){
  if(presentation!=null&&lastPalette!=presentation.Current)Refresh();
  // [ and ] step through the grip chart while in the Grip Lab (paused or playing).
  if(Lab&&(shell==null||!shell.BlocksGameplay)){
   if(Input.GetKeyDown(KeyCode.RightBracket))SelectLabGrip(CurrentLabSelection+1);
   if(Input.GetKeyDown(KeyCode.LeftBracket))SelectLabGrip(CurrentLabSelection-1);
  }
  if(shell!=null&&shell.BlocksGameplay){controls.interactable=false;controls.blocksRaycasts=false;controls.alpha=0;return;}
  // ApartmentPlayer owns both Escape and Tab before gameplay consumers update.
  controls.interactable=experience.player.Paused;controls.blocksRaycasts=experience.player.Paused;controls.alpha=experience.player.Paused?1f:0f;
  heading.text="SESSION OPTIONS";
  if(handButton!=null)handButton.GetComponentInChildren<Text>().text=experience.LeftSelected?"LEFT HAND (SWITCH)":"RIGHT HAND (SWITCH)";
  inputLabel.text=experience.CameraMode?"INPUT: CAMERA":experience.Hardware?"INPUT: PHASE 2":"INPUT: DEMO";
 }
 private void Living()=>SelectStation(false);
 private void KitchenRoom()=>SelectStation(true);
 private void Resume()=>experience.player.SetPaused(false);
 private void ToggleInput(){experience.CycleInputMode();Refresh();}
 public bool SelectStation(bool kitchen){
  var player=experience.player;experience.Release();
  var activity=experience.GetComponent<ActivityRunner>();if(activity!=null&&activity.Current!=null&&!activityStarting)activity.End(false);
  if(Lab)LeaveLab();
  Kitchen=kitchen;
  if(kitchen){player.TeleportToStanding(kitchenStation.position,kitchenStation.rotation);player.LookAt(kitchenObjects[KitchenSelection].transform.position);}
  else{player.TeleportToStanding(experience.seats[0].standFeet.position,experience.seats[0].seatedFeet.rotation);player.Sit(experience.seats[0]);player.LookAt(livingObjects[LivingSelection].transform.position);}
  Refresh();return true;
 }
 /// <summary>Grip Lab: seated at the living-room table with the chart's props, one at a time.</summary>
 public void SelectLab()=>OpenLab(false);
 /// <summary>Material Lab: the deformation chart's materials and forms, one at a time, at the same table.</summary>
 public void SelectMaterialLab()=>OpenLab(true);
 /// <summary>True while an activity sets up its room (so choosing the station does not end it).</summary>
 [NonSerialized] public bool activityStarting;
 void OpenLab(bool materials){
  var activity=experience.GetComponent<ActivityRunner>();if(activity!=null&&activity.Current!=null)activity.End(false);
  if((materials?materialObjects:labObjects).Length==0)return;
  var player=experience.player;experience.Release();
  if(Lab)HideLabObjects();else foreach(var item in livingObjects)item.gameObject.SetActive(false);
  Lab=true;MaterialLab=materials;Kitchen=false;
  player.TeleportToStanding(experience.seats[0].standFeet.position,experience.seats[0].seatedFeet.rotation);player.Sit(experience.seats[0]);
  SelectLabGrip(CurrentLabSelection);
 }
 void HideLabObjects(){foreach(var set in new[]{labObjects,materialObjects})foreach(var item in set){if(item.gameObject.activeSelf){item.ResetObject();item.gameObject.SetActive(false);}}}
 void LeaveLab(){
  Lab=false;MaterialLab=false;HideLabObjects();
  for(int i=0;i<livingObjects.Length;i++){livingObjects[i].gameObject.SetActive(true);livingObjects[i].ResetObject();livingObjects[i].gameObject.SetActive(i==LivingSelection);}
 }
 /// <summary>Selects the index-th prop of the open lab (wraps around).</summary>
 public void SelectLabGrip(int index){
  var set=CurrentLab;if(!Lab||set.Length==0)return;
  index=((index%set.Length)+set.Length)%set.Length;
  experience.Release();
  for(int i=0;i<set.Length;i++){set[i].gameObject.SetActive(true);set[i].ResetObject();set[i].gameObject.SetActive(i==index);}
  if(MaterialLab)MaterialSelection=index;else LabSelection=index;
  experience.player.LookAt(set[index].transform.position);Refresh();
 }
 public void SelectObject(int index){
  // In the Grip Lab the three object buttons are previous / current grip / next.
  if(Lab){SelectLabGrip(index==0?CurrentLabSelection-1:index==2?CurrentLabSelection+1:CurrentLabSelection);return;}
  var objects=Kitchen?kitchenObjects:livingObjects;if(index<0||index>=objects.Length)return;
  experience.Release();
  for(int i=0;i<objects.Length;i++){objects[i].gameObject.SetActive(true);objects[i].ResetObject();objects[i].gameObject.SetActive(i==index);}
  if(Kitchen)KitchenSelection=index;else LivingSelection=index;
  experience.player.LookAt(objects[index].transform.position);Refresh();
 }
 public void ResetSelectionForTests(){if(Lab)LeaveLab();Kitchen=false;LivingSelection=KitchenSelection=LabSelection=MaterialSelection=0;}
 private void Refresh(){
  var list=Kitchen?kitchenObjects:livingObjects;int selected=Kitchen?KitchenSelection:LivingSelection;
  lastPalette=presentation!=null?presentation.Current:null;
  if(Lab&&CurrentLab.Length>0){
   var item=CurrentLab[CurrentLabSelection];var grip=GripCatalog.Get(item.GetComponent<GripProp>().grip);var m=item.Material;
   objectButtons[0].GetComponentInChildren<Text>().text="< PREV  [";
   objectButtons[1].GetComponentInChildren<Text>().text=MaterialLab&&m!=null
    ?(CurrentLabSelection+1)+". "+m.name.ToUpperInvariant()+" ("+ClassName(m.materialClass)+")\n"+item.displayName
    :grip.number+". "+grip.name.ToUpperInvariant()+"\n"+item.displayName;
   objectButtons[2].GetComponentInChildren<Text>().text="]  NEXT >";
   for(int i=0;i<objectButtons.Length;i++)PaintChoice(objectButtons[i],i==1);
  } else for(int i=0;i<objectButtons.Length;i++){objectButtons[i].GetComponentInChildren<Text>().text=list[i].displayName;PaintChoice(objectButtons[i],i==selected);}
  PaintChoice(livingButton,!Kitchen&&!Lab);PaintChoice(kitchenButton,Kitchen&&!Lab);
  if(labButton!=null)PaintChoice(labButton,Lab&&!MaterialLab);if(materialLabButton!=null)PaintChoice(materialLabButton,MaterialLab);
 }
 public static string ClassName(MaterialClass c)=>c==MaterialClass.SemiRigid?"semi-rigid":c==MaterialClass.Soft?"soft":c==MaterialClass.Flexible?"flexible":"rigid";
 // Stage 11: selected = filled and a marker bar (not colour alone); bold labels stay legible on every fill;
 // every choice gets the shared focus ring.
 internal void PaintChoice(Button button,bool selected){
  if(presentation!=null)lastPalette=presentation.Current;
  button.image.color=lastPalette!=null?(selected?lastPalette.primary:lastPalette.secondary):new Color(.10f,.13f,.19f);
  var label=button.GetComponentInChildren<Text>();
  label.color=lastPalette!=null?(selected?lastPalette.textOnPrimary:lastPalette.textOnSecondary):Color.white;label.fontStyle=FontStyle.Bold;
  if(accessibility==null)accessibility=FindAnyObjectByType<GripRehab.UI.GripUiAccessibility>();
  var feedback=GripRehab.UI.GripUiControlFeedback.Attach(accessibility,button);if(feedback!=null)feedback.SetSelected(selected);
 }
 private GripRehab.UI.GripUiAccessibility accessibility;
}
}
