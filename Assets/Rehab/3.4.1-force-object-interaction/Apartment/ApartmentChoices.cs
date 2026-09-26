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
 private void OnEnable(){
  if(handButton!=null)handButton.onClick.AddListener(ToggleHand);livingButton.onClick.AddListener(Living);kitchenButton.onClick.AddListener(KitchenRoom);resumeButton.onClick.AddListener(Resume);inputButton.onClick.AddListener(ToggleInput);
  callbacks=new UnityAction[objectButtons.Length];for(int i=0;i<objectButtons.Length;i++){int choice=i;callbacks[i]=()=>SelectObject(choice);objectButtons[i].onClick.AddListener(callbacks[i]);}
 }
 private void OnDisable(){
  if(handButton!=null)handButton.onClick.RemoveListener(ToggleHand);livingButton.onClick.RemoveListener(Living);kitchenButton.onClick.RemoveListener(KitchenRoom);resumeButton.onClick.RemoveListener(Resume);inputButton.onClick.RemoveListener(ToggleInput);
  if(callbacks!=null)for(int i=0;i<objectButtons.Length;i++)objectButtons[i].onClick.RemoveListener(callbacks[i]);
 }
 private void Start(){shell=FindAnyObjectByType<RehabAppShell>();experience.SelectHand(false);experience.player.SetPaused(true);Refresh();}
 private void ToggleHand(){experience.SelectHand(!experience.LeftSelected);Refresh();}
 private void Update(){
  if(shell!=null&&shell.BlocksGameplay){controls.interactable=false;controls.blocksRaycasts=false;controls.alpha=0;return;}
  if(Input.GetKeyDown(KeyCode.Tab))experience.player.SetPaused(!experience.player.Paused);
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
  if(player.IsSeated&&!player.Stand())return false;
  Kitchen=kitchen;
  if(kitchen){player.Teleport(kitchenStation.position,kitchenStation.rotation);player.LookAt(kitchenObjects[KitchenSelection].transform.position);}
  else{player.Teleport(experience.seats[0].standFeet.position,experience.seats[0].seatedFeet.rotation);player.Sit(experience.seats[0]);player.LookAt(livingObjects[LivingSelection].transform.position);}
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
  var active=new Color(.12f,.49f,.47f);var inactive=new Color(.08f,.20f,.25f);
  for(int i=0;i<objectButtons.Length;i++){objectButtons[i].GetComponentInChildren<Text>().text=list[i].displayName;objectButtons[i].image.color=i==selected?active:inactive;}
  livingButton.image.color=!Kitchen?active:inactive;kitchenButton.image.color=Kitchen?active:inactive;
 }
}
}
