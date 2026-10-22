using UnityEngine;

namespace BeastSoccer.UI
{
    /// <summary>
    /// V12.5.2 compatibility shell.
    /// FIX29_MENU_EDITABLE is authored in MainMenu.unity and MUST NOT be laid out at runtime.
    /// This class intentionally performs no transform, texture, crop, reparent or sizing work.
    /// </summary>
    public sealed class DuelMenuLayout : MonoBehaviour
    {
        public static void Install(BeastMenuOverhaul menu)
        {
            // Intentionally empty.
            // Kept only so older references/calls compile safely.
        }
    }

    // Compatibility shells for older scene/script references.
    public sealed class MenuDesignScale : MonoBehaviour { }
    public sealed class SelectionPop : MonoBehaviour { }
}
