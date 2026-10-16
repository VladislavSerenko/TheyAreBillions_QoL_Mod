# Validation — 0.2.1

Tested on Windows, 2026-10-16, against the exact GOG x64 1.1.4.10 executable
identified in the README. These results describe this build, not other game
versions.

## Automated checks

30 checks pass with the optional supported original EXE supplied. The first
27 run without the game. Coverage includes:

- Supported speed steps, half speed, limits and invalid values.
- Refund/reward arithmetic and duplicate award guards in the policy layer.
- Settings persistence, atomic replacement, invalid/conflicting hotkeys.
- Independent mission slots and native save/checksum pairs.
- Preservation of the native serialization filename during staged loading.
- Current/previous generation retention, failed writes and checksum rejection.
- Previous-version settings migration, building overrides and cheat key conflicts.
- Patching a temporary original EXE, validating eight runtime hook names, preserving
  the original and rejecting a second patch.

Policy arithmetic checks alone do not verify native campaign behavior. The
integration checks below exercise the game's own implementations.

## Local game integration

Tests used separately named campaign copies with separate game IDs. Original
player save/checksum files were compared with the backup taken before each
update and remained byte-for-byte unchanged (22 files for the 0.2.0 and 0.2.1 updates).

| Feature | Verified behavior |
| --- | --- |
| Runtime/install | Patched game launches; runtime initializes; uninstall restores the original hash |
| Mission quick slot | Save, change state, load; colony gold, level script data and campaign points return; game name/ID and pause remain intact |
| Manual slot | Native UI Save/Load buttons operate a separate manual slot |
| Hotkeys | Physical F5/F9 and bracket keys operate in the active game window |
| Rebinding | Change Quick Save from F5 to F6 through the settings UI, save with F6, restore F5 |
| Simulation speed | Native setters accept all six values; UI selection persists; changing speed while paused preserves pause |
| Research reset | Refund equals all purchased costs, associated empire bonuses are removed, repeated reset grants no extra points |
| Hero skip | The patched native Start callback completes an available hero mission, grants fixed plus maximum collectible rewards, and prevents repeat rewards |
| UI | Native Options tab, settings, mission slots and world controls render inside the game |
| Campaign UI (0.2.1) | Physical click on the QoL gear opens the native brass campaign window; checkbox persists off/on; research confirmation and settings open; Escape closes; the technology tab remains accessible |
| Building shortcuts | Physical T selects native tent placement; rebind to U and repeat; unaffordable buildings cannot enter placement |
| Building settings | In-game binding button accepts V and updates the displayed key and XML; key hints render on native construction buttons |
| Debug panel | Physical F12 opens/closes the panel; protection toggles and five debug actions render |
| Damage protection | Lethal damage and predicted damage rejected for player units; enemies take damage; disabling restores damage; buildings have a separate toggle |
| Healing/resources | Living player units reach full native life; resource setters fill native storage limits (2800 gold and 70 of each material in the test colony) |
| Cheat lifecycle | Both protection switches reset after loading a snapshot; native save/load still restores script data, campaign points, identity and pause |
| Debug victory | In-game confirmation creates before-cheat-win, opens Colony Completed, and applies native rewards/unlocks after closing the result screen |
| Victory rollback | F10 opens slots over the result screen; the before-cheat-win Load button restores Playing, original research/empire points, identity and pause |

The hero test used a prepared, uncompleted hero mission in an isolated campaign
copy. It did not modify an original campaign. Diagnostic command handlers used
for these local checks are excluded from the release and repository.

## Limits

- Only the exact supported EXE is accepted. No Steam/x86 compatibility claim.
- Campaign colony save/load was exercised directly. Hero/swarm and survival
  save/load share the adapter but have not all been independently play-tested.
- Saving requires an ongoing mission. Loading also works from a finished
  mission's result screen while that mission remains loaded. Resume the
  matching game/mission normally before using a mod slot after restarting.
- UI checks used 1920×1080. Other resolutions and unusual display scaling have
  not been exhaustively tested.
- No extended full-campaign playthrough, power-loss test, or compatibility test
  with other mods was performed. Atomic pointers and checksums protect the mod
  slot writer; native campaign saving follows the game's own format.
- 5× is a requested simulation multiplier; hardware may limit actual progress.
- Combat protection covers native damage and predicted-damage paths. Scripted
  entity removal, map hazards that directly edit health, and every hero/swarm
  script have not been exhaustively tested. Survival victory shares the native
  adapter but was not independently exercised in this update.
- Local integration tooling and proprietary game files are intentionally absent.
  Reproduce automated checks with `Build.ps1`; game integration requires a local
  copy of the supported game and disposable test campaigns.
