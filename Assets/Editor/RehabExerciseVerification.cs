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

                // Hand-pointer assembly: 5 s dwell picks a piece up, it follows the hand, a 2 s hold places it.
                scored = -1;
                suite.Open(2, 1);
                board = (AssemblyBoard)typeof(RehabExerciseSuite).GetField("assembly", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                Vector2 Local(float x, float y) => new Vector2((x - .5f) * 1040, (y - .5f) * 276);
                var first = board.Pieces[0]; var second = board.Pieces[1];
                for (int i = 0; i < 48; i++) suite.AssemblyHand(Local(first.X, first.Y), true, .1f, 5);
                Require(!suite.HandCarrying, "a piece is not picked up before the five-second dwell");
                for (int i = 0; i < 4; i++) suite.AssemblyHand(Local(first.X, first.Y), true, .1f, 5);
                Require(suite.HandCarrying, "holding the hand on a piece for five seconds picks it up");
                suite.AssemblyHand(Local(.45f, .5f), true, .1f, 5);
                Require(Mathf.Abs(first.X - .45f) < .01f && !first.Placed, "the carried piece follows the hand");
                for (int i = 0; i < 25 && suite.HandCarrying; i++) suite.AssemblyHand(Local(second.TargetX, second.TargetY), true, .1f, 5);
                Require(!first.Placed && board.Errors == 1 && Mathf.Abs(first.X - first.StartX) < .001f, "holding a piece on the wrong outline counts one error and returns it");
                for (int i = 0; i < 52; i++) suite.AssemblyHand(Local(first.X, first.Y), true, .1f, 5);
                Require(suite.HandCarrying, "the returned piece can be picked up again");
                for (int i = 0; i < 20; i++) suite.Tick();
                Require(!suite.HandCarrying && board.Errors == 1 && Mathf.Abs(first.X - first.StartX) < .001f, "losing the hand puts the piece back without an error");
                foreach (var piece in board.Pieces)
                {
                    for (int i = 0; i < 52 && !suite.HandCarrying; i++) suite.AssemblyHand(Local(piece.X, piece.Y), true, .1f, 5);
                    for (int i = 0; i < 25 && suite.HandCarrying; i++) suite.AssemblyHand(Local(piece.TargetX, piece.TargetY), true, .1f, 5);
                    Require(piece.Placed, "piece " + (piece.Id + 1) + " is placed by hand");
                }
                for (int i = 0; i < 16; i++) suite.Tick();
                Require(board.Complete && scored == 90, "assembly completes by hand with the one wrong outline counted (score " + scored + ")");

                // Hand-pointer maze: touch the marker, then it follows the hand cell by cell.
                suite.LevelSource = (m, t) => 5; scored = -1;
                suite.Open(3, 1);
                maze = (MazeTask)typeof(RehabExerciseSuite).GetField("maze", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
                float cell = Mathf.Min(560f / maze.Columns, 266f / maze.Rows);
                Vector2 Cell(int x, int y) => new Vector2(-150 + (x - (maze.Columns - 1) / 2f) * cell, ((maze.Rows - 1) / 2f - y) * cell);
                for (int i = 0; i < 10; i++) suite.MazeHand(Cell(maze.Columns - 1, maze.Rows - 1), true, .1f, 5);
                Require(maze.X == 0 && maze.Y == 0 && !suite.MazeEngaged, "the marker ignores the hand until the hand touches it");
                suite.MazeHand(Cell(0, 0), true, .1f, 5);
                Require(suite.MazeEngaged, "touching the marker takes hold of it");
                int movesBefore = maze.Moves;
                var wall = maze.IsWall(1, 0) ? new Vector2Int(1, 0) : maze.IsWall(0, 1) ? new Vector2Int(0, 1) : new Vector2Int(-1, -1);
                if (wall.x >= 0)
                {
                    for (int i = 0; i < 10; i++) suite.MazeHand(Cell(wall.x, wall.y), true, .1f, 5);
                    Require(maze.Collisions == 1 && maze.X == 0 && maze.Y == 0, "pushing the hand into a wall counts one contact, not one per frame (" + maze.Collisions + ")");
                    suite.MazeHand(Cell(0, 0), true, .1f, 5);
                }
                Require(maze.Moves == movesBefore, "taking hold of the marker never moves it");
                for (int i = 0; i < 20; i++) suite.Tick();
                Require(!suite.MazeEngaged, "losing the hand lets go of the marker");
                route = maze.Solve();
                suite.MazeHand(Cell(0, 0), true, .1f, 5);
                foreach (var step in route) for (int i = 0; i < 3; i++) suite.MazeHand(Cell(step.x, step.y), true, .1f, 5);
                for (int i = 0; i < 16; i++) suite.Tick();
                Require(maze.Complete && maze.Moves == maze.ShortestPath && maze.Collisions == (wall.x >= 0 ? 1 : 0) && scored == Mathf.RoundToInt(maze.Score),
                    "following the corridors by hand reaches the goal on the shortest route (score " + scored + ", moves " + maze.Moves + ", contacts " + maze.Collisions + ")");
                Debug.Log("[REHAB_EXERCISE] PASS: memory reveal survives hitches/focus loss, blocks early answers and hides on time; camera gestures, generated maze and tap assembly complete with outcomes; assembly and maze play fully by hand pointer");
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
