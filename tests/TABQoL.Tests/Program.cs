using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using TABQoL;
using TABQoL.Installer;
using Installer = TABQoL.Installer.Program;

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

    private static void SteamLifecycle(string root, string original)
    {
        string game = Path.Combine(root, "SteamLifecycle");
        Directory.CreateDirectory(game);
        string exe = Path.Combine(game, "TheyAreBillions.exe");
        string config = exe + ".config";
        string runtime = Path.Combine(game, "TABQoL.Runtime.dll"), harmony = Path.Combine(game, "0Harmony.dll");
        string installation = Path.Combine(game, "TABQoL.Installation");
        File.Copy(original, exe);
        File.WriteAllText(config, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n" +
            "  <!-- Кампания: keep original bytes -->\r\n  <runtime><gcServer enabled=\"true\" /></runtime>\r\n</configuration>\r\n", new UTF8Encoding(false));
        string originalConfigHash = SnapshotStore.Hash(config);
        int backups = 0, guards = 0;
        Action backup = () => backups++;
        Action guard = () => guards++;
        Installer.Install(game, backup, guard);
        var manifest = Installer.ReadManifest(game);
        Check(manifest != null && manifest.Mode == "steam-startup-config" && backups == 1 && guards == 1 &&
            manifest.OriginalConfigExists && manifest.OriginalConfigHash == originalConfigHash &&
            SnapshotStore.Hash(exe) == Compatibility.SteamSha256 &&
            SnapshotStore.Hash(Path.Combine(installation, "original", "TheyAreBillions.exe")) == Compatibility.SteamSha256 &&
            SnapshotStore.Hash(Path.Combine(installation, "original", "TheyAreBillions.exe.config")) == originalConfigHash,
            "Steam install keeps the exact EXE and records exact original EXE/config backups");
        Check(SnapshotStore.Hash(runtime) == manifest.RuntimeHash && SnapshotStore.Hash(harmony) == manifest.HarmonyHash &&
            SnapshotStore.Hash(config) == manifest.PatchedConfigHash && manifest.PatchedHash == Compatibility.SteamSha256,
            "Steam manifest owns both copied DLLs and the startup config by checksum");
        var configXml = new XmlDocument();
        configXml.Load(config);
        Check(configXml.SelectSingleNode("/configuration/runtime/gcServer/@enabled").Value == "true" &&
            configXml.SelectSingleNode("/configuration/runtime/appDomainManagerType/@value").Value == "TABQoL.SteamDomainManager",
            "Steam installed config keeps existing options and enables the runtime manager");
        string inspectOutput;
        var console = Console.Out;
        using (var writer = new StringWriter())
        {
            try { Console.SetOut(writer); Installer.Inspect(game); }
            finally { Console.SetOut(console); }
            inspectOutput = writer.ToString();
        }
        Check(inspectOutput.Contains("TAB QoL is installed") && !inspectOutput.Contains("Compatible original EXE"),
            "Steam inspect recognizes the manifest before the unchanged original EXE hash");
        Installer.Install(game, backup, guard);
        Check(backups == 1 && SnapshotStore.Hash(config) == manifest.PatchedConfigHash,
            "repeated Steam install recognizes ownership without another backup or config edit");
        var owned = new[] { exe, config, runtime, harmony,
            Path.Combine(installation, "original", "TheyAreBillions.exe"),
            Path.Combine(installation, "original", "TheyAreBillions.exe.config") };
        foreach (string changed in owned)
        {
            byte[] unchanged = File.ReadAllBytes(changed);
            File.AppendAllText(changed, "changed after install");
            var before = owned.ToDictionary(path => path, SnapshotStore.Hash);
            Check(Throws<IOException>(() => Installer.Uninstall(game, guard)) &&
                before.All(file => SnapshotStore.Hash(file.Key) == file.Value) && Installer.ReadManifest(game) != null,
                "Steam uninstall rejects changed " + Path.GetFileName(Path.GetDirectoryName(changed)) + "/" + Path.GetFileName(changed) + " before modifying files");
            File.WriteAllBytes(changed, unchanged);
        }
        Installer.Uninstall(game, guard);
        var archives = Directory.GetDirectories(game, "TABQoL.Installation.uninstalled-*");
        Check(SnapshotStore.Hash(exe) == Compatibility.SteamSha256 && SnapshotStore.Hash(config) == originalConfigHash &&
            !File.Exists(runtime) && !File.Exists(harmony) && !Directory.Exists(installation),
            "Steam uninstall restores the exact config and removes only its owned DLLs");
        Check(archives.Length == 1 && Path.GetDirectoryName(archives[0]) == game &&
            SnapshotStore.Hash(Path.Combine(archives[0], "original", "TheyAreBillions.exe.config")) == originalConfigHash,
            "Steam uninstall archives the exact backups inside the test game directory");
        File.Delete(config);
        Installer.Install(game, backup, guard);
        manifest = Installer.ReadManifest(game);
        Check(!manifest.OriginalConfigExists && string.IsNullOrEmpty(manifest.OriginalConfigHash) && File.Exists(config),
            "Steam install records a previously absent startup config");
        Installer.Uninstall(game, guard);
        Check(!File.Exists(config) && !File.Exists(runtime) && !File.Exists(harmony) && SnapshotStore.Hash(exe) == Compatibility.SteamSha256,
            "Steam uninstall removes a newly created config and preserves the original EXE");
        File.WriteAllText(config, "<configuration><runtime><appDomainManagerType value=\"Another.Mod\" /></runtime></configuration>");
        string foreignConfigHash = SnapshotStore.Hash(config);
        Check(Throws<InvalidDataException>(() => Installer.Install(game, backup, guard)) &&
            SnapshotStore.Hash(config) == foreignConfigHash && !Directory.Exists(installation) && !File.Exists(runtime) && !File.Exists(harmony),
            "Steam install refuses another manager without creating installation files");
        File.Delete(config);
        foreach (string unowned in new[] { runtime, harmony })
        {
            File.WriteAllText(unowned, "another mod owns this DLL");
            string unownedHash = SnapshotStore.Hash(unowned);
            Check(Throws<IOException>(() => Installer.Install(game, backup, guard)) &&
                SnapshotStore.Hash(unowned) == unownedHash && !Directory.Exists(installation) && !File.Exists(config),
                "Steam install preserves unowned " + Path.GetFileName(unowned));
            File.Delete(unowned);
        }
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
            Check(settings.CheatsKey == 123 && settings.MenuKey == 122 && settings.AutosaveMinutes == 10 && settings.BuildingKeys.Count == 0 && settings.Speed == 2, "old defaults migrate to F11 and ten-minute autosaves");
            File.WriteAllText(settingsPath, "<Settings><MenuKey>117</MenuKey><Speed>1</Speed></Settings>");
            Check(Settings.Read(settingsPath).MenuKey == 117, "custom menu binding survives migration");
            Check(Throws<InvalidDataException>(() => new Settings { AutosaveMinutes = -1 }.Validate()) &&
                Throws<InvalidDataException>(() => new Settings { AutosaveMinutes = 1441 }.Validate()), "autosave interval bounds enforced");
            Check(Policy.GridKey(1) == 'Q' && Policy.GridKey(2) == 'W' && Policy.GridKey(5) == 'T' && Policy.GridKey(6) == 'A' && Policy.GridKey(15) == 'B' && Policy.GridKey(0) == 0, "command grid follows QWERT, ASDFG, ZXCVB");
            var schedule = new AutosaveSchedule();
            Check(!schedule.Tick(0, true, 1), "autosave does not fire immediately");
            for (int second = 1; second < 30; second++) schedule.Tick(second, true, 1);
            for (int second = 30; second < 90; second++) schedule.Tick(second, false, 1);
            Check(!schedule.Tick(90, true, 1), "paused or modal time excluded from autosave timer");
            bool due = false;
            for (int second = 91; second <= 120; second++) due = schedule.Tick(second, true, 1);
            Check(due, "autosave becomes due after sixty seconds of active play");
            schedule.Completed();
            Check(!schedule.Tick(500, true, 1), "no catch-up save after a suspended process");
            schedule.Reset();
            Check(!schedule.Tick(501, true, 1) && !schedule.Tick(502, true, 0), "mission reset and disabled autosave cancel pending saves");
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
            for (int i = 1; i <= 5; i++)
            {
                string slot = store.NextAutosaveSlot("history");
                Check(slot == "autosave" + i, "autosave fills separate history slot " + i);
                store.Save("history", slot, nativeSaves, p => Write(p, "history " + i));
            }
            string oldest = store.NextAutosaveSlot("history");
            Check(oldest == "autosave1", "full autosave ring replaces oldest slot");
            store.Save("history", oldest, nativeSaves, p => Write(p, "sixth"));
            Check(store.NextAutosaveSlot("history") == "autosave2" && store.Read("history", "quick") == null && store.Read("history", "slot1") == null, "autosave rotates without touching quick or manual slots");
            string configPath = Path.Combine(root, "TheyAreBillions.exe.config");
            const string unicodeConfigText = "Кампания — испытание 猫";
            var utf8 = new UTF8Encoding(false, true);
            File.WriteAllText(configPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<configuration>\n  <!-- keep existing options -->\n" +
                "  <startup useLegacyV2RuntimeActivationPolicy=\"true\"><supportedRuntime version=\"v4.0\" sku=\".NETFramework,Version=v4.6.2\" /></startup>\n" +
                "  <appSettings><add key=\"Campaign\" value=\"" + unicodeConfigText + "\" /></appSettings>\n" +
                "  <runtime><gcServer enabled=\"true\" /><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\">" +
                "<dependentAssembly><assemblyIdentity name=\"Example.Dependency\" publicKeyToken=\"0123456789abcdef\" culture=\"neutral\" />" +
                "<bindingRedirect oldVersion=\"0.0.0.0-2.0.0.0\" newVersion=\"2.0.0.0\" /></dependentAssembly></assemblyBinding></runtime>\n</configuration>", utf8);
            string originalConfigHash = SnapshotStore.Hash(configPath);
            byte[] preparedConfig = StartupConfig.Prepare(configPath);
            XmlDocument ReadConfig(byte[] bytes)
            {
                var document = new XmlDocument { XmlResolver = null };
                using (var stream = new MemoryStream(bytes)) document.Load(stream);
                return document;
            }
            var config = ReadConfig(preparedConfig);
            var configNamespaces = new XmlNamespaceManager(config.NameTable);
            configNamespaces.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");
            Check(config.SelectNodes("/configuration/runtime").Count == 1 &&
                config.SelectSingleNode("/configuration/startup/@useLegacyV2RuntimeActivationPolicy").Value == "true" &&
                config.SelectSingleNode("/configuration/startup/supportedRuntime/@sku").Value == ".NETFramework,Version=v4.6.2" &&
                config.SelectSingleNode("/configuration/runtime/gcServer/@enabled").Value == "true" &&
                config.SelectSingleNode("/configuration/runtime/asm:assemblyBinding/asm:dependentAssembly/asm:bindingRedirect/@newVersion", configNamespaces).Value == "2.0.0.0",
                "Steam startup config preserves CLR options and namespaced assembly redirects");
            Check(utf8.GetString(preparedConfig).Contains(unicodeConfigText) &&
                config.SelectSingleNode("/configuration/appSettings/add[@key='Campaign']/@value").Value == unicodeConfigText &&
                SnapshotStore.Hash(configPath) == originalConfigHash,
                "Steam config preserves UTF-8 text without modifying the original file");
            Check(config.SelectNodes("/configuration/runtime/appDomainManagerAssembly").Count == 1 &&
                config.SelectNodes("/configuration/runtime/appDomainManagerType").Count == 1 &&
                config.SelectSingleNode("/configuration/runtime/appDomainManagerAssembly/@value").Value == typeof(Bridge).Assembly.FullName &&
                config.SelectSingleNode("/configuration/runtime/appDomainManagerType/@value").Value == "TABQoL.SteamDomainManager",
                "Steam config selects the exact runtime manager assembly and type");
            var createdConfig = ReadConfig(StartupConfig.Prepare(null));
            Check(createdConfig.DocumentElement.Name == "configuration" &&
                createdConfig.SelectSingleNode("/configuration/runtime/appDomainManagerAssembly/@value").Value == typeof(Bridge).Assembly.FullName &&
                createdConfig.SelectSingleNode("/configuration/runtime/appDomainManagerType/@value").Value == "TABQoL.SteamDomainManager",
                "Steam startup config is created when the game has no config file");
            foreach (string managerNode in new[] { "appDomainManagerAssembly", "appDomainManagerType" })
            {
                File.WriteAllText(configPath, "<configuration><runtime><" + managerNode + " value=\"Another.Mod\" /></runtime></configuration>", utf8);
                Check(Throws<InvalidDataException>(() => StartupConfig.Prepare(configPath)), "existing " + managerNode + " prevents startup config takeover");
            }
            File.WriteAllText(configPath, "<configuration><runtime configSource=\"external.config\" /></configuration>", utf8);
            Check(Throws<InvalidDataException>(() => StartupConfig.Prepare(configPath)), "external runtime config is rejected without following it");
            File.WriteAllText(configPath, "<configuration><runtime></configuration>", utf8);
            Check(Throws<XmlException>(() => StartupConfig.Prepare(configPath)), "malformed startup XML is rejected");
            File.WriteAllText(configPath, "<configuration><runtime /><runtime /></configuration>", utf8);
            Check(Throws<InvalidDataException>(() => StartupConfig.Prepare(configPath)), "duplicate runtime sections are rejected");
            Check(Compatibility.Supported(Compatibility.OriginalSha256) && Compatibility.Supported(Compatibility.SteamSha256) && !Compatibility.Supported("unknown"), "only exact GOG and Steam originals supported");
            foreach (string original in args)
            {
                string originalHash = SnapshotStore.Hash(original);
                string prepared = Path.Combine(root, originalHash + "-prepared.exe");
                if (originalHash == Compatibility.SteamSha256)
                {
                    Check(Throws<InvalidDataException>(() => Patcher.Prepare(original, prepared)) && !File.Exists(prepared) &&
                        SnapshotStore.Hash(original) == originalHash, "Steam EXE rewriting is rejected before creating output");
                    SteamLifecycle(root, original);
                    continue;
                }
                Patcher.Prepare(original, prepared);
                Patcher.Verify(prepared);
                Patcher.VerifyMetadata(original, prepared);
                Check(true, "original metadata token identities preserved");
                Check(File.Exists(prepared), "actual " + Compatibility.VersionFor(originalHash) + " patched and all nine hook names verified");
                Check(SnapshotStore.Hash(original) == originalHash, "original EXE unchanged");
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
