#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.UI;

public static class BeastMenuEditableBaker
{
    const string RootName = "FIX29_MENU_EDITABLE";

    [MenuItem("Beast Soccer/Bake / Refresh Editable Menu (FIX29)")]
    public static void Bake()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Stop Play Mode first. This command creates normal scene objects that you can edit and save.", "OK");
            return;
        }

        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Beast Soccer", "Open the MainMenu scene first. No MainMenuUI was found in the open scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Bake Beast Soccer Editable Menu");

        var old = menu.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var controller = menu.GetComponent<BeastMenuOverhaul>();
        if (controller == null) controller = Undo.AddComponent<BeastMenuOverhaul>(menu.gameObject);

        var root = NewGO(RootName, menu.transform, typeof(RectTransform));
        Stretch(root.GetComponent<RectTransform>());

        var baseArt = ImageFull(root.transform, "BASE", "MenuBase");
        var mode = Group(root.transform, "MODE_SCREEN");
        var pick = Group(root.transform, "YOUR_BEAST_SCREEN");
        var rival = Group(root.transform, "RIVAL_SCREEN");

        // MODE
        ImageFull(mode.transform, "QUICK_MATCH_ART", "QuickMatch");
        var quick = InvisibleButton(mode.transform, "QUICK_MATCH", new Rect(.37f,.53f,.23f,.25f));
        UnityEventTools.AddPersistentListener(quick.onClick, controller.ShowPick);

        // YOUR BEAST
        AddCharacterArtAndButtons(pick.transform, controller, false);
        var yourVolt = ImageFull(pick.transform, "YOUR_VOLT", "YourVolt");
        var yourLeo  = ImageFull(pick.transform, "YOUR_LEO", "YourLeo");
        var yourGoro = ImageFull(pick.transform, "YOUR_GORO", "YourGoro");
        yourVolt.SetActive(true); yourLeo.SetActive(false); yourGoro.SetActive(false);

        var infoTitle = CreateText(pick.transform, "INFO_TITLE", new Vector2(.055f,.72f), new Vector2(.22f,.08f), 34, TextAnchor.MiddleLeft);
        var infoBody  = CreateText(pick.transform, "INFO_BODY", new Vector2(.055f,.31f), new Vector2(.22f,.41f), 22, TextAnchor.UpperLeft);

        var continueHit = InvisibleButton(pick.transform, "CONTINUE_PICK", new Rect(.345f,.035f,.31f,.10f));
        UnityEventTools.AddPersistentListener(continueHit.onClick, controller.ShowRival);
        var continueButton = AddSimpleButton(pick.transform, "CONTINUE", new Vector2(0,-455), new Vector2(460,78), 26);
        UnityEventTools.AddPersistentListener(continueButton.onClick, controller.ShowRival);

        // RIVAL
        AddCharacterArtAndButtons(rival.transform, controller, true);
        var yourVoltR = ImageFull(rival.transform, "YOUR_VOLT_R", "YourVolt");
        var yourLeoR  = ImageFull(rival.transform, "YOUR_LEO_R", "YourLeo");
        var yourGoroR = ImageFull(rival.transform, "YOUR_GORO_R", "YourGoro");
        yourVoltR.SetActive(true); yourLeoR.SetActive(false); yourGoroR.SetActive(false);
        var rivalVolt = ImageFull(rival.transform, "RIVAL_VOLT", "RivalVolt");
        var rivalLeo  = ImageFull(rival.transform, "RIVAL_LEO", "RivalLeo");
        var rivalGoro = ImageFull(rival.transform, "RIVAL_GORO", "RivalGoro");
        rivalVolt.SetActive(false); rivalLeo.SetActive(true); rivalGoro.SetActive(false);

        ImageFull(rival.transform, "START_MATCH_ART", "StartMatch");
        var startHit = InvisibleButton(rival.transform, "START_MATCH", new Rect(.36f,.69f,.30f,.12f));
        UnityEventTools.AddPersistentListener(startHit.onClick, controller.StartSelectedMatch);
        var back = AddSimpleButton(rival.transform, "BACK", new Vector2(-690,-430), new Vector2(180,60), 22);
        UnityEventTools.AddPersistentListener(back.onClick, controller.ShowPick);

        controller.root = root;
        controller.modeScreen = mode;
        controller.pickScreen = pick;
        controller.rivalScreen = rival;
        controller.infoTitle = infoTitle;
        controller.infoBody = infoBody;
        controller.selectedPlayer = BeastSoccer.Data.CharacterType.Volt;
        controller.selectedRival = BeastSoccer.Data.CharacterType.Leo;

        mode.SetActive(true); pick.SetActive(false); rival.SetActive(false);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = root;

        Debug.Log("[Beast Soccer] FIX29 editable menu baked into the open MainMenu scene. Move/resize FIX29_MENU_EDITABLE children normally, then Ctrl+S. Do NOT rebake after manual layout edits unless you intentionally want to reset them.");
    }

    static void AddCharacterArtAndButtons(Transform parent, BeastMenuOverhaul c, bool rival)
    {
        ImageFull(parent, "VOLT_CARD", "VoltCard");
        ImageFull(parent, "LEO_CARD", "LeoCard");
        ImageFull(parent, "GORO_CARD", "GoroCard");

        var v = InvisibleButton(parent, "VOLT_PICK", new Rect(.735f,.58f,.25f,.22f));
        var l = InvisibleButton(parent, "LEO_PICK", new Rect(.735f,.36f,.25f,.22f));
        var g = InvisibleButton(parent, "GORO_PICK", new Rect(.735f,.14f,.25f,.22f));
        if (rival)
        {
            UnityEventTools.AddPersistentListener(v.onClick, c.RivalVolt);
            UnityEventTools.AddPersistentListener(l.onClick, c.RivalLeo);
            UnityEventTools.AddPersistentListener(g.onClick, c.RivalGoro);
        }
        else
        {
            UnityEventTools.AddPersistentListener(v.onClick, c.PickVolt);
            UnityEventTools.AddPersistentListener(l.onClick, c.PickLeo);
            UnityEventTools.AddPersistentListener(g.onClick, c.PickGoro);
        }
    }

    static GameObject NewGO(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject Group(Transform p, string n)
    {
        var g = NewGO(n, p, typeof(RectTransform));
        Stretch(g.GetComponent<RectTransform>());
        return g;
    }

    static GameObject ImageFull(Transform p, string n, string resource)
    {
        var g = NewGO(n, p, typeof(RectTransform), typeof(RawImage));
        Stretch(g.GetComponent<RectTransform>());
        var img = g.GetComponent<RawImage>();
        img.texture = Resources.Load<Texture2D>("BeastMenu/" + resource);
        img.raycastTarget = false;
        return g;
    }

    static Button InvisibleButton(Transform p, string n, Rect normalized)
    {
        var g = NewGO(n, p, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = g.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(normalized.xMin, 1f-normalized.yMax);
        rt.anchorMax = new Vector2(normalized.xMax, 1f-normalized.yMin);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var im = g.GetComponent<Image>();
        im.color = new Color(1,1,1,0.001f);
        return g.GetComponent<Button>();
    }

    static Button AddSimpleButton(Transform p, string label, Vector2 pos, Vector2 size, int font)
    {
        var g = NewGO(label, p, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = g.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        g.GetComponent<Image>().color = new Color(.05f,.08f,.11f,.92f);
        var t = CreateText(g.transform, label + "_TEXT", Vector2.zero, Vector2.one, font, TextAnchor.MiddleCenter, true);
        t.text = label;
        return g.GetComponent<Button>();
    }

    static Text CreateText(Transform p, string n, Vector2 anchorMin, Vector2 anchorSize, int font, TextAnchor align, bool local=false)
    {
        var g = NewGO(n, p, typeof(RectTransform), typeof(Text));
        var rt = g.GetComponent<RectTransform>();
        if (local)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMin + anchorSize; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        var t = g.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = font;
        t.alignment = align;
        t.color = new Color(.92f,.82f,.55f,1f);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
#endif
