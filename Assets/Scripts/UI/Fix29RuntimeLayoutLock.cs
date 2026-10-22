using System.Collections.Generic;
using UnityEngine;

namespace BeastSoccer.UI
{
    /// <summary>
    /// V12.5.2: makes the saved FIX29 hierarchy the runtime geometry source of truth.
    ///
    /// It snapshots the authored RectTransforms before normal Start() logic runs,
    /// then restores only geometry after all normal LateUpdate work.
    /// Active state, text, selection state and button callbacks are NOT restored.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class Fix29RuntimeLayoutLock : MonoBehaviour
    {
        public RectTransform root;

        sealed class Entry
        {
            public RectTransform rt;
            public Transform parent;
            public int siblingIndex;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector3 localScale;
            public Quaternion localRotation;
        }

        readonly List<Entry> entries = new List<Entry>();
        bool captured;

        void Awake()
        {
            ResolveRoot();
            Capture();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            ResolveRoot();
            if (!captured) Capture();
        }

        public void CaptureNow()
        {
            ResolveRoot();
            Capture();
        }

        void ResolveRoot()
        {
            if (root != null) return;

            Transform t = transform.Find("FIX29_MENU_EDITABLE");
            if (t == null)
            {
                var menu = GetComponent<MainMenuUI>();
                if (menu != null)
                    t = menu.transform.Find("FIX29_MENU_EDITABLE");
            }

            if (t != null) root = t as RectTransform;
        }

        void Capture()
        {
            entries.Clear();
            captured = false;
            if (root == null) return;

            var rects = root.GetComponentsInChildren<RectTransform>(true);
            foreach (var rt in rects)
            {
                if (rt == null) continue;

                entries.Add(new Entry
                {
                    rt = rt,
                    parent = rt.parent,
                    siblingIndex = rt.GetSiblingIndex(),
                    anchorMin = rt.anchorMin,
                    anchorMax = rt.anchorMax,
                    pivot = rt.pivot,
                    anchoredPosition = rt.anchoredPosition,
                    sizeDelta = rt.sizeDelta,
                    localScale = rt.localScale,
                    localRotation = rt.localRotation
                });
            }

            captured = true;
        }

        // Very late so stale scripts cannot win after this.
        void LateUpdate()
        {
            if (!captured || root == null) return;

            foreach (var e in entries)
            {
                if (e == null || e.rt == null) continue;

                if (e.parent != null && e.rt.parent != e.parent)
                    e.rt.SetParent(e.parent, false);

                e.rt.anchorMin = e.anchorMin;
                e.rt.anchorMax = e.anchorMax;
                e.rt.pivot = e.pivot;
                e.rt.anchoredPosition = e.anchoredPosition;
                e.rt.sizeDelta = e.sizeDelta;
                e.rt.localScale = e.localScale;
                e.rt.localRotation = e.localRotation;

                if (e.parent != null)
                {
                    int max = Mathf.Max(0, e.parent.childCount - 1);
                    e.rt.SetSiblingIndex(Mathf.Clamp(e.siblingIndex, 0, max));
                }
            }
        }
    }
}
