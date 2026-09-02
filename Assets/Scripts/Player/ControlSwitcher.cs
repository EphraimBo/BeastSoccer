using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Player
{
    public class ControlSwitcher : MonoBehaviour
    {
        public static ControlSwitcher Instance { get; private set; }
        public bool IsKeeperSequence { get; private set; }
        private PlayerController preKeeperHuman;
        private PlayerController special;
        private PlayerController genericKeeper;
        private bool rolesSwapped;
        private Vector2 preSpecialPos;
        private Vector2 preKeeperPos;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            // No automatic switching in normal play. The only automatic takeover left is the
            // intentionally-designed goalkeeper 1v1 sequence in Defending mode.
            if (GameManager.Instance == null || GameManager.Instance.Mode != GameMode.Defending || GameManager.Instance.Phase != MatchPhase.Playing || IsKeeperSequence) return;
            var attacker = TeamManager.Instance.BallOwner(TeamSide.Away);
            if (attacker == null) return;
            var lastDefender = GoalSideOutfieldDefender();
            if (lastDefender == null) return;
            int attackDir = TeamManager.Instance.AttackDirFor(TeamSide.Away);
            bool past = attackDir > 0 ? attacker.transform.position.x > lastDefender.transform.position.x : attacker.transform.position.x < lastDefender.transform.position.x;
            float goalX = attackDir * GameConfig.Instance.pitchLength * 0.5f;
            bool nearGoal = Mathf.Abs(goalX - attacker.transform.position.x) <= GameConfig.Instance.defendingGkTriggerDistance;
            if (past && nearGoal) BeginKeeperSequence();
        }

        public void SwitchToClosestToBall()
        {
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing || IsKeeperSequence || TeamManager.Instance == null) return;

            var current = TeamManager.Instance.CurrentHuman();

            // FIX37: while defending, SWITCH uses a deliberate three-player cycle instead of
            // always jumping to the nearest defender:
            //   Special -> closest-to-ball -> remaining outfielder -> Special.
            // If control is currently on some unexpected player, the first press returns to Special.
            bool homeHasPossession = GameManager.Instance.Possession == Possession.Home;
            if (!homeHasPossession)
            {
                var target = DefensiveSwitchCycleTarget(current);
                if (target != null) TeamManager.Instance.SetHuman(target);
                return;
            }

            // Keep the existing manual-switch behavior when our team actually owns the ball.
            var normalTarget = TeamManager.Instance.BestManualSwitchTarget(current);
            if (normalTarget != null) TeamManager.Instance.SetHuman(normalTarget);
        }

        private PlayerController DefensiveSwitchCycleTarget(PlayerController current)
        {
            if (TeamManager.Instance == null) return null;

            var specialPlayer = TeamManager.Instance.SpecialFor(TeamSide.Home);
            if (!IsValidOutfieldSwitchTarget(specialPlayer)) specialPlayer = null;

            // The closest stage deliberately ignores the special so pressing SWITCH from the
            // special character always advances to a different defender.
            PlayerController closest = null;
            float closestSq = float.MaxValue;
            Vector2 ballPos = TeamManager.Instance.Ball != null
                ? (Vector2)TeamManager.Instance.Ball.position
                : Vector2.zero;

            foreach (var p in TeamManager.Instance.HomeTeam)
            {
                if (!IsValidOutfieldSwitchTarget(p) || p == specialPlayer) continue;
                float d = TeamManager.Instance.Ball != null
                    ? ((Vector2)p.transform.position - ballPos).sqrMagnitude
                    : 0f;
                if (closest == null || d < closestSq)
                {
                    closest = p;
                    closestSq = d;
                }
            }

            // With 3 outfielders this is the one player that is neither Special nor Closest.
            PlayerController remaining = null;
            foreach (var p in TeamManager.Instance.HomeTeam)
            {
                if (!IsValidOutfieldSwitchTarget(p) || p == specialPlayer || p == closest) continue;
                remaining = p;
                break;
            }

            // Primary/default defensive selection is always the chosen special character.
            if (current == null || current == specialPlayer ||
                (current != closest && current != remaining))
            {
                if (current == specialPlayer)
                    return closest != null ? closest : (remaining != null ? remaining : specialPlayer);
                return specialPlayer != null ? specialPlayer : (closest != null ? closest : remaining);
            }

            if (current == closest)
                return remaining != null ? remaining : (specialPlayer != null ? specialPlayer : closest);

            // current == remaining
            return specialPlayer != null ? specialPlayer : (closest != null ? closest : remaining);
        }

        private static bool IsValidOutfieldSwitchTarget(PlayerController p)
        {
            return p != null && p.Side == TeamSide.Home && p.Role != FieldRole.Goalkeeper && !p.IsSuppressed;
        }

        private void BeginKeeperSequence()
        {
            IsKeeperSequence = true;
            preKeeperHuman = TeamManager.Instance.CurrentHuman();
            special = TeamManager.Instance.SpecialFor(TeamSide.Home);
            genericKeeper = TeamManager.Instance.GoalkeeperFor(TeamSide.Home);
            if (special == null || genericKeeper == null) { IsKeeperSequence = false; return; }

            preSpecialPos = special.transform.position;
            preKeeperPos = genericKeeper.transform.position;
            special.TeleportTo(preKeeperPos);
            genericKeeper.TeleportTo(preSpecialPos);
            special.Role = FieldRole.Goalkeeper;
            special.FormationRole = TacticalRole.Goalkeeper;
            genericKeeper.Role = FieldRole.Outfield;
            genericKeeper.FormationRole = TacticalRole.Anchor;
            rolesSwapped = true;
            TeamManager.Instance.SetHuman(special, true);

            foreach (var p in TeamManager.Instance.HomeTeam)
            {
                if (p == null || p == special) continue;
                p.SetExternalSpeedMultiplier(GameConfig.Instance.fadedDefenderSpeedMultiplier);
                p.SetSuppressed(true);
                p.Visual?.SetAlpha(GameConfig.Instance.fadedDefenderAlpha);
            }
        }

        public void RestoreNormalControl()
        {
            bool hadKeeperSequence = rolesSwapped || preKeeperHuman != null;
            IsKeeperSequence = false;
            if (rolesSwapped && special != null && genericKeeper != null)
            {
                special.Role = FieldRole.Outfield;
                special.FormationRole = TacticalRole.Rover;
                genericKeeper.Role = FieldRole.Goalkeeper;
                genericKeeper.FormationRole = TacticalRole.Goalkeeper;
                special.TeleportTo(preSpecialPos);
                genericKeeper.TeleportTo(preKeeperPos);
            }
            rolesSwapped = false;

            if (TeamManager.Instance != null)
            {
                foreach (var p in TeamManager.Instance.HomeTeam)
                {
                    if (p == null) continue;
                    p.SetExternalSpeedMultiplier(1f);
                    p.SetSuppressed(false);
                    p.Visual?.SetAlpha(1f);
                }
                if (hadKeeperSequence)
                {
                    var selected = TeamManager.Instance.SpecialFor(TeamSide.Home);
                    TeamManager.Instance.SetHuman(preKeeperHuman != null && preKeeperHuman.Role != FieldRole.Goalkeeper ? preKeeperHuman : selected);
                }
            }
            preKeeperHuman = null;
            special = null;
            genericKeeper = null;
        }

        private PlayerController GoalSideOutfieldDefender()
        {
            int attackDir = TeamManager.Instance.AttackDirFor(TeamSide.Away);
            PlayerController best = null;
            float bestProgress = float.MinValue;
            foreach (var p in TeamManager.Instance.HomeTeam)
            {
                if (p == null || p.Role == FieldRole.Goalkeeper) continue;
                float progressTowardGoal = p.transform.position.x * attackDir;
                if (progressTowardGoal > bestProgress) { bestProgress = progressTowardGoal; best = p; }
            }
            return best;
        }
    }
}
