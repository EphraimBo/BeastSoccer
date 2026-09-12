using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Input;

namespace BeastSoccer.UI
{
    public class DuelHud : MonoBehaviour
    {
        private MatchUI ui;
        private ArcGraphic shotGlow;
        private ArcGraphic tackleCover;
        private Text hint;

        public static void Install(MatchUI ui)
        {
            if (!DuelRules.Enabled || ui.GetComponent<DuelHud>() != null) return;
            var hud = ui.gameObject.AddComponent<DuelHud>();
            hud.ui = ui;
            hud.Build();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position; rt.sizeDelta = size; rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
        public static Text Label(Transform parent, string name, string text, int size, Color color)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = size - 4; label.resizeTextMaxSize = size;
            return label;
        }
        public static ArcGraphic Ring(Transform parent, string name, float size, float thickness, Color color)
        {
            var ring = Rect(name, parent).gameObject.AddComponent<ArcGraphic>();
            Place(ring.rectTransform, new Vector2(.5f,.5f), Vector2.zero, Vector2.one * size);
            ring.thickness = thickness; ring.color = color; ring.raycastTarget = false;
            return ring;
        }

        private void Build()
        {
            var canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            SafeAreaRoot.ConfigureCanvas(canvas);
            var safeRoot = Rect("DuelSafeArea", canvas.transform);
            safeRoot.gameObject.AddComponent<SafeAreaRoot>();
            var safe = Rect("DuelHudDesignSpace", safeRoot);
            safe.gameObject.AddComponent<SafeAreaDesignSpace>();
            Move(ui.primaryButton, safe, new Vector2(1,0), new Vector2(-160,145), new Vector2(210,210));
            Move(ui.ultimateButton, safe, new Vector2(1,0), new Vector2(-150,370), new Vector2(176,176));
            Hide(ui.secondaryButton); Hide(ui.tertiaryButton); Hide(ui.flyButton); Hide(ui.lobButton); Hide(ui.switchButton);
            if (ui.primaryLabel != null)
            {
                ui.primaryLabel.transform.SetParent(ui.primaryButton.transform, false);
                Place(ui.primaryLabel.rectTransform, Vector2.one*.5f, Vector2.zero, new Vector2(130,65));
                ui.primaryLabel.fontSize = 28; ui.primaryLabel.alignment = TextAnchor.MiddleCenter;
            }
            shotGlow = Ring(ui.primaryButton.transform, "ShootingZoneGlow", 232, 7, new Color(.15f,.65f,1f,1));
            Ring(shotGlow.transform, "SoftOuterGlow", 248, 8, new Color(.1f,.55f,1f,.16f));
            tackleCover = Ring(ui.primaryButton.transform, "TackleLabelBacking", 120, 60, new Color(.42f,.025f,.025f,1));
            if (ui.primaryLabel) ui.primaryLabel.transform.SetAsLastSibling();
            if (ui.ultimateLabel)
            {
                Place(ui.ultimateLabel.rectTransform, new Vector2(.5f,.12f), Vector2.zero, new Vector2(180,40));
                ui.ultimateLabel.fontSize = 25;
            }
            var joystick = Object.FindAnyObjectByType<JoystickInput>();
            if (joystick != null)
            {
                var rt = joystick.transform as RectTransform;
                rt.SetParent(safe, false);
                Place(rt, Vector2.zero, new Vector2(220,210), new Vector2(344,344));
                if (joystick.background != rt)
                    Place(joystick.background, Vector2.one*.5f, Vector2.zero, new Vector2(200,200));
                else rt.sizeDelta = new Vector2(200,200);
                joystick.knobRange = 160f;
                if (joystick.knob != null) joystick.knob.sizeDelta = new Vector2(72,72);
                if (joystick.walkRing != null) joystick.walkRing.rectTransform.sizeDelta = new Vector2(200,200);
                if (joystick.sprintRing != null) joystick.sprintRing.gameObject.SetActive(false);
                joystick.sprintTrack = Ring(joystick.background, "OuterSprintTrack", 336, 4, new Color(.56f,1f,.18f,.27f));
                joystick.sprintSweep = Ring(joystick.background, "ThumbSprintSweep", 336, 8, new Color(.63f,1f,.1f,.7f));
                joystick.sprintSweep.sweep = 58;
                joystick.knob?.SetAsLastSibling();
            }
            if (ui.sprintStaminaPanel != null)
            {
                ui.sprintStaminaPanel.transform.SetParent(safe, false);
                Place((RectTransform)ui.sprintStaminaPanel.transform, Vector2.zero, new Vector2(220,425), new Vector2(270,22));
                ui.sprintStaminaMaxWidth = 260;
                if (ui.sprintStaminaFill) { Place(ui.sprintStaminaFill.rectTransform, new Vector2(0,.5f), new Vector2(5,0), new Vector2(260,12)); ui.sprintStaminaFill.rectTransform.pivot = new Vector2(0,.5f); }
                if (ui.sprintStaminaLabel) Place(ui.sprintStaminaLabel.rectTransform, new Vector2(.5f,1), new Vector2(0,20), new Vector2(240,28));
            }
            if (ui.ultMeterFill != null)
            {
                var meter = ui.ultMeterFill.transform.parent as RectTransform;
                meter.SetParent(safe, false);
                Place(meter, new Vector2(.5f,0), new Vector2(0,40), new Vector2(450,20));
                Place(ui.ultMeterFill.rectTransform, new Vector2(0,.5f), new Vector2(5,0), new Vector2(440,12));
                ui.ultMeterFill.rectTransform.pivot = new Vector2(0,.5f);
                ui.ultMeterFill.color = new Color(.66f,1f,.12f,1);
                var label = Label(meter, "PowerCaption", "BEAST POWER", 18, new Color(.85f,.88f,.7f));
                Place(label.rectTransform, new Vector2(.5f,1), new Vector2(0,20), new Vector2(250,30));
            }
            if (ui.ultStaminaPanel) ui.ultStaminaPanel.SetActive(false);
            Move(ui.scoreText, safe, new Vector2(.5f,1), new Vector2(0,-40), new Vector2(210,55));
            Move(ui.timerText, safe, new Vector2(.5f,1), new Vector2(0,-88), new Vector2(140,38));
            Move(ui.bannerText, safe, new Vector2(.5f,.65f), Vector2.zero, new Vector2(680,80));
            var home = Label(safe, "HomeClub", "AGUILAR", 24, new Color(1,.5f,.17f));
            var away = Label(safe, "AwayClub", "ALIANZO", 24, Color.white);
            Place(home.rectTransform,new Vector2(.5f,1),new Vector2(-180,-40),new Vector2(170,50));
            Place(away.rectTransform,new Vector2(.5f,1),new Vector2(180,-40),new Vector2(170,50));
            hint = Label(safe, "ZoneHint", "ATTACK  →", 22, new Color(.8f,.9f,1,.8f));
            Place(hint.rectTransform,new Vector2(.5f,0),new Vector2(0,105),new Vector2(540,38));
            foreach (var b in canvas.GetComponentsInChildren<Button>(true))
            {
                bool pause = b.name.Equals("PAUSE", System.StringComparison.OrdinalIgnoreCase) || b.name == "II";
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                    pause |= b.onClick.GetPersistentMethodName(i) == "OnPause";
                if (pause) Move(b,safe,Vector2.one,new Vector2(-65,-60),new Vector2(86,86));
            }
            var minimap = Object.FindAnyObjectByType<MinimapUI>();
            if (minimap != null) Move(minimap,safe,new Vector2(0,1),new Vector2(115,-75),new Vector2(180,108));
            // Keep menus above the relocated controls, including their raycast blocker.
            var modalRoot = Rect("DuelModalSafeArea", canvas.transform);
            modalRoot.gameObject.AddComponent<SafeAreaRoot>();
            var modalDesign = Rect("DuelModalDesignSpace", modalRoot);
            modalDesign.gameObject.AddComponent<SafeAreaDesignSpace>();
            foreach (string panelName in new[] { "PausePanel", "FullTimePanel", "HalfTimePanel" })
            {
                var panel = canvas.transform.Find(panelName) as RectTransform;
                if (panel == null) continue;
                // Full-safe-area blocker only exists while this particular panel is active.
                var blocker = Rect(panelName + "InputBlocker", modalDesign).gameObject.AddComponent<Image>();
                blocker.color = new Color(0, 0, 0, .35f);
                blocker.enabled = panel.gameObject.activeInHierarchy;
                blocker.rectTransform.anchorMin = Vector2.zero;
                blocker.rectTransform.anchorMax = Vector2.one;
                blocker.rectTransform.offsetMin = blocker.rectTransform.offsetMax = Vector2.zero;
                blocker.gameObject.AddComponent<ModalInputBlocker>().panel = panel.gameObject;
                Vector2 size = panel.sizeDelta;
                panel.SetParent(modalDesign, false);
                Place(panel, Vector2.one * .5f, Vector2.zero, size);
            }
        }
        private static void Move(Component c, Transform root, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            if (c == null) return; c.transform.SetParent(root,false); Place(c.transform as RectTransform,anchor,pos,size);
        }
        private static void Hide(Component c) { if(c != null) c.gameObject.SetActive(false); }
        private void LateUpdate()
        {
            var human = TeamManager.Instance?.CurrentHuman();
            if (human == null || ui.primaryButton == null) return;
            bool playing = GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Playing;
            bool ready = human.HasBall && DuelRules.CanScoreFrom(human);
            ui.primaryButton.interactable = playing && (!human.HasBall || ready);
            if (ui.primaryButton.targetGraphic is Image img) { img.sprite=ui.shootButtonSprite; img.color=human.HasBall && !ready ? new Color(.7f,.7f,.7f,.8f) : Color.white; img.preserveAspect=true; }
            if (tackleCover) tackleCover.gameObject.SetActive(!human.HasBall);
            if (ui.primaryLabel) ui.primaryLabel.text = human.HasBall ? "" : "TACKLE";
            if (shotGlow) { shotGlow.gameObject.SetActive(playing && ready); shotGlow.color = new Color(.12f,.65f,1,.72f + .2f*Mathf.Sin(Time.time*5)); }
            if (hint) hint.text = !playing ? "" : human.HasBall ? (ready ? "SHOOTING ZONE" : "ATTACK  →  ENTER THE BLUE ARC") : "WIN THE BALL";
        }
    }

    public sealed class ModalInputBlocker : MonoBehaviour
    {
        public GameObject panel;
        private void LateUpdate()
        {
            var graphic = GetComponent<Image>();
            graphic.enabled = panel != null && panel.activeInHierarchy;
        }
    }
}
