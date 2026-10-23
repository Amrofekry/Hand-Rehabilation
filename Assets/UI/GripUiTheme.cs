using UnityEngine;

namespace GripRehab.UI
{
    // Presentation tokens only. Clinical/gameplay values are never stored here.
    [CreateAssetMenu(menuName = "Grip Rehab/UI Theme", fileName = "GripUiTheme")]
    public sealed class GripUiTheme : ScriptableObject
    {
        public Color backdrop, surface, darkSurface, primary, secondary, accent;
        public Color textOnDark, textOnLight, textOnPrimary, textOnSecondary;
        public Color mutedOnDark, mutedOnLight, danger, warning, success;
        public Font latinFont, arabicFont;
        public Font headingFont;
        public Color backdropArtworkTint = Color.white;
        public TMPro.TMP_FontAsset displayFont;
        [Range(0.05f, .25f)] public float controlSeconds = .14f;
        [Range(1f, 1.04f)] public float hoverScale = 1.015f;
        [Range(1f, 8f)] public float focusPixels = 4f;
        [Range(.4f, .95f)] public float modalOpacity = .8f;
        [Range(100f, 1600f)] public float hoverToneHz = 540f, clickToneHz = 720f;
        [Range(0.05f, 0.6f)] public float transitionSeconds = 0.28f;
        [Range(0f, 1f)] public float shadowOpacity = 0.18f;
        [Range(0f, 1f)] public float glowOpacity = 0.12f;
        public int gridPixels = 8, patientTargetPixels = 64, doctorTargetPixels = 44;
        public int displaySize = 64, h1Size = 34, h2Size = 26, bodySize = 18, captionSize = 14;
        [Range(0f, .3f)] public float backgroundOpacity = .09f;
        [Range(0f, 32f)] public float backgroundTravel = 12f;
        [Range(.005f, .1f)] public float backgroundSpeed = .035f;

        public Color Resolve(GripUiRole role)
        {
            switch (role)
            {
                case GripUiRole.Backdrop: return backdrop;
                case GripUiRole.Surface: return surface;
                case GripUiRole.DarkSurface: return darkSurface;
                case GripUiRole.Primary: return primary;
                case GripUiRole.Secondary: return secondary;
                case GripUiRole.Accent: return accent;
                case GripUiRole.TextOnDark: return textOnDark;
                case GripUiRole.TextOnLight: return textOnLight;
                case GripUiRole.TextOnPrimary: return textOnPrimary;
                case GripUiRole.TextOnSecondary: return textOnSecondary;
                case GripUiRole.MutedOnDark: return mutedOnDark;
                case GripUiRole.MutedOnLight: return mutedOnLight;
                case GripUiRole.Danger: return danger;
                case GripUiRole.Warning: return warning;
                default: return success;
            }
        }
    }

    public enum GripUiRole
    {
        Backdrop, Surface, DarkSurface, Primary, Secondary, Accent,
        TextOnDark, TextOnLight, TextOnPrimary, TextOnSecondary,
        MutedOnDark, MutedOnLight, Danger, Warning, Success
    }
}
