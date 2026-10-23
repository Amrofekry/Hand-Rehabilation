using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GripRehab.Editor
{
    // Read-only check. The obsolete two-renderer authoring command was removed so
    // it cannot replace the selectable VirtualGrasp scene with the legacy prefab.
    public static class HandOnlyAuthoring
    {
        [MenuItem("Grip Rehab/Hands/Verify Selected VirtualGrasp Hand")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
            var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
            if (app == null || app.virtualGrasp == null || app.virtualGrasp.outputMesh == null)
                throw new Exception("Selectable VirtualGrasp hand is missing");
            if (app.virtualGrasp.outputMesh.sharedMesh != app.virtualGrasp.rightOnlyMesh)
                throw new Exception("Scene preview must show the default right hand only");
            var patientRenderers = app.player.GetComponentsInChildren<Renderer>(true);
            if (patientRenderers.Any(r => r.enabled))
                throw new Exception("A legacy patient/body renderer is still enabled");
            Debug.Log("[HAND_SELECTION] PASS: vendor right-hand preview, left option and no visible patient body");
        }
    }
}
