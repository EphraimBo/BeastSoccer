using System.Collections;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Ball;
using BeastSoccer.Audio;
using BeastSoccer.Presentation;

namespace BeastSoccer.Player
{
    [RequireComponent(typeof(PlayerController), typeof(Rigidbody2D))]
    public class DefensiveActions : MonoBehaviour
    {
        private PlayerController player;
        private Rigidbody2D rb;
        private bool busy;

        private void Awake() { player=GetComponent<PlayerController>(); rb=GetComponent<Rigidbody2D>(); }
        private void OnDisable() { CancelAll(); }

        public void Tackle()
        {
            if (!CanDefend() || !player.CanAttemptTackle) return;
            StartCoroutine(TackleRoutine());
        }

        private IEnumerator TackleRoutine()
        {
            busy=true;
            // Tackling is an action state, not a locomotion freeze. The player keeps moving
            // through the tackle so the button never feels like a brake pedal.
            // FIX22: the tackle is represented by the collision itself rather than a canned lunge animation.
            float windup = GameConfig.Instance.tackleWindupSeconds;
            if (player.Character == CharacterType.Goro) windup *= GameConfig.Instance.goroTackleWindupMultiplier;
            yield return new WaitForSeconds(windup / Mathf.Max(0.5f, player.TackleMultiplier));

            // Stability-first tackle: the action no longer MovePositions a dynamic body while
            // PlayerController is also solving velocity. The active tackle window does the hit test;
            // a visual lunge can be added later through animation without creating a second motion owner.
            yield return new WaitForFixedUpdate();
            bool success=false;
            float activeEnd = Time.time + Mathf.Max(0.05f, GameConfig.Instance.tackleActiveSeconds);
            while (Time.time < activeEnd && !success)
            {
                success = TryResolveTackle();
                yield return new WaitForFixedUpdate();
            }

            yield return new WaitForSeconds(GameConfig.Instance.tackleRecoverySeconds / Mathf.Max(0.5f, player.TackleMultiplier));
            busy=false;
        }

        private bool TryResolveTackle()
        {
            float tackleRange = GameConfig.Instance.tackleRange * (player.Character == CharacterType.Goro ? GameConfig.Instance.goroTackleRangeMultiplier : 1f);
            var hits = Physics2D.OverlapCircleAll(rb.position, tackleRange);
            foreach (var hit in hits)
            {
                var target = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                if (target==null || target.Side==player.Side || !target.HasBall || target.IsFlying) continue;

                // FIX22: tackle eligibility is based on where the tackler is relative to the CARRIER.
                // The only illegal tackle zone is a 120-degree cone directly behind the carrier
                // (60 degrees either side of straight-behind). Front and side collisions are fair.
                Vector2 toTarget = (Vector2)target.transform.position - rb.position;
                Vector2 carrierFacing = target.MoveFacing.sqrMagnitude > 0.01f
                    ? target.MoveFacing.normalized
                    : new Vector2(TeamManager.Instance.AttackDirFor(target.Side), 0f);
                Vector2 carrierToTackler = rb.position - (Vector2)target.transform.position;
                if (carrierToTackler.sqrMagnitude > 0.001f)
                {
                    float fromCarrierForward = Vector2.Angle(carrierFacing, carrierToTackler.normalized);
                    float rearThreshold = 180f - Mathf.Clamp(GameConfig.Instance.tackleRearForbiddenHalfAngleDegrees, 0f, 89.9f);
                    if (fromCarrierForward >= rearThreshold) continue;
                }

                // Keepers are protected while holding after a save/claim. This gives their team time
                // to spread out instead of forming a tackle scrum around the six-yard area.
                if (target.Role == FieldRole.Goalkeeper && Time.time < target.KeeperHoldUntil) return true;

                // Goro's attacking ult is a bulldozer state: ordinary outfield tackles bounce off.
                // A goalkeeper is the deliberate exception and can still stop him.
                if (target.IsGoroUltTackleImmune && player.Role != FieldRole.Goalkeeper)
                {
                    Vector2 away = (Vector2)player.transform.position - (Vector2)target.transform.position;
                    if (away.sqrMagnitude < 0.001f) away = -target.MoveFacing;
                    player.ApplyBulldozePush(away.normalized * GameConfig.Instance.goroBulldozePush);
                    player.Animation?.Trigger("Hit");
                    ComicImpactFX.Spawn(((Vector2)player.transform.position + (Vector2)target.transform.position) * 0.5f, 1.05f);
                    return true;
                }

                if (!target.CanBeTackled) continue;
                float attackStrength = target.StrengthMultiplier;
                float tackleStrength = player.TackleMultiplier;
                bool overpoweringCarrier = attackStrength > tackleStrength * 1.50f;
                if (!overpoweringCarrier)
                {
                    // A clean tackle carries the tackler through the challenge instead of
                    // stopping both bodies dead. The dispossessed carrier is nudged away/laterally
                    // in LoseBallFromTackle, creating visible separation before the next duel.
                    Vector2 throughDir = ((Vector2)target.transform.position - rb.position);
                    if (throughDir.sqrMagnitude < 0.001f) throughDir = player.MoveFacing;
                    throughDir = throughDir.sqrMagnitude > 0.001f ? throughDir.normalized : Vector2.right;
                    target.LoseBallFromTackle(player);
                    float followMult = player.Character == CharacterType.Goro ? GameConfig.Instance.goroTackleFollowThroughMultiplier : 1f;
                    ComicImpactFX.Spawn(((Vector2)player.transform.position + (Vector2)target.transform.position) * 0.5f, player.Character == CharacterType.Goro ? 1.65f : 1.45f);
                    if (GameManager.Instance.Mode == GameMode.Defending && player.Side == TeamSide.Home)
                    {
                        Vector2 clearDir = new Vector2(-TeamManager.Instance.AttackDirFor(TeamSide.Away), Random.Range(-0.18f,0.18f)).normalized;
                        BallControl.Instance.Deflect(clearDir * GameConfig.Instance.throughForce, player);
                    }
                    else player.GainBall(GameConfig.Instance.postTackleProtectionSeconds);
                    // Apply the collision carry-through AFTER GainBall. GainBall intentionally clears
                    // old contact velocity, which previously made a successful tackle feel like a stop.
                    player.ApplyTackleFollowThrough(throughDir, followMult);
                    TeamManager.Instance?.AddTeamUltCharge(player.Side, GameConfig.Instance.ultChargePerAction);
                    AudioManager.Instance?.PlayTackle();
                    GameFeel.Shake(0.13f);
                    return true;
                }

                player.ApplyExternalPush(-player.MoveFacing * 0.30f);
                player.Animation?.Trigger("Hit");
                ComicImpactFX.Spawn(((Vector2)player.transform.position + (Vector2)target.transform.position) * 0.5f, 0.85f);
                return true;
            }
            return false;
        }

        public void Intercept()
        {
            if (!CanDefend() || BallControl.Instance == null || BallControl.Instance.Mode != BallControl.BallMode.Free) return;
            if (BallControl.Instance.LastKickType == KickType.Lob &&
                !BallControl.Instance.CanOutfieldControlLob(player)) return;
            StartCoroutine(InterceptRoutine());
        }

        private IEnumerator InterceptRoutine()
        {
            busy=true;
            player.SetDefensiveActionLock(true);
            player.Animation?.Trigger("Intercept");
            float d = Vector2.Distance(transform.position, BallControl.Instance.transform.position);
            if (d <= GameConfig.Instance.interceptRange)
            {
                if (GameManager.Instance.Mode == GameMode.Defending && player.Side == TeamSide.Home)
                {
                    Vector2 clearDir = new Vector2(-TeamManager.Instance.AttackDirFor(TeamSide.Away), Random.Range(-0.15f,0.15f)).normalized;
                    BallControl.Instance.Deflect(clearDir * GameConfig.Instance.passForce, player);
                    TeamManager.Instance?.AddTeamUltCharge(player.Side, GameConfig.Instance.ultChargePerAction);
                }
                else
                {
                    player.GainBall();
                    if (player.HasBall) TeamManager.Instance?.AddTeamUltCharge(player.Side, GameConfig.Instance.ultChargePerAction);
                }
            }
            yield return new WaitForSeconds(GameConfig.Instance.interceptRecoverySeconds);
            player.SetDefensiveActionLock(false);
            busy=false;
        }

        public void Jockey(Vector2 pushDirection)
        {
            if (!CanDefend()) return;
            if (player.Role == FieldRole.Goalkeeper || player.Character != CharacterType.Generic) return;
            var carrier = OpponentCarrierNearby();
            if (carrier != null) StartCoroutine(JockeyRoutine(carrier, pushDirection));
        }

        private IEnumerator JockeyRoutine(PlayerController carrier, Vector2 pushDir)
        {
            busy=true;
            player.Animation?.Trigger("Jockey");
            float end = Time.time + GameConfig.Instance.jockeySeconds;
            while (Time.time < end && carrier != null && carrier.HasBall && Vector2.Distance(transform.position,carrier.transform.position) <= GameConfig.Instance.jockeyRange*1.35f)
            {
                Vector2 dir = player.MoveFacing.sqrMagnitude > 0.01f ? player.MoveFacing.normalized : (pushDir.sqrMagnitude > 0.01f ? pushDir.normalized : Vector2.right);
                float resist = Mathf.Max(0.5f, carrier.StrengthMultiplier / Mathf.Max(0.2f,player.StrengthMultiplier));
                carrier.ApplyExternalPush(dir * (GameConfig.Instance.jockeyPushSpeed / resist));
                yield return new WaitForFixedUpdate();
            }
            busy=false;
        }

        public void CancelForRestart() => CancelAll();
        public void CancelForControlChange() => CancelAll();

        private void CancelAll()
        {
            StopAllCoroutines();
            busy=false;
            if(player!=null) player.SetDefensiveActionLock(false);
        }

        private bool CanDefend()
        {
            if (busy || player.IsActionLocked || player.IsSuppressed || player.IsWingBlocking || player.IsFlying || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing)
                return false;

            // During a keeper's protected hold, opponents are required to clear the area rather
            // than spam tackle/jockey animations into an untackleable carrier.
            if (TeamManager.Instance != null && GameConfig.Instance != null)
            {
                TeamSide other = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                var carrier = TeamManager.Instance.BallOwner(other);
                if (carrier != null && carrier.Role == FieldRole.Goalkeeper && Time.time < carrier.KeeperHoldUntil &&
                    Vector2.Distance(player.transform.position, carrier.transform.position) < GameConfig.Instance.keeperNoCrowdRadius)
                    return false;
            }
            return true;
        }

        private PlayerController OpponentCarrierNearby()
        {
            TeamSide other = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            var c = TeamManager.Instance.BallOwner(other);
            if (c == null) return null;
            if (c.IsGoroBulldozing) return null;
            if (c.Role == FieldRole.Goalkeeper && Time.time < c.KeeperHoldUntil) return null;
            return Vector2.Distance(transform.position,c.transform.position) <= GameConfig.Instance.jockeyRange ? c : null;
        }
    }
}
