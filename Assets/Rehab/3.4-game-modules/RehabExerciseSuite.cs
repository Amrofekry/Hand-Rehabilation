using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GripRehab
{
    // Playable research exercises for Modules 02 and 03. Their scores describe
    // game performance only; they are not clinical or measured-force outcomes.
    public sealed class RehabExerciseSuite
    {
        public enum Exercise { Memory, Assembly, Tracing, Maze }

        readonly Action back;
        readonly Action<int, int> started;
        readonly Action<int, int, int, float> completed;
        readonly CanvasGroup group;
        readonly Text title, instruction, cue, metrics;
        readonly Button[] choices = new Button[4];
        readonly Button[] tabs = new Button[2];
        readonly GameObject choiceArea, traceArea, mazeArea;
        readonly Image[] traceMarkers = new Image[5];
        readonly Text[] traceNumbers = new Text[5];
        readonly Text mazeMap;
        readonly Font font;
        SequenceMemoryTask memory;
        SpatialAssemblyTask assembly;
        TraceTask tracing;
        MazeTask maze;
        Exercise current;
        float revealUntil;
        bool memoryRevealVisible;
        float startedAt;
        float lastTraceAt;
        bool recorded;

        static readonly Color Deep = Html("1A2230"), Raised = Html("2B415C");
        static readonly Color White = Html("FFFFFF"), Mint = Html("B9F7FF");
        static readonly Color Jade = Html("00F0FF"), Muted = Html("C2D2E6");
        readonly GripRehab.UI.GripUiThemeController presentation;

        public CanvasGroup Group => group;
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
            cue = Label("Task cue", card, new Vector2(0, 95), new Vector2(1100, 115), 35, Mint);
            metrics = Label("Performance", card, new Vector2(0, -220), new Vector2(1100, 55), 19, White);
            Button("Back to modules", card, new Vector2(-520, -292), new Vector2(190, 52),
                "MODULES", () => back?.Invoke());
            Button("Restart exercise", card, new Vector2(515, -292), new Vector2(190, 52),
                "RESTART", Restart);
            tabs[0] = Button("First task", card, new Vector2(-190, 164), new Vector2(360, 50),
                "TASK A", () => SelectTask(0));
            tabs[1] = Button("Second task", card, new Vector2(190, 164), new Vector2(360, 50),
                "TASK B", () => SelectTask(1));

            choiceArea = Rect("Four choices", card, new Vector2(0, -53), new Vector2(1090, 136)).gameObject;
            for (int i = 0; i < 4; i++)
            {
                int token = i;
                choices[i] = Button("Choice " + (i + 1), choiceArea.transform,
                    new Vector2(-399 + i * 266, 0), new Vector2(235, 85),
                    (i + 1).ToString(), () => Submit(token));
            }

            traceArea = Rect("Trace pad", card, new Vector2(0, -54), new Vector2(1040, 188)).gameObject;
            traceArea.AddComponent<Image>().color = Raised;
            var traceInput = traceArea.AddComponent<RehabTracePad>();
            traceInput.OnPoint = TracePoint;
            for (int i = 0; i < traceMarkers.Length; i++)
            {
                var point = TraceTask.Path[i];
                var marker = Rect("Waypoint " + (i + 1), traceArea.transform,
                    new Vector2((point.x - .5f) * 1040, (point.y - .5f) * 188),
                    new Vector2(42, 42));
                traceMarkers[i] = marker.gameObject.AddComponent<Image>();
                traceMarkers[i].color = Muted;
                traceNumbers[i] = Label("Number", marker, Vector2.zero, new Vector2(38, 38), 19, Deep);
                traceNumbers[i].text = (i + 1).ToString();
            }

            mazeArea = Rect("Maze controls", card, new Vector2(0, -54), new Vector2(1040, 188)).gameObject;
            mazeArea.AddComponent<Image>().color = Raised;
            mazeMap = Label("Maze map", mazeArea.transform, new Vector2(-130, 0),
                new Vector2(460, 170), 23, White);
            Button("Maze up", mazeArea.transform, new Vector2(310, 54), new Vector2(110, 48),
                "UP", () => MoveMaze(0, -1));
            Button("Maze left", mazeArea.transform, new Vector2(196, -10), new Vector2(110, 48),
                "LEFT", () => MoveMaze(-1, 0));
            Button("Maze right", mazeArea.transform, new Vector2(424, -10), new Vector2(110, 48),
                "RIGHT", () => MoveMaze(1, 0));
            Button("Maze down", mazeArea.transform, new Vector2(310, -74), new Vector2(110, 48),
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

        public void Restart()
        {
            started?.Invoke((int)current < 2 ? 2 : 3, (int)current % 2);
            startedAt = Time.unscaledTime;
            recorded = false;
            memory = new SequenceMemoryTask(DateTime.UtcNow.DayOfYear + 2048, 4);
            assembly = new SpatialAssemblyTask();
            tracing = new TraceTask();
            maze = new MazeTask();
            revealUntil = Time.unscaledTime + 3.5f;
            memoryRevealVisible = current == Exercise.Memory;
            choiceArea.SetActive(current == Exercise.Memory || current == Exercise.Assembly);
            traceArea.SetActive(current == Exercise.Tracing);
            mazeArea.SetActive(current == Exercise.Maze);
            title.text = current == Exercise.Memory ? "02 / SEQUENCE MEMORY" :
                current == Exercise.Assembly ? "02 / SPATIAL ASSEMBLY" :
                current == Exercise.Tracing ? "03 / VISUAL TRACING" : "03 / MAZE NAVIGATION";
            instruction.text = current == Exercise.Memory ?
                "Memorise the four-number pattern, then enter it with the buttons or keys 1–4." :
                current == Exercise.Assembly ?
                "Place the numbered pieces in the shown order. Use the buttons or keys 1–4." :
                current == Exercise.Tracing ?
                "Trace waypoints 1–5 with your hand pointer or drag with the mouse." :
                "Guide P to G without hitting a wall. Use the arrow keys or direction buttons.";
            Refresh();
        }

        public void Tick()
        {
            if (current == Exercise.Memory && !memory.Complete && Time.unscaledTime >= revealUntil)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) Submit(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Submit(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Submit(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Submit(3);
            }
            else if (current == Exercise.Assembly && !assembly.Complete)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) Submit(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Submit(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Submit(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Submit(3);
            }
            else if (current == Exercise.Maze && !maze.Complete)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) MoveMaze(0, -1);
                if (Input.GetKeyDown(KeyCode.DownArrow)) MoveMaze(0, 1);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveMaze(-1, 0);
                if (Input.GetKeyDown(KeyCode.RightArrow)) MoveMaze(1, 0);
            }
            if (current == Exercise.Memory && memoryRevealVisible &&
                Time.unscaledTime >= revealUntil)
            {
                memoryRevealVisible = false;
                Refresh();
            }
        }

        public void Submit(int token)
        {
            if (token < 0 || token > 3 || Complete) return;
            if (current == Exercise.Memory)
            {
                if (Time.unscaledTime < revealUntil) return;
                memory.Submit(token);
            }
            else if (current == Exercise.Assembly) assembly.Place(token);
            else return;
            Refresh();
            RecordIfComplete();
        }

        public void TracePoint(Vector2 point)
        {
            if (current != Exercise.Tracing || tracing.Complete ||
                Time.unscaledTime - lastTraceAt < .035f) return;
            lastTraceAt = Time.unscaledTime;
            tracing.Submit(point);
            Refresh();
            RecordIfComplete();
        }

        public void MoveMaze(int dx, int dy)
        {
            if (current != Exercise.Maze || maze.Complete) return;
            maze.Move(dx, dy);
            Refresh();
            RecordIfComplete();
        }

        void RecordIfComplete()
        {
            if (recorded || !Complete) return;
            recorded = true;
            int errors = current == Exercise.Memory ? memory.Mistakes :
                current == Exercise.Assembly ? assembly.Errors :
                current == Exercise.Tracing ? tracing.OffPathSamples : maze.Collisions;
            int score = current == Exercise.Tracing ? Mathf.RoundToInt(tracing.Accuracy * 100f) :
                current == Exercise.Maze ? Mathf.RoundToInt(maze.Score) :
                Mathf.Max(0, 100 - errors * 10);
            completed?.Invoke((int)current < 2 ? 2 : 3, (int)current % 2,
                score, Time.unscaledTime - startedAt);
            Refresh();
        }

        void Refresh()
        {
            switch (current)
            {
                case Exercise.Memory:
                    bool showing = Time.unscaledTime < revealUntil;
                    cue.text = memory.Complete ? "PATTERN COMPLETE" : showing ?
                        "MEMORISE   " + memory.SequenceDisplay :
                        "ENTER PATTERN   " + memory.Correct + " / " + memory.Length;
                    metrics.text = "Correct steps " + memory.Correct + "/" + memory.Length +
                        "   ·   Retries " + memory.Mistakes + (showing ? "   ·   Pattern hides shortly" : "");
                    foreach (var button in choices) button.interactable = !showing && !memory.Complete;
                    break;
                case Exercise.Assembly:
                    cue.text = assembly.Complete ? "ASSEMBLY COMPLETE" :
                        "TARGET  " + assembly.TargetDisplay + "\nPLACED  " + assembly.ProgressDisplay;
                    metrics.text = "Placed " + assembly.Placed + "/4   ·   Errors " + assembly.Errors;
                    foreach (var button in choices) button.interactable = !assembly.Complete;
                    break;
                case Exercise.Tracing:
                    cue.text = tracing.Complete ? "TRACE COMPLETE" :
                        "DRAG TO WAYPOINT  " + (tracing.NextWaypoint + 1);
                    metrics.text = "Waypoints " + tracing.NextWaypoint + "/5   ·   Game accuracy " +
                        Mathf.RoundToInt(tracing.Accuracy * 100f) + "%";
                    for (int i = 0; i < traceMarkers.Length; i++)
                    {
                        var palette = presentation != null ? presentation.Current : null;
                        bool highlighted = i <= tracing.NextWaypoint;
                        traceMarkers[i].color = palette != null ?
                            (i < tracing.NextWaypoint ? palette.success : i == tracing.NextWaypoint ? palette.primary : palette.secondary) :
                            (highlighted ? Jade : Raised);
                        traceNumbers[i].color = palette != null ?
                            (highlighted ? palette.textOnPrimary : palette.textOnSecondary) : (highlighted ? Deep : White);
                    }
                    break;
                case Exercise.Maze:
                    cue.text = maze.Complete ? "MAZE COMPLETE" : "FIND THE GOAL";
                    mazeMap.text = maze.Draw();
                    metrics.text = "Moves " + maze.Moves + "   ·   Wall contacts " + maze.Collisions +
                        "   ·   Game score " + Mathf.RoundToInt(maze.Score);
                    break;
            }
            if (recorded) metrics.text += SaveEnabled ? "   ·   Result saved locally" :
                "   ·   Guest practice: result not saved";
        }

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
}
