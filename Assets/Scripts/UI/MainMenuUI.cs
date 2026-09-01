using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.UI
{
    /// <summary>
    /// Carries the player's menu choices into the Match scene via a tiny static
    /// holder so the Match scene's GameManager can read them on load.
    /// </summary>
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
            if (overhaul == null) overhaul = gameObject.AddComponent<BeastMenuOverhaul>();
            overhaul.Build(this);
            return;
            #pragma warning disable CS0162
            if (MatchSetup.OpenCharacterSelectOnLoad)
            {
                MatchSetup.OpenCharacterSelectOnLoad = false;
                Show(characterPanel);
            }
            else Show(modePanel);
            #pragma warning restore CS0162
        }

        public void OnPlay() => Show(modePanel);

        // Mode
        public void OnSelectRegular() { MatchSetup.Mode = GameMode.Regular; Show(characterPanel); }
        public void OnSelectDefending() { /* Defending mode intentionally disabled for the current prototype. */ }

        private void ApplyPrototypeMenuTweaks()
        {
            // Keep the Defending tile visible as a coming-soon placeholder, but make it impossible
            // to enter that legacy mode from either newly built or already-existing menu scenes.
            if (modePanel != null)
            {
                var defending = modePanel.transform.Find("DEFENDING");
                if (defending != null)
                {
                    var b = defending.GetComponent<Button>();
                    if (b != null) b.interactable = false;
                    var img = defending.GetComponent<Image>();
                    if (img != null) img.color = new Color(.22f,.22f,.26f,.72f);
                }
            }

            // FIX26 menu order: VOLT, LEO, GORO. This is applied at runtime as well as in the
            // builder so the user does not need to rebuild and lose custom Match scene artwork.
            PositionButton(characterPanel, "VOLT", new Vector2(-320f, 0f));
            PositionButton(characterPanel, "LEO",  new Vector2(0f, 0f));
            PositionButton(characterPanel, "GORO", new Vector2(320f, 0f));

            PositionButton(opponentPanel, "VOLT", new Vector2(-360f, 70f));
            PositionButton(opponentPanel, "LEO",  new Vector2(-120f, 70f));
            PositionButton(opponentPanel, "GORO", new Vector2(120f, 70f));
            PositionButton(opponentPanel, "RANDOM", new Vector2(360f, 70f));
        }

        private static void PositionButton(GameObject panel, string childName, Vector2 pos)
        {
            if (panel == null) return;
            var child = panel.transform.Find(childName);
            if (child == null) return;
            var rt = child.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;
        }

        // Character
        public void OnPickLeo() { MatchSetup.PlayerCharacter = CharacterType.Leo; Show(opponentPanel); }
        public void OnPickGoro() { MatchSetup.PlayerCharacter = CharacterType.Goro; Show(opponentPanel); }
        public void OnPickVolt() { MatchSetup.PlayerCharacter = CharacterType.Volt; Show(opponentPanel); }

        // Opponent
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
