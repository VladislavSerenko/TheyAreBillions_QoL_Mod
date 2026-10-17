using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace TABQoL.Installer
{
    public static class Patcher
    {
        public static void Prepare(string original, string output)
        {
            string hash = SnapshotStore.Hash(original);
            if (!Compatibility.Supported(hash))
                throw new InvalidDataException("Unsupported or already modified EXE. See the exact supported hashes in README.");
            bool steam = hash == Compatibility.SteamSha256;
            string Symbol(string name) { return Compatibility.Symbol(name, steam); }
            using (var module = ModuleDefMD.Load(original))
            {
                var game = module.Types.Single(t => t.FullName == Symbol(Compatibility.Game));
                var world = module.Types.Single(t => t.FullName == Symbol(Compatibility.WorldSystem));
                MethodDef Get(TypeDef type, string name) { return type.Methods.Single(m => m.Name == Symbol(name) && m.HasBody); }
                var importer = new Importer(module);
                IMethod Hook(string name) { return importer.Import(typeof(Bridge).GetMethod(name)); }

                void Prefix(MethodDef method, params Instruction[] instructions)
                {
                    method.Body.SimplifyMacros(method.Parameters);
                    for (int i = 0; i < instructions.Length; i++) method.Body.Instructions.Insert(i, instructions[i]);
                    method.Body.OptimizeMacros();
                }

                void Postfix(MethodDef method, string hook)
                {
                    method.Body.SimplifyMacros(method.Parameters);
                    foreach (var ret in method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
                    {
                        // Reuse the original return instruction as the hook entry, preserving existing branch targets.
                        ret.OpCode = OpCodes.Ldarg_0;
                        int index = method.Body.Instructions.IndexOf(ret);
                        method.Body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Call, Hook(hook)));
                        method.Body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ret));
                    }
                    method.Body.OptimizeMacros();
                }

                Prefix(Get(game, "OnStartFrame"), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("OnFrame")));
                var control = module.Types.Single(t => t.FullName == "ZX.GUI.ZXControlCommand");
                Prefix(Get(control, "#=zwjLekNY="), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Hook("CommandRefreshing")));
                var mission = module.Types.Single(t => t.FullName == Symbol(Compatibility.MissionSystem));
                var keyDown = Get(mission, "OnKeyDown");
                var keyEntry = keyDown.Body.Instructions.First();
                Prefix(keyDown, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call, Hook("BuildingKeyDown")),
                    Instruction.Create(OpCodes.Brfalse, keyEntry), Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret));
                foreach (var keyUp in new[] { Get(game, "ProcessSpecialKeys_KeyUp"), Get(mission, "OnKeyUp") })
                {
                    var entry = keyUp.Body.Instructions.First();
                    Prefix(keyUp, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call, Hook("GlobalKeyUp")),
                        Instruction.Create(OpCodes.Brfalse, entry), Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret));
                }
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
                options.Body.SimplifyMacros(options.Parameters);
                var constructor = options.Body.Instructions.Single(i => i.OpCode == OpCodes.Newobj &&
                    i.Operand is IMethod mr && mr.DeclaringType.FullName == "ZX.GUI.ZXWindowTab");
                // Keep the new window on the original stack and pass a duplicate to the runtime.
                var duplicate = Instruction.Create(OpCodes.Dup);
                int constructorIndex = options.Body.Instructions.IndexOf(constructor);
                options.Body.Instructions.Insert(constructorIndex + 1, duplicate);
                options.Body.Instructions.Insert(constructorIndex + 2, Instruction.Create(OpCodes.Call, Hook("OptionsCreated")));
                options.Body.OptimizeMacros();

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                // Virtualized game methods resolve existing metadata tokens at runtime.
                // Preserve all original tokens and heap offsets when adding mod hooks.
                var writer = new ModuleWriterOptions(module);
                writer.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
                module.Write(output, writer);
            }
            Verify(output);
            VerifyMetadata(original, output);
        }

        public static void Verify(string patched)
        {
            using (var module = ModuleDefMD.Load(patched))
            {
                var hooks = new HashSet<string>();
                void Scan(TypeDef type)
                {
                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    {
                        var instructions = new HashSet<Instruction>(method.Body.Instructions);
                        foreach (var instruction in instructions)
                        {
                            if (instruction.Operand is Instruction target && !instructions.Contains(target)) throw new InvalidDataException("Dangling branch.");
                            if (instruction.Operand is Instruction[] targets && targets.Any(t => !instructions.Contains(t))) throw new InvalidDataException("Dangling switch.");
                            if (instruction.Operand is IMethod mr && mr.DeclaringType.FullName == typeof(Bridge).FullName) hooks.Add(mr.Name);
                        }
                    }
                    foreach (var nested in type.NestedTypes) Scan(nested);
                }
                foreach (var type in module.Types) Scan(type);
                string[] expected = { "OnFrame", "WorldShown", "WorldDisposed", "TrySkipHero", "OptionsCreated", "CommandRefreshing", "BuildingKeyDown", "IgnoreDamage", "GlobalKeyUp" };
                if (!hooks.SetEquals(expected)) throw new InvalidDataException("Patched hook verification failed.");
            }
        }

        public static void VerifyMetadata(string original, string patched)
        {
            using (var source = ModuleDefMD.Load(original))
            using (var result = ModuleDefMD.Load(patched))
            {
                var providers = source.GetTypes().Cast<IMDTokenProvider>()
                    .Concat(source.GetTypes().SelectMany(t => t.Methods))
                    .Concat(source.GetTypes().SelectMany(t => t.Fields))
                    .Concat(source.GetTypeRefs()).Concat(source.GetMemberRefs());
                foreach (var provider in providers)
                {
                    var preserved = result.ResolveToken(provider.MDToken.Raw);
                    if (preserved == null || preserved.ToString() != provider.ToString())
                        throw new InvalidDataException("Original metadata token changed: " + provider.MDToken);
                }
            }
        }
    }
}
