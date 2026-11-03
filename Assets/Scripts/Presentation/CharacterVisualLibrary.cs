using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    /// <summary>Assigns approved named-character art, or the generic larger-team fallback.</summary>
    public sealed class CharacterVisualLibrary : MonoBehaviour
    {
        public CharacterArtDatabase database;
        public GameObject placeholderToDisable;

        public void Apply(CharacterType type, TeamSide side, SpriteRenderer renderer,
            Animator animator, CharacterAnimationDriver driver)
        {
            if (renderer == null) return;
            bool named = type != CharacterType.Generic;
            var spriteAnimation = GetComponent<CharacterSpriteAnimator>();
            var glow = GetComponent<UltGlowEffect>();
            if (animator != null)
            {
                if (!named && database == null) database = Object.FindAnyObjectByType<CharacterArtDatabase>();
                animator.runtimeAnimatorController = named || database == null ? null : database.genericController;
                animator.enabled = !named && animator.runtimeAnimatorController != null;
            }
            driver?.RefreshParameters();
            if (named)
            {
                if (spriteAnimation == null) spriteAnimation = gameObject.AddComponent<CharacterSpriteAnimator>();
                spriteAnimation.enabled = true;
                spriteAnimation.Configure(GetComponent<PlayerVisualProxy>(), renderer);
                renderer.transform.localScale = Vector3.one * (type == CharacterType.Goro ? 1.2f : 1f)
                    * GameConfig.Instance.characterArtworkScale;
                renderer.sprite = CharacterSpriteAnimator.First(type, side,
                    type == CharacterType.Goro ? "Idle_Ready_ThreeQuarter" : "Run_Side");
                renderer.color = Color.white;
                if (glow == null) glow = gameObject.AddComponent<UltGlowEffect>();
                glow.enabled = true;
                glow.visual = GetComponent<PlayerVisualProxy>();
                glow.targetRenderer = renderer;
                glow.character = type;
            }
            else
            {
                if (spriteAnimation != null) spriteAnimation.enabled = false;
                if (glow != null) Object.Destroy(glow);
                renderer.transform.localScale = Vector3.one;
                renderer.sprite = database != null ? database.genericIdleSprite : null;
                renderer.color = side == TeamSide.Home ? new Color(.55f, .72f, 1f) : new Color(1f, .58f, .58f);
            }
            renderer.sortingOrder = 50;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            if (placeholderToDisable != null) placeholderToDisable.SetActive(!named);
        }
    }
}
