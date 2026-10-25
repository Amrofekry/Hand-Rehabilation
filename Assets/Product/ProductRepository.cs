using System;
using System.Collections.Generic;
using System.IO;

namespace GripRehab.Product
{
    /// <summary>Local role-gated repository with copy-before-save transactions.</summary>
    public sealed class ProductRepository
    {
        readonly IProductPersistence persistence;
        readonly IProductClock clock;
        readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        readonly Dictionary<string, double> lockedUntil = new Dictionary<string, double>();
        LocalProductCatalog catalog;
        string signedIn;
        public LocalAccount Current => CopyAccount(catalog.accounts.Find(a => a.id == signedIn));
        public bool NeedsDoctorSetup => !catalog.accounts.Exists(a => a.role == LocalRole.Doctor);
        public ProductRepository(IProductPersistence persistence, IProductClock clock)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            catalog = WithAudit(persistence.Clone(persistence.Load() ?? new LocalProductCatalog())); Validate(catalog);
        }
        public static void Validate(LocalProductCatalog data)
        {
            if (data == null || data.schemaVersion != 1 || data.accounts == null || data.sessions == null ||
                data.baselines == null || data.notes == null) throw new ArgumentException("Damaged or newer account catalog.");
            var ids = new HashSet<string>(); var names = new HashSet<string>(); var profiles = new HashSet<string>();
            foreach (var account in data.accounts)
            {
                if (account == null || string.IsNullOrEmpty(account.id) || !ids.Add(account.id) ||
                    account.username != Normalize(account.username) || !names.Add(account.username) ||
                    (account.role != LocalRole.Patient && account.role != LocalRole.Doctor) || account.password == null)
                    throw new ArgumentException("Invalid account identity.");
                if (!string.IsNullOrEmpty(account.profileId) && !profiles.Add(account.profileId))
                    throw new ArgumentException("A profile cannot belong to two accounts.");
                CatalogValidation.Credential(account.password);
            }
            var recordIds = new HashSet<string>();
            foreach (var session in data.sessions)
            {
                if (session == null || string.IsNullOrEmpty(session.id) || !recordIds.Add(session.id) ||
                    !ids.Contains(session.accountId) || session.metrics == null || session.policyEvents == null ||
                    session.module < 1 || session.module > 3) throw new ArgumentException("Invalid session record.");
                if (data.accounts.Find(a => a.id == session.accountId).role != LocalRole.Patient) throw new ArgumentException("Invalid session owner role.");
                CatalogValidation.Session(session);
            }
            foreach (var baseline in data.baselines)
            {
                if (baseline == null || !ids.Contains(baseline.accountId) || data.accounts.Find(a => a.id == baseline.accountId).role != LocalRole.Patient)
                    throw new ArgumentException("Invalid baseline owner.");
                CatalogValidation.Baseline(baseline);
            }
            foreach (var note in data.notes)
                if (note == null || !ids.Contains(note.doctorId) || !ids.Contains(note.patientId) ||
                    data.accounts.Find(a => a.id == note.doctorId).role != LocalRole.Doctor ||
                    data.accounts.Find(a => a.id == note.patientId).role != LocalRole.Patient ||
                    string.IsNullOrEmpty(note.id) || string.IsNullOrWhiteSpace(note.text) || note.text.Length > 2000)
                    throw new ArgumentException("Invalid note ownership.");
            if (data.audit != null)
            {
                if (data.audit.Count > MaxAudit) throw new ArgumentException("Audit trail is too long.");
                foreach (var entry in data.audit)
                    if (entry == null || string.IsNullOrEmpty(entry.utc) || string.IsNullOrEmpty(entry.action) || entry.action.Length > 80 || !ids.Contains(entry.actorId))
                        throw new ArgumentException("Invalid audit entry.");
            }
        }
        // Stage 6 (D5): catalogs written before the audit trail existed load with an empty one.
        public const int MaxAudit = 5000;
        static LocalProductCatalog WithAudit(LocalProductCatalog c) { if (c != null && c.audit == null) c.audit = new List<AuditEntry>(); return c; }
        static void Append(LocalProductCatalog next, string actor, string action, string subject, string utc)
        {
            next.audit.Add(new AuditEntry { utc = utc, actorId = actor, action = action, subjectId = subject });
            if (next.audit.Count > MaxAudit) next.audit.RemoveRange(0, next.audit.Count - MaxAudit);
        }
        /// <summary>D5: records a doctor's action on a patient record (view, export).</summary>
        public void RecordAudit(string action, string patientId)
        {
            RequireDoctor(); RequireRead(patientId);
            if (string.IsNullOrWhiteSpace(action) || action.Length > 80) throw new ArgumentException("Invalid audit action.");
            string actor = Current.id, stamp = clock.Utc;
            Commit(next => Append(next, actor, action, patientId, stamp));
        }
        /// <summary>D5: the audit trail for one patient record (doctor only), oldest first.</summary>
        public AuditEntry[] AuditTrail(string patientId)
        {
            RequireDoctor(); RequireRead(patientId);
            return persistence.Clone(catalog).audit?.FindAll(e => e.subjectId == patientId).ToArray() ?? new AuditEntry[0];
        }
        /// <summary>D5: this doctor's own sign-in and sign-out entries, oldest first.</summary>
        public AuditEntry[] MyAccess()
        {
            RequireDoctor(); string me = Current.id;
            return persistence.Clone(catalog).audit?.FindAll(e => e.actorId == me && string.IsNullOrEmpty(e.subjectId)).ToArray() ?? new AuditEntry[0];
        }
        void TryAudit(string actor, string action, string subject)
        {
            // Sign-in and sign-out are recorded when storage is writable; a full disk must not lock a doctor out.
            string stamp = clock.Utc;
            try { Commit(next => Append(next, actor, action, subject, stamp)); }
            catch (IOException) { }
        }
        static string Normalize(string name)
        {
            name = (name ?? "").Trim().ToLowerInvariant();
            if (name.Length < 3 || name.Length > 40) throw new ArgumentException("Username needs 3–40 characters.");
            foreach (char c in name) if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                throw new ArgumentException("Use letters, digits, dots, underscores or dashes.");
            return name;
        }
        static LocalAccount CopyAccount(LocalAccount a) => a == null ? null : new LocalAccount {
            id = a.id, username = a.username, displayName = a.displayName, profileId = a.profileId,
            role = a.role, createdUtc = a.createdUtc };
        void Commit(Action<LocalProductCatalog> mutation)
        { var next = WithAudit(persistence.Clone(catalog)); mutation(next); Validate(next); persistence.Save(next); catalog = WithAudit(persistence.Clone(next)); }
        public LocalAccount Register(string username, string displayName, string password, LocalRole role)
        {
            username = Normalize(username); displayName = (displayName ?? "").Trim();
            if (displayName.Length < 2 || displayName.Length > 60) throw new ArgumentException("Name needs 2–60 characters.");
            if (role != LocalRole.Patient && role != LocalRole.Doctor) throw new ArgumentException("Invalid role.");
            if (role == LocalRole.Doctor && !NeedsDoctorSetup) RequireDoctor();
            if (catalog.accounts.Exists(a => a.username == username)) throw new ArgumentException("Username is already used.");
            var account = new LocalAccount { id = Guid.NewGuid().ToString("N"), username = username,
                displayName = displayName, password = PasswordKdf.Create(password), role = role, createdUtc = clock.Utc };
            Commit(next => next.accounts.Add(account)); return CopyAccount(account);
        }
        public bool SignIn(string username, string password)
        {
            signedIn = null; username = Normalize(username);
            if (lockedUntil.TryGetValue(username, out var until) && clock.Seconds < until) return false;
            var account = catalog.accounts.Find(a => a.username == username);
            if (account == null || !PasswordKdf.Verify(password, account.password))
            {
                attempts.TryGetValue(username, out var count); count++; attempts[username] = count;
                if (count >= 5) { lockedUntil[username] = clock.Seconds + 30; attempts[username] = 0; }
                return false;
            }
            attempts.Remove(username); lockedUntil.Remove(username); signedIn = account.id;
            if (account.role == LocalRole.Doctor) TryAudit(account.id, "signed in", null);
            return true;
        }
        public void SignOut()
        {
            if (Current != null && Current.role == LocalRole.Doctor) TryAudit(Current.id, "signed out", null);
            signedIn = null;
        }
        // A saved in-progress record from a previous process is an interrupted
        // attempt. Continue starts a new attempt from the profile checkpoint.
        public int RecoverInterruptedSessions()
        {
            if (Current == null || Current.role != LocalRole.Patient) return 0;
            string owner = Current.id;
            int count = catalog.sessions.FindAll(s => s.accountId == owner && s.state == RecordState.InProgress).Count;
            if (count > 0) Commit(next => {
                foreach (var s in next.sessions)
                    if (s.accountId == owner && s.state == RecordState.InProgress)
                    { s.state = RecordState.Interrupted; s.endedUtc = clock.Utc; }
            });
            return count;
        }
        public void RequireDoctor()
        { if (Current == null || Current.role != LocalRole.Doctor) throw new UnauthorizedAccessException("Doctor sign-in required."); }
        void RequireOwner(string id)
        { if (Current == null || Current.id != id) throw new UnauthorizedAccessException("Sign in to your account."); }
        void RequireRead(string id)
        { if (Current == null || (Current.id != id && Current.role != LocalRole.Doctor)) throw new UnauthorizedAccessException("This record belongs to another account."); }
        public LocalAccount[] Patients()
        { RequireDoctor(); return catalog.accounts.FindAll(a => a.role == LocalRole.Patient).ConvertAll(CopyAccount).ToArray(); }
        public LocalAccount Account(string id) { RequireRead(id); return CopyAccount(catalog.accounts.Find(a => a.id == id)); }
        public void LinkProfile(string profileId, bool importedLegacy = false)
        {
            if (Current == null || Current.role != LocalRole.Patient) throw new UnauthorizedAccessException("Patient sign-in required.");
            if (importedLegacy) throw new UnauthorizedAccessException("A doctor must assign imported profiles.");
            AssignProfile(Current.id, profileId);
        }
        public void AssignImportedProfile(string patientId, string profileId) { RequireDoctor(); AssignProfile(patientId, profileId); }
        void AssignProfile(string accountId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Choose a profile.");
            if (catalog.accounts.Exists(a => a.profileId == profileId && a.id != accountId)) throw new ArgumentException("Profile is already assigned.");
            Commit(next => { var account = next.accounts.Find(a => a.id == accountId);
                if (account == null || account.role != LocalRole.Patient) throw new ArgumentException("Choose a patient."); account.profileId = profileId; });
        }
        public void SaveBaseline(CapabilityProfile baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            RequireOwner(baseline.accountId);
            if (Current.role != LocalRole.Patient || baseline.profileId != Current.profileId) throw new UnauthorizedAccessException("Baseline must match your linked hand profile.");
            if (!baseline.consent || baseline.acceptedSamples < 2 || baseline.validSeconds <= 0)
                throw new ArgumentException("A baseline requires consent and valid samples.");
            CatalogValidation.Baseline(baseline);
            baseline.id = Guid.NewGuid().ToString("N"); baseline.createdUtc = clock.Utc; Commit(next => next.baselines.Add(baseline));
        }
        public CapabilityProfile LatestBaseline(string id)
        { RequireRead(id); return persistence.Clone(catalog).baselines.FindLast(b => b.accountId == id); }
        public SessionRecord Begin(int module, int task, bool synthetic)
        {
            if (Current == null || Current.role != LocalRole.Patient) throw new UnauthorizedAccessException("Patient sign-in required.");
            if (string.IsNullOrEmpty(Current.profileId)) throw new InvalidOperationException("Link your hand profile before starting practice.");
            if (module < 1 || module > 3 || task < 0 || task > 1) throw new ArgumentException("Choose a valid module/task.");
            var session = new SessionRecord { id = Guid.NewGuid().ToString("N"), accountId = Current.id,
                profileId = Current.profileId, module = module, task = task, startedUtc = clock.Utc,
                baselineId = LatestBaseline(Current.id)?.id, synthetic = synthetic, state = RecordState.InProgress };
            Commit(next => next.sessions.Add(session)); return session;
        }
        public void SaveSession(SessionRecord session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RequireOwner(session.accountId);
            var original = catalog.sessions.Find(s => s.id == session.id && s.accountId == session.accountId);
            if (original == null) throw new ArgumentException("Begin the session first.");
            // Unity's JSON stores a null string as "", so a patient with no baseline must still match (Stage 6 finding S6-01).
            if (session.module != original.module || session.task != original.task || !Same(session.profileId, original.profileId) ||
                !Same(session.baselineId, original.baselineId) || !Same(session.startedUtc, original.startedUtc) ||
                (original.state != RecordState.InProgress && session.state != original.state))
                throw new ArgumentException("Session identity and completed state cannot change.");
            CatalogValidation.Session(session);
            if (session.metrics.Count > 1800 || session.policyEvents.Count > 500 || session.score < 0 || session.score > 100 ||
                float.IsNaN(session.durationSeconds) || float.IsInfinity(session.durationSeconds) || session.durationSeconds < 0)
                throw new ArgumentException("Invalid or excessive session metrics.");
            if (session.state != RecordState.InProgress) session.endedUtc = clock.Utc;
            Commit(next => next.sessions[next.sessions.FindIndex(s => s.id == session.id)] = session);
        }
        static bool Same(string a, string b) => (a ?? "") == (b ?? "");
        public SessionRecord[] Sessions(string id) { RequireRead(id); return persistence.Clone(catalog).sessions.FindAll(s => s.accountId == id).ToArray(); }
        public ClinicalNote[] Notes(string id) { RequireRead(id); return persistence.Clone(catalog).notes.FindAll(n => n.patientId == id).ToArray(); }
        public ClinicalNote AddNote(string patientId, string text, string supersedes = null)
        {
            RequireDoctor(); text = (text ?? "").Trim();
            if (text.Length < 1 || text.Length > 2000 || !catalog.accounts.Exists(a => a.id == patientId && a.role == LocalRole.Patient))
                throw new ArgumentException("Choose a patient and write a note of at most 2000 characters.");
            if (!string.IsNullOrEmpty(supersedes) && !catalog.notes.Exists(n => n.id == supersedes && n.patientId == patientId)) throw new ArgumentException("The original note was not found.");
            var note = new ClinicalNote { id = Guid.NewGuid().ToString("N"), doctorId = Current.id,
                patientId = patientId, text = text, createdUtc = clock.Utc, supersedesId = supersedes };
            Commit(next => { next.notes.Add(note); Append(next, note.doctorId, "added a note", patientId, note.createdUtc); }); return note;
        }
    }
}
