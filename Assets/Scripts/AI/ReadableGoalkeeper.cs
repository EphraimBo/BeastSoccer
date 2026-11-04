using UnityEngine;
using BeastSoccer.Ball;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Player;
using BeastSoccer.UI;
using BeastSoccer.Upgrades;

namespace BeastSoccer.AI
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class ReadableGoalkeeper : MonoBehaviour
    {
        private PlayerController player;
        private float shotTime = -999f, diveStart = -999f, recoverUntil, nextTrack;
        private Vector2 target, previousBall;
        private bool previousValid, committed;
        public bool IsDiving => UpgradeSettings.Keepers && Time.time >= diveStart && Time.time < diveStart + Profile.diveSeconds;
        public bool IsRecovering => UpgradeSettings.Keepers && !IsDiving && Time.time < recoverUntil;
        public float DiveStartedAt => diveStart;
        public float DiveDirection { get; private set; }
        public float MoveSpeed => IsRecovering ? 0f : IsDiving
            ? Mathf.Min(8f, Profile.diveDistance / Mathf.Max(.1f, Profile.diveSeconds)) : Profile.moveSpeed;
        public KeeperProfile Profile => player != null && player.Side == TeamSide.Away
            ? UpgradeSettings.Current.Profile(MatchSetup.Difficulty).keeper : UpgradeSettings.Current.homeKeeper;
        private void Awake() => player = GetComponent<PlayerController>();
        public bool Tick()
        {
            if (!UpgradeSettings.Keepers || player == null || player.Role != FieldRole.Goalkeeper || player.IsHuman) return false;
            if (player.HasBall) { previousValid = false; return false; } // Existing hold/distribution flow remains authoritative.
            var ball = BallControl.Instance; var cfg = GameConfig.Instance;
            if (ball == null || cfg == null || TeamManager.Instance == null) return false;
            var profile = Profile;
            Vector2 position = player.transform.position, ballPos = ball.transform.position;
            int direction = TeamManager.Instance.AttackDirFor(player.Side);
            float goalX = -direction * cfg.pitchLength * .5f;
            float standingX = goalX + direction * cfg.keeperGoalOffset;
            bool threat = ball.LastKicker != null && ball.LastKicker.Side != player.Side ||
                ball.LastTouch != null && ball.LastTouch.Side != player.Side || ball.LastKicker == null && ball.LastTouch == null;
            bool incoming = ball.IsShot && threat;
            if (!incoming && Time.time >= recoverUntil)
            {
                committed = false;
                if (Time.time >= nextTrack)
                {
                    nextTrack = Time.time + Mathf.Max(.02f, profile.trackingInterval);
                    target = new Vector2(standingX, Mathf.Clamp(ballPos.y * .55f, -cfg.keeperLateralRange, cfg.keeperLateralRange));
                }
            }
            if (incoming && shotTime != ball.LastKickTime)
            {
                shotTime = ball.LastKickTime; committed = false;
                // A shot cannot cancel an existing dive or recovery.
                if (Time.time >= recoverUntil) target = position;
            }
            if (incoming && !committed && Time.time >= recoverUntil && Time.time >= shotTime + profile.reactionSeconds)
            {
                var body = ball.GetComponent<Rigidbody2D>();
                if (body != null && ShotMath.GoalCrossing(ballPos, body.linearVelocity, standingX, out float arrivalY))
                {
                    float delta = Mathf.Clamp(arrivalY, -cfg.keeperLateralRange, cfg.keeperLateralRange) - position.y;
                    if (Mathf.Abs(delta) > profile.standingReach)
                    {
                        diveStart = Time.time; DiveDirection = Mathf.Sign(delta);
                        target = new Vector2(standingX, position.y + Mathf.Clamp(delta, -profile.diveDistance, profile.diveDistance));
                        recoverUntil = diveStart + Mathf.Max(.1f, profile.diveSeconds) + Mathf.Max(0f, profile.recoverySeconds);
                    }
                    committed = true;
                }
            }
            Vector2 toTarget = target - position;
            player.SetAIMove(IsRecovering || toTarget.magnitude < .04f ? Vector2.zero : toTarget.normalized * Mathf.Clamp01(toTarget.magnitude / .25f), false);
            TryCatch(ball);
            return true;
        }
        // Also checked by the boundary authority after physics so fast balls cannot skip a save.
        public bool TryCatch(BallControl ball)
        {
            if (!UpgradeSettings.Keepers || player == null || player.IsHuman || player.HasBall || ball == null) return false;
            Vector2 ballPos = ball.transform.position;
            bool free = ball.Mode == BallControl.BallMode.Free;
            bool threat = ball.LastKicker != null && ball.LastKicker.Side != player.Side ||
                ball.LastTouch != null && ball.LastTouch.Side != player.Side || ball.LastKicker == null && ball.LastTouch == null;
            bool contact = free && threat && !IsRecovering && ball.VisualArcHeight <= Profile.maxCatchHeight &&
                ShotMath.SegmentDistance(player.transform.position, previousValid ? previousBall : ballPos, ballPos) <= Mathf.Max(.05f, Profile.standingReach);
            previousBall = ballPos; previousValid = free;
            if (!contact || GameConfig.Instance == null) return false;
            var cfg = GameConfig.Instance;
            player.GainBall(cfg.keeperPossessionSeconds + .45f);
            if (!player.HasBall) return false;
            previousValid = false;
            player.SetKeeperHold(cfg.keeperPossessionSeconds);
            player.Animation?.Trigger("Save");
            if (GameManager.Instance != null && GameManager.Instance.Mode == GameMode.Defending && player.Side == TeamSide.Home)
                ScoreManager.Instance?.RegisterDefendingSave(player);
            return true;
        }
        public void ResetForRestart()
        {
            diveStart = shotTime = -999f; recoverUntil = nextTrack = 0f;
            previousValid = committed = false; target = transform.position;
        }
        private void OnDrawGizmosSelected()
        {
            if (!UpgradeSettings.Keepers || !UpgradeSettings.Current.showKeeperDebug) return;
            if (player == null) player = GetComponent<PlayerController>();
            Vector3 p = transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(p.x, .05f, p.y), Profile.standingReach);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(p.x, .05f, p.y - Profile.diveDistance), new Vector3(p.x, .05f, p.y + Profile.diveDistance));
        }
    }
}
