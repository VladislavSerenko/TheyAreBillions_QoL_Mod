using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TABQoL
{
    internal static class BuildingHotkeys
    {
        internal sealed class Entry
        {
            internal object Command;
            internal string Id, Name, Category;
            internal int Key;
        }
        private static List<Entry> entries;
        private static readonly ConditionalWeakTable<object, object> hints = new ConditionalWeakTable<object, object>();
        private const string Grid = "#=zK$ZAIPBoZfgY";

        internal static List<Entry> Entries
        {
            get
            {
                if (entries != null) return entries;
                var structure = Reflect.Type("ZX.Entities.Structure");
                entries = Reflect.Items(Reflect.Field(Bridge.Game, "#=zDB6tDoo="))
                    .Where(c => c.GetType().FullName == "ZX.Commands.Build" && structure.IsAssignableFrom((Type)Reflect.Get(c, "EntityType")))
                    .Where(c => Reflect.Get(c, "Entity") != null)
                    .Where(c => Reflect.Get(c, "Category").ToString() != "None" && (int)Reflect.Get(c, "PanelPosition") > 0)
                    .Select(c => new Entry { Command = c, Id = ((Type)Reflect.Get(c, "EntityType")).FullName,
                        Name = (string)Reflect.Call(c, "#=zEb$WviU=", (object)null), Category = Reflect.Get(c, "Category").ToString() })
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .OrderBy(e => e.Category).ThenBy(e => (int)Reflect.Get(e.Command, "PanelPosition")).ThenBy(e => e.Id).ToList();
                foreach (var group in entries.GroupBy(e => e.Category))
                {
                    var used = new HashSet<int>();
                    foreach (var entry in group.OrderBy(e => e.Id.EndsWith(".TentHouse", StringComparison.Ordinal) ? 0 : 1))
                    {
                        var custom = Bridge.Config.BuildingKeys.FirstOrDefault(b => b.EntityType == entry.Id);
                        // The remaining keys follow the command grid, with T reserved for the tent.
                        int preferred = entry.Id.EndsWith(".TentHouse", StringComparison.Ordinal) ? 84 :
                            "QWERTASDFGZXCVBYUIOPHJKLNM"[((int)Reflect.Get(entry.Command, "PanelPosition") - 1) % 26];
                        entry.Key = custom != null ? custom.Key :
                            new[] { preferred }.Concat("QWERTASDFGZXCVBYUIOPHJKLNM".Select(c => (int)c))
                                .FirstOrDefault(k => !used.Contains(k) && !Bridge.Config.GlobalKeys().Contains(k));
                        if (entry.Key != 0) used.Add(entry.Key);
                    }
                }
                return entries;
            }
        }

        internal static void Validate()
        {
            if (Entries.Any(e => e.Key != 0 && Bridge.Config.GlobalKeys().Contains(e.Key)))
                throw new InvalidOperationException("This key is used by a building. Change its building binding first.");
            if (Entries.GroupBy(e => e.Category).Any(g => g.Where(e => e.Key != 0).GroupBy(e => e.Key).Any(k => k.Count() > 1)))
                throw new InvalidOperationException("Choose different keys within each building category.");
        }

        internal static void Bind(Entry entry, int key)
        {
            int previous = entry.Key;
            var old = Bridge.Config.BuildingKeys.Select(b => new BuildingBinding { EntityType = b.EntityType, Key = b.Key }).ToList();
            entry.Key = key;
            Bridge.Config.BuildingKeys.RemoveAll(b => b.EntityType == entry.Id);
            Bridge.Config.BuildingKeys.Add(new BuildingBinding { EntityType = entry.Id, Key = key });
            try { Validate(); Bridge.PersistSettings(); }
            catch { entry.Key = previous; Bridge.Config.BuildingKeys = old; throw; }
            Refresh();
        }

        internal static void Refresh()
        {
            if (Bridge.InMission) Reflect.Call(Bridge.System, "#=ze3cy7Z0=");
        }

        internal static void UpdateControl(object control)
        {
            if (Bridge.Config == null) return;
            var command = Reflect.Get(control, "Tag");
            var entry = command != null && command.GetType().FullName == "ZX.Commands.Build" ? Entries.FirstOrDefault(e => ReferenceEquals(e.Command, command)) : null;
            object hint;
            hints.TryGetValue(control, out hint);
            if (entry == null)
            {
                if (hint != null) Reflect.Set(hint, "Visible", false);
                return;
            }
            Reflect.Set(control, "FixedKey", Enum.ToObject(Reflect.Type("DXVision.DXKeys"), entry.Key));
            if (hint == null)
            {
                var sceneObject = Reflect.Get(control, "ScnObject");
                float width = (float)Reflect.Get(sceneObject, "Width");
                var font = Reflect.Call(Reflect.Type(Compatibility.Fonts), Compatibility.BodyFont);
                font = Reflect.Call(font, "Clone");
                Reflect.Set(font, "TextAlign", ContentAlignment.TopRight);
                hint = Reflect.Call(font, "CreateObject", "", width);
                Reflect.Set(hint, "Size", new SizeF(width - 4, 28));
                Reflect.Set(hint, "Location", new PointF(0, 0));
                Reflect.Set(hint, "Z", 1000);
                Reflect.Call(sceneObject, "AddObject", hint);
                hints.Add(control, hint);
            }
            Reflect.Set(hint, "Text", entry.Key == 0 ? "" : ((char)entry.Key).ToString());
            Reflect.Set(hint, "Visible", (bool)Reflect.Get(control, "Visible") && entry.Key != 0);
        }

        internal static bool OnKey(object system, int key)
        {
            if (!Bridge.InMission || !Bridge.Ready || Ui.CaptureKey != null || Bridge.HasModifier ||
                (bool)Reflect.Get(Reflect.Type("DXVision.GUI.DXModalScreen"), "IsInModal")) return false;
            var grid = Reflect.Field(system, Grid);
            if (grid == null || !(bool)Reflect.Get(Reflect.Get(grid, "ScnObject"), "Visible")) return false;
            var buttons = (Array)Reflect.Field(grid, "#=zZFf9lzN32EUS");
            foreach (var button in buttons)
            {
                var command = Reflect.Get(button, "Tag");
                if (command == null || command.GetType().FullName != "ZX.Commands.Build" || !(bool)Reflect.Get(button, "Visible")) continue;
                var entry = Entries.FirstOrDefault(e => ReferenceEquals(e.Command, command));
                if (entry == null || entry.Key != key) continue;
                if ((bool)Reflect.Get(button, "Enabled")) Reflect.Call(system, "#=zEHnEwyMK7mIMOOClaw==", button);
                return true;
            }
            return false;
        }
    }
}
