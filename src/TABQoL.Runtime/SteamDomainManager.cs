using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;

namespace TABQoL
{
    // The CLR loads this manager through the Steam game's startup config.
    // The original executable, metadata and Steam initialization stay intact.
    public sealed class SteamDomainManager : AppDomainManager
    {
        private const string HarmonyId = "TABQoL.Steam";
        private const uint DiscoveryTimeoutMilliseconds = 5 * 60 * 1000;
        private static readonly object DiscoverySync = new object();
        private static Harmony harmony;
        private static bool attaching, started, discoveryStopped;
        private static int discoveryStarted, scanning, discoveryWarning, keyDownWarning, keyUpWarning;
        // 0: pending, 1: installing, 2: active, 3: disabled after failure.
        private static int hookState;
        private static Timer discovery;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public SteamDomainManager() { Start(); }

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            Start();
        }

        private static void Start()
        {
            try
            {
                lock (DiscoverySync)
                {
                    if (started) return;
                    started = true;
                    discoveryStarted = Environment.TickCount;
                    AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
                    // Create the timer before scanning so immediate attachment also
                    // disposes it. The default domain may already have loaded the EXE.
                    discovery = new Timer(Discover, null, 500, 500);
                }
                Log("Steam startup loader initialized.");
                Discover(null);
            }
            catch (Exception ex)
            {
                StopDiscovery();
                Log("Steam startup loader could not start: " + ex);
            }
        }

        private static void AssemblyLoaded(object sender, AssemblyLoadEventArgs args) { Attach(args.LoadedAssembly); }

        private static void Discover(object state)
        {
            if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0) return;
            try
            {
                lock (DiscoverySync) { if (discoveryStopped) return; }
                if (unchecked((uint)(Environment.TickCount - discoveryStarted)) >= DiscoveryTimeoutMilliseconds)
                {
                    StopDiscovery();
                    Log("Steam startup loader stopped: the supported game type was not found within five minutes.");
                    return;
                }
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) Attach(assembly);
            }
            catch (Exception ex)
            {
                StopDiscovery();
                Log("Steam startup discovery failed: " + ex);
            }
            finally { Interlocked.Exchange(ref scanning, 0); }
        }

        private static void StopDiscovery()
        {
            Timer timer;
            lock (DiscoverySync)
            {
                discoveryStopped = true;
                AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
                timer = discovery;
                discovery = null;
            }
            timer?.Dispose();
        }

        private static void Attach(Assembly assembly)
        {
            lock (DiscoverySync)
            {
                if (discoveryStopped || attaching) return;
                attaching = true;
            }
            try
            {
                if (assembly.IsDynamic) return;
                Type game;
                try { game = assembly.GetType(Compatibility.Symbol(Compatibility.Game, true), false); }
                catch (Exception ex)
                {
                    // An unrelated or still-loading assembly must not end discovery.
                    if (Interlocked.Exchange(ref discoveryWarning, 1) == 0) Log("Steam assembly discovery will retry: " + ex);
                    return;
                }
                if (game == null) return;
                // A Steam update can leave the startup config installed. Refuse
                // stale symbol mappings before changing any running method.
                using (var stream = new FileStream(assembly.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (hash != Compatibility.SteamSha256) throw new InvalidDataException("Unsupported Steam executable. Reinstall a compatible TAB QoL version after updating the game.");
                }
                var frame = game.GetMethod("OnStartFrame", Flags);
                if (frame == null) throw new MissingMethodException(game.FullName, "OnStartFrame");
                harmony = new Harmony(HarmonyId);
                harmony.Patch(frame, prefix: new HarmonyMethod(typeof(SteamDomainManager), nameof(Frame)));
                StopDiscovery();
                Log("Steam startup loader attached.");
            }
            catch (Exception ex)
            {
                StopDiscovery();
                DisableHooks("Steam startup attachment failed", ex);
            }
            finally { lock (DiscoverySync) { attaching = false; } }
        }

        private static void Frame(object __instance)
        {
            if (Interlocked.CompareExchange(ref hookState, 1, 0) == 0) InstallHooks(__instance);
            if (HooksEnabled) Bridge.OnFrame(__instance);
        }

        private static bool HooksEnabled { get { return Volatile.Read(ref hookState) == 2; } }

        private static void InstallHooks(object instance)
        {
            try
            {
                var assembly = instance.GetType().Assembly;
                string Symbol(string name) { return Compatibility.Symbol(name, true); }
                Type Type(string name) { return assembly.GetType(Symbol(name), true); }
                MethodInfo Method(Type type, string name) { return type.GetMethods(Flags).Single(m => m.Name == Symbol(name)); }
                HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(SteamDomainManager), name); }
                var game = instance.GetType();
                var mission = Type(Compatibility.MissionSystem);
                var world = Type(Compatibility.WorldSystem);
                var closure = world.GetNestedTypes(Flags).Single(t => t.Name == Compatibility.StartMissionClosure);
                var targets = new[] {
                    Method(Type("ZX.GUI.ZXControlCommand"), "#=zwjLekNY="),
                    Method(mission, "OnKeyDown"), Method(game, "ProcessSpecialKeys_KeyUp"), Method(mission, "OnKeyUp"),
                    Method(Type("ZX.Components.CLife"), "#=zG9K55tU="), Method(Type("ZX.Components.CLife"), "#=z9SDftOMfXc1$"),
                    Method(world, Compatibility.ShowWorld), Method(world, "OnDispose"),
                    Method(closure, Compatibility.StartMission), Method(game, Compatibility.Options),
                    Method(mission, "#=zEHnEwyMK7mIMOOClaw==")
                };
                harmony.Patch(targets[0], prefix: Hook(nameof(Command)));
                harmony.Patch(targets[1], prefix: Hook(nameof(KeyDown)));
                harmony.Patch(targets[2], prefix: Hook(nameof(KeyUp)));
                harmony.Patch(targets[3], prefix: Hook(nameof(KeyUp)));
                harmony.Patch(targets[4], prefix: Hook(nameof(Damage)));
                harmony.Patch(targets[5], prefix: Hook(nameof(Damage)));
                harmony.Patch(targets[6], postfix: Hook(nameof(WorldShown)));
                harmony.Patch(targets[7], prefix: Hook(nameof(WorldDisposed)));
                harmony.Patch(targets[8], prefix: Hook(nameof(StartHero)));
                harmony.Patch(targets[9], transpiler: Hook(nameof(Options)));
                harmony.Patch(targets[10], prefix: Hook(nameof(Activate)));
                Volatile.Write(ref hookState, 2);
                Log("Steam runtime hooks installed.");
            }
            catch (Exception ex) { DisableHooks("Steam runtime hooks could not be installed", ex); }
        }

        private static void DisableHooks(string reason, Exception exception)
        {
            Volatile.Write(ref hookState, 3);
            Log(reason + ": " + exception);
            // Failed installation must not leave only part of the mod active.
            // Restrict rollback to our ID so other Harmony mods stay intact.
            try { harmony?.UnpatchAll(HarmonyId); }
            catch (Exception ex) { Log("Steam runtime hook rollback failed: " + ex); }
        }

        private static void Command(object __instance) { if (HooksEnabled) Bridge.CommandRefreshing(__instance); }
        private static bool Activate(object __instance, object[] __args)
        {
            return !HooksEnabled || !Bridge.CommandActivating(__instance, __args[0]);
        }
        private static void WorldShown(object __instance) { if (HooksEnabled) Bridge.WorldShown(__instance); }
        private static void WorldDisposed(object __instance) { if (HooksEnabled) Bridge.WorldDisposed(__instance); }
        private static void OptionsCreated(object window) { if (HooksEnabled) Bridge.OptionsCreated(window); }
        private static bool StartHero(object __instance) { return !HooksEnabled || !Bridge.TrySkipHero(__instance); }
        private static bool KeyDown(object __instance, object[] __args, ref bool __result)
        {
            if (!HooksEnabled) return true;
            try
            {
                if (!Bridge.BuildingKeyDown(__instance, Convert.ToInt32(__args[0], CultureInfo.InvariantCulture))) return true;
                __result = true; return false;
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref keyDownWarning, 1) == 0) Log("Steam key-down hook failed: " + ex);
                return true;
            }
        }
        private static bool KeyUp(object __instance, object[] __args, ref bool __result)
        {
            if (!HooksEnabled) return true;
            try
            {
                if (!Bridge.GlobalKeyUp(__instance, Convert.ToInt32(__args[0], CultureInfo.InvariantCulture))) return true;
                __result = true; return false;
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref keyUpWarning, 1) == 0) Log("Steam key-up hook failed: " + ex);
                return true;
            }
        }
        private static bool Damage(object __instance, ref bool __result)
        {
            if (!HooksEnabled || !Bridge.IgnoreDamage(__instance)) return true;
            __result = false; return false;
        }
        private static IEnumerable<CodeInstruction> Options(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Newobj || !(instruction.operand is ConstructorInfo constructor) || constructor.DeclaringType.FullName != "ZX.GUI.ZXWindowTab") continue;
                count++;
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Call, typeof(SteamDomainManager).GetMethod(nameof(OptionsCreated), Flags));
            }
            if (count != 1) throw new InvalidDataException("Steam options hook target did not match.");
        }
        private static void Log(string message)
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "They Are Billions", "TABQoL");
                Directory.CreateDirectory(root);
                File.AppendAllText(Path.Combine(root, "TABQoL.log"), DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
