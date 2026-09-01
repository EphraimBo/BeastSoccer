using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;
using BeastSoccer.Ball;

namespace BeastSoccer.AI
{
    [RequireComponent(typeof(PlayerController))]
    public class AIController : MonoBehaviour
    {
        private enum RequestedAction { None, Pass, Through, Lob, Shoot }

        private PlayerController player;
        private float nextDecision;
        private float homeLaneY;
        private PlayerController requestedTarget;
        private float requestedActionAt;
        private RequestedAction requestedAction;

        private Vector2 steeringTarget;
        private bool hasSteeringTarget;
        private bool steeringSprint;
        private float wideCarryUntil;
        private float wideCarryY;
        private bool hadBallLastStep;
        private float possessionCommitUntil;
        private PlayerController recentPasser;
        private float avoidReturnPassUntil;
        private float closeEscapeUntil;
        private float closeEscapeY;
        private float duelDisengageUntil;
        private Vector2 duelDisengageTarget;
        private float nextTackleAllowed;
        private PlayerController kickoffReturnTarget;
        private float kickoffReturnAt;

        private void Awake() { player = GetComponent<PlayerController>(); }
        private void Start() { homeLaneY = transform.position.y; ForceImmediateDecision(); }

        public void RequestPass(PlayerController target, bool through = false)
        {
            if (player.IsHuman || !player.HasBall || target == null || target.Side != player.Side) return;
            requestedTarget = target;
            if (player.Role == FieldRole.Goalkeeper)
            {
                // A human call-for-pass overrides the keeper stall immediately; keeper distribution
                // is always a throw/lob style outlet.
                player.ReleaseKeeperHoldNow();
                requestedAction = RequestedAction.Lob;
                requestedActionAt = Time.time + Mathf.Max(0f, GameConfig.Instance.keeperCallPassImmediateDelay);
                return;
            }
            if (!through && IsPassLaneBlocked(target)) requestedAction = RequestedAction.Lob;
            else requestedAction = through ? RequestedAction.Through : RequestedAction.Pass;
            requestedActionAt = Time.time + Random.Range(GameConfig.Instance.passRequestDelayMin, GameConfig.Instance.passRequestDelayMax);
        }

        public void RequestLob(PlayerController target)
        {
            if (player.IsHuman || !player.HasBall || target == null || target.Side != player.Side) return;
            requestedTarget = target;
            if (player.Role == FieldRole.Goalkeeper) player.ReleaseKeeperHoldNow();
            requestedAction = RequestedAction.Lob;
            requestedActionAt = player.Role == FieldRole.Goalkeeper ? Time.time + Mathf.Max(0f, GameConfig.Instance.keeperCallPassImmediateDelay) : Time.time + Random.Range(GameConfig.Instance.passRequestDelayMin, GameConfig.Instance.passRequestDelayMax);
        }

        public void RequestShoot()
        {
            if (player.IsHuman || !player.HasBall) return;
            requestedTarget = null;
            requestedAction = RequestedAction.Shoot;
            requestedActionAt = Time.time + Random.Range(GameConfig.Instance.passRequestDelayMin, GameConfig.Instance.passRequestDelayMax);
        }

        private bool IsPassLaneBlocked(PlayerController target)
        {
            if (target == null || TeamManager.Instance == null || GameConfig.Instance == null) return false;
            Vector2 a = transform.position;
            Vector2 b = target.transform.position;
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.25f) return false;
            TeamSide otherSide = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            float radius = Mathf.Max(0.15f, GameConfig.Instance.requestedPassLaneBlockRadius);
            foreach (var opp in TeamManager.Instance.Team(otherSide))
            {
                if (opp == null || opp.IsSuppressed || opp.Role == FieldRole.Goalkeeper) continue;
                Vector2 q = opp.transform.position;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / lenSq);
                if (t <= 0.08f || t >= 0.92f) continue;
                Vector2 closest = a + ab * t;
                if (Vector2.Distance(q, closest) <= radius) return true;
            }
            return false;
        }

        public void ForceImmediateDecision()
        {
            nextDecision = 0f;
            hasSteeringTarget = false;
            steeringSprint = false;
        }

        public void ResetBrain()
        {
            ClearRequest();
            wideCarryUntil = 0f;
            wideCarryY = 0f;
            hadBallLastStep = false;
            possessionCommitUntil = 0f;
            recentPasser = null;
            avoidReturnPassUntil = 0f;
            closeEscapeUntil = 0f;
            closeEscapeY = 0f;
            duelDisengageUntil = 0f;
            duelDisengageTarget = Vector2.zero;
            nextTackleAllowed = 0f;
            kickoffReturnTarget = null;
            kickoffReturnAt = 0f;
            ForceImmediateDecision();
        }

        private void FixedUpdate()
        {
            if (player == null || player.IsHuman || player.IsSuppressed || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing)
                return;

            bool hasBallNow = player.HasBall;
            // Home outfielders never autonomously pass or shoot. If one wins/receives possession,
            // control immediately transfers to that player and the previous human becomes AI.
            if (hasBallNow && player.Side == TeamSide.Home && player.Role != FieldRole.Goalkeeper)
            {
                TeamManager.Instance?.SetHuman(player, false, true);
                return;
            }
            if (hasBallNow && !hadBallLastStep)
            {
                possessionCommitUntil = Time.time + GameConfig.Instance.aiReceiveCommitSeconds;
                var lastKicker = BallControl.Instance != null ? BallControl.Instance.LastKicker : null;
                if (lastKicker != null && lastKicker != player && lastKicker.Side == player.Side)
                {
                    recentPasser = lastKicker;
                    avoidReturnPassUntil = Time.time + GameConfig.Instance.aiReturnPassCooldown;
                }
                else
                {
                    recentPasser = null;
                    avoidReturnPassUntil = 0f;
                }

                // The one exception to the normal no-return-pass rule: the first recipient from a
                // centre kickoff deliberately gives the ball straight back to the original kicker.
                if (RoundReset.Instance != null && RoundReset.Instance.TryGetKickoffReturnTarget(player, out var returnTo))
                {
                    kickoffReturnTarget = returnTo;
                    kickoffReturnAt = Time.time + GameConfig.Instance.kickoffReturnDelay;
                    possessionCommitUntil = Mathf.Min(possessionCommitUntil, kickoffReturnAt);
                    player.ProtectPossession(GameConfig.Instance.kickoffReturnDelay + 0.30f);
                }

                var nearbyOpponent = ClosestOpponent(transform.position, false);
                if (nearbyOpponent != null && Vector2.Distance(transform.position, nearbyOpponent.transform.position) <= GameConfig.Instance.aiCloseContactEscapeDistance)
                    BeginCloseContactEscape(nearbyOpponent);
            }
            else if (!hasBallNow && hadBallLastStep)
            {
                // A carrier who just lost a close duel yields space for a beat instead of instantly
                // becoming the new tackler. This breaks the two-AI tackle/pass/tackle latch loop.
                TeamSide otherSide = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                var newCarrier = TeamManager.Instance != null ? TeamManager.Instance.BallOwner(otherSide) : null;
                if (newCarrier != null)
                {
                    player.LockTackleAfterPossessionLoss(GameConfig.Instance.tackleLockoutAfterLossSeconds);
                    nextTackleAllowed = Mathf.Max(nextTackleAllowed, Time.time + GameConfig.Instance.tackleLockoutAfterLossSeconds);
                    if (Vector2.Distance(transform.position, newCarrier.transform.position) <= GameConfig.Instance.aiCloseContactEscapeDistance * 1.35f)
                        BeginDuelDisengage(newCarrier);
                }
            }
            hadBallLastStep = hasBallNow;

            if (!hasBallNow && requestedAction != RequestedAction.None) ClearRequest();

            if (hasBallNow && kickoffReturnTarget != null)
            {
                // Centre kickoff choreography: receive, settle for a fraction of a second, return
                // the ball to the original kicker exactly once. No other AI decision can interrupt it.
                SetSteering(Vector2.zero, false, false);
                player.SetAIMove(Vector2.zero, false);
                if (Time.time >= kickoffReturnAt)
                {
                    var target = kickoffReturnTarget;
                    kickoffReturnTarget = null;
                    kickoffReturnAt = 0f;
                    if (target != null && target.Side == player.Side)
                    {
                        RoundReset.Instance?.CompleteKickoffReturn(player);
                        player.AIKickTo(target, false);
                    }
                }
                return;
            }

            if (player.Side == TeamSide.Away && player.Character != CharacterType.Generic && player.Ult != null &&
                MatchTimer.Instance != null && MatchTimer.Instance.IsFinalStretch && player.Ult.Charge >= 0.999f &&
                Random.value < GameConfig.Instance.finalStretchAIAutoUltChance * Time.fixedDeltaTime * 3f)
            {
                player.Ult.TryActivateAI();
            }

            if (player.Role == FieldRole.Goalkeeper)
            {
                GoalkeeperThink();
                return;
            }

            // Requested pass/through/shot commands are responsive and do not wait for the slower tactical decision clock.
            if (player.HasBall && ExecuteRequestedAction())
            {
                SetSteering(Vector2.zero, false, false);
                return;
            }

            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + GameConfig.Instance.aiDecisionInterval + Random.Range(-0.035f, 0.035f);
                PlanDecision();
            }

            ApplySteering();
        }

        private void PlanDecision()
        {
            if (player.HasBall) PlanCarryBall();
            else if (TeamHasBall()) PlanSupportAttack();
            else PlanDefend();
        }

        private bool TeamHasBall()
        {
            var ball = BallControl.Instance;
            var owner = ball != null ? ball.Owner : null;
            if (owner != null) return owner.Side == player.Side;
            // A shot/pass in flight is still an attacking phase for a brief grace window. Without
            // this, CBs instantly sprint back toward their own goal the frame a teammate shoots.
            return ball != null && ball.IsRecentKickBy(player.Side, GameConfig.Instance.possessionUiFlightGraceSeconds);
        }

        private void PlanCarryBall()
        {
            int dir = TeamManager.Instance.AttackDirFor(player.Side);
            float goalX = dir * GameConfig.Instance.pitchLength * 0.5f;
            Vector2 goal = new Vector2(goalX, 0f);
            float distGoal = Vector2.Distance(transform.position, goal);
            var nearestDef = ClosestOpponent(transform.position, false);
            float pressure = nearestDef != null ? Vector2.Distance(transform.position, nearestDef.transform.position) : 99f;

            if (nearestDef != null && pressure <= GameConfig.Instance.aiCloseContactEscapeDistance && Time.time >= closeEscapeUntil)
                BeginCloseContactEscape(nearestDef);

            // Break contact loops before making another pass/tackle decision. The carrier commits
            // to a short forward-diagonal escape rather than ping-ponging the ball in a body pile.
            if (Time.time < closeEscapeUntil)
            {
                Vector2 escape = new Vector2(transform.position.x + dir * GameConfig.Instance.aiCarryLookAhead, closeEscapeY);
                SetSteering(ClampTarget(escape), false, true);
                return;
            }

            if (distGoal <= GameConfig.Instance.aiShootDistance)
            {
                SetSteering(Vector2.zero, false, false);
                player.Shoot();
                return;
            }

            // ATT (internally Rover) is a true forward. Once it gets the ball autonomously, its
            // default intent is to advance and score, never recycle backward. It still bends its run
            // wide to avoid a defender rather than beelining down the centre. Explicit human call-pass
            // requests are handled earlier and can still override this.
            if (player.FormationRole == TacticalRole.Rover)
            {
                // Hold a chosen channel for a short spell instead of choosing left/right every AI
                // decision tick. That gives ATT a readable curved/wide run rather than zig-zagging.
                if (Time.time >= wideCarryUntil)
                {
                    float laneSide;
                    if (nearestDef != null && Mathf.Abs(nearestDef.transform.position.y - transform.position.y) < 1.6f)
                        laneSide = nearestDef.transform.position.y >= transform.position.y ? -1f : 1f;
                    else if (Mathf.Abs(transform.position.y) > 0.55f)
                        laneSide = Mathf.Sign(transform.position.y);
                    else
                        laneSide = Mathf.Abs(homeLaneY) > 0.2f ? Mathf.Sign(homeLaneY) : (Random.value < 0.5f ? -1f : 1f);

                    wideCarryY = Mathf.Clamp(laneSide * GameConfig.Instance.aiCarryWideWidth,
                        -GameConfig.Instance.pitchWidth * 0.42f, GameConfig.Instance.pitchWidth * 0.42f);
                    wideCarryUntil = Time.time + Random.Range(GameConfig.Instance.aiCarryWideSecondsMin, GameConfig.Instance.aiCarryWideSecondsMax);
                }

                Vector2 attackTarget = new Vector2(transform.position.x + dir * GameConfig.Instance.aiCarryLookAhead, wideCarryY);
                SetSteering(ClampTarget(attackTarget), pressure < GameConfig.Instance.aiPassPressureDistance * 0.72f, true);
                return;
            }

            bool pressured = pressure <= GameConfig.Instance.aiPassPressureDistance;
            bool settlingAfterReceive = Time.time < possessionCommitUntil;

            // In the defensive third the AI's first job is to EXIT danger. It never looks backward
            // toward its goalkeeper. Prefer a forward/wide outlet; otherwise carry diagonally away
            // from the crowded centre rather than recycling the ball beside its own goal.
            float ownGoalX = -dir * GameConfig.Instance.pitchLength * 0.5f;
            float progressFromOwnGoal = (transform.position.x - ownGoalX) * dir;
            bool inDefensiveThird = progressFromOwnGoal <= GameConfig.Instance.pitchLength * GameConfig.Instance.aiDefensiveThirdFraction;
            if (inDefensiveThird && !settlingAfterReceive)
            {
                var exitTarget = TeamManager.Instance.BestForwardOutlet(player, GameConfig.Instance.aiExitForwardMin, true);
                bool exitPass = exitTarget != null && (pressured || Random.value < GameConfig.Instance.aiDefensiveExitPassChance);
                if (exitPass)
                {
                    wideCarryUntil = 0f;
                    player.AIKickTo(exitTarget, false);
                    return;
                }

                float side = Mathf.Abs(transform.position.y) > 0.45f ? Mathf.Sign(transform.position.y) :
                    (nearestDef != null && nearestDef.transform.position.y >= transform.position.y ? -1f : 1f);
                Vector2 escapeTarget = new Vector2(transform.position.x + dir * GameConfig.Instance.aiCarryLookAhead,
                    side * GameConfig.Instance.aiDefensiveExitWideY);
                SetSteering(ClampTarget(escapeTarget), false, true);
                return;
            }

            var passTarget = BestAITeammate();
            // After receiving, carry the ball for a brief beat instead of instantly ping-ponging it
            // back in a crowded group. Very close danger can still force a decision on the next cycle.
            bool passNow = !settlingAfterReceive && passTarget != null &&
                Random.value < (pressured ? GameConfig.Instance.aiPressurePassChance : GameConfig.Instance.aiOpenPassChance);
            if (passNow)
            {
                bool through = !pressured && Random.value < GameConfig.Instance.aiThroughPassChance;
                wideCarryUntil = 0f;
                player.AIKickTo(passTarget, through);
                return;
            }

            // Carriers do not always take the shortest line to goal. Sometimes they commit to a
            // wide diagonal lane for a short spell, which opens the middle and makes the pitch width matter.
            if (distGoal > GameConfig.Instance.aiShootDistance * 1.20f && Time.time >= wideCarryUntil && Random.value < GameConfig.Instance.aiCarryWideChance)
            {
                float side;
                if (nearestDef != null && Mathf.Abs(nearestDef.transform.position.y) > 0.25f)
                    side = nearestDef.transform.position.y > 0f ? -1f : 1f;
                else if (Mathf.Abs(transform.position.y) > 0.65f)
                    side = Mathf.Sign(transform.position.y);
                else
                    side = Mathf.Abs(homeLaneY) > 0.2f ? Mathf.Sign(homeLaneY) : (Random.value < 0.5f ? -1f : 1f);

                wideCarryY = side * GameConfig.Instance.aiCarryWideWidth;
                wideCarryUntil = Time.time + Random.Range(GameConfig.Instance.aiCarryWideSecondsMin, GameConfig.Instance.aiCarryWideSecondsMax);
            }

            if (Time.time < wideCarryUntil)
            {
                Vector2 wideTarget = new Vector2(transform.position.x + dir * GameConfig.Instance.aiCarryLookAhead, wideCarryY);
                SetSteering(ClampTarget(wideTarget), false, true);
                return;
            }

            Vector2 forward = new Vector2(dir,0f);
            Vector2 avoid = Vector2.zero;
            if (nearestDef != null && pressure < 3.0f)
            {
                Vector2 away = ((Vector2)transform.position - (Vector2)nearestDef.transform.position).normalized;
                float side = Mathf.Sign(Mathf.Abs(away.y) < 0.01f ? homeLaneY + 0.1f : away.y);
                avoid = new Vector2(0f, side) * (1f - Mathf.Clamp01(pressure / 3.0f));
            }

            Vector2 dirMove = (forward + avoid * 0.9f).normalized;
            SetSteering(ClampTarget((Vector2)transform.position + dirMove * GameConfig.Instance.aiCarryLookAhead), false, true);
        }

        private void PlanSupportAttack()
        {
            var owner = TeamManager.Instance.BallOwner(player.Side);
            if (owner == null && BallControl.Instance != null && BallControl.Instance.IsRecentKickBy(player.Side, GameConfig.Instance.possessionUiFlightGraceSeconds))
                owner = BallControl.Instance.LastKicker;
            if (owner == null)
            {
                SetSteering(transform.position, false, true);
                return;
            }

            int dir = TeamManager.Instance.AttackDirFor(player.Side);
            float ownGoalX = -dir * GameConfig.Instance.pitchLength * 0.5f;
            Vector2 target;

            // When our goalkeeper has possession, the three outfielders spread FIRST. The keeper
            // holds safely for a couple of seconds, then distributes to one of these clear outlets.
            if (owner.Role == FieldRole.Goalkeeper)
            {
                float lane = Mathf.Abs(homeLaneY) > 0.2f ? Mathf.Sign(homeLaneY) : 1f;
                switch (player.FormationRole)
                {
                    case TacticalRole.Rover:
                        target = new Vector2(ownGoalX + dir * GameConfig.Instance.keeperOutletForwardDistance, lane * GameConfig.Instance.keeperOutletWideY);
                        break;
                    case TacticalRole.Presser:
                        target = new Vector2(ownGoalX + dir * (GameConfig.Instance.keeperOutletForwardDistance + 1.25f), -lane * (GameConfig.Instance.keeperOutletWideY * 0.72f));
                        break;
                    default:
                        target = new Vector2(ownGoalX + dir * (GameConfig.Instance.keeperOutletForwardDistance * 0.78f), 0f);
                        break;
                }
                SetSteering(ClampTarget(AddSpacing(target)), false, true);
                return;
            }

            switch (player.FormationRole)
            {
                case TacticalRole.Anchor:
                {
                    float minProgress = (-GameConfig.Instance.pitchLength * 0.5f) + GameConfig.Instance.aiAnchorGoalDistance;
                    float maxProgress = (-GameConfig.Instance.pitchLength * 0.5f) + GameConfig.Instance.aiAnchorMaxAdvance;
                    float ownerProgress = owner.transform.position.x * dir;
                    float desiredProgress = Mathf.Clamp(ownerProgress - GameConfig.Instance.aiAttackAnchorBehindBall, minProgress, maxProgress);
                    target = new Vector2(desiredProgress * dir, Mathf.Clamp(owner.transform.position.y * 0.22f, -1.25f, 1.25f));
                    break;
                }
                case TacticalRole.Presser:
                {
                    // If the Rover is carrying the ball, the Presser becomes the wide option so
                    // the attack still has width. Otherwise it offers a more central forward run.
                    if (owner.FormationRole == TacticalRole.Rover)
                    {
                        float laneSign = homeLaneY >= 0f ? -1f : 1f;
                        target = new Vector2(owner.transform.position.x + dir * GameConfig.Instance.aiRoverForward,
                            laneSign * GameConfig.Instance.aiRoverWidth);
                    }
                    else
                    {
                        target = (Vector2)owner.transform.position + new Vector2(dir * GameConfig.Instance.aiAttackPresserForward, 0f);
                        target.y = Mathf.Clamp(owner.transform.position.y * 0.35f, -1.5f, 1.5f);
                    }
                    break;
                }
                default: // Rover is the preferred wide outlet whenever somebody else carries.
                {
                    float laneSign = Mathf.Abs(homeLaneY) > 0.2f ? Mathf.Sign(homeLaneY) : 1f;
                    target = new Vector2(owner.transform.position.x + dir * GameConfig.Instance.aiRoverForward,
                        laneSign * GameConfig.Instance.aiRoverWidth);
                    break;
                }
            }

            target = AddSpacing(target);
            SetSteering(ClampTarget(target), false, true);
        }

        private void PlanDefend()
        {
            TeamSide otherSide = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            var ballOwner = TeamManager.Instance.BallOwner(otherSide);
            Vector2 danger = ballOwner != null ? (Vector2)ballOwner.transform.position : (BallControl.Instance != null ? (Vector2)BallControl.Instance.transform.position : (Vector2)transform.position);
            int dir = TeamManager.Instance.AttackDirFor(player.Side);
            float ownGoalX = -dir * GameConfig.Instance.pitchLength * 0.5f;

            if (player.FormationRole == TacticalRole.Presser)
            {
                if (Time.time < duelDisengageUntil)
                {
                    SetSteering(ClampTarget(duelDisengageTarget), false, true);
                    return;
                }

                Vector2 target = danger;
                bool keeperCarrier = ballOwner != null && ballOwner.Role == FieldRole.Goalkeeper;
                if (ballOwner != null)
                {
                    Vector2 fromGoal = (danger - new Vector2(ownGoalX, 0f)).normalized;
                    float normalStandOff = player.Side == TeamSide.Away
                        ? GameConfig.Instance.aiOpponentPresserStandOff
                        : GameConfig.Instance.aiPresserStandOff;
                    float standOff = keeperCarrier ? Mathf.Max(GameConfig.Instance.keeperNoCrowdRadius, normalStandOff) : normalStandOff;
                    target = danger - fromGoal * standOff;
                }
                float pressDistance = ballOwner != null ? Vector2.Distance(transform.position, ballOwner.transform.position) : 999f;
                bool sprintPress = player.Side == TeamSide.Away && !keeperCarrier && pressDistance > GameConfig.Instance.aiOpponentPressSprintDistance;
                SetSteering(ClampTarget(target), sprintPress, true);

                // Do not create a tackle scrum around a keeper who is already holding the ball.
                // The keeper is required to distribute quickly, so pressure is positional here.
                float tackleRangeMult = player.Side == TeamSide.Away ? GameConfig.Instance.aiOpponentTackleRangeMultiplier : 0.92f;
                if (!keeperCarrier && ballOwner != null && Time.time >= nextTackleAllowed && player.CanAttemptTackle && ballOwner.CanBeTackled && Vector2.Distance(transform.position,ballOwner.transform.position) <= GameConfig.Instance.tackleRange*tackleRangeMult)
                {
                    nextTackleAllowed = Time.time + (player.Side == TeamSide.Away ? GameConfig.Instance.aiOpponentTackleCooldownSeconds : GameConfig.Instance.aiTackleCooldownSeconds);
                    if (player.Character == CharacterType.Generic && Random.value < GameConfig.Instance.aiJockeyChance)
                        player.Defense?.Jockey(player.MoveFacing);
                    else
                        player.Defense?.Tackle();
                }
                return;
            }

            if (player.FormationRole == TacticalRole.Anchor)
            {
                // The Anchor is visibly the last outfield line in front of the goalkeeper.
                float anchorProgress = (-GameConfig.Instance.pitchLength * 0.5f) + GameConfig.Instance.aiAnchorGoalDistance;
                Vector2 target = new Vector2(anchorProgress * dir, Mathf.Clamp(danger.y * 0.28f, -1.45f, 1.45f));
                SetSteering(ClampTarget(AddSpacing(target)), false, true);
                return;
            }

            // Rover holds a true wide lane. It does not become a second ball-chaser.
            float wideProgress = (-GameConfig.Instance.pitchLength * 0.5f) + GameConfig.Instance.aiWideDefensiveDepth;
            float laneSign = Mathf.Abs(homeLaneY) > 0.2f ? Mathf.Sign(homeLaneY) : 1f;
            Vector2 cover = new Vector2(wideProgress * dir, laneSign * GameConfig.Instance.aiRoverWidth);
            SetSteering(ClampTarget(AddSpacing(cover)), false, true);
        }


        private Vector2 AddSpacing(Vector2 desired)
        {
            Vector2 push = Vector2.zero;
            float minSpacing = GameConfig.Instance.aiMinTeammateSpacing;
            foreach (var mate in TeamManager.Instance.Team(player.Side))
            {
                if (mate == null || mate == player || mate.Role == FieldRole.Goalkeeper) continue;
                Vector2 delta = (Vector2)transform.position - (Vector2)mate.transform.position;
                float d = delta.magnitude;
                if (d > 0.01f && d < minSpacing)
                    push += delta.normalized * (1f - d / minSpacing) * 1.2f;
            }
            return desired + push;
        }

        private bool ExecuteRequestedAction()
        {
            if (requestedAction == RequestedAction.None || Time.time < requestedActionAt || !player.HasBall) return false;

            switch (requestedAction)
            {
                case RequestedAction.Pass:
                    if (requestedTarget != null && requestedTarget.Side == player.Side)
                    {
                        player.AIKickTo(requestedTarget, false);
                        ClearRequest();
                        return true;
                    }
                    break;
                case RequestedAction.Through:
                    if (requestedTarget != null && requestedTarget.Side == player.Side)
                    {
                        player.AIKickTo(requestedTarget, true);
                        ClearRequest();
                        return true;
                    }
                    break;
                case RequestedAction.Lob:
                    if (requestedTarget != null && requestedTarget.Side == player.Side)
                    {
                        player.AIKickLobTo(requestedTarget);
                        ClearRequest();
                        return true;
                    }
                    break;
                case RequestedAction.Shoot:
                    player.Shoot();
                    ClearRequest();
                    return true;
            }
            ClearRequest();
            return false;
        }

        private void ClearRequest()
        {
            requestedTarget = null;
            requestedAction = RequestedAction.None;
            requestedActionAt = 0f;
        }

        private void GoalkeeperThink()
        {
            if (player.HasBall)
            {
                ApplyKeeperNoCrowdField();

                int holdDir = TeamManager.Instance.AttackDirFor(player.Side);
                float holdGoalX = -holdDir * GameConfig.Instance.pitchLength * 0.5f;
                Vector2 holdTarget = new Vector2(holdGoalX + holdDir * GameConfig.Instance.keeperHoldCentralDepth, 0f);
                Vector2 holdDelta = holdTarget - (Vector2)transform.position;

                // The keeper is protected but not frozen: during the stall they settle toward a
                // central distribution spot and face upfield, like preparing a throw.
                if (Time.time < player.KeeperHoldUntil)
                {
                    float scale = Mathf.Clamp01(holdDelta.magnitude / 1.2f) * 0.65f;
                    player.SetAIMove(holdDelta.sqrMagnitude > 0.02f ? holdDelta.normalized * scale : Vector2.zero, false);
                    if (holdDelta.sqrMagnitude <= 0.04f) player.SetFacing(new Vector2(holdDir, 0f));
                    return;
                }

                player.SetAIMove(Vector2.zero, false);
                // Critical: do not restart the distribution coroutine every FixedUpdate. Once a keeper
                // begins the throw/lob, wait for that action to complete.
                if (player.IsActionLocked) return;
                if (ExecuteRequestedAction()) return;

                var passTarget = TeamManager.Instance.BestGoalkeeperDistributionTarget(player);
                if (passTarget != null)
                {
                    player.SetFacing((Vector2)passTarget.transform.position - (Vector2)transform.position);
                    // Keeper distribution is a thrown/lobbed outlet rather than a ground pass.
                    player.AIKickLobTo(passTarget);
                    return;
                }
                return;
            }

            var ball = BallControl.Instance;
            int dir = TeamManager.Instance.AttackDirFor(player.Side);
            float ownGoalX = -dir * GameConfig.Instance.pitchLength*0.5f;
            Vector2 ballPos = ball != null ? (Vector2)ball.transform.position : Vector2.zero;
            float ballDistance = ball != null ? Vector2.Distance(transform.position, ballPos) : 999f;

            // A keeper should save opponent/neutral balls, but should not vacuum up a teammate's
            // attempted outlet and accidentally turn it into a back-pass loop.
            if (ball != null && ball.Mode == BallControl.BallMode.Free && ballDistance <= GameConfig.Instance.keeperSaveRadius && KeeperShouldClaim(ball))
            {
                player.GainBall(GameConfig.Instance.keeperPossessionSeconds + 0.45f);
                if (player.HasBall)
                {
                    player.SetKeeperHold(GameConfig.Instance.keeperPossessionSeconds);
                    player.Animation?.Trigger("Save");
                    player.SetAIMove(Vector2.zero, false);
                    return;
                }
            }

            TeamSide other = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            var carrier = TeamManager.Instance.BallOwner(other);
            if (carrier != null && Vector2.Distance(transform.position, carrier.transform.position) <= GameConfig.Instance.keeperChallengeDistance)
            {
                player.Defense?.Tackle();
            }

            float x = ownGoalX + dir*GameConfig.Instance.keeperGoalOffset;
            float track = Mathf.Clamp(ballPos.y, -GameConfig.Instance.keeperLateralRange, GameConfig.Instance.keeperLateralRange);
            Vector2 target = new Vector2(x, track);

            if (ball != null && ball.Mode == BallControl.BallMode.Free && ballDistance <= GameConfig.Instance.keeperEngageDistance && KeeperShouldClaim(ball))
                target = ballPos;

            MoveToward(target, false);
        }

        private bool KeeperShouldClaim(BallControl ball)
        {
            if (ball == null) return false;
            TeamSide opponent = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            bool opponentThreat = (ball.LastKicker != null && ball.LastKicker.Side == opponent) ||
                                  (ball.LastTouch != null && ball.LastTouch.Side == opponent);
            bool neutral = ball.LastKicker == null && ball.LastTouch == null;
            return opponentThreat || neutral;
        }

        private void ApplyKeeperNoCrowdField()
        {
            if (TeamManager.Instance == null || GameConfig.Instance == null || !player.HasBall || player.Role != FieldRole.Goalkeeper) return;
            TeamSide other = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            float radius = Mathf.Max(0.5f, GameConfig.Instance.keeperNoCrowdRadius);
            foreach (var opponent in TeamManager.Instance.Team(other))
            {
                if (opponent == null || opponent.Role == FieldRole.Goalkeeper || opponent.IsSuppressed) continue;
                Vector2 away = (Vector2)opponent.transform.position - (Vector2)player.transform.position;
                float d = away.magnitude;
                if (d >= radius) continue;
                if (d < 0.01f) away = new Vector2(-TeamManager.Instance.AttackDirFor(opponent.Side), opponent.transform.position.y >= 0f ? 0.35f : -0.35f);
                float strength = Mathf.Lerp(GameConfig.Instance.keeperNoCrowdPushSpeed * 0.45f, GameConfig.Instance.keeperNoCrowdPushSpeed, 1f - Mathf.Clamp01(d / radius));
                opponent.Defense?.CancelForControlChange();
                opponent.ApplyKeeperRepulsion(away.normalized * strength);
            }
        }

        private void BeginDuelDisengage(PlayerController carrier)
        {
            if (carrier == null || GameConfig.Instance == null || TeamManager.Instance == null) return;
            int dir = TeamManager.Instance.AttackDirFor(player.Side);
            Vector2 away = (Vector2)transform.position - (Vector2)carrier.transform.position;
            if (away.sqrMagnitude < 0.01f)
                away = new Vector2(-dir, Mathf.Abs(homeLaneY) > 0.1f ? Mathf.Sign(homeLaneY) * 0.55f : 0.55f);
            away.Normalize();
            Vector2 goalSide = new Vector2(-dir, 0f);
            Vector2 retreatDir = (away * 0.65f + goalSide * 0.55f).normalized;
            duelDisengageTarget = ClampTarget((Vector2)transform.position + retreatDir * GameConfig.Instance.aiDuelRetreatDistance);
            duelDisengageUntil = Time.time + GameConfig.Instance.aiDuelDisengageSeconds;
            nextTackleAllowed = Mathf.Max(nextTackleAllowed, duelDisengageUntil);
            closeEscapeUntil = 0f;
        }

        private void BeginCloseContactEscape(PlayerController opponent)
        {
            float awayY = opponent != null ? transform.position.y - opponent.transform.position.y : homeLaneY;
            float side = Mathf.Abs(awayY) > 0.05f ? Mathf.Sign(awayY) : (Mathf.Abs(homeLaneY) > 0.2f ? Mathf.Sign(homeLaneY) : (Random.value < 0.5f ? -1f : 1f));
            closeEscapeY = Mathf.Clamp(transform.position.y + side * GameConfig.Instance.aiCloseContactEscapeWidth,
                -GameConfig.Instance.pitchWidth * 0.42f, GameConfig.Instance.pitchWidth * 0.42f);
            closeEscapeUntil = Time.time + GameConfig.Instance.aiCloseContactEscapeSeconds;
            wideCarryUntil = 0f;
        }

        private PlayerController BestAITeammate()
        {
            Vector2 aim = new Vector2(TeamManager.Instance.AttackDirFor(player.Side),0f);
            PlayerController exclude = Time.time < avoidReturnPassUntil ? recentPasser : null;
            return TeamManager.Instance.BestPassTarget(player, aim, false, exclude, GameConfig.Instance.aiMinPassDistance);
        }

        private PlayerController ClosestOpponent(Vector2 pos, bool includeGk)
        {
            TeamSide other = player.Side==TeamSide.Home?TeamSide.Away:TeamSide.Home;
            PlayerController best=null; float bd=float.MaxValue;
            foreach(var p in TeamManager.Instance.Team(other))
            {
                if(p==null || (!includeGk && p.Role==FieldRole.Goalkeeper)) continue;
                float d=((Vector2)p.transform.position-pos).sqrMagnitude;
                if(d<bd){bd=d;best=p;}
            }
            return best;
        }

        private void SetSteering(Vector2 target, bool sprint, bool hasTarget)
        {
            steeringTarget = target;
            steeringSprint = sprint;
            hasSteeringTarget = hasTarget;
            if (!hasTarget) player.SetAIMove(Vector2.zero, false);
        }

        private void ApplySteering()
        {
            if (!hasSteeringTarget)
            {
                player.SetAIMove(Vector2.zero, false);
                return;
            }
            MoveToward(steeringTarget, steeringSprint);
        }

        private void MoveToward(Vector2 target, bool sprint)
        {
            Vector2 delta=target-(Vector2)transform.position;
            float slowRadius = Mathf.Max(0.15f, GameConfig.Instance.aiSteeringSlowRadius);
            float mag = Mathf.Clamp01(delta.magnitude / slowRadius);
            Vector2 input = delta.sqrMagnitude > 0.0025f ? delta.normalized * mag : Vector2.zero;
            player.SetAIMove(input, sprint && mag > 0.82f);
        }

        private Vector2 ClampTarget(Vector2 p)
        {
            float hx=GameConfig.Instance.pitchLength*0.5f-1.15f;
            float hy=GameConfig.Instance.pitchWidth*0.5f-0.8f;
            return new Vector2(Mathf.Clamp(p.x,-hx,hx),Mathf.Clamp(p.y,-hy,hy));
        }
    }
}
