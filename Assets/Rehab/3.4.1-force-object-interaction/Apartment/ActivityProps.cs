using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
/// <summary>An object that belongs to a running activity (kind, book height, start pose).</summary>
public sealed class ActivityItem : MonoBehaviour {
 public string kind;public float height;public int index;
 /// <summary>Yaw symmetry in degrees for orientation scoring (360 = one way, 180 = either way round, 1 = round).</summary>
 public float yawSymmetry=360;
 public Quaternion restRotation=Quaternion.identity;
 public SnapTarget snappedTo;public bool cooked;public float cookProgress;
 public SqueezableObject Item=>GetComponent<SqueezableObject>();
}

/// <summary>Where an item belongs. A carrier target moves with its carrier (pan, plate) and holds what it receives.</summary>
public sealed class SnapTarget {
 public string id,label;public Func<ActivityItem,bool> accepts;
 public Vector3 position;public float yaw;public float yawSymmetry=360;
 public Transform carrier;public Vector3 carrierOffset;public float carrierRadius;public int capacity=1;
 public readonly List<ActivityItem> occupants=new List<ActivityItem>();
 public GameObject ghost;
 public bool Full=>occupants.Count>=capacity;
 public Vector3 WorldPosition=>carrier!=null?carrier.TransformPoint(carrierOffset):position;
}

/// <summary>A static surface that redirects placement into a volume (storage box interior).</summary>
public sealed class PlacementVolume : MonoBehaviour { public Bounds interior; }

/// <summary>
/// Food that a knife can cut: its visible mesh is kept as pieces along one axis and rebuilt with small gaps after
/// each cut (PlaneCutter). A geometric approximation of cutting, not a material simulation.
/// </summary>
public sealed class CuttableFood : MonoBehaviour {
 public MeshFilter visual;public Vector3 axis=Vector3.right;
 readonly List<PlaneCutter.Piece> pieces=new List<PlaneCutter.Piece>();Mesh shown;
 public int Pieces=>pieces.Count;
 public int Cuts=>Math.Max(0,pieces.Count-1);
 public void Init(){
  var m=visual.sharedMesh;var p=new PlaneCutter.Piece();p.vertices.AddRange(m.vertices);p.triangles.AddRange(m.triangles);pieces.Add(p);
  shown=new Mesh{name=name+" pieces"};visual.sharedMesh=shown;Rebuild();
 }
 static void Range(PlaneCutter.Piece p,Vector3 axis,out float lo,out float hi){lo=float.MaxValue;hi=float.MinValue;foreach(var v in p.vertices){float s=Vector3.Dot(v,axis);lo=Math.Min(lo,s);hi=Math.Max(hi,s);}}
 /// <summary>Lengths of the pieces along the axis (metres, mesh space = world for these unscaled props).</summary>
 public List<float> Lengths(){var l=new List<float>();foreach(var p in pieces){Range(p,axis,out float lo,out float hi);l.Add(hi-lo);}return l;}
 /// <summary>Fraction 0..1 of a world point along the food's full length.</summary>
 public float Along(Vector3 world){
  float lo=float.MaxValue,hi=float.MinValue;foreach(var p in pieces){Range(p,axis,out float a,out float b);lo=Math.Min(lo,a);hi=Math.Max(hi,b);}
  float s=Vector3.Dot(visual.transform.InverseTransformPoint(world),axis);return hi>lo?(s-lo)/(hi-lo):0;
 }
 /// <summary>Cuts the piece under a world point; refuses cuts closer than minGap to an end or an earlier cut.</summary>
 public bool Cut(Vector3 world,float minGap){
  var local=visual.transform.InverseTransformPoint(world);float s=Vector3.Dot(local,axis);
  for(int i=0;i<pieces.Count;i++){
   Range(pieces[i],axis,out float lo,out float hi);
   if(s<=lo||s>=hi)continue;
   if(s-lo<minGap||hi-s<minGap)return false;
   if(!PlaneCutter.Split(pieces[i].vertices.ToArray(),pieces[i].triangles.ToArray(),axis*s,axis,out var above,out var below))return false;
   pieces[i]=below;pieces.Insert(i+1,above);Rebuild();return true;
  }
  return false;
 }
 void Rebuild(){
  pieces.Sort((a,b)=>{Range(a,axis,out float la,out _);Range(b,axis,out float lb,out _);return la.CompareTo(lb);});
  var v=new List<Vector3>();var t=new List<int>();float middle=(pieces.Count-1)*.5f;
  for(int i=0;i<pieces.Count;i++){
   var offset=axis*((i-middle)*.004f);int start=v.Count;
   foreach(var p in pieces[i].vertices)v.Add(p+offset);foreach(int k in pieces[i].triangles)t.Add(start+k);
  }
  shown.Clear();shown.indexFormat=v.Count>65000?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
  shown.SetVertices(v);shown.SetTriangles(t,0);shown.RecalculateNormals();shown.RecalculateBounds();
 }
}

/// <summary>Stage 10 props built in code (D7): books, shelf, tableware, box, pan, utensils, board, food.</summary>
public static class ActivityProps {
 public static Material Paint(Material source,Color color,float smooth,float metal=0,bool clear=false){
  var m=new Material(source){name="Activity "+ColorUtility.ToHtmlStringRGB(color)};
  if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",null);if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",null);
  if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);m.color=color;
  if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal);
  // Thin lathed props (plate, bowl, tray rims) are drawn from both sides so no face can drop out.
  if(m.HasProperty("_Cull"))m.SetFloat("_Cull",0);
  return m;
 }
 static GameObject Child(GameObject root,string name,Vector3 position,Vector3 euler=default){
  var go=new GameObject(name){layer=root.layer};go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(euler);return go;
 }
 /// <summary>Static furniture piece (shelf, box, board): visible mesh and box colliders, not grabbable.</summary>
 public static GameObject Fixture(string name,Transform parent,Vector3 position,Quaternion rotation,Material material,params (Vector3 centre,Vector3 size)[] boxes){
  var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.SetPositionAndRotation(position,rotation);
  var kit=new MeshKit();foreach(var b in boxes){kit.Box(0,MeshKit.At(b.centre),b.size,2);var c=Child(root,"Collider",b.centre).AddComponent<BoxCollider>();c.size=b.size;}
  root.AddComponent<MeshFilter>().sharedMesh=kit.Build(name);root.AddComponent<MeshRenderer>().sharedMaterial=material;
  return root;
 }
 /// <summary>Flat outline marking a target (no collider).</summary>
 public static GameObject Ghost(string name,Transform parent,Vector3 position,float yaw,Vector3 size,Material material){
  var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.SetPositionAndRotation(position+Vector3.up*.0015f,Quaternion.Euler(0,yaw,0));
  float w=.004f;var kit=new MeshKit();
  kit.Box(0,MeshKit.At(new Vector3(0,0,size.z*.5f)),new Vector3(size.x,.002f,w),1).Box(0,MeshKit.At(new Vector3(0,0,-size.z*.5f)),new Vector3(size.x,.002f,w),1)
     .Box(0,MeshKit.At(new Vector3(size.x*.5f,0,0)),new Vector3(w,.002f,size.z),1).Box(0,MeshKit.At(new Vector3(-size.x*.5f,0,0)),new Vector3(w,.002f,size.z),1);
  root.AddComponent<MeshFilter>().sharedMesh=kit.Build(name);root.AddComponent<MeshRenderer>().sharedMaterial=material;
  return root;
 }
 /// <summary>
 /// A grabbable activity item: SqueezableObject with a chart grip and material, built inactive then woken so its
 /// material applies on Awake. Placed with its lowest point on 'surface'.
 /// </summary>
 public static ActivityItem Item(string name,string kind,Transform parent,Vector3 surface,float yaw,Mesh mesh,Material[] materials,Action<GameObject> colliders,
  GripType grip,DeformationMaterial material,float mass,Vector3 gripLocal=default,Vector3? axisY=null,Vector3? axisZ=null,float yawSymmetry=360){
  var root=new GameObject(name){layer=9};root.SetActive(false);root.transform.SetParent(parent,true);
  var visual=new GameObject("Deformable surface"){layer=9};visual.transform.SetParent(root.transform,false);
  visual.AddComponent<MeshFilter>().sharedMesh=mesh;visual.AddComponent<MeshRenderer>().sharedMaterials=materials;
  colliders(root);
  var body=root.AddComponent<Rigidbody>();body.mass=Mathf.Max(.01f,mass);body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
  var item=root.AddComponent<SqueezableObject>();item.displayName=name;item.visual=visual.transform;
  var profile=ScriptableObject.CreateInstance<PhysicalMaterialProfile>();profile.name=name+" profile";profile.massKg=Mathf.Max(.01f,mass);profile.friction=.8f;profile.restitution=.05f;
  profile.deformable=!material.Rigid;if(profile.deformable){profile.fullCompressionNewtons=Mathf.Max(1,material.stiffnessNewtons);profile.maxDepthMeters=Mathf.Max(.0001f,material.maxDepthMeters);profile.deformationRadiusMeters=.018f;}
  if(material.result==MaterialResult.Brittle&&material.fractureNewtons>0){profile.breakable=true;profile.sustainedForceNewtons=material.fractureNewtons;}
  item.profile=profile;item.radius=Mathf.Max(.012f,mesh.bounds.extents.magnitude);item.SetMaterialOnAwake(material);
  var prop=root.AddComponent<GripProp>();prop.grip=grip;prop.gripLocal=gripLocal;prop.axisY=axisY??new Vector3(0,1,0);prop.axisZ=axisZ??new Vector3(0,0,1);
  var a=root.AddComponent<ActivityItem>();a.kind=kind;a.yawSymmetry=yawSymmetry;
  var rotation=Quaternion.Euler(0,yaw,0);a.restRotation=Quaternion.identity;
  root.transform.SetPositionAndRotation(surface+Vector3.up*(-mesh.bounds.min.y+.003f),rotation);
  // Props rest where they are laid out until first picked up (round food would otherwise roll off the counter).
  body.isKinematic=true;body.angularDrag=4;
  root.SetActive(true);
  return a;
 }
 public static void BoxC(GameObject r,Vector3 c,Vector3 size){var b=Child(r,"Collider",c).AddComponent<BoxCollider>();b.size=size;}
 public static void SphereC(GameObject r,Vector3 c,float radius){var s=Child(r,"Collider",c).AddComponent<SphereCollider>();s.radius=radius;}
 public static void CapsuleC(GameObject r,Vector3 c,float radius,float height,int direction){var k=Child(r,"Collider",c).AddComponent<CapsuleCollider>();k.radius=radius;k.height=height;k.direction=direction;}
 public static void Octagon(GameObject r,Vector3 c,float radius,float height){float side=radius*2*.924f;BoxC(r,c,new Vector3(side,height,side));var b=Child(r,"Collider",c,new Vector3(0,45,0)).AddComponent<BoxCollider>();b.size=new Vector3(side,height,side);}

 // ---- Meshes (metres, upright, lowest point near y = 0 is not required: Item() rests them on the surface).
 public static Mesh Book(float height,float thickness,float depth)=>new MeshKit().Box(0,MeshKit.At(new Vector3(0,height*.5f,0)),new Vector3(thickness,height,depth),2).Box(1,MeshKit.At(new Vector3(.0006f,height*.5f,0)),new Vector3(thickness*.86f,height*.96f,depth*1.002f),1).Build("Book");
 public static Mesh Cup(float r,float h)=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,0),new Vector2(r*.75f,0),new Vector2(r*.85f,h*.06f),new Vector2(r,h),new Vector2(r*.92f,h),new Vector2(r*.84f,h*.15f),new Vector2(0,h*.15f)},28)
  .Torus(0,MeshKit.At(new Vector3(r*1.12f,h*.55f,0)),h*.22f,.0045f,-90,90,14,6).Build("Cup");
 public static Mesh Plate(float r)=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,0),new Vector2(r*.6f,0),new Vector2(r*.65f,.004f),new Vector2(r,.016f),new Vector2(r*.96f,.018f),new Vector2(r*.62f,.007f),new Vector2(0,.007f)},36).Build("Plate");
 public static Mesh Bowl(float r,float h)=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,0),new Vector2(r*.5f,0),new Vector2(r*.9f,h*.5f),new Vector2(r,h),new Vector2(r*.92f,h),new Vector2(r*.82f,h*.5f),new Vector2(r*.4f,h*.12f),new Vector2(0,h*.12f)},30).Build("Bowl");
 public static Mesh Tray(Vector3 size)=>new MeshKit().Box(0,MeshKit.At(new Vector3(0,.004f,0)),new Vector3(size.x,.008f,size.z),2)
  .Box(0,MeshKit.At(new Vector3(0,size.y*.5f,size.z*.5f-.004f)),new Vector3(size.x,size.y,.008f),1).Box(0,MeshKit.At(new Vector3(0,size.y*.5f,-size.z*.5f+.004f)),new Vector3(size.x,size.y,.008f),1)
  .Box(0,MeshKit.At(new Vector3(size.x*.5f-.004f,size.y*.5f,0)),new Vector3(.008f,size.y,size.z),1).Box(0,MeshKit.At(new Vector3(-size.x*.5f+.004f,size.y*.5f,0)),new Vector3(.008f,size.y,size.z),1).Build("Tray");
 public static Mesh Pan(float r)=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,0),new Vector2(r,0),new Vector2(r*1.04f,.04f),new Vector2(r*.98f,.04f),new Vector2(r*.95f,.006f),new Vector2(0,.006f)},32)
  .Cylinder(1,MeshKit.At(new Vector3(r+.07f,.03f,0),new Vector3(0,0,90)),.011f,.14f,12,4).Build("Pan");
 public static Mesh Spoon()=>new MeshKit().Cylinder(0,MeshKit.At(new Vector3(0,.075f,0)),.0045f,.13f,12,6).Sphere(0,new Vector3(0,-.005f,0),new Vector3(.02f,.008f,.026f),10,16).Build("Spoon");
 public static Mesh Knife()=>new MeshKit().Cylinder(1,MeshKit.At(new Vector3(0,.05f,0)),.011f,.1f,14,6).Box(0,MeshKit.At(new Vector3(0,.16f,.008f)),new Vector3(.003f,.12f,.028f),4).Build("Knife");
 public static Mesh Cucumber(float length,float r)=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero,new Vector3(0,0,-90)),new[]{new Vector2(0,-length*.5f),new Vector2(r*.55f,-length*.48f),new Vector2(r,-length*.4f),new Vector2(r,length*.4f),new Vector2(r*.55f,length*.48f),new Vector2(0,length*.5f)},20).Build("Cucumber");
 public static Mesh Round(float r,float squash=1)=>new MeshKit().Sphere(0,Vector3.zero,new Vector3(r,r*squash,r),14,22).Build("Food");
 public static Mesh Slab(Vector3 size)=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),size,4).Build("Slab");
 /// <summary>
 /// Teapot for the tea activity (spout toward +X, handle toward -X; material 0 glaze, 1 lid knob). The body's inner
 /// radius and height, the spout outlet and the grip match TeaPouring's physics constants.
 /// </summary>
 public static Mesh Teapot(){
  var kit=new MeshKit();
  kit.Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,0),new Vector2(.05f,0),new Vector2(.058f,.006f),new Vector2(.066f,.03f),new Vector2(.068f,.055f),
   new Vector2(.064f,.08f),new Vector2(.052f,.098f),new Vector2(.042f,.104f),new Vector2(.04f,.1f),new Vector2(0,.1f)},36);
  kit.Lathe(0,MeshKit.At(new Vector3(0,.1f,0)),new[]{new Vector2(0,.016f),new Vector2(.02f,.014f),new Vector2(.036f,.007f),new Vector2(.044f,.002f),new Vector2(.044f,0),new Vector2(0,0)},30);
  kit.Sphere(1,new Vector3(0,.122f,0),new Vector3(.009f,.008f,.009f),8,14);
  // Spout: a tapered tube from the belly up to the outlet (TeaPouring.SpoutTip), along TeaPouring.SpoutDirection.
  var from=new Vector3(.052f,.03f,0);var to=TeaPouring.SpoutTip;var d=to-from;float len=d.magnitude;
  float tilt=Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg;
  kit.Lathe(0,MeshKit.At(from,new Vector3(0,0,-tilt)),new[]{new Vector2(0,0),new Vector2(.017f,0),new Vector2(.012f,len*.45f),new Vector2(.0075f,len*.92f),new Vector2(.0068f,len),new Vector2(.0052f,len),new Vector2(0,len)},16);
  // Handle: a loop on the far side, gripped at TeaPouring.TeapotGrip.
  kit.Torus(0,MeshKit.At(new Vector3(-.068f,.062f,0),new Vector3(0,180,0)),.034f,.0065f,-80,80,16,8);
  return kit.Build("Teapot");
 }
 /// <summary>Tea cup with an inner cavity of TeaPouring's cup dimensions (and a handle).</summary>
 public static Mesh TeaCup()=>Cup(TeaPouring.CupR,TeaPouring.CupH);
 public static Mesh Saucer(float r)=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,0),new Vector2(r*.55f,0),new Vector2(r*.6f,.003f),new Vector2(r,.012f),new Vector2(r*.95f,.014f),new Vector2(r*.58f,.006f),new Vector2(0,.006f)},36).Build("Saucer");
 /// <summary>A thin ring for the fill line inside the cup (height h on a wall of radius r).</summary>
 public static Mesh FillLine(float r,float h)=>new MeshKit().Torus(0,MeshKit.At(new Vector3(0,h,0),new Vector3(90,0,0)),r,.0007f,0,360,40,4).Build("Fill line");
 public static Mesh Can(float r,float h)=>new MeshKit().Cylinder(0,MeshKit.At(new Vector3(0,h*.5f,0)),r,h,22,3).Build("Can");
}
}
