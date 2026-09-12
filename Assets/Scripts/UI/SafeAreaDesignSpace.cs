using UnityEngine;

namespace BeastSoccer.UI
{
    // Keep anchored controls at safe-area edges while fitting their authored sizes as a group.
    // A notch can make the safe area smaller than the CanvasScaler's full-screen reference.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaDesignSpace : MonoBehaviour
    {
        public Vector2 minimumSize = new Vector2(1600, 900);

        private void OnEnable()
        {
            Canvas.willRenderCanvases += Fit;
            Fit();
        }
        private void OnDisable() => Canvas.willRenderCanvases -= Fit;
        private void LateUpdate() => Fit();

        private void Fit()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            float scale = Mathf.Min(parent.rect.width / Mathf.Max(1, minimumSize.x),
                parent.rect.height / Mathf.Max(1, minimumSize.y));
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * .5f;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = parent.rect.size / scale;
            rt.localScale = Vector3.one * scale;
        }
    }
}
