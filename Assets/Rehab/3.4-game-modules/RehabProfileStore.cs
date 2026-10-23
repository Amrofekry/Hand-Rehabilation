using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GripRehab
{
    [Serializable]
    public sealed class RehabExerciseResult
    {
        public int module, task, score;
        public float seconds;
        public string completedUtc;
    }

    [Serializable]
    public sealed class RehabProfile
    {
        public string id, name, gender, affectedHand, dominantHand, concern, goal, createdUtc, updatedUtc;
        public int age, painToday = -1;
        public bool hasSession, kitchen, leftHand;
        public int livingObject, kitchenObject, inputMode; // 0 camera, 1 keyboard demo, 2 Phase 2 hardware.
        public string lastSessionUtc;
        public int lastModule = 1, lastExercise;
        public List<RehabExerciseResult> results = new List<RehabExerciseResult>();
    }

    [Serializable]
    public sealed class RehabProfileCatalog
    {
        public int schemaVersion = 1;
        public string activeId;
        public List<RehabProfile> profiles = new List<RehabProfile>();
    }

    // Local prototype identity and checkpoint storage. These self-reported
    // answers are never interpreted as a diagnosis or a measured baseline.
    public sealed class RehabProfileStore
    {
        readonly string path;
        readonly string legacyPath;
        public RehabProfileCatalog Catalog { get; private set; }
        public bool MigrationCleanupRequired { get; private set; }
        public RehabProfile Active
        {
            get { return Catalog.profiles.Find(p => p.id == Catalog.activeId); }
        }

        public RehabProfileStore(string directory)
        {
            path = Path.Combine(directory, "profiles.dat");
            legacyPath = Path.Combine(directory, "profiles.json");
            Catalog = new RehabProfileCatalog();
            if (File.Exists(path) || File.Exists(path + ".bak"))
            {
                try { ReadProtected(path); }
                catch (Exception primary)
                {
                    if (!File.Exists(path + ".bak")) throw new IOException("Could not read protected profiles", primary);
                    try
                    {
                        ReadProtected(path + ".bak");
                        File.Copy(path + ".bak", path, true);
                        ReadProtected(path);
                    }
                    catch (Exception backup) { throw new IOException("Both protected profile copies are unreadable", backup); }
                }
                return;
            }
            if (!File.Exists(legacyPath) && !File.Exists(legacyPath + ".bak")) return;
            try { ReadLegacy(legacyPath); }
            catch (Exception primary)
            {
                if (!File.Exists(legacyPath + ".bak")) throw new IOException("Could not read legacy profiles", primary);
                try { ReadLegacy(legacyPath + ".bak"); }
                catch (Exception backup) { throw new IOException("Both legacy profile copies are unreadable", backup); }
            }
            // Never remove the old catalog until both new encrypted copies
            // round-trip and preserve the exact JSON selected for migration.
            var expected = JsonUtility.ToJson(Catalog, true);
            Save();
            if (ReadProtectedText(path) != expected || ReadProtectedText(path + ".bak") != expected)
                throw new IOException("Protected profile migration did not verify; legacy data retained");
            try
            {
                if (File.Exists(legacyPath)) File.Delete(legacyPath);
                if (File.Exists(legacyPath + ".bak")) File.Delete(legacyPath + ".bak");
            }
            catch (IOException) { MigrationCleanupRequired = true; }
            catch (UnauthorizedAccessException) { MigrationCleanupRequired = true; }
            if (MigrationCleanupRequired)
                Debug.LogError("Protected profile migration succeeded but plaintext cleanup needs attention.");
        }

        void ReadLegacy(string file)
        {
            if (new FileInfo(file).Length > 1024 * 1024) throw new InvalidDataException("Legacy catalog too large");
            Catalog = ParseCatalog(File.ReadAllText(file, Encoding.UTF8));
        }

        void ReadProtected(string file)
        {
            Catalog = ParseCatalog(ReadProtectedText(file));
        }

        static string ReadProtectedText(string file)
        {
            if (new FileInfo(file).Length > 16 * 1024 * 1024) throw new InvalidDataException("Protected catalog too large");
            var clear = WindowsProfileProtection.Unprotect(File.ReadAllBytes(file));
            try { return Encoding.UTF8.GetString(clear); }
            finally { Array.Clear(clear, 0, clear.Length); }
        }

        static RehabProfileCatalog ParseCatalog(string json)
        {
            var loaded = JsonUtility.FromJson<RehabProfileCatalog>(json);
            if (loaded == null || loaded.schemaVersion != 1 || loaded.profiles == null)
                throw new InvalidDataException("Unknown or damaged profile catalog");
            var ids = new HashSet<string>();
            foreach (var profile in loaded.profiles)
                if (profile == null || string.IsNullOrEmpty(profile.id) || !ids.Add(profile.id))
                    throw new InvalidDataException("Invalid profile identity");
            return loaded;
        }

        public RehabProfile Create(string name, int age, string gender, string affected, string dominant,
                                   string concern, string goal, int pain)
        {
            name = (name ?? "").Trim();
            if (name.Length < 2 || name.Length > 60) throw new ArgumentException("Name needs 2–60 characters");
            if (age < 1 || age > 120) throw new ArgumentException("Enter an age from 1 to 120");
            if (pain < -1 || pain > 10) throw new ArgumentException("Pain entry must be 0–10 or blank");
            if (!OneOf(affected, "Right", "Left", "Both")) throw new ArgumentException("Choose the hand to train");
            if (!OneOf(dominant, "Right", "Left", "Both")) throw new ArgumentException("Choose your dominant hand");
            if (!OneOf(gender, "", "Female", "Male", "Self-described", "Prefer not to say"))
                throw new ArgumentException("Choose a listed gender option");
            concern = (concern ?? "").Trim(); goal = (goal ?? "").Trim();
            if (concern.Length > 400 || goal.Length > 200) throw new ArgumentException("A text field is too long");
            var now = DateTime.UtcNow.ToString("O");
            var profile = new RehabProfile
            {
                id = Guid.NewGuid().ToString("N"), name = name, age = age, gender = gender,
                affectedHand = affected, dominantHand = dominant, concern = concern, goal = goal,
                painToday = pain, createdUtc = now, updatedUtc = now
            };
            var previousActive = Catalog.activeId;
            Catalog.profiles.Add(profile);
            Catalog.activeId = profile.id;
            try { Save(); }
            catch
            {
                Catalog.profiles.Remove(profile);
                Catalog.activeId = previousActive;
                throw;
            }
            return profile;
        }

        static bool OneOf(string value, params string[] options)
        {
            foreach (var option in options) if (value == option) return true;
            return false;
        }

        public bool Select(string id)
        {
            if (Catalog.profiles.Find(p => p.id == id) == null) return false;
            var previousActive = Catalog.activeId;
            Catalog.activeId = id;
            try { Save(); }
            catch { Catalog.activeId = previousActive; throw; }
            return true;
        }

        public void SaveCheckpoint(bool kitchen, int livingObject, int kitchenObject, bool leftHand, int inputMode)
        {
            var profile = Active;
            if (profile == null) throw new InvalidOperationException("Select a profile first");
            var previousHasSession = profile.hasSession;
            var previousKitchen = profile.kitchen;
            var previousLivingObject = profile.livingObject;
            var previousKitchenObject = profile.kitchenObject;
            var previousLeftHand = profile.leftHand;
            var previousInputMode = profile.inputMode;
            var previousSessionUtc = profile.lastSessionUtc;
            var previousUpdatedUtc = profile.updatedUtc;
            var previousModule = profile.lastModule;
            var previousExercise = profile.lastExercise;
            profile.hasSession = true;
            profile.lastModule = 1;
            profile.lastExercise = 0;
            profile.kitchen = kitchen;
            profile.livingObject = Mathf.Clamp(livingObject, 0, 2);
            profile.kitchenObject = Mathf.Clamp(kitchenObject, 0, 2);
            profile.leftHand = leftHand;
            profile.inputMode = Mathf.Clamp(inputMode, 0, 2);
            profile.lastSessionUtc = profile.updatedUtc = DateTime.UtcNow.ToString("O");
            try { Save(); }
            catch
            {
                profile.hasSession = previousHasSession;
                profile.kitchen = previousKitchen;
                profile.livingObject = previousLivingObject;
                profile.kitchenObject = previousKitchenObject;
                profile.leftHand = previousLeftHand;
                profile.inputMode = previousInputMode;
                profile.lastSessionUtc = previousSessionUtc;
                profile.updatedUtc = previousUpdatedUtc;
                profile.lastModule = previousModule;
                profile.lastExercise = previousExercise;
                throw;
            }
        }

        public void SaveExerciseStart(int module, int task)
        {
            if (module < 2 || module > 3 || task < 0 || task > 1)
                throw new ArgumentOutOfRangeException(nameof(module));
            var profile = Active;
            if (profile == null) throw new InvalidOperationException("Select a profile first");
            var oldModule = profile.lastModule;
            var oldTask = profile.lastExercise;
            var oldHasSession = profile.hasSession;
            var oldSessionUtc = profile.lastSessionUtc;
            var oldUpdatedUtc = profile.updatedUtc;
            profile.lastModule = module;
            profile.lastExercise = task;
            profile.hasSession = true;
            profile.lastSessionUtc = profile.updatedUtc = DateTime.UtcNow.ToString("O");
            try { Save(); }
            catch
            {
                profile.lastModule = oldModule;
                profile.lastExercise = oldTask;
                profile.hasSession = oldHasSession;
                profile.lastSessionUtc = oldSessionUtc;
                profile.updatedUtc = oldUpdatedUtc;
                throw;
            }
        }

        public void SaveExerciseResult(int module, int task, int score, float seconds)
        {
            if (module < 2 || module > 3 || task < 0 || task > 1 ||
                score < 0 || score > 100 || float.IsNaN(seconds) ||
                float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(score));
            var profile = Active;
            if (profile == null) throw new InvalidOperationException("Select a profile first");
            var oldResults = profile.results == null ? null :
                new List<RehabExerciseResult>(profile.results);
            var oldUpdatedUtc = profile.updatedUtc;
            if (profile.results == null) profile.results = new List<RehabExerciseResult>();
            profile.results.Add(new RehabExerciseResult
            {
                module = module, task = task, score = score,
                seconds = seconds, completedUtc = DateTime.UtcNow.ToString("O")
            });
            if (profile.results.Count > 100) profile.results.RemoveAt(0);
            profile.updatedUtc = DateTime.UtcNow.ToString("O");
            try { Save(); }
            catch { profile.results = oldResults; profile.updatedUtc = oldUpdatedUtc; throw; }
        }

        public void Save()
        {
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            var expected = JsonUtility.ToJson(Catalog, true);
            var clear = Encoding.UTF8.GetBytes(expected);
            byte[] protectedBytes;
            try { protectedBytes = WindowsProfileProtection.Protect(clear); }
            finally { Array.Clear(clear, 0, clear.Length); }
            try
            {
                File.WriteAllBytes(temporary, protectedBytes);
                if (ReadProtectedText(temporary) != expected)
                    throw new IOException("Protected profile write failed read-back");
                if (File.Exists(path))
                {
                    // Windows scanners can briefly hold the encrypted file
                    // between the read-back and atomic replacement. Retry only
                    // transient I/O failures; never delete the previous copy.
                    for (int attempt = 0; ; attempt++)
                    {
                        try { File.Replace(temporary, path, path + ".bak"); break; }
                        catch (IOException) when (attempt < 3 && File.Exists(temporary))
                        {
                            System.Threading.Thread.Sleep(20 << attempt);
                        }
                    }
                }
                else
                {
                    File.Move(temporary, path);
                    File.Copy(path, path + ".bak", true);
                }
                if (ReadProtectedText(path) != expected)
                    throw new IOException("Protected profile save failed read-back");
            }
            finally { Array.Clear(protectedBytes, 0, protectedBytes.Length); }
        }
    }
}
