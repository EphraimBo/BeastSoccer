using System.Collections;
using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Player;
using BeastSoccer.UI;
using BeastSoccer.Audio;

namespace BeastSoccer.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameMode Mode = GameMode.Regular;
        public CharacterType PlayerCharacter = CharacterType.Leo;
        public CharacterType OpponentCharacter = CharacterType.Goro;
        public bool OpponentRandom;

        public MatchPhase Phase { get; private set; } = MatchPhase.PreMatch;
        public int CurrentHalf { get; private set; } = 1;
        public Possession Possession { get; private set; } = Possession.Loose;
        public bool KickoffReady { get; private set; }
        public bool IsGoldenGoal => MatchTimer.Instance != null && MatchTimer.Instance.InGoldenGoal;
        public SetPieceType CurrentSetPieceType { get; private set; } = SetPieceType.None;
        public TeamSide CurrentSetPieceSide { get; private set; } = TeamSide.Home;
        public bool AwaitingSetPieceKick => awaitingSetPieceKick;
        public PlayerController PreparedSetPieceTaker => preparedSetPieceTaker;

        public bool IsHumanAimingSetPiece(PlayerController player)
        {
            if (player == null || !awaitingSetPieceKick || preparedSetPieceTaker != player || CurrentSetPieceSide != TeamSide.Home) return false;
            return CurrentSetPieceType == SetPieceType.ThrowIn || CurrentSetPieceType == SetPieceType.Corner;
        }

        public System.Action<MatchPhase> OnPhaseChanged;
        public System.Action<int> OnHalfChanged;
        public System.Action<Possession> OnPossessionChanged;

        private Coroutine phaseRoutine;
        private MatchPhase phaseBeforePause = MatchPhase.Playing;
        private bool awaitingSetPieceKick;
        private PlayerController preparedSetPieceTaker;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            ApplyRuntimeSetup();
            if (OpponentRandom) RandomizeOpponent();
            TeamManager.Instance?.ApplyCharacterSelections(PlayerCharacter, OpponentCharacter);
            MatchTimer.Instance?.PauseClock();
            if (Mode == GameMode.Defending) RoundReset.Instance?.ResetDefendingRound();
            else RoundReset.Instance?.ResetForKickoff(TeamSide.Home);
            EnterKickoff();
        }

        private void ApplyRuntimeSetup()
        {
            // FIX21: menu choices are authoritative when a match is launched from MainMenu.
            // If Match is opened directly for debugging, keep the values visible on GameManager
            // in the Inspector instead of silently forcing the static Leo/Goro defaults.
            if (!MatchSetup.HasExplicitSelection) return;
            Mode = MatchSetup.Mode;
            PlayerCharacter = MatchSetup.PlayerCharacter;
            OpponentCharacter = MatchSetup.OpponentCharacter;
            OpponentRandom = MatchSetup.OpponentRandom;
        }

        public void SetPossession(Possession value)
        {
            if (Possession == value) return;
            Possession = value;
            OnPossessionChanged?.Invoke(value);
        }

        public void EnterKickoff()
        {
            CurrentSetPieceType = SetPieceType.None;
            awaitingSetPieceKick = false;
            preparedSetPieceTaker = null;
            KickoffReady = false;
            MatchTimer.Instance?.PauseClock();
            SetPhaseInternal(MatchPhase.Kickoff);
            StartPhaseRoutine(KickoffRoutine());
        }

        public bool CanTakeKickoff(PlayerController player)
        {
            return KickoffReady && Phase == MatchPhase.Kickoff && RoundReset.Instance != null && RoundReset.Instance.IsKickoffKicker(player);
        }

        public void NotifyKickoffTaken(PlayerController kicker, KickType type)
        {
            if (Phase != MatchPhase.Kickoff || !KickoffReady || type != KickType.Pass || RoundReset.Instance == null || !RoundReset.Instance.IsKickoffKicker(kicker)) return;

            KickoffReady = false;
            // Direct-control kickoff: the user now controls the pass receiver, so there is no
            // AI return-pass choreography.
            RoundReset.Instance.ReleaseKickoffLock();
            SetPhaseInternal(MatchPhase.Playing);
            MatchTimer.Instance?.ResumeClock();
        }

        public void BeginSetPiece(SetPieceType type, TeamSide restartSide, Vector2 spot)
        {
            if (Phase == MatchPhase.MatchOver || type == SetPieceType.None) return;
            CancelPhaseRoutine();
            KickoffReady = false;
            awaitingSetPieceKick = false;
            preparedSetPieceTaker = null;
            CurrentSetPieceType = type;
            CurrentSetPieceSide = restartSide;
            MatchTimer.Instance?.PauseClock();
            BeastSoccer.Ball.BallControl.Instance?.ForceReleaseAndStop();
            SetPossession(Possession.Loose);
            TeamManager.Instance?.StopAllPlayers();
            AudioManager.Instance?.PlayWhistle();
            SetPhaseInternal(MatchPhase.SetPiece);
            StartPhaseRoutine(SetPieceRoutine(type, restartSide, spot));
        }

        private IEnumerator SetPieceRoutine(SetPieceType type, TeamSide restartSide, Vector2 spot)
        {
            float setup = GameConfig.Instance != null ? GameConfig.Instance.setPieceSetupDelay : 1.65f;
            float ready = GameConfig.Instance != null ? GameConfig.Instance.setPieceReadyDelay : 0.75f;
            yield return new WaitForSeconds(setup);

            preparedSetPieceTaker = RoundReset.Instance != null
                ? RoundReset.Instance.PrepareSetPiece(type, restartSide, spot)
                : null;

            yield return new WaitForSeconds(ready);
            if (Phase != MatchPhase.SetPiece) yield break;

            awaitingSetPieceKick = preparedSetPieceTaker != null;
            if (!awaitingSetPieceKick)
            {
                CurrentSetPieceType = SetPieceType.None;
                MatchTimer.Instance?.ResumeClock();
                SetPhaseInternal(MatchPhase.Playing);
                yield break;
            }

            // Human throw-ins/corners become an aiming phase: the clock stays frozen, the taker
            // cannot walk away from the spot, but the opposition and off-ball players can move.
            bool humanAim = restartSide == TeamSide.Home &&
                            (type == SetPieceType.ThrowIn || type == SetPieceType.Corner) &&
                            preparedSetPieceTaker.Role != FieldRole.Goalkeeper;

            SetPhaseInternal(MatchPhase.Playing);
            if (humanAim)
            {
                TeamManager.Instance?.SetHuman(preparedSetPieceTaker, false);
                preparedSetPieceTaker.ClearMovementInput(true);
                yield break;
            }

            // Away restarts and goal kicks remain automatic after the visible setup pause.
            if (RoundReset.Instance != null && !RoundReset.Instance.ExecutePreparedSetPiece())
            {
                awaitingSetPieceKick = false;
                CurrentSetPieceType = SetPieceType.None;
                MatchTimer.Instance?.ResumeClock();
            }
        }

        public void NotifySetPieceTaken(PlayerController kicker, KickType type)
        {
            if (!awaitingSetPieceKick || kicker == null || kicker != preparedSetPieceTaker) return;
            awaitingSetPieceKick = false;
            preparedSetPieceTaker = null;
            CurrentSetPieceType = SetPieceType.None;
            RoundReset.Instance?.ClearPreparedSetPiece();
            MatchTimer.Instance?.ResumeClock();
        }

        public void NotifyGoalScored()
        {
            CurrentSetPieceType = SetPieceType.None;
            awaitingSetPieceKick = false;
            preparedSetPieceTaker = null;
            KickoffReady = false;
            MatchTimer.Instance?.PauseClock();
            BeastSoccer.Ball.BallControl.Instance?.ForceReleaseAndStop();
            SetPossession(Possession.Loose);
            TeamManager.Instance?.ClearTransientPlayerState();
            TeamManager.Instance?.StopAllPlayers();
            SetPhaseInternal(MatchPhase.GoalScored);
            if (IsGoldenGoal) StartPhaseRoutine(EndAfterGoldenGoal(GameConfig.Instance.postGoalDelay));
            else StartPhaseRoutine(RestartAfterEvent(GameConfig.Instance.postGoalDelay));
        }

        private IEnumerator EndAfterGoldenGoal(float delay)
        {
            yield return new WaitForSeconds(delay);
            EndMatch();
        }

        public void NotifySaveMade()
        {
            KickoffReady = false;
            MatchTimer.Instance?.PauseClock();
            BeastSoccer.Ball.BallControl.Instance?.ForceReleaseAndStop();
            SetPossession(Possession.Loose);
            TeamManager.Instance?.ClearTransientPlayerState();
            TeamManager.Instance?.StopAllPlayers();
            SetPhaseInternal(MatchPhase.StopMade);
            StartPhaseRoutine(RestartAfterEvent(GameConfig.Instance.postSaveDelay));
        }

        private IEnumerator RestartAfterEvent(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Mode == GameMode.Defending)
            {
                RoundReset.Instance?.ResetDefendingRound();
            }
            else
            {
                TeamSide conceding = ScoreManager.Instance != null ? ScoreManager.Instance.LastConcedingSide : TeamSide.Away;
                RoundReset.Instance?.ResetForKickoff(conceding);
            }
            EnterKickoff();
        }

        private IEnumerator KickoffRoutine()
        {
            // First let the hard reset settle. Players remain locked in their canonical restart
            // positions even after this delay; the actual pass is what releases them and starts time.
            yield return new WaitForSeconds(GameConfig.Instance.kickoffDelay);
            RoundReset.Instance?.StabilizeBeforePlay();
            yield return new WaitForFixedUpdate();
            if (Mode == GameMode.Defending)
            {
                KickoffReady = false;
                RoundReset.Instance?.ReleaseKickoffLock();
                SetPhaseInternal(MatchPhase.Playing);
                MatchTimer.Instance?.ResumeClock();
                yield break;
            }

            KickoffReady = true;
            RoundReset.Instance?.OnKickoffReady();
        }

        public void RestartDefendingWave()
        {
            if (Mode != GameMode.Defending || Phase == MatchPhase.MatchOver) return;
            CancelPhaseRoutine();
            MatchTimer.Instance?.PauseClock();
            RoundReset.Instance?.ResetDefendingRound();
            EnterKickoff();
        }

        public void BeginGoldenGoal()
        {
            CancelPhaseRoutine();
            KickoffReady = false;
            MatchTimer.Instance?.PauseClock();
            SetPossession(Possession.Loose);
            TeamManager.Instance?.ClearTransientPlayerState();
            TeamManager.Instance?.StopAllPlayers();
            TeamSide kickoff = Random.value < 0.5f ? TeamSide.Home : TeamSide.Away;
            RoundReset.Instance?.ResetForKickoff(kickoff);
            EnterKickoff();
        }

        public void PlayUltGoalMoment()
        {
            StartCoroutine(UltGoalSlowMoRoutine());
        }

        private IEnumerator UltGoalSlowMoRoutine()
        {
            if (GameConfig.Instance == null) yield break;
            float previous = Time.timeScale;
            Time.timeScale = Mathf.Clamp(GameConfig.Instance.ultGoalSlowMoScale, 0.1f, 1f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, GameConfig.Instance.ultGoalSlowMoRealSeconds));
            if (Phase != MatchPhase.Paused) Time.timeScale = previous;
        }

        public void BeginHalfTime()
        {
            CurrentSetPieceType = SetPieceType.None;
            awaitingSetPieceKick = false;
            preparedSetPieceTaker = null;
            CancelPhaseRoutine();
            KickoffReady = false;
            MatchTimer.Instance?.PauseClock();
            SetPossession(Possession.Loose);
            TeamManager.Instance?.ClearTransientPlayerState();
            TeamManager.Instance?.StopAllPlayers();
            SetPhaseInternal(MatchPhase.HalfTime);
        }

        public void ContinueFromHalfTime()
        {
            if (Phase != MatchPhase.HalfTime) return;
            CurrentHalf = 2;
            OnHalfChanged?.Invoke(CurrentHalf);
            TeamManager.Instance?.SwapAttackingDirections();
            TeamSide secondHalfKickoff = TeamSide.Away;
            if (Mode == GameMode.Defending) RoundReset.Instance?.ResetDefendingRound();
            else RoundReset.Instance?.ResetForKickoff(secondHalfKickoff);
            EnterKickoff();
        }

        public void PauseMatch()
        {
            if (Phase == MatchPhase.Paused || Phase == MatchPhase.MatchOver || Phase == MatchPhase.HalfTime) return;
            phaseBeforePause = Phase;
            Time.timeScale = 0f;
            Phase = MatchPhase.Paused;
            OnPhaseChanged?.Invoke(Phase);
        }

        public void ResumeMatch()
        {
            if (Phase != MatchPhase.Paused) return;
            Time.timeScale = 1f;
            Phase = phaseBeforePause;
            OnPhaseChanged?.Invoke(Phase);
        }

        public void EndMatch()
        {
            CurrentSetPieceType = SetPieceType.None;
            awaitingSetPieceKick = false;
            preparedSetPieceTaker = null;
            Time.timeScale = 1f;
            CancelPhaseRoutine();
            KickoffReady = false;
            MatchTimer.Instance?.PauseClock();
            TeamManager.Instance?.StopAllPlayers();
            SetPhaseInternal(MatchPhase.MatchOver);
        }

        private void SetPhaseInternal(MatchPhase phase)
        {
            Phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        private void StartPhaseRoutine(IEnumerator routine)
        {
            CancelPhaseRoutine();
            phaseRoutine = StartCoroutine(routine);
        }

        private void CancelPhaseRoutine()
        {
            if (phaseRoutine != null)
            {
                StopCoroutine(phaseRoutine);
                phaseRoutine = null;
            }
        }

        private void RandomizeOpponent()
        {
            var choices = new[] { CharacterType.Leo, CharacterType.Goro, CharacterType.Volt };
            OpponentCharacter = choices[Random.Range(0, choices.Length)];
        }
    }
}
