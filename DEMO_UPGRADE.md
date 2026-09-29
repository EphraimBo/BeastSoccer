# Beast Soccer demo upgrade

This upgrade replaces the V2 overlay with changes integrated into the existing match and menu. Import every included file together. It also works on the original GitHub project.

## Apply the patch

Close Play Mode. Copy the supplied `Assets` and `ProjectSettings` folders into the Unity project root and replace matching files. In particular, replace **both** `DemoMatchRules.cs` and `DemoPresentation.cs` from V2: retaining either old installer can reintroduce the old HUD and errors. Their original paths and metadata are included for this reason. Let Unity finish importing, then clear the Console and start from `Assets/Scenes/MainMenu.unity`.

No scene rebuild command or manual component installation is required. The pitch material already references the new bird texture; the menu and match install their layouts during their normal scene startup.

## Included behavior

- One selectable Leo/Volt per side; two AI Goro goalkeepers. No Goro outfielder selection or generic field players.
- Orange/black home kit and white rival kit. Existing Leo/Volt animation frames and poses remain intact; the team material changes their clothing colors at render time.
- Goro has illustrated ready, shuffle, catch, hold and throw key poses, facing into the field. The existing movement and ball simulation remain authoritative.
- The full-size pitch keeps its grass, field lines and camera style, with a modest camera pullback and a new spread-wing bird mowing pattern.
- The V2 duration of **75 seconds** is retained, with a real countdown, no halftime reversal and no overtime. Home always attacks right.
- A curved shooting zone controls both the shoot button's blue glow and legal shots. The AI uses the same geometry; scoring eligibility is captured when the ball is released.
- Saves keep the match clock running. After about 0.75 seconds Goro throws to his one outfielder; control stays with the human field character.
- Automatic kickoff and short possession restarts prevent one-player teams waiting for a nonexistent pass target.
- Larger action buttons, a 200-unit movement circle and a separate 336-unit sprint track. Thumb travel is 160 units; the arc follows thumb direction. The bottom power meter remains visible and drains during ult use.
- The V2 bigger ball (1.65× visual scale), faster kicks (1.55×) and lower, larger shoot-button placement are retained.
- Landscape menus, safe-area layouts, centered selection overlays, correct progress steps and removal of old debug labels/markers.
- The V2 Arial runtime error is removed. Particle velocity axes use the same curve mode. Roster checks validate the four-character setup.

## Art files

`Assets/Resources/Duel/PitchBird.png` is the generated pitch texture. Its prompt requested replacement of only the central animal motif with a faded olive spread-wing eagle, preserving the grass palette and field markings.

`Assets/Resources/Duel/GoroKeeper.png` is the generated illustrated key-pose atlas. Its prompt requested the existing white-furred, green-beaded Goro in an orange shirt, black shorts and orange socks, in eight elevated three-quarter keeper poses. It uses a magenta key removed by `TeamKit.shader`; the key is not meant to be visible in-game. Both images were made with the built-in image generator.

The Goro frames are temporary key poses, not a fully hand-animated sequence. `GoroKeeperVisual.cs` contains the atlas bounds and timing so the final animation can replace them. The kit shader uses color and regional masks rather than separate clothing layers; complex feather/cloth intersections should be checked on device when replacing the art.

## Validation

The source and asset checks are recorded in `VALIDATION.md`. Unity/iOS execution is still required; this workspace does not have the Unity Editor or Xcode. In Unity's Test Runner, run the PlayMode `DuelFlowTests` suite. It covers zone edges, launching from the menu, Goro selection restrictions, four-player roster, keeper throws, continuous time and pause/resume.

For a device check, enter a match from the menu; test both Leo and Volt, both landscape orientations, a save by each keeper, an out-of-bounds restart, scoring inside the blue arc and the final countdown. Check that the joystick and both bottom meters fit inside the screen's safe area.


## V6 quick tuning
- Shot launch velocity increased by 1.3x over V5.
- Goro keeper outlets use the same launch-speed calculation as shots and no longer get slowed by lob receiver-lock steering.
- Strike-zone radius increased to 7.5 and shot aim assist tightened toward the goal.
- Goal trigger width is slightly more forgiving for the arcade demo.
- Away outfielder gets a short protected sprint burst after a keeper outlet and auto-activates a ready ult immediately.


## V7 balance correction
- Keeper outlet no longer uses full shot-speed launch; it has a dedicated fast-but-short capped throw profile so it reaches the attacker without clearing the pitch.
- V6 opponent outlet burst / protection / auto-ult behavior is preserved.
- Scoring ease moved to the midpoint between V5 and V6: 7.0 strike-zone radius, 0.45 aim assist, 0.365 miss margin, and 2.16 goal trigger half-width.
- V6 1.3x shot-speed boost is preserved.
