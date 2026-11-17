using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
/// <summary>
/// The multi-step environmental activities: Stage 10's six (living room 1-3, kitchen 4-6) and pouring tea (living room, 7).
/// New kinds go at the end: the number is saved with every session (task = 2 + kind).
/// </summary>
public enum ActivityKind { Bookshelf, CoffeeTable, StorageBox, SimpleMeal, Cutting, PrepareAndServe, PourTea }

/// <summary>Difficulty of an activity at a level (1-10, the same scale as ExerciseTuning).</summary>
public struct ActivityTuning {
 /// <summary>How far (metres) and how many degrees off a target a placement may be and still count.</summary>
 public float positionTolerance,angleTolerance;
 /// <summary>Items to handle (books, layout items, box items, ingredients) and cuts to make.</summary>
 public int items,cuts;
 /// <summary>Stirring: seconds of grip at or above stirNewtons.</summary>
 public float stirSeconds,stirNewtons,chopNewtons;
 public static ActivityTuning ForLevel(int level){
  float t=(Math.Max(1,Math.Min(10,level))-1)/9f; // the 1..10 scale of ExerciseTuning
  return new ActivityTuning{
   positionTolerance=Mathf.Lerp(.06f,.02f,t),angleTolerance=Mathf.Lerp(35,12,t),
   items=3+(int)Math.Round(t*2),cuts=2+(int)Math.Round(t*2),
   stirSeconds=Mathf.Lerp(2,5,t),stirNewtons=Mathf.Lerp(10,20,t),chopNewtons=Mathf.Lerp(12,22,t)};
 }
}

/// <summary>One placement as measured before snapping.</summary>
public struct PlacementResult { public bool accepted;public float positionError,angleError,score; }

/// <summary>Pure scoring and rules shared by the game and the offline tests. Game measures, not clinical ones.</summary>
public static class ActivityRules {
 public static string Title(ActivityKind kind){
  switch(kind){
   case ActivityKind.Bookshelf:return "Organise the bookshelf";
   case ActivityKind.CoffeeTable:return "Set the coffee table";
   case ActivityKind.StorageBox:return "Pack the storage box";
   case ActivityKind.SimpleMeal:return "Cook a simple meal";
   case ActivityKind.Cutting:return "Cut the vegetables";
   case ActivityKind.PourTea:return "Pour and drink tea";
   default:return "Prepare and serve";
  }
 }
 public static bool Kitchen(ActivityKind kind)=>kind==ActivityKind.SimpleMeal||kind==ActivityKind.Cutting||kind==ActivityKind.PrepareAndServe;
 /// <summary>The activities of each room, in menu order.</summary>
 public static readonly ActivityKind[] LivingRoom={ActivityKind.Bookshelf,ActivityKind.CoffeeTable,ActivityKind.StorageBox,ActivityKind.PourTea};
 public static readonly ActivityKind[] KitchenRoom={ActivityKind.SimpleMeal,ActivityKind.Cutting,ActivityKind.PrepareAndServe};
 public static ActivityKind[] Room(ActivityKind kind)=>Kitchen(kind)?KitchenRoom:LivingRoom;
 /// <summary>The next activity in the same room, wrapping round.</summary>
 public static ActivityKind NextInRoom(ActivityKind kind){var room=Room(kind);return room[(Array.IndexOf(room,kind)+1)%room.Length];}
 /// <summary>Module 1 task number used for saved sessions (task 0 = force rounds).</summary>
 public static int Task(ActivityKind kind)=>2+(int)kind;

 /// <summary>Smallest absolute difference of two yaw angles in degrees, honouring symmetry (360 = none, 180 = two-fold...).</summary>
 public static float YawError(float a,float b,float symmetry=360){
  if(!float.IsFinite(a)||!float.IsFinite(b))return 180;
  symmetry=float.IsFinite(symmetry)&&symmetry>0?Math.Min(360,symmetry):360;
  float d=((a-b)%symmetry+symmetry)%symmetry;return Math.Min(d,symmetry-d);
 }
 /// <summary>Score 0..1 of one placement: full marks at the target, zero at the tolerance edge.</summary>
 public static PlacementResult Placement(float positionError,float angleError,float positionTolerance,float angleTolerance){
  var r=new PlacementResult{positionError=positionError,angleError=angleError};
  if(!float.IsFinite(positionError)||!float.IsFinite(angleError)||positionTolerance<=0||angleTolerance<=0)return r;
  r.accepted=positionError<=positionTolerance&&angleError<=angleTolerance;
  if(r.accepted)r.score=.7f*(1-positionError/positionTolerance)+.3f*(1-angleError/angleTolerance);
  return r;
 }
 /// <summary>Bookshelf rule: slot i (left to right) takes the book of height rank i (shortest first).</summary>
 public static bool BookFitsSlot(float[] heights,int book,int slot){
  if(heights==null||book<0||book>=heights.Length||slot<0||slot>=heights.Length)return false;
  int rank=0;for(int i=0;i<heights.Length;i++)if(heights[i]<heights[book]||heights[i]==heights[book]&&i<book)rank++;
  return rank==slot;
 }
 /// <summary>Evenness of cut pieces, 0..1 (1 = all the same length): 1 - coefficient of variation.</summary>
 public static float Evenness(IList<float> lengths){
  if(lengths==null||lengths.Count<2)return lengths!=null&&lengths.Count==1?1:0;
  double sum=0;foreach(var l in lengths){if(!float.IsFinite(l)||l<=0)return 0;sum+=l;}
  double mean=sum/lengths.Count,variance=0;foreach(var l in lengths)variance+=(l-mean)*(l-mean);
  return (float)Math.Max(0,1-Math.Sqrt(variance/lengths.Count)/mean);
 }
 /// <summary>Where along a length the next of n even cuts should go (0..1), for guidance and scoring.</summary>
 public static float EvenCutPosition(int index,int cuts)=>cuts<=0?.5f:(index+1)/(float)(cuts+1);
 /// <summary>
 /// Activity score 0-100: 60 from placement accuracy (0..1), 25 for finishing every step, 15 lost over five errors.
 /// Stars as in the other exercises (85 / 65 / any).
 /// </summary>
 public static int Score(float accuracy,bool complete,int errors){
  accuracy=float.IsFinite(accuracy)?Mathf.Clamp01(accuracy):0;
  return Mathf.Clamp(Mathf.RoundToInt(60*accuracy+(complete?25:0)+15*Mathf.Clamp01(1-Math.Max(0,errors)/5f)),0,100);
 }
 public static int Stars(int score)=>score>=85?3:score>=65?2:score>0?1:0;
}

/// <summary>Ordered steps with instructions and per-step progress; the activity advances when a step completes.</summary>
public sealed class ActivitySteps {
 readonly List<string> instructions=new List<string>();readonly List<int> needed=new List<int>();readonly List<int> done=new List<int>();
 public int Count=>instructions.Count;
 public int Current{get;private set;}
 public bool Complete=>Count>0&&Current>=Count;
 public void Add(string instruction,int count=1){instructions.Add(instruction);needed.Add(Math.Max(1,count));done.Add(0);}
 public string Instruction=>Complete?"Done":instructions[Current];
 public int CurrentDone=>Complete?0:done[Current];
 public int CurrentNeeded=>Complete?0:needed[Current];
 /// <summary>Overall progress 0..1 counting units inside steps.</summary>
 public float Progress{get{int total=0,got=0;for(int i=0;i<Count;i++){total+=needed[i];got+=Math.Min(needed[i],done[i]);}return total==0?0:got/(float)total;}}
 /// <summary>Credits units to the current step; moves on when it is full. Returns true if the step just finished.</summary>
 public bool Advance(int units=1){
  if(Complete||units<=0)return false;
  done[Current]=Math.Min(needed[Current],done[Current]+units);
  if(done[Current]<needed[Current])return false;
  Current++;return true;
 }
 /// <summary>Takes a unit back from the current step (an item removed again).</summary>
 public void Retract(){if(!Complete&&done[Current]>0)done[Current]--;}
 public void Reset(){Current=0;for(int i=0;i<done.Count;i++)done[i]=0;}
}
}
