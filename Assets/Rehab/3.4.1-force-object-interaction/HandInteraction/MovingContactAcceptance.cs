using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace GripRehab {
[DefaultExecutionOrder(20)]
public sealed class MovingContactInput:MonoBehaviour {public MovingContactAcceptance owner;void Update(){owner?.AdvanceInput();}}

// Moving-input regression in the real SDK/player. Explicit opt-in; no camera or user records.
[DefaultExecutionOrder(2000)]
public sealed class MovingContactAcceptance:MonoBehaviour {
 [Serializable] sealed class Row {public string material,hand;public int frames;public float peakPenetrationMm,strictPeakPenetrationMm,peakWristCorrectionStepMm;public double meanMs,p95Ms,maxMs,resolveMeanMs,pressureMeanMs,refineMeanMs;}
 [Serializable] sealed class Result {public bool passed;public string utc,scope,error;public Row[] rows;}
 readonly List<Row> rows=new List<Row>();readonly List<double> costs=new List<double>();
 ApartmentExperience app;Row current;float elapsed;Vector3 previousCorrection;Quaternion neutral;bool measuring;int errors;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-moving-contact")>=0){var go=new GameObject("Moving contact acceptance");var test=go.AddComponent<MovingContactAcceptance>();go.AddComponent<MovingContactInput>().owner=test;}
 }
 void Error(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
 public void AdvanceInput(){
  if(!measuring)return;
  elapsed+=Time.deltaTime;
  // Movement through the same source rig as the player, independent of solved output.
  app.SetDemoHandOffset(app.LeftSelected,new Vector3(Mathf.Sin(elapsed*2)*.025f,Mathf.Sin(elapsed*1.5f)*.015f,0));
  app.ActiveHand.transform.localRotation=neutral*Quaternion.AngleAxis(Mathf.Sin(elapsed*2)*12,Vector3.forward);
  app.TestForce=22+Mathf.Sin(elapsed*3)*8;
 }
 void LateUpdate(){
  if(!measuring)return;
  current.frames++;double cost=app.LastContactMs;costs.Add(cost);current.meanMs+=(cost-current.meanMs)/current.frames;current.maxMs=Math.Max(current.maxMs,cost);
  current.resolveMeanMs+=(app.LastResolveMs-current.resolveMeanMs)/current.frames;current.pressureMeanMs+=(app.LastPressureMs-current.pressureMeanMs)/current.frames;current.refineMeanMs+=(app.LastRefineMs-current.refineMeanMs)/current.frames;
  current.peakPenetrationMm=Mathf.Max(current.peakPenetrationMm,app.surfaceGuard.ResidualPenetration*1000);
  if(current.frames>1)current.peakWristCorrectionStepMm=Mathf.Max(current.peakWristCorrectionStepMm,Vector3.Distance(app.surfaceGuard.ResolvedWristOffset,previousCorrection)*1000);
  previousCorrection=app.surfaceGuard.ResolvedWristOffset;
  if(current.frames%10==0&&app.Held!=null)current.strictPeakPenetrationMm=Mathf.Max(current.strictPeakPenetrationMm,app.surfaceGuard.Skin.StrictDepth(app.Held)*1000);
 }
 IEnumerator Start(){
  Application.runInBackground=true;Application.logMessageReceived+=Error;
  yield return new WaitForSeconds(2);
  app=FindAnyObjectByType<ApartmentExperience>();
  if(app==null||!app.virtualGrasp.Ready){Finish("SDK unavailable");yield break;}
  app.player.enabled=false;app.player.SetPaused(false);
  for(int side=0;side<2;side++)for(int selected=0;selected<6;selected++){
   app.SetHardware(false);app.TestForce=0;app.ResetObjects();app.choices.SelectStation(selected>=3);app.choices.SelectObject(selected%3);app.SelectHand(side==1);app.player.SetPaused(false);
   yield return new WaitForSeconds(.25f);
   if(!app.TryGrab(app.choices.SelectedObject)){Finish("Acquisition failed: "+app.Status);yield break;}
   app.TestForce=22;yield return new WaitForSeconds(.8f);
   current=new Row{material=app.Held.displayName,hand=side==0?"Right":"Left"};rows.Add(current);costs.Clear();elapsed=0;neutral=app.ActiveHand.transform.localRotation;measuring=true;
   yield return new WaitForSeconds(2.5f);
   measuring=false;costs.Sort();current.p95Ms=costs.Count>0?costs[(int)((costs.Count-1)*.95)]:0;
   app.Release();
  }
  string failure=errors>0?"Runtime errors: "+errors:"";
  foreach(var row in rows)if(row.frames<10||!float.IsFinite(row.peakPenetrationMm)||row.peakPenetrationMm>=2||row.strictPeakPenetrationMm>=2)failure+=" / "+row.material+" "+row.hand+" moving-contact overlap/frame count failed";
  Finish(failure);
 }
 void Finish(string error){
  Application.logMessageReceived-=Error;
  Directory.CreateDirectory("Logs");File.WriteAllText("Logs/moving-contact.json",JsonUtility.ToJson(new Result{passed=error=="",utc=DateTime.UtcNow.ToString("O"),scope="Synthetic moving SDK hand, varying pressure, both hands/six objects; not live camera or finger-count grasp validation",error=error,rows=rows.ToArray()},true));
  Application.Quit(error==""?0:1);
 }
}
}
