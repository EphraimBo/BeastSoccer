using UnityEngine;
using UnityEngine.SceneManagement;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.UI
{
    public class PauseUI : MonoBehaviour
    {
        public GameObject panel;
        public string mainMenuScene = "MainMenu";
        private bool bound;

        private void OnEnable() => Bind();
        private void Start() => Bind();

        private void Bind()
        {
            if (bound || GameManager.Instance == null) return;
            GameManager.Instance.OnPhaseChanged += Handle;
            bound = true;
            Handle(GameManager.Instance.Phase);
        }

        private void OnDisable()
        {
            if (bound && GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= Handle;
            bound = false;
        }

        private void Handle(MatchPhase p)
        {
            if (panel != null) panel.SetActive(p == MatchPhase.Paused);
        }

        public void OnResume() => GameManager.Instance?.ResumeMatch();

        public void OnRestart()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void OnQuitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
