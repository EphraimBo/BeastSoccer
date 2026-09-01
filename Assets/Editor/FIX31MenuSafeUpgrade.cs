#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.UI;

public static class FIX31MenuSafeUpgrade
{
    [MenuItem("Beast Soccer/Apply FIX31 Menu Flow Upgrade (Preserve Layout)")]
    public static void Apply()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first.", "OK"); return; }
        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        if (!menu) { EditorUtility.DisplayDialog("Beast Soccer", "Open MainMenu.unity first.", "OK"); return; }
        var controller = menu.GetComponent<BeastMenuOverhaul>();
        var root = menu.transform.Find("FIX29_MENU_EDITABLE");
        if (!controller || !root) { EditorUtility.DisplayDialog("Beast Soccer", "Editable menu hierarchy/controller is missing.", "OK"); return; }

        var pick = root.Find("YOUR_BEAST_SCREEN");
        if (pick != null && pick.Find("BACK_PICK") == null)
        {
            var go = new GameObject("BACK_PICK", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, "Add FIX31 Player Pick Back Button");
            go.transform.SetParent(pick, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.sizeDelta = new Vector2(180,60);
            rt.anchoredPosition = new Vector2(-690,-430);
            var img = go.GetComponent<Image>();
            img.color = new Color(.05f,.08f,.11f,.92f);
            var b = go.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(b.onClick, controller.BackToMode);

            var tg = new GameObject("BACK_PICK_TEXT", typeof(RectTransform), typeof(Text));
            Undo.RegisterCreatedObjectUndo(tg, "Add FIX31 Back Text");
            tg.transform.SetParent(go.transform, false);
            var tr = tg.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = tg.GetComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text = "BACK"; t.fontSize = 22; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        }

        // Existing rival BACK button should return to the player page.
        var rival = root.Find("RIVAL_SCREEN");
        if (rival != null)
        {
            var back = rival.Find("BACK");
            if (back != null)
            {
                var b = back.GetComponent<Button>();
                if (b != null)
                {
                    Undo.RecordObject(b, "Update Rival Back Button");
                    b.onClick.RemoveAllListeners();
                    UnityEventTools.AddPersistentListener(b.onClick, controller.BackToPlayerPick);
                }
            }
        }

        var live = menu.GetComponent<BeastMenuLiveLayout>();
        if (live != null)
        {
            Undo.RecordObject(live, "Capture FIX31 Menu Layout");
            live.CaptureFromScene();
            EditorUtility.SetDirty(live);
        }
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Debug.Log("[Beast Soccer] FIX31 menu flow upgrade applied without rebuilding or moving existing menu items. A missing BACK_PICK was added; FIX30 layout controls were recaptured if present.");
    }
}
#endif
