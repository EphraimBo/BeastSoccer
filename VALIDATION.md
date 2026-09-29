# Validation for the demo upgrade

Checked in the remote workspace:

- All 68 C# source files parse without syntax errors (including editor tools and the new PlayMode tests).
- Git whitespace/error check passes.
- Both serialized `GORO_PICK` objects in MainMenu are inactive; both normal and legacy Goro selection callbacks cannot select an outfielder.
- The generated pitch and goalkeeper atlas exist at the exact `Resources.Load` paths, with Unity importer metadata.
- Both texture properties of `BeastPitchArtwork.mat` reference the new bird texture's GUID.
- The old V2 first-scene bootstrap is removed; menu and match startup invoke their own setup on every entry.
- There are no `GetBuiltinResource<Font>("Arial.ttf")` calls in the project scripts.
- The particle velocity X, Y and Z axes are all `TwoConstants` curves.
- Runtime/test assembly references use `UnityEngine.UI`, verified against Unity's uGUI source assembly definition.
- Compatibility files replace the V2 overlay paths so applying this package over V2 does not run two competing installers.

Not executed here: Unity compilation, shader compilation, the Unity PlayMode tests, iOS/Xcode build and device playtesting. The Unity Editor and Xcode are not installed in this workspace. Syntax and reference checks do not substitute for those tests.

Use Unity Test Runner → PlayMode → `DuelFlowTests` to check the menu-to-match path and save/throw flow. See `DEMO_UPGRADE.md` for installation and the short device check.
