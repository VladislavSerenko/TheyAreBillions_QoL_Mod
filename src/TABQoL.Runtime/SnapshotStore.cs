using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace TABQoL
{
    public sealed class Snapshot
    {
        public string Scope { get; set; }
        public string Slot { get; set; }
        public string Generation { get; set; }
        public string SaveHash { get; set; }
        public string CheckHash { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class SnapshotStore
    {
        private readonly string root;
        public SnapshotStore(string root) { this.root = Path.GetFullPath(root); }

        public static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) throw new InvalidDataException("Invalid snapshot identifier.");
            foreach (char c in value)
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) throw new InvalidDataException("Invalid snapshot identifier.");
            return value;
        }

        private string DirectoryFor(string scope, string slot) { return Path.Combine(root, Safe(scope), Safe(slot)); }

        public string NextAutosaveSlot(string scope)
        {
            return Enumerable.Range(1, 5).Select(i => new { Slot = "autosave" + i, Snapshot = Read(scope, "autosave" + i) })
                .OrderBy(s => s.Snapshot == null ? DateTime.MinValue : s.Snapshot.CreatedUtc).ThenBy(s => s.Slot)
                .First().Slot;
        }

        public Snapshot Read(string scope, string slot)
        {
            string path = Path.Combine(DirectoryFor(scope, slot), "current.xml");
            if (!File.Exists(path)) return null;
            Snapshot snapshot;
            using (var stream = File.OpenRead(path)) snapshot = (Snapshot)new XmlSerializer(typeof(Snapshot)).Deserialize(stream);
            if (snapshot.Scope != scope || snapshot.Slot != slot) throw new InvalidDataException("Snapshot belongs to another mission.");
            Safe(snapshot.Generation);
            return snapshot;
        }

        public Snapshot Save(string scope, string slot, string savesDirectory, Action<string> writeNativeSave)
        {
            string generation = Guid.NewGuid().ToString("N");
            string directory = DirectoryFor(scope, slot);
            string staging = Path.Combine(savesDirectory, "TABQoL" + generation + ".zxsav");
            string pending = Path.Combine(directory, generation + "-pending");
            Directory.CreateDirectory(pending);
            try
            {
                writeNativeSave(staging);
                string check = Path.ChangeExtension(staging, ".zxcheck");
                if (!File.Exists(staging) || !File.Exists(check) || new FileInfo(staging).Length == 0)
                    throw new IOException("The game did not write a complete save/checksum pair.");
                var snapshot = new Snapshot { Scope = scope, Slot = slot, Generation = generation, CreatedUtc = DateTime.UtcNow,
                    SaveHash = Hash(staging), CheckHash = Hash(check) };
                File.Move(staging, Path.Combine(pending, "state.zxsav"));
                File.Move(check, Path.Combine(pending, "state.zxcheck"));
                Directory.Move(pending, Path.Combine(directory, generation));
                string pointer = Path.Combine(directory, "current.xml");
                using (var stream = File.Create(pointer + ".new")) new XmlSerializer(typeof(Snapshot)).Serialize(stream, snapshot);
                if (File.Exists(pointer)) File.Replace(pointer + ".new", pointer, Path.Combine(directory, "previous.xml"));
                else File.Move(pointer + ".new", pointer);
                Prune(directory, generation);
                return snapshot;
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
                if (File.Exists(Path.ChangeExtension(staging, ".zxcheck"))) File.Delete(Path.ChangeExtension(staging, ".zxcheck"));
                if (Directory.Exists(pending)) Directory.Delete(pending, true);
            }
        }

        private static void Prune(string directory, string current)
        {
            // Never let housekeeping turn an already committed save into a failure.
            try
            {
                string previous = null;
                string pointer = Path.Combine(directory, "previous.xml");
                if (File.Exists(pointer))
                    using (var stream = File.OpenRead(pointer)) previous = ((Snapshot)new XmlSerializer(typeof(Snapshot)).Deserialize(stream)).Generation;
                foreach (string path in Directory.GetDirectories(directory))
                {
                    string name = Path.GetFileName(path);
                    Guid generation;
                    if (name != current && name != previous && Guid.TryParseExact(name, "N", out generation)) Directory.Delete(path, true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
        }

        public string StageLoad(string scope, string slot, string savesDirectory)
        {
            var snapshot = Read(scope, slot) ?? throw new FileNotFoundException("This slot is empty.");
            string directory = Path.Combine(DirectoryFor(scope, slot), snapshot.Generation);
            string save = Path.Combine(directory, "state.zxsav"), check = Path.Combine(directory, "state.zxcheck");
            if (Hash(save) != snapshot.SaveHash || Hash(check) != snapshot.CheckHash)
                throw new InvalidDataException("Snapshot checksum mismatch; the slot was not loaded.");
            // Native serialization derives its password from the filename. Keep the
            // basename used by Save, even though the files live outside Saves at rest.
            string staging = Path.Combine(savesDirectory, "TABQoL" + snapshot.Generation + ".zxsav");
            string stagedCheck = Path.ChangeExtension(staging, ".zxcheck");
            if (File.Exists(staging) || File.Exists(stagedCheck))
            {
                if (File.Exists(staging) && File.Exists(stagedCheck) && Hash(staging) == snapshot.SaveHash && Hash(stagedCheck) == snapshot.CheckHash)
                    return staging;
                throw new IOException("A different file already occupies the snapshot staging path.");
            }
            try
            {
                File.Copy(save, staging);
                File.Copy(check, stagedCheck);
                return staging;
            }
            catch
            {
                if (File.Exists(staging)) File.Delete(staging);
                if (File.Exists(stagedCheck)) File.Delete(stagedCheck);
                throw;
            }
        }
    }
}
