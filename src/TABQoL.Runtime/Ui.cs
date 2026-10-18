using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace TABQoL
{
    internal static class Ui
    {
        internal static Action<int> CaptureKey;
        private static object captureButton;
        private static readonly List<object> windows = new List<object>();
        private static readonly Dictionary<object, object> hiddenParents = new Dictionary<object, object>();
        private static object worldControls;
        private static bool reporting;
        private static object cheatsWindow;
        private static object slotsWindow, buildingWindow;
        private static OptionsLayout optionsLayout;
        private sealed class OptionsLayout
        {
            internal object Window, Panel, Content;
            internal object[] Actions, Footers;
            internal bool Attached;
        }
        internal static bool CheatsOpen { get { return cheatsWindow != null && Reflect.Get(cheatsWindow, "Scene") != null; } }
        private static float Unit { get { return Convert.ToSingle(Reflect.Get(Reflect.Type("DXVision.DXScene"), "Unit"), CultureInfo.InvariantCulture); } }

        private static object Label(string text, float width, ContentAlignment alignment = ContentAlignment.TopLeft)
        {
            var font = Reflect.Call(Reflect.Call(Reflect.Type(Compatibility.Fonts), Compatibility.BodyFont), "Clone");
            Reflect.Set(font, "TextAlign", alignment);
            return Reflect.Call(font, "CreateObject", text, width);
        }

        private static void Add(object parent, object child, float x, float y, float width, float height)
        {
            Reflect.Set(child, "Size", new SizeF(width, height));
            Reflect.Set(child, "Location", new PointF(x, y));
            Reflect.Call(parent, "AddObject", child);
        }

        private static object Button(object parent, string text, float x, float y, float width, Action action)
        {
            var button = Reflect.New("ZX.GUI.ZXButtonScreen", text, null);
            var font = Reflect.Call(Reflect.Call(Reflect.Type(Compatibility.Fonts), Compatibility.BodyFont), "Clone");
            Reflect.Set(font, "TextAlign", ContentAlignment.MiddleCenter);
            Reflect.Set(button, "TextRenderer", font);
            Reflect.Set(button, "ColorHue", Color.Gold);
            Add(parent, button, x, y, width, 36 * Unit);
            Reflect.Event(button, "Activated", _ => Bridge.Queue(action));
            return button;
        }

        private static void Check(object parent, string text, float y, float width, bool value, Action<bool> changed, float groupWidth = 0)
        {
            float u = Unit;
            var label = Label(text, width);
            if (groupWidth == 0) groupWidth = Math.Min(width, (float)Reflect.Call(label, "ComputeWidth") + 38 * u);
            float x = (width - groupWidth) / 2;
            var checkbox = Reflect.New("ZX.GUI.ZXCheckBox");
            Reflect.Set(checkbox, "Checked", value);
            Add(parent, checkbox, x, y, 18 * u, 18 * u);
            Add(parent, label, x + 28 * u, y, groupWidth - 28 * u, 26 * u);
            Reflect.Event(checkbox, "CheckedChanged", _ => Bridge.Run(() => changed((bool)Reflect.Get(checkbox, "Checked"))));
        }

        private static object Window(string title, bool compact = false)
        {
            var window = Reflect.New("ZX.GUI.ZXWindow", title, Reflect.EnumValue("ZX.GUI.ZXWindow+TWindowType", compact ? "MessageBox" : "BigWindow"));
            windows.Add(window);
            Reflect.Call(window, Compatibility.CloseButton, (Action)(() => Forget(window)), Reflect.EnumValue("ZX.GUI.ZXWindow+TButtonPosition", "Center"));
            return window;
        }

        private static void Forget(object window)
        {
            windows.Remove(window);
            object parent;
            if (hiddenParents.TryGetValue(window, out parent))
            {
                hiddenParents.Remove(window);
                if (Reflect.Get(parent, "Scene") != null) Reflect.Set(parent, "Visible", true);
            }
            if (ReferenceEquals(cheatsWindow, window)) cheatsWindow = null;
            if (ReferenceEquals(slotsWindow, window)) slotsWindow = null;
            if (ReferenceEquals(buildingWindow, window)) buildingWindow = null;
            CaptureKey = null;
            captureButton = null;
        }

        private static void Close(object window)
        {
            if (window == null) return;
            int index = windows.IndexOf(window);
            if (index >= 0)
                foreach (var child in windows.Skip(index + 1).Reverse().ToArray()) Close(child);
            Reflect.Call(window, Compatibility.CloseWindow, (Action)null);
            Forget(window);
        }

        private static void Show(object window)
        {
            var modal = Reflect.Type("DXVision.GUI.DXModalScreen");
            var parent = (bool)Reflect.Get(modal, "IsInModal") ? Reflect.Get(modal, "CurrentModalObject") : null;
            Reflect.Call(window, Compatibility.ShowWindow, null, Reflect.EnumValue("ZX.ZXWindowAnimationType", "None"));
            // Native windows are translucent. Keep the previous modal alive
            // for navigation and pause restoration, but hide its overlapping UI.
            if (parent != null && (bool)Reflect.Get(parent, "Visible"))
            {
                hiddenParents.Add(window, parent);
                Reflect.Set(parent, "Visible", false);
            }
        }

        internal static void CloseModWindows()
        {
            CaptureKey = null;
            // Unwind newest first so native modal pause restoration reaches the
            // state captured by the root, rather than an intermediate child.
            foreach (var window in windows.AsEnumerable().Reverse().ToArray()) Close(window);
        }

        internal static void CheckKeyCapture()
        {
            UpdateOptionsLayout();
            foreach (var window in windows.Where(w => Reflect.Get(w, "Scene") == null).Reverse().ToArray()) Forget(window);
            if (CaptureKey != null && captureButton != null && Reflect.Get(captureButton, "Scene") == null)
            {
                CaptureKey = null;
                captureButton = null;
            }
        }

        internal static void OpenMenu()
        {
            if (windows.Count > 0) { CloseModWindows(); return; }
            var window = Window("TAB QoL", true);
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width");
            MetalButton(content, "Save / Load", 0, 0, width, () => OpenSlots());
            MetalButton(content, "Speed & hotkeys", 0, 58 * Unit, width, () => OpenSettings());
            MetalButton(content, "Reset technologies", 0, 116 * Unit, width, ConfirmReset);
            MetalButton(content, "Building hotkeys", 0, 174 * Unit, width, () => OpenBuildingKeys());
            MetalButton(content, "Debug cheats", 0, 232 * Unit, width, OpenCheats);
            Add(content, Label("F5: quick save    F9: quick load    [ / ]: speed\nF11: this menu    F12: debug cheats (default keys)", width, ContentAlignment.TopCenter), 0, 294 * Unit, width, 48 * Unit);
            Show(window);
        }

        internal static void OpenSlots(int page = 0)
        {
            Bridge.RequireLoadedMission();
            Close(slotsWindow);
            var window = Window("Save / Load - " + (page == 2 ? "Autosaves" : "Manual " + (page + 1) + " / 2"));
            slotsWindow = window;
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width");
            string scope = Bridge.Scope();
            float u = Unit;
            for (int tab = 0; tab < 3; tab++)
            {
                int target = tab;
                MetalButton(content, new[] { "Manual 1 - 5", "Manual 6 - 10", "Autosaves" }[tab], tab * width / 3, 0, width / 3, () => OpenSlots(target));
            }
            var slots = page == 2 ? Enumerable.Range(1, 5).Select(i => "autosave" + i).ToArray() :
                new[] { "quick" }.Concat(Enumerable.Range(page * 5 + 1, 5).Select(i => "slot" + i)).Concat(new[] { "before-cheat-win" }).ToArray();
            var sample = SecondaryButton("Save");
            float buttonWidth = (float)Reflect.Get(sample, "Width");
            var buttonFace = (RectangleF)Reflect.Get(Reflect.Get(sample, "OBar"), "Area");
            float groupX = width - buttonWidth * 2 - 6 * u;
            for (int i = 0; i < slots.Length; i++)
            {
                string slot = slots[i];
                var snapshot = Bridge.Store.Read(scope, slot);
                string caption = slot + (snapshot == null ? " (empty)" : "  " + snapshot.CreatedUtc.ToLocalTime().ToString("g"));
                float y = (60 + i * 52) * u;
                var label = Label(caption, groupX - 8 * u, ContentAlignment.MiddleLeft);
                float labelHeight = (float)Reflect.Call(label, "ComputeHeight", groupX - 8 * u);
                Add(content, label, 0, y + buttonFace.Y + (buttonFace.Height - labelHeight) / 2, groupX - 8 * u, labelHeight);
                var load = MetalButton(content, "Load", groupX + buttonWidth + 6 * u, y, buttonWidth, () => Bridge.Load(slot));
                Reflect.Set(load, "Enabled", snapshot != null);
                if (page != 2 && slot != "before-cheat-win") MetalButton(content, "Save", groupX, y, buttonWidth, () =>
                {
                    Bridge.Save(slot);
                    Reflect.Set(label, "Text", slot + "  " + DateTime.Now.ToString("g"));
                    Reflect.Set(load, "Enabled", true);
                });
            }
            if (page == 2) Add(content, Label("Five rotating autosaves. Interval: " + (Bridge.Config.AutosaveMinutes == 0 ? "off" : Bridge.Config.AutosaveMinutes + " min of active play") + ".\nChange the interval in Options > TAB QoL.", width, ContentAlignment.TopCenter), 0, 334 * u, width, 60 * u);
            Show(window);
        }

        internal static void OpenSettings()
        {
            var window = Window("TAB QoL - settings");
            PopulateSettings(Reflect.Get(window, "OContent"));
            Show(window);
        }

        internal static void AddOptions(object window)
        {
            var page = Reflect.Call(window, Compatibility.AddTab, "TAB QoL");
            var panel = Reflect.Get(page, "Panel");
            var content = Reflect.Get(panel, "Content");
            optionsLayout = new OptionsLayout
            {
                Window = window,
                Panel = panel,
                Content = content,
                Actions = PopulateSettings(content, Convert.ToSingle(Reflect.Get(panel, "ContentWidth"), CultureInfo.InvariantCulture)),
                Footers = Reflect.Type("ZX.GUI.ZXWindow").GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType.FullName == "ZX.GUI.ZXButtonSteam").Select(f => f.GetValue(window))
                .Where(b => b != null).ToArray()
            };
            Reflect.Call(panel, "Update");
        }

        private static void UpdateOptionsLayout()
        {
            var layout = optionsLayout;
            if (layout == null) return;
            if (Reflect.Get(layout.Window, "Scene") == null)
            {
                if (layout.Attached) optionsLayout = null;
                return;
            }
            layout.Attached = true;
            // A newly created tab is hidden until selected. Keep checking its
            // final scene geometry, including later tab switches and resizing.
            if (!(bool)Reflect.Get(layout.Window, "Visible") || !(bool)Reflect.Get(layout.Panel, "Visible") ||
                Reflect.Get(layout.Content, "Scene") == null) return;
            var footers = layout.Footers.Where(b => (bool)Reflect.Get(b, "Visible"))
                .OrderBy(b => OffsetX(b, layout.Window)).ToArray();
            if (footers.Length < 2) return;
            float origin = OffsetX(layout.Content, layout.Window);
            bool changed = false;
            for (int i = 0; i < layout.Actions.Length; i++)
            {
                // The two rightmost visible native footer buttons are Cancel
                // and Apply; an optional left footer does not affect alignment.
                var footer = footers[footers.Length - 2 + i % 2];
                float center = OffsetX(footer, layout.Window) + (float)Reflect.Get(footer, "Width") / 2 - origin;
                float x = center - (float)Reflect.Get(layout.Actions[i], "Width") / 2;
                if (Math.Abs((float)Reflect.Get(layout.Actions[i], "X") - x) < 0.1f) continue;
                Reflect.Set(layout.Actions[i], "X", x);
                changed = true;
            }
            if (changed) Reflect.Call(layout.Panel, "Update");
        }

        private static float OffsetX(object control, object ancestor)
        {
            float x = 0;
            for (int i = 0; control != null && !ReferenceEquals(control, ancestor) && i < 20; i++)
            {
                x += (float)Reflect.Get(control, "X");
                control = Reflect.Get(control, "Parent");
            }
            return x;
        }

        private static object[] PopulateSettings(object content, float width = 0)
        {
            if (width == 0) width = (float)Reflect.Get(content, "Width");
            float u = Unit;
            const float rowHeight = 34;
            float fieldX = width * 0.5f, fieldWidth = width * 0.48f, controlHeight = 22 * u;
            Add(content, Label("Simulation speed", width * 0.48f, ContentAlignment.MiddleLeft), 0, 0, width * 0.48f, controlHeight);
            var speedBox = Reflect.New("ZX.GUI.ZXComboBox");
            Reflect.Set(speedBox, "Items", Policy.Speeds.Select(s => s.ToString("0.#", CultureInfo.InvariantCulture) + "x").ToList());
            Reflect.Set(speedBox, "SelectedIndex", Array.IndexOf(Policy.Speeds, Bridge.Config.Speed));
            Add(content, speedBox, fieldX, 0, fieldWidth, controlHeight);
            Reflect.Event(speedBox, "SelectedIndexChanged", _ => Bridge.Run(() =>
            {
                int selected = (int)Reflect.Get(speedBox, "SelectedIndex");
                if (selected >= 0 && selected < Policy.Speeds.Length) Bridge.SetSpeed(Policy.Speeds[selected]);
            }));
            string[] names = { "Quick save", "Quick load", "Slower", "Faster", "QoL menu", "Debug cheats" };
            Func<int>[] getters = { () => Bridge.Config.QuickSaveKey, () => Bridge.Config.QuickLoadKey, () => Bridge.Config.SlowerKey, () => Bridge.Config.FasterKey, () => Bridge.Config.MenuKey, () => Bridge.Config.CheatsKey };
            Action<int>[] setters = { k => Bridge.Config.QuickSaveKey = k, k => Bridge.Config.QuickLoadKey = k, k => Bridge.Config.SlowerKey = k, k => Bridge.Config.FasterKey = k, k => Bridge.Config.MenuKey = k, k => Bridge.Config.CheatsKey = k };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float y = (i + 1) * rowHeight * u;
                Add(content, Label(names[i], width * 0.48f, ContentAlignment.MiddleLeft), 0, y, width * 0.48f, controlHeight);
                object button = null;
                button = Button(content, KeyName(getters[i]()), fieldX, y, fieldWidth, () =>
                {
                    Reflect.Set(button, "Text", "Press a key (Esc: cancel)");
                    captureButton = button;
                    CaptureKey = key =>
                    {
                        if (key != 27 && !Settings.BindableKey(key)) return;
                        CaptureKey = null;
                        captureButton = null;
                        int previous = getters[index]();
                        if (key != 27)
                        {
                            setters[index](key);
                            try { Bridge.PersistSettings(); }
                            catch (Exception ex) { setters[index](previous); Bridge.Report(ex); }
                        }
                        Reflect.Set(button, "Text", KeyName(getters[index]()));
                    };
                });
                Reflect.Set(button, "Height", controlHeight);
            }
            float autosaveY = rowHeight * 7 * u;
            Add(content, Label("Autosave interval (active play)", width * 0.48f, ContentAlignment.MiddleLeft), 0, autosaveY, width * 0.48f, controlHeight);
            var autosaveBox = Reflect.New("ZX.GUI.ZXComboBox");
            var intervals = new[] { 0 }.Concat(Enumerable.Range(1, 12).Select(i => i * 5)).ToList();
            // Keep an existing custom interval visible until the player chooses
            // a preset. Merely opening settings must not rewrite preferences.
            if (!intervals.Contains(Bridge.Config.AutosaveMinutes)) intervals.Add(Bridge.Config.AutosaveMinutes);
            Reflect.Set(autosaveBox, "Items", intervals.Select(m => m == 0 ? "Off" : m + " min").ToList());
            Reflect.Set(autosaveBox, "SelectedIndex", intervals.IndexOf(Bridge.Config.AutosaveMinutes));
            Add(content, autosaveBox, fieldX, autosaveY, fieldWidth, controlHeight);
            Reflect.Event(autosaveBox, "SelectedIndexChanged", _ => Bridge.Run(() =>
            {
                int selected = (int)Reflect.Get(autosaveBox, "SelectedIndex");
                if (selected < 0 || selected >= intervals.Count) return;
                int previous = Bridge.Config.AutosaveMinutes;
                Bridge.Config.AutosaveMinutes = intervals[selected];
                try { Bridge.PersistSettings(); }
                catch { Bridge.Config.AutosaveMinutes = previous; throw; }
            }));
            float[] actionCenters = { width * 0.28f, width * 0.72f };
            float actionWidth = Math.Min(width * 0.46f, 190 * u);
            return new[] {
                MetalButton(content, "Building hotkeys", actionCenters[0] - actionWidth / 2, 286 * u, actionWidth, () => OpenBuildingKeys()),
                MetalButton(content, "Debug cheats", actionCenters[1] - actionWidth / 2, 286 * u, actionWidth, OpenCheats),
                MetalButton(content, "Save / Load", actionCenters[0] - actionWidth / 2, 338 * u, actionWidth, () => OpenSlots()),
                MetalButton(content, "Reset technologies", actionCenters[1] - actionWidth / 2, 338 * u, actionWidth, ConfirmReset)
            };
        }

        private static string KeyName(int key)
        {
            if (key == 0) return "Unbound";
            if (key == 219) return "[";
            if (key == 221) return "]";
            return ((Keys)key).ToString();
        }

        private static string BuildingKeyName(BuildingHotkeys.Entry entry)
        {
            return Bridge.Config.BuildingKeys.Any(b => b.EntityType == entry.Id) ? KeyName(entry.Key) : "Auto";
        }

        internal static void OpenBuildingKeys(int page = 0)
        {
            Close(buildingWindow);
            var entries = BuildingHotkeys.Entries;
            int pages = Math.Max(1, (entries.Count + 7) / 8);
            page = Math.Max(0, Math.Min(page, pages - 1));
            var window = Window("Building hotkeys - " + (page + 1) + " / " + pages);
            buildingWindow = window;
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width"), u = Unit;
            Add(content, Label("Auto follows icon rows: QWERT / ASDFG / ZXCVB.\nA-Z: assign key; Backspace: unbind; Esc: cancel.\nCustom keys can repeat in different categories.", width, ContentAlignment.TopCenter), 0, 0, width, 80 * u);
            int row = 0;
            foreach (var entry in entries.Skip(page * 8).Take(8))
            {
                float y = (88 + row++ * 36) * u;
                Add(content, Label(entry.Category + ": " + entry.Name, width * 0.72f), 0, y, width * 0.72f, 34 * u);
                object button = null;
                button = Button(content, BuildingKeyName(entry), width * 0.75f, y, width * 0.23f, () =>
                {
                    Reflect.Set(button, "Text", "Key?");
                    captureButton = button;
                    CaptureKey = key =>
                    {
                        if (key != 27 && key != 8 && !Settings.BindableBuildingKey(key)) return;
                        CaptureKey = null;
                        captureButton = null;
                        if (key != 27)
                        {
                            try { BuildingHotkeys.Bind(entry, key == 8 ? 0 : key); }
                            catch (Exception ex) { Bridge.Report(ex); }
                        }
                        Reflect.Set(button, "Text", BuildingKeyName(entry));
                    };
                });
            }
            if (page > 0) Button(content, "Previous", 0, 390 * u, width * 0.48f, () => OpenBuildingKeys(page - 1));
            if (page + 1 < pages) Button(content, "Next", width * 0.5f, 390 * u, width * 0.48f, () => OpenBuildingKeys(page + 1));
            Show(window);
        }

        internal static void OpenCheats()
        {
            if (CheatsOpen) { Close(cheatsWindow); return; }
            Bridge.RequireMission();
            cheatsWindow = Window("TAB QoL - debug cheats");
            var content = Reflect.Get(cheatsWindow, "OContent");
            float width = (float)Reflect.Get(content, "Width"), u = Unit;
            Add(content, Label("Debug actions affect the current mission.\nProtection switches reset after loading or changing missions.", width, ContentAlignment.TopCenter), 0, 0, width, 58 * u);
            Check(content, "Immortal player units (including hero)", 72 * u, width, Cheats.Units, Cheats.SetUnits, 300 * u);
            Check(content, "Invulnerable player buildings", 112 * u, width, Cheats.Buildings, Cheats.SetBuildings, 300 * u);
            MetalButton(content, "Heal player units", 0, 164 * u, width, Cheats.Heal);
            MetalButton(content, "Fill resources", 0, 216 * u, width, Cheats.FillResources);
            MetalButton(content, "Win mission...", 0, 268 * u, width, ConfirmWin);
            Add(content, Label("Resource and victory changes can persist in saves.\nF12 closes this panel (default key).", width, ContentAlignment.TopCenter), 0, 336 * u, width, 58 * u);
            Show(cheatsWindow);
        }

        private static void ConfirmWin()
        {
            var window = Window("Win current mission?");
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width");
            Add(content, Label("Finish this mission through the normal victory sequence.\nThe mod saves a separate before-cheat-win snapshot first.\nCampaign progress and rewards follow the native result screen.", width, ContentAlignment.TopCenter), 0, 0, width, 120 * Unit);
            Button(content, "Win mission", 0, 140 * Unit, width, Cheats.Win);
            Show(window);
        }

        internal static void ConfirmReset()
        {
            if (!Bridge.OnWorld) throw new InvalidOperationException("Open the campaign world map to reset technologies.");
            var window = Window("Reset technologies?", true);
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width");
            Add(content, Label("Refund all paid technologies and keep the starting technologies.\nA backup will be created before the reset.", width, ContentAlignment.TopCenter), 0, 0, width, 60 * Unit);
            CampaignButton(content, "Reset", 78 * Unit, width, () => { Bridge.ResetResearch(); Close(window); });
            Show(window);
        }

        internal static void AddWorldControls(object world)
        {
            RemoveWorldControls();
            float u = Unit;
            var hud = Reflect.Field(world, "#=zELX0oyIBb7sj");
            var menu = Reflect.Items(Reflect.Get(hud, "DescendantsZAscending"))
                .Single(o => o.GetType().FullName == "ZX.GUI.ZXButtonImage" &&
                    Reflect.Items(Reflect.Field(o, "#=zlFNeqMQ=")).Any(k => Convert.ToInt32(k, CultureInfo.InvariantCulture) == 27));
            var sections = (IDictionary)Reflect.Field(world, "#=zY2B0_ig=");
            var firstTab = sections.Values.Cast<object>().Select(s => Reflect.Get(s, "Button"))
                .OrderBy(b => (float)Reflect.Get(b, "X")).First();
            var tabArea = (RectangleF)Reflect.Get(firstTab, "Area");
            worldControls = Reflect.New("DXVision.DXObject");
            Reflect.Set(worldControls, "ID", "TABQoL.WorldControls");
            Reflect.Set(worldControls, "Size", new SizeF(64 * u, 64 * u));
            Reflect.Set(worldControls, "Location", new PointF(tabArea.Left - 102 * u, tabArea.Top + (tabArea.Height - 64 * u) / 2));
            Reflect.Set(worldControls, "Z", 1000000);
            var frame = Reflect.New("DXVision.DXObject");
            Reflect.Set(frame, "Z", -1);
            Reflect.Set(frame, "MouseClickInteractionEnabled", false);
            var frameImage = Reflect.Call(Reflect.Type("#=zgTbz09XqLxYj+#=zz8951q8="), "#=zC92uNhZ_knxtSrhV$Q==");
            Reflect.Call(frameImage, "SetImageFor", frame);
            Add(worldControls, frame, 0, 0, 64 * u, 64 * u);
            var keys = Array.CreateInstance(Reflect.Type("DXVision.DXKeys"), 0);
            var button = Reflect.New("ZX.GUI.ZXButtonImage", menu, keys);
            Reflect.Set(button, "ID", "TABQoL.CampaignButton");
            Reflect.Set(button, "Z", 1);
            Reflect.Set(button, "Tag", Reflect.New("ZX.ZXHelp", "TAB QoL", "Campaign options, research reset and mod settings.", null, null, null, null));
            Add(worldControls, button, 8 * u, 8 * u, 48 * u, 48 * u);
            Reflect.Event(button, "Activated", _ => Bridge.Queue(OpenCampaignOptions));
            Reflect.Call(Reflect.Get(firstTab, "Parent"), "AddObject", worldControls);
        }

        internal static void OpenCampaignOptions()
        {
            if (!Bridge.OnWorld || !Bridge.Ready) return;
            var window = Window("TAB QoL - Campaign", true);
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width"), u = Unit;
            CampaignButton(content, "Reset technologies", 0, width, ConfirmReset);
            CampaignButton(content, "Mod settings", 58 * u, width, OpenSettings);
            Check(content, "Skip hero missions (all rewards)", 138 * u, width, Bridge.Config.SkipHeroMissions, value =>
            {
                Bridge.Config.SkipHeroMissions = value;
                Bridge.PersistSettings();
            });
            Show(window);
        }

        private static void CampaignButton(object content, string text, float y, float width, Action action)
        {
            MetalButton(content, text, 0, y, width, action);
        }

        private static object MetalButton(object content, string text, float x, float y, float width, Action action)
        {
            var button = SecondaryButton(text);
            Reflect.Set(button, "Location", new PointF(x + (width - (float)Reflect.Get(button, "Width")) / 2, y));
            Reflect.Call(content, "AddObject", button);
            Reflect.Event(button, "Activated", _ => Bridge.Queue(action));
            return button;
        }

        private static object SecondaryButton(string text)
        {
            var button = Reflect.New("ZX.GUI.ZXButtonSteam", text, null);
            Reflect.Set(button, "UseSmallFontStyle", true);
            Reflect.Set(button, "SupportsVisible", false);
            var size = (SizeF)Reflect.Get(button, "Size");
            Reflect.Call(button, "SetSizeReScalingChildren", new SizeF(size.Width * 0.86f, size.Height * 0.86f));
            // The tiny side tips are baked into the native background image.
            // Trim those edges while retaining the brass caps and top loop.
            float trim = (float)Reflect.Get(button, "Width") * 0.07f;
            foreach (var child in Reflect.Items(Reflect.Get(button, "Children")))
                Reflect.Set(child, "X", (float)Reflect.Get(child, "X") - trim);
            Reflect.Set(button, "Width", (float)Reflect.Get(button, "Width") - 2 * trim);
            Reflect.Set(button, "Clip", true);
            return button;
        }

        internal static void RemoveWorldControls()
        {
            if (worldControls == null) return;
            Reflect.Call(worldControls, "RemoveFromScene");
            worldControls = null;
        }

        internal static void Error(string text)
        {
            if (reporting) return;
            reporting = true;
            try
            {
                var window = Window("TAB QoL");
                var content = Reflect.Get(window, "OContent");
                float width = (float)Reflect.Get(content, "Width");
                Add(content, Label(text, width), 0, 0, width, 160 * Unit);
                Show(window);
            }
            catch (Exception ex) { Bridge.Log(ex); }
            finally { reporting = false; }
        }
    }
}
