using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace GripRehab
{
    public enum VoiceMode { Off, PushToTalk, AlwaysOn }

    /// <summary>
    /// Stage 13: voice as an extra input (V-01). Windows' on-device keyword recognizer listens for a fixed list of commands, or
    /// for spelling words while a text field is selected; nothing is sent online and no audio is stored. <see cref="VoiceCommandRouter"/>
    /// decides; this component carries the decision out through the same actions the mouse, keyboard and hand pointer use.
    /// Off by default. Push-to-talk is "hold Ctrl"; always-on can be put to sleep by voice. Free dictation is not offered:
    /// Windows dictation runs as an online service (you chose offline spelling only, 2026-10-27).
    /// </summary>
    public sealed class RehabVoiceInput : MonoBehaviour
    {
        const string ModeKey = "GripRehab.Voice.Mode";
        public const float MoveSeconds = 4f, PushToTalkTailSeconds = .8f;
        static RehabVoiceInput instance;
        public static RehabVoiceInput Instance => instance;

        RehabAppShell shell; ApartmentExperience app; ActivityRunner runner; ApartmentGuide guide;
        readonly VoiceCommandRouter router = new VoiceCommandRouter();
        readonly SpellingBuffer spelling = new SpellingBuffer();
        readonly Queue<(string phrase, VoiceConfidence confidence)> heard = new Queue<(string, VoiceConfidence)>();
        GameObject spellingField; bool sleeping; float lastPushToTalk = -10;
        Vector3 move; float moveUntil;
        Text toastText; CanvasGroup toast; float toastUntil; RectTransform toastRect;
        public RectTransform ToastRect => toastRect;
        public bool ToastVisible => toast != null && toast.alpha > .5f;
        public string ToastText => toastText != null ? toastText.text : "";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        KeywordRecognizer commands, letters;
#endif

        public static VoiceMode Mode
        {
            get => (VoiceMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(ModeKey, (int)value); if (instance != null) instance.ApplyMode(); }
        }
        public static string ModeName(VoiceMode m) => m == VoiceMode.Off ? "OFF" : m == VoiceMode.PushToTalk ? "HOLD CTRL TO TALK" : "ALWAYS ON";
        /// <summary>Why voice cannot run on this computer, or null when it can.</summary>
        public string Unavailable { get; private set; }
        public bool RecognizerRunning { get; private set; }
        public VoiceDecision LastDecision { get; private set; }
        public string LastMessage { get; private set; } = "";
        public string SpellingText => spelling.Text;
        public bool Spelling => spellingField != null;
        public Vector3 Moving => Time.unscaledTime < moveUntil ? move : Vector3.zero;
        public int Executed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null || FindAnyObjectByType<RehabAppShell>() == null) return;
            instance = new GameObject("Voice input").AddComponent<RehabVoiceInput>();
        }

        void Start()
        {
            shell = FindAnyObjectByType<RehabAppShell>(); app = shell != null ? shell.experience : null;
            BuildToast();
            ApplyMode();
        }

        // ---------- recognizer ----------
        void ApplyMode()
        {
            Unavailable = Check();
            bool want = Mode != VoiceMode.Off && Unavailable == null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (want && commands == null)
            {
                try
                {
                    // Low minimum confidence: low results reach the router, which asks for confirmation instead of acting.
                    commands = new KeywordRecognizer(VoiceCommandRouter.CommandKeywords(), ConfidenceLevel.Low);
                    letters = new KeywordRecognizer(VoiceCommandRouter.SpellingKeywords(), ConfidenceLevel.Low);
                    commands.OnPhraseRecognized += Heard; letters.OnPhraseRecognized += Heard;
                    PhraseRecognitionSystem.OnError += code => { Unavailable = "Speech error: " + code; Feedback(false, "VOICE UNAVAILABLE", "Voice stopped: " + code + ". Other controls still work."); };
                }
                catch (System.Exception error) { Unavailable = "Speech could not start: " + error.Message; want = false; }
            }
            if (!want) StopRecognizers();
#endif
            sleeping = false;
            if (Mode == VoiceMode.Off) ApartmentGuide.Clear(ApartmentGuide.Channel.Voice);
            else ApartmentGuide.Set(ApartmentGuide.Channel.Voice, false, Unavailable != null ? "UNAVAILABLE" : Mode == VoiceMode.PushToTalk ? "HOLD CTRL" : "LISTENING");
        }

        /// <summary>Microphone and speech-platform checks (V-10). A missing microphone or blocked permission shows as unavailable, never as listening.</summary>
        static string Check()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!PhraseRecognitionSystem.isSupported) return "Speech recognition is not supported on this computer.";
            if (Microphone.devices == null || Microphone.devices.Length == 0) return "No microphone found. Connect one, or allow microphone access in Windows Settings > Privacy > Microphone.";
            return null;
#else
            return "Voice commands need Windows.";
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void Heard(PhraseRecognizedEventArgs e)
        {
            var c = e.confidence == ConfidenceLevel.High ? VoiceConfidence.High : e.confidence == ConfidenceLevel.Medium ? VoiceConfidence.Medium
                : e.confidence == ConfidenceLevel.Low ? VoiceConfidence.Low : VoiceConfidence.Rejected;
            heard.Enqueue((e.text, c));
        }
        void StopRecognizers()
        {
            if (commands != null && commands.IsRunning) commands.Stop();
            if (letters != null && letters.IsRunning) letters.Stop();
            RecognizerRunning = false;
        }
        void RunRecognizer(bool spellingMode)
        {
            if (commands == null) return;
            var on = spellingMode ? letters : commands; var off = spellingMode ? commands : letters;
            if (off.IsRunning) off.Stop();
            if (!on.IsRunning) { try { on.Start(); } catch (System.Exception error) { Unavailable = "Microphone or speech blocked: " + error.Message; StopRecognizers(); return; } }
            RecognizerRunning = on.IsRunning;
        }
        void OnDestroy() { StopRecognizers(); commands?.Dispose(); letters?.Dispose(); }
#endif

        /// <summary>Feeds a transcript as if it had been heard (checks and tests; SYNTHETIC, no microphone).</summary>
        public void Inject(string phrase, VoiceConfidence confidence = VoiceConfidence.High) => heard.Enqueue((phrase, confidence));

        // ---------- per frame ----------
        void Update()
        {
            if (shell == null) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl) lastPushToTalk = Time.unscaledTime;
            TrackTextField();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Mode != VoiceMode.Off && Unavailable == null) RunRecognizer(Spelling);
#endif
            while (heard.Count > 0)
            {
                var (phrase, confidence) = heard.Dequeue();
                // Push-to-talk: speech counts only while Ctrl is held, or just after (the recognizer finishes after the words end).
                if (Mode == VoiceMode.PushToTalk && Time.unscaledTime - lastPushToTalk > PushToTalkTailSeconds && confidence != VoiceConfidence.Rejected && !synthetic) continue;
                Handle(router.Route(phrase, confidence, Context(), Time.unscaledTimeAsDouble));
            }
            Walk();
            if (toast != null && toast.alpha > 0 && Time.unscaledTime > toastUntil) { toast.alpha = 0; ApartmentGuide.Set(ApartmentGuide.Channel.Voice, false, StripIdle()); }
        }
        bool synthetic => Application.isBatchMode || System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a.StartsWith("-apartment-"));

        string StripIdle() => Mode == VoiceMode.Off ? "OFF" : Unavailable != null ? "UNAVAILABLE" : sleeping ? "ASLEEP" : Spelling ? "SPELLING" : Mode == VoiceMode.PushToTalk ? "HOLD CTRL" : "LISTENING";

        void TrackTextField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool field = selected != null && selected.activeInHierarchy && (selected.GetComponent<InputField>() != null || selected.GetComponent<TMPro.TMP_InputField>() != null);
            if (field && selected != spellingField) { spellingField = selected; spelling.Begin(FieldText(selected)); }
            else if (!field && spellingField != null) spellingField = null;
        }

        VoiceContext Context()
        {
            bool menu = shell.BlocksGameplay, game = !menu && app != null;
            bool paused = game && app.player != null && app.player.Paused;
            if (runner == null && app != null) runner = app.GetComponent<ActivityRunner>();
            var target = app != null && app.choices != null ? app.choices.SelectedObject : null;
            bool password = false;
            if (spellingField != null)
            {
                var f = spellingField.GetComponent<InputField>(); var t = spellingField.GetComponent<TMPro.TMP_InputField>();
                password = f != null && (f.contentType == InputField.ContentType.Password || f.contentType == InputField.ContentType.Pin || f.inputType == InputField.InputType.Password)
                    || t != null && (t.contentType == TMPro.TMP_InputField.ContentType.Password || t.contentType == TMPro.TMP_InputField.ContentType.Pin || t.inputType == TMPro.TMP_InputField.InputType.Password);
            }
            return new VoiceContext
            {
                inMenu = menu, inGame = game, paused = paused, sleeping = sleeping,
                activityRunning = runner != null && runner.Active, holding = app != null && app.Held != null,
                objectAvailable = target != null && target.gameObject.activeInHierarchy && !target.IsBroken || runner != null && runner.Active,
                textField = spellingField != null, passwordField = password,
                canGoBack = menu && shell.CurrentPage != "Home" || paused
            };
        }

        // ---------- acting on a decision ----------
        void Handle(VoiceDecision d)
        {
            LastDecision = d; LastMessage = "";
            switch (d.outcome)
            {
                case VoiceOutcome.Ignored: if (sleeping) return; Feedback(false, "NOT CLEAR", d.message, sound: false); return;
                case VoiceOutcome.Rejected: Feedback(false, "NOT NOW", d.message); return;
                case VoiceOutcome.NeedsConfirmation: Feedback(true, "CONFIRM?", d.message, RehabSound.Chime); return;
            }
            string result = Act(d); if (result != null) LastMessage = "";
            if (result != null) { Feedback(false, "CAN'T DO THAT", result); return; }
            Executed++;
            Feedback(true, "HEARD: " + d.phrase.ToUpperInvariant(), LastMessage.Length > 0 ? LastMessage : d.message);
        }

        /// <summary>Carries out the command; returns null on success or the reason it could not happen.</summary>
        string Act(VoiceDecision d)
        {
            var player = app != null ? app.player : null;
            switch (d.command)
            {
                case VoiceCommand.Letter: case VoiceCommand.Space: case VoiceCommand.DeleteLetter: case VoiceCommand.ClearText: case VoiceCommand.Done:
                case VoiceCommand.Confirm when Spelling: case VoiceCommand.Cancel when Spelling:
                    LastMessage = spelling.Apply(d.command, d.letter, out var committed);
                    if (committed != null) SetFieldText(spellingField, committed);
                    return null;
                case VoiceCommand.Cancel: return null; // a pending confirmation was cleared by the router
                case VoiceCommand.StopListening: sleeping = true; move = Vector3.zero; return null;
                case VoiceCommand.StartListening: sleeping = false; return null;
                case VoiceCommand.Help: LastMessage = HelpText(); return null;
                case VoiceCommand.Next: return Navigate(true);
                case VoiceCommand.Previous: return Navigate(false);
                case VoiceCommand.Select:
                {
                    var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                    if (selected == null || !selected.activeInHierarchy) return "Nothing is selected. Say next to move to a button.";
                    ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); return null;
                }
                case VoiceCommand.OpenSettings: return PressButton("SETTINGS", "APPEARANCE") ? null : "There is no settings button on this screen.";
                case VoiceCommand.Back:
                    if (player != null && player.Paused && !shell.BlocksGameplay) { player.SetPaused(false); return null; }
                    return PressButton("BACK", "HOME") ? null : "There is no back button on this screen.";
                case VoiceCommand.MainMenu: move = Vector3.zero; shell.ReturnHome(); return null;
                case VoiceCommand.MoveForward: return Move(Vector3.forward);
                case VoiceCommand.MoveBack: return Move(Vector3.back);
                case VoiceCommand.MoveLeft: return Move(Vector3.left);
                case VoiceCommand.MoveRight: return Move(Vector3.right);
                case VoiceCommand.Stop: move = Vector3.zero; moveUntil = 0; return null;
                case VoiceCommand.LookAtObject:
                {
                    var target = app.choices != null ? app.choices.SelectedObject : null;
                    if (target == null || !target.gameObject.activeInHierarchy) return "There is no object to look at here.";
                    player.LookAt(target.transform.position); return null;
                }
                case VoiceCommand.GrabObject:
                    // Same path as the E key: whatever you are looking at and can reach.
                    return app.TryKeyboardAction() && app.Held != null ? null : "Nothing within reach where you are looking. Say look at object, or move closer.";
                case VoiceCommand.ReleaseObject: app.Release(); return null;
                case VoiceCommand.OpenInventory: case VoiceCommand.Pause: move = Vector3.zero; player.SetPaused(true); return null;
                case VoiceCommand.Resume: player.SetPaused(false); return null;
                case VoiceCommand.Instructions:
                    if (runner != null && runner.Active) { LastMessage = "Step " + (runner.Current.steps.Current + 1) + " of " + runner.Current.steps.Count + ": " + runner.Current.steps.Instruction; return null; }
                    if (guide == null) guide = FindAnyObjectByType<ApartmentGuide>();
                    if (guide != null) { guide.Restart(true); return null; }
                    LastMessage = HelpText(); return null;
                case VoiceCommand.Restart: return runner != null && runner.Begin(runner.Current.kind, runner.Level) ? null : runner != null ? runner.Feedback : "No activity to restart.";
                case VoiceCommand.StartBookshelf: return StartActivity(ActivityKind.Bookshelf);
                case VoiceCommand.StartCoffeeTable: return StartActivity(ActivityKind.CoffeeTable);
                case VoiceCommand.StartStorageBox: return StartActivity(ActivityKind.StorageBox);
                case VoiceCommand.StartMeal: return StartActivity(ActivityKind.SimpleMeal);
                case VoiceCommand.StartCutting: return StartActivity(ActivityKind.Cutting);
                case VoiceCommand.StartServe: return StartActivity(ActivityKind.PrepareAndServe);
                default: return null;
            }
        }

        string StartActivity(ActivityKind kind)
        {
            if (runner == null) return "Activities are not available here.";
            if (!runner.Begin(kind)) return runner.Feedback.Length > 0 ? runner.Feedback : "That activity could not start.";
            app.player.SetPaused(false); return null;
        }

        string Move(Vector3 direction)
        {
            var player = app.player;
            if (player.IsSeated) return "You are seated. Stand up first.";
            move = direction; moveUntil = Time.unscaledTime + MoveSeconds; return null;
        }

        /// <summary>Walks for up to 4 s or until "stop", through the player's own controller; stops when paused or in a menu.</summary>
        void Walk()
        {
            if (move == Vector3.zero || app == null || app.player == null) return;
            var player = app.player;
            if (Time.unscaledTime > moveUntil || player.Paused || shell.BlocksGameplay || player.IsSeated || player.Controller == null || !player.Controller.enabled) { move = Vector3.zero; return; }
            player.Controller.Move(player.transform.TransformDirection(move) * player.settings.walkSpeed * Time.deltaTime);
        }

        static string Navigate(bool forward)
        {
            var events = EventSystem.current; if (events == null) return "No menu here.";
            var current = events.currentSelectedGameObject != null ? events.currentSelectedGameObject.GetComponent<Selectable>() : null;
            Selectable next = null;
            if (current != null && current.isActiveAndEnabled)
                next = forward ? current.FindSelectableOnDown() ?? current.FindSelectableOnRight() : current.FindSelectableOnUp() ?? current.FindSelectableOnLeft();
            if (next == null)
            {
                var all = Selectable.allSelectablesArray.Where(s => s.isActiveAndEnabled && s.IsInteractable() && Visible(s)).OrderByDescending(s => s.transform.position.y).ThenBy(s => s.transform.position.x).ToList();
                if (all.Count == 0) return "Nothing to move to.";
                int i = current != null ? all.IndexOf(current) : -1;
                next = all[((i < 0 ? (forward ? -1 : 0) : i) + (forward ? 1 : -1) + all.Count) % all.Count];
            }
            next.Select(); return null;
        }

        static bool Visible(Selectable s) { var g = s.GetComponentInParent<CanvasGroup>(); return g == null || g.alpha > .5f && g.interactable; }

        /// <summary>Presses the first visible, enabled button whose label contains one of the words (the same as clicking it).</summary>
        static bool PressButton(params string[] words)
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!button.isActiveAndEnabled || !button.IsInteractable() || !Visible(button)) continue;
                var label = button.GetComponentInChildren<Text>(); var tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
                string text = ((label != null ? label.text : "") + " " + (tmp != null ? tmp.text : "")).ToUpperInvariant();
                if (words.Any(w => text.Contains(w))) { button.onClick.Invoke(); return true; }
            }
            return false;
        }

        static string FieldText(GameObject g) { var f = g.GetComponent<InputField>(); if (f != null) return f.text; var t = g.GetComponent<TMPro.TMP_InputField>(); return t != null ? t.text : ""; }
        static void SetFieldText(GameObject g, string text) { var f = g.GetComponent<InputField>(); if (f != null) { f.text = text; return; } var t = g.GetComponent<TMPro.TMP_InputField>(); if (t != null) t.text = text; }

        string HelpText()
        {
            var x = Context();
            var phrases = VoiceCommandRouter.Commands.GroupBy(p => p.Value).Where(g => g.Key != VoiceCommand.Confirm && g.Key != VoiceCommand.Cancel && VoiceCommandRouter.Allowed(g.Key, x) == null)
                .Select(g => g.First().Key).Take(12);
            return x.textField ? "Spell with alpha, bravo, charlie..., then say done and confirm." : "You can say: " + string.Join(", ", phrases) + ".";
        }

        // ---------- feedback (V-08) ----------
        void Feedback(bool positive, string strip, string message, RehabSound? cue = null, bool sound = true)
        {
            LastMessage = message;
            ApartmentGuide.Set(ApartmentGuide.Channel.Voice, positive, strip);
            if (sound) RehabAudio.Play(cue ?? (positive ? RehabSound.Step : RehabSound.Error), positive ? 1.25f : 1f);
            if (toastText != null) { PlaceToast(); toastText.text = message; toast.alpha = 1; toastUntil = Time.unscaledTime + Mathf.Clamp(1.5f + message.Length * .05f, 2.5f, 7f); }
        }

        /// <summary>
        /// In the apartment the feedback sits just above the input strip, in the free band between the bottom cards, so it never
        /// covers the activity panel or HUD text; in menus it sits in the bottom margin under the panels.
        /// </summary>
        void PlaceToast()
        {
            if (guide == null) guide = FindAnyObjectByType<ApartmentGuide>();
            var strip = guide != null ? guide.Strip : null;
            var canvas = (RectTransform)toastRect.parent;
            if (strip != null && strip.gameObject.activeInHierarchy && !shell.BlocksGameplay)
            {
                var c = new Vector3[4]; strip.GetWorldCorners(c);
                var top = new Vector2((c[1].x + c[2].x) * .5f, c[1].y + 8);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, top, null, out var local))
                { toastRect.anchoredPosition = new Vector2(local.x, local.y + canvas.rect.height * .5f); return; }
            }
            toastRect.anchoredPosition = new Vector2(0, 8); // menus: the bottom margin under the panels
        }

        void BuildToast()
        {
            var canvasObject = new GameObject("Voice feedback", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            canvasObject.AddComponent<GripRehab.UI.GripUiAspectFit>(); // same 4:3 rule as the other screens
            toast = canvasObject.GetComponent<CanvasGroup>(); toast.alpha = 0; toast.blocksRaycasts = false; toast.interactable = false;
            var panel = new GameObject("Panel", typeof(Image)); panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.GetComponent<RectTransform>(); toastRect = rect;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(520, 60);
            // Near-black panel with white text: about 17:1 whatever the theme, readable over the 3D scene.
            panel.GetComponent<Image>().color = new Color(.03f, .08f, .1f, .94f); panel.GetComponent<Image>().raycastTarget = false;
            var label = new GameObject("Message", typeof(Text)); label.transform.SetParent(panel.transform, false);
            var lr = label.GetComponent<RectTransform>(); lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(18, 6); lr.offsetMax = new Vector2(-18, -6);
            toastText = label.GetComponent<Text>(); toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); toastText.fontSize = 18; toastText.fontStyle = FontStyle.Bold;
            toastText.color = new Color(.96f, .98f, .98f); toastText.alignment = TextAnchor.MiddleCenter; toastText.raycastTarget = false;
            toastText.resizeTextForBestFit = true; toastText.resizeTextMinSize = 13; toastText.resizeTextMaxSize = 18;
        }
    }
}
