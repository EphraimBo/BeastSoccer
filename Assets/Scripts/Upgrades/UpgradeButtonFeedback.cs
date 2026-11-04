using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeastSoccer.Upgrades
{
    public sealed class UpgradeButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform visual;
        public Image face;
        public bool primary;
        public GameObject selectedMarker;
        private Button button;
        private Vector3 initialScale;
        private bool selected;
        private float swellUntil;
        private void Awake() { button = GetComponent<Button>(); if (visual != null) initialScale = visual.localScale; }
        public void Select(bool value) { selected = value; Refresh(); }
        private void Refresh()
        {
            if (selectedMarker != null) selectedMarker.SetActive(selected);
            if (face != null) face.color = selected || primary ? Color.white : new Color(.78f, .78f, .78f, 1f);
        }
        private void OnEnable() { if (visual != null) initialScale = visual.localScale; Refresh(); }
        private void OnDisable() { if (visual != null) visual.localScale = initialScale; swellUntil = 0f; }
        private void Update()
        {
            if (visual == null) return;
            float target = Time.unscaledTime < swellUntil && button != null && button.interactable ? 1.035f : 1f;
            visual.localScale = Vector3.Lerp(visual.localScale, initialScale * target, 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime));
        }
        public void OnPointerDown(PointerEventData e) { if (button != null && button.interactable) swellUntil = Time.unscaledTime + .16f; }
        public void OnPointerUp(PointerEventData e) { swellUntil = Time.unscaledTime + .08f; }
        public void OnPointerExit(PointerEventData e) { swellUntil = 0f; }
    }
}
