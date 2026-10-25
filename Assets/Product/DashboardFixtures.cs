using System;
using System.Collections.Generic;

namespace GripRehab.Product
{
    /// <summary>
    /// Stage 6 gate: fabricated, clearly labelled demo records for walking the patient and doctor
    /// dashboards in an isolated store (launch flag -dashboard-fixtures). Never written to the
    /// player's real storage. Every name says "Fabricated" and every session is SYNTHETIC.
    /// </summary>
    public static class DashboardFixtures
    {
        public const string Password = "Fabricated-password-2026";
        public const string Doctor = "doctor.demo", Alpha = "patient.alpha", Beta = "patient.beta";

        sealed class Plan
        {
            public int module, task, score, level, adapted; public RecordState state; public float seconds, peak;
            public OutcomeMetric[] outcomes = new OutcomeMetric[0];
            public bool stop;
        }

        public static void Seed(ProductRepository repo)
        {
            if (repo == null) throw new ArgumentNullException(nameof(repo));
            if (!repo.NeedsDoctorSetup) throw new InvalidOperationException("Fixtures are only seeded into an empty, isolated store.");
            repo.Register(Doctor, "Fabricated Doctor (demo)", Password, LocalRole.Doctor);
            repo.Register(Alpha, "Fabricated Alpha (demo)", Password, LocalRole.Patient);
            repo.Register(Beta, "Fabricated Beta (demo)", Password, LocalRole.Patient);

            Sign(repo, Alpha); repo.LinkProfile("fabricated-profile-alpha");
            repo.SaveBaseline(Baseline(repo.Current.id, "fabricated-profile-alpha", 20));
            foreach (var plan in new[]
            {
                M1(62, 1, 0, 16.4f, 41, 14.2f, .081f, 1.10f),
                M1(74, 1, 0, 19.2f, 48, 18.6f, .062f, .92f),
                new Plan { module = 1, state = RecordState.SafetyStopped, level = 2, seconds = 44, peak = 12.5f, stop = true },
                M1(88, 1, 2, 22.0f, 55, 22.7f, .041f, .63f),
                new Plan { module = 2, task = 0, state = RecordState.Completed, score = 100, level = 1, seconds = 38,
                    outcomes = new[] { O("memory.accuracy", "Answer accuracy", "%", 100f), O("memory.reactionSeconds", "Reaction time", "s", 1.8f), O("stars", "Stars", "", 3f) } },
                new Plan { module = 2, task = 1, state = RecordState.Interrupted, level = 1, seconds = 12 },
                new Plan { module = 3, task = 0, state = RecordState.Completed, score = 83, level = 1, seconds = 27,
                    outcomes = new[] { O("trace.accuracy", "On-line accuracy", "%", 83f), O("trace.meanDeviation", "Mean deviation", "% pad", 4.2f), O("stars", "Stars", "", 2f) } }
            }) Save(repo, plan);
            repo.SignOut();

            Sign(repo, Beta); repo.LinkProfile("fabricated-profile-beta");
            Save(repo, M1(50, 1, 0, 9.0f, 30, 8.0f, .12f, 1.6f));
            repo.SignOut();

            Sign(repo, Doctor);
            var alpha = Array.Find(repo.Patients(), p => p.username == Alpha);
            repo.AddNote(alpha.id, "Fabricated demo note: steady progress across three force sessions; one research safety stop reviewed.");
            repo.SignOut();
        }

        static Plan M1(int score, int level, int adapted, float peak, float effort, float endurance, float cv, float reaction) => new Plan
        {
            module = 1, state = RecordState.Completed, score = score, level = level, adapted = adapted, seconds = 60, peak = peak,
            outcomes = new[] { O("force.rounds", "Rounds completed", "", 3f), O("force.peakEffort", "Peak effort", "% ref", effort),
                O("force.enduranceSeconds", "Time in target band", "s", endurance), O("force.stabilityCv", "Grip steadiness (CV)", "", cv),
                O("force.reactionSeconds", "Mean time to target", "s", reaction), O("force.breaks", "Over-squeezes", "", 0f),
                O("stars", "Stars", "", score >= 85 ? 3 : score >= 65 ? 2 : 1) }
        };

        static OutcomeMetric O(string key, string label, string unit, float value) => new OutcomeMetric { key = key, label = label, unit = unit, value = value };

        static void Sign(ProductRepository repo, string user)
        { if (!repo.SignIn(user, Password)) throw new InvalidOperationException("Fixture sign-in failed for " + user); }

        static void Save(ProductRepository repo, Plan plan)
        {
            var s = repo.Begin(plan.module, plan.task, true);
            s.state = plan.state; s.score = plan.state == RecordState.Completed ? plan.score : 0; s.level = plan.level; s.adaptedLevel = plan.adapted;
            s.durationSeconds = plan.seconds; s.validSeconds = plan.seconds; s.activeSeconds = plan.module == 1 ? plan.seconds * .6f : 0;
            s.hasForce = plan.module == 1; s.peakNewtons = plan.module == 1 ? plan.peak : 0; s.meanClosure = .5f;
            if (plan.module == 1)
                for (int i = 0; i < 3; i++)
                    s.metrics.Add(new MetricRecord { seconds = i * 10, value = plan.peak * (.6f + .2f * i), unit = InputUnit.Newtons, source = "Keyboard demo",
                        synthetic = true, valid = true, shape = "Demo grip", calibrationRevision = "demo-1", handClosure = .5f, emotionClass = -1 });
            if (plan.stop)
            {
                s.policyEvents.Add(new PolicyEvent { seconds = 30, status = "warning", reason = "one persistent sign", settingsVersion = "research-ai-1", level = 1, signs = 1 });
                s.policyEvents.Add(new PolicyEvent { seconds = 40, status = "stopped", reason = "multiple independent signs", settingsVersion = "research-ai-1", level = 1, signs = 3 });
            }
            s.outcomes = new List<OutcomeMetric>();
            foreach (var o in plan.outcomes) s.outcomes.Add(O(o.key, o.label, o.unit, o.value));
            if (plan.level > 0) s.outcomes.Add(new OutcomeMetric { key = "level", label = "Level", unit = "", value = plan.level });
            repo.SaveSession(s);
        }

        static CapabilityProfile Baseline(string account, string profile, float newtons)
        {
            var trial = new CapabilityTrial(account, profile, true, 1);
            for (int i = 1; i <= 12; i++)
                trial.Step(new TrialSample { now = i * .1, capturedAt = i * .1, sequence = i, source = "Keyboard demo", shape = "Demo grip",
                    calibrationRevision = "demo-1", unit = InputUnit.Newtons, value = newtons, closure = .5f, speed = .1f, jitter = .01f,
                    valid = true, handValid = true, synthetic = true }, .1f);
            var b = trial.Finish(); b.mvcNewtons = newtons; return b;
        }
    }
}
