#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BeastSoccerEventSystemEditorSafety
{
    private const string NewModuleTypeName =
        "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

    private static bool queued;

    static BeastSoccerEventSystemEditorSafety()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.hierarchyChanged += QueuePatch;
        EditorApplication.delayCall += PatchOpenScenes;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => QueuePatch();

    private static void QueuePatch()
    {
        if (queued || Application.isPlaying) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            PatchOpenScenes();
        };
    }

    [MenuItem("Beast Soccer/Fix EventSystems For New Input")]
    public static void PatchOpenScenes()
    {
        if (Application.isPlaying) return;
        Type newType = Type.GetType(NewModuleTypeName);
        if (newType == null) return;

        foreach (var es in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (es == null) continue;
            GameObject go = es.gameObject;
            bool changed = false;

            var legacy = go.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                UnityEngine.Object.DestroyImmediate(legacy, true);
                changed = true;
            }

            if (go.GetComponent(newType) == null)
            {
                go.AddComponent(newType);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(go);
                if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
#endif
