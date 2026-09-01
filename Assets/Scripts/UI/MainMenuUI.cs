using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.UI
{
    public static class MatchSetup
    {
        public static GameMode Mode = GameMode.Regular;
        public static CharacterType PlayerCharacter = CharacterType.Leo;
        public static CharacterType OpponentCharacter = CharacterType.Goro;
        public static bool OpponentRandom = false;
        public static bool OpenCharacterSelectOnLoad = false;
        public static bool HasExplicitSelection = false;
    }

    public class MainMenuUI : MonoBehaviour
    {
        public string matchScene = "Match";
        public GameObject modePanel;
        public GameObject characterPanel;
        public GameObject opponentPanel;

        private void Start()
        {
            ApplyPrototypeMenuTweaks();
            var overhaul = GetComponent<BeastMenuOverhaul>();
            if (overhaul != null)
            {
                overhaul.Initialize(this);
                return;
            }

            // Legacy fallback only if FIX29 controller is absent.
            if (MatchSetup.OpenCharacterSelectOnLoad)
            {
                MatchSetup.OpenCharacterSelectOnLoad = false;
                Show(characterPanel);
            }
            else Show(modePanel);
        }

        public void OnPlay() => Show(modePanel);
        public void OnSelectRegular() { MatchSetup.Mode = GameMode.Regular; Show(characterPanel); }
        public void OnSelectDefending() { }

        private void ApplyPrototypeMenuTweaks()
        {
            if (modePanel != null)
            {
                var defending = modePanel.transform.Find("DEFENDING");
                if (defending != null)
                {
                    var b = defending.GetComponent<Button>();
                    if (b != null) b.interactable = false;
                }
            }
        }

        public void OnPickLeo() { MatchSetup.PlayerCharacter = CharacterType.Leo; Show(opponentPanel); }
        public void OnPickGoro() { MatchSetup.PlayerCharacter = CharacterType.Goro; Show(opponentPanel); }
        public void OnPickVolt() { MatchSetup.PlayerCharacter = CharacterType.Volt; Show(opponentPanel); }

        public void OnOpponentLeo() { SetOpp(CharacterType.Leo); }
        public void OnOpponentGoro() { SetOpp(CharacterType.Goro); }
        public void OnOpponentVolt() { SetOpp(CharacterType.Volt); }
        public void OnOpponentRandom() { MatchSetup.OpponentRandom = true; StartMatch(); }

        private void SetOpp(CharacterType c)
        {
            MatchSetup.OpponentCharacter = c;
            MatchSetup.OpponentRandom = false;
            StartMatch();
        }

        private void StartMatch()
        {
            MatchSetup.HasExplicitSelection = true;
            SceneManager.LoadScene(matchScene);
        }

        private void Show(GameObject panel)
        {
            if (modePanel != null) modePanel.SetActive(panel == modePanel);
            if (characterPanel != null) characterPanel.SetActive(panel == characterPanel);
            if (opponentPanel != null) opponentPanel.SetActive(panel == opponentPanel);
        }

        public void OnQuit() => Application.Quit();
    }
}
