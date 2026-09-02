using UnityEngine;

namespace BeastSoccer.Data
{
    [DefaultExecutionOrder(-1000)]
    public class GameConfig : MonoBehaviour
    {
        public static GameConfig Instance { get; private set; }

        [Header("Match")]
        public float matchRealSeconds = 300f;
        public float kickoffDelay = 0.9f;
        public float kickoffReturnDelay = 0.38f;
        public float postGoalDelay = 1.5f;
        public float postSaveDelay = 1.15f;
        public float outOfBoundsRestartDelay = 0.75f;

        [Header("Set Pieces")]
        public float setPieceSetupDelay = 1.65f;
        public float setPieceReadyDelay = 0.75f;
        public float throwInInset = 0.22f;
        public float goalKickDepth = 3.0f;
        public float cornerInset = 0.28f;

        [Header("Pitch / Presentation")]
        public float pitchLength = 24f;
        public float pitchWidth = 15.86f;
        public float cameraAngle = 50f;
        public float cameraVisibleFraction = 0.92f;
        public float finalStretchCameraTighten = 0.93f;
        public float cameraFollowLerp = 4.0f;
        public bool enableHaptics = true;
        public float cameraLookAhead = 1.15f;
        public float cameraOutsideViewMargin = 4.0f;
        public float cameraBallVelocityLookAheadSeconds = 0.10f;
        public float cameraBallVelocityLookAheadMax = 1.20f;
        public float ballVisualRadius = 0.18f;
        public bool showDebugPlayerIndicators = true;
        public bool showUltDebugOverlay = false;

        [Header("Movement")]
        public float gameplayPaceMultiplier = 1.175f;
        public float baseMoveSpeed = 1.55f;
        public float acceleration = 8.0f;
        public float deceleration = 5.8f;
        public float sprintMultiplier = 1.50f;
        public float sprintEnterThreshold = 0.88f;
        public float sprintExitThreshold = 0.78f;
        public float sprintRampSeconds = 0.22f;
        // Always-on character identity tweaks.
        public float voltSprintRampMultiplier = 0.55f;
        public float minMoveGait = 0.52f;
        public float fullMoveGaitThreshold = 0.72f;
        public float playerLinearDamping = 2.0f;
        public float hardSpeedCapMultiplier = 1.18f;
        public float externalPushMaxSpeed = 1.15f;
        public float externalPushDecay = 7.5f;
        public float pitchPlayerPadding = 0.45f;

        [Header("Sprint Stamina")]
        // Stamina affects sprint only. Normal running, passing, shooting and abilities are unchanged.
        // Sprint drains by DISTANCE rather than button-held time: a full bar is roughly one full
        // pitch length of actual sprint travel. This also makes the UI/debug result deterministic.
        public float sprintStaminaDrainDistance = 24f;
        public float sprintStaminaRecoverSeconds = 6.5f; // empty-to-full recovery time once recovery begins
        public float sprintStaminaRecoveryDelay = 0.75f;
        [Range(0f,1f)] public float sprintStaminaResumeThreshold = 0.22f;

        [Header("Character Base Stats")]
        public float leoSpeed = 1.06f, leoAccel = 1.05f, leoShot = 1.12f, leoStrength = 1.00f, leoTackle = 1.00f;
        public float goroSpeed = 0.91f, goroAccel = 0.90f, goroShot = 1.22f, goroStrength = 1.35f, goroTackle = 1.20f;
        public float voltSpeed = 1.12f, voltAccel = 1.10f, voltShot = 0.98f, voltStrength = 0.85f, voltTackle = 0.90f;
        public float genericSpeed = 0.94f, genericAccel = 0.94f, genericShot = 0.95f, genericStrength = 0.95f, genericTackle = 0.95f;
        public float leoPassAssistConeBonusDegrees = 18f;
        public float goroTackleWindupMultiplier = 1.25f;
        public float goroTackleRangeMultiplier = 1.12f;
        public float goroTackleFollowThroughMultiplier = 1.22f;

        [Header("Ball")]
        public float dribbleLead = 0.38f;
        public float sprintDribbleLead = 0.54f;
        public float dribbleTouchAmplitude = 0.070f;
        public float sprintDribbleTouchAmplitude = 0.092f;
        public float dribbleTouchFrequency = 3.2f;
        public float sprintDribbleTouchFrequency = 4.2f;
        public float dribbleVisualBob = 0.010f;
        public float receiveRadius = 0.60f;
        public float receiveProtectionSeconds = 0.28f;
        public float shootContactSeconds = 0.22f;
        public float passContactSeconds = 0.18f;
        public float throughContactSeconds = 0.20f;
        public float lobContactSeconds = 0.20f;
        // Shots use a distance-aware launch speed: hard off the foot, then natural Rigidbody damping.
        // shootForce remains as a fallback for any non-targeted legacy use.
        public float shootForce = 13.5f;
        public float shotMinForce = 12.0f;
        public float shotMaxForce = 20.0f;
        public float shotBaseForce = 9.5f;
        public float shotForcePerUnit = 0.90f;
        // passForce / throughForce remain useful for clearances and fallbacks.
        // Normal targeted passes use the distance-assisted values below.
        public float passForce = 5.2f;
        public float throughForce = 6.5f;
        public float passMinForce = 5.0f;
        public float passMaxForce = 30.0f;
        public float passBaseForce = 2.2f;
        public float passForcePerUnit = 1.22f;
        public float throughMinForce = 6.5f;
        public float throughMaxForce = 34.0f;
        public float throughBaseForce = 2.8f;
        public float throughForcePerUnit = 1.32f;
        // Targeted passes have no user-controlled power meter. Their launch speed is derived
        // from target distance + damping so they still arrive with useful pace.
        public float passArrivalSpeed = 4.15f;
        public float throughArrivalSpeed = 4.6f;
        // Lob is a high assisted pass. While visibly above this height, outfield players cannot
        // auto-trap or intercept it, so it actually clears nearby bodies.
        public float lobMinForce = 6.8f;
        public float lobMaxForce = 28.0f;
        public float lobArrivalSpeed = 4.35f;
        public float lobAssistConeDegrees = 112f;
        public float throwInAssistConeDegrees = 126f;
        [Range(0f,1f)] public float lobDirectionalAssist = 0.84f;
        public float lobTargetLeadSeconds = 0.22f;
        public float lobVisualArc = 1.35f;
        public float lobOutfieldControlHeight = 0.30f;
        // On ascent, only an opponent directly in the launch lane can cut out a lob before it rises.
        // Once high, it is not outfield-interceptible until it descends below control height.
        public float lobEarlyInterceptHalfAngleDegrees = 16f;
        public float lobEarlyInterceptMaxDistance = 1.35f;
        public float aerialRecoverFailsafeSeconds = 3.6f;
        public int aerialGroundBounceCount = 2;
        public float aerialGroundBounceHeightRetention = 0.34f;
        public float aerialGroundBounceTimeRetention = 0.46f;
        public float aerialGroundBounceSpeedRetention = 0.78f;
        public float goalPostBounciness = 0.78f;
        public float ballLinearDrag = 1.30f;
        public float passAssistConeDegrees = 116f;
        // Human targeted passing is intentionally forgiving: choose the teammate nearest the
        // aimed direction, then give that intended receiver a larger catch window and only a
        // very mild mid-flight correction. It should feel assisted, not like a homing ball.
        public float directionalPassMaxDistance = 12.5f;
        public float intendedReceiverCatchRadius = 0.98f;
        public float intendedReceiverAssistSeconds = 2.1f;
        public float intendedReceiverSteerPerSecond = 0.72f;
        public float rareInterceptionRadius = 0.25f;
        [Range(0f,1f)] public float rareInterceptionChance = 0.12f;
        public float passRequestDelayMin = 0.18f;
        public float passRequestDelayMax = 0.42f;
        public bool autoSwitchOnCompletedPass = false; // legacy flag; direct-control switching is now unconditional for home outfield possession
        public float throughLeadSeconds = 0.56f;
        public float shotAimWidthFraction = 1.18f;
        // Shot aiming uses the movement/facing direction projected onto the goal line.
        // A small miss margin lets poorly-aimed shots go wide instead of forcing every strike on target.
        public float shotGoalHalfWidth = 2.08f;
        public float shotMissMargin = 0.55f;
        public float shotForwardAimMin = 0.18f;
        [Range(0f,1f)] public float shotAimAssist = 0.18f;
        // Fast shots cannot be vacuum-trapped by ordinary outfield receive logic.
        // A defender must actually be very close to the ball to body-block it.
        public float shotBlockRadius = 0.34f;
        public float shotOutfieldControlMaxSpeed = 5.5f;
        public float shotBlockSpeedRetention = 0.48f;
        public float shotVisualArc = 0.35f;
        public float passVisualArc = 0.22f;
        public float throughVisualArc = 0.28f;
        public float possessionUiFlightGraceSeconds = 0.62f;
        public float possessionUiDebounceSeconds = 0.15f;

        [Header("Defending")]
        public float tackleRange = 0.90f;
        public float tackleLungeDistance = 0.42f;
        public float tackleWindupSeconds = 0.06f;
        public float tackleActiveSeconds = 0.16f;
        public float tackleRecoverySeconds = 0.34f;
        public float tackleFollowThroughSpeed = 2.85f;
        public float tackleVictimPushSpeed = 6.50f;
        public float tackleVictimVisualLaunchHeight = 0.38f;
        public float tackleVictimVisualLaunchSeconds = 0.62f;
        // Tackles are valid from the front and sides. Only the 120-degree cone directly behind
        // the ball carrier is protected (60 degrees either side of straight-behind).
        public float tackleRearForbiddenHalfAngleDegrees = 60f;
        public float interceptRange = 1.05f;
        public float interceptRecoverySeconds = 0.38f;
        public float jockeyRange = 0.95f;
        public float jockeySeconds = 2f;
        public float jockeyPushSpeed = 1.30f;
        public float defendingGkTriggerDistance = 6.0f;
        public float fadedDefenderAlpha = 0.32f;
        public float fadedDefenderSpeedMultiplier = 0.22f;

        [Header("Goalkeeper")]
        public float keeperGoalOffset = 0.95f;
        public float keeperGoalLineClearance = 0.78f;
        public float keeperMaxDepth = 1.85f;
        public float keeperLateralRange = 2.15f;
        public float defendingHumanKeeperDepth = 3.2f;
        public float keeperSaveRadius = 1.15f;
        public float keeperEngageDistance = 2.10f;
        public float keeperChallengeDistance = 0.95f;
        public float keeperPossessionSeconds = 4.00f;
        public float keeperDistributionMinDistance = 3.0f;
        public float keeperNoCrowdRadius = 3.25f;
        public float keeperNoCrowdPushSpeed = 2.85f;
        public float keeperHoldCentralDepth = 1.25f;
        public float keeperOutletForwardDistance = 5.2f;
        public float keeperOutletWideY = 4.85f;
        public float keeperCallPassImmediateDelay = 0.02f;
        public float throwInNoCrowdRadius = 1.85f;
        public float throwInNoCrowdPushSpeed = 2.6f;
        public float requestedPassLaneBlockRadius = 0.62f;

        [Header("AI Decisions")]
        public float aiDecisionInterval = 0.32f;
        public float aiShootDistance = 4.6f;
        public float aiPassPressureDistance = 2.65f;
        [Range(0f,1f)] public float aiPressurePassChance = 0.52f;
        [Range(0f,1f)] public float aiOpenPassChance = 0.15f;
        [Range(0f,1f)] public float aiThroughPassChance = 0.06f;
        [Range(0f,1f)] public float aiCarryWideChance = 0.24f;
        public float aiCarryWideWidth = 3.45f;
        public float aiCarryWideSecondsMin = 0.85f;
        public float aiCarryWideSecondsMax = 1.45f;
        public float aiCarryLookAhead = 2.75f;
        // Stops short-range AI ping-pong / tackle-pass loops in crowded areas.
        public float aiMinPassDistance = 2.75f;
        public float aiReceiveCommitSeconds = 0.78f;
        public float aiReturnPassCooldown = 1.75f;
        public float postTackleProtectionSeconds = 1.35f;
        public float tackleLockoutAfterLossSeconds = 1.80f;
        public float aiCloseContactEscapeDistance = 1.15f;
        public float aiCloseContactEscapeSeconds = 0.90f;
        public float aiCloseContactEscapeWidth = 2.25f;
        public float aiDuelDisengageSeconds = 1.10f;
        public float aiTackleCooldownSeconds = 0.95f;
        public float aiOpponentTackleCooldownSeconds = 0.42f;
        public float aiOpponentTackleRangeMultiplier = 1.18f;
        public float aiOpponentPresserStandOff = 0.30f;
        public float aiOpponentPressSprintDistance = 1.45f;
        public float aiDuelRetreatDistance = 1.35f;
        public float aiDefensiveThirdFraction = 0.38f;
        public float aiExitForwardMin = 0.80f;
        [Range(0f,1f)] public float aiDefensiveExitPassChance = 0.78f;
        public float aiDefensiveExitWideY = 4.15f;
        [Range(0f,1f)] public float aiJockeyChance = 0.0f; // jockey removed from prototype controls
        [Range(0f,1f)] public float aiUltAggression = 0f;
        public bool enableAIAutoUlts = false;

        [Header("AI Formation")]
        public float aiAnchorGoalDistance = 4.0f;
        public float aiAnchorMaxAdvance = 7.0f;
        public float aiPresserStandOff = 0.78f;
        public float aiRoverForward = 1.35f;
        public float aiRoverWidth = 4.85f;
        public float aiWideDefensiveDepth = 5.4f;
        public float aiAttackAnchorBehindBall = 3.2f;
        public float aiAttackPresserForward = 1.8f;
        public float aiSteeringSlowRadius = 1.1f;
        // Roles are deliberately sticky in the 3+1 prototype: only the Presser AI challenges.
        public float aiRoleSwapAdvantage = 0.62f;
        public float aiMinTeammateSpacing = 2.10f;

        [Header("Endgame / Comeback")]
        public bool enableGoldenGoal = true;
        public float goldenGoalRealSeconds = 60f;
        [Range(0.02f,0.25f)] public float finalStretchFraction = 0.10f;
        public float comebackUltRechargePerGoal = 0.50f;
        public int comebackUltMaxDeficit = 3;
        public float finalStretchAIAutoUltChance = 0.12f;

        [Header("Ultimate")]
        // Every active ultimate receives this universal boost on top of its character-specific identity.
        public float ultUniversalBuff = 1.10f;
        public float ultDurationSeconds = 12f;
        public float ultShotBallSpeedBonus = 1.15f;
        [Range(0f,1f)] public float ultShotKeeperMissChance = 0.30f;
        public float ultTransitionSeconds = 0.34f;
        public float ultActivationSlowMoScale = 0.45f;
        public float ultActivationSlowMoRealSeconds = 0.12f;
        public float ultGoalSlowMoScale = 0.55f;
        public float ultGoalSlowMoRealSeconds = 0.24f;
        public float ultChargeTeamGoal = 0.30f;
        public float ultChargePersonalGoal = 0.65f;
        public float ultChargePerAction = 0.0f; // recharge is timer + goals; ordinary actions no longer add charge
        public float ultChargeSuccessfulTackle = 0.50f;
        public float ultChargeGoal = 1.00f;
        public float ultPassiveRechargePerSec = 0.028f; // ~36 seconds from empty before goal bonuses
        public float leoAtkSpeedBonus = 1.28f;
        public float leoAtkShotBonus = 1.42f;
        public float leoDefSpeedBonus = 1.26f;
        public float leoDefTackleBonus = 1.45f;
        public float goroAtkStrengthBonus = 1.50f;
        public float goroAtkShotBonus = 1.55f;
        public float goroDefStrengthBonus = 1.65f;
        public float goroDefTackleBonus = 1.55f;
        public float goroContactPush = 1.55f;
        public float goroBulldozePush = 4.35f;
        public float goroBulldozeRepeatSeconds = 0.10f;
        public float goroChargeSeconds = 3.0f;
        public float goroChargeSpeed = 5.4f;
        public float goroChargePush = 14.5f;
        public float goroChargeRepeatSeconds = 0.08f;
        public float goroShoveSideBias = 1.05f;
        public float goroShoveBehindBias = 0.48f;
        public float goroChargeShakeInterval = 0.10f;
        public float goroSlamWindupSeconds = 0.16f;
        public float goroSlamCooldown = 1.0f;
        public float goroSlamRadius = 2.35f;
        public float goroSlamPush = 12.0f;
        public float goroLaunchVisualSeconds = 0.34f;
        public float goroLaunchVisualHeight = 0.95f;
        public float goroSizeIncrease = 0.15f;
        public float voltAtkSpeedBonus = 1.35f;
        public float voltAtkShotBonus = 1.15f;
        public float voltFlyDistance = 3.6f;
        public float voltFlySeconds = 4.0f;
        public float voltFlyLaunchSeconds = 0.16f;
        public float voltFlyVisualHeight = 2.25f;
        public float voltFlyVisualBob = 0.025f;
        public float voltWingWidthFraction = 0.28f;
        public float voltWingWalkSpeed = 0.62f;
        public float voltBlockSeconds = 3.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // FIX24 migration: update only known older defaults so an existing generated Match
            // scene can be tested without rebuilding (and without deleting manually added art).
            // FIX32 camera settles between the old close framing and FIX27's very wide framing.
            if (cameraVisibleFraction >= 0.94f || cameraVisibleFraction < 0.87f) cameraVisibleFraction = 0.92f;
            if (Mathf.Approximately(cameraLookAhead, 1.0f)) cameraLookAhead = 1.15f;
            if (cameraOutsideViewMargin >= 5.49f || cameraOutsideViewMargin < 2.3f) cameraOutsideViewMargin = 4.0f;
            if (Mathf.Approximately(dribbleTouchAmplitude, 0.035f) || Mathf.Approximately(dribbleTouchAmplitude, 0.055f)) dribbleTouchAmplitude = 0.070f;
            if (Mathf.Approximately(sprintDribbleTouchAmplitude, 0.055f) || Mathf.Approximately(sprintDribbleTouchAmplitude, 0.075f)) sprintDribbleTouchAmplitude = 0.092f;
            if (Mathf.Approximately(lobArrivalSpeed, 3.8f)) lobArrivalSpeed = 4.35f;
            if (Mathf.Approximately(passAssistConeDegrees, 62f)) passAssistConeDegrees = 116f;
            if (Mathf.Approximately(lobAssistConeDegrees, 82f)) lobAssistConeDegrees = 112f;
            if (Mathf.Approximately(throwInAssistConeDegrees, 100f)) throwInAssistConeDegrees = 126f;
            if (Mathf.Approximately(lobDirectionalAssist, 0.72f)) lobDirectionalAssist = 0.84f;
            if (Mathf.Approximately(passArrivalSpeed, 3.4f)) passArrivalSpeed = 4.15f;
            if (Mathf.Approximately(aiOpponentTackleCooldownSeconds, 0.52f)) aiOpponentTackleCooldownSeconds = 0.42f;
            if (Mathf.Approximately(aiOpponentTackleRangeMultiplier, 1.10f)) aiOpponentTackleRangeMultiplier = 1.18f;
            if (Mathf.Approximately(aiOpponentPresserStandOff, 0.42f)) aiOpponentPresserStandOff = 0.30f;
            if (Mathf.Approximately(aiThroughPassChance, 0.18f)) aiThroughPassChance = 0.06f;
            if (Mathf.Approximately(aiCarryWideChance, 0.38f)) aiCarryWideChance = 0.24f;
            if (Mathf.Approximately(aiCarryWideWidth, 3.85f)) aiCarryWideWidth = 3.45f;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }
    }
}
