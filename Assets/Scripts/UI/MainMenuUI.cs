using UnityEngine;
using UnityEngine.SceneManagement;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    /// <summary>Launches matches from the authored menu.</summary>
    [RequireComponent(typeof(MenuController))]
    public sealed class MainMenuUI : MonoBehaviour
    {
        public string matchScene = "Match";
        private void Start() => GetComponent<MenuController>().Initialize(this);

        public void StartMatch(CharacterType player, CharacterType opponent,
            GameMode mode = GameMode.Regular, bool randomOpponent = false)
        {
            MatchSetup.Mode = mode;
            MatchSetup.PlayerCharacter = player;
            MatchSetup.OpponentCharacter = opponent;
            MatchSetup.OpponentRandom = randomOpponent;
            MatchSetup.HasExplicitSelection = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(matchScene);
        }

        public void OnQuit() => Application.Quit();
    }
}
