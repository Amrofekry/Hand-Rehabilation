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
  MaterialChartChecks();
  ActivityChecks();
  Console.WriteLine("Deformation/fracture checks passed: "+passed);
 }

 // ---- Stage 10: activity rules and the plane cutter (production code).
 static void ActivityChecks(){
  var easy=ActivityTuning.ForLevel(1);var hard=ActivityTuning.ForLevel(10);
  Check(hard.positionTolerance<easy.positionTolerance&&hard.angleTolerance<easy.angleTolerance&&hard.items>=easy.items&&hard.cuts>easy.cuts&&hard.stirSeconds>easy.stirSeconds,"Higher levels ask for more items and tighter placement");
  Check(ActivityTuning.ForLevel(-5).items==easy.items&&ActivityTuning.ForLevel(99).cuts==hard.cuts,"Out-of-range levels are clamped");
  Check(Math.Abs(ActivityRules.YawError(350,10)-20)<1e-4f&&Math.Abs(ActivityRules.YawError(0,170,180)-10)<1e-4f&&ActivityRules.YawError(float.NaN,0)==180,"Yaw error wraps and honours symmetry");
  var perfect=ActivityRules.Placement(0,0,.04f,20);var near=ActivityRules.Placement(.02f,10,.04f,20);var off=ActivityRules.Placement(.05f,0,.04f,20);
  Check(perfect.accepted&&Math.Abs(perfect.score-1)<1e-5f&&near.accepted&&near.score<perfect.score&&near.score>0&&!off.accepted&&off.score==0,"Placement scores fall with error and fail past the tolerance");
  Check(!ActivityRules.Placement(float.NaN,0,.04f,20).accepted&&!ActivityRules.Placement(0,0,0,20).accepted,"Bad placement input is not accepted");
  var heights=new[]{.21f,.15f,.18f};
  Check(ActivityRules.BookFitsSlot(heights,1,0)&&ActivityRules.BookFitsSlot(heights,2,1)&&ActivityRules.BookFitsSlot(heights,0,2)&&!ActivityRules.BookFitsSlot(heights,0,0),"Books go shortest to tallest, left to right");
  Check(!ActivityRules.BookFitsSlot(heights,5,0)&&!ActivityRules.BookFitsSlot(null,0,0),"Bad book or slot is rejected");
  Check(Math.Abs(ActivityRules.Evenness(new[]{.03f,.03f,.03f})-1)<1e-5f&&ActivityRules.Evenness(new[]{.01f,.05f})<.5f&&ActivityRules.Evenness(new[]{.03f,-1f})==0,"Cut evenness: equal pieces 1, uneven lower, bad lengths 0");
  Check(ActivityRules.Score(1,true,0)==100&&ActivityRules.Score(1,false,0)<=75&&ActivityRules.Score(1,true,5)==85&&ActivityRules.Score(float.NaN,true,0)==40,"Score: accuracy, completion and errors");
  Check(ActivityRules.Stars(100)==3&&ActivityRules.Stars(70)==2&&ActivityRules.Stars(10)==1&&ActivityRules.Stars(0)==0,"Stars match the other exercises");
  Check(ActivityRules.Task(ActivityKind.Bookshelf)==2&&ActivityRules.Task(ActivityKind.PrepareAndServe)==7&&ActivityRules.Kitchen(ActivityKind.Cutting)&&!ActivityRules.Kitchen(ActivityKind.StorageBox),"Activities are Module 1 tasks 2-7, kitchen ones flagged");
  var steps=new ActivitySteps();steps.Add("Place the books",3);steps.Add("Check the order");
  steps.Advance();steps.Advance();Check(!steps.Complete&&steps.CurrentDone==2&&Math.Abs(steps.Progress-.5f)<1e-5f,"Steps count units toward progress");
  steps.Retract();Check(steps.CurrentDone==1,"An item taken back is uncounted");
  steps.Advance(5);steps.Advance();Check(steps.Complete&&Math.Abs(steps.Progress-1)<1e-5f&&steps.Instruction=="Done"&&!steps.Advance(),"All steps complete; extra credit is ignored");
  steps.Reset();Check(steps.Current==0&&steps.Progress==0,"Replay resets the steps");

  // Plane cutter on a sphere: two closed pieces whose volumes add up to the original.
  Sphere(.03f,16,24,out var v,out var t);float whole=Math.Abs(DeformationDynamics.Volume(v,t));
  Check(PlaneCutter.Split(v,t,new Vector3(0,.01f,0),Vector3.up,out var above,out var below),"A plane through the object cuts it");
  float va=Math.Abs(DeformationDynamics.Volume(above.vertices.ToArray(),above.triangles.ToArray())),vb=Math.Abs(DeformationDynamics.Volume(below.vertices.ToArray(),below.triangles.ToArray()));
  Check(Math.Abs(va+vb-whole)/whole<.01f&&va<vb,"Cut pieces are closed and their volumes add up ("+(va+vb)/whole*100+" %)");
  bool sides=true;foreach(var p in above.vertices)sides&=p.y>=.01f-1e-5f;foreach(var p in below.vertices)sides&=p.y<=.01f+1e-5f;
  Check(sides,"Each piece lies on its own side of the cut");
  Check(!PlaneCutter.Split(v,t,new Vector3(0,.5f,0),Vector3.up,out _,out _),"A plane that misses the object does not cut");
  Check(!PlaneCutter.Split(v,t,new Vector3(float.NaN,0,0),Vector3.up,out _,out _)&&!PlaneCutter.Split(v,new[]{0,1},Vector3.zero,Vector3.up,out _,out _)&&!PlaneCutter.Split(v,t,Vector3.zero,Vector3.zero,out _,out _),"Bad cut input is rejected");
  var flipped=(int[])t.Clone();for(int i=0;i<flipped.Length;i+=3){int s=flipped[i+1];flipped[i+1]=flipped[i+2];flipped[i+2]=s;}
  PlaneCutter.Split(v,flipped,Vector3.zero,Vector3.right,out var fa,out var fb);
  float fva=DeformationDynamics.Volume(fa.vertices.ToArray(),fa.triangles.ToArray()),fvb=DeformationDynamics.Volume(fb.vertices.ToArray(),fb.triangles.ToArray());
  Check(Math.Sign(fva)==Math.Sign(DeformationDynamics.Volume(v,flipped))&&Math.Abs(Math.Abs(fva)+Math.Abs(fvb)-whole)/whole<.01f,"Cut faces follow the mesh's own winding");
 }

 // ---- Stage 9: the deformation chart (materials, dynamics, volume, forms) on production code.
 static void Sphere(float r,int rings,int slices,out Vector3[] v,out int[] t){
  var vs=new System.Collections.Generic.List<Vector3>();var ts=new System.Collections.Generic.List<int>();
  for(int a=0;a<=rings;a++)for(int b=0;b<=slices;b++){double p=Math.PI*a/rings,q=2*Math.PI*b/slices;vs.Add(new Vector3((float)(Math.Sin(p)*Math.Cos(q))*r,(float)Math.Cos(p)*r,(float)(Math.Sin(p)*Math.Sin(q))*r));}
  for(int a=0;a<rings;a++)for(int b=0;b<slices;b++){int i=a*(slices+1)+b,j=i+slices+1;ts.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
  v=vs.ToArray();t=ts.ToArray();
 }
 static float Volume(LocalDeformationState s,int[] t)=>Math.Abs(DeformationDynamics.Volume(s.Vertices,t));
 static LocalDeformationState Squeezed(DeformationMaterial m,Vector3[] v,int[] t,float depth,int steps){
  var s=new LocalDeformationState(v);s.SetTopology(t);float restVolume=Math.Abs(DeformationDynamics.Volume(v,t));
  for(int k=0;k<steps;k++){
   s.BeginStep();
   for(int i=0;i<v.Length;i++){var d=ContactDeformation.Indentation(v[i],new Vector3(0,.035f,0),Vector3.up,depth,.025f,v[i],0);if(d.sqrMagnitude>0)s.SetTarget(i,d);}
   s.StepDynamic(.02f,m,restVolume);
  }
  return s;
 }
 static void Release(LocalDeformationState s,DeformationMaterial m,int[] t,int steps){float rv=Math.Abs(DeformationDynamics.Volume(s.Rest,t));for(int k=0;k<steps;k++){s.BeginStep();s.StepDynamic(.02f,m,rv);}}
 static void MaterialChartChecks(){
  var chart=DeformationMaterial.ChartMaterials();
  string[] names={"Steel","Glass","Concrete","Wood","ABS plastic","Dense foam","Rubber","Silicone","Fabric","Gel","Sponge","Clay"};
  MaterialClass[] classes={MaterialClass.Rigid,MaterialClass.Rigid,MaterialClass.Rigid,MaterialClass.SemiRigid,MaterialClass.SemiRigid,MaterialClass.SemiRigid,MaterialClass.Flexible,MaterialClass.Flexible,MaterialClass.Flexible,MaterialClass.Soft,MaterialClass.Soft,MaterialClass.Soft};
  bool order=chart.Length==12;for(int i=0;i<chart.Length&&order;i++)order&=chart[i].name==names[i]&&chart[i].materialClass==classes[i];
  Check(order,"Chart: twelve example materials in chart order with their class (rigid, semi-rigid, flexible, soft)");
  Check(DeformationMaterial.Glass().result==MaterialResult.Brittle&&DeformationMaterial.Glass().fractureNewtons>0&&DeformationMaterial.Clay().result==MaterialResult.Plastic&&DeformationMaterial.Rubber().result==MaterialResult.Elastic,"Chart results: glass fractures, clay stays deformed, rubber returns");
  Check(DeformationMaterial.Steel().Rigid&&!DeformationMaterial.Rubber().Rigid&&!DeformationMaterial.Wood().Rigid,"Rigid materials do not dent; wood bends as a whole");
  bool soft=true;foreach(var m in chart)if(m.materialClass==MaterialClass.Soft)soft&=m.maxDepthMeters>DeformationMaterial.Steel().maxDepthMeters*100;
  Check(soft&&DeformationMaterial.Gel().maxDepthMeters>DeformationMaterial.AbsPlastic().maxDepthMeters,"Soft materials dent far more than rigid or semi-rigid ones");
  var forms=new[]{DeformationMaterial.Sponge().form,DeformationMaterial.Silicone().form,DeformationMaterial.Wood().form,DeformationMaterial.PaperStack().form,DeformationMaterial.SteelWire().form,DeformationMaterial.PlasticStraw().form};
  Check(forms[0]==DeformationForm.Compression&&forms[1]==DeformationForm.Tension&&forms[2]==DeformationForm.Bending&&forms[3]==DeformationForm.Shear&&forms[4]==DeformationForm.Torsion&&forms[5]==DeformationForm.Buckling,"Chart forms 1-6 each have an example object (compression to buckling)");

  // Spring-damper: under-damped gel overshoots, over-damped foam creeps, stiff steel stays stable at a long step.
  float x=0,v=0,peak=0;for(int i=0;i<150;i++){DeformationDynamics.Spring(ref x,ref v,1,4,.12f,.02f);peak=Math.Max(peak,x);}
  Check(peak>1.4f&&Math.Abs(x-1)<.05f,"Gel-like spring wobbles past its goal and settles ("+peak+")");
  x=v=peak=0;for(int i=0;i<150;i++){DeformationDynamics.Spring(ref x,ref v,1,2.5f,1.3f,.02f);peak=Math.Max(peak,x);}
  Check(peak<=1.0001f&&x>.9f,"Over-damped foam creeps back without overshoot");
  x=v=0;bool stable=true;for(int i=0;i<200;i++){DeformationDynamics.Spring(ref x,ref v,1,30,1,.05f);stable&=float.IsFinite(x)&&Math.Abs(x)<2;}
  Check(stable&&Math.Abs(x-1)<.01f,"Stiff spring stays stable at a 50 ms frame hitch");
  x=float.NaN;v=float.NaN;DeformationDynamics.Spring(ref x,ref v,1,8,.5f,.02f);Check(float.IsFinite(x)&&float.IsFinite(v),"Bad spring state is reset, never propagated");
  x=.3f;v=0;DeformationDynamics.Spring(ref x,ref v,float.NaN,8,.5f,.02f);Check(x==.3f,"Bad spring goal is ignored");

  // Yield and permanent set.
  Check(DeformationDynamics.PlasticSet(0,.5f,.6f,.9f)==0,"Below yield: no permanent set");
  Check(Math.Abs(DeformationDynamics.PlasticSet(0,1,.6f,.5f)-.2f)<1e-5f,"Past yield: plasticity of the excess stays");
  Check(DeformationDynamics.PlasticSet(.3f,.1f,0,1)==.3f&&DeformationDynamics.PlasticSet(.3f,float.NaN,0,1)==.3f,"Permanent set never shrinks and ignores bad input");

  // Seams: duplicated positions are welded, so they share neighbours.
  Sphere(.035f,16,24,out var sv,out var st);
  var weld=DeformationDynamics.WeldGroups(sv);
  Check(weld[24]==weld[0]&&weld[1]==weld[0],"Seam column and pole duplicates are welded");
  DeformationDynamics.Neighbours(st,sv.Length,out var start,out var list,weld);
  bool same=start[25+24+1]-start[25+24]==start[25+1]-start[25];Check(same,"Welded seam vertices share one neighbour set");
  Invalid(()=>DeformationDynamics.Neighbours(new[]{0,1},3,out _,out _),"Incomplete triangle list rejected");
  Invalid(()=>DeformationDynamics.Neighbours(new[]{0,1,9},3,out _,out _),"Out-of-range neighbour index rejected");
  float restVolume=Math.Abs(DeformationDynamics.Volume(sv,st));float exact=(float)(4/3.0*Math.PI*Math.Pow(.035,3));
  Check(Math.Abs(restVolume-exact)/exact<.03f,"Mesh volume matches the sphere ("+restVolume+" vs "+exact+")");

  // Volume: rubber keeps it (bulge beside the dent), sponge loses it; far side and seams stay intact.
  var rubber=Squeezed(DeformationMaterial.Rubber(),sv,st,.012f,120);var sponge=Squeezed(DeformationMaterial.Sponge(),sv,st,.012f,120);
  float rubberChange=(Volume(rubber,st)-restVolume)/restVolume,spongeChange=(Volume(sponge,st)-restVolume)/restVolume;
  Check(rubber.Vertices[0].y<sv[0].y-.008f,"Rubber dents under the finger");
  Check(Math.Abs(rubberChange)<.02f,"Rubber keeps its volume when squeezed ("+rubberChange*100+" %)");
  Check(spongeChange<-.03f&&spongeChange<rubberChange-.02f,"Sponge loses volume when squeezed ("+spongeChange*100+" %)");
  int south=16*25;Check((rubber.Vertices[south]-sv[south]).magnitude<.00005f,"The far side does not move");
  // A whole hand: five fingertip dents of 10 mm (1.6 cm radius) around the ball, as in the game.
  var hand=new LocalDeformationState(sv);hand.SetTopology(st);
  var tips=new[]{new Vector3(.035f,0,0),new Vector3(.025f,.025f,0),new Vector3(.025f,-.025f,0),new Vector3(0,0,.035f),new Vector3(-.03f,0,.018f)};
  for(int k=0;k<150;k++){
   hand.BeginStep();
   foreach(var tipPoint in tips){var n=tipPoint.normalized;var surface=n*.035f;for(int i=0;i<sv.Length;i++){var d=ContactDeformation.Indentation(sv[i],surface,n,.01f,.016f,sv[i],0);if(d.sqrMagnitude>0)hand.SetTarget(i,d);}}
   hand.StepDynamic(.02f,DeformationMaterial.Rubber(),restVolume);
  }
  float handChange=(Volume(hand,st)-restVolume)/restVolume;
  Check(Math.Abs(handChange)<.03f,"Rubber keeps its volume under five fingers ("+handChange*100+" %)");
  // Unity meshes may be wound the other way: the same squeeze must still keep the volume.
  var flipped=(int[])st.Clone();for(int i=0;i<flipped.Length;i+=3){int a=flipped[i+1];flipped[i+1]=flipped[i+2];flipped[i+2]=a;}
  var other=Squeezed(DeformationMaterial.Rubber(),sv,flipped,.012f,120);float otherChange=(Volume(other,flipped)-restVolume)/restVolume;
  Check(Math.Abs(otherChange)<.02f&&other.Vertices[0].y<sv[0].y-.008f,"Either triangle winding: dents go in and the volume is kept ("+otherChange*100+" %)");
  bool seams=true;for(int i=0;i<sv.Length;i++)seams&=(rubber.Vertices[i]-rubber.Vertices[weld[i]]).magnitude<1e-6f;
  Check(seams,"Seam and pole vertices stay together (no tearing)");
  bool smooth=true;for(int i=0;i<sv.Length;i++)for(int k=start[i];k<start[i+1];k++)smooth&=(rubber.Vertices[i]-sv[i]-(rubber.Vertices[list[k]]-sv[list[k]])).magnitude<.008f;
  Check(smooth,"Neighbouring vertices move together (continuous surface)");
  Release(rubber,DeformationMaterial.Rubber(),st,150);
  float residual=0;for(int i=0;i<sv.Length;i++)residual=Math.Max(residual,(rubber.Vertices[i]-sv[i]).magnitude);
  Check(residual<.0002f,"Elastic rubber returns to its original shape ("+residual*1000+" mm)");
  var clay=Squeezed(DeformationMaterial.Clay(),sv,st,.012f,120);Release(clay,DeformationMaterial.Clay(),st,150);
  Check(clay.Vertices[0].y<sv[0].y-.005f,"Plastic clay keeps the dent after release");
  var again=Squeezed(DeformationMaterial.Clay(),sv,st,.012f,120);again.Reset();
  Check((again.Vertices[0]-sv[0]).magnitude<1e-6f,"Reset clears the permanent set");
  var bad=new LocalDeformationState(sv);Check(!bad.StepDynamic(.02f,DeformationMaterial.Rubber(),restVolume),"Dynamics without topology does nothing");
  Check(!rubber.StepDynamic(float.NaN,DeformationMaterial.Rubber(),restVolume),"Bad time cannot poison the surface");

  // Forms: exact geometry of the chart's six forms on a 0.2 m bar along +Y held at the origin.
  var o=new Vector3();var up=Vector3.up;var side=Vector3.right;var tip=new Vector3(0,.2f,0);
  var bend=tip+DeformationDynamics.Form(tip,DeformationForm.Bending,.5f,o,up,side,.2f,.3f);float r=.2f/.5f;
  Check((bend-new Vector3(r*(1-(float)Math.Cos(.5)),r*(float)Math.Sin(.5),0)).magnitude<1e-4f,"Bending puts the free end on the arc");
  Check(DeformationDynamics.Form(o,DeformationForm.Bending,.5f,o,up,side,.2f,.3f).magnitude<1e-6f,"Bending keeps the held end still");
  Check(Math.Abs(DeformationDynamics.Form(tip,DeformationForm.Tension,.5f,o,up,side,.2f,.3f).y-.1f)<1e-5f,"Tension stretches the free end by the strain");
  Check(DeformationDynamics.Form(new Vector3(.01f,.1f,0),DeformationForm.Tension,.5f,o,up,side,.2f,.4f).x<0,"Tension thins the bar (Poisson)");
  float thin=.01f+DeformationDynamics.Form(new Vector3(.01f,.1f,0),DeformationForm.Tension,.69f,o,up,side,.2f,.5f).x;
  Check(Math.Abs(thin*thin*1.69f-.0001f)<1e-7f,"Stretching with Poisson 0.5 keeps the volume (radius² × length constant)");
  float fat=.01f+DeformationDynamics.Form(new Vector3(.01f,.15f,0),DeformationForm.Compression,.36f,o,up,side,.2f,.5f).x;
  Check(Math.Abs(fat*fat*.64f-.0001f)<1e-7f,"Squashing with Poisson 0.5 keeps the volume");
  var rim=new Vector3(.01f,.2f,0);var twisted=rim+DeformationDynamics.Form(rim,DeformationForm.Torsion,(float)Math.PI/2,o,up,side,.2f,.3f);
  Check(Math.Abs(new Vector3(twisted.x,0,twisted.z).magnitude-.01f)<1e-5f&&Math.Abs(twisted.x)<1e-4f,"Torsion turns the free section by the angle, keeping its radius");
  Check(Math.Abs(DeformationDynamics.Form(tip,DeformationForm.Shear,.02f,o,up,side,.2f,.3f).x-.02f)<1e-6f&&DeformationDynamics.Form(o,DeformationForm.Shear,.02f,o,up,side,.2f,.3f).magnitude<1e-6f,"Shear slides the top layer, not the bottom");
  Check(Math.Abs(DeformationDynamics.Form(new Vector3(0,.1f,0),DeformationForm.Buckling,.03f,o,up,side,.2f,.3f).x-.03f)<1e-5f&&DeformationDynamics.Form(o,DeformationForm.Buckling,.03f,o,up,side,.2f,.3f).magnitude<1e-6f,"Buckling bows the middle while the ends stay on the line");
  Check(DeformationDynamics.Form(new Vector3(0,.1f,0),DeformationForm.Compression,.3f,o,up,side,.2f,.3f).magnitude<1e-6f&&DeformationDynamics.Form(tip,DeformationForm.Compression,.3f,o,up,side,.2f,.3f).y<0,"Compression squashes toward the middle");
  Check(DeformationDynamics.Form(tip,DeformationForm.Bending,float.NaN,o,up,side,.2f,.3f).magnitude==0&&DeformationDynamics.Form(tip,DeformationForm.Bending,.5f,o,up,side,0,.3f).magnitude==0,"Bad form input does nothing");
  var straw=DeformationMaterial.PlasticStraw();
  Check(DeformationDynamics.FormTarget(straw,straw.formNewtons*.4f)==0&&DeformationDynamics.FormTarget(straw,straw.formNewtons*.8f)>0,"A column does not buckle below its critical load, then gives way");
  var wood=DeformationMaterial.Wood();
  Check(DeformationDynamics.FormTarget(wood,wood.formNewtons*.5f)<DeformationDynamics.FormTarget(wood,wood.formNewtons)&&DeformationDynamics.FormTarget(wood,float.NaN)==0&&DeformationDynamics.FormTarget(null,10)==0,"Form grows with grip force and rejects bad input");
 }
}
