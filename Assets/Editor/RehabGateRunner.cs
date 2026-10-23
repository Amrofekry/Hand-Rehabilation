using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    // Repeatable, fixed Editor gate commands for this local project. No input
    // text is evaluated as C# or passed to a shell. The request file is under
    // ignored Logs so it never becomes a build asset or a patient record.
    [InitializeOnLoad]
    public static class RehabGateRunner
    {
        static string LogsPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
        static string Request => Path.Combine(LogsPath, "editor-gate.request");
        static string Result => Path.Combine(LogsPath, "editor-gate-result.json");
        static double nextPoll;
        static UnityEditor.PackageManager.Requests.AddRequest particleRepair;
        static double packageDeadline;

        static RehabGateRunner()
        {
            EditorApplication.update += Poll;
            if (UnityEditor.SessionState.GetBool("GripRehab.ParticleRepair", false))
            {
                UnityEditor.SessionState.SetBool("GripRehab.ParticleRepair", false);
                EditorApplication.delayCall += () => {
                    bool restored = Array.Exists(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages(), p => p.name == "com.unity.modules.particlesystem");
                    Write(restored ? "PASS" : "FAIL", "repair-particles", restored ? "Particle module is registered after Editor reload" : "Particle module is still unavailable after reload");
                };
            }
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (particleRepair != null)
            {
                if (!particleRepair.IsCompleted && EditorApplication.timeSinceStartup < packageDeadline) return;
                bool success = particleRepair.IsCompleted && particleRepair.Status == UnityEditor.PackageManager.StatusCode.Success;
                var detail = success ? "Built-in particle module restored" : particleRepair.Error?.message ?? "Particle module repair timed out";
                particleRepair = null; UnityEditor.SessionState.SetBool("GripRehab.ParticleRepair", false);
                Write(success ? "PASS" : "FAIL", "repair-particles", detail);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Request)) return;
            string command;
            try
            {
                command = File.ReadAllText(Request).Trim();
                File.Delete(Request);
                Directory.CreateDirectory(LogsPath);
                switch (command)
                {
                    case "repair-particles":
                        UnityEditor.SessionState.SetBool("GripRehab.ParticleRepair", true);
                        particleRepair = UnityEditor.PackageManager.Client.Add("com.unity.modules.particlesystem@1.0.0");
                        packageDeadline = EditorApplication.timeSinceStartup + 60;
                        Write("RUNNING", command, "Restoring the built-in module required by imported apartment assets");
                        break;
                    case "verify":
                        RehabVerification.PrepareAndVerify();
                        Write("PASS", command, "Scene, logic and references verified");
                        break;
                    case "merge-scenes":
                        SceneOrganization.Consolidate();
                        Write("PASS", command, "Current authored content merged and native references saved in one scene");
                        break;
                    case "split-scenes":
                        SceneOrganization.Split();
                        Write("PASS", command, "Additive scene composition migrated and reference-checked");
                        break;
                    case "flatten-scenes":
                        SceneOrganization.Flatten();
                        Write("PASS", command, "Scene assets moved into one flat folder with GUIDs preserved");
                        break;
                    case "scene-workspace":
                        SceneOrganization.OpenWorkspace();
                        Write("PASS", command, "Complete additive scene workspace open");
                        break;
                    case "verify-scenes":
                        SceneOrganization.Verify();
                        Write("PASS", command, "Scene composition references verified");
                        break;
                    case "configure-scenes":
                        SceneOrganization.ConfigureBuildScenes();
                        Write("PASS", command, "Bootstrap and all content scenes saved in Build Settings");
                        break;
                    case "scene-qa":
                        SceneCompositionQA.Begin();
                        Write("RUNNING", command, "Three additive-load Play/Stop cycles started");
                        break;
                    case "ui-fix":
                        RehabPremiumSkin.RepairLegibility();
                        Write("PASS", command, "Saved HUD and dropdown contrast repaired and verified");
                        break;
                    case "module-ui":
                        RehabPremiumSkin.EnableModuleCards();
                        Write("PASS", command, "Saved cards now show Modules 02 and 03 as playable");
                        break;
                    case "theme-ui":
                        GripUiThemeAuthoring.Install();
                        Write("PASS", command, "Saved dark, light and high-contrast theme controls");
                        break;
                    case "tmp-essentials":
                        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
                        Write("PASS", command, "TMP essentials import requested without an interactive dialog");
                        break;
                    case "accessibility-ui":
                        GripUiAccessibilityAuthoring.Install();
                        Write("PASS", command, "Saved presentation accessibility controls and semantic themes");
                        break;
                    case "workspace":
                        RehabEditorQA.OpenWorkspace();
                        Write("PASS", command, "Main scene open outside Play with workspace panels");
                        break;
                    case "persistence":
                        RehabEditorQA.VerifyEditPersistence();
                        Write("PASS", command, "Visual edit/save/reopen verified; Windows build preserved main scene bytes");
                        break;
                    case "refresh":
                        AssetDatabase.Refresh();
                        Write("PASS", command, "Asset refresh requested");
                        break;
                    case "verify-accessibility":
                        GripUiAccessibilityAuthoring.Verify();
                        Write("PASS", command, "Accessibility references and theme contrast verified");
                        break;
                    case "gallery-ui":
                        GripUiGalleryAuthoring.Install();
                        Write("PASS", command, "Saved isolated UI gallery, eight component templates and ambient background");
                        break;
                    case "verify-gallery":
                        GripUiGalleryAuthoring.Verify();
                        Write("PASS", command, "Saved UI gallery templates and background references verified");
                        break;
                    case "import-ui-screenshots":
                        foreach (var resolution in new[] { "1280x720", "1600x900" })
                            for (int palette = 0; palette < 3; palette++)
                                foreach (var view in new[] { "home", "appearance", "appearance-large" })
                                {
                                    var name = "theme-" + palette + "-" + view + ".png";
                                    var source = Path.Combine(LogsPath, "ui-screenshots-" + resolution, name);
                                    if (!File.Exists(source) || new FileInfo(source).Length < 1024)
                                        throw new Exception("UI screenshot missing: " + source);
                                    var folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                        "Docs", "screenshots", palette == 0 ? "dark" : palette == 1 ? "light" : "contrast", "en", resolution);
                                    Directory.CreateDirectory(folder); File.Copy(source, Path.Combine(folder, view + ".png"), true);
                                }
                        Write("PASS", command, "Eighteen validated theme screenshots imported to Docs");
                        break;
                    case "dwell-check":
                        RehabDwellVerification.Run();
                        Write("PASS", command, "Hands-free pointer fixture checks passed");
                        break;
                    case "reload-check":
                        RehabReloadVerification.Begin();
                        Write("RUNNING", command, "Native SDK active-Play assembly reload regression started");
                        break;
                    case "import-player-screenshots":
                        var screenshotSource = Path.Combine(LogsPath, "player-screenshots");
                        var screenshotDestination = Path.Combine(
                            Directory.GetParent(Application.dataPath).FullName,
                            "Docs", "screenshots", "player-1280x720");
                        Directory.CreateDirectory(screenshotDestination);
                        foreach (var name in new[] {
                            "apartment-options", "vg-ball-before", "vg-ball-deformed",
                            "apartment-seated-grip", "vg-mug-fractured", "apartment-kitchen-grip",
                            "apartment-two-hands-tracked", "vg-left-kitchen-grip",
                            "apartment-overview", "ui-main-menu", "ui-new-profile",
                            "ui-module-selection" })
                        {
                            var source = Path.Combine(screenshotSource, name + ".png");
                            if (!File.Exists(source) || new FileInfo(source).Length < 1024)
                                throw new Exception("Player screenshot missing: " + name);
                            File.Copy(source, Path.Combine(screenshotDestination, name + ".png"), true);
                        }
                        Write("PASS", command, "Twelve validated player screenshots copied to Docs");
                        break;
                    case "cleanup-qa":
                        if (EditorApplication.isPlaying) throw new Exception("Leave Play mode first");
                        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                        int removed = 0;
                        foreach (var item in scene.GetRootGameObjects())
                            if (item.name == "Editor acceptance" && item.GetComponent<ApartmentAcceptance>() != null)
                            { UnityEngine.Object.DestroyImmediate(item); removed++; }
                        if (removed > 0)
                        {
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                        }
                        Write("PASS", command, "Removed " + removed + " saved test-only acceptance objects");
                        break;
                    case "qa":
                        RehabEditorQA.Begin();
                        Write("RUNNING", command, "Three Play/Stop cycles started; see Logs/editor-qa.txt");
                        break;
                    case "journey-qa":
                        ResearchJourneyQA.Begin();
                        Write("RUNNING", command, "Three isolated account journey Play/Stop cycles requested");
                        break;
                    case "build":
                        RehabVerification.BuildWindows();
                        Write("PASS", command, "Windows build completed");
                        break;
                    default:
                        Write("FAIL", command, "Unknown fixed gate command");
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Write("FAIL", "gate", error.Message);
            }
        }

        static void Write(string status, string command, string detail)
        {
            Directory.CreateDirectory(LogsPath);
            File.WriteAllText(Result, JsonUtility.ToJson(new GateResult
            {
                status = status, command = command, detail = detail,
                utc = DateTime.UtcNow.ToString("O")
            }, true));
        }

        internal static void CompleteQa(bool passed, string detail) =>
            Write(passed ? "PASS" : "FAIL", "qa", detail);
        internal static void CompleteSceneQa(bool passed, string detail) =>
            Write(passed ? "PASS" : "FAIL", "scene-qa", detail);
        internal static void CompleteJourneyQa(bool passed, string detail) =>
            Write(passed ? "PASS" : "FAIL", "journey-qa", detail);

        [Serializable]
        sealed class GateResult
        {
            public string status, command, detail, utc;
        }
    }
}
