using System;
using System.Collections.Generic;
using GripRehab.Product;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GripRehab
{
    // Playable research exercises for Modules 02 and 03. Their scores describe
    // game performance only; they are not clinical or measured-force outcomes.
    // Stage 3: gesture avatar memory, drag-and-drop assembly, generated mazes and
    // guided tracing, each with a 1..10 level (level 1 = the original exercises).
    public sealed class RehabExerciseSuite
    {
        public enum Exercise { Memory, Assembly, Tracing, Maze }
        static readonly string[] GestureNames = { "OPEN", "FIST", "POINT", "PINCH" };

        readonly Action back;
        readonly Action<int, int> started;
        readonly Action<int, int, int, float> completed;
        readonly CanvasGroup group;
        readonly Text title, instruction, cue, metrics;
        readonly Button[] choices = new Button[4];
        readonly Button[] tabs = new Button[2];
        readonly GameObject choiceArea, traceArea, mazeArea, assemblyArea, sequenceStrip;
        readonly RectTransform traceRect, mazeBoard, assemblyRect;
        readonly Text mazeMap;
        readonly Font font;
        readonly List<RectTransform> stripCells = new List<RectTransform>();
        readonly List<GameObject> transient = new List<GameObject>();
        readonly List<Image> traceMarkers = new List<Image>();
        readonly List<Text> traceNumbers = new List<Text>();
        readonly List<Image> trailDots = new List<Image>();
        readonly List<Image> mazeCells = new List<Image>();
        readonly List<RectTransform> pieceViews = new List<RectTransform>();
        readonly List<Image> stripTiles = new List<Image>();
        readonly List<GameObject> stripGlyphs = new List<GameObject>();
        readonly GestureStabilizer gestures = new GestureStabilizer();
        SequenceMemoryTask memory;
        AssemblyBoard assembly;
        TraceTask tracing;
        MazeTask maze;
        Exercise current;
        // Counted only in visible, focused frames so hitches or another window cannot
        // consume the reveal before the player sees it (audit P-012).
        float revealRemaining, revealTotal;
        const float RevealPerItem = .9f, MinReveal = 3.5f, MaxRevealStep = .1f;
        float startedAt, lastTraceAt, revealEndedAt = -1, lastAnswerAt = -1, firstAnswerSeconds = -1, intervalSum, collisionFlash;
        int answers, correctAnswers, intervals, selectedPiece = -1, trailNext;
        bool recorded;
        RectTransform mazeAvatar;
        HandGesture seenGesture = HandGesture.None;

        static readonly Color Deep = Html("1A2230"), Raised = Html("2B415C");
        static readonly Color White = Html("FFFFFF"), Mint = Html("B9F7FF");
        static readonly Color Jade = Html("00F0FF"), Muted = Html("C2D2E6");
        static readonly Color Green = Html("37D67A"), Red = Html("FF5A6A"), Wall = Html("101722");
        static readonly Color[] PieceColors = { Html("00C2D1"), Html("F2B134"), Html("B07CF2"), Html("37D67A"), Html("FF7A59"), Html("5C9DFF") };
        readonly GripRehab.UI.GripUiThemeController presentation;

        public CanvasGroup Group => group;
        public Func<float> FrameDelta { get; set; } = () => Time.unscaledDeltaTime;
        /// <summary>Optional camera gesture for Sequence Memory (None when no tracked hand).</summary>
        public Func<HandGesture> GestureSource { get; set; }
        /// <summary>Level for a module/task, normally the player's progression; defaults to 1.</summary>
        public Func<int, int, int> LevelSource { get; set; }
        public int Level { get; private set; } = 1;
        public List<OutcomeMetric> LastOutcomes { get; private set; } = new List<OutcomeMetric>();
        bool Revealing => current == Exercise.Memory && revealRemaining > 0;
        public bool SaveEnabled { get; set; } = true;
        public Exercise Current => current;
        public bool Complete => current == Exercise.Memory ? memory.Complete :
            current == Exercise.Assembly ? assembly.Complete :
            current == Exercise.Tracing ? tracing.Complete : maze.Complete;

        public RehabExerciseSuite(Transform parent, Action onBack, Action<int, int> onStarted,
            Action<int, int, int, float> onCompleted)
        {
            back = onBack;
            started = onStarted;
            completed = onCompleted;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var page = Rect("Exercise page", parent, Vector2.zero, new Vector2(1450, 790));
            group = page.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.interactable = group.blocksRaycasts = false;
            var card = Rect("Exercise card", page, Vector2.zero, new Vector2(1320, 690));
            card.gameObject.AddComponent<Image>().color = Deep;
            title = Label("Title", card, new Vector2(0, 276), new Vector2(1190, 55), 35, White);
            instruction = Label("Instruction", card, new Vector2(0, 218), new Vector2(1190, 72), 19, Muted);
            cue = Label("Task cue", card, new Vector2(0, 118), new Vector2(1100, 40), 24, Mint);
            metrics = Label("Performance", card, new Vector2(0, -222), new Vector2(1180, 50), 18, White);
            Button("Back to modules", card, new Vector2(-520, -292), new Vector2(190, 52),
                "MODULES", () => back?.Invoke());
            Button("Restart exercise", card, new Vector2(515, -292), new Vector2(190, 52),
                "RESTART", Restart);
            tabs[0] = Button("First task", card, new Vector2(-190, 164), new Vector2(360, 50),
                "TASK A", () => SelectTask(0));
            tabs[1] = Button("Second task", card, new Vector2(190, 164), new Vector2(360, 50),
                "TASK B", () => SelectTask(1));

            // Sequence Memory: avatar strip shows the pattern; gesture buttons answer it.
            sequenceStrip = Rect("Gesture sequence", card, new Vector2(0, 40), new Vector2(1100, 110)).gameObject;
            for (int i = 0; i < 8; i++)
                stripCells.Add(Rect("Step " + (i + 1), sequenceStrip.transform, new Vector2(-472 + i * 135, 0), new Vector2(118, 104)));
            choiceArea = Rect("Four choices", card, new Vector2(0, -110), new Vector2(1090, 136)).gameObject;
            for (int i = 0; i < 4; i++)
            {
                int token = i;
                choices[i] = Button("Choice " + (i + 1), choiceArea.transform,
                    new Vector2(-399 + i * 266, 0), new Vector2(235, 120),
                    "", () => Submit(token));
                var glyph = GestureGlyph(choices[i].transform, (HandGesture)i, new Vector2(-62, 0), 70, White);
                glyph.GetComponent<Image>().raycastTarget = false;
                Label("Caption", choices[i].transform, new Vector2(40, 0), new Vector2(130, 100), 18, White).text = (i + 1) + "  " + GestureNames[i];
            }

            assemblyArea = Rect("Assembly board", card, new Vector2(0, -38), new Vector2(1040, 300)).gameObject;
            assemblyArea.AddComponent<Image>().color = Raised;
            assemblyRect = (RectTransform)assemblyArea.transform;

            traceArea = Rect("Trace pad", card, new Vector2(0, -38), new Vector2(1040, 300)).gameObject;
            traceArea.AddComponent<Image>().color = Raised;
            traceRect = (RectTransform)traceArea.transform;
            var traceInput = traceArea.AddComponent<RehabTracePad>();
            traceInput.OnPoint = TracePoint;
            for (int i = 0; i < 160; i++)
            {
                var dot = Rect("Trail", traceArea.transform, Vector2.zero, new Vector2(7, 7)).gameObject.AddComponent<Image>();
                dot.raycastTarget = false; dot.enabled = false; trailDots.Add(dot);
            }

            mazeArea = Rect("Maze controls", card, new Vector2(0, -38), new Vector2(1040, 300)).gameObject;
            mazeArea.AddComponent<Image>().color = Raised;
            mazeBoard = Rect("Maze board", mazeArea.transform, new Vector2(-150, 0), new Vector2(560, 290));
            mazeMap = Label("Maze map", mazeArea.transform, new Vector2(-150, 0), new Vector2(10, 10), 2, White);
            mazeMap.enabled = false; // Kept for older tools; the board is drawn with tiles.
            Button("Maze up", mazeArea.transform, new Vector2(330, 64), new Vector2(110, 56),
                "UP", () => MoveMaze(0, -1));
            Button("Maze left", mazeArea.transform, new Vector2(214, 0), new Vector2(110, 56),
                "LEFT", () => MoveMaze(-1, 0));
            Button("Maze right", mazeArea.transform, new Vector2(446, 0), new Vector2(110, 56),
                "RIGHT", () => MoveMaze(1, 0));
            Button("Maze down", mazeArea.transform, new Vector2(330, -64), new Vector2(110, 56),
                "DOWN", () => MoveMaze(0, 1));
            var canvas = parent.GetComponentInParent<Canvas>();
            presentation = canvas != null ? canvas.GetComponentInChildren<GripRehab.UI.GripUiThemeController>(true) : null;
            if (presentation != null) presentation.RegisterPresentation(page);
        }

        public void Open(int module, int task = 0)
        {
            current = module == 3 ? Exercise.Tracing : Exercise.Memory;
            if (task == 1) current++;
            var cognitive = module == 2;
            tabs[0].GetComponentInChildren<Text>().text = cognitive ? "SEQUENCE MEMORY" : "VISUAL TRACING";
            tabs[1].GetComponentInChildren<Text>().text = cognitive ? "SPATIAL ASSEMBLY" : "MAZE NAVIGATION";
            Restart();
        }

        void SelectTask(int task)
        {
            var module = (int)current < 2 ? 2 : 3;
            Open(module, task);
        }

        int Module => (int)current < 2 ? 2 : 3;
        int Task => (int)current % 2;

        public void Restart()
        {
            started?.Invoke(Module, Task);
            Level = ExerciseTuning.Clamp(LevelSource != null ? LevelSource(Module, Task) : 1);
            int seed = Environment.TickCount; // A-015: a fresh pattern every attempt.
            startedAt = Time.unscaledTime;
            recorded = false; LastOutcomes = new List<OutcomeMetric>();
            answers = correctAnswers = intervals = 0; intervalSum = 0; firstAnswerSeconds = revealEndedAt = lastAnswerAt = -1;
            selectedPiece = -1; trailNext = 0; collisionFlash = 0; gestures.Reset(); seenGesture = HandGesture.None;
            memory = new SequenceMemoryTask(seed, ExerciseTuning.MemoryLength(Level));
            assembly = new AssemblyBoard(ExerciseTuning.AssemblyPieces(Level), seed);
            tracing = new TraceTask(TraceTask.PathFor(Level), ExerciseTuning.TraceTolerance(Level));
            int cells = ExerciseTuning.MazeCells(Level);
            maze = cells == 0 ? new MazeTask() : new MazeTask(cells, seed);
            revealTotal = Mathf.Max(MinReveal, RevealPerItem * memory.Length);
            revealRemaining = current == Exercise.Memory ? revealTotal : 0;
            foreach (var item in transient) if (item != null) { item.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item); }
            transient.Clear(); stripTiles.Clear(); stripGlyphs.Clear(); traceMarkers.Clear(); traceNumbers.Clear(); mazeCells.Clear(); pieceViews.Clear(); mazeAvatar = null;
            foreach (var dot in trailDots) dot.enabled = false;
            sequenceStrip.SetActive(current == Exercise.Memory);
            choiceArea.SetActive(current == Exercise.Memory);
            assemblyArea.SetActive(current == Exercise.Assembly);
            traceArea.SetActive(current == Exercise.Tracing);
            mazeArea.SetActive(current == Exercise.Maze);
            if (current == Exercise.Memory) BuildStrip();
            if (current == Exercise.Assembly) BuildAssembly();
            if (current == Exercise.Tracing) BuildTrace();
            if (current == Exercise.Maze) BuildMaze();
            title.text = (current == Exercise.Memory ? "02 / SEQUENCE MEMORY" :
                current == Exercise.Assembly ? "02 / SPATIAL ASSEMBLY" :
                current == Exercise.Tracing ? "03 / VISUAL TRACING" : "03 / MAZE NAVIGATION") + "   ·   LEVEL " + Level;
            instruction.text = current == Exercise.Memory ?
                "Watch the hand show " + memory.Length + " gestures, then repeat them: make each gesture to the camera, or use the buttons or keys 1–4." :
                current == Exercise.Assembly ?
                "Drag each piece onto the outline of the same shape and colour. Or tap a piece, then tap its outline." :
                current == Exercise.Tracing ?
                "Follow the dashed line through waypoints 1–" + tracing.Route.Length + " with your hand pointer or mouse. Green = on the line, red = off." :
                "Guide the marker to the green goal without bumping walls. Use the arrow keys or direction buttons.";
            Refresh();
        }

        public void Tick()
        {
            float dt = Mathf.Clamp(FrameDelta(), 0, MaxRevealStep);
            if (Revealing)
            {
                revealRemaining -= dt;
                if (!Revealing) { revealEndedAt = Time.unscaledTime; Refresh(); }
                else UpdateStripReveal();
            }
            else if (current == Exercise.Memory && !memory.Complete)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) Submit(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Submit(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Submit(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Submit(3);
                var gesture = GestureSource != null ? GestureSource() : HandGesture.None;
                if (gesture != seenGesture) { seenGesture = gesture; Refresh(); }
                if (gestures.Step(gesture, dt, out var accepted)) Submit((int)accepted);
            }
            else if (current == Exercise.Assembly && !assembly.Complete)
            {
                for (int i = 0; i < Mathf.Min(6, assembly.Pieces.Count); i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { selectedPiece = i; Refresh(); }
            }
            else if (current == Exercise.Maze && !maze.Complete)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveMaze(0, -1);
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveMaze(0, 1);
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveMaze(-1, 0);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveMaze(1, 0);
            }
            if (current == Exercise.Maze && mazeAvatar != null)
            {
                var target = MazeCellPosition(maze.X, maze.Y);
                mazeAvatar.anchoredPosition = Vector2.Lerp(mazeAvatar.anchoredPosition, target, Mathf.Clamp01(dt * 18));
                if (collisionFlash > 0) { collisionFlash -= dt; mazeAvatar.GetComponent<Image>().color = collisionFlash > 0 ? Red : Accent; }
            }
        }

        public void Submit(int token)
        {
            if (token < 0 || token > 3 || Complete || current != Exercise.Memory || Revealing) return;
            float now = Time.unscaledTime;
            if (firstAnswerSeconds < 0) firstAnswerSeconds = revealEndedAt >= 0 ? now - revealEndedAt : 0;
            if (lastAnswerAt >= 0) { intervalSum += now - lastAnswerAt; intervals++; }
            lastAnswerAt = now; answers++;
            if (memory.Submit(token)) correctAnswers++;
            Refresh();
            RecordIfComplete();
        }

        public void TracePoint(Vector2 point)
        {
            if (current != Exercise.Tracing || tracing.Complete ||
                Time.unscaledTime - lastTraceAt < .035f) return;
            lastTraceAt = Time.unscaledTime;
            tracing.Submit(point);
            var dot = trailDots[trailNext]; trailNext = (trailNext + 1) % trailDots.Count;
            dot.enabled = true; dot.color = tracing.LastOnPath ? Green : Red;
            dot.rectTransform.anchoredPosition = PadPosition(point);
            Refresh();
            RecordIfComplete();
        }

        public void MoveMaze(int dx, int dy)
        {
            if (current != Exercise.Maze || maze.Complete) return;
            if (!maze.Move(dx, dy)) collisionFlash = .3f;
            Refresh();
            RecordIfComplete();
        }

        void DragPiece(int id, Vector2 point)
        {
            if (current != Exercise.Assembly || assembly.Complete) return;
            assembly.Move(id, point.x, point.y); PlacePieceView(id);
        }

        void DropPiece(int id, Vector2 point)
        {
            if (current != Exercise.Assembly || assembly.Complete) return;
            assembly.Drop(id, point.x, point.y); selectedPiece = -1; PlacePieceView(id);
            Refresh(); RecordIfComplete();
        }

        void TapPiece(int id) { if (current == Exercise.Assembly && !assembly.Pieces[id].Placed) { selectedPiece = id; Refresh(); } }

        void TapSlot(int slot)
        {
            if (current != Exercise.Assembly || selectedPiece < 0 || assembly.Complete) return;
            int id = selectedPiece; assembly.PlaceByTap(id, slot); selectedPiece = -1; PlacePieceView(id);
            Refresh(); RecordIfComplete();
        }

        void RecordIfComplete()
        {
            if (recorded || !Complete) return;
            recorded = true;
            float seconds = Time.unscaledTime - startedAt;
            int score;
            var o = LastOutcomes = new List<OutcomeMetric>();
            switch (current)
            {
                case Exercise.Memory:
                    score = Mathf.Max(0, 100 - memory.Mistakes * 10);
                    o.Add(Metric("memory.length", "Sequence length", "", memory.Length));
                    o.Add(Metric("memory.accuracy", "Answer accuracy", "%", answers == 0 ? 0 : 100f * correctAnswers / answers));
                    o.Add(Metric("memory.reactionSeconds", "Reaction time", "s", Mathf.Max(0, firstAnswerSeconds)));
                    o.Add(Metric("memory.intervalSeconds", "Time between answers", "s", intervals == 0 ? 0 : intervalSum / intervals));
                    o.Add(Metric("memory.retries", "Retries", "", memory.Mistakes));
                    break;
                case Exercise.Assembly:
                    score = Mathf.Max(0, 100 - assembly.Errors * 10);
                    o.Add(Metric("assembly.pieces", "Pieces placed", "", assembly.PlacedCount));
                    o.Add(Metric("assembly.errors", "Wrong outlines", "", assembly.Errors));
                    o.Add(Metric("assembly.placementError", "Placement error", "% board", assembly.MeanPlacementError * 100));
                    o.Add(Metric("assembly.pathEfficiency", "Path efficiency", "%", assembly.PathEfficiency * 100));
                    o.Add(Metric("assembly.speed", "Mean drag speed", "board/s", seconds <= 0 ? 0 : assembly.TotalPath / seconds));
                    break;
                case Exercise.Tracing:
                    score = Mathf.RoundToInt(tracing.Accuracy * 100f);
                    o.Add(Metric("trace.accuracy", "On-line accuracy", "%", tracing.Accuracy * 100));
                    o.Add(Metric("trace.meanDeviation", "Mean deviation", "% pad", tracing.MeanDeviation * 100));
                    o.Add(Metric("trace.waypoints", "Waypoints", "", tracing.Route.Length));
                    break;
                default:
                    score = Mathf.RoundToInt(maze.Score);
                    o.Add(Metric("maze.moves", "Moves", "", maze.Moves));
                    o.Add(Metric("maze.shortest", "Shortest route", "", maze.ShortestPath));
                    o.Add(Metric("maze.efficiency", "Route efficiency", "%", maze.Efficiency * 100));
                    o.Add(Metric("maze.collisions", "Wall contacts", "", maze.Collisions));
                    break;
            }
            o.Add(Metric("level", "Level", "", Level));
            o.Add(Metric("stars", "Stars", "", score >= 85 ? 3 : score >= 65 ? 2 : score > 0 ? 1 : 0));
            completed?.Invoke(Module, Task, score, seconds);
            Refresh();
        }

        static OutcomeMetric Metric(string key, string label, string unit, float value) =>
            new OutcomeMetric { key = key, label = label, unit = unit, value = Mathf.Round(value * 100) / 100 };

        void Refresh()
        {
            switch (current)
            {
                case Exercise.Memory:
                    bool showing = Revealing;
                    cue.text = memory.Complete ? "PATTERN COMPLETE" : showing ?
                        "MEMORISE   " + memory.SequenceDisplay :
                        "ENTER PATTERN   " + memory.Correct + " / " + memory.Length;
                    metrics.text = "Correct steps " + memory.Correct + "/" + memory.Length +
                        "   ·   Retries " + memory.Mistakes + (showing ? "   ·   Pattern hides shortly" :
                        GestureSource != null ? "   ·   Camera sees: " + (seenGesture == HandGesture.None ? "—" : GestureNames[(int)seenGesture]) : "");
                    foreach (var button in choices) button.interactable = !showing && !memory.Complete;
                    if (!showing) UpdateStripAnswers();
                    break;
                case Exercise.Assembly:
                    cue.text = assembly.Complete ? "ASSEMBLY COMPLETE" :
                        selectedPiece >= 0 ? "PIECE " + (selectedPiece + 1) + " SELECTED — TAP ITS OUTLINE" : "MATCH EVERY PIECE TO ITS OUTLINE";
                    metrics.text = "Placed " + assembly.PlacedCount + "/" + assembly.Pieces.Count + "   ·   Wrong outlines " + assembly.Errors;
                    for (int i = 0; i < pieceViews.Count; i++)
                    {
                        var outline = pieceViews[i].GetComponent<Outline>();
                        if (outline != null) outline.enabled = i == selectedPiece;
                    }
                    break;
                case Exercise.Tracing:
                    cue.text = tracing.Complete ? "TRACE COMPLETE" :
                        "FOLLOW THE LINE TO WAYPOINT  " + (tracing.NextWaypoint + 1);
                    metrics.text = "Waypoints " + tracing.NextWaypoint + "/" + tracing.Route.Length + "   ·   Game accuracy " +
                        Mathf.RoundToInt(tracing.Accuracy * 100f) + "%";
                    for (int i = 0; i < traceMarkers.Count; i++)
                    {
                        bool highlighted = i <= tracing.NextWaypoint;
                        traceMarkers[i].color = i < tracing.NextWaypoint ? Green : i == tracing.NextWaypoint ? Accent : Secondary;
                        traceNumbers[i].color = highlighted ? Deep : White;
                    }
                    break;
                case Exercise.Maze:
                    cue.text = maze.Complete ? "MAZE COMPLETE" : "FIND THE GOAL";
                    mazeMap.text = maze.Draw();
                    metrics.text = "Moves " + maze.Moves + " (shortest " + maze.ShortestPath + ")   ·   Wall contacts " + maze.Collisions +
                        "   ·   Game score " + Mathf.RoundToInt(maze.Score);
                    break;
            }
            if (recorded) metrics.text += SaveEnabled ? "   ·   Result saved locally" :
                "   ·   Guest practice: result not saved";
        }

        Color Accent => presentation != null && presentation.Current != null ? presentation.Current.primary : Jade;
        Color Secondary => presentation != null && presentation.Current != null ? presentation.Current.secondary : Muted;

        // ---------- Sequence Memory avatar strip ----------
        void BuildStrip()
        {
            for (int i = 0; i < stripCells.Count; i++)
            {
                stripCells[i].gameObject.SetActive(i < memory.Length);
                if (i >= memory.Length) continue;
                var tile = Rect("Tile", stripCells[i], Vector2.zero, stripCells[i].sizeDelta).gameObject.AddComponent<Image>();
                tile.raycastTarget = false; tile.color = Raised; transient.Add(tile.gameObject); stripTiles.Add(tile);
                var glyph = GestureGlyph(stripCells[i], (HandGesture)memory.Sequence[i], new Vector2(0, 6), 78, White);
                glyph.name = "Avatar gesture"; transient.Add(glyph.gameObject); stripGlyphs.Add(glyph.gameObject);
                var number = Label("Order", stripCells[i], new Vector2(0, -42), new Vector2(110, 22), 15, Muted);
                number.text = (i + 1).ToString(); transient.Add(number.gameObject);
            }
            UpdateStripReveal();
        }

        void UpdateStripReveal()
        {
            // The avatar demonstrates one gesture at a time, then the whole pattern stays visible briefly.
            float shown = revealTotal - revealRemaining;
            int active = Mathf.Min(memory.Length - 1, Mathf.FloorToInt(shown / RevealPerItem));
            bool demo = shown < RevealPerItem * memory.Length;
            for (int i = 0; i < stripTiles.Count; i++)
            {
                stripGlyphs[i].SetActive(!demo || i <= active);
                stripTiles[i].color = demo && i == active ? Accent : Raised;
                stripCells[i].localScale = demo && i == active ? Vector3.one * 1.08f : Vector3.one;
            }
        }

        void UpdateStripAnswers()
        {
            for (int i = 0; i < stripTiles.Count; i++)
            {
                stripGlyphs[i].SetActive(i < memory.Correct || memory.Complete);
                stripTiles[i].color = i < memory.Correct || memory.Complete ? Green : i == memory.Correct ? Accent : Raised;
                stripCells[i].localScale = Vector3.one;
            }
        }

        /// <summary>A stylised hand "avatar": palm plus five fingers posed for the gesture.</summary>
        RectTransform GestureGlyph(Transform parent, HandGesture gesture, Vector2 position, float size, Color color)
        {
            var root = Rect("Hand " + gesture, parent, position, new Vector2(size, size));
            var rootImage = root.gameObject.AddComponent<Image>(); rootImage.color = new Color(0, 0, 0, 0); rootImage.raycastTarget = false;
            float u = size / 100f;
            Bar(root, "Palm", new Vector2(0, -22) * u, new Vector2(46, 40) * u, 0, color);
            float[] x = { -15, -5, 5, 15 };
            for (int f = 0; f < 4; f++)
            {
                bool longFinger = gesture == HandGesture.Open || (gesture == HandGesture.Point && f == 0) || (gesture == HandGesture.Pinch && f > 0);
                float length = longFinger ? 40 : 12;
                float angle = gesture == HandGesture.Pinch && f == 0 ? 35 : 0;
                Bar(root, "Finger " + f, new Vector2(x[f], 2 + length / 2) * u, new Vector2(8, length) * u, angle, color);
            }
            bool thumbOut = gesture == HandGesture.Open || gesture == HandGesture.Point;
            if (gesture == HandGesture.Pinch) Bar(root, "Thumb", new Vector2(-24, 6) * u, new Vector2(8, 26) * u, -40, color);
            else if (thumbOut) Bar(root, "Thumb", new Vector2(-30, -10) * u, new Vector2(8, 24) * u, 50, color);
            else Bar(root, "Thumb", new Vector2(-6, -8) * u, new Vector2(26, 8) * u, 0, color);
            return root;
        }

        void Bar(RectTransform parent, string name, Vector2 position, Vector2 size, float angle, Color color)
        {
            var rect = Rect(name, parent, position, size); rect.localRotation = Quaternion.Euler(0, 0, angle);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        }

        // ---------- Spatial Assembly ----------
        static readonly Vector2[] PieceSizes = { new Vector2(64, 64), new Vector2(42, 86), new Vector2(86, 42), new Vector2(58, 58) };

        void BuildAssembly()
        {
            var divider = Rect("Divider", assemblyRect, new Vector2(-20, 0), new Vector2(4, 280)).gameObject.AddComponent<Image>();
            divider.color = Deep; divider.raycastTarget = false; transient.Add(divider.gameObject);
            foreach (var piece in assembly.Pieces)
            {
                var slot = Rect("Outline " + (piece.Id + 1), assemblyRect, BoardPosition(assemblyRect, piece.TargetX, piece.TargetY), PieceSizes[piece.Shape] + new Vector2(10, 10));
                if (piece.Shape == 3) slot.localRotation = Quaternion.Euler(0, 0, 45);
                var fill = slot.gameObject.AddComponent<Image>(); var c = PieceColors[piece.Id % PieceColors.Length]; fill.color = new Color(c.r, c.g, c.b, .18f);
                var border = slot.gameObject.AddComponent<Outline>(); border.effectColor = c; border.effectDistance = new Vector2(3, -3);
                int slotIndex = assembly.SlotOf(piece.Id);
                slot.gameObject.AddComponent<RehabBoardTarget>().OnTap = () => TapSlot(slotIndex);
                transient.Add(slot.gameObject);
            }
            foreach (var piece in assembly.Pieces)
            {
                var view = Rect("Piece " + (piece.Id + 1), assemblyRect, BoardPosition(assemblyRect, piece.X, piece.Y), PieceSizes[piece.Shape]);
                if (piece.Shape == 3) view.localRotation = Quaternion.Euler(0, 0, 45);
                view.gameObject.AddComponent<Image>().color = PieceColors[piece.Id % PieceColors.Length];
                var selected = view.gameObject.AddComponent<Outline>(); selected.effectColor = White; selected.effectDistance = new Vector2(4, -4); selected.enabled = false;
                var label = Label("Number", view, Vector2.zero, new Vector2(40, 30), 20, Deep); label.text = (piece.Id + 1).ToString();
                if (piece.Shape == 3) label.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
                var drag = view.gameObject.AddComponent<RehabDragPiece>();
                int id = piece.Id; drag.Board = assemblyRect;
                drag.OnMove = p => DragPiece(id, p); drag.OnDrop = p => DropPiece(id, p); drag.OnTap = () => TapPiece(id);
                pieceViews.Add(view); transient.Add(view.gameObject);
            }
        }

        void PlacePieceView(int id)
        {
            var piece = assembly.Pieces[id];
            pieceViews[id].anchoredPosition = BoardPosition(assemblyRect, piece.X, piece.Y);
            if (piece.Placed) { pieceViews[id].GetComponent<RehabDragPiece>().enabled = false; pieceViews[id].SetAsFirstSibling(); }
            else pieceViews[id].SetAsLastSibling();
        }

        static Vector2 BoardPosition(RectTransform board, float x, float y)
        {
            var size = board.rect.size == Vector2.zero ? board.sizeDelta : board.rect.size;
            return new Vector2((x - .5f) * size.x, (y - .5f) * size.y);
        }

        // ---------- Visual Tracing ----------
        Vector2 PadPosition(Vector2 point) => BoardPosition(traceRect, point.x, point.y);

        void BuildTrace()
        {
            var route = tracing.Route;
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = PadPosition(route[i - 1]), b = PadPosition(route[i]);
                float length = Vector2.Distance(a, b); var direction = (b - a) / Mathf.Max(1, length);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                for (float d = 10; d < length - 10; d += 26)
                {
                    var dash = Rect("Guide", traceRect, a + direction * d, new Vector2(14, 4)); dash.localRotation = Quaternion.Euler(0, 0, angle);
                    var image = dash.gameObject.AddComponent<Image>(); image.color = Muted; image.raycastTarget = false;
                    transient.Add(dash.gameObject);
                }
            }
            for (int i = 0; i < route.Length; i++)
            {
                var marker = Rect("Waypoint " + (i + 1), traceRect, PadPosition(route[i]), new Vector2(40, 40));
                var image = marker.gameObject.AddComponent<Image>(); image.raycastTarget = false;
                traceMarkers.Add(image);
                var number = Label("Number", marker, Vector2.zero, new Vector2(36, 36), 18, Deep); number.text = (i + 1).ToString();
                traceNumbers.Add(number); transient.Add(marker.gameObject);
            }
            foreach (var dot in trailDots) dot.transform.SetAsLastSibling();
        }

        // ---------- Maze ----------
        float MazeCellSize => Mathf.Min(mazeBoard.sizeDelta.x / maze.Columns, mazeBoard.sizeDelta.y / maze.Rows);
        Vector2 MazeCellPosition(int x, int y)
        {
            float cell = MazeCellSize;
            return new Vector2((x - (maze.Columns - 1) / 2f) * cell, ((maze.Rows - 1) / 2f - y) * cell);
        }

        void BuildMaze()
        {
            float cell = MazeCellSize;
            for (int y = 0; y < maze.Rows; y++)
                for (int x = 0; x < maze.Columns; x++)
                {
                    var tile = Rect("Cell", mazeBoard, MazeCellPosition(x, y), Vector2.one * (cell - 2)).gameObject.AddComponent<Image>();
                    tile.raycastTarget = false;
                    tile.color = maze.IsWall(x, y) ? Wall : x == maze.Columns - 1 && y == maze.Rows - 1 ? Green : Deep;
                    mazeCells.Add(tile); transient.Add(tile.gameObject);
                }
            mazeAvatar = Rect("Avatar", mazeBoard, MazeCellPosition(maze.X, maze.Y), Vector2.one * (cell * .7f));
            mazeAvatar.gameObject.AddComponent<Image>().color = Accent; mazeAvatar.GetComponent<Image>().raycastTarget = false;
            transient.Add(mazeAvatar.gameObject);
        }

        // ---------- Shared UI helpers ----------
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        Text Label(string name, Transform parent, Vector2 position, Vector2 size,
            int fontSize, Color color)
        {
            var rect = Rect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Button Button(string name, Transform parent, Vector2 position, Vector2 size,
            string caption, UnityEngine.Events.UnityAction click)
        {
            var rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = Raised;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(click);
            if (!string.IsNullOrEmpty(caption))
                Label("Caption", rect, Vector2.zero, size - new Vector2(12, 8), 17, White).text = caption;
            var colors = button.colors;
            colors.highlightedColor = Mint; colors.pressedColor = Jade;
            colors.disabledColor = new Color(.45f, .56f, .56f, .7f);
            button.colors = colors;
            return button;
        }

        static Color Html(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var result);
            return result;
        }
    }

    public sealed class RehabTracePad : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<Vector2> OnPoint;
        public void OnPointerDown(PointerEventData eventData) => Submit(eventData);
        public void OnDrag(PointerEventData eventData) => Submit(eventData);
        public void SubmitScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, screenPoint, eventCamera, out var point)) return;
            var bounds = rect.rect;
            OnPoint?.Invoke(new Vector2(Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y)));
        }
        void Submit(PointerEventData eventData)
        {
            SubmitScreenPoint(eventData.position, eventData.pressEventCamera);
        }
    }

    /// <summary>Stage 3.2: a draggable assembly piece reporting normalized board coordinates.</summary>
    public sealed class RehabDragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public RectTransform Board;
        public Action<Vector2> OnMove, OnDrop;
        public Action OnTap;
        bool dragging;
        public void OnBeginDrag(PointerEventData eventData) { dragging = true; Report(eventData, OnMove); }
        public void OnDrag(PointerEventData eventData) => Report(eventData, OnMove);
        public void OnEndDrag(PointerEventData eventData) { dragging = false; Report(eventData, OnDrop); }
        public void OnPointerClick(PointerEventData eventData) { if (!dragging && !eventData.dragging) OnTap?.Invoke(); }
        void Report(PointerEventData eventData, Action<Vector2> target)
        {
            if (!enabled || Board == null || target == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Board, eventData.position, eventData.pressEventCamera, out var point)) return;
            var bounds = Board.rect;
            target(new Vector2(Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x), Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y)));
        }
    }

    /// <summary>A tappable assembly outline (accessible tap-piece-then-slot placement).</summary>
    public sealed class RehabBoardTarget : MonoBehaviour, IPointerClickHandler
    {
        public Action OnTap;
        public void OnPointerClick(PointerEventData eventData) => OnTap?.Invoke();
    }
}
