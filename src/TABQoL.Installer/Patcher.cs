using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace TABQoL.Installer
{
    public static class Patcher
    {
        public static void Prepare(string original, string output)
        {
            if (SnapshotStore.Hash(original) != Compatibility.OriginalSha256)
                throw new InvalidDataException("Unsupported or already modified EXE. Supported: " + Compatibility.GameVersion);
            using (var assembly = AssemblyDefinition.ReadAssembly(original, new ReaderParameters { ReadSymbols = false, InMemory = true }))
            {
                var module = assembly.MainModule;
                var game = module.Types.Single(t => t.FullName == Compatibility.Game);
                var world = module.Types.Single(t => t.FullName == Compatibility.WorldSystem);
                MethodDefinition Get(TypeDefinition type, string name) { return type.Methods.Single(m => m.Name == name && m.HasBody); }
                MethodReference Hook(string name) { return module.ImportReference(typeof(Bridge).GetMethod(name)); }

                void Prefix(MethodDefinition method, params Instruction[] instructions)
                {
                    method.Body.SimplifyMacros();
                    var il = method.Body.GetILProcessor();
                    var first = method.Body.Instructions.First();
                    foreach (var instruction in instructions) il.InsertBefore(first, instruction);
                    method.Body.OptimizeMacros();
                }

                void Postfix(MethodDefinition method, string hook)
                {
                    method.Body.SimplifyMacros();
                    var il = method.Body.GetILProcessor();
                    foreach (var ret in method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
                    {
                        // Reuse the original return instruction as the hook entry, preserving existing branch targets.
                        ret.OpCode = OpCodes.Ldarg_0;
                        il.InsertAfter(ret, Instruction.Create(OpCodes.Call, Hook(hook)));
                        il.InsertAfter(ret.Next, Instruction.Create(OpCodes.Ret));
                    }
                    method.Body.OptimizeMacros();
                }

                Prefix(Get(game, "OnStartFrame"), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("OnFrame")));
                var control = module.Types.Single(t => t.FullName == "ZX.GUI.ZXControlCommand");
                Prefix(Get(control, "#=zwjLekNY="), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("CommandRefreshing")));
                var mission = module.Types.Single(t => t.FullName == Compatibility.MissionSystem);
                var keyDown = Get(mission, "OnKeyDown");
                var keyEntry = keyDown.Body.Instructions.First();
                Prefix(keyDown, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call, Hook("BuildingKeyDown")),
                    Instruction.Create(OpCodes.Brfalse, keyEntry), Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret));
                var life = module.Types.Single(t => t.FullName == "ZX.Components.CLife");
                foreach (string name in new[] { "#=zG9K55tU=", "#=z9SDftOMfXc1$" })
                {
                    var damage = Get(life, name);
                    var damageEntry = damage.Body.Instructions.First();
                    Prefix(damage, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("IgnoreDamage")),
                        Instruction.Create(OpCodes.Brfalse, damageEntry), Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Ret));
                }
                Postfix(Get(world, Compatibility.ShowWorld), "WorldShown");
                Prefix(Get(world, "OnDispose"), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("WorldDisposed")));
                var closure = world.NestedTypes.Single(t => t.Name == Compatibility.StartMissionClosure);
                var start = Get(closure, Compatibility.StartMission);
                var startEntry = start.Body.Instructions.First();
                Prefix(start, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("TrySkipHero")),
                    Instruction.Create(OpCodes.Brfalse, startEntry), Instruction.Create(OpCodes.Ret));

                var options = Get(game, Compatibility.Options);
                options.Body.SimplifyMacros();
                var constructor = options.Body.Instructions.Single(i => i.OpCode == OpCodes.Newobj &&
                    i.Operand is MethodReference mr && mr.DeclaringType.FullName == "ZX.GUI.ZXWindowTab");
                // Keep the new window on the original stack and pass a duplicate to the runtime.
                var optionsIl = options.Body.GetILProcessor();
                var duplicate = Instruction.Create(OpCodes.Dup);
                optionsIl.InsertAfter(constructor, duplicate);
                optionsIl.InsertAfter(duplicate, Instruction.Create(OpCodes.Call, Hook("OptionsCreated")));
                options.Body.OptimizeMacros();

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                assembly.Write(output);
            }
            Verify(output);
        }

        public static void Verify(string patched)
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(patched))
            {
                var hooks = new HashSet<string>();
                void Scan(TypeDefinition type)
                {
                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    {
                        var instructions = new HashSet<Instruction>(method.Body.Instructions);
                        foreach (var instruction in instructions)
                        {
                            if (instruction.Operand is Instruction target && !instructions.Contains(target)) throw new InvalidDataException("Dangling branch.");
                            if (instruction.Operand is Instruction[] targets && targets.Any(t => !instructions.Contains(t))) throw new InvalidDataException("Dangling switch.");
                            if (instruction.Operand is MethodReference mr && mr.DeclaringType.FullName == typeof(Bridge).FullName) hooks.Add(mr.Name);
                        }
                    }
                    foreach (var nested in type.NestedTypes) Scan(nested);
                }
                foreach (var type in assembly.MainModule.Types) Scan(type);
                string[] expected = { "OnFrame", "WorldShown", "WorldDisposed", "TrySkipHero", "OptionsCreated", "CommandRefreshing", "BuildingKeyDown", "IgnoreDamage" };
                if (!hooks.SetEquals(expected)) throw new InvalidDataException("Patched hook verification failed.");
            }
        }
    }
}
