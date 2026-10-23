using GripRehab.UI;
using UnityEngine;

namespace GripRehab
{
    // Thin UI bridge compiled outside the isolated UI asmdef. Does not own
    // camera lifecycle or landmarks; only configures the existing menu cursor.
    public sealed class GripDwellSettingsAdapter : MonoBehaviour, IGripDwellSettings, IGripUiTestNavigation
    {
        public RehabAppShell shell;
        RehabDwellPointer pointer;
        public void ShowHomeForTest()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-apartment-ui-smoke") < 0)
                throw new System.InvalidOperationException("Requires opt-in isolated UI fixture");
            shell.ShowHome();
        }
        public float DwellSeconds
        {
            get
            {
                if (pointer == null && shell != null) pointer = shell.GetComponent<RehabDwellPointer>();
                return pointer != null ? RehabPointerPolicy.Duration(pointer.dwellSeconds) : 5;
            }
            set
            {
                if (pointer == null && shell != null) pointer = shell.GetComponent<RehabDwellPointer>();
                if (pointer != null) pointer.dwellSeconds = Mathf.Clamp(RehabPointerPolicy.Duration(value), 2, 8);
            }
        }
    }
}
