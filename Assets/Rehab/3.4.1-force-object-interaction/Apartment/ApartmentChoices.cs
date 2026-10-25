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
 [NonSerialized] public Button labButton;
 public bool Lab{get;private set;}
 public int LabSelection{get;private set;}
 public int LivingSelection{get;private set;}
 public int KitchenSelection{get;private set;}
 public SqueezableObject SelectedObject=>Lab&&labObjects.Length>0?labObjects[LabSelection]:Kitchen?kitchenObjects[KitchenSelection]:livingObjects[LivingSelection];
 private void Awake(){
  if(experience==null||livingObjects==null||livingObjects.Length==0||livingObjects[0]==null)return;
  GripLab.AssignStationGrips(experience.objects);
  var ball=livingObjects[0];var renderer=ball.visual!=null?ball.visual.GetComponentInChildren<Renderer>(true):null;
  var source=renderer!=null?renderer.sharedMaterial:null;
  if(source==null)return;
  // The table top under the station ball: its lowest collider point.
  var col=ball.GetComponent<Collider>();float drop=col is SphereCollider sphere?sphere.radius*ball.transform.lossyScale.y:col!=null?col.bounds.extents.y:.07f;
  labObjects=GripLab.Build(ball.transform.parent,ball.transform.position+Vector3.down*drop,source);
  var all=new SqueezableObject[experience.objects.Length+labObjects.Length];experience.objects.CopyTo(all,0);labObjects.CopyTo(all,experience.objects.Length);experience.objects=all;
  // Third station button beside LIVING ROOM and KITCHEN.
  labButton=Instantiate(kitchenButton,kitchenButton.transform.parent);labButton.name="GRIP LAB";
  var buttons=new[]{livingButton,kitchenButton,labButton};
  for(int i=0;i<3;i++){
   var rect=(RectTransform)buttons[i].transform;rect.sizeDelta=new Vector2(132,35);rect.anchoredPosition=new Vector2(-139+139*i,rect.anchoredPosition.y);
   var label=buttons[i].GetComponentInChildren<Text>();var labelRect=label.rectTransform;labelRect.sizeDelta=new Vector2(124,31);labelRect.anchoredPosition=new Vector2(0,-17.5f);
  }
  labButton.GetComponentInChildren<Text>().text="GRIP LAB";
 }
 private UnityAction[] callbacks;
 private RehabAppShell shell;
 private GripRehab.UI.GripUiThemeController presentation;
 private GripRehab.UI.GripUiTheme lastPalette;
 private void OnEnable(){
  if(labButton!=null)labButton.onClick.AddListener(SelectLab);
  if(handButton!=null)handButton.onClick.AddListener(ToggleHand);livingButton.onClick.AddListener(Living);kitchenButton.onClick.AddListener(KitchenRoom);resumeButton.onClick.AddListener(Resume);inputButton.onClick.AddListener(ToggleInput);
  callbacks=new UnityAction[objectButtons.Length];for(int i=0;i<objectButtons.Length;i++){int choice=i;callbacks[i]=()=>SelectObject(choice);objectButtons[i].onClick.AddListener(callbacks[i]);}
 }
 private void OnDisable(){
  if(labButton!=null)labButton.onClick.RemoveListener(SelectLab);
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
   if(Input.GetKeyDown(KeyCode.RightBracket))SelectLabGrip(LabSelection+1);
   if(Input.GetKeyDown(KeyCode.LeftBracket))SelectLabGrip(LabSelection-1);
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
  if(Lab)LeaveLab();
  Kitchen=kitchen;
  if(kitchen){player.TeleportToStanding(kitchenStation.position,kitchenStation.rotation);player.LookAt(kitchenObjects[KitchenSelection].transform.position);}
  else{player.TeleportToStanding(experience.seats[0].standFeet.position,experience.seats[0].seatedFeet.rotation);player.Sit(experience.seats[0]);player.LookAt(livingObjects[LivingSelection].transform.position);}
  Refresh();return true;
 }
 /// <summary>Grip Lab: seated at the living-room table with the chart's props, one at a time.</summary>
 public void SelectLab(){
  if(labObjects.Length==0)return;
  var player=experience.player;experience.Release();
  if(!Lab){foreach(var item in livingObjects)item.gameObject.SetActive(false);}
  Lab=true;Kitchen=false;
  player.TeleportToStanding(experience.seats[0].standFeet.position,experience.seats[0].seatedFeet.rotation);player.Sit(experience.seats[0]);
  SelectLabGrip(LabSelection);
 }
 void LeaveLab(){
  Lab=false;foreach(var item in labObjects){if(item.gameObject.activeSelf){item.ResetObject();item.gameObject.SetActive(false);}}
  for(int i=0;i<livingObjects.Length;i++){livingObjects[i].gameObject.SetActive(true);livingObjects[i].ResetObject();livingObjects[i].gameObject.SetActive(i==LivingSelection);}
 }
 public void SelectLabGrip(int index){
  if(!Lab||labObjects.Length==0)return;
  index=((index%labObjects.Length)+labObjects.Length)%labObjects.Length;
  experience.Release();
  for(int i=0;i<labObjects.Length;i++){labObjects[i].gameObject.SetActive(true);labObjects[i].ResetObject();labObjects[i].gameObject.SetActive(i==index);}
  LabSelection=index;experience.player.LookAt(labObjects[index].transform.position);Refresh();
 }
 public void SelectObject(int index){
  // In the Grip Lab the three object buttons are previous / current grip / next.
  if(Lab){SelectLabGrip(index==0?LabSelection-1:index==2?LabSelection+1:LabSelection);return;}
  var objects=Kitchen?kitchenObjects:livingObjects;if(index<0||index>=objects.Length)return;
  experience.Release();
  for(int i=0;i<objects.Length;i++){objects[i].gameObject.SetActive(true);objects[i].ResetObject();objects[i].gameObject.SetActive(i==index);}
  if(Kitchen)KitchenSelection=index;else LivingSelection=index;
  experience.player.LookAt(objects[index].transform.position);Refresh();
 }
 public void ResetSelectionForTests(){if(Lab)LeaveLab();Kitchen=false;LivingSelection=KitchenSelection=LabSelection=0;}
 private void Refresh(){
  var list=Kitchen?kitchenObjects:livingObjects;int selected=Kitchen?KitchenSelection:LivingSelection;
  lastPalette=presentation!=null?presentation.Current:null;
  if(Lab&&labObjects.Length>0){
   var grip=GripCatalog.Get(labObjects[LabSelection].GetComponent<GripProp>().grip);
   objectButtons[0].GetComponentInChildren<Text>().text="< PREV  [";
   objectButtons[1].GetComponentInChildren<Text>().text=grip.number+". "+grip.name.ToUpperInvariant()+"\n"+labObjects[LabSelection].displayName;
   objectButtons[2].GetComponentInChildren<Text>().text="]  NEXT >";
   for(int i=0;i<objectButtons.Length;i++)PaintChoice(objectButtons[i],i==1);
  } else for(int i=0;i<objectButtons.Length;i++){objectButtons[i].GetComponentInChildren<Text>().text=list[i].displayName;PaintChoice(objectButtons[i],i==selected);}
  PaintChoice(livingButton,!Kitchen&&!Lab);PaintChoice(kitchenButton,Kitchen&&!Lab);if(labButton!=null)PaintChoice(labButton,Lab);
 }
 private void PaintChoice(Button button,bool selected){
  button.image.color=lastPalette!=null?(selected?lastPalette.primary:lastPalette.secondary):new Color(.10f,.13f,.19f);
  button.GetComponentInChildren<Text>().color=lastPalette!=null?(selected?lastPalette.textOnPrimary:lastPalette.textOnSecondary):Color.white;
 }
}
}
