# Mobile cleanup review

Baseline: `release/mobile-2026-10-22`, commit `885dba3550df1b6d8e9b1c768d7eb835d68bd714`.
Implementation branch: `cleanup/mobile-systems-2026-11-03`.

## Result

The menu/HUD are authored scene data, camera behavior has one owner, named characters use one animation
pipeline, and goals/boundaries share one resolver. Historical migrations and duplicate initialization
were removed. Layout-editing commands now have straightforward names and purpose labels.

Both backdrops are intentional and retained. Larger-team and future-upgrade implementations remain in
the active codebase. The cleanup does not reduce the project to a duel-only game.

## Original audit decisions

| Audit items | Action |
| --- | --- |
| 1–5: overlapping patches/camera/logos/retries | Retired both persistent presentation patches. Saved HUD/logo references and camera settings in their owning scenes/components. |
| 6–8: menu lock/art restoration/backdrops | Removed the per-frame lock and saved the texture/UV results. Preserved the geometry the lock actually displayed and retained both backdrops. |
| 9–11: HUD layout/safe areas/orientation | Saved the duel HUD and a retained team profile. Removed duplicate canvas fitting writes and the conflicting orientation override. |
| 12–15: animation/glow/configuration/ball setup | Kept approved PNG animation and generic Animator support; removed obsolete named-character overrides and the second aura. TeamManager configures the roster once. BallControl owns ball physics setup. |
| 16–18: migrations/duration/tuning | Applied known migrations once to scene data, saved the effective duration, and exposed surviving demo/throw/shot constants in GameConfig. |
| 19–22: input/AI/control/boundaries | Serialized the correct input modules, unified AI ultimate decisions, removed repeated AI possession handoff, and centralized goal/boundary arbitration with the existing mouth grace protection. |
| 23–26: legacy menus/empty shells/old Goro override | Removed obsolete hierarchies and compatibility shells; retained source artwork for future menu/character options. |
| 27: dribble reward tracking | Kept the functional future hook, with an immediate exit when its configured reward is zero. |
| 28–31: unused AI component/kickoff return/dead fields/markers | Removed abandoned implementations, 29 ineffective configuration fields, and permanently suppressed marker objects. |
| 32–38: larger teams, modes, controls, abilities, halftime/golden goal | Retained intentionally for upgrades. The eight-player scene and original team HUD profile remain. |
| 39: editor tooling/docs/tests | Replaced repair commands with one non-mutating scene validator and a build guard. Archived old tools/notes and updated/extended PlayMode tests. |
| 40: packages | Removed navigation, Unity version-control integration, multiplayer center, Timeline and visual scripting. Retained purchasing for the future shop. Dependency graph remains complete. |
| 41–42: duplicate assets/template content | Remapped ten identical Leo-frame copies, archived historical art/controllers/materials, and removed the unused template scene/tutorial. Retained the pipeline's referenced volume profile with a descriptive name. |

The obsolete kickoff *return* sequence was never armed; ordinary kickoff formation/pass behavior stays.
The old save/throw, ball landing and tackle-protection behavior has been preserved. Camera fitting and
goal/boundary consolidation need rendered/device regression checks before merge.

## Checks performed in this environment

- All active C# files parsed with the C# tree-sitter grammar.
- Product scene local references, callback method names, transform-parent links and script GUIDs checked.
- No new unresolved asset GUIDs relative to the imported baseline.
- Both serialized input modules/actions, full authored roster and named-character animation sequences checked.
- Remaining direct/transitive package dependencies checked.
- All surviving authored menu RectTransform geometry compared against the baseline: unchanged.
- All 44 pre-existing 2D physics components compared against the baseline: unchanged.
- Surviving effective GameConfig settings compared against the previous migration results: preserved.
- Approved runtime animation files compared against the baseline: unchanged.
- Git whitespace checks.

## Checks not performed

Unity is not installed in this environment. No Unity semantic compilation, PlayMode execution,
rendered screenshot comparison, Android/iOS build or device gameplay test has been performed.
The branch must pass those checks before merge; the exact smoke-test list is in README.md.
