using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Player;

namespace BeastSoccer.Input
{
    public class TouchInputManager : MonoBehaviour
    {
        public JoystickInput joystick;
        private PlayerController Human => TeamManager.Instance != null ? TeamManager.Instance.CurrentHuman() : null;

        private void Update()
        {
            var h = Human;
            if (h == null || !h.IsHuman) return;

#if UNITY_EDITOR
            // Pause/resume remains available even when gameplay input is gated.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                OnPause();
                return;
            }
#endif

            if (GameManager.Instance == null) return;

            // Kickoff is a fixed formation. The only gameplay input allowed once the setup delay
            // finishes is PASS from the actual kickoff player. That pass releases everyone and
            // starts/resumes the match clock.
            if (GameManager.Instance.Phase == BeastSoccer.Data.MatchPhase.Kickoff)
            {
                h.ClearMovementInput(true);
#if UNITY_EDITOR
                if (GameManager.Instance.CanTakeKickoff(h) && UnityEngine.Input.GetKeyDown(KeyCode.E)) OnPass();
#endif
                return;
            }

            // Human throw-in/corner aiming: the match clock remains paused, the taker stays on
            // the restart spot, and the movement control becomes an aim stick. Other players remain live.
            if (GameManager.Instance.IsHumanAimingSetPiece(h))
            {
#if UNITY_EDITOR
                Vector2 aim = new Vector2(
                    UnityEngine.Input.GetAxisRaw("Horizontal"),
                    UnityEngine.Input.GetAxisRaw("Vertical")
                );
                if (aim.sqrMagnitude <= 0.01f && joystick != null) aim = joystick.Direction;
                h.SetAimInput(aim);
                if (UnityEngine.Input.GetKeyDown(KeyCode.E) || UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.C))
                    OnLob();
#else
                h.SetAimInput(joystick != null ? joystick.Direction : Vector2.zero);
#endif
                return;
            }

            // Never queue movement or action state during goal reset, halftime, etc.
            if (GameManager.Instance.Phase != BeastSoccer.Data.MatchPhase.Playing)
            {
                h.ClearMovementInput(true);
                return;
            }

#if UNITY_EDITOR
            Vector2 keyboard = new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical")
            );
            bool keyboardMoving = keyboard.sqrMagnitude > 0.01f;
            if (keyboardMoving)
                h.SetEditorMoveInput(keyboard.normalized, UnityEngine.Input.GetKey(KeyCode.LeftShift));
            else
                h.SetMoveInput(joystick != null ? joystick.Direction : Vector2.zero);

            // Desktop debug controls. These call exactly the same gameplay methods as touch UI.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) OnShoot();
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) OnPass();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) OnLob();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F)) OnTackle();
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) OnIntercept();
            if (UnityEngine.Input.GetKeyDown(KeyCode.C)) OnLob();
            if (UnityEngine.Input.GetKeyDown(KeyCode.X)) OnUltimate();
            if (UnityEngine.Input.GetKeyDown(KeyCode.V)) OnFly();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) OnSwitch();
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
