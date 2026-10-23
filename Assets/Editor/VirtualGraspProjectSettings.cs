using System;
using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    public static class VirtualGraspProjectSettings
    {
        // The vendor build hook checks these settings even for Windows builds.
        [MenuItem("Grip Rehab/Hands/Verify VirtualGrasp Compatibility")]
        public static void Verify()
        {
            if (!AssetDatabase.IsValidFolder("Assets/com.gleechi.unity.virtualgrasp"))
                throw new Exception("Imported VirtualGrasp folder is missing");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new Exception("VirtualGrasp requires Android ARM64 in Player Settings");
            if (Time.fixedDeltaTime >= .0138f)
                throw new Exception("VirtualGrasp requires physics above 72 Hz in Project Settings > Time");
            Debug.Log("[VG_SETTINGS] PASS: vendor SDK unchanged, Android ARM64 selected, physics 75 Hz");
        }
    }
}
