using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace GripRehab.Editor {
public static class HandSurfaceVerification {
 [Serializable] class Result {public bool passed;public int assertions;public string utc,scope;}
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 public static void Run(){
  checks=0;var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  Check(app!=null&&app.surfaceGuard!=null&&app.surfaceGuard.app==app,"Missing direct surface guard");
  var data=app.surfaceGuard.skinData;Check(data!=null&&data.vertices.Length>0&&data.vertices.Length==data.weights.Length&&data.bindPoses.Length==app.virtualGrasp.outputMesh.bones.Length&&data.rightVertices.Length>0&&data.leftVertices.Length>0,"Missing player-readable contact skin data");
  Check(app.virtualGrasp.outputMesh!=null&&!app.virtualGrasp.outputMesh.forceRenderingOff,"Original SDK renderer must remain visible");
  Check(app.contactRig.probes.Length==15,"Keep original 15 probes");
  foreach(var p in app.contactRig.probes)Check(p.GetComponent<Rigidbody>().isKinematic&&p.GetComponent<CapsuleCollider>().isTrigger,"Finger probes must not be spring-driven hand bodies");
  Check(UnityEngine.Object.FindObjectsByType<Joint>(FindObjectsInactive.Include).Length==0,"Rejected dynamic hand joints remain");
  Check(LayerMask.NameToLayer("RehabPhysicsHand")==-1&&LayerMask.NameToLayer("RehabHandSurface")==-1,"Rejected hand layers remain");
  var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
  try{
   cube.transform.SetPositionAndRotation(new Vector3(2,3,4),Quaternion.Euler(23,48,17));cube.transform.localScale=new Vector3(.1f,.2f,.3f);
   var mesh=cube.GetComponent<MeshFilter>().sharedMesh;var surface=new MeshContactSurface(mesh,cube.transform);var v=mesh.vertices;
   foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){
    Vector3 query=cube.transform.TransformPoint(axis*.8f),expected=cube.transform.TransformPoint(axis*.5f);
    Check(surface.Closest(query,v,out var point,out var normal),"Surface query failed");
    Check(Vector3.Distance(point,expected)<.00001f,"Wrong face at UV seam / non-uniform scale");
    Check(Vector3.Dot(normal,cube.transform.TransformDirection(axis))>.99f,"Wrong surface normal");
   }
   Check(!surface.Closest(new Vector3(float.NaN,0,0),v,out _,out _),"Invalid surface input accepted");
   surface.Closest(cube.transform.position,v,out _,out _);
   Check(surface.Inside(cube.transform.position),"Closed mesh interior must be classified inside");
   Check(!surface.Inside(cube.transform.TransformPoint(Vector3.one)),"Outside mesh must not be repelled as an interior point");
   for(int mirror=0;mirror<2;mirror++){
    cube.transform.localScale=new Vector3(mirror==0?.1f:-.1f,.2f,.3f);
    for(int sample=0;sample<16;sample++){
     float x=sample%2==0?.4f:.7f;var query=cube.transform.TransformPoint(new Vector3(x,.013f*sample-.1f,.09f));
     surface.Closest(query,v,out var hit,out var n);
     Check(!surface.FaceSignReliable||(Vector3.Dot(query-hit,n)<0)==surface.Inside(query),"Closed-face signed distance disagrees with parity under scale/reflection");
    }
   }
   // Closest vertex is on the distant small triangle, but the large triangle
   // is the closest SURFACE. A vertex-neighbour shortcut gets this wrong.
   var uneven=new Mesh();
   try{uneven.vertices=new[]{new Vector3(-10,0,0),new Vector3(10,0,0),new Vector3(0,10,0),new Vector3(-.1f,.9f,2),new Vector3(.1f,.9f,2),new Vector3(0,1.1f,2)};uneven.triangles=new[]{0,1,2,3,4,5};
    var exact=new MeshContactSurface(uneven,cube.transform);var q=cube.transform.TransformPoint(new Vector3(0,1,.1f));
    Check(exact.Closest(q,uneven.vertices,out var hit,out _),"Uneven surface query failed");
    Check(Vector3.Distance(hit,cube.transform.TransformPoint(new Vector3(0,1,0)))<.00001f,"Nearest vertex shortcut chose the wrong surface");
   }finally{UnityEngine.Object.DestroyImmediate(uneven);}

  }finally{UnityEngine.Object.DestroyImmediate(cube);}
  Directory.CreateDirectory("Docs/evidence/hand-correction-2026-10-08");File.WriteAllText("Docs/evidence/hand-correction-2026-10-08/native-surface-tests.json",JsonUtility.ToJson(new Result{passed=true,assertions=checks,utc=DateTime.UtcNow.ToString("O"),scope="Native Unity surface geometry and saved-scene direct-hand architecture; no human camera trial"},true));
 }
}
}
