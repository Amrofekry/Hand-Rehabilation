using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using GripRehab.Product;

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
                var suite = new RehabExerciseSuite(canvas.transform, null, (module, task) => { }, null) { FrameDelta = () => delta, TutorialsEnabled = false };
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

                // Stage 3.2: complete Sequence Memory with camera gestures only.
                var memoryTask = (SequenceMemoryTask)typeof(RehabExerciseSuite).GetField("memory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                for (int i = 0; i < 40; i++) suite.Tick();
                var shown = HandGesture.None; suite.GestureSource = () => shown;
                int scored = -1; float completedSeconds = -1;
                typeof(RehabExerciseSuite).GetField("completed", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(suite, (Action<int, int, int, float>)((m, t, score, seconds) => { scored = score; completedSeconds = seconds; }));
                foreach (var token in memoryTask.Sequence)
                {
                    shown = (HandGesture)token; for (int i = 0; i < 6; i++) suite.Tick();
                    shown = HandGesture.None; for (int i = 0; i < 3; i++) suite.Tick();
                }
                Require(memoryTask.Complete && scored == -1, "the finished board stays visible before results");
                for (int i = 0; i < 16; i++) suite.Tick();
                Require(memoryTask.Complete && scored == 100, "camera gestures complete the pattern (score " + scored + ")");
                Require(Has(suite, "memory.accuracy", 100) && Has(suite, "memory.length", memoryTask.Length), "memory outcomes recorded");
                suite.GestureSource = null;

                // Stage 3.3: a generated level-5 maze walked along its shortest route.
                suite.LevelSource = (m, t) => 5; scored = -1;
                suite.Open(3, 1);
                var maze = (MazeTask)typeof(RehabExerciseSuite).GetField("maze", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                Require(maze.Columns == ExerciseTuning.MazeCells(5) && suite.Level == 5, "level 5 maze is generated (" + maze.Columns + ")");
                var route = maze.Solve();
                for (int i = 1; i < route.Count; i++) suite.MoveMaze(route[i].x - route[i - 1].x, route[i].y - route[i - 1].y);
                for (int i = 0; i < 16; i++) suite.Tick();
                Require(maze.Complete && scored == 100 && Has(suite, "maze.efficiency", 100), "shortest route scores 100 with full efficiency");

                // Stage 3.2: assembly by tap-piece-then-outline.
                suite.LevelSource = null; scored = -1;
                suite.Open(2, 1);
                var board = (AssemblyBoard)typeof(RehabExerciseSuite).GetField("assembly", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                var tapPiece = typeof(RehabExerciseSuite).GetMethod("TapPiece", BindingFlags.NonPublic | BindingFlags.Instance);
                var tapSlot = typeof(RehabExerciseSuite).GetMethod("TapSlot", BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var piece in board.Pieces) { tapPiece.Invoke(suite, new object[] { piece.Id }); tapSlot.Invoke(suite, new object[] { board.SlotOf(piece.Id) }); }
                for (int i = 0; i < 16; i++) suite.Tick();
                Require(board.Complete && scored == 100 && Has(suite, "assembly.pieces", board.Pieces.Count), "assembly completes by tapping (score " + scored + ")");
                Debug.Log("[REHAB_EXERCISE] PASS: memory reveal survives hitches/focus loss, blocks early answers and hides on time; camera gestures, generated maze and tap assembly complete with outcomes");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        static bool Has(RehabExerciseSuite suite, string key, float value)
        {
            foreach (var o in suite.LastOutcomes) if (o.key == key && Mathf.Abs(o.value - value) < .01f) return true;
            return false;
        }

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new Exception("Exercise display: " + reason);
        }
    }
}
