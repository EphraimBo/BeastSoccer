using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BeastSoccer.Upgrades
{
    public interface IProgressionFiles
    {
        bool Exists(int slot);
        string Read(int slot);
        void Write(int slot, string content);
    }

    public sealed class DiskProgressionFiles : IProgressionFiles
    {
        private readonly string directory;
        public DiskProgressionFiles(string directory) { this.directory = directory; }
        private string PathFor(int slot) => Path.Combine(directory, "progression-v1-" + slot + ".json");
        public bool Exists(int slot) => File.Exists(PathFor(slot));
        public string Read(int slot) => File.ReadAllText(PathFor(slot), Encoding.UTF8);
        public void Write(int slot, string content)
        {
            Directory.CreateDirectory(directory);
            string path = PathFor(slot), temp = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            // Only replace the older slot. The latest valid slot remains intact through this write.
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }

    [Serializable]
    public sealed class ProgressionData
    {
        public int version = 1;
        public long revision;
        public int coins, completedMatches;
        public string[] ownedPitches = { "base" };
        public AIDifficulty difficulty = AIDifficulty.Normal;
        public string activeMatchId = "", completedMatchId = "";
        public int lastHomeGoal, goalsPaid, ultGoalsPaid, tacklesPaid;
        public int matchGoalCoins, matchTackleCoins, matchCompletionCoins, matchResultCoins;
        public int MatchCoins => matchGoalCoins + matchTackleCoins + matchCompletionCoins + matchResultCoins;
    }

    [Serializable]
    internal sealed class SaveEnvelope { public string payload, sha256; }

    /// <summary>One durable transaction for each reward/purchase. Two checksummed save slots, newest valid wins.</summary>
    public sealed class ProgressionStore
    {
        private readonly IProgressionFiles files;
        public ProgressionData Data { get; private set; }
        public ProgressionStore(IProgressionFiles files)
        {
            this.files = files;
            bool any = false;
            for (int slot = 0; slot < 2; slot++)
            {
                if (!files.Exists(slot)) continue;
                any = true;
                try
                {
                    var envelope = JsonUtility.FromJson<SaveEnvelope>(files.Read(slot));
                    if (envelope == null || envelope.payload == null || Hash(envelope.payload) != envelope.sha256) continue;
                    var candidate = JsonUtility.FromJson<ProgressionData>(envelope.payload);
                    if (!Valid(candidate)) continue;
                    if (Data == null || candidate.revision > Data.revision) Data = candidate;
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException) { }
            }
            // Existing unreadable/future saves are preserved; never overwrite them with zero progress.
            if (Data == null && any) throw new InvalidDataException("No readable progression save. Both original files were preserved.");
            if (Data == null) Data = new ProgressionData();
        }
        private static bool Valid(ProgressionData d) => d != null && d.version == 1 && d.revision >= 0 &&
            d.coins >= 0 && d.completedMatches >= 0 && d.ownedPitches != null && Array.IndexOf(d.ownedPitches, "base") >= 0;
        private static string Hash(string value)
        {
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        private ProgressionData Copy() => JsonUtility.FromJson<ProgressionData>(JsonUtility.ToJson(Data));
        private void Commit(ProgressionData next)
        {
            next.revision = checked(Data.revision + 1);
            string payload = JsonUtility.ToJson(next);
            files.Write((int)(next.revision % 2), JsonUtility.ToJson(new SaveEnvelope { payload = payload, sha256 = Hash(payload) }));
            Data = next; // Publish only after the durable write succeeds.
        }
        public bool Owns(string pitch) => Array.IndexOf(Data.ownedPitches, pitch) >= 0;
        public bool Purchase(PitchDefinition pitch)
        {
            if (pitch == null || string.IsNullOrEmpty(pitch.id) || pitch.coinCost < 0 || pitch.completedMatches < 0 ||
                Owns(pitch.id) || Data.completedMatches < pitch.completedMatches || Data.coins < pitch.coinCost) return false;
            var next = Copy(); next.coins -= pitch.coinCost;
            Array.Resize(ref next.ownedPitches, next.ownedPitches.Length + 1);
            next.ownedPitches[next.ownedPitches.Length - 1] = pitch.id;
            Commit(next); return true;
        }
        public void SetDifficulty(AIDifficulty difficulty)
        {
            if (Data.difficulty == difficulty || !Enum.IsDefined(typeof(AIDifficulty), difficulty)) return;
            var next = Copy(); next.difficulty = difficulty; Commit(next);
        }
        public void BeginMatch(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.activeMatchId == id) return;
            var next = Copy(); next.activeMatchId = id; next.lastHomeGoal = 0;
            next.goalsPaid = next.ultGoalsPaid = next.tacklesPaid = 0;
            next.matchGoalCoins = next.matchTackleCoins = next.matchCompletionCoins = next.matchResultCoins = 0;
            Commit(next);
        }
        private bool Live(string id) => !string.IsNullOrEmpty(id) && Data.activeMatchId == id && Data.completedMatchId != id;
        public bool Goal(string id, int homeScore, bool ultimate, UpgradeSettings settings)
        {
            if (!Live(id) || homeScore <= Data.lastHomeGoal) return false;
            var next = Copy(); next.lastHomeGoal = homeScore;
            int gain = 0;
            if (next.goalsPaid < Mathf.Max(0, settings.goalCap)) { gain += Mathf.Max(0, settings.goalCoins); next.goalsPaid++; }
            if (ultimate && next.ultGoalsPaid < Mathf.Max(0, settings.ultGoalCap))
            { gain += Mathf.Max(0, settings.ultGoalBonus); next.ultGoalsPaid++; }
            next.coins = checked(next.coins + gain); next.matchGoalCoins += gain; Commit(next); return true;
        }
        public bool Tackle(string id, UpgradeSettings settings)
        {
            if (!Live(id) || Data.tacklesPaid >= Mathf.Max(0, settings.tackleCap)) return false;
            var next = Copy(); int gain = Mathf.Max(0, settings.tackleCoins);
            next.tacklesPaid++; next.coins = checked(next.coins + gain); next.matchTackleCoins += gain; Commit(next); return true;
        }
        public bool Complete(string id, int home, int away, UpgradeSettings settings)
        {
            if (!Live(id)) return false;
            var next = Copy(); next.completedMatchId = id;
            next.matchCompletionCoins = Mathf.Max(0, settings.matchCoins);
            next.matchResultCoins = Mathf.Max(0, home > away ? settings.winCoins : home == away ? settings.drawCoins : 0);
            next.coins = checked(next.coins + next.matchCompletionCoins + next.matchResultCoins);
            next.completedMatches = checked(next.completedMatches + 1); Commit(next); return true;
        }
        public void Flush() => Commit(Copy());
    }

    public static class Progression
    {
        private static ProgressionStore store;
        private static bool attempted;
        public static string Error { get; private set; }
        public static event Action Changed;
        public static ProgressionStore Store
        {
            get
            {
                if (!attempted)
                {
                    attempted = true;
                    try { store = new ProgressionStore(new DiskProgressionFiles(Path.Combine(Application.persistentDataPath, "BeastSoccerProgression"))); }
                    catch (Exception ex) { Error = ex.Message; Debug.LogError("Progression load failed: " + Error); }
                }
                return store;
            }
        }
        public static bool Apply(Action<ProgressionStore> transaction)
        {
            if (Store == null) return false;
            try { transaction(store); Error = null; Changed?.Invoke(); return true; }
            catch (Exception ex) { Error = ex.Message; Debug.LogError("Progression save failed: " + Error); Changed?.Invoke(); return false; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { store = null; attempted = false; Error = null; Changed = null; }
    }
}
