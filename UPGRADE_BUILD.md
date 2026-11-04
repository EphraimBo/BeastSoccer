# Gameplay and progression build

Branch: `feature/gameplay-progression-2026-11-04`.
Base: `cleanup/mobile-systems-2026-11-03`, commit `4d2ebb97d641bb1785c246b4df9ea4367b92338f`.
The release branch remains unchanged.

## Run and revert

Open with Unity **6000.5.1f1**. Open `Assets/Scenes/MainMenu.unity` and enter Play.
The two existing build scenes remain MainMenu then Match.

Select `Assets/Resources/UpgradeSettings.asset` in the Inspector. Turn **Enable Upgrades** off to use the cleanup version's menus, AI, keeper logic and pitch presentation. The new saved balance and ownership remain on disk. Individual switches allow menus, rewards, skins, tactical AI and readable keepers to be compared separately. For consistent comparisons change gameplay switches before launching a fresh match. Disabling rewards during a match stops that match's further rewards; enabling rewards starts tracking on the next match.

For an exact source rollback, check out the cleanup branch/commit above. No new system writes to or deletes the old game's data.

## Flow and presentation

Main Menu → Your Beast → Rival + Difficulty → Select Map → Start Match.
Selecting your beast advances to rival selection. Rival confirmation says Select Map.
The store button sits below a compact inline gold `Coins 640` balance on each menu screen. The store has its own back button. Results include Rematch, Store, Change Match and Main Menu. Gameplay and pause have no store access. Gameplay retains separate floating score/time text, with a floating wallet and pause button; no solid top HUD bar.

Returning from the store to Select Map preserves both characters, difficulty and selected pitch. Buying a pitch does not equip it. Other store visits return to Main Menu. Buttons brighten and swell briefly when tapped; the hit area stays fixed. Selected rivals, difficulty and maps retain a gold selection marker.

Unowned map thumbnails are **16% brightness**. Labels remain readable. Store previews show the artwork at full brightness for inspection. Pitches use the latest approved faded artwork as 2D textures on existing surfaces. Existing ball, goals, collisions and shooting-zone geometry are retained.

| Pitch | Completed matches | Price |
| --- | ---: | ---: |
| Original | 0 | Free |
| Lion | 3 | 500 coins |
| Bird | 9 | 500 coins |
| Gorilla | 27 | 500 coins |
| Snake | 81 | 500 coins |

Both gates appear on each unowned map tile. Before the match requirement the tile cannot be selected. After it is met, tapping an unowned map opens its store listing. Ownership survives app restarts. Thresholds are explicit data values, without exponent calculations.

## Rewards and persistence

- Complete a match: 50 coins, plus 25 for a win or 10 for a draw.
- Home goals: 5 each, first four per match.
- Home ult goals: another 5 each, first two per match.
- Successful home tackles: 1 each, first five per match.

Action rewards are banked immediately; the wallet animates toward the saved balance. Quitting keeps already banked action coins, without counting an unfinished game or awarding completion coins. Full-time completion is guarded by a unique match ID and paid once. The results breakdown shows that match's total, including coins already banked during play. Failed completion writes are retried while results remain open.

Each transaction writes a checksummed JSON envelope to the older of two save slots under `Application.persistentDataPath/BeastSoccerProgression/`. A flushed temporary file is moved into that slot while the newest valid slot stays intact. The in-memory balance changes only after the write succeeds. Purchases validate both gates and debit coins/add ownership in one transaction. Repeated purchases are rejected. Load chooses the newest valid slot; if both existing slots are unreadable, it preserves them and reports an error instead of creating a zero balance. A detected write/load failure is shown in the UI.

This is local device storage: closing/relaunching the app retains progress. Uninstalling, clearing app data or losing the device is outside local persistence; cloud/account sync is not included.

## AI and goalkeepers

Easy, Normal, Hard and GGs change decision intervals, lookahead, sampled routes, prediction strength, aim error, tackle timing and ult benefit thresholds. Opponents compare predicted defender clearance along candidate routes, evaluate five goal placements with ball damping/travel estimates and keeper reach, and use forward passing outlets in larger-team play. Outfield movement, stamina and kick constraints remain shared with the player. These are bounded tactical heuristics; the difficulty values still need playtest tuning.

Both keepers have finite standing reach, a reaction delay, one committed lateral dive and recovery. The home keeper has a fixed profile; away keeper profiles follow difficulty. Saves use actual keeper position and swept ball movement, including a check before the goal authority resolves a fast shot. Legacy broad save zones and generic keeper catches are bypassed while this module is enabled. Existing Goro dive/recovery animation plays even when a dive misses. `Show Keeper Debug` displays reach/dive gizmos for selected keepers in the editor.

## Verification

`python -m pip install -r Tools/requirements.txt`

`python Tools/validate_project.py`

The source validator checks C# syntax, asset GUIDs, scene references, callback names, parent links, module bindings, catalog gates and preserved roster/artwork. It does **not** compile Unity assemblies.

PlayMode tests added: `ProgressionTests` (reloads, duplicate payments, action caps, abandoned matches, both purchase gates, failed writes, corrupt-slot recovery and actual disk round trips) and `ShotMathTests` (swept contact, bounded segment distance, goal crossings and damped travel). Run these alongside the existing CleanupFlowTests and DuelFlowTests in Unity Test Runner.

Unity Editor, PlayMode execution, rendered/device checks and a distributable binary were unavailable in the build environment. Before release, verify the complete flow on a phone, close/reopen after earning and buying, compare all four difficulties, shoot across keeper reach at both ends, compare base/new feature switches, and check each texture's field-line alignment against the unchanged geometry. No claims of completed device or Unity compilation testing are made.

Scene UI is serialized and directly editable. `Tools/author_upgrade_scenes.py` documents the one-time authoring operation against the cleanup baseline; it intentionally rejects re-authoring an already upgraded scene.
