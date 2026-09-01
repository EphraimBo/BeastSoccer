using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Ball;

namespace BeastSoccer.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        public float shakeDecay = 8f;
        private Vector3 shakeOffset;
        private Vector3 smoothPosition;
        private bool initialized;
        private Camera cam;

        private void Start()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 46f;
            smoothPosition = transform.position;
            initialized = true;
        }

        private void LateUpdate()
        {
            if (!TryGetDesired(out Vector3 desired)) return;
            if (!initialized) { smoothPosition = transform.position; initialized = true; }

            float dt = Time.deltaTime;
            float followLerp = GameConfig.Instance != null ? GameConfig.Instance.cameraFollowLerp : 4f;
            float lerp = dt > 0f ? 1f - Mathf.Exp(-followLerp * dt) : 0f;
            smoothPosition = Vector3.Lerp(smoothPosition, desired, lerp);
            transform.position = smoothPosition + shakeOffset;
            transform.rotation = Quaternion.Euler(GameConfig.Instance.cameraAngle, 0f, 0f);
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, dt > 0f ? 1f - Mathf.Exp(-shakeDecay * dt) : 0f);
        }

        // Kept under the old method name so existing reset/control-switch code remains compatible.
        // FIX26 camera ownership is the BALL, not the currently controlled player.
        public void SnapToCurrentHuman()
        {
            SnapToBall();
        }

        public void SnapToBall()
        {
            if (!TryGetDesired(out Vector3 desired)) return;
            smoothPosition = desired;
            shakeOffset = Vector3.zero;
            transform.position = desired;
            transform.rotation = Quaternion.Euler(GameConfig.Instance.cameraAngle, 0f, 0f);
            initialized = true;
        }

        private bool TryGetDesired(out Vector3 desired)
        {
            desired = transform.position;
            if (GameConfig.Instance == null) return false;
            if (cam == null) cam = GetComponent<Camera>();

            Vector2 focus2D;
            float visualHeight = 0f;
            var ball = BallControl.Instance;
            if (ball != null)
            {
                focus2D = ball.transform.position;
                visualHeight = ball.VisualArcHeight;

                // Small velocity look-ahead keeps a hard pass/shot from sitting on the very edge of
                // the frame without making the camera jump to the receiver before the ball gets there.
                var ballRb = ball.GetComponent<Rigidbody2D>();
                if (ballRb != null && ball.Mode == BallControl.BallMode.Free)
                {
                    Vector2 look = ballRb.linearVelocity * Mathf.Max(0f, GameConfig.Instance.cameraBallVelocityLookAheadSeconds);
                    look = Vector2.ClampMagnitude(look, Mathf.Max(0f, GameConfig.Instance.cameraBallVelocityLookAheadMax));
                    focus2D += look;
                }
            }
            else
            {
                // Safe fallback for editor/setup moments before BallControl is alive.
                var h = TeamManager.Instance != null ? TeamManager.Instance.CurrentHuman() : null;
                if (h == null) return false;
                focus2D = h.transform.position;
                if (h.Visual != null) visualHeight = h.Visual.baseHeight + h.Visual.extraHeight;
            }

            Vector3 target = new Vector3(focus2D.x, visualHeight, focus2D.y);

            float aspect = Mathf.Max(.5f, cam.aspect);
            float vfov = cam.fieldOfView * Mathf.Deg2Rad;
            float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * .5f) * aspect);
            float visibleFraction = Mathf.Max(GameConfig.Instance.cameraVisibleFraction, 0.87f);
            if (MatchTimer.Instance != null && MatchTimer.Instance.IsFinalStretch)
                visibleFraction *= GameConfig.Instance.finalStretchCameraTighten;

            // Always include some world beyond the white pitch line so the net/goal model and a
            // pitch underlay remain visible. This is presentation only; gameplay bounds do not move.
            float outsideMargin = Mathf.Max(GameConfig.Instance.cameraOutsideViewMargin, 2.35f);
            float visibleLength = GameConfig.Instance.pitchLength * visibleFraction + outsideMargin;
            float slant = visibleLength / (2f * Mathf.Tan(hfov * .5f));
            float angle = GameConfig.Instance.cameraAngle * Mathf.Deg2Rad;
            float height = slant * Mathf.Sin(angle);
            float distance = slant * Mathf.Cos(angle);

            desired = target + new Vector3(0f, height, -distance);
            return true;
        }

        public void AddShake(float amount)
        {
            shakeOffset += new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount), 0f);
        }
    }
}
