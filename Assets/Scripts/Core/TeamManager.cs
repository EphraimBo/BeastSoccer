using System.Collections.Generic;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Player;
using BeastSoccer.Ball;
using BeastSoccer.AI;
using BeastSoccer.CameraSystem;

namespace BeastSoccer.Core
{
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        public List<PlayerController> HomeTeam = new List<PlayerController>();
        public List<PlayerController> AwayTeam = new List<PlayerController>();
        public PlayerController HumanPlayer { get; private set; }
        public Transform Ball;

        public int HomeAttackDir { get; private set; } = 1;
        public int AwayAttackDir { get; private set; } = -1;

        public IEnumerable<PlayerController> AllPlayers
        {
            get { foreach (var p in HomeTeam) yield return p; foreach (var p in AwayTeam) yield return p; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ApplyCharacterSelections(CharacterType homeSpecial, CharacterType awaySpecial)
        {
            var hs = SpecialFor(TeamSide.Home);
            var aspc = SpecialFor(TeamSide.Away);
            if (hs != null) hs.ConfigureCharacter(homeSpecial);
            if (aspc != null) aspc.ConfigureCharacter(awaySpecial);
            AssignGenericNumbers(HomeTeam);
            AssignGenericNumbers(AwayTeam);
            EnsureTacticalRoles(HomeTeam, TeamSide.Home);
            EnsureTacticalRoles(AwayTeam, TeamSide.Away);
            ConfigureFriendlyBodyCollisions(HomeTeam);
            ConfigureFriendlyBodyCollisions(AwayTeam);
            ConfigureBallPlayerBodyCollisions();
            SetHuman(hs);
        }


        private void ConfigureFriendlyBodyCollisions(List<PlayerController> team)
        {
            // Teammates should form around one another, not become a physics pile. Opponents
            // still collide normally; tackles/intercepts use explicit gameplay queries.
            for (int i = 0; i < team.Count; i++)
            {
                var a = team[i] != null ? team[i].GetComponent<CapsuleCollider2D>() : null;
                if (a == null) continue;
                for (int j = i + 1; j < team.Count; j++)
                {
                    var b = team[j] != null ? team[j].GetComponent<CapsuleCollider2D>() : null;
                    if (b != null) Physics2D.IgnoreCollision(a, b, true);
                }
            }
        }


        public void ConfigureBallPlayerBodyCollisions()
        {
            // The gameplay ball must never physically shove a player. Receiving, tackling,
            // interceptions and goalkeeper saves are handled explicitly by gameplay logic.
            // Leaving rigidbody collisions enabled caused both passer recoil and high-speed
            // receiver launches when a fast pass contacted a capsule before auto-receive.
            var ball = BallControl.Instance;
            if (ball == null) return;
            var ballCollider = ball.GetComponent<CircleCollider2D>();
            if (ballCollider == null) return;

            foreach (var p in AllPlayers)
            {
                if (p == null) continue;
                var body = p.GetComponent<CapsuleCollider2D>();
                if (body != null) Physics2D.IgnoreCollision(ballCollider, body, true);
            }
        }

        private void AssignGenericNumbers(List<PlayerController> team)
        {
            foreach (var p in team)
            {
                if (p == null || p.Character != CharacterType.Generic) continue;
                p.ShirtNumber = p.Role == FieldRole.Goalkeeper ? 1 : Random.Range(2, 100);
            }
        }

        private void EnsureTacticalRoles(List<PlayerController> team, TeamSide side)
        {
            PlayerController special = null;
            var generics = new List<PlayerController>();
            foreach (var p in team)
            {
                if (p == null) continue;
                if (p.Role == FieldRole.Goalkeeper)
                {
                    p.FormationRole = TacticalRole.Goalkeeper;
                    continue;
                }
                if (p.Character != CharacterType.Generic) special = p;
                else generics.Add(p);
            }

            // Home keeps the selected special as the ATT/Rover by default. The opponent special
            // is always the pressing MID so the featured opponent is visibly involved in defence.
            if (side == TeamSide.Away)
            {
                if (special != null) special.FormationRole = TacticalRole.Presser;
                if (generics.Count > 0) generics[0].FormationRole = TacticalRole.Rover;
                if (generics.Count > 1) generics[1].FormationRole = TacticalRole.Anchor;
            }
            else
            {
                if (special != null) special.FormationRole = TacticalRole.Rover;
                if (generics.Count > 0) generics[0].FormationRole = TacticalRole.Presser;
                if (generics.Count > 1) generics[1].FormationRole = TacticalRole.Anchor;
            }
        }

        public void SetHuman(PlayerController target, bool allowGoalkeeper = false, bool preserveTargetMomentum = false)
        {
            if (target == null || target.Side != TeamSide.Home) return;
            if (!allowGoalkeeper && target.Role == FieldRole.Goalkeeper) return;

            var outgoing = CurrentHuman();
            if (outgoing == target)
            {
                target.SetHumanControlled(true, preserveTargetMomentum);
                HumanPlayer = target;
                var sameCam = Camera.main;
                if (sameCam != null) sameCam.GetComponent<FollowCamera>()?.SnapToCurrentHuman();
                return;
            }

            if (outgoing != null)
            {
                outgoing.SetHumanControlled(false);
                outgoing.GetComponent<AIController>()?.ForceImmediateDecision();
            }

            foreach (var p in HomeTeam)
            {
                if (p == null || p == outgoing || p == target) continue;
                if (p.IsHuman) p.SetHumanControlled(false);
            }

            target.SetHumanControlled(true, preserveTargetMomentum);
            HumanPlayer = target;
            var cam = Camera.main;
            if (cam != null) cam.GetComponent<FollowCamera>()?.SnapToCurrentHuman();
        }

        public PlayerController CurrentHuman()
        {
            foreach (var p in HomeTeam) if (p != null && p.IsHuman) return p;
            return HumanPlayer;
        }

        public PlayerController SpecialFor(TeamSide side)
        {
            var team = side == TeamSide.Home ? HomeTeam : AwayTeam;
            foreach (var p in team) if (p != null && p.Character != CharacterType.Generic) return p;
            return null;
        }

        public PlayerController GoalkeeperFor(TeamSide side)
        {
            var team = side == TeamSide.Home ? HomeTeam : AwayTeam;
            foreach (var p in team) if (p != null && p.Role == FieldRole.Goalkeeper) return p;
            return null;
        }

        public PlayerController RoleFor(TeamSide side, TacticalRole role)
        {
            foreach (var p in Team(side)) if (p != null && p.FormationRole == role) return p;
            return null;
        }

        public PlayerController BallOwner(TeamSide side)
        {
            var team = side == TeamSide.Home ? HomeTeam : AwayTeam;
            foreach (var p in team) if (p != null && p.HasBall) return p;
            return null;
        }

        public List<PlayerController> Team(TeamSide side) => side == TeamSide.Home ? HomeTeam : AwayTeam;
        public int AttackDirFor(TeamSide side) => side == TeamSide.Home ? HomeAttackDir : AwayAttackDir;
        public void SwapAttackingDirections() { HomeAttackDir *= -1; AwayAttackDir *= -1; }

        public PlayerController ClosestToBall(TeamSide side, bool includeGoalkeeper, PlayerController exclude = null)
        {
            if (Ball == null) return null;
            PlayerController best = null; float bestSq = float.MaxValue;
            foreach (var p in Team(side))
            {
                if (p == null || p == exclude || (!includeGoalkeeper && p.Role == FieldRole.Goalkeeper)) continue;
                float d = ((Vector2)p.transform.position - (Vector2)Ball.position).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            return best;
        }

        public PlayerController BestManualSwitchTarget(PlayerController current)
        {
            if (Ball == null) return null;
            Vector2 ballPos = Ball.position;
            int dir = AttackDirFor(TeamSide.Home);
            float ballProgress = ballPos.x * dir;
            PlayerController best = null;
            float bestScore = float.MaxValue;

            foreach (var p in HomeTeam)
            {
                if (p == null || p == current || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                float distance = Vector2.Distance(p.transform.position, ballPos);
                float progress = p.transform.position.x * dir;
                bool goalSide = progress <= ballProgress + 0.25f;
                float score = distance;
                if (!goalSide) score += 1.35f;
                if (p.FormationRole == TacticalRole.Presser) score -= 0.30f;
                if (p.FormationRole == TacticalRole.Anchor && distance > 3.2f) score += 0.25f;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public PlayerController BestPassTarget(PlayerController from, Vector2 aimDir, bool through, PlayerController exclude = null, float minDistance = 0f)
        {
            var candidates = Team(from.Side);
            PlayerController best = null;
            float bestScore = float.MinValue;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = new Vector2(AttackDirFor(from.Side), 0f);
            aimDir.Normalize();
            foreach (var p in candidates)
            {
                if (p == null || p == from || p == exclude || p.Role == FieldRole.Goalkeeper) continue;
                Vector2 delta = (Vector2)p.transform.position - (Vector2)from.transform.position;
                float dist = delta.magnitude;
                if (dist < Mathf.Max(0.1f, minDistance)) continue;
                float angleScore = Vector2.Dot(aimDir, delta / dist);
                float forwardScore = Mathf.Clamp01(Vector2.Dot(new Vector2(AttackDirFor(from.Side), 0f), delta / dist) * 0.5f + 0.5f);
                float spacingBonus = Mathf.Clamp01(dist / 7f);
                float roleBonus = p.FormationRole == TacticalRole.Rover ? 0.20f : p.FormationRole == TacticalRole.Presser ? 0.10f : 0f;
                float score = angleScore * 2f + forwardScore + spacingBonus * (through ? 0.9f : 0.25f) + roleBonus;
                float assistCone = GameConfig.Instance.passAssistConeDegrees + (from.Character == CharacterType.Leo ? GameConfig.Instance.leoPassAssistConeBonusDegrees : 0f);
                if (angleScore < Mathf.Cos(assistCone * Mathf.Deg2Rad * 0.5f)) score -= 1.0f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }



        public PlayerController BestDirectionalTarget(PlayerController from, Vector2 aimDir, float maxAngleDegrees, float minDistance = 0.6f)
        {
            if (from == null) return null;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = new Vector2(AttackDirFor(from.Side), 0f);
            aimDir.Normalize();
            float minDot = Mathf.Cos(Mathf.Clamp(maxAngleDegrees, 5f, 175f) * Mathf.Deg2Rad * 0.5f);
            float maxDistance = GameConfig.Instance != null ? Mathf.Max(2f, GameConfig.Instance.directionalPassMaxDistance) : 12.5f;
            PlayerController best = null;
            float bestScore = float.MinValue;
            foreach (var p in Team(from.Side))
            {
                if (p == null || p == from || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                Vector2 delta = (Vector2)p.transform.position - (Vector2)from.transform.position;
                float dist = delta.magnitude;
                if (dist < Mathf.Max(0.1f, minDistance) || dist > maxDistance) continue;
                Vector2 dir = delta / Mathf.Max(0.01f, dist);
                float dot = Vector2.Dot(aimDir, dir);
                if (dot < minDot) continue;

                // User intent is directional first. A teammate closest to the stick/WASD direction
                // wins; distance is only a mild tie-break so the game does not unexpectedly select
                // a different lane simply because that player is farther upfield.
                float anglePriority = dot * 8.0f;
                float distancePenalty = Mathf.Clamp01(dist / maxDistance) * 0.35f;
                float score = anglePriority - distancePenalty;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public PlayerController ClosestTeammateToAim(PlayerController from, Vector2 aimDir, float minDistance = 0.6f)
        {
            if (from == null) return null;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = new Vector2(AttackDirFor(from.Side), 0f);
            aimDir.Normalize();
            PlayerController best = null;
            float bestScore = float.MinValue;
            float maxDistance = GameConfig.Instance != null ? Mathf.Max(2f, GameConfig.Instance.directionalPassMaxDistance) : 12.5f;
            foreach (var p in Team(from.Side))
            {
                if (p == null || p == from || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                Vector2 delta = (Vector2)p.transform.position - (Vector2)from.transform.position;
                float dist = delta.magnitude;
                if (dist < Mathf.Max(0.1f, minDistance) || dist > maxDistance) continue;
                float dot = Vector2.Dot(aimDir, delta / Mathf.Max(0.01f, dist));
                // Direction dominates. Distance only breaks close angular ties. Unlike
                // BestDirectionalTarget this never returns null simply because the user is a little
                // outside a cone, which is what we want for locked crosses/throw-ins.
                float score = dot * 10f - Mathf.Clamp01(dist / maxDistance) * 0.25f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public void AdjustHumanForOpponentPossession(Vector2 ballPosition)
        {
            var current = CurrentHuman();
            PlayerController closest = null;
            float closestSq = float.MaxValue;
            foreach (var p in HomeTeam)
            {
                if (p == null || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                float sq = ((Vector2)p.transform.position - ballPosition).sqrMagnitude;
                if (sq < closestSq) { closestSq = sq; closest = p; }
            }
            if (closest == null) return;
            if (current == null || current.Role == FieldRole.Goalkeeper || current.IsSuppressed)
            {
                SetHuman(closest, false, true);
                return;
            }

            float currentSq = ((Vector2)current.transform.position - ballPosition).sqrMagnitude;
            // Stay on the player who just lost it unless another outfielder is actually closer.
            // Tiny sub-frame differences are ignored to prevent control flicker.
            const float advantage = 0.18f;
            if (closest != current && Mathf.Sqrt(closestSq) + advantage < Mathf.Sqrt(currentSq))
                SetHuman(closest, false, true);
        }

        public PlayerController BestKickoffTarget(PlayerController from)
        {
            if (from == null) return null;
            int dir = AttackDirFor(from.Side);
            PlayerController best = null;
            float bestScore = float.MinValue;
            foreach (var p in Team(from.Side))
            {
                if (p == null || p == from || p.Role == FieldRole.Goalkeeper) continue;
                Vector2 delta = (Vector2)p.transform.position - (Vector2)from.transform.position;
                float dist = Mathf.Max(0.01f, delta.magnitude);
                float forward = delta.x * dir;
                float wide = Mathf.Abs(p.transform.position.y);
                float roleBonus = p.FormationRole == TacticalRole.Rover ? 1.0f : p.FormationRole == TacticalRole.Presser ? 0.45f : 0f;
                float score = forward * 0.85f + wide * 0.30f + dist * 0.08f + roleBonus;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public PlayerController BestGoalkeeperDistributionTarget(PlayerController keeper)
        {
            if (keeper == null) return null;
            int dir = AttackDirFor(keeper.Side);
            PlayerController best = null;
            float bestScore = float.MinValue;
            float halfWidth = GameConfig.Instance != null ? GameConfig.Instance.pitchWidth * 0.5f : 7f;
            float minDistance = GameConfig.Instance != null ? GameConfig.Instance.keeperDistributionMinDistance : 3.0f;

            foreach (var p in Team(keeper.Side))
            {
                if (p == null || p == keeper || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                Vector2 delta = (Vector2)p.transform.position - (Vector2)keeper.transform.position;
                float dist = delta.magnitude;
                if (dist < minDistance) continue;

                float forwardProgress = delta.x * dir;
                float width01 = Mathf.Clamp01(Mathf.Abs(p.transform.position.y) / Mathf.Max(0.1f, halfWidth));
                float roleBonus = p.FormationRole == TacticalRole.Rover ? 2.2f : p.FormationRole == TacticalRole.Presser ? 0.8f : 0f;
                // Keepers should clear danger: strongly prefer forward, wide, distant outlets.
                float score = forwardProgress * 1.25f + width01 * 3.1f + dist * 0.18f + roleBonus;
                if (score > bestScore) { bestScore = score; best = p; }
            }

            if (best != null) return best;

            // Fallback: there should always be three outfield players, but if they are all too close,
            // choose the farthest legal outfielder rather than holding the ball forever.
            float farthest = float.MinValue;
            foreach (var p in Team(keeper.Side))
            {
                if (p == null || p == keeper || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                float dist = Vector2.Distance(p.transform.position, keeper.transform.position);
                if (dist > farthest) { farthest = dist; best = p; }
            }
            return best;
        }

        public PlayerController BestForwardOutlet(PlayerController from, float minForwardProgress, bool preferWide)
        {
            if (from == null) return null;
            int dir = AttackDirFor(from.Side);
            PlayerController best = null;
            float bestScore = float.MinValue;
            foreach (var p in Team(from.Side))
            {
                if (p == null || p == from || p.Role == FieldRole.Goalkeeper || p.IsSuppressed) continue;
                Vector2 delta = (Vector2)p.transform.position - (Vector2)from.transform.position;
                float progress = delta.x * dir;
                if (progress < minForwardProgress) continue;
                float dist = delta.magnitude;
                float wide = Mathf.Abs(p.transform.position.y);
                float roleBonus = p.FormationRole == TacticalRole.Rover ? 0.9f : 0f;
                float score = progress * 1.4f + dist * 0.12f + (preferWide ? wide * 0.45f : wide * 0.10f) + roleBonus;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public void AddTeamUltCharge(TeamSide side, float amount)
        {
            var special = SpecialFor(side);
            special?.Ult?.AddCharge(amount);
        }

        public void ClearTransientPlayerState()
        {
            foreach (var p in AllPlayers)
            {
                if (p == null) continue;
                p.Ult?.CancelForRestart();
                p.ResetForRestart();
            }
        }

        public void StopAllPlayers()
        {
            foreach (var p in AllPlayers) if (p != null) p.StopImmediately();
        }
    }
}
