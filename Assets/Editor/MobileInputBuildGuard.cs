#if UNITY_EDITOR
using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BeastSoccer.EditorTools
{
    /// <summary>
    /// Converts build-scene EventSystems to the new UI input module when the
    /// Input System package is present, without requiring a compile-time reference.
    /// </summary>
    public sealed class MobileInputBuildGuard : IProcessSceneWithReport
    {
        private const string NewUiModuleType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        public int callbackOrder => -1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            Type newType = Type.GetType(NewUiModuleType);
            if (newType == null) return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var eventSystem in root.GetComponentsInChildren<EventSystem>(true))
                {
                    if (eventSystem == null) continue;
                    var go = eventSystem.gameObject;

                    var legacy = go.GetComponent<StandaloneInputModule>();
                    if (legacy != null)
                        UnityEngine.Object.DestroyImmediate(legacy, true);

                    if (go.GetComponent(newType) == null)
                        go.AddComponent(newType);
                }
            }
        }
    }
}
#endif
