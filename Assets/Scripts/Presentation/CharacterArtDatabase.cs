using UnityEngine;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// The only place you need to hook up character sprite animation assets.
    /// Assign one Animator Controller (and optional idle sprite) per character type.
    /// </summary>
    public class CharacterArtDatabase : MonoBehaviour
    {
        public RuntimeAnimatorController leoController;
        public RuntimeAnimatorController goroController;
        public RuntimeAnimatorController voltController;
        public RuntimeAnimatorController genericController;
        public Sprite leoIdleSprite;
        public Sprite goroIdleSprite;
        public Sprite voltIdleSprite;
        public Sprite genericIdleSprite;
    }
}
