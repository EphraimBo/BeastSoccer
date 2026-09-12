using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Ball;
using BeastSoccer.UI;
using BeastSoccer.Presentation;

namespace BeastSoccer.Tests
{
    public class DuelFlowTests
    {
        [TestCase(4f/3f, 0f, .025f)]
        [TestCase(16f/9f, 0f, 0f)]
        [TestCase(19.5f/9f, .05f, .04f)]
        [TestCase(20f/9f, .06f, .04f)]
        [TestCase(21f/9f, .06f, .04f)]
        public void CompletePitchFitsPhoneAndTabletViewports(float aspect, float sideInset, float bottomInset)
        {
            var go = new GameObject("ViewportTestCamera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 46f;
                camera.aspect = aspect;
                camera.transform.rotation = Quaternion.Euler(50, 0, 0);
                Rect safe = Rect.MinMaxRect(sideInset, bottomInset, 1-sideInset, 1);
                Rect view = Rect.MinMaxRect(safe.xMin + safe.width*.035f, safe.yMin + safe.height*.16f,
                    safe.xMax - safe.width*.035f, safe.yMax - safe.height*.13f);
                var bounds = new Bounds(new Vector3(0,1.6f,0), new Vector3(28.4f,3.2f,18.26f));
                camera.transform.position = BeastSoccer.CameraSystem.FollowCamera.FitBoundsPosition(
                    bounds, camera.transform.rotation, camera.fieldOfView, aspect, view);
                for (int i=0; i<8; i++)
                {
                    Vector3 corner = bounds.center + new Vector3(
                        (i&1)==0 ? -bounds.extents.x : bounds.extents.x,
                        (i&2)==0 ? -bounds.extents.y : bounds.extents.y,
                        (i&4)==0 ? -bounds.extents.z : bounds.extents.z);
                    var screen = camera.WorldToViewportPoint(corner);
                    Assert.Greater(screen.z, camera.nearClipPlane);
                    Assert.That(screen.x, Is.InRange(view.xMin,view.xMax));
                    Assert.That(screen.y, Is.InRange(view.yMin,view.yMax));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(5.5f,0f,true)]
        [TestCase(5.49f,0f,false)]
        [TestCase(8f,2f,true)]
        [TestCase(6f,6f,false)]
        [TestCase(12.1f,0f,false)]
        public void ZoneIsSymmetricAndMatchesTheArc(float x,float y,bool expected)
        {
            Assert.AreEqual(expected,DuelRules.InsideZone(new Vector2(x,y),1,24,6.5f));
            Assert.AreEqual(expected,DuelRules.InsideZone(new Vector2(-x,y),-1,24,6.5f));
        }

        [UnityTest]
        public IEnumerator MenuToMatchAndKeeperThrowStayLive()
        {
            Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var menu=Object.FindAnyObjectByType<BeastMenuOverhaul>();
            Assert.IsNotNull(menu);
            menu.ShowPick();menu.PickGoro();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var voltCard = menu.pickScreen.transform.Find("VOLT_CARD") as RectTransform;
            var leoCard = menu.pickScreen.transform.Find("LEO_CARD") as RectTransform;
            Assert.LessOrEqual(voltCard.rect.width, 320.1f);
            Assert.LessOrEqual(voltCard.rect.height, 156.1f);
            Assert.LessOrEqual(leoCard.rect.width, 320.1f);
            Assert.LessOrEqual(leoCard.rect.height, 156.1f);
            Assert.Greater(Mathf.Abs(voltCard.anchoredPosition.y - leoCard.anchoredPosition.y),
                (voltCard.rect.height + leoCard.rect.height) * .5f);
            Assert.IsFalse(menu.hasPlayerSelection,"Goro cannot be picked as an outfielder.");
            Assert.IsFalse(menu.pickScreen.transform.Find("GORO_PICK").gameObject.activeInHierarchy);
            menu.PickVolt();menu.ShowRival();menu.RivalGoro();
            Assert.IsFalse(menu.hasRivalSelection);
            menu.RivalLeo();menu.StartSelectedMatch();
            yield return null;
            yield return new WaitForSeconds(1.4f);
            var teams=TeamManager.Instance;
            Assert.AreEqual(2,teams.HomeTeam.Count);
            Assert.AreEqual(2,teams.AwayTeam.Count);
            Assert.AreEqual(MatchPhase.Playing,GameManager.Instance.Phase,"Kickoff must not wait for a missing teammate.");
            Assert.AreEqual(1,teams.HomeAttackDir);
            var human=teams.CurrentHuman();
            Assert.AreEqual(CharacterType.Volt,human.Character);
            Assert.IsNotNull(Object.FindAnyObjectByType<DuelHud>());
            Canvas.ForceUpdateCanvases();
            var hud = Object.FindAnyObjectByType<MatchUI>();
            Rect safePixels = Screen.safeArea;
            var corners = new Vector3[4];
            foreach (var control in new RectTransform[] {
                hud.primaryButton.transform as RectTransform, hud.ultimateButton.transform as RectTransform,
                hud.scoreText.rectTransform, hud.timerText.rectTransform,
                hud.ultMeterFill.transform.parent as RectTransform,
                Object.FindAnyObjectByType<BeastSoccer.Input.JoystickInput>().background })
            {
                control.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var pixel = RectTransformUtility.WorldToScreenPoint(null,corner);
                    Assert.That(pixel.x, Is.InRange(safePixels.xMin-.5f,safePixels.xMax+.5f));
                    Assert.That(pixel.y, Is.InRange(safePixels.yMin-.5f,safePixels.yMax+.5f));
                }
            }
            foreach(TeamSide side in new[]{TeamSide.Home,TeamSide.Away})
            {
                var keeper=teams.GoalkeeperFor(side);
                Assert.AreEqual(CharacterType.Goro,keeper.Character);
                Assert.IsNotNull(keeper.Visual.GetComponent<DemoCharacterArtV8>());
                Assert.IsFalse(keeper.Visual.animator.enabled);
                Assert.IsNull(keeper.Visual.animator.runtimeAnimatorController);
                var legacy = keeper.Visual.GetComponent<GoroKeeperVisual>();
                Assert.IsTrue(legacy == null || !legacy.enabled);
                Assert.That(keeper.Visual.spriteRenderer.transform.localScale.x, Is.EqualTo(1.5408f).Within(.001f));
                keeper.Visual.ApplyCharacter();
                Assert.That(keeper.Visual.spriteRenderer.transform.localScale.x, Is.EqualTo(1.5408f).Within(.001f));
                keeper.GainBall();
                float timeBefore=MatchTimer.Instance.ElapsedTotal;
                yield return new WaitForSeconds(2.8f);
                Assert.IsTrue(keeper.HasBall,"Goro must keep the additional post-save hold.");
                yield return new WaitForSeconds(.8f);
                Assert.AreEqual(MatchPhase.Playing,GameManager.Instance.Phase);
                Assert.Greater(MatchTimer.Instance.ElapsedTotal,timeBefore);
                Assert.AreSame(human,teams.CurrentHuman());
                Assert.IsFalse(keeper.HasBall,"A save must lead to a throw, not permanent possession.");
            }
            Assert.That(human.Visual.spriteRenderer.transform.localScale.x, Is.EqualTo(1.284f).Within(.001f));
            human.Visual.ApplyCharacter();
            Assert.That(human.Visual.spriteRenderer.transform.localScale.x, Is.EqualTo(1.284f).Within(.001f));
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.name.StartsWith("Pitch_Artwork"))
                    Assert.That(Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0,180,0)), Is.LessThan(.01f));
            GameManager.Instance.PauseMatch();
            float paused=MatchTimer.Instance.ElapsedTotal;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(paused,MatchTimer.Instance.ElapsedTotal);
            GameManager.Instance.ResumeMatch();
            Assert.AreEqual(MatchPhase.Playing,GameManager.Instance.Phase);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator KeeperOutletsStayInsidePitchWithMovingReceivers()
        {
            Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync("Match");
            yield return new WaitForSeconds(1.4f);
            var teams=TeamManager.Instance;
            var ball=BallControl.Instance;
            foreach(var p in teams.AllPlayers)
            {
                p.StopImmediately();
                p.enabled=false;
                var ai=p.GetComponent<BeastSoccer.AI.AIController>();
                if(ai!=null) ai.enabled=false;
            }
            float halfLength=GameConfig.Instance.pitchLength*.5f;
            float halfWidth=GameConfig.Instance.pitchWidth*.5f;
            foreach(TeamSide side in new[]{TeamSide.Home,TeamSide.Away})
            foreach(float lane in new[]{-1f,1f})
            {
                var keeper=teams.GoalkeeperFor(side);
                BeastSoccer.Player.PlayerController receiver=null;
                foreach(var p in teams.Team(side)) if(p.Role!=FieldRole.Goalkeeper) receiver=p;
                Assert.IsNotNull(receiver);
                keeper.TeleportTo(new Vector2(-teams.AttackDirFor(side)*(halfLength-1.25f),0));
                Vector2 target=new Vector2(teams.AttackDirFor(side)*(halfLength-1f),lane*(halfWidth-.85f));
                receiver.TeleportTo(target);
                keeper.GainBall();
                // Deliberately excessive launch force must still land inside the field.
                Assert.IsTrue(ball.Kick(keeper,target-(Vector2)keeper.transform.position,100f,KickType.Lob,.32f,receiver));
                float started=Time.time;
                while(Time.time-started<.7f)
                {
                    receiver.TeleportTo(target + new Vector2(-teams.AttackDirFor(side)*(Time.time-started)*2f,0));
                    yield return new WaitForFixedUpdate();
                    Assert.Less(Mathf.Abs(ball.transform.position.x),halfLength-.4f);
                    Assert.Less(Mathf.Abs(ball.transform.position.y),halfWidth-.4f);
                    Assert.AreEqual(MatchPhase.Playing,GameManager.Instance.Phase);
                }
                Assert.AreSame(receiver,ball.Owner,"The moving teammate should collect the keeper outlet.");
                Assert.IsFalse(ball.IsAirborne);
            }
            // A normal shot still receives the same launch-speed multiplier as V9.
            var attacker=teams.CurrentHuman();
            attacker.TeleportTo(Vector2.zero);
            attacker.GainBall();
            Assert.IsTrue(ball.Kick(attacker,Vector2.right,10f,KickType.Shot,.35f));
            Assert.That(ball.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Is.EqualTo(18.5f).Within(.001f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }
    }
}
