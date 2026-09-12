using System.Collections.Generic;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;

namespace BeastSoccer.Ball
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class BallControl : MonoBehaviour
    {
        public static BallControl Instance { get; private set; }
        public enum BallMode { Free, Controlled, ActionSetup }

        public BallMode Mode { get; private set; } = BallMode.Free;
        public PlayerController Owner { get; private set; }
        public PlayerController LastTouch { get; private set; }
        public PlayerController LastKicker { get; private set; }
        public KickType LastKickType { get; private set; } = KickType.None;
        public float LastKickTime { get; private set; } = -999f;
        public bool IsShot => LastKickType == KickType.Shot && Mode == BallMode.Free;
        public bool LastKickWasUltShot { get; private set; }
        public bool LastShotEligible { get; private set; }
        public float VisualArcHeight { get; private set; }
        public float VisualArc01 { get; private set; }
        public float DribbleVisualBobHeight { get; private set; }
        public bool IsAirborne => VisualArcHeight > 0.01f;

        private Rigidbody2D rb;
        private Vector2 lastOwnerDir = Vector2.right;
        private float recaptureLockoutUntil;
        private PlayerController recaptureLockedPlayer;
        private float actionUntil;
        private PlayerController actionOwner;
        private float arcDuration;
        private float arcElapsed;
        private float arcPeak;
        private float airborneStartedAt = -999f;
        private int groundBouncesRemaining;
        private PlayerController pendingHumanPassReceiver;
        private float pendingHumanPassReceiverUntil;
        private bool pendingReceiverHardLock;
        private readonly HashSet<PlayerController> interceptionChecked = new HashSet<PlayerController>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = GameConfig.Instance != null ? GameConfig.Instance.ballLinearDrag : 1.15f;
        }

        private void FixedUpdate()
        {
            if (GameConfig.Instance != null) rb.linearDamping = GameConfig.Instance.ballLinearDrag;
            if (GameManager.Instance == null) return;

            // During kickoff keep an already-owned ball attached to the kicker, but never let a
            // loose ball auto-receive or carry residual physics through a whistle/reset state.
            if (GameManager.Instance.Phase != MatchPhase.Playing)
            {
                if (GameManager.Instance.Phase == MatchPhase.Kickoff && Mode == BallMode.Controlled)
                    FollowOwner();
                else if (Mode == BallMode.Free)
                    rb.linearVelocity = Vector2.zero;
                return;
            }

            UpdateVisualArc();

            // Aerial lobs/crosses must never become permanently non-interactable. If an arc state
            // survives longer than intended (restart, damping, unusual collision), force it back to
            // a grounded/receivable state.
            if (Mode == BallMode.Free && LastKickType == KickType.Lob &&
                Time.time - airborneStartedAt >= GameConfig.Instance.aerialRecoverFailsafeSeconds)
            {
                VisualArcHeight = 0f;
                VisualArc01 = 1f;
                arcPeak = 0f;
                arcElapsed = arcDuration;
                groundBouncesRemaining = 0;
            }

            if (Mode == BallMode.Controlled) FollowOwner();
            else if (Mode == BallMode.ActionSetup) FollowActionOwner();
            else
            {
                DribbleVisualBobHeight = 0f;
                ApplyIntendedReceiverAssist();
                TryAutoReceive();
            }
        }

        public bool SetOwner(PlayerController p, bool force = false)
        {
            if (p == null) return false;
            // Home outfield possession is always player-controlled. There is no autonomous
            // home attacking AI: whoever actually owns the ball becomes the human-controlled player.
            bool transferHumanControl = p.Side == TeamSide.Home && p.Role != FieldRole.Goalkeeper;
            pendingHumanPassReceiver = null;
            pendingHumanPassReceiverUntil = 0f;
            pendingReceiverHardLock = false;
            Owner = p;
            LastShotEligible = false;
            VisualArcHeight = 0f;
            arcPeak = 0f;
            groundBouncesRemaining = 0;
            if (p.Role == FieldRole.Goalkeeper)
                p.SetKeeperHold(DuelRules.Enabled ? DemoMatchRules.KeeperHoldSeconds : GameConfig.Instance.keeperPossessionSeconds);
            LastTouch = p;
            LastKickType = KickType.None;
            LastKickWasUltShot = false;
            Mode = BallMode.Controlled;
            actionOwner = null;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = p.transform.position;
            GameManager.Instance?.SetPossession(p.Side == TeamSide.Home ? Possession.Home : Possession.Away);
            interceptionChecked.Clear();
            if (TeamManager.Instance != null)
            {
                if (transferHumanControl && !p.IsHuman) TeamManager.Instance.SetHuman(p, false, true);
                else if (p.Side == TeamSide.Away && GameManager.Instance != null && GameManager.Instance.Mode == GameMode.Regular)
                    TeamManager.Instance.AdjustHumanForOpponentPossession(p.transform.position);
            }
            return true;
        }

        public bool Release(PlayerController from)
        {
            if (Owner != from) return false;
            Owner = null;
            actionOwner = null;
            Mode = BallMode.Free;
            rb.bodyType = RigidbodyType2D.Dynamic;
            GameManager.Instance?.SetPossession(Possession.Loose);
            return true;
        }

        public bool BeginAction(PlayerController p, Vector2 aimDir, float setupSeconds)
        {
            if (Owner != p) return false;
            actionOwner = p;
            Mode = BallMode.ActionSetup;
            actionUntil = Time.time + Mathf.Max(0.02f, setupSeconds);
            lastOwnerDir = SafeDir(aimDir, new Vector2(TeamManager.Instance.AttackDirFor(p.Side),0f));
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        public void CancelActionSetup(PlayerController p)
        {
            if (p == null || Owner != p || actionOwner != p || Mode != BallMode.ActionSetup) return;
            actionOwner = null;
            Mode = BallMode.Controlled;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        public bool Kick(PlayerController kicker, Vector2 direction, float force, KickType type, float visualArc, PlayerController intendedReceiver = null)
        {
            if (kicker == null || Owner != kicker) return false;
            direction = SafeDir(direction, new Vector2(TeamManager.Instance.AttackDirFor(kicker.Side),0f));
            LastTouch = kicker;
            LastKicker = kicker;
            LastKickType = type;
            LastShotEligible = type == KickType.Shot && DuelRules.CanScoreFrom(kicker);
            LastKickWasUltShot = type == KickType.Shot && kicker.Ult != null && kicker.Ult.IsActive;
            LastKickTime = Time.time;
            // Possession play is direct-control football: on a user pass/lob, control transfers
            // immediately to the intended receiver so the user can run onto the ball. The passer
            // becomes AI-controlled at the same instant. This also applies to the opening kickoff.
            bool switchToReceiver = GameManager.Instance != null &&
                (GameManager.Instance.Phase == MatchPhase.Playing || GameManager.Instance.Phase == MatchPhase.Kickoff) &&
                kicker.IsHuman && kicker.Side == TeamSide.Home && intendedReceiver != null &&
                intendedReceiver.Side == TeamSide.Home && intendedReceiver.Role != FieldRole.Goalkeeper &&
                (type == KickType.Pass || type == KickType.Through || type == KickType.Lob);
            bool targetedDistribution = intendedReceiver != null && LastKicker != null &&
                intendedReceiver.Side == kicker.Side &&
                (type == KickType.Pass || type == KickType.Through || type == KickType.Lob);
            pendingHumanPassReceiver = targetedDistribution ? intendedReceiver : null;
            pendingHumanPassReceiverUntil = targetedDistribution
                ? Time.time + Mathf.Max(0.4f, GameConfig.Instance.intendedReceiverAssistSeconds)
                : 0f;
            // FIX26: lobbed passes/crosses and throw-ins are deliberately receiver-locked.
            // Ground passes retain the softer assist so they still feel free/physical.
            // Fast keeper throws need a landing target as well as launch speed. Otherwise the
            // ball keeps travelling after passing the receiver and can clear the touchline.
            pendingReceiverHardLock = targetedDistribution && type == KickType.Lob &&
                (kicker.Role != FieldRole.Goalkeeper || DuelRules.Enabled);
            interceptionChecked.Clear();
            Owner = null;
            actionOwner = null;
            Mode = BallMode.Free;
            rb.bodyType = RigidbodyType2D.Dynamic;
            bool fastKeeperOutlet = DuelRules.Enabled && kicker.Role == FieldRole.Goalkeeper && type == KickType.Lob;
            // Keeper outlets have their own direct launch profile. Applying the global shot boost
            // made the arc last longer and read like a hovering ball rather than a quick throw.
            if (DuelRules.Enabled && !fastKeeperOutlet) force *= DemoMatchRules.BallSpeedBoost;
            rb.linearVelocity = direction * force;
            if (switchToReceiver && TeamManager.Instance != null)
                TeamManager.Instance.SetHuman(intendedReceiver, false, true);
            recaptureLockoutUntil = Time.time + GameConfig.Instance.receiveProtectionSeconds;
            recaptureLockedPlayer = kicker;
            GameManager.Instance?.SetPossession(Possession.Loose);
            // A regular kickoff becomes live only when the centre pass actually leaves the foot.
            GameManager.Instance?.NotifyKickoffTaken(kicker, type);
            if (fastKeeperOutlet)
            {
                float outletDistance = intendedReceiver != null
                    ? Vector2.Distance(kicker.transform.position, intendedReceiver.transform.position)
                    : 5f;
                StartVisualArc(Mathf.Min(.34f, visualArc), Mathf.Clamp(outletDistance / Mathf.Max(7f, force), .34f, .58f));
                groundBouncesRemaining = 0;
                if (pendingReceiverHardLock)
                    pendingHumanPassReceiverUntil = Mathf.Max(pendingHumanPassReceiverUntil, Time.time + arcDuration + .1f);
                // Set the very first physics step too: a receiver near the keeper or touchline
                // must not be overshot before FixedUpdate can begin steering.
                if (pendingReceiverHardLock)
                    rb.linearVelocity = (KeeperOutletLandingPoint(intendedReceiver.transform.position) - rb.position) / arcDuration;
            }
            else StartVisualArc(visualArc, Mathf.Clamp(force / 10f, 0.35f, 1.5f));
            GameManager.Instance?.NotifySetPieceTaken(kicker, type);
            return true;
        }

        public bool IsRecentKickBy(TeamSide side, float seconds)
        {
            return Mode == BallMode.Free && LastKickType != KickType.None && LastKicker != null && LastKicker.Side == side && Time.time - LastKickTime <= Mathf.Max(0f, seconds);
        }

        public void RegisterPhysicalTouch(PlayerController p)
        {
            if (p != null) LastTouch = p;
        }

        public void ForceReleaseAndStop()
        {
            LastShotEligible = false;
            Owner = null;
            actionOwner = null;
            Mode = BallMode.Free;
            LastKickType = KickType.None;
            LastKickWasUltShot = false;
            LastKicker = null;
            LastKickTime = -999f;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            VisualArcHeight = 0f;
            VisualArc01 = 0f;
            DribbleVisualBobHeight = 0f;
            airborneStartedAt = -999f;
            groundBouncesRemaining = 0;
            pendingHumanPassReceiver = null;
            pendingHumanPassReceiverUntil = 0f;
            pendingReceiverHardLock = false;
            interceptionChecked.Clear();
            GameManager.Instance?.SetPossession(Possession.Loose);
        }

        public void TeleportFree(Vector2 pos)
        {
            ForceReleaseAndStop();
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.Sleep();
        }

        public void Deflect(Vector2 velocity, PlayerController toucher)
        {
            Owner = null;
            actionOwner = null;
            Mode = BallMode.Free;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = velocity;
            LastTouch = toucher;
            LastKicker = null;
            LastKickType = KickType.None;
            LastKickWasUltShot = false;
            LastKickTime = Time.time;
            recaptureLockoutUntil = Time.time + 0.08f;
            recaptureLockedPlayer = toucher;
            DribbleVisualBobHeight = 0f;
            pendingHumanPassReceiver = null;
            pendingHumanPassReceiverUntil = 0f;
            pendingReceiverHardLock = false;
            GameManager.Instance?.SetPossession(Possession.Loose);
        }

        private void FollowOwner()
        {
            if (Owner == null) { ForceReleaseAndStop(); return; }
            if (DuelRules.Enabled && Owner.Role == FieldRole.Goalkeeper)
            {
                lastOwnerDir = new Vector2(TeamManager.Instance.AttackDirFor(Owner.Side), 0f);
                VisualArcHeight = .85f;
                DribbleVisualBobHeight = 0f;
                rb.MovePosition((Vector2)Owner.transform.position + lastOwnerDir * .38f);
                return;
            }
            Vector2 dir = Owner.MoveFacing;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(TeamManager.Instance.AttackDirFor(Owner.Side),0f);
            lastOwnerDir = dir.normalized;
            float baseLead = Owner.IsSprinting ? GameConfig.Instance.sprintDribbleLead : GameConfig.Instance.dribbleLead;
            bool liveDribble = GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing;
            var ownerRb = Owner.GetComponent<Rigidbody2D>();
            float ownerSpeed = ownerRb != null ? ownerRb.linearVelocity.magnitude : 0f;
            float referenceSpeed = Mathf.Max(0.25f, GameConfig.Instance.baseMoveSpeed * GameConfig.Instance.gameplayPaceMultiplier);
            float move01 = liveDribble ? Mathf.Clamp01(ownerSpeed / referenceSpeed) : 0f;

            float amplitude = liveDribble
                ? (Owner.IsSprinting ? GameConfig.Instance.sprintDribbleTouchAmplitude : GameConfig.Instance.dribbleTouchAmplitude) * move01
                : 0f;
            float frequency = Owner.IsSprinting ? GameConfig.Instance.sprintDribbleTouchFrequency : GameConfig.Instance.dribbleTouchFrequency;
            float touchWave = liveDribble ? (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f : 0.5f;
            float lead = Mathf.Max(0.18f, baseLead - amplitude * 0.45f + touchWave * amplitude);

            if (liveDribble && move01 > 0.05f)
            {
                DribbleVisualBobHeight = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f);
                DribbleVisualBobHeight = Mathf.Max(0f, DribbleVisualBobHeight) * GameConfig.Instance.dribbleVisualBob * move01;
            }
            else DribbleVisualBobHeight = 0f;

            Vector2 desired = (Vector2)Owner.transform.position + lastOwnerDir * lead;
            // Do not let the cosmetic dribble lead itself manufacture throw-ins when a player runs
            // along the touchline. The carrier can still play/kick the ball out normally.
            float touchlineLimit = GameConfig.Instance.pitchWidth * 0.5f - GameConfig.Instance.ballVisualRadius - 0.04f;
            desired.y = Mathf.Clamp(desired.y, -touchlineLimit, touchlineLimit);
            rb.MovePosition(desired);
        }

        private void FollowActionOwner()
        {
            if (actionOwner == null || Owner != actionOwner)
            {
                Mode = Owner != null ? BallMode.Controlled : BallMode.Free;
                return;
            }
            DribbleVisualBobHeight = 0f;
            Vector2 contact = (Vector2)actionOwner.transform.position + lastOwnerDir * GameConfig.Instance.dribbleLead * 0.72f;
            rb.MovePosition(Vector2.Lerp(rb.position, contact, 0.55f));
            if (Time.time >= actionUntil) Mode = BallMode.Controlled;
        }

        public bool CanOutfieldControlLob(PlayerController p)
        {
            if (LastKickType != KickType.Lob) return true;
            if (p == null || GameConfig.Instance == null) return false;

            // Descending: once the ball drops below control height, normal receiving/interception resumes.
            if (VisualArc01 >= 0.5f)
                return VisualArcHeight <= GameConfig.Instance.lobOutfieldControlHeight;

            // Ascending/high: teammates cannot vacuum their own lob early, and opponents only get
            // a chance if they are directly in the launch lane before the ball has climbed over them.
            if (LastKicker == null || p.Side == LastKicker.Side) return false;
            if (VisualArcHeight > GameConfig.Instance.lobOutfieldControlHeight) return false;

            Vector2 launchDir = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f
                ? rb.linearVelocity.normalized
                : ((Vector2)transform.position - (Vector2)LastKicker.transform.position).normalized;
            Vector2 toPlayer = (Vector2)p.transform.position - (Vector2)LastKicker.transform.position;
            float distance = toPlayer.magnitude;
            if (distance < 0.05f || distance > GameConfig.Instance.lobEarlyInterceptMaxDistance) return false;
            float angle = Vector2.Angle(launchDir, toPlayer / distance);
            return angle <= GameConfig.Instance.lobEarlyInterceptHalfAngleDegrees;
        }

        private void ApplyIntendedReceiverAssist()
        {
            if (pendingHumanPassReceiver == null || Time.time > pendingHumanPassReceiverUntil || rb == null) return;
            if (pendingHumanPassReceiver.IsSuppressed || pendingHumanPassReceiver.Role == FieldRole.Goalkeeper) return;
            Vector2 targetPosition = pendingHumanPassReceiver.transform.position;
            if (IsKeeperOutlet) targetPosition = KeeperOutletLandingPoint(targetPosition);
            Vector2 toTarget = targetPosition - rb.position;
            if (pendingReceiverHardLock && IsKeeperOutlet && toTarget.sqrMagnitude < .0004f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            if (toTarget.sqrMagnitude < 0.0004f) return;

            if (pendingReceiverHardLock && LastKickType == KickType.Lob)
            {
                // Crosses/throw-ins are intentionally exact in FIX26. Recompute the velocity every
                // physics step so a moving receiver is still where the ball lands when the arc ends.
                float remaining = Mathf.Max(0.08f, arcDuration - arcElapsed);
                rb.linearVelocity = toTarget / remaining;
                return;
            }

            if (rb.linearVelocity.sqrMagnitude < 0.04f) return;
            float speed = rb.linearVelocity.magnitude;
            Vector2 currentDir = rb.linearVelocity / Mathf.Max(0.01f, speed);
            Vector2 wantedDir = toTarget.normalized;
            float t = Mathf.Clamp01(GameConfig.Instance.intendedReceiverSteerPerSecond * Time.fixedDeltaTime);
            Vector2 assisted = Vector2.Lerp(currentDir, wantedDir, t).normalized;
            rb.linearVelocity = assisted * speed;
        }

        private void TryAutoReceive()
        {
            // Unity 6 deprecates OverlapCircleNonAlloc. This allocates a very small temporary array,
            // which is acceptable for the prototype and removes the obsolete API dependency.
            bool intendedWindow = pendingHumanPassReceiver != null && Time.time <= pendingHumanPassReceiverUntil;
            float receiveQueryRadius = intendedWindow
                ? Mathf.Max(GameConfig.Instance.receiveRadius, GameConfig.Instance.intendedReceiverCatchRadius)
                : GameConfig.Instance.receiveRadius;
            var hits = Physics2D.OverlapCircleAll(rb.position, receiveQueryRadius);

            // A hard shot should not be vacuum-caught by the normal 0.6-unit receive radius. That was
            // making almost every strike disappear into the first defender it passed near. Outfield
            // players can only body-block a fast shot when the ball actually crosses a much smaller
            // body radius; goalkeepers continue to use their dedicated save logic.
            if (IsShot && rb.linearVelocity.magnitude > GameConfig.Instance.shotOutfieldControlMaxSpeed)
            {
                PlayerController blocker = null;
                float blockSq = GameConfig.Instance.shotBlockRadius * GameConfig.Instance.shotBlockRadius;
                float bestBlockSq = blockSq;
                foreach (var hit in hits)
                {
                    var p = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                    if (p == null || p.IsSuppressed || p.Role == FieldRole.Goalkeeper || p.IsFlying) continue;
                    if (LastKicker != null && p.Side == LastKicker.Side) continue;
                    float sq = ((Vector2)p.transform.position - rb.position).sqrMagnitude;
                    if (sq <= bestBlockSq) { bestBlockSq = sq; blocker = p; }
                }

                if (blocker != null)
                {
                    Vector2 incoming = SafeDir(rb.linearVelocity, Vector2.right);
                    Vector2 normal = (rb.position - (Vector2)blocker.transform.position);
                    if (normal.sqrMagnitude < 0.001f) normal = new Vector2(-incoming.y, incoming.x);
                    normal.Normalize();
                    Vector2 deflectDir = SafeDir(incoming * 0.45f + normal * 0.85f, normal);
                    float deflectSpeed = Mathf.Max(2.8f, rb.linearVelocity.magnitude * GameConfig.Instance.shotBlockSpeedRetention);
                    Deflect(deflectDir * deflectSpeed, blocker);
                    blocker.Animation?.Trigger("Block");
                    TeamManager.Instance?.AddTeamUltCharge(blocker.Side, GameConfig.Instance.ultChargePerAction);
                }
                return;
            }

            PlayerController best = null;
            float bestSq = float.MaxValue;
            foreach (var hit in hits)
            {
                var p = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                if (p == null || p.IsSuppressed || p.IsActionLocked || p.IsWingBlocking || p.IsFlying) continue;
                // All catches go through the save zone, including its one-roll miss cooldown.
                if (DuelRules.Enabled && p.Role == FieldRole.Goalkeeper) continue;
                if (pendingReceiverHardLock && intendedWindow && p != pendingHumanPassReceiver) continue;
                if (GameManager.Instance != null && GameManager.Instance.Mode == GameMode.Defending && p.Side == TeamSide.Home && p.Role != FieldRole.Goalkeeper) continue;
                if (p.Role != FieldRole.Goalkeeper && LastKickType == KickType.Lob && !CanOutfieldControlLob(p)) continue;

                float candidateDistance = Vector2.Distance(p.transform.position, rb.position);
                if (intendedWindow)
                {
                    if (p == pendingHumanPassReceiver)
                    {
                        if (candidateDistance > GameConfig.Instance.intendedReceiverCatchRadius) continue;
                    }
                    else if (LastKicker != null && p.Side != LastKicker.Side && p.Role != FieldRole.Goalkeeper)
                    {
                        // Ordinary interceptions are deliberately rare. Possession should usually
                        // change through the TACKLE action, not through a broad invisible pickup aura.
                        if (candidateDistance > GameConfig.Instance.rareInterceptionRadius) continue;
                        if (interceptionChecked.Contains(p)) continue;
                        interceptionChecked.Add(p);
                        if (Random.value > GameConfig.Instance.rareInterceptionChance) continue;
                    }
                    else if (candidateDistance > GameConfig.Instance.receiveRadius) continue;
                }
                else if (candidateDistance > GameConfig.Instance.receiveRadius) continue;

                // Goalkeepers do not auto-receive their own team's loose outlet/back-pass in the
                // generic reception system. Saves and opponent shots are handled normally.
                if (p.Role == FieldRole.Goalkeeper)
                {
                    TeamSide opponent = p.Side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                    bool opponentThreat = (LastKicker != null && LastKicker.Side == opponent) ||
                                          (LastTouch != null && LastTouch.Side == opponent);
                    bool neutral = LastKicker == null && LastTouch == null;
                    if (!opponentThreat && !neutral) continue;
                }

                if (Time.time < recaptureLockoutUntil && p == recaptureLockedPlayer) continue;
                float sq = ((Vector2)p.transform.position - rb.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            if (best != null) best.GainBall();
        }

        private void StartVisualArc(float peak, float duration)
        {
            arcPeak = Mathf.Max(0f, peak);
            arcDuration = Mathf.Max(0.15f, duration);
            arcElapsed = 0f;
            airborneStartedAt = Time.time;
            groundBouncesRemaining = LastKickType == KickType.Lob && GameConfig.Instance != null
                ? Mathf.Max(0, GameConfig.Instance.aerialGroundBounceCount)
                : 0;
            DribbleVisualBobHeight = 0f;
        }

        private void UpdateVisualArc()
        {
            if (arcPeak <= 0f)
            {
                VisualArcHeight = 0f;
                VisualArc01 = 0f;
                return;
            }

            arcElapsed += Time.fixedDeltaTime;
            VisualArc01 = Mathf.Clamp01(arcElapsed / Mathf.Max(0.01f, arcDuration));
            VisualArcHeight = Mathf.Sin(VisualArc01 * Mathf.PI) * arcPeak;

            if (arcElapsed < arcDuration) return;

            VisualArcHeight = 0f;
            VisualArc01 = 1f;

            if (pendingReceiverHardLock && pendingHumanPassReceiver != null &&
                Time.time <= pendingHumanPassReceiverUntil && !pendingHumanPassReceiver.IsSuppressed)
            {
                var receiver = pendingHumanPassReceiver;
                Vector2 landing = IsKeeperOutlet
                    ? KeeperOutletLandingPoint(receiver.transform.position)
                    : (Vector2)receiver.transform.position;
                rb.position = landing;
                rb.linearVelocity = Vector2.zero;
                pendingReceiverHardLock = false;
                pendingHumanPassReceiver = null;
                pendingHumanPassReceiverUntil = 0f;
                arcPeak = 0f;
                groundBouncesRemaining = 0;
                // If a receiver cannot collect (e.g. airborne or beyond the safe landing
                // margin), leave a grounded loose ball in play rather than teleport it out.
                if (!IsKeeperOutlet || (!receiver.IsFlying && !receiver.IsWingBlocking &&
                    Vector2.Distance(receiver.transform.position, landing) <= GameConfig.Instance.receiveRadius))
                    receiver.GainBall();
                return;
            }

            // A lob/cross lands with a couple of small visual/velocity bounces instead of snapping
            // instantly to the turf. This keeps the 2D simulation simple while the 3D visual ball
            // reads like a physical football.
            if (LastKickType == KickType.Lob && Mode == BallMode.Free && groundBouncesRemaining > 0 &&
                rb.linearVelocity.magnitude > 0.45f && GameConfig.Instance != null)
            {
                groundBouncesRemaining--;
                arcPeak = Mathf.Max(0.04f, arcPeak * GameConfig.Instance.aerialGroundBounceHeightRetention);
                arcDuration = Mathf.Max(0.16f, arcDuration * GameConfig.Instance.aerialGroundBounceTimeRetention);
                arcElapsed = 0f;
                rb.linearVelocity *= GameConfig.Instance.aerialGroundBounceSpeedRetention;
                return;
            }

            arcPeak = 0f;
            groundBouncesRemaining = 0;
        }

        private bool IsKeeperOutlet => DuelRules.Enabled && LastKickType == KickType.Lob &&
            LastKicker != null && LastKicker.Role == FieldRole.Goalkeeper;

        private static Vector2 KeeperOutletLandingPoint(Vector2 target)
        {
            var config = GameConfig.Instance;
            if (config == null) return target;
            // Keep the whole enlarged visual ball inside the pitch at landing.
            float margin = Mathf.Max(.75f, config.ballVisualRadius * DemoMatchRules.BallScale + .20f);
            float halfLength = Mathf.Max(.1f, config.pitchLength * .5f - margin);
            float halfWidth = Mathf.Max(.1f, config.pitchWidth * .5f - margin);
            return new Vector2(Mathf.Clamp(target.x, -halfLength, halfLength),
                Mathf.Clamp(target.y, -halfWidth, halfWidth));
        }

        private static Vector2 SafeDir(Vector2 dir, Vector2 fallback)
        {
            return dir.sqrMagnitude > 0.001f ? dir.normalized : fallback.normalized;
        }
    }
}
