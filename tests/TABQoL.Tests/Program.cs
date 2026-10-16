using System;
using System.Collections.Generic;
using System.IO;
using TABQoL;
using TABQoL.Installer;

internal static class Program
{
    private static int passed;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        passed++;
        Console.WriteLine("PASS " + name);
    }
    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    private static int Main(string[] args)
    {
        string root = Path.Combine(Path.GetTempPath(), "TABQoL.Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Check(Policy.StepSpeed(1, 1) == 2 && Policy.StepSpeed(1, -1) == 0.5, "speed steps and half speed");
            Check(Policy.StepSpeed(5, 1) == 5 && Policy.StepSpeed(0.5, -1) == 0.5, "speed boundaries");
            Check(Policy.StepSpeed(1.4, 1) == 2 && Policy.StepSpeed(1.4, -1) == 1, "speed steps from a nonstandard game value");
            Check(!Policy.ValidSpeed(double.NaN) && !Policy.ValidSpeed(6), "invalid speeds rejected");
            Check(Policy.Refund(new[] { "a", "a", "b", "free" }, new Dictionary<string, int> { { "a", 120 }, { "b", 80 }, { "free", 0 } }) == 200, "refund does not count duplicate unlocks");
            Check(Policy.RewardDelta(false, 100, 250) == 350 && Policy.RewardDelta(true, 100, 250) == 0, "fixed reward plus full loot, no repeat award");
            string settingsPath = Path.Combine(root, "Settings.xml");
            var settings = new Settings { Speed = 0.5, SkipHeroMissions = true };
            settings.Write(settingsPath);
            Check(Settings.Read(settingsPath).SkipHeroMissions && Settings.Read(settingsPath).Speed == 0.5, "settings round trip");
            settings.Speed = 5;
            settings.Write(settingsPath);
            Check(Settings.Read(settingsPath).Speed == 5 && File.Exists(settingsPath + ".bak"), "settings atomic replacement with previous copy");
            settings.FasterKey = settings.SlowerKey;
            Check(Throws<InvalidDataException>(settings.Validate), "duplicate hotkeys rejected");
            settings = new Settings { MenuKey = 27 };
            Check(Throws<InvalidDataException>(settings.Validate), "Escape reserved for cancelling key capture");
            Check(!Settings.BindableKey(16) && !Settings.BindableKey(162) && !Settings.BindableKey(91) && Settings.BindableKey(117), "modifier/Windows keys rejected and ordinary function keys accepted");
            File.WriteAllText(settingsPath, "<Settings><Speed>2</Speed><MenuKey>121</MenuKey></Settings>");
            settings = Settings.Read(settingsPath);
            Check(settings.CheatsKey == 123 && settings.BuildingKeys.Count == 0 && settings.Speed == 2, "0.1 settings migrate with F12 and default building keys");
            settings.BuildingKeys.Add(new BuildingBinding { EntityType = "ZX.Entities.TentHouse", Key = 84 });
            settings.Write(settingsPath);
            Check(Settings.Read(settingsPath).BuildingKeys[0].Key == 84, "building overrides round trip");
            settings.MenuKey = 84;
            Check(Throws<InvalidDataException>(settings.Validate), "building and global hotkey conflict rejected");
            settings = new Settings { CheatsKey = 116 };
            Check(Throws<InvalidDataException>(settings.Validate), "cheat panel and quicksave conflict rejected");
            Check(Settings.BindableBuildingKey(65) && Settings.BindableBuildingKey(90) && !Settings.BindableBuildingKey(32) && !Settings.BindableBuildingKey(49), "building keys exclude native pause and control groups");
            var store = new SnapshotStore(Path.Combine(root, "Snapshots"));
            string nativeSaves = Path.Combine(root, "NativeSaves");
            Directory.CreateDirectory(nativeSaves);
            void Write(string path, string value) { File.WriteAllText(path, value); File.WriteAllText(Path.ChangeExtension(path, ".zxcheck"), "native-check"); }
            Check(store.Read("campaign1-mission1", "quick") == null, "empty slot handled");
            var first = store.Save("campaign1-mission1", "quick", nativeSaves, p => Write(p, "first"));
            store.Save("campaign1-mission2", "quick", nativeSaves, p => Write(p, "other mission"));
            string staged = store.StageLoad("campaign1-mission1", "quick", nativeSaves);
            Check(File.ReadAllText(staged) == "first" && File.Exists(Path.ChangeExtension(staged, ".zxcheck")), "save/checksum staged together and scoped to mission");
            Check(Path.GetFileName(staged) == "TABQoL" + first.Generation + ".zxsav", "native filename preserved for filename-dependent serialization");
            Check(store.StageLoad("campaign1-mission1", "quick", nativeSaves) == staged, "same immutable snapshot can be staged repeatedly");
            store.Save("campaign1-mission1", "quick", nativeSaves, p => Write(p, "second"));
            Check(File.ReadAllText(Path.Combine(root, "Snapshots", "campaign1-mission1", "quick", first.Generation, "state.zxsav")) == "first", "previous quicksave generation retained");
            Check(Throws<IOException>(() => store.Save("campaign1-mission1", "quick", nativeSaves, p => { throw new IOException("disk full"); })), "native write failure propagated");
            Check(File.ReadAllText(store.StageLoad("campaign1-mission1", "quick", nativeSaves)) == "second", "failed save leaves current slot loadable");
            store.Save("campaign1-mission1", "quick", nativeSaves, p => Write(p, "third"));
            Check(Directory.GetDirectories(Path.Combine(root, "Snapshots", "campaign1-mission1", "quick")).Length == 2 &&
                !Directory.Exists(Path.Combine(root, "Snapshots", "campaign1-mission1", "quick", first.Generation)), "only current and previous snapshot generations retained");
            var current = store.Read("campaign1-mission1", "quick");
            string currentPath = Path.Combine(root, "Snapshots", "campaign1-mission1", "quick", current.Generation, "state.zxsav");
            File.AppendAllText(currentPath, "corruption");
            Check(Throws<InvalidDataException>(() => store.StageLoad("campaign1-mission1", "quick", nativeSaves)), "corrupt snapshot rejected before load");
            Check(Throws<InvalidDataException>(() => store.Read("..", "quick")), "snapshot path traversal rejected");
            Check(Throws<IOException>(() => store.Save("campaign2", "quick", nativeSaves, p => File.WriteAllText(p, "no-checksum"))), "incomplete native save pair rejected");
            if (args.Length == 1)
            {
                string prepared = Path.Combine(root, "prepared.exe");
                Patcher.Prepare(args[0], prepared);
                Patcher.Verify(prepared);
                Check(File.Exists(prepared), "actual supported EXE patched and all eight hook names verified");
                Check(SnapshotStore.Hash(args[0]) == Compatibility.OriginalSha256, "original EXE unchanged");
                Check(Throws<InvalidDataException>(() => Patcher.Prepare(prepared, Path.Combine(root, "double.exe"))), "double patch rejected");
            }
            Console.WriteLine(passed + " checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // This directory is created above with a random, task-owned name.
            Directory.Delete(root, true);
        }
    }
}
