using System;
using System.Collections.Generic;
using System.Text;

namespace GripRehab
{
    /// <summary>Everything a voice phrase can ask for (Stage 13, V-02 to V-04, V-06).</summary>
    public enum VoiceCommand
    {
        None,
        // UI
        Next, Previous, Select, Back, OpenSettings, Confirm, Cancel, Help,
        // In-game
        MoveForward, MoveBack, MoveLeft, MoveRight, Stop, LookAtObject, GrabObject, ReleaseObject, OpenInventory,
        StartBookshelf, StartCoffeeTable, StartStorageBox, StartMeal, StartCutting, StartServe, StartTea, StopPouring,
        // Special actions that were keys only (2026-10-28): F, R, N, B, C and , .
        SitDown, StandUp, ResetObjects, NextActivity, CameraMode, KeyboardMode, RecenterHand, RotateLeft, RotateRight,
        // Session
        Restart, Pause, Resume, Instructions, MainMenu,
        // Control
        StopListening, StartListening,
        // Spelling
        Letter, Space, DeleteLetter, ClearText, Done,
        // Movement assist level (2026-11-21)
        AssistOff, AssistLow, AssistMedium, AssistHigh
    }

    public enum VoiceConfidence { High, Medium, Low, Rejected }
    public enum VoiceOutcome { Execute, NeedsConfirmation, Rejected, Ignored }

    /// <summary>What the game is doing now, so a command can be accepted or refused (V-09).</summary>
    public struct VoiceContext
    {
        public bool inMenu, inGame, paused, activityRunning, holding, objectAvailable, textField, passwordField, canGoBack, sleeping;
    }

    public struct VoiceDecision
    {
        public VoiceOutcome outcome; public VoiceCommand command; public char letter; public string phrase, message;
        public override string ToString() => outcome + " " + command + (letter != '\0' ? " '" + letter + "'" : "") + ": " + message;
    }

    /// <summary>
    /// Stage 13 voice router. Unity-free so it can be tested offline from text transcripts (no microphone).
    /// The grammar is a fixed keyword list, recognised on the device by Windows (no cloud). Commands that are not valid
    /// in the current state are refused with a reason; low-confidence results and commands that lose progress
    /// (restart, main menu) ask for "confirm" first. Voice never produces grip force: squeezing stays with the hand,
    /// keyboard or sensor, so no measured value can come from speech.
    /// </summary>
    public sealed class VoiceCommandRouter
    {
        public static readonly Dictionary<string, VoiceCommand> Commands = new Dictionary<string, VoiceCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["next"] = VoiceCommand.Next, ["down"] = VoiceCommand.Next, ["previous"] = VoiceCommand.Previous, ["up"] = VoiceCommand.Previous,
            ["select"] = VoiceCommand.Select, ["press"] = VoiceCommand.Select, ["back"] = VoiceCommand.Back, ["go back"] = VoiceCommand.Back,
            ["open settings"] = VoiceCommand.OpenSettings, ["settings"] = VoiceCommand.OpenSettings,
            ["confirm"] = VoiceCommand.Confirm, ["yes"] = VoiceCommand.Confirm, ["cancel"] = VoiceCommand.Cancel, ["no"] = VoiceCommand.Cancel,
            ["help"] = VoiceCommand.Help, ["what can I say"] = VoiceCommand.Help,
            ["move forward"] = VoiceCommand.MoveForward, ["walk forward"] = VoiceCommand.MoveForward, ["move back"] = VoiceCommand.MoveBack, ["walk back"] = VoiceCommand.MoveBack,
            ["move left"] = VoiceCommand.MoveLeft, ["move right"] = VoiceCommand.MoveRight, ["stop"] = VoiceCommand.Stop,
            ["look at object"] = VoiceCommand.LookAtObject, ["look at it"] = VoiceCommand.LookAtObject,
            ["grab"] = VoiceCommand.GrabObject, ["pick up"] = VoiceCommand.GrabObject, ["select object"] = VoiceCommand.GrabObject,
            ["release"] = VoiceCommand.ReleaseObject, ["let go"] = VoiceCommand.ReleaseObject, ["put down"] = VoiceCommand.ReleaseObject,
            ["open inventory"] = VoiceCommand.OpenInventory, ["inventory"] = VoiceCommand.OpenInventory, ["options"] = VoiceCommand.OpenInventory,
            ["start bookshelf"] = VoiceCommand.StartBookshelf, ["start coffee table"] = VoiceCommand.StartCoffeeTable, ["start storage box"] = VoiceCommand.StartStorageBox,
            ["start simple meal"] = VoiceCommand.StartMeal, ["start cutting"] = VoiceCommand.StartCutting, ["start prepare and serve"] = VoiceCommand.StartServe, ["start pouring tea"] = VoiceCommand.StartTea, ["stop pouring"] = VoiceCommand.StopPouring,
            ["restart"] = VoiceCommand.Restart, ["pause"] = VoiceCommand.Pause, ["resume"] = VoiceCommand.Resume, ["continue"] = VoiceCommand.Resume,
            ["instructions"] = VoiceCommand.Instructions, ["main menu"] = VoiceCommand.MainMenu,
            ["stop listening"] = VoiceCommand.StopListening, ["start listening"] = VoiceCommand.StartListening,
            ["sit down"] = VoiceCommand.SitDown, ["stand up"] = VoiceCommand.StandUp, ["reset objects"] = VoiceCommand.ResetObjects,
            ["next activity"] = VoiceCommand.NextActivity, ["camera mode"] = VoiceCommand.CameraMode, ["keyboard mode"] = VoiceCommand.KeyboardMode,
            ["recenter hand"] = VoiceCommand.RecenterHand, ["rotate left"] = VoiceCommand.RotateLeft, ["rotate right"] = VoiceCommand.RotateRight,
            ["assist off"] = VoiceCommand.AssistOff, ["assist low"] = VoiceCommand.AssistLow, ["assist medium"] = VoiceCommand.AssistMedium, ["assist high"] = VoiceCommand.AssistHigh
        };

        /// <summary>Spelling words: NATO alphabet (recognised far more reliably than single letters) plus the plain letters.</summary>
        public static readonly string[] Nato = { "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliet", "kilo", "lima", "mike",
            "november", "oscar", "papa", "quebec", "romeo", "sierra", "tango", "uniform", "victor", "whiskey", "x ray", "yankee", "zulu" };
        public static readonly Dictionary<string, VoiceCommand> SpellingControls = new Dictionary<string, VoiceCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = VoiceCommand.Space, ["delete"] = VoiceCommand.DeleteLetter, ["back space"] = VoiceCommand.DeleteLetter,
            ["clear"] = VoiceCommand.ClearText, ["done"] = VoiceCommand.Done, ["finished"] = VoiceCommand.Done,
            ["confirm"] = VoiceCommand.Confirm, ["yes"] = VoiceCommand.Confirm, ["cancel"] = VoiceCommand.Cancel, ["no"] = VoiceCommand.Cancel,
            ["stop listening"] = VoiceCommand.StopListening
        };

        /// <summary>All command keywords (for the command recognizer).</summary>
        public static string[] CommandKeywords() { var k = new List<string>(Commands.Keys); return k.ToArray(); }
        /// <summary>All spelling keywords (for the spelling recognizer): NATO words, letters a–z and the controls.</summary>
        public static string[] SpellingKeywords()
        {
            var k = new List<string>(Nato);
            for (char c = 'a'; c <= 'z'; c++) k.Add(c.ToString());
            k.AddRange(SpellingControls.Keys);
            return k.ToArray();
        }

        /// <summary>Commands that lose progress are always confirmed.</summary>
        public static bool Destructive(VoiceCommand c) => c == VoiceCommand.Restart || c == VoiceCommand.MainMenu;

        VoiceDecision? pending; double pendingUntil;
        public const double ConfirmSeconds = 6;
        public string PendingDescription => pending.HasValue ? pending.Value.command.ToString() : null;

        /// <summary>Parses a recognised phrase and decides what to do with it in the given state. 'now' is seconds (for the confirmation window).</summary>
        public VoiceDecision Route(string phrase, VoiceConfidence confidence, VoiceContext context, double now)
        {
            phrase = (phrase ?? "").Trim();
            var d = new VoiceDecision { phrase = phrase };
            if (confidence == VoiceConfidence.Rejected || phrase.Length == 0) return Ignore(d, "Not heard clearly");
            if (pending.HasValue && now > pendingUntil) pending = null;

            // Spelling mode: a text field has focus.
            if (context.textField)
            {
                if (context.passwordField) return Reject(d, "Voice is turned off in password fields. Type the password.");
                if (TryLetter(phrase, out char letter)) { d.command = VoiceCommand.Letter; d.letter = letter; return Low(confidence) ? Confirm(d, now, "Did you say '" + char.ToUpperInvariant(letter) + "'? Say confirm or cancel.") : Execute(d, "Letter " + char.ToUpperInvariant(letter)); }
                if (SpellingControls.TryGetValue(phrase, out var control)) return Resolve(d, control, confidence, context, now);
                return Reject(d, "While spelling, say a letter (alpha, bravo...), space, delete, clear or done.");
            }
            if (!Commands.TryGetValue(phrase, out var command)) return Reject(d, "Not a command. Say help for the list.");
            return Resolve(d, command, confidence, context, now);
        }

        VoiceDecision Resolve(VoiceDecision d, VoiceCommand command, VoiceConfidence confidence, VoiceContext context, double now)
        {
            d.command = command;
            if (context.sleeping && command != VoiceCommand.StartListening) return Ignore(d, "Voice is asleep. Say start listening.");
            if (command == VoiceCommand.Confirm && pending.HasValue)
            {
                var p = pending.Value; pending = null;
                // Re-check: the state may have changed while waiting.
                var again = Allowed(p.command, context);
                if (again != null) return Reject(p, again);
                p.outcome = VoiceOutcome.Execute; p.message = "Confirmed: " + Describe(p.command, p.letter); return p;
            }
            if (command == VoiceCommand.Cancel && pending.HasValue) { pending = null; return Execute(d, "Cancelled"); }
            var refusal = Allowed(command, context);
            if (refusal != null) return Reject(d, refusal);
            if (Destructive(command)) return Confirm(d, now, Describe(command, '\0') + ": your progress here will end. Say confirm or cancel.");
            if (Low(confidence) && command != VoiceCommand.Stop && command != VoiceCommand.Cancel)
                return Confirm(d, now, "Did you say '" + d.phrase + "'? Say confirm or cancel.");
            return Execute(d, Describe(command, '\0'));
        }

        /// <summary>Null if the command is valid now, otherwise the reason it is not (V-09).</summary>
        public static string Allowed(VoiceCommand c, VoiceContext x)
        {
            switch (c)
            {
                case VoiceCommand.MoveForward: case VoiceCommand.MoveBack: case VoiceCommand.MoveLeft: case VoiceCommand.MoveRight:
                    return x.inGame && !x.paused ? null : "Moving works while you are in the apartment and not paused.";
                case VoiceCommand.LookAtObject:
                    return x.inGame && !x.paused ? (x.objectAvailable ? null : "There is no object to look at here.") : "Looking works in the apartment.";
                case VoiceCommand.GrabObject:
                    return !x.inGame || x.paused ? "Picking up works in the apartment." : x.holding ? "You are already holding something. Say release first." : x.objectAvailable ? null : "There is no object within reach.";
                case VoiceCommand.ReleaseObject:
                    return x.inGame && x.holding ? null : "You are not holding anything.";
                case VoiceCommand.OpenInventory: case VoiceCommand.Pause:
                    return x.inGame && !x.paused ? null : x.paused ? "Already paused." : "Pause works in the apartment.";
                case VoiceCommand.Resume:
                    return x.inGame && x.paused ? null : "Nothing to resume.";
                case VoiceCommand.StartBookshelf: case VoiceCommand.StartCoffeeTable: case VoiceCommand.StartStorageBox:
                case VoiceCommand.StartMeal: case VoiceCommand.StartCutting: case VoiceCommand.StartServe: case VoiceCommand.StartTea:
                    return x.inGame ? (x.activityRunning ? "An activity is running. Finish it or say restart." : null) : "Activities start in the apartment.";
                case VoiceCommand.Restart:
                    return x.inGame && x.activityRunning ? null : "Restart works during an activity.";
                case VoiceCommand.StopPouring:
                    return x.inGame && x.activityRunning && x.holding ? null : "Stop pouring works while you hold the teapot or cup.";
                case VoiceCommand.SitDown: case VoiceCommand.StandUp: case VoiceCommand.ResetObjects: case VoiceCommand.CameraMode: case VoiceCommand.KeyboardMode: case VoiceCommand.RecenterHand:
                    return x.inGame ? null : "That works in the apartment.";
                case VoiceCommand.NextActivity:
                    return x.inGame ? null : "Activities are chosen in the apartment.";
                case VoiceCommand.RotateLeft: case VoiceCommand.RotateRight:
                    return x.inGame && x.activityRunning && x.holding ? null : "Rotate works while you hold an activity item.";
                case VoiceCommand.MainMenu:
                    return x.inMenu && !x.canGoBack ? "You are on the main menu." : null;
                case VoiceCommand.Back:
                    return x.canGoBack ? null : "There is nothing to go back to.";
                case VoiceCommand.Next: case VoiceCommand.Previous: case VoiceCommand.Select:
                    return x.inMenu || x.paused ? null : "Menu navigation works in menus and when paused.";
                case VoiceCommand.Confirm: case VoiceCommand.Cancel:
                    return x.textField ? null : "Nothing is waiting for confirmation."; // while spelling they review the text
                case VoiceCommand.StartListening:
                    return x.sleeping ? null : "Already listening.";
                case VoiceCommand.Space: case VoiceCommand.DeleteLetter: case VoiceCommand.ClearText: case VoiceCommand.Done:
                    return x.textField ? null : "Select a text field first.";
                default: return null;
            }
        }

        public static string Describe(VoiceCommand c, char letter)
        {
            switch (c)
            {
                case VoiceCommand.Letter: return "Letter " + char.ToUpperInvariant(letter);
                case VoiceCommand.MainMenu: return "Main menu";
                case VoiceCommand.Restart: return "Restart the activity";
                default:
                    var s = new StringBuilder();
                    foreach (var ch in c.ToString()) { if (char.IsUpper(ch) && s.Length > 0) s.Append(' '); s.Append(s.Length == 0 ? ch : char.ToLowerInvariant(ch)); }
                    return s.ToString();
            }
        }

        /// <summary>A spelling word or single letter to its letter.</summary>
        public static bool TryLetter(string phrase, out char letter)
        {
            letter = '\0'; var p = phrase.Trim().ToLowerInvariant();
            if (p.Length == 1 && p[0] >= 'a' && p[0] <= 'z') { letter = p[0]; return true; }
            if (p == "xray" || p == "x-ray") p = "x ray"; if (p == "juliett") p = "juliet";
            int i = Array.IndexOf(Nato, p);
            if (i < 0) return false;
            letter = (char)('a' + i); return true;
        }

        static bool Low(VoiceConfidence c) => c == VoiceConfidence.Low;
        VoiceDecision Confirm(VoiceDecision d, double now, string message) { d.outcome = VoiceOutcome.NeedsConfirmation; d.message = message; pending = d; pendingUntil = now + ConfirmSeconds; return d; }
        static VoiceDecision Execute(VoiceDecision d, string message) { d.outcome = VoiceOutcome.Execute; d.message = message; return d; }
        static VoiceDecision Reject(VoiceDecision d, string message) { d.outcome = VoiceOutcome.Rejected; d.message = message; return d; }
        static VoiceDecision Ignore(VoiceDecision d, string message) { d.outcome = VoiceOutcome.Ignored; d.message = message; return d; }
    }

    /// <summary>
    /// Letter-by-letter spelling into a review buffer (V-06, V-07). Nothing reaches the field until the person
    /// says done and then confirms; until then the text can be corrected (delete, clear) by voice or keyboard.
    /// Names are capitalised at the start and after a space.
    /// </summary>
    public sealed class SpellingBuffer
    {
        public const int MaxLength = 40;
        readonly StringBuilder text = new StringBuilder();
        public string Text => text.ToString();
        public bool Reviewing { get; private set; }

        public void Begin(string existing) { text.Clear(); text.Append(existing ?? ""); Reviewing = false; }

        /// <summary>Applies a routed spelling command; returns a message for the feedback line, and the committed text when confirmed.</summary>
        public string Apply(VoiceCommand c, char letter, out string committed)
        {
            committed = null;
            switch (c)
            {
                case VoiceCommand.Letter:
                    if (Reviewing) Reviewing = false;
                    if (text.Length >= MaxLength) return "That is the longest name allowed.";
                    bool start = text.Length == 0 || text[text.Length - 1] == ' ';
                    text.Append(start ? char.ToUpperInvariant(letter) : char.ToLowerInvariant(letter));
                    return "Spelling: " + Text;
                case VoiceCommand.Space:
                    if (text.Length > 0 && text[text.Length - 1] != ' ') text.Append(' ');
                    return "Spelling: " + Text + "_";
                case VoiceCommand.DeleteLetter:
                    if (text.Length > 0) text.Length--; Reviewing = false; return "Spelling: " + Text;
                case VoiceCommand.ClearText:
                    text.Clear(); Reviewing = false; return "Spelling cleared.";
                case VoiceCommand.Done:
                    Reviewing = true; return "Is \"" + Text.Trim() + "\" right? Say confirm to put it in the field, or delete to correct it.";
                case VoiceCommand.Confirm:
                    if (!Reviewing) return "Say done first to review the text.";
                    Reviewing = false; committed = Text.Trim(); return "Entered \"" + committed + "\".";
                case VoiceCommand.Cancel:
                    Reviewing = false; return "Still spelling: " + Text;
                default: return null;
            }
        }
    }
}
