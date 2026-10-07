using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GripRehab.Composition
{
    [Serializable] public sealed class ScenePart { public string path, rootId; }
    [Serializable] public sealed class SceneLink
    {
        public string ownerId, property, targetId;
        public int ownerComponent, targetComponent; // -1 denotes GameObject.
    }
    public sealed class SceneComposition : ScriptableObject
    {
        public ScenePart[] parts;
        public SceneLink[] links;
        public string sunLightId;
        public const string Main = "Assets/Scenes/3.1-Bootstrap.unity";
        public const string Asset = "Assets/Resources/SceneComposition.asset";
        public static SceneComposition Current => Resources.Load<SceneComposition>("SceneComposition");
        public bool Loaded
        {
            get { foreach (var part in parts) if (!SceneManager.GetSceneByPath(part.path).isLoaded) return false; return true; }
        }
        Dictionary<string, GameObject> Objects()
        {
            var result = new Dictionary<string, GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var marker in root.GetComponentsInChildren<SceneReferenceId>(true))
                        if (!string.IsNullOrEmpty(marker.id))
                        {
                            if (result.ContainsKey(marker.id)) throw new InvalidOperationException("Duplicate scene reference ID: " + marker.id);
                            result.Add(marker.id, marker.gameObject);
                        }
            }
            return result;
        }
        public void Bind()
        {
            var objects = Objects();
            foreach (var link in links)
            {
                if (!objects.TryGetValue(link.ownerId, out var owner) || !objects.TryGetValue(link.targetId, out var target))
                    throw new InvalidOperationException("Scene reference missing: " + link.property + ". Open the complete workspace.");
                var component = owner.GetComponents<Component>()[link.ownerComponent];
                UnityEngine.Object value = link.targetComponent < 0 ? (UnityEngine.Object)target : target.GetComponents<Component>()[link.targetComponent];
                Assign(component, link.property, value);
            }
            if (!string.IsNullOrEmpty(sunLightId) && objects.TryGetValue(sunLightId, out var sun)) RenderSettings.sun = sun.GetComponent<Light>();
        }
        public void Clear(Scene scene, bool recordPrefab = false)
        {
            var objects = Objects();
            foreach (var link in links)
                if (objects.TryGetValue(link.ownerId, out var owner) && owner.scene == scene)
                {
                    var component = owner.GetComponents<Component>()[link.ownerComponent];
#if UNITY_EDITOR
                    if (!recordPrefab) { Assign(component, link.property, null); continue; }
                    var serialized = new UnityEditor.SerializedObject(component);
                    var property = serialized.FindProperty(link.property);
                    if (property == null) throw new InvalidOperationException("Serialized scene field missing: " + link.property);
                    property.objectReferenceValue = null; serialized.ApplyModifiedPropertiesWithoutUndo();
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
#else
                    Assign(component, link.property, null);
#endif
                }
        }
        public void Activate(bool active)
        {
            var objects = Objects();
            foreach (var part in parts)
                if (objects.TryGetValue(part.rootId, out var root)) root.SetActive(active);
                else throw new InvalidOperationException("Scene content root missing: " + part.path);
        }
        public static void Assign(Component owner, string property, UnityEngine.Object value)
        {
            if (owner is Canvas canvas && property == "m_Camera") { canvas.worldCamera = value as Camera; return; }
            if (owner is Renderer renderer && property == "m_ProbeAnchor") { renderer.probeAnchor = value as Transform; return; }
            if (owner is Joint joint && property == "m_ConnectedBody") { joint.connectedBody = value as Rigidbody; return; }
            var path = property.Replace(".Array.data[", "[");
            var tokens = Regex.Matches(path, @"[^.\[\]]+|\[\d+\]");
            Write(owner, tokens, 0, value);
        }
        static object Write(object current, MatchCollection tokens, int index, object value)
        {
            if (index == tokens.Count) return value;
            if (current == null) throw new InvalidOperationException("Null serialized container at " + tokens[index].Value);
            var token = tokens[index].Value;
            if (token[0] == '[')
            {
                var list = current as IList ?? throw new InvalidOperationException("Expected serialized array/list.");
                int element = int.Parse(token.Substring(1, token.Length - 2));
                list[element] = Write(list[element], tokens, index + 1, value); return current;
            }
            FieldInfo field = null;
            for (var type = current.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(token, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) throw new InvalidOperationException("Unsupported scene reference: " + current.GetType().Name + "." + token);
            field.SetValue(current, Write(field.GetValue(current), tokens, index + 1, value)); return current;
        }
    }
}
