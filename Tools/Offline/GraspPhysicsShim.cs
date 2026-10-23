// TEST ONLY: records collider pair state and bounds; does not simulate PhysX.
using System;
using System.Collections.Generic;
namespace UnityEngine {
 public sealed class GameObject { public bool activeInHierarchy=true; }
 public struct Bounds {
  public Vector3 min,max;
  public Bounds(Vector3 min,Vector3 max){this.min=min;this.max=max;}
 }
 public sealed class Collider {
  public bool enabled=true,isTrigger;public GameObject gameObject=new GameObject();public Bounds bounds;
 }
 public static class Physics {
  static readonly Dictionary<Tuple<Collider,Collider>,bool> ignored=new Dictionary<Tuple<Collider,Collider>,bool>();
  public static bool GetIgnoreCollision(Collider a,Collider b){bool value;return ignored.TryGetValue(Tuple.Create(a,b),out value)&&value;}
  public static void IgnoreCollision(Collider a,Collider b,bool value){ignored[Tuple.Create(a,b)]=value;}
 }
}
