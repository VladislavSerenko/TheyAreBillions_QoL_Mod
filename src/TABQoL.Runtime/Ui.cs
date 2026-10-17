using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace TABQoL
{
    internal static class Ui
    {
        internal static Action<int> CaptureKey;
        private static object captureButton;
        private static readonly List<object> windows = new List<object>();
        private static object worldControls;
        private static bool reporting;
        private static object cheatsWindow;
        internal static bool CheatsOpen { get { return cheatsWindow != null && Reflect.Get(cheatsWindow, "Scene") != null; } }
        private static float Unit { get { return Convert.ToSingle(Reflect.Get(Reflect.Type("DXVision.DXScene"), "Unit"), CultureInfo.InvariantCulture); } }

        private static object Label(string text, float width)
        {
            var font = Reflect.Call(Reflect.Type(Compatibility.Fonts), Compatibility.BodyFont);
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

        private static void Check(object parent, string text, float y, float width, bool value, Action<bool> changed)
        {
            var checkbox = Reflect.New("ZX.GUI.ZXCheckBox");
            Reflect.Set(checkbox, "Checked", value);
            Add(parent, checkbox, 0, y, 25 * Unit, 25 * Unit);
            Add(parent, Label(text, width - 38 * Unit), 38 * Unit, y, width - 38 * Unit, 34 * Unit);
            Reflect.Event(checkbox, "CheckedChanged", _ => Bridge.Run(() => changed((bool)Reflect.Get(checkbox, "Checked"))));
        }

        private static object Window(string title, bool compact = false)
        {
            var window = Reflect.New("ZX.GUI.ZXWindow", title, Reflect.EnumValue("ZX.GUI.ZXWindow+TWindowType", compact ? "MessageBox" : "BigWindow"));
            windows.Add(window);
            Reflect.Call(window, Compatibility.CloseButton, (Action)(() => { windows.Remove(window); CaptureKey = null; }), Reflect.EnumValue("ZX.GUI.ZXWindow+TButtonPosition", "Center"));
            return window;
        }

        private static void Show(object window)
        {
            Reflect.Call(window, Compatibility.ShowWindow, null, Reflect.EnumValue("ZX.ZXWindowAnimationType", "None"));
        }

        internal static void CloseModWindows()
        {
            CaptureKey = null;
            cheatsWindow = null;
            foreach (var window in windows.ToArray()) Reflect.Call(window, Compatibility.CloseWindow, (Action)null);
            windows.Clear();
        }

        internal static void CheckKeyCapture()
        {
            windows.RemoveAll(w => Reflect.Get(w, "Scene") == null);
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
            Add(content, Label("F5: quick save    F9: quick load    [ / ]: speed\nF11: this menu    F12: debug cheats (default keys)", width), 0, 294 * Unit, width, 60 * Unit);
            Show(window);
        }

        internal static void OpenSlots(int page = 0)
        {
            Bridge.RequireLoadedMission();
            CloseModWindows();
            var window = Window("Save / Load - " + (page == 2 ? "Autosaves" : "Manual " + (page + 1) + " / 2"));
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
            var sample = Reflect.New("ZX.GUI.ZXButtonSteam", "Save", null);
            float buttonWidth = (float)Reflect.Get(sample, "Width");
            float buttonHeight = (float)Reflect.Get(sample, "Height");
            float groupX = width - buttonWidth * 2 - 6 * u;
            for (int i = 0; i < slots.Length; i++)
            {
                string slot = slots[i];
                var snapshot = Bridge.Store.Read(scope, slot);
                string caption = slot + (snapshot == null ? " (empty)" : "  " + snapshot.CreatedUtc.ToLocalTime().ToString("g"));
                float y = (60 + i * 52) * u;
                var label = Label(caption, groupX - 8 * u);
                Add(content, label, 0, y + (buttonHeight - 14 * u) / 2, groupX - 8 * u, 40 * u);
                var load = MetalButton(content, "Load", groupX + buttonWidth + 6 * u, y, buttonWidth, () => Bridge.Load(slot));
                Reflect.Set(load, "Enabled", snapshot != null);
                if (page != 2 && slot != "before-cheat-win") MetalButton(content, "Save", groupX, y, buttonWidth, () =>
                {
                    Bridge.Save(slot);
                    Reflect.Set(label, "Text", slot + "  " + DateTime.Now.ToString("g"));
                    Reflect.Set(load, "Enabled", true);
                });
            }
            if (page == 2) Add(content, Label("Five rotating autosaves. Interval: " + (Bridge.Config.AutosaveMinutes == 0 ? "off" : Bridge.Config.AutosaveMinutes + " min of active play") + ".\nChange the interval in Options > TAB QoL.", width), 0, 334 * u, width, 60 * u);
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
            PopulateSettings(Reflect.Get(panel, "Content"), Convert.ToSingle(Reflect.Get(panel, "ContentWidth"), CultureInfo.InvariantCulture));
        }

        private static void PopulateSettings(object content, float width = 0)
        {
            if (width == 0) width = (float)Reflect.Get(content, "Width");
            float u = Unit;
            Add(content, Label("Simulation speed", width * 0.48f), 0, 0, width * 0.48f, 36 * u);
            var speedBox = Reflect.New("ZX.GUI.ZXComboBox");
            Reflect.Set(speedBox, "Items", Policy.Speeds.Select(s => s.ToString("0.#", CultureInfo.InvariantCulture) + "x").ToList());
            Reflect.Set(speedBox, "SelectedIndex", Array.IndexOf(Policy.Speeds, Bridge.Config.Speed));
            Add(content, speedBox, width * 0.5f, 0, width * 0.48f, 36 * u);
            Reflect.Event(speedBox, "SelectedIndexChanged", _ => Bridge.Run(() => Bridge.SetSpeed(Policy.Speeds[(int)Reflect.Get(speedBox, "SelectedIndex")])));
            string[] names = { "Quick save", "Quick load", "Slower", "Faster", "QoL menu", "Debug cheats" };
            Func<int>[] getters = { () => Bridge.Config.QuickSaveKey, () => Bridge.Config.QuickLoadKey, () => Bridge.Config.SlowerKey, () => Bridge.Config.FasterKey, () => Bridge.Config.MenuKey, () => Bridge.Config.CheatsKey };
            Action<int>[] setters = { k => Bridge.Config.QuickSaveKey = k, k => Bridge.Config.QuickLoadKey = k, k => Bridge.Config.SlowerKey = k, k => Bridge.Config.FasterKey = k, k => Bridge.Config.MenuKey = k, k => Bridge.Config.CheatsKey = k };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float y = (46 + i * 38) * u;
                Add(content, Label(names[i], width * 0.48f), 0, y, width * 0.48f, 34 * u);
                object button = null;
                button = Button(content, KeyName(getters[i]()), width * 0.5f, y, width * 0.48f, () =>
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
            }
            Add(content, Label("Autosave interval (active play)", width * 0.48f), 0, 280 * u, width * 0.48f, 34 * u);
            var autosaveBox = Reflect.New("ZX.GUI.ZXComboBox");
            Reflect.Set(autosaveBox, "Items", Enumerable.Range(0, 1441).Select(m => m == 0 ? "Off" : m + " min").ToList());
            Reflect.Set(autosaveBox, "SelectedIndex", Bridge.Config.AutosaveMinutes);
            Add(content, autosaveBox, width * 0.5f, 280 * u, width * 0.48f, 34 * u);
            Reflect.Event(autosaveBox, "SelectedIndexChanged", _ => Bridge.Run(() =>
            {
                int previous = Bridge.Config.AutosaveMinutes;
                Bridge.Config.AutosaveMinutes = (int)Reflect.Get(autosaveBox, "SelectedIndex");
                try { Bridge.PersistSettings(); }
                catch { Bridge.Config.AutosaveMinutes = previous; throw; }
            }));
            MetalButton(content, "Building hotkeys", 0, 332 * u, width * 0.5f, () => OpenBuildingKeys());
            MetalButton(content, "Debug cheats", width * 0.5f, 332 * u, width * 0.5f, OpenCheats);
            MetalButton(content, "Save / Load", 0, 390 * u, width * 0.5f, () => OpenSlots());
            MetalButton(content, "Reset technologies", width * 0.5f, 390 * u, width * 0.5f, ConfirmReset);
        }

        private static string KeyName(int key)
        {
            if (key == 0) return "Unbound";
            if (key == 219) return "[";
            if (key == 221) return "]";
            return ((Keys)key).ToString();
        }

        internal static void OpenBuildingKeys(int page = 0)
        {
            CloseModWindows();
            var entries = BuildingHotkeys.Entries;
            int pages = Math.Max(1, (entries.Count + 7) / 8);
            page = Math.Max(0, Math.Min(page, pages - 1));
            var window = Window("Building hotkeys - " + (page + 1) + " / " + pages);
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width"), u = Unit;
            Add(content, Label("Select the command center and open a building category.\nA-Z: assign key; Backspace: unbind; Esc: cancel.\nKeys can repeat in different categories.", width), 0, 0, width, 80 * u);
            int row = 0;
            foreach (var entry in entries.Skip(page * 8).Take(8))
            {
                float y = (88 + row++ * 36) * u;
                Add(content, Label(entry.Category + ": " + entry.Name, width * 0.72f), 0, y, width * 0.72f, 34 * u);
                object button = null;
                button = Button(content, KeyName(entry.Key), width * 0.75f, y, width * 0.23f, () =>
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
                        Reflect.Set(button, "Text", KeyName(entry.Key));
                    };
                });
            }
            if (page > 0) Button(content, "Previous", 0, 390 * u, width * 0.48f, () => OpenBuildingKeys(page - 1));
            if (page + 1 < pages) Button(content, "Next", width * 0.5f, 390 * u, width * 0.48f, () => OpenBuildingKeys(page + 1));
            Show(window);
        }

        internal static void OpenCheats()
        {
            if (CheatsOpen) { CloseModWindows(); return; }
            Bridge.RequireMission();
            CloseModWindows();
            cheatsWindow = Window("TAB QoL - debug cheats");
            var content = Reflect.Get(cheatsWindow, "OContent");
            float width = (float)Reflect.Get(content, "Width"), u = Unit;
            Add(content, Label("Debug actions affect the current mission.\nProtection switches reset after loading or changing missions.", width), 0, 0, width, 70 * u);
            Check(content, "Immortal player units (including hero)", 80 * u, width, Cheats.Units, Cheats.SetUnits);
            Check(content, "Invulnerable player buildings", 125 * u, width, Cheats.Buildings, Cheats.SetBuildings);
            Button(content, "Heal player units", 0, 180 * u, width, Cheats.Heal);
            Button(content, "Fill resource storage", 0, 228 * u, width, Cheats.FillResources);
            Button(content, "Win current mission...", 0, 276 * u, width, ConfirmWin);
            Add(content, Label("Resource and victory changes can persist in saves.\nF12 closes this panel (default key).", width), 0, 332 * u, width, 70 * u);
            Show(cheatsWindow);
        }

        private static void ConfirmWin()
        {
            var window = Window("Win current mission?");
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width");
            Add(content, Label("Finish this mission through the normal victory sequence.\nThe mod saves a separate before-cheat-win snapshot first.\nCampaign progress and rewards follow the native result screen.", width), 0, 0, width, 120 * Unit);
            Button(content, "Win mission", 0, 140 * Unit, width, Cheats.Win);
            Show(window);
        }

        internal static void ConfirmReset()
        {
            if (!Bridge.OnWorld) throw new InvalidOperationException("Open the campaign world map to reset technologies.");
            var window = Window("Reset technologies?", true);
            var content = Reflect.Get(window, "OContent");
            float width = (float)Reflect.Get(content, "Width");
            Add(content, Label("Refund all paid technologies and keep the starting technologies.\nA backup will be created before the reset.", width), 0, 0, width, 60 * Unit);
            CampaignButton(content, "Reset", 78 * Unit, width, () => { Bridge.ResetResearch(); CloseModWindows(); });
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
            var button = Reflect.New("ZX.GUI.ZXButtonSteam", text, null);
            Reflect.Set(button, "UseSmallFontStyle", true);
            Reflect.Set(button, "Location", new PointF(x + (width - (float)Reflect.Get(button, "Width")) / 2, y));
            Reflect.Call(content, "AddObject", button);
            Reflect.Event(button, "Activated", _ => Bridge.Queue(action));
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
