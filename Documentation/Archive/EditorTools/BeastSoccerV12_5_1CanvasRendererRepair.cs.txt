#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeastSoccer.UI;

[InitializeOnLoad]
public static class BeastSoccerV12_5_1CanvasRendererRepair
{
    static BeastSoccerV12_5_1CanvasRendererRepair()
    {
        EditorApplication.delayCall += RepairOpenScenes;
    }

    [MenuItem("Beast Soccer/Repair V12.5 Progress CanvasRenderers")]
    public static void RepairOpenScenes()
    {
        if (Application.isPlaying) return;

        int repaired = 0;

        var arcs = Object.FindObjectsByType<ArcGraphic>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var arc in arcs)
        {
            if (arc == null) continue;

            if (arc.GetComponent<CanvasRenderer>() == null)
            {
                Undo.AddComponent<CanvasRenderer>(arc.gameObject);
                EditorUtility.SetDirty(arc.gameObject);

                if (arc.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(arc.gameObject.scene);

                repaired++;
            }

            // Progress rings are display-only and should never intercept UI input.
            arc.raycastTarget = false;
            EditorUtility.SetDirty(arc);
        }

        if (repaired > 0)
            Debug.Log("[Beast Soccer] V12.5.1 repaired " + repaired +
                      " ArcGraphic CanvasRenderer component(s). Ctrl+S to save the scene.");
        else
            Debug.Log("[Beast Soccer] V12.5.1 CanvasRenderer check PASS — no missing components.");
    }

    [MenuItem("Beast Soccer/Validate ArcGraphic CanvasRenderers")]
    public static void Validate()
    {
        int missing = 0;

        var arcs = Object.FindObjectsByType<ArcGraphic>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var arc in arcs)
        {
            if (arc != null && arc.GetComponent<CanvasRenderer>() == null)
            {
                Debug.LogError("[Beast Soccer] Missing CanvasRenderer: " +
                               GetPath(arc.transform), arc);
                missing++;
            }
        }

        if (missing == 0)
            EditorUtility.DisplayDialog(
                "Beast Soccer",
                "ArcGraphic CanvasRenderer validation PASS.",
                "OK");
        else
            EditorUtility.DisplayDialog(
                "Beast Soccer",
                "FAILED: " + missing +
                " ArcGraphic object(s) are missing CanvasRenderer.",
                "OK");
    }

    static string GetPath(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
#endif
