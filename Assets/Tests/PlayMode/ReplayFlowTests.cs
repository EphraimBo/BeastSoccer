using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BeastSoccer.Ball;
using BeastSoccer.CameraSystem;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Player;
using BeastSoccer.Replay;
using BeastSoccer.UI;
using BeastSoccer.Upgrades;

namespace BeastSoccer.Tests
{
    public sealed class ReplayFlowTests
    {
        private bool previousMaster, previousReplay, previousRewards;
        [SetUp] public void EnableReplayWithoutWritingPersonalProgress()
        {
            var settings = UpgradeSettings.Current;
            previousMaster = settings.enableUpgrades; previousReplay = settings.enableCinematicReplays; previousRewards = settings.enableRewards;
            settings.enableUpgrades = settings.enableCinematicReplays = true; settings.enableRewards = false;
        }
        private IEnumerator LoadMatch()
        {
            Time.timeScale = 1; MatchSetup.HasExplicitSelection = false;
            yield return SceneManager.LoadSceneAsync("Match");
            yield return new WaitForSeconds(1.4f);
            Assert.That(GameManager.Instance.Phase, Is.EqualTo(MatchPhase.Playing));
            foreach (var p in TeamManager.Instance.AllPlayers)
            { p.GetComponent<BeastSoccer.AI.AIController>().enabled = false; p.StopImmediately(); }
            yield return new WaitForSeconds(.15f);
            CinematicReplay.Instance.goalAnnouncementSeconds = 0;
        }
        private void Goal()
        {
            var ball = BallControl.Instance;
            ball.TeleportFree(new Vector2(GameConfig.Instance.pitchLength * .5f + .1f, .3f));
            ScoreManager.Instance.ScoreGoal(TeamSide.Home, TeamManager.Instance.SpecialFor(TeamSide.Home));
        }
        private IEnumerator WaitForPlayback(bool active)
        {
            float until = Time.realtimeSinceStartup + 2;
            while (CinematicReplay.Instance.IsPlaying != active && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(CinematicReplay.Instance.IsPlaying, Is.EqualTo(active));
        }
        [UnityTest] public IEnumerator SkipRestoresCameraHUDAndClockWithoutScoringAgain()
        {
            yield return LoadMatch();
            var replay = CinematicReplay.Instance; var camera = Camera.main; var follow = camera.GetComponent<FollowCamera>();
            float fov = camera.fieldOfView; Goal();
            yield return WaitForPlayback(true);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(replay.gameplayHUD.alpha, Is.Zero);
            Assert.That(replay.replayUI.activeInHierarchy, Is.True); Assert.That(follow.enabled, Is.False);
            var copies = replay.transform.Find("Replay Visual Copies (no gameplay components)");
            Assert.That(copies, Is.Not.Null); Assert.That(copies.GetComponentsInChildren<PlayerController>(true), Is.Empty);
            Assert.That(copies.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(copies.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(copies.GetComponentsInChildren<MatchRewards>(true), Is.Empty);
            var cameraPosition = camera.transform.position; float elapsed = MatchTimer.Instance.ElapsedTotal;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition)); Assert.That(MatchTimer.Instance.ElapsedTotal, Is.EqualTo(elapsed));
            Assert.That(ScoreManager.Instance.HomeScore, Is.EqualTo(1));
            replay.Skip(); yield return WaitForPlayback(false);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(replay.gameplayHUD.alpha, Is.EqualTo(1));
            Assert.That(replay.replayUI.activeSelf, Is.False); Assert.That(follow.enabled, Is.True);
            Assert.That(camera.fieldOfView, Is.EqualTo(fov)); Assert.That(ScoreManager.Instance.HomeScore, Is.EqualTo(1));
            yield return new WaitForSeconds(1.4f);
            Assert.That(GameManager.Instance.Phase, Is.EqualTo(MatchPhase.Playing));
        }
        [UnityTest] public IEnumerator SwitchingModuleOffDuringPlaybackRestoresPresentation()
        {
            yield return LoadMatch(); Goal(); yield return WaitForPlayback(true);
            UpgradeSettings.Current.enableCinematicReplays = false;
            yield return WaitForPlayback(false);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(CinematicReplay.Instance.gameplayHUD.alpha, Is.EqualTo(1));
            Assert.That(Camera.main.GetComponent<FollowCamera>().enabled, Is.True);
            Assert.That(CinematicReplay.Instance.replayUI.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator DisabledReplayUsesExistingGoalSequence()
        {
            UpgradeSettings.Current.enableCinematicReplays = false;
            yield return LoadMatch(); Goal();
            for (int i = 0; i < 20; i++)
            {
                Assert.That(CinematicReplay.Instance.IsPlaying, Is.False);
                Assert.That(CinematicReplay.Instance.gameplayHUD.alpha, Is.EqualTo(1));
                yield return null;
            }
            Assert.That(ScoreManager.Instance.HomeScore, Is.EqualTo(1));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (CinematicReplay.Instance != null) CinematicReplay.Instance.Skip();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            Time.timeScale = 1;
            var settings = UpgradeSettings.Current;
            settings.enableUpgrades = previousMaster; settings.enableCinematicReplays = previousReplay; settings.enableRewards = previousRewards;
        }
    }
}
