using System;
using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Ball;

namespace BeastSoccer.Upgrades
{
    [DefaultExecutionOrder(-400)]
    public sealed class MatchRewards : MonoBehaviour
    {
        public static MatchRewards Instance { get; private set; }
        public string MatchId { get; private set; }
        public bool Saved { get; private set; }
        private bool active, played, completed;
        private float nextCompletionRetry;
        private GameManager manager;
        private ScoreManager score;
        private void Awake() => Instance = this;
        private void Start()
        {
            manager = GameManager.Instance; score = ScoreManager.Instance;
            if (!UpgradeSettings.Rewards || manager == null || score == null) return;
            MatchId = Guid.NewGuid().ToString("N");
            active = Progression.Apply(s => s.BeginMatch(MatchId));
            manager.OnPhaseChanged += Phase;
            score.GoalScored += Goal;
        }
        private void Update()
        {
            if (!UpgradeSettings.Rewards) { active = false; return; }
            if (active && played && !completed && manager != null && manager.Phase == MatchPhase.MatchOver && Time.unscaledTime >= nextCompletionRetry)
            {
                nextCompletionRetry = Time.unscaledTime + 1f;
                Phase(MatchPhase.MatchOver);
            }
        }
        private void Goal(TeamSide side, bool ultimate)
        {
            if (!active || !UpgradeSettings.Rewards || side != TeamSide.Home || completed) return;
            Progression.Apply(s => s.Goal(MatchId, score.HomeScore, ultimate, UpgradeSettings.Current));
        }
        public void SuccessfulTackle(TeamSide side)
        {
            if (active && UpgradeSettings.Rewards && !completed && side == TeamSide.Home && manager.Phase == MatchPhase.Playing)
                Progression.Apply(s => s.Tackle(MatchId, UpgradeSettings.Current));
        }
        private void Phase(MatchPhase phase)
        {
            if (phase == MatchPhase.Playing) played = true;
            if (!active || !UpgradeSettings.Rewards || !played || phase != MatchPhase.MatchOver || completed) return;
            Saved = Progression.Apply(s => s.Complete(MatchId, score.HomeScore, score.AwayScore, UpgradeSettings.Current));
            completed = Saved;
        }
        private void Flush() { if (active && UpgradeSettings.Rewards) Progression.Apply(s => s.Flush()); }
        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() => Flush();
        private void OnDestroy()
        {
            if (manager != null) manager.OnPhaseChanged -= Phase;
            if (score != null) score.GoalScored -= Goal;
            if (Instance == this) Instance = null;
        }
    }
}
