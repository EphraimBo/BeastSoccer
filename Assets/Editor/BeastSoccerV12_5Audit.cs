#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BeastSoccer.UI;

public static class BeastSoccerV12_5Audit
{
    private static readonly Color Gold = new Color(.87f,.70f,.34f,1f);
    private static readonly Color Inactive = new Color(.28f,.31f,.34f,1f);

    [MenuItem("Beast Soccer/Finalize Existing FIX29 V12.5 (Preserve Layout)")]
    public static void FinalizeExisting()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first.", "OK");
            return;
        }

        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Open MainMenu.unity first.", "OK");
            return;
        }

        var overhaul = menu.GetComponent<BeastMenuOverhaul>();
        Transform root = menu.transform.Find("FIX29_MENU_EDITABLE");
        if (overhaul == null || root == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer",
                "FIX29_MENU_EDITABLE / BeastMenuOverhaul is missing. Run the FIX29 bake once first.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Finalize FIX29 V12.5");

        overhaul.root = root.gameObject;
        overhaul.modeScreen = FindGO(root, "MODE_SCREEN");
        overhaul.pickScreen = FindGO(root, "YOUR_BEAST_SCREEN");
        overhaul.rivalScreen = FindGO(root, "RIVAL_SCREEN");

        if (overhaul.pickScreen != null)
        {
            Transform ti = overhaul.pickScreen.transform.Find("INFO_TITLE");
            Transform bi = overhaul.pickScreen.transform.Find("INFO_BODY");
            if (ti != null) overhaul.infoTitle = ti.GetComponent<Text>();
            if (bi != null) overhaul.infoBody = bi.GetComponent<Text>();
        }

        // One visual source of truth: FIX29 only.
        if (menu.modePanel != null) menu.modePanel.SetActive(false);
        if (menu.characterPanel != null) menu.characterPanel.SetActive(false);
        if (menu.opponentPanel != null) menu.opponentPanel.SetActive(false);
        root.gameObject.SetActive(true);

        EnsureBackdrop(menu.transform);
        EnsureProgress(root);
        EnsureVersion(root);
        RestoreOldMenuTextures(root);

        SetActive(overhaul.pickScreen, "CONTINUE_PICK", false);
        SetActive(overhaul.pickScreen, "CONTINUE", false);
        SetActive(overhaul.rivalScreen, "START_MATCH", false);
        SetActive(overhaul.rivalScreen, "START_MATCH_ART", false);
        SetButtonInteractable(overhaul.pickScreen, "GORO_PICK", false);
        SetButtonInteractable(overhaul.rivalScreen, "GORO_PICK", false);

        if (overhaul.infoBody != null && !string.IsNullOrEmpty(overhaul.infoBody.text))
            overhaul.infoBody.text = overhaul.infoBody.text.Replace("ALIANZO", "ALIANZA");

        // Remove known runtime-layout leftovers if a prior patch accidentally saved them.
        Transform oldProgress = root.Find("FIX29_RUNTIME_PROGRESS");
        if (oldProgress != null) Undo.DestroyObjectImmediate(oldProgress.gameObject);
        var fitter = root.GetComponent<AspectRatioFitter>();
        if (fitter != null) Undo.DestroyObjectImmediate(fitter);

        // FIX30 becomes a mirror of the ACTUAL scene layout instead of an older snapshot.
        var live = menu.GetComponent<BeastMenuLiveLayout>();
        if (live != null)
        {
            Undo.RecordObject(live, "Sync FIX30 with current FIX29 layout");
            live.CaptureFromScene();
            live.livePreview = true;
            EditorUtility.SetDirty(live);
        }

        BeastSoccerEventSystemEditorSafety.PatchOpenScenes();

        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(overhaul);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        AssetDatabase.SaveAssets();
        ValidateInternal(true);
    }

    [MenuItem("Beast Soccer/Validate V12.5 Setup")]
    public static void ValidateSetup() => ValidateInternal(true);

    private static void ValidateInternal(bool showDialog)
    {
        var errors = new List<string>();
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            errors.Add("MainMenuUI not found. Open MainMenu.unity.");
        }
        else
        {
            Transform root = menu.transform.Find("FIX29_MENU_EDITABLE");
            var overhaul = menu.GetComponent<BeastMenuOverhaul>();
            if (root == null) errors.Add("FIX29_MENU_EDITABLE missing.");
            if (overhaul == null) errors.Add("BeastMenuOverhaul missing.");

            if (root != null)
            {
                Require(root, "MODE_SCREEN", errors);
                Require(root, "YOUR_BEAST_SCREEN", errors);
                Require(root, "RIVAL_SCREEN", errors);
                Require(root, "FIX29_PROGRESS", errors);

                Transform pick = root.Find("YOUR_BEAST_SCREEN");
                Transform rival = root.Find("RIVAL_SCREEN");
                RequireButtonMethod(pick, "VOLT_PICK", "PickVolt", errors);
                RequireButtonMethod(pick, "LEO_PICK", "PickLeo", errors);
                RequireButtonMethod(rival, "VOLT_PICK", "RivalVolt", errors);
                RequireButtonMethod(rival, "LEO_PICK", "RivalLeo", errors);
                RequireButtonMethod(rival, "START_MATCH", "StartSelectedMatch", errors);

                if (pick != null)
                {
                    Transform c = pick.Find("CONTINUE");
                    Transform cp = pick.Find("CONTINUE_PICK");
                    if (c != null && c.gameObject.activeSelf) errors.Add("CONTINUE is still active.");
                    if (cp != null && cp.gameObject.activeSelf) errors.Add("CONTINUE_PICK is still active.");
                }

                if (root.GetComponent<AspectRatioFitter>() != null)
                    errors.Add("FIX29 root still has an AspectRatioFitter that can change saved layout.");
            }

            if (menu.modePanel != null && menu.modePanel.activeSelf) errors.Add("Legacy ModePanel is active.");
            if (menu.characterPanel != null && menu.characterPanel.activeSelf) errors.Add("Legacy CharacterPanel is active.");
            if (menu.opponentPanel != null && menu.opponentPanel.activeSelf) errors.Add("Legacy OpponentPanel is active.");
        }

        string[] resources =
        {
            "BeastMenu/MenuBase", "BeastMenu/QuickMatch", "BeastMenu/VoltCard", "BeastMenu/LeoCard",
            "BeastMenu/GoroCard", "BeastMenu/YourVolt", "BeastMenu/YourLeo",
            "BeastMenu/RivalVolt", "BeastMenu/RivalLeo",
            "TeamLogos/AguilaLogo", "TeamLogos/AlianzaLogo"
        };
        foreach (string r in resources)
            if (Resources.Load<Texture2D>(r) == null) errors.Add("Missing resource: " + r);

        Type inputUiType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputUiType == null) errors.Add("InputSystemUIInputModule cannot be resolved.");

        foreach (var es in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (es.GetComponent<StandaloneInputModule>() != null)
                errors.Add("Legacy StandaloneInputModule remains on " + es.name + ".");
            if (inputUiType != null && es.GetComponent(inputUiType) == null)
                errors.Add("InputSystemUIInputModule missing on " + es.name + ".");
        }

        bool main = false, match = false;
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            if (scene.path.EndsWith("/MainMenu.unity", StringComparison.OrdinalIgnoreCase)) main = true;
            if (scene.path.EndsWith("/Match.unity", StringComparison.OrdinalIgnoreCase)) match = true;
        }
        if (!main) errors.Add("MainMenu.unity is not enabled in Build Settings.");
        if (!match) errors.Add("Match.unity is not enabled in Build Settings.");

        string message = errors.Count == 0
            ? "V12.5 VALIDATION PASS\n\nNo blocking setup conflicts found."
            : "V12.5 VALIDATION FAILED\n\n• " + string.Join("\n• ", errors);

        if (errors.Count == 0) Debug.Log("[Beast Soccer] " + message);
        else Debug.LogError("[Beast Soccer] " + message);

        if (showDialog) EditorUtility.DisplayDialog("Beast Soccer V12.5 Audit", message, "OK");
    }

    private static void EnsureBackdrop(Transform canvas)
    {
        Transform t = canvas.Find("FIX29_FULLSCREEN_BACKDROP");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("FIX29_FULLSCREEN_BACKDROP", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create FIX29 backdrop");
            go.transform.SetParent(canvas, false);
        }
        else go = t.gameObject;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        Image image = go.GetComponent<Image>();
        image.color = new Color(.012f,.028f,.043f,1f);
        image.raycastTarget = false;
        go.transform.SetAsFirstSibling();
    }

    private static void EnsureProgress(Transform root)
    {
        if (root.Find("FIX29_PROGRESS") != null) return;
        GameObject progress = new GameObject("FIX29_PROGRESS", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(progress, "Create FIX29 progress");
        progress.transform.SetParent(root, false);

        RectTransform rt = progress.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f,1f);
        rt.pivot = new Vector2(.5f,1f);
        rt.anchoredPosition = new Vector2(15,-6);
        rt.sizeDelta = new Vector2(865,115);
        Image bg = progress.GetComponent<Image>();
        bg.color = new Color(.025f,.055f,.08f,1f);
        bg.raycastTarget = false;

        string[] names = { "MODE", "YOUR BEAST", "RIVAL", "READY" };
        for (int i=0;i<4;i++)
        {
            GameObject step = new GameObject("Step"+(i+1), typeof(RectTransform), typeof(ArcGraphic));
            Undo.RegisterCreatedObjectUndo(step, "Create FIX29 progress step");
            step.transform.SetParent(progress.transform, false);
            RectTransform srt = step.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(.5f,.5f);
            srt.sizeDelta = new Vector2(52,52);
            srt.anchoredPosition = new Vector2(-330+i*220,20);
            ArcGraphic ring = step.GetComponent<ArcGraphic>();
            ring.thickness = 3;
            ring.sweep = 360;
            ring.color = i == 0 ? Gold : Inactive;
            CreateLabel(step.transform, "Number", (i+1).ToString(), Vector2.zero, new Vector2(50,50), 28, ring.color);
            CreateLabel(step.transform, "Label", names[i], new Vector2(0,-46), new Vector2(190,36), 20, ring.color);
        }
    }

    private static void EnsureVersion(Transform root)
    {
        if (root.Find("FIX29_VERSION") != null) return;
        CreateLabel(root, "FIX29_VERSION", "DEMO V12.5", new Vector2(0,-440), new Vector2(220,28), 18, new Color(.7f,.75f,.8f,.8f));
    }

    private static void CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        Undo.RegisterCreatedObjectUndo(go, "Create FIX29 label");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
    }

    private static void RestoreOldMenuTextures(Transform root)
    {
        SetTexture(root, "BASE", "BeastMenu/MenuBase");
        SetTexture(root, "MODE_SCREEN/QUICK_MATCH_ART", "BeastMenu/QuickMatch");
        foreach (string page in new[] { "YOUR_BEAST_SCREEN", "RIVAL_SCREEN" })
        {
            SetTexture(root, page+"/VOLT_CARD", "BeastMenu/VoltCard");
            SetTexture(root, page+"/LEO_CARD", "BeastMenu/LeoCard");
            SetTexture(root, page+"/GORO_CARD", "BeastMenu/GoroCard");
        }
        SetTexture(root, "YOUR_BEAST_SCREEN/YOUR_VOLT", "BeastMenu/YourVolt");
        SetTexture(root, "YOUR_BEAST_SCREEN/YOUR_LEO", "BeastMenu/YourLeo");
        SetTexture(root, "RIVAL_SCREEN/YOUR_VOLT_R", "BeastMenu/YourVolt");
        SetTexture(root, "RIVAL_SCREEN/YOUR_LEO_R", "BeastMenu/YourLeo");
        SetTexture(root, "RIVAL_SCREEN/RIVAL_VOLT", "BeastMenu/RivalVolt");
        SetTexture(root, "RIVAL_SCREEN/RIVAL_LEO", "BeastMenu/RivalLeo");
    }

    private static void SetTexture(Transform root, string path, string resource)
    {
        Transform t = root.Find(path);
        if (t == null) return;
        RawImage image = t.GetComponent<RawImage>();
        Texture2D texture = Resources.Load<Texture2D>(resource);
        if (image == null || texture == null) return;
        Undo.RecordObject(image, "Restore BeastMenu texture");
        image.texture = texture;
        image.uvRect = new Rect(0,0,1,1);
        image.material = null;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static void Require(Transform root, string path, List<string> errors)
    {
        if (root.Find(path) == null) errors.Add("Missing FIX29 object: " + path);
    }

    private static void RequireButtonMethod(Transform page, string name, string method, List<string> errors)
    {
        if (page == null) return;
        Transform t = page.Find(name);
        if (t == null) { errors.Add("Missing button: " + page.name + "/" + name); return; }
        Button b = t.GetComponent<Button>();
        if (b == null) { errors.Add("Button component missing: " + page.name + "/" + name); return; }
        bool found = false;
        for (int i=0;i<b.onClick.GetPersistentEventCount();i++)
            if (b.onClick.GetPersistentMethodName(i) == method) found = true;
        if (!found) errors.Add(page.name + "/" + name + " is not wired to " + method + ".");
    }

    private static GameObject FindGO(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.gameObject : null;
    }

    private static void SetActive(GameObject parent, string name, bool active)
    {
        if (parent == null) return;
        Transform t = parent.transform.Find(name);
        if (t != null) t.gameObject.SetActive(active);
    }

    private static void SetButtonInteractable(GameObject parent, string name, bool value)
    {
        if (parent == null) return;
        Transform t = parent.transform.Find(name);
        if (t == null) return;
        Button b = t.GetComponent<Button>();
        if (b != null) b.interactable = value;
    }
}
#endif
