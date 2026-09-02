using UnityEngine;
using BeastSoccer.Player;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// FIX34: complete temporary Goro sprite wiring. Uses the supplied directional run sheets
    /// during normal movement and the supplied ult frames for CHARGE / SLAM. It is runtime-only,
    /// so no Animator Controller or scene rebuild is required.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class GoroSpriteOverride : MonoBehaviour
    {
        public PlayerVisualProxy visual;
        public SpriteRenderer targetRenderer;
        public float runFps = 10f;
        public float ultFps = 9f;
        public float pixelsPerUnit = 360f;

        private Sprite[] side;
        private Sprite[] front3Q;
        private Sprite[] front;
        private Sprite[] back3Q;
        private Sprite[] back;
        private Sprite[] frontDown;
        private Sprite[] charge;
        private Sprite[] slam;

        private void Awake()
        {
            if (visual == null) visual = GetComponent<PlayerVisualProxy>();
            if (targetRenderer == null && visual != null) targetRenderer = visual.spriteRenderer;
            LoadFrames();
        }

        private void LoadFrames()
        {
            side = LoadSequence("GoroRun/Side/Goro_Run_Side_", 8);
            front3Q = LoadSequence("GoroRun/Front3Q/Goro_Run_Front3Q_", 8);
            front = LoadSequence("GoroRun/Front/Goro_Run_Front_", 8);
            back3Q = LoadSequence("GoroRun/Back3Q/Goro_Run_Back3Q_", 8);
            back = LoadSequence("GoroRun/Back/Goro_Run_Back_", 8);
            frontDown = LoadSequence("GoroRun/FrontDown/Goro_Run_FrontDown_", 8);
            charge = LoadSequence("GoroUlt/Charge/Goro_Charge_", 4);
            slam = LoadSequence("GoroUlt/Slam/Goro_Slam_", 4);
        }

        private Sprite[] LoadSequence(string prefix, int count)
        {
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string path = prefix + (i + 1).ToString("00");
                Texture2D tex = Resources.Load<Texture2D>(path);
                if (tex == null) continue;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                result[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .08f), Mathf.Max(1f, pixelsPerUnit), 0, SpriteMeshType.FullRect);
                result[i].name = path.Substring(path.LastIndexOf('/') + 1) + "_Runtime";
            }
            return result;
        }

        private void LateUpdate()
        {
            if (visual == null || targetRenderer == null || visual.source == null) return;
            PlayerController source = visual.source;
            if (source.Character != CharacterType.Goro) return;

            Sprite[] frames;
            float fps;
            bool forceNoFlip = false;

            if (source.Ult != null && source.Ult.IsCharging)
            {
                frames = charge;
                fps = ultFps;
                forceNoFlip = true;
            }
            else if (source.Ult != null && source.Ult.IsSlamming)
            {
                frames = slam;
                fps = ultFps;
                forceNoFlip = true;
            }
            else
            {
                Vector2 facing = source.MoveFacing.sqrMagnitude > 0.001f ? source.MoveFacing.normalized : Vector2.right;
                frames = ChooseRunFrames(facing);
                fps = runFps * Mathf.Clamp(source.CurrentVelocity.magnitude / 3.0f, 0.72f, 1.25f);
                targetRenderer.flipX = facing.x < -0.05f;
            }

            if (forceNoFlip) targetRenderer.flipX = false;
            ApplyFrame(frames, fps);
        }

        private Sprite[] ChooseRunFrames(Vector2 facing)
        {
            float ax = Mathf.Abs(facing.x);
            float ay = Mathf.Abs(facing.y);
            float depth = Mathf.Atan2(ay, Mathf.Max(.0001f, ax)) * Mathf.Rad2Deg;
            bool towardCamera = facing.y < -0.08f;
            bool awayFromCamera = facing.y > 0.08f;

            if (depth <= 18f) return side;
            if (depth < 66f) return towardCamera ? front3Q : back3Q;
            if (towardCamera)
            {
                // The extra front-down sheet gets the steepest toward-camera angle.
                return depth >= 82f ? frontDown : front;
            }
            if (awayFromCamera) return back;
            return side;
        }

        private void ApplyFrame(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0) return;
            int frame = Mathf.FloorToInt(Time.time * Mathf.Max(1f, fps)) % frames.Length;
            for (int tries = 0; tries < frames.Length && frames[frame] == null; tries++)
                frame = (frame + 1) % frames.Length;
            if (frames[frame] != null) targetRenderer.sprite = frames[frame];
        }
    }
}
