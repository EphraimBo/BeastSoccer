# Beast Soccer V8 — approved individual art integration

Extract this ZIP into the Unity project root and replace matching files. Start from
`Assets/Scenes/MainMenu.unity`; do not run an old scene-builder command.

## Included

- Orange home and blue away match sprites loaded directly from the supplied individual PNGs.
- Directional run, shot and tackle presentation for Leo and Volt.
- Volt's approved ability frames at activation.
- Goro three-quarter ready, holding and nine-frame throw animations.
- Goro catch, near/far dive and ground recovery animations.
- Faster shot contact and launch speed.
- Keeper hold reduced from 0.75 seconds to 0.42 seconds.
- Keeper throw contact reduced to 0.12 seconds, launch force raised to 10.5–15.5 before pace scaling, with a direct lower arc lasting at most 0.58 seconds and no post-throw hover/bounces.
- Previous generated Goro/pitch assets copied to `Assets/ArtArchive/PreV8_Runtime`.

## Explicitly excluded

- Every supplied folder whose name contained `Don't use`, `dont use`, or the marked Leo home ability typo.
- Goro side idle, side throw and shuffle frames.
- Both old Goro throw subfolders.
- Leo ability frames.
- The duplicate near-empty home recovery image and macOS metadata.

The older runtime Goro atlas remains at its original path for a safe, non-destructive overlay,
but V8 overrides it and the recolour shader at runtime. Unity/iPhone playback is still required
to confirm final frame scale and pivot timing on device.
