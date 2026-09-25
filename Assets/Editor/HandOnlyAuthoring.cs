using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GripRehab.Editor
{
    public static class HandOnlyAuthoring
    {
        public const string PlayerPrefab = "Assets/Rehab/3.1-environment/Apartment/Prefabs/FirstPersonPatient.prefab";

        [MenuItem("Grip Rehab/Hands/Apply Hands-Only Patient")]
        public static void Apply()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var player = prefab.GetComponent<ApartmentPlayer>();
                if (player == null) throw new Exception("Patient controller is missing");
                var body = prefab.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.StartsWith("Visible body", StringComparison.Ordinal));
                if (body != null) UnityEngine.Object.DestroyImmediate(body.gameObject);

                var hands = prefab.GetComponentsInChildren<FingerPoseDriver>(true);
                if (hands.Length != 2) throw new Exception("Expected exactly two skinned hands");
                foreach (var hand in hands)
                {
                    foreach (var name in new[] { "Forearm", "Navy sleeve cuff" })
                    {
                        var extra = hand.transform.Find(name);
                        if (extra != null) UnityEngine.Object.DestroyImmediate(extra.gameObject);
                    }
                    hand.holdLastTrackedPoseOnLoss = false;
                    if (hand.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 1)
                        throw new Exception("Each hand must retain exactly one skinned mesh: " + hand.name);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
            var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
            if (app == null || app.player.GetComponentsInChildren<FingerPoseDriver>(true).Length != 2)
                throw new Exception("Scene did not retain both hands");
            if (app.player.GetComponentsInChildren<Renderer>(true)
                .Any(r => !r.transform.IsChildOf(app.rightHand.transform) &&
                          !r.transform.IsChildOf(app.leftHand.transform)))
                throw new Exception("A non-hand patient renderer remains");
            ApartmentAuthoring.VerifyScene();
            Debug.Log("[HANDS_ONLY] PASS: two hand meshes, no torso, legs, shoes, sleeves or forearms");
        }
    }
}
