# TAB QoL — They Are Billions

[Русская инструкция](README.ru.md)

An unofficial Windows mod with mission save/load slots, campaign research
reset, optional hero mission skipping with full rewards, and adjustable
simulation speed, building hotkeys and an F12 debug cheat panel. It patches
your own installed game locally.

## Supported version

**They Are Billions 1.1.4.10, GOG, x64** only. The original
`TheyAreBillions.exe` must have this SHA-256:

```text
f502e38dd6840a5d52331c1c8d68ab94c79f620e2dd5453e9bf5168d909433d3
```

The installer rejects other versions and modified executables. Steam, x86 and
other builds are currently unsupported, even if their displayed version matches.
Requires Windows and .NET Framework 4.6.2 or newer. The game must already run
normally with its own dependencies installed.

## Install

1. Close the game. Extract `TABQoL-0.2.1.zip` into a separate folder.
2. Open PowerShell in the extracted folder and run:

   ```powershell
   .\TABQoL.Installer.exe inspect 'C:\Games\They Are Billions'
   .\Install.ps1 -GameDirectory 'C:\Games\They Are Billions'
   ```

   You can also invoke `TABQoL.Installer.exe install '<game-directory>'` directly.
   Administrator privileges are needed only if Windows requires them to write
   to your game directory.
3. Launch the game normally. Open **Options > TAB QoL** or press **F10**.

The installer backs up the original EXE and user save files before replacing
the executable. Keep `TABQoL.Installation` in the game directory for uninstall.
To upgrade, uninstall the previous version before installing the new version.

## Controls

| Action | Default |
| --- | --- |
| Quick save | F5 |
| Quick load | F9 |
| Slower | `[` |
| Faster | `]` |
| QoL menu | F10 |
| Debug cheat panel | F12 |

Change keys and choose **0.5×, 1×, 2×, 3×, 4× or 5×** in **Options > TAB QoL**.
Click a key button, then press the replacement key; Escape cancels. Keys must
be unique. Enter, Escape, Windows and modifier keys cannot be assigned. Choose
keys that do not conflict with your native game bindings. The bracket defaults
refer to the physical Windows OEM bracket keys; printed characters vary with
keyboard layout. Settings apply immediately and persist between sessions.

### Mission saves

F5/F9 use the quick slot. **F10 > Save / Load** provides that slot and five
manual slots. Slots are separate for each game and campaign mission. Resume
that mission through the normal game menu before loading one of its mod slots.
Saving captures the native colony and campaign state while briefly pausing the
simulation. Loading restores the entire snapshot, including resources, units
and campaign points.

Saving is available in a loaded, ongoing mission, including while paused.
Wait for loading/transitions to finish. Close other dialogs before using
mission hotkeys. Loading also works through F10 while a finished mission is
still loaded, including from its result screen. The world map cannot use slots.
Snapshots remain separate from the game's ordinary saves. Each slot retains
its current and previous generation; SHA-256 hashes are checked before loading.
Slots from completed/abandoned games remain on disk until you remove them.

### Building hotkeys

Select your command center and open a construction category, then press the
letter displayed on a building button. **T** builds a tent in the Colonists
category. Other defaults follow the command grid. Keys work in the current
category, and preserve native unlock, resource and placement requirements.
Unit orders keep their normal bindings when the construction menu is closed.

Open **F10 > Building hotkeys** or **Options > TAB QoL > Building hotkeys** to
change each building key. Assign A–Z, Backspace to unbind, or Escape to cancel.
Use different keys within a category; different categories may reuse a key.
Building keys cannot overlap the mod's global hotkeys. Shift/Ctrl/Alt/Windows
combinations do not activate construction shortcuts. Bindings persist in XML.

### Debug cheats

**F12** opens/closes the debug panel during an ongoing mission. It provides
immortal player units (including heroes), invulnerable player buildings,
healing of living player units, and filling gold/wood/stone/iron/oil storage
to its native limits. Protection affects player entities only; enemies still
take damage. Both protection switches default to off and reset after loading
or changing missions. They are not stored in settings or snapshots.

**Win current mission...** asks for confirmation inside the game, saves a
separate `before-cheat-win` snapshot, then runs the native victory sequence.
Finish the normal result screen to apply campaign progress and rewards.
To undo before leaving that screen, open **F10 > Save / Load** and load the
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

If installation fails before committing, the original EXE remains unchanged
and its backup is retained. Inspect the error and the retained
`TABQoL.Installation` directory before moving that directory aside and retrying.
Do not overwrite an EXE that has subsequently been updated by the game store.

## Uninstall

Close the game and run:

```powershell
.\Uninstall.ps1 -GameDirectory 'C:\Games\They Are Billions'
# Or: .\TABQoL.Installer.exe uninstall 'C:\Games\They Are Billions'
```

Uninstall verifies hashes and restores the exact original EXE. It removes the
runtime DLL, archives the installation record and preserves mod data/backups.
It refuses to overwrite an executable changed after installation. Mod mission
slots need the mod to load; the game's ordinary saves remain native files.

## Build and validation

On Windows with the .NET SDK 8 or newer:

```powershell
.\Build.ps1 -Package
# Optional: also verify patching against your own supported original EXE:
.\Build.ps1 -Package -OriginalExe 'C:\path\to\original\TheyAreBillions.exe'
```

The release ZIP is written to `artifacts`. NuGet restores Mono.Cecil 0.11.6 and
.NET Framework reference assemblies. No game assemblies are required to compile.
Automated checks cover settings, speed stepping, slot isolation/retention,
checksum rejection and interrupted writes. With `-OriginalExe`, they also patch
a temporary copy, verify all eight hook names and reject double patching.

Local game integration checks use isolated campaign copies. See
[VALIDATION.md](VALIDATION.md) for the tested scope and remaining limits. High
simulation speeds may be limited by hardware. Compatibility with other mods is
not established. The UI added by this version is in English.

## Share on GitHub

Original mod code: [MIT](LICENSE). Bundled libraries:
[third-party notices](THIRD-PARTY-NOTICES.md). Unofficial; not affiliated with the
game's developers or publishers. No game code or assets are distributed.
