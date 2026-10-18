using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
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
                    var used = new HashSet<int>(group.Select(e => Bridge.Config.BuildingKeys.FirstOrDefault(b => b.EntityType == e.Id))
                        .Where(b => b != null && b.Key != 0).Select(b => b.Key));
                    foreach (var entry in group)
                    {
                        var custom = Bridge.Config.BuildingKeys.FirstOrDefault(b => b.EntityType == entry.Id);
                        int preferred = Policy.GridKey((int)Reflect.Get(entry.Command, "PanelPosition"));
                        entry.Key = custom != null ? custom.Key :
                            new[] { preferred }.Concat("QWERTASDFGZXCVBYUIOPHJKLNM".Select(c => (int)c))
                                .FirstOrDefault(k => k != 0 && !used.Contains(k) && !Bridge.Config.GlobalKeys().Contains(k));
                        if (entry.Key != 0) used.Add(entry.Key);
                    }
                }
                return entries;
            }
        }

        internal static void Validate()
        {
            // Auto keys follow the visible grid and yield to global shortcuts
            // and explicit overrides. Only saved overrides reserve keys.
            var custom = Entries.Where(e => Bridge.Config.BuildingKeys.Any(b => b.EntityType == e.Id)).ToArray();
            if (custom.Any(e => e.Key != 0 && Bridge.Config.GlobalKeys().Contains(e.Key)))
                throw new InvalidOperationException("This key is used by a building. Change its building binding first.");
            if (custom.GroupBy(e => e.Category).Any(g => g.Where(e => e.Key != 0).GroupBy(e => e.Key).Any(k => k.Count() > 1)))
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
            ApplyKey(control, SelectedStructure() && (bool)Reflect.Get(control, "Visible") ? KeyFor(control) : 0);
        }

        private static void ApplyKey(object control, int key)
        {
            object hint;
            hints.TryGetValue(control, out hint);
            if (key == 0)
            {
                // The native grid resets keys before repopulating a selection.
                // Do not erase a native unit key assigned during that refresh.
                if (hint != null) Reflect.Set(hint, "Visible", false);
                return;
            }
            Reflect.Set(control, "FixedKey", Enum.ToObject(Reflect.Type("DXVision.DXKeys"), key));
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
            Reflect.Set(hint, "Text", ((char)key).ToString());
            Reflect.Set(hint, "Visible", true);
        }

        private static bool SelectedStructure()
        {
            if (!Bridge.InMission) return false;
            var selected = Reflect.Call(Reflect.Type("ZX.Components.CSelectable"), "#=zBmnjUnslgJaG");
            return selected != null && Reflect.Type("ZX.Entities.Structure").IsInstanceOfType(selected) && Reflect.Get(selected, "Team").ToString() == "Player";
        }

        private static int KeyFor(object control)
        {
            // Back/Cancel uses Escape on key-down so the native key-up handler
            // can suppress opening the game menu. Keep navigation outside the
            // construction grid, including custom bindings such as T.
            if (Convert.ToInt32(Reflect.Get(control, "FixedKey")) == 27) return 0;
            var command = Reflect.Get(control, "Tag");
            var entry = command != null && command.GetType().FullName == "ZX.Commands.Build" ? Entries.FirstOrDefault(e => ReferenceEquals(e.Command, command)) : null;
            if (entry != null && Bridge.Config.BuildingKeys.Any(b => b.EntityType == entry.Id)) return entry.Key;
            var buttons = Buttons(control);
            if (buttons == null) return 0;
            // Every native menu keeps the same physical keyboard rows, including
            // category buttons separated by gaps: QWERT / ASDFG / ZXCVB.
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 5; column++)
                    if (ReferenceEquals(buttons.GetValue(column, row), control))
                    {
                        int key = Policy.GridKey(row * 5 + column + 1);
                        return Bridge.Config.GlobalKeys().Contains(key) || CustomUsesKey(buttons, control, key) ? 0 : key;
                    }
            return 0;
        }

        private static bool CustomUsesKey(Array buttons, object control, int key)
        {
            foreach (var button in buttons)
            {
                if (ReferenceEquals(button, control) || !(bool)Reflect.Get(button, "Visible")) continue;
                var command = Reflect.Get(button, "Tag");
                if (command == null || command.GetType().FullName != "ZX.Commands.Build") continue;
                var entry = Entries.FirstOrDefault(e => ReferenceEquals(e.Command, command));
                if (entry != null && entry.Key == key && Bridge.Config.BuildingKeys.Any(b => b.EntityType == entry.Id)) return true;
            }
            return false;
        }

        private static Array Buttons(object control)
        {
            var parent = Reflect.Get(Reflect.Get(control, "ScnObject"), "Parent");
            var grid = parent == null ? null : Reflect.Get(parent, "Tag");
            return grid == null || grid.GetType().FullName != "ZX.GUI.ZXControlCommandsManager" ? null : (Array)Reflect.Field(grid, "#=zZFf9lzN32EUS");
        }

        internal static bool OnKey(object system, int key)
        {
            if (!Bridge.InMission || !Bridge.Ready || Ui.CaptureKey != null || Bridge.HasModifier || !SelectedStructure() ||
                (bool)Reflect.Get(Reflect.Type("DXVision.GUI.DXModalScreen"), "IsInModal")) return false;
            var grid = Reflect.Field(system, Grid);
            if (grid == null || !(bool)Reflect.Get(Reflect.Get(grid, "ScnObject"), "Visible")) return false;
            var buttons = (Array)Reflect.Field(grid, "#=zZFf9lzN32EUS");
            foreach (var button in buttons)
            {
                if (!(bool)Reflect.Get(button, "Visible") || KeyFor(button) != key) continue;
                // Keyboard and mouse both enter the native activation method.
                // Its shared hook owns group dispatch, so one key buys once.
                if ((bool)Reflect.Get(button, "Enabled"))
                    Reflect.Call(system, "#=zEHnEwyMK7mIMOOClaw==", button);
                return true;
            }
            return false;
        }

        internal static bool OnActivate(object system, object control)
        {
            var command = Reflect.Get(control, "Tag");
            if (command == null || command.GetType().FullName != "ZX.Commands.Train") return false;
            bool group = false;
            try
            {
                var selectable = Reflect.Type("ZX.Components.CSelectable");
                var selection = selectable.GetField(Compatibility.RuntimeSymbol("#=zV9GubOAdTaKJ"), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (selection == null) throw new MissingFieldException(selectable.FullName, "selected entities");
                var structure = Reflect.Type("ZX.Entities.Structure");
                // DXConcurrentHashSet exposes its own locked snapshot method;
                // it does not implement System.Collections.IEnumerable.
                var buildings = Reflect.Items(Reflect.Call(selection.GetValue(null), "ToList")).Select(s => Reflect.Get(s, "Entity"))
                    .Where(e => e != null && structure.IsInstanceOfType(e) && Reflect.Get(e, "Team").ToString() == "Player").Distinct().ToList();
                if (buildings.Count < 2) return false;
                group = true;
                // Train has no target. Match the native completion path by
                // leaving any pending placement or rally-point mode first.
                Reflect.Call(system, "#=zxEeLa$s1cFWH9gUCClxHkJs=");
                var unit = (Type)Reflect.Get(command, "EntityType");
                var target = Activator.CreateInstance(Reflect.Type("ZX.Commands.ZXCommandTarget"));
                foreach (var building in buildings)
                {
                    var commandable = Reflect.Call(building, "#=zd4B080STejtVeVx5odMu2mI=");
                    if (commandable == null) continue;
                    var train = Reflect.Items(Reflect.Get(commandable, "AvailableCommands"))
                        .FirstOrDefault(c => c.GetType().FullName == "ZX.Commands.Train" && Equals(Reflect.Get(c, "EntityType"), unit));
                    // Recheck each building immediately before execution: the
                    // previous purchase may have spent the remaining resources.
                    // These native checks also enforce unlocks, queue limits,
                    // construction/repair state and other unit requirements.
                    if (train == null || !(bool)Reflect.Call(train, "#=zBxvmPXf0pX6L", building) ||
                        !(bool)Reflect.Call(train, "#=ztKqx10CcsHta", building)) continue;
                    Reflect.Call(train, "#=z9be3Bbo=", building, target);
                }
            }
            catch (Exception ex)
            {
                // Never replay the native group activation after a partial
                // purchase, including when every producer is ineligible.
                Bridge.Log(ex);
            }
            finally
            {
                if (group)
                {
                    try { Reflect.Call(system, "#=ze3cy7Z0="); }
                    catch (Exception ex) { Bridge.Log(ex); }
                }
            }
            return group;
        }
    }
}
