using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
// Object-owned: survives clearing Held after release. Floors and furniture remain solid.
public sealed class GraspCollisionShield {
 sealed class Pair { public Collider item,player;public bool originalIgnore; }
 readonly List<Pair> pairs=new List<Pair>();
 public void Protect(Collider[] items,Collider[] players){
  if(items==null||players==null)return;
  foreach(var item in items)foreach(var player in players){
   if(item==null||player==null||item==player||item.isTrigger||player.isTrigger)continue;
   bool existing=false;foreach(var pair in pairs)if(pair.item==item&&pair.player==player){existing=true;break;}
   if(!existing)pairs.Add(new Pair{item=item,player=player,originalIgnore=Physics.GetIgnoreCollision(item,player)});
   Physics.IgnoreCollision(item,player,true);
  }
 }
 public void Step(bool held){
  for(int i=pairs.Count-1;i>=0;i--){
   var pair=pairs[i];
   if(pair.item==null||pair.player==null){pairs.RemoveAt(i);continue;}
   bool itemActive=pair.item.enabled&&pair.item.gameObject.activeInHierarchy;
   bool playerActive=pair.player.enabled&&pair.player.gameObject.activeInHierarchy;
   if(!itemActive){Restore(pair);pairs.RemoveAt(i);continue;}
   if(!playerActive)continue; // preserve the exclusion until the controller returns
   var a=pair.item.bounds;var b=pair.player.bounds;
   // Never restore an overlapping drop just because a timeout elapsed.
   if(!held&&GraspMotionPolicy.Separated(a.min,a.max,b.min,b.max,.015f)){Restore(pair);pairs.RemoveAt(i);}
   else Physics.IgnoreCollision(pair.item,pair.player,true);
  }
 }
 static void Restore(Pair pair){if(pair.item!=null&&pair.player!=null)Physics.IgnoreCollision(pair.item,pair.player,pair.originalIgnore);}
 public void Clear(){foreach(var pair in pairs)Restore(pair);pairs.Clear();}
}
}
