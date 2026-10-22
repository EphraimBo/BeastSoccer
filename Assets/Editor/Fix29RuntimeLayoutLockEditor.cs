#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeastSoccer.UI;

public static class Fix29RuntimeLayoutLockEditor
{
    [MenuItem("Beast Soccer/Arm FIX29 Runtime Layout Lock")]
    public static void Arm()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first.", "OK");
            return;
        }

        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Open MainMenu.unity first.", "OK");
            return;
        }

        Transform root = menu.transform.Find("FIX29_MENU_EDITABLE");
        if (root == null)
        {
            EditorUtility.DisplayDialog(
                "Beast Soccer",
                "FIX29_MENU_EDITABLE was not found.",
                "OK");
            return;
        }

        // Remove any serialized copy of the old runtime layout component.
        var old = menu.GetComponent<DuelMenuLayout>();
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        var locker = menu.GetComponent<Fix29RuntimeLayoutLock>();
        if (locker == null)
            locker = Undo.AddComponent<Fix29RuntimeLayoutLock>(menu.gameObject);

        Undo.RecordObject(locker, "Arm FIX29 Runtime Layout Lock");
        locker.root = root as RectTransform;
        EditorUtility.SetDirty(locker);

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[Beast Soccer] FIX29 runtime layout lock armed. " +
            "Your currently saved FIX29 RectTransforms will be the Play Mode layout. Ctrl+S now.");
    }

    [MenuItem("Beast Soccer/Validate FIX29 Runtime Layout Lock")]
    public static void Validate()
    {
        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "FAIL: MainMenuUI not found.", "OK");
            return;
        }

        var root = menu.transform.Find("FIX29_MENU_EDITABLE");
        var locker = menu.GetComponent<Fix29RuntimeLayoutLock>();
        var old = menu.GetComponent<DuelMenuLayout>();

        if (root == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "FAIL: FIX29_MENU_EDITABLE missing.", "OK");
            return;
        }

        if (locker == null || locker.root != root)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "FAIL: FIX29 runtime lock is not armed.", "OK");
            return;
        }

        if (old != null)
        {
            EditorUtility.DisplayDialog(
                "Beast Soccer",
                "FAIL: a serialized DuelMenuLayout component is still attached.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "Beast Soccer",
            "PASS: FIX29 saved layout is locked as the Play Mode geometry source.",
            "OK");
    }
}
#endif
