#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using BeastSoccer.Ball;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.UI;

namespace BeastSoccer.EditorTools
{
    /// <summary>Checks scene wiring without rebuilding scenes or silently repairing assets.</summary>
    public sealed class SceneValidator : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("Beast Soccer/Validate Scene (check wiring before building)")]
        public static void ValidateOpenScene()
        {
            var errors = Validate(SceneManager.GetActiveScene());
            foreach (string error in errors) Debug.LogError("[Beast Soccer] " + error);
            if (errors.Count == 0) Debug.Log("[Beast Soccer] Scene wiring checks passed.");
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Only validate the two product scenes, never test harnesses or imported examples.
            if (scene.name != "MainMenu" && scene.name != "Match") return;
            var errors = Validate(scene);
            if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        public static List<string> Validate(Scene scene)
        {
            var errors = new List<string>();
            var eventSystem = Find<EventSystem>(scene);
            if (eventSystem == null || eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                errors.Add(scene.name + ": assign the new Input System UI module.");
            if (eventSystem != null && eventSystem.GetComponent<StandaloneInputModule>() != null)
                errors.Add(scene.name + ": remove the legacy input module.");

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        errors.Add(scene.name + ": missing script on " + transform.name);
                foreach (var arc in root.GetComponentsInChildren<ArcGraphic>(true))
                    if (arc.GetComponent<CanvasRenderer>() == null)
                        errors.Add(scene.name + ": missing CanvasRenderer on " + arc.name);
            }
            if (scene.name == "MainMenu")
            {
                var menu = Find<MenuController>(scene);
                if (menu == null || menu.root == null || menu.modeScreen == null ||
                    menu.pickScreen == null || menu.rivalScreen == null || menu.infoTitle == null || menu.infoBody == null)
                    errors.Add("MainMenu: assign all menu references.");
                return errors;
            }
            if (scene.name != "Match") return errors;
            var teams = Find<TeamManager>(scene);
            var config = Find<GameConfig>(scene);
            if (config == null || Find<GameManager>(scene) == null || Find<BallControl>(scene) == null ||
                Find<RoundReset>(scene) == null || Find<MatchTimer>(scene) == null || Find<ScoreManager>(scene) == null)
                errors.Add("Match: one or more core match systems are missing.");
            if (teams == null) errors.Add("Match: TeamManager is missing.");
            else
            {
                foreach (var roster in new[] { teams.HomeTeam, teams.AwayTeam })
                {
                    int keepers = 0, outfielders = 0;
                    foreach (var player in roster)
                    {
                        if (player == null) { errors.Add("Match: null roster entry."); continue; }
                        if (player.Role == FieldRole.Goalkeeper) keepers++; else outfielders++;
                    }
                    if (keepers != 1 || outfielders < 1)
                        errors.Add("Match: each team needs one keeper and at least one outfielder.");
                    if (config != null && !config.arcadeDuel && outfielders != 3)
                        errors.Add("Match: the current larger-team formation requires three outfielders.");
                }
            }
            var hud = Find<MatchHudLayout>(scene);
            if (hud == null || hud.duelHud == null || hud.teamLayout == null || hud.teamLayout.Length == 0)
                errors.Add("Match: assign the duel HUD and retained team layout.");
            return errors;
        }
    }
}
#endif
