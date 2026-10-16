using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace TABQoL.Installer
{
    public sealed class Manifest
    {
        public string OriginalHash { get; set; }
        public string PatchedHash { get; set; }
        public string RuntimeHash { get; set; }
        public DateTime InstalledUtc { get; set; }
    }

    internal static class Program
    {
        private const string Executable = "TheyAreBillions.exe";
        private const string Runtime = "TABQoL.Runtime.dll";

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("TAB QoL: inspect|install|uninstall <game-directory>\n         prepare <game-directory> <output-exe>");
                    return 2;
                }
                string directory = Path.GetFullPath(args[1]);
                string exe = Path.Combine(directory, Executable);
                if (!File.Exists(exe)) throw new FileNotFoundException(Executable, exe);
                switch (args[0].ToLowerInvariant())
                {
                    case "inspect":
                        Console.WriteLine("EXE SHA-256: " + SnapshotStore.Hash(exe));
                        Console.WriteLine("Supported: " + Compatibility.GameVersion);
                        if (SnapshotStore.Hash(exe) == Compatibility.OriginalSha256) { Console.WriteLine("Compatible original EXE."); return 0; }
                        var status = ReadManifest(directory);
                        if (status != null && SnapshotStore.Hash(exe) == status.PatchedHash) { Console.WriteLine("TAB QoL is installed."); return 0; }
                        throw new InvalidDataException("This executable is not supported.");
                    case "prepare":
                        if (args.Length != 3) throw new ArgumentException("prepare needs an output EXE path.");
                        if (string.Equals(exe, Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use install to modify the game.");
                        if (File.Exists(args[2])) throw new IOException("Output already exists.");
                        Patcher.Prepare(exe, args[2]);
                        Console.WriteLine("Prepared and verified: " + Path.GetFullPath(args[2]));
                        return 0;
                    case "install": Install(directory); return 0;
                    case "uninstall": Uninstall(directory); return 0;
                    default: throw new ArgumentException("Unknown command: " + args[0]);
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }

        private static string Installation(string directory) { return Path.Combine(directory, "TABQoL.Installation"); }
        private static string ManifestPath(string directory) { return Path.Combine(Installation(directory), "manifest.xml"); }

        private static Manifest ReadManifest(string directory)
        {
            if (!File.Exists(ManifestPath(directory))) return null;
            using (var stream = File.OpenRead(ManifestPath(directory))) return (Manifest)new XmlSerializer(typeof(Manifest)).Deserialize(stream);
        }

        private static void NotRunning()
        {
            if (Process.GetProcessesByName("TheyAreBillions").Any() || Process.GetProcessesByName("TheyAreBillions_x86").Any())
                throw new InvalidOperationException("Close the game before installing or uninstalling TAB QoL.");
        }

        private static void Install(string directory)
        {
            NotRunning();
            string exe = Path.Combine(directory, Executable), root = Installation(directory);
            var existing = ReadManifest(directory);
            if (existing != null)
            {
                if (SnapshotStore.Hash(exe) == existing.PatchedHash && File.Exists(Path.Combine(directory, Runtime)) && SnapshotStore.Hash(Path.Combine(directory, Runtime)) == existing.RuntimeHash)
                { Console.WriteLine("TAB QoL is already installed."); return; }
                throw new IOException("An installation manifest already exists. Run uninstall before reinstalling.");
            }
            if (SnapshotStore.Hash(exe) != Compatibility.OriginalSha256) throw new InvalidDataException("Unsupported/modified game EXE; no files changed.");
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
                var manifest = new Manifest { OriginalHash = Compatibility.OriginalSha256, PatchedHash = SnapshotStore.Hash(staged), RuntimeHash = SnapshotStore.Hash(runtimeSource), InstalledUtc = DateTime.UtcNow };
                BackupSaves();
                File.Copy(runtimeSource, runtimeTarget);
                using (var stream = File.Create(ManifestPath(directory))) new XmlSerializer(typeof(Manifest)).Serialize(stream, manifest);
                File.Replace(staged, exe, null);
                committed = true;
                Console.WriteLine("Installed TAB QoL for " + Compatibility.GameVersion + ".");
                Console.WriteLine("F5 save, F9 load, [ slower, ] faster, F10 menu, F12 debug cheats. Options > TAB QoL.");
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

        private static void Uninstall(string directory)
        {
            NotRunning();
            var manifest = ReadManifest(directory) ?? throw new FileNotFoundException("No TAB QoL installation manifest.");
            string exe = Path.Combine(directory, Executable), original = Path.Combine(Installation(directory), "original", Executable);
            string currentHash = SnapshotStore.Hash(exe);
            if (currentHash != manifest.PatchedHash && currentHash != manifest.OriginalHash)
                throw new IOException("The game EXE changed after installation. Refusing to overwrite it.");
            if (manifest.OriginalHash != Compatibility.OriginalSha256 || SnapshotStore.Hash(original) != Compatibility.OriginalSha256)
                throw new IOException("Original backup checksum mismatch. Refusing to restore it.");
            string runtime = Path.Combine(directory, Runtime);
            if (File.Exists(runtime) && SnapshotStore.Hash(runtime) != manifest.RuntimeHash)
                throw new IOException("Runtime DLL changed after installation. Refusing to delete it.");
            if (currentHash != manifest.OriginalHash)
            {
                string restore = Path.Combine(Installation(directory), "restore.exe");
                File.Copy(original, restore, true);
                File.Replace(restore, exe, null);
            }
            if (File.Exists(runtime)) File.Delete(runtime);
            string archived = Installation(directory) + ".uninstalled-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            Directory.Move(Installation(directory), archived);
            Console.WriteLine("Original EXE restored. Snapshots/settings/backups are preserved.\nBackup archive: " + archived);
        }
    }
}
