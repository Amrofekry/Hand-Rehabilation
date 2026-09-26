using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GripRehab
{
    [Serializable]
    public sealed class RehabProfile
    {
        public string id, name, gender, affectedHand, dominantHand, concern, goal, createdUtc, updatedUtc;
        public int age, painToday = -1;
        public bool hasSession, kitchen, leftHand;
        public int livingObject, kitchenObject, inputMode; // 0 camera, 1 keyboard demo, 2 Phase 2 hardware.
        public string lastSessionUtc;
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
        public RehabProfileCatalog Catalog { get; private set; }
        public RehabProfile Active
        {
            get { return Catalog.profiles.Find(p => p.id == Catalog.activeId); }
        }

        public RehabProfileStore(string directory)
        {
            path = Path.Combine(directory, "profiles.json");
            Catalog = new RehabProfileCatalog();
            if (!File.Exists(path)) return;
            try { Read(path); }
            catch (Exception primary)
            {
                if (!File.Exists(path + ".bak")) throw new IOException("Could not read local profiles", primary);
                try { Read(path + ".bak"); }
                catch (Exception backup) { throw new IOException("Both local profile copies are unreadable", backup); }
            }
        }

        void Read(string file)
        {
            var loaded = JsonUtility.FromJson<RehabProfileCatalog>(File.ReadAllText(file));
            if (loaded == null || loaded.schemaVersion != 1 || loaded.profiles == null)
                throw new InvalidDataException("Unknown or damaged profile catalog");
            var ids = new HashSet<string>();
            foreach (var profile in loaded.profiles)
                if (profile == null || string.IsNullOrEmpty(profile.id) || !ids.Add(profile.id))
                    throw new InvalidDataException("Invalid profile identity");
            Catalog = loaded;
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
            Catalog.profiles.Add(profile);
            Catalog.activeId = profile.id;
            Save();
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
            Catalog.activeId = id;
            Save();
            return true;
        }

        public void SaveCheckpoint(bool kitchen, int livingObject, int kitchenObject, bool leftHand, int inputMode)
        {
            var profile = Active;
            if (profile == null) throw new InvalidOperationException("Select a profile first");
            profile.hasSession = true;
            profile.kitchen = kitchen;
            profile.livingObject = Mathf.Clamp(livingObject, 0, 2);
            profile.kitchenObject = Mathf.Clamp(kitchenObject, 0, 2);
            profile.leftHand = leftHand;
            profile.inputMode = Mathf.Clamp(inputMode, 0, 2);
            profile.lastSessionUtc = profile.updatedUtc = DateTime.UtcNow.ToString("O");
            Save();
        }

        public void Save()
        {
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(Catalog, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
