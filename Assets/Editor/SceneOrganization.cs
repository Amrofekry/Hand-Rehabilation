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
        const string Folder = "Assets/Scenes";
        const string Backup = "Assets/SceneBackups/GripRehab-BeforeSceneSplit.unity";
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
            Directory.CreateDirectory(Path.GetDirectoryName(Backup)); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
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
            if (converting) return;
            var layout = SceneComposition.Current;
            if (layout != null && !layout.Loaded) OpenWorkspace();
            else if (layout != null) BindWorkspace();
        }
        [MenuItem("Grip Rehab/Scenes/Flatten Scene Folder")]
        public static void Flatten()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before moving scene assets.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException("Scene move canceled.");
            var layout = SceneComposition.Current ?? throw new Exception("Scene composition is missing.");
            var originalParts = layout.parts.Select(p => p.path).ToArray();
            var originalBuild = EditorBuildSettings.scenes;
            var moves = new List<AssetMove>();
            if (File.Exists("Assets/Scenes/GripRehab.unity")) moves.Add(new AssetMove { from = "Assets/Scenes/GripRehab.unity", to = SceneComposition.Main });
            foreach (var part in layout.parts)
            {
                var destination = Folder + "/" + Path.GetFileName(part.path);
                if (part.path != destination) moves.Add(new AssetMove { from = part.path, to = destination });
            }
            if (File.Exists("Assets/Scenes/Archive/GripRehab-BeforeSceneSplit.unity"))
                moves.Add(new AssetMove { from = "Assets/Scenes/Archive/GripRehab-BeforeSceneSplit.unity", to = Backup });
            if (File.Exists("Assets/Scenes/README.md")) moves.Add(new AssetMove { from = "Assets/Scenes/README.md", to = "Assets/SceneBackups/README.md" });
            foreach (var move in moves)
            {
                if (!File.Exists(move.from) || File.Exists(move.to)) throw new Exception("Unsafe asset destination: " + move.to);
                move.guid = AssetDatabase.AssetPathToGUID(move.from);
                move.bytes = File.ReadAllBytes(move.from);
            }
            var completed = new List<AssetMove>();
            converting = true;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory("Assets/SceneBackups"); AssetDatabase.Refresh();
                foreach (var move in moves)
                {
                    var error = AssetDatabase.MoveAsset(move.from, move.to);
                    if (!string.IsNullOrEmpty(error)) throw new Exception(error);
                    completed.Add(move);
                    if (AssetDatabase.AssetPathToGUID(move.to) != move.guid || !move.bytes.SequenceEqual(File.ReadAllBytes(move.to)))
                        throw new Exception("Asset identity/content changed while moving: " + move.to);
                }
                // Closing all scenes/refreshing can unload the Resources instance.
                layout = AssetDatabase.LoadAssetAtPath<SceneComposition>(SceneComposition.Asset);
                if (layout == null) throw new Exception("Composition asset was unavailable after asset moves.");
                foreach (var part in layout.parts) part.path = Folder + "/" + Path.GetFileName(part.path);
                EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets();
                EditorBuildSettings.scenes = originalBuild.Select(s => new EditorBuildSettingsScene(
                    moves.FirstOrDefault(m => m.from == s.path)?.to ?? s.path, s.enabled)).ToArray();
                ConfigureBuildScenes();
                // Remove only the two known directories after proving they are empty.
                foreach (var oldFolder in new[] { "Assets/Scenes/Workspace", "Assets/Scenes/Archive" })
                    if (Directory.Exists(oldFolder))
                    {
                        if (Directory.EnumerateFileSystemEntries(oldFolder).Any()) throw new Exception("Old scene folder contains unexpected files: " + oldFolder);
                        if (!AssetDatabase.DeleteAsset(oldFolder)) throw new Exception("Could not remove empty scene folder: " + oldFolder);
                    }
                converting = false; OpenWorkspace(); Verify();
                if (Directory.GetDirectories(Folder).Length != 0) throw new Exception("Scenes still contains a subfolder.");
                File.WriteAllText("Logs/scene-flatten-checks.json", JsonUtility.ToJson(new FlatReport { passed = true,
                    entry = SceneComposition.Main, scenes = layout.parts.Select(p => p.path).ToArray(),
                    backup = Backup, moves = moves.ToArray(), utc = DateTime.UtcNow.ToString("O") }, true));
            }
            catch
            {
                converting = true; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (var move in completed.AsEnumerable().Reverse())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(move.from)); AssetDatabase.Refresh();
                    if (!string.IsNullOrEmpty(AssetDatabase.MoveAsset(move.to, move.from))) Debug.LogError("Asset rollback failed: " + move.from);
                }
                layout = AssetDatabase.LoadAssetAtPath<SceneComposition>(SceneComposition.Asset);
                for (int i = 0; i < layout.parts.Length; i++) layout.parts[i].path = originalParts[i];
                EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets(); EditorBuildSettings.scenes = originalBuild;
                EditorBuildSettings.globalScenes = originalBuild;
                converting = false; throw;
            }
        }

        [MenuItem("Grip Rehab/Scenes/Restore Single Scene")]
        public static void Consolidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before merging scenes.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException();
            var layout = SceneComposition.Current;
            if (layout == null) { OpenWorkspace(); Verify(); return; }
            var partPaths = layout.parts.Select(p => p.path).ToArray();
            const string oldEntry = "Assets/Scenes/3.1-Bootstrap.unity";
            if (File.Exists(SceneComposition.Main)) throw new Exception("Refusing to replace an existing working scene.");
            if (!File.Exists("Docs/evidence/single-scene-2026-10-07/before-merge.zip")) throw new Exception("Recovery snapshot missing.");
            var entryGuid = AssetDatabase.AssetPathToGUID(oldEntry);
            converting = true;
            try
            {
                var destination = EditorSceneManager.OpenScene(oldEntry);
                foreach (var path in partPaths) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(destination);
                layout = AssetDatabase.LoadAssetAtPath<SceneComposition>(SceneComposition.Asset);
                layout.Bind();
                var linkedObjects = new Dictionary<string, GameObject>();
                foreach (var scenePath in partPaths)
                    foreach (var root in SceneManager.GetSceneByPath(scenePath).GetRootGameObjects())
                        foreach (var marker in root.GetComponentsInChildren<SceneReferenceId>(true)) linkedObjects.Add(marker.id, marker.gameObject);
                // Keep actual native object references, including inherited prefab fields.
                var references = layout.links.Select(link => new SavedReference {
                    owner = linkedObjects[link.ownerId].GetComponents<Component>()[link.ownerComponent],
                    value = link.targetComponent < 0 ? (UnityEngine.Object)linkedObjects[link.targetId] : linkedObjects[link.targetId].GetComponents<Component>()[link.targetComponent],
                    property = link.property }).ToArray();
                int expectedObjects = partPaths.Sum(path => SceneManager.GetSceneByPath(path).GetRootGameObjects()
                    .Sum(root => root.GetComponentsInChildren<Transform>(true).Length - 1));
                foreach (var path in partPaths)
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    foreach (var container in scene.GetRootGameObjects())
                    {
                        while (container.transform.childCount > 0)
                        {
                            var child = container.transform.GetChild(0);
                            child.SetParent(null, true);
                            SceneManager.MoveGameObjectToScene(child.gameObject, destination);
                        }
                        UnityEngine.Object.DestroyImmediate(container);
                    }
                    EditorSceneManager.CloseScene(scene, true);
                }
                foreach (var reference in references)
                {
                    var serialized = new SerializedObject(reference.owner);
                    var field = serialized.FindProperty(reference.property);
                    if (field == null) throw new Exception("Missing native field: " + reference.property);
                    field.objectReferenceValue = reference.value;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.IsPartOfPrefabInstance(reference.owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(reference.owner);
                    EditorUtility.SetDirty(reference.owner);
                }
                foreach (var root in destination.GetRootGameObjects())
                    if (root.GetComponent<SceneBootstrap>() != null) UnityEngine.Object.DestroyImmediate(root);
                foreach (var marker in destination.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneReferenceId>(true)).ToArray())
                    UnityEngine.Object.DestroyImmediate(marker);
                var error = AssetDatabase.MoveAsset(oldEntry, SceneComposition.Main);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
                if (AssetDatabase.AssetPathToGUID(SceneComposition.Main) != entryGuid) throw new Exception("Main scene GUID changed.");
                EditorSceneManager.MarkSceneDirty(destination);
                if (!EditorSceneManager.SaveScene(destination, SceneComposition.Main)) throw new Exception("Merged scene save failed.");
                var checks = references.Select(r => new PersistedReference {
                    ownerId = GlobalObjectId.GetGlobalObjectIdSlow(r.owner).ToString(),
                    targetId = GlobalObjectId.GetGlobalObjectIdSlow(r.value).ToString(), property = r.property }).ToArray();
                // Re-open before deleting source scenes; verify persisted references,
                // object counts, prefab modifications and actual scene scripts/materials.
                destination = EditorSceneManager.OpenScene(SceneComposition.Main);
                foreach (var check in checks)
                {
                    if (!GlobalObjectId.TryParse(check.ownerId, out var ownerId) || !GlobalObjectId.TryParse(check.targetId, out var targetId)) throw new Exception("Invalid persisted reference identity.");
                    var owner = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(ownerId);
                    var target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(targetId);
                    if (owner == null || target == null || new SerializedObject(owner).FindProperty(check.property)?.objectReferenceValue != target)
                        throw new Exception("Reference lost on reload: " + check.property);
                }
                int actualObjects = destination.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
                if (actualObjects != expectedObjects) throw new Exception("Authored objects changed during merge.");
                ApartmentAuthoring.VerifyScene();
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneComposition.Main, true) }
                    .Concat(EditorBuildSettings.scenes.Where(s => s.enabled && s.path != oldEntry && s.path != SceneComposition.Main &&
                        !partPaths.Contains(s.path) && !s.path.StartsWith("Assets/SceneBackups/"))).ToArray();
                EditorBuildSettings.globalScenes = EditorBuildSettings.scenes;
                foreach (var path in partPaths) if (!AssetDatabase.DeleteAsset(path)) throw new Exception("Cannot remove merged partition: " + path);
                if (!AssetDatabase.DeleteAsset(SceneComposition.Asset)) throw new Exception("Cannot remove retired composition manifest.");
                AssetDatabase.SaveAssets();
                File.WriteAllText("Logs/single-scene-checks.json", JsonUtility.ToJson(new MergeReport {
                    passed = true, scene = SceneComposition.Main, guid = entryGuid, references = checks.Length,
                    objects = actualObjects, retired = partPaths, bindings = checks, utc = DateTime.UtcNow.ToString("O") }, true));
            }
            finally { converting = false; }
        }
        sealed class SavedReference { public Component owner; public UnityEngine.Object value; public string property; }
        [Serializable] sealed class PersistedReference { public string ownerId, targetId, property; }
        [Serializable] sealed class MergeReport { public bool passed; public string scene, guid, utc; public int references, objects; public string[] retired; public PersistedReference[] bindings; }

        public static string[] BuildScenePaths()
        {
            var layout = SceneComposition.Current;
            if (layout == null) return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            return new[] { SceneComposition.Main }.Concat(layout.parts.Select(p => p.path))
                .Concat(EditorBuildSettings.scenes.Where(s => s.enabled && s.path != SceneComposition.Main &&
                    !s.path.StartsWith("Assets/Scenes/Archive/") && !s.path.StartsWith("Assets/SceneBackups/"))
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
            var paths = new[] { SceneComposition.Main }.Concat(SceneComposition.Current == null ? Array.Empty<string>() : SceneComposition.Current.parts.Select(p => p.path)).ToArray();
            var bytes = paths.ToDictionary(p => p, File.ReadAllBytes);
            RehabVerification.BuildWindows();
            foreach (var path in paths) if (!bytes[path].SequenceEqual(File.ReadAllBytes(path))) throw new Exception("Build changed scene: " + path);
            File.WriteAllText("Logs/scene-persistence-checks.json", JsonUtility.ToJson(new Report { passed = true, scenes = paths,
                references = SceneComposition.Current == null ? 0 : SceneComposition.Current.links.Length, utc = DateTime.UtcNow.ToString("O") }, true));
        }
        [MenuItem("Grip Rehab/Scenes/Verify Composition")]
        public static void Verify()
        {
            var layout = SceneComposition.Current;
            if (layout == null) { ApartmentAuthoring.VerifyScene(); return; }
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
        [Serializable] sealed class AssetMove { public string from, to, guid; [NonSerialized] public byte[] bytes; }
        [Serializable] sealed class FlatReport { public bool passed; public string entry, backup, utc; public string[] scenes; public AssetMove[] moves; }
        [Serializable] sealed class Report { public bool passed; public int references; public string[] scenes; public string utc; }
    }
}
