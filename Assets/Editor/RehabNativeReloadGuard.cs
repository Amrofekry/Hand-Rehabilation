using UnityEditor;
using VirtualGrasp;

namespace GripRehab.Editor
{
    // The imported native SDK has process lifetime; managed script reload does
    // not unload its DLL. Release it before that boundary; Unity owns the
    // Play transition and must not be interrupted inside assembly reload.
    [InitializeOnLoad]
    public static class RehabNativeReloadGuard
    {
        const string Live = "GripRehab.NativeLivePlay";
        static RehabNativeReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += () => {
                if(UnityEditor.SessionState.GetBool(Live,false)&&EditorApplication.isPlaying)
                    EditorApplication.delayCall += () => {if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();};
            };
            EditorApplication.playModeStateChanged += state => {
                if(state==PlayModeStateChange.EnteredPlayMode)UnityEditor.SessionState.SetBool(Live,true);
                else if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.ExitingEditMode)
                    UnityEditor.SessionState.SetBool(Live,false);
            };
        }
        static void BeforeReload()
        {
            // Do not cancel the normal domain reload on entry into Play.
            if(!EditorApplication.isPlaying||!UnityEditor.SessionState.GetBool(Live,false))return;
            VG_Controller.Release(); // SDK's Release is null-guarded/idempotent.
        }
    }
}
