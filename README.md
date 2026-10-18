# TAB QoL — They Are Billions

An unofficial Windows mod with mission save/load slots, campaign research
reset, optional hero mission skipping with full rewards, and adjustable
simulation speed, building hotkeys and an F12 debug cheat panel. It installs
into your own local copy of the game.

**Steam and GOG use the same installer.** No branch switching or compilation is
needed. Follow the [installation steps](#install-steam-and-gog) below.

## Supported versions

Version 0.3.4 supports **Steam and GOG x64** for the exact builds below.
See [tested behavior and limits](VALIDATION.md). The installer detects the
edition from the original `TheyAreBillions.exe` SHA-256:

```text
GOG 1.1.4.10:
f502e38dd6840a5d52331c1c8d68ab94c79f620e2dd5453e9bf5168d909433d3
Steam (EXE file version 1.0.14.29):
695cef4ad913f9ffd1f9f0508d26862546b130922db53dce716051fe9b0a9d2d
```

The installer rejects other versions, x86 and modified executables, even if
their displayed version matches. Steam updates may require a new adapter.
Requires Windows and .NET Framework 4.6.2 or newer. The game must already run
normally with its own dependencies installed.

## Install (Steam and GOG)

Download the ready-to-install **`TABQoL-0.3.4.zip`** from
[Releases](https://github.com/VladislavSerenko/TheyAreBillions_QoL_Mod/releases),
under **Assets**. The **Source code** archives and **Code > Download ZIP** contain
project source files without the ready-to-run installer.

If no release is listed yet, download the current build from
[Actions > Build and test](https://github.com/VladislavSerenko/TheyAreBillions_QoL_Mod/actions/workflows/build.yml?query=branch%3Amain).
Sign in to GitHub, open the latest successful run on **main**, scroll to
**Artifacts**, and download **TABQoL-0.3.4**. This download may contain another
`TABQoL-0.3.4.zip`; extract that ZIP too.

1. **Close the game.** Extract all files into a separate folder. You should see
   `TABQoL.Installer.exe` alongside the included DLL files. Keep them together.
2. **Find your game folder** containing `TheyAreBillions.exe`:

   - **Steam:** Library > right-click They Are Billions > Manage > Browse local files.
   - **GOG Galaxy:** select the game > menu beside Play > Manage installation > Show folder.

3. **Open PowerShell in the extracted mod folder.** Open that folder in File
   Explorer, type `powershell` into the address bar, and press Enter.
4. **Run the installer.** Replace `GAME_FOLDER` with the full game folder path
   from step 2, keeping the quotation marks:

   ```powershell
   .\TABQoL.Installer.exe install "GAME_FOLDER"
   ```

   For example, if your game is installed in `C:\Games\They Are Billions`:

   ```powershell
   .\TABQoL.Installer.exe install "C:\Games\They Are Billions"
   ```

   Use your own path for either edition. The installer detects Steam or GOG
   automatically. Wait for it to report success before launching the game.
5. **Launch the game normally**, through Steam for the Steam edition. Press
   **F11** or open **Options > TAB QoL**.

If Windows reports **Access denied**, open PowerShell as administrator, return
to the extracted mod folder, and retry. Administrator privileges are needed
only when Windows blocks writing to your game folder.

The installer backs up the original game files and user saves. Keep the
`TABQoL.Installation` folder inside your game directory for uninstall.
Steam keeps its original EXE unchanged; GOG installation patches the supported
EXE. See [installation modes](INSTALLATION.md) for technical details and
compatibility restrictions.

For removal and upgrade steps, see [Uninstall or update](#uninstall-or-update).

## Controls

| Action | Default |
| --- | --- |
| Quick save | F5 |
| Quick load | F9 |
| Toggle health bars | F8 |
| Slower | `[` |
| Faster | `]` |
| QoL menu | F11 |
| Debug cheat panel | F12 |

Change keys and choose **0.5×, 1×, 2×, 3×, 4× or 5×** in **Options > TAB QoL**.
Click a key button, then press the replacement key; Escape cancels. Keys must
be unique. Enter, Escape, Windows and modifier keys cannot be assigned. Choose
keys that do not conflict with your native game bindings. The bracket defaults
refer to the physical Windows OEM bracket keys; printed characters vary with
keyboard layout. Settings apply immediately and persist between sessions.
The previous default F10 is migrated to F11 to avoid the game's image capture
action. Custom menu bindings are preserved. The mod consumes its assigned keys
before native special actions, including the game's F11 developer action.

QoL windows use compact centered layouts with aligned controls and smaller
brass action buttons. **OK** returns to the previous QoL screen. Changing
Save/Load pages stays within that screen.
In native Options, the four QoL actions align with the Cancel/Apply columns
when the tab becomes visible and after switching tabs.

### Health bars

Press **F8** once to keep native health bars visible and again to restore the
normal display. The toggle covers visible units, enemies and buildings, and
works while paused. Rebind **Toggle health bars** in **Options > TAB QoL**.
Its state and key are saved between sessions; the game's held health-bar key
still works when the toggle is off. Existing custom bindings are preserved;
if F8 is already assigned, upgrading chooses an available key for the toggle.

### Mission saves

F5/F9 use the quick slot. **F11 > Save / Load** provides that slot and **ten
manual slots** across two pages, plus a separate **Autosaves** page with five
rotating slots. Save and Load buttons are grouped beside each row.
Slots are separate for each edition, game and campaign mission. Resume
that mission through the normal game menu before loading one of its mod slots.
Saving captures the native colony and campaign state while briefly pausing the
simulation. Loading restores the entire snapshot, including resources, units
and campaign points.

Saving is available in a loaded, ongoing mission, including while paused.
Wait for loading/transitions to finish. Close other dialogs before using
mission hotkeys. Loading also works through F11 while a finished mission is
still loaded, including from its result screen. The world map cannot use slots.
Snapshots remain separate from the game's ordinary saves. Each slot retains
its current and previous generation; SHA-256 hashes are checked before loading.
Slots from completed/abandoned games remain on disk until you remove them.

Autosave defaults to **every 10 real minutes in the loaded mission, including
ordinary pauses**. In
**Options > TAB QoL**, choose **Off** or **5, 10, 15, …, 60 minutes**.
A custom interval saved by an older version stays visible and unchanged until
you select a preset.
Modal menus, loading and long process suspensions do not count; game speed
does not change the interval. Five slots retain the last five autosaves
(about 50 minutes at the default interval). The oldest is replaced next.
Quick/manual saves are independent. Quickloads and restarts of the same
mission retain the countdown, so repeated attempts cannot postpone autosaves
indefinitely. A different mission, interval change or game restart starts a
fresh countdown; missed saves during a long suspension are not queued.

### Building hotkeys

Select a player building and use **Q W E R T** for its top command row,
**A S D F G** for the middle row, and **Z X C V B** for the bottom row.
Keys follow the actual icon positions, including the command-center category
icons. This covers individual buildings, barracks recruitment and other building
commands. Campaign unlocks can change which icons occupy each slot.
Visible hints follow the current command grid. Native unlock, resource, queue
and placement requirements still apply. Escape retains native Back/Cancel
behavior. Unit selections keep their native keys.

With multiple production buildings selected, clicking a unit icon or pressing
its grid key queues one unit in each eligible building. Mouse and keyboard
use the same activation path, so each action queues once per building.
Each building uses the native resource,
unlock and queue checks, so available resources can limit how many accept it.

Open **F11 > Building hotkeys** or **Options > TAB QoL > Building hotkeys** to
change each building key. Assign A–Z, Backspace to unbind, or Escape to cancel.
Use different keys within a category; different categories may reuse a key.
Building keys cannot overlap the mod's global hotkeys. Shift/Ctrl/Alt/Windows
combinations do not activate construction shortcuts. Bindings persist in XML.
Unconfigured buildings show **Auto** and follow their visible icon slot.
Existing custom construction keys are retained. A visible custom binding takes
priority over an automatic shortcut using the same key; that automatic shortcut
is suppressed to avoid triggering the wrong command.

### Debug cheats

**F12** opens/closes the debug panel during an ongoing mission. It provides
immortal player units (including heroes), invulnerable player buildings,
healing of living player units, and filling gold/wood/stone/iron/oil storage
to its native limits. Protection affects player entities only; enemies still
take damage. Both protection switches default to off and reset after loading
or changing missions. They are not stored in settings or snapshots.

**Fill resources** fills native storage. **Win mission...** asks for confirmation
inside the game, saves a
separate `before-cheat-win` snapshot, then runs the native victory sequence.
Finish the normal result screen to apply campaign progress and rewards.
To undo before leaving that screen, open **F11 > Save / Load** and load the
`before-cheat-win` row. Saving a finished mission is unavailable.
The debug win does not grant uncollected hero loot; the campaign's optional
hero skip checkbox still provides maximum rewards. Resource, health and
victory changes can persist in native saves. Mission snapshots can revert
those changes while the matching mission is loaded.

### Reset technologies

On the campaign world map, click the **QoL gear** beside the upper tabs,
choose **Reset technologies**, then confirm **Reset**. The compact campaign
window uses the game's own brass frame, checkboxes and metal buttons.
All purchased technologies are refunded through the game's own research logic.
Starting technologies remain unlocked. Empire points granted by removed
technologies are also removed; buying them again grants the points again.
Completed missions and their rewards remain. Reset is unavailable during a
mission or a replay. A campaign backup is created before applying the change.

### Skip hero missions

Open the **QoL gear** on the campaign world map and enable
**Skip hero missions (all rewards)**. When you
accept **Start** for an available, uncompleted hero mission, the mod completes
it immediately through the native campaign completion function. It grants the
fixed reward, every research/empire collectible, and normal completion unlocks.
The mission uses the campaign's selected difficulty. Already completed missions
and replays do not grant duplicate rewards. Colony and swarm missions continue
to start normally. This checkbox defaults to off and is saved in settings.

## Data and recovery

Under your Windows Documents directory:

```text
My Games\They Are Billions\TABQoL\
  Settings.xml       Settings and key bindings (.bak is the previous copy)
  Snapshots\         Mission slots, grouped by game/mission identity
  Backups\           Installation and campaign-change backups
  TABQoL.log         Mod messages and errors
```

Backups use UTC timestamps. Installation backups contain the original top-level
user files and a `Saves` subdirectory. Campaign-change backups contain copies of
native save/checksum pairs directly in the backup directory. To restore one,
close the game and copy the matching `.zxsav`/`.zxcheck` and, where present,
`.zxcampaign`/`.zxcheck` pair back to the native `Saves` directory, preserving
the exact filenames. Copy your current saves elsewhere first. Do not rename
native saves: their serialization depends on the filename.

If installation fails, inspect the error and any retained
`TABQoL.Installation` directory before retrying. Do not overwrite files that
have subsequently been changed by the game store or another mod.

## Uninstall or update

Close the game. Open PowerShell in the extracted mod folder as above and run
the command below, replacing `GAME_FOLDER` with your game folder path:

```powershell
.\TABQoL.Installer.exe uninstall "GAME_FOLDER"
```

Uninstall the previous mod version before installing a newer one or
updating/verifying the game in Steam.

Uninstall verifies owned-file and backup hashes before making changes. For
Steam it restores the exact original config, or removes the config if none
existed, and removes the owned runtime/Harmony DLLs; the EXE stays unchanged.
For GOG it restores the exact original EXE and removes the runtime DLL. It
archives the installation record and preserves saves, mod data and backups.
Changed owned files are reported and left in place. Mod mission slots need
the mod to load; the game's ordinary saves remain native files.

## Build and validation

On Windows with the .NET SDK 8 or newer:

```powershell
.\Build.ps1 -Package
# Optional: also verify installation against your supported original EXE:
.\Build.ps1 -Package -OriginalExe 'C:\path\to\original\TheyAreBillions.exe'
```

The release ZIP is written to `artifacts`. NuGet restores dnlib 4.5.0,
Lib.Harmony 2.4.1 and .NET Framework reference assemblies. No game assemblies
are required to compile. The clean **0.3.3** build passes **130 checks with both
supported originals supplied**; **100 checks** do not require game files.
Native verification is tracked in [VALIDATION.md](VALIDATION.md). The checks cover settings,
autosave timing, command-grid mapping, slot isolation/retention, checksum
rejection, interrupted writes and startup config preservation. With
`-OriginalExe`, they also verify GOG patch hooks/metadata and Steam
install/inspect/uninstall, ownership checks and exact config restoration in
temporary directories. Steam EXE rewriting is rejected. Supply multiple paths
to test both editions:

```powershell
.\Build.ps1 -OriginalExe @('C:\GOG-original\TheyAreBillions.exe', 'C:\Steam-original\TheyAreBillions.exe')
```

Local game integration checks use isolated campaign copies. See
[VALIDATION.md](VALIDATION.md) for the tested scope and remaining limits. High
simulation speeds may be limited by hardware. Compatibility with other mods is
not established. The UI added by this version is in English.

## Share on GitHub

Steam and GOG share this repository and release ZIP. Attach the ZIP from
`artifacts` to a GitHub Release. Keep installed game files, saves, logs and
personal backups out of Git; build outputs are ignored. GitHub Actions builds
and runs the checks that do not require game files.

Original mod code: [MIT](LICENSE). Bundled libraries:
[third-party notices](THIRD-PARTY-NOTICES.md). Unofficial; not affiliated with the
game's developers or publishers. No game code or assets are distributed.
