using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    /// <summary>Updates duel controls. Artwork and geometry are authored in Match.unity.</summary>
    public sealed class DuelHud : MonoBehaviour
    {
        public MatchUI ui;
        public ArcGraphic shotGlow;
        public ArcGraphic tackleCover;
        public Text hint;

        public void Initialize(MatchUI owner) => ui = owner;

        private void LateUpdate()
        {
            if (!DuelRules.Enabled || ui == null) return;
            var human = TeamManager.Instance?.CurrentHuman();
            if (human == null || ui.primaryButton == null) return;
            bool playing = GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing;
            bool ready = human.HasBall && DuelRules.CanScoreFrom(human);
            ui.primaryButton.interactable = playing && (!human.HasBall || ready);
            if (ui.primaryButton.targetGraphic is Image image)
            {
                image.sprite = ui.shootButtonSprite;
                image.color = human.HasBall && !ready ? new Color(.7f, .7f, .7f, .8f) : Color.white;
                image.preserveAspect = true;
            }
            if (tackleCover != null) tackleCover.gameObject.SetActive(!human.HasBall);
            if (ui.primaryLabel != null) ui.primaryLabel.text = human.HasBall ? "" : "TACKLE";
            if (shotGlow != null)
            {
                shotGlow.gameObject.SetActive(playing && ready);
                shotGlow.color = new Color(.12f, .65f, 1f, .72f + .2f * Mathf.Sin(Time.time * 5f));
            }
            if (hint != null)
                hint.text = !playing ? "" : human.HasBall
                    ? (ready ? "SHOOTING ZONE" : "ATTACK  →  ENTER THE BLUE ARC") : "WIN THE BALL";
        }
    }
}
