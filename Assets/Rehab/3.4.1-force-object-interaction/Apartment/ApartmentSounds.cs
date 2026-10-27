using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
/// <summary>
/// Stage 12 (S-02, S-05, S-06): material sounds in the apartment. Gives every SqueezableObject an <see cref="ObjectSound"/>
/// (impacts from real contact speed), plays grab and release at the hand, drives the held object's strain loop from its
/// compression rate, plays stress cracks and fractures from the damage state, and a slice when food is cut.
/// Sound only: nothing here changes physics, input or any recorded value.
/// </summary>
public sealed class ApartmentSounds : MonoBehaviour {
 ApartmentExperience app;
 readonly Dictionary<CuttableFood,int> cuts=new Dictionary<CuttableFood,int>();
 SqueezableObject lastHeld;float lastCompression;float nextScan;
 public static ApartmentSounds Instance{get;private set;}
 public int Tracked{get;private set;}

 public static ApartmentSounds Ensure(ApartmentExperience experience){
  if(Instance!=null)return Instance;
  var s=new GameObject("Apartment sounds").AddComponent<ApartmentSounds>();s.app=experience;return s;
 }
 void Awake(){Instance=this;}
 void OnEnable(){if(app!=null)Hook();}
 void Start(){Hook();}
 void OnDisable(){if(app!=null){app.ObjectGrabbed-=Grabbed;app.ObjectReleased-=Released;}}
 void Hook(){app.ObjectGrabbed-=Grabbed;app.ObjectReleased-=Released;app.ObjectGrabbed+=Grabbed;app.ObjectReleased+=Released;}

 public static SoundFamily FamilyOf(SqueezableObject o){
  var m=o!=null?o.Material:null;
  return RehabSoundBank.Classify(m?.name,o!=null?o.displayName:null,m?.materialClass);
 }

 void Grabbed(SqueezableObject o){if(o==null)return;Track(o);RehabAudio.PlayAt(FamilyOf(o),SoundEvent.Grab,o.transform.position,.8f,o.radius);}
 void Released(SqueezableObject o){if(o==null)return;RehabAudio.PlayAt(FamilyOf(o),SoundEvent.Release,o.transform.position,.7f,o.radius);}

 void Track(SqueezableObject o){if(o.GetComponent<ObjectSound>()==null)o.gameObject.AddComponent<ObjectSound>().Bind(o);}

 void Update(){
  if(app==null)return;
  if(Time.unscaledTime>=nextScan){
   // New objects appear when an activity starts; a slow scan picks them up without per-frame cost.
   nextScan=Time.unscaledTime+.5f;int n=0;
   foreach(var o in FindObjectsByType<SqueezableObject>(FindObjectsSortMode.None)){Track(o);n++;}
   Tracked=n;
   foreach(var f in FindObjectsByType<CuttableFood>(FindObjectsSortMode.None))if(!cuts.ContainsKey(f))cuts[f]=f.Cuts;
  }
  foreach(var f in new List<CuttableFood>(cuts.Keys)){
   if(f==null){cuts.Remove(f);continue;}
   if(f.Cuts>cuts[f])RehabAudio.PlayAt(SoundFamily.Soft,SoundEvent.Cut,f.transform.position,.9f,.05f);
   cuts[f]=f.Cuts;
  }
  var held=app.Held;
  if(held!=lastHeld){lastHeld=held;lastCompression=held!=null?held.Compression:0;}
  if(held!=null&&app.ForceValid&&Time.deltaTime>0){
   float c=held.Compression,rate=(c-lastCompression)/Time.deltaTime;lastCompression=c;
   RehabAudio.SetStrain(held.HasLocalizedDeformation?FamilyOf(held):(SoundFamily?)null,c,rate,held.transform.position);
  } else RehabAudio.SetStrain(null,0,0,Vector3.zero);
 }
}
}
