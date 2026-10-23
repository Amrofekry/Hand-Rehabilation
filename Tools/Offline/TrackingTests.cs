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
    static int Main()
    {
        try
        {
            // Independent known-axis expectations also check the managed shim.
            Check(Vector3.Distance(Quaternion.AngleAxis(90,Vector3.forward)*Vector3.right,Vector3.up)<.0001f,"math shim positive Z rotates X to Y");
            Check(Vector3.Distance(Quaternion.LookRotation(Vector3.right,Vector3.up)*Vector3.forward,Vector3.right)<.0001f,"math shim LookRotation maps forward");
            foreach(bool left in new[]{false,true})foreach(var rest in new[]{Quaternion.identity,Quaternion.AngleAxis(35,Vector3.up)})Run(left,rest);
            Stability(false);Stability(true);
            StreamOrder();
            Tracing();
            Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed; production pose source with managed test math; no Unity/camera/native SDK");return failed==0?0:1;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e);return 1; }
    }
}
