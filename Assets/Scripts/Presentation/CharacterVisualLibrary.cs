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
            if(placeholderToDisable!=null&&(controller!=null||idle!=null))placeholderToDisable.SetActive(false);
        }
    }
}
