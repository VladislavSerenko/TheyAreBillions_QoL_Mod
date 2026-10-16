using System;
using System.Linq;

namespace TABQoL
{
    internal static class Cheats
    {
        private static object level;
        internal static volatile bool Units, Buildings;

        internal static void Reset() { Units = Buildings = false; level = null; }
        internal static void SetUnits(bool value) { Bridge.RequireMission(); level = Bridge.Level; Units = value; }
        internal static void SetBuildings(bool value) { Bridge.RequireMission(); level = Bridge.Level; Buildings = value; }

        internal static bool IgnoreDamage(object life)
        {
            if ((!Units && !Buildings) || level == null || !ReferenceEquals(level, Bridge.Level)) return false;
            var entity = Reflect.Get(life, "Entity");
            if (entity == null || Reflect.Get(entity, "Team").ToString() != "Player") return false;
            return (Units && Reflect.Type("ZX.Entities.Unit").IsInstanceOfType(entity)) ||
                (Buildings && Reflect.Type("ZX.Entities.Structure").IsInstanceOfType(entity));
        }

        internal static void FillResources()
        {
            Bridge.RequireMission();
            foreach (string resource in new[] { "Gold", "Wood", "Stone", "Iron", "Oil" }) Reflect.Set(Bridge.Level, resource, int.MaxValue);
            Bridge.Notify("Resource storage filled");
        }

        internal static void Heal()
        {
            Bridge.RequireMission();
            int count = 0;
            foreach (var entity in Reflect.Items(Reflect.Get(Bridge.Game, "Entities")).ToArray())
            {
                if (!Reflect.Type("ZX.Entities.Unit").IsInstanceOfType(entity) || Reflect.Get(entity, "Team").ToString() != "Player") continue;
                var life = Reflect.Call(entity, "#=zQcFqp7YsrFgY");
                if (life == null || !(bool)Reflect.Get(life, "IsAlive")) continue;
                Reflect.Set(life, "Life", int.MaxValue);
                count++;
            }
            Bridge.Notify("Units healed: " + count);
        }

        internal static void Win()
        {
            Bridge.RequireMission();
            Bridge.Save("before-cheat-win");
            Ui.CloseModWindows();
            Reflect.Call(Bridge.System, "#=z87KhQl1b3Apo", true);
        }
    }
}
