using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BeastSoccer.Input
{
    /// <summary>
    /// Mobile-safe EventSystem compatibility without a compile-time dependency on
    /// UnityEngine.InputSystem. If the new Input System package is available, the
    /// scene's legacy StandaloneInputModule is replaced with InputSystemUIInputModule.
    /// Otherwise the legacy module is retained.
    /// </summary>
    public static class EventSystemCompatibility
    {
        private const string NewUiModuleType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PatchInitialScene() => PatchAllEventSystems();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PatchAllEventSystems();

        public static void PatchAllEventSystems()
        {
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var eventSystem in systems)
                if (eventSystem != null) Patch(eventSystem.gameObject);
        }

        private static void Patch(GameObject go)
        {
            if (go == null) return;

            Type newType = Type.GetType(NewUiModuleType);
            var legacy = go.GetComponent<StandaloneInputModule>();

            if (newType != null)
            {
                if (legacy != null)
                {
                    legacy.enabled = false;
                    UnityEngine.Object.Destroy(legacy);
                }

                if (go.GetComponent(newType) == null)
                    go.AddComponent(newType);
            }
            else
            {
                // Package unavailable: keep a usable legacy module instead of creating
                // a missing-type compile/runtime failure.
                if (legacy == null)
                    go.AddComponent<StandaloneInputModule>();
                else
                    legacy.enabled = true;
            }
        }
    }
}
