using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.Editor
{
    // Module 2/3 exercise display checks (audit P-012). Uses a fabricated frame clock;
    // no scene, patient record or camera is opened.
    public static class RehabExerciseVerification
    {
        [MenuItem("Grip Rehab/Verify Exercise Display")]
        public static void Run()
        {
            var canvas = new GameObject("Exercise verification canvas", typeof(Canvas));
            try
            {
                float delta = .1f;
                var suite = new RehabExerciseSuite(canvas.transform, null, (module, task) => { }, null) { FrameDelta = () => delta };
                var cue = (Text)typeof(RehabExerciseSuite).GetField("cue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                suite.Open(2, 0);
                Require(cue.text.StartsWith("MEMORISE"), "pattern is shown when Sequence Memory opens: '" + cue.text + "'");
                delta = 30;  // A long hitch or a return from another window.
                suite.Tick();
                Require(cue.text.StartsWith("MEMORISE"), "a hitch or focus return cannot consume the reveal");
                var task = (SequenceMemoryTask)typeof(RehabExerciseSuite).GetField("memory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                suite.Submit(task.Sequence[0]);
                Require(task.Correct == 0 && task.Mistakes == 0, "answers are ignored while the pattern is shown");
                delta = .1f;
                for (int i = 0; i < 40; i++) suite.Tick();
                Require(cue.text.StartsWith("ENTER PATTERN"), "pattern hides after about 3.5 visible seconds");
                suite.Restart();
                Require(cue.text.StartsWith("MEMORISE"), "restart shows a fresh pattern");
                Debug.Log("[REHAB_EXERCISE] PASS: memory reveal survives hitches/focus loss, blocks early answers and hides on time");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new Exception("Exercise display: " + reason);
        }
    }
}
