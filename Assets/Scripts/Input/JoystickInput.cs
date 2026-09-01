using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeastSoccer.Input
{
    public class JoystickInput : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform background;
        public RectTransform knob;
        public Image walkRing;
        public Image sprintRing;
        public float knobRange=90f;
        public Vector2 Direction{get;private set;}

        private void Start() { RefreshSprintVisual(false); }

        private void LateUpdate()
        {
            // The art reflects actual sprint state, not merely how far the thumb is dragged.
            // This keeps the ring grey when stamina is exhausted even if the stick is at the rim.
            var human = BeastSoccer.Core.TeamManager.Instance != null ? BeastSoccer.Core.TeamManager.Instance.CurrentHuman() : null;
            if (human != null) RefreshSprintVisual(human.IsSprinting && !human.IsSprintExhausted);
        }

        public void OnPointerDown(PointerEventData e)=>OnDrag(e);
        public void OnDrag(PointerEventData e)
        {
            if(background==null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(background,e.position,e.pressEventCamera,out var local);
            Vector2 clamped=Vector2.ClampMagnitude(local,knobRange);
            if(knob!=null) knob.anchoredPosition=clamped;
            Direction=knobRange>0?clamped/knobRange:Vector2.zero;
            float enter = BeastSoccer.Data.GameConfig.Instance != null ? BeastSoccer.Data.GameConfig.Instance.sprintEnterThreshold : .88f;
            RefreshSprintVisual(Direction.magnitude>=enter);
        }
        public void OnPointerUp(PointerEventData e)
        {
            Direction=Vector2.zero;
            if(knob!=null) knob.anchoredPosition=Vector2.zero;
            RefreshSprintVisual(false);
        }

        private void RefreshSprintVisual(bool sprint)
        {
            if(walkRing!=null) walkRing.enabled=!sprint;
            if(sprintRing!=null) sprintRing.enabled=sprint;
        }
    }
}
