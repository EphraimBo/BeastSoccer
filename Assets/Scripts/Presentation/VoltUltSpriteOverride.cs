using UnityEngine;
using BeastSoccer.Player;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// FIX32: temporary Volt ult presentation layer. During FLY it replaces the normal run
    /// animation with the generated four-frame wing flap. During BLOCK it uses the supplied
    /// expanded-wing frames while gameplay movement remains normal ground movement.
    /// Runtime-created Sprites keep this patch scene-safe: no Animator Controller rebake required.
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

        private Sprite[] flightFrames;
        private Sprite[] blockFrames;

        private void Awake()
        {
            if (visual == null) visual = GetComponent<PlayerVisualProxy>();
            if (targetRenderer == null && visual != null) targetRenderer = visual.spriteRenderer;
            LoadFrames();
        }

        private void Start()
        {
            if (flightFrames == null || blockFrames == null) LoadFrames();
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

            if (frames == null || frames.Length == 0) return;
            int validCount = 0;
            for (int i = 0; i < frames.Length; i++) if (frames[i] != null) validCount++;
            if (validCount == 0) return;

            int frame = Mathf.FloorToInt(Time.time * Mathf.Max(1f, fps)) % frames.Length;
            for (int tries = 0; tries < frames.Length && frames[frame] == null; tries++) frame = (frame + 1) % frames.Length;
            if (frames[frame] != null) targetRenderer.sprite = frames[frame];
        }
    }
}
