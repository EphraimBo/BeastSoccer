using System.Collections;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Audio;
using BeastSoccer.Ball;

namespace BeastSoccer.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class UltimateAbility : MonoBehaviour
    {
        public float Charge { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsActivating { get; private set; }
        public bool IsAttackingVariant { get; private set; }
        public bool IsWingBlockActive { get; private set; }
        public bool IsFlying { get; private set; }
        public bool IsCharging { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;
        public float ShotMultiplier { get; private set; } = 1f;
        public float StrengthMultiplier { get; private set; } = 1f;
        public float TackleMultiplier { get; private set; } = 1f;
        public float ActiveSecondsRemaining { get; private set; }
        public float ActiveFractionRemaining => IsActivating ? 1f : (IsActive && GameConfig.Instance != null ? Mathf.Clamp01(ActiveSecondsRemaining / Mathf.Max(0.01f, GameConfig.Instance.ultDurationSeconds)) : 0f);
        public bool CanTriggerVoltFlight => player != null && player.Character == CharacterType.Volt && player.IsHuman && IsActive && !IsActivating && IsAttackingVariant && !IsFlying && GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing;
        public bool CanTriggerGoroCharge => player != null && player.Character == CharacterType.Goro && player.IsHuman && IsActive && !IsActivating && IsAttackingVariant && !IsCharging && GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing;
        public bool CanTriggerSpecialAction => CanTriggerVoltFlight || CanTriggerGoroCharge;
        public bool VoltFlightUsed => false; // flights are reusable while the ult timer remains active

        public BoxCollider2D wingBlockCollider;
        public WingBlockZone wingBlockZone;

        private PlayerController player;
        private Coroutine activeRoutine;
        private Vector3 baseVisualScale = Vector3.one;
        private float passiveLockUntil;
        private float preUltTimeScale = 1f;
        private bool ownsActivationSlowMo;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            RefreshBaseVisualScale();
            if (wingBlockCollider != null) wingBlockCollider.enabled = false;
        }

        private void Start()
        {
            RefreshBaseVisualScale();
            if (wingBlockCollider != null) wingBlockCollider.enabled = false;
        }

        private void Update()
        {
            if (player == null || player.Character == CharacterType.Generic || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing || IsActive || IsActivating || Time.time < passiveLockUntil) return;
            float rechargeMultiplier = 1f;
            if (ScoreManager.Instance != null && GameConfig.Instance != null)
            {
                int mine = player.Side == TeamSide.Home ? ScoreManager.Instance.HomeScore : ScoreManager.Instance.AwayScore;
                int theirs = player.Side == TeamSide.Home ? ScoreManager.Instance.AwayScore : ScoreManager.Instance.HomeScore;
                int deficit = Mathf.Clamp(theirs - mine, 0, Mathf.Max(0, GameConfig.Instance.comebackUltMaxDeficit));
                rechargeMultiplier += deficit * Mathf.Max(0f, GameConfig.Instance.comebackUltRechargePerGoal);
            }
            AddCharge(GameConfig.Instance.ultPassiveRechargePerSec * rechargeMultiplier * Time.deltaTime);
        }

        public void RefreshBaseVisualScale()
        {
            if (player != null && player.Visual != null) baseVisualScale = player.Visual.transform.localScale;
        }

        public void AddCharge(float amount)
        {
            if (player == null || player.Character == CharacterType.Generic) return;
            Charge = Mathf.Clamp01(Charge + Mathf.Max(0f,amount));
        }

        public bool TryActivate()
        {
            if (player == null || !player.IsHuman) return false;
            return TryActivateInternal();
        }

        public bool TryActivateAI()
        {
            if (player == null || player.IsHuman || player.Side != TeamSide.Away) return false;
            return TryActivateInternal();
        }

        private bool TryActivateInternal()
        {
            if (player == null || player.Character == CharacterType.Generic || IsActive || IsActivating || Charge < 0.999f || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return false;
            bool attacking = IsTeamAttackingAtPress();
            activeRoutine = StartCoroutine(UltRoutine(attacking));
            return true;
        }

        private bool IsTeamAttackingAtPress()
        {
            if (GameManager.Instance.Mode == GameMode.Defending) return player.Side == TeamSide.Away;

            Possession possession = GameManager.Instance.Possession;
            if (possession == Possession.Home) return player.Side == TeamSide.Home;
            if (possession == Possession.Away) return player.Side == TeamSide.Away;

            var ball = BallControl.Instance;
            if (ball != null)
            {
                var myClosest = TeamManager.Instance.ClosestToBall(player.Side, false);
                TeamSide other = player.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                var theirClosest = TeamManager.Instance.ClosestToBall(other, false);
                if (myClosest != null && theirClosest != null)
                {
                    float mine = Vector2.Distance(myClosest.transform.position, ball.transform.position);
                    float theirs = Vector2.Distance(theirClosest.transform.position, ball.transform.position);
                    if (Mathf.Abs(mine - theirs) > 0.15f) return mine < theirs;
                }
                if (ball.LastTouch != null) return ball.LastTouch.Side == player.Side;
            }
            return player.HasBall;
        }

        private IEnumerator UltRoutine(bool attacking)
        {
            IsActivating = true;
            IsActive = true;
            IsAttackingVariant = attacking;
            ActiveSecondsRemaining = GameConfig.Instance.ultDurationSeconds;
            player.Animation?.Trigger("UltEnter");
            AudioManager.Instance?.PlayUlt(player.Character);
            GameFeel.Shake(0.09f);

            preUltTimeScale = Time.timeScale;
            ownsActivationSlowMo = false;
            float slowMoReal = Mathf.Clamp(GameConfig.Instance.ultActivationSlowMoRealSeconds, 0f, GameConfig.Instance.ultTransitionSeconds);
            if (slowMoReal > 0f)
            {
                ownsActivationSlowMo = true;
                Time.timeScale = Mathf.Clamp(GameConfig.Instance.ultActivationSlowMoScale, 0.1f, 1f);
                yield return new WaitForSecondsRealtime(slowMoReal);
                RestoreActivationSlowMo();
            }
            float remainingTransition = Mathf.Max(0f, GameConfig.Instance.ultTransitionSeconds - slowMoReal);
            if (remainingTransition > 0f) yield return new WaitForSecondsRealtime(remainingTransition);
            if (!IsActive || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing)
            {
                // Charge was not spent yet, so an interrupted transition costs nothing.
                Deactivate();
                yield break;
            }

            Charge = 0f;
            IsActivating = false;
            RefreshBaseVisualScale();
            Apply(attacking);
            player.Animation?.SetUltActive(true);

            ActiveSecondsRemaining = GameConfig.Instance.ultDurationSeconds;
            while (ActiveSecondsRemaining > 0f && IsActive && GameManager.Instance != null)
            {
                if (GameManager.Instance.Phase == MatchPhase.Playing)
                    ActiveSecondsRemaining -= Time.deltaTime;
                else if (GameManager.Instance.Phase != MatchPhase.Paused)
                    break;
                yield return null;
            }
            Deactivate();
        }

        private void RestoreActivationSlowMo()
        {
            if (!ownsActivationSlowMo) return;
            ownsActivationSlowMo = false;
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Paused)
                Time.timeScale = preUltTimeScale <= 0f ? 1f : preUltTimeScale;
        }

        private void Apply(bool attacking)
        {
            var c = GameConfig.Instance;
            // Every ultimate improves the whole player by 10% first. Character-specific
            // bonuses then multiply on top, so the ult always feels like a true powered-up state.
            float universal = Mathf.Max(1f, c.ultUniversalBuff);
            SpeedMultiplier = universal;
            ShotMultiplier = universal;
            StrengthMultiplier = universal;
            TackleMultiplier = universal;

            switch (player.Character)
            {
                case CharacterType.Leo:
                    if (attacking)
                    {
                        SpeedMultiplier *= c.leoAtkSpeedBonus;
                        ShotMultiplier *= c.leoAtkShotBonus;
                    }
                    else
                    {
                        SpeedMultiplier *= c.leoDefSpeedBonus;
                        TackleMultiplier *= c.leoDefTackleBonus;
                    }
                    break;
                case CharacterType.Goro:
                    if (attacking)
                    {
                        StrengthMultiplier *= c.goroAtkStrengthBonus;
                        ShotMultiplier *= c.goroAtkShotBonus;
                    }
                    else
                    {
                        StrengthMultiplier *= c.goroDefStrengthBonus;
                        TackleMultiplier *= c.goroDefTackleBonus;
                        RefreshBaseVisualScale();
                        if (player.Visual != null) player.Visual.transform.localScale = baseVisualScale * (1f + c.goroSizeIncrease);
                        player.SetBodyScale(1f + c.goroSizeIncrease);
                    }
                    break;
                case CharacterType.Volt:
                    if (attacking)
                    {
                        SpeedMultiplier *= c.voltAtkSpeedBonus;
                        ShotMultiplier *= c.voltAtkShotBonus;
                        // Flight is a repeatable player-triggered move while the ult timer remains.
                        IsFlying = false;
                    }
                    else
                    {
                        IsWingBlockActive=true;
                        if (wingBlockCollider != null)
                        {
                            wingBlockCollider.enabled=true;
                            wingBlockCollider.size = new Vector2(0.55f, c.pitchWidth*c.voltWingWidthFraction);
                        }
                    }
                    break;
            }
        }


        public bool TryVoltFlight()
        {
            if (!CanTriggerVoltFlight || GameConfig.Instance == null) return false;
            IsFlying = true;
            player.BeginVoltFlight(GameConfig.Instance.voltFlySeconds, GameConfig.Instance.voltFlyVisualHeight);
            player.Animation?.Trigger("Fly");
            GameFeel.Shake(0.045f);
            return true;
        }

        public bool TryGoroCharge()
        {
            if (!CanTriggerGoroCharge || GameConfig.Instance == null) return false;
            IsCharging = true;
            player.BeginGoroCharge(GameConfig.Instance.goroChargeSeconds);
            player.Animation?.Trigger("Charge");
            GameFeel.Shake(0.06f);
            return true;
        }

        public bool TrySpecialAction()
        {
            if (player == null) return false;
            if (player.Character == CharacterType.Volt) return TryVoltFlight();
            if (player.Character == CharacterType.Goro) return TryGoroCharge();
            return false;
        }

        public void NotifyVoltFlightEnded()
        {
            if (player.Character != CharacterType.Volt) return;
            IsFlying = false;
            if (player.Visual != null) player.Visual.extraHeight = 0f;
        }

        public void NotifyGoroChargeEnded()
        {
            if (player.Character != CharacterType.Goro) return;
            IsCharging = false;
        }

        public void CancelForRestart()
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = null;
            Deactivate();
            passiveLockUntil = Time.time + 0.2f;
        }

        private void Deactivate()
        {
            RestoreActivationSlowMo();
            IsActive=false;
            IsActivating=false;
            IsAttackingVariant=false;
            IsWingBlockActive=false;
            IsFlying=false;
            IsCharging=false;
            ActiveSecondsRemaining=0f;
            SpeedMultiplier=ShotMultiplier=StrengthMultiplier=TackleMultiplier=1f;
            if (wingBlockCollider != null) wingBlockCollider.enabled=false;
            if (player != null && player.Visual != null)
            {
                player.Visual.extraHeight=0f;
                player.Visual.transform.localScale=baseVisualScale;
            }
            if (player != null)
            {
                player.SetBodyScale(1f);
                player.SetFlightCollision(false);
                player.Animation?.SetUltActive(false);
            }
        }
    }
}
