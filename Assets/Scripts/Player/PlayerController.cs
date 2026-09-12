using System.Collections;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Ball;
using BeastSoccer.AI;
using BeastSoccer.Audio;
using BeastSoccer.Presentation;

namespace BeastSoccer.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        public TeamSide Side;
        public FieldRole Role = FieldRole.Outfield;
        public TacticalRole FormationRole = TacticalRole.Rover;
        public CharacterType Character = CharacterType.Generic;
        public int ShirtNumber;
        public bool IsHuman;

        public PlayerVisualProxy Visual;
        public CharacterAnimationDriver Animation;
        public UltimateAbility Ult { get; private set; }
        public DefensiveActions Defense { get; private set; }

        public bool HasBall => BallControl.Instance != null && BallControl.Instance.Owner == this;
        public bool IsSprinting { get; private set; }
        public bool IsActionLocked => actionLocked || defensiveActionLocked || kickBusy;
        public bool CanBeTackled => Time.time >= possessionProtectedUntil &&
            !(GameManager.Instance != null && GameManager.Instance.IsHumanAimingSetPiece(this));
        public bool CanAttemptTackle => Time.time >= tackleLockedUntil;
        public bool IsWingBlocking => Ult != null && Ult.IsWingBlockActive;
        public bool IsFlying => Ult != null && Ult.IsFlying;
        public bool IsGoroBulldozing => Character == CharacterType.Goro && Ult != null && Ult.IsActive && Ult.IsAttackingVariant && HasBall;
        public bool IsGoroCharging => Ult != null && Ult.IsCharging;
        public bool IsGoroSlamming => Ult != null && Ult.IsSlamming;
        // While any Goro ultimate is active, a ball-carrying Goro cannot be stripped by an
        // ordinary outfield tackle. Goalkeepers remain the deliberate exception.
        public bool IsGoroUltTackleImmune => Character == CharacterType.Goro && Ult != null && Ult.IsActive && HasBall;
        public bool IsSuppressed { get; private set; }
        public Vector2 MoveFacing { get; private set; } = Vector2.right;
        public float StrengthMultiplier => baseStrength * (Ult != null ? Ult.StrengthMultiplier : 1f);
        public float TackleMultiplier => baseTackle * (Ult != null ? Ult.TackleMultiplier : 1f);
        public float KeeperHoldUntil { get; private set; }
        public float SprintStamina01 { get; private set; } = 1f;
        public bool IsSprintExhausted { get; private set; }
        public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
        public KickType VisualKickType { get; private set; } = KickType.None;
        public float VisualKickStartedAt { get; private set; } = -999f;
        public float VisualKickUntil { get; private set; } = -999f;
        public float ImpactVisualHeight
        {
            get
            {
                if (impactVisualDuration <= 0f) return 0f;
                float t = (Time.time - impactVisualStartedAt) / impactVisualDuration;
                if (t < 0f || t >= 1f) return 0f;
                return Mathf.Sin(t * Mathf.PI) * impactVisualPeak;
            }
        }

        private Rigidbody2D rb;
        private CapsuleCollider2D bodyCollider;
        private Vector2 bodyBaseSize;
        private Vector2 desiredMove;
        private float inputMagnitude;
        private bool requestedSprint;
        private bool actionLocked;
        private bool defensiveActionLocked;
        // A kick can be action-busy without freezing locomotion. This keeps passing/shooting fluid.
        private bool kickBusy;
        private Coroutine kickRoutine;
        private Coroutine flightRoutine;
        private Coroutine goroChargeRoutine;
        private Coroutine goroSlamRoutine;
        private Vector2 goroChargeDirection = Vector2.right;
        private float sprintBlend;
        private float baseSpeed = 1f, baseAccel = 1f, baseShot = 1f, baseStrength = 1f, baseTackle = 1f;
        private float externalSpeedMultiplier = 1f;
        private Vector2 externalPushVelocity;
        private float possessionProtectedUntil;
        private bool setPieceAimAnchored;
        private Vector2 setPieceAimAnchor;
        private float tackleLockedUntil;
        private float sprintRecoveryAllowedAt;
        private float nextGoroChargeShake;
        private float impactVisualStartedAt = -999f;
        private float impactVisualDuration;
        private float impactVisualPeak;
        private readonly System.Collections.Generic.Dictionary<PlayerController,float> bulldozeHitCooldowns = new System.Collections.Generic.Dictionary<PlayerController,float>();

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CapsuleCollider2D>();
            if (bodyCollider != null) bodyBaseSize = bodyCollider.size;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (GameConfig.Instance != null) rb.linearDamping = GameConfig.Instance.playerLinearDamping;
            Ult = GetComponent<UltimateAbility>();
            Defense = GetComponent<DefensiveActions>();
        }

        private void Start() { ConfigureCharacter(Character); }

        private void FixedUpdate()
        {
            if (GameConfig.Instance != null) rb.linearDamping = GameConfig.Instance.playerLinearDamping;

            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing || IsSuppressed)
            {
                setPieceAimAnchored = false;
                rb.linearVelocity = Vector2.zero;
                UpdateAnimation();
                return;
            }

            // During a human throw-in/corner aiming phase the stick changes facing only. The taker
            // is anchored at the restart spot so collisions cannot shove them while the user aims.
            if (GameManager.Instance.IsHumanAimingSetPiece(this))
            {
                if (!setPieceAimAnchored)
                {
                    setPieceAimAnchored = true;
                    setPieceAimAnchor = rb.position;
                }
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.MovePosition(setPieceAimAnchor);
                UpdateAnimation();
                return;
            }
            setPieceAimAnchored = false;

            ApplyMovement();
            ClampToPitch();
            UpdateAnimation();
        }

        public void ConfigureCharacter(CharacterType type)
        {
            Character = type;
            var c = GameConfig.Instance;
            if (c == null) return;
            switch (type)
            {
                case CharacterType.Leo: baseSpeed=c.leoSpeed; baseAccel=c.leoAccel; baseShot=c.leoShot; baseStrength=c.leoStrength; baseTackle=c.leoTackle; break;
                case CharacterType.Goro: baseSpeed=c.goroSpeed; baseAccel=c.goroAccel; baseShot=c.goroShot; baseStrength=c.goroStrength; baseTackle=c.goroTackle; break;
                case CharacterType.Volt: baseSpeed=c.voltSpeed; baseAccel=c.voltAccel; baseShot=c.voltShot; baseStrength=c.voltStrength; baseTackle=c.voltTackle; break;
                default: baseSpeed=c.genericSpeed; baseAccel=c.genericAccel; baseShot=c.genericShot; baseStrength=c.genericStrength; baseTackle=c.genericTackle; break;
            }
            Visual?.ApplyCharacter();
            Ult?.RefreshBaseVisualScale();
        }

        public void SetHumanControlled(bool human, bool preserveMomentum = false)
        {
            if (IsHuman == human)
            {
                ClearMovementInput(!preserveMomentum);
                return;
            }

            IsHuman = human;
            CancelPendingKick();
            Defense?.CancelForControlChange();
            // Receiving a user pass can hand control to a teammate without making them plant
            // their feet on the exact reception frame. Manual switches/restarts still hard-stop.
            ClearMovementInput(!preserveMomentum);
        }

        public void SetMoveInput(Vector2 input)
        {
            if (!IsHuman) return;
            float mag = Mathf.Clamp01(input.magnitude);
            desiredMove = mag > 0.01f ? input.normalized : Vector2.zero;
            inputMagnitude = mag;

            if (!IsSprinting && mag >= GameConfig.Instance.sprintEnterThreshold) IsSprinting = true;
            else if (IsSprinting && mag <= GameConfig.Instance.sprintExitThreshold) IsSprinting = false;

            requestedSprint = IsSprinting && mag > 0.01f;
            if (desiredMove.sqrMagnitude > 0.02f) MoveFacing = desiredMove;
        }

        public void SetEditorMoveInput(Vector2 input, bool sprint)
        {
            if (!IsHuman) return;
            float mag = Mathf.Clamp01(input.magnitude);
            desiredMove = mag > 0.01f ? input.normalized : Vector2.zero;
            inputMagnitude = mag > 0.01f ? 1f : 0f;
            requestedSprint = sprint && desiredMove.sqrMagnitude > 0.01f;
            IsSprinting = requestedSprint;
            if (desiredMove.sqrMagnitude > 0.02f) MoveFacing = desiredMove;
        }

        // Set-piece aiming uses the movement control as an aim stick without translating the taker.
        public void SetAimInput(Vector2 input)
        {
            if (!IsHuman) return;
            desiredMove = Vector2.zero;
            inputMagnitude = 0f;
            requestedSprint = false;
            IsSprinting = false;
            sprintBlend = 0f;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (input.sqrMagnitude > 0.02f) MoveFacing = input.normalized;
        }

        public void SetAIMove(Vector2 direction, bool sprint)
        {
            if (IsHuman) return;
            float mag = Mathf.Clamp01(direction.magnitude);
            desiredMove = mag > 0.01f ? direction.normalized : Vector2.zero;
            inputMagnitude = mag;
            requestedSprint = sprint && mag > 0.05f;
            IsSprinting = requestedSprint;
            if (desiredMove.sqrMagnitude > 0.02f) MoveFacing = desiredMove;
        }

        public void ClearMovementInput(bool clearVelocity)
        {
            desiredMove = Vector2.zero;
            inputMagnitude = 0f;
            requestedSprint = false;
            IsSprinting = false;
            sprintBlend = 0f;
            if (clearVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
                MoveFacing = direction.normalized;
        }

        public void SetExternalSpeedMultiplier(float value) => externalSpeedMultiplier = Mathf.Max(0f, value);

        public void SetSuppressed(bool suppressed)
        {
            IsSuppressed = suppressed;
            if (bodyCollider != null) bodyCollider.enabled = !suppressed;
            if (suppressed) StopImmediately();
        }

        // All action/contact pushes are fed through the normal velocity solver instead of
        // teleporting a dynamic Rigidbody2D. This prevents tackle/jockey motion from stacking
        // with locomotion and producing rare speed bursts.
        public void ApplyExternalPush(Vector2 velocity)
        {
            if (rb == null || IsSuppressed || GameConfig.Instance == null) return;
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            // Dedicated AI keepers should never be knocked through/behind their own goal line.
            if (Role == FieldRole.Goalkeeper && !IsHuman) return;
            externalPushVelocity = Vector2.ClampMagnitude(velocity, GameConfig.Instance.externalPushMaxSpeed);
        }

        public void ApplyExternalDisplacement(Vector2 delta)
        {
            if (Time.fixedDeltaTime <= 0f) return;
            ApplyExternalPush(delta / Time.fixedDeltaTime);
        }

        // Dedicated no-crowd response for a keeper holding the ball. The ordinary external-push
        // cap is intentionally lower than player run speed, so it was possible to push through it.
        // This bounded repulsion wins against inward movement without teleporting the attacker.
        public void ApplyKeeperRepulsion(Vector2 velocity)
        {
            if (rb == null || IsSuppressed || GameConfig.Instance == null) return;
            if (Role == FieldRole.Goalkeeper) return;
            externalPushVelocity = Vector2.ClampMagnitude(velocity, GameConfig.Instance.keeperNoCrowdPushSpeed);
        }

        public void ApplyBulldozePush(Vector2 velocity)
        {
            if (rb == null || IsSuppressed || GameConfig.Instance == null) return;
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            if (Role == FieldRole.Goalkeeper && !IsHuman) return;
            externalPushVelocity = Vector2.ClampMagnitude(velocity, GameConfig.Instance.goroBulldozePush);
        }

        public void ApplyTackleFollowThrough(Vector2 direction, float multiplier = 1f)
        {
            if (rb == null || GameConfig.Instance == null || IsSuppressed) return;
            if (direction.sqrMagnitude < 0.001f) direction = MoveFacing;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            externalPushVelocity = direction * GameConfig.Instance.tackleFollowThroughSpeed * Mathf.Max(0.1f, multiplier);
        }

        public void ApplyTackleVictimPush(Vector2 direction)
        {
            if (rb == null || GameConfig.Instance == null || IsSuppressed) return;
            if (direction.sqrMagnitude < 0.001f) direction = -MoveFacing;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.left;
            // A successful tackle is a real collision event: the victim is launched several
            // body-widths away, then the existing bounded push decay settles them naturally.
            Vector2 launch = direction * GameConfig.Instance.tackleVictimPushSpeed;
            rb.linearVelocity = launch;
            externalPushVelocity = launch;
            impactVisualStartedAt = Time.time;
            impactVisualDuration = Mathf.Max(0.15f, GameConfig.Instance.tackleVictimVisualLaunchSeconds);
            impactVisualPeak = Mathf.Max(0f, GameConfig.Instance.tackleVictimVisualLaunchHeight);
        }

        public void ApplyGoroChargePush(Vector2 velocity)
        {
            if (rb == null || IsSuppressed || GameConfig.Instance == null) return;
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            if (Role == FieldRole.Goalkeeper) return;
            Vector2 launch = Vector2.ClampMagnitude(velocity, Mathf.Max(GameConfig.Instance.goroChargePush, GameConfig.Instance.goroSlamPush));
            // Goro contact should feel extreme: immediate launch + a short airborne presentation arc.
            rb.linearVelocity = launch;
            externalPushVelocity = launch;
            impactVisualStartedAt = Time.time;
            impactVisualDuration = Mathf.Max(0.18f, GameConfig.Instance.goroLaunchVisualSeconds);
            impactVisualPeak = Mathf.Max(0.4f, GameConfig.Instance.goroLaunchVisualHeight);
        }

        public void SetDefensiveActionLock(bool locked)
        {
            defensiveActionLocked = locked;
            if (locked) requestedSprint = false;
        }

        public void SetKeeperHold(float seconds)
        {
            KeeperHoldUntil = Mathf.Max(KeeperHoldUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public void ReleaseKeeperHoldNow()
        {
            KeeperHoldUntil = Time.time;
        }

        public void ProtectPossession(float seconds)
        {
            possessionProtectedUntil = Mathf.Max(possessionProtectedUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public void LockTackleAfterPossessionLoss(float seconds)
        {
            tackleLockedUntil = Mathf.Max(tackleLockedUntil, Time.time + Mathf.Max(0f, seconds));
            Defense?.CancelForControlChange();
        }

        private float UltMoveSpeedMultiplier()
        {
            return Ult != null && Ult.IsActive ? DemoMatchRules.UltMovementMultiplier : 1f;
        }

        private void ApplyMovement()
        {
            var cfg = GameConfig.Instance;
            if (cfg == null) return;

            UpdateSprintStamina(cfg);

            // Goro CHARGE is the one intentional straight-line locomotion override. It is
            // bounded, timed, and owns movement for its three-second window.
            if (IsGoroCharging)
            {
                float ultMoveSpeed = UltMoveSpeedMultiplier();
                Vector2 chargeTarget = goroChargeDirection * cfg.goroChargeSpeed * cfg.gameplayPaceMultiplier * ultMoveSpeed + externalPushVelocity;
                if (Time.time >= nextGoroChargeShake)
                {
                    nextGoroChargeShake = Time.time + Mathf.Max(0.08f, cfg.goroChargeShakeInterval);
                    GameFeel.Shake(0.022f);
                }
                rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, chargeTarget, cfg.acceleration * 2.4f * Time.fixedDeltaTime);
                externalPushVelocity = Vector2.MoveTowards(externalPushVelocity, Vector2.zero, cfg.externalPushDecay * Time.fixedDeltaTime);
                float cap = cfg.goroChargeSpeed * cfg.gameplayPaceMultiplier * ultMoveSpeed * 1.12f;
                if (rb.linearVelocity.sqrMagnitude > cap * cap) rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, cap);
                ResolveGoroChargeContacts();
                return;
            }

            if (actionLocked || defensiveActionLocked)
            {
                rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, externalPushVelocity, cfg.deceleration * Time.fixedDeltaTime);
                externalPushVelocity = Vector2.MoveTowards(externalPushVelocity, Vector2.zero, cfg.externalPushDecay * Time.fixedDeltaTime);
                ClampVelocityToLegalMaximum();
                return;
            }

            bool canActuallySprint = requestedSprint && !IsSprintExhausted && SprintStamina01 > 0.001f && desiredMove.sqrMagnitude > 0.01f;
            IsSprinting = canActuallySprint;
            float targetSprint = canActuallySprint ? 1f : 0f;
            float rampSeconds = cfg.sprintRampSeconds * (Character == CharacterType.Volt ? Mathf.Max(0.15f, cfg.voltSprintRampMultiplier) : 1f);
            float ramp = rampSeconds <= 0f ? 99f : Time.fixedDeltaTime / rampSeconds;
            sprintBlend = Mathf.MoveTowards(sprintBlend, targetSprint, ramp);

            float ultSpeed = UltMoveSpeedMultiplier();
            float wingSpeed = 1f; // FIX32 BLOCK keeps Volt on normal ground movement speed.
            float speed = cfg.baseMoveSpeed * cfg.gameplayPaceMultiplier * baseSpeed * ultSpeed * externalSpeedMultiplier * wingSpeed;
            speed *= Mathf.Lerp(1f, cfg.sprintMultiplier, sprintBlend);

            float gait = inputMagnitude <= 0.01f
                ? 0f
                : Mathf.Lerp(cfg.minMoveGait, 1f, Mathf.InverseLerp(0.12f, Mathf.Max(0.13f, cfg.fullMoveGaitThreshold), inputMagnitude));

            Vector2 target = desiredMove * speed * gait + externalPushVelocity;
            ApplyThrowInExclusion(ref target, cfg);
            float accel = desiredMove.sqrMagnitude > 0.01f ? cfg.acceleration * cfg.gameplayPaceMultiplier * baseAccel : cfg.deceleration * cfg.gameplayPaceMultiplier;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, accel * Time.fixedDeltaTime);
            externalPushVelocity = Vector2.MoveTowards(externalPushVelocity, Vector2.zero, cfg.externalPushDecay * Time.fixedDeltaTime);
            ClampVelocityToLegalMaximum();
        }

        private void ApplyThrowInExclusion(ref Vector2 targetVelocity, GameConfig cfg)
        {
            if (cfg == null || GameManager.Instance == null || !GameManager.Instance.AwaitingSetPieceKick ||
                GameManager.Instance.CurrentSetPieceType != SetPieceType.ThrowIn) return;
            var taker = GameManager.Instance.PreparedSetPieceTaker;
            if (taker == null || taker == this || taker.Side == Side) return;

            Vector2 away = rb.position - (Vector2)taker.transform.position;
            float dist = away.magnitude;
            float radius = Mathf.Max(0.8f, cfg.throwInNoCrowdRadius);
            if (dist > radius + 0.30f) return;
            if (dist < 0.001f) away = new Vector2(-TeamManager.Instance.AttackDirFor(Side), 0f);
            else away /= dist;

            float inward = Vector2.Dot(targetVelocity, -away);
            if (inward > 0f) targetVelocity += away * inward;
            if (dist < radius)
            {
                float strength = Mathf.Lerp(cfg.throwInNoCrowdPushSpeed, cfg.throwInNoCrowdPushSpeed * 0.30f, Mathf.Clamp01(dist / radius));
                targetVelocity += away * strength;
            }
        }

        private void UpdateSprintStamina(GameConfig cfg)
        {
            if (cfg == null || rb == null) return;

            bool wantsSprint = requestedSprint && desiredMove.sqrMagnitude > 0.01f;
            bool draining = wantsSprint && !IsSprintExhausted && SprintStamina01 > 0f;
            if (draining)
            {
                // Drain by actual sprint DISTANCE. A full bar therefore corresponds to roughly one
                // pitch length of sprinting, regardless of character pace or frame rate. Push/charge
                // velocity is capped out of the calculation so collisions cannot drain stamina.
                float drainDistance = cfg.sprintStaminaDrainDistance > 0.1f
                    ? cfg.sprintStaminaDrainDistance
                    : Mathf.Max(1f, cfg.pitchLength);
                float ultSpeed = UltMoveSpeedMultiplier();
                float maxSprintTravelSpeed = cfg.baseMoveSpeed * cfg.gameplayPaceMultiplier * baseSpeed * cfg.sprintMultiplier * ultSpeed * Mathf.Max(1f, externalSpeedMultiplier);
                float actualSprintSpeed = Mathf.Min(rb.linearVelocity.magnitude, maxSprintTravelSpeed);
                float distanceThisStep = actualSprintSpeed * Time.fixedDeltaTime;
                SprintStamina01 = Mathf.Max(0f, SprintStamina01 - distanceThisStep / drainDistance);
                sprintRecoveryAllowedAt = Time.time + Mathf.Max(0f, cfg.sprintStaminaRecoveryDelay);
                if (SprintStamina01 <= 0.001f)
                {
                    SprintStamina01 = 0f;
                    IsSprintExhausted = true;
                    IsSprinting = false;
                }
            }
            else if (Time.time >= sprintRecoveryAllowedAt)
            {
                float recoverSeconds = Mathf.Max(0.25f, cfg.sprintStaminaRecoverSeconds);
                SprintStamina01 = Mathf.Min(1f, SprintStamina01 + Time.fixedDeltaTime / recoverSeconds);
            }

            if (IsSprintExhausted && SprintStamina01 >= Mathf.Clamp01(cfg.sprintStaminaResumeThreshold))
                IsSprintExhausted = false;
        }

        private void ClampVelocityToLegalMaximum()
        {
            if (GameConfig.Instance == null || rb == null || IsFlying) return;
            float ultSpeed = UltMoveSpeedMultiplier();
            float wingSpeed = 1f; // FIX32 BLOCK keeps Volt on normal ground movement speed.
            float maxLegal = GameConfig.Instance.baseMoveSpeed * GameConfig.Instance.gameplayPaceMultiplier * baseSpeed * GameConfig.Instance.sprintMultiplier * ultSpeed * Mathf.Max(1f, externalSpeedMultiplier) * wingSpeed;
            maxLegal *= GameConfig.Instance.hardSpeedCapMultiplier;
            // Explicit, bounded shove effects (Goro bulldoze / keeper exclusion) may briefly move
            // a victim faster than their own sprint without being mistaken for the old speed bug.
            maxLegal = Mathf.Max(maxLegal, externalPushVelocity.magnitude * 1.05f);
            if (rb.linearVelocity.sqrMagnitude > maxLegal * maxLegal)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Movement clamp] {name} velocity {rb.linearVelocity.magnitude:F2} > legal {maxLegal:F2}. Human={IsHuman}, Ball={HasBall}, Sprint={IsSprinting}, Ult={(Ult != null && Ult.IsActive)}");
#endif
                rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxLegal);
            }
        }

        private void ClampToPitch()
        {
            if (rb == null) return;
            Vector2 p = rb.position;
            Vector2 clamped = ClampPointToPitch(p);
            if ((p - clamped).sqrMagnitude > 0.000001f)
            {
                Vector2 v = rb.linearVelocity;
                if (Mathf.Abs(p.x - clamped.x) > 0.0001f) v.x = 0f;
                if (Mathf.Abs(p.y - clamped.y) > 0.0001f) v.y = 0f;
                rb.linearVelocity = v;
                rb.MovePosition(clamped);
            }
        }

        private Vector2 ClampPointToPitch(Vector2 p)
        {
            var c = GameConfig.Instance;
            if (c == null) return p;
            float hx = c.pitchLength * 0.5f - c.pitchPlayerPadding;
            float hy = c.pitchWidth * 0.5f - c.pitchPlayerPadding;
            Vector2 clamped = new Vector2(Mathf.Clamp(p.x,-hx,hx), Mathf.Clamp(p.y,-hy,hy));

            if (Role == FieldRole.Goalkeeper)
            {
                int attackDir = TeamManager.Instance != null ? TeamManager.Instance.AttackDirFor(Side) : (Side == TeamSide.Home ? 1 : -1);
                float ownGoalX = -attackDir * c.pitchLength * 0.5f;

                if (IsHuman && GameManager.Instance != null && GameManager.Instance.Mode == GameMode.Defending)
                {
                    float fieldEdge = ownGoalX + attackDir * c.defendingHumanKeeperDepth;
                    float minX = Mathf.Min(ownGoalX + attackDir * c.pitchPlayerPadding, fieldEdge);
                    float maxX = Mathf.Max(ownGoalX + attackDir * c.pitchPlayerPadding, fieldEdge);
                    clamped.x = Mathf.Clamp(p.x, minX, maxX);
                    clamped.y = Mathf.Clamp(p.y, -c.keeperLateralRange, c.keeperLateralRange);
                }
                else if (!IsHuman)
                {
                    // Dedicated normal-play keepers are hard-bounded in front of their goal line.
                    // They can track laterally and step forward a little, but can never run into/behind the net.
                    float nearLine = ownGoalX + attackDir * c.keeperGoalLineClearance;
                    float fieldEdge = ownGoalX + attackDir * c.keeperMaxDepth;
                    float minX = Mathf.Min(nearLine, fieldEdge);
                    float maxX = Mathf.Max(nearLine, fieldEdge);
                    clamped.x = Mathf.Clamp(p.x, minX, maxX);
                    clamped.y = Mathf.Clamp(p.y, -c.keeperLateralRange, c.keeperLateralRange);
                }
            }
            return clamped;
        }

        public void GainBall(float protectionSeconds = -1f)
        {
            if (IsWingBlocking) return;
            if (BallControl.Instance != null && BallControl.Instance.SetOwner(this))
            {
                // A received pass should never carry a contact impulse into the new dribble state.
                externalPushVelocity = Vector2.zero;
                ClampVelocityToLegalMaximum();
                float protection = protectionSeconds >= 0f ? protectionSeconds : GameConfig.Instance.receiveProtectionSeconds;
                possessionProtectedUntil = Time.time + protection;
                Animation?.Trigger("Receive");
            }
        }

        public void LoseBallFromTackle(PlayerController tackler)
        {
            if (BallControl.Instance == null || !HasBall) return;
            CancelPendingKick();
            BallControl.Instance.Release(this);
            LockTackleAfterPossessionLoss(GameConfig.Instance.tackleLockoutAfterLossSeconds);
            Vector2 away = tackler != null ? ((Vector2)transform.position - (Vector2)tackler.transform.position) : -MoveFacing;
            if (away.sqrMagnitude < 0.001f) away = -MoveFacing;
            away.Normalize();
            // Break the tackle contact line slightly sideways so the tackler can carry past
            // instead of both players stopping/latching on the same point.
            Vector2 lateral = new Vector2(-away.y, away.x);
            float side = tackler != null && Mathf.Abs(transform.position.y - tackler.transform.position.y) > 0.03f
                ? Mathf.Sign(transform.position.y - tackler.transform.position.y)
                : (Random.value < 0.5f ? -1f : 1f);
            Vector2 victimDir = (away * 0.82f + lateral * side * 0.42f).normalized;
            ApplyTackleVictimPush(victimDir);
            Animation?.Trigger("Hit");
        }

        public void Shoot()
        {
            if (DuelRules.Enabled && !DuelRules.CanScoreFrom(this)) return;
            if (!HasBall) return;
            if (!CanKick(KickType.Shot)) return;
            StartKick(KickType.Shot, null, ShotDirection());
        }

        public void Pass()
        {
            if (!HasBall) return;
            if (!CanKick(KickType.Pass)) return;
            PlayerController target;
            if (GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Kickoff && GameManager.Instance.CanTakeKickoff(this) && RoundReset.Instance != null)
                target = RoundReset.Instance.GetKickoffPassTarget(this);
            else
            {
                float cone = GameConfig.Instance.passAssistConeDegrees +
                    (Character == CharacterType.Leo ? GameConfig.Instance.leoPassAssistConeBonusDegrees : 0f);
                target = TeamManager.Instance.BestDirectionalTarget(this, MoveFacing, cone, 0.70f);
                if (target == null) target = TeamManager.Instance.BestPassTarget(this, MoveFacing, false);
            }
            Vector2 aim = target != null ? (Vector2)target.transform.position - (Vector2)transform.position : MoveFacing;
            StartKick(KickType.Pass, target, aim);
        }

        public void ThroughBall()
        {
            if (!HasBall) return;
            if (!CanKick(KickType.Through)) return;
            var target = TeamManager.Instance.BestPassTarget(this, MoveFacing, true);
            Vector2 targetPoint;
            if (target != null)
            {
                var trb = target.GetComponent<Rigidbody2D>();
                Vector2 leadVel = trb != null ? trb.linearVelocity : target.MoveFacing * GameConfig.Instance.baseMoveSpeed;
                targetPoint = ClampKickTargetToPitch((Vector2)target.transform.position + leadVel * GameConfig.Instance.throughLeadSeconds);
            }
            else targetPoint = (Vector2)transform.position + SafeAim() * 5f;
            StartKick(KickType.Through, target, targetPoint - (Vector2)transform.position);
        }

        public void Lob()
        {
            if (!HasBall) return;
            if (!CanKick(KickType.Lob)) return;

            Vector2 rawDir = SafeAim();
            bool throwIn = GameManager.Instance != null && GameManager.Instance.IsHumanAimingSetPiece(this) &&
                           GameManager.Instance.CurrentSetPieceType == SetPieceType.ThrowIn;
            float cone = throwIn ? GameConfig.Instance.throwInAssistConeDegrees : GameConfig.Instance.lobAssistConeDegrees;
            var target = TeamManager.Instance.BestDirectionalTarget(this, rawDir, cone, 0.75f);
            if (target == null) target = TeamManager.Instance.ClosestTeammateToAim(this, rawDir, 0.75f);

            Vector2 aim = rawDir * 5.0f;
            if (target != null)
            {
                // FIX26 crosses/throw-ins are receiver-locked once a target is chosen. The aim
                // stick chooses WHO, while BallControl keeps the flight attached to that moving
                // teammate until the ball arrives.
                aim = (Vector2)target.transform.position - (Vector2)transform.position;
            }
            StartKick(KickType.Lob, target, aim);
        }

        public void AIKickLobTo(PlayerController target)
        {
            if (!HasBall || target == null || target.Side != Side) return;
            Vector2 aim = (Vector2)target.transform.position - (Vector2)transform.position;
            StartKick(KickType.Lob, target, aim);
        }

        public void AIKickTo(PlayerController target, bool through)
        {
            if (!HasBall || target == null) return;
            Vector2 point = target.transform.position;
            if (through)
            {
                var trb = target.GetComponent<Rigidbody2D>();
                Vector2 v = trb != null ? trb.linearVelocity : target.MoveFacing * GameConfig.Instance.baseMoveSpeed;
                point = ClampKickTargetToPitch(point + v * GameConfig.Instance.throughLeadSeconds);
            }
            StartKick(through ? KickType.Through : KickType.Pass, target, point - (Vector2)transform.position);
        }

        private bool CanKick(KickType type)
        {
            if (!HasBall || actionLocked || defensiveActionLocked || kickBusy || IsWingBlocking || GameManager.Instance == null) return false;
            if (GameManager.Instance.Phase == MatchPhase.Playing) return true;
            // Regular kickoffs are deliberately started by a PASS after the reset has settled.
            // Shooting/through balls cannot start the clock from the centre spot.
            return type == KickType.Pass && GameManager.Instance.CanTakeKickoff(this);
        }

        private void StartKick(KickType type, PlayerController target, Vector2 aim)
        {
            if (kickRoutine != null) StopCoroutine(kickRoutine);
            kickRoutine = StartCoroutine(KickRoutine(type, target, aim));
        }

        private IEnumerator KickRoutine(KickType type, PlayerController target, Vector2 aim)
        {
            float contactDelay = type == KickType.Shot ? GameConfig.Instance.shootContactSeconds
                : type == KickType.Through ? GameConfig.Instance.throughContactSeconds
                : type == KickType.Lob ? GameConfig.Instance.lobContactSeconds
                : GameConfig.Instance.passContactSeconds;
            if (DuelRules.Enabled)
            {
                if (type == KickType.Shot) contactDelay = 0.14f;
                else if (type == KickType.Lob && Role == FieldRole.Goalkeeper) contactDelay = 0.12f;
            }
            if (BallControl.Instance == null || !BallControl.Instance.BeginAction(this, aim, contactDelay)) yield break;

            // IMPORTANT: kicking no longer locks movement. The player keeps their current run/turn
            // through the contact animation; kickBusy only prevents stacking another gameplay action.
            kickBusy = true;
            VisualKickType = type;
            VisualKickStartedAt = Time.time;
            VisualKickUntil = Time.time + (Role == FieldRole.Goalkeeper && type == KickType.Lob ? .40f : type == KickType.Shot ? .26f : .22f);
            Animation?.Trigger(type == KickType.Shot ? "Shoot" : type == KickType.Through ? "Through" : "Pass");
            yield return new WaitForSeconds(contactDelay);

            // Ownership/token validation: a tackle, restart, or control reset during windup cancels cleanly.
            if (!HasBall || BallControl.Instance.Owner != this)
            {
                kickBusy = false;
                kickRoutine = null;
                yield break;
            }

            // Re-evaluate a targeted pass at ball contact. Teammates can move during the windup,
            // and this game has no manual power meter, so pass pace is assisted by target distance.
            Vector2 finalAim = aim;
            // A shot should go where the player is aiming at CONTACT, not where they happened
            // to be facing when the button was pressed 0.2 seconds earlier.
            if (type == KickType.Shot)
            {
                finalAim = ShotDirection();
            }
            else if (target != null && target.Side == Side)
            {
                Vector2 targetPoint = target.transform.position;
                var trb = target.GetComponent<Rigidbody2D>();
                if (type == KickType.Through)
                {
                    Vector2 v = trb != null ? trb.linearVelocity : target.MoveFacing * GameConfig.Instance.baseMoveSpeed;
                    targetPoint = ClampKickTargetToPitch(targetPoint + v * GameConfig.Instance.throughLeadSeconds);
                    finalAim = targetPoint - (Vector2)transform.position;
                }
                else if (type == KickType.Lob)
                {
                    // The selected receiver is authoritative for a lob/cross. BallControl performs
                    // the moving lock during flight, so contact aims at the receiver's current spot.
                    finalAim = targetPoint - (Vector2)transform.position;
                }
                else
                {
                    finalAim = targetPoint - (Vector2)transform.position;
                }
            }

            float distance = Mathf.Max(0.1f, finalAim.magnitude);
            float force; float arc;
            switch (type)
            {
                case KickType.Shot:
                    // V6: make strikes decisively arcade-fast. This is 1.3x the already-boosted V5
                    // shot launch speed; ball damping still handles the tail of the trajectory.
                    force = Mathf.Clamp(GameConfig.Instance.shotBaseForce + distance * GameConfig.Instance.shotForcePerUnit,
                        GameConfig.Instance.shotMinForce, GameConfig.Instance.shotMaxForce);
                    force *= baseShot * (Ult != null ? Ult.ShotMultiplier : 1f);
                    if (Ult != null && Ult.IsActive) force *= GameConfig.Instance.ultShotBallSpeedBonus;
                    force *= 1.45f;
                    arc = GameConfig.Instance.shotVisualArc;
                    break;
                case KickType.Through:
                    // No manual power meter: targeted passes are guaranteed enough launch speed
                    // to travel beyond the intended lead point despite ball damping.
                    force=Mathf.Clamp(distance * GameConfig.Instance.ballLinearDrag + GameConfig.Instance.throughArrivalSpeed,
                        GameConfig.Instance.throughMinForce, GameConfig.Instance.throughMaxForce);
                    arc=GameConfig.Instance.throughVisualArc;
                    break;
                case KickType.Lob:
                    if (Role == FieldRole.Goalkeeper && DuelRules.Enabled)
                    {
                        // V7: keeper outlets stay fast, but use their own capped launch profile.
                        // V6 used full boosted shot velocity here, which could clear the entire pitch.
                        force = Mathf.Clamp(6.5f + distance * .85f, 10.5f, 15.5f);
                        arc = .32f;
                    }
                    else
                    {
                        force=Mathf.Clamp(distance * GameConfig.Instance.ballLinearDrag + GameConfig.Instance.lobArrivalSpeed,
                            GameConfig.Instance.lobMinForce, GameConfig.Instance.lobMaxForce);
                        arc=GameConfig.Instance.lobVisualArc;
                    }
                    break;
                default:
                    force=Mathf.Clamp(distance * GameConfig.Instance.ballLinearDrag + GameConfig.Instance.passArrivalSpeed,
                        GameConfig.Instance.passMinForce, GameConfig.Instance.passMaxForce);
                    arc=GameConfig.Instance.passVisualArc;
                    break;
            }

            force *= GameConfig.Instance.gameplayPaceMultiplier;
            BallControl.Instance.Kick(this, finalAim.normalized, force, type, arc, target);
            AudioManager.Instance?.PlayKick();
            GameFeel.Shake(type==KickType.Shot?0.070f:0.025f);

            // Very short action-busy tail for double-tap protection, without touching velocity.
            yield return new WaitForSeconds(0.06f);
            kickBusy = false;
            kickRoutine = null;
        }

        private void CancelPendingKick()
        {
            if (kickRoutine != null) StopCoroutine(kickRoutine);
            kickRoutine = null;
            kickBusy = false;
            VisualKickType = KickType.None;
            VisualKickUntil = -999f;
            actionLocked = false;
            BallControl.Instance?.CancelActionSetup(this);
        }

        private Vector2 ShotDirection()
        {
            var cfg = GameConfig.Instance;
            int dir = TeamManager.Instance.AttackDirFor(Side);
            float goalX = dir * cfg.pitchLength * 0.5f;
            Vector2 origin = transform.position;
            Vector2 facing = MoveFacing.sqrMagnitude > 0.01f ? MoveFacing.normalized : new Vector2(dir, 0f);

            // Project the player's current facing ray onto the goal line. This makes up/down/diagonal
            // input visibly alter where the shot is headed instead of only adding a tiny fixed offset.
            float forwardComponent = facing.x * dir;
            float maxAimY = cfg.shotGoalHalfWidth + cfg.shotMissMargin;
            float targetY;
            if (forwardComponent >= cfg.shotForwardAimMin && Mathf.Abs(facing.x) > 0.001f)
            {
                float t = (goalX - origin.x) / facing.x;
                targetY = origin.y + facing.y * Mathf.Max(0f, t);
            }
            else
            {
                // Mostly sideways/backward input still produces a football shot toward goal, but the
                // vertical input strongly selects the near/far side instead of snapping to centre.
                targetY = origin.y + facing.y * cfg.shotGoalHalfWidth * 1.35f;
            }

            targetY = Mathf.Clamp(targetY, -maxAimY, maxAimY);
            float assistedY = Mathf.Clamp(targetY, -cfg.shotGoalHalfWidth * 0.92f, cfg.shotGoalHalfWidth * 0.92f);
            targetY = Mathf.Lerp(targetY, assistedY, cfg.shotAimAssist);

            // Keep the full vector so shot power can still scale with distance to the goal line.
            return new Vector2(goalX - origin.x, targetY - origin.y);
        }

        private Vector2 ClampKickTargetToPitch(Vector2 point)
        {
            if (GameConfig.Instance == null) return point;
            float hx = GameConfig.Instance.pitchLength * 0.5f - 0.70f;
            float hy = GameConfig.Instance.pitchWidth * 0.5f - 0.70f;
            return new Vector2(Mathf.Clamp(point.x, -hx, hx), Mathf.Clamp(point.y, -hy, hy));
        }

        private Vector2 SafeAim()
        {
            return MoveFacing.sqrMagnitude > 0.01f ? MoveFacing.normalized : new Vector2(TeamManager.Instance.AttackDirFor(Side),0f);
        }

        public void SetBodyScale(float multiplier)
        {
            if (bodyCollider != null) bodyCollider.size = bodyBaseSize * Mathf.Max(0.5f, multiplier);
        }

        public void SetFlightCollision(bool flying)
        {
            if (bodyCollider != null) bodyCollider.isTrigger = flying;
        }

        public void BeginVoltFlight(float duration, float maxHeight)
        {
            if (flightRoutine != null) StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(FlightRoutine(duration, maxHeight));
        }

        public void BeginGoroCharge(float duration)
        {
            if (goroChargeRoutine != null) StopCoroutine(goroChargeRoutine);
            goroChargeDirection = SafeAim();
            if (goroChargeDirection.sqrMagnitude < 0.001f)
                goroChargeDirection = new Vector2(TeamManager.Instance.AttackDirFor(Side), 0f);
            goroChargeDirection.Normalize();
            SetFacing(goroChargeDirection);
            if (bodyCollider != null) bodyCollider.isTrigger = true;
            goroChargeRoutine = StartCoroutine(GoroChargeRoutine(duration));
        }

        public void BeginGoroSlam(float windupSeconds, float radius)
        {
            if (goroSlamRoutine != null) StopCoroutine(goroSlamRoutine);
            goroSlamRoutine = StartCoroutine(GoroSlamRoutine(windupSeconds, radius));
        }

        private IEnumerator GoroChargeRoutine(float duration)
        {
            float end = Time.time + Mathf.Max(0.1f, duration);
            while (Time.time < end && Ult != null && Ult.IsActive && Ult.IsCharging &&
                   GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing)
                yield return new WaitForFixedUpdate();

            if (bodyCollider != null) bodyCollider.isTrigger = false;
            Ult?.NotifyGoroChargeEnded();
            goroChargeRoutine = null;
        }

        private IEnumerator GoroSlamRoutine(float windupSeconds, float radius)
        {
            defensiveActionLocked = true;
            float end = Time.time + Mathf.Max(0.05f, windupSeconds);
            while (Time.time < end && Ult != null && Ult.IsActive && Ult.IsSlamming &&
                   GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing)
                yield return null;

            if (Ult == null || !Ult.IsActive || !Ult.IsSlamming || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing)
            {
                defensiveActionLocked = false;
                Ult?.NotifyGoroSlamEnded();
                goroSlamRoutine = null;
                yield break;
            }

            ResolveGoroSlam(radius);
            yield return new WaitForSeconds(0.08f);
            defensiveActionLocked = false;
            Ult?.NotifyGoroSlamEnded();
            goroSlamRoutine = null;
        }

        private void ResolveGoroSlam(float radius)
        {
            if (rb == null || GameConfig.Instance == null) return;
            float push = Mathf.Max(1f, GameConfig.Instance.goroSlamPush);
            Vector2 center = rb.position;
            var hits = Physics2D.OverlapCircleAll(center, Mathf.Max(0.8f, radius));
            GameFeel.Shake(0.10f);
            foreach (var hit in hits)
            {
                var other = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                if (other == null || other == this || other.Side == Side || other.Role == FieldRole.Goalkeeper) continue;
                Vector2 dir = (Vector2)other.transform.position - center;
                if (dir.sqrMagnitude < 0.001f) dir = SafeAim();
                dir.Normalize();
                other.Defense?.CancelForControlChange();
                other.ApplyGoroChargePush(dir * push);
                other.Animation?.Trigger("Hit");
            }
        }

        private IEnumerator FlightRoutine(float duration, float maxHeight)
        {
            // FIX22: Volt is a huge jump with hang time, not a hovering platform. Horizontal
            // movement remains the normal ground solver while the presentation follows a jump arc.
            actionLocked = false;
            SetFlightCollision(true);
            float elapsed = 0f;
            duration = Mathf.Max(0.35f, duration);
            while (elapsed < duration && Ult != null && Ult.IsActive && Ult.IsFlying)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float jump = Mathf.Sin(t * Mathf.PI);
                // Slightly flatten the apex for readable hang time without the old long hover/glide.
                float height = maxHeight * Mathf.Pow(Mathf.Max(0f, jump), 0.82f);
                if (Visual != null) Visual.extraHeight = height;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            if (Visual != null) Visual.extraHeight = 0f;
            SetFlightCollision(false);
            Ult?.NotifyVoltFlightEnded();
            flightRoutine = null;
        }

        private Vector2 GoroShoveDirection(PlayerController other, Vector2 forward)
        {
            if (forward.sqrMagnitude < 0.001f) forward = new Vector2(TeamManager.Instance.AttackDirFor(Side), 0f);
            forward.Normalize();
            Vector2 right = new Vector2(-forward.y, forward.x);
            Vector2 relative = other != null ? (Vector2)other.transform.position - (Vector2)transform.position : right;
            float sideSign = Vector2.Dot(relative, right) >= 0f ? 1f : -1f;
            Vector2 lateral = right * sideSign;
            Vector2 behind = -forward;
            return (lateral * GameConfig.Instance.goroShoveSideBias + behind * GameConfig.Instance.goroShoveBehindBias).normalized;
        }

        private void ResolveGoroChargeContacts()
        {
            if (!IsGoroCharging || rb == null || bodyCollider == null || GameConfig.Instance == null) return;
            float radius = Mathf.Max(0.48f, Mathf.Max(bodyCollider.size.x, bodyCollider.size.y) * 0.62f);
            var hits = Physics2D.OverlapCircleAll(rb.position, radius);
            foreach (var hit in hits)
            {
                var other = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                if (other == null || other == this || other.Side == Side || other.Role == FieldRole.Goalkeeper) continue;
                if (bulldozeHitCooldowns.TryGetValue(other, out float nextAllowed) && Time.time < nextAllowed) continue;
                bulldozeHitCooldowns[other] = Time.time + GameConfig.Instance.goroChargeRepeatSeconds;
                Vector2 shoveDir = GoroShoveDirection(other, goroChargeDirection);
                other.Defense?.CancelForControlChange();
                other.ApplyGoroChargePush(shoveDir * GameConfig.Instance.goroChargePush);
                other.Animation?.Trigger("Hit");
            }
        }

        public void ResetForRestart()
        {
            CancelPendingKick();
            if (flightRoutine != null) StopCoroutine(flightRoutine);
            if (goroChargeRoutine != null) StopCoroutine(goroChargeRoutine);
            if (goroSlamRoutine != null) StopCoroutine(goroSlamRoutine);
            flightRoutine = null;
            goroChargeRoutine = null;
            goroSlamRoutine = null;
            Defense?.CancelForRestart();
            desiredMove = Vector2.zero;
            inputMagnitude = 0f;
            requestedSprint = false;
            IsSprinting = false;
            sprintBlend = 0f;
            actionLocked = false;
            defensiveActionLocked = false;
            kickBusy = false;
            externalSpeedMultiplier = 1f;
            externalPushVelocity = Vector2.zero;
            impactVisualStartedAt = -999f;
            impactVisualDuration = 0f;
            impactVisualPeak = 0f;
            bulldozeHitCooldowns.Clear();
            IsSuppressed = false;
            if (bodyCollider != null) bodyCollider.enabled = true;
            Ult?.NotifyGoroSlamEnded();
            KeeperHoldUntil = 0f;
            VisualKickType = KickType.None;
            VisualKickStartedAt = -999f;
            VisualKickUntil = -999f;
            possessionProtectedUntil = 0f;
            tackleLockedUntil = 0f;
            // Sprint stamina is match state, not restart state. Goals/kickoffs do not refill it;
            // it recovers through the normal non-sprinting recovery rule. A fresh scene/match starts full.
            SetFlightCollision(false);
            if (bodyCollider != null) bodyCollider.isTrigger = false;
            SetBodyScale(1f);
            StopImmediately();
            Visual?.SetAlpha(1f);
            GetComponent<AIController>()?.ResetBrain();
        }

        public void TeleportTo(Vector2 pos)
        {
            ClearMovementInput(true);
            externalPushVelocity = Vector2.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.position = pos;
                // Write the Transform as well so interpolation cannot visually preserve the old
                // pre-goal location for a frame during a hard restart.
                transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                rb.Sleep();
            }
            else transform.position = pos;
        }

        public void StopImmediately()
        {
            ClearMovementInput(true);
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryBulldozeCollision(collision);
        private void OnCollisionStay2D(Collision2D collision) => TryBulldozeCollision(collision);

        private void TryBulldozeCollision(Collision2D collision)
        {
            if ((!IsGoroBulldozing && !IsGoroCharging) || collision == null || GameConfig.Instance == null) return;
            var other = collision.collider.GetComponentInParent<PlayerController>();
            if (other == null || other == this || other.Side == Side || other.Role == FieldRole.Goalkeeper) return;

            float repeat = IsGoroCharging ? GameConfig.Instance.goroChargeRepeatSeconds : GameConfig.Instance.goroBulldozeRepeatSeconds;
            if (bulldozeHitCooldowns.TryGetValue(other, out float nextAllowed) && Time.time < nextAllowed) return;
            bulldozeHitCooldowns[other] = Time.time + repeat;

            // Normal ult contact shrugs defenders away. CHARGE is intentionally much more
            // explosive: Goro holds his line while the opponent is launched several spaces away.
            float advantage = Mathf.Clamp(StrengthMultiplier / Mathf.Max(0.5f, other.StrengthMultiplier), 1.10f, 2.35f);
            other.Defense?.CancelForControlChange();
            Vector2 forward = IsGoroCharging ? goroChargeDirection : (MoveFacing.sqrMagnitude > 0.01f ? MoveFacing.normalized : new Vector2(TeamManager.Instance.AttackDirFor(Side), 0f));
            Vector2 shoveDir = GoroShoveDirection(other, forward);
            if (IsGoroCharging) other.ApplyGoroChargePush(shoveDir * GameConfig.Instance.goroChargePush);
            else other.ApplyBulldozePush(shoveDir * GameConfig.Instance.goroBulldozePush * advantage);
            other.Animation?.Trigger("Hit");
        }

        private void UpdateAnimation()
        {
            if (Animation == null || rb == null || GameConfig.Instance == null) return;
            float norm = rb.linearVelocity.magnitude / Mathf.Max(0.1f, GameConfig.Instance.baseMoveSpeed * GameConfig.Instance.gameplayPaceMultiplier * baseSpeed);
            Animation.SetMovement(norm, IsSprinting, HasBall);
        }
    }
}
