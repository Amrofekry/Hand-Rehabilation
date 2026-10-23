using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.UI
{
    /// <summary>Bounded reusable score bars; input is real session data or explicitly labelled preview.</summary>
    public sealed class ResearchChart : MaskableGraphic
    {
        readonly float[] values = new float[64]; int count;
        public void SetValues(float[] input)
        {
            count = input == null ? 0 : Mathf.Min(values.Length, input.Length);
            for (int i = 0; i < count; i++) values[i] = float.IsNaN(input[i]) || float.IsInfinity(input[i]) ? 0 : Mathf.Clamp01(input[i]);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (count == 0) return;
            var bounds = rectTransform.rect; float step = bounds.width / count;
            for (int i = 0; i < count; i++)
            {
                float left = bounds.xMin + step * i + 3, right = bounds.xMin + step * (i + 1) - 3;
                float top = bounds.yMin + Mathf.Max(2, bounds.height * values[i]); int first = helper.currentVertCount;
                helper.AddVert(new Vector3(left, bounds.yMin), color, Vector2.zero);
                helper.AddVert(new Vector3(left, top), color, Vector2.up);
                helper.AddVert(new Vector3(right, top), color, Vector2.one);
                helper.AddVert(new Vector3(right, bounds.yMin), color, Vector2.right);
                helper.AddTriangle(first, first + 1, first + 2); helper.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
