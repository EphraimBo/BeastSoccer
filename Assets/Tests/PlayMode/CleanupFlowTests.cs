using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BeastSoccer.Ball;
using BeastSoccer.CameraSystem;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.UI;

namespace BeastSoccer.Tests
{
    public class CleanupFlowTests
    {
        private bool previousUpgrades;
        [SetUp] public void SelectBaseFeatureConfiguration()
        {
            var settings = BeastSoccer.Upgrades.UpgradeSettings.Current;
            previousUpgrades = settings.enableUpgrades;
            settings.enableUpgrades = false;
        }
        [TearDown] public void RestoreFeatureConfiguration()
        {
            BeastSoccer.Upgrades.UpgradeSettings.Current.enableUpgrades = previousUpgrades;
        }

        [UnityTest]
        public IEnumerator ChangePlayersOpensSelectionAndConsumesRequest()
        {
            MatchSetup.OpenCharacterSelectOnLoad = true;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var menu = Object.FindFirstObjectByType<MenuController>();
            Assert.IsTrue(menu.pickScreen.activeSelf);
            Assert.IsFalse(menu.modeScreen.activeSelf);
            Assert.IsFalse(MatchSetup.OpenCharacterSelectOnLoad);
            Assert.IsNotNull(menu.transform.Find("Fullscreen Backdrop (covers screen edges)"));
            Assert.IsNotNull(menu.transform.Find("Menu Artwork Backdrop (behind menu)"));
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<MenuController>().modeScreen.activeSelf);
        }

        [UnityTest]
        public IEnumerator CameraKeepsApprovedFramingWithoutGlobalPatches()
        {
            MatchSetup.HasExplicitSelection = false;
            yield return SceneManager.LoadSceneAsync("Match");
            yield return new WaitForSeconds(1.4f);
            Camera camera = Camera.main;
            var follow = camera.GetComponent<FollowCamera>();
            var config = GameConfig.Instance;
            var safe = SafeAreaRoot.NormalizedSafeArea();
            var view = Rect.MinMaxRect(safe.xMin + safe.width * .035f, safe.yMin + safe.height * .16f,
                safe.xMax - safe.width * .035f, safe.yMax - safe.height * .13f);
            var bounds = new Bounds(new Vector3(0f, 1.6f, 0f),
                new Vector3(config.pitchLength + 4.4f, 3.2f, config.pitchWidth + 2.4f));
            Vector3 expected = FollowCamera.FitBoundsPosition(bounds,
                Quaternion.Euler(config.cameraAngle, 0f, 0f), 46f, camera.aspect, view);
            expected.y -= 4f;
            expected = bounds.center + (expected - bounds.center) * .8f * .78f;
            follow.SnapToBall();
            Assert.That(Vector3.Distance(expected, camera.transform.position), Is.LessThan(.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(39.5f).Within(.001f));
            Assert.That(config.matchRealSeconds, Is.EqualTo(195f));
        }

        [UnityTest]
        public IEnumerator LargerTeamKickoffControlsAndHalftimeRemainAvailable()
        {
            MatchSetup.HasExplicitSelection = false;
            void ConfigureTeamMode(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "Match") return;
                GameConfig.Instance.arcadeDuel = false;
                GameManager.Instance.PlayerCharacter = CharacterType.Leo;
                GameManager.Instance.OpponentCharacter = CharacterType.Volt;
                Object.FindFirstObjectByType<MatchHudLayout>().ApplyMode(false);
            }
            SceneManager.sceneLoaded += ConfigureTeamMode;
            try { yield return SceneManager.LoadSceneAsync("Match"); }
            finally { SceneManager.sceneLoaded -= ConfigureTeamMode; }
            yield return new WaitForSeconds(1.4f);
            Assert.AreEqual(4, TeamManager.Instance.HomeTeam.Count);
            Assert.AreEqual(4, TeamManager.Instance.AwayTeam.Count);
            Assert.AreEqual(MatchPhase.Kickoff, GameManager.Instance.Phase);
            var ui = Object.FindFirstObjectByType<MatchUI>();
            Assert.IsTrue(ui.secondaryButton.gameObject.activeInHierarchy);
            Assert.IsTrue(ui.switchButton.gameObject.activeInHierarchy);
            Assert.IsFalse(ui.GetComponent<DuelHud>().enabled);
            TeamManager.Instance.CurrentHuman().Pass();
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(MatchPhase.Playing, GameManager.Instance.Phase);
            GameManager.Instance.BeginHalfTime();
            GameManager.Instance.ContinueFromHalfTime();
            Assert.AreEqual(-1, TeamManager.Instance.HomeAttackDir);
            Assert.AreEqual(2, GameManager.Instance.CurrentHalf);
        }

        [UnityTest]
        public IEnumerator GoalWinsAgainstQueuedEndLineRestart()
        {
            MatchSetup.HasExplicitSelection = false;
            yield return SceneManager.LoadSceneAsync("Match");
            yield return new WaitForSeconds(1.4f);
            foreach (var player in TeamManager.Instance.AllPlayers)
            {
                player.GetComponent<BeastSoccer.AI.AIController>().enabled = false;
                player.StopImmediately();
            }
            var resolver = MatchBoundaryResolver.Instance;
            var ball = BallControl.Instance;
            var endLine = GameObject.Find("Out_PosX").GetComponent<BoundaryTrigger>();
            var goal = GameObject.Find("Goal_PosX").GetComponent<GoalTrigger>();
            ball.TeleportFree(new Vector2(goal.goalLineX + .1f, 0f));
            resolver.RequestRestart(endLine, ball);
            Assert.IsTrue(resolver.TryScore(ball));
            Assert.AreEqual(1, ScoreManager.Instance.HomeScore);
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(1, ScoreManager.Instance.HomeScore);
            Assert.AreEqual(MatchPhase.GoalScored, GameManager.Instance.Phase);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            MatchSetup.HasExplicitSelection = false;
            MatchSetup.OpenCharacterSelectOnLoad = false;
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }
    }
}
