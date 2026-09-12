using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Core
{
    public class MatchTimer : MonoBehaviour
    {
        public static MatchTimer Instance { get; private set; }
        public float ElapsedTotal { get; private set; }
        public int DisplayMinutes { get; private set; }
        public int DisplaySeconds { get; private set; }
        public bool ClockRunning { get; private set; }
        public bool InGoldenGoal { get; private set; }
        public float GoldenGoalElapsed { get; private set; }
        public float GoldenGoalRemaining => GameConfig.Instance == null ? 0f : Mathf.Max(0f, GameConfig.Instance.goldenGoalRealSeconds - GoldenGoalElapsed);
        public bool IsFinalStretch
        {
            get
            {
                if (GameConfig.Instance == null) return false;
                if (InGoldenGoal)
                    return GoldenGoalRemaining <= GameConfig.Instance.goldenGoalRealSeconds * GameConfig.Instance.finalStretchFraction;
                float total = Mathf.Max(1f, GameConfig.Instance.matchRealSeconds);
                return ElapsedTotal >= total * (1f - GameConfig.Instance.finalStretchFraction);
            }
        }

        private bool halftimeTriggered;
        private bool regulationCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ClockRunning = false;
        }

        public void PauseClock() => ClockRunning = false;
        public void ResumeClock() => ClockRunning = true;

        private void Update()
        {
            if (!ClockRunning || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;

            if (InGoldenGoal)
            {
                GoldenGoalElapsed += Time.deltaTime;
                float totalET = Mathf.Max(1f, GameConfig.Instance.goldenGoalRealSeconds);
                float footballExtraMinutes = Mathf.Clamp01(GoldenGoalElapsed / totalET) * 10f;
                DisplayMinutes = 90 + Mathf.FloorToInt(footballExtraMinutes);
                DisplaySeconds = Mathf.FloorToInt((footballExtraMinutes - Mathf.Floor(footballExtraMinutes)) * 60f);
                if (GoldenGoalElapsed >= totalET)
                {
                    ClockRunning = false;
                    GameManager.Instance.EndMatch();
                }
                return;
            }

            float total = Mathf.Max(1f, GameConfig.Instance.matchRealSeconds);
            ElapsedTotal = Mathf.Min(total, ElapsedTotal + Time.deltaTime);
            float footballSeconds = (ElapsedTotal / total) * 90f * 60f;
            DisplayMinutes = Mathf.FloorToInt(footballSeconds / 60f);
            DisplaySeconds = Mathf.FloorToInt(footballSeconds % 60f);

            if (!DuelRules.Enabled && !halftimeTriggered && ElapsedTotal >= total * 0.5f)
            {
                halftimeTriggered = true;
                ClockRunning = false;
                GameManager.Instance.BeginHalfTime();
                return;
            }

            if (!regulationCompleted && ElapsedTotal >= total)
            {
                regulationCompleted = true;
                ClockRunning = false;
                bool tied = ScoreManager.Instance != null && ScoreManager.Instance.HomeScore == ScoreManager.Instance.AwayScore;
                if (tied && GameConfig.Instance.enableGoldenGoal && GameManager.Instance.Mode == GameMode.Regular)
                {
                    InGoldenGoal = true;
                    GoldenGoalElapsed = 0f;
                    GameManager.Instance.BeginGoldenGoal();
                }
                else GameManager.Instance.EndMatch();
            }
        }
    }
}
