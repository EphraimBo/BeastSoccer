using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Player;

namespace BeastSoccer.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public TeamSide LastConcedingSide { get; private set; } = TeamSide.Away;
        public System.Action<int,int> OnScoreChanged;
        public System.Action<CharacterType> OnUltGoal;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ScoreGoal(TeamSide scoringSide, PlayerController scorer)
        {
            if (GameManager.Instance != null && GameManager.Instance.Mode == GameMode.Defending && scoringSide == TeamSide.Home)
            {
                // Defending mode never turns a clearance into an attacking score. Home earns points from saves.
                GameManager.Instance.RestartDefendingWave();
                return;
            }

            bool ultGoal = scorer != null && scorer.Ult != null && scorer.Ult.IsActive;
            CharacterType ultGoalCharacter = scorer != null ? scorer.Character : CharacterType.Generic;
            if (scoringSide == TeamSide.Home) HomeScore++; else AwayScore++;
            LastConcedingSide = scoringSide == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            OnScoreChanged?.Invoke(HomeScore, AwayScore);
            AwardGoalCharge(scoringSide, scorer);
            scorer?.Animation?.Trigger("Celebrate");
            GameFeel.Shake(0.14f); GameFeel.Haptic();
            if (ultGoal)
            {
                OnUltGoal?.Invoke(ultGoalCharacter);
                BeastSoccer.Audio.AudioManager.Instance?.PlayUltGoal();
                GameManager.Instance?.PlayUltGoalMoment();
            }
            GameManager.Instance.NotifyGoalScored();
        }

        public void RegisterDefendingSave(PlayerController keeper)
        {
            HomeScore++;
            OnScoreChanged?.Invoke(HomeScore, AwayScore);
            GameFeel.Shake(0.10f); GameFeel.Haptic();
            var special = TeamManager.Instance.SpecialFor(TeamSide.Home);
            special?.Ult?.AddCharge(GameConfig.Instance.ultChargePerAction);
            GameManager.Instance.NotifySaveMade();
        }

        private void AwardGoalCharge(TeamSide side, PlayerController scorer)
        {
            var special = TeamManager.Instance != null ? TeamManager.Instance.SpecialFor(side) : null;
            if (special == null || special.Ult == null || GameConfig.Instance == null) return;
            // FIX38: any goal by this team fills its special character's ultimate completely.
            special.Ult.AddCharge(Mathf.Max(0f, GameConfig.Instance.ultChargeGoal));
        }
    }
}
