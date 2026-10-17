# Validation — 0.3.0

Tested on Windows on 2026-10-17 with the exact Steam and GOG x64 executables
identified in the README. Results apply to those builds. Steam loads the mod
through its startup config and Harmony while keeping the original EXE
byte-for-byte unchanged. GOG uses the dnlib EXE patch.

## Automated verification

**75 checks pass with both supported original executables supplied. 52 run
without game files.** Coverage includes:

- Speed steps and limits, research-refund/reward policy and duplicate guards.
- Settings persistence, invalid/conflicting bindings, F10-to-F11 migration and
  preservation of custom menu keys.
- Autosave active-time accounting, pauses, suspension, timer resets and disabling.
- Five independent autosave slots, oldest-slot replacement, manual isolation,
  current/previous generations, checksums and interrupted native writes.
- QWERT / ASDFG / ZXCVB command-grid mapping.
- Startup XML preservation, including UTF-8 text, existing CLR options and
  namespaced binding redirects; rejection of conflicting managers and invalid
  or external runtime sections.
- Exact-build detection; GOG patch hooks, preservation of original metadata
  token identities and rejection of double patching.
- Rejection of Steam EXE rewriting; Steam install/inspect/uninstall in temporary
  directories, with and without an existing startup config.
- Steam ownership hashes, rejection of changed installed files or backups,
  protection of unowned DLLs, exact original config restoration and backup
  archival inside the supplied test directory.

Reproduce with `Build.ps1`; supply both originals through `-OriginalExe` to run
the complete suite. These tests do not require shipping game assemblies.

## Native Steam integration

Tests use separately named disposable campaign copies with separate game IDs.
The diagnostic command handler used for local checks is excluded from the
release and repository. Physical-key checks target the active game window;
UI captures come from the game's own renderer at 1920×1080.

| Feature | Verified behavior |
| --- | --- |
| Startup | Native game reaches the main menu; the startup loader attaches and runtime hooks initialize; original Steam EXE hash remains exact |
| Options | TAB QoL tab renders; all four bottom actions use native brass buttons |
| Save/load UI | Grouped Save/Load controls render with ten manual slots across two pages and a separate five-slot autosave page |
| Manual slot 10 | Native save/change/load restores colony gold, script data, campaign data, game name/ID and pause state |
| Autosave history | Five real native autosaves fill the history; a sixth replaces the oldest; loading a retained autosave succeeds |
| Physical shortcuts | F11 opens the mod menu without the native image-capture window; F5 saves and F9 loads |
| Construction | Selecting the command center, Q then Q enters native tent placement; two Escape presses retain native cancel/back behavior |
| Contextual commands | Command-center categories use consecutive keys; physical Q/W recruit the first two barracks units with native resource costs |
| Simulation speed | Native setters accept all six supported speeds |
| Campaign controls | Physical click opens the moved, caption-free gear and native campaign panel; technology tab remains accessible |
| Research reset | A purchased technology refunds 100 points; native research/empire balances match the expected totals; repeating reset grants nothing extra |
| Hero skip | Patched native Start callback completes an available hero mission with maximum research/empire rewards and prevents duplicate rewards |
| Debug actions | F12 toggles the panel; lethal player damage/prediction are blocked, enemies take damage, disabling restores damage, buildings have separate protection, and healing uses native health setters |
| Victory and rollback | Debug win creates before-cheat-win and opens the native result; loading restores Playing and clears protection; completing the result applies native campaign progress |
| Release installation | Clean package launches and opens with F11; real Steam uninstall restores the exact original config before reinstallation |

Autosave integration primes the scheduler privately before allowing the normal
runtime save path to run. It verifies actual native saves and rotation; an
uninterrupted ten-minute wall-clock wait was not performed. Active-time rules
are covered by the automated scheduler checks.

All 32 original player save/checksum files match the fresh pre-test baseline
byte for byte. Disposable test saves and their mod snapshot scopes were removed.

## GOG 0.3.0 startup check

The clean 0.3.0 package upgrades the previous local GOG installation, launches
successfully with the dnlib-patched EXE, initializes the runtime, and opens the
QoL panel with physical F11. Detailed feature retesting in this update uses
Steam; previous GOG feature coverage is recorded below.

## Previous GOG coverage

The following behavior was exercised in the GOG 0.2.1 release on 2026-10-16.
It is historical coverage and does not substitute for retesting a changed
adapter or hook in 0.3.0:

- Mission quick/manual save-load restored colony/script/campaign state,
  identity and pause; physical F5/F9 and speed brackets worked.
- Native speed setters accepted all six values; UI selection persisted and
  speed changes preserved pause.
- Research reset refunded purchased costs and removed associated empire
  bonuses; a repeated reset granted no additional points.
- The native hero Start callback granted fixed plus maximum collectible
  rewards and prevented repeat rewards in an isolated campaign.
- Campaign gear, native campaign panel, checkbox persistence and confirmation
  dialogs rendered and responded to physical input.
- Building shortcuts, custom rebinding, native hints and affordability checks
  operated in the game.
- Physical F12 opened the debug panel. Unit/building protection, enemy damage,
  healing and resource filling used native game state. Protection reset after
  loading a snapshot.
- Confirmed debug victory created `before-cheat-win`, opened the native result
  screen and applied rewards/unlocks; loading the snapshot rolled it back while
  the mission remained loaded.
- Uninstall restored the exact original EXE. Original player saves remained
  byte-for-byte unchanged during those tests.

## Limits

- Only the exact supported x64 builds are accepted. Other Steam/GOG updates
  and x86 builds require separate compatibility work.
- Colony/campaign mission save-load has direct native coverage. Hero, swarm
  and survival saves share the adapter but have not all been independently
  play-tested. Resume the matching mission normally before loading a mod slot
  after restarting the game.
- UI checks use 1920×1080. Other resolutions and display scaling have not been
  exhaustively tested.
- No complete campaign playthrough, power-loss test or compatibility test with
  other mods has been performed. Atomic pointers/checksums protect mod slots;
  native campaign saving follows the game's own format.
- 5× requests a simulation multiplier; hardware may limit actual progress.
- Damage protection covers native damage and predicted-damage paths. Direct
  scripted removal/health edits, every hero/swarm script and survival victory
  have not been exhaustively exercised.
- Local diagnostics, proprietary game files and player saves are not included
  in the repository or release. Native integration requires a supported local
  game and disposable test campaigns.
