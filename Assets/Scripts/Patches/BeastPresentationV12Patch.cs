using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BeastSoccer.Core;
using BeastSoccer.UI;
using BeastSoccer.Player;
using BeastSoccer.Ball;

namespace BeastSoccer.Patches
{
    /// <summary>
    /// V12 presentation-only patch.
    /// - Full-screen menu coverage
    /// - Higher/centered Quick Match
    /// - Restores the older designed character-card art
    /// - Keeps rival selection as an immediate READY-state transition
    /// - Adds Aguila / Alianza crests to the duel HUD
    /// - Corrects ALIANZO -> ALIANZA
    /// - Zooms the duel camera in without changing gameplay bounds/physics
    /// - Re-applies the top-down interpolation stability guard from the prior fix
    ///
    /// No speed, shooting, passing, tackling, AI, keeper, ult, possession,
    /// rules, pitch dimensions, or collision values are modified.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class BeastPresentationV12Patch : MonoBehaviour
    {
        private const float DesignW = 1672f;
        private const float DesignH = 941f;

        // Noticeable but still safe fixed-duel zoom. Presentation only.
        private const float DuelCameraDistanceMultiplier = 0.78f;

        private BeastMenuOverhaul menu;
        private bool menuPatched;
        private bool hudPatched;
        private bool interpolationPatched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<BeastPresentationV12Patch>() != null) return;
            var go = new GameObject("BEAST_PRESENTATION_V12_PATCH");
            DontDestroyOnLoad(go);
            go.AddComponent<BeastPresentationV12Patch>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(PatchAfterSceneReady());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            menu = null;
            menuPatched = false;
            hudPatched = false;
            interpolationPatched = false;
            StartCoroutine(PatchAfterSceneReady());
        }

        private IEnumerator PatchAfterSceneReady()
        {
            // Let MainMenuUI / BeastMenuOverhaul / DuelHud finish their own Start/Build pass first.
            yield return null;
            yield return null;

            ApplyMenuPatch();
            ApplyHudPatch();
            ApplyTopDownStabilityGuard();
        }

        private void Update()
        {
            // Handles editor/device timing where UI is created one or two frames later.
            if (!menuPatched) ApplyMenuPatch();
            if (!hudPatched) ApplyHudPatch();
            if (!interpolationPatched) ApplyTopDownStabilityGuard();

            // Rival click should immediately become the READY state. The underlying
            // menu already advances the progress indicator via hasRivalSelection;
            // this ensures no stray rival "Continue" control can remain visible.
            if (menu != null && menu.hasRivalSelection && menu.rivalScreen != null)
            {
                HideChild(menu.rivalScreen, "CONTINUE");
                HideChild(menu.rivalScreen, "CONTINUE_RIVAL");
                HideChild(menu.rivalScreen, "CONTINUE_PICK");
            }
        }

        private void LateUpdate()
        {
            ApplyDuelCameraZoom();
        }

        private void ApplyMenuPatch()
        {
            menu = FindFirstObjectByType<BeastMenuOverhaul>();
            if (menu == null || menu.root == null || menu.modeScreen == null ||
                menu.pickScreen == null || menu.rivalScreen == null)
                return;

            Canvas canvas = menu.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            InstallFullScreenBackdrop(canvas);
            MoveQuickMatchHigher();
            RestoreOldCharacterSelectionArt();

            // Keep the current selection logic/rules. Goro stays keeper-only.
            HideChild(menu.pickScreen, "GORO_PICK");
            HideChild(menu.pickScreen, "GORO_CARD");
            HideChild(menu.rivalScreen, "GORO_PICK");
            HideChild(menu.rivalScreen, "GORO_CARD");

            menuPatched = true;
        }

        private static void InstallFullScreenBackdrop(Canvas canvas)
        {
            Transform existing = canvas.transform.Find("V12_MENU_FULLSCREEN_BACKDROP");
            if (existing != null) return;

            var go = new GameObject("V12_MENU_FULLSCREEN_BACKDROP", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.018f, 0.040f, 0.058f, 1f);
            image.raycastTarget = false;
        }

        private void MoveQuickMatchHigher()
        {
            // Current V11 layout places this around y=-295. Bring it up while keeping
            // it centered and preserving the authored art/click area.
            PlaceChild(menu.modeScreen, "QUICK_MATCH_ART", new Vector2(0f, -165f), new Vector2(380f, 262f));
            PlaceChild(menu.modeScreen, "QUICK_MATCH",     new Vector2(0f, -165f), new Vector2(380f, 262f));
        }

        private void RestoreOldCharacterSelectionArt()
        {
            // DuelMenuLayout currently replaces these authored card textures with
            // runtime side sprites. Restore the designed card/portrait artwork that
            // is still in Resources/BeastMenu.

            // YOUR BEAST chooser cards.
            RestoreRaw(menu.pickScreen, "VOLT_CARD", "BeastMenu/VoltCard",
                new Rect(1233, 559, 417, 199), new Vector2(610, 225), new Vector2(320, 156));
            RestoreRaw(menu.pickScreen, "LEO_CARD", "BeastMenu/LeoCard",
                new Rect(1230, 369, 420, 193), new Vector2(610, 5), new Vector2(320, 156));

            // Current player's center slot.
            RestoreRaw(menu.pickScreen, "YOUR_VOLT", "BeastMenu/YourVolt",
                new Rect(624, 536, 159, 183), new Vector2(-134, -92), new Vector2(160, 184));
            RestoreRaw(menu.pickScreen, "YOUR_LEO", "BeastMenu/YourLeo",
                new Rect(624, 536, 159, 183), new Vector2(-134, -92), new Vector2(160, 184));

            // Rival chooser cards.
            RestoreRaw(menu.rivalScreen, "VOLT_CARD", "BeastMenu/VoltCard",
                new Rect(1233, 559, 417, 199), new Vector2(610, 225), new Vector2(320, 156));
            RestoreRaw(menu.rivalScreen, "LEO_CARD", "BeastMenu/LeoCard",
                new Rect(1230, 369, 420, 193), new Vector2(610, 5), new Vector2(320, 156));

            // READY / versus center slots.
            RestoreRaw(menu.rivalScreen, "YOUR_VOLT_R", "BeastMenu/YourVolt",
                new Rect(624, 536, 159, 183), new Vector2(-134, -92), new Vector2(160, 184));
            RestoreRaw(menu.rivalScreen, "YOUR_LEO_R", "BeastMenu/YourLeo",
                new Rect(624, 536, 159, 183), new Vector2(-134, -92), new Vector2(160, 184));
            RestoreRaw(menu.rivalScreen, "RIVAL_VOLT", "BeastMenu/RivalVolt",
                new Rect(624, 536, 159, 183), new Vector2(127, -92), new Vector2(160, 184));
            RestoreRaw(menu.rivalScreen, "RIVAL_LEO", "BeastMenu/RivalLeo",
                new Rect(624, 536, 159, 183), new Vector2(127, -92), new Vector2(160, 184));
        }

        private static void RestoreRaw(GameObject page, string childName, string resource,
            Rect pixelCrop, Vector2 position, Vector2 size)
        {
            if (page == null) return;
            Transform child = page.transform.Find(childName);
            if (child == null) return;

            RawImage image = child.GetComponent<RawImage>();
            if (image == null) return;

            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return;

            image.texture = texture;
            image.material = null;
            image.color = Color.white;
            image.uvRect = new Rect(
                pixelCrop.x / DesignW,
                (DesignH - pixelCrop.y - pixelCrop.height) / DesignH,
                pixelCrop.width / DesignW,
                pixelCrop.height / DesignH);

            var fitter = child.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false;

            RectTransform rt = child as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = Vector2.one * 0.5f;
                rt.pivot = Vector2.one * 0.5f;
                rt.anchoredPosition = position;
                rt.sizeDelta = size;
                rt.localScale = Vector3.one;
            }
        }

        private static void PlaceChild(GameObject page, string name, Vector2 position, Vector2 size)
        {
            if (page == null) return;
            RectTransform rt = page.transform.Find(name) as RectTransform;
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = Vector2.one * 0.5f;
            rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static void HideChild(GameObject page, string name)
        {
            if (page == null) return;
            Transform t = page.transform.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        private void ApplyHudPatch()
        {
            if (!DuelRules.Enabled) return;

            MatchUI ui = FindFirstObjectByType<MatchUI>();
            if (ui == null) return;

            Canvas canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Text home = FindText(canvas.transform, "HomeClub");
            Text away = FindText(canvas.transform, "AwayClub");
            if (home == null || away == null) return; // DuelHud has not built yet.

            // Keep the existing home label exactly as-is; only fix the requested typo.
            away.text = "ALIANZA";

            RectTransform parent = home.transform.parent as RectTransform;
            if (parent == null) return;

            // Slightly spread the names so each crest can sit cleanly beside it.
            DuelHud.Place(home.rectTransform, new Vector2(.5f, 1f), new Vector2(-205f, -40f), new Vector2(170f, 50f));
            DuelHud.Place(away.rectTransform, new Vector2(.5f, 1f), new Vector2(205f, -40f), new Vector2(170f, 50f));

            AddLogo(parent, "HomeClubLogo", "TeamBranding/Aguila",
                new Vector2(-315f, -40f), new Vector2(42f, 52f));
            AddLogo(parent, "AwayClubLogo", "TeamBranding/Alianza",
                new Vector2(315f, -40f), new Vector2(52f, 52f));

            hudPatched = true;
        }

        private static Text FindText(Transform root, string objectName)
        {
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t != null && t.name == objectName) return t;
            return null;
        }

        private static void AddLogo(Transform parent, string name, string resource,
            Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return;

            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return;

            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = Color.white;
            raw.raycastTarget = false;

            DuelHud.Place(go.GetComponent<RectTransform>(),
                new Vector2(.5f, 1f), position, size);
        }

        private void ApplyDuelCameraZoom()
        {
            if (!DuelRules.Enabled || GameManager.Instance == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            // FollowCamera computes the stable full-duel framing first. Pull that result
            // closer to the same world center afterward; this changes only presentation.
            Vector3 framingCenter = new Vector3(0f, 1.6f, 0f);
            Vector3 offset = cam.transform.position - framingCenter;
            cam.transform.position = framingCenter + offset * DuelCameraDistanceMultiplier;
        }

        private void ApplyTopDownStabilityGuard()
        {
            if (!DuelRules.Enabled) return;

            // Prior top-down/mobile fix: all gameplay rigidbodies interpolate visually so
            // the fixed camera does not expose FixedUpdate stepping/judder. No forces,
            // speeds, positions, collider values or gameplay timing are changed here.
            foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            if (BallControl.Instance != null)
            {
                Rigidbody2D ballRb = BallControl.Instance.GetComponent<Rigidbody2D>();
                if (ballRb != null) ballRb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            interpolationPatched = true;
        }
    }
}
