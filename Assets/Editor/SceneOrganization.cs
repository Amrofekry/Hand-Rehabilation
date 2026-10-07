using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GripRehab.Composition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GripRehab.Editor
{
    [InitializeOnLoad]
    public static class SceneOrganization
    {
        const string Folder = "Assets/Scenes/Workspace";
        const string Backup = "Assets/Scenes/Archive/GripRehab-BeforeSceneSplit.unity";
        static bool saving, converting;
        static readonly string[] Names = { "3.1-Apartment", "3.1-Lighting", "3.2-PlayerAndSensors", "3.3-VirtualHands", "3.4.1-Interaction", "3.4-Interface" };
        static SceneOrganization()
        {
            EditorSceneManager.sceneSaving += BeforeSave;
            EditorSceneManager.sceneSaved += AfterSave;
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += BindWorkspace;
            EditorApplication.playModeStateChanged += state => {
                var layout = SceneComposition.Current;
                if (layout == null) return;
                if (state == PlayModeStateChange.ExitingEditMode && !SceneManager.GetSceneByPath(SceneComposition.Main).isLoaded)
                {
                    EditorApplication.isPlaying = false;
                    Debug.LogWarning("Open Grip Rehab > Scenes > Open Complete Workspace before Play. Content scenes are for editing.");
                    EditorApplication.delayCall += OpenWorkspace;
                    return;
                }
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    foreach (var part in layout.parts)
                    {
                        var scene = SceneManager.GetSceneByPath(part.path);
                        if (!scene.isLoaded) continue;
                        foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
                        layout.Clear(scene);
                    }
                }
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += BindWorkspace;
            };
        }
        static void BeforeSave(Scene scene, string path)
        {
            var layout = SceneComposition.Current;
            if (converting || layout == null) return;
            if (path == SceneComposition.Main && SceneManager.GetActiveScene() == scene)
            { RenderSettings.sun = null; saving = true; return; }
            if (!layout.parts.Any(p => p.path == path)) return;
            saving = true; layout.Clear(scene, true);
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        }
        static void AfterSave(Scene scene)
        { if (!saving) return; saving = false; EditorApplication.delayCall += BindWorkspace; }
        static void BindWorkspace()
        {
            var layout = SceneComposition.Current;
            if (converting || layout == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (layout.Loaded) { layout.Bind(); layout.Activate(true); }
            else foreach (var part in layout.parts)
            {
                var scene = SceneManager.GetSceneByPath(part.path);
                if (scene.isLoaded) foreach (var root in scene.GetRootGameObjects()) root.SetActive(true);
            }
        }
        [MenuItem("Grip Rehab/Scenes/Open Complete Workspace")]
        public static void OpenWorkspace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before editing scenes.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var layout = SceneComposition.Current;
            EditorSceneManager.OpenScene(SceneComposition.Main);
            if (layout == null) return;
            foreach (var part in layout.parts) EditorSceneManager.OpenScene(part.path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(SceneComposition.Main)); BindWorkspace();
        }
        static int Group(GameObject root)
        {
            if (root.GetComponent<Canvas>() != null || root.GetComponent<EventSystem>() != null) return 5;
            if (root.name.Contains("First person patient")) return 2;
            if (root.name.Contains("VirtualGrasp visible hands")) return 3;
            if (root.name.Contains("Force interaction stations")) return 4;
            if (root.name.Contains("Daylight and room bounce")) return 1;
            return 0;
        }
        static string Id(GameObject obj)
        {
            var marker = obj.GetComponent<SceneReferenceId>() ?? obj.AddComponent<SceneReferenceId>();
            if (string.IsNullOrEmpty(marker.id)) marker.id = Guid.NewGuid().ToString("N");
            EditorUtility.SetDirty(marker); return marker.id;
        }
        static GameObject ObjectGameObject(UnityEngine.Object value) => value is Component c ? c.gameObject : value as GameObject;
        static List<SceneLink> Capture(Scene scene)
        {
            var links = new List<SceneLink>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var owner in root.GetComponentsInChildren<Component>(true))
                {
                    if (owner == null) throw new Exception("Missing script in source scene.");
                    var serialized = new SerializedObject(owner); var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = property.objectReferenceValue; var target = ObjectGameObject(value);
                        if (target == null || target.scene != scene || EditorUtility.IsPersistent(value) || Group(target.transform.root.gameObject) == Group(root)) continue;
                        SceneComposition.Assign(owner, property.propertyPath, value); // Fail before moving if unsupported.
                        links.Add(new SceneLink { ownerId = Id(owner.gameObject), ownerComponent = Array.IndexOf(owner.GetComponents<Component>(), owner),
                            property = property.propertyPath, targetId = Id(target), targetComponent = value is Component component ? Array.IndexOf(target.GetComponents<Component>(), component) : -1 });
                    }
                }
            return links;
        }
        [MenuItem("Grip Rehab/Scenes/Split Authored Scene Once")]
        public static void Split()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before scene migration.");
            if (SceneComposition.Current != null) { OpenWorkspace(); Verify(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException("Scene split canceled.");
            var source = EditorSceneManager.OpenScene(SceneComposition.Main);
            var roots = source.GetRootGameObjects();
            if (roots.Length < 7 || !roots.Any(r => r.GetComponent<Canvas>() != null)) throw new Exception("The expected authored scene is not loaded.");
            foreach (var name in Names) if (File.Exists(Folder + "/" + name + ".unity")) throw new Exception("Refusing to replace an existing scene: " + name);
            if (File.Exists(Backup) && !File.ReadAllBytes(Backup).SequenceEqual(File.ReadAllBytes(SceneComposition.Main)))
                throw new Exception("A different source backup already exists; it will not be replaced.");
            Directory.CreateDirectory("Assets/Scenes/Archive"); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            if (!File.Exists(Backup) && !AssetDatabase.CopyAsset(SceneComposition.Main, Backup)) throw new Exception("Could not preserve source scene.");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/scenes-build-settings-before.json", JsonUtility.ToJson(new BuildBackup { scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray() }, true));
            var oldBuild = EditorBuildSettings.scenes;
            converting = true;
            try
            {
                var layout = ScriptableObject.CreateInstance<SceneComposition>();
                layout.sunLightId = RenderSettings.sun != null ? Id(RenderSettings.sun.gameObject) : null;
                layout.links = Capture(source).ToArray();
                var parts = new List<ScenePart>();
                var scenes = new List<Scene>();
                for (int group = 0; group < Names.Length; group++)
                {
                    var destination = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    var container = new GameObject(Names[group] + " / content");
                    SceneManager.MoveGameObjectToScene(container, destination); container.SetActive(false);
                    string rootId = Id(container);
                    foreach (var root in roots.Where(r => Group(r) == group))
                    { SceneManager.MoveGameObjectToScene(root, destination); root.transform.SetParent(container.transform, false); }
                    if (container.transform.childCount == 0) throw new Exception("Empty scene partition: " + Names[group]);
                    parts.Add(new ScenePart { path = Folder + "/" + Names[group] + ".unity", rootId = rootId }); scenes.Add(destination);
                    layout.Clear(destination, true);
                    if (!EditorSceneManager.SaveScene(destination, parts[group].path)) throw new Exception("Scene save failed.");
                }
                layout.parts = parts.ToArray();
                AssetDatabase.CreateAsset(layout, SceneComposition.Asset); AssetDatabase.SaveAssets();
                foreach (var scene in scenes) layout.Clear(scene, true);
                for (int i = 0; i < scenes.Count; i++) if (!EditorSceneManager.SaveScene(scenes[i], parts[i].path)) throw new Exception("Scene save failed.");
                var boot = new GameObject("00 / Workspace bootstrap"); SceneManager.MoveGameObjectToScene(boot, source);
                boot.AddComponent<SceneBootstrap>().composition = layout;
                RenderSettings.sun = null;
                if (!EditorSceneManager.SaveScene(source)) throw new Exception("Bootstrap save failed.");
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneComposition.Main, true) }
                    .Concat(parts.Select(p => new EditorBuildSettingsScene(p.path, true)))
                    .Concat(oldBuild.Where(s => s.path != SceneComposition.Main)).ToArray();
                AssetDatabase.SaveAssets();
                converting = false; OpenWorkspace(); Verify();
            }
            catch
            {
                converting = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.Copy(Backup, SceneComposition.Main, true);
                AssetDatabase.DeleteAsset(SceneComposition.Asset);
                foreach (var name in Names) AssetDatabase.DeleteAsset(Folder + "/" + name + ".unity");
                EditorBuildSettings.scenes = oldBuild; AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(SceneComposition.Main); throw;
            }
        }
        public static void EnsureWorkspace()
        {
            var layout = SceneComposition.Current;
            if (layout != null && !layout.Loaded) OpenWorkspace();
            else if (layout != null) BindWorkspace();
        }
        public static string[] BuildScenePaths()
        {
            var layout = SceneComposition.Current;
            if (layout == null) return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            return new[] { SceneComposition.Main }.Concat(layout.parts.Select(p => p.path))
                .Concat(EditorBuildSettings.scenes.Where(s => s.enabled && s.path != SceneComposition.Main && !s.path.StartsWith("Assets/Scenes/Archive/"))
                    .Select(s => s.path)).Distinct().ToArray();
        }
        [MenuItem("Grip Rehab/Scenes/Configure Build Scenes")]
        public static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = BuildScenePaths().Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorBuildSettings.globalScenes = EditorBuildSettings.scenes;
            AssetDatabase.SaveAssets();
            if (!EditorBuildSettings.globalScenes.Select(s => s.path).SequenceEqual(BuildScenePaths())) throw new Exception("Build scene list did not update.");
            File.WriteAllText("Logs/scene-build-settings.json", JsonUtility.ToJson(new BuildBackup { scenes = EditorBuildSettings.globalScenes.Select(s => s.path).ToArray() }, true));
        }
        public static void VerifyEditPersistence()
        {
            EnsureWorkspace();
            var item = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>().holdAnchor;
            var original = item.localPosition;
            try
            {
                item.localPosition = original + Vector3.right * .01f;
                EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
                if (!EditorSceneManager.SaveScene(item.gameObject.scene)) throw new Exception("Part scene save failed.");
                OpenWorkspace();
                item = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>().holdAnchor;
                if (Vector3.Distance(item.localPosition, original + Vector3.right * .01f) > .0001f) throw new Exception("Saved edit lost on reopen.");
            }
            finally
            {
                item.localPosition = original;
                EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
                EditorSceneManager.SaveScene(item.gameObject.scene);
            }
            OpenWorkspace(); Verify(); ConfigureBuildScenes();
            var paths = new[] { SceneComposition.Main }.Concat(SceneComposition.Current.parts.Select(p => p.path)).ToArray();
            var bytes = paths.ToDictionary(p => p, File.ReadAllBytes);
            RehabVerification.BuildWindows();
            foreach (var path in paths) if (!bytes[path].SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Build changed scene: " + path);
            File.WriteAllText("Logs/scene-persistence-checks.json", JsonUtility.ToJson(new Report { passed = true, scenes = paths,
                references = SceneComposition.Current.links.Length, utc = DateTime.UtcNow.ToString("O") }, true));
        }
        [MenuItem("Grip Rehab/Scenes/Verify Composition")]
        public static void Verify()
        {
            var layout = SceneComposition.Current ?? throw new Exception("Scene composition has not been created.");
            EnsureWorkspace(); layout.Bind();
            if (layout.parts.Length != 6) throw new Exception("Expected six focused content scenes.");
            if (UnityEngine.Object.FindObjectsByType<SceneBootstrap>().Length != 1) throw new Exception("Expected one bootstrap.");
            foreach (var part in layout.parts)
            {
                var scene = SceneManager.GetSceneByPath(part.path);
                if (scene.GetRootGameObjects().Length != 1) throw new Exception("Expected one organized content group per scene.");
                foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script in " + part.path);
            }
            ApartmentAuthoring.VerifyScene();
            File.WriteAllText("Logs/scene-composition-checks.json", JsonUtility.ToJson(new Report { passed = true, references = layout.links.Length,
                scenes = layout.parts.Select(p => p.path).ToArray(), utc = DateTime.UtcNow.ToString("O") }, true));
        }
        [Serializable] sealed class BuildBackup { public string[] scenes; }
        [Serializable] sealed class Report { public bool passed; public int references; public string[] scenes; public string utc; }
    }
}
