using UnityEditor; using UnityEditor.PackageManager; using UnityEditor.PackageManager.Requests; using UnityEngine;
namespace GripRehab.Editor {
public static class RehabPackageSetup {
 static AddAndRemoveRequest request; static double deadline;
 public static void Install() { request=Client.AddAndRemove(new[]{"com.unity.modules.physics@1.0.0","com.unity.modules.animation@1.0.0"}); deadline=EditorApplication.timeSinceStartup+180; EditorApplication.update+=Poll; }
 static void Poll() { if(!request.IsCompleted) { if(EditorApplication.timeSinceStartup>deadline) { Debug.LogError("Package setup timed out"); EditorApplication.Exit(1); } return; } EditorApplication.update-=Poll; if(request.Status==StatusCode.Success) { Debug.Log("[REHAB_PACKAGES] Physics and Animation ready"); EditorApplication.Exit(0); } else {Debug.LogError(request.Error.message);EditorApplication.Exit(1);} }
}}
