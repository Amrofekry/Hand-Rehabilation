using System;

namespace GripRehab
{
    /// <summary>How much the game helps the camera hand do what the patient means (a setting; Medium by default).</summary>
    public enum AssistLevel { Off = 0, Low = 1, Medium = 2, High = 3 }

    /// <summary>
    /// Intent assist (2026-11-21; plan Phase 4). Unity-free so the offline harness tests it. Each frame the caller lists
    /// the candidates the hand could be going for (objects to pick up, or places for the held object) as screen points in
    /// the hand mapping's units (u, v: -1..1 at the view edges), each with a prior (higher for what the activity needs
    /// next). The hand's screen point and its recent motion give every candidate a likelihood: near the hand, and in the
    /// direction it is moving (a kinematic, Bayesian target predictor). The belief is smoothed over a quarter second.
    ///
    /// Outputs, scaled by the level and only for a confident, nearby target:
    /// - OffsetU/V: a bounded pull of the virtual hand toward the target (never while the hand moves away from it);
    /// - CloseFactor: the grip-close threshold multiplier (a partial close grabs the intended object);
    /// - OpenFactor: how much of the gap between open and close thresholds a partial opening may skip (placing);
    /// - RangeScale: smaller real movements cover the screen.
    /// Everything assisted is reported by the caller separately; it is never the patient's own ability. No allocation.
    /// </summary>
    public sealed class IntentAssist
    {
        public const int MaxCandidates = 32;
        public AssistLevel Level = AssistLevel.Medium;

        // Tuning (screen units: 1 = from the centre to the view edge).
        public float distanceSigma = .32f;    // how far the hand may be from a target that still counts as near
        public float directionKappa = 2.5f;   // how strongly heading toward a target raises it
        public float minSpeed = .15f;         // below this the hand is "still" and heading is ignored (screen units/s)
        public float memorySeconds = .25f;    // belief smoothing while the hand moves
        public float stillMemorySeconds = 1.2f; // ...and once it slows down: the intent formed during the reach is kept
        public float lookaheadSeconds = .3f;  // nearness is judged where the moving hand is heading (end-point prediction)
        public float confidentAt = .5f;       // probability above which the target counts as the intent
        public float captureRadius = .45f;    // the pull starts inside this distance
        public float snapRadius = .10f;       // inside this the pull completes (Medium and High)
        public float maxPullPerSecond = 2.5f; // the offset never changes faster than this (screen units/s)
        public float awaySpeed = .35f;        // moving away this fast...
        public float awaySeconds = .25f;      // ...for this long releases the pull (tremor alone never does)

        readonly int[] ids = new int[MaxCandidates], lastIds = new int[MaxCandidates];
        readonly float[] cu = new float[MaxCandidates], cv = new float[MaxCandidates], prior = new float[MaxCandidates];
        readonly float[] score = new float[MaxCandidates], lastScore = new float[MaxCandidates];
        int count, lastCount;
        float hu, hv, velU, velV, offU, offV, awayFor;
        bool hasHand;

        /// <summary>Slot of the most likely candidate this frame (-1: none), its caller id and probability.</summary>
        public int Target { get; private set; } = -1;
        public int TargetId => Target >= 0 ? ids[Target] : -1;
        public float Confidence { get; private set; }
        /// <summary>Distance (screen units) from the hand to the target; +inf without one.</summary>
        public float TargetDistance { get; private set; } = float.PositiveInfinity;
        public float OffsetU => offU;
        public float OffsetV => offV;
        public float Speed => (float)Math.Sqrt(velU * velU + velV * velV);
        /// <summary>The pull is moving the hand now (for counting assisted actions).</summary>
        public bool Pulling => offU * offU + offV * offV > 1e-4f;
        /// <summary>A confident target near the hand: grip and release help apply.</summary>
        public bool Engaged => Level != AssistLevel.Off && Target >= 0 && Confidence >= confidentAt && TargetDistance <= captureRadius;

        static readonly float[] PullGain = { 0f, .3f, .6f, .85f };
        static readonly float[] CloseFactors = { 1f, .9f, .75f, .6f };
        static readonly float[] OpenFactors = { 0f, .2f, .4f, .6f };
        static readonly float[] RangeScales = { 1f, 1f, .85f, .7f };

        public float CloseFactor => Engaged ? CloseFactors[(int)Level] : 1f;
        public float OpenFactor => Engaged ? OpenFactors[(int)Level] : 0f;
        public float RangeScale => RangeScales[Math.Max(0, Math.Min(3, (int)Level))];

        public void BeginFrame() { count = 0; }

        /// <summary>One candidate this frame. id: the caller's stable identity (keeps the belief across frames).</summary>
        public void AddCandidate(int id, float u, float v, float priorWeight = 1f)
        {
            if (count >= MaxCandidates || !Finite(u) || !Finite(v) || !(priorWeight > 0) || float.IsInfinity(priorWeight)) return;
            ids[count] = id; cu[count] = u; cv[count] = v; prior[count] = priorWeight; count++;
        }

        public void Reset()
        {
            count = lastCount = 0; Target = -1; Confidence = 0; TargetDistance = float.PositiveInfinity;
            hasHand = false; velU = velV = offU = offV = awayFor = 0;
        }

        /// <summary>Advance one frame. handU/V: the mapped hand before any assist; usable: tracking valid, game running.</summary>
        public void Step(float handU, float handV, float dt, bool usable)
        {
            if (!(dt > 0) || dt > .5f) dt = 0;
            if (!usable || !Finite(handU) || !Finite(handV) || Level == AssistLevel.Off || count == 0)
            {
                Decay(dt); Target = -1; Confidence = 0; TargetDistance = float.PositiveInfinity;
                if (!usable || !Finite(handU) || !Finite(handV)) { hasHand = false; velU = velV = 0; }
                else { hu = handU; hv = handV; hasHand = true; }
                Remember(); return;
            }
            // Hand velocity (exponential average over about 0.1 s).
            if (hasHand && dt > 0)
            {
                float k = 1 - (float)Math.Exp(-dt / .2f); // 0.2 s: shake averages out, a reach does not
                velU += ((handU - hu) / dt - velU) * k; velV += ((handV - hv) / dt - velV) * k;
            }
            hu = handU; hv = handV; hasHand = true;
            float speed = Speed, heading = speed > minSpeed ? directionKappa * Math.Min(1f, speed) : 0f;
            // A patient often stops short of the object: the belief built while reaching changes slowly once the hand slows
            // (activity probe 2026-11-21: a reach stopped at 70 % picked the neighbour once nearness took over).
            float memory = speed > minSpeed ? memorySeconds : stillMemorySeconds;
            float blend = dt > 0 ? 1 - (float)Math.Exp(-dt / Math.Max(1e-3f, memory)) : 1f;
            float pu = hu + velU * lookaheadSeconds * (speed > minSpeed ? 1 : 0), pv = hv + velV * lookaheadSeconds * (speed > minSpeed ? 1 : 0);
            float best = float.NegativeInfinity; int bestAt = -1;
            for (int i = 0; i < count; i++)
            {
                float du = cu[i] - hu, dv = cv[i] - hv, d = (float)Math.Sqrt(du * du + dv * dv);
                float eu = cu[i] - pu, ev = cv[i] - pv, e2 = eu * eu + ev * ev; // distance to where the hand is heading
                float like = -e2 / (2 * distanceSigma * distanceSigma) + (float)Math.Log(prior[i]);
                if (heading > 0 && d > 1e-4f) like += heading * ((du * velU + dv * velV) / (d * speed) - 1);
                float previous = Previous(ids[i]);
                score[i] = float.IsNaN(previous) ? like : previous + (like - previous) * blend;
                if (score[i] > best) { best = score[i]; bestAt = i; }
            }
            // Softmax probability of the best candidate.
            double sum = 0;
            for (int i = 0; i < count; i++) sum += Math.Exp(score[i] - best);
            Target = bestAt; Confidence = (float)(1 / sum);
            float tu = cu[bestAt] - hu, tv = cv[bestAt] - hv; TargetDistance = (float)Math.Sqrt(tu * tu + tv * tv);
            Pull(tu, tv, speed, dt);
            Remember();
        }

        void Pull(float tu, float tv, float speed, float dt)
        {
            float wantU = 0, wantV = 0, d = TargetDistance;
            // Only a deliberate move away (fast and sustained) releases the pull; tremor makes the heading jump about.
            awayFor = speed > awaySpeed && tu * velU + tv * velV < 0 ? awayFor + dt : 0;
            bool away = awayFor >= awaySeconds;
            if (Engaged && !away)
            {
                float conf = Clamp01((Confidence - (confidentAt - .1f)) / .3f);
                float near = d <= captureRadius * .6f ? 1f : Clamp01((captureRadius - d) / (captureRadius * .4f));
                float gain = PullGain[(int)Level] * conf * near;
                if (d <= snapRadius && Level >= AssistLevel.Medium) gain = 1f;
                wantU = tu * gain; wantV = tv * gain;
            }
            float step = maxPullPerSecond * dt, du = wantU - offU, dv = wantV - offV, m = (float)Math.Sqrt(du * du + dv * dv);
            if (m > step && m > 0) { du *= step / m; dv *= step / m; }
            offU += du; offV += dv;
        }

        void Decay(float dt)
        {
            float step = maxPullPerSecond * dt, m = (float)Math.Sqrt(offU * offU + offV * offV);
            if (m <= step) { offU = offV = 0; return; }
            offU -= offU / m * step; offV -= offV / m * step;
        }

        float Previous(int id)
        {
            for (int j = 0; j < lastCount; j++) if (lastIds[j] == id) return lastScore[j];
            return float.NaN;
        }

        void Remember()
        {
            for (int i = 0; i < count; i++) { lastIds[i] = ids[i]; lastScore[i] = score[i]; }
            lastCount = count;
        }

        static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
