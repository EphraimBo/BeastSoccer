#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeastSoccer.UI;

[CustomEditor(typeof(BeastMenuLiveLayout))]
public class BeastMenuLiveLayoutEditor : Editor
{
    string search = "";

    public override void OnInspectorGUI()
    {
        var c = (BeastMenuLiveLayout)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "FIX30 LIVE MENU LAYOUT\n\n" +
            "Keep MainMenu.unity open and Play Mode OFF. Change Position / Size / Scale below and watch the menu move immediately in Game view. Ctrl+S saves it. No rebake is needed.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        c.livePreview = EditorGUILayout.Toggle("Live Preview", c.livePreview);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(c, "Toggle Beast Menu Live Preview");
            EditorUtility.SetDirty(c);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Current Scene Layout"))
            {
                Undo.RecordObject(c, "Capture Beast Menu Layout");
                c.CaptureFromScene();
                EditorUtility.SetDirty(c);
            }
            if (GUILayout.Button("Pull Manual RectTransform Edits"))
            {
                Undo.RecordObject(c, "Pull Beast Menu Layout");
                c.PullCurrentValues();
                EditorUtility.SetDirty(c);
            }
        }

        EditorGUILayout.Space(5);
        search = EditorGUILayout.TextField("Filter", search);
        EditorGUILayout.LabelField("Tip", "Filter with VOLT, START, INFO, QUICK, RIVAL, etc.", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        if (c.items == null || c.items.Count == 0)
        {
            EditorGUILayout.HelpBox("No controls captured yet. Click Capture Current Scene Layout.", MessageType.Warning);
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
            EditorGUILayout.LabelField(item.label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(item.path, EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            Vector2 pos = EditorGUILayout.Vector2Field("Position", item.anchoredPosition);
            Vector2 size = EditorGUILayout.Vector2Field("Size", item.sizeDelta);
            Vector2 scale2 = EditorGUILayout.Vector2Field("Scale", new Vector2(item.localScale.x, item.localScale.y));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(c, "Edit Beast Menu Layout");
                if (item.target != null) Undo.RecordObject(item.target, "Edit Beast Menu RectTransform");
                item.anchoredPosition = pos;
                item.sizeDelta = size;
                item.localScale = new Vector3(scale2.x, scale2.y, item.localScale.z == 0 ? 1 : item.localScale.z);
                anyChange = true;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                item.target = (RectTransform)EditorGUILayout.ObjectField("Scene Object", item.target, typeof(RectTransform), true);
            }
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

public static class BeastMenuLiveLayoutCommands
{
    [MenuItem("Beast Soccer/Enable Live Menu Layout Controls (FIX30)")]
    public static void Enable()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first.", "OK");
            return;
        }

        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Open MainMenu.unity first. No MainMenuUI was found.", "OK");
            return;
        }

        var root = menu.transform.Find("FIX29_MENU_EDITABLE");
        if (root == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "FIX29_MENU_EDITABLE is not in this scene yet. Run the FIX29 bake once, then run this command.", "OK");
            return;
        }

        var c = menu.GetComponent<BeastMenuLiveLayout>();
        if (c == null) c = Undo.AddComponent<BeastMenuLiveLayout>(menu.gameObject);
        Undo.RecordObject(c, "Enable Beast Menu Live Layout");
        c.CaptureFromScene();
        c.livePreview = true;
        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = menu.gameObject;
        Debug.Log("[Beast Soccer] FIX30 live menu layout enabled. Select MainMenuUI and edit FIX30 LIVE MENU LAYOUT values while Play Mode is OFF. Changes are visible immediately and save with Ctrl+S.");
    }
}
#endif
