# Validation — 0.3.3

## 0.3.3 verification

This update adds a persistent, rebindable native health-bar toggle (F8 by
default). It also fixes autosave starvation during repeated quickloads or
restarts of the same mission. Ordinary planning pauses now count toward the
interval; modal menus, loading and long process suspensions do not.

The clean build passes **130 checks with both supported original executables
supplied**, with **zero warnings and errors**. **100 checks** run without game
files. New coverage includes legacy hotkey migration and conflicts, health-bar
settings persistence, native setter signatures and GOG hook argument handling,
and a simulated two-hour session with a reload every two minutes. That session
produces eleven ten-minute autosaves after excluding its loading periods.

Native checks on 2026-10-18 use separately named disposable mission copies:

| Feature | Confirmed behavior |
| --- | --- |
| Steam health bars | Physical F8 shows native bars on visible units and buildings while paused; another F8 restores the normal display |
| GOG health bars | Physical F8 shows native bars while paused; rebinding to F7 in Settings and pressing F7 restores the normal display |
| Reloads | Both editions retain the toggle and accumulated autosave time through a quickload of the same mission |
| Paused autosaves | Both editions produce real native saves at one-minute test intervals while continuously paused; five history slots fill and the oldest is replaced |
| Autosave loading | Both editions successfully load a generated autosave into the disposable mission |
| Settings layout | Reviewed native Steam and GOG captures show the seventh hotkey row and autosave dropdown without overlapping the action buttons |
| Clean installations | Both editions pass startup, physical F11, Settings and OK smoke checks after removing the private diagnostic helper |

Both installations pass installer inspection and contain the clean release
runtime DLL. The diagnostic helper and its frame call are absent from the
source and release DLL. The original Steam EXE remains byte-for-byte unchanged.
After removing six task-owned save/checksum files and two disposable snapshot
scopes, all **72 baseline save/cloud files** match their pre-test SHA-256 hashes.
The original `Settings.xml` also matches exactly, preserving the ten-minute
interval and existing preferences. Installation backup archives are retained.

The one-minute integration interval advances naturally; the scheduler is not
primed. The default remains ten minutes. The native held health-bar key is
preserved by forwarding its requested value when the toggle is off; that
specific held-key interaction was not separately exercised in these checks.
Other zoom levels, native key conflicts and combinations with other mods have
not been exhaustively tested.

The completed 0.3.2 and earlier results below are historical coverage.

## 0.3.2 verification

This update routes mouse clicks and native command-grid keys through the same
recruitment activation hook, so a group action queues once per eligible
production building. It also aligns the four Options actions to the native
Cancel/Apply columns when the QoL tab is actually visible, including after tab
switches and layout changes. A hidden tab no longer loses a one-shot alignment
callback.

The clean build passes **112 checks with both supported original executables
supplied**, with **zero warnings and errors**. **85 checks** run without game
files. The additions include 28 isolated API checks for group activation
(14 per edition, using its actual symbol names) and four checks against the
original EXEs for activation hooks and signatures.

Confirmed native checks:

| Feature | Confirmed behavior |
| --- | --- |
| Steam grid key | Physical Q starts one Ranger in each of two idle production buildings; gold changes from 26,300 to 26,060 |
| Steam icon after key | Clicking the unit icon adds one Ranger to each queue and spends another 240 gold, leaving 25,820 |
| Steam icon from idle | Reloading the baseline and clicking the icon alone starts both idle queues for 240 gold |
| Steam insufficient gold | Clicking with zero gold adds no units to either queue |
| GOG grid key | Physical Q starts one Ranger in each of two idle production buildings; gold changes from 26,300 to 26,060 |
| GOG icon after key | Clicking the unit icon adds one Ranger to each queue and spends another 240 gold, leaving 25,820 |
| GOG icon from idle | Reloading the baseline and clicking the icon alone starts both idle queues for 240 gold |
| GOG insufficient gold | Pressing Q with zero gold adds no units to either queue |
| Options alignment | Native Steam and GOG captures show the four QoL actions in the Cancel/Apply columns |
| Tab switching | Switching Game → TAB QoL in Steam retains the footer-column alignment |
| Clean releases | Steam and GOG both pass physical startup/F11/Settings/OK smoke checks; native captures were reviewed |

Both final installations pass installer inspection and contain a runtime DLL
matching the clean release. The private integration probe and its frame call
are absent from the source and clean release DLL. The original Steam EXE
remains byte-for-byte unchanged.

Ten task-owned disposable save/checksum files were removed. All 56 original
files in the save directory match the pre-test baseline exactly: 55 player
files and `steam_autocloud.vdf`. The original `Settings.xml` SHA-256 also
matches, preserving the existing preferences and custom interval.

Native checks cover the specific selections and layouts described above;
other locked-command layouts and compatibility with other mods have not been
exhaustively tested.

The completed 0.3.1 and earlier results below are historical coverage.

## 0.3.1 verification

Later player reports exposed gaps in the native activation and initially hidden
Options-tab coverage. Version 0.3.2 addresses those paths; the results below
record the earlier test scope.

The finishing update changes window layout and navigation, autosave presets,
icon-row shortcuts and commands for multiple selected production buildings.

The clean 0.3.1 build passes **80 automated checks with both supported original
executables supplied**, with **zero warnings and errors**. Of these checks,
57 do not require game files. The private integration probe and its frame call
have been removed from the source and are absent from the release binary.

Five binding regression checks verify that explicit construction/global keys
can reserve an Auto grid key, explicit construction keys still reject global
conflicts and duplicates within a category, and separate categories can reuse
an explicit key.

The following native Steam checks are confirmed on 2026-10-18:

| Feature | Confirmed behavior |
| --- | --- |
| Clean Steam release | Startup, physical F11, Settings and OK work with the release build; the original Steam EXE remains byte-for-byte unchanged |
| Options layout | QoL controls align within the native Options window; secondary brass buttons use smaller cropped side tips, and the dropdown is compact |
| Autosave presets | Off and 5, 10, 15, …, 60 minute choices are selectable |
| Existing interval | A saved custom 1-minute interval remains visible and unchanged until a preset is selected |
| Campaign layout | The compact campaign panel and checkbox group render centered |
| Child navigation | Physical Settings → Building hotkeys → Next → OK returns to Settings; the next OK returns to the root QoL screen; hidden parents do not show through |
| Slot navigation | Changing slot pages and then pressing OK returns directly to the root QoL screen |
| Modal lifecycle | F12 closes the debug/confirmation window family; returning through two OK actions restores the previously running simulation |
| Multiple production buildings | Physical Q queues one unit in each of two selected buildings, charging the native 120 gold each (240 total) |
| Queue and resource limits | With the first queue prefilled to four by the test fixture, real Q skips it and adds one unit to the other queue, spending the remaining 120 gold; a repeated Q adds no duplicate |
| Worker limit | Exhausting available workers stops further native recruitment |
| Packed icon rows | Defense commands use Q/W/E on the first visible row and A/S/D on the second; native Escape retains Back/Cancel behavior |
| Custom construction key | Tent uses explicit Y while Cottage and Stone House keep the visible-slot W and E keys |
| Player data | After removing eight disposable test saves, all 46 baseline save/checksum files still match exactly; original Settings.xml also matches its pre-test SHA-256, including the legacy 1-minute interval |

The queue-limit fixture inserts the initial four entries without charging
resources. Subsequent shortcut actions run the native queue and resource checks.

The completed 0.3.0 results below are retained as a baseline. They do not
substitute for verification of the changed 0.3.1 behavior.

The clean GOG 0.3.1 release also passes startup, physical F11, Settings and OK
smoke checks. Its log confirms runtime initialization and completion of the
native scene transition. Detailed native feature tests use Steam; GOG coverage
for this update is limited to startup and UI smoke checks.

Native tests exercise the specific icon layouts, custom override and production
limits above. Other locked-command arrangements and custom-key collisions have
not been exhaustively tested in the running game.

## Completed 0.3.0 verification

Tested on Windows on 2026-10-17 with the exact Steam and GOG x64 executables
identified in the README. Results apply to those builds. Steam loads the mod
through its startup config and Harmony while keeping the original EXE
byte-for-byte unchanged. GOG uses the dnlib EXE patch.

### Automated verification — 0.3.0

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

### Native Steam integration — 0.3.0

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
