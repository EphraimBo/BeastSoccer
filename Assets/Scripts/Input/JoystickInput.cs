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
        public BeastSoccer.UI.ArcGraphic sprintTrack;
        public BeastSoccer.UI.ArcGraphic sprintSweep;
        private int pointerId = int.MinValue;
        public Vector2 Direction{get;private set;}

        private void Start() { RefreshSprintVisual(false); }

        private void LateUpdate()
        {
            // The art reflects actual sprint state, not merely how far the thumb is dragged.
            // This keeps the ring grey when stamina is exhausted even if the stick is at the rim.
            var human = BeastSoccer.Core.TeamManager.Instance != null ? BeastSoccer.Core.TeamManager.Instance.CurrentHuman() : null;
            if (human != null) RefreshSprintVisual(human.IsSprinting && !human.IsSprintExhausted);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if(pointerId != int.MinValue) return;
            pointerId=e.pointerId; OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(background==null || pointerId != e.pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(background,e.position,e.pressEventCamera,out var local);
            Vector2 clamped=Vector2.ClampMagnitude(local,knobRange);
            if(knob!=null) knob.anchoredPosition=clamped;
            Direction=knobRange>0?clamped/knobRange:Vector2.zero;
            float enter = BeastSoccer.Data.GameConfig.Instance != null ? BeastSoccer.Data.GameConfig.Instance.sprintEnterThreshold : .88f;
            RefreshSprintVisual(Direction.magnitude>=enter);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(pointerId != e.pointerId) return;
            ResetInput();
        }
        private void OnDisable() => ResetInput();
        private void OnApplicationFocus(bool focus) { if(!focus) ResetInput(); }
        private void ResetInput()
        {
            pointerId=int.MinValue;
            Direction=Vector2.zero;
            if(knob!=null) knob.anchoredPosition=Vector2.zero;
            RefreshSprintVisual(false);
        }

        private void RefreshSprintVisual(bool sprint)
        {
            if(walkRing!=null) walkRing.enabled=true;
            if(sprintRing!=null) sprintRing.enabled=sprint;
            if(sprintSweep!=null)
            {
                sprintSweep.gameObject.SetActive(Direction.sqrMagnitude>.01f);
                sprintSweep.SetArc(Mathf.Atan2(Direction.y,Direction.x)*Mathf.Rad2Deg, sprint ? 82 : 48,
                    sprint ? new Color(.65f,1f,.08f,1) : new Color(.56f,.85f,.3f,.35f));
            }
        }
    }
}
