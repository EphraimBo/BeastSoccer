using System.Collections.Generic;
using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    /// <summary>Authored pitch artwork references and geometry for the duel shooting arcs.</summary>
    [AddComponentMenu("Beast Soccer/Pitch Presentation (artwork and shooting zones)")]
    public sealed class PitchPresentation : MonoBehaviour
    {
        public Renderer[] pitchRenderers;
        public Texture2D duelPitchArtwork;
        private Material lineMaterial;
        private readonly List<Material> pitchMaterials = new List<Material>();

        private void Start()
        {
            if (!DuelRules.Enabled) return;
            if (duelPitchArtwork != null && pitchRenderers != null)
                foreach (var renderer in pitchRenderers)
                {
                    if (renderer == null) continue;
                    var material = renderer.material;
                    material.mainTexture = duelPitchArtwork;
                    pitchMaterials.Add(material);
                }
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            AddZone(1);
            AddZone(-1);
        }

        private void AddZone(int direction)
        {
            var go = new GameObject(direction == 1 ? "Home Shooting Zone" : "Away Shooting Zone");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 65;
            line.widthMultiplier = .055f;
            line.startColor = line.endColor = new Color(.22f, .65f, 1f, .44f);
            line.numCornerVertices = 3;
            for (int i = 0; i < 65; i++)
            {
                float angle = (90f + 180f * i / 64f) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(direction * (GameConfig.Instance.pitchLength * .5f +
                    Mathf.Cos(angle) * DuelRules.ShotRadius), .065f, Mathf.Sin(angle) * DuelRules.ShotRadius));
            }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
            foreach (var material in pitchMaterials) if (material != null) Destroy(material);
        }
    }
}
