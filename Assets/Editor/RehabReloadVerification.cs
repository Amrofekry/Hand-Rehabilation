using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    // Opt-in native SDK reload regression. Never alters the authored scene.
    [InitializeOnLoad]
    public static class RehabReloadVerification
    {
        const string Active="GripRehab.ReloadCheck";
        const string Phase="GripRehab.ReloadPhase";
        static double readyAt;
        static RehabReloadVerification()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>{
                if(UnityEditor.SessionState.GetBool(Active,false)&&
                   (type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
                    UnityEditor.SessionState.SetInt("GripRehab.ReloadErrors",UnityEditor.SessionState.GetInt("GripRehab.ReloadErrors",0)+1);
            };
            AssemblyReloadEvents.afterAssemblyReload+=()=>{
                if(UnityEditor.SessionState.GetBool(Active,false)&&UnityEditor.SessionState.GetInt(Phase,0)==1)
                    UnityEditor.SessionState.SetInt(Phase,2);
            };
        }
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            readyAt=0;
            UnityEditor.SessionState.SetBool(Active,true);
            UnityEditor.SessionState.SetInt(Phase,0);
            UnityEditor.SessionState.SetInt("GripRehab.ReloadErrors",0);
            UnityEditor.SessionState.SetString("GripRehab.ReloadStarted",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!UnityEditor.SessionState.GetBool(Active,false)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(DateTime.TryParse(UnityEditor.SessionState.GetString("GripRehab.ReloadStarted",""),out var started)&&DateTime.UtcNow-started.ToUniversalTime()>TimeSpan.FromSeconds(90)){
                UnityEditor.SessionState.SetBool(Active,false);
                File.WriteAllText("Logs/native-reload-check.json","{\"status\":\"FAIL\",\"detail\":\"Reload regression timed out\"}");
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                return;
            }
            int phase=UnityEditor.SessionState.GetInt(Phase,0);
            if(phase==0&&EditorApplication.isPlaying){
                var sdk=UnityEngine.Object.FindAnyObjectByType<RehabVirtualGrasp>();
                if(sdk==null||!sdk.Ready)return;
                if(readyAt==0)readyAt=EditorApplication.timeSinceStartup+3;
                if(EditorApplication.timeSinceStartup<readyAt)return;
                UnityEditor.SessionState.SetInt(Phase,1);
                File.WriteAllText("Logs/native-reload-check.json","{\"status\":\"RUNNING\",\"nativeReady\":true}");
                EditorUtility.RequestScriptReload();
            }
            if(phase==2&&!EditorApplication.isPlayingOrWillChangePlaymode){
                int errors=UnityEditor.SessionState.GetInt("GripRehab.ReloadErrors",0);
                UnityEditor.SessionState.SetBool(Active,false);
                File.WriteAllText("Logs/native-reload-check.json",JsonUtility.ToJson(new Result{
                    utc=DateTime.UtcNow.ToString("O"),started=UnityEditor.SessionState.GetString("GripRehab.ReloadStarted",""),
                    status=errors==0?"PASS":"FAIL",errors=errors,playing=false,nativeReadyBeforeReload=true},true));
            }
        }
        [Serializable]sealed class Result{public string utc,started,status;public int errors;public bool playing,nativeReadyBeforeReload;}
    }
}
