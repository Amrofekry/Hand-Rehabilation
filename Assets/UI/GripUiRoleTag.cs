using UnityEngine;

namespace GripRehab.UI
{
    // Explicit semantic binding for authored controls. Legacy graphics retain
    // their inferred binding so listeners and data references stay intact.
    public sealed class GripUiRoleTag : MonoBehaviour
    {
        public GripUiRole role;
        public bool excludeFromTheme;
    }
}
