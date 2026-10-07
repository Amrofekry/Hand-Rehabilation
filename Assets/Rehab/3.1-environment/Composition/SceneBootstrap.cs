using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GripRehab.Composition
{
    [DefaultExecutionOrder(-32000)]
    public sealed class SceneBootstrap : MonoBehaviour
    {
        public SceneComposition composition;
        public bool Ready { get; private set; }
        public string Problem { get; private set; }
        IEnumerator Start()
        {
            if (composition == null) { Fail("Scene composition is missing."); yield break; }
            foreach (var part in composition.parts)
            {
                if (SceneManager.GetSceneByPath(part.path).isLoaded) continue;
                var operation = SceneManager.LoadSceneAsync(part.path, LoadSceneMode.Additive);
                if (operation == null) { Fail("Cannot load " + part.path); yield break; }
                yield return operation;
            }
            try
            {
                composition.Bind();
                // Reference binding precedes all gameplay Awake/OnEnable callbacks.
                composition.Activate(true); Ready = true;
            }
            catch (Exception error) { Fail(error.ToString()); }
        }
        void Fail(string text) { Problem = text; Debug.LogError("[SCENE_COMPOSITION] " + text); }
        void OnGUI()
        {
            if (Ready) return;
            GUI.Box(new Rect(20, 20, Screen.width - 40, 90), Problem == null ? "Loading rehabilitation workspace..." : "Workspace could not load.\n" + Problem);
        }
    }
}
