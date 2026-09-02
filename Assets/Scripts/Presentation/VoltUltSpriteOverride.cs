using UnityEngine;
using BeastSoccer.Player;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// Volt ult art override. FIX35 automatically matches FLY/BLOCK art to Volt's normal
    /// on-field body height so activating the ult cannot make him visibly shrink.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class VoltUltSpriteOverride : MonoBehaviour
    {
        public PlayerVisualProxy visual;
        public SpriteRenderer targetRenderer;
        public float flightFps = 8f;
        public float blockFps = 8f;
        public float flightPixelsPerUnit = 300f;
        public float blockPixelsPerUnit = 500f;
        public float ultHeightMultiplier = 1.03f;

        private Sprite[] flightFrames;
        private Sprite[] blockFrames;
        private bool wasOverriding;
        private UnityEngine.Vector3 baseLocalScale = UnityEngine.Vector3.one;
        private float normalSpriteHeight = 1f;

        private void Awake()
        {
            if (visual == null) visual = GetComponent<PlayerVisualProxy>();
            if (targetRenderer == null && visual != null) targetRenderer = visual.spriteRenderer;
            LoadFrames();
        }

        private void Start()
        {
            if (flightFrames == null || blockFrames == null) LoadFrames();
            CaptureNormalPresentation();
        }

        private void CaptureNormalPresentation()
        {
            if (targetRenderer == null) return;
            baseLocalScale = targetRenderer.transform.localScale;
            if (targetRenderer.sprite != null)
                normalSpriteHeight = Mathf.Max(0.01f, targetRenderer.sprite.bounds.size.y);
        }

        private void LoadFrames()
        {
            flightFrames = LoadSequence("VoltUlt/Flight/Volt_Fly_", 4, flightPixelsPerUnit);
            blockFrames = LoadSequence("VoltUlt/Block/Volt_Block_", 7, blockPixelsPerUnit);
        }

        private Sprite[] LoadSequence(string prefix, int count, float ppu)
        {
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string path = prefix + (i + 1).ToString("00");
                Texture2D tex = Resources.Load<Texture2D>(path);
                if (tex == null) continue;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                result[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .08f), Mathf.Max(1f, ppu), 0, SpriteMeshType.FullRect);
                result[i].name = path.Substring(path.LastIndexOf('/') + 1) + "_Runtime";
            }
            return result;
        }

        private void LateUpdate()
        {
            if (visual == null || targetRenderer == null || visual.source == null) return;
            var source = visual.source;
            if (source.Character != CharacterType.Volt || source.Ult == null) return;

            Sprite[] frames = null;
            float fps = 0f;
            if (source.Ult.IsFlying)
            {
                frames = flightFrames;
                fps = flightFps;
            }
            else if (source.Ult.IsWingBlockActive)
            {
                frames = blockFrames;
                fps = blockFps;
            }

            if (frames == null || frames.Length == 0)
            {
                if (wasOverriding)
                {
                    targetRenderer.transform.localScale = baseLocalScale;
                    wasOverriding = false;
                }
                else
                {
                    // Keep following the actual normal presentation in case its scale is adjusted elsewhere.
                    baseLocalScale = targetRenderer.transform.localScale;
                    if (targetRenderer.sprite != null)
                        normalSpriteHeight = Mathf.Max(0.01f, targetRenderer.sprite.bounds.size.y);
                }
                return;
            }

            if (!wasOverriding)
            {
                CaptureNormalPresentation();
                wasOverriding = true;
            }

            int validCount = 0;
            for (int i = 0; i < frames.Length; i++) if (frames[i] != null) validCount++;
            if (validCount == 0) return;

            int frame = Mathf.FloorToInt(Time.time * Mathf.Max(1f, fps)) % frames.Length;
            for (int tries = 0; tries < frames.Length && frames[frame] == null; tries++) frame = (frame + 1) % frames.Length;
            Sprite chosen = frames[frame];
            if (chosen == null) return;

            targetRenderer.sprite = chosen;

            // Match the normal visible body height rather than relying on arbitrary PPU guesses.
            float ultHeight = Mathf.Max(0.01f, chosen.bounds.size.y);
            float scaleFactor = (normalSpriteHeight / ultHeight) * Mathf.Max(0.5f, ultHeightMultiplier);
            targetRenderer.transform.localScale = baseLocalScale * scaleFactor;
        }

        private void OnDisable()
        {
            if (targetRenderer != null && wasOverriding)
                targetRenderer.transform.localScale = baseLocalScale;
            wasOverriding = false;
        }
    }
}
