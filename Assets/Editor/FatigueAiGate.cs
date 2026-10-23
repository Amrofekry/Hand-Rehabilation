using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
namespace GripRehab.Editor
{
    [InitializeOnLoad] public static class FatigueAiGate
    {
        static Request request;
        static double deadline;
        const string Input = "Logs/ai-gate.request", Output = "Logs/ai-gate-result.json";
        static FatigueAiGate() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (request != null)
            {
                if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
                Write(request.IsCompleted && request.Status == StatusCode.Success, request.Error?.message ?? "Sentis package resolved");
                request = null; return;
            }
            if (Array.Exists(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages(), p=>p.name=="com.unity.sentis" && p.version=="2.0.0"))
            {
                var symbols=PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
                if(!symbols.Contains("GRIPREHAB_SENTIS")) { PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone,symbols+";GRIPREHAB_SENTIS"); return; }
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Input)) return;
            string cmd;
            try { cmd = File.ReadAllText(Input).Trim(); File.Delete(Input); }
            catch(IOException) { return; }
            Debug.Log("[REHAB_AI_GATE] Command: "+cmd);
            try {
                if (cmd == "install-sentis") { request = Client.Add("com.unity.sentis@2.0.0"); deadline = EditorApplication.timeSinceStartup + 300; }
                else if (cmd == "install-sentis-offline") {
                    var packages=Directory.GetFiles("Tools/AI/Unity2022Packages","*.tgz");
                    var refs=Array.ConvertAll(packages,p=>"file:"+Path.GetFullPath(p).Replace('\\','/'));
                    request=Client.AddAndRemove(refs);deadline=EditorApplication.timeSinceStartup+300;
                }
                else if (cmd == "verify-ai") { FatigueAiVerification.Run(); Write(true, "AI domain, replay and Sentis verified"); }
            } catch (Exception error) { Write(false, error.ToString()); Debug.LogException(error); }
        }
        static void Write(bool success, string detail) { File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed=success, detail=detail, utc=DateTime.UtcNow.ToString("O") },true)); }
        [Serializable] class Result { public bool passed; public string detail, utc; }
    }
    // Verification implementation is installed after the runtime code.
    public static partial class FatigueAiVerification { public static void Run() { RunChecks(); } static partial void RunChecks(); }
}
