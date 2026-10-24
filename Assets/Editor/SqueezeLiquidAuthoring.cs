using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    // Stage 3.4: one-time authoring of the squeeze-liquid material and the two liquid
    // profiles. Referencing the material from the profiles keeps its URP shader in builds.
    public static class SqueezeLiquidAuthoring
    {
        const string MaterialPath = "Assets/Rehab/3.4.1-force-object-interaction/Apartment/Materials/SqueezeLiquid.mat";
        const string Profiles = "Assets/Rehab/3.4.1-force-object-interaction/Apartment/Profiles/";

        [MenuItem("Grip Rehab/Author Squeeze Liquid")]
        public static void Run()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new System.InvalidOperationException("URP Particles/Unlit shader not found");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
                material = new Material(shader) { name = "SqueezeLiquid" };
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Configure("02_Kitchen_sponge.asset", material, new Color(.72f, .86f, 1f, .75f), .2f, 80, 0);
            Configure("06_Ripe_tomato.asset", material, new Color(.86f, .12f, .08f, .9f), .35f, 45, 60);
            AssetDatabase.SaveAssets();
            Debug.Log("[SQUEEZE_LIQUID] PASS: material and sponge/tomato liquid profiles authored");
        }

        /// <summary>Guard: the liquid profiles keep a URP particle material so builds never render pink.</summary>
        public static void Verify()
        {
            foreach (var file in new[] { "02_Kitchen_sponge.asset", "06_Ripe_tomato.asset" })
            {
                var profile = AssetDatabase.LoadAssetAtPath<PhysicalMaterialProfile>(Profiles + file);
                if (profile == null || !profile.releasesLiquid || profile.liquidMaterial == null ||
                    profile.liquidMaterial.shader == null || profile.liquidMaterial.shader.name != "Universal Render Pipeline/Particles/Unlit")
                    throw new System.Exception("Squeeze liquid not authored for " + file);
            }
            Debug.Log("[SQUEEZE_LIQUID] PASS: sponge and tomato release liquid with a URP particle material");
        }

        static void Configure(string file, Material material, Color color, float start, float rate, int burst)
        {
            var profile = AssetDatabase.LoadAssetAtPath<PhysicalMaterialProfile>(Profiles + file);
            if (profile == null) throw new System.InvalidOperationException("Missing profile " + file);
            profile.releasesLiquid = true; profile.liquidMaterial = material; profile.liquidColor = color;
            profile.liquidStartCompression = start; profile.liquidRate = rate; profile.liquidBurst = burst;
            EditorUtility.SetDirty(profile);
        }
    }
}
