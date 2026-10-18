using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TABQoL
{
    public static class Bridge
    {
        internal static object Game;
        internal static Settings Config;
        internal static string DataDirectory;
        internal static SnapshotStore Store;
        private static bool initialized, failed, busy;
        private static object previousLevel;
        private static object loadSourceLevel;
        private static bool loadPaused;
        private static DateTime lastKey;
        private static Action pending;
        private static string stagedLoad;
        private static int loadFrames;
        private static readonly HashSet<int> heldKeys = new HashSet<int>();
        private static readonly HashSet<int> contextualKeys = new HashSet<int>();
        private static readonly AutosaveSchedule autosave = new AutosaveSchedule();
        private static readonly global::System.Diagnostics.Stopwatch playClock = global::System.Diagnostics.Stopwatch.StartNew();
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        internal static bool HasModifier { get { return new[] { 16, 17, 18, 91, 92 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); } }

        private static void PollKeys()
        {
            Ui.CheckKeyCapture();
            uint process;
            GetWindowThreadProcessId(GetForegroundWindow(), out process);
            if (process != (uint)global::System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                heldKeys.Clear();
                contextualKeys.Clear();
                return;
            }
            var keys = Ui.CaptureKey != null ? Enumerable.Range(8, 247) : Config.GlobalKeys().AsEnumerable();
            foreach (int key in keys)
            {
                bool down = (GetAsyncKeyState(key) & 0x8000) != 0;
                if (down) heldKeys.Add(key);
                else if (heldKeys.Remove(key)) OnKey(Game, key);
            }
        }
        internal static object State { get { return Reflect.Current("ZX.ZXGameState"); } }
        internal static object Campaign { get { return State == null ? null : Reflect.Get(State, "CampaignState"); } }
        internal static object Level { get { return State == null ? null : Reflect.Get(State, "LevelState"); } }
        internal static object System { get { return Reflect.Get(Game, "CurrentGameSystem"); } }
        internal static bool InMission { get { return System != null && System.GetType().FullName == Compatibility.RuntimeSymbol(Compatibility.MissionSystem) && Level != null; } }
        internal static bool OnWorld { get { return System != null && System.GetType().FullName == Compatibility.RuntimeSymbol(Compatibility.WorldSystem) && Campaign != null && Level == null; } }
        internal static string Saves { get { return (string)Reflect.Call(Reflect.Type(Compatibility.Game), Compatibility.SavesDirectory); } }
        internal static bool Loading { get { return (bool)Reflect.Get(Game, "IsLoading"); } }
        internal static bool Ready { get { return !Loading && (bool)Reflect.Get(Game, "InputEnabled"); } }

        private static void Initialize(object game)
        {
            if (initialized || failed) return;
            Game = game;
            Reflect.GameAssembly = game.GetType().Assembly;
            Compatibility.RuntimeSteam = game.GetType().FullName == Compatibility.Symbol(Compatibility.Game, true);
            DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "They Are Billions", "TABQoL");
            Directory.CreateDirectory(DataDirectory);
            string path = Path.Combine(DataDirectory, "Settings.xml");
            try { Config = Settings.Read(path); }
            catch (Exception ex)
            {
                Log(ex);
                File.Copy(path, path + ".invalid-" + DateTime.UtcNow.Ticks, false);
                Config = new Settings();
            }
            Store = new SnapshotStore(Path.Combine(DataDirectory, "Snapshots"));
            initialized = true;
            Log("TAB QoL initialized; " + (Compatibility.RuntimeSteam ? Compatibility.SteamVersion : Compatibility.GameVersion));
        }

        public static void OnFrame(object game)
        {
            try
            {
                Initialize(game);
                if (!initialized) return;
                PollKeys();
                if (pending != null && !Loading)
                {
                    var action = pending;
                    pending = null;
                    Run(action);
                }
                if (stagedLoad != null && ++loadFrames > 300 && !Loading)
                {
                    File.Delete(stagedLoad);
                    File.Delete(Path.ChangeExtension(stagedLoad, ".zxcheck"));
                    stagedLoad = null;
                }
                if (InMission && Ready)
                {
                    if (!ReferenceEquals(previousLevel, Level))
                    {
                        Cheats.Reset();
                        autosave.Reset();
                        if (loadSourceLevel != null && !ReferenceEquals(loadSourceLevel, Level))
                        {
                            Reflect.Set(Game, "Paused", loadPaused);
                            loadSourceLevel = null;
                        }
                        previousLevel = Level;
                    }
                    // The native pause button resumes at 1x. Restore the user's
                    // selected multiplier when simulation resumes.
                    if ((double)Reflect.Get(Game, "GameSpeed") != Config.Speed) ApplySpeed(Config.Speed);
                }
                else if (!InMission) Cheats.Reset();
                bool eligible = InMission && Ready && !busy && pending == null &&
                    !(bool)Reflect.Get(Game, "Paused") && Reflect.Get(Level, "EndStatus").ToString() == "Playing" &&
                    !(bool)Reflect.Get(Reflect.Type("DXVision.GUI.DXModalScreen"), "IsInModal");
                if (autosave.Tick(playClock.Elapsed.TotalSeconds, eligible, Config.AutosaveMinutes))
                {
                    try { Save(Store.NextAutosaveSlot(Scope())); autosave.Completed(); }
                    catch (Exception ex) { autosave.Failed(); Log(ex); Notify("Autosave failed. See TABQoL.log."); }
                }
            }
            catch (Exception ex)
            {
                Log(ex);
                if (!initialized) failed = true;
            }
        }

        public static bool OnKey(object game, int key)
        {
            try
            {
                Initialize(game);
                if (!initialized) return false;
                if (Ui.CaptureKey != null) { lastKey = DateTime.UtcNow; Ui.CaptureKey(key); heldKeys.Clear(); return true; }
                bool recognized = Config.GlobalKeys().Contains(key);
                if (!recognized || !Ready) return false;
                if ((DateTime.UtcNow - lastKey).TotalMilliseconds < 250) return true;
                lastKey = DateTime.UtcNow;
                if (key == Config.MenuKey) { Queue(Ui.OpenMenu); return true; }
                if (key == Config.CheatsKey)
                {
                    if (InMission && (!(bool)Reflect.Get(Reflect.Type("DXVision.GUI.DXModalScreen"), "IsInModal") || Ui.CheatsOpen)) Queue(Ui.OpenCheats);
                    return true;
                }
                if (!InMission || (bool)Reflect.Get(Reflect.Type("DXVision.GUI.DXModalScreen"), "IsInModal")) return false;
                if (key == Config.QuickSaveKey) Queue(() => Save("quick"));
                else if (key == Config.QuickLoadKey) Queue(() => Load("quick"));
                else
                {
                    double current = (double)Reflect.Get(Game, "GameSpeed");
                    SetSpeed(Policy.StepSpeed(current == 0 ? Config.Speed : current, key == Config.FasterKey ? 1 : -1));
                }
                return true;
            }
            catch (Exception ex) { Report(ex); return true; }
        }

        internal static void Queue(Action action)
        {
            if (pending != null || busy) return;
            pending = action;
        }

        internal static void Run(Action action)
        {
            if (busy) return;
            busy = true;
            try { action(); }
            catch (Exception ex) { Report(ex); }
            finally { busy = false; }
        }

        internal static string Scope()
        {
            string mission = Campaign == null ? "survival" : (string)Reflect.Get(Campaign, "IDCurrentMission");
            string identity = Convert.ToString(Reflect.Get(State, "ID"), CultureInfo.InvariantCulture) + ":" + mission;
            if (Compatibility.RuntimeSteam) identity = "steam:" + identity;
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
        }

        internal static void RequireMission()
        {
            RequireLoadedMission();
            if (Reflect.Get(Level, "EndStatus").ToString() != "Playing")
                throw new InvalidOperationException("Wait until the mission is ready.");
        }

        internal static void RequireLoadedMission()
        {
            if (!InMission || !Ready) throw new InvalidOperationException("Wait until the mission is ready.");
        }

        internal static void Save(string slot)
        {
            RequireMission();
            bool paused = (bool)Reflect.Get(Game, "Paused");
            try
            {
                Reflect.Set(Game, "Paused", true);
                Reflect.Call(Level, Compatibility.CaptureLevel);
                Store.Save(Scope(), slot, Saves, path => Reflect.Call(Game, Compatibility.WriteSave, path));
                Notify("Saved: " + slot);
            }
            finally { Reflect.Set(Game, "Paused", paused); }
        }

        internal static void Load(string slot)
        {
            RequireLoadedMission();
            if (stagedLoad != null)
            {
                File.Delete(stagedLoad);
                File.Delete(Path.ChangeExtension(stagedLoad, ".zxcheck"));
                stagedLoad = null;
            }
            string path = Store.StageLoad(Scope(), slot, Saves);
            loadSourceLevel = Level;
            loadPaused = (bool)Reflect.Get(Game, "Paused");
            Ui.CloseModWindows();
            Reflect.Call(Reflect.Type("DXVision.GUI.DXModalScreen"), "CloseAll");
            try { Reflect.Call(Game, Compatibility.Load, Path.GetFileName(path)); }
            catch { loadSourceLevel = null; File.Delete(path); File.Delete(Path.ChangeExtension(path, ".zxcheck")); throw; }
            stagedLoad = path;
            loadFrames = 0;
        }

        internal static void SetSpeed(double speed)
        {
            if (!Policy.ValidSpeed(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
            Config.Speed = speed;
            PersistSettings();
            if (InMission && Ready) ApplySpeed(speed);
            Notify("Speed: " + speed.ToString("0.#", CultureInfo.InvariantCulture) + "x");
        }

        private static void ApplySpeed(double speed)
        {
            // Native Paused is implemented through GameSpeed (0/1), so setting
            // Paused=false after assigning a multiplier would overwrite it.
            if (!(bool)Reflect.Get(Game, "Paused")) Reflect.Set(Game, "GameSpeed", speed);
        }

        internal static void PersistSettings() { BuildingHotkeys.Validate(); Config.Write(Path.Combine(DataDirectory, "Settings.xml")); }

        public static void CommandRefreshing(object control)
        {
            try { BuildingHotkeys.UpdateControl(control); }
            catch (Exception ex) { Log(ex); }
        }

        public static bool CommandActivating(object system, object control)
        {
            try
            {
                if (!initialized || !InMission || !Ready) return false;
                return BuildingHotkeys.OnActivate(system, control);
            }
            catch (Exception ex) { Log(ex); return false; }
        }

        public static bool BuildingKeyDown(object system, int key)
        {
            try
            {
                if (!initialized) return false;
                if (Ui.CaptureKey != null || Config.GlobalKeys().Contains(key)) return true;
                if (!BuildingHotkeys.OnKey(system, key)) return false;
                contextualKeys.Add(key);
                return true;
            }
            catch (Exception ex) { Log(ex); return false; }
        }

        public static bool GlobalKeyUp(object game, int key)
        {
            if (!initialized) return false;
            // A contextual command can change the selection on key-down. Keep
            // its matching release away from native global key-up actions.
            bool contextual = contextualKeys.Remove(key);
            if (Ui.CaptureKey == null && !Config.GlobalKeys().Contains(key)) return contextual;
            OnKey(Game, key);
            return true;
        }

        public static bool IgnoreDamage(object life)
        {
            try { return Cheats.IgnoreDamage(life); }
            catch { return false; }
        }

        private static void SaveCampaign()
        {
            bool paused = (bool)Reflect.Get(Game, "Paused");
            try
            {
                Reflect.Set(Game, "Paused", true);
                string name = (string)Reflect.Get(State, "Name");
                Reflect.Call(Game, Compatibility.WriteSave, Path.Combine(Saves, Path.ChangeExtension(Path.GetFileName(name), ".zxsav")));
                string campaignPath = (string)Reflect.Call(Game, "#=zQA96tlNNErChKjVzAa3pMCo=", name);
                Reflect.Call(Game, Compatibility.WriteSave, campaignPath);
            }
            finally { Reflect.Set(Game, "Paused", paused); }
        }

        private static string BackupCampaign(string reason)
        {
            SaveCampaign();
            string directory = Path.Combine(DataDirectory, "Backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + reason);
            Directory.CreateDirectory(directory);
            foreach (string path in Directory.GetFiles(Saves))
                if (new[] { ".zxsav", ".zxcampaign", ".zxcheck" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
            return Path.Combine(directory, Path.ChangeExtension(Path.GetFileName((string)Reflect.Get(State, "Name")), ".zxsav"));
        }

        private static void ChangeCampaign(string reason, Action change)
        {
            string backup = BackupCampaign(reason);
            object original = Reflect.Call(Game, Compatibility.ReadSave, backup);
            try { change(); SaveCampaign(); }
            catch (Exception failure)
            {
                try
                {
                    Reflect.Call(Reflect.Type("ZX.ZXGameState"), Compatibility.SetState, original);
                    Reflect.Call(System, Compatibility.RefreshWorld);
                    Reflect.Call(System, Compatibility.RefreshResearch);
                    SaveCampaign();
                }
                catch (Exception rollback)
                {
                    throw new AggregateException("Campaign change failed. Restore the backup from: " + backup, failure, rollback);
                }
                throw;
            }
        }

        internal static void ResetResearch()
        {
            if (!OnWorld || !Ready || (bool)Reflect.Get(Campaign, "ReplayingMission"))
                throw new InvalidOperationException("Research reset is available on the campaign world map.");
            object campaign = Campaign;
            var researches = Reflect.Items(Reflect.Get(campaign, "ResearchsUnlocked")).ToArray();
            int refund = checked(researches.Sum(r => (int)Reflect.Get(r, "Cost")));
            if (refund == 0) { Notify("No paid technologies to reset."); return; }
            int oldResearch = (int)Reflect.Get(campaign, "ResearchPoints");
            ChangeCampaign("research-reset", () =>
            {
                foreach (var research in researches.Where(r => (int)Reflect.Get(r, "Cost") > 0))
                    Reflect.Call(campaign, Compatibility.RefundResearch, (string)Reflect.Get(research, "ID"));
                var ids = (IList)Reflect.Get(campaign, "IDResearchUnlocked");
                foreach (var research in researches.Where(r => (int)Reflect.Get(r, "Cost") == 0))
                    if (!ids.Contains(Reflect.Get(research, "ID"))) ids.Add(Reflect.Get(research, "ID"));
                ((IList)Reflect.Get(campaign, "IDResearchsRecentUnlocked")).Clear();
                if ((int)Reflect.Get(campaign, "ResearchPoints") != checked(oldResearch + refund))
                    throw new InvalidOperationException("The game's refund total did not match the expected total.");
                Reflect.Call(campaign, Compatibility.RebuildEntities);
                Reflect.Call(System, Compatibility.RefreshResearch);
            });
            Notify("Technologies reset. Refunded: " + refund);
        }

        // Runs after Start is accepted, before the game disposes the world map.
        public static bool TrySkipHero(object closure)
        {
            try
            {
                if (!initialized || !Config.SkipHeroMissions || !OnWorld || !Ready) return false;
                var mission = Reflect.FieldOfType(closure, "ZX.ZXMission");
                var state = Reflect.Get(mission, "CurrentState");
                if (Reflect.Get(mission, "MissionType").ToString() != "Tactic" || (bool)Reflect.Get(state, "Completed") || (bool)Reflect.Get(Campaign, "ReplayingMission")) return false;
                if (!(bool)Reflect.Get(state, "Playable")) return false;
                ChangeCampaign("hero-skip", () =>
                {
                    // Collected loot is separate from the fixed mission reward in the native completion function.
                    Reflect.Set(state, "EmpirePointsTaken", Reflect.Get(mission, "BonusItemsPointsEmpire"));
                    Reflect.Set(state, "ResearchPointsTaken", Reflect.Get(mission, "BonusItemsPointsResearch"));
                    Reflect.Set(state, "DifficultyType", Reflect.Get(Campaign, "DefaultDifficultyType"));
                    Reflect.Set(state, "NTimesPlayed", Math.Max(1, (int)Reflect.Get(state, "NTimesPlayed")));
                    Reflect.Call(Campaign, Compatibility.CompleteMission, mission);
                    Reflect.Call(Reflect.Type("DXVision.GUI.DXModalScreen"), "CloseAll");
                    Reflect.Call(System, Compatibility.RefreshWorld);
                });
                Notify("Hero mission completed with all research and empire rewards.");
                return true;
            }
            catch (Exception ex) { Report(ex); return true; }
        }

        public static void WorldShown(object world)
        {
            try { Initialize(Reflect.Get(world, "Game")); Ui.AddWorldControls(world); }
            catch (Exception ex) { Report(ex); }
        }

        public static void WorldDisposed(object world) { try { Ui.RemoveWorldControls(); } catch (Exception ex) { Log(ex); } }
        public static void OptionsCreated(object window) { try { Ui.AddOptions(window); } catch (Exception ex) { Report(ex); } }

        internal static void Notify(string text)
        {
            Log(text);
            if (InMission) Reflect.Call(System, Compatibility.Notify, text, Color.Gold, 2500);
        }

        internal static void Report(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            Log(ex);
            if (initialized) Ui.Error(ex.Message);
        }

        internal static void Log(object message)
        {
            try
            {
                if (DataDirectory != null) File.AppendAllText(Path.Combine(DataDirectory, "TABQoL.log"), DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
