using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// FIX41: visible sprite-free ult aura. Uses enlarged echo silhouettes plus a small procedural
    /// energy particle field. No authored glow sprites/materials are required.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class UltGlowEffect : MonoBehaviour
    {
        public PlayerVisualProxy visual;
        public SpriteRenderer targetRenderer;
        public CharacterType character = CharacterType.Generic;

        [Header("Aura")]
        public float pulseSpeed = 4.6f;
        public float innerScale = 1.49f;
        public float outerScale = 2.19f;
        public float innerAlpha = 0.34f;
        public float outerAlpha = 0.18f;
        public float edgeOffset = 0.15f;

        [Header("Energy")]
        public float particleRate = 15f;
        public float particleLifetime = 0.62f;
        public float particleSize = 0.10f;

        private SpriteRenderer inner;
        private SpriteRenderer outer;
        private SpriteRenderer left;
        private SpriteRenderer right;
        private SpriteRenderer up;
        private SpriteRenderer down;
        private ParticleSystem energy;
        private ParticleSystemRenderer energyRenderer;
        private Material energyMaterial;
        private Texture2D softDot;

        private void Awake()
        {
            if (visual == null) visual = GetComponent<PlayerVisualProxy>();
            if (targetRenderer == null && visual != null) targetRenderer = visual.spriteRenderer;
            EnsureLayers();
            EnsureEnergy();
        }

        private void Start()
        {
            EnsureLayers();
            EnsureEnergy();
        }

        private Color GlowColor()
        {
            switch (character)
            {
                case CharacterType.Volt: return new Color(0.18f, 0.86f, 1f, 1f);
                case CharacterType.Goro: return new Color(0.48f, 1f, 0.18f, 1f);
                case CharacterType.Leo:  return new Color(1f, 0.69f, 0.12f, 1f);
                default: return Color.white;
            }
        }

        private void EnsureLayers()
        {
            if (targetRenderer == null) return;
            if (inner == null) inner = MakeLayer("ULT_AURA_INNER", -1);
            if (outer == null) outer = MakeLayer("ULT_AURA_OUTER", -2);
            if (left == null)  left  = MakeLayer("ULT_AURA_LEFT",  -3);
            if (right == null) right = MakeLayer("ULT_AURA_RIGHT", -3);
            if (up == null)    up    = MakeLayer("ULT_AURA_UP",    -3);
            if (down == null)  down  = MakeLayer("ULT_AURA_DOWN",  -3);
        }

        private SpriteRenderer MakeLayer(string layerName, int orderOffset)
        {
            Transform existing = targetRenderer.transform.Find(layerName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(layerName);
            if (existing == null) go.transform.SetParent(targetRenderer.transform, false);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = targetRenderer.sortingLayerID;
            sr.sortingOrder = targetRenderer.sortingOrder + orderOffset;
            sr.sharedMaterial = targetRenderer.sharedMaterial;
            sr.enabled = false;
            return sr;
        }

        private void EnsureEnergy()
        {
            if (targetRenderer == null || energy != null) return;

            Transform existing = targetRenderer.transform.Find("ULT_SPIRIT_ENERGY");
            GameObject go = existing != null ? existing.gameObject : new GameObject("ULT_SPIRIT_ENERGY");
            if (existing == null) go.transform.SetParent(targetRenderer.transform, false);
            go.transform.localPosition = UnityEngine.Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = UnityEngine.Vector3.one;

            energy = go.GetComponent<ParticleSystem>();
            if (energy == null) energy = go.AddComponent<ParticleSystem>();
            energyRenderer = go.GetComponent<ParticleSystemRenderer>();

            var main = energy.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = particleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.55f, particleSize * 1.35f);
            main.maxParticles = 48;

            var emission = energy.emission;
            emission.rateOverTime = particleRate;

            var shape = energy.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new UnityEngine.Vector3(0.72f, 1.10f, 0.04f);
            shape.position = new UnityEngine.Vector3(0f, 0.43f, 0f);

            var velocity = energy.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var colorLife = energy.colorOverLifetime;
            colorLife.enabled = true;
            Gradient g = new Gradient();
            Color c = GlowColor();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.20f), new GradientAlphaKey(0f, 1f) }
            );
            colorLife.color = g;

            softDot = BuildSoftDot(32);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                energyMaterial = new Material(shader);
                energyMaterial.name = "ULT_SPIRIT_ENERGY_RUNTIME";
                energyMaterial.mainTexture = softDot;
                energyRenderer.sharedMaterial = energyMaterial;
            }
            energyRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            energyRenderer.sortingOrder = targetRenderer.sortingOrder + 2;
            energy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private Texture2D BuildSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "ULT_SOFT_DOT_RUNTIME";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float radius = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, false);
            return tex;
        }

        private void LateUpdate()
        {
            if (targetRenderer == null || visual == null || visual.source == null)
            {
                SetVisible(false);
                return;
            }

            bool active = visual.source.Ult != null && visual.source.Ult.IsActive;
            if (!active || targetRenderer.sprite == null)
            {
                SetVisible(false);
                return;
            }

            EnsureLayers();
            EnsureEnergy();
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            Color c = GlowColor();

            SyncLayer(inner, c, Mathf.Lerp(innerAlpha * .78f, innerAlpha, pulse), innerScale + pulse * .018f, UnityEngine.Vector3.zero);
            SyncLayer(outer, c, Mathf.Lerp(outerAlpha * .62f, outerAlpha, pulse), outerScale + pulse * .035f, UnityEngine.Vector3.zero);

            float o = edgeOffset * Mathf.Lerp(.65f, 1.20f, pulse);
            float edgeAlpha = Mathf.Lerp(.11f, .20f, pulse);
            float edgeScale = 1.055f + pulse * .018f;
            SyncLayer(left,  c, edgeAlpha, edgeScale, new UnityEngine.Vector3(-o, 0f, 0f));
            SyncLayer(right, c, edgeAlpha, edgeScale, new UnityEngine.Vector3( o, 0f, 0f));
            SyncLayer(up,    c, edgeAlpha, edgeScale, new UnityEngine.Vector3(0f,  o, 0f));
            SyncLayer(down,  c, edgeAlpha, edgeScale, new UnityEngine.Vector3(0f, -o, 0f));

            if (energy != null && !energy.isPlaying)
            {
                var main = energy.main;
                main.startColor = new Color(c.r, c.g, c.b, 0.72f);
                energy.Play();
            }
        }

        private void SyncLayer(SpriteRenderer layer, Color baseColor, float alpha, float scale, UnityEngine.Vector3 offset)
        {
            if (layer == null) return;
            layer.enabled = true;
            layer.sprite = targetRenderer.sprite;
            layer.flipX = targetRenderer.flipX;
            layer.flipY = targetRenderer.flipY;
            layer.sortingLayerID = targetRenderer.sortingLayerID;
            layer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            layer.transform.localPosition = offset;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = UnityEngine.Vector3.one * scale;
        }

        private void SetVisible(bool visible)
        {
            if (inner != null) inner.enabled = visible;
            if (outer != null) outer.enabled = visible;
            if (left != null) left.enabled = visible;
            if (right != null) right.enabled = visible;
            if (up != null) up.enabled = visible;
            if (down != null) down.enabled = visible;
            if (energy != null)
            {
                if (visible)
                {
                    if (!energy.isPlaying) energy.Play();
                }
                else if (energy.isPlaying)
                {
                    energy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (energyMaterial != null) Destroy(energyMaterial);
            if (softDot != null) Destroy(softDot);
        }
    }
}
