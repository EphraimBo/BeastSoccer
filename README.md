# Beast Soccer

Unity mobile prototype with 2D gameplay simulation and a 3D pitch/character presentation.
The tracked project version is Unity **6000.5.1f1**. The cleanup does not upgrade the editor version.

## Open and edit

Open `Assets/Scenes/MainMenu.unity`. The product build scene list remains MainMenu, then Match.

| Unity menu command | Purpose |
| --- | --- |
| Beast Soccer → Edit Menu Layout (positions and sizes) | Select and edit the saved menu RectTransforms. Keep Play Mode off; save the scene afterward. |
| Beast Soccer → Edit Match Layout (controls and HUD) | Edit the saved duel HUD, including the controls, logos and overlays. |
| Beast Soccer → Refresh Match Layout (read current scene) | Refresh the inspector's list after manually changing the hierarchy. |
| Beast Soccer → Validate Scene (check wiring before building) | Check scripts, input modules, roster references and HUD profiles without rebuilding the scene. |

The menu root is **Menu (edit layout here)**. Both intentionally layered backdrops remain:
**Fullscreen Backdrop (covers screen edges)** and **Menu Artwork Backdrop (behind menu)**.
The saved menu geometry is authoritative; no runtime layout lock or presentation patch rewrites it.

The Match Canvas contains the saved duel HUD. `MatchHudLayout` retains the original team layout as a
separate profile. Do not overwrite that profile when editing duel controls.

## Systems retained for upgrades

The duel is temporary. The scene still contains the full eight-player authored roster, which is reduced
to two outfielders and two keepers only when `GameConfig.arcadeDuel` is enabled.

The following implementations remain available:

- Larger-team rosters, tactical roles, formation AI and control switching.
- Passing, through balls, lobs, interceptions, jockeying and goalkeeper distribution.
- Defending mode, keeper takeover, wave resets and save scoring.
- Team throw-ins/corners, halftime, direction changes and optional golden goal.
- Goro's charge/slam and Volt's flight/block abilities.
- Optional dribble/action ultimate rewards. The tracker exits immediately while their configured reward is zero.
- Generic-character Animator/idle-art hooks for larger-team matches.
- The purchasing package and billing configuration for a future shop.

To inspect the existing larger-team implementation, turn **Arcade Duel** off on the Match scene's
GameConfig before entering Play Mode. The team HUD profile is restored at startup. The present formation
implementation expects three outfielders and one keeper per team; expanding it further is upgrade work.

## Ownership of settings and presentation

| Responsibility | Owner |
| --- | --- |
| Match launch choices | MainMenuUI / MatchSetup |
| Menu selection state | MenuController |
| Camera framing, rendered FOV and zoom | FollowCamera |
| Approved named-character frames | CharacterSpriteAnimator |
| Generic/future Animator parameters | CharacterAnimationDriver |
| Character aura | UltGlowEffect |
| Goal/boundary arbitration | MatchBoundaryResolver |
| Per-player and ball interpolation | Their own Rigidbody initialization |
| Screen orientation and safe areas | SafeAreaRoot / SafeAreaDesignSpace |
| Gameplay tuning, including former demo constants | GameConfig |

The regulation duration is saved directly as **195 seconds**, matching the previous runtime duration
patch. Known historical GameConfig migrations have been applied to the saved scene and retired.
Ineffective Inspector fields have been removed. Current shot/throw speeds and contact delays retain
their effective settings; the approved camera view retains its 46° fitting lens, 39.5° rendered lens,
0.8 distance scale and 0.78 duel zoom factor under one component.

Both scenes now serialize the new Input System UI module and its actions. The new Input System remains
a direct assembly dependency, and Editor keyboard controls support it. No editor/build/runtime module
conversion scripts are needed.

## Validation

Run the source/serialization checks with:

```bash
python -m pip install -r Tools/requirements.txt
python Tools/validate_project.py
```

These checks validate C# syntax, local scene references, callback names, parent links, asset GUIDs,
input wiring, the authored roster, approved animation sequences and the remaining package graph.
Known external asset references from the imported baseline are recorded in
`Tools/package_reference_guids.json`; resolving those packages/assets still requires a Unity import.

**These checks are not a Unity compilation or runtime test.**

In Unity, run the PlayMode suite under **Window → General → Test Runner**. It covers menu/match flow,
keeper outlets, pause/resume, viewport fitting, current camera settings, Change Players, a queued
goal-versus-boundary event, and the retained larger-team kickoff/halftime flow.

Before merging/building for testers:

1. Import and compile the branch in Unity; run the PlayMode suite.
2. Play both Leo and Volt against both rivals. Check menu artwork, both backdrops, buttons and card animation.
3. Check phone/tablet safe areas and both landscape orientations, including pause and result panels.
4. Test keeper catches/throws, moving outlet receivers, shots beside both posts, deflections, corners and throw-ins.
5. Test ultimate activation, tackle protection, pause during an ability, restart, match end and Change Players.
6. Disable Arcade Duel and check the larger-team controls, kickoff, halftime and defending mode.
7. Make and smoke-test fresh Android/iOS builds before distributing them.

## Archives

`Documentation/Archive` preserves historical patch notes and obsolete editor tools as text.
Those old tools describe earlier scene formats and are historical reference, not current scene repair commands.

`SourceArt` preserves superseded artwork, menu options, Animator clips/controllers and unreferenced
generated materials outside Unity's active import tree. Approved runtime animation PNGs remain unchanged.
Binary-identical Leo frame imports were consolidated after their asset references were remapped.
