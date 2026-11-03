using UnityEngine;
using BeastSoccer.Ball;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    public class BallVisualProxy : MonoBehaviour
    {
        public BallControl source;
        public Transform visualModel;
        public float rollScale = 180f;
        private Vector3 lastPos;

        private void Start()
        {
            if (BeastSoccer.Core.DuelRules.Enabled && visualModel != null)
                visualModel.localScale *= GameConfig.Instance.duelBallScale;
            lastPos = transform.position;
        }

        private void LateUpdate()
        {
            if (source == null) return;
            Vector2 p = source.transform.position;
            float ownerFlightHeight = 0f;
            if (source.Owner != null && source.Owner.IsFlying && source.Owner.Visual != null)
                ownerFlightHeight = source.Owner.Visual.extraHeight;
            float y = GameConfig.Instance.ballVisualRadius + source.VisualArcHeight + source.DribbleVisualBobHeight + ownerFlightHeight;
            transform.position = new Vector3(p.x, y, p.y);
            Vector3 delta = transform.position - lastPos;
            if (visualModel != null && delta.sqrMagnitude > 0.00001f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(delta.x, 0f, delta.z)).normalized;
                visualModel.Rotate(axis, delta.magnitude * rollScale, Space.World);
            }
            lastPos = transform.position;
        }
    }
}
