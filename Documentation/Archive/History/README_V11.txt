BEAST SOCCER V11 - MOBILE FIT AND THREE-QUARTER ANIMATIONS

INSTALL AND DEPLOY
1. Close Unity. Extract this cumulative patch into your existing Unity project
   root, merge Assets/ProjectSettings and replace matching files.
2. Reopen Unity and allow compilation/import to finish. Do not run the old
   scene-building or bake commands.
3. Open Assets/Scenes/MainMenu.unity. The menu should say DEMO V11 near the bottom.
4. In the active mobile Build Profile's scene list, use this project's
   Assets/Scenes/MainMenu.unity first, then Assets/Scenes/Match.unity. If the
   profile overrides the global scene list, check that list too.
5. Build and install the updated app. Copying a patch into Unity does not update
   an app already installed on the phone. Check for DEMO V11 on the phone.

CHANGES
- Three-quarter running now covers 14-76 degrees from horizontal (was 24-66).
  Direction confirmation is 25ms rather than 75ms. Run-cycle phase is preserved
  when turning, avoiding repeated restarts at the first frame.
- Duel camera fits the entire pitch, both goals and a character-height margin
  against BOTH screen dimensions. It stays centered during play and accounts
  for phone safe areas, with room above/below for HUD elements. Camera shake
  remains. Wide displays no longer force a width-only zoom that crops vertically.
- HUD uses Expand canvas scaling plus a fitted safe-area design space. Controls,
  sprint ring, score, timer and power meter remain within the usable display.
- The actual pause button is named II in the supplied scene; it is now correctly
  recognized and placed inside the safe area.
- Pause/halftime/fulltime panels fit inside the safe area above the HUD, with
  input blockers preventing taps through the panel to gameplay controls.
- Main menu uses Expand canvas scaling and retains its fitted artwork layout.
  Both scene scaler settings are saved, as well as applied at runtime.

This is a cumulative project overlay with all V10 changes and approved PNGs.
No Git push. Player scale, kits, throws, shot tuning and ult speed are retained.
Full-pitch framing means players can occupy fewer screen pixels than with the
old cropped camera, although their world/artwork scale has not been reduced.

VALIDATION
See VALIDATION_V11.txt for checks actually run. Unity Editor and an iPhone are
unavailable in this environment. Included Unity tests require running in your
project; they have not been run here. Earlier README files are historical;
this file takes precedence for V11 installation and presentation behavior.
