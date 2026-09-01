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
                if(idle!=null)renderer.sprite=idle;
                renderer.color=type==CharacterType.Generic?(side==TeamSide.Home?new Color(.55f,.72f,1f,1f):new Color(1f,.58f,.58f,1f)):Color.white;
                renderer.sortingOrder=50; // Sprite presentation stays visually above debug ground indicators.
            }
            if (driver != null)
            {
                driver.ConfigureDirectionalPrototype(type, controller);
                driver.RefreshParameters();
            }
            // FIX32: Volt gets a scene-safe runtime sprite override for FLY/BLOCK art.
            // Other characters never receive this component, keeping their animation wiring isolated.
            if (type == CharacterType.Volt)
            {
                var ultArt = GetComponent<VoltUltSpriteOverride>();
                if (ultArt == null) ultArt = gameObject.AddComponent<VoltUltSpriteOverride>();
                ultArt.visual = GetComponent<PlayerVisualProxy>();
                ultArt.targetRenderer = renderer;
            }
            else
            {
                var staleUltArt = GetComponent<VoltUltSpriteOverride>();
                if (staleUltArt != null) Object.Destroy(staleUltArt);
            }
            if(placeholderToDisable!=null&&(controller!=null||idle!=null))placeholderToDisable.SetActive(false);
        }
    }
}
