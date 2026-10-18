using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TABQoL;

// A small native-API fixture: emitted names exercise both real reflection
// adapters without loading or distributing the proprietary game assembly.
public static class TrainingActivationTests
{
    public sealed class Ranger { }
    public sealed class Soldier { }
    public sealed class Budget { public int Gold = 1000; }
    public class Producer
    {
        public string Team { get; set; } = "Player";
        public Commandable Commands = new Commandable();
        public Budget Budget;
        public bool Enabled = true, Unlocked = true, Building, Repairing, Fail;
        public int Queue;
        public List<Type> Trained = new List<Type>();
        public Action Purchased;
        public Commandable GetCommandable() { return Commands; }
    }
    public sealed class Commandable { public List<object> AvailableCommands { get; } = new List<object>(); }
    public class Train
    {
        public Type EntityType { get; set; }
        public bool IsVisible(Producer building) { return building.Unlocked && !building.Building; }
        public bool IsAvailable(Producer building)
        {
            return building.Enabled && !building.Repairing && building.Queue < 4 && building.Budget.Gold >= 100;
        }
        public void Execute(Producer building, object target)
        {
            if (building.Fail) throw new InvalidOperationException("native purchase failed");
            building.Budget.Gold -= 100;
            building.Queue++;
            building.Trained.Add(EntityType);
            building.Purchased?.Invoke();
        }
    }
    public sealed class Selectable { public object Entity { get; set; } }
    // The real game's concurrent selection set is not IEnumerable either.
    public sealed class Selection
    {
        public readonly List<object> Items = new List<object>();
        public int Snapshots;
        public bool Fail;
        public object[] ToList()
        {
            Snapshots++;
            if (Fail) throw new InvalidOperationException("selection unavailable");
            return Items.ToArray();
        }
    }
    public sealed class Control { public object Tag { get; set; } }
    public class Mission
    {
        public int Cancellations, Refreshes;
        public bool Targeting = true;
        public void CancelTargeting() { Cancellations++; Targeting = false; }
        public void Refresh() { Refreshes++; }
    }

    private static Type Derived(ModuleBuilder module, string name, Type parent, params string[] forwards)
    {
        var type = module.DefineType(name, TypeAttributes.Public, parent);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        for (int i = 0; i < forwards.Length; i += 2)
        {
            var source = parent.GetMethod(forwards[i + 1]);
            var parameters = source.GetParameters().Select(p => p.ParameterType).ToArray();
            var method = type.DefineMethod(forwards[i], MethodAttributes.Public, source.ReturnType, parameters);
            var il = method.GetILGenerator();
            for (int argument = 0; argument <= parameters.Length; argument++) il.Emit(OpCodes.Ldarg, (short)argument);
            il.Emit(OpCodes.Call, source);
            il.Emit(OpCodes.Ret);
        }
        return type.CreateType();
    }

    public static void Run(Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var runtime = typeof(Bridge).Assembly;
        var gameAssembly = runtime.GetType("TABQoL.Reflect", true).GetField("GameAssembly", flags);
        var steamField = typeof(Compatibility).GetField("RuntimeSteam", flags);
        var logDirectory = typeof(Bridge).GetField("DataDirectory", flags);
        var activate = runtime.GetType("TABQoL.BuildingHotkeys", true).GetMethod("OnActivate", flags);
        object previousAssembly = gameAssembly.GetValue(null), previousSteam = steamField.GetValue(null), previousLog = logDirectory.GetValue(null);
        try
        {
            logDirectory.SetValue(null, null);
            foreach (bool steam in new[] { false, true })
            {
                // Each edition exposes only its real native names. Expected
                // spellings are independent of Compatibility.Symbol, so an
                // absent or incorrect mapping cannot pass through a GOG alias.
                string Native(string gog, string steamName) { return steam ? steamName : gog; }
                var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName("TABQoL.NativeTrainingFixture." + (steam ? "Steam" : "GOG")), AssemblyBuilderAccess.Run);
                var module = assembly.DefineDynamicModule("fixture");
                Type producer = Derived(module, "ZX.Entities.Structure", typeof(Producer),
                    Native("#=zd4B080STejtVeVx5odMu2mI=", "#=zkK_YqS85HrT3g75MJrlvMYo="), "GetCommandable");
                Type train = Derived(module, "ZX.Commands.Train", typeof(Train),
                    Native("#=zBxvmPXf0pX6L", "#=zXAX1VeKTLe7O"), "IsVisible",
                    Native("#=ztKqx10CcsHta", "#=zKc8Kpc_EaFhf"), "IsAvailable",
                    Native("#=z9be3Bbo=", "#=zfmgzkuw="), "Execute");
                Type mission = Derived(module, "NativeMission", typeof(Mission),
                    Native("#=zxEeLa$s1cFWH9gUCClxHkJs=", "#=z$xo$17_AskhgfUuy9Epz5JQ="), "CancelTargeting",
                    Native("#=ze3cy7Z0=", "#=zR1qHCYY="), "Refresh");
                Derived(module, "ZX.Commands.ZXCommandTarget", typeof(object));
                var selectableBuilder = module.DefineType("ZX.Components.CSelectable", TypeAttributes.Public);
                var selectionField = selectableBuilder.DefineField(Native("#=zV9GubOAdTaKJ", "#=zIiHCgn_odqH7"),
                    typeof(Selection), FieldAttributes.Public | FieldAttributes.Static);
                Type selectable = selectableBuilder.CreateType();
                var selected = selectable.GetField(selectionField.Name);
                gameAssembly.SetValue(null, assembly);
                steamField.SetValue(null, steam);
                string adapter = steam ? "Steam: " : "GOG: ";
                var budget = new Budget();
                Selection selection = null;
                Mission system = null;
                Producer[] buildings = null;
                Train command = null;
                Producer NewProducer()
                {
                    var building = (Producer)Activator.CreateInstance(producer);
                    building.Budget = budget;
                    foreach (Type unit in new[] { typeof(Ranger), typeof(Soldier) })
                    {
                        var nativeTrain = (Train)Activator.CreateInstance(train);
                        nativeTrain.EntityType = unit;
                        building.Commands.AvailableCommands.Add(nativeTrain);
                    }
                    return building;
                }
                void Setup(int count = 2)
                {
                    budget.Gold = 1000;
                    buildings = Enumerable.Range(0, count).Select(_ => NewProducer()).ToArray();
                    selection = new Selection();
                    selection.Items.AddRange(buildings.Select(b => (object)new Selectable { Entity = b }));
                    selected.SetValue(null, selection);
                    system = (Mission)Activator.CreateInstance(mission);
                    command = (Train)buildings[0].Commands.AvailableCommands[0];
                }
                bool Activate(object tag = null)
                {
                    return (bool)activate.Invoke(null, new object[] { system, new Control { Tag = tag ?? command } });
                }
                Setup();
                check(Activate() && buildings.All(b => b.Queue == 1 && b.Trained.SequenceEqual(new[] { typeof(Ranger) })) &&
                    budget.Gold == 800 && selection.Snapshots == 1 && system.Cancellations == 1 && !system.Targeting && system.Refreshes == 1,
                    adapter + "shared Ranger activation queues once in each idle producer and clears targeting");
                check(Activate() && buildings.All(b => b.Queue == 2) && budget.Gold == 600 && system.Refreshes == 2,
                    adapter + "a second activation adds exactly one unit to each selected queue");
                Setup();
                command = (Train)buildings[0].Commands.AvailableCommands[1];
                check(Activate() && buildings.All(b => b.Trained.SequenceEqual(new[] { typeof(Soldier) })),
                    adapter + "group training resolves the requested unit separately for each producer");
                Setup(); buildings[0].Queue = 4;
                check(Activate() && buildings[0].Queue == 4 && buildings[1].Queue == 1 && budget.Gold == 900,
                    adapter + "a full first queue does not block the next selected producer");
                Setup(); buildings[0].Enabled = false;
                check(Activate() && buildings[0].Queue == 0 && buildings[1].Queue == 1,
                    adapter + "a disabled producer is skipped without blocking another building");
                Setup(); budget.Gold = 100;
                check(Activate() && buildings.Sum(b => b.Queue) == 1 && budget.Gold == 0,
                    adapter + "native resource eligibility is rechecked after each purchase");
                Setup(4); buildings[0].Unlocked = false; buildings[1].Building = true; buildings[2].Repairing = true;
                check(Activate() && buildings.Take(3).All(b => b.Queue == 0) && buildings[3].Queue == 1,
                    adapter + "unlock, construction and repair restrictions remain effective");
                Setup(3); buildings[0].Commands.AvailableCommands.Clear(); buildings[1].Team = "Zombie";
                check(Activate() && buildings[0].Queue == 0 && buildings[1].Queue == 0 && buildings[2].Queue == 1,
                    adapter + "incompatible buildings and enemy producers never receive the order");
                Setup(); budget.Gold = 0;
                check(Activate() && buildings.All(b => b.Queue == 0) && system.Refreshes == 1,
                    adapter + "an ineligible group is consumed without native replay");
                Setup(); buildings[1].Fail = true;
                check(Activate() && buildings[0].Queue == 1 && buildings[1].Queue == 0 && budget.Gold == 900 && system.Refreshes == 1,
                    adapter + "failure after a purchase is consumed without duplicating the successful order");
                Setup(); buildings[0].Purchased = () => selection.Items.Clear();
                check(Activate() && buildings.All(b => b.Queue == 1) && selection.Items.Count == 0 && selection.Snapshots == 1,
                    adapter + "all selected producers are snapshotted before executing any command");
                Setup(1);
                check(!Activate() && buildings[0].Queue == 0 && system.Cancellations == 0 && system.Refreshes == 0,
                    adapter + "single-building activation falls through unchanged");
                Setup();
                check(!Activate(new object()) && selection.Snapshots == 0 && system.Refreshes == 0,
                    adapter + "non-training commands fall through unchanged");
                selection.Fail = true;
                check(!Activate() && buildings.All(b => b.Queue == 0) && system.Cancellations == 0 && system.Refreshes == 0,
                    adapter + "selection discovery failure falls through before any purchase");
            }
        }
        finally
        {
            gameAssembly.SetValue(null, previousAssembly);
            steamField.SetValue(null, previousSteam);
            logDirectory.SetValue(null, previousLog);
        }
    }
}
