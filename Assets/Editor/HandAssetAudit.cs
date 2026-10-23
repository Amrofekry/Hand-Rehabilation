using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace GripRehab.Editor
{
    public static class HandAssetAudit
    {
        public static void Run()
        {
            var paths = new[] {
                "Assets/ThirdParty/UnityHands/LeftHand.fbx",
                "Assets/ThirdParty/UnityHands/RightHand.fbx",
                "Assets/com.gleechi.unity.virtualgrasp/Runtime/Resources/GleechiHands/GleechiLeftHand.fbx",
                "Assets/com.gleechi.unity.virtualgrasp/Runtime/Resources/GleechiHands/GleechiRig.fbx"
            };
            var lines = paths.Select(path => {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) return path + " MISSING";
                var meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                return path + " renderers=" + meshes.Length +
                    " vertices=" + meshes.Sum(x => x.sharedMesh != null ? x.sharedMesh.vertexCount : 0) +
                    " bones=" + string.Join(",", meshes.SelectMany(x => x.bones).Where(x => x != null).Select(x => x.name).Distinct()) +
                    " materials=" + string.Join(",", meshes.SelectMany(x => x.sharedMaterials).Where(x => x != null).Select(x => x.name).Distinct());
            }).ToArray();
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/hand-asset-audit.txt", lines);
            Debug.Log("[HAND_ASSET_AUDIT] " + string.Join("\n", lines));
        }
    }
}
