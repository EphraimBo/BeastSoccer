using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;
using BeastSoccer.Ball;

namespace BeastSoccer.UI
{
    public class MatchUI : MonoBehaviour
    {
        public Text scoreText, timerText, bannerText;
        public Button primaryButton, secondaryButton, tertiaryButton, ultimateButton, switchButton, flyButton, lobButton;
        public Text primaryLabel, secondaryLabel, tertiaryLabel, ultimateLabel, flyLabel, lobLabel, sprintStaminaLabel;
        public Image ultMeterFill, ultActiveFill, ultButtonImage, ultStaminaFill, sprintStaminaFill, ultScreenTint, ultFlashImage;
        public Sprite shootButtonSprite, passButtonSprite, lobButtonSprite, ultButtonSprite;
        public GameObject sprintStaminaPanel;
        public GameObject ultStaminaPanel;
        public Text ultDebugText, ultGoalText;
        public float sprintStaminaMaxWidth = 230f;
        private bool wasUltActive;
        private float ultFlashAlpha;
        private float ultGoalUntil;

        private bool bound;
        private ActionContext currentContext = (ActionContext)(-1);
        private ActionContext pendingContext = (ActionContext)(-1);
        private float pendingSince;

        private enum ActionContext { HumanHasBall, TeammateHasBall, Defending }

        private void OnEnable() => Bind();

        private void Start()
        {
            Bind();
            UpdateScore(ScoreManager.Instance != null ? ScoreManager.Instance.HomeScore : 0,
                        ScoreManager.Instance != null ? ScoreManager.Instance.AwayScore : 0);
            if (GameManager.Instance != null) Phase(GameManager.Instance.Phase);
            RefreshActionContext(true);
        }

        private void Bind()
        {
            if (bound || ScoreManager.Instance == null || GameManager.Instance == null) return;
            ScoreManager.Instance.OnScoreChanged += UpdateScore;
            ScoreManager.Instance.OnUltGoal += ShowUltGoal;
            GameManager.Instance.OnPhaseChanged += Phase;
            bound = true;
            UpdateScore(ScoreManager.Instance.HomeScore, ScoreManager.Instance.AwayScore);
            Phase(GameManager.Instance.Phase);
        }

        private void OnDisable()
        {
            if (!bound) return;
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= UpdateScore;
                ScoreManager.Instance.OnUltGoal -= ShowUltGoal;
            }
            if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= Phase;
            bound = false;
        }

        private void Update()
        {
            if (!bound) Bind();
            if (MatchTimer.Instance != null && timerText != null)
            {
                timerText.text = MatchTimer.Instance.InGoldenGoal
                    ? $"GG {Mathf.CeilToInt(MatchTimer.Instance.GoldenGoalRemaining):00}"
                    : $"{MatchTimer.Instance.DisplayMinutes:00}:{MatchTimer.Instance.DisplaySeconds:00}";
                timerText.color = MatchTimer.Instance.IsFinalStretch ? new Color(1f,.72f,.15f,1f) : Color.white;
            }

            var aimingHuman = TeamManager.Instance != null ? TeamManager.Instance.CurrentHuman() : null;
            if (bannerText != null && GameManager.Instance != null)
            {
                if (aimingHuman != null && GameManager.Instance.IsHumanAimingSetPiece(aimingHuman))
                    bannerText.text = GameManager.Instance.CurrentSetPieceType == SetPieceType.ThrowIn ? "THROW IN · AIM" : "CORNER · AIM";
                else if (GameManager.Instance.Phase == MatchPhase.Playing && (bannerText.text.StartsWith("THROW IN") || bannerText.text.StartsWith("CORNER")))
                    bannerText.text = "";
            }

            RefreshActionContext(false);
            RefreshUltimate();
            RefreshSpecialAction();
            RefreshLob();
            RefreshSprintStamina();
            RefreshSwitch();
            RefreshUltScreenFX();
        }

        private void RefreshActionContext(bool force)
        {
            if (GameManager.Instance == null || TeamManager.Instance == null) return;

            if (GameManager.Instance.Phase == MatchPhase.Kickoff)
            {
                var human = TeamManager.Instance.CurrentHuman();
                bool canTake = human != null && GameManager.Instance.CanTakeKickoff(human);
                SetActionButton(primaryButton, primaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                SetActionButton(secondaryButton, secondaryLabel, canTake ? "PASS TO START" : "GET READY", passButtonSprite,
                    canTake ? Color.white : new Color(.55f,.55f,.55f,.65f));
                SetActionButton(tertiaryButton, tertiaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                if (primaryButton != null) primaryButton.interactable = false;
                if (secondaryButton != null) secondaryButton.interactable = canTake;
                if (tertiaryButton != null) tertiaryButton.interactable = false;
                if (lobButton != null) lobButton.interactable = false;
                return;
            }

            var setPieceHuman = TeamManager.Instance.CurrentHuman();
            if (setPieceHuman != null && GameManager.Instance.IsHumanAimingSetPiece(setPieceHuman))
            {
                SetActionButton(primaryButton, primaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                SetActionButton(secondaryButton, secondaryLabel, "THROW", lobButtonSprite, Color.white);
                SetActionButton(tertiaryButton, tertiaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                if (primaryButton != null) primaryButton.interactable = false;
                if (secondaryButton != null) secondaryButton.interactable = true;
                if (tertiaryButton != null) tertiaryButton.interactable = false;
                return;
            }

            if (GameManager.Instance.Phase == MatchPhase.SetPiece)
            {
                SetActionButton(primaryButton, primaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                SetActionButton(secondaryButton, secondaryLabel, "GET READY", null, new Color(.20f,.30f,.42f,.65f));
                SetActionButton(tertiaryButton, tertiaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                if (primaryButton != null) primaryButton.interactable = false;
                if (secondaryButton != null) secondaryButton.interactable = false;
                if (tertiaryButton != null) tertiaryButton.interactable = false;
                if (lobButton != null) lobButton.interactable = false;
                return;
            }

            ActionContext next = DetermineContext();

            if (force || currentContext == (ActionContext)(-1))
            {
                ApplyContext(next);
                pendingContext = next;
                pendingSince = Time.time;
                return;
            }

            if (next != pendingContext)
            {
                pendingContext = next;
                pendingSince = Time.time;
            }

            // Receiving the ball should feel immediate; other context changes debounce briefly so passes do not flash defense UI.
            float debounce = next == ActionContext.HumanHasBall ? 0f : GameConfig.Instance.possessionUiDebounceSeconds;
            if (next != currentContext && Time.time - pendingSince >= debounce) ApplyContext(next);
        }

        private ActionContext DetermineContext()
        {
            if (GameManager.Instance.Mode == GameMode.Defending) return ActionContext.Defending;
            PlayerController human = TeamManager.Instance.CurrentHuman();
            if (human != null && human.HasBall) return ActionContext.HumanHasBall;
            var owner = BallControl.Instance != null ? BallControl.Instance.Owner : null;
            if (owner != null && owner.Side == TeamSide.Home) return ActionContext.TeammateHasBall;
            if (BallControl.Instance != null && BallControl.Instance.IsRecentKickBy(TeamSide.Home, GameConfig.Instance.possessionUiFlightGraceSeconds)) return ActionContext.TeammateHasBall;
            return ActionContext.Defending;
        }

        private void ApplyContext(ActionContext next)
        {
            currentContext = next;
            if (primaryButton != null) primaryButton.interactable = true;
            if (secondaryButton != null) secondaryButton.interactable = true;
            if (tertiaryButton != null) tertiaryButton.interactable = true;
            switch (next)
            {
                case ActionContext.HumanHasBall:
                    // FIX21: keep the attacking cluster intentionally simple: SHOOT / PASS / LOB.
                    SetActionButton(primaryButton, primaryLabel, "", shootButtonSprite, Color.white);
                    SetActionButton(secondaryButton, secondaryLabel, "", passButtonSprite, Color.white);
                    SetActionButton(tertiaryButton, tertiaryLabel, "", lobButtonSprite, Color.white);
                    break;
                case ActionContext.TeammateHasBall:
                    // Direct-control possession model: this is only the brief ball-flight window
                    // after a user pass. No CALL/request actions exist anymore.
                    SetActionButton(primaryButton, primaryLabel, "", shootButtonSprite, new Color(.72f,.72f,.72f,.78f));
                    SetActionButton(secondaryButton, secondaryLabel, "", passButtonSprite, new Color(.72f,.72f,.72f,.78f));
                    SetActionButton(tertiaryButton, tertiaryLabel, "", lobButtonSprite, new Color(.72f,.72f,.72f,.78f));
                    if (primaryButton != null) primaryButton.interactable = false;
                    if (secondaryButton != null) secondaryButton.interactable = false;
                    if (tertiaryButton != null) tertiaryButton.interactable = false;
                    break;
                default:
                    SetActionButton(primaryButton, primaryLabel, "TACKLE", null, new Color(.90f,.18f,.18f,.92f));
                    SetActionButton(secondaryButton, secondaryLabel, "INTERCEPT", null, new Color(.12f,.45f,.95f,.92f));
                    SetActionButton(tertiaryButton, tertiaryLabel, "—", null, new Color(.20f,.24f,.30f,.52f));
                    if (tertiaryButton != null) tertiaryButton.interactable = false;
                    break;
            }
        }

        private void RefreshUltimate()
        {
            if (TeamManager.Instance == null) return;
            var special = TeamManager.Instance.SpecialFor(TeamSide.Home);
            var human = TeamManager.Instance.CurrentHuman();
            var ult = special != null ? special.Ult : null;
            float charge = ult != null ? ult.Charge : 0f;
            bool active = ult != null && (ult.IsActive || ult.IsActivating);

            if (ultMeterFill != null) ultMeterFill.fillAmount = charge;
            if (ultActiveFill != null)
            {
                ultActiveFill.enabled = active;
                ultActiveFill.fillAmount = active ? ult.ActiveFractionRemaining : 0f;
            }

            bool controllingSpecial = special != null && human == special;
            if (ultimateButton != null)
            {
                // FIX21: the ULT control is permanent HUD furniture. Possession and charge only
                // affect whether it can be pressed / how it is tinted; they never hide the button.
                ultimateButton.gameObject.SetActive(true);
                ultimateButton.interactable = GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing && controllingSpecial && !active && charge >= 0.999f;
            }

            if (ultimateLabel != null)
            {
                // When supplied icon art is present, keep text minimal so it does not cover the art.
                ultimateLabel.text = ultButtonSprite != null
                    ? (active ? "ACTIVE" : (controllingSpecial ? "" : "SPECIAL"))
                    : (active ? "ULT ACTIVE" : (controllingSpecial ? "ULT" : "ULT\nSPECIAL"));
            }

            if (ultButtonImage != null)
            {
                if (ultButtonSprite != null) { ultButtonImage.sprite = ultButtonSprite; ultButtonImage.preserveAspect = true; }
                if (active) ultButtonImage.color = new Color(1f,.92f,.55f,1f);
                else if (controllingSpecial && charge >= 0.999f) ultButtonImage.color = new Color(.70f,.24f,1f,1f);
                else ultButtonImage.color = new Color(.38f,.25f,.48f,.90f);
            }

            if (ultStaminaPanel != null) ultStaminaPanel.SetActive(active);
            if (ultStaminaFill != null) ultStaminaFill.fillAmount = active ? ult.ActiveFractionRemaining : 0f;
            if (ultDebugText != null)
            {
                bool showDebug = false; // FIX22: remove the intrusive ult debug overlay from normal play.
                ultDebugText.gameObject.SetActive(showDebug);
                if (showDebug)
                {
                    string variant = ult.IsAttackingVariant ? "ATTACK" : "DEFENCE";
                    string specialState = "";
                    if (special.Character == CharacterType.Volt && ult.IsAttackingVariant)
                        specialState = ult.IsFlying ? "   FLIGHT ACTIVE" : "   FLY READY";
                    else if (special.Character == CharacterType.Goro && ult.IsAttackingVariant)
                        specialState = ult.IsCharging ? "   CHARGE ACTIVE" : "   CHARGE READY";
                    ultDebugText.text = $"{special.Character.ToString().ToUpperInvariant()} ULT · {variant}   SPD x{ult.SpeedMultiplier:0.00}   SHOT x{ult.ShotMultiplier:0.00}   STR x{ult.StrengthMultiplier:0.00}   TKL x{ult.TackleMultiplier:0.00}{specialState}";
                }
            }
        }


        private void RefreshSpecialAction()
        {
            if (flyButton == null) return;
            var human = TeamManager.Instance != null ? TeamManager.Instance.CurrentHuman() : null;
            var ult = human != null ? human.Ult : null;
            bool isSpecialActionCharacter = human != null && ult != null &&
                                            (human.Character == CharacterType.Volt || human.Character == CharacterType.Goro);

            // Keep the button physically visible whenever the controlled character owns this move.
            // That makes FLY/CHARGE discoverable instead of making the control materialize mid-ult.
            flyButton.gameObject.SetActive(isSpecialActionCharacter);
            if (!isSpecialActionCharacter) return;

            bool attackingUlt = ult.IsActive && !ult.IsActivating && ult.IsAttackingVariant;
            if (human.Character == CharacterType.Volt)
            {
                if (flyLabel != null) flyLabel.text = ult.IsFlying ? "FLYING" : "FLY";
                if (ult.IsFlying) SetButtonColor(flyButton, new Color(.95f,.82f,.18f,.95f));
                else if (attackingUlt) SetButtonColor(flyButton, new Color(.18f,.78f,.95f,.95f));
                else SetButtonColor(flyButton, new Color(.18f,.32f,.40f,.62f));
            }
            else
            {
                if (flyLabel != null) flyLabel.text = ult.IsCharging ? "CHARGING" : "CHARGE";
                if (ult.IsCharging) SetButtonColor(flyButton, new Color(1f,.62f,.12f,.98f));
                else if (attackingUlt) SetButtonColor(flyButton, new Color(.92f,.32f,.10f,.95f));
                else SetButtonColor(flyButton, new Color(.42f,.22f,.16f,.62f));
            }
            flyButton.interactable = attackingUlt && ult.CanTriggerSpecialAction;
        }

        private void RefreshLob()
        {
            // FIX21: LOB now occupies the stable tertiary action slot. Any legacy standalone
            // lob button from an older scene is hidden so the HUD never shows duplicate controls.
            if (lobButton != null) lobButton.gameObject.SetActive(false);
        }

        private void RefreshSprintStamina()
        {
            var human = TeamManager.Instance != null ? TeamManager.Instance.CurrentHuman() : null;
            bool show = human != null && GameManager.Instance != null &&
                        (GameManager.Instance.Phase == MatchPhase.Playing || GameManager.Instance.Phase == MatchPhase.Kickoff);
            if (sprintStaminaPanel != null) sprintStaminaPanel.SetActive(show);
            if (sprintStaminaFill != null && human != null)
            {
                float stamina = Mathf.Clamp01(human.SprintStamina01);
                sprintStaminaFill.fillAmount = 1f;
                RectTransform rt = sprintStaminaFill.rectTransform;
                if (rt != null)
                {
                    Vector2 size = rt.sizeDelta;
                    size.x = sprintStaminaMaxWidth * stamina;
                    rt.sizeDelta = size;
                }
                sprintStaminaFill.color = human.IsSprintExhausted || stamina <= 0.08f
                    ? new Color(.92f,.18f,.16f,.98f)
                    : stamina < 0.30f ? new Color(1f,.68f,.12f,.98f)
                    : new Color(.20f,.86f,.42f,.96f);
            }
            if (sprintStaminaLabel != null && human != null)
                sprintStaminaLabel.text = human.IsSprintExhausted ? "SPRINT EMPTY" : (human.SprintStamina01 < 0.30f ? "SPRINT LOW" : "SPRINT");
        }

        private static Color CharacterColor(CharacterType c)
        {
            if (c == CharacterType.Leo) return new Color(1f,.38f,.08f,1f);
            if (c == CharacterType.Goro) return new Color(.22f,.92f,.38f,1f);
            if (c == CharacterType.Volt) return new Color(.20f,.75f,1f,1f);
            return new Color(.65f,.35f,1f,1f);
        }

        private void RefreshUltScreenFX()
        {
            var homeSpecial = TeamManager.Instance != null ? TeamManager.Instance.SpecialFor(TeamSide.Home) : null;
            var awaySpecial = TeamManager.Instance != null ? TeamManager.Instance.SpecialFor(TeamSide.Away) : null;
            var special = homeSpecial != null && homeSpecial.Ult != null && (homeSpecial.Ult.IsActive || homeSpecial.Ult.IsActivating)
                ? homeSpecial
                : awaySpecial != null && awaySpecial.Ult != null && (awaySpecial.Ult.IsActive || awaySpecial.Ult.IsActivating) ? awaySpecial : homeSpecial;
            var ult = special != null ? special.Ult : null;
            bool active = ult != null && (ult.IsActive || ult.IsActivating);
            Color c = special != null ? CharacterColor(special.Character) : Color.white;

            // FIX22: no full-screen ult tint/flash. Ult readability is carried by the character glow
            // and the persistent ULT button/meter instead of an overlay covering gameplay.
            wasUltActive = active;
            ultFlashAlpha = 0f;
            if (ultScreenTint != null) ultScreenTint.enabled = false;
            if (ultFlashImage != null) ultFlashImage.enabled = false;
            if (ultGoalText != null)
                ultGoalText.gameObject.SetActive(Time.unscaledTime < ultGoalUntil);
        }

        private void ShowUltGoal(CharacterType character)
        {
            if (ultGoalText == null) return;
            ultGoalText.text = character.ToString().ToUpperInvariant() + " ULT GOAL!";
            ultGoalText.color = CharacterColor(character);
            ultGoalUntil = Time.unscaledTime + 1.35f;
            ultGoalText.gameObject.SetActive(true);
            ultFlashAlpha = 0.62f;
        }

        private void RefreshSwitch()
        {
            if (switchButton == null || GameManager.Instance == null) return;
            bool allowed = GameManager.Instance.Phase == MatchPhase.Playing && (ControlSwitcher.Instance == null || !ControlSwitcher.Instance.IsKeeperSequence);
            switchButton.interactable = allowed;
        }

        private static void SetActionButton(Button button, Text label, string text, Sprite sprite, Color tint)
        {
            if (button != null && button.targetGraphic is Image img)
            {
                img.sprite = sprite;
                img.preserveAspect = sprite != null;
                img.color = tint;
            }
            if (label != null) label.text = text;
        }

        private static void SetLabel(Text t,string value){ if(t!=null)t.text=value; }
        private static void SetButtonColor(Button b,Color c){ if(b!=null && b.targetGraphic is Image img) img.color=c; }

        private void UpdateScore(int home, int away)
        {
            if (scoreText != null) scoreText.text = $"{home} - {away}";
        }

        private void Phase(MatchPhase phase)
        {
            if (bannerText == null) return;
            switch (phase)
            {
                case MatchPhase.GoalScored: bannerText.text = "GOAL!"; break;
                case MatchPhase.StopMade: bannerText.text = "SAVE!"; break;
                case MatchPhase.Kickoff: bannerText.text = MatchTimer.Instance != null && MatchTimer.Instance.InGoldenGoal ? "GOLDEN GOAL" : "KICK OFF"; break;
                case MatchPhase.SetPiece:
                    bannerText.text = GameManager.Instance != null && GameManager.Instance.CurrentSetPieceType == SetPieceType.ThrowIn ? "THROW IN"
                        : GameManager.Instance != null && GameManager.Instance.CurrentSetPieceType == SetPieceType.GoalKick ? "GOAL KICK"
                        : GameManager.Instance != null && GameManager.Instance.CurrentSetPieceType == SetPieceType.Corner ? "CORNER"
                        : "OUT OF PLAY";
                    break;
                case MatchPhase.HalfTime: bannerText.text = "HALF TIME"; break;
                case MatchPhase.MatchOver: bannerText.text = "FULL TIME"; break;
                default: bannerText.text = ""; break;
            }
            RefreshActionContext(true);
        }
    }
}
