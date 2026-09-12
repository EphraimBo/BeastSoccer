using UnityEngine;
using UnityEngine.UI;

namespace BeastSoccer.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaRoot : MonoBehaviour
    {
        private Rect last;
        private Vector2 lastSize;

        public static void ConfigureCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            // Expand preserves the complete design height on wide phones and width on tablets.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        public static Rect NormalizedSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return new Rect(0, 0, 1, 1);
            var area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0) return new Rect(0, 0, 1, 1);
            return Rect.MinMaxRect(Mathf.Clamp01(area.xMin / Screen.width),
                Mathf.Clamp01(area.yMin / Screen.height), Mathf.Clamp01(area.xMax / Screen.width),
                Mathf.Clamp01(area.yMax / Screen.height));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Landscape()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        private void OnEnable() => Apply();
        private void Update()
        {
            if (last != Screen.safeArea || lastSize != new Vector2(Screen.width, Screen.height)) Apply();
        }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            last = Screen.safeArea;
            lastSize = new Vector2(Screen.width, Screen.height);
            var rt = (RectTransform)transform;
            var area = NormalizedSafeArea();
            rt.anchorMin = area.min;
            rt.anchorMax = area.max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }
}
