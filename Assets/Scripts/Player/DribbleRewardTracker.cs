using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class DribbleRewardTracker : MonoBehaviour
    {
        private PlayerController player;
        private PlayerController candidate;
        private float candidateStart;
        private float cooldownUntil;
        private void Awake(){player=GetComponent<PlayerController>();}
        private void Update()
        {
            if(Time.time<cooldownUntil || player==null || !player.HasBall || GameManager.Instance==null || GameManager.Instance.Phase!=MatchPhase.Playing){candidate=null;return;}
            int dir=TeamManager.Instance.AttackDirFor(player.Side);
            if(candidate==null)
            {
                TeamSide other=player.Side==TeamSide.Home?TeamSide.Away:TeamSide.Home;
                foreach(var p in TeamManager.Instance.Team(other))
                {
                    if(p==null||p.Role==FieldRole.Goalkeeper)continue;
                    if(Vector2.Distance(transform.position,p.transform.position)<1.25f){candidate=p;candidateStart=transform.position.x*dir;break;}
                }
            }
            else
            {
                if(!player.HasBall){candidate=null;return;}
                float progress=transform.position.x*dir-candidateStart;
                float behind=(transform.position.x-candidate.transform.position.x)*dir;
                if(progress>0.85f && behind>0.55f)
                {
                    TeamManager.Instance?.AddTeamUltCharge(player.Side, GameConfig.Instance.ultChargePerAction);
                    cooldownUntil=Time.time+1.8f;candidate=null;
                }
                else if(Vector2.Distance(transform.position,candidate.transform.position)>3f)candidate=null;
            }
        }
    }
}
