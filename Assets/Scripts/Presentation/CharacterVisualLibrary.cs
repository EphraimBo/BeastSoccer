using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    public class CharacterVisualLibrary : MonoBehaviour
    {
        public CharacterArtDatabase database;
        public GameObject placeholderToDisable;

        public void Apply(CharacterType type, TeamSide side, SpriteRenderer renderer, Animator animator, CharacterAnimationDriver driver)
        {
            if (type != CharacterType.Generic && renderer != null)
            {
                // Approved PNGs own the sprite. Stop legacy animation/material writers rather
                // than loading them and overwriting their output every frame.
                if (animator != null)
                {
                    animator.enabled = false;
                    animator.runtimeAnimatorController = null;
                }
                if (driver != null)
                {
                    driver.ConfigureDirectionalPrototype(type, null);
                    driver.RefreshParameters();
                }
                DisableLegacy<TeamKitVisual>();
                DisableLegacy<GoroKeeperVisual>();
                DisableLegacy<GoroSpriteOverride>();
                DisableLegacy<VoltUltSpriteOverride>();
                var approvedArt = GetComponent<DemoCharacterArtV8>();
                if (approvedArt == null) approvedArt = gameObject.AddComponent<DemoCharacterArtV8>();
                approvedArt.enabled = true;
                approvedArt.Configure(GetComponent<PlayerVisualProxy>(), renderer);
                // In the shipped scenes the sprite and visual proxy share a Transform:
                // V8 ultimately wrote 1 for outfielders and 1.2 for Goro. Scale those
                // effective values exactly once; repeated ApplyCharacter calls cannot stack it.
                renderer.transform.localScale = Vector3.one * (type == CharacterType.Goro ? 1.2f : 1f)
                    * BeastSoccer.Core.DemoMatchRules.ArtworkScaleBoost;
                renderer.sprite = DemoCharacterArtV8.First(type, side,
                    type == CharacterType.Goro ? "Idle_Ready_ThreeQuarter" : "Run_Side");
                renderer.color = Color.white;
                renderer.sortingOrder = 50;
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                var glow = GetComponent<UltGlowEffect>();
                if (glow == null) glow = gameObject.AddComponent<UltGlowEffect>();
                glow.visual = GetComponent<PlayerVisualProxy>();
                glow.targetRenderer = renderer;
                glow.character = type;
                if (placeholderToDisable != null) placeholderToDisable.SetActive(false);
                return;
            }
            if(database==null) database=Object.FindAnyObjectByType<CharacterArtDatabase>();
            if(database==null) return;
            RuntimeAnimatorController controller=null;Sprite idle=null;
            switch(type)
            {
                case CharacterType.Leo:controller=database.leoController;idle=database.leoIdleSprite;break;
                case CharacterType.Goro:controller=database.goroController;idle=database.goroIdleSprite;break;
                case CharacterType.Volt:controller=database.voltController;idle=database.voltIdleSprite;break;
                default:controller=database.genericController;idle=database.genericIdleSprite;break;
            }
            if(animator!=null && controller!=null)animator.runtimeAnimatorController=controller;
            if(renderer!=null)
            {
                var kit = GetComponent<TeamKitVisual>();
                if (kit == null) kit = gameObject.AddComponent<TeamKitVisual>();
                kit.target = renderer; kit.character = type; kit.side = side;
                var keeperArt = GetComponent<GoroKeeperVisual>();
                if (keeperArt == null) keeperArt = gameObject.AddComponent<GoroKeeperVisual>();
                keeperArt.visual = GetComponent<PlayerVisualProxy>();
                var approvedArt = GetComponent<DemoCharacterArtV8>();
                if (approvedArt == null) approvedArt = gameObject.AddComponent<DemoCharacterArtV8>();
                approvedArt.Configure(GetComponent<PlayerVisualProxy>(), renderer);
                if(idle!=null)renderer.sprite=idle;
                renderer.color=type==CharacterType.Generic?(side==TeamSide.Home?new Color(.55f,.72f,1f,1f):new Color(1f,.58f,.58f,1f)):Color.white;
                renderer.sortingOrder=50; // Sprite presentation stays visually above debug ground indicators.
            }
            // FIX34A: character-specific presentation scale. This changes only the real sprite renderer,
            // never the shared player/gameplay root. Leo and Volt remain unchanged; Goro is intentionally larger.
            if (renderer != null)
            {
                renderer.transform.localScale = type == CharacterType.Goro
                    ? UnityEngine.Vector3.one * 2.4f
                    : UnityEngine.Vector3.one;
            }

            if (driver != null)
            {
                driver.ConfigureDirectionalPrototype(type, controller);
                driver.RefreshParameters();
            }
            // Runtime sprite overrides keep each character's temporary art isolated.
            if (type == CharacterType.Volt)
            {
                var voltArt = GetComponent<VoltUltSpriteOverride>();
                if (voltArt == null) voltArt = gameObject.AddComponent<VoltUltSpriteOverride>();
                voltArt.visual = GetComponent<PlayerVisualProxy>();
                voltArt.targetRenderer = renderer;
                var staleGoro = GetComponent<GoroSpriteOverride>();
                if (staleGoro != null) Object.Destroy(staleGoro);
            }
            else if (type == CharacterType.Goro)
            {
                var goroArt = GetComponent<GoroSpriteOverride>();
                if (goroArt == null) goroArt = gameObject.AddComponent<GoroSpriteOverride>();
                goroArt.visual = GetComponent<PlayerVisualProxy>();
                goroArt.targetRenderer = renderer;
                var staleVolt = GetComponent<VoltUltSpriteOverride>();
                if (staleVolt != null) Object.Destroy(staleVolt);
            }
            else
            {
                var staleVolt = GetComponent<VoltUltSpriteOverride>();
                if (staleVolt != null) Object.Destroy(staleVolt);
                var staleGoro = GetComponent<GoroSpriteOverride>();
                if (staleGoro != null) Object.Destroy(staleGoro);
            }
            // FIX35: every real character receives the same lightweight ult glow presentation.
            // It duplicates the current sprite behind itself at low alpha, so no new glow sprites are required.
            if (type != CharacterType.Generic && renderer != null)
            {
                var glow = GetComponent<UltGlowEffect>();
                if (glow == null) glow = gameObject.AddComponent<UltGlowEffect>();
                glow.visual = GetComponent<PlayerVisualProxy>();
                glow.targetRenderer = renderer;
                glow.character = type;
            }
            else
            {
                var staleGlow = GetComponent<UltGlowEffect>();
                if (staleGlow != null) Object.Destroy(staleGlow);
            }

            // FIX34A: Leo, Volt and Goro all use exactly the same placeholder rule.
            // The debug capsule is a prototype fallback only, so every real named character hides it
            // regardless of whether its current art comes from an Animator Controller or a sprite override.
            if (placeholderToDisable != null)
                placeholderToDisable.SetActive(type == CharacterType.Generic);
        }
        private void DisableLegacy<T>() where T : Behaviour
        {
            var component = GetComponent<T>();
            if (component != null) component.enabled = false;
        }
    }
}
