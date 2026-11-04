using UnityEngine;

namespace BeastSoccer.Upgrades
{
    public enum AIDifficulty { Easy, Normal, Hard, GGs }

    [CreateAssetMenu(menuName = "Beast Soccer/Upgrade Settings")]
    public sealed class UpgradeSettings : ScriptableObject
    {
        private static UpgradeSettings cached;
        public static UpgradeSettings Current => cached != null ? cached : (cached = Resources.Load<UpgradeSettings>("UpgradeSettings"));
        public bool enableUpgrades = true;
        public bool enableMenus = true;
        public bool enableRewards = true;
        public bool enablePitchSkins = true;
        public bool enableTacticalAI = true;
        public bool enableReadableKeepers = true;
        public bool showKeeperDebug;
        [Range(.05f, 1f)] public float unownedMapBrightness = .16f;
        public int matchCoins = 50, winCoins = 25, drawCoins = 10;
        public int goalCoins = 5, goalCap = 4, ultGoalBonus = 5, ultGoalCap = 2;
        public int tackleCoins = 1, tackleCap = 5;
        public PitchDefinition[] pitches;
        public DifficultyProfile[] difficulties;
        public KeeperProfile homeKeeper = new KeeperProfile();
        public bool Active => enableUpgrades;
        public static bool Menus => Current != null && Current.Active && Current.enableMenus;
        public static bool Rewards => Current != null && Current.Active && Current.enableRewards;
        public static bool TacticalAI => Current != null && Current.Active && Current.enableTacticalAI;
        public static bool Keepers => Current != null && Current.Active && Current.enableReadableKeepers;
        public PitchDefinition Pitch(string id)
        {
            if (pitches != null) foreach (var pitch in pitches) if (pitch != null && pitch.id == id) return pitch;
            return null;
        }
        public DifficultyProfile Profile(AIDifficulty difficulty)
        {
            if (difficulties != null) foreach (var profile in difficulties)
                if (profile != null && profile.difficulty == difficulty) return profile;
            return new DifficultyProfile();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;
    }

    [System.Serializable]
    public sealed class PitchDefinition
    {
        public string id, displayName;
        public int completedMatches, coinCost;
        public Texture2D texture;
        public Sprite preview;
    }

    [System.Serializable]
    public sealed class KeeperProfile
    {
        public float reactionSeconds = .15f;
        public float trackingInterval = .12f;
        public float moveSpeed = 3.6f;
        public float standingReach = .4f;
        public float diveDistance = 1.15f;
        public float diveSeconds = .25f;
        public float recoverySeconds = .65f;
        public float maxCatchHeight = 1.1f;
    }

    [System.Serializable]
    public sealed class DifficultyProfile
    {
        public AIDifficulty difficulty = AIDifficulty.Normal;
        public float decisionSeconds = .22f;
        public float lookAheadSeconds = .65f;
        [Range(5, 13)] public int directionSamples = 9;
        public float predictionWeight = .7f;
        public float aimError = .24f;
        public float tackleCooldown = .8f;
        public float ultMinimumBenefit = 1.8f;
        public float shotMinimumQuality = .2f;
        public KeeperProfile keeper = new KeeperProfile();
    }
}
