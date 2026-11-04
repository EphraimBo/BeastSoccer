using UnityEngine;
using UnityEngine.UI;

namespace BeastSoccer.Upgrades
{
    public sealed class PitchTileView : MonoBehaviour
    {
        public Button button;
        public RawImage thumbnail;
        public Text nameText, matchesText, coinsText, statusText;
        public GameObject selectedMarker;
        public void Refresh(PitchDefinition pitch, bool storeTile, string selected)
        {
            var save = Progression.Store;
            bool owned = pitch.id == "base" || save != null && save.Owns(pitch.id);
            int games = save != null ? save.Data.completedMatches : 0;
            bool eligible = games >= pitch.completedMatches;
            nameText.text = pitch.displayName;
            thumbnail.texture = pitch.texture;
            float brightness = owned ? 1f : UpgradeSettings.Current.unownedMapBrightness;
            thumbnail.color = new Color(brightness, brightness, brightness, 1f);
            matchesText.text = owned ? "" : pitch.completedMatches + " matches";
            coinsText.text = owned ? "" : pitch.coinCost + " coins";
            statusText.text = !storeTile && selected == pitch.id ? "SELECTED" : owned ? "OWNED" :
                eligible ? "Purchase required" : (pitch.completedMatches - games) + " more matches";
            button.interactable = storeTile || owned || eligible;
            if (selectedMarker != null) selectedMarker.SetActive(selected == pitch.id);
        }
    }
}
