using UnityEngine;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// FIX27 runtime-only guard for manually installed presentation art.
    /// It never creates, deletes, repositions or replaces custom pitch/goal/ball assets.
    /// It only hides generated placeholders when a custom model already exists.
    /// </summary>
    public static class PresentationArtGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PreserveInstalledArt()
        {
            var visuals = GameObject.Find("VISUALS_3D");
            if (visuals == null) return;

            var ballRoot = visuals.transform.Find("Ball_3D_ATTACH_MODEL");
            if (ballRoot != null)
            {
                Transform placeholder = ballRoot.Find("BallModel_PLACEHOLDER");
                bool hasCustom = false;
                for (int i = 0; i < ballRoot.childCount; i++)
                {
                    var c = ballRoot.GetChild(i);
                    if (c != placeholder && c.gameObject.activeSelf) { hasCustom = true; break; }
                }
                if (placeholder != null && hasCustom) placeholder.gameObject.SetActive(false);
            }

            var env = visuals.transform.Find("Environment_ATTACH_OR_REPLACE_3D_MODELS");
            if (env == null) return;
            HideGoalPlaceholdersWhenCustomExists(env.Find("Goal_PosX_ATTACH_PREFAB_HERE"));
            HideGoalPlaceholdersWhenCustomExists(env.Find("Goal_NegX_ATTACH_PREFAB_HERE"));
        }

        private static void HideGoalPlaceholdersWhenCustomExists(Transform root)
        {
            if (root == null) return;
            bool hasCustom = false;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!c.name.StartsWith("PLACEHOLDER_") && c.gameObject.activeSelf)
                {
                    hasCustom = true;
                    break;
                }
            }
            if (!hasCustom) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name.StartsWith("PLACEHOLDER_")) c.gameObject.SetActive(false);
            }
        }
    }
}
