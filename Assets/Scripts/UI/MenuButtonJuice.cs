using UnityEngine;
using UnityEngine.EventSystems;

namespace BeastSoccer.UI
{
    /// <summary>Small visual response for menu buttons without changing their saved layout.</summary>
    public class MenuButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform visualTarget;
        public float hoverScale = 1.045f;
        public float pressedScale = 0.975f;
        public float wobbleDegrees = 1.4f;
        public float speed = 14f;

        Vector3 baseScale;
        Quaternion baseRotation;
        bool over;
        bool down;
        float phase;

        void OnEnable()
        {
            if (!visualTarget) visualTarget = transform as RectTransform;
            if (visualTarget)
            {
                baseScale = visualTarget.localScale;
                baseRotation = visualTarget.localRotation;
            }
            phase = Random.Range(0f, 10f);
        }

        void OnDisable()
        {
            if (!visualTarget) return;
            visualTarget.localScale = baseScale;
            visualTarget.localRotation = baseRotation;
        }

        void Update()
        {
            if (!visualTarget) return;
            float mult = down ? pressedScale : over ? hoverScale : 1f;
            visualTarget.localScale = Vector3.Lerp(visualTarget.localScale, baseScale * mult, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            float z = over && !down ? Mathf.Sin((Time.unscaledTime + phase) * 7f) * wobbleDegrees : 0f;
            visualTarget.localRotation = Quaternion.Slerp(visualTarget.localRotation, baseRotation * Quaternion.Euler(0f, 0f, z), 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        }

        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }
    }
}
