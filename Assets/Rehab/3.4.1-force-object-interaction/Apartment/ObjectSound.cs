using UnityEngine;
namespace GripRehab {
/// <summary>Per-object impacts and damage sounds. Impact gain comes from the contact's normal speed and the object's mass.</summary>
public sealed class ObjectSound : MonoBehaviour {
 SqueezableObject item;SoundFamily family;float nextImpact,quietUntil;DamageState lastDamage;
 public int Impacts{get;private set;}
 /// <summary>Loudest impact gain since the last reset (checks).</summary>
 public float PeakGain;
 /// <summary>Contacts at impact speed (≥ 0.12 m/s), sounded or not, and the last normal speed (checks).</summary>
 public int Enters;public float LastSpeed;
 public SoundFamily Family=>family;
 public void Bind(SqueezableObject o){item=o;family=ApartmentSounds.FamilyOf(o);lastDamage=o.Damage;quietUntil=Time.time+.6f;} // physics settles on spawn
 // Enter alone misses a landing when the physics engine keeps the contact pair alive after the object rested;
 // Stay checks the normal speed too (a resting or sliding contact has ~0 normal speed, so it stays silent).
 void OnCollisionEnter(Collision c)=>Hit(c,.12f);
 void OnCollisionStay(Collision c)=>Hit(c,.25f); // a contact that persists needs a firmer hit to count, so rolling does not tick
 void Hit(Collision c,float minimum){
  if(item==null)return;
  // Every contact point, not just the first: an object leaning on a wall has a sideways first contact while it lands on the floor.
  int n=Mathf.Min(c.contactCount,8);float speed=0;Vector3 point=transform.position;
  for(int i=0;i<n;i++){var contact=c.GetContact(i);float s=Mathf.Abs(Vector3.Dot(c.relativeVelocity,contact.normal));if(s>speed){speed=s;point=contact.point;}}
  if(speed<minimum)return;Enters++;LastSpeed=speed;
  if(Time.time<nextImpact||Time.time<quietUntil)return;
  float gain=RehabSoundBank.ImpactGain(speed,item.Body!=null?item.Body.mass:.2f);
  if(gain<=0)return;
  nextImpact=Time.time+.08f;Impacts++;PeakGain=Mathf.Max(PeakGain,gain); // one sound per bounce, not one per contact point
  RehabAudio.PlayAt(family,SoundEvent.Impact,point,gain,item.radius);
 }
 void Update(){
  if(item==null)return;
  var d=item.Damage;if(d==lastDamage)return;
  if(d==DamageState.Broken)RehabAudio.PlayAt(family,SoundEvent.Fracture,transform.position,1,item.radius);
  else if(d>lastDamage)RehabAudio.PlayAt(family,SoundEvent.StressCrack,transform.position,.5f+.15f*(int)d,item.radius);
  lastDamage=d;
 }
}
}
