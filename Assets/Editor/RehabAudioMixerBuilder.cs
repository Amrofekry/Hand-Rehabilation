using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace GripRehab.Editor
{
    /// <summary>
    /// Stage 12 (S-07, S-08): builds the project's AudioMixer asset with Master, SFX, Ambience and Voice groups and one
    /// exposed volume per group. Unity has no public API for creating mixers, so this calls the editor's own
    /// AudioMixerController methods (UnityCsReference 2022.3, Editor/Mono/Audio/Mixer/Bindings/AudioMixerController.cs)
    /// by reflection. It runs once; the asset is committed. Batch mode:
    /// -executeMethod GripRehab.Editor.RehabAudioMixerBuilder.Build
    /// </summary>
    public static class RehabAudioMixerBuilder
    {
        public const string Path = "Assets/Resources/Audio/GripRehabMixer.mixer";
        public static readonly string[] Groups = { "SFX", "Ambience", "Voice" };
        public static readonly string[] Parameters = { "MasterVolume", "SfxVolume", "AmbienceVolume", "VoiceVolume" };
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(Path) != null) { Debug.Log("[REHAB_MIXER] exists: " + Path); return; }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var editor = typeof(UnityEditor.Editor).Assembly;
            var controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
            var groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
            var pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);

            var controller = controllerType.GetMethod("CreateMixerControllerAtPath", Any).Invoke(null, new object[] { Path });
            var master = controllerType.GetProperty("masterGroup", Any).GetValue(controller);
            var all = new[] { master }.ToList();
            foreach (var name in Groups)
            {
                var group = controllerType.GetMethod("CreateNewGroup", Any).Invoke(controller, new object[] { name, false });
                controllerType.GetMethod("AddChildToParent", Any).Invoke(controller, new[] { group, master });
                all.Add(group);
            }
            // Expose each group's volume, then give the exposed parameters readable names.
            for (int i = 0; i < all.Count; i++)
            {
                var guid = groupType.GetMethod("GetGUIDForVolume", Any).Invoke(all[i], null);
                var path = Activator.CreateInstance(pathType, all[i], guid);
                controllerType.GetMethod("AddExposedParameter", Any).Invoke(controller, new[] { path });
            }
            var exposedProperty = controllerType.GetProperty("exposedParameters", Any);
            var exposed = (Array)exposedProperty.GetValue(controller);
            var element = exposed.GetType().GetElementType();
            var guidField = element.GetField("guid", Any); var nameField = element.GetField("name", Any);
            for (int i = 0; i < exposed.Length; i++)
            {
                var item = exposed.GetValue(i); var guid = guidField.GetValue(item);
                for (int g = 0; g < all.Count; g++)
                    if (Equals(groupType.GetMethod("GetGUIDForVolume", Any).Invoke(all[g], null), guid)) nameField.SetValue(item, Parameters[g]);
                exposed.SetValue(item, i);
            }
            exposedProperty.SetValue(controller, exposed);
            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Verify();
        }

        /// <summary>The mixer loads, has the four groups and each exposed volume accepts a value.</summary>
        public static void Verify()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
            if (mixer == null) throw new Exception("Mixer missing at " + Path);
            foreach (var g in new[] { "Master" }.Concat(Groups))
                if (mixer.FindMatchingGroups(g).Length == 0) throw new Exception("Mixer group missing: " + g);
            foreach (var p in Parameters)
                if (!mixer.GetFloat(p, out _)) throw new Exception("Exposed parameter missing: " + p);
            Debug.Log("[REHAB_MIXER] PASS: 4 groups, " + Parameters.Length + " exposed volumes at " + Path);
        }
    }
}
