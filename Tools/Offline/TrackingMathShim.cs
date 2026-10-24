// Test-only managed math/Transform substitute. Never included in Assets or a game.
// Executes the production pose driver without a Unity process. Rendering, PhysX,
// native VirtualGrasp and camera accuracy are outside this test's evidence.
using System;
using N = System.Numerics;
namespace UnityEngine
{
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s) {} }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) {} }
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float a,float b) {} }
    public class MonoBehaviour {
        public Transform transform = new Transform();
#if HAND_RIG_TESTS
        public bool enabled = true;
        protected static T FindAnyObjectByType<T>() where T:class => Object.FindAnyObjectByType<T>();
#endif
    }
    public class Transform
    {
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
#if HAND_RIG_TESTS
        public string name;
        public Vector3 position { get => localPosition; set => localPosition=value; }
        public Quaternion rotation { get => localRotation; set => localRotation=value; }
        public readonly System.Collections.Generic.List<Transform> children = new System.Collections.Generic.List<Transform>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class {
            var all=new System.Collections.Generic.List<T>();
            if(this is T self)all.Add(self);
            foreach(var child in children)all.AddRange(child.GetComponentsInChildren<T>(includeInactive));
            return all.ToArray();
        }
#endif
    }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) { this.x=x;this.y=y; }
        public static implicit operator Vector2(Vector3 p) { return new Vector2 { x=p.x,y=p.y }; }
        public static float Distance(Vector2 a,Vector2 b) { return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)); }
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 right => new Vector3(1,0,0);
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 forward => new Vector3(0,0,1);
        public static Vector3 one => new Vector3(1,1,1);
        public float sqrMagnitude => x*x+y*y+z*z;
        public Vector3 normalized => sqrMagnitude < 1e-20f ? new Vector3() : this/(float)Math.Sqrt(sqrMagnitude);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a,float b) => a*(1/b);
        public static float Dot(Vector3 a,Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a,Vector3 b) => new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n) => n.sqrMagnitude < 1e-20f ? a : a-n*(Dot(a,n)/n.sqrMagnitude);
        public static float Distance(Vector3 a,Vector3 b) => (float)Math.Sqrt((a-b).sqrMagnitude);
        public static float Angle(Vector3 a,Vector3 b) => a.sqrMagnitude*b.sqrMagnitude < 1e-20f ? 0 : (float)(Math.Acos(Mathf.Clamp(Dot(a,b)/(float)Math.Sqrt(a.sqrMagnitude*b.sqrMagnitude),-1,1))*180/Math.PI);
        public static float SignedAngle(Vector3 a,Vector3 b,Vector3 n) => Angle(a,b)*(Dot(n,Cross(a,b))<0?-1:1);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t) => a+(b-a)*Mathf.Clamp01(t);
    }
    public struct Quaternion
    {
        public float x,y,z,w;
        public Quaternion(float x,float y,float z,float w) { this.x=x;this.y=y;this.z=z;this.w=w; }
        N.Quaternion Value => new N.Quaternion(x,y,z,w);
        static Quaternion Wrap(N.Quaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static Quaternion identity => new Quaternion(0,0,0,1);
        public static Quaternion operator *(Quaternion a,Quaternion b) => Wrap(a.Value*b.Value);
        public static Vector3 operator *(Quaternion q,Vector3 v)
        { var p=N.Vector3.Transform(new N.Vector3(v.x,v.y,v.z),q.Value);return new Vector3(p.X,p.Y,p.Z); }
        public static Quaternion Inverse(Quaternion q) => Wrap(N.Quaternion.Inverse(q.Value));
        public static Quaternion AngleAxis(float degrees,Vector3 axis)
        { axis=axis.normalized;return Wrap(N.Quaternion.CreateFromAxisAngle(new N.Vector3(axis.x,axis.y,axis.z),degrees*(float)Math.PI/180)); }
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t) => Wrap(N.Quaternion.Slerp(a.Value,b.Value,Mathf.Clamp01(t)));
        public static float Angle(Quaternion a,Quaternion b)
        { float dot=Math.Abs(N.Quaternion.Dot(N.Quaternion.Normalize(a.Value),N.Quaternion.Normalize(b.Value)));return (float)(2*Math.Acos(Math.Min(1,dot))*180/Math.PI); }
        public static Quaternion RotateTowards(Quaternion a,Quaternion b,float degrees)
        { float angle=Angle(a,b);return angle<1e-6f ? b : Slerp(a,b,Math.Min(1,degrees/angle)); }
        public static Quaternion LookRotation(Vector3 forward,Vector3 up)
        {
            var z=forward.normalized;var x=Vector3.Cross(up,z).normalized;var y=Vector3.Cross(z,x);
            return Wrap(N.Quaternion.CreateFromRotationMatrix(new N.Matrix4x4(x.x,x.y,x.z,0,y.x,y.y,y.z,0,z.x,z.y,z.z,0,0,0,0,1)));
        }
    }
    public static class Mathf
    {
        public static float Clamp(float x,float a,float b) => Math.Max(a,Math.Min(b,x));
        public static int Clamp(int x,int a,int b) => Math.Max(a,Math.Min(b,x));
        public static float Clamp01(float x) => Clamp(x,0,1);
        public static float Abs(float x) => Math.Abs(x);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Min(float a,float b) => Math.Min(a,b);
        public static float Sqrt(float x) => (float)Math.Sqrt(x);
        public static int RoundToInt(float x) => (int)Math.Round(x);
        public static float Exp(float x) => (float)Math.Exp(x);
        public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
        public static float SmoothStep(float a,float b,float t) { t=Clamp01(t);return a+(b-a)*t*t*(3-2*t); }
    }
}
