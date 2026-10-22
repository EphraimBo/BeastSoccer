using UnityEngine;
using UnityEngine.SceneManagement;
using BeastSoccer.Data;

namespace BeastSoccer.Patches
{
    /// <summary>
    /// V12.2 duration migration.
    ///
    /// Existing Beast Soccer Match scenes serialize matchRealSeconds as 75 seconds total.
    /// In modes that use halftime, that is 37.5 seconds per half.
    ///
    /// User request: increase each half by one real minute.
    /// New regulation total = 75 + 60 + 60 = 195 seconds.
    ///
    /// Arcade Duel currently skips the halftime break by design, so it uses the same
    /// 195-second regulation total continuously. This patch does not alter that rule.
    /// </summary>
    public static class MatchDurationPatchV12_2
    {
        private const float OldRegulationSeconds = 75f;
        private const float NewRegulationSeconds = 195f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterAndApply()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
        }

        private static void Apply()
        {
            var cfg = GameConfig.Instance;
            if (cfg == null)
                cfg = Object.FindFirstObjectByType<GameConfig>();

            if (cfg == null)
                return;

            // Migrate only the known V11/V12 serialized value. This avoids repeatedly
            // adding time if a future scene or inspector override already uses 195 seconds.
            if (Mathf.Approximately(cfg.matchRealSeconds, OldRegulationSeconds))
                cfg.matchRealSeconds = NewRegulationSeconds;
        }
    }
}
