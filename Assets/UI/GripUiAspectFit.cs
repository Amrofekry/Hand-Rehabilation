using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.UI
{
    // Stage 11: the screens are authored on a 1600x900 reference. Below a 3:2 aspect (4:3, 5:4) matching
    // width keeps the full 1600-unit width on screen and gives the extra height to the layout, instead of
    // cutting off the right and left edges. Wider screens keep the authored 0.5 blend.
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class GripUiAspectFit : MonoBehaviour
    {
        public const float NarrowAspect = 1.5f;
        CanvasScaler scaler;
        float authored = -1;
        int width, height;

        void Awake() { scaler = GetComponent<CanvasScaler>(); authored = scaler.matchWidthOrHeight; Apply(); }

        void Update() { if (Screen.width != width || Screen.height != height) Apply(); }

        void Apply()
        {
            width = Screen.width; height = Screen.height;
            if (scaler == null || height <= 0) return;
            scaler.matchWidthOrHeight = width / (float)height < NarrowAspect ? 0f : authored;
        }
    }
}
