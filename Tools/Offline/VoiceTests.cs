using System;
using GripRehab;

// Executes the production voice router and spelling buffer with text transcripts only.
// This is not a microphone, Windows speech or Unity test.
class VoiceTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static VoiceContext Game(bool holding = false, bool paused = false, bool activity = false) =>
        new VoiceContext { inGame = true, paused = paused, holding = holding, activityRunning = activity, objectAvailable = true, canGoBack = paused };
    static VoiceContext Menu(bool home = true) => new VoiceContext { inMenu = true, canGoBack = !home };
    static VoiceContext Field(bool password = false) => new VoiceContext { inMenu = true, textField = true, passwordField = password, canGoBack = true };

    static int Main()
    {
        try
        {
            var r = new VoiceCommandRouter(); double t = 0;
            VoiceDecision Say(string p, VoiceContext x, VoiceConfidence c = VoiceConfidence.High) => r.Route(p, c, x, t += .5);

            // Coverage: every command keyword parses to a command, and every command has at least one phrase.
            foreach (var pair in VoiceCommandRouter.Commands) Check(pair.Value != VoiceCommand.None, "keyword '" + pair.Key + "' maps to a command");
            foreach (VoiceCommand c in Enum.GetValues(typeof(VoiceCommand)))
            {
                if (c == VoiceCommand.None || c == VoiceCommand.Letter) continue;
                Check(VoiceCommandRouter.Commands.ContainsValue(c) || VoiceCommandRouter.SpellingControls.ContainsValue(c), "command " + c + " has a phrase");
            }
            Check(VoiceCommandRouter.SpellingKeywords().Length == 26 + 26 + VoiceCommandRouter.SpellingControls.Count, "spelling grammar: NATO, letters and controls");

            // V-02 UI.
            Check(Say("next", Menu()).outcome == VoiceOutcome.Execute, "menu: next");
            Check(Say("select", Menu()).outcome == VoiceOutcome.Execute, "menu: select");
            Check(Say("open settings", Menu()).outcome == VoiceOutcome.Execute, "menu: open settings");
            Check(Say("back", Menu(true)).outcome == VoiceOutcome.Rejected, "home: back has nowhere to go");
            Check(Say("back", Menu(false)).outcome == VoiceOutcome.Execute, "sub-page: back");
            Check(Say("start bookshelf", Menu()).outcome == VoiceOutcome.Rejected, "menu: activities start in the apartment");
            Check(Say("start bookshelf", Game()).outcome == VoiceOutcome.Execute, "apartment: launch an activity");
            Check(Say("start cutting", Game(activity: true)).outcome == VoiceOutcome.Rejected, "one activity at a time");

            // V-03 in-game.
            Check(Say("move forward", Game()).command == VoiceCommand.MoveForward && Say("move forward", Game()).outcome == VoiceOutcome.Execute, "move forward");
            Check(Say("move left", Game(paused: true)).outcome == VoiceOutcome.Rejected, "no moving while paused");
            Check(Say("stop", Game()).outcome == VoiceOutcome.Execute, "stop");
            Check(Say("grab", Game()).outcome == VoiceOutcome.Execute, "grab");
            Check(Say("grab", Game(holding: true)).outcome == VoiceOutcome.Rejected, "grab while holding is refused");
            Check(Say("release", Game()).outcome == VoiceOutcome.Rejected, "release with empty hands is refused");
            Check(Say("let go", Game(holding: true)).outcome == VoiceOutcome.Execute, "release while holding");
            Check(Say("look at object", Game()).outcome == VoiceOutcome.Execute, "look at object");
            Check(Say("open inventory", Game()).outcome == VoiceOutcome.Execute, "open inventory");
            Check(Say("grab", Menu()).outcome == VoiceOutcome.Rejected, "no grabbing from a menu");

            // V-04 session; destructive commands need confirm, then are re-checked against the current state.
            Check(Say("pause", Game()).outcome == VoiceOutcome.Execute && Say("resume", Game(paused: true)).outcome == VoiceOutcome.Execute, "pause and resume");
            Check(Say("resume", Game()).outcome == VoiceOutcome.Rejected, "nothing to resume");
            Check(Say("instructions", Game()).outcome == VoiceOutcome.Execute, "instructions");
            Check(Say("restart", Game()).outcome == VoiceOutcome.Rejected, "restart needs an activity");
            Check(Say("restart", Game(activity: true)).outcome == VoiceOutcome.NeedsConfirmation, "restart asks to confirm");
            Check(Say("confirm", Game(activity: true)).outcome == VoiceOutcome.Execute && r.PendingDescription == null, "confirm runs it");
            Check(Say("main menu", Game()).outcome == VoiceOutcome.NeedsConfirmation && Say("cancel", Game()).outcome == VoiceOutcome.Execute && Say("confirm", Game()).outcome == VoiceOutcome.Rejected, "cancel drops it");
            Check(Say("restart", Game(activity: true)).outcome == VoiceOutcome.NeedsConfirmation && Say("confirm", Game(activity: false)).outcome == VoiceOutcome.Rejected, "state changed before confirm: refused");
            Check(Say("main menu", Game()).outcome == VoiceOutcome.NeedsConfirmation && r.Route("confirm", VoiceConfidence.High, Game(), t += 10).outcome == VoiceOutcome.Rejected, "confirmation expires");

            // Special actions that were keys only (2026-10-28).
            Check(Say("sit down", Game()).outcome == VoiceOutcome.Execute && Say("stand up", Game()).outcome == VoiceOutcome.Execute, "sit down / stand up (was F)");
            Check(Say("reset objects", Game()).outcome == VoiceOutcome.Execute && Say("reset objects", Menu()).outcome == VoiceOutcome.Rejected, "reset objects in the apartment only (was R)");
            Check(Say("next activity", Game()).outcome == VoiceOutcome.Execute, "next activity (was N)");
            Check(Say("camera mode", Game()).outcome == VoiceOutcome.Execute && Say("keyboard mode", Game()).outcome == VoiceOutcome.Execute, "input mode (was B)");
            Check(Say("recenter hand", Game()).outcome == VoiceOutcome.Execute, "recenter hand (was C)");
            Check(Say("rotate left", Game(holding: true, activity: true)).outcome == VoiceOutcome.Execute, "rotate while holding an activity item (was , and .)");
            Check(Say("rotate right", Game(activity: true)).outcome == VoiceOutcome.Rejected, "rotate needs a held item");

            // V-09 ambiguity, noise, failures.
            Check(Say("grab", Game(), VoiceConfidence.Low).outcome == VoiceOutcome.NeedsConfirmation, "low confidence asks first");
            Check(Say("yes", Game()).outcome == VoiceOutcome.Execute && r.Route("x", VoiceConfidence.High, Game(), t).command == VoiceCommand.None, "yes confirms a low-confidence command");
            Check(Say("stop", Game(), VoiceConfidence.Low).outcome == VoiceOutcome.Execute, "stop acts even at low confidence");
            Check(Say("grab", Game(), VoiceConfidence.Rejected).outcome == VoiceOutcome.Ignored, "rejected audio is ignored");
            Check(Say("", Game()).outcome == VoiceOutcome.Ignored, "silence is ignored");
            Check(Say("make me a sandwich", Game()).outcome == VoiceOutcome.Rejected, "unknown phrase is refused with help");
            Check(Say("confirm", Game()).outcome == VoiceOutcome.Rejected, "confirm with nothing pending");
            Check(Say("stop listening", Game()).outcome == VoiceOutcome.Execute, "stop listening");
            var asleep = Game(); asleep.sleeping = true;
            Check(Say("grab", asleep).outcome == VoiceOutcome.Ignored && Say("start listening", asleep).outcome == VoiceOutcome.Execute, "asleep: only start listening");

            // V-05 to V-07 spelling with review; passwords excluded.
            Check(Say("grab", Field()).outcome == VoiceOutcome.Rejected, "commands are not spelling");
            Check(Say("alpha", Field(true)).outcome == VoiceOutcome.Rejected, "password field refuses voice");
            var buffer = new SpellingBuffer(); buffer.Begin("");
            string committed = null;
            foreach (var word in new[] { "alpha", "mike", "romeo", "oscar", "space", "f", "echo", "kilo", "romeo", "yankee", "yankee", "delete" })
            {
                var d = Say(word, Field());
                Check(d.outcome == VoiceOutcome.Execute, "spell '" + word + "'");
                buffer.Apply(d.command, d.letter, out committed);
            }
            Check(buffer.Text == "Amro Fekry", "spelled text with capitals and a correction: " + buffer.Text);
            buffer.Apply(VoiceCommand.Confirm, '\0', out committed);
            Check(committed == null, "nothing is entered before review");
            var done = Say("done", Field()); buffer.Apply(done.command, done.letter, out committed);
            Check(buffer.Reviewing && committed == null, "done shows the text for review");
            var yes = Say("confirm", Field()); buffer.Apply(yes.command, yes.letter, out committed);
            Check(committed == "Amro Fekry", "confirm enters the reviewed text");
            var low = Say("bravo", Field(), VoiceConfidence.Low);
            Check(low.outcome == VoiceOutcome.NeedsConfirmation && Say("confirm", Field()).letter == 'b', "unclear letter is confirmed first");
            Check(VoiceCommandRouter.TryLetter("x-ray", out var x) && x == 'x' && VoiceCommandRouter.TryLetter("Juliett", out var j) && j == 'j', "spelling variants");
            buffer.Begin(""); for (int i = 0; i < 60; i++) buffer.Apply(VoiceCommand.Letter, 'a', out _);
            Check(buffer.Text.Length == SpellingBuffer.MaxLength, "length is capped");

            Console.WriteLine("VOICE TESTS PASSED: " + passed);
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error.Message); return 1; }
    }
}
