using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.UI;
using BeastSoccer.Data;

namespace BeastSoccer.Upgrades
{
    /// <summary>Only controls authored screens and bindings; all layout remains editable in MainMenu.unity.</summary>
    public sealed class ExpandedMenuController : MonoBehaviour
    {
        public GameObject expandedRoot, legacyRoot;
        public GameObject homePage, beastPage, rivalPage, mapPage, storePage;
        public Text walletText, titleText, matchupText, mapSelectionText, storeName, storeRequirement, storePrice, storeNotice, storeBackLabel;
        public RawImage storePreview;
        public Button storeButton, selectMapButton, buyButton;
        public Text buyLabel;
        public PitchTileView[] mapTiles, storeTiles;
        public UpgradeButtonFeedback[] difficultyButtons;
        public UpgradeButtonFeedback[] rivalButtons;
        private MainMenuUI owner;
        private GameObject page;
        private bool expanded, hasPlayer, hasRival, returnToMaps;
        private CharacterType player = CharacterType.Volt, rival = CharacterType.Leo;
        private AIDifficulty difficulty = AIDifficulty.Normal;
        private string selectedPitch = "base", focusedPitch = "lion", notice;
        private float displayedCoins;
        public void Initialize(MainMenuUI menu)
        {
            owner = menu;
            Progression.Changed += Refresh;
            var save = UpgradeSettings.Menus ? Progression.Store : null;
            if (save != null) { difficulty = save.Data.difficulty; displayedCoins = save.Data.coins; }
            for (int i = 0; i < mapTiles.Length; i++) { int index = i; mapTiles[i].button.onClick.AddListener(() => PickMap(index)); }
            for (int i = 0; i < storeTiles.Length; i++) { int index = i + 1; storeTiles[i].button.onClick.AddListener(() => Focus(index)); }
            for (int i = 0; i < difficultyButtons.Length; i++) { int index = i; difficultyButtons[i].GetComponent<Button>().onClick.AddListener(() => SetDifficulty(index)); }
            ApplyMode();
            if (!expanded) return;
            if (MatchSetup.HasExplicitSelection)
            {
                player = MatchSetup.PlayerCharacter; rival = MatchSetup.OpponentCharacter;
                difficulty = MatchSetup.Difficulty; selectedPitch = MatchSetup.PitchId;
                hasPlayer = hasRival = true;
            }
            if (MatchSetup.OpenStoreOnLoad) { returnToMaps = false; Show(storePage); }
            else if (MatchSetup.OpenMapSelectOnLoad && hasRival) Show(mapPage);
            else if (MatchSetup.OpenCharacterSelectOnLoad) Show(beastPage);
            else Show(homePage);
            MatchSetup.OpenStoreOnLoad = MatchSetup.OpenMapSelectOnLoad = MatchSetup.OpenCharacterSelectOnLoad = false;
        }
        private void ApplyMode()
        {
            expanded = UpgradeSettings.Menus;
            expandedRoot.SetActive(expanded); legacyRoot.SetActive(!expanded);
            if (!expanded) GetComponent<MenuController>().Initialize(owner);
            else Show(homePage);
        }
        private void Update()
        {
            if (owner == null) return;
            if (expanded != UpgradeSettings.Menus) ApplyMode();
            if (!expanded) return;
            int coins = Progression.Store != null ? Progression.Store.Data.coins : 0;
            displayedCoins = Mathf.MoveTowards(displayedCoins, coins, Mathf.Max(20f, Mathf.Abs(coins - displayedCoins) * 14f) * Time.unscaledDeltaTime);
            walletText.text = Progression.Store != null ? "Coins " + Mathf.RoundToInt(displayedCoins).ToString("N0") : "Progress unavailable";
        }
        public void Home() => Show(homePage);
        public void QuickMatch() => Show(beastPage);
        public void PickVolt() => PickPlayer(CharacterType.Volt);
        public void PickLeo() => PickPlayer(CharacterType.Leo);
        private void PickPlayer(CharacterType value) { player = value; hasPlayer = true; hasRival = false; Show(rivalPage); }
        public void RivalVolt() { rival = CharacterType.Volt; hasRival = true; Refresh(); }
        public void RivalLeo() { rival = CharacterType.Leo; hasRival = true; Refresh(); }
        public void BackToBeasts() => Show(beastPage);
        public void BackToRivals() => Show(rivalPage);
        public void SelectMap() { if (hasPlayer && hasRival) Show(mapPage); }
        private void SetDifficulty(int index)
        {
            var chosen = (AIDifficulty)index;
            if (Progression.Store != null && !Progression.Apply(s => s.SetDifficulty(chosen))) { Refresh(); return; }
            difficulty = chosen; Refresh();
        }
        private void PickMap(int index)
        {
            var pitch = UpgradeSettings.Current.pitches[index]; var save = Progression.Store;
            if (pitch.id == "base" || save != null && save.Owns(pitch.id)) { selectedPitch = pitch.id; Refresh(); return; }
            if (save == null || save.Data.completedMatches < pitch.completedMatches) return;
            focusedPitch = pitch.id; OpenStore();
        }
        public void OpenStore()
        {
            returnToMaps = page == mapPage; notice = null; Show(storePage);
        }
        public void StoreBack() => Show(returnToMaps ? mapPage : homePage);
        private void Focus(int index) { focusedPitch = UpgradeSettings.Current.pitches[index].id; notice = null; Refresh(); }
        public void Buy()
        {
            var pitch = UpgradeSettings.Current.Pitch(focusedPitch); bool bought = false;
            if (Progression.Apply(s => bought = s.Purchase(pitch))) notice = bought ? pitch.displayName + " purchased." : "Purchase unavailable.";
            Refresh();
        }
        public void StartMatch()
        {
            if (!hasPlayer || !hasRival) return;
            var save = Progression.Store;
            if (selectedPitch != "base" && (save == null || !save.Owns(selectedPitch))) selectedPitch = "base";
            MatchSetup.Difficulty = difficulty; MatchSetup.PitchId = selectedPitch;
            owner.StartMatch(player, rival);
        }
        private void Show(GameObject target)
        {
            page = target;
            homePage.SetActive(page == homePage); beastPage.SetActive(page == beastPage);
            rivalPage.SetActive(page == rivalPage); mapPage.SetActive(page == mapPage); storePage.SetActive(page == storePage);
            Refresh();
        }
        private void Refresh()
        {
            if (!expanded || UpgradeSettings.Current == null) return;
            var save = Progression.Store; var settings = UpgradeSettings.Current;
            storeButton.gameObject.SetActive(page != storePage);
            selectMapButton.interactable = hasPlayer && hasRival;
            titleText.text = page == homePage ? "QUICK MATCH" : page == beastPage ? "YOUR BEAST" :
                page == rivalPage ? "RIVAL + DIFFICULTY" : page == mapPage ? "SELECT MAP" : "PITCH STORE";
            matchupText.text = player.ToString().ToUpperInvariant() + " vs " + rival.ToString().ToUpperInvariant() +
                "   ·   " + difficulty.ToString().ToUpperInvariant() + "   ·   " + (save != null ? save.Data.completedMatches : 0) + " MATCHES COMPLETED";
            for (int i = 0; i < difficultyButtons.Length; i++) difficultyButtons[i].Select((int)difficulty == i);
            if (rivalButtons.Length == 2) { rivalButtons[0].Select(hasRival && rival == CharacterType.Volt); rivalButtons[1].Select(hasRival && rival == CharacterType.Leo); }
            for (int i = 0; i < mapTiles.Length && i < settings.pitches.Length; i++) mapTiles[i].Refresh(settings.pitches[i], false, selectedPitch);
            for (int i = 0; i < storeTiles.Length && i + 1 < settings.pitches.Length; i++) storeTiles[i].Refresh(settings.pitches[i + 1], true, focusedPitch);
            mapSelectionText.text = (settings.Pitch(selectedPitch)?.displayName ?? "Original Pitch") + " selected";
            var pitch = settings.Pitch(focusedPitch);
            if (pitch == null) return;
            bool owned = save != null && save.Owns(pitch.id), eligible = save != null && save.Data.completedMatches >= pitch.completedMatches;
            bool afford = save != null && save.Data.coins >= pitch.coinCost;
            storePreview.texture = pitch.texture; storeName.text = pitch.displayName.ToUpperInvariant();
            storeRequirement.text = eligible ? "MATCH REQUIREMENT MET" : (save != null ? save.Data.completedMatches : 0) + " / " + pitch.completedMatches + " MATCHES";
            storePrice.text = pitch.coinCost + " COINS";
            buyButton.interactable = !owned && eligible && afford;
            buyLabel.text = owned ? "OWNED" : !eligible ? "PLAY " + pitch.completedMatches + " MATCHES" : afford ? "BUY PITCH" : "NEED " + (pitch.coinCost - save.Data.coins) + " COINS";
            storeNotice.text = Progression.Error ?? notice ?? (owned ? "Available on Select Map" : "Complete the matches, then buy with coins");
            storeBackLabel.text = returnToMaps ? "BACK TO MAPS" : "MAIN MENU";
        }
        private void OnDestroy() => Progression.Changed -= Refresh;
    }
}
