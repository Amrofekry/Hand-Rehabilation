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
 public int LivingSelection{get;private set;}
 public int KitchenSelection{get;private set;}
 public SqueezableObject SelectedObject=>Kitchen?kitchenObjects[KitchenSelection]:livingObjects[LivingSelection];
 private UnityAction[] callbacks;
 private RehabAppShell shell;
 private GripRehab.UI.GripUiThemeController presentation;
 private GripRehab.UI.GripUiTheme lastPalette;
 private void OnEnable(){
  if(handButton!=null)handButton.onClick.AddListener(ToggleHand);livingButton.onClick.AddListener(Living);kitchenButton.onClick.AddListener(KitchenRoom);resumeButton.onClick.AddListener(Resume);inputButton.onClick.AddListener(ToggleInput);
  callbacks=new UnityAction[objectButtons.Length];for(int i=0;i<objectButtons.Length;i++){int choice=i;callbacks[i]=()=>SelectObject(choice);objectButtons[i].onClick.AddListener(callbacks[i]);}
 }
 private void OnDisable(){
  if(handButton!=null)handButton.onClick.RemoveListener(ToggleHand);livingButton.onClick.RemoveListener(Living);kitchenButton.onClick.RemoveListener(KitchenRoom);resumeButton.onClick.RemoveListener(Resume);inputButton.onClick.RemoveListener(ToggleInput);
  if(callbacks!=null)for(int i=0;i<objectButtons.Length;i++)objectButtons[i].onClick.RemoveListener(callbacks[i]);
 }
 private void Start(){shell=FindAnyObjectByType<RehabAppShell>();presentation=FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();if(presentation!=null)presentation.PresentationApplied+=Refresh;experience.SelectHand(false);experience.player.SetPaused(true);Refresh();}
 private void OnDestroy(){if(presentation!=null)presentation.PresentationApplied-=Refresh;}
 private void ToggleHand(){experience.SelectHand(!experience.LeftSelected);Refresh();}
 private void Update(){
  if(presentation!=null&&lastPalette!=presentation.Current)Refresh();
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
  Kitchen=kitchen;
  if(kitchen){player.TeleportToStanding(kitchenStation.position,kitchenStation.rotation);player.LookAt(kitchenObjects[KitchenSelection].transform.position);}
  else{player.TeleportToStanding(experience.seats[0].standFeet.position,experience.seats[0].seatedFeet.rotation);player.Sit(experience.seats[0]);player.LookAt(livingObjects[LivingSelection].transform.position);}
  Refresh();return true;
 }
 public void SelectObject(int index){
  var objects=Kitchen?kitchenObjects:livingObjects;if(index<0||index>=objects.Length)return;
  experience.Release();
  for(int i=0;i<objects.Length;i++){objects[i].gameObject.SetActive(true);objects[i].ResetObject();objects[i].gameObject.SetActive(i==index);}
  if(Kitchen)KitchenSelection=index;else LivingSelection=index;
  experience.player.LookAt(objects[index].transform.position);Refresh();
 }
 public void ResetSelectionForTests(){Kitchen=false;LivingSelection=KitchenSelection=0;}
 private void Refresh(){
  var list=Kitchen?kitchenObjects:livingObjects;int selected=Kitchen?KitchenSelection:LivingSelection;
  lastPalette=presentation!=null?presentation.Current:null;
  for(int i=0;i<objectButtons.Length;i++){objectButtons[i].GetComponentInChildren<Text>().text=list[i].displayName;PaintChoice(objectButtons[i],i==selected);}
  PaintChoice(livingButton,!Kitchen);PaintChoice(kitchenButton,Kitchen);
 }
 private void PaintChoice(Button button,bool selected){
  button.image.color=lastPalette!=null?(selected?lastPalette.primary:lastPalette.secondary):new Color(.10f,.13f,.19f);
  button.GetComponentInChildren<Text>().color=lastPalette!=null?(selected?lastPalette.textOnPrimary:lastPalette.textOnSecondary):Color.white;
 }
}
}
