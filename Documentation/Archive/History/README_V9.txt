BEAST SOCCER V9 - SIZE, MENU, ANIMATION AND KEEPER HOLD

INSTALL
Exit Play Mode and close Unity. Extract this ZIP into the Unity project root
(the folder containing Assets, Packages and ProjectSettings), merging folders
and replacing matching files. Reopen Unity, let it import, then open
Assets/Scenes/MainMenu.unity and press Play. Do not rebuild the scenes.

This is a cumulative overlay containing the V8 approved art and prior game patch,
plus the V9 corrections. It is not a standalone Unity project or an iPhone build.

CHANGES FROM V8
- All match characters are 1.2x their actual V8 rendered size: Leo/Volt renderer
  scale 1 -> 1.2; Goro renderer scale 1.2 -> 1.44. Reapplying a character does not
  stack the multiplier. Gameplay colliders and ball size retain their V8 values.
- Both Pitch_Artwork planes have permanent local Y rotation 180 in Match.unity.
- Leo/Volt menu portraits fit within smaller assigned card/selection bounds.
  Removed portrait FitInParent behaviour that expanded them to the whole page.
  Character click regions and Continue/Start controls are smaller too.
- Accumulated animation frame time replaces global time multiplied by changing
  movement speed. Direction changes are debounced, movement idle threshold has
  hysteresis, and tackles stop pinning the final pose throughout recovery.
- Approved animations are loaded on character setup; older Animator, Goro/Volt
  overrides and team recolouring components are disabled for those characters.
- Sprite overlap sorting uses the foot pivot; ult aura updates after new art.
- Keeper possession hold is 3.25 seconds (2.5 seconds beyond the effective V8
  save-zone hold of 0.75). The fast throw release and low flight arc are retained.

ART
179 approved individual PNGs are included without pixel changes from V8.
All previously excluded folders/actions remain excluded. The PreV8_Runtime art
backup is retained. Earlier V8 and upgrade notes are historical; this file takes
precedence for V9 behaviour and installation.

VALIDATION
Parsed all included C# source files with a C# syntax parser; checked the exact
scene rotation changes, unchanged PNG hashes, portrait aspect-fitting bounds,
complete cumulative ZIP contents and ZIP integrity.
Updated the existing Unity PlayMode test for menu bounds, repeated size setup,
approved art ownership, permanent pitch rotation and delayed keeper release.
Unity Editor and iOS are unavailable here: C# semantic compilation and PlayMode
tests have NOT been run. Close-contact smoothness needs a device/Unity playtest;
the specific timing and competing-animation issues above were corrected.
