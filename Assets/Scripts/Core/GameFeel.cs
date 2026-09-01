using UnityEngine;
using BeastSoccer.CameraSystem;
using BeastSoccer.Data;

namespace BeastSoccer.Core
{
    public static class GameFeel
    {
        public static void Shake(float amount)
        {
            if(Camera.main==null)return;
            var follow=Camera.main.GetComponent<FollowCamera>();
            follow?.AddShake(amount);
        }

        public static void Haptic()
        {
            // Intentionally left as a platform-safe hook.
            // Native Android/iOS haptics can be connected here later without
            // making the core prototype depend on a platform-specific API.
            if (GameConfig.Instance == null || !GameConfig.Instance.enableHaptics)
                return;
        }
    }
}
