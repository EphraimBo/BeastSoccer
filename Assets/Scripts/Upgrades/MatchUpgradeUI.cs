using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.UI;

namespace BeastSoccer.Upgrades
{
    public sealed class MatchUpgradeUI : MonoBehaviour
    {
        public GameObject coinRoot, resultRoot, pauseRoot;
        public GameObject pauseButtonRoot, legacyPauseButton;
        public Text coins, earnings, rewardBreakdown, savedLabel, resultScore, resultDetails, pauseDifficulty;
        private GameManager manager;
        private float shownCoins;
        private void Start()
        {
            manager = GameManager.Instance;
            if (UpgradeSettings.Rewards && Progression.Store != null) shownCoins = Progression.Store.Data.coins;
            if (manager != null) manager.OnPhaseChanged += Phase;
            Progression.Changed += Refresh;
            Refresh();
        }
        private void Phase(MatchPhase phase) => Refresh();
        private void LateUpdate()
        {
            coinRoot.SetActive(UpgradeSettings.Rewards);
            resultRoot.SetActive(UpgradeSettings.Menus && manager != null && manager.Phase == MatchPhase.MatchOver);
            if (pauseRoot != null) pauseRoot.SetActive(UpgradeSettings.Menus && manager != null && manager.Phase == MatchPhase.Paused);
            if (pauseButtonRoot != null) pauseButtonRoot.SetActive(UpgradeSettings.Menus && manager != null && manager.Phase != MatchPhase.MatchOver && manager.Phase != MatchPhase.Paused);
            if (legacyPauseButton != null) legacyPauseButton.SetActive(!UpgradeSettings.Menus);
            if (pauseDifficulty != null) pauseDifficulty.gameObject.SetActive(UpgradeSettings.Menus);
            if (!UpgradeSettings.Rewards) return;
            var save = Progression.Store;
            if (save == null) { coins.text = "Progress unavailable"; return; }
            shownCoins = Mathf.MoveTowards(shownCoins, save.Data.coins, Mathf.Max(20f, Mathf.Abs(save.Data.coins - shownCoins) * 14f) * Time.unscaledDeltaTime);
            coins.text = "Coins " + Mathf.RoundToInt(shownCoins).ToString("N0");
            // Results can subscribe before the reward tracker. Refresh from committed data until settled.
            if (manager != null && manager.Phase == MatchPhase.MatchOver) Refresh();
        }
        private void Refresh()
        {
            var save = UpgradeSettings.Rewards ? Progression.Store : null;
            var d = save != null ? save.Data : null;
            if (d != null)
            {
                earnings.text = "+" + d.MatchCoins + " COINS";
                rewardBreakdown.text = "Match completed    +" + d.matchCompletionCoins + "\nResult bonus    +" + d.matchResultCoins +
                    "\nGoals + ult bonuses    +" + d.matchGoalCoins + "\nSuccessful tackles    +" + d.matchTackleCoins;
            }
            else { earnings.text = ""; rewardBreakdown.text = UpgradeSettings.Rewards ? "Progress unavailable" : ""; }
            savedLabel.text = Progression.Error != null ? "SAVE FAILED — " + Progression.Error :
                MatchRewards.Instance != null && MatchRewards.Instance.Saved ? "SAVED TO YOUR BALANCE" : "";
            var score = ScoreManager.Instance;
            resultScore.text = score != null ? score.HomeScore + " — " + score.AwayScore : "";
            resultDetails.text = (manager != null ? manager.PlayerCharacter : MatchSetup.PlayerCharacter).ToString().ToUpperInvariant() + " vs " +
                (manager != null ? manager.OpponentCharacter : MatchSetup.OpponentCharacter).ToString().ToUpperInvariant() + "   ·   " + MatchSetup.Difficulty.ToString().ToUpperInvariant();
            if (pauseDifficulty != null) pauseDifficulty.text = MatchSetup.Difficulty.ToString().ToUpperInvariant();
        }
        public void Rematch() { Time.timeScale = 1f; SceneManager.LoadScene("Match"); }
        public void Resume() => manager?.ResumeMatch();
        public void Pause() => manager?.PauseMatch();
        public void ChangeMatch() { MatchSetup.OpenMapSelectOnLoad = true; MainMenu(); }
        public void Store() { if (manager == null || manager.Phase != MatchPhase.MatchOver) return; MatchSetup.OpenStoreOnLoad = true; MainMenu(); }
        public void MainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
        private void OnDestroy()
        {
            Progression.Changed -= Refresh;
            if (manager != null) manager.OnPhaseChanged -= Phase;
        }
    }
}
