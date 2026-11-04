using System.Collections.Generic;
using UnityEngine;
using BeastSoccer.Presentation;
using BeastSoccer.UI;

namespace BeastSoccer.Upgrades
{
    [DefaultExecutionOrder(500)]
    public sealed class SelectedPitchPresentation : MonoBehaviour
    {
        public PitchPresentation presentation;
        private readonly Dictionary<Renderer, MaterialPropertyBlock> original = new Dictionary<Renderer, MaterialPropertyBlock>();
        private bool applied;
        private void Start() => Refresh();
        private void Update() => Refresh();
        private void Refresh()
        {
            bool active = UpgradeSettings.Current != null && UpgradeSettings.Current.Active && UpgradeSettings.Current.enablePitchSkins;
            if (!active) { if (applied) Restore(); return; }
            if (applied || presentation == null || MatchSetup.PitchId == "base") return;
            var pitch = UpgradeSettings.Current.Pitch(MatchSetup.PitchId);
            if (pitch == null || pitch.texture == null || Progression.Store == null || !Progression.Store.Owns(pitch.id)) return;
            foreach (var surface in presentation.pitchRenderers)
            {
                if (surface == null || surface.sharedMaterial == null) continue;
                var block = new MaterialPropertyBlock(); surface.GetPropertyBlock(block); original[surface] = block;
                var edited = new MaterialPropertyBlock(); surface.GetPropertyBlock(edited);
                if (surface.sharedMaterial.HasProperty("_BaseMap")) edited.SetTexture("_BaseMap", pitch.texture);
                if (surface.sharedMaterial.HasProperty("_MainTex")) edited.SetTexture("_MainTex", pitch.texture);
                surface.SetPropertyBlock(edited);
            }
            applied = true;
        }
        private void Restore() { foreach (var entry in original) if (entry.Key != null) entry.Key.SetPropertyBlock(entry.Value); original.Clear(); applied = false; }
        private void OnDisable() => Restore();
    }
}
