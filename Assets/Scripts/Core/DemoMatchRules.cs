using BeastSoccer.Player;

namespace BeastSoccer.Core
{
    // Compatibility with the previously distributed V2 patch. Scene lifecycle now belongs
    // to GameManager/MainMenuUI; no second bootstrap or reflection-based runtime overrides.
    public static class DemoMatchRules
    {
        public const float MatchSeconds = 75f;
        public const float ShootingZoneDepth = 7.0f;
        // The save zone used the V8 scene's .75-second hold. Add 2.5 seconds to that.
        public const float KeeperHoldSeconds = 3.25f;
        public const float DemoCameraVisibleFraction = 1.02f;
        public const float BallScale = 1.98f;
        public const float PlayerScale = 1.20f;
        public const float ArtworkScaleBoost = 1.284f; // V9 size * 1.07.
        public const float UltMovementMultiplier = 1.45f; // 16% faster than the previous 1.25x.
        public const float BallSpeedBoost = 1.85f;
        public static bool Enabled => DuelRules.Enabled;
        public static bool IsInsideShootingZone(PlayerController player) => DuelRules.CanScoreFrom(player);
    }
}
