using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;

namespace BeastSoccer.AI
{
    [RequireComponent(typeof(UltimateAbility),typeof(PlayerController))]
    public class AIUltUsage : MonoBehaviour
    {
        public bool allowAutoUlt = false;
        private UltimateAbility ult;
        private PlayerController player;
        private float nextCheck;

        private void Awake(){ult=GetComponent<UltimateAbility>();player=GetComponent<PlayerController>();}

        private void Update()
        {
            if (player != null && player.Role == FieldRole.Goalkeeper) return;
            // Deliberately hard-disabled unless BOTH the global debug switch and this component opt in.
            // Normal prototype play never fires AI ults on its own.
            if(GameConfig.Instance==null || !GameConfig.Instance.enableAIAutoUlts || !allowAutoUlt) return;
            if(player==null || player.IsHuman || GameManager.Instance==null || GameManager.Instance.Phase!=MatchPhase.Playing || ult==null || ult.IsActive || ult.IsActivating || ult.Charge<0.999f || Time.time<nextCheck) return;
            nextCheck=Time.time+0.6f;
            if(Random.value<GameConfig.Instance.aiUltAggression) ult.TryActivate();
        }
    }
}
