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
            var target = TeamManager.Instance.BestManualSwitchTarget(current);
            if (target != null) TeamManager.Instance.SetHuman(target);
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
