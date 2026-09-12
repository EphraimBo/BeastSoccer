using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Player;

namespace BeastSoccer.Core
{
    // One shared geometric rule for the button, AI, shot release and pitch overlay.
    public static class DuelRules
    {
        public static bool Enabled => GameConfig.Instance == null || GameConfig.Instance.arcadeDuel;
        public static float ShotRadius => GameConfig.Instance != null ? GameConfig.Instance.shootingZoneRadius : 7.0f;

        public static bool InsideZone(Vector2 position, int attackDir, float pitchLength, float radius)
        {
            Vector2 goal = new Vector2(attackDir * pitchLength * .5f, 0f);
            return position.x * attackDir <= pitchLength * .5f &&
                   (position - goal).sqrMagnitude <= radius * radius;
        }

        public static bool CanScoreFrom(PlayerController player)
        {
            if (player == null || player.Role == FieldRole.Goalkeeper) return false;
            if (!Enabled) return true;
            int dir = TeamManager.Instance != null ? TeamManager.Instance.AttackDirFor(player.Side) : (player.Side == TeamSide.Home ? 1 : -1);
            return InsideZone(player.transform.position, dir, GameConfig.Instance.pitchLength, ShotRadius);
        }

        public static CharacterType Outfielder(CharacterType requested, CharacterType fallback)
            => requested == CharacterType.Leo || requested == CharacterType.Volt ? requested : fallback;
    }
}
