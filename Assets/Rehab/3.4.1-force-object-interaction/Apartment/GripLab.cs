using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
/// <summary>
/// How an object is held (Stage 8 grip chart): the grip, the object-local point that sits at the hand's
/// grip anchor, and the object's axes expressed in the hand basis (x = thumb side, y = palm-facing,
/// z = wrist-to-fingers). Objects without this component keep the older shape-based hold.
/// </summary>
public sealed class GripProp : MonoBehaviour {
 public GripType grip;
 public Vector3 gripLocal;
 public Vector3 axisY=new Vector3(0,1,0),axisZ=new Vector3(0,0,1);
 public bool lab;
 public GripDefinition Definition=>GripCatalog.Get(grip);
 /// <summary>World rotation of the object for a hand basis (thumb side, palm-facing, along).</summary>
 public Quaternion HoldRotation(Vector3 thumb,Vector3 palm,Vector3 along){
  var y=thumb*axisY.x+palm*axisY.y+along*axisY.z;var z=thumb*axisZ.x+palm*axisZ.y+along*axisZ.z;
  if(y.sqrMagnitude<1e-8f)y=palm;if(z.sqrMagnitude<1e-8f)z=along;
  z=Vector3.ProjectOnPlane(z,y.normalized);if(z.sqrMagnitude<1e-8f)z=Vector3.Cross(y,thumb);
  return Quaternion.LookRotation(z.normalized,y.normalized);
 }
}

/// <summary>Procedural meshes for the grip-lab props (D7: built in code; no downloads or licences).</summary>
public sealed class MeshKit {
 readonly List<Vector3> v=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();
 readonly List<List<int>> sub=new List<List<int>>();
 List<int> T(int material){while(sub.Count<=material)sub.Add(new List<int>());return sub[material];}
 void Grid(int material,int rows,int cols,Func<float,float,Vector3> at,bool flip=false){
  int start=v.Count;
  for(int r=0;r<=rows;r++)for(int c=0;c<=cols;c++){float a=r/(float)rows,b=c/(float)cols;v.Add(at(a,b));uv.Add(new Vector2(b,a));}
  var t=T(material);
  for(int r=0;r<rows;r++)for(int c=0;c<cols;c++){
   int i=start+r*(cols+1)+c,j=i+cols+1;
   if(flip)t.AddRange(new[]{i,i+1,j,i+1,j+1,j});else t.AddRange(new[]{i,j,i+1,i+1,j,j+1});
  }
 }
 public MeshKit Sphere(int material,Vector3 centre,Vector3 radii,int rings=20,int slices=32,Func<Vector3,float> bump=null){
  Grid(material,rings,slices,(a,b)=>{float p=Mathf.PI*a,q=Mathf.PI*2*b;var n=new Vector3(Mathf.Sin(p)*Mathf.Cos(q),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(q));
   float k=bump!=null?bump(n):1;return centre+Vector3.Scale(n,radii)*k;});
  return this;
 }
 /// <summary>Surface of revolution around local Y. Profile points are (radius, y), bottom to top.</summary>
 public MeshKit Lathe(int material,Matrix4x4 m,Vector2[] profile,int slices=32){
  Grid(material,profile.Length-1,slices,(a,b)=>{
   float f=a*(profile.Length-1);int i=Mathf.Min(profile.Length-2,(int)f);var p=Vector2.Lerp(profile[i],profile[i+1],f-i);
   float q=Mathf.PI*2*b;return m.MultiplyPoint3x4(new Vector3(Mathf.Cos(q)*p.x,p.y,Mathf.Sin(q)*p.x));},true);
  return this;
 }
 public MeshKit Cylinder(int material,Matrix4x4 m,float radius,float height,int slices=28,int rings=6){
  float h=height*.5f;var profile=new List<Vector2>{new Vector2(0,-h)};
  for(int i=0;i<=rings;i++)profile.Add(new Vector2(radius,-h+height*i/rings));profile.Add(new Vector2(0,h));
  return Lathe(material,m,profile.ToArray(),slices);
 }
 /// <summary>Box with each face subdivided, so local dents look smooth.</summary>
 public MeshKit Box(int material,Matrix4x4 m,Vector3 size,int divisions=6){
  var h=size*.5f;
  void Face(Vector3 n,Vector3 u,Vector3 w,float du,float dw,float dn){
   Grid(material,divisions,divisions,(a,b)=>m.MultiplyPoint3x4(n*dn+u*((b-.5f)*2*du)+w*((a-.5f)*2*dw)));
  }
  Face(Vector3.right,Vector3.forward,Vector3.up,h.z,h.y,h.x);Face(Vector3.left,Vector3.back,Vector3.up,h.z,h.y,h.x);
  Face(Vector3.up,Vector3.right,Vector3.forward,h.x,h.z,h.y);Face(Vector3.down,Vector3.left,Vector3.forward,h.x,h.z,h.y);
  Face(Vector3.forward,Vector3.left,Vector3.up,h.x,h.y,h.z);Face(Vector3.back,Vector3.right,Vector3.up,h.x,h.y,h.z);
  return this;
 }
 public MeshKit Torus(int material,Matrix4x4 m,float major,float minor,float fromDegrees,float toDegrees,int around=24,int tube=10){
  Grid(material,around,tube,(a,b)=>{float q=Mathf.Deg2Rad*Mathf.Lerp(fromDegrees,toDegrees,a),t=Mathf.PI*2*b;
   var ring=new Vector3(Mathf.Cos(q),Mathf.Sin(q),0);return m.MultiplyPoint3x4(ring*(major+minor*Mathf.Cos(t))+Vector3.forward*minor*Mathf.Sin(t));},true);
  return this;
 }
 public Mesh Build(string name){
  var mesh=new Mesh{name=name};mesh.indexFormat=v.Count>65000?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
  mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.subMeshCount=sub.Count;for(int i=0;i<sub.Count;i++)mesh.SetTriangles(sub[i],i);
  mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
 }
 public static Matrix4x4 At(Vector3 p,Vector3 euler=default)=>Matrix4x4.TRS(p,Quaternion.Euler(euler),Vector3.one);
}

/// <summary>
/// The Grip Lab station: one code-built prop per grip of the 2026-10-25 chart, placed on the living-room
/// table when selected. Built at runtime so the authored scene asset is unchanged.
/// </summary>
public static class GripLab {
 enum Solid { Rigid, Rubber, Soft, Foam, Porcelain }
 sealed class Spec {
  public string name;public GripType grip;public Solid solid;public float mass,deformRadius,fullNewtons,maxDepth;
  public Vector3 gripLocal,axisY=new Vector3(0,1,0),axisZ=new Vector3(0,0,1);
  public Func<Material[],Mesh> mesh;public Action<GameObject> colliders;public Color[] colors;public float smooth=.4f,metal;
  /// <summary>Stage 9 chart material; when absent it is derived from the solid kind.</summary>
  public Func<DeformationMaterial> material;
 }
 /// <summary>Stage 9: the chart material of a Grip Lab prop built before materials existed.</summary>
 static DeformationMaterial FromSolid(Spec s){
  DeformationMaterial m;
  switch(s.solid){
   case Solid.Rubber:m=DeformationMaterial.Rubber();m.stiffnessNewtons=s.fullNewtons;m.maxDepthMeters=s.maxDepth;break;
   case Solid.Soft:m=DeformationMaterial.Rubber();m.name="Soft rubber foam";m.stiffnessNewtons=s.fullNewtons;m.maxDepthMeters=s.maxDepth;m.dampingRatio=.6f;break;
   case Solid.Foam:m=DeformationMaterial.Fabric();m.maxDepthMeters=s.maxDepth;break;
   case Solid.Porcelain:m=DeformationMaterial.Porcelain();break;
   default:m=DeformationMaterial.RigidSolid(s.name.Contains("Coin")||s.name.Contains("Screw")||s.name.Contains("hammer")||s.name.Contains("Kettlebell")?"Steel":"Rigid plastic");break;
  }
  return m;
 }
 static GameObject Child(GameObject root,string name,Vector3 position,Vector3 euler=default){
  var go=new GameObject(name){layer=root.layer};go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(euler);return go;
 }
 static void SphereC(GameObject r,Vector3 c,float radius){var s=Child(r,"Collider",c).AddComponent<SphereCollider>();s.radius=radius;}
 static void BoxC(GameObject r,Vector3 c,Vector3 size,Vector3 euler=default){var b=Child(r,"Collider",c,euler).AddComponent<BoxCollider>();b.size=size;}
 static void CapsuleC(GameObject r,Vector3 c,float radius,float height,int direction=1){var k=Child(r,"Collider",c).AddComponent<CapsuleCollider>();k.radius=radius;k.height=height;k.direction=direction;}
 // Two boxes at 45 degrees: an octagonal prism within 3 mm of a round cylinder wall, with flat ends.
 static void OctagonC(GameObject r,Vector3 c,float radius,float height){float side=radius*2*.924f;BoxC(r,c,new Vector3(side,height,side));BoxC(r,c,new Vector3(side,height,side),new Vector3(0,45,0));}

 static Spec[] Specs()=>new[]{
  new Spec{name="Blue rubber ball",grip=GripType.Power,solid=Solid.Rubber,mass=.12f,deformRadius=.025f,fullNewtons=35,maxDepth=.01f,colors=new[]{new Color(.09f,.24f,.78f)},smooth=.55f,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,Vector3.one*.036f).Build("Rubber ball"),colliders=g=>SphereC(g,Vector3.zero,.036f)},
  new Spec{name="Small red cube",grip=GripType.Precision,solid=Solid.Rubber,mass=.02f,deformRadius=.01f,fullNewtons=40,maxDepth=.003f,colors=new[]{new Color(.86f,.08f,.06f)},
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),Vector3.one*.024f).Build("Cube"),colliders=g=>BoxC(g,Vector3.zero,Vector3.one*.024f)},
  new Spec{name="Ballpoint pen",grip=GripType.ThreeFinger,solid=Solid.Rigid,mass=.012f,colors=new[]{new Color(.06f,.12f,.32f),new Color(.75f,.76f,.78f)},smooth=.7f,
   gripLocal=new Vector3(0,-.035f,0),axisY=new Vector3(0,-.6f,-.8f),axisZ=new Vector3(1,0,0),
   mesh=_=>new MeshKit().Cylinder(0,MeshKit.At(new Vector3(0,.006f,0)),.0045f,.13f,20,8).Lathe(1,MeshKit.At(new Vector3(0,-.0715f,0)),new[]{new Vector2(0,-.0045f),new Vector2(.0015f,0),new Vector2(.0045f,.012f)},20).Build("Pen"),
   colliders=g=>CapsuleC(g,new Vector3(0,0,0),.0045f,.145f)},
  new Spec{name="Plastic card",grip=GripType.LateralPinch,solid=Solid.Rigid,mass=.005f,colors=new[]{new Color(.68f,.70f,.74f)},smooth=.6f,metal=.2f,
   gripLocal=new Vector3(-.03f,0,0),axisY=new Vector3(0,0,1),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.085f,.054f,.0012f),4).Build("Card"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.085f,.054f,.0012f))},
  new Spec{name="Kettlebell",grip=GripType.Hook,solid=Solid.Rigid,mass=4,colors=new[]{new Color(.05f,.05f,.06f)},smooth=.35f,
   gripLocal=new Vector3(0,.035f,0),axisY=new Vector3(0,-1,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Sphere(0,new Vector3(0,-.085f,0),new Vector3(.075f,.07f,.075f)).Torus(0,MeshKit.At(new Vector3(0,-.03f,0)),.06f,.013f,10,170).Build("Kettlebell"),
   colliders=g=>{SphereC(g,new Vector3(0,-.085f,0),.072f);CapsuleC(g,new Vector3(0,.031f,0),.013f,.11f,0);CapsuleC(g,new Vector3(-.055f,-.005f,0),.013f,.075f);CapsuleC(g,new Vector3(.055f,-.005f,0),.013f,.075f);}},
  new Spec{name="Basketball",grip=GripType.Claw,solid=Solid.Rubber,mass=.6f,deformRadius=.05f,fullNewtons=90,maxDepth=.008f,colors=new[]{new Color(.80f,.33f,.10f)},smooth=.3f,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,Vector3.one*.12f,24,40).Build("Basketball"),colliders=g=>SphereC(g,Vector3.zero,.12f)},
  new Spec{name="Water bottle",grip=GripType.Palm,solid=Solid.Rubber,mass=.5f,deformRadius=.03f,fullNewtons=45,maxDepth=.008f,colors=new[]{new Color(.07f,.09f,.13f),new Color(.12f,.35f,.75f)},smooth=.6f,
   gripLocal=new Vector3(0,-.02f,0),axisY=new Vector3(1,0,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,-.105f),new Vector2(.031f,-.105f),new Vector2(.034f,-.098f),new Vector2(.034f,-.02f),new Vector2(.034f,.06f),new Vector2(.032f,.075f),new Vector2(.018f,.092f),new Vector2(.015f,.098f)},32)
    .Cylinder(1,MeshKit.At(new Vector3(0,.108f,0)),.0165f,.022f,24,2).Build("Bottle"),
   colliders=g=>{OctagonC(g,new Vector3(0,-.02f,0),.034f,.17f);CapsuleC(g,new Vector3(0,.09f,0),.017f,.05f);}},
  new Spec{name="Stress ball",grip=GripType.Spherical,solid=Solid.Soft,mass=.05f,deformRadius=.03f,fullNewtons=18,maxDepth=.016f,colors=new[]{new Color(.04f,.45f,.42f)},smooth=.35f,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,Vector3.one*.032f).Build("Stress ball"),colliders=g=>SphereC(g,Vector3.zero,.032f)},
  new Spec{name="Coin",grip=GripType.FingerTip,solid=Solid.Rigid,mass=.007f,colors=new[]{new Color(.66f,.64f,.58f)},smooth=.75f,metal=.9f,
   gripLocal=new Vector3(0,0,-.006f),axisY=new Vector3(0,1,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Cylinder(0,MeshKit.At(Vector3.zero),.0125f,.002f,28,1).Build("Coin"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.023f,.002f,.023f))},
  new Spec{name="Porcelain cup",grip=GripType.Cupped,solid=Solid.Porcelain,mass=.25f,colors=new[]{new Color(.93f,.93f,.9f)},smooth=.8f,
   axisY=new Vector3(1,0,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,-.042f),new Vector2(.028f,-.042f),new Vector2(.032f,-.038f),new Vector2(.04f,.035f),new Vector2(.041f,.042f),new Vector2(.037f,.042f),new Vector2(.036f,.035f),new Vector2(.03f,-.03f),new Vector2(0,-.03f)},36).Build("Cup"),
   colliders=g=>OctagonC(g,Vector3.zero,.039f,.084f)},
  new Spec{name="USB flash drive",grip=GripType.TwoFinger,solid=Solid.Rigid,mass=.01f,colors=new[]{new Color(.05f,.05f,.06f),new Color(.72f,.72f,.75f)},smooth=.5f,
   gripLocal=new Vector3(0,-.012f,0),axisY=new Vector3(0,0,1),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.018f,.045f,.008f),3).Box(1,MeshKit.At(new Vector3(0,.0285f,0)),new Vector3(.012f,.012f,.0045f),2).Build("USB drive"),
   colliders=g=>{BoxC(g,Vector3.zero,new Vector3(.018f,.045f,.008f));BoxC(g,new Vector3(0,.0285f,0),new Vector3(.012f,.012f,.0045f));}},
  new Spec{name="Smartphone",grip=GripType.LateralThumb,solid=Solid.Rigid,mass=.18f,colors=new[]{new Color(.04f,.05f,.08f)},smooth=.85f,
   gripLocal=new Vector3(0,-.02f,0),axisY=new Vector3(1,0,0),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.072f,.15f,.008f),4).Build("Phone"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.072f,.15f,.008f))},
  new Spec{name="Screw",grip=GripType.PreciseThreeFinger,solid=Solid.Rigid,mass=.004f,colors=new[]{new Color(.55f,.56f,.58f)},smooth=.7f,metal=.9f,
   gripLocal=new Vector3(0,.012f,0),axisY=new Vector3(0,-1,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,-.02f),new Vector2(.0025f,-.014f),new Vector2(.0025f,.016f),new Vector2(.0055f,.017f),new Vector2(.0055f,.02f),new Vector2(0,.02f)},20).Build("Screw"),
   colliders=g=>{CapsuleC(g,new Vector3(0,-.002f,0),.0026f,.036f);CapsuleC(g,new Vector3(0,.0185f,0),.0055f,.011f,0);}},
  new Spec{name="Grip strength tool",grip=GripType.Fist,solid=Solid.Rubber,mass=.2f,deformRadius=.02f,fullNewtons=50,maxDepth=.008f,colors=new[]{new Color(.18f,.19f,.21f)},smooth=.25f,
   axisY=new Vector3(1,0,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,-.062f),new Vector2(.016f,-.06f),new Vector2(.02f,-.05f),new Vector2(.021f,0),new Vector2(.02f,.05f),new Vector2(.016f,.06f),new Vector2(0,.062f)},28).Build("Grip tool"),
   colliders=g=>CapsuleC(g,Vector3.zero,.02f,.124f)},
  new Spec{name="Hardcover book",grip=GripType.Side,solid=Solid.Rigid,mass=.6f,colors=new[]{new Color(.08f,.16f,.36f),new Color(.93f,.91f,.85f)},smooth=.3f,
   gripLocal=new Vector3(.065f,-.03f,0),axisY=new Vector3(-1,0,0),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.15f,.22f,.032f),3).Box(1,MeshKit.At(new Vector3(.003f,0,0)),new Vector3(.147f,.214f,.0322f),2).Build("Book"),
   colliders=g=>BoxC(g,Vector3.zero,new Vector3(.15f,.22f,.032f))},
  new Spec{name="Large rubber ball",grip=GripType.Interlocking,solid=Solid.Rubber,mass=.45f,deformRadius=.05f,fullNewtons=60,maxDepth=.01f,colors=new[]{new Color(.14f,.33f,.82f)},smooth=.5f,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,Vector3.one*.11f,24,40).Build("Large ball"),colliders=g=>SphereC(g,Vector3.zero,.11f)},
  new Spec{name="Cleaning cloth",grip=GripType.Folded,solid=Solid.Foam,mass=.04f,deformRadius=.05f,fullNewtons=10,maxDepth=.025f,colors=new[]{new Color(.45f,.46f,.48f)},smooth=.08f,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,new Vector3(.045f,.036f,.04f),22,34,n=>1+.09f*Mathf.Sin(n.x*13+n.y*7)*Mathf.Cos(n.z*11-n.y*5)+.05f*Mathf.Sin(n.y*23)).Build("Cloth"),
   colliders=g=>SphereC(g,Vector3.zero,.038f)},
  new Spec{name="Claw hammer",grip=GripType.Dynamic,solid=Solid.Rigid,mass=.6f,colors=new[]{new Color(.08f,.08f,.09f),new Color(.6f,.61f,.64f)},smooth=.45f,metal=.4f,
   gripLocal=new Vector3(0,-.07f,0),axisY=new Vector3(1,0,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Cylinder(0,MeshKit.At(new Vector3(0,-.01f,0)),.014f,.27f,20,8).Box(1,MeshKit.At(new Vector3(.01f,.14f,0)),new Vector3(.12f,.028f,.028f),3).Build("Hammer"),
   colliders=g=>{CapsuleC(g,new Vector3(0,-.01f,0),.014f,.27f);BoxC(g,new Vector3(.01f,.14f,0),new Vector3(.12f,.028f,.028f));}},
 };

 // Stage 9 Material Lab: the twelve chart materials (rigid, semi-rigid, flexible, soft) then the forms the
 // chart lists that the twelve do not already show (torsion, shear, buckling). Each is held in a chart grip.
 static Spec[] MaterialSpecs()=>new[]{
  new Spec{name="Steel block",grip=GripType.Power,solid=Solid.Rigid,mass=.7f,colors=new[]{new Color(.55f,.57f,.6f)},smooth=.6f,metal=.9f,material=DeformationMaterial.Steel,
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),Vector3.one*.045f,3).Build("Steel block"),colliders=g=>BoxC(g,Vector3.zero,Vector3.one*.045f)},
  new Spec{name="Glass tumbler",grip=GripType.Cupped,solid=Solid.Porcelain,mass=.25f,colors=new[]{new Color(.78f,.88f,.92f)},smooth=.95f,material=DeformationMaterial.Glass,
   axisY=new Vector3(1,0,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Lathe(0,MeshKit.At(Vector3.zero),new[]{new Vector2(0,-.045f),new Vector2(.03f,-.045f),new Vector2(.036f,.045f),new Vector2(.033f,.045f),new Vector2(.027f,-.038f),new Vector2(0,-.038f)},32).Build("Glass"),
   colliders=g=>OctagonC(g,Vector3.zero,.035f,.09f)},
  new Spec{name="Concrete block",grip=GripType.Power,solid=Solid.Rigid,mass=.25f,colors=new[]{new Color(.6f,.6f,.58f)},smooth=.08f,material=DeformationMaterial.Concrete,
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.05f,.045f,.05f),3).Build("Concrete"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.05f,.045f,.05f))},
  new Spec{name="Wooden plank (bending)",grip=GripType.Dynamic,solid=Solid.Rigid,mass=.15f,colors=new[]{new Color(.72f,.5f,.28f)},smooth=.3f,material=DeformationMaterial.Wood,
   gripLocal=new Vector3(0,-.095f,0),axisY=new Vector3(1,0,0),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.035f,.26f,.012f),14).Build("Plank"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.035f,.26f,.012f))},
  new Spec{name="ABS plastic box",grip=GripType.Power,solid=Solid.Rubber,mass=.06f,deformRadius=.02f,colors=new[]{new Color(.92f,.93f,.95f)},smooth=.5f,material=DeformationMaterial.AbsPlastic,
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.085f,.055f,.06f),8).Build("ABS box"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.085f,.055f,.06f))},
  new Spec{name="Dense foam block",grip=GripType.Power,solid=Solid.Rubber,mass=.03f,deformRadius=.02f,colors=new[]{new Color(.95f,.95f,.9f)},smooth=.1f,material=DeformationMaterial.DenseFoam,
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.08f,.05f,.05f),8).Build("Foam"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.08f,.05f,.05f))},
  new Spec{name="Rubber ball",grip=GripType.Spherical,solid=Solid.Rubber,mass=.1f,deformRadius=.016f,colors=new[]{new Color(.1f,.45f,.85f)},smooth=.55f,material=DeformationMaterial.Rubber,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,Vector3.one*.035f).Build("Rubber ball"),colliders=g=>SphereC(g,Vector3.zero,.035f)},
  new Spec{name="Silicone band (tension)",grip=GripType.Precision,solid=Solid.Rubber,mass=.01f,deformRadius=.008f,colors=new[]{new Color(.92f,.55f,.15f)},smooth=.4f,material=DeformationMaterial.Silicone,
   gripLocal=new Vector3(0,-.055f,0),axisY=new Vector3(0,0,1),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.012f,.12f,.004f),12).Build("Band"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.012f,.12f,.004f))},
  new Spec{name="Fabric",grip=GripType.Folded,solid=Solid.Foam,mass=.04f,deformRadius=.02f,colors=new[]{new Color(.45f,.5f,.58f)},smooth=.08f,material=DeformationMaterial.Fabric,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,new Vector3(.045f,.034f,.04f),22,34,n=>1+.1f*Mathf.Sin(n.x*11+n.y*9)*Mathf.Cos(n.z*13-n.y*4)+.04f*Mathf.Sin(n.y*21)).Build("Fabric"),
   colliders=g=>SphereC(g,Vector3.zero,.037f)},
  new Spec{name="Gel blob",grip=GripType.Spherical,solid=Solid.Soft,mass=.08f,deformRadius=.018f,colors=new[]{new Color(.2f,.75f,.85f)},smooth=.9f,material=DeformationMaterial.Gel,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,new Vector3(.04f,.03f,.04f),22,34).Build("Gel"),colliders=g=>SphereC(g,Vector3.zero,.034f)},
  new Spec{name="Sponge (compression)",grip=GripType.Power,solid=Solid.Soft,mass=.02f,deformRadius=.025f,colors=new[]{new Color(.98f,.8f,.2f)},smooth=.1f,material=DeformationMaterial.Sponge,
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.1f,.065f,.04f),10).Build("Sponge"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.1f,.065f,.04f))},
  new Spec{name="Clay",grip=GripType.Spherical,solid=Solid.Soft,mass=.15f,deformRadius=.016f,colors=new[]{new Color(.45f,.33f,.25f)},smooth=.15f,material=DeformationMaterial.Clay,
   mesh=_=>new MeshKit().Sphere(0,Vector3.zero,Vector3.one*.033f,22,34,n=>1+.04f*Mathf.Sin(n.x*9+n.z*7)).Build("Clay"),colliders=g=>SphereC(g,Vector3.zero,.033f)},
  // A flat strip, as in the chart's torsion photo: a twist on a round wire would not be visible.
  new Spec{name="Steel strip (torsion)",grip=GripType.Dynamic,solid=Solid.Rigid,mass=.03f,colors=new[]{new Color(.62f,.63f,.66f)},smooth=.6f,metal=.9f,material=DeformationMaterial.SteelWire,
   gripLocal=new Vector3(0,-.09f,0),axisY=new Vector3(1,0,0),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.016f,.22f,.003f),12).Build("Strip"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.016f,.22f,.003f))},
  new Spec{name="Paper stack (shear)",grip=GripType.Side,solid=Solid.Rigid,mass=.25f,colors=new[]{new Color(.96f,.96f,.93f)},smooth=.2f,material=DeformationMaterial.PaperStack,
   gripLocal=new Vector3(.045f,-.03f,0),axisY=new Vector3(-1,0,0),axisZ=new Vector3(0,1,0),
   mesh=_=>new MeshKit().Box(0,MeshKit.At(Vector3.zero),new Vector3(.12f,.16f,.03f),10).Build("Paper"),colliders=g=>BoxC(g,Vector3.zero,new Vector3(.12f,.16f,.03f))},
  new Spec{name="Plastic straw (buckling)",grip=GripType.Dynamic,solid=Solid.Rigid,mass=.005f,colors=new[]{new Color(.85f,.2f,.25f)},smooth=.5f,material=DeformationMaterial.PlasticStraw,
   gripLocal=new Vector3(0,-.085f,0),axisY=new Vector3(1,0,0),axisZ=new Vector3(0,0,1),
   mesh=_=>new MeshKit().Cylinder(0,MeshKit.At(Vector3.zero),.0045f,.2f,12,30).Build("Straw"),colliders=g=>CapsuleC(g,Vector3.zero,.0045f,.2f)},
 };

 static PhysicalMaterialProfile Profile(Spec s,DeformationMaterial m){
  var p=Profile(s);
  // The chart material decides deformability and breaking; the profile keeps mass, friction and bounce.
  p.deformable=!m.Rigid;
  if(p.deformable){p.fullCompressionNewtons=Mathf.Max(1,m.stiffnessNewtons);p.maxDepthMeters=Mathf.Clamp(m.maxDepthMeters,.0001f,.5f);if(s.deformRadius<=0)p.deformationRadiusMeters=.03f;}
  if(m.result==MaterialResult.Brittle&&m.fractureNewtons>0){p.breakable=true;p.sustainedForceNewtons=m.fractureNewtons;}
  return p;
 }
 static PhysicalMaterialProfile Profile(Spec s){
  var p=ScriptableObject.CreateInstance<PhysicalMaterialProfile>();p.name=s.name+" profile";p.massKg=Mathf.Max(.01f,s.mass);
  switch(s.solid){
   case Solid.Rubber:p.kind=MaterialKind.Rubber;p.friction=.8f;p.restitution=.5f;break;
   case Solid.Soft:p.kind=MaterialKind.Rubber;p.friction=.85f;p.restitution=.2f;p.recoveryPerSecond=4;break;
   case Solid.Foam:p.kind=MaterialKind.Foam;p.friction=.9f;p.restitution=.02f;p.plasticity=.35f;p.recoveryPerSecond=3;break;
   case Solid.Porcelain:p.kind=MaterialKind.Ceramic;p.deformable=false;p.breakable=true;p.sustainedForceNewtons=60;p.impactEnergyJoules=.6f;p.friction=.45f;p.restitution=.05f;break;
   default:p.kind=MaterialKind.Ceramic;p.deformable=false;p.friction=.6f;p.restitution=.1f;break;
  }
  if(p.deformable){p.fullCompressionNewtons=s.fullNewtons;p.maxDepthMeters=s.maxDepth;p.deformationRadiusMeters=Mathf.Max(.005f,s.deformRadius);}
  return p;
 }

 static Material Paint(Material source,Color color,float smooth,float metal){
  var m=new Material(source){name="Grip lab "+ColorUtility.ToHtmlStringRGB(color)};
  if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",null);if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",null);
  if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);m.color=color;
  if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal);
  return m;
 }

 /// <summary>Builds the Grip Lab props (inactive) on the table at the given surface point, in chart order.</summary>
 public static SqueezableObject[] Build(Transform parent,Vector3 tableTop,Material source)=>BuildSet(Specs(),parent,tableTop,source);
 /// <summary>Stage 9 Material Lab: the deformation chart's materials and forms, in chart order.</summary>
 public static SqueezableObject[] BuildMaterials(Transform parent,Vector3 tableTop,Material source)=>BuildSet(MaterialSpecs(),parent,tableTop,source);
 static SqueezableObject[] BuildSet(Spec[] specs,Transform parent,Vector3 tableTop,Material source){
  var result=new SqueezableObject[specs.Length];
  for(int i=0;i<specs.Length;i++){
   var s=specs[i];
   var root=new GameObject(s.name){layer=9};root.SetActive(false);root.transform.SetParent(parent,true);
   var mats=new Material[s.colors.Length];for(int k=0;k<mats.Length;k++)mats[k]=Paint(source,s.colors[k],s.smooth,s.metal);
   var visual=Child(root,"Deformable surface",Vector3.zero);
   visual.AddComponent<MeshFilter>().sharedMesh=s.mesh(mats);visual.AddComponent<MeshRenderer>().sharedMaterials=mats;
   s.colliders(root);
   var body=root.AddComponent<Rigidbody>();body.mass=Mathf.Max(.01f,s.mass);body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
   var chartMaterial=s.material!=null?s.material():FromSolid(s);
   var item=root.AddComponent<SqueezableObject>();item.displayName=s.name;item.profile=Profile(s,chartMaterial);item.visual=visual.transform;
   item.SetMaterialOnAwake(chartMaterial);
   var bounds=visual.GetComponent<MeshFilter>().sharedMesh.bounds;item.radius=Mathf.Max(.012f,bounds.extents.magnitude);
   var prop=root.AddComponent<GripProp>();prop.grip=s.grip;prop.gripLocal=s.gripLocal;prop.axisY=s.axisY;prop.axisZ=s.axisZ;prop.lab=true;
   // Rest on the table, lowest point down, without overlapping it.
   root.transform.SetPositionAndRotation(tableTop+Vector3.up*(-bounds.min.y+.004f),Quaternion.identity);
   result[i]=item;
  }
  return result;
 }

 /// <summary>Chart grips for the six station objects, so every object is held like its photograph.</summary>
 public static void AssignStationGrips(SqueezableObject[] objects){
  foreach(var item in objects){
   if(item==null||item.GetComponent<GripProp>()!=null)continue;
   var prop=item.gameObject.AddComponent<GripProp>();
   switch(item.displayName){
    case "Silicone ball":prop.grip=GripType.Power;break;
    case "Kitchen sponge":prop.grip=GripType.Power;break;
    case "Brittle cracker":prop.grip=GripType.Precision;prop.axisY=new Vector3(0,0,1);prop.axisZ=new Vector3(0,1,0);break;
    case "Porcelain mug":prop.grip=GripType.Cupped;prop.axisY=new Vector3(1,0,0);prop.axisZ=new Vector3(0,0,1);break;
    case "Silicone cup":prop.grip=GripType.Cupped;prop.axisY=new Vector3(1,0,0);prop.axisZ=new Vector3(0,0,1);break;
    case "Ripe tomato":prop.grip=GripType.Spherical;break;
    default:prop.grip=GripType.Power;break;
   }
   // Stage 9: chart material class and behaviour; the authored profile keeps its force and depth calibration.
   DeformationMaterial m;
   switch(item.displayName){
    case "Silicone ball":m=DeformationMaterial.Rubber();m.name="Silicone rubber";break;
    case "Kitchen sponge":m=DeformationMaterial.Sponge();break;
    case "Brittle cracker":m=DeformationMaterial.DryCracker();break;
    case "Porcelain mug":m=DeformationMaterial.Porcelain();break;
    case "Silicone cup":m=DeformationMaterial.Silicone();m.formMax=0;break;
    case "Ripe tomato":m=DeformationMaterial.Food();break;
    default:m=null;break;
   }
   if(m!=null&&item.profile!=null){
    m.stiffnessNewtons=Mathf.Max(1,item.profile.fullCompressionNewtons);m.maxDepthMeters=item.profile.maxDepthMeters;
    if(m.result==MaterialResult.Brittle)m.fractureNewtons=item.profile.sustainedForceNewtons;
    if(item.profile.deformable)item.SetMaterialOnAwake(m);
   }
   // Hold the collider centre (asset pivots need not be centred).
   // Inactive objects report empty bounds, so use the collider's own centre.
   var col=Array.Find(item.GetComponentsInChildren<Collider>(true),c=>!c.isTrigger);
   Vector3 centre=col is BoxCollider box?box.center:col is SphereCollider sphere?sphere.center:col is CapsuleCollider capsule?capsule.center:col is MeshCollider mesh&&mesh.sharedMesh!=null?mesh.sharedMesh.bounds.center:Vector3.zero;
   if(col!=null)prop.gripLocal=item.transform.InverseTransformPoint(col.transform.TransformPoint(centre));
  }
 }
}
}
