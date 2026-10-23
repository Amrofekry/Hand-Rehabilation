using System;
namespace GripRehab.Product
{
    /// <summary>Same causal feature schema as fatigue_ai.data.feature_sequence, 10 Hz, 5 s.</summary>
    public sealed class TemporalForceWindow
    {
        readonly float[] ratios = new float[10], rows = new float[400];
        int n, firstCount; float earlySum; double last = -1e30;
        public int Count => n;
        public bool Add(double now, float newtons, float mvc)
        {
            if(!TrialSample.Finite(now)||!TrialSample.Finite(newtons)||!TrialSample.Finite(mvc)||newtons<0||mvc<=0||now<=last)return false;
            if(n>0 && now-last>=.5)Clear();
            last=now;float ratio=newtons/mvc;ratios[n%10]=ratio;
            if(firstCount<10){earlySum+=ratio;firstCount++;}
            int count=Math.Min(10,n+1);float sum=0,peak=0;
            for(int i=0;i<count;i++){sum+=ratios[i];peak=Math.Max(peak,ratios[i]);}
            float mean=sum/count;double variance=0,slope=0;
            for(int i=0;i<count;i++){
                float value=ratios[(Math.Max(0,n-count+1)+i)%10];
                variance+=(value-mean)*(value-mean);slope+=(i-(count-1)/2.0)*(value-mean);
            }
            float std=(float)Math.Sqrt(variance/count);
            if(count>1)slope=slope/(count*(count*count-1)/12.0)*10;
            else slope=0;
            if(n>=50)Array.Copy(rows,8,rows,0,392);
            int offset=Math.Min(n,49)*8;float early=Math.Max(1e-6f,earlySum/firstCount);
            rows[offset]=ratio;rows[offset+1]=mean;rows[offset+2]=(float)slope;rows[offset+3]=std;
            rows[offset+4]=std/Math.Max(mean,1e-6f);rows[offset+5]=Math.Max(0,1-peak/early);
            rows[offset+6]=sum/10;rows[offset+7]=Math.Max(0,1-mean/early);n++;return true;
        }
        public bool CopyTo(float[] destination,double now)
        {if(n<50||now<last||now-last>=.5||destination==null||destination.Length!=400)return false;Array.Copy(rows,destination,400);return true;}
        public void Clear(){n=firstCount=0;earlySum=0;last=-1e30;Array.Clear(ratios,0,10);Array.Clear(rows,0,400);}
    }
}
