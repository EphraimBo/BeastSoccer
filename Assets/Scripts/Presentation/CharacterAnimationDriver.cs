using System.Collections.Generic;
using UnityEngine;

namespace BeastSoccer.Presentation
{
    /// <summary>Optional Animator adapter for generic/future character controllers.</summary>
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        public Animator animator;
        private bool hasSpeed, hasSprint, hasPossession, hasUlt;
        private readonly HashSet<int> triggers = new HashSet<int>();
        private void Awake() => RefreshParameters();

        public void RefreshParameters()
        {
            hasSpeed = hasSprint = hasPossession = hasUlt = false;
            triggers.Clear();
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "Speed") hasSpeed = true;
                else if (parameter.name == "Sprint") hasSprint = true;
                else if (parameter.name == "HasBall") hasPossession = true;
                else if (parameter.name == "UltActive") hasUlt = true;
                if (parameter.type == AnimatorControllerParameterType.Trigger) triggers.Add(parameter.nameHash);
            }
        }

        public void SetMovement(float speed, bool sprint, bool possession)
        {
            if (animator == null || !animator.enabled) return;
            if (hasSpeed) animator.SetFloat("Speed", speed);
            if (hasSprint) animator.SetBool("Sprint", sprint);
            if (hasPossession) animator.SetBool("HasBall", possession);
        }

        public void Trigger(string trigger)
        {
            int hash = Animator.StringToHash(trigger);
            if (animator != null && animator.enabled && triggers.Contains(hash)) animator.SetTrigger(hash);
        }

        public void SetUltActive(bool active)
        {
            if (animator != null && animator.enabled && hasUlt) animator.SetBool("UltActive", active);
        }
    }
}
