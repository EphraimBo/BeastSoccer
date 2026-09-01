#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Ball;
using BeastSoccer.Data;
using BeastSoccer.Presentation;

namespace BeastSoccer.EditorTools
{
    public static class PrototypeValidator
    {
        [MenuItem("Beast Soccer/Validate Open Match Scene")]
        public static void Validate()
        {
            int errors=0;
            errors+=Need(Object.FindAnyObjectByType<GameConfig>()!=null,"GameConfig");
            var tm=Object.FindAnyObjectByType<TeamManager>();
            errors+=Need(tm!=null,"TeamManager");
            errors+=Need(Object.FindAnyObjectByType<GameManager>()!=null,"GameManager");
            errors+=Need(Object.FindAnyObjectByType<BallControl>()!=null,"BallControl");
            errors+=Need(Object.FindAnyObjectByType<RoundReset>()!=null,"RoundReset");
            errors+=Need(Object.FindAnyObjectByType<CharacterArtDatabase>()!=null,"CharacterArtDatabase");
            if(tm!=null)
            {
                errors+=Need(tm.HomeTeam.Count==4,"Home roster = 4 bodies (3 outfield + GK)");
                errors+=Need(tm.AwayTeam.Count==4,"Away roster = 4 bodies (3 outfield + GK)");
                errors+=Need(tm.GoalkeeperFor(TeamSide.Home)!=null,"Home GK");
                errors+=Need(tm.GoalkeeperFor(TeamSide.Away)!=null,"Away GK");
                errors+=Need(tm.RoleFor(TeamSide.Home,TacticalRole.Anchor)!=null,"Home Anchor");
                errors+=Need(tm.RoleFor(TeamSide.Home,TacticalRole.Presser)!=null,"Home Presser");
                errors+=Need(tm.RoleFor(TeamSide.Home,TacticalRole.Rover)!=null,"Home Rover");
                errors+=Need(tm.RoleFor(TeamSide.Away,TacticalRole.Anchor)!=null,"Away Anchor");
                errors+=Need(tm.RoleFor(TeamSide.Away,TacticalRole.Presser)!=null,"Away Presser");
                errors+=Need(tm.RoleFor(TeamSide.Away,TacticalRole.Rover)!=null,"Away Rover");
            }
            if(errors==0)Debug.Log("[Beast Soccer] Open Match scene validation passed.");
            else Debug.LogError($"[Beast Soccer] Validation found {errors} issue(s). See messages above.");
        }

        private static int Need(bool ok,string label)
        {
            if(!ok){Debug.LogError("[Beast Soccer] Missing/invalid: "+label);return 1;}
            return 0;
        }
    }
}
#endif
