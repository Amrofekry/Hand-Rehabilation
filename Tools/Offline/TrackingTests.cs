using System;
using System.Reflection;
using GripRehab;
using UnityEngine;
class TrackingTests
{
    static int passed,failed;
    static void Check(bool ok,string message)
    { if(ok){passed++;Console.WriteLine("PASS "+message);}else{failed++;Console.Error.WriteLine("FAIL "+message);} }
    static void Settle(FingerPoseDriver driver,LandmarkFrame frame,bool camera=true)
    { for(int i=0;i<24;i++)driver.Pose(frame,true,0,.05f,.045f,new Vector3(),.85f,camera); }
    static LandmarkFrame Frame(bool left,Quaternion turn,float x=.5f,float y=.5f,float scale=1)
    {
        var f=new LandmarkFrame { schemaVersion=1,confidence=.95f,handedness=left?"Left":"Right",source="fabricated offline test",sequence=1,captureSeconds=1,imageWidth=640,imageHeight=480,landmarks=new Vector3[21],worldLandmarks=new Vector3[21] };
        var shape=new Vector3[21];
        for(int finger=0;finger<5;finger++)for(int joint=0;joint<4;joint++)
            shape[1+finger*4+joint]=new Vector3((finger-2)*.025f*(left?-1:1),.10f+joint*.04f,0);
        for(int i=0;i<21;i++)
        {
            var p=turn*shape[i];f.worldLandmarks[i]=new Vector3(p.x,-p.y,-p.z);
            var image=shape[i]*scale;f.landmarks[i]=new Vector3(x+image.x,y-image.y,-image.z);
        }
        return f;
    }
    static FingerPoseDriver Driver(Quaternion rest)
    {
        var d=new FingerPoseDriver { joints=new Transform[15] };
        d.transform.localRotation=rest;
        for(int i=0;i<15;i++)d.joints[i]=new Transform();
        typeof(FingerPoseDriver).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(d,null);
        return d;
    }
    static LandmarkFrame ImageOnly(LandmarkFrame frame)
    {
        // Encode the same isotropic shape into width-normalized X/Z and
        // height-normalized Y, then discard world points before production code.
        for(int i=0;i<21;i++)
        {
            var p=frame.worldLandmarks[i];
            frame.landmarks[i]=new Vector3(.5f+p.x,.5f+p.y/.75f,p.z);
        }
        frame.worldLandmarks=null;return frame;
    }
    static void Run(bool left,Quaternion authoredRest)
    {
        string side=left?"Left":"Right";var d=Driver(authoredRest);var open=Frame(left,Quaternion.identity);
        Settle(d,open);var neutral=d.transform.localRotation;
        var oldFacing=Quaternion.AngleAxis(180,Vector3.forward)*authoredRest;
        Check(Vector3.Distance(neutral*Vector3.forward,new Vector3(-(oldFacing*Vector3.forward).x,(oldFacing*Vector3.forward).y,-(oldFacing*Vector3.forward).z))<.001f,side+" turns palm/back facing over around upright Y");
        Check(Vector3.Distance(neutral*Vector3.up,new Vector3(-(oldFacing*Vector3.up).x,(oldFacing*Vector3.up).y,-(oldFacing*Vector3.up).z))<.001f,side+" rest facing retains vertical finger component");
        Settle(d,Frame(left,Quaternion.identity,.65f));Check(d.transform.localPosition.x>.12f,side+" received image-right moves game-right");
        Settle(d,Frame(left,Quaternion.identity,.35f));Check(d.transform.localPosition.x<-.12f,side+" received image-left moves game-left");
        Settle(d,Frame(left,Quaternion.identity,.5f,.35f));Check(d.transform.localPosition.y>.12f,side+" image-up still moves game-up");
        Settle(d,Frame(left,Quaternion.identity,.5f,.65f));Check(d.transform.localPosition.y<-.12f,side+" image-down still moves game-down");
        Settle(d,Frame(left,Quaternion.identity,scale:1.2f));Check(d.transform.localPosition.z>.14f,side+" larger palm retains forward depth");
        Settle(d,Frame(left,Quaternion.identity,scale:.8f));Check(d.transform.localPosition.z<-.14f,side+" smaller palm retains backward depth");
        foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})foreach(float angle in new[]{-75f,75f})
        {
            var q=Quaternion.AngleAxis(angle,axis);Settle(d,Frame(left,q));
            // Compare vectors, not an implementation-derived reflected quaternion.
            var delta=d.transform.localRotation*Quaternion.Inverse(neutral);
            Check(Vector3.Distance(delta*Vector3.right,q*Vector3.right)<.001f&&Vector3.Distance(delta*Vector3.up,q*Vector3.up)<.001f&&Vector3.Distance(delta*Vector3.forward,q*Vector3.forward)<.001f,side+" signed "+angle+" turn on "+axis.x+","+axis.y+","+axis.z);
        }
        Settle(d,open);var baseline=(float[])d.JointAngles.Clone();
        for(int finger=0;finger<5;finger++)
        {
            var bent=Frame(left,Quaternion.identity);int n=finger<4?5+finger*4:1;
            bent.worldLandmarks[n+2]=bent.worldLandmarks[n+1]+new Vector3(0,0,.04f);
            bent.worldLandmarks[n+3]=bent.worldLandmarks[n+2]+new Vector3(0,0,.02f);
            Settle(d,bent);bool others=true;
            for(int j=0;j<15;j++)if(j/3!=finger&&Math.Abs(d.JointAngles[j]-baseline[j])>.1f)others=false;
            Check(d.JointAngles[finger*3+1]>60&&others,side+" finger "+finger+" remains independent");
        }
        var recentered=Frame(left,Quaternion.AngleAxis(65,Vector3.forward));d.RecenterTracking();Settle(d,recentered);
        Check(Quaternion.Angle(d.transform.localRotation,neutral)<.1f,side+" recalibration returns to corrected rest");
        var bad=Frame(left,Quaternion.identity);Array.Clear(bad.worldLandmarks,0,21);Settle(d,bad);
        Check(!d.TrackingUsed,side+" collapsed palm is rejected");
        d.Pose(open,false,0,.05f,.045f,new Vector3(),.85f,true);
        Check(!d.TrackingUsed,side+" explicit tracking loss stays inactive");
        Settle(d,open);Check(d.TrackingUsed,side+" tracking resumes after valid frame");
        var legacy=ImageOnly(Frame(left,Quaternion.identity));d.RecenterTracking();Settle(d,legacy);
        Check(d.TrackingUsed&&!d.UsingWorldLandmarks,side+" legacy image-only input still works");
        var legacyNeutral=d.transform.localRotation;
        foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})foreach(float angle in new[]{-60f,60f})
        {
            var q=Quaternion.AngleAxis(angle,axis);Settle(d,ImageOnly(Frame(left,q)));
            var delta=d.transform.localRotation*Quaternion.Inverse(legacyNeutral);
            Check(Vector3.Distance(delta*Vector3.right,q*Vector3.right)<.001f&&Vector3.Distance(delta*Vector3.up,q*Vector3.up)<.001f&&Vector3.Distance(delta*Vector3.forward,q*Vector3.forward)<.001f,side+" image-only signed "+angle+" turn on "+axis.x+","+axis.y+","+axis.z);
        }
        Settle(d,Frame(left,Quaternion.identity,.65f));Check(d.transform.localPosition.x>.12f,side+" legacy-to-world transition retains X sign");
        var keyboard=Driver(authoredRest);keyboard.independentKeyboard=true;keyboard.AdjustFinger(0,1);
        for(int i=0;i<24;i++)keyboard.Pose(null,false,0,.05f,.045f);
        Check(keyboard.JointAngles[1]>80&&keyboard.JointAngles[4]<.01f&&Quaternion.Angle(keyboard.transform.localRotation,authoredRest)<.1f,side+" keyboard fingers and authored demo rest unchanged");
    }
    static void Stability(bool left)
    {
        var driver=Driver(Quaternion.identity);var neutral=Frame(left,Quaternion.identity);
        Settle(driver,neutral);
        var turned=Frame(left,Quaternion.AngleAxis(40,Vector3.forward),.6f);
        Settle(driver,turned);var rotation=driver.transform.localRotation;var position=driver.transform.localPosition;
        driver.Pose(null,false,0,.02f,.045f,new Vector3(),.85f,true);
        // WITHDRAWN (AUDIT A-022, rolled back 12 Oct): Check(!driver.TrackingUsed&&Vector3.Distance(position,driver.transform.localPosition)<.0001f,"lost sample holds visuals but invalidates tracking");
        Settle(driver,turned);
        // WITHDRAWN (AUDIT A-022, rolled back 12 Oct): Check(Quaternion.Angle(rotation,driver.transform.localRotation)<.1f,"reacquisition preserves calibration instead of resetting wrist rotation");
        // Pinch scoring was withdrawn with the 12 Oct rollback; its checks were removed with it.
        var stable=Driver(Quaternion.identity);Settle(stable,neutral);
        float inputEnergy=0,outputEnergy=0;
        for(int i=0;i<180;i++){
            float noise=(i%2==0?1:-1)*.003f;
            var noisy=Frame(left,Quaternion.AngleAxis(i%2==0?1.5f:-1.5f,Vector3.forward),.5f+noise);
            stable.Pose(noisy,true,0,1f/60,.045f,new Vector3(),.85f,true);
            if(i>30){inputEnergy+=noise*noise*.85f*.85f;outputEnergy+=stable.transform.localPosition.x*stable.transform.localPosition.x;}
        }
        Check(outputEnergy<inputEnergy*.4f,"alternating webcam-sized wrist noise is attenuated without overshoot");
        var moved=Frame(left,Quaternion.identity,.7f);float response=0;
        while(stable.transform.localPosition.x<.17f*.9f&&response<.3f){
            stable.Pose(moved,true,0,1f/60,.045f,new Vector3(),.85f,true);response+=1f/60;
        }
        // WITHDRAWN (AUDIT A-022, rolled back 12 Oct): Check(response<=.08f,"intentional translation reaches 90 percent within 80 ms of received sample");
        var gate=new CameraGripGate();
        for(int i=0;i<10;i++)gate.Step(.8f,true,.02f,.55f,.28f,.12f);
        Check(gate.Closed,"close dwell establishes grip");
        for(int i=0;i<50;i++)gate.Step(.4f,true,.02f,.55f,.28f,.12f);
        Check(gate.Closed,"hysteresis band retains grip");
        for(int i=0;i<10;i++)gate.Step(.1f,true,.02f,.55f,.28f,.12f);
        Check(!gate.Closed,"open dwell releases grip");
    }
    // AUDIT A-001: a restarted camera worker restarts its sequence at 1.
    static void StreamOrder()
    {
        Check(HandStreamOrder.Accepts(11,2.1,10,2.0,false),"next packet accepted");
        Check(!HandStreamOrder.Accepts(10,2.1,10,2.0,false),"duplicate sequence rejected while stream is live");
        Check(!HandStreamOrder.Accepts(11,1.9,10,2.0,false),"older capture rejected while stream is live");
        Check(HandStreamOrder.Accepts(1,5.0,20000,3.0,true),"restarted worker accepted after the stream went stale");
        Check(!HandStreamOrder.Accepts(1,5.0,20000,3.0,false),"sequence reset rejected while the old stream is still live");
        Check(HandStreamOrder.Accepts(0,0,10,2.0,false),"legacy packet without sequence or capture accepted");
    }
    // AUDIT A-018/P-015: accuracy follows the drawn path and fast drags cannot skip a waypoint.
    static TraceTask TraceAlong(int stepsPerSegment)
    {
        var task=new TraceTask();var path=TraceTask.Path;task.Submit(path[0]);
        for(int i=1;i<path.Length;i++)for(int k=1;k<=stepsPerSegment;k++)
        { float t=(float)k/stepsPerSegment;task.Submit(new Vector2(path[i-1].x+(path[i].x-path[i-1].x)*t,path[i-1].y+(path[i].y-path[i-1].y)*t)); }
        return task;
    }
    static void Tracing()
    {
        var careful=TraceAlong(20);
        Check(careful.Complete&&careful.Accuracy>=.95f,"tracing exactly along the path completes with high accuracy ("+careful.Accuracy+")");
        // Samples land about 0.1 before and after each waypoint, never inside its 0.095 radius.
        var path=TraceTask.Path;var fast=new TraceTask();fast.Submit(path[0]);
        for(int i=1;i<path.Length;i++)
        {
            var a=path[i-1];var b=path[i];var c=i+1<path.Length?path[i+1]:new Vector2(b.x+(b.x-a.x),b.y+(b.y-a.y));
            fast.Submit(new Vector2(a.x+(b.x-a.x)*.55f,a.y+(b.y-a.y)*.55f));
            fast.Submit(new Vector2(b.x+(c.x-b.x)*.45f,b.y+(c.y-b.y)*.45f));
        }
        Check(fast.Complete,"a fast drag that straddles each waypoint still reaches every waypoint ("+fast.NextWaypoint+")");
        var wander=new TraceTask();wander.Submit(TraceTask.Path[0]);
        for(int k=0;k<20;k++)wander.Submit(new Vector2(.3f+k*.02f,.9f));
        Check(!wander.Complete&&wander.Accuracy<.5f,"wandering far from the path lowers accuracy ("+wander.Accuracy+")");
        var jump=new TraceTask();jump.Submit(TraceTask.Path[0]);jump.Submit(TraceTask.Path[4]);
        Check(jump.NextWaypoint<=2,"jumping to the last waypoint cannot skip the route ("+jump.NextWaypoint+")");
    }
    // STAGE 3.2: gesture tokens for Sequence Memory (Open, Fist, Point, Pinch).
    static LandmarkFrame GestureFrame(bool[] extended,bool pinch)
    {
        var f=new LandmarkFrame{schemaVersion=1,confidence=.95f,handedness="Right",source="fabricated gesture",landmarks=new Vector3[21]};
        f.landmarks[0]=new Vector3(.5f,.8f,0);
        f.landmarks[1]=new Vector3(.44f,.74f,0);f.landmarks[2]=new Vector3(.40f,.68f,0);f.landmarks[3]=new Vector3(.37f,.63f,0);f.landmarks[4]=new Vector3(.35f,.58f,0);
        for(int i=0;i<4;i++)
        {
            float x=.45f+.035f*i;int b=5+4*i;
            f.landmarks[b]=new Vector3(x,.62f,0);
            if(extended[i]){f.landmarks[b+1]=new Vector3(x,.55f,0);f.landmarks[b+2]=new Vector3(x,.50f,0);f.landmarks[b+3]=new Vector3(x,.46f,0);}
            else{f.landmarks[b+1]=new Vector3(x,.58f,0);f.landmarks[b+2]=new Vector3(x,.64f,0);f.landmarks[b+3]=new Vector3(x,.66f,0);}
        }
        if(pinch){f.landmarks[8]=new Vector3(.45f,.55f,0);f.landmarks[4]=new Vector3(.46f,.56f,0);}
        return f;
    }
    static void Gestures()
    {
        Check(GestureClassifier.Classify(GestureFrame(new[]{true,true,true,true},false))==HandGesture.Open,"open hand classified");
        Check(GestureClassifier.Classify(GestureFrame(new[]{false,false,false,false},false))==HandGesture.Fist,"fist classified");
        Check(GestureClassifier.Classify(GestureFrame(new[]{true,false,false,false},false))==HandGesture.Point,"index point classified");
        Check(GestureClassifier.Classify(GestureFrame(new[]{false,true,true,true},true))==HandGesture.Pinch,"thumb-index pinch classified");
        Check(GestureClassifier.Classify(GestureFrame(new[]{true,true,false,false},false))==HandGesture.None,"ambiguous two-finger pose is not guessed");
        Check(GestureClassifier.Classify(null)==HandGesture.None,"missing frame is None");
        var s=new GestureStabilizer();int emitted=0;HandGesture last=HandGesture.None;
        for(int i=0;i<3;i++)if(s.Step(HandGesture.Fist,.1f,out last))emitted++;
        Check(emitted==0,"a brief gesture is not accepted");
        HandGesture accepted=HandGesture.None;
        for(int i=0;i<10;i++)if(s.Step(HandGesture.Fist,.1f,out last)){emitted++;accepted=last;}
        Check(emitted==1&&accepted==HandGesture.Fist,"a held gesture is accepted once ("+emitted+")");
        for(int i=0;i<3;i++)s.Step(HandGesture.None,.1f,out last);
        for(int i=0;i<6;i++)if(s.Step(HandGesture.Fist,.1f,out last))emitted++;
        Check(emitted==2,"the same gesture again needs a release in between");
    }
    // STAGE 3.3: generated mazes and level routes.
    static void Mazes()
    {
        var legacy=new MazeTask();Check(legacy.Columns==7&&legacy.Rows==5&&legacy.ShortestPath==10,"legacy maze unchanged ("+legacy.ShortestPath+")");
        for(int seed=1;seed<=5;seed++)
        {
            var maze=new MazeTask(9,seed);
            Check(maze.Columns==9&&maze.Rows==9&&!maze.IsWall(0,0)&&!maze.IsWall(8,8)&&maze.ShortestPath>=16,"generated 9x9 maze is solvable (seed "+seed+", path "+maze.ShortestPath+")");
        }
        Check(new MazeTask(9,3).Draw()==new MazeTask(9,3).Draw()&&new MazeTask(9,3).Draw()!=new MazeTask(9,4).Draw(),"maze generation is seeded");
        var solve=new MazeTask(7,2);var path=solve.Solve();
        for(int i=1;i<path.Count;i++)solve.Move(path[i].x-path[i-1].x,path[i].y-path[i-1].y);
        Check(solve.Complete&&solve.Moves==solve.ShortestPath&&solve.Score==100,"walking the shortest path scores 100 ("+solve.Score+")");
        var bump=new MazeTask(7,2);bump.Move(-1,0);bump.Move(0,-1);Check(bump.Collisions==2,"walls cost collisions");
    }
    static void TraceLevels()
    {
        Check(TraceTask.PathFor(1).Length==TraceTask.Path.Length&&TraceTask.PathFor(1)[2].x==TraceTask.Path[2].x,"level 1 route is the legacy route");
        for(int level=2;level<=10;level++)
        {
            var route=TraceTask.PathFor(level);bool inside=true;foreach(var p in route)inside&=p.x>=.05f&&p.x<=.95f&&p.y>=.1f&&p.y<=.9f;
            Check(route.Length>=5&&inside,"level "+level+" route has "+route.Length+" points inside the pad");
        }
        var hard=new TraceTask(TraceTask.PathFor(6),.07f);var route6=TraceTask.PathFor(6);hard.Submit(route6[0]);
        for(int i=1;i<route6.Length;i++)for(int k=1;k<=10;k++){float t=k/10f;hard.Submit(new Vector2(route6[i-1].x+(route6[i].x-route6[i-1].x)*t,route6[i-1].y+(route6[i].y-route6[i-1].y)*t));}
        Check(hard.Complete&&hard.Accuracy>=.95f&&hard.MeanDeviation<.01f,"a careful trace of a harder route stays accurate ("+hard.Accuracy+", "+hard.MeanDeviation+")");
    }
    static int Main()
    {
        // A wrist roll rotates the hand in the image: its palm size (the depth cue) must not change, on a 640x480 frame.
        {
            var a=new Vector3(.4325f,.48f,0); var b=new Vector3(.5675f,.48f,0); float px=(b.x-a.x)*640;
            double angle=60*System.Math.PI/180; var rb=a+new Vector3((float)(px*System.Math.Cos(angle))/640,(float)(px*System.Math.Sin(angle))/480,0);
            float before=GripRehab.FingerPoseDriver.ImageDistance(a,b,640f/480), after=GripRehab.FingerPoseDriver.ImageDistance(a,rb,640f/480);
            Check(System.Math.Abs(after-before)<1e-4f,"palm size is unchanged by an in-image rotation (wrist roll): "+before.ToString("0.0000")+" vs "+after.ToString("0.0000"));
            Check(System.Math.Abs(Vector3.Distance(a,rb)-Vector3.Distance(a,b))>.02f,"(the unscaled normalised distance does change: the bug this guards against)");
        }
        try
        {
            // Independent known-axis expectations also check the managed shim.
            Check(Vector3.Distance(Quaternion.AngleAxis(90,Vector3.forward)*Vector3.right,Vector3.up)<.0001f,"math shim positive Z rotates X to Y");
            Check(Vector3.Distance(Quaternion.LookRotation(Vector3.right,Vector3.up)*Vector3.forward,Vector3.right)<.0001f,"math shim LookRotation maps forward");
            foreach(bool left in new[]{false,true})foreach(var rest in new[]{Quaternion.identity,Quaternion.AngleAxis(35,Vector3.up)})Run(left,rest);
            Stability(false);Stability(true);
            StreamOrder();
            Tracing();
            Gestures();
            Mazes();
            TraceLevels();
            Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed; production pose source with managed test math; no Unity/camera/native SDK");return failed==0?0:1;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e);return 1; }
    }
}
