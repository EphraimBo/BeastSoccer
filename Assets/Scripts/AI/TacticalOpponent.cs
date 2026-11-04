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
    public sealed class TacticalOpponent : MonoBehaviour
    {
        private PlayerController player;
        private float nextDecision, nextTackle, nextUlt;
        private Vector2 move;
        private bool sprint;
        private readonly Vector2[] candidates = new Vector2[13];
        private void Awake() => player = GetComponent<PlayerController>();
        public void ResetForRestart() { nextDecision = nextTackle = nextUlt = 0f; move = Vector2.zero; }
        public void Tick()
        {
            var cfg = GameConfig.Instance; var team = TeamManager.Instance; var ball = BallControl.Instance;
            if (cfg == null || team == null || ball == null) return;
            var profile = UpgradeSettings.Current.Profile(MatchSetup.Difficulty);
            int dir = team.AttackDirFor(player.Side);
            TeamSide other = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            Vector2 p = transform.position;
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + Mathf.Max(.06f, profile.decisionSeconds);
                if (player.HasBall) Carry(profile, p, dir, other);
                else
                {
                    var carrier = team.BallOwner(other);
                    if (carrier != null)
                    {
                        Vector2 lead = carrier.CurrentVelocity * profile.lookAheadSeconds * profile.predictionWeight;
                        Vector2 intercept = Clamp((Vector2)carrier.transform.position + lead, cfg);
                        move = (intercept - p).normalized;
                        bool protectedKeeper = carrier.Role == FieldRole.Goalkeeper && Time.time < carrier.KeeperHoldUntil;
                        if (protectedKeeper)
                            move = (p - (Vector2)carrier.transform.position).magnitude < cfg.keeperNoCrowdRadius
                                ? (p - (Vector2)carrier.transform.position).normalized : Vector2.zero;
                        bool close = Vector2.Distance(p, carrier.transform.position) <= cfg.tackleRange;
                        Vector2 toTackler = p - (Vector2)carrier.transform.position;
                        bool legalAngle = Vector2.Angle(carrier.MoveFacing, toTackler) < 180f - cfg.tackleRearForbiddenHalfAngleDegrees;
                        if (!protectedKeeper && close && legalAngle && carrier.CanBeTackled && player.CanAttemptTackle && Time.time >= nextTackle)
                        { player.Defense?.Tackle(); nextTackle = Time.time + Mathf.Max(.2f, profile.tackleCooldown); }
                    }
                    else
                    {
                        var owner = ball.Owner;
                        Vector2 target = owner != null && owner.Side == player.Side
                            ? new Vector2(owner.transform.position.x + dir * 2f, -Mathf.Sign(owner.transform.position.y + .01f) * 3f)
                            : (Vector2)ball.transform.position;
                        move = (Clamp(target, cfg) - p).normalized;
                    }
                    sprint = player.SprintStamina01 > .2f;
                    TryUlt(profile, player.HasBall ? 0f : Vector2.Distance(p, ball.transform.position) < 3f ? 2f : 0f);
                }
            }
            player.SetAIMove(move, sprint);
        }
        private void Carry(DifficultyProfile profile, Vector2 p, int dir, TeamSide other)
        {
            var cfg = GameConfig.Instance;
            float goalX = dir * cfg.pitchLength * .5f;
            float goalDistance = Vector2.Distance(p, new Vector2(goalX, 0f));
            float shotQuality = BestShot(profile, p, goalX, other, out Vector2 shotTarget);
            float pressure = 99f;
            foreach (var defender in TeamManager.Instance.Team(other))
                if (defender != null && !defender.IsSuppressed) pressure = Mathf.Min(pressure, Vector2.Distance(p, defender.transform.position));
            TryUlt(profile, (pressure < 2.4f ? 1.8f : 0f) + (DuelRules.CanScoreFrom(player) ? .8f : 0f));
            if (DuelRules.CanScoreFrom(player) && goalDistance < cfg.aiShootDistance + 1.2f && shotQuality >= profile.shotMinimumQuality)
            {
                shotTarget.y += Random.Range(-profile.aimError, profile.aimError);
                shotTarget.y = Mathf.Clamp(shotTarget.y, -cfg.shotGoalHalfWidth + .12f, cfg.shotGoalHalfWidth - .12f);
                player.AIShootAt(shotTarget); move = Vector2.zero; sprint = false; return;
            }
            // Future larger teams can pass to a safe forward outlet under pressure.
            if (!DuelRules.Enabled && pressure < 1.6f)
            {
                var mate = TeamManager.Instance.BestPassTarget(player, new Vector2(dir, 0f), false, null, cfg.aiMinPassDistance);
                if (mate != null && mate.transform.position.x * dir > p.x * dir &&
                    LaneClearance(p, mate.transform.position, other) > .7f)
                { player.AIKickTo(mate, false); move = Vector2.zero; return; }
            }
            int count = Mathf.Clamp(profile.directionSamples, 5, candidates.Length);
            float best = float.NegativeInfinity; Vector2 selected = new Vector2(dir, 0f);
            float baseSpeed = player.EstimatedRunSpeed;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(-115f, 115f, i / (float)(count - 1)) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle) * dir, Mathf.Sin(angle));
                candidates[i] = direction;
                Vector2 end = p + direction * baseSpeed * profile.lookAheadSeconds;
                float value = direction.x * dir * 1.3f - Mathf.Abs(end.y) * .035f;
                if (end != Clamp(end, cfg)) value -= 8f;
                foreach (var defender in TeamManager.Instance.Team(other))
                {
                    if (defender == null || defender.IsSuppressed) continue;
                    Vector2 anticipated = (Vector2)defender.transform.position + defender.CurrentVelocity * profile.lookAheadSeconds * profile.predictionWeight;
                    float clearance = ShotMath.SegmentDistance(anticipated, p, end);
                    value -= Mathf.Max(0f, 2f - clearance) * (1.2f + profile.predictionWeight);
                }
                value += Vector2.Dot(direction, move) * .35f; // Stable commitment beats frame-to-frame zigzagging.
                if (value > best) { best = value; selected = direction; }
            }
            move = selected; sprint = pressure < 2.8f && player.SprintStamina01 > .25f;
        }
        private float BestShot(DifficultyProfile profile, Vector2 origin, float goalX, TeamSide other, out Vector2 target)
        {
            var cfg = GameConfig.Instance; var keeper = TeamManager.Instance.GoalkeeperFor(other);
            target = new Vector2(goalX, 0f); float best = float.NegativeInfinity;
            float half = cfg.shotGoalHalfWidth - .18f;
            for (int i = 0; i < 5; i++)
            {
                Vector2 point = new Vector2(goalX, Mathf.Lerp(-half, half, i / 4f));
                float distance = Vector2.Distance(origin, point);
                float speed = player.EstimatedShotSpeed(distance);
                float time = ShotMath.TravelTime(distance, speed, cfg.ballLinearDrag);
                float clearance = LaneClearance(origin, point, other);
                float quality = Mathf.Clamp01(clearance / 2f) - distance * .018f;
                if (keeper != null)
                {
                    var kp = other == TeamSide.Home ? UpgradeSettings.Current.homeKeeper : profile.keeper;
                    float available = Mathf.Max(0f, time - kp.reactionSeconds);
                    float reachable = kp.standingReach + Mathf.Min(kp.diveDistance, available * kp.moveSpeed);
                    quality += (Mathf.Abs(point.y - keeper.transform.position.y) - reachable) * .45f;
                }
                if (quality > best) { best = quality; target = point; }
            }
            return best;
        }
        private static float LaneClearance(Vector2 a, Vector2 b, TeamSide other)
        {
            float closest = 3f;
            foreach (var defender in TeamManager.Instance.Team(other))
                if (defender != null && !defender.IsSuppressed && defender.Role != FieldRole.Goalkeeper)
                    closest = Mathf.Min(closest, ShotMath.SegmentDistance(defender.transform.position, a, b));
            return closest;
        }
        private void TryUlt(DifficultyProfile profile, float benefit)
        {
            if (Time.time < nextUlt || player.Ult == null || player.Ult.Charge < .999f || player.Ult.IsActive || player.Ult.IsActivating) return;
            nextUlt = Time.time + Mathf.Max(.15f, profile.decisionSeconds);
            if (benefit >= profile.ultMinimumBenefit) player.Ult.TryActivateAI();
        }
        private static Vector2 Clamp(Vector2 p, GameConfig cfg) => new Vector2(
            Mathf.Clamp(p.x, -cfg.pitchLength * .5f + .4f, cfg.pitchLength * .5f - .4f),
            Mathf.Clamp(p.y, -cfg.pitchWidth * .5f + .4f, cfg.pitchWidth * .5f - .4f));
    }
}
