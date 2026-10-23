using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace GripRehab.Editor {
public static class ApartmentOptionsAuthoring {
 const string Folder="Assets/Rehab/3.1-environment/Apartment";
 public static void AddOptions(){
  var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
  if(UnityEngine.Object.FindAnyObjectByType<ApartmentChoices>()!=null)throw new Exception("Options already authored; edit the existing scene rather than overwrite it.");
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  var white=Material("Ivory porcelain",new Color(.85f,.88f,.82f),.7f);var teal=Material("Soft silicone cup",new Color(.09f,.43f,.40f),.36f);var red=Material("Tomato skin",new Color(.66f,.045f,.025f),.4f);var green=Material("Tomato leaf",new Color(.12f,.22f,.025f),.15f);
  var fallback=Material("Warm bulb surface",new Color(.95f,.92f,.8f),.4f);
  int missing=0;foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   var mats=renderer.sharedMaterials;bool changed=false;for(int i=0;i<mats.Length;i++)if(mats[i]==null||mats[i].shader==null||mats[i].shader.name=="Hidden/InternalErrorShader"){mats[i]=fallback;changed=true;missing++;}
   if(changed)renderer.sharedMaterials=mats;
  }
  Debug.Log("Repaired missing vendor material slots: "+missing);
  var ball=app.objects[0];var sponge=app.objects[1];var cracker=app.objects[2];ball.displayName="Silicone ball";sponge.displayName="Kitchen sponge";cracker.displayName="Brittle cracker";
  var cupMesh=CupMesh();AssetDatabase.CreateAsset(cupMesh,Folder+"/CupMesh.asset");
  var mug=Object("Porcelain mug",cupMesh,white,ball.transform.position+Vector3.down*.01f,ball.transform.parent,new Vector3(.115f,.115f,.115f));mug.breakable=true;mug.maximumCompression=.025f;mug.fullCompressionNewtons=200;mug.fractureNewtons=60;mug.fragmentsPrefab=Shards(white);mug.BodyIfEditor();
  var cup=Object("Silicone cup",cupMesh,teal,sponge.transform.position+Vector3.up*.017f,sponge.transform.parent,new Vector3(.12f,.12f,.12f));cup.maximumCompression=.38f;cup.fullCompressionNewtons=30;
  var tomatoMesh=TomatoMesh();AssetDatabase.CreateAsset(tomatoMesh,Folder+"/TomatoMesh.asset");
  var tomato=Object("Ripe tomato",tomatoMesh,red,sponge.transform.position+Vector3.up*.005f,sponge.transform.parent,new Vector3(.09f,.085f,.09f));tomato.maximumCompression=.3f;tomato.fullCompressionNewtons=28;
  var leaves=new GameObject("Tomato calyx",typeof(MeshFilter),typeof(MeshRenderer));leaves.transform.SetParent(tomato.visual,false);leaves.GetComponent<MeshFilter>().sharedMesh=LeafMesh();AssetDatabase.CreateAsset(leaves.GetComponent<MeshFilter>().sharedMesh,Folder+"/TomatoCalyx.asset");leaves.GetComponent<MeshRenderer>().sharedMaterial=green;leaves.layer=9;
  app.objects=new[]{ball,sponge,cracker,mug,cup,tomato};
  var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
  var panel=new GameObject("Station and object selector",typeof(RectTransform),typeof(Image),typeof(CanvasGroup));panel.transform.SetParent(canvas.transform,false);
  var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(75,-24);rect.sizeDelta=new Vector2(430,206);panel.GetComponent<Image>().color=new Color(.025f,.055f,.105f,.93f);
  var menu=panel.AddComponent<ApartmentChoices>();menu.experience=app;menu.controls=panel.GetComponent<CanvasGroup>();menu.controls.interactable=false;menu.controls.blocksRaycasts=false;
  menu.heading=TextAt(panel.transform,"ESC / TAB  •  STATIONS & OBJECTS",new Vector2(0,-19),new Vector2(400,28),16);
  menu.livingButton=ButtonAt(panel.transform,"LIVING ROOM",new Vector2(-103,-57),new Vector2(198,35));menu.kitchenButton=ButtonAt(panel.transform,"KITCHEN",new Vector2(103,-57),new Vector2(198,35));
  menu.objectButtons=new[]{ButtonAt(panel.transform,"Silicone ball",new Vector2(-139,-107),new Vector2(132,48)),ButtonAt(panel.transform,"Porcelain mug",new Vector2(0,-107),new Vector2(132,48)),ButtonAt(panel.transform,"Brittle cracker",new Vector2(139,-107),new Vector2(132,48))};
  menu.inputButton=ButtonAt(panel.transform,"INPUT: DEMO",new Vector2(-103,-164),new Vector2(198,37));menu.inputLabel=menu.inputButton.GetComponentInChildren<Text>();menu.resumeButton=ButtonAt(panel.transform,"RESUME / EXPLORE",new Vector2(103,-164),new Vector2(198,37));
  menu.livingObjects=new[]{ball,mug,cracker};menu.kitchenObjects=new[]{sponge,cup,tomato};menu.kitchenStation=GameObject.Find("Kitchen standing station").transform;
  // Keep a single selected object visible per station. All options are serialized editable scene objects.
  cracker.gameObject.SetActive(false);mug.gameObject.SetActive(false);cup.gameObject.SetActive(false);tomato.gameObject.SetActive(false);
  PrefabUtility.RecordPrefabInstancePropertyModifications(app);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  foreach(var path in AssetDatabase.GetDependencies(ApartmentAuthoring.ScenePath,true))if(AssetImporter.GetAtPath(path) is TextureImporter texture&&texture.maxTextureSize>2048){texture.maxTextureSize=2048;texture.SaveAndReimport();}
  ApartmentAuthoring.VerifyScene();RehabEditorQA.VerifyEditPersistence();
 }
 static Material Material(string name,Color color,float smooth){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,Folder+"/Materials/"+name.Replace(' ','_')+".mat");return m;}
 static SqueezableObject Object(string name,Mesh mesh,Material material,Vector3 pos,Transform parent,Vector3 scale){
  var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=pos;go.layer=9;
  var visual=new GameObject("Deformable surface",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(go.transform,false);visual.transform.localScale=scale;visual.layer=9;visual.GetComponent<MeshFilter>().sharedMesh=mesh;visual.GetComponent<MeshRenderer>().sharedMaterial=material;
  var collider=go.AddComponent<BoxCollider>();collider.center=Vector3.Scale(mesh.bounds.center,scale);collider.size=Vector3.Scale(mesh.bounds.size,scale);
  var rb=go.AddComponent<Rigidbody>();rb.mass=.18f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
  var obj=go.AddComponent<SqueezableObject>();obj.displayName=name;obj.visual=visual.transform;obj.radius=.085f;return obj;
 }
 static Text TextAt(Transform parent,string text,Vector2 pos,Vector2 size,int fontsize){var go=new GameObject(text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.anchoredPosition=pos;r.sizeDelta=size;var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=fontsize;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.86f,.94f,1);t.raycastTarget=false;return t;}
 static Button ButtonAt(Transform parent,string text,Vector2 pos,Vector2 size){var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.anchoredPosition=pos;r.sizeDelta=size;go.GetComponent<Image>().color=new Color(.10f,.19f,.29f);var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();var t=TextAt(go.transform,text,new Vector2(0,-size.y*.5f),size-new Vector2(8,4),16);return button;}
 static Mesh CupMesh(){
  var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
  // Lathed cross section: underside -> rounded outer wall -> lip -> inner wall -> basin.
  var rings=new[]{new Vector2(0,-.5f),new Vector2(.30f,-.5f),new Vector2(.34f,-.47f),new Vector2(.39f,.40f),new Vector2(.38f,.46f),new Vector2(.34f,.46f),new Vector2(.33f,.40f),new Vector2(.28f,-.39f),new Vector2(0,-.39f)};
  const int segments=64;foreach(var ring in rings)for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(a)*ring.x,ring.y,Mathf.Sin(a)*ring.x));uv.Add(new Vector2(i/(float)segments,(ring.y+.5f)));}
  for(int r=0;r<rings.Length-1;r++)for(int i=0;i<segments;i++){int a=r*(segments+1)+i,b=a+segments+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
  // Continuous loop handle, joined visually to the cup wall.
  int offset=vertices.Count;const int major=40,minor=10;
  for(int i=0;i<=major;i++){float a=i*Mathf.PI*2/major;for(int j=0;j<=minor;j++){float b=j*Mathf.PI*2/minor;vertices.Add(new Vector3(.40f+(.21f+.045f*Mathf.Cos(b))*Mathf.Cos(a),.02f+(.29f+.045f*Mathf.Cos(b))*Mathf.Sin(a),.045f*Mathf.Sin(b)));uv.Add(new Vector2(i/(float)major,j/(float)minor));}}
  for(int i=0;i<major;i++)for(int j=0;j<minor;j++){int a=offset+i*(minor+1)+j,b=a+minor+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
  return Mesh(vertices,triangles,uv,"Hollow cup with rounded lip and handle");
 }
 static Mesh TomatoMesh(){var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();const int rings=24,slices=48;for(int r=0;r<=rings;r++){float p=Mathf.PI*r/rings;for(int s=0;s<=slices;s++){float a=Mathf.PI*2*s/slices;float radius=.5f*Mathf.Sin(p)*(1+.035f*Mathf.Cos(5*a)*Mathf.Sin(p));v.Add(new Vector3(radius*Mathf.Cos(a),.46f*Mathf.Cos(p),radius*Mathf.Sin(a)));uv.Add(new Vector2(s/(float)slices,r/(float)rings));}}
  for(int r=0;r<rings;r++)for(int s=0;s<slices;s++){int a=r*(slices+1)+s,b=a+slices+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}return Mesh(v,t,uv,"Lobed ripe tomato");}
 static Mesh LeafMesh(){var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-direction.z,0,direction.x)*.065f;int n=v.Count;v.Add(new Vector3(0,.475f,0));v.Add(direction*.12f+side+Vector3.up*.50f);v.Add(direction*.31f+Vector3.up*.42f);v.Add(direction*.12f-side+Vector3.up*.50f);uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+2,n+1,n,n+3,n+2,n});}return Mesh(v,t,uv,"Tomato calyx");}
 static Mesh Mesh(List<Vector3> v,List<int> t,List<Vector2> uv,string name){var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
 static GameObject Shards(Material material){var root=new GameObject("Porcelain shards");for(int i=0;i<14;i++){float a=i*Mathf.PI*2/14;var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(Mathf.Cos(a)*.035f,(i%3-1)*.025f,Mathf.Sin(a)*.035f);go.transform.localScale=new Vector3(.023f,.033f,.007f);go.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);go.GetComponent<Renderer>().sharedMaterial=material;go.AddComponent<Rigidbody>().mass=.008f;}var asset=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Prefabs/PorcelainShards.prefab");UnityEngine.Object.DestroyImmediate(root);return asset;}
 static void BodyIfEditor(this SqueezableObject item){} // All physical state is initialized by Awake in Play mode.
}
}
