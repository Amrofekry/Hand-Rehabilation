using UnityEngine;
namespace GripRehab {
// Bounded compound approximation for PhysX; no per-frame convex cooking and
// no independent node bodies capable of pushing the player. The cavity stays open.
public sealed class DeformationColliderProxy {
 readonly BoxCollider[] walls;
 readonly Vector3[] centers;
 readonly int[] nodes;
 public DeformationColliderProxy(Transform visual,Mesh source,Collider[] original,bool hollow,SoftBodyTopology topology){
  if(!hollow){walls=System.Array.Empty<BoxCollider>();centers=System.Array.Empty<Vector3>();nodes=System.Array.Empty<int>();return;}
  foreach(var c in original)c.enabled=false;
  // The X bound includes the loop handle. Using it as the body radius shifted
  // the entire contact ring toward the handle and created phantom contacts.
  Bounds bounds=source.bounds;float r=bounds.extents.z*.96f;
  Vector3 center=new Vector3(0,bounds.center.y,bounds.center.z);
  float thickness=Mathf.Max(r*.10f,.003f/Mathf.Max(.0001f,visual.lossyScale.x));
  walls=new BoxCollider[13];centers=new Vector3[13];nodes=new int[13];
  for(int i=0;i<13;i++){
   var part=new GameObject(i==12?"Cup base collision":"Cup wall collision "+i);part.layer=visual.gameObject.layer;
   part.transform.SetParent(visual,false);var box=part.AddComponent<BoxCollider>();walls[i]=box;
   if(i==12){box.center=new Vector3(center.x,bounds.min.y+thickness*.5f,center.z);box.size=new Vector3(r*1.4f,thickness,r*1.4f);}
   else{float angle=i*Mathf.PI*2/12;box.center=center+new Vector3(Mathf.Sin(angle)*r,0,Mathf.Cos(angle)*r);
    part.transform.localRotation=Quaternion.Euler(0,i*30,0);box.center=Quaternion.Inverse(part.transform.localRotation)*box.center;
    box.size=new Vector3(r*.55f,bounds.size.y,thickness);}
   centers[i]=box.center;nodes[i]=-1;
   if(topology!=null){Vector3 sample=part.transform.localRotation*box.center;float best=float.PositiveInfinity;
    for(int n=0;n<topology.nodes.Length;n++){float d=(sample-topology.nodes[n]).sqrMagnitude;if(d<best){best=d;nodes[i]=n;}}}
  }
 }
 public void Update(NodeSpringDeformation solver){
  if(solver==null)return;
  for(int i=0;i<walls.Length;i++)if(nodes[i]>=0)walls[i].center=centers[i]+Quaternion.Inverse(walls[i].transform.localRotation)*solver.NodeOffset(nodes[i]);
 }
 public void Reset(){for(int i=0;i<walls.Length;i++)walls[i].center=centers[i];}
}
}
