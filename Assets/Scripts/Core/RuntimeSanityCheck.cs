using UnityEngine;
using BeastSoccer.Ball;
using BeastSoccer.Data;

namespace BeastSoccer.Core
{
    public class RuntimeSanityCheck : MonoBehaviour
    {
        private System.Collections.IEnumerator Start()
        {
            yield return null; // GameManager.Start must finish configuring the roster first.
            bool ok=true;
            ok &= Check(GameConfig.Instance!=null,"GameConfig missing");
            ok &= Check(GameManager.Instance!=null,"GameManager missing");
            ok &= Check(TeamManager.Instance!=null,"TeamManager missing");
            ok &= Check(BallControl.Instance!=null,"BallControl missing");
            ok &= Check(RoundReset.Instance!=null,"RoundReset missing");
            if(TeamManager.Instance!=null)
            {
                if (DuelRules.Enabled)
                {
                    ok &= Check(TeamManager.Instance.HomeTeam.Count == 2, "Home roster must contain one outfielder and Goro");
                    ok &= Check(TeamManager.Instance.AwayTeam.Count == 2, "Away roster must contain one outfielder and Goro");
                    foreach (TeamSide side in new[] {TeamSide.Home, TeamSide.Away})
                    {
                        var keeper = TeamManager.Instance.GoalkeeperFor(side);
                        var field = TeamManager.Instance.SpecialFor(side);
                        ok &= Check(keeper != null && keeper.Character == CharacterType.Goro, side + " Goro goalkeeper missing");
                        ok &= Check(field != null && field.Character != CharacterType.Goro, side + " outfielder must be Leo or Volt");
                    }
                    if (ok) Debug.Log("[Beast Soccer] Duel ready: two outfielders, two Goro keepers, ball and match systems present.");
                    yield break;
                }
                ok &= Check(TeamManager.Instance.HomeTeam.Count==4,"Home roster must contain exactly 4 bodies (3 outfield + GK)");
                ok &= Check(TeamManager.Instance.AwayTeam.Count==4,"Away roster must contain exactly 4 bodies (3 outfield + GK)");
                ok &= Check(TeamManager.Instance.GoalkeeperFor(TeamSide.Home)!=null,"Home goalkeeper missing");
                ok &= Check(TeamManager.Instance.GoalkeeperFor(TeamSide.Away)!=null,"Away goalkeeper missing");
                ok &= Check(TeamManager.Instance.RoleFor(TeamSide.Home,TacticalRole.Anchor)!=null,"Home anchor missing");
                ok &= Check(TeamManager.Instance.RoleFor(TeamSide.Home,TacticalRole.Presser)!=null,"Home presser missing");
                ok &= Check(TeamManager.Instance.RoleFor(TeamSide.Home,TacticalRole.Rover)!=null,"Home rover missing");
                ok &= Check(TeamManager.Instance.RoleFor(TeamSide.Away,TacticalRole.Anchor)!=null,"Away anchor missing");
                ok &= Check(TeamManager.Instance.RoleFor(TeamSide.Away,TacticalRole.Presser)!=null,"Away presser missing");
                ok &= Check(TeamManager.Instance.RoleFor(TeamSide.Away,TacticalRole.Rover)!=null,"Away rover missing");
                ok &= Check(TeamManager.Instance.SpecialFor(TeamSide.Home)!=null,"Home special character missing");
                ok &= Check(TeamManager.Instance.SpecialFor(TeamSide.Away)!=null,"Away special character missing");
            }
            if(ok)Debug.Log("[Beast Soccer] Runtime sanity check passed: 3+1, tactical roles, ball, and systems present.");
        }

        private bool Check(bool condition,string message)
        {
            if(!condition)Debug.LogError("[Beast Soccer] "+message);
            return condition;
        }
    }
}
