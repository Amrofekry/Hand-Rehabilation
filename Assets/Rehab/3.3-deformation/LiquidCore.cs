using System;

namespace GripRehab {
/// <summary>
/// Pure liquid physics for the tea activity (no Unity types, so the offline harness compiles and tests it).
/// SI units inside (metres, seconds, cubic metres); millilitres only at the edges. A game-scale model built on real
/// relations (frustum volume, Torricelli outflow, the first sloshing mode of a cylindrical tank), not a CFD solver.
/// </summary>
public static class LiquidMath {
 public const float Gravity=9.81f,MlPerM3=1e6f;
 public static float Ml(float m3)=>m3*MlPerM3;
 public static float M3(float ml)=>ml/MlPerM3;
 /// <summary>Volume of an upright frustum filled to height h (radius r0 at the bottom, r1 at height H).</summary>
 public static float FrustumVolume(float r0,float r1,float H,float h){
  if(!(H>0)||!(h>0))return 0;h=Math.Min(h,H);float rh=r0+(r1-r0)*h/H;
  return (float)(Math.PI*h/3*(r0*r0+r0*rh+rh*rh));
 }
 /// <summary>Fill height of an upright frustum holding volume v (bisection; exact to under 0.01 mm).</summary>
 public static float FrustumLevel(float r0,float r1,float H,float v){
  if(!(v>0)||!(H>0))return 0;if(v>=FrustumVolume(r0,r1,H,H))return H;
  float lo=0,hi=H;for(int i=0;i<32;i++){float mid=(lo+hi)/2;if(FrustumVolume(r0,r1,H,mid)<v)lo=mid;else hi=mid;}
  return (lo+hi)/2;
 }
 /// <summary>
 /// Most liquid a cylinder of radius R and height H holds when its free surface rises by slope s (tan of the angle between
 /// the surface and the vessel's base) across the vessel: the surface plane touches the rim. Exact for the cylinder while
 /// the plane stays above the base; past that the triangular-section form (continuous at the switch) approximates the ungula.
 /// </summary>
 public static float RetainedVolume(float R,float H,float s){
  if(!(R>0)||!(H>0))return 0;s=Math.Abs(float.IsFinite(s)?s:1e6f);
  float area=(float)(Math.PI*R*R);
  return s*R<=H/2?area*(H-R*s):area*H*H/(4*R*s);
 }
 /// <summary>Natural angular frequency (rad/s) of the first sloshing mode of liquid of depth h in a cylinder of radius R.</summary>
 public static float SloshOmega(float R,float h){
  if(!(R>0)||!(h>0))return 0;const float k=1.8412f; // first root of J1'
  return (float)Math.Sqrt(k*Gravity/R*Math.Tanh(k*h/R));
 }
 /// <summary>Torricelli outflow (m³/s) through an opening of area A under head h, discharge coefficient cd.</summary>
 public static float Outflow(float cd,float area,float head)=>head>0&&area>0?cd*area*(float)Math.Sqrt(2*Gravity*head):0;
 /// <summary>Speed (m/s) of liquid leaving under head h (velocity coefficient cv).</summary>
 public static float ExitSpeed(float cv,float head)=>head>0?cv*(float)Math.Sqrt(2*Gravity*head):0;
}

/// <summary>
/// The liquid in one vessel: volume, level and free-surface slosh. Tilt and acceleration are given in the vessel's
/// horizontal frame as two components (x, z): the tangent of the axis lean and the horizontal acceleration.
/// </summary>
public sealed class VesselLiquid {
 public readonly float bottomRadius,topRadius,height;
 public float Volume{get;private set;}
 /// <summary>Surface slope from sloshing (tan of the wave tilt), per horizontal axis.</summary>
 public float SloshX{get;private set;}
 public float SloshZ{get;private set;}
 float vx,vz;
 /// <summary>Damping ratio of the slosh mode (water in a small cup is lightly damped).</summary>
 public float damping=.045f;
 /// <summary>How fast excess liquid leaves over the rim (1/s); finite, so it pours over rather than vanishing.</summary>
 public float overflowRate=9f;
 public float SpilledOverRim{get;private set;}
 public VesselLiquid(float bottomRadius,float topRadius,float height){this.bottomRadius=bottomRadius;this.topRadius=topRadius;this.height=height;}
 public float MeanRadius=>(bottomRadius+topRadius)/2;
 public float Capacity=>LiquidMath.FrustumVolume(bottomRadius,topRadius,height,height);
 public float Level=>LiquidMath.FrustumLevel(bottomRadius,topRadius,height,Volume);
 public float FillFraction=>Capacity>0?Volume/Capacity:0;
 public void Reset(float volume=0){Volume=Math.Max(0,Math.Min(Capacity,volume));SloshX=SloshZ=vx=vz=0;SpilledOverRim=0;}
 /// <summary>Adds liquid (m³); returns what did not fit (it runs over the rim at once).</summary>
 public float Add(float m3){if(!(m3>0))return 0;float room=Capacity-Volume,taken=Math.Min(room,m3);Volume+=taken;return m3-taken;}
 /// <summary>Removes up to m3 (drinking); returns what was removed.</summary>
 public float Remove(float m3){if(!(m3>0))return 0;float taken=Math.Min(Volume,m3);Volume-=taken;return taken;}
 /// <summary>Rim rise of the free surface: R times the combined slope of tilt and wave.</summary>
 public float SurfaceSlope(float tiltX,float tiltZ){float x=tiltX+SloshX,z=tiltZ+SloshZ;return (float)Math.Sqrt(x*x+z*z);}
 /// <summary>
 /// One step: the slosh mode is driven by the vessel's horizontal acceleration (quasi-static tilt a/g), then whatever
 /// the tilted, sloshing surface cannot hold leaves over the rim. Returns the volume spilled over the rim this step (m³).
 /// </summary>
 public float Step(float dt,float tiltX,float tiltZ,float accelX,float accelZ){
  if(!(dt>0)||dt>.25f)return 0;
  if(!float.IsFinite(tiltX))tiltX=0;if(!float.IsFinite(tiltZ))tiltZ=0;
  if(!float.IsFinite(accelX))accelX=0;if(!float.IsFinite(accelZ))accelZ=0;
  float w=LiquidMath.SloshOmega(MeanRadius,Math.Max(.002f,Level));
  if(Volume<=0||w<=0){SloshX=SloshZ=vx=vz=0;return 0;}
  // Semi-implicit Euler, substepped so w*h stays small (stable at 30 Hz camera rates and at 0.25 s pauses).
  int n=Math.Max(1,(int)Math.Ceiling(dt*w/.2f));float h=dt/n,tx=-accelX/LiquidMath.Gravity,tz=-accelZ/LiquidMath.Gravity;
  for(int i=0;i<n;i++){
   vx+=(-2*damping*w*vx-w*w*(SloshX-tx))*h;SloshX+=vx*h;
   vz+=(-2*damping*w*vz-w*w*(SloshZ-tz))*h;SloshZ+=vz*h;
  }
  const float maxSlope=1.2f;float s=(float)Math.Sqrt(SloshX*SloshX+SloshZ*SloshZ);
  if(s>maxSlope){SloshX*=maxSlope/s;SloshZ*=maxSlope/s;vx*=.5f;vz*=.5f;}
  float keep=LiquidMath.RetainedVolume(MeanRadius,height,SurfaceSlope(tiltX,tiltZ));
  if(Volume<=keep)return 0;
  float lost=Math.Min(Volume-keep,(Volume-keep)*overflowRate*dt+1e-7f);
  Volume-=lost;SpilledOverRim+=lost;return lost;
 }
 /// <summary>A kick to the wave (a stream landing, a bump), as a change of slope speed.</summary>
 public void Kick(float dx,float dz){if(float.IsFinite(dx)&&float.IsFinite(dz)){vx+=dx;vz+=dz;}}
}

/// <summary>
/// A teapot body (cylinder radius R, height H) with a spout whose outlet sits at height spoutHeight and horizontal
/// distance spoutReach from the axis, in the direction the pot is tipped to pour.
/// </summary>
public sealed class SpoutVessel {
 public readonly float radius,height,spoutHeight,spoutReach,spoutArea;
 public float dischargeCoefficient=.62f,velocityCoefficient=.97f;
 public float Volume{get;private set;}
 public SpoutVessel(float radius,float height,float spoutHeight,float spoutReach,float spoutRadius){
  this.radius=radius;this.height=height;this.spoutHeight=spoutHeight;this.spoutReach=spoutReach;spoutArea=(float)(Math.PI*spoutRadius*spoutRadius);}
 public float Capacity=>(float)(Math.PI*radius*radius*height);
 public void Fill(float m3){Volume=Math.Max(0,Math.Min(Capacity,m3));}
 /// <summary>
 /// Head (m) of liquid above the spout outlet when the pot is tipped by pitch radians toward the spout: the free surface
 /// stays horizontal while the outlet drops by reach*sin(pitch) and its height scales by cos(pitch).
 /// </summary>
 public float Head(float pitch){
  if(Volume<=0||!float.IsFinite(pitch))return 0;
  double c=Math.Cos(pitch),s=Math.Sin(pitch);
  // Liquid depth at the axis: the pot's own volume over its area while the surface stays above the base.
  double axisDepth=Volume/(Math.PI*radius*radius);
  double surface=axisDepth*c,outlet=spoutHeight*c-spoutReach*s;
  // Tipped hard, the surface on the spout side cannot rise above the body's top edge near the spout.
  return (float)Math.Max(0,surface-outlet);
 }
 public float FlowRate(float pitch)=>LiquidMath.Outflow(dischargeCoefficient,spoutArea,Head(pitch));
 public float ExitSpeed(float pitch)=>LiquidMath.ExitSpeed(velocityCoefficient,Head(pitch));
 /// <summary>Pours for dt seconds at pitch; returns the volume that left through the spout (m³).</summary>
 public float Pour(float pitch,float dt){if(!(dt>0))return 0;float out_=Math.Min(Volume,FlowRate(pitch)*dt);Volume-=out_;return out_;}
}

/// <summary>
/// Hand tremor from a camera track, with no allocation after construction: the slow (voluntary) motion is removed with a
/// moving average, the rest gives an RMS amplitude, and a direct Fourier scan of 3-12 Hz gives the dominant frequency.
/// Positions in millimetres (the caller scales camera pixels by a reference hand size), times in seconds. An estimate
/// for game feedback and research analysis, not a clinical tremor measurement.
/// </summary>
public sealed class TremorEstimator {
 readonly double[] t,x,y;readonly float[] rx,ry;int count,head;
 public readonly float detrendSeconds;
 public TremorEstimator(int capacity=160,float detrendSeconds=.33f){t=new double[capacity];x=new double[capacity];y=new double[capacity];rx=new float[capacity];ry=new float[capacity];this.detrendSeconds=detrendSeconds;}
 public int Count=>count;
 public double Span=>count<2?0:t[(head-1+t.Length)%t.Length]-t[(head-count+t.Length)%t.Length];
 public void Clear(){count=head=0;}
 public void Add(double time,float xMm,float yMm){
  if(!double.IsFinite(time)||!float.IsFinite(xMm)||!float.IsFinite(yMm))return;
  if(count>0&&time<=t[(head-1+t.Length)%t.Length])return;
  t[head]=time;x[head]=xMm;y[head]=yMm;head=(head+1)%t.Length;if(count<t.Length)count++;
 }
 int Index(int i)=>(head-count+i+t.Length)%t.Length;
 /// <summary>Measures the current window. False until it spans at least 1.5 s.</summary>
 public bool Measure(out float rmsMm,out float hz)=>Measure(out rmsMm,out hz,out _);
 /// <summary>
 /// As Measure, and slowRangeMm: how far the slow (voluntary) path travelled in the window. Tremor is read while the hand
 /// holds a position or pours, so callers skip windows with a large voluntary move (lifting the cup to the mouth).
 /// </summary>
 public bool Measure(out float rmsMm,out float hz,out float slowRangeMm){
  rmsMm=0;hz=0;slowRangeMm=0;if(count<16||Span<1.5)return false;
  double minX=double.MaxValue,maxX=double.MinValue,minY=double.MaxValue,maxY=double.MinValue;
  // Residual after a centred moving average (removes the slow voluntary path).
  double half=detrendSeconds/2,sum=0;int used=0;
  for(int i=0;i<count;i++){
   double ti=t[Index(i)],sx=0,sy=0;int n=0;
   for(int j=0;j<count;j++){double d=t[Index(j)]-ti;if(d>=-half&&d<=half){sx+=x[Index(j)];sy+=y[Index(j)];n++;}}
   rx[i]=(float)(x[Index(i)]-sx/n);ry[i]=(float)(y[Index(i)]-sy/n);
   double mx=sx/n,my=sy/n;if(mx<minX)minX=mx;if(mx>maxX)maxX=mx;if(my<minY)minY=my;if(my>maxY)maxY=my;
   if(t[Index(i)]-t[Index(0)]>=half&&t[Index(count-1)]-t[Index(i)]>=half){sum+=rx[i]*rx[i]+ry[i]*ry[i];used++;}
  }
  if(used<8)return false;
  rmsMm=(float)Math.Sqrt(sum/used);slowRangeMm=(float)Math.Sqrt((maxX-minX)*(maxX-minX)+(maxY-minY)*(maxY-minY));
  // Dominant frequency: power of the residual at 3-12 Hz in 0.25 Hz steps (timestamps need not be even).
  double best=-1;double t0=t[Index(0)];
  for(double f=3;f<=12.0001;f+=.25){
   double cx=0,sx=0,cy=0,sy=0,w=2*Math.PI*f;
   for(int i=0;i<count;i++){double a=w*(t[Index(i)]-t0),c=Math.Cos(a),s=Math.Sin(a);cx+=rx[i]*c;sx+=rx[i]*s;cy+=ry[i]*c;sy+=ry[i]*s;}
   double p=cx*cx+sx*sx+cy*cy+sy*sy;if(p>best){best=p;hz=(float)f;}
  }
  return true;
 }
}

/// <summary>Running summary of repeated tremor measurements (mean amplitude, amplitude-weighted frequency).</summary>
public sealed class TremorSummary {
 double rms,hzWeighted,weight;int samples;
 public int Samples=>samples;
 public float MeanRmsMm=>samples==0?0:(float)(rms/samples);
 public float DominantHz=>weight<=0?0:(float)(hzWeighted/weight);
 public void Add(float rmsMm,float hz){if(!float.IsFinite(rmsMm)||!float.IsFinite(hz)||rmsMm<0)return;rms+=rmsMm;hzWeighted+=hz*rmsMm;weight+=rmsMm;samples++;}
 public void Clear(){rms=hzWeighted=weight=0;samples=0;}
}

/// <summary>Running mean and standard deviation (Welford), for flow and tilt steadiness.</summary>
public sealed class RunningStats {
 double mean,m2;int n;
 public int Count=>n;public float Mean=>(float)mean;
 public float Sd=>n<2?0:(float)Math.Sqrt(m2/(n-1));
 public float Cv=>n<2||Math.Abs(mean)<1e-9?0:(float)(Math.Sqrt(m2/(n-1))/Math.Abs(mean));
 public void Add(float v){if(!float.IsFinite(v))return;n++;double d=v-mean;mean+=d/n;m2+=d*(v-mean);}
 public void Clear(){mean=m2=0;n=0;}
}

/// <summary>Tea activity rules: how full the cup should be, how close counts, and the score. Game measures, not clinical.</summary>
public static class TeaRules {
 /// <summary>Target fill as a fraction of the cup's capacity.</summary>
 public const float TargetFraction=.75f;
 /// <summary>How far from the fill line still counts (fraction of the target), level 1 (generous) to 10 (tight).</summary>
 public static float Tolerance(int level){float t=(Math.Max(1,Math.Min(10,level))-1)/9f;return .14f+(.05f-.14f)*t;}
 /// <summary>Spilled millilitres that count as one error (more forgiving at low levels).</summary>
 public static float SpillErrorMl(int level){float t=(Math.Max(1,Math.Min(10,level))-1)/9f;return 15+(5-15)*t;}
 /// <summary>Fill accuracy 0..1: 1 on the line, 0 at the tolerance edge or beyond.</summary>
 public static float FillAccuracy(float filledMl,float targetMl,int level){
  if(!(targetMl>0)||!float.IsFinite(filledMl))return 0;
  float err=Math.Abs(filledMl-targetMl)/targetMl;return Math.Max(0,1-err/Tolerance(level));
 }
 public static bool FillAccepted(float filledMl,float targetMl,int level)=>targetMl>0&&Math.Abs(filledMl-targetMl)/targetMl<=Tolerance(level);
 /// <summary>Share of the poured tea that went into the cup, 0..1 (1 when nothing has been poured yet).</summary>
 public static float PourAccuracy(float intoCupMl,float pouredMl)=>pouredMl<=0?1:Math.Max(0,Math.Min(1,intoCupMl/pouredMl));
 /// <summary>Accuracy used for the activity score: 60 % filling to the line, 40 % keeping the tea in the cup.</summary>
 public static float Accuracy(float fillAccuracy,float pourAccuracy)=>Math.Max(0,Math.Min(1,.6f*fillAccuracy+.4f*pourAccuracy));
}
}
