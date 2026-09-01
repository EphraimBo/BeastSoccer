using System.Collections.Generic;
using UnityEngine;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// Tiny procedural comic/NFL-style collision burst. No texture asset is required:
    /// radial shards + an expanding ring are generated at the tackle contact point.
    /// </summary>
    public class ComicImpactFX : MonoBehaviour
    {
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private float born;
        private float life = 0.34f;
        private Material lineMaterial;

        public static void Spawn(Vector2 simulationPosition, float scale = 1f)
        {
            var go = new GameObject("Tackle_ComicImpactFX");
            go.transform.position = new Vector3(simulationPosition.x, 0.82f, simulationPosition.y);
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            go.transform.localScale = Vector3.one * Mathf.Max(0.35f, scale);
            go.AddComponent<ComicImpactFX>().Build();
        }

        private void Build()
        {
            born = Time.time;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) { Destroy(gameObject); return; }
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            const int shardCount = 16;
            for (int i = 0; i < shardCount; i++)
            {
                float a = (i / (float)shardCount) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
                float inner = Random.Range(0.08f, 0.17f);
                float outer = Random.Range(0.48f, 0.78f);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var lr = MakeLine("Spark_" + i, false, Random.Range(0.035f, 0.060f));
                lr.positionCount = 2;
                lr.SetPosition(0, dir * inner);
                lr.SetPosition(1, dir * outer);
                lines.Add(lr);
            }

            var ring = MakeLine("ImpactRing", true, 0.030f);
            ring.positionCount = 28;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.22f, 0f));
            }
            lines.Add(ring);
        }

        private LineRenderer MakeLine(string childName, bool loop, float width)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var lr = child.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.sharedMaterial = lineMaterial;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.45f;
            lr.startColor = new Color(1f, 0.96f, 0.18f, 1f);
            lr.endColor = new Color(1f, 0.28f, 0.05f, 0.95f);
            lr.sortingOrder = 100;
            return lr;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - born) / life);
            float alpha = 1f - t;
            transform.localScale *= 1f + Time.deltaTime * 2.25f;
            foreach (var lr in lines)
            {
                if (lr == null) continue;
                Color a = lr.startColor; a.a = alpha; lr.startColor = a;
                Color b = lr.endColor; b.a = alpha * 0.9f; lr.endColor = b;
            }
            if (t >= 1f)
            {
                if (lineMaterial != null) Destroy(lineMaterial);
                Destroy(gameObject);
            }
        }
    }
}
