using System.Collections.Generic;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Player;
using BeastSoccer.Ball;
using BeastSoccer.AI;
using BeastSoccer.CameraSystem;

namespace BeastSoccer.Core
{
    public class RoundReset : MonoBehaviour
    {
        public static RoundReset Instance { get; private set; }
        public Transform ballSpawn;
        public Transform[] homeSpawns = new Transform[4];
        public Transform[] awaySpawns = new Transform[4];

        private readonly Dictionary<PlayerController, Vector2> kickoffLocks = new Dictionary<PlayerController, Vector2>();
        private bool holdKickoffShape;
        private PlayerController kickoffKicker;
        private TeamSide kickoffSide;
        private PlayerController kickoffPlannedTarget;
        private PlayerController kickoffReturnKicker;
        private PlayerController kickoffReturnReceiver;
        private bool kickoffReturnPending;

        private PlayerController preparedSetPieceTaker;
        private PlayerController preparedSetPieceTarget;
        private SetPieceType preparedSetPieceType = SetPieceType.None;
        private TeamSide preparedSetPieceSide;

        public PlayerController KickoffKicker => kickoffKicker;
        public PlayerController PreparedSetPieceTaker => preparedSetPieceTaker;
        public TeamSide KickoffSide => kickoffSide;

        public PlayerController GetKickoffPassTarget(PlayerController kicker)
        {
            if (kicker == null || kicker != kickoffKicker) return null;
            if (kickoffPlannedTarget == null || kickoffPlannedTarget.Side != kicker.Side || kickoffPlannedTarget.Role == FieldRole.Goalkeeper)
                kickoffPlannedTarget = TeamManager.Instance != null ? TeamManager.Instance.BestKickoffTarget(kicker) : null;
            return kickoffPlannedTarget;
        }

        public void ArmKickoffReturn(PlayerController kicker)
        {
            if (kicker == null || kicker != kickoffKicker) return;
            var receiver = GetKickoffPassTarget(kicker);
            if (receiver == null || receiver == kicker) return;
            kickoffReturnKicker = kicker;
            kickoffReturnReceiver = receiver;
            kickoffReturnPending = true;
        }

        public bool TryGetKickoffReturnTarget(PlayerController receiver, out PlayerController returnTarget)
        {
            returnTarget = null;
            if (!kickoffReturnPending || receiver == null || receiver != kickoffReturnReceiver || kickoffReturnKicker == null) return false;
            if (receiver.Side != kickoffReturnKicker.Side) { ClearKickoffReturn(); return false; }
            returnTarget = kickoffReturnKicker;
            return true;
        }

        public void CompleteKickoffReturn(PlayerController receiver)
        {
            if (kickoffReturnPending && receiver == kickoffReturnReceiver) ClearKickoffReturn();
        }

        private void ClearKickoffReturn()
        {
            kickoffReturnPending = false;
            kickoffReturnKicker = null;
            kickoffReturnReceiver = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void FixedUpdate()
        {
            if (!holdKickoffShape || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Kickoff)
                return;

            // The whole restart formation is authoritative until the kickoff pass actually leaves
            // the kicker's foot. No movement input, AI steering, or physics contact can shift it.
            ApplyKickoffLocks();
        }

        public bool IsKickoffKicker(PlayerController player) => player != null && player == kickoffKicker;

        public void ResetForKickoff(TeamSide side)
        {
            kickoffSide = side;
            ResetCommon();
            kickoffSide = side;
            BuildKickoffLocks(side);
            ApplyKickoffLocks();
            kickoffPlannedTarget = kickoffKicker != null && TeamManager.Instance != null ? TeamManager.Instance.BestKickoffTarget(kickoffKicker) : null;

            PlaceBallAtCenter();
            if (kickoffKicker != null)
                BallControl.Instance?.SetOwner(kickoffKicker, true);

            var homeSpecial = TeamManager.Instance.SpecialFor(TeamSide.Home);
            if (homeSpecial != null) TeamManager.Instance.SetHuman(homeSpecial);

            holdKickoffShape = true;
            Physics2D.SyncTransforms();
            SnapCameraToHuman();
        }

        public void ResetDefendingRound()
        {
            kickoffSide = TeamSide.Away;
            ResetCommon();
            kickoffSide = TeamSide.Away;
            BuildDefendingLocks();
            ApplyKickoffLocks();
            PlaceBallAtCenter();

            var attacker = TeamManager.Instance.SpecialFor(TeamSide.Away);
            kickoffKicker = attacker;
            if (attacker != null)
            {
                int dir = TeamManager.Instance.AttackDirFor(attacker.Side);
                attacker.SetFacing(new Vector2(dir, 0f));
                BallControl.Instance?.SetOwner(attacker, true);
            }

            var special = TeamManager.Instance.SpecialFor(TeamSide.Home);
            if (special != null) TeamManager.Instance.SetHuman(special);

            holdKickoffShape = true;
            Physics2D.SyncTransforms();
            SnapCameraToHuman();
        }

        public void OnKickoffReady()
        {
            if (!holdKickoffShape || kickoffKicker == null || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Kickoff) return;

            // Human home kickoffs wait for the user's PASS input. Away kickoffs are taken by AI
            // automatically, but are still real passes: the clock and formation release happen
            // only when BallControl.Kick reports that the pass has actually left the foot.
            if (kickoffKicker.Side == TeamSide.Away)
            {
                var target = GetKickoffPassTarget(kickoffKicker);
                if (target != null) kickoffKicker.AIKickTo(target, false);
            }
        }

        public void RestartNear(Vector2 spot, TeamSide restartSide)
        {
            holdKickoffShape = false;
            kickoffLocks.Clear();
            kickoffKicker = null;
            kickoffPlannedTarget = null;
            ClearKickoffReturn();
            ClearPreparedSetPiece();

            BallControl.Instance?.ForceReleaseAndStop();
            GameManager.Instance?.SetPossession(Possession.Loose);
            var receiver = NearestEligible(restartSide, spot);
            if (receiver == null) return;
            receiver.TeleportTo(ClampToPitch(spot + new Vector2(-TeamManager.Instance.AttackDirFor(restartSide) * 0.45f, 0f)));
            receiver.SetFacing(new Vector2(TeamManager.Instance.AttackDirFor(restartSide), 0f));
            BallControl.Instance?.TeleportFree(ClampToPitch(spot));
            BallControl.Instance?.SetOwner(receiver, true);
        }

        public PlayerController PrepareSetPiece(SetPieceType type, TeamSide restartSide, Vector2 spot)
        {
            holdKickoffShape = false;
            kickoffLocks.Clear();
            kickoffKicker = null;
            kickoffPlannedTarget = null;
            ClearKickoffReturn();
            ClearPreparedSetPiece();

            TeamManager.Instance?.StopAllPlayers();
            BallControl.Instance?.ForceReleaseAndStop();

            preparedSetPieceType = type;
            preparedSetPieceSide = restartSide;

            if (type == SetPieceType.GoalKick)
                preparedSetPieceTaker = TeamManager.Instance != null ? TeamManager.Instance.GoalkeeperFor(restartSide) : null;
            else
                preparedSetPieceTaker = NearestEligible(restartSide, spot);

            if (preparedSetPieceTaker == null) return null;

            Vector2 takerPos = ClampToPitch(spot);
            if (type == SetPieceType.ThrowIn)
            {
                float hy = GameConfig.Instance.pitchWidth * 0.5f - GameConfig.Instance.pitchPlayerPadding;
                takerPos.y = Mathf.Sign(spot.y == 0f ? 1f : spot.y) * hy;
            }

            preparedSetPieceTaker.TeleportTo(takerPos);
            preparedSetPieceTaker.SetFacing(new Vector2(TeamManager.Instance.AttackDirFor(restartSide), 0f));
            BallControl.Instance?.TeleportFree(ClampToPitch(spot));
            BallControl.Instance?.SetOwner(preparedSetPieceTaker, true);

            if (type == SetPieceType.GoalKick)
                preparedSetPieceTarget = TeamManager.Instance.BestGoalkeeperDistributionTarget(preparedSetPieceTaker);
            else
                preparedSetPieceTarget = TeamManager.Instance.BestForwardOutlet(preparedSetPieceTaker, -0.2f, true);

            if (preparedSetPieceTarget == null)
                preparedSetPieceTarget = TeamManager.Instance.BestPassTarget(
                    preparedSetPieceTaker,
                    new Vector2(TeamManager.Instance.AttackDirFor(restartSide), 0f),
                    false,
                    null,
                    1.0f);

            FacePlayersForSetPiece(restartSide, spot, preparedSetPieceTaker, preparedSetPieceTarget);
            Physics2D.SyncTransforms();
            return preparedSetPieceTaker;
        }


        private void FacePlayersForSetPiece(TeamSide restartSide, Vector2 spot, PlayerController taker, PlayerController target)
        {
            if (TeamManager.Instance == null) return;
            foreach (var p in TeamManager.Instance.AllPlayers)
            {
                if (p == null) continue;
                Vector2 facing;
                if (p.Role == FieldRole.Goalkeeper)
                {
                    facing = new Vector2(TeamManager.Instance.AttackDirFor(p.Side), 0f);
                }
                else if (p == taker && target != null)
                {
                    facing = (Vector2)target.transform.position - (Vector2)p.transform.position;
                }
                else
                {
                    // Everyone else visibly resets attention toward the dead-ball location.
                    // Once play restarts, AI/human movement naturally updates facing again.
                    facing = spot - (Vector2)p.transform.position;
                    if (facing.sqrMagnitude < 0.04f)
                        facing = new Vector2(TeamManager.Instance.AttackDirFor(p.Side), 0f);
                }
                p.SetFacing(facing);
            }
        }

        public bool ExecutePreparedSetPiece()
        {
            if (preparedSetPieceTaker == null || !preparedSetPieceTaker.HasBall) return false;

            if (preparedSetPieceTarget == null)
                preparedSetPieceTarget = TeamManager.Instance.BestForwardOutlet(preparedSetPieceTaker, -0.2f, true);
            if (preparedSetPieceTarget == null) return false;

            // Throw-ins, goal kicks and corners use the lob/throw flight model so they are
            // visually distinct from ordinary ground passes and cannot be instantly vacuumed.
            preparedSetPieceTaker.AIKickLobTo(preparedSetPieceTarget);
            return true;
        }

        public void ClearPreparedSetPiece()
        {
            preparedSetPieceTaker = null;
            preparedSetPieceTarget = null;
            preparedSetPieceType = SetPieceType.None;
        }

        private void ResetCommon()
        {
            holdKickoffShape = false;
            kickoffLocks.Clear();
            kickoffKicker = null;
            kickoffPlannedTarget = null;
            ClearKickoffReturn();
            ClearPreparedSetPiece();

            BallControl.Instance?.ForceReleaseAndStop();
            ControlSwitcher.Instance?.RestoreNormalControl();
            TeamManager.Instance?.ClearTransientPlayerState();
            TeamManager.Instance?.StopAllPlayers();
            TeamManager.Instance?.ConfigureBallPlayerBodyCollisions();
            GameManager.Instance?.SetPossession(Possession.Loose);
        }

        private void BuildKickoffLocks(TeamSide side)
        {
            BuildTeamLocks(TeamManager.Instance.HomeTeam, homeSpawns);
            BuildTeamLocks(TeamManager.Instance.AwayTeam, awaySpawns);

            kickoffKicker = FindKickoffPlayer(side);
            kickoffPlannedTarget = null;
            if (kickoffKicker != null)
            {
                int dir = TeamManager.Instance.AttackDirFor(side);
                Vector2 center = ballSpawn != null ? (Vector2)ballSpawn.position : Vector2.zero;
                float lead = GameConfig.Instance != null ? GameConfig.Instance.dribbleLead : 0.5f;
                kickoffLocks[kickoffKicker] = center - new Vector2(dir * lead, 0f);
            }
        }

        private void BuildDefendingLocks()
        {
            BuildTeamLocks(TeamManager.Instance.HomeTeam, homeSpawns);
            BuildTeamLocks(TeamManager.Instance.AwayTeam, awaySpawns);
        }

        private void BuildTeamLocks(List<PlayerController> players, Transform[] spawns)
        {
            foreach (var player in players)
            {
                if (player == null) continue;
                int index = SpawnIndexFor(player);
                if (index < 0 || index >= spawns.Length || spawns[index] == null) continue;

                Vector2 pos = spawns[index].position;
                if (GameManager.Instance != null && GameManager.Instance.CurrentHalf == 2)
                    pos.x *= -1f;
                kickoffLocks[player] = pos;
            }
        }

        private int SpawnIndexFor(PlayerController player)
        {
            if (player.Role == FieldRole.Goalkeeper) return 3;
            if (player.FormationRole == TacticalRole.Presser) return 1;
            if (player.FormationRole == TacticalRole.Anchor) return 2;
            return 0;
        }

        private void ApplyKickoffLocks()
        {
            foreach (var kvp in kickoffLocks)
            {
                var player = kvp.Key;
                if (player == null) continue;

                player.TeleportTo(kvp.Value);
                int dir = TeamManager.Instance != null ? TeamManager.Instance.AttackDirFor(player.Side) : (player.Side == TeamSide.Home ? 1 : -1);
                player.SetFacing(new Vector2(dir, 0f));
                player.GetComponent<AIController>()?.ResetBrain();
            }

            if (kickoffKicker != null)
            {
                int dir = TeamManager.Instance.AttackDirFor(kickoffKicker.Side);
                kickoffKicker.SetFacing(new Vector2(dir, 0f));
            }

            Physics2D.SyncTransforms();
        }

        private void PlaceBallAtCenter()
        {
            Vector2 pos = ballSpawn != null ? (Vector2)ballSpawn.position : Vector2.zero;
            BallControl.Instance?.TeleportFree(pos);
        }

        private PlayerController FindKickoffPlayer(TeamSide side)
        {
            var special = TeamManager.Instance.SpecialFor(side);
            return special != null ? special : NearestEligible(side, Vector2.zero);
        }

        private PlayerController NearestEligible(TeamSide side, Vector2 pos)
        {
            PlayerController best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in TeamManager.Instance.Team(side))
            {
                if (p == null || p.Role == FieldRole.Goalkeeper) continue;
                float d = ((Vector2)p.transform.position - pos).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }

        public void StabilizeBeforePlay()
        {
            if (TeamManager.Instance == null) return;

            if (holdKickoffShape) ApplyKickoffLocks();

            foreach (var p in TeamManager.Instance.AllPlayers)
            {
                if (p == null) continue;
                p.ClearMovementInput(true);
                p.Defense?.CancelForRestart();
                p.GetComponent<AIController>()?.ForceImmediateDecision();
            }
            Physics2D.SyncTransforms();
            SnapCameraToHuman();
        }

        public void ReleaseKickoffLock()
        {
            holdKickoffShape = false;
            kickoffLocks.Clear();
            kickoffKicker = null;
            kickoffPlannedTarget = null;
            // Do not clear kickoffReturnPending here. The first return pass happens after play
            // becomes live, so its tiny two-pass sequence must survive the lock release.
        }

        private void SnapCameraToHuman()
        {
            var cam = Camera.main;
            if (cam != null) cam.GetComponent<FollowCamera>()?.SnapToCurrentHuman();
        }

        private Vector2 ClampToPitch(Vector2 p)
        {
            float hx = GameConfig.Instance.pitchLength * 0.5f - 0.7f;
            float hy = GameConfig.Instance.pitchWidth * 0.5f - 0.7f;
            return new Vector2(Mathf.Clamp(p.x,-hx,hx), Mathf.Clamp(p.y,-hy,hy));
        }
    }
}
