using UnityEngine;
using UnityEngine.UI;

namespace BeastSoccer.UI
{
    /// <summary>Blocks gameplay taps while its associated menu panel is visible.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class ModalInputBlocker : MonoBehaviour
    {
        public GameObject panel;
        private Image graphic;
        private void Awake() => graphic = GetComponent<Image>();
        private void LateUpdate() => graphic.enabled = panel != null && panel.activeInHierarchy;
    }
}
