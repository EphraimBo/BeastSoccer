BEAST SOCCER V10 - KEEPER OUTLETS, PLAYER SIZE, OPPONENT ULTS

INSTALL
Close Unity. Extract this cumulative ZIP into the existing Unity project root,
merge folders and replace matching files. Reopen Assets/Scenes/MainMenu.unity.
No scene rebuild or Git push is required. Includes the V9 patch and 179 approved
art frames. This is an overlay, not a standalone project or iPhone binary.

CHANGES
- Keeper throws now steer toward the receiver throughout the short flight,
  including the very first physics step. The landing point stays at least .75
  units inside the pitch edges. The ball stops at landing rather than continuing
  past the receiver into touch. If a receiver cannot collect, it stays loose on
  the turf. Deflections and ownership changes cancel the receiver tracking.
- The 3.25-second keeper hold, .12-second release and short low arc are retained.
- All character artwork is another 7% larger: Leo/Volt scale 1.2 -> 1.284 and
  Goro 1.44 -> 1.5408 in the supplied scene. Reapplying does not compound scale.
- Away outfield AI checks ready ults every .5 seconds in live attacks/defence or
  nearby loose-ball situations. It no longer needs a keeper outlet or final
  stretch to spend a full meter. No keeper ults or extra/free charge are added.
- Ult locomotion multiplier 1.25 -> 1.45 for both sides (16% above previous ult
  speed). The movement cap and sprint distance calculation use the same value.
- Shot launch tuning, menu layout, pitch rotation and approved PNGs are unchanged.

VALIDATION
Local C# syntax parsing, short-flight trajectory checks for moving edge targets,
unchanged asset hashes and ZIP integrity checked. The existing Unity PlayMode
suite was updated for new scales and keeper delivery to moving receivers near
both touchlines, including unchanged shot launch behavior.
Unity Editor is unavailable here; semantic compilation, PlayMode tests and
iPhone playback have not been run. README_V10 takes precedence over older notes.
