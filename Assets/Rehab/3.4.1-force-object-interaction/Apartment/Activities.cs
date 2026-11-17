using System;
using System.Collections.Generic;
using System.Linq;
using GripRehab.Product;
using UnityEngine;
namespace GripRehab {
/// <summary>One scripted action of the activity probe (SYNTHETIC input through the real grab/aim/release path).</summary>
public sealed class ProbeStep {
 public enum Kind { Place, Grab, Release, Squeeze, Chop, Pour, Drink }
 public Kind kind;public ActivityItem item;public Func<Vector3> aim;public float seconds,newtons;
 /// <summary>Pour: millilitres wanted in the cup.</summary>
 public float ml;
 public static ProbeStep Place(ActivityItem item,Func<Vector3> aim)=>new ProbeStep{kind=Kind.Place,item=item,aim=aim};
 public static ProbeStep Grab(ActivityItem item)=>new ProbeStep{kind=Kind.Grab,item=item};
}

/// <summary>One of the six Stage 10 activities: its props, targets, steps, rules and outcomes.</summary>
public abstract class Activity {
 public ActivityKind kind;public ActivityTuning tuning;
 public readonly ActivitySteps steps=new ActivitySteps();
 public readonly List<ActivityItem> items=new List<ActivityItem>();
 public readonly List<SnapTarget> targets=new List<SnapTarget>();
 public int errors,misses;
 protected readonly List<PlacementResult> placements=new List<PlacementResult>();
 protected Material ghost;

 public static Activity Create(ActivityKind kind,ActivityTuning tuning){
  Activity a;
  switch(kind){
   case ActivityKind.Bookshelf:a=new BookshelfActivity();break;
   case ActivityKind.CoffeeTable:a=new CoffeeTableActivity();break;
   case ActivityKind.StorageBox:a=new StorageBoxActivity();break;
   case ActivityKind.SimpleMeal:a=new SimpleMealActivity();break;
   case ActivityKind.Cutting:a=new CuttingActivity();break;
   case ActivityKind.PourTea:a=new TeaActivity();break;
   default:a=new PrepareServeActivity();break;
  }
  a.kind=kind;a.tuning=tuning;return a;
 }
 public void Build(ActivityRunner r){ghost=r.Paint(new Color(.15f,.9f,.95f),.2f);BuildProps(r);}
 protected abstract void BuildProps(ActivityRunner r);

 // ---- Hooks (defaults suit simple placement)
 public virtual bool Decide(ActivityItem item,SnapTarget target,out string reason){reason="";return true;}
 public virtual void Placed(ActivityItem item,SnapTarget target,PlacementResult result){placements.Add(result);steps.Advance();}
 public virtual void Removed(ActivityItem item,SnapTarget target){steps.Retract();}
 public virtual void Grabbed(ActivityItem item){}
 public virtual bool AllowsFreePlacement(ActivityItem item)=>false;
 public virtual void Squeeze(ActivityRunner r,ActivityItem held,Collider target,float dt){}
 public virtual void Chop(ActivityRunner r,CuttableFood food,Vector3 point){}
 public virtual void Tick(ActivityRunner r,float dt){}
 public virtual string PraiseFor(ActivityItem item,SnapTarget target)=>"Good: the "+item.kind+" is on the "+target.label+".";
 /// <summary>A correct solution as probe actions (used only by the SYNTHETIC activity probe).</summary>
 public abstract IEnumerable<ProbeStep> Solution();
 protected Func<Vector3> At(SnapTarget t,float up=.01f)=>()=>t.WorldPosition+Vector3.up*up;
 protected virtual void AddOutcomes(List<OutcomeMetric> o){}
 /// <summary>Placement accuracy 0..1 used for the score (cutting uses evenness instead).</summary>
 protected virtual float Accuracy=>placements.Count==0?1:placements.Average(p=>p.score);

 public ActivityResult Result(int level,float seconds){
  var r=new ActivityResult{kind=kind,level=level,seconds=seconds};
  r.score=ActivityRules.Score(Accuracy,steps.Complete,errors);
  var o=r.outcomes;
  o.Add(M("activity.id","Activity "+((int)kind+1)+": "+ActivityRules.Title(kind),"",(int)kind+1));
  o.Add(M("activity.accuracy","Placement accuracy","%",Accuracy*100));
  o.Add(M("activity.steps","Steps completed","",steps.Current));
  o.Add(M("activity.errors","Wrong placements","",errors));
  o.Add(M("activity.retries","Placements off target","",misses));
  var accepted=placements.Where(p=>p.accepted).ToList();
  if(accepted.Count>0){o.Add(M("activity.positionErrorMm","Mean position error","mm",accepted.Average(p=>p.positionError)*1000));o.Add(M("activity.angleErrorDeg","Mean turn error","°",accepted.Average(p=>p.angleError)));}
  o.Add(M("activity.seconds","Time","s",seconds));
  AddOutcomes(o);
  o.Add(M("level","Level","",level));o.Add(M("stars","Stars","",ActivityRules.Stars(r.score)));
  return r;
 }
 protected static OutcomeMetric M(string key,string label,string unit,float value)=>new OutcomeMetric{key=key,label=label,unit=unit,value=Mathf.Round(value*100)/100};

 // ---- Builders
 protected SnapTarget Target(ActivityRunner r,string id,string label,Vector3 at,float yaw,Vector3 outline,Func<ActivityItem,bool> accepts,float symmetry=360){
  var t=new SnapTarget{id=id,label=label,position=at,yaw=yaw,yawSymmetry=symmetry,accepts=accepts};
  t.ghost=ActivityProps.Ghost("Target "+label,r.Root,at,yaw,outline,ghost);targets.Add(t);return t;
 }
 protected SnapTarget Carrier(string id,string label,ActivityItem carrier,Vector3 offset,float radius,int capacity,Func<ActivityItem,bool> accepts){
  var t=new SnapTarget{id=id,label=label,carrier=carrier.transform,carrierOffset=offset,carrierRadius=radius,capacity=capacity,accepts=accepts};targets.Add(t);return t;
 }
 protected ActivityItem Add(ActivityItem item){items.Add(item);return item;}
 protected static DeformationMaterial Rigid(string name)=>DeformationMaterial.RigidSolid(name);
}

// ---------------- 1. Living room: bookshelf (order by height)
sealed class BookshelfActivity:Activity {
 float[] heights;int firstTry;readonly HashSet<ActivityItem> tried=new HashSet<ActivityItem>();
 protected override void BuildProps(ActivityRunner r){
  int n=tuning.items;heights=new float[n];
  var rng=new System.Random(17+n);for(int i=0;i<n;i++)heights[i]=.14f+.1f*i/Math.Max(1,n-1);
  for(int i=n-1;i>0;i--){int j=rng.Next(i+1);var h=heights[i];heights[i]=heights[j];heights[j]=h;}
  float slot=.055f,width=slot*n+.016f,height=.27f,depth=.12f,t=.008f;
  var at=r.Surface(.52f,.2f);var wood=r.Paint(new Color(.45f,.3f,.18f),.3f);
  var boxes=new List<(Vector3,Vector3)>{(new Vector3(0,t*.5f,depth*.5f),new Vector3(width,t,depth)),(new Vector3(0,height,depth*.5f),new Vector3(width,t,depth)),
   (new Vector3(0,height*.5f,depth),new Vector3(width,height,t)),(new Vector3(-width*.5f,height*.5f,depth*.5f),new Vector3(t,height,depth)),(new Vector3(width*.5f,height*.5f,depth*.5f),new Vector3(t,height,depth))};
  for(int i=1;i<n;i++)boxes.Add((new Vector3(-width*.5f+.008f+slot*i,.05f,depth*.55f),new Vector3(.004f,.09f,depth*.8f)));
  var shelf=ActivityProps.Fixture("Bookshelf",r.Root,at,r.Facing,wood,boxes.ToArray());
  Color[] covers={new Color(.7f,.15f,.12f),new Color(.12f,.35f,.65f),new Color(.15f,.5f,.25f),new Color(.75f,.6f,.15f),new Color(.4f,.2f,.5f)};
  var pages=r.Paint(new Color(.94f,.92f,.85f),.2f);
  for(int i=0;i<n;i++){
   int book=i;
   var slotPoint=shelf.transform.TransformPoint(new Vector3(-width*.5f+.008f+slot*(i+.5f),t,depth*.5f));
   Target(r,"slot"+i,"shelf slot "+(i+1),slotPoint,r.Yaw,new Vector3(.034f,0,depth*.8f),a=>a.kind=="book",180).ghost.SetActive(false);
   var start=r.Surface(.32f,-.42f+.075f*i);
   var b=Add(ActivityProps.Item("Book "+(i+1),"book",r.Root,start,r.Yaw,ActivityProps.Book(heights[i],.03f,.11f),new[]{r.Paint(covers[i%covers.Length],.3f),pages},
    g=>ActivityProps.BoxC(g,new Vector3(0,heights[book]*.5f,0),new Vector3(.03f,heights[book],.11f)),GripType.Side,Rigid("Paper and board"),.3f,new Vector3(0,heights[book]*.5f,.04f),new Vector3(-1,0,0),new Vector3(0,0,1),180));
   b.height=heights[i];b.index=i;
  }
  steps.Add("Put the books on the shelf, shortest on the left to tallest on the right",n);
 }
 public override IEnumerable<ProbeStep> Solution(){
  for(int slot=0;slot<heights.Length;slot++)foreach(var b in items)if(ActivityRules.BookFitsSlot(heights,b.index,slot))yield return ProbeStep.Place(b,At(targets[slot],.004f));
 }
 public override bool Decide(ActivityItem item,SnapTarget target,out string reason){
  int slot=targets.IndexOf(target);bool right=ActivityRules.BookFitsSlot(heights,item.index,slot);
  if(!tried.Contains(item)){tried.Add(item);if(right)firstTry++;}
  reason=right?"":"That book is "+(item.height*100).ToString("0")+" cm tall; slot "+(slot+1)+" needs the "+Ordinal(slot)+" shortest. Pick it up and try another slot.";
  return right;
 }
 static string Ordinal(int i)=>i==0?"1st":i==1?"2nd":i==2?"3rd":(i+1)+"th";
 protected override void AddOutcomes(List<OutcomeMetric> o){o.Add(M("bookshelf.firstTry","Right slot first time","%",heights.Length==0?0:100f*firstTry/heights.Length));o.Add(M("bookshelf.books","Books","",heights.Length));}
}

// ---------------- 2. Living room: coffee table layout
sealed class CoffeeTableActivity:Activity {
 protected override void BuildProps(ActivityRunner r){
  var white=r.Paint(new Color(.95f,.95f,.93f),.8f);var wood=r.Paint(new Color(.55f,.38f,.22f),.4f);var blue=r.Paint(new Color(.2f,.4f,.75f),.6f);var cloth=r.Paint(new Color(.8f,.3f,.3f),.1f);
  var y=r.Yaw;
  var plan=new List<(string kind,string label,float f,float rr,float yaw,float sym,Vector3 outline,Func<ActivityItem> make)>{
   ("plate","plate outline",.47f,-.02f,0,1,new Vector3(.2f,0,.2f),()=>ActivityProps.Item("Plate","plate",r.Root,r.Surface(.3f,-.45f),y,ActivityProps.Plate(.1f),new[]{white},g=>ActivityProps.BoxC(g,new Vector3(0,.009f,0),new Vector3(.19f,.018f,.19f)),GripType.Side,DeformationMaterial.Porcelain(),.35f,new Vector3(.08f,.01f,0),null,null,1)),
   ("cup","cup outline (handle to the right)",.44f,.2f,0,360,new Vector3(.09f,0,.09f),()=>ActivityProps.Item("Cup","cup",r.Root,r.Surface(.3f,-.2f),y+90,ActivityProps.Cup(.035f,.08f),new[]{white},g=>ActivityProps.Octagon(g,new Vector3(0,.04f,0),.036f,.08f),GripType.Cupped,DeformationMaterial.Porcelain(),.2f,new Vector3(0,.04f,0),new Vector3(1,0,0),new Vector3(0,0,1),360)),
   ("tray","tray outline",.47f,-.3f,0,180,new Vector3(.22f,0,.15f),()=>ActivityProps.Item("Tray","tray",r.Root,r.Surface(.3f,.3f),y+90,ActivityProps.Tray(new Vector3(.2f,.03f,.14f)),new[]{wood},g=>ActivityProps.BoxC(g,new Vector3(0,.015f,0),new Vector3(.2f,.03f,.14f)),GripType.Side,Rigid("Wood"),.3f,new Vector3(.09f,.015f,0),null,null,180)),
   ("bowl","bowl outline",.5f,.37f,0,1,new Vector3(.12f,0,.12f),()=>ActivityProps.Item("Bowl","bowl",r.Root,r.Surface(.3f,.45f),y,ActivityProps.Bowl(.055f,.05f),new[]{blue},g=>ActivityProps.Octagon(g,new Vector3(0,.025f,0),.055f,.05f),GripType.Cupped,DeformationMaterial.Porcelain(),.25f,new Vector3(0,.025f,0),new Vector3(1,0,0),new Vector3(0,0,1),1)),
   ("napkin","napkin outline",.5f,.12f,0,90,new Vector3(.11f,0,.11f),()=>ActivityProps.Item("Napkin","napkin",r.Root,r.Surface(.31f,.08f),y+30,ActivityProps.Slab(new Vector3(.1f,.006f,.1f)),new[]{cloth},g=>ActivityProps.BoxC(g,Vector3.zero,new Vector3(.1f,.006f,.1f)),GripType.Precision,DeformationMaterial.Fabric(),.02f,Vector3.zero,null,null,90)),
  };
  int n=Mathf.Clamp(tuning.items,3,plan.Count);
  for(int i=0;i<n;i++){var p=plan[i];string kind=p.kind;Target(r,kind,p.label,r.Surface(p.f,p.rr),y+p.yaw,p.outline,a=>a.kind==kind,p.sym);Add(p.make());}
  steps.Add("Set the table: put each item on its outline, turned the same way",n);
 }
 public override IEnumerable<ProbeStep> Solution(){foreach(var t in targets)yield return ProbeStep.Place(items.First(i=>t.accepts(i)),At(t,.002f));}
 public override string PraiseFor(ActivityItem item,SnapTarget target)=>"The "+item.kind+" is in place.";
 protected override void AddOutcomes(List<OutcomeMetric> o){
  // Spacing: how far the finished layout's gaps differ from the target gaps (pairwise distances).
  float sum=0;int pairs=0;var placed=targets.Where(t=>t.occupants.Count>0).ToList();
  for(int i=0;i<placed.Count;i++)for(int j=i+1;j<placed.Count;j++){
   float want=Vector3.Distance(placed[i].WorldPosition,placed[j].WorldPosition),got=Vector3.Distance(placed[i].occupants[0].transform.position,placed[j].occupants[0].transform.position);
   sum+=Mathf.Abs(want-got);pairs++;}
  o.Add(M("table.spacingErrorMm","Spacing error","mm",pairs==0?0:sum/pairs*1000));
 }
}

// ---------------- 3. Living room: storage box (fit everything inside)
public sealed class StorageBoxActivity:Activity {
 PlacementVolume box;int overlapsSeen;float settle;
 protected override void BuildProps(ActivityRunner r){
  float w=.30f,h=.1f,d=.22f,t=.008f;var at=r.Surface(.42f,.24f);var card=r.Paint(new Color(.62f,.48f,.32f),.15f);
  var fx=ActivityProps.Fixture("Storage box",r.Root,at,r.Facing,card,(new Vector3(0,t*.5f,0),new Vector3(w,t,d)),
   (new Vector3(0,h*.5f,d*.5f),new Vector3(w,h,t)),(new Vector3(0,h*.5f,-d*.5f),new Vector3(w,h,t)),(new Vector3(w*.5f,h*.5f,0),new Vector3(t,h,d)),(new Vector3(-w*.5f,h*.5f,0),new Vector3(t,h,d)));
  box=fx.AddComponent<PlacementVolume>();box.interior=new Bounds(new Vector3(0,t+(h-t)*.5f,0),new Vector3(w-2*t,h-t,d-2*t));
  var y=r.Yaw;
  var make=new List<Func<int,ActivityItem>>{
   i=>ActivityProps.Item("Ball","ball",r.Root,r.Surface(.3f,-.45f),y,ActivityProps.Round(.028f),new[]{r.Paint(new Color(.85f,.2f,.2f),.5f)},g=>ActivityProps.SphereC(g,Vector3.zero,.028f),GripType.Spherical,DeformationMaterial.Rubber(),.06f),
   i=>ActivityProps.Item("Block","block",r.Root,r.Surface(.3f,-.33f),y,ActivityProps.Slab(new Vector3(.05f,.05f,.05f)),new[]{r.Paint(new Color(.85f,.7f,.3f),.3f)},g=>ActivityProps.BoxC(g,Vector3.zero,new Vector3(.05f,.05f,.05f)),GripType.Power,Rigid("Wood"),.08f),
   i=>ActivityProps.Item("Can","can",r.Root,r.Surface(.3f,-.21f),y,ActivityProps.Can(.028f,.07f),new[]{r.Paint(new Color(.7f,.72f,.75f),.7f,.8f)},g=>ActivityProps.Octagon(g,new Vector3(0,.035f,0),.028f,.07f),GripType.Palm,Rigid("Steel"),.15f,new Vector3(0,.035f,0),new Vector3(1,0,0),new Vector3(0,0,1)),
   i=>ActivityProps.Item("Small book","book",r.Root,r.Surface(.3f,-.09f),y,ActivityProps.Slab(new Vector3(.09f,.022f,.12f)),new[]{r.Paint(new Color(.2f,.45f,.3f),.3f)},g=>ActivityProps.BoxC(g,Vector3.zero,new Vector3(.09f,.022f,.12f)),GripType.Side,Rigid("Paper and board"),.2f,new Vector3(.03f,0,0),null,null,180),
   i=>ActivityProps.Item("Sponge","sponge",r.Root,r.Surface(.34f,.02f),y,ActivityProps.Slab(new Vector3(.07f,.03f,.045f)),new[]{r.Paint(new Color(.98f,.8f,.2f),.1f)},g=>ActivityProps.BoxC(g,Vector3.zero,new Vector3(.07f,.03f,.045f)),GripType.Power,DeformationMaterial.Sponge(),.02f),
  };
  int n=Mathf.Clamp(tuning.items,3,make.Count);for(int i=0;i<n;i++)Add(make[i](i));
  steps.Add("Pack every item into the box: nothing sticking out over the rim",n);
 }
 public override IEnumerable<ProbeStep> Solution(){
  var b=box.interior;int i=0;
  // Five spread spots: the four corners and the middle, each 6.5 cm in from the walls (the book is 12 cm long).
  Vector3[] spots={new Vector3(b.min.x+.065f,b.min.y,b.min.z+.065f),new Vector3(b.max.x-.065f,b.min.y,b.min.z+.065f),new Vector3(b.min.x+.065f,b.min.y,b.max.z-.065f),new Vector3(b.max.x-.065f,b.min.y,b.max.z-.065f),new Vector3(0,b.min.y,0)};
  // Largest first, as a person packs: the flat book goes on the floor before the small things.
  foreach(var a in items.OrderByDescending(x=>x.Item.radius)){var local=spots[i++%spots.Length];var tr=box.transform;yield return ProbeStep.Place(a,()=>tr.TransformPoint(local));}
 }
 // The box is a volume, not a single snap point: items count while they rest fully inside it.
 public override bool AllowsFreePlacement(ActivityItem item)=>true;
 public override void Tick(ActivityRunner r,float dt){
  settle+=dt;if(settle<.4f)return;settle=0;
  int inside=0,overlaps=0;var inner=new List<ActivityItem>();
  foreach(var a in items){if(a==null||a.Item==null||a.Item.IsHeld)continue;if(Inside(a)){inside++;inner.Add(a);}}
  for(int i=0;i<inner.Count;i++)for(int j=i+1;j<inner.Count;j++)if(Overlap(inner[i],inner[j]))overlaps++;
  overlapsSeen=Math.Max(overlapsSeen,overlaps);
  while(steps.CurrentDone<inside&&!steps.Complete){steps.Advance();placements.Add(new PlacementResult{accepted=true,score=1});if(steps.Complete)r.Say("Everything is packed.");}
  while(!steps.Complete&&steps.CurrentDone>inside)steps.Retract();
 }
 public string LastInsideProblem="";
 public Vector3 BoxCentre=>box.transform.TransformPoint(box.interior.center-new Vector3(0,box.interior.extents.y,0));
 bool Inside(ActivityItem a){
  var b=box.interior;var tr=box.transform;
  foreach(var c in a.GetComponentsInChildren<Collider>()){if(!c.enabled)continue;var cb=c.bounds;
   foreach(var corner in new[]{cb.min,cb.max,new Vector3(cb.min.x,cb.min.y,cb.max.z),new Vector3(cb.max.x,cb.min.y,cb.min.z)}){
    var l=tr.InverseTransformPoint(corner);if(l.x<b.min.x-.012f||l.x>b.max.x+.012f||l.z<b.min.z-.012f||l.z>b.max.z+.012f||l.y>b.max.y+.012f||l.y<b.min.y-.02f){LastInsideProblem=a.name+" corner "+l.ToString("F3")+" interior "+b.min.ToString("F3")+".."+b.max.ToString("F3");return false;}}}
  bool still=a.Item.Body.isKinematic||a.Item.Body.velocity.sqrMagnitude<.0004f;if(!still)LastInsideProblem=a.name+" moving "+a.Item.Body.velocity.magnitude.ToString("F3");
  return still;
 }
 static bool Overlap(ActivityItem a,ActivityItem b){
  foreach(var x in a.GetComponentsInChildren<Collider>())foreach(var y in b.GetComponentsInChildren<Collider>())
   if(x.enabled&&y.enabled&&Physics.ComputePenetration(x,x.transform.position,x.transform.rotation,y,y.transform.position,y.transform.rotation,out _,out float depth)&&depth>.003f)return true;
  return false;
 }
 protected override float Accuracy=>items.Count==0?1:Mathf.Clamp01(steps.Progress-.1f*overlapsSeen);
 protected override void AddOutcomes(List<OutcomeMetric> o){o.Add(M("box.items","Items packed","",steps.CurrentDone+(steps.Complete?items.Count:0)));o.Add(M("box.overlaps","Items pressed into each other","",overlapsSeen));}
}

// ---------------- 4. Kitchen: simple meal (pan on the burner, ingredients, stir with grip, plate)
sealed class SimpleMealActivity:Activity {
 ActivityItem pan,plate;readonly List<ActivityItem> food=new List<ActivityItem>();float stir;Color[] raw;
 protected override void BuildProps(ActivityRunner r){
  var y=r.Yaw;var steel=r.Paint(new Color(.25f,.26f,.28f),.6f,.7f);var handle=r.Paint(new Color(.08f,.08f,.08f),.3f);var white=r.Paint(new Color(.95f,.95f,.93f),.8f);
  pan=Add(ActivityProps.Item("Pan","pan",r.Root,r.Surface(.72f,-.12f),y,ActivityProps.Pan(.09f),new[]{steel,handle},g=>{ActivityProps.BoxC(g,new Vector3(0,.012f,0),new Vector3(.17f,.024f,.17f));ActivityProps.CapsuleC(g,new Vector3(.16f,.03f,0),.011f,.14f,0);},
   GripType.Dynamic,Rigid("Steel"),.8f,new Vector3(.18f,.03f,0),new Vector3(1,0,0),new Vector3(0,1,0)));
  plate=Add(ActivityProps.Item("Plate","plate",r.Root,r.Surface(.7f,.3f),y,ActivityProps.Plate(.1f),new[]{white},g=>ActivityProps.BoxC(g,new Vector3(0,.009f,0),new Vector3(.19f,.018f,.19f)),GripType.Side,DeformationMaterial.Porcelain(),.35f,new Vector3(.08f,.01f,0),null,null,1));
  Target(r,"burner","burner",r.Surface(.7f,.6f),y,new Vector3(.2f,0,.2f),a=>a.kind=="pan",1);
  (string name,Color c,float rad,float squash)[] kinds={("Tomato",new Color(.8f,.12f,.08f),.03f,.9f),("Mushroom",new Color(.85f,.78f,.65f),.026f,.6f),("Pepper",new Color(.2f,.6f,.15f),.028f,1f),("Onion",new Color(.6f,.3f,.55f),.03f,.9f)};
  int n=Mathf.Clamp(tuning.items-1,2,4);raw=new Color[n];
  for(int i=0;i<n;i++){var k=kinds[i];raw[i]=k.c;
   var f=Add(ActivityProps.Item(k.name,"ingredient",r.Root,r.Surface(.66f+.05f*(i%2),-.52f+.07f*(i/2)),y,ActivityProps.Round(k.rad,k.squash),new[]{r.Paint(k.c,.5f)},g=>ActivityProps.SphereC(g,Vector3.zero,k.rad*.95f),GripType.Spherical,DeformationMaterial.Food(),.08f));
   f.index=i;food.Add(f);}
  Add(ActivityProps.Item("Spoon","spoon",r.Root,r.Surface(.78f,.17f),y,ActivityProps.Spoon(),new[]{r.Paint(new Color(.65f,.45f,.25f),.4f)},g=>ActivityProps.CapsuleC(g,new Vector3(0,.07f,0),.008f,.16f,1),GripType.ThreeFinger,Rigid("Wood"),.03f,new Vector3(0,.09f,0),new Vector3(0,-.6f,-.8f),new Vector3(1,0,0)));
  Carrier("pan","pan",pan,new Vector3(0,.006f,0),.085f,n,a=>a.kind=="ingredient"&&!a.cooked);
  Carrier("plate","plate",plate,new Vector3(0,.007f,0),.095f,n,a=>a.kind=="ingredient"&&a.cooked);
  steps.Add("Put the pan on the burner");
  steps.Add("Add the ingredients to the pan",n);
  steps.Add("Stir: hold the spoon, look at the pan and squeeze ("+tuning.stirSeconds.ToString("0")+" s at "+tuning.stirNewtons.ToString("0")+" N or more)");
  steps.Add("Serve: put the cooked food on the plate",n);
 }
 public override IEnumerable<ProbeStep> Solution(){
  yield return ProbeStep.Place(pan,At(targets.First(t=>t.id=="burner"),.002f));
  foreach(var f in food)yield return ProbeStep.Place(f,()=>pan.transform.position+Vector3.up*.01f);
  yield return ProbeStep.Grab(items.First(i=>i.kind=="spoon"));
  yield return new ProbeStep{kind=ProbeStep.Kind.Squeeze,aim=()=>pan.transform.position+Vector3.up*.01f,seconds=tuning.stirSeconds+1,newtons=tuning.stirNewtons+4};
  yield return new ProbeStep{kind=ProbeStep.Kind.Release,aim=()=>plate.transform.position+(pan.transform.position-plate.transform.position)*.5f+Vector3.up*.002f};
  foreach(var f in food)yield return ProbeStep.Place(f,()=>plate.transform.position+Vector3.up*.01f);
 }
 public override bool Decide(ActivityItem item,SnapTarget target,out string reason){
  reason="";
  if(target.id=="pan"&&steps.Current<1){reason="Put the pan on the burner first.";return false;}
  if(target.id=="plate"&&steps.Current<3){reason="Cook the food before serving it.";return false;}
  return true;
 }
 public override void Removed(ActivityItem item,SnapTarget target){if(target.id=="burner"&&steps.Current>0)return;base.Removed(item,target);}
 public override void Squeeze(ActivityRunner r,ActivityItem held,Collider target,float dt){
  if(held.kind!="spoon"||steps.Current!=2||target.GetComponentInParent<ActivityItem>()!=pan)return;
  stir+=dt;float t=Mathf.Clamp01(stir/tuning.stirSeconds);
  foreach(var f in food){var rend=f.GetComponentInChildren<Renderer>();if(rend!=null)rend.material.color=Color.Lerp(raw[f.index],new Color(.42f,.24f,.1f),t*.7f);f.cookProgress=t;}
  if(stir>=tuning.stirSeconds){
   foreach(var f in food){f.cooked=true;var t2=targets.First(x=>x.id=="pan");t2.occupants.Remove(f);f.snappedTo=null;f.transform.SetParent(r.Root,true);ActivityRunner.SetColliders(f,true);f.Item.Body.isKinematic=true;}
   steps.Advance();placements.Add(new PlacementResult{accepted=true,score=1});r.Say("Cooked. Now put the food on the plate.");
  }
 }
 public override string PraiseFor(ActivityItem item,SnapTarget target)=>target.id=="burner"?"The pan is on the burner.":target.id=="pan"?"In the pan.":"Served.";
 protected override void AddOutcomes(List<OutcomeMetric> o){o.Add(M("meal.stirSeconds","Stirring with grip","s",stir));o.Add(M("meal.ingredients","Ingredients","",food.Count));}
}

// ---------------- 5. Kitchen: cutting (knife strokes cut the vegetable into even pieces)
sealed class CuttingActivity:Activity {
 CuttableFood cucumber;Vector3 start,end;
 protected override void BuildProps(ActivityRunner r){
  var y=r.Yaw;var board=r.Paint(new Color(.72f,.55f,.35f),.3f);
  var at=r.Surface(.73f,-.15f);ActivityProps.Fixture("Cutting board",r.Root,at,r.Facing,board,(new Vector3(0,.009f,0),new Vector3(.34f,.018f,.2f)));
  float length=.22f,rad=.022f;
  var veg=new GameObject("Cucumber");veg.transform.SetParent(r.Root,false);veg.transform.SetPositionAndRotation(at+Vector3.up*(.018f+rad),r.Facing);
  var visual=new GameObject("Visual");visual.transform.SetParent(veg.transform,false);
  visual.AddComponent<MeshFilter>().sharedMesh=ActivityProps.Cucumber(length,rad);visual.AddComponent<MeshRenderer>().sharedMaterial=r.Paint(new Color(.2f,.45f,.15f),.5f);
  var col=veg.AddComponent<CapsuleCollider>();col.direction=0;col.radius=rad;col.height=length;
  cucumber=veg.AddComponent<CuttableFood>();cucumber.visual=visual.GetComponent<MeshFilter>();cucumber.axis=Vector3.right;cucumber.Init();
  start=veg.transform.TransformPoint(new Vector3(-length*.5f,0,0));end=veg.transform.TransformPoint(new Vector3(length*.5f,0,0));
  // Guides at the even cut positions on the easier levels only (precision without help on the harder ones).
  if(tuning.cuts<=3){var mark=r.Paint(new Color(.95f,.95f,.95f),.2f);for(int i=0;i<tuning.cuts;i++){var p=Vector3.Lerp(start,end,ActivityRules.EvenCutPosition(i,tuning.cuts));ActivityProps.Fixture("Guide",r.Root,p+Vector3.up*(rad+.002f),r.Facing,mark,(Vector3.zero,new Vector3(.002f,.001f,.03f))).GetComponentInChildren<Collider>().enabled=false;}}
  Add(ActivityProps.Item("Knife","knife",r.Root,r.Surface(.66f,.18f),y,ActivityProps.Knife(),new[]{r.Paint(new Color(.75f,.77f,.8f),.8f,.9f),r.Paint(new Color(.12f,.1f,.08f),.3f)},
   g=>{ActivityProps.CapsuleC(g,new Vector3(0,.05f,0),.011f,.1f,1);ActivityProps.BoxC(g,new Vector3(0,.16f,.008f),new Vector3(.004f,.12f,.028f));},GripType.Dynamic,Rigid("Steel"),.1f,new Vector3(0,.04f,0),new Vector3(1,0,0),new Vector3(0,0,1)));
  steps.Add("Pick up the knife");
  steps.Add("Cut the cucumber into "+(tuning.cuts+1)+" even pieces: look at each cut and squeeze ("+tuning.chopNewtons.ToString("0")+" N)",tuning.cuts);
 }
 public override IEnumerable<ProbeStep> Solution(){
  yield return ProbeStep.Grab(items.First(i=>i.kind=="knife"));
  for(int i=0;i<tuning.cuts;i++){var p=Vector3.Lerp(start,end,ActivityRules.EvenCutPosition(i,tuning.cuts));yield return new ProbeStep{kind=ProbeStep.Kind.Chop,aim=()=>p,newtons=tuning.chopNewtons+5};}
 }
 public override void Grabbed(ActivityItem item){if(item.kind=="knife"&&steps.Current==0)steps.Advance();}
 public override void Chop(ActivityRunner r,CuttableFood food,Vector3 point){
  if(steps.Current!=1||food!=cucumber)return;
  // Cut where the look hits, projected onto the cucumber's axis.
  var axis=end-start;var p=start+axis*Mathf.Clamp01(Vector3.Dot(point-start,axis)/axis.sqrMagnitude);
  if(food.Cut(p,.012f)){steps.Advance();r.Say("Cut "+food.Cuts+" of "+tuning.cuts+".");}
  else{misses++;r.Say("Too close to the end or another cut: move along a little.");}
 }
 protected override float Accuracy=>cucumber==null||cucumber.Cuts==0?0:ActivityRules.Evenness(cucumber.Lengths());
 protected override void AddOutcomes(List<OutcomeMetric> o){o.Add(M("cut.evenness","Even pieces","%",Accuracy*100));o.Add(M("cut.cuts","Cuts","",cucumber==null?0:cucumber.Cuts));}
}

// ---------------- 6. Kitchen: prepare and serve (plate, bread, cut tomato, serve)
sealed class PrepareServeActivity:Activity {
 ActivityItem plate,tomato;CuttableFood tomatoCut;
 protected override void BuildProps(ActivityRunner r){
  var y=r.Yaw;var white=r.Paint(new Color(.95f,.95f,.93f),.8f);
  plate=Add(ActivityProps.Item("Plate","plate",r.Root,r.Surface(.66f,.32f),y,ActivityProps.Plate(.1f),new[]{white},g=>ActivityProps.BoxC(g,new Vector3(0,.009f,0),new Vector3(.19f,.018f,.19f)),GripType.Side,DeformationMaterial.Porcelain(),.35f,new Vector3(.08f,.01f,0),null,null,1));
  Add(ActivityProps.Item("Bread","bread",r.Root,r.Surface(.8f,-.42f),y,ActivityProps.Slab(new Vector3(.09f,.014f,.09f)),new[]{r.Paint(new Color(.85f,.68f,.42f),.15f)},g=>ActivityProps.BoxC(g,Vector3.zero,new Vector3(.09f,.014f,.09f)),GripType.Precision,DeformationMaterial.Sponge(),.03f,Vector3.zero,null,null,90));
  var boardAt=r.Surface(.74f,-.22f);ActivityProps.Fixture("Cutting board",r.Root,boardAt,r.Facing,r.Paint(new Color(.72f,.55f,.35f),.3f),(new Vector3(0,.009f,0),new Vector3(.24f,.018f,.18f)));
  tomato=Add(ActivityProps.Item("Tomato","tomato",r.Root,boardAt+Vector3.up*.018f,y,ActivityProps.Round(.032f,.9f),new[]{r.Paint(new Color(.8f,.12f,.08f),.5f)},g=>ActivityProps.SphereC(g,Vector3.zero,.03f),GripType.Spherical,Rigid("Ripe food"),.1f));
  tomatoCut=tomato.gameObject.AddComponent<CuttableFood>();tomatoCut.visual=tomato.Item.visual.GetComponent<MeshFilter>();tomatoCut.axis=Vector3.right;tomatoCut.Init();
  Add(ActivityProps.Item("Knife","knife",r.Root,r.Surface(.64f,.08f),y,ActivityProps.Knife(),new[]{r.Paint(new Color(.75f,.77f,.8f),.8f,.9f),r.Paint(new Color(.12f,.1f,.08f),.3f)},
   g=>{ActivityProps.CapsuleC(g,new Vector3(0,.05f,0),.011f,.1f,1);ActivityProps.BoxC(g,new Vector3(0,.16f,.008f),new Vector3(.004f,.12f,.028f));},GripType.Dynamic,Rigid("Steel"),.1f,new Vector3(0,.04f,0),new Vector3(1,0,0),new Vector3(0,0,1)));
  Target(r,"prep","preparation mat",r.Surface(.72f,.05f),y,new Vector3(.22f,0,.22f),a=>a.kind=="plate",1);
  Carrier("onplate","plate",plate,new Vector3(0,.007f,0),.095f,2,a=>a.kind=="bread"||a.kind=="tomato");
  Target(r,"serve","serving mat",r.Surface(.6f,-.55f),y,new Vector3(.22f,0,.22f),a=>a.kind=="plate",1);
  steps.Add("Put the plate on the preparation mat");
  steps.Add("Put the bread on the plate");
  steps.Add("Cut the tomato in half: hold the knife, look at the tomato and squeeze");
  steps.Add("Put the tomato halves on the plate");
  steps.Add("Serve: carry the plate to the serving mat");
 }
 public override IEnumerable<ProbeStep> Solution(){
  var prep=targets.First(t=>t.id=="prep");var serve=targets.First(t=>t.id=="serve");
  yield return ProbeStep.Place(plate,At(prep,.002f));
  yield return ProbeStep.Place(items.First(i=>i.kind=="bread"),()=>plate.transform.position+Vector3.up*.01f);
  yield return ProbeStep.Grab(items.First(i=>i.kind=="knife"));
  yield return new ProbeStep{kind=ProbeStep.Kind.Chop,aim=()=>tomato.transform.position,newtons=tuning.chopNewtons+5};
  yield return new ProbeStep{kind=ProbeStep.Kind.Release,aim=()=>prep.WorldPosition+(prep.WorldPosition-serve.WorldPosition).normalized*.18f};
  yield return ProbeStep.Place(tomato,()=>plate.transform.position+Vector3.up*.01f);
  yield return ProbeStep.Place(plate,At(serve,.002f));
 }
 public override bool Decide(ActivityItem item,SnapTarget target,out string reason){
  reason="";int s=steps.Current;
  if(target.id=="prep"&&s!=0){reason="The plate is already prepared; serve it on the serving mat.";return false;}
  if(target.id=="serve"&&s<4){reason="Finish preparing the plate before serving it.";return false;}
  if(target.id=="onplate"&&item.kind=="bread"&&s<1){reason="Put the plate on the preparation mat first.";return false;}
  if(target.id=="onplate"&&item.kind=="tomato"&&s<3){reason=s<2?"Put the bread on the plate, then cut the tomato.":"Cut the tomato in half first.";return false;}
  return true;
 }
 public override void Removed(ActivityItem item,SnapTarget target){if(target.id=="prep"&&steps.Current>=1)return;base.Removed(item,target);}
 public override void Chop(ActivityRunner r,CuttableFood food,Vector3 point){
  if(steps.Current!=2||food!=tomatoCut)return;
  if(food.Cut(food.transform.position,.01f)){steps.Advance();r.Say("Halved. Put the tomato on the plate.");}
 }
 public override string PraiseFor(ActivityItem item,SnapTarget target)=>target.id=="serve"?"Served!":"Done: "+item.kind+" on the "+target.label+".";
}

// ---------------- 7. Living room: pour a cup of tea and drink it (liquid: TeaPouring on LiquidCore)
public sealed class TeaActivity:Activity {
 public ActivityItem teapot,cup;public TeaPouring tea;SnapTarget mat,saucer;int level=1;int spillErrors;bool overflowCounted;
 protected override void BuildProps(ActivityRunner r){
  level=r.Level;
  var glaze=r.Paint(new Color(.93f,.92f,.88f),.85f);var knob=r.Paint(new Color(.42f,.3f,.2f),.5f);var china=r.Paint(new Color(.97f,.97f,.95f),.85f);var gold=r.Paint(new Color(.8f,.62f,.25f),.7f,.6f);
  var y=r.Yaw;bool left=r.app.LeftSelected;float side=left?-1:1; // the teapot sits by the hand that holds it
  var potAt=r.Surface(.42f,.2f*side);var cupAt=r.Surface(.44f,-.05f*side);
  teapot=Add(ActivityProps.Item("Teapot","teapot",r.Root,potAt,y+(left?0:180),ActivityProps.Teapot(),new[]{glaze,knob},
   g=>{ActivityProps.CapsuleC(g,new Vector3(0,.052f,0),.066f,.11f,1);ActivityProps.BoxC(g,new Vector3(-.098f,.062f,0),new Vector3(.018f,.07f,.016f));},
   GripType.Power,Rigid("Ceramic"),.65f,TeaPouring.TeapotGrip,null,null,1));
  teapot.gameObject.AddComponent<PourVessel>().teapot=true;
  cup=Add(ActivityProps.Item("Tea cup","cup",r.Root,cupAt+Vector3.up*.006f,y,ActivityProps.TeaCup(),new[]{china},
   g=>ActivityProps.Octagon(g,new Vector3(0,TeaPouring.CupH*.5f,0),TeaPouring.CupR,TeaPouring.CupH),GripType.Cupped,DeformationMaterial.Porcelain(),.22f,new Vector3(0,TeaPouring.CupH*.5f,0),null,null,1));
  cup.gameObject.AddComponent<PourVessel>();
  // Saucer under the cup (a static prop the cup rests on) and the gold fill line inside the cup.
  var saucerGo=new GameObject("Saucer");saucerGo.transform.SetParent(r.Root,true);saucerGo.transform.SetPositionAndRotation(cupAt,Quaternion.Euler(0,y,0));
  saucerGo.AddComponent<MeshFilter>().sharedMesh=ActivityProps.Saucer(.075f);saucerGo.AddComponent<MeshRenderer>().sharedMaterial=china;
  var sc=saucerGo.AddComponent<BoxCollider>();sc.center=new Vector3(0,.003f,0);sc.size=new Vector3(.13f,.006f,.13f);
  float r0=TeaPouring.CupInnerBottom,r1=TeaPouring.CupInnerTop,depth=TeaPouring.CupDepth;
  float lineH=LiquidMath.FrustumLevel(r0,r1,depth,LiquidMath.FrustumVolume(r0,r1,depth,depth)*TeaRules.TargetFraction);
  var line=new GameObject("Fill line");line.transform.SetParent(cup.Item.visual,false);
  line.AddComponent<MeshFilter>().sharedMesh=ActivityProps.FillLine(r0+(r1-r0)*lineH/depth,TeaPouring.CupBottom+lineH);line.AddComponent<MeshRenderer>().sharedMaterial=gold;
  mat=Target(r,"mat","teapot mat",potAt,y,new Vector3(.17f,0,.17f),a=>a.kind=="teapot",1);
  saucer=Target(r,"saucer","saucer",cupAt+Vector3.up*.006f,y,new Vector3(.12f,0,.12f),a=>a.kind=="cup",1);
  tea=TeaPouring.Create(r,teapot,cup,level);
  steps.Add("Pick up the teapot by its handle");
  steps.Add("Tip the teapot to pour tea into the cup, up to the gold line ("+tea.TargetMl.ToString("0")+" ml), then level it");
  steps.Add("Put the teapot back on its mat");
  steps.Add("Pick up the cup");
  steps.Add("Raise the cup to your mouth and tip it to drink, until it is empty");
  steps.Add("Put the cup back on its saucer");
 }
 public override void Grabbed(ActivityItem item){
  if(item==teapot&&steps.Current==0)steps.Advance();
  else if(item==cup&&steps.Current==3)steps.Advance();
 }
 // Putting things down never undoes a step (the steps are a sequence, not a count of placed items).
 public override void Removed(ActivityItem item,SnapTarget target){}
 public override void Placed(ActivityItem item,SnapTarget target,PlacementResult result){
  if(item==teapot&&steps.Current==2||item==cup&&steps.Current==5){placements.Add(result);steps.Advance();}
 }
 public override string PraiseFor(ActivityItem item,SnapTarget target)=>
  item==cup&&steps.Current<=4&&tea.CupMl>3?"The cup is on its saucer. Pick it up again to drink the rest.":
  item==teapot?"The teapot is back on its mat.":"The cup is back on its saucer.";
 public override void Tick(ActivityRunner r,float dt){
  if(tea==null)return;
  // Errors: tea over the rim of a full cup, and every few millilitres spilled (more forgiving at low levels).
  if(tea.Overflowed&&!overflowCounted){overflowCounted=true;errors++;}
  int spills=Mathf.Min(5,Mathf.FloorToInt(tea.SpilledMl/TeaRules.SpillErrorMl(level)));if(spills>spillErrors){errors+=spills-spillErrors;spillErrors=spills;}
  if(steps.Current==1){
   bool enough=tea.CupMl>=tea.TargetMl*(1-TeaRules.Tolerance(level))||tea.PotMl<1;
   if(enough&&tea.LevelSeconds>=.8f&&!tea.Pouring){tea.MarkFilled();steps.Advance();if(teapot.snappedTo==mat)steps.Advance();}
  } else if(steps.Current==4){
   if(tea.CupMl<3&&tea.DrankMl>0){steps.Advance();if(cup.snappedTo==saucer)steps.Advance();}
  }
 }
 protected override float Accuracy=>tea==null?0:TeaRules.Accuracy(TeaRules.FillAccuracy(tea.FilledMl,tea.TargetMl,level),TeaRules.PourAccuracy(tea.IntoCupMl,tea.PouredMl));
 public int Level=>level;
 public override IEnumerable<ProbeStep> Solution(){
  yield return ProbeStep.Grab(teapot);
  yield return new ProbeStep{kind=ProbeStep.Kind.Pour,item=teapot,ml=tea.TargetMl,aim=()=>cup.transform.position};
  yield return ProbeStep.Place(teapot,At(mat,.002f));
  yield return ProbeStep.Grab(cup);
  yield return new ProbeStep{kind=ProbeStep.Kind.Drink,item=cup};
  yield return ProbeStep.Place(cup,At(saucer,.002f));
 }
 protected override void AddOutcomes(List<OutcomeMetric> o){
  if(tea==null)return;
  o.Add(M("tea.targetMl","Fill target","ml",tea.TargetMl));o.Add(M("tea.fillMl","Tea in the cup","ml",tea.FilledMl));
  o.Add(M("tea.fillErrorPct","Off the fill line","%",tea.TargetMl>0?100*Mathf.Abs(tea.FilledMl-tea.TargetMl)/tea.TargetMl:0));
  o.Add(M("tea.pourAccuracyPct","Tea kept in the cup","%",100*TeaRules.PourAccuracy(tea.IntoCupMl,tea.PouredMl)));
  o.Add(M("tea.spillMl","Tea spilled","ml",tea.SpilledMl));o.Add(M("tea.carrySpillMl","Spilled while holding the cup","ml",tea.CarrySpillMl));
  o.Add(M("tea.pourSeconds","Pouring time","s",tea.PourSeconds));o.Add(M("tea.flowCv","Pour steadiness (flow CV)","",tea.flow.Cv));
  o.Add(M("tea.tiltSdDeg","Pour angle steadiness (SD)","°",tea.tilt.Sd));
  o.Add(M("tea.drankMl","Tea drunk","ml",tea.DrankMl));o.Add(M("tea.drinkSeconds","Drinking time","s",tea.DrinkSeconds));
  // Tremor only from the camera hand: a keyboard run has no hand to measure, so nothing is reported (never a made-up 0).
  if(tea.tremor.Samples>0){o.Add(M("tea.tremorRmsMm","Hand tremor (camera estimate)","mm",tea.tremor.MeanRmsMm));o.Add(M("tea.tremorHz","Tremor frequency (camera estimate)","Hz",tea.tremor.DominantHz));}
 }
}
}
