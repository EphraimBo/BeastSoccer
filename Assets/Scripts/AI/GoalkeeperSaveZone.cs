using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;
using BeastSoccer.Ball;

namespace BeastSoccer.AI
{
    public class GoalkeeperSaveZone : MonoBehaviour
    {
        public PlayerController keeper;
        private float cooldownUntil;
        private CircleCollider2D saveCollider;

        private void Awake(){saveCollider=GetComponent<CircleCollider2D>();}
        private void Start(){if(saveCollider!=null && GameConfig.Instance!=null)saveCollider.radius=GameConfig.Instance.keeperSaveRadius;}
        private void OnTriggerEnter2D(Collider2D other) { TrySave(other); }
        private void OnTriggerStay2D(Collider2D other) { TrySave(other); }

        private void TrySave(Collider2D other)
        {
            if (Time.time < cooldownUntil || keeper==null || keeper.Role!=FieldRole.Goalkeeper || keeper.IsWingBlocking || GameManager.Instance==null || GameManager.Instance.Phase!=MatchPhase.Playing) return;
            if(saveCollider!=null) saveCollider.radius=GameConfig.Instance.keeperSaveRadius;
            var ball=other.GetComponentInParent<BallControl>();
            if(ball==null || ball.Mode!=BallControl.BallMode.Free) return;
            if(Vector2.Distance(keeper.transform.position,ball.transform.position)>GameConfig.Instance.keeperSaveRadius) return;

            TeamSide opponent=keeper.Side==TeamSide.Home?TeamSide.Away:TeamSide.Home;
            bool opponentThreat = (ball.LastKicker != null && ball.LastKicker.Side == opponent) ||
                                  (ball.LastTouch != null && ball.LastTouch.Side == opponent);
            bool neutral = ball.LastKicker == null && ball.LastTouch == null;

            // Do not vacuum-catch a teammate's outlet near goal. That was creating accidental
            // back-pass loops where the keeper immediately reclaimed its own team's pass.
            if (!opponentThreat && !neutral) return;

            // FIX22: an ult-powered shot is harder for the keeper to read/save. The failure roll is
            // made once per contact window; cooldown prevents OnTriggerStay from re-rolling every frame.
            if (opponentThreat && ball.LastKickWasUltShot && ball.LastKickType == KickType.Shot &&
                Random.value < GameConfig.Instance.ultShotKeeperMissChance)
            {
                cooldownUntil = Time.time + 0.80f;
                return;
            }

            cooldownUntil=Time.time+0.35f;
            keeper.GainBall(GameConfig.Instance.keeperPossessionSeconds + 0.45f);
            if (!keeper.HasBall) return;
            keeper.SetKeeperHold(GameConfig.Instance.keeperPossessionSeconds);
            keeper.Animation?.Trigger("Save");
            if(GameManager.Instance.Mode==GameMode.Defending && keeper.Side==TeamSide.Home)
            {
                if (opponentThreat) ScoreManager.Instance.RegisterDefendingSave(keeper);
                else GameManager.Instance.RestartDefendingWave();
            }
        }
    }
}
