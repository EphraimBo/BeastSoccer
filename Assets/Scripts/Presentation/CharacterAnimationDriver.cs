using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    public class CharacterAnimationDriver : MonoBehaviour
    {
        public Animator animator;

        [Header("Directional sprite prototype")]
        public bool directionalRunPrototype;
        public CharacterType directionalCharacter = CharacterType.Generic;
        public bool sourceArtFacesLeft = true;
        public bool actionArtFacesLeft = true;
        public string sideRunState;
        public string frontThreeQuarterRunState;
        public string frontRunState;
        public string backThreeQuarterRunState;
        public string backRunState;
        public string shootState;
        public string ultEnterState;

        private bool hasSpeed, hasSprint, hasPossession, hasUlt;
        private readonly System.Collections.Generic.HashSet<int> triggers = new System.Collections.Generic.HashSet<int>();
        private int currentDirectionalState;
        private string currentDirectionalStateName;
        private string pendingDirectionalStateName;
        private float pendingDirectionalStateSince;
        private float lastNormalizedSpeed;
        private float prototypeActionUntil;

        public bool UsesDirectionalRunPrototype => directionalRunPrototype && animator != null && animator.runtimeAnimatorController != null;
        public bool SourceArtFacesLeft => PrototypeActionPlaying ? actionArtFacesLeft : sourceArtFacesLeft;
        public bool PrototypeActionPlaying => Time.time < prototypeActionUntil;

        private void Awake() { RefreshParameters(); }

        public void RefreshParameters()
        {
            hasSpeed = hasSprint = hasPossession = hasUlt = false;
            triggers.Clear();
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed") hasSpeed = true;
                else if (p.name == "Sprint") hasSprint = true;
                else if (p.name == "HasBall") hasPossession = true;
                else if (p.name == "UltActive") hasUlt = true;
                else if (p.type == AnimatorControllerParameterType.Trigger) triggers.Add(p.nameHash);
            }
        }

        public void ConfigureDirectionalPrototype(CharacterType character, RuntimeAnimatorController controller)
        {
            directionalRunPrototype = controller != null;
            directionalCharacter = character;
            currentDirectionalState = 0;
            currentDirectionalStateName = "";
            pendingDirectionalStateName = "";
            pendingDirectionalStateSince = 0f;
            prototypeActionUntil = 0f;
            sourceArtFacesLeft = true;
            actionArtFacesLeft = true;
            sideRunState = frontThreeQuarterRunState = frontRunState = backThreeQuarterRunState = backRunState = shootState = ultEnterState = "";

            if (controller == null) return;
            if (character == CharacterType.Leo && controller.name.Contains("Leo_Directional"))
            {
                sideRunState = "Leo_Run_Side";
                frontThreeQuarterRunState = "Leo_Run_3Q";
                frontRunState = "Leo_Run_Front";
                shootState = "Leo_Shoot_Temp";
                ultEnterState = "Leo_Ult_Temp";
                // The temporary Shoot/Ult sheets face the opposite default direction from Leo's run art.
                actionArtFacesLeft = false;
            }
            else if (character == CharacterType.Volt && controller.name.Contains("Volt_Directional"))
            {
                sideRunState = "Volt_Run_Side";
                frontThreeQuarterRunState = "Volt_Run_Front3Q";
                frontRunState = "Volt_Run_Front";
                backThreeQuarterRunState = "Volt_Run_Back3Q";
                backRunState = "Volt_Run_Back";
            }
            else directionalRunPrototype = false;
        }

        public void SetMovement(float normalizedSpeed, bool sprint, bool hasBall)
        {
            lastNormalizedSpeed = normalizedSpeed;
            if (animator == null) return;
            if (hasSpeed) animator.SetFloat("Speed", normalizedSpeed);
            if (hasSprint) animator.SetBool("Sprint", sprint);
            if (hasPossession) animator.SetBool("HasBall", hasBall);
        }

        public void UpdateDirectionalRun(CharacterType character, Vector2 facing)
        {
            if (!UsesDirectionalRunPrototype || character != directionalCharacter || string.IsNullOrEmpty(sideRunState)) return;

            if (PrototypeActionPlaying)
            {
                animator.speed = 1f;
                return;
            }

            float ax = Mathf.Abs(facing.x);
            float ay = Mathf.Abs(facing.y);
            bool towardCamera = facing.y < -0.08f;
            bool awayFromCamera = facing.y > 0.08f;
            float depthAngle = Mathf.Atan2(ay, Mathf.Max(0.0001f, ax)) * Mathf.Rad2Deg; // 0=side, 90=front/back

            // FIX26 direction hysteresis: keep the currently displayed angle a little longer near
            // boundaries instead of flipping back/forth every time the joystick wiggles by a degree.
            const float sideEnter = 20f;
            const float sideLeave = 29f;
            const float frontEnter = 70f;
            const float frontLeave = 61f;

            bool currentIsSide = currentDirectionalStateName == sideRunState;
            bool currentIsFront = currentDirectionalStateName == frontRunState || currentDirectionalStateName == backRunState;

            string stateName;
            if (currentIsSide ? depthAngle < sideLeave : depthAngle <= sideEnter)
            {
                stateName = sideRunState;
            }
            else if (currentIsFront ? depthAngle >= frontLeave : depthAngle >= frontEnter)
            {
                if (towardCamera && HasPrototypeState(frontRunState)) stateName = frontRunState;
                else if (awayFromCamera && HasPrototypeState(backRunState)) stateName = backRunState;
                else stateName = sideRunState;
            }
            else
            {
                if (towardCamera && HasPrototypeState(frontThreeQuarterRunState)) stateName = frontThreeQuarterRunState;
                else if (awayFromCamera && HasPrototypeState(backThreeQuarterRunState)) stateName = backThreeQuarterRunState;
                else stateName = sideRunState;
            }

            if (string.IsNullOrEmpty(currentDirectionalStateName))
            {
                SwitchDirectionalState(stateName);
            }
            else if (stateName != currentDirectionalStateName)
            {
                if (pendingDirectionalStateName != stateName)
                {
                    pendingDirectionalStateName = stateName;
                    pendingDirectionalStateSince = Time.time;
                }
                else if (Time.time - pendingDirectionalStateSince >= 0.065f)
                {
                    SwitchDirectionalState(stateName);
                }
            }
            else
            {
                pendingDirectionalStateName = "";
                pendingDirectionalStateSince = 0f;
            }

            animator.speed = lastNormalizedSpeed > 0.035f
                ? Mathf.Clamp(0.80f + lastNormalizedSpeed * 0.28f, 0.80f, 1.18f)
                : 0f;
        }

        private void SwitchDirectionalState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName) || !HasPrototypeState(stateName)) return;
            string fullStateName = "Base Layer." + stateName;
            int stateHash = Animator.StringToHash(fullStateName);
            if (currentDirectionalState != stateHash)
            {
                animator.CrossFade(fullStateName, 0.060f, 0);
                currentDirectionalState = stateHash;
                currentDirectionalStateName = stateName;
            }
            pendingDirectionalStateName = "";
            pendingDirectionalStateSince = 0f;
        }

        public void Trigger(string name)
        {
            if (animator == null) return;

            if (UsesDirectionalRunPrototype && directionalCharacter == CharacterType.Leo)
            {
                if (name == "Shoot" && PlayPrototypeAction(shootState, 0.44f)) return;
                if (name == "UltEnter" && PlayPrototypeAction(ultEnterState, 0.82f)) return;
            }

            int h = Animator.StringToHash(name);
            if (triggers.Contains(h)) animator.SetTrigger(h);
        }

        private bool PlayPrototypeAction(string stateName, float seconds)
        {
            if (!HasPrototypeState(stateName)) return false;
            animator.speed = 1f;
            animator.CrossFade("Base Layer." + stateName, 0.025f, 0, 0f);
            currentDirectionalState = 0;
            currentDirectionalStateName = "";
            pendingDirectionalStateName = "";
            prototypeActionUntil = Time.time + Mathf.Max(0.05f, seconds);
            return true;
        }

        private bool HasPrototypeState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;
            return animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
        }

        public void SetUltActive(bool active)
        {
            if (animator != null && hasUlt) animator.SetBool("UltActive", active);
        }
    }
}
