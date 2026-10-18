# Installation modes — 0.3.4

Use `TABQoL-0.3.4.zip` and `Install.ps1 -GameDirectory <path>` for either
supported edition. Close the game first. The installer accepts only the exact
original executable hashes listed in the README.

- **Steam:** the executable stays byte-for-byte unchanged. The installer adds
  the CLR `TABQoL.SteamDomainManager` startup entry to
  `TheyAreBillions.exe.config` and copies `TABQoL.Runtime.dll` and `0Harmony.dll`
  beside the game. Harmony installs the hooks in memory. Existing startup and
  runtime XML options are preserved. A config that already selects another
  AppDomainManager, or an unowned DLL at either destination, is rejected.
- **GOG:** the installer patches the supported executable and copies the
  runtime DLL. The `prepare` command is for the GOG executable only.

`TABQoL.Installation/original` contains the exact original executable and, for
Steam, the original startup config if one existed. The manifest records hashes
for all owned files. `TABQoL.Installer.exe inspect <path>` validates an installed
copy even though the Steam executable still has its original hash.

Use `Uninstall.ps1 -GameDirectory <path>` before changing mod versions or
updating/verifying the game in Steam. Uninstall checks the installed files and
backups before restoring the exact original config or GOG executable. It removes
the owned DLLs and archives the installation backup inside the game directory.
If Steam originally had no config, uninstall removes the one created by the mod.
Modified owned files are reported and left in place. Player saves, mod slots,
settings and backup history are preserved.

Keep every DLL from the release ZIP beside the installer when installing.
Steam uses `0Harmony.dll` from Lib.Harmony 2.4.1 for .NET Framework 4.5.2; its
dependencies are supplied by .NET Framework. No separate MonoMod or Cecil DLL
is needed. See `THIRD-PARTY-NOTICES.md` for bundled library licenses.
