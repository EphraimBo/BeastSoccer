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
        public static CharacterType OpponentCharacter = CharacterType.Volt;
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
            ApplyMenuTypography();
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

        private static Font displayFont;

        private void ApplyMenuTypography()
        {
            Font font = GetDisplayFont(26);
            if (font == null) return;
            foreach (var label in GetComponentsInChildren<Text>(true))
            {
                if (label == null) continue;
                bool longCopy = !string.IsNullOrEmpty(label.text) && (label.text.Length > 30 || label.text.Contains("\n"));
                label.font = font;
                label.fontStyle = FontStyle.Normal;
                label.resizeTextForBestFit = true;
                if (!longCopy)
                {
                    label.fontSize = Mathf.Max(label.fontSize, 24);
                    label.alignment = TextAnchor.MiddleCenter;
                }
                else
                {
                    label.fontSize = Mathf.Max(label.fontSize, 20);
                }

                var outline = label.GetComponent<Outline>();
                if (outline == null) outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = longCopy ? new Vector2(0.7f, -0.7f) : new Vector2(0.9f, -0.9f);

                var shadow = label.GetComponent<Shadow>();
                if (shadow == null) shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
                shadow.effectDistance = new Vector2(0f, -1.6f);
            }
        }

        private static Font GetDisplayFont(int size)
        {
            if (displayFont != null) return displayFont;
            try
            {
                displayFont = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Trebuchet MS", "Verdana", "Arial"
                }, size);
            }
            catch
            {
                displayFont = null;
            }

            if (displayFont == null)
            {
                try { displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { displayFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            }
            return displayFont;
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
        public void OnPickGoro() { } // Goro is reserved for the two AI goalkeepers.
        public void OnPickVolt() { MatchSetup.PlayerCharacter = CharacterType.Volt; Show(opponentPanel); }

        public void OnOpponentLeo() { SetOpp(CharacterType.Leo); }
        public void OnOpponentGoro() { }
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
