using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    /// <summary>
    /// FIX31: editable menu runtime behaviour only. Never rebuilds or repositions the baked menu.
    /// Selection slots begin empty and only fill after the player actually makes a choice.
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
            if (menu != null)
            {
                if (menu.modePanel) menu.modePanel.SetActive(false);
                if (menu.characterPanel) menu.characterPanel.SetActive(false);
                if (menu.opponentPanel) menu.opponentPanel.SetActive(false);
            }

            if (root == null)
            {
                var t = transform.Find("FIX29_MENU_EDITABLE");
                if (t != null) root = t.gameObject;
            }
            if (root == null)
            {
                Debug.LogWarning("[Beast Soccer] Editable menu root is missing. The current scene layout was not changed.", this);
                return;
            }

            MatchSetup.Mode = GameMode.Regular;
            hasPlayerSelection = false;
            hasRivalSelection = false;
            ConfigureButtonJuice();
            ShowMode();
        }

        public void ShowMode()
        {
            SetScreen(modeScreen, true);
            SetScreen(pickScreen, false);
            SetScreen(rivalScreen, false);
        }

        public void ShowPick()
        {
            SetScreen(modeScreen, false);
            SetScreen(pickScreen, true);
            SetScreen(rivalScreen, false);

            // FIX31: returning to this page preserves a choice already made, but the very first
            // visit is deliberately blank instead of pretending Leo/Volt was chosen for the user.
            RefreshPlayerPage();
        }

        public void ShowRival()
        {
            if (!hasPlayerSelection) return;
            SetScreen(modeScreen, false);
            SetScreen(pickScreen, false);
            SetScreen(rivalScreen, true);

            // Each fresh trip from player select asks for a rival choice. No default rival.
            hasRivalSelection = false;
            RefreshRivalPage();
            RefreshYourCardOnRival();
        }

        public void BackToMode() => ShowMode();
        public void BackToPlayerPick() => ShowPick();

        public void PickVolt() { selectedPlayer = CharacterType.Volt; hasPlayerSelection = true; ApplyPlayerSelection(); }
        public void PickLeo()  { selectedPlayer = CharacterType.Leo;  hasPlayerSelection = true; ApplyPlayerSelection(); }
        public void PickGoro() { selectedPlayer = CharacterType.Goro; hasPlayerSelection = true; ApplyPlayerSelection(); }

        public void RivalVolt() { selectedRival = CharacterType.Volt; hasRivalSelection = true; ApplyRivalSelection(); }
        public void RivalLeo()  { selectedRival = CharacterType.Leo;  hasRivalSelection = true; ApplyRivalSelection(); }
        public void RivalGoro() { selectedRival = CharacterType.Goro; hasRivalSelection = true; ApplyRivalSelection(); }

        public void StartSelectedMatch()
        {
            if (!hasPlayerSelection || !hasRivalSelection) return;
            if (menu == null) menu = GetComponent<MainMenuUI>();
            if (menu == null) return;

            MatchSetup.Mode = GameMode.Regular;
            MatchSetup.PlayerCharacter = selectedPlayer;
            MatchSetup.OpponentCharacter = selectedRival;
            MatchSetup.OpponentRandom = false;

            if (selectedRival == CharacterType.Volt) menu.OnOpponentVolt();
            else if (selectedRival == CharacterType.Goro) menu.OnOpponentGoro();
            else menu.OnOpponentLeo();
        }

        public void ApplyPlayerSelection()
        {
            if (!hasPlayerSelection) { RefreshPlayerPage(); return; }
            MatchSetup.PlayerCharacter = selectedPlayer;
            Toggle(pickScreen, "YOUR_VOLT", selectedPlayer == CharacterType.Volt);
            Toggle(pickScreen, "YOUR_LEO",  selectedPlayer == CharacterType.Leo);
            Toggle(pickScreen, "YOUR_GORO", selectedPlayer == CharacterType.Goro);

            SetActive(pickScreen, "CONTINUE_PICK", true);
            SetActive(pickScreen, "CONTINUE", true);

            if (infoTitle) infoTitle.text = selectedPlayer == CharacterType.Volt ? "VOLT — THE SKY STRIKER" : selectedPlayer == CharacterType.Leo ? "LEO — THE PRIDE" : "GORO — THE TITAN";
            if (infoBody) infoBody.text = InfoFor(selectedPlayer);
        }

        private void RefreshPlayerPage()
        {
            Toggle(pickScreen, "YOUR_VOLT", hasPlayerSelection && selectedPlayer == CharacterType.Volt);
            Toggle(pickScreen, "YOUR_LEO",  hasPlayerSelection && selectedPlayer == CharacterType.Leo);
            Toggle(pickScreen, "YOUR_GORO", hasPlayerSelection && selectedPlayer == CharacterType.Goro);
            SetActive(pickScreen, "CONTINUE_PICK", hasPlayerSelection);
            SetActive(pickScreen, "CONTINUE", hasPlayerSelection);
            if (!hasPlayerSelection)
            {
                if (infoTitle) infoTitle.text = "SELECT YOUR BEAST";
                if (infoBody) infoBody.text = "Choose Volt, Leo or Goro.";
            }
            else ApplyPlayerSelection();
        }

        public void ApplyRivalSelection()
        {
            if (!hasRivalSelection) { RefreshRivalPage(); return; }
            MatchSetup.OpponentCharacter = selectedRival;
            MatchSetup.OpponentRandom = false;
            Toggle(rivalScreen, "RIVAL_VOLT", selectedRival == CharacterType.Volt);
            Toggle(rivalScreen, "RIVAL_LEO",  selectedRival == CharacterType.Leo);
            Toggle(rivalScreen, "RIVAL_GORO", selectedRival == CharacterType.Goro);
            SetActive(rivalScreen, "START_MATCH", true);
            SetActive(rivalScreen, "START_MATCH_ART", true);
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
            Toggle(rivalScreen, "YOUR_LEO_R",  hasPlayerSelection && selectedPlayer == CharacterType.Leo);
            Toggle(rivalScreen, "YOUR_GORO_R", hasPlayerSelection && selectedPlayer == CharacterType.Goro);
        }

        private void ConfigureButtonJuice()
        {
            AddJuice(modeScreen, "QUICK_MATCH", modeScreen, "QUICK_MATCH_ART");

            AddJuice(pickScreen, "VOLT_PICK", pickScreen, "VOLT_CARD");
            AddJuice(pickScreen, "LEO_PICK", pickScreen, "LEO_CARD");
            AddJuice(pickScreen, "GORO_PICK", pickScreen, "GORO_CARD");
            AddJuice(pickScreen, "CONTINUE_PICK", pickScreen, "CONTINUE");
            AddJuice(pickScreen, "CONTINUE", pickScreen, "CONTINUE");
            AddJuice(pickScreen, "BACK_PICK", pickScreen, "BACK_PICK");

            AddJuice(rivalScreen, "VOLT_PICK", rivalScreen, "VOLT_CARD");
            AddJuice(rivalScreen, "LEO_PICK", rivalScreen, "LEO_CARD");
            AddJuice(rivalScreen, "GORO_PICK", rivalScreen, "GORO_CARD");
            AddJuice(rivalScreen, "START_MATCH", rivalScreen, "START_MATCH_ART");
            AddJuice(rivalScreen, "BACK", rivalScreen, "BACK");
        }

        private static void AddJuice(GameObject buttonRoot, string buttonName, GameObject visualRoot, string visualName)
        {
            if (!buttonRoot || !visualRoot) return;
            var bt = buttonRoot.transform.Find(buttonName);
            if (!bt) return;
            var b = bt.GetComponent<Button>();
            if (!b) return;
            var vt = visualRoot.transform.Find(visualName) as RectTransform;
            if (!vt) vt = bt as RectTransform;
            var juice = bt.GetComponent<MenuButtonJuice>();
            if (!juice) juice = bt.gameObject.AddComponent<MenuButtonJuice>();
            juice.visualTarget = vt;
        }

        private static void SetScreen(GameObject g, bool on) { if (g) g.SetActive(on); }

        private string InfoFor(CharacterType c)
        {
            if (c == CharacterType.Volt) return "ROLE: ATTACKER\n\nSPEED      ■■■■■\nSHOOT      ■■■■□\nSTRENGTH   ■■□□□\nPASS       ■■■□□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nFLY / SKY BURST\nMassive jump with hang time.\nFast, direct attacking play.";
            if (c == CharacterType.Goro) return "ROLE: BRUISER\n\nSPEED      ■■□□□\nSHOOT      ■■■□□\nSTRENGTH   ■■■■■\nPASS       ■■□□□\nDEFENSE    ■■■■□\n\nSPECIAL ABILITY\nCHARGE\nPower through defenders and\nwin physical collisions.";
            return "ROLE: ATTACKER\n\nSPEED      ■■■■□\nSHOOT      ■■■■□\nSTRENGTH   ■■■□□\nPASS       ■■■■□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nPRIDE RUSH\nFast playmaker with improved\npassing and attacking speed.";
        }

        private static void Toggle(GameObject parent, string child, bool on) => SetActive(parent, child, on);
        private static void SetActive(GameObject parent, string child, bool on)
        {
            if (!parent) return;
            var t = parent.transform.Find(child);
            if (t) t.gameObject.SetActive(on);
        }
    }
}
