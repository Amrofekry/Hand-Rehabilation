using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Runtime.Serialization.Json;
using GripRehab;
using GripRehab.Product;

// Fabricated accounts only. Never opens Application.persistentDataPath or a user's catalog.
static class ProductTests
{
    const string Secret = "Fabricated-password-2026";
    static int passed, failed;
    static PasswordVerifier verifier;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static void Test(string name, Action action)
    { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    sealed class Clock : IProductClock { public double now; public double Seconds => now; public string Utc => "2026-09-30T12:00:00.0000000Z"; }
    sealed class MemoryStore : IProductPersistence
    {
        public LocalProductCatalog data = new LocalProductCatalog(); public bool fail;
        public LocalProductCatalog Clone(LocalProductCatalog source)
        {
            using (var stream = new MemoryStream())
            { var codec = new DataContractJsonSerializer(typeof(LocalProductCatalog)); codec.WriteObject(stream, source); stream.Position = 0; return (LocalProductCatalog)codec.ReadObject(stream); }
        }
        public LocalProductCatalog Load() => Clone(data);
        public void Save(LocalProductCatalog value) { if (fail) throw new IOException("Injected storage fault"); data = Clone(value); }
    }
    sealed class Fixture
    {
        public readonly MemoryStore store = new MemoryStore(); public readonly Clock clock = new Clock(); public readonly ProductRepository repo;
        public Fixture()
        {
            foreach (var pair in new[] { "patient.one", "patient.two", "doctor.one" })
                store.data.accounts.Add(new LocalAccount { id = pair, username = pair, displayName = pair, profileId = pair == "doctor.one" ? null : "profile-" + pair,
                    role = pair == "doctor.one" ? LocalRole.Doctor : LocalRole.Patient, password = verifier, createdUtc = clock.Utc });
            repo = new ProductRepository(store, clock);
        }
        public void Login(string id = "patient.one") { Check(repo.SignIn(id, Secret), "Fixture sign in"); }
    }
    static TrialSample Sample(int i, InputUnit unit = InputUnit.Newtons, float value = 20)
        => new TrialSample { now = i * .1, capturedAt = i * .1, sequence = i, source = "Test simulator", shape = "Sphere", calibrationRevision = "1", unit = unit,
            value = value, closure = .5f, speed = .1f, jitter = .01f, valid = true, handValid = true, synthetic = true };
    static CapabilityProfile Baseline(InputUnit unit = InputUnit.Newtons)
    {
        var trial = new CapabilityTrial("patient.one", "profile-patient.one", true, 1);
        for (int i = 1; i <= 12; i++) trial.Step(Sample(i, unit, unit == InputUnit.Newtons ? 20 : .5f), .1f);
        return trial.Finish();
    }
    static GuidanceLimits Limits() => new GuidanceLimits { persistence = 1, stableSeconds = 2, cooldown = 3 };
    static int Main()
    {
        verifier = PasswordKdf.Create(Secret);
        Test("PBKDF2 password verification", () => { Check(PasswordKdf.Verify(Secret, verifier), "Correct password"); Check(!PasswordKdf.Verify("wrong", verifier), "Wrong password"); Check(verifier.hashBase64 != Secret, "No plaintext password"); });
        Test("Verifier corruption and iteration bounds", () => {
            Check(!PasswordKdf.Verify(Secret, new PasswordVerifier { iterations = 10000000, saltBase64 = verifier.saltBase64, hashBase64 = verifier.hashBase64 }), "Iteration cap");
            Check(!PasswordKdf.Verify(Secret, new PasswordVerifier { iterations = verifier.iterations, saltBase64 = "invalid", hashBase64 = verifier.hashBase64 }), "Broken salt");
            Reject<ArgumentException>(() => PasswordKdf.Create("short")); });
        Test("Patient cannot read another patient's results", () => { var f = new Fixture(); f.Login(); Reject<UnauthorizedAccessException>(() => f.repo.Sessions("patient.two")); Reject<UnauthorizedAccessException>(() => f.repo.Patients()); });
        Test("Returned account cannot grant doctor privileges", () => { var f = new Fixture(); f.Login(); f.repo.Current.role = LocalRole.Doctor; Reject<UnauthorizedAccessException>(() => f.repo.Patients()); Check(f.repo.Current.password == null, "Password verifier not exposed"); });
        Test("Doctor can read patients and author audited notes", () => { var f = new Fixture(); f.Login("doctor.one"); Check(f.repo.Patients().Length == 2, "Patients listed"); f.repo.AddNote("patient.one", "Fabricated research note"); var note = f.repo.Notes("patient.one")[0]; Check(note.doctorId == "doctor.one" && note.createdUtc == f.clock.Utc, "Author and time retained"); Reject<UnauthorizedAccessException>(() => f.repo.Begin(1, 0, true)); });
        Test("Patient cannot create an additional doctor", () => { var f = new Fixture(); f.Login(); Reject<UnauthorizedAccessException>(() => f.repo.Register("new.doctor", "Doctor", Secret, LocalRole.Doctor)); });
        Test("First doctor setup is explicit and persisted", () => { var store = new MemoryStore(); var repo = new ProductRepository(store, new Clock()); Check(repo.NeedsDoctorSetup, "Setup offered"); repo.Register("first.doctor", "Fabricated Doctor", Secret, LocalRole.Doctor); Check(!repo.NeedsDoctorSetup && store.data.accounts.Count == 1, "Setup saved"); });
        Test("Sign-in throttle and sign-out", () => { var f = new Fixture(); for (int i = 0; i < 5; i++) Check(!f.repo.SignIn("unknown.user", Secret), "Unknown login denied"); f.repo.Register("unknown.user", "Fabricated User", Secret, LocalRole.Patient); Check(!f.repo.SignIn("unknown.user", Secret), "Correct login throttled"); f.clock.now = 31; Check(f.repo.SignIn("unknown.user", Secret), "Throttle expires"); f.repo.SignOut(); Reject<UnauthorizedAccessException>(() => f.repo.Begin(1, 0, true)); });
        Test("Duplicate profile assignment rejected", () => { var f = new Fixture(); f.Login(); Reject<ArgumentException>(() => f.repo.LinkProfile("profile-patient.two")); Reject<UnauthorizedAccessException>(() => f.repo.LinkProfile("imported", true)); });
        Test("Failed save does not change in-memory account", () => { var f = new Fixture(); f.Login(); f.store.fail = true; Reject<IOException>(() => f.repo.LinkProfile("replacement")); Check(f.repo.Current.profileId == "profile-patient.one", "Rollback retained profile"); });
        Test("Session snapshot prevents alias mutation", () => { var f = new Fixture(); f.Login(); var s = f.repo.Begin(2, 0, true); s.score = 77; Check(f.repo.Sessions("patient.one")[0].score == 0, "Begin isolated"); s.durationSeconds = 2; s.state = RecordState.Completed; f.repo.SaveSession(s); s.score = 1; var snapshot = f.repo.Sessions("patient.one"); snapshot[0].score = 5; Check(f.repo.Sessions("patient.one")[0].score == 77, "Stored score isolated"); });
        Test("Session identity and terminal state immutable", () => { var f = new Fixture(); f.Login(); var s = f.repo.Begin(1, 0, true); s.module = 3; Reject<ArgumentException>(() => f.repo.SaveSession(s)); s.module = 1; s.durationSeconds = 1; s.state = RecordState.Completed; f.repo.SaveSession(s); s.state = RecordState.InProgress; Reject<ArgumentException>(() => f.repo.SaveSession(s)); });
        Test("Invalid and excessive metrics rejected", () => { var f = new Fixture(); f.Login(); var s = f.repo.Begin(1, 0, true); s.durationSeconds = float.NaN; Reject<ArgumentException>(() => f.repo.SaveSession(s)); s.durationSeconds = 1; s.validSeconds = 2; Reject<ArgumentException>(() => f.repo.SaveSession(s)); s.validSeconds = 0; s.metrics.Add(new MetricRecord { seconds = 0, unit = InputUnit.ClosureRatio, source = "Camera", value = 3 }); Reject<ArgumentException>(() => f.repo.SaveSession(s)); });
        Test("Baseline source and ownership preserved", () => { var f = new Fixture(); f.Login(); var b = Baseline(); f.repo.SaveBaseline(b); b.meanNewtons = 99; Check(f.repo.LatestBaseline("patient.one").meanNewtons == 20, "Baseline isolated"); b.accountId = "patient.two"; Reject<UnauthorizedAccessException>(() => f.repo.SaveBaseline(b)); });
        Test("Camera baseline never stores inferred force", () => { var b = Baseline(InputUnit.ClosureRatio); Check(!b.hasForce && b.peakNewtons == 0 && b.meanClosure == .5f, "Camera unit provenance"); var f = new Fixture(); f.Login(); b.hasForce = true; Reject<ArgumentException>(() => f.repo.SaveBaseline(b)); });
        Test("Catalog corruption fails closed", () => { var f = new Fixture(); var broken = f.store.Clone(f.store.data); broken.accounts[0].password.hashBase64 = "broken"; Reject<ArgumentException>(() => ProductRepository.Validate(broken)); });
        Test("500 ms freshness and invalid sample bounds", () => { var s = Sample(1); s.now = .6; Check(!s.Fresh, "Boundary expires"); s.now = .59; Check(s.Fresh, "Below boundary"); s.value = float.NaN; Check(!s.Fresh, "NaN rejected"); s = Sample(1, InputUnit.ClosureRatio, 1.1f); Check(!s.Fresh, "Closure range"); });
        Test("Baseline rejects duplicates and stale samples", () => { var trial = new CapabilityTrial("p", "profile", true, 1); var s = Sample(1); trial.Step(s, .1f); trial.Step(s, .1f); s.now += 1; trial.Step(s, .1f); Check(trial.Progress < .11f && !trial.Complete, "Rejected input cannot advance"); });
        Test("Baseline calibration change requires restart", () => { var trial = new CapabilityTrial("p", "profile", true, 1); trial.Step(Sample(1), .1f); var s = Sample(2); s.calibrationRevision = "2"; trial.Step(s, .1f); Check(trial.Problem != null && !trial.Complete, "Calibration invalidates"); Reject<InvalidOperationException>(() => trial.Finish()); });
        Test("Baseline frame hitch cannot fake elapsed time", () => { var trial = new CapabilityTrial("p", "profile", true, 1); trial.Step(Sample(1), 20); Check(trial.Progress <= .11f, "Hitch cap"); Reject<InvalidOperationException>(() => new CapabilityTrial("p", "profile", false)); });
        Test("Window ignores duplicated sensor samples", () => { var window = new FeatureWindow(); var s = Sample(1); for (int i = 0; i < 20; i++) window.Add(s); Check(!window.Read(.2).forceValid && window.Read(.2).count == 1, "No repeated sample inflation"); });
        Test("Window resets across source/calibration changes", () => { var window = new FeatureWindow(); for (int i = 1; i <= 12; i++) window.Add(Sample(i)); Check(window.Read(1.25).forceValid, "Initial window"); var s = Sample(13); s.calibrationRevision = "2"; window.Add(s); Check(!window.Read(1.35).forceValid && window.Read(1.35).count == 1, "Calibration reset"); Check(window.Read(20).count == 0, "Old window expires"); });
        Test("Bad hand kinematics cannot poison the window", () => { var window = new FeatureWindow(); for (int i = 1; i <= 12; i++) { var s = Sample(i); s.jitter = float.NaN; window.Add(s); } Check(!window.Read(1.2).motionValid && !float.IsNaN(window.Read(1.2).jitter), "Invalid kinematics ignored"); });
        Test("Force and motion are independent sign categories", () => { var b = Baseline(); var f = new WindowFeatures { forceValid = true, motionValid = true, forceMean = 10, jitter = .1f }; Check(StatisticalPredictor.Evaluate(f, b, Limits()) == (ResearchSigns.Force | ResearchSigns.Motion), "Two distinct categories"); });
        Test("One persistent sign pauses until explicit resume", () => { var p = new GuidancePolicy(Limits()); p.Step(0, true, false, ResearchSigns.Force); p.Step(1.1, true, false, ResearchSigns.Force); Check(p.State == GuidanceState.Warning, "Warning"); Reject<InvalidOperationException>(() => p.Resume()); p.Step(1.2, true, false, ResearchSigns.None); Check(p.State == GuidanceState.Warning, "No auto-resume"); p.Resume(); Check(p.State == GuidanceState.Active, "Explicit resume"); });
        Test("Two persistent signs stop and remain latched", () => { var p = new GuidancePolicy(Limits()); p.Step(0, true, false, ResearchSigns.Force | ResearchSigns.Motion); p.Step(1.1, true, false, ResearchSigns.Force | ResearchSigns.Motion); Check(p.State == GuidanceState.Stopped, "Stopped"); p.Step(2, true, true, ResearchSigns.None); Check(p.State == GuidanceState.Stopped, "Latched stop"); Reject<InvalidOperationException>(() => p.Resume()); });
        Test("Invalid inputs cannot increase difficulty", () => { var p = new GuidancePolicy(Limits()); p.Step(0, false, true, ResearchSigns.None); p.Step(100, false, true, ResearchSigns.None); Check(p.Level == 1 && p.State == GuidanceState.MissingInput, "No increase"); });
        Test("Difficulty cooldown and upper bound", () => { var p = new GuidancePolicy(Limits()); p.Step(0, true, true, ResearchSigns.None); p.Step(2.1, true, true, ResearchSigns.None); Check(p.Level == 2, "Stable increase"); p.Step(4.2, true, true, ResearchSigns.None); Check(p.Level == 2, "Cooldown respected"); for (int i = 5; i < 100; i++) p.Step(i, true, true, ResearchSigns.None); Check(p.Level == 5, "Bounded maximum"); });
        Test("Bad policy clock and threshold settings rejected", () => { var p = new GuidancePolicy(Limits()); p.Step(1, true, false, ResearchSigns.None); Reject<ArgumentException>(() => p.Step(1, true, false, ResearchSigns.None)); Reject<ArgumentException>(() => new GuidancePolicy(new GuidanceLimits { forceDecline = float.NaN })); });
        Test("CSV formula escaping and numeric culture", () => { Check(ResearchExport.CsvCell(" =SUM(1,2)").StartsWith("\"'"), "Formula guarded"); Check(ResearchExport.CsvCell("a\"b") == "\"a\"\"b\"", "Quote escaping"); var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); var csv = ResearchExport.SessionsCsv(new[] { new SessionRecord { id = "fabricated", durationSeconds = 1.5f, hasForce = false } }); Check(csv.Contains(",1.5,") && csv.Contains(",0,,0"), "Invariant numbers; absent force blank"); } finally { CultureInfo.CurrentCulture = old; } });
        Test("PDF pagination and xref offsets", () => { var sessions = Enumerable.Range(0, 30).Select(i => new SessionRecord { id = "fabricated-" + i, startedUtc = "2026-09-30", module = 1, score = i, synthetic = true }).ToArray(); var bytes = ResearchExport.Pdf("fabricated-account", sessions); var pdf = Encoding.ASCII.GetString(bytes); Check(pdf.Contains("/Count 3") && pdf.Contains("SYNTHETIC"), "Pages and labels"); var marker = pdf.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10; int end = pdf.IndexOf('\n', marker); var xref = int.Parse(pdf.Substring(marker, end - marker)); Check(pdf.Substring(xref, 4) == "xref", "xref position"); var entries = pdf.Substring(xref).Split('\n'); int number = int.Parse(entries[1].Split(' ')[1]); for (int i = 1; i < number; i++) { int offset = int.Parse(entries[i + 2].Substring(0, 10)); Check(pdf.Substring(offset).StartsWith(i + " 0 obj"), "Object offset " + i); } File.WriteAllBytes("Logs/offline/fabricated-report.pdf", bytes); });
        Test("Windows DPAPI roundtrip and corruption rejection", () => { var clear = Encoding.UTF8.GetBytes("Fabricated test payload only"); var encrypted = WindowsProfileProtection.Protect(clear); Check(!clear.SequenceEqual(encrypted), "Encrypted bytes"); Check(clear.SequenceEqual(WindowsProfileProtection.Unprotect(encrypted)), "Round trip"); encrypted[encrypted.Length - 1] ^= 1; Reject<System.ComponentModel.Win32Exception>(() => WindowsProfileProtection.Unprotect(encrypted)); });
        Test("Pointer fires exactly once after five qualified seconds", () => { var gate = new RehabDwellGate(); int clicks = 0; for (int i = 0; i < 120; i++) if (gate.Step(7, true, .1f, 5, .2f)) clicks++; Check(clicks == 1 && gate.Latched, "One click per dwell"); gate.Step(0, false, .1f, 5, .2f); Check(!gate.Latched && gate.Elapsed == 0, "Loss resets"); });
        Test("Pointer gesture flicker pauses qualified dwell", () => { var gate = new RehabDwellGate(); for (int i = 0; i < 31; i++) gate.Step(7, true, .1f, 5, .2f); var prior = gate.Elapsed; Check(!gate.Step(7, false, .1f, 5, .2f) && gate.Elapsed == prior, "No unqualified click"); bool clicked = false; for (int i = 0; i < 21; i++) clicked |= gate.Step(7, true, .1f, 5, .2f); Check(clicked, "Resume preserved effort"); });
        Test("Pointer target changes and frame hitches cannot click early", () => { var gate = new RehabDwellGate(); for (int i = 0; i < 49; i++) gate.Step(1, true, .1f, 5, .2f); Check(!gate.Step(2, true, 10, 5, .2f) && gate.Elapsed == 0, "New target reset"); Check(!gate.Step(2, true, 10, 5, .2f) && gate.Elapsed <= .1f, "Hitch bounded"); Check(!gate.Step(2, true, .1f, float.NaN, .2f), "Invalid duration rejected"); });
        Test("Memory task repeatability, errors and completion", () => { var a = new SequenceMemoryTask(42); var b = new SequenceMemoryTask(42); Check(a.Sequence.SequenceEqual(b.Sequence), "Deterministic pattern"); a.Submit((a.Sequence[0] + 1) % 4); Check(a.Mistakes == 1 && a.Correct == 0, "Error resets sequence"); foreach (var token in a.Sequence) a.Submit(token); Check(a.Complete && !a.Submit(0), "Complete stays complete"); });
        Test("Spatial assembly rejects wrong piece and completes ordered set", () => { var task = new SpatialAssemblyTask(); Check(!task.Place(0) && task.Errors == 1, "Wrong order recorded"); foreach (int token in new[] { 2, 0, 3, 1 }) task.Place(token); Check(task.Complete, "Assembly completed"); });
        Test("Crash recovery is owner-scoped and terminal-state preserving", () => {
            var f = new Fixture(); f.Login(); var first = f.repo.Begin(1, 0, true);
            var finished = f.repo.Begin(2, 0, true); finished.state = RecordState.Completed; f.repo.SaveSession(finished);
            f.Login("patient.two"); var other = f.repo.Begin(3, 0, true);
            f.Login(); Check(f.repo.RecoverInterruptedSessions() == 1, "Only this patient's open record recovered");
            Check(f.repo.Sessions("patient.one").Single(s => s.id == first.id).state == RecordState.Interrupted, "Open record interrupted");
            Check(f.repo.Sessions("patient.one").Single(s => s.id == finished.id).state == RecordState.Completed, "Completed record retained");
            Check(f.repo.RecoverInterruptedSessions() == 0, "Recovery idempotent"); f.Login("patient.two");
            Check(f.repo.Sessions("patient.two").Single(s => s.id == other.id).state == RecordState.InProgress, "Other patient's record untouched");
        });
        Test("Crash recovery failure preserves the saved checkpoint", () => {
            var f = new Fixture(); f.Login(); var s = f.repo.Begin(1, 0, true); f.store.fail = true;
            Reject<IOException>(() => f.repo.RecoverInterruptedSessions());
            Check(f.repo.Sessions("patient.one").Single().state == RecordState.InProgress, "No false recovery after save failure");
            f.store.fail = false; Check(f.repo.RecoverInterruptedSessions() == 1, "Retry commits recovery");
        });
        Test("Patient session cannot start without a linked profile", () => {
            var f = new Fixture(); f.store.data.accounts.Find(a => a.id == "patient.one").profileId = null;
            var repo = new ProductRepository(f.store, f.clock); Check(repo.SignIn("patient.one", Secret), "Signed in");
            Reject<InvalidOperationException>(() => repo.Begin(1, 0, true)); Check(repo.Sessions("patient.one").Length == 0, "No ownerless session created");
        });
        // AUDIT A-002/A-020: a transient read failure must never roll the newest catalog back to .bak.
        Func<string, string> readText = file => { var text = File.ReadAllText(file); if (text.StartsWith("corrupt")) throw new InvalidDataException("Fabricated corruption"); return text; };
        Func<string> recoveryFolder = () => { var dir = Path.Combine(Path.GetTempPath(), "GripRehab-Recovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); return dir; };
        Test("Transient primary lock never restores the older backup", () => {
            var dir = recoveryFolder(); var path = Path.Combine(dir, "store.dat"); File.WriteAllText(path, "newest"); File.WriteAllText(path + ".bak", "older");
            int attempts = 0;
            Reject<IOException>(() => ProtectedFileRecovery.Load(path, f => { attempts++; if (f == path) throw new IOException("Fabricated sharing violation"); return File.ReadAllText(f); }, _ => { }));
            Check(attempts >= 3 && File.ReadAllText(path) == "newest", "Primary retained after retries");
            Check(Directory.GetFiles(dir).Length == 2, "No recovery copy written for a busy file"); Directory.Delete(dir, true);
        });
        Test("Primary lock released during retry loads the newest copy", () => {
            var dir = recoveryFolder(); var path = Path.Combine(dir, "store.dat"); File.WriteAllText(path, "newest"); File.WriteAllText(path + ".bak", "older");
            int attempts = 0;
            var loaded = ProtectedFileRecovery.Load(path, f => { if (f == path && attempts++ < 2) throw new IOException("Fabricated sharing violation"); return File.ReadAllText(f); }, _ => { });
            Check(loaded == "newest" && File.ReadAllText(path) == "newest", "Newest copy used"); Directory.Delete(dir, true);
        });
        Test("Corrupt primary is preserved before backup recovery", () => {
            var dir = recoveryFolder(); var path = Path.Combine(dir, "store.dat"); File.WriteAllText(path, "corrupt-bytes"); File.WriteAllText(path + ".bak", "older");
            var loaded = ProtectedFileRecovery.Load(path, readText, _ => { });
            var preserved = Directory.GetFiles(dir, "store.dat.unreadable-*");
            Check(loaded == "older" && File.ReadAllText(path) == "older", "Backup restored");
            Check(preserved.Length == 1 && File.ReadAllText(preserved[0]) == "corrupt-bytes", "Unreadable primary kept for recovery"); Directory.Delete(dir, true);
        });
        Test("Missing primary is restored from its backup", () => {
            var dir = recoveryFolder(); var path = Path.Combine(dir, "store.dat"); File.WriteAllText(path + ".bak", "older");
            Check(ProtectedFileRecovery.Load(path, readText, _ => { }) == "older" && File.ReadAllText(path) == "older", "Primary recreated"); Directory.Delete(dir, true);
        });
        Test("Both copies unreadable fails closed without replacing the primary", () => {
            var dir = recoveryFolder(); var path = Path.Combine(dir, "store.dat"); File.WriteAllText(path, "corrupt-a"); File.WriteAllText(path + ".bak", "corrupt-b");
            Reject<IOException>(() => ProtectedFileRecovery.Load(path, readText, _ => { }));
            Check(File.ReadAllText(path) == "corrupt-a" && File.ReadAllText(path + ".bak") == "corrupt-b", "Originals untouched"); Directory.Delete(dir, true);
        });
        Test("No files returns the empty default", () => {
            var dir = recoveryFolder(); Check(ProtectedFileRecovery.Load(Path.Combine(dir, "store.dat"), readText, _ => { }) == null, "Nothing to load"); Directory.Delete(dir, true);
        });
        // STAGE 3.0: one difficulty scale; level 1 keeps the legacy exercises unchanged.
        Test("Exercise tuning keeps level 1 legacy values and stays bounded", () => {
            Check(ExerciseTuning.Clamp(-5) == 1 && ExerciseTuning.Clamp(99) == 10, "Levels clamp to 1..10");
            Check(ExerciseTuning.MemoryLength(1) == 4 && ExerciseTuning.MemoryLength(10) == 8, "Memory 4..8");
            Check(ExerciseTuning.AssemblyPieces(1) == 4 && ExerciseTuning.AssemblyPieces(10) == 6, "Assembly 4..6");
            Check(Math.Abs(ExerciseTuning.TraceTolerance(1) - .12f) < 1e-6 && ExerciseTuning.TraceTolerance(10) < ExerciseTuning.TraceTolerance(1), "Trace tolerance tightens");
            Check(ExerciseTuning.MazeCells(1) == 0 && ExerciseTuning.MazeCells(2) >= 5 && ExerciseTuning.MazeCells(10) <= 13 && ExerciseTuning.MazeCells(10) % 2 == 1, "Maze 0 = legacy map, then odd sizes");
            for (int level = 1; level <= 10; level++)
            {
                var f = ExerciseTuning.Force(level);
                Check(f.rounds >= 3 && f.bandLow > 0 && f.bandHigh > f.bandLow && f.breakAt > f.bandHigh && f.breakAt <= 1 && f.holdSeconds >= 2 && f.pulses >= 3, "Force tuning sane at " + level);
            }
            Check(ExerciseTuning.NextLevel(3, 90) == 4 && ExerciseTuning.NextLevel(3, 40) == 2 && ExerciseTuning.NextLevel(3, 70) == 3 && ExerciseTuning.NextLevel(10, 100) == 10 && ExerciseTuning.NextLevel(1, 0) == 1, "Progression is bounded");
        });
        // STAGE 3.1: Module 1 rounds (hold, control without breaking, pulse rhythm).
        Func<ForceRoundSession, float, float, int> drive = (session, effort, seconds) => { int steps = (int)Math.Round(seconds / .1f); for (int i = 0; i < steps && !session.Complete; i++) session.Step(effort, .1f); return steps; };
        Test("Force rounds: steady in-band effort completes every round with three stars", () => {
            var tuning = ExerciseTuning.Force(1); var s = new ForceRoundSession(tuning);
            float mid = (tuning.bandLow + tuning.bandHigh) / 2;
            for (int guard = 0; guard < 3000 && !s.Complete; guard++)
            {
                if (s.Current.Kind == ForceRoundKind.Pulse) { drive(s, mid, .5f); drive(s, 0, .4f); }
                else s.Step(mid, .1f);
            }
            Check(s.Complete && s.RoundsCompleted == s.RoundCount && s.Score >= 85 && s.Stars == 3, "Clean session " + s.Score + "/" + s.Stars);
            Check(s.InBandSeconds > 0 && s.PeakEffort >= mid - 1e-4 && s.MeanReactionSeconds < 1, "Metrics recorded");
            var keys = s.Outcomes().Select(o => o.key).ToArray();
            Check(keys.Contains("force.peakEffort") && keys.Contains("force.enduranceSeconds") && keys.Contains("force.stabilityCv") && keys.Contains("force.reactionSeconds") && keys.Contains("force.rounds"), "PDF metrics present: " + string.Join(",", keys));
        });
        Test("Force rounds: idle input never completes and times out with a low score", () => {
            var s = new ForceRoundSession(ExerciseTuning.Force(1));
            drive(s, 0, 600);
            Check(s.Complete && s.RoundsCompleted == 0 && s.Score <= 25 && s.Stars <= 1, "Idle session " + s.Score);
        });
        Test("Force rounds: squeezing past the break point in a control round restarts it", () => {
            var tuning = ExerciseTuning.Force(1); var s = new ForceRoundSession(tuning);
            float mid = (tuning.bandLow + tuning.bandHigh) / 2;
            while (s.Current.Kind != ForceRoundKind.Control && !s.Complete) s.Step(mid, .1f);
            Check(!s.Complete, "Reached a control round");
            drive(s, mid, 1); var before = s.Current.Progress; s.Step(tuning.breakAt + .05f, .1f);
            Check(s.Breaks == 1 && s.Current.Progress == 0 && before > 0, "Break restarts the hold");
        });
        Test("Force rounds: one sustained over-squeeze counts as one crack (playtest)", () => {
            var tuning = ExerciseTuning.Force(1); var s = new ForceRoundSession(tuning);
            float mid = (tuning.bandLow + tuning.bandHigh) / 2;
            while (s.Current.Kind != ForceRoundKind.Control && !s.Complete) s.Step(mid, .1f);
            drive(s, tuning.breakAt + .3f, 1.2f); Check(s.Breaks == 1, "Held over the limit = 1 crack (" + s.Breaks + ")");
            drive(s, mid, .3f); drive(s, tuning.breakAt + .3f, .3f); Check(s.Breaks == 2, "Dropping back then crossing again = 2 cracks");
        });
        Test("Force rounds: a pulse needs a real release between squeezes", () => {
            var tuning = ExerciseTuning.Force(1); var s = new ForceRoundSession(tuning);
            float mid = (tuning.bandLow + tuning.bandHigh) / 2;
            while (s.Current.Kind != ForceRoundKind.Pulse && !s.Complete) s.Step(mid, .1f);
            drive(s, mid, 5); Check(s.Current.PulseCount <= 1, "Holding is not pulsing (" + s.Current.PulseCount + ")");
            drive(s, 0, .4f); drive(s, mid, .5f); drive(s, 0, .4f); Check(s.Current.PulseCount >= 2, "Release then squeeze counts");
        });
        Test("Force rounds: invalid samples are ignored", () => {
            var s = new ForceRoundSession(ExerciseTuning.Force(1)); s.Step(float.NaN, .1f); s.Step(.5f, float.NaN); s.Step(-1, .1f); s.Step(.5f, -1);
            Check(s.Current.Elapsed == 0 && s.PeakEffort == 0, "Nothing advanced");
        });
        Test("Session outcomes are validated and optional for older records", () => {
            var f = new Fixture(); f.Login(); var session = f.repo.Begin(1, 0, true);
            session.outcomes = new System.Collections.Generic.List<OutcomeMetric> { new OutcomeMetric { key = "force.rounds", label = "Rounds", unit = "", value = 3 } };
            session.level = 2; f.repo.SaveSession(session);
            session.outcomes.Add(new OutcomeMetric { key = "bad", label = "Bad", unit = "", value = float.NaN });
            Reject<ArgumentException>(() => f.repo.SaveSession(session));
            session.outcomes = null; session.level = 0; f.repo.SaveSession(session);
        });
        // STAGE 3.2: drag-and-drop spatial assembly with placement metrics.
        Test("Assembly board: matching drops snap, wrong slots count errors and pieces return", () => {
            var board = new AssemblyBoard(4, 7);
            Check(board.Pieces.Count == 4 && board.Pieces.All(p => p.TargetX > .5f && p.StartX < .45f), "Pieces start left, slots are right");
            var a = board.Pieces[0]; var b = board.Pieces[1];
            board.Move(a.Id, b.TargetX, b.TargetY); Check(!board.Drop(a.Id, b.TargetX, b.TargetY) && board.Errors == 1 && a.X == a.StartX && a.Y == a.StartY, "Wrong slot returns the piece");
            board.Move(a.Id, .2f, .2f); Check(!board.Drop(a.Id, .2f, .2f) && board.Errors == 1, "Dropping in empty space is not an error");
            foreach (var piece in board.Pieces) { board.Move(piece.Id, (piece.StartX + piece.TargetX) / 2, piece.StartY); board.Move(piece.Id, piece.TargetX + .01f, piece.TargetY); Check(board.Drop(piece.Id, piece.TargetX + .01f, piece.TargetY), "Correct slot accepted"); }
            Check(board.Complete && board.MeanPlacementError > 0 && board.MeanPlacementError < .02f && board.PathEfficiency > .5f && board.PathEfficiency <= 1, "Metrics " + board.MeanPlacementError + " / " + board.PathEfficiency);
        });
        Test("Assembly board: tap piece then slot is an accessible alternative", () => {
            var board = new AssemblyBoard(5, 3);
            Check(!board.PlaceByTap(board.Pieces[0].Id, board.SlotOf(board.Pieces[1].Id)) && board.Errors == 1, "Wrong tap counts");
            foreach (var piece in board.Pieces) Check(board.PlaceByTap(piece.Id, board.SlotOf(piece.Id)), "Right tap places");
            Check(board.Complete && new AssemblyBoard(5, 3).Pieces.Select(p => p.StartY).SequenceEqual(new AssemblyBoard(5, 3).Pieces.Select(p => p.StartY)), "Seeded layout");
        });
        // AUDIT P-007: idle but fresh input is not active grip time.
        Test("Active grip needs real effort, not just fresh input", () => {
            Check(!SessionRecord.IsActiveGrip(Sample(1, InputUnit.Newtons, 0)), "Idle keyboard demo is not active");
            Check(SessionRecord.IsActiveGrip(Sample(1, InputUnit.Newtons, 18)), "Squeeze is active");
            Check(!SessionRecord.IsActiveGrip(Sample(1, InputUnit.ClosureRatio, .3f)), "Open hand is not active");
            Check(SessionRecord.IsActiveGrip(Sample(1, InputUnit.ClosureRatio, .7f)), "Closed hand is active");
            var stale = Sample(1, InputUnit.Newtons, 18); stale.valid = false; Check(!SessionRecord.IsActiveGrip(stale), "Invalid input is never active");
        });
        // AUDIT P-023: a correctly timed sustained grip must produce endurance seconds.
        Test("Staged baseline counts a sustained grip during the endurance stage", () => {
            var trial = new StagedCapabilityTrial("patient.one", "profile-patient.one", true);
            for (int i = 1; i <= 600 && !trial.Complete; i++)
            {
                double t = i * .1; bool gripStage = t < 3 || (t >= 8 && t < 11) || (t >= 16 && t < 19) || (t >= 34 && t < 44);
                var s = Sample(i, InputUnit.Newtons, gripStage ? 18 : 0); trial.Step(s, .1f);
            }
            var result = trial.Finish();
            Check(Math.Abs(result.mvcNewtons - 18) < .01f, "Peak from grip trials"); Check(result.enduranceSeconds > 9 && result.enduranceSeconds <= 10.1f, "Endurance ~10 s, got " + result.enduranceSeconds);
        });
        // ---------- Stage 5: closed adaptive loop and safety UX ----------
        Test("Adaptive difficulty moves one bounded step per decision", () => {
            var a = new AdaptiveDifficulty(5, 1);
            Check(!a.Apply(1) && a.Level == 5, "Same policy level holds");
            Check(a.Apply(3) && a.Level == 6, "A two-level jump moves one step"); Check(a.Apply(3) && a.Level == 7, "Then the next step");
            Check(!a.Apply(3) && a.Level == 7 && a.Changes == 2, "Holds at the target");
            for (int i = 0; i < 20; i++) a.Apply(50); Check(a.Level == 8, "At most three above the start, got " + a.Level);
            for (int i = 0; i < 20; i++) a.Apply(-50); Check(a.Level == 2, "At most three below the start, got " + a.Level);
            var low = new AdaptiveDifficulty(1, 1); for (int i = 0; i < 5; i++) low.Apply(-5); Check(low.Level == 1, "Never below level 1");
            var high = new AdaptiveDifficulty(10, 1); for (int i = 0; i < 5; i++) high.Apply(9); Check(high.Level == 10, "Never above level 10");
        });
        Test("Adaptive loop: a policy replay keeps every module inside its bounds", () => {
            var policy = new FatigueSafetyPolicy(new FatigueSafetyConfig { maximumLevel = 3 }); var adaptive = new AdaptiveDifficulty(3, policy.Level);
            var rounds = new ForceRoundSession(ExerciseTuning.Force(3)); int previous = adaptive.Level, ups = 0, downs = 0;
            for (int t = 0; t < 120; t++)
            {
                // SYNTHETIC replay: 60 s steady and clear, then a persistent force sign, then clear again.
                float force = t >= 60 && t < 64 ? .9f : .1f;
                var d = policy.Step(t, true, force, null, null, null, true);
                if (d.state == "warning" && policy.CanResume) policy.Resume();
                if (adaptive.Apply(d.level)) { rounds.Retune(ExerciseTuning.Force(adaptive.Level)); if (adaptive.Level > previous) ups++; else downs++; }
                int level = adaptive.Level;
                Check(Math.Abs(level - previous) <= 1 && Math.Abs(level - 3) <= AdaptiveDifficulty.MaxOffset && level >= 1 && level <= 10, "Bounded level at " + t);
                var f = ExerciseTuning.Force(level);
                Check(f.bandLow > 0 && f.bandHigh > f.bandLow && f.breakAt <= 1 && f.holdSeconds >= 2.5f && f.holdSeconds <= 6, "Module 1 band at " + t);
                Check(rounds.Rounds.All(r => r.BandLow > 0 && r.BandHigh > r.BandLow && r.BreakAt > r.BandHigh) && rounds.RoundCount == ExerciseTuning.Force(3).rounds, "Module 1 rounds at " + t);
                int memory = ExerciseTuning.MemoryLength(level), pieces = ExerciseTuning.AssemblyPieces(level), cells = ExerciseTuning.MazeCells(level);
                float tolerance = ExerciseTuning.TraceTolerance(level);
                Check(memory >= 4 && memory <= 8 && pieces >= 4 && pieces <= 6 && (cells == 0 || (cells % 2 == 1 && cells >= 5 && cells <= 13)) && tolerance >= .07f && tolerance <= .12f, "Module 2/3 settings at " + t);
                previous = level;
            }
            Check(ups >= 2 && downs >= 1, "The replay both raised and eased difficulty (" + ups + " up, " + downs + " down)");
        });
        Test("Force rounds: retune changes only rounds not yet started", () => {
            var start = ExerciseTuning.Force(1); var s = new ForceRoundSession(start); s.Step(.4f, .1f);
            var harder = ExerciseTuning.Force(4); int changed = s.Retune(harder);
            Check(changed == s.RoundCount - 1 && s.RoundCount == start.rounds, "Round in progress keeps its target; count unchanged");
            Check(s.Rounds[0].BandLow == start.bandLow && s.Rounds[1].BandLow == harder.bandLow && s.Rounds[2].HoldSeconds == harder.holdSeconds && s.CurrentTuning.bandLow == harder.bandLow, "Later rounds retuned");
            Reject<ArgumentOutOfRangeException>(() => s.Retune(new ForceTuning()));
        });
        Test("Next session level follows adaptation, score and safety stops", () => {
            Check(ExerciseTuning.NextSessionLevel(3, 0, 90, RecordState.Completed) == 4, "Strong result raises");
            Check(ExerciseTuning.NextSessionLevel(3, 5, 70, RecordState.Completed) == 5, "Adapted level is the new start");
            Check(ExerciseTuning.NextSessionLevel(3, 5, 90, RecordState.Completed) == 6, "Adapted level then follows the score");
            Check(ExerciseTuning.NextSessionLevel(3, 0, 0, RecordState.SafetyStopped) == 2, "Safety stop eases the next attempt");
            Check(ExerciseTuning.NextSessionLevel(1, 0, 0, RecordState.SafetyStopped) == 1, "Never below level 1");
            Check(ExerciseTuning.NextSessionLevel(4, 0, 0, RecordState.Interrupted) == 4, "Interrupted attempt keeps its level");
            Check(ExerciseTuning.NextSessionLevel(0, 0, 90, RecordState.Completed) == 2, "Older records count as level 1");
        });
        Test("Safety: simultaneous signs stop, latch and are explained", () => {
            var p = new FatigueSafetyPolicy(); FatigueSafetyDecision d = default;
            for (int i = 0; i < 3; i++) d = p.Step(i, true, .9f, .9f, null, null, false);
            Check(d.state == "stopped" && d.signs == 3, "Two categories stop");
            Check(p.Step(3, true, .1f, .1f, null, null, true).state == "stopped", "Stop latches"); Reject<InvalidOperationException>(() => p.Resume());
            var m = SafetyMessages.Describe(d, false);
            Check(m.stopped && !m.canResume && m.body.Contains("grip force") && m.body.Contains("movement") && m.body.Contains("will not restart"), "Plain-language stop: " + m.body);
            Check(SafetyMessages.Indicator.Contains("RESEARCH INDICATOR") && SafetyMessages.Indicator.Contains("not a medical"), "Labelled as a research indicator");
        });
        Test("Safety: stale or invalid predictions pause without a sign and never raise difficulty", () => {
            var p = new FatigueSafetyPolicy(); for (int i = 0; i < 20; i++) p.Step(i, true, .1f, null, null, null, true);
            int level = p.Level; Check(level >= 2, "Steady play raised difficulty first");
            var d = p.Step(20, true, float.NaN, null, null, null, true);
            Check(d.state == "warning" && d.signs == 0 && d.level <= level, "NaN prediction pauses with no sign");
            var m = SafetyMessages.Describe(d, false);
            Check(m.title.Contains("signal lost") && m.body.Contains("not counted as tiredness") && m.canResume, "Signal-lost wording: " + m.title);
            for (int i = 21; i < 40; i++) p.Step(i, false, null, null, null, null, true);
            Check(p.State == "warning" && p.Level <= level && !p.CanResume, "Bad input holds the pause and never raises difficulty");
            Reject<InvalidOperationException>(() => p.Resume());
            for (int i = 40; i < 43; i++) p.Step(i, true, .1f, null, null, null, true);
            Check(p.CanResume && p.Resume().state == "active", "Fresh settled windows allow an explicit resume");
        });
        Test("Safety: one sign warns, and the player can finish during the warning", () => {
            var p = new FatigueSafetyPolicy(); FatigueSafetyDecision d = default;
            for (int i = 0; i < 3; i++) d = p.Step(i, true, .9f, null, null, null, false);
            var m = SafetyMessages.Describe(d, false);
            Check(d.state == "warning" && d.signs == 1 && m.title == "Time for a short rest" && m.body.Contains("grip force") && m.canResume, "One sign warns: " + m.body);
            var q = p.Quit(); Check(q.state == "stopped" && p.Step(3, true, .1f, null, null, null, true).state == "stopped", "Quit latches");
            Reject<InvalidOperationException>(() => p.Resume());
            Check(SafetyMessages.Describe(q, false, true).title == "Session finished", "Player finish wording");
            var changed = SafetyMessages.Describe(d, true); Check(changed.title == "Input changed" && !changed.canResume, "Input change cannot resume");
        });
        Test("Proxy model never reads real or camera input", () => {
            Check(ProxyModelGuard.Applies(true, Sample(1)), "Synthetic Newtons with opt-in");
            var real = Sample(1); real.synthetic = false; Check(!ProxyModelGuard.Applies(true, real), "Real device force");
            Check(!ProxyModelGuard.Applies(true, Sample(1, InputUnit.ClosureRatio, .5f)), "Camera closure"); Check(!ProxyModelGuard.Applies(false, Sample(1)), "Opt-in required");
        });
        Test("Baseline match per Prepare input", () => {
            var b = Baseline(); b.source = "Keyboard demo";
            Check(BaselineMatch.ForInputMode(b, 1) && !BaselineMatch.ForInputMode(b, 0) && !BaselineMatch.ForInputMode(b, 2), "Keyboard baseline");
            b.source = "Phase 2"; Check(BaselineMatch.ForInputMode(b, 2), "Device baseline");
            var camera = Baseline(InputUnit.ClosureRatio); camera.source = "Camera / right";
            Check(BaselineMatch.ForInputMode(camera, 0) && !BaselineMatch.ForInputMode(camera, 1), "Camera baseline"); Check(!BaselineMatch.ForInputMode(null, 1), "No baseline");
        });
        Test("Baseline coach: stage quality and cues", () => {
            var trial = new StagedCapabilityTrial("patient.one", "profile-patient.one", true);
            for (int i = 1; i <= 20; i++) trial.Step(Sample(i, InputUnit.Newtons, 18), .1f);
            Check(trial.StageQuality > .85f && trial.Cue(0) == "SQUEEZE", "Steady signal is good quality");
            var gap = new StagedCapabilityTrial("patient.one", "profile-patient.one", true);
            for (int i = 1; i <= 10; i++) gap.Step(Sample(i, InputUnit.Newtons, 18), .1f);
            var repeat = Sample(10, InputUnit.Newtons, 18); for (int i = 0; i < 10; i++) gap.Step(repeat, .1f);
            Check(gap.StageQuality < .6f, "Missing samples lower quality, got " + gap.StageQuality);
            Check(StagedCapabilityTrial.QualityLabel(.9f) == "GOOD" && StagedCapabilityTrial.QualityLabel(.6f) == "FAIR" && StagedCapabilityTrial.QualityLabel(.2f).StartsWith("POOR"), "Quality labels");
        });
        // ---------- Stage 6: dashboards ----------
        Func<ProductRepository> Seeded = () => { var r = new ProductRepository(new MemoryStore(), new Clock()); DashboardFixtures.Seed(r); return r; };
        Func<ProductRepository, string, string> IdOf = (r, user) => { r.SignIn(DashboardFixtures.Doctor, DashboardFixtures.Password); var id = r.Patients().Single(p => p.username == user).id; r.SignOut(); return id; };
        Test("Dashboard fixtures reproduce exact patient card numbers", () => {
            var r = Seeded(); r.SignIn(DashboardFixtures.Alpha, DashboardFixtures.Password);
            var sessions = r.Sessions(r.Current.id); var cards = ProgressSummary.Cards(sessions);
            string Card(string label) => cards.Single(c => c.label == label).value;
            Check(Card("Force") == "55 % ref" && Card("Endurance") == "22.7 s" && Card("Stability") == "0.041 CV", "Module 1 cards: " + string.Join(", ", cards.Select(c => c.value)));
            Check(Card("Reaction time") == "1.80 s" && Card("Accuracy") == "83 %", "Latest reaction and accuracy: " + Card("Reaction time") + " / " + Card("Accuracy"));
            Check(ProgressSummary.BaselineComparison(sessions, r.LatestBaseline(r.Current.id)) == "22.0 N today vs 20.0 N baseline (110%) · SYNTHETIC", "Baseline comparison: " + ProgressSummary.BaselineComparison(sessions, r.LatestBaseline(r.Current.id)));
        });
        Test("Dashboard fixtures reproduce exact module statistics and safety events", () => {
            var r = Seeded(); r.SignIn(DashboardFixtures.Alpha, DashboardFixtures.Password);
            var sessions = r.Sessions(r.Current.id); var m = ProgressSummary.ByModule(sessions);
            Check(m[0].attempts == 4 && m[0].completed == 3 && m[0].safetyStops == 1 && m[0].sessionsWithPauses == 1 && m[0].latestScore == 88 && m[0].bestScore == 88 && m[0].latestLevel == 2, "Module 1 statistics");
            Check(m[0].scoreTrend.SequenceEqual(new[] { .62f, .74f, .88f }), "Module 1 trend uses completed scores only");
            Check(m[1].attempts == 2 && m[1].completed == 1 && m[1].incomplete == 1 && m[2].completed == 1 && m[2].latestScore == 83, "Modules 2 and 3");
            var events = ProgressSummary.SafetyEvents(sessions);
            Check(events.Count == 2 && events[0].Contains("PAUSE at 30 s") && events[0].Contains("grip force") && events[1].Contains("STOP at 40 s") && events[1].Contains("movement"), "Plain safety events: " + string.Join(" | ", events));
        });
        Test("Stage 10 activity sessions keep their named outcomes for the dashboards", () => {
            var f = new Fixture(); f.Login();
            Reject<ArgumentException>(() => f.repo.Begin(2, 2, true));
            Reject<ArgumentException>(() => f.repo.Begin(1, 8, true));
            var s = f.repo.Begin(1, 4, true); s.durationSeconds = 42; s.state = RecordState.Completed; s.score = 96; s.level = 3;
            s.outcomes = new System.Collections.Generic.List<OutcomeMetric> {
                new OutcomeMetric { key = "activity.id", label = "Activity 3: Pack the storage box", unit = "", value = 3 },
                new OutcomeMetric { key = "activity.accuracy", label = "Placement accuracy", unit = "%", value = 95.5f },
                new OutcomeMetric { key = "activity.errors", label = "Wrong placements", unit = "", value = 0 },
                new OutcomeMetric { key = "box.overlaps", label = "Items pressed into each other", unit = "", value = 1 },
                new OutcomeMetric { key = "stars", label = "Stars", unit = "", value = 3 } };
            f.repo.SaveSession(s);
            var stored = f.repo.Sessions("patient.one").Single(x => x.task == 4);
            Check(stored.outcomes.Any(o => o.label == "Activity 3: Pack the storage box") && stored.score == 96, "Activity outcome stored with its name");
            var pdf = Encoding.ASCII.GetString(ResearchExport.SessionPdf("fabricated", stored));
            Check(pdf.Contains("Activity 3: Pack the storage box") && pdf.Contains("Placement accuracy"), "Session PDF lists the activity outcomes");
            Check(ProgressSummary.ByModule(new[] { stored })[0].completed == 1, "Activity counts as Module 1 practice");
        });
        Test("Dashboards show missing data as a dash, never 0", () => {
            var r = Seeded(); r.SignIn(DashboardFixtures.Alpha, DashboardFixtures.Password);
            Check(ProgressSummary.Cards(new SessionRecord[0]).All(c => c.missing && c.value == ProgressSummary.Missing), "No sessions: every card is a dash");
            var stopped = r.Sessions(r.Current.id).Single(s => s.state == RecordState.SafetyStopped);
            Check(ProgressSummary.ScoreText(stopped) == ProgressSummary.Missing && ProgressSummary.StateLabel(stopped).StartsWith("STOPPED"), "Stopped attempt has no score and is marked");
            Check(ProgressSummary.BaselineComparison(new SessionRecord[0], null).StartsWith(ProgressSummary.Missing), "No baseline is a dash");
            var empty = ProgressSummary.ByModule(new SessionRecord[0]); Check(!empty[0].latestScore.HasValue && !empty[0].latestLevel.HasValue && empty[0].scoreTrend.Length == 0, "Empty module statistics stay empty");
            var pdf = Encoding.ASCII.GetString(ResearchExport.SessionPdf("fabricated", stopped));
            Check(pdf.Contains("not finished") && pdf.Contains("STOP") && pdf.Contains("startxref"), "Session PDF marks the stop and shows no score");
            Check(Encoding.ASCII.GetString(ResearchExport.Pdf("fabricated", new[] { stopped })).Contains("not finished"), "Report PDF shows no score for a stopped attempt");
        });
        Test("Dashboards never mix patients and enforce roles", () => {
            var r = Seeded(); string alpha = IdOf(r, DashboardFixtures.Alpha), beta = IdOf(r, DashboardFixtures.Beta);
            r.SignIn(DashboardFixtures.Beta, DashboardFixtures.Password);
            var cards = ProgressSummary.Cards(r.Sessions(beta));
            Check(cards.Single(c => c.label == "Force").value == "30 % ref" && cards.Single(c => c.label == "Accuracy").missing, "Beta sees only Beta's numbers");
            Reject<UnauthorizedAccessException>(() => r.Sessions(alpha)); Reject<UnauthorizedAccessException>(() => r.AuditTrail(beta)); Reject<UnauthorizedAccessException>(() => r.RecordAudit("viewed patient record", beta));
            r.SignOut(); r.SignIn(DashboardFixtures.Doctor, DashboardFixtures.Password);
            Check(r.Sessions(alpha).All(s => s.accountId == alpha) && r.Sessions(beta).All(s => s.accountId == beta) && r.Sessions(beta).Length == 1, "Doctor reads each record separately");
        });
        Test("Doctor audit trail records views, notes, exports and sign-in", () => {
            var r = Seeded(); string alpha = IdOf(r, DashboardFixtures.Alpha), beta = IdOf(r, DashboardFixtures.Beta);
            r.SignIn(DashboardFixtures.Doctor, DashboardFixtures.Password);
            r.RecordAudit("viewed patient record", alpha); r.RecordAudit("exported PDF report", alpha);
            var trail = r.AuditTrail(alpha);
            Check(trail.Select(e => e.action).SequenceEqual(new[] { "added a note", "viewed patient record", "exported PDF report" }) && trail.All(e => !string.IsNullOrEmpty(e.utc)), "Alpha trail: " + string.Join(", ", trail.Select(e => e.action)));
            Check(r.AuditTrail(beta).Length == 0, "Beta's trail has none of Alpha's entries");
            r.SignOut(); r.SignIn(DashboardFixtures.Doctor, DashboardFixtures.Password);
            var access = r.MyAccess().Select(e => e.action).ToArray();
            Check(access.Count(a => a == "signed in") >= 3 && access.Count(a => a == "signed out") >= 2, "Sign-in and sign-out recorded: " + string.Join(",", access));
            Reject<ArgumentException>(() => r.RecordAudit("", alpha));
        });
        Test("Older catalogs without an audit trail still load, and fixtures need an empty store", () => {
            var store = new MemoryStore(); var r = new ProductRepository(store, new Clock()); DashboardFixtures.Seed(r);
            store.data.audit = null;
            var reopened = new ProductRepository(store, new Clock());
            Check(reopened.SignIn(DashboardFixtures.Doctor, DashboardFixtures.Password) && reopened.MyAccess().Length == 1, "Audit restarts empty on an older catalog");
            Reject<InvalidOperationException>(() => DashboardFixtures.Seed(reopened));
        });
        // ---------- Stage 7: robustness ----------
        Test("Diagnostics log rotates, caps its size and scrubs secrets", () => {
            var folder = Path.Combine("Logs", "offline", "diag-" + Guid.NewGuid().ToString("N"));
            try
            {
                for (int i = 0; i < 7; i++) using (var old = new DiagnosticsLog(folder, new DateTime(2026, 10, 20 + i % 5, 10, i, 0, DateTimeKind.Utc))) old.Write(DateTime.UtcNow, "Info", "run " + i);
                Check(Directory.GetFiles(folder, "griprehab-*.log").Length <= DiagnosticsLog.KeepFiles, "Only the newest logs are kept");
                using (var log = new DiagnosticsLog(folder, new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc)))
                {
                    log.Write(DateTime.UtcNow, "Info", "Sign-in with password Fabricated-password-2026 typed");
                    log.Write(DateTime.UtcNow, "Error", "Save failed at " + Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\secret.dat", "stack line");
                    log.Write(DateTime.UtcNow, "Exception", "boom");
                    for (int i = 0; i < 5; i++) log.Write(DateTime.UtcNow, "Error", "[VISION_CAMERA] retry failed");
                    log.Write(DateTime.UtcNow, "Info", "something else");
                    Check(log.Errors == 6 && log.Recent().Count(l => l.Contains("retry failed")) == 1 && log.Recent().Any(l => l.Contains("repeated 4 more times")), "Repeated lines are written once, counted every time");
                    for (int i = 0; i < 400; i++) log.Write(DateTime.UtcNow, "Info", i + new string('x', 4000));
                    Check(log.Errors == 6 && log.Exceptions == 1 && log.Recent().Length == DiagnosticsLog.RecentLines, "Counters and recent lines");
                    log.Dispose();
                    var text = File.ReadAllText(log.FilePath);
                    Check(!text.Contains("Fabricated-password-2026") && text.Contains("password [removed]"), "Password-like text removed");
                    Check(text.Contains("%USERPROFILE%") && !text.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), "Profile path removed");
                    Check(new FileInfo(log.FilePath).Length <= DiagnosticsLog.MaxBytes + 200 && text.Contains("size limit reached"), "File capped at 1 MB");
                }
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        });
        Test("Disk full during a session save keeps the last saved copy", () => {
            var f = new Fixture(); f.Login(); var s = f.repo.Begin(1, 0, true); s.score = 10; s.durationSeconds = 5; s.validSeconds = 5; f.repo.SaveSession(s);
            f.store.fail = true; s.score = 99; s.durationSeconds = 9; s.validSeconds = 9;
            Reject<IOException>(() => f.repo.SaveSession(s));
            f.store.fail = false;
            Check(f.repo.Sessions("patient.one").Single(x => x.id == s.id).score == 10, "Previous saved copy retained after a failed save");
            f.repo.SaveSession(s); Check(f.repo.Sessions("patient.one").Single(x => x.id == s.id).score == 99, "Retry succeeds once space is available");
        });
        Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed; fabricated fixtures only");
        return failed == 0 ? 0 : 1;
    }
}
