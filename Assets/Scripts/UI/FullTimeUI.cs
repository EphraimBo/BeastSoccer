using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    public class FullTimeUI : MonoBehaviour
    {
        public GameObject panel;
        public Text finalScoreText;
        public string mainMenuScene = "MainMenu";
        private bool bound;

        private void OnEnable() => Bind();
        private void Start() => Bind();

        private void Bind()
        {
            if (bound || GameManager.Instance == null) return;
            GameManager.Instance.OnPhaseChanged += HandlePhase;
            bound = true;
            HandlePhase(GameManager.Instance.Phase);
        }

        private void OnDisable()
        {
            if (bound && GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= HandlePhase;
            bound = false;
        }

        private void HandlePhase(MatchPhase phase)
        {
            bool show = phase == MatchPhase.MatchOver;
            if (panel != null) panel.SetActive(show);
            if (!show || finalScoreText == null || ScoreManager.Instance == null) return;
            finalScoreText.text = $"{ScoreManager.Instance.HomeScore}  -  {ScoreManager.Instance.AwayScore}";
        }

        public void OnRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void OnChangePlayers()
        {
            Time.timeScale = 1f;
            MatchSetup.OpenCharacterSelectOnLoad = true;
            SceneManager.LoadScene(mainMenuScene);
        }

        public void OnMainMenu()
        {
            Time.timeScale = 1f;
            MatchSetup.OpenCharacterSelectOnLoad = false;
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
