using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeastSoccer.UI
{
    /// <summary>
    /// FIX30: edit-mode layout data for the baked Beast Soccer menu.
    /// This component does nothing in Play Mode. In Edit Mode the custom inspector
    /// applies changes immediately to the actual RectTransforms in MainMenu.unity,
    /// so what you see is what gets saved with the scene.
    /// </summary>
    [ExecuteAlways]
    public class BeastMenuLiveLayout : MonoBehaviour
    {
        [Serializable]
        public class LayoutItem
        {
            public string label;
            public string path;
            public RectTransform target;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector3 localScale = Vector3.one;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot = new Vector2(.5f, .5f);
        }

        [Tooltip("When enabled, Inspector edits are pushed to the visible menu immediately while Play Mode is OFF.")]
        public bool livePreview = true;

        [Tooltip("Every editable RectTransform beneath FIX29_MENU_EDITABLE. Use the custom inspector to move/resize them while looking at the Game view.")]
        public List<LayoutItem> items = new List<LayoutItem>();

        public void CaptureFromScene()
        {
            items.Clear();
            Transform root = transform.Find("FIX29_MENU_EDITABLE");
            if (root == null)
            {
                Debug.LogWarning("[Beast Soccer] FIX30 could not find FIX29_MENU_EDITABLE. Bake the editable menu once first.", this);
                return;
            }

            var rects = root.GetComponentsInChildren<RectTransform>(true);
            foreach (var rt in rects)
            {
                if (rt.transform == root) continue;

                items.Add(new LayoutItem
                {
                    label = FriendlyName(rt),
                    path = RelativePath(root, rt.transform),
                    target = rt,
                    anchoredPosition = rt.anchoredPosition,
                    sizeDelta = rt.sizeDelta,
                    localScale = rt.localScale,
                    anchorMin = rt.anchorMin,
                    anchorMax = rt.anchorMax,
                    pivot = rt.pivot
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
                if (item.target == null) continue;
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
            foreach (var i in items)
            {
                if ((i.label != null && i.label.ToLowerInvariant().Contains(key)) ||
                    (i.path != null && i.path.ToLowerInvariant().Contains(key)))
                    return i;
            }
            return null;
        }

        void ResolveMissingTargets()
        {
            Transform root = transform.Find("FIX29_MENU_EDITABLE");
            if (root == null) return;
            foreach (var item in items)
            {
                if (item.target != null) continue;
                var t = root.Find(item.path);
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
            string p = rt.parent != null ? rt.parent.name : "";
            return string.IsNullOrEmpty(p) ? rt.name : p + "  /  " + rt.name;
        }
    }
}
