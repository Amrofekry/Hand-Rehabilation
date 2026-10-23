using System;
using System.Collections.Generic;
using GripRehab;
using UnityEngine;
// Real production solver/partition source with managed test math only; no native physics/rendering.
class DeformationTests {
 static int passed;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
 static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.00001f;
 static void Invalid(Action action,string name){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,name);}
 static Vector3[] Grid()=>new[]{new Vector3(0,.1f,0),new Vector3(.01f,.1f,0),new Vector3(-.025f,.1f,.015f),new Vector3(.025f,.1f,-.025f),new Vector3(.15f,.1f,0),new Vector3(0,-.1f,0)};
 static void Load(LocalDeformationState state,float depth,float bulge,float plastic){
  state.BeginStep();var surface=state.Rest[0];
  for(int i=0;i<state.Rest.Length;i++)state.SetTarget(i,ContactDeformation.Indentation(state.Rest[i],surface,Vector3.up,depth,.06f,state.Rest[i],bulge));
  state.Step(.02f,8,plastic);
 }
 static void Main(){
  Check(ContactDeformation.Depth(80,35,.018f,3)==.018f,"Maximum pressure saturates at material depth");
  Check(ContactDeformation.Depth(18,35,.018f,3)>0,"Normal demo pressure makes a visible dent");
  Check(ContactDeformation.Depth(18,35,.018f,0)==0,"No contact creates no dent");
  Check(ContactDeformation.Depth(0,35,.018f,3)==0,"Zero force creates no dent");
  Check(ContactDeformation.Depth(-2,35,.018f,3)==0,"Negative force creates no dent");
  Check(ContactDeformation.Depth(80,35,0,3)==0,"Rigid material cannot bend");
  Check(ContactDeformation.Depth(80,35,.018f,15)<ContactDeformation.Depth(80,35,.018f,3),"Many phalanges do not each receive full pressure");
  foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
   Check(ContactDeformation.Depth(value,35,.018f,3)==0,"Bad force fails closed "+value);
   Check(ContactDeformation.Depth(80,value,.018f,3)==0,"Bad stiffness fails closed "+value);
   Check(ContactDeformation.Depth(80,35,value,3)==0,"Bad material depth fails closed "+value);
   Check(ContactDeformation.SafeDt(value)==0,"Bad timestep ignored "+value);
  }
  Check(ContactDeformation.SafeDt(1)==.05f,"Frame hitch cannot fake one second of damage");
  var rest=Grid();
  Vector3 dent=ContactDeformation.Indentation(rest[0],rest[0],Vector3.up,.018f,.06f,rest[0],.18f);
  Check(dent.y<-.01f&&Vector3.Distance(dent,new Vector3())<=.01801f,"Centre indentation is inward and bounded");
  Check(Near(ContactDeformation.Indentation(rest[4],rest[0],Vector3.up,.018f,.06f,rest[4],.18f),new Vector3()),"Distant vertices untouched");
  Check(Near(ContactDeformation.Indentation(rest[0],rest[0],new Vector3(),.018f,.06f,rest[0],0),new Vector3()),"Degenerate normal creates no displacement");
  Check(Near(dent,ContactDeformation.Indentation(rest[0],rest[0],Vector3.up,.018f,.06f,rest[0],.18f)),"No frame-to-frame random jitter");
  var left=ContactDeformation.Indentation(rest[2],rest[0],Vector3.up,.018f,.06f,rest[2],.18f);
  var right=ContactDeformation.Indentation(rest[3],rest[0],Vector3.up,.018f,.06f,rest[3],.18f);
  Check(Math.Abs(left.y-right.y)>.0001f,"Contact deformation has irregular local shape");
  Check(ContactDeformation.Indentation(rest[1],rest[0],Vector3.up,.018f,.06f,rest[1],.18f).x>0,"Rubber has a small lateral bulge");
  foreach(float sign in new[]{-1f,1f}){
   var rotation=Quaternion.AngleAxis(sign*80,Vector3.forward);
   var a=ContactDeformation.Indentation(rotation*rest[2],rotation*rest[0],rotation*Vector3.up,.018f,.06f,rest[2],.18f);
   Check(Near(a,rotation*left),"Contact response follows object rotation "+sign);
  }
  var rubber=new LocalDeformationState(rest);
  Check(!ReferenceEquals(rest,rubber.Rest)&&!ReferenceEquals(rest,rubber.Vertices),"State never mutates the shared source mesh array");
  for(int i=0;i<80;i++)Load(rubber,.018f,.18f,0);
  Check(rubber.Vertices[0].y<rubber.Rest[0].y-.01f,"Rubber visibly indents under a sustained squeeze");
  Check(Near(rubber.Vertices[4],rest[4])&&Near(rubber.Vertices[5],rest[5]),"No global object scaling or far-side collapse");
  Vector3 original=rest[0];
  Check(Near(rest[0],original),"Original rest array unchanged");
  bool finalUpload=false;
  for(int i=0;i<500;i++){
   rubber.BeginStep();var was=rubber.Vertices[0];bool changed=rubber.Step(.02f,9,0);
   if(was.y!=rest[0].y&&rubber.Vertices[0].y==rest[0].y)finalUpload|=changed;
  }
  Check(Near(rubber.Vertices[0],rest[0]),"Rubber fully recovers after release");
  Check(finalUpload,"Final recovery position requests a mesh upload");
  Check(!rubber.Step(.02f,9,0),"Settled mesh avoids unnecessary re-uploads");
  var food=new LocalDeformationState(rest);
  for(int i=0;i<80;i++)Load(food,.025f,0,.25f);
  float peak=Vector3.Distance(food.Vertices[0],rest[0]);
  for(int i=0;i<500;i++){food.BeginStep();food.Step(.02f,3,.25f);}
  float residual=Vector3.Distance(food.Vertices[0],rest[0]);
  Check(residual>.001f&&residual<peak,"Tomato-like material retains a bounded dent");
  food.Reset();Check(Near(food.Vertices[0],rest[0])&&!food.Step(.02f,3,.25f),"Reset restores both visible mesh and permanent state");
  var state=new LocalDeformationState(rest);state.BeginStep();state.SetTarget(0,new Vector3(0,-.01f,0));
  Check(!state.Step(float.NaN,9,0)&&Near(state.Vertices[0],rest[0]),"Bad time cannot poison vertices");
  state.SetTarget(0,new Vector3(float.NaN,0,0));state.Step(.02f,9,0);
  Check(GraspMotionPolicy.Finite(state.Vertices[0]),"Bad displacement is rejected");
  float dwell=0;
  Check(ContactDeformation.GripDamage(ref dwell,true,80,.02f,60,.35f,.35f)==0,"One peak frame cannot break a mug");
  for(int i=0;i<10;i++)ContactDeformation.GripDamage(ref dwell,true,80,.02f,60,.35f,.35f);
  Check(ContactDeformation.GripDamage(ref dwell,false,80,.02f,60,.35f,.35f)==0&&dwell==0,"Contact loss resets continuous high-force dwell");
  Check(ContactDeformation.GripDamage(ref dwell,true,40,.02f,60,.35f,.35f)==0,"Subthreshold squeeze does not damage porcelain");
  float damage=0;for(int i=0;i<35;i++)damage+=ContactDeformation.GripDamage(ref dwell,true,80,.02f,60,.35f,.35f);
  Check(damage>=1,"Sustained maximum visual/demo force can break porcelain");
  dwell=0;damage=0;for(int i=0;i<20;i++)damage+=ContactDeformation.GripDamage(ref dwell,true,65,.02f,55,.25f,.4f);
  Check(damage>=1,"Cracker has its own brittle-force response");
  dwell=0;Check(ContactDeformation.GripDamage(ref dwell,true,float.NaN,.02f,60,.35f,.35f)==0,"Invalid grip does not accumulate damage");
  Check(ContactDeformation.GripDamage(ref dwell,true,80,.02f,60,0,.35f)==0,"Invalid dwell setting fails closed");
  Check(ContactDeformation.GripDamage(ref dwell,true,80,.02f,60,.35f,0)==0,"Invalid damage capacity fails closed");
  Check(ContactDeformation.ImpactDamage(.18f,.1f,.13f)==0,"Gentle porcelain contact is harmless");
  Check(ContactDeformation.ImpactDamage(.18f,16,.13f)>=1,"Strong porcelain impact fractures");
  Check(ContactDeformation.ImpactDamage(.18f,float.NaN,.13f)==0,"Bad impact input is rejected");
  Check(ContactDeformation.ImpactDamage(.18f,16,0)==0,"Invalid impact threshold fails closed");
  var vertices=new List<Vector3>();var triangles=new List<int>();
  // Synthetic cup wall with an open top: validates surface-piece fidelity, not a screenshot.
  for(int ring=0;ring<3;ring++)for(int s=0;s<16;s++){float a=s*(float)Math.PI*2/16;vertices.Add(new Vector3(.04f*(float)Math.Cos(a),ring*.04f,.04f*(float)Math.Sin(a)));}
  for(int ring=0;ring<2;ring++)for(int s=0;s<16;s++){int a=ring*16+s,b=ring*16+(s+1)%16;triangles.AddRange(new[]{a,b,a+16,b,b+16,a+16});}
  var input=vertices.ToArray();var indices=triangles.ToArray();
  var layout=FracturePartitioner.Split(input,indices,12);
  Check(layout.chunks.Length>1&&layout.chunks.Length<=12,"Fracture produces multiple bounded source-shaped pieces");
  int triangleCount=0;bool geometry=true,compact=true,indicesValid=true;
  foreach(var piece in layout.chunks){
   triangleCount+=piece.triangles.Length;
   for(int i=0;i<piece.vertices.Length;i++)geometry&=Near(piece.vertices[i]+piece.center,input[piece.sourceIndices[i]]);
   compact&=piece.vertices.Length==piece.sourceIndices.Length;
   foreach(int index in piece.triangles)indicesValid&=index>=0&&index<piece.vertices.Length;
  }
  Check(triangleCount==indices.Length,"No source triangle disappears during fracture");
  Check(geometry,"Reassembled piece vertices match original geometry");
  Check(compact&&indicesValid,"Each fragment uses compact valid mesh indices");
  var again=FracturePartitioner.Split(input,indices,12);bool deterministic=again.trianglePieces.Length==layout.trianglePieces.Length;
  for(int i=0;i<layout.trianglePieces.Length;i++)deterministic&=layout.trianglePieces[i]==again.trianglePieces[i];
  Check(deterministic,"Repeated fracture partition is deterministic");
  Check(FracturePartitioner.Split(input,indices,1000).chunks.Length<=24,"Piece count is bounded");
  Invalid(()=>FracturePartitioner.Split(input,new[]{0,1},12),"Incomplete triangles rejected");
  Invalid(()=>FracturePartitioner.Split(input,new[]{0,1,999},12),"Invalid index rejected");
  Invalid(()=>FracturePartitioner.Split(new[]{new Vector3(float.NaN,0,0)},new[]{0,0,0},1),"Nonfinite geometry rejected");
  Invalid(()=>new LocalDeformationState(new[]{new Vector3(float.PositiveInfinity,0,0)}),"Nonfinite rest vertices rejected");
  Console.WriteLine("Deformation/fracture checks passed: "+passed);
 }
}
