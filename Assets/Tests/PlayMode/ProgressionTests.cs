using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using BeastSoccer.Upgrades;

namespace BeastSoccer.Tests
{
    public sealed class ProgressionTests
    {
        private sealed class Files : IProgressionFiles
        {
            public readonly string[] slots = new string[2];
            public bool fail;
            public bool Exists(int slot) => slots[slot] != null;
            public string Read(int slot) => slots[slot];
            public void Write(int slot, string content) { if (fail) throw new IOException("Disk full"); slots[slot] = content; }
        }
        private UpgradeSettings settings;
        [SetUp] public void Setup() => settings = ScriptableObject.CreateInstance<UpgradeSettings>();
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(settings);
        [Test] public void RewardsSurviveReloadAndCompletionCannotPayTwice()
        {
            var files = new Files(); var store = new ProgressionStore(files);
            store.BeginMatch("match");
            Assert.That(store.Goal("match", 1, true, settings), Is.True);
            Assert.That(store.Goal("match", 1, true, settings), Is.False);
            Assert.That(store.Tackle("match", settings), Is.True);
            store = new ProgressionStore(files);
            Assert.That(store.Data.coins, Is.EqualTo(11));
            Assert.That(store.Data.completedMatches, Is.Zero);
            Assert.That(store.Complete("match", 1, 0, settings), Is.True);
            Assert.That(store.Complete("match", 1, 0, settings), Is.False);
            Assert.That(store.Goal("match", 2, false, settings), Is.False);
            var loaded = new ProgressionStore(files);
            Assert.That(loaded.Data.coins, Is.EqualTo(86));
            Assert.That(loaded.Data.completedMatches, Is.EqualTo(1));
            Assert.That(loaded.Data.MatchCoins, Is.EqualTo(86));
        }
        [Test] public void ActionCapsAndAbandonedMatchesKeepOnlyBankedCoins()
        {
            var store = new ProgressionStore(new Files()); store.BeginMatch("one");
            for (int n = 1; n <= 12; n++) { store.Goal("one", n, true, settings); store.Tackle("one", settings); }
            Assert.That(store.Data.coins, Is.EqualTo(35));
            store.BeginMatch("two");
            Assert.That(store.Data.coins, Is.EqualTo(35));
            Assert.That(store.Data.completedMatches, Is.Zero);
            Assert.That(store.Data.MatchCoins, Is.Zero);
            Assert.That(store.Complete("one", 20, 0, settings), Is.False);
            Assert.That(store.Complete("two", 0, 0, settings), Is.True);
            Assert.That(store.Data.coins, Is.EqualTo(95));
        }
        [Test] public void PurchaseRequiresBothGatesAndIsAtomicAndUnique()
        {
            var files = new Files(); var store = new ProgressionStore(files);
            var pitch = new PitchDefinition { id = "lion", completedMatches = 3, coinCost = 200 };
            for (int n = 0; n < 2; n++) { store.BeginMatch(n.ToString()); store.Complete(n.ToString(), 1, 0, settings); }
            Assert.That(store.Purchase(pitch), Is.False); // Matches gate.
            store.BeginMatch("three"); store.Complete("three", 1, 0, settings);
            pitch.coinCost = 500; Assert.That(store.Purchase(pitch), Is.False); // Coin gate.
            pitch.coinCost = 200; files.fail = true;
            Assert.Throws<IOException>(() => store.Purchase(pitch));
            Assert.That(store.Data.coins, Is.EqualTo(225)); Assert.That(store.Owns("lion"), Is.False);
            files.fail = false; Assert.That(store.Purchase(pitch), Is.True);
            Assert.That(store.Purchase(pitch), Is.False);
            store = new ProgressionStore(files);
            Assert.That(store.Owns("lion"), Is.True); Assert.That(store.Data.coins, Is.EqualTo(25));
        }
        [Test] public void CorruptNewestFallsBackAndTwoCorruptFilesArePreserved()
        {
            var files = new Files(); var store = new ProgressionStore(files);
            store.BeginMatch("one"); store.Goal("one", 1, false, settings); store.Tackle("one", settings);
            files.slots[(int)(store.Data.revision % 2)] = "broken";
            Assert.That(new ProgressionStore(files).Data.coins, Is.EqualTo(5));
            files.slots[0] = files.slots[1] = "broken";
            Assert.Throws<InvalidDataException>(() => new ProgressionStore(files));
            Assert.That(files.slots, Is.EqualTo(new[] { "broken", "broken" }));
        }
        [Test] public void FailedWriteLeavesPublishedBalanceUnchanged()
        {
            var files = new Files(); var store = new ProgressionStore(files); store.BeginMatch("one");
            files.fail = true; Assert.Throws<IOException>(() => store.Goal("one", 1, true, settings));
            Assert.That(store.Data.coins, Is.Zero); Assert.That(store.Data.lastHomeGoal, Is.Zero);
            files.fail = false; Assert.That(store.Goal("one", 1, true, settings), Is.True);
            Assert.That(store.Data.coins, Is.EqualTo(10));
        }
        [Test] public void DiskSlotsRoundTripInPersistentStyleDirectory()
        {
            string dir = Path.Combine(Path.GetTempPath(), "beast-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ProgressionStore(new DiskProgressionFiles(dir)); store.BeginMatch("disk");
                store.Goal("disk", 1, true, settings); store.Complete("disk", 1, 0, settings);
                Assert.That(new ProgressionStore(new DiskProgressionFiles(dir)).Data.coins, Is.EqualTo(85));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
