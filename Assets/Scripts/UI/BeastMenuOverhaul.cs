using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    /// <summary>
    /// FIX29/V12.5 menu controller. This script controls state only.
    /// It never moves, resizes, reanchors, crops or replaces saved menu artwork.
    /// </summary>
    public class BeastMenuOverhaul : MonoBehaviour
    {
        [Header("Baked menu references")]
        public GameObject root;
        public GameObject modeScreen;
        public GameObject pickScreen;
        public GameObject rivalScreen;
        public Text infoTitle;
        public Text infoBody;

        [Header("Current menu state")]
        public CharacterType selectedPlayer = CharacterType.Volt;
        public CharacterType selectedRival = CharacterType.Leo;
        public bool hasPlayerSelection;
        public bool hasRivalSelection;

        private MainMenuUI menu;

        public void Initialize(MainMenuUI owner)
        {
            menu = owner;
            ResolveReferences();
            if (root == null)
            {
                Debug.LogWarning("[Beast Soccer] FIX29_MENU_EDITABLE is missing.", this);
                return;
            }

            root.SetActive(true);
            if (menu != null)
            {
                if (menu.modePanel != null) menu.modePanel.SetActive(false);
                if (menu.characterPanel != null) menu.characterPanel.SetActive(false);
                if (menu.opponentPanel != null) menu.opponentPanel.SetActive(false);
            }

            MatchSetup.Mode = GameMode.Regular;
            hasPlayerSelection = false;
            hasRivalSelection = false;
            DisableContinue();
            SetButtonInteractable(pickScreen, "GORO_PICK", false);
            SetButtonInteractable(rivalScreen, "GORO_PICK", false);
            ConfigureButtonJuice();
            ShowMode();
        }

        private void ResolveReferences()
        {
            if (root == null)
            {
                var t = transform.Find("FIX29_MENU_EDITABLE");
                if (t != null) root = t.gameObject;
            }
            if (root == null) return;

            if (modeScreen == null)
            {
                var t = root.transform.Find("MODE_SCREEN");
                if (t != null) modeScreen = t.gameObject;
            }
            if (pickScreen == null)
            {
                var t = root.transform.Find("YOUR_BEAST_SCREEN");
                if (t != null) pickScreen = t.gameObject;
            }
            if (rivalScreen == null)
            {
                var t = root.transform.Find("RIVAL_SCREEN");
                if (t != null) rivalScreen = t.gameObject;
            }

            if (pickScreen != null)
            {
                if (infoTitle == null)
                {
                    var t = pickScreen.transform.Find("INFO_TITLE");
                    if (t != null) infoTitle = t.GetComponent<Text>();
                }
                if (infoBody == null)
                {
                    var t = pickScreen.transform.Find("INFO_BODY");
                    if (t != null) infoBody = t.GetComponent<Text>();
                }
            }
        }

        public void ShowMode()
        {
            SetScreen(modeScreen, true);
            SetScreen(pickScreen, false);
            SetScreen(rivalScreen, false);
            DisableContinue();
        }

        public void ShowPick()
        {
            SetScreen(modeScreen, false);
            SetScreen(pickScreen, true);
            SetScreen(rivalScreen, false);
            RefreshPlayerPage();
            DisableContinue();
        }

        public void ShowRival()
        {
            if (!hasPlayerSelection) return;
            SetScreen(modeScreen, false);
            SetScreen(pickScreen, false);
            SetScreen(rivalScreen, true);
            hasRivalSelection = false;
            RefreshRivalPage();
            RefreshYourCardOnRival();
        }

        public void BackToMode() => ShowMode();
        public void BackToPlayerPick() => ShowPick();

        // Requested 2-choice flow: choose your Beast -> rival screen immediately.
        public void PickVolt() => SelectPlayer(CharacterType.Volt);
        public void PickLeo() => SelectPlayer(CharacterType.Leo);
        public void PickGoro() { }

        private void SelectPlayer(CharacterType c)
        {
            selectedPlayer = c;
            hasPlayerSelection = true;
            MatchSetup.PlayerCharacter = c;
            if (infoTitle != null) infoTitle.text = c == CharacterType.Volt ? "VOLT — THE SKY STRIKER" : "LEO — THE PRIDE";
            if (infoBody != null) infoBody.text = InfoFor(c);
            ShowRival();
        }

        public void RivalVolt() => SelectRival(CharacterType.Volt);
        public void RivalLeo() => SelectRival(CharacterType.Leo);
        public void RivalGoro() { }

        private void SelectRival(CharacterType c)
        {
            selectedRival = c;
            hasRivalSelection = true;
            MatchSetup.OpponentCharacter = c;
            MatchSetup.OpponentRandom = false;
            Toggle(rivalScreen, "RIVAL_VOLT", c == CharacterType.Volt);
            Toggle(rivalScreen, "RIVAL_LEO", c == CharacterType.Leo);
            Toggle(rivalScreen, "RIVAL_GORO", false);
            SetActive(rivalScreen, "START_MATCH", true);
            SetActive(rivalScreen, "START_MATCH_ART", true);
        }

        public void StartSelectedMatch()
        {
            if (!hasPlayerSelection || !hasRivalSelection) return;
            selectedPlayer = BeastSoccer.Core.DuelRules.Outfielder(selectedPlayer, CharacterType.Volt);
            selectedRival = BeastSoccer.Core.DuelRules.Outfielder(selectedRival, CharacterType.Leo);
            if (menu == null) menu = GetComponent<MainMenuUI>();
            if (menu == null) return;

            MatchSetup.Mode = GameMode.Regular;
            MatchSetup.PlayerCharacter = selectedPlayer;
            MatchSetup.OpponentCharacter = selectedRival;
            MatchSetup.OpponentRandom = false;
            if (selectedRival == CharacterType.Volt) menu.OnOpponentVolt();
            else menu.OnOpponentLeo();
        }

        private void RefreshPlayerPage()
        {
            Toggle(pickScreen, "YOUR_VOLT", hasPlayerSelection && selectedPlayer == CharacterType.Volt);
            Toggle(pickScreen, "YOUR_LEO", hasPlayerSelection && selectedPlayer == CharacterType.Leo);
            Toggle(pickScreen, "YOUR_GORO", false);
            DisableContinue();

            if (!hasPlayerSelection)
            {
                if (infoTitle != null) infoTitle.text = "SELECT YOUR BEAST";
                if (infoBody != null)
                    infoBody.text = "Choose Volt or Leo.\n\nGoro guards both goals.\n\nAGUILAR\nOrange home kit\n\nALIANZA\nBlue away kit";
            }
        }

        private void RefreshRivalPage()
        {
            Toggle(rivalScreen, "RIVAL_VOLT", false);
            Toggle(rivalScreen, "RIVAL_LEO", false);
            Toggle(rivalScreen, "RIVAL_GORO", false);
            SetActive(rivalScreen, "START_MATCH", false);
            SetActive(rivalScreen, "START_MATCH_ART", false);
        }

        public void RefreshYourCardOnRival()
        {
            Toggle(rivalScreen, "YOUR_VOLT_R", hasPlayerSelection && selectedPlayer == CharacterType.Volt);
            Toggle(rivalScreen, "YOUR_LEO_R", hasPlayerSelection && selectedPlayer == CharacterType.Leo);
            Toggle(rivalScreen, "YOUR_GORO_R", false);
        }

        private void DisableContinue()
        {
            SetActive(pickScreen, "CONTINUE_PICK", false);
            SetActive(pickScreen, "CONTINUE", false);
        }

        private void ConfigureButtonJuice()
        {
            AddJuice(modeScreen, "QUICK_MATCH", modeScreen, "QUICK_MATCH_ART");
            AddJuice(pickScreen, "VOLT_PICK", pickScreen, "VOLT_CARD");
            AddJuice(pickScreen, "LEO_PICK", pickScreen, "LEO_CARD");
            AddJuice(pickScreen, "BACK_PICK", pickScreen, "BACK_PICK");
            AddJuice(rivalScreen, "VOLT_PICK", rivalScreen, "VOLT_CARD");
            AddJuice(rivalScreen, "LEO_PICK", rivalScreen, "LEO_CARD");
            AddJuice(rivalScreen, "START_MATCH", rivalScreen, "START_MATCH_ART");
            AddJuice(rivalScreen, "BACK", rivalScreen, "BACK");
        }

        private static void AddJuice(GameObject buttonRoot, string buttonName, GameObject visualRoot, string visualName)
        {
            if (buttonRoot == null || visualRoot == null) return;
            var bt = buttonRoot.transform.Find(buttonName);
            if (bt == null || bt.GetComponent<Button>() == null) return;
            var vt = visualRoot.transform.Find(visualName) as RectTransform;
            if (vt == null) vt = bt as RectTransform;
            var juice = bt.GetComponent<MenuButtonJuice>();
            if (juice == null) juice = bt.gameObject.AddComponent<MenuButtonJuice>();
            juice.visualTarget = vt;
        }

        private string InfoFor(CharacterType c)
        {
            if (c == CharacterType.Volt)
                return "ROLE: ATTACKER\n\nSPEED      ■■■■■\nSHOOT      ■■■■□\nSTRENGTH   ■■□□□\nPASS       ■■■□□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nFLY / SKY BURST\nMassive jump with hang time.\nFast, direct attacking play.";
            return "ROLE: ATTACKER\n\nSPEED      ■■■■□\nSHOOT      ■■■■□\nSTRENGTH   ■■■□□\nPASS       ■■■■□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nPRIDE RUSH\nFast playmaker with improved\npassing and attacking speed.";
        }

        private static void SetScreen(GameObject g, bool on) { if (g != null) g.SetActive(on); }
        private static void Toggle(GameObject parent, string child, bool on) => SetActive(parent, child, on);
        private static void SetActive(GameObject parent, string child, bool on)
        {
            if (parent == null) return;
            var t = parent.transform.Find(child);
            if (t != null) t.gameObject.SetActive(on);
        }
        private static void SetButtonInteractable(GameObject parent, string child, bool value)
        {
            if (parent == null) return;
            var t = parent.transform.Find(child);
            if (t == null) return;
            var b = t.GetComponent<Button>();
            if (b != null) b.interactable = value;
        }
    }
}
