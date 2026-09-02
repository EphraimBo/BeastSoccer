using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeastSoccer.UI
{
    /// <summary>
    /// Edit-mode live layout data for the Match HUD. This component does nothing in Play Mode.
    /// It captures the existing scene UI and lets the custom inspector move/resize/scale it while
    /// you watch the Game view, without rebuilding Match.unity.
    /// </summary>
    [ExecuteAlways]
    public class BeastMatchLiveLayout : MonoBehaviour
    {
        [Serializable]
        public class LayoutItem
        {
            public string label;
            public string path;
            public RectTransform target;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public UnityEngine.Vector3 localScale = UnityEngine.Vector3.one;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot = new Vector2(.5f, .5f);
            public bool editable = true;
        }

        [Tooltip("When enabled, Inspector edits are pushed to the visible Match HUD immediately while Play Mode is OFF.")]
        public bool livePreview = true;

        [Tooltip("Captured RectTransforms from the Match Canvas. Runtime-only objects are intentionally excluded unless they already exist in the scene.")]
        public List<LayoutItem> items = new List<LayoutItem>();

        public RectTransform canvasRoot;

        public void CaptureFromScene()
        {
            items.Clear();
            if (canvasRoot == null) canvasRoot = FindMatchCanvas();
            if (canvasRoot == null)
            {
                Debug.LogWarning("[Beast Soccer] FIX36 could not find a Canvas in the open Match scene.", this);
                return;
            }

            var rects = canvasRoot.GetComponentsInChildren<RectTransform>(true);
            foreach (var rt in rects)
            {
                if (rt == null || rt == canvasRoot) continue;
                if (ShouldSkip(rt)) continue;

                items.Add(new LayoutItem
                {
                    label = FriendlyName(rt),
                    path = RelativePath(canvasRoot, rt.transform),
                    target = rt,
                    anchoredPosition = rt.anchoredPosition,
                    sizeDelta = rt.sizeDelta,
                    localScale = rt.localScale,
                    anchorMin = rt.anchorMin,
                    anchorMax = rt.anchorMax,
                    pivot = rt.pivot,
                    editable = true
                });
            }
        }

        public void PullCurrentValues()
        {
            ResolveMissingTargets();
            foreach (var item in items)
            {
                if (item.target == null) continue;
                var rt = item.target;
                item.anchoredPosition = rt.anchoredPosition;
                item.sizeDelta = rt.sizeDelta;
                item.localScale = rt.localScale;
                item.anchorMin = rt.anchorMin;
                item.anchorMax = rt.anchorMax;
                item.pivot = rt.pivot;
            }
        }

        public void ApplyLayout()
        {
            if (Application.isPlaying || !livePreview) return;
            ResolveMissingTargets();
            foreach (var item in items)
            {
                if (!item.editable || item.target == null) continue;
                var rt = item.target;
                rt.anchorMin = item.anchorMin;
                rt.anchorMax = item.anchorMax;
                rt.pivot = item.pivot;
                rt.anchoredPosition = item.anchoredPosition;
                rt.sizeDelta = item.sizeDelta;
                rt.localScale = item.localScale;
            }
        }

        public LayoutItem Find(string contains)
        {
            if (string.IsNullOrWhiteSpace(contains)) return null;
            string key = contains.ToLowerInvariant();
            foreach (var item in items)
            {
                string hay = ((item.label ?? "") + " " + (item.path ?? "")).ToLowerInvariant();
                if (hay.Contains(key)) return item;
            }
            return null;
        }

        RectTransform FindMatchCanvas()
        {
            var canvases = GetComponentsInChildren<Canvas>(true);
            if (canvases != null && canvases.Length > 0)
                return canvases[0].transform as RectTransform;

            var anyCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            return anyCanvas != null ? anyCanvas.transform as RectTransform : null;
        }

        static bool ShouldSkip(RectTransform rt)
        {
            string n = rt.name.ToLowerInvariant();
            // Skip pure layout internals that are usually annoying to edit directly.
            if (n.Contains("viewport") || n.Contains("content") || n.Contains("scrollbar")) return true;
            return false;
        }

        void ResolveMissingTargets()
        {
            if (canvasRoot == null) canvasRoot = FindMatchCanvas();
            if (canvasRoot == null) return;
            foreach (var item in items)
            {
                if (item.target != null) continue;
                var t = canvasRoot.Find(item.path);
                if (t != null) item.target = t as RectTransform;
            }
        }

        static string RelativePath(Transform root, Transform target)
        {
            var names = new Stack<string>();
            Transform t = target;
            while (t != null && t != root)
            {
                names.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", names.ToArray());
        }

        static string FriendlyName(RectTransform rt)
        {
            string parent = rt.parent != null ? rt.parent.name : "";
            return string.IsNullOrEmpty(parent) ? rt.name : parent + "  /  " + rt.name;
        }
    }
}
