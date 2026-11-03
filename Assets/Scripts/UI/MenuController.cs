using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    /// <summary>Selection state and callbacks for the saved menu. It never rewrites its layout or artwork.</summary>
    [AddComponentMenu("Beast Soccer/Menu Controller (player and rival selection)")]
    public sealed class MenuController : MonoBehaviour
    {
        public GameObject root;
        public GameObject modeScreen;
        public GameObject pickScreen;
        public GameObject rivalScreen;
        public Text infoTitle;
        public Text infoBody;
        public CharacterType selectedPlayer = CharacterType.Volt;
        public CharacterType selectedRival = CharacterType.Leo;
        public bool hasPlayerSelection;
        public bool hasRivalSelection;
        private MainMenuUI menu;

        public void Initialize(MainMenuUI owner)
        {
            menu = owner;
            hasPlayerSelection = false;
            hasRivalSelection = false;
            root.SetActive(true);
            ConfigureButtonJuice();
            bool pickOnLoad = MatchSetup.OpenCharacterSelectOnLoad;
            MatchSetup.OpenCharacterSelectOnLoad = false;
            if (pickOnLoad) ShowPick();
            else ShowMode();
        }

        public void ShowMode() => Show(modeScreen);
        public void ShowPick()
        {
            Show(pickScreen);
            Toggle(pickScreen, "YOUR_VOLT", hasPlayerSelection && selectedPlayer == CharacterType.Volt);
            Toggle(pickScreen, "YOUR_LEO", hasPlayerSelection && selectedPlayer == CharacterType.Leo);
            if (!hasPlayerSelection)
            {
                infoTitle.text = "SELECT YOUR BEAST";
                infoBody.text = "Choose Volt or Leo.\n\nGoro guards both goals.\n\nAGUILAR\nOrange home kit\n\nALIANZA\nBlue away kit";
            }
        }

        public void ShowRival()
        {
            if (!hasPlayerSelection) return;
            Show(rivalScreen);
            hasRivalSelection = false;
            Toggle(rivalScreen, "RIVAL_VOLT", false);
            Toggle(rivalScreen, "RIVAL_LEO", false);
            Toggle(rivalScreen, "START_MATCH", false);
            Toggle(rivalScreen, "START_MATCH_ART", false);
            RefreshYourCardOnRival();
        }

        public void BackToMode() => ShowMode();
        public void BackToPlayerPick() => ShowPick();
        public void PickVolt() => SelectPlayer(CharacterType.Volt);
        public void PickLeo() => SelectPlayer(CharacterType.Leo);
        public void RivalVolt() => SelectRival(CharacterType.Volt);
        public void RivalLeo() => SelectRival(CharacterType.Leo);

        private void SelectPlayer(CharacterType character)
        {
            selectedPlayer = character;
            hasPlayerSelection = true;
            infoTitle.text = character == CharacterType.Volt ? "VOLT — THE SKY STRIKER" : "LEO — THE PRIDE";
            infoBody.text = character == CharacterType.Volt
                ? "ROLE: ATTACKER\n\nSPEED      ■■■■■\nSHOOT      ■■■■□\nSTRENGTH   ■■□□□\nPASS       ■■■□□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nFLY / SKY BURST\nMassive jump with hang time.\nFast, direct attacking play."
                : "ROLE: ATTACKER\n\nSPEED      ■■■■□\nSHOOT      ■■■■□\nSTRENGTH   ■■■□□\nPASS       ■■■■□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nPRIDE RUSH\nFast playmaker with improved\npassing and attacking speed.";
            ShowRival();
        }

        private void SelectRival(CharacterType character)
        {
            selectedRival = character;
            hasRivalSelection = true;
            Toggle(rivalScreen, "RIVAL_VOLT", character == CharacterType.Volt);
            Toggle(rivalScreen, "RIVAL_LEO", character == CharacterType.Leo);
            Toggle(rivalScreen, "START_MATCH", true);
            Toggle(rivalScreen, "START_MATCH_ART", true);
        }

        public void StartSelectedMatch()
        {
            if (!hasPlayerSelection || !hasRivalSelection) return;
            selectedPlayer = DuelRules.Outfielder(selectedPlayer, CharacterType.Volt);
            selectedRival = DuelRules.Outfielder(selectedRival, CharacterType.Leo);
            menu.StartMatch(selectedPlayer, selectedRival);
        }

        public void RefreshYourCardOnRival()
        {
            Toggle(rivalScreen, "YOUR_VOLT_R", hasPlayerSelection && selectedPlayer == CharacterType.Volt);
            Toggle(rivalScreen, "YOUR_LEO_R", hasPlayerSelection && selectedPlayer == CharacterType.Leo);
        }

        private void Show(GameObject page)
        {
            modeScreen.SetActive(page == modeScreen);
            pickScreen.SetActive(page == pickScreen);
            rivalScreen.SetActive(page == rivalScreen);
        }

        private void ConfigureButtonJuice()
        {
            AddJuice(modeScreen, "QUICK_MATCH", "QUICK_MATCH_ART");
            AddJuice(pickScreen, "VOLT_PICK", "VOLT_CARD");
            AddJuice(pickScreen, "LEO_PICK", "LEO_CARD");
            AddJuice(pickScreen, "BACK_PICK", "BACK_PICK");
            AddJuice(rivalScreen, "VOLT_PICK", "VOLT_CARD");
            AddJuice(rivalScreen, "LEO_PICK", "LEO_CARD");
            AddJuice(rivalScreen, "START_MATCH", "START_MATCH_ART");
            AddJuice(rivalScreen, "BACK", "BACK");
        }

        private static void AddJuice(GameObject page, string buttonName, string visualName)
        {
            var button = page.transform.Find(buttonName);
            if (button == null || button.GetComponent<Button>() == null) return;
            var visual = page.transform.Find(visualName) as RectTransform;
            var juice = button.GetComponent<MenuButtonJuice>();
            if (juice == null) juice = button.gameObject.AddComponent<MenuButtonJuice>();
            juice.SetTarget(visual != null ? visual : button as RectTransform);
        }

        private static void Toggle(GameObject page, string name, bool active)
        {
            var child = page.transform.Find(name);
            if (child != null) child.gameObject.SetActive(active);
        }
    }
}
