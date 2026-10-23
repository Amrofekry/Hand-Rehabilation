using System;

namespace GripRehab
{
    // Frame boundary and held-input policy shared by player movement and grip.
    // No Unity calls: test the same policy without launching an Editor/player.
    public sealed class RehabKeyboardGate
    {
        int blockedThroughFrame = -1, sampledFrame = -1;
        bool spaceArmed, mouseArmed;
        public bool Allowed { get; private set; }
        public bool Squeezing { get; private set; }

        public void Block(int frame)
        {
            blockedThroughFrame = Math.Max(blockedThroughFrame, frame);
            Allowed = Squeezing = spaceArmed = mouseArmed = false;
        }

        public void Sample(int frame, bool contextAllowed, bool spaceHeld, bool mouseHeld)
        {
            if (!contextAllowed || frame <= blockedThroughFrame)
            {
                Block(frame); sampledFrame = frame; return;
            }
            if (sampledFrame == frame) return;
            sampledFrame = frame; Allowed = true;
            if (!spaceHeld) spaceArmed = true;
            if (!mouseHeld) mouseArmed = true;
            Squeezing = spaceHeld && spaceArmed || mouseHeld && mouseArmed;
        }

        public static bool TogglePause(bool escapeDown, bool tabDown,
                                       bool shellBlocks, bool textEntry, bool focused) =>
            focused && !shellBlocks && !textEntry && (escapeDown || tabDown);

        public static float AdjustSetpoint(float current, float wheel, bool increase,
                                           bool decrease, float seconds)
        {
            float delta = wheel * 3 + ((increase ? 1 : 0) - (decrease ? 1 : 0)) * 25 * Math.Max(0, seconds);
            return Math.Max(1, Math.Min(80, current + delta));
        }

        public static float DemoForce(bool demoMode, bool allowed, bool squeezing,
                                      bool maximum, float setpoint) =>
            demoMode && allowed && squeezing ? (maximum ? 80 : Math.Max(1, Math.Min(80, setpoint))) : 0;
    }
}
