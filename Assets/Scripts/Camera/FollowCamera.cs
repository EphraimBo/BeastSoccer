using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Ball;
using BeastSoccer.UI;

namespace BeastSoccer.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        public float shakeDecay = 8f;

        [Header("Camera Zoom")]
        [Tooltip("1 = current distance. Lower = camera physically closer without changing FOV.")]
        [Range(0.7f, 1.1f)]
        public float cameraDistanceScale = 0.8f;

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

            // Snap the initial fit before the first rendered match frame.
            SnapToBall();
        }

        private void LateUpdate()
        {
            if (!TryGetDesired(out Vector3 desired))
                return;

            if (!initialized)
            {
                smoothPosition = transform.position;
                initialized = true;
            }

            if (DuelRules.Enabled)
            {
                smoothPosition = desired;
                transform.position = desired + shakeOffset;

                transform.rotation =
                    Quaternion.Euler(GameConfig.Instance.cameraAngle, 0f, 0f);

                shakeOffset = Vector3.Lerp(
                    shakeOffset,
                    Vector3.zero,
                    1f - Mathf.Exp(-shakeDecay * Time.deltaTime)
                );

                return;
            }

            float dt = Time.deltaTime;

            float followLerp =
                GameConfig.Instance != null
                    ? GameConfig.Instance.cameraFollowLerp
                    : 4f;

            float lerp =
                dt > 0f
                    ? 1f - Mathf.Exp(-followLerp * dt)
                    : 0f;

            smoothPosition =
                Vector3.Lerp(smoothPosition, desired, lerp);

            transform.position =
                smoothPosition + shakeOffset;

            transform.rotation =
                Quaternion.Euler(
                    GameConfig.Instance.cameraAngle,
                    0f,
                    0f
                );

            shakeOffset = Vector3.Lerp(
                shakeOffset,
                Vector3.zero,
                dt > 0f
                    ? 1f - Mathf.Exp(-shakeDecay * dt)
                    : 0f
            );
        }

        public void SnapToCurrentHuman()
        {
            SnapToBall();
        }

        public void SnapToBall()
        {
            if (!TryGetDesired(out Vector3 desired))
                return;

            smoothPosition = desired;
            shakeOffset = Vector3.zero;
            transform.position = desired;

            transform.rotation =
                Quaternion.Euler(
                    GameConfig.Instance.cameraAngle,
                    0f,
                    0f
                );

            initialized = true;
        }

        private bool TryGetDesired(out Vector3 desired)
        {
            desired = transform.position;

            if (GameConfig.Instance == null)
                return false;

            if (cam == null)
                cam = GetComponent<Camera>();

            if (DuelRules.Enabled)
            {
                cam.orthographic = false;
                cam.fieldOfView = 46f;
                cam.rect = new Rect(0, 0, 1, 1);

                var cfg = GameConfig.Instance;

                Rect safe = SafeAreaRoot.NormalizedSafeArea();

                Rect playingView = Rect.MinMaxRect(
                    safe.xMin + safe.width * .035f,
                    safe.yMin + safe.height * .16f,
                    safe.xMax - safe.width * .035f,
                    safe.yMax - safe.height * .13f
                );

                var bounds = new Bounds(
                    new Vector3(0, 1.6f, 0),
                    new Vector3(
                        cfg.pitchLength + 4.4f,
                        3.2f,
                        cfg.pitchWidth + 2.4f
                    )
                );

                desired = FitBoundsPosition(
                    bounds,
                    Quaternion.Euler(cfg.cameraAngle, 0, 0),
                    cam.fieldOfView,
                    cam.aspect,
                    playingView
                );
                desired.y -= 4f;
                // Physically move camera closer/further
                // WITHOUT changing perspective/FOV.
                desired =
                    bounds.center +
                    (desired - bounds.center) *
                    cameraDistanceScale;

                return true;
            }

            Vector2 focus2D;
            float visualHeight = 0f;

            var ball = BallControl.Instance;

            if (ball != null)
            {
                focus2D = ball.transform.position;
                visualHeight = ball.VisualArcHeight;

                var ballRb =
                    ball.GetComponent<Rigidbody2D>();

                if (ballRb != null &&
                    ball.Mode == BallControl.BallMode.Free)
                {
                    Vector2 look =
                        ballRb.linearVelocity *
                        Mathf.Max(
                            0f,
                            GameConfig.Instance
                                .cameraBallVelocityLookAheadSeconds
                        );

                    look = Vector2.ClampMagnitude(
                        look,
                        Mathf.Max(
                            0f,
                            GameConfig.Instance
                                .cameraBallVelocityLookAheadMax
                        )
                    );

                    focus2D += look;
                }
            }
            else
            {
                var h =
                    TeamManager.Instance != null
                        ? TeamManager.Instance.CurrentHuman()
                        : null;

                if (h == null)
                    return false;

                focus2D = h.transform.position;

                if (h.Visual != null)
                {
                    visualHeight =
                        h.Visual.baseHeight +
                        h.Visual.extraHeight;
                }
            }

            Vector3 target =
                new Vector3(
                    focus2D.x,
                    visualHeight,
                    focus2D.y
                );

            float aspect =
                Mathf.Max(.5f, cam.aspect);

            float vfov =
                cam.fieldOfView *
                Mathf.Deg2Rad;

            float hfov =
                2f *
                Mathf.Atan(
                    Mathf.Tan(vfov * .5f) *
                    aspect
                );

            float visibleFraction =
                Mathf.Max(
                    GameConfig.Instance.cameraVisibleFraction,
                    0.82f
                );

            if (MatchTimer.Instance != null &&
                MatchTimer.Instance.IsFinalStretch)
            {
                visibleFraction *=
                    GameConfig.Instance
                        .finalStretchCameraTighten;
            }

            float outsideMargin =
                Mathf.Max(
                    GameConfig.Instance.cameraOutsideViewMargin,
                    3.2f
                );

            float visibleLength =
                GameConfig.Instance.pitchLength *
                visibleFraction +
                outsideMargin;

            float slant =
                visibleLength /
                (2f * Mathf.Tan(hfov * .5f));

            float angle =
                GameConfig.Instance.cameraAngle *
                Mathf.Deg2Rad;

            float height =
                slant * Mathf.Sin(angle);

            float distance =
                slant * Mathf.Cos(angle);

            Vector3 offset =
                new Vector3(
                    0f,
                    height,
                    -distance
                );

            // Same physical zoom control for normal gameplay.
            offset *= cameraDistanceScale;

            desired = target + offset;

            return true;
        }

        public void AddShake(float amount)
        {
            shakeOffset += new Vector3(
                Random.Range(-amount, amount),
                Random.Range(-amount, amount),
                0f
            );
        }

        public static Vector3 FitBoundsPosition(
            Bounds bounds,
            Quaternion rotation,
            float verticalFov,
            float aspect,
            Rect viewport
        )
        {
            Vector3 right =
                rotation * Vector3.right;

            Vector3 up =
                rotation * Vector3.up;

            Vector3 forward =
                rotation * Vector3.forward;

            float tanV =
                Mathf.Tan(
                    verticalFov *
                    Mathf.Deg2Rad *
                    .5f
                );

            float tanH =
                tanV *
                Mathf.Max(.1f, aspect);

            float left =
                viewport.xMin * 2 - 1;

            float bottom =
                viewport.yMin * 2 - 1;

            float rightEdge =
                viewport.xMax * 2 - 1;

            float top =
                viewport.yMax * 2 - 1;

            float cx =
                viewport.center.x * 2 - 1;

            float cy =
                viewport.center.y * 2 - 1;

            float distance = 1f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner =
                    new Vector3(
                        (i & 1) == 0
                            ? -bounds.extents.x
                            : bounds.extents.x,

                        (i & 2) == 0
                            ? -bounds.extents.y
                            : bounds.extents.y,

                        (i & 4) == 0
                            ? -bounds.extents.z
                            : bounds.extents.z
                    );

                float x =
                    Vector3.Dot(corner, right);

                float y =
                    Vector3.Dot(corner, up);

                float z =
                    Vector3.Dot(corner, forward);

                distance =
                    Mathf.Max(
                        distance,
                        1f - z
                    );

                distance =
                    Mathf.Max(
                        distance,
                        (x - rightEdge * tanH * z) /
                        ((rightEdge - cx) * tanH)
                    );

                distance =
                    Mathf.Max(
                        distance,
                        (left * tanH * z - x) /
                        ((cx - left) * tanH)
                    );

                distance =
                    Mathf.Max(
                        distance,
                        (y - top * tanV * z) /
                        ((top - cy) * tanV)
                    );

                distance =
                    Mathf.Max(
                        distance,
                        (bottom * tanV * z - y) /
                        ((cy - bottom) * tanV)
                    );
            }

            distance += .5f;

            return
                bounds.center -
                forward * distance -
                right * (cx * tanH * distance) -
                up * (cy * tanV * distance);
        }
    }
}