#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeastSoccer.UI;

[CustomEditor(typeof(BeastMatchLiveLayout))]
public class BeastMatchLiveLayoutEditor : Editor
{
    string search = "";
    bool showAdvanced = false;

    public override void OnInspectorGUI()
    {
        var c = (BeastMatchLiveLayout)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "FIX36 LIVE MATCH UI\n\n" +
            "Open Match.unity and keep Play Mode OFF. Change Position / Size / Scale below and watch the HUD move immediately in Game view. Ctrl+S saves it. This does NOT rebuild the Match scene.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        bool live = EditorGUILayout.Toggle("Live Preview", c.livePreview);
        RectTransform canvas = (RectTransform)EditorGUILayout.ObjectField("Canvas Root", c.canvasRoot, typeof(RectTransform), true);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(c, "Edit Match Live Layout Settings");
            c.livePreview = live;
            c.canvasRoot = canvas;
            EditorUtility.SetDirty(c);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Current Match UI"))
            {
                Undo.RecordObject(c, "Capture Match UI Layout");
                c.CaptureFromScene();
                EditorUtility.SetDirty(c);
            }
            if (GUILayout.Button("Pull Manual RectTransform Edits"))
            {
                Undo.RecordObject(c, "Pull Match UI Layout");
                c.PullCurrentValues();
                EditorUtility.SetDirty(c);
            }
        }

        EditorGUILayout.Space(5);
        search = EditorGUILayout.TextField("Filter", search);
        EditorGUILayout.LabelField("Useful filters", "JOYSTICK, SHOOT, PASS, LOB, ULT, SPECIAL, SWITCH, SCORE, TIMER, STAMINA", EditorStyles.miniLabel);
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced anchors/pivot", true);
        EditorGUILayout.Space(4);

        if (c.items == null || c.items.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing captured yet. Click Capture Current Match UI.", MessageType.Warning);
            return;
        }

        bool anyChange = false;
        for (int i = 0; i < c.items.Count; i++)
        {
            var item = c.items[i];
            if (!string.IsNullOrWhiteSpace(search))
            {
                string hay = ((item.label ?? "") + " " + (item.path ?? "")).ToLowerInvariant();
                if (!hay.Contains(search.ToLowerInvariant())) continue;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(item.label, EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                bool editable = EditorGUILayout.ToggleLeft("Edit", item.editable, GUILayout.Width(48));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(c, "Toggle Match UI Item");
                    item.editable = editable;
                    anyChange = true;
                }
            }
            EditorGUILayout.LabelField(item.path, EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(!item.editable))
            {
                EditorGUI.BeginChangeCheck();
                Vector2 pos = EditorGUILayout.Vector2Field("Position", item.anchoredPosition);
                Vector2 size = EditorGUILayout.Vector2Field("Size", item.sizeDelta);
                Vector2 scale2 = EditorGUILayout.Vector2Field("Scale", new Vector2(item.localScale.x, item.localScale.y));
                Vector2 anchorMin = item.anchorMin;
                Vector2 anchorMax = item.anchorMax;
                Vector2 pivot = item.pivot;
                if (showAdvanced)
                {
                    anchorMin = EditorGUILayout.Vector2Field("Anchor Min", item.anchorMin);
                    anchorMax = EditorGUILayout.Vector2Field("Anchor Max", item.anchorMax);
                    pivot = EditorGUILayout.Vector2Field("Pivot", item.pivot);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(c, "Edit Match UI Layout");
                    if (item.target != null) Undo.RecordObject(item.target, "Edit Match UI RectTransform");
                    item.anchoredPosition = pos;
                    item.sizeDelta = size;
                    item.localScale = new UnityEngine.Vector3(scale2.x, scale2.y, item.localScale.z == 0 ? 1 : item.localScale.z);
                    item.anchorMin = anchorMin;
                    item.anchorMax = anchorMax;
                    item.pivot = pivot;
                    anyChange = true;
                }
            }

            item.target = (RectTransform)EditorGUILayout.ObjectField("Scene Object", item.target, typeof(RectTransform), true);
            EditorGUILayout.EndVertical();
        }

        if (anyChange)
        {
            c.ApplyLayout();
            EditorUtility.SetDirty(c);
            if (!Application.isPlaying && c.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}

public static class BeastMatchLiveLayoutCommands
{
    [MenuItem("Beast Soccer/Enable Live Match UI Layout Controls (FIX36)")]
    public static void Enable()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first.", "OK");
            return;
        }

        var matchUI = UnityEngine.Object.FindFirstObjectByType<MatchUI>();
        if (matchUI == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Open Match.unity first. No MatchUI was found.", "OK");
            return;
        }

        var c = matchUI.GetComponent<BeastMatchLiveLayout>();
        if (c == null) c = Undo.AddComponent<BeastMatchLiveLayout>(matchUI.gameObject);
        Undo.RecordObject(c, "Enable Live Match UI Layout");
        c.CaptureFromScene();
        c.livePreview = true;
        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(matchUI.gameObject.scene);
        Selection.activeGameObject = matchUI.gameObject;
        Debug.Log("[Beast Soccer] FIX36 live Match UI layout enabled. Select the MatchUI object and edit the FIX36 controls while Play Mode is OFF. Ctrl+S saves changes.");
    }

    [MenuItem("Beast Soccer/Capture Current Match UI Into FIX36")]
    public static void Capture()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first.", "OK");
            return;
        }
        var c = UnityEngine.Object.FindFirstObjectByType<BeastMatchLiveLayout>();
        if (c == null)
        {
            Enable();
            return;
        }
        Undo.RecordObject(c, "Capture Match UI Layout");
        c.CaptureFromScene();
        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        Selection.activeGameObject = c.gameObject;
    }
}
#endif
