using System.Collections.Generic;
using UnityEngine;

namespace GripRehab
{
    public enum RehabSurface
    {
        OakFloor, Plaster, LinenFabric, Quartz, Ceramic, Rug,
        PaintedWood, Walnut, DarkMetal, BrushedMetal, Rubber, Glass
    }

    public static class RehabMaterials
    {
        private static Material _base;
        private static readonly Dictionary<RehabSurface, Material> Surfaces =
            new Dictionary<RehabSurface, Material>();

        public static Material Create(Color color)
        {
            var material = CloneBase();
            if (material != null) material.color = color;
            return material;
        }

        public static void Apply(Renderer renderer, Color color)
        {
            var material = Create(color);
            if (material != null) renderer.sharedMaterial = material;
        }

        public static void Apply(Renderer renderer, RehabSurface surface)
        {
            if (!Surfaces.TryGetValue(surface, out var material))
            {
                material = BuildSurface(surface);
                if (material != null) Surfaces.Add(surface, material);
            }
            if (material != null) renderer.sharedMaterial = material;
        }

        private static Material CloneBase()
        {
            if (_base == null) _base = Resources.Load<Material>("RehabBase");
            if (_base != null) return new Material(_base);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return shader == null ? null : new Material(shader);
        }

        private static Material BuildSurface(RehabSurface surface)
        {
            var material = CloneBase();
            if (material == null) return null;
            material.name = "Rehab " + surface;
            var tint = Color.white;
            var textureName = "";
            var tiling = Vector2.one;
            var metallic = 0f;
            var smoothness = .4f;
            var bump = .3f;
            switch (surface)
            {
                case RehabSurface.OakFloor:
                    textureName = "OakFloor"; tiling = new Vector2(5, 4); smoothness = .34f; break;
                case RehabSurface.Plaster:
                    textureName = "Plaster"; tiling = new Vector2(3, 2); smoothness = .12f; bump = .17f; break;
                case RehabSurface.LinenFabric:
                    textureName = "LinenFabric"; tiling = new Vector2(2, 2); smoothness = .16f; bump = .35f; break;
                case RehabSurface.Quartz:
                    textureName = "Quartz"; tiling = new Vector2(2, 1); smoothness = .76f; bump = .1f; break;
                case RehabSurface.Ceramic:
                    textureName = "Ceramic"; tiling = new Vector2(3, 2); smoothness = .72f; bump = .14f; break;
                case RehabSurface.Rug:
                    textureName = "Rug"; tiling = new Vector2(2, 2); smoothness = .12f; bump = .35f; break;
                case RehabSurface.PaintedWood:
                    tint = new Color(.48f, .58f, .56f); smoothness = .46f; break;
                case RehabSurface.Walnut:
                    textureName = "OakFloor"; tint = new Color(.48f, .34f, .24f);
                    tiling = new Vector2(1, 1); smoothness = .52f; break;
                case RehabSurface.DarkMetal:
                    tint = new Color(.21f, .24f, .26f); metallic = .82f; smoothness = .67f; break;
                case RehabSurface.BrushedMetal:
                    tint = new Color(.66f, .7f, .71f); metallic = .78f; smoothness = .72f; break;
                case RehabSurface.Rubber:
                    tint = new Color(.14f, .29f, .29f); smoothness = .27f; break;
                case RehabSurface.Glass:
                    tint = new Color(.72f, .86f, .92f); smoothness = .91f; break;
            }
            material.color = tint;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (!string.IsNullOrEmpty(textureName))
            {
                var albedo = Resources.Load<Texture2D>("Surfaces/" + textureName);
                var normal = Resources.Load<Texture2D>("Surfaces/" + textureName + "_Normal");
                if (albedo != null)
                {
                    material.SetTexture("_BaseMap", albedo);
                    material.SetTextureScale("_BaseMap", tiling);
                }
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", bump);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            return material;
        }
    }
}
