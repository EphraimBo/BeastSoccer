using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Player;

namespace BeastSoccer.Input
{
    public class TouchInputManager : MonoBehaviour
    {
        public JoystickInput joystick;
        private PlayerController Human => TeamManager.Instance != null ? TeamManager.Instance.CurrentHuman() : null;

#if UNITY_EDITOR
        private static Vector2 EditorMoveVector()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical")
            );
#else
            // Touch UI remains fully testable in Play Mode. Keyboard debug input is
            // intentionally disabled when Unity has legacy input turned off.
            return Vector2.zero;
#endif
        }

        private static bool EditorSprintHeld()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }

        private static bool EditorKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(key);
#else
            return false;
#endif
        }
#endif

        private void Update()
        {
            var h = Human;
            if (h == null || !h.IsHuman) return;

#if UNITY_EDITOR
            if (EditorKeyDown(KeyCode.Escape))
            {
                OnPause();
                return;
            }
#endif

            if (GameManager.Instance == null) return;

            if (GameManager.Instance.Phase == BeastSoccer.Data.MatchPhase.Kickoff)
            {
                h.ClearMovementInput(true);
#if UNITY_EDITOR
                if (GameManager.Instance.CanTakeKickoff(h) && EditorKeyDown(KeyCode.E)) OnPass();
#endif
                return;
            }

            if (GameManager.Instance.IsHumanAimingSetPiece(h))
            {
#if UNITY_EDITOR
                Vector2 aim = EditorMoveVector();
                if (aim.sqrMagnitude <= 0.01f && joystick != null) aim = joystick.Direction;
                h.SetAimInput(aim);
                if (EditorKeyDown(KeyCode.E) || EditorKeyDown(KeyCode.Q) || EditorKeyDown(KeyCode.C))
                    OnLob();
#else
                h.SetAimInput(joystick != null ? joystick.Direction : Vector2.zero);
#endif
                return;
            }

            if (GameManager.Instance.Phase != BeastSoccer.Data.MatchPhase.Playing)
            {
                h.ClearMovementInput(true);
                return;
            }

#if UNITY_EDITOR
            Vector2 keyboard = EditorMoveVector();
            bool keyboardMoving = keyboard.sqrMagnitude > 0.01f;
            if (keyboardMoving)
                h.SetEditorMoveInput(keyboard.normalized, EditorSprintHeld());
            else
                h.SetMoveInput(joystick != null ? joystick.Direction : Vector2.zero);

            if (EditorKeyDown(KeyCode.Space)) OnShoot();
            if (EditorKeyDown(KeyCode.E)) OnPass();
            if (EditorKeyDown(KeyCode.Q)) OnLob();
            if (EditorKeyDown(KeyCode.F)) OnTackle();
            if (EditorKeyDown(KeyCode.R)) OnIntercept();
            if (EditorKeyDown(KeyCode.C)) OnLob();
            if (EditorKeyDown(KeyCode.X)) OnUltimate();
            if (EditorKeyDown(KeyCode.V)) OnFly();
            if (EditorKeyDown(KeyCode.Tab)) OnSwitch();
#else
            h.SetMoveInput(joystick != null ? joystick.Direction : Vector2.zero);
#endif
        }

        public void OnPrimary()
        {
            var h = Human; if (h == null) return;
            if (DuelRules.Enabled) { if (h.HasBall) OnShoot(); else OnTackle(); return; }
            if (GameManager.Instance != null && GameManager.Instance.Mode == BeastSoccer.Data.GameMode.Defending) { OnTackle(); return; }
            if (h.HasBall || TeamManager.Instance.BallOwner(BeastSoccer.Data.TeamSide.Home) != null || (BeastSoccer.Ball.BallControl.Instance != null && BeastSoccer.Ball.BallControl.Instance.IsRecentKickBy(BeastSoccer.Data.TeamSide.Home, BeastSoccer.Data.GameConfig.Instance.possessionUiFlightGraceSeconds))) OnShoot();
            else OnTackle();
        }

        public void OnSecondary()
        {
            var h = Human; if (h == null) return;
            if (GameManager.Instance != null && GameManager.Instance.Mode == BeastSoccer.Data.GameMode.Defending) { OnIntercept(); return; }
            if (h.HasBall || TeamManager.Instance.BallOwner(BeastSoccer.Data.TeamSide.Home) != null || (BeastSoccer.Ball.BallControl.Instance != null && BeastSoccer.Ball.BallControl.Instance.IsRecentKickBy(BeastSoccer.Data.TeamSide.Home, BeastSoccer.Data.GameConfig.Instance.possessionUiFlightGraceSeconds))) OnPass();
            else OnIntercept();
        }

        public void OnTertiary()
        {
            var h = Human; if (h == null) return;
            if (GameManager.Instance != null && GameManager.Instance.Mode == BeastSoccer.Data.GameMode.Defending) return;
            if (h.HasBall || TeamManager.Instance.BallOwner(BeastSoccer.Data.TeamSide.Home) != null || (BeastSoccer.Ball.BallControl.Instance != null && BeastSoccer.Ball.BallControl.Instance.IsRecentKickBy(BeastSoccer.Data.TeamSide.Home, BeastSoccer.Data.GameConfig.Instance.possessionUiFlightGraceSeconds))) OnLob();
        }

        public void OnShoot() => Human?.Shoot();

        public void OnPass()
        {
            var h = Human;
            if (h == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsHumanAimingSetPiece(h)) h.Lob();
            else h.Pass();
        }

        public void OnThrough() => Human?.ThroughBall();
        public void OnLob() => Human?.Lob();

        public void OnUltimate()
        {
            var ult = Human != null ? Human.Ult : null;
            if (ult == null) return;
            if (DuelRules.Enabled && ult.IsActive) ult.TrySpecialAction();
            else ult.TryActivate();
        }

        public void OnFly() => Human?.Ult?.TrySpecialAction();

        public void OnTackle()
        {
            var h = Human;
            if (h == null || h.Defense == null) return;
            GameFeel.Shake(0.035f);
            h.Defense.Tackle();
        }

        public void OnIntercept() => Human?.Defense?.Intercept();
        public void OnJockey() => Human?.Defense?.Jockey(Human != null ? Human.MoveFacing : Vector2.right);
        public void OnSwitch() => ControlSwitcher.Instance?.SwitchToClosestToBall();

        public void OnPause()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.Phase == BeastSoccer.Data.MatchPhase.Paused) GameManager.Instance.ResumeMatch();
            else GameManager.Instance.PauseMatch();
        }
    }
}
