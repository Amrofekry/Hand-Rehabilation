using UnityEngine;

namespace GripRehab.UI
{
    // WCAG 2.x relative luminance and contrast ratio (sRGB). Presentation only.
    public static class GripUiContrast
    {
        static float Channel(float c) => c <= .04045f ? c / 12.92f : Mathf.Pow((c + .055f) / 1.055f, 2.4f);

        public static float Luminance(Color c) =>
            .2126f * Channel(c.r) + .7152f * Channel(c.g) + .0722f * Channel(c.b);

        public static float Ratio(Color a, Color b)
        {
            float x = Luminance(a), y = Luminance(b);
            return (Mathf.Max(x, y) + .05f) / (Mathf.Min(x, y) + .05f);
        }

        // The preferred colour when it reaches the minimum against the fill, else the candidate
        // with the highest contrast (a cyan focus ring on a cyan button would be invisible).
        public static Color MostVisible(Color fill, Color preferred, Color second, Color third, float minimum)
        {
            if (Ratio(preferred, fill) >= minimum) return preferred;
            var best = preferred; float bestRatio = Ratio(preferred, fill);
            float r2 = Ratio(second, fill); if (r2 > bestRatio) { best = second; bestRatio = r2; }
            float r3 = Ratio(third, fill); if (r3 > bestRatio) best = third;
            return best;
        }
    }
}
