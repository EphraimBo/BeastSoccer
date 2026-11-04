using BeastSoccer.Data;
using UnityEngine;
using BeastSoccer.Upgrades;

namespace BeastSoccer.UI
{
    /// <summary>Explicit launch choices carried across menu and match scene loads.</summary>
    public static class MatchSetup
    {
        public static GameMode Mode = GameMode.Regular;
        public static CharacterType PlayerCharacter = CharacterType.Leo;
        public static CharacterType OpponentCharacter = CharacterType.Volt;
        public static bool OpponentRandom;
        public static bool OpenCharacterSelectOnLoad;
        public static bool HasExplicitSelection;
        public static AIDifficulty Difficulty = AIDifficulty.Normal;
        public static string PitchId = "base";
        public static bool OpenStoreOnLoad, OpenMapSelectOnLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Mode = GameMode.Regular;
            PlayerCharacter = CharacterType.Leo;
            OpponentCharacter = CharacterType.Volt;
            OpponentRandom = OpenCharacterSelectOnLoad = HasExplicitSelection = false;
            Difficulty = AIDifficulty.Normal; PitchId = "base";
            OpenStoreOnLoad = OpenMapSelectOnLoad = false;
        }
    }
}
