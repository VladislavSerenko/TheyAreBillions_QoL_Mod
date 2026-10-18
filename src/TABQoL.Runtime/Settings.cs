using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace TABQoL
{
    public sealed class Settings
    {
        public bool SkipHeroMissions { get; set; }
        public double Speed { get; set; } = 1;
        public int QuickSaveKey { get; set; } = 116;
        public int QuickLoadKey { get; set; } = 120;
        public int SlowerKey { get; set; } = 219;
        public int FasterKey { get; set; } = 221;
        public int MenuKey { get; set; } = 122;
        public int CheatsKey { get; set; } = 123;
        public int HealthBarsKey { get; set; } = 119;
        public bool HealthBarsVisible { get; set; }
        public int SettingsVersion { get; set; }
        public int AutosaveMinutes { get; set; } = 10;
        public List<BuildingBinding> BuildingKeys { get; set; } = new List<BuildingBinding>();

        public int[] GlobalKeys() { return new[] { QuickSaveKey, QuickLoadKey, SlowerKey, FasterKey, MenuKey, CheatsKey, HealthBarsKey }; }

        public void Validate()
        {
            if (!Policy.ValidSpeed(Speed)) throw new InvalidDataException("Speed must be 0.5, 1, 2, 3, 4 or 5.");
            if (AutosaveMinutes < 0 || AutosaveMinutes > 1440) throw new InvalidDataException("Autosave interval must be 0 (off) or 1 to 1440 minutes.");
            var keys = GlobalKeys();
            if (keys.Any(k => !BindableKey(k)) || keys.Distinct().Count() != keys.Length)
                throw new InvalidDataException("Choose distinct keyboard keys; Enter, Escape, Windows and modifier keys are reserved.");
            if (BuildingKeys == null || BuildingKeys.Any(b => b == null || string.IsNullOrWhiteSpace(b.EntityType) || (b.Key != 0 && !BindableBuildingKey(b.Key))) ||
                BuildingKeys.Select(b => b.EntityType).Distinct().Count() != BuildingKeys.Count)
                throw new InvalidDataException("Invalid building key bindings.");
            if (BuildingKeys.Any(b => b.Key != 0 && keys.Contains(b.Key)))
                throw new InvalidDataException("Building keys must not conflict with QoL keys.");
        }

        public static bool BindableBuildingKey(int key) { return key >= 65 && key <= 90; }

        public static bool BindableKey(int key)
        {
            return key >= 8 && key <= 254 && key != 13 && key != 27 && key != 91 && key != 92 &&
                !(key >= 16 && key <= 18) && !(key >= 160 && key <= 165);
        }

        public static Settings Read(string path)
        {
            if (!File.Exists(path)) return new Settings();
            using (var stream = File.OpenRead(path))
            {
                var settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
                if (settings.SettingsVersion < 3 && settings.MenuKey == 121 &&
                    !new[] { settings.QuickSaveKey, settings.QuickLoadKey, settings.SlowerKey, settings.FasterKey, settings.CheatsKey }.Contains(122))
                    settings.MenuKey = 122;
                if (settings.SettingsVersion < 4)
                {
                    var used = new[] { settings.QuickSaveKey, settings.QuickLoadKey, settings.SlowerKey, settings.FasterKey, settings.MenuKey, settings.CheatsKey }
                        .Concat((settings.BuildingKeys ?? new List<BuildingBinding>()).Where(b => b != null).Select(b => b.Key)).ToArray();
                    if (used.Contains(settings.HealthBarsKey))
                        settings.HealthBarsKey = Enumerable.Range(112, 24).Concat(Enumerable.Range(8, 247))
                            .First(k => BindableKey(k) && !used.Contains(k));
                }
                settings.SettingsVersion = 4;
                settings.Validate();
                return settings;
            }
        }

        public void Write(string path)
        {
            SettingsVersion = 4;
            Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string pending = path + ".new";
            using (var writer = new StreamWriter(pending, false, new UTF8Encoding(false)))
                new XmlSerializer(typeof(Settings)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(pending, path, path + ".bak");
            else File.Move(pending, path);
        }
    }

    public sealed class BuildingBinding
    {
        public string EntityType { get; set; }
        public int Key { get; set; }
    }
}
