using System;
using UnityEngine;
#if GRIPREHAB_SENTIS
using Unity.Sentis;
#endif
namespace GripRehab
{
    /// <summary>Explicit prototype inference; EMG weights must NEVER receive measured Newtons.</summary>
    public sealed class ResearchAiRuntime : IDisposable
    {
        [Serializable] public class Scaler { public string version, provenance; public float[] mean, std; public string[] features; public int[] input_shape; }
        readonly Scaler scaler;
#if GRIPREHAB_SENTIS
        Worker worker;
#endif
        public bool Available { get; private set; }
        public string Status { get; private set; } = "ONNX model unavailable";
        public const string ModelResource = "ResearchAi/PROTOTYPE_PROXY_cnn";
        public ResearchAiRuntime() : this(ModelResource) { }
        /// <summary>modelResource: a Resources path. A missing model leaves Available false with a readable Status; the rule-based fatigue limits never depend on it.</summary>
        public ResearchAiRuntime(string modelResource)
        {
            try {
                var text = Resources.Load<TextAsset>("ResearchAi/scaler");
                if (text == null) return;
                scaler = JsonUtility.FromJson<Scaler>(text.text);
                if (scaler.mean == null || scaler.std == null || scaler.mean.Length != 8 || scaler.std.Length != 8) throw new ArgumentException("Scaler dimensions");
                for (int i=0;i<8;i++) if ((float.IsNaN(scaler.mean[i]) || float.IsInfinity(scaler.mean[i])) || (float.IsNaN(scaler.std[i]) || float.IsInfinity(scaler.std[i])) || scaler.std[i] <= 0) throw new ArgumentException("Invalid scaler");
#if GRIPREHAB_SENTIS
                var asset = Resources.Load<ModelAsset>(modelResource);
                if (asset == null) { Status = "Fatigue model file missing; rule-based safety limits still apply"; return; }
                worker = new Worker(ModelLoader.Load(asset), BackendType.CPU); Available = true;
                Status = "PROTOTYPE-PROXY / public EMG; device use blocked";
#else
                Status = "Sentis package is not enabled";
#endif
            } catch (Exception error) { Status = error.Message; Dispose(); }
        }
        public float PredictNormalizedFixture(float[] normalized)
        {
            if (!Available || normalized == null || normalized.Length != 400) throw new InvalidOperationException(Status);
            foreach (var f in normalized) if (float.IsNaN(f) || float.IsInfinity(f)) throw new ArgumentException("Non-finite feature");
#if GRIPREHAB_SENTIS
            using (var input = new Tensor<float>(new TensorShape(1,50,8), normalized))
            {
                worker.Schedule(input);
                var output = worker.PeekOutput() as Tensor<float>;
                if (output == null) throw new InvalidOperationException("Invalid Sentis output");
                using (var cpu = output.ReadbackAndClone())
                {
                    float p = cpu[0];
                    if ((float.IsNaN(p) || float.IsInfinity(p)) || p < 0 || p > 1) throw new InvalidOperationException("Invalid fatigue probability");
                    return p;
                }
            }
#else
            throw new InvalidOperationException(Status);
#endif
        }
        public bool TryPredict(float[] raw, bool synthetic, bool enableSyntheticProxy, out float probability)
        {
            probability = 0;
            // This guard is independent of UI options. No public EMG -> Newton domain substitution.
            if (!synthetic || !enableSyntheticProxy || !Available || raw == null || raw.Length != 400) return false;
            var normalized = new float[400];
            for (int i=0;i<400;i++) normalized[i]=(raw[i]-scaler.mean[i%8])/scaler.std[i%8];
            try { probability=PredictNormalizedFixture(normalized); return true; }
            catch (Exception error) { Status="Inference unavailable: "+error.Message; return false; }
        }
        public void Dispose()
        {
#if GRIPREHAB_SENTIS
            worker?.Dispose(); worker=null;
#endif
            Available=false;
        }
    }
}
