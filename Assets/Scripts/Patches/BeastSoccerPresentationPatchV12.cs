using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BeastSoccer.Ball;
using BeastSoccer.Core;
using BeastSoccer.Player;
using BeastSoccer.UI;

namespace BeastSoccer.Patches
{
    /// <summary>
    /// V12.5 match-presentation patch only.
    /// It intentionally never touches MainMenu/FIX29 layout or menu artwork.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class BeastSoccerPresentationPatchV12 : MonoBehaviour
    {
        private const float DuelCameraFov = 39.5f;
        private static BeastSoccerPresentationPatchV12 instance;
        private int lastStabilityFrame = -9999;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("BeastSoccer_PresentationPatch_V12_5");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BeastSoccerPresentationPatchV12>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(ApplyWhenReady());
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            for (int frame = 0; frame < 90; frame++)
            {
                ApplyHudPatch();
                ApplyTopDownStability();
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (DuelRules.Enabled && Camera.main != null)
            {
                Camera.main.fieldOfView = DuelCameraFov;
                Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
                Camera.main.transparencySortAxis = new Vector3(0f, 0f, 1f);
            }

            if (Time.frameCount - lastStabilityFrame > 120)
                ApplyTopDownStability();
        }

        private static void ApplyHudPatch()
        {
            if (!DuelRules.Enabled) return;
            MatchUI ui = Object.FindFirstObjectByType<MatchUI>();
            if (ui == null) return;

            Canvas canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Transform design = FindDeep(canvas.transform, "DuelHudDesignSpace");
            if (design == null) return;

            Text home = FindText(design, "HomeClub");
            Text away = FindText(design, "AwayClub");
            if (home != null) home.text = "AGUILAR";
            if (away != null) away.text = "ALIANZA";

            EnsureClubLogo(design, "V12_AguilaLogo", "TeamLogos/AguilaLogo",
                new Vector2(.5f, 1f), new Vector2(-292f, -40f), new Vector2(58f, 58f));
            EnsureClubLogo(design, "V12_AlianzaLogo", "TeamLogos/AlianzaLogo",
                new Vector2(.5f, 1f), new Vector2(292f, -40f), new Vector2(58f, 58f));
        }

        private static void EnsureClubLogo(Transform parent, string name, string resource,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RawImage image;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<RawImage>();
            }
            else
            {
                image = existing.GetComponent<RawImage>();
                if (image == null) image = existing.gameObject.AddComponent<RawImage>();
            }

            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return;

            image.texture = texture;
            image.color = Color.white;
            image.material = null;
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private void ApplyTopDownStability()
        {
            lastStabilityFrame = Time.frameCount;

            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (player == null) continue;
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            BallControl ball = BallControl.Instance;
            if (ball != null)
            {
                var rb = ball.GetComponent<Rigidbody2D>();
                if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            if (Camera.main != null)
            {
                Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
                Camera.main.transparencySortAxis = new Vector3(0f, 0f, 1f);
            }
        }

        private static Text FindText(Transform root, string name)
        {
            Transform t = FindDeep(root, name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
