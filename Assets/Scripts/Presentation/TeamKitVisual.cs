using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    [DefaultExecutionOrder(1300)]
    public class TeamKitVisual : MonoBehaviour
    {
        public SpriteRenderer target;
        public CharacterType character;
        public TeamSide side;
        private Material kit;
        public static Material CreateMaterial(CharacterType type, TeamSide team)
        {
            var shader = Resources.Load<Shader>("TeamKit");
            if (shader == null) return null;
            var material = new Material(shader);
            material.SetFloat("_Character", type == CharacterType.Goro ? 1 : type == CharacterType.Volt ? 2 : 0);
            material.SetFloat("_Away", team == TeamSide.Away ? 1 : 0);
            return material;
        }
        private void LateUpdate()
        {
            if (target == null || target.sprite == null) return;
            if (kit == null) kit = CreateMaterial(character,side);
            if (kit == null) return;
            var s=target.sprite;
            var r=s.rect;
            kit.SetVector("_SpriteRect",new Vector4(r.x/s.texture.width,r.y/s.texture.height,r.width/s.texture.width,r.height/s.texture.height));
            kit.SetFloat("_Character", character == CharacterType.Goro ? 1 : character == CharacterType.Volt ? 2 : 0);
            kit.SetFloat("_Away", side == TeamSide.Away ? 1 : 0);
            bool keeper = s.texture.name == "GoroKeeper";
            kit.SetFloat("_KeyMagenta", keeper ? 1 : 0);
            if (keeper && int.TryParse(s.name,out int pose)) kit.SetFloat("_Pose",pose);
            target.sharedMaterial=kit;
        }
        private void OnDestroy() { if(kit != null) Destroy(kit); }
    }
}
