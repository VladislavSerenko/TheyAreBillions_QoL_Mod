using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

[assembly: InternalsVisibleTo("TABQoL.Tests")]

namespace TABQoL.Installer
{
    public sealed class Manifest
    {
        public string Mode { get; set; }
        public string OriginalHash { get; set; }
        public string PatchedHash { get; set; }
        public string RuntimeHash { get; set; }
        public string HarmonyHash { get; set; }
        public bool OriginalConfigExists { get; set; }
        public string OriginalConfigHash { get; set; }
        public string PatchedConfigHash { get; set; }
        public DateTime InstalledUtc { get; set; }
    }

    internal static class Program
    {
        private const string Executable = "TheyAreBillions.exe";
        private const string Runtime = "TABQoL.Runtime.dll";
        private const string Harmony = "0Harmony.dll";
        private const string Config = Executable + ".config";
        private const string StartupMode = "steam-startup-config";
        private const string ExeMode = "exe-patch";

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("TAB QoL: inspect|install|uninstall <game-directory>\n         prepare <GOG-game-directory> <output-exe>");
                    return 2;
                }
                string directory = Path.GetFullPath(args[1]);
                string exe = Path.Combine(directory, Executable);
                if (!File.Exists(exe)) throw new FileNotFoundException(Executable, exe);
                switch (args[0].ToLowerInvariant())
                {
                    case "inspect":
                        Inspect(directory);
                        return 0;
                    case "prepare":
                        if (args.Length != 3) throw new ArgumentException("prepare needs an output EXE path.");
                        if (string.Equals(exe, Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use install to modify the game.");
                        if (File.Exists(args[2])) throw new IOException("Output already exists.");
                        Patcher.Prepare(exe, args[2]);
                        Console.WriteLine("Prepared and verified: " + Path.GetFullPath(args[2]));
                        return 0;
                    case "install": Install(directory, BackupSaves, NotRunning); return 0;
                    case "uninstall": Uninstall(directory, NotRunning); return 0;
                    default: throw new ArgumentException("Unknown command: " + args[0]);
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }

        private static string Installation(string directory) { return Path.Combine(directory, "TABQoL.Installation"); }
        private static string ManifestPath(string directory) { return Path.Combine(Installation(directory), "manifest.xml"); }

        internal static Manifest ReadManifest(string directory)
        {
            if (!File.Exists(ManifestPath(directory))) return null;
            using (var stream = File.OpenRead(ManifestPath(directory))) return (Manifest)new XmlSerializer(typeof(Manifest)).Deserialize(stream);
        }

        private static void NotRunning()
        {
            if (Process.GetProcessesByName("TheyAreBillions").Any() || Process.GetProcessesByName("TheyAreBillions_x86").Any())
                throw new InvalidOperationException("Close the game before installing or uninstalling TAB QoL.");
        }

        internal static void Inspect(string directory)
        {
            string inspectedHash = SnapshotStore.Hash(Path.Combine(directory, Executable));
            Console.WriteLine("EXE SHA-256: " + inspectedHash);
            Console.WriteLine("Supported: " + Compatibility.GameVersion + "; " + Compatibility.SteamVersion);
            var status = ReadManifest(directory);
            if (status != null)
            {
                ValidateInstalled(directory, status);
                Console.WriteLine("TAB QoL is installed (" + (status.Mode == StartupMode ? "Steam startup config; original EXE unchanged" : "EXE patch") + ").");
                return;
            }
            if (!Compatibility.Supported(inspectedHash)) throw new InvalidDataException("This executable is not supported.");
            Console.WriteLine("Compatible original EXE: " + Compatibility.VersionFor(inspectedHash));
        }

        // Environment guards/backups are supplied by the CLI. Tests exercise the
        // same file transactions on isolated copies without touching player data.
        internal static void Install(string directory, Action backupSaves, Action verifyNotRunning)
        {
            verifyNotRunning();
            string exe = Path.Combine(directory, Executable), root = Installation(directory);
            var existing = ReadManifest(directory);
            if (existing != null)
            {
                ValidateInstalled(directory, existing);
                Console.WriteLine("TAB QoL is already installed. Run uninstall before installing another version.");
                return;
            }
            string originalHash = SnapshotStore.Hash(exe);
            if (!Compatibility.Supported(originalHash)) throw new InvalidDataException("Unsupported/modified game EXE; no files changed.");
            if (originalHash == Compatibility.SteamSha256) { InstallSteam(directory, backupSaves); return; }
            string runtimeSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Runtime), runtimeTarget = Path.Combine(directory, Runtime);
            if (!File.Exists(runtimeSource)) throw new FileNotFoundException("Keep the installer and runtime DLL together.");
            if (File.Exists(runtimeTarget)) throw new IOException("An unowned runtime DLL already exists; no files changed.");
            string original = Path.Combine(root, "original", Executable);
            if (Directory.Exists(root)) throw new IOException("TABQoL.Installation already exists; inspect it before installing.");
            Directory.CreateDirectory(Path.GetDirectoryName(original));
            File.Copy(exe, original);
            string staged = Path.Combine(root, "prepared.exe");
            bool committed = false;
            try
            {
                Patcher.Prepare(original, staged);
                var manifest = new Manifest { Mode = ExeMode, OriginalHash = originalHash, PatchedHash = SnapshotStore.Hash(staged), RuntimeHash = SnapshotStore.Hash(runtimeSource), InstalledUtc = DateTime.UtcNow };
                backupSaves();
                File.Copy(runtimeSource, runtimeTarget);
                using (var stream = File.Create(ManifestPath(directory))) new XmlSerializer(typeof(Manifest)).Serialize(stream, manifest);
                File.Replace(staged, exe, null);
                committed = true;
                Console.WriteLine("Installed TAB QoL for " + Compatibility.VersionFor(originalHash) + ".");
                Console.WriteLine("F5 save, F9 load, F8 health bars, [ slower, ] faster, F11 menu, F12 debug cheats. Options > TAB QoL.");
                Console.WriteLine("Original EXE: " + original);
            }
            finally
            {
                if (!committed)
                {
                    if (File.Exists(runtimeTarget) && SnapshotStore.Hash(runtimeTarget) == SnapshotStore.Hash(runtimeSource)) File.Delete(runtimeTarget);
                    if (File.Exists(staged)) File.Delete(staged);
                    if (File.Exists(ManifestPath(directory))) File.Delete(ManifestPath(directory));
                    // Retain the original backup for diagnosis/recovery.
                    Console.Error.WriteLine("Install did not commit. The original backup is in " + root);
                }
            }
        }

        private static void RequireHash(string path, string expected, string description)
        {
            if (string.IsNullOrEmpty(expected) || !File.Exists(path) || SnapshotStore.Hash(path) != expected)
                throw new IOException(description + " checksum mismatch or file missing. Refusing to change the installation.");
        }

        private static void ValidateMode(Manifest manifest)
        {
            if (!string.IsNullOrEmpty(manifest.Mode) && manifest.Mode != ExeMode && manifest.Mode != StartupMode)
                throw new InvalidDataException("Unknown TAB QoL installation mode. Refusing to change the installation.");
        }

        private static void ValidateStartupBackup(string directory, Manifest manifest)
        {
            if (manifest.OriginalHash != Compatibility.SteamSha256 || manifest.PatchedHash != manifest.OriginalHash)
                throw new InvalidDataException("Invalid Steam startup installation manifest.");
            string originalDirectory = Path.Combine(Installation(directory), "original");
            RequireHash(Path.Combine(originalDirectory, Executable), manifest.OriginalHash, "Original EXE backup");
            string originalConfig = Path.Combine(originalDirectory, Config);
            if (manifest.OriginalConfigExists) RequireHash(originalConfig, manifest.OriginalConfigHash, "Original config backup");
            else if (!string.IsNullOrEmpty(manifest.OriginalConfigHash) || File.Exists(originalConfig) || Directory.Exists(originalConfig))
                throw new IOException("Original config backup does not match the installation manifest.");
        }

        private static void ValidateInstalled(string directory, Manifest manifest)
        {
            ValidateMode(manifest);
            if (manifest.Mode == StartupMode)
            {
                ValidateStartupBackup(directory, manifest);
                RequireHash(Path.Combine(directory, Executable), manifest.OriginalHash, "Original Steam EXE");
                RequireHash(Path.Combine(directory, Config), manifest.PatchedConfigHash, "Installed startup config");
                RequireHash(Path.Combine(directory, Harmony), manifest.HarmonyHash, "Installed Harmony DLL");
            }
            else
            {
                if (!Compatibility.Supported(manifest.OriginalHash)) throw new InvalidDataException("Unsupported original EXE in installation manifest.");
                RequireHash(Path.Combine(directory, Executable), manifest.PatchedHash, "Patched game EXE");
            }
            RequireHash(Path.Combine(directory, Runtime), manifest.RuntimeHash, "Installed runtime DLL");
        }

        private static void InstallSteam(string directory, Action backupSaves)
        {
            string exe = Path.Combine(directory, Executable), root = Installation(directory);
            RequireHash(exe, Compatibility.SteamSha256, "Original Steam EXE");
            string runtimeSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Runtime);
            string harmonySource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Harmony);
            if (!File.Exists(runtimeSource) || !File.Exists(harmonySource))
                throw new FileNotFoundException("Keep the installer, TABQoL.Runtime.dll and 0Harmony.dll together.");
            string runtimeTarget = Path.Combine(directory, Runtime), harmonyTarget = Path.Combine(directory, Harmony);
            if (File.Exists(runtimeTarget) || Directory.Exists(runtimeTarget) || File.Exists(harmonyTarget) || Directory.Exists(harmonyTarget))
                throw new IOException("An unowned runtime or Harmony DLL already exists; no files changed.");
            if (Directory.Exists(root) || File.Exists(root)) throw new IOException("TABQoL.Installation already exists; inspect it before installing.");
            string config = Path.Combine(directory, Config);
            if (Directory.Exists(config)) throw new IOException("The startup config path is occupied by a directory; no files changed.");
            bool configExists = File.Exists(config);
            string originalConfigHash = configExists ? SnapshotStore.Hash(config) : null;
            // Validate and prepare XML in memory before creating any game files.
            byte[] preparedConfig = StartupConfig.Prepare(configExists ? config : null);
            string originalDirectory = Path.Combine(root, "original");
            string staged = Path.Combine(root, "prepared.config");
            var manifest = new Manifest
            {
                Mode = StartupMode,
                OriginalHash = Compatibility.SteamSha256,
                PatchedHash = Compatibility.SteamSha256,
                RuntimeHash = SnapshotStore.Hash(runtimeSource),
                HarmonyHash = SnapshotStore.Hash(harmonySource),
                OriginalConfigExists = configExists,
                OriginalConfigHash = originalConfigHash,
                InstalledUtc = DateTime.UtcNow
            };
            bool committed = false;
            Directory.CreateDirectory(originalDirectory);
            try
            {
                File.Copy(exe, Path.Combine(originalDirectory, Executable));
                if (configExists) File.Copy(config, Path.Combine(originalDirectory, Config));
                ValidateStartupBackup(directory, manifest);
                File.WriteAllBytes(staged, preparedConfig);
                manifest.PatchedConfigHash = SnapshotStore.Hash(staged);
                backupSaves();
                File.Copy(runtimeSource, runtimeTarget);
                File.Copy(harmonySource, harmonyTarget);
                RequireHash(runtimeTarget, manifest.RuntimeHash, "Copied runtime DLL");
                RequireHash(harmonyTarget, manifest.HarmonyHash, "Copied Harmony DLL");
                RequireHash(exe, manifest.OriginalHash, "Original Steam EXE");
                if (configExists) RequireHash(config, manifest.OriginalConfigHash, "Original startup config");
                else if (File.Exists(config) || Directory.Exists(config)) throw new IOException("The startup config appeared during installation. Refusing to overwrite it.");
                using (var stream = File.Create(ManifestPath(directory))) new XmlSerializer(typeof(Manifest)).Serialize(stream, manifest);
                if (configExists) File.Replace(staged, config, null);
                else File.Move(staged, config);
                committed = true;
                Console.WriteLine("Installed TAB QoL for " + Compatibility.SteamVersion + " using the startup config.");
                Console.WriteLine("Steam EXE is unchanged. F11 menu, F5 save, F9 load, F8 health bars, F12 debug cheats. Options > TAB QoL.");
                Console.WriteLine("Original EXE/config backup: " + originalDirectory);
            }
            finally
            {
                if (!committed)
                {
                    if (File.Exists(runtimeTarget) && SnapshotStore.Hash(runtimeTarget) == manifest.RuntimeHash) File.Delete(runtimeTarget);
                    if (File.Exists(harmonyTarget) && SnapshotStore.Hash(harmonyTarget) == manifest.HarmonyHash) File.Delete(harmonyTarget);
                    if (File.Exists(staged)) File.Delete(staged);
                    if (File.Exists(ManifestPath(directory))) File.Delete(ManifestPath(directory));
                    Console.Error.WriteLine("Install did not commit. The original backup is in " + root);
                }
            }
        }

        private static string ArchivePath(string directory)
        {
            string game = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string root = Path.GetFullPath(Installation(directory));
            string archived = Path.GetFullPath(root + ".uninstalled-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            if (!root.StartsWith(game, StringComparison.OrdinalIgnoreCase) || !archived.StartsWith(game, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), game.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Installation archive paths must stay inside the game directory.");
            return archived;
        }

        private static void BackupSaves()
        {
            string userRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "They Are Billions");
            if (!Directory.Exists(userRoot)) return;
            string backup = Path.Combine(userRoot, "TABQoL", "Backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-installation");
            Directory.CreateDirectory(backup);
            foreach (string file in Directory.GetFiles(userRoot)) File.Copy(file, Path.Combine(backup, Path.GetFileName(file)));
            string saves = Path.Combine(userRoot, "Saves");
            if (!Directory.Exists(saves)) return;
            Directory.CreateDirectory(Path.Combine(backup, "Saves"));
            foreach (string file in Directory.GetFiles(saves)) File.Copy(file, Path.Combine(backup, "Saves", Path.GetFileName(file)));
            Console.WriteLine("Save backup: " + backup);
        }

        internal static void Uninstall(string directory, Action verifyNotRunning)
        {
            verifyNotRunning();
            var manifest = ReadManifest(directory) ?? throw new FileNotFoundException("No TAB QoL installation manifest.");
            ValidateMode(manifest);
            if (manifest.Mode == StartupMode) { UninstallSteam(directory, manifest); return; }
            string exe = Path.Combine(directory, Executable), original = Path.Combine(Installation(directory), "original", Executable);
            string currentHash = SnapshotStore.Hash(exe);
            if (currentHash != manifest.PatchedHash && currentHash != manifest.OriginalHash)
                throw new IOException("The game EXE changed after installation. Refusing to overwrite it.");
            if (!Compatibility.Supported(manifest.OriginalHash) || SnapshotStore.Hash(original) != manifest.OriginalHash)
                throw new IOException("Original backup checksum mismatch. Refusing to restore it.");
            string runtime = Path.Combine(directory, Runtime);
            if (File.Exists(runtime) && SnapshotStore.Hash(runtime) != manifest.RuntimeHash)
                throw new IOException("Runtime DLL changed after installation. Refusing to delete it.");
            string archived = ArchivePath(directory);
            if (currentHash != manifest.OriginalHash)
            {
                string restore = Path.Combine(Installation(directory), "restore.exe");
                File.Copy(original, restore, true);
                File.Replace(restore, exe, null);
            }
            if (File.Exists(runtime)) File.Delete(runtime);
            Directory.Move(Installation(directory), archived);
            Console.WriteLine("Original EXE restored. Snapshots/settings/backups are preserved.\nBackup archive: " + archived);
        }

        private static void UninstallSteam(string directory, Manifest manifest)
        {
            ValidateInstalled(directory, manifest);
            string root = Installation(directory), config = Path.Combine(directory, Config);
            string archived = ArchivePath(directory);
            if (manifest.OriginalConfigExists)
            {
                string restore = Path.Combine(root, "restore.config");
                File.Copy(Path.Combine(root, "original", Config), restore, true);
                RequireHash(restore, manifest.OriginalConfigHash, "Restored startup config");
                File.Replace(restore, config, null);
            }
            else File.Delete(config);
            File.Delete(Path.Combine(directory, Runtime));
            File.Delete(Path.Combine(directory, Harmony));
            Directory.Move(root, archived);
            Console.WriteLine("Original startup config restored; Steam EXE was unchanged. Snapshots/settings/backups are preserved.\nBackup archive: " + archived);
        }
    }
}
