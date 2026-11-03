using System;
using UnityEngine;
using BeastSoccer.Core;

namespace BeastSoccer.UI
{
    /// <summary>
    /// The scene contains the editable duel layout. Its original larger-team layout is retained
    /// as a startup profile, so switching arcadeDuel off restores the team controls and geometry.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("Beast Soccer/Match HUD Layout (duel and larger-team profiles)")]
    public sealed class MatchHudLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class RectState
        {
            public RectTransform target;
            public Transform parent;
            public int siblingIndex;
            public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
            public Vector3 localScale;
            public Quaternion localRotation;
            public bool activeSelf;

            public void ApplyGeometry()
            {
                if (target == null) return;
                target.anchorMin = anchorMin;
                target.anchorMax = anchorMax;
                target.pivot = pivot;
                target.anchoredPosition = anchoredPosition;
                target.sizeDelta = sizeDelta;
                target.localScale = localScale;
                target.localRotation = localRotation;
                target.gameObject.SetActive(activeSelf);
            }
        }

        public GameObject[] duelOnlyObjects;
        public RectState[] teamLayout;
        public DuelHud duelHud;
        public BeastSoccer.Input.JoystickInput joystick;
        public float teamKnobRange = 90f;
        public float duelKnobRange = 160f;
        public MatchUI matchUI;
        public float teamSprintStaminaWidth = 230f;
        private RectState[] duelLayout;
        private ArcGraphic duelSprintTrack, duelSprintSweep;
        private float duelSprintStaminaWidth;

        private void Awake()
        {
            CaptureDuelLayout();
            ApplyMode(DuelRules.Enabled);
        }

        private void CaptureDuelLayout()
        {
            if (duelLayout != null || teamLayout == null) return;
            duelLayout = new RectState[teamLayout.Length];
            for (int i = 0; i < teamLayout.Length; i++)
            {
                var target = teamLayout[i].target;
                duelLayout[i] = target == null ? new RectState() : new RectState
                {
                    target = target, parent = target.parent, siblingIndex = target.GetSiblingIndex(),
                    anchorMin = target.anchorMin, anchorMax = target.anchorMax, pivot = target.pivot,
                    anchoredPosition = target.anchoredPosition, sizeDelta = target.sizeDelta,
                    localScale = target.localScale, localRotation = target.localRotation,
                    activeSelf = target.gameObject.activeSelf
                };
            }
            if (joystick != null)
            {
                duelSprintTrack = joystick.sprintTrack;
                duelSprintSweep = joystick.sprintSweep;
            }
            if (matchUI != null) duelSprintStaminaWidth = matchUI.sprintStaminaMaxWidth;
        }

        public void ApplyMode(bool duel)
        {
            CaptureDuelLayout();
            var profile = duel ? duelLayout : teamLayout;
            if (profile != null)
            {
                foreach (var entry in profile)
                    if (entry.target != null) entry.target.SetParent(entry.parent, false);
                foreach (var entry in profile) entry.ApplyGeometry();
                // Reinsert in ascending order so a later low-index item cannot displace a
                // previously restored high-index item (especially modal blockers/panels).
                var siblingOrder = (RectState[])profile.Clone();
                Array.Sort(siblingOrder, (a, b) => a.siblingIndex.CompareTo(b.siblingIndex));
                foreach (var entry in siblingOrder)
                    if (entry.target != null) entry.target.SetSiblingIndex(entry.siblingIndex);
            }
            if (duelOnlyObjects != null)
                foreach (var item in duelOnlyObjects)
                    if (item != null) item.SetActive(duel);
            if (duelHud != null) duelHud.enabled = duel;
            if (joystick != null)
            {
                joystick.knobRange = duel ? duelKnobRange : teamKnobRange;
                joystick.sprintTrack = duel ? duelSprintTrack : null;
                joystick.sprintSweep = duel ? duelSprintSweep : null;
            }
            if (matchUI != null)
                matchUI.sprintStaminaMaxWidth = duel ? duelSprintStaminaWidth : teamSprintStaminaWidth;
        }
    }
}
