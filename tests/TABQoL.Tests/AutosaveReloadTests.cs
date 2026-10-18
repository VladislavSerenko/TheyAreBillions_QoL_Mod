using System;
using TABQoL;

internal static class AutosaveReloadTests
{
    internal static void Run(Action<bool, string> check)
    {
        var schedule = new AutosaveSchedule();
        schedule.EnterMission("colony:mission-a");
        schedule.Tick(0, true, 10);
        int saves = 0;
        for (int second = 1; second <= 7200; second++)
        {
            // Repeated attempts never last the ten-minute interval. Each
            // replacement level represents the same campaign mission.
            if (second % 120 == 0) schedule.EnterMission("colony:mission-a");
            bool loaded = second % 120 >= 10;
            if (schedule.Tick(second, loaded, 10)) { saves++; schedule.Completed(); }
        }
        check(saves == 11, "two hours of frequent same-mission reloads still produce eleven ten-minute autosaves outside loading");

        schedule = new AutosaveSchedule();
        schedule.EnterMission("colony:mission-a");
        for (int second = 0; second <= 59; second++) schedule.Tick(second, true, 1);
        schedule.EnterMission("colony:mission-a");
        check(schedule.Tick(60, true, 1), "same mission replacement preserves a nearly due autosave");
        schedule.Completed();
        check(!schedule.Tick(61, true, 1), "completed save starts the next interval without a catch-up burst");
        for (int second = 62; second <= 119; second++) schedule.Tick(second, true, 1);
        schedule.EnterMission("colony:mission-b");
        check(!schedule.Tick(120, true, 1), "different campaign mission starts a fresh countdown");
        for (int second = 121; second <= 179; second++) schedule.Tick(second, true, 1);
        check(schedule.Tick(180, true, 1), "new mission saves after its own full interval");
        schedule.Failed();
        check(!schedule.Tick(181, true, 10), "changing interval clears a pending failed save");
        check(!schedule.Tick(182, true, 0), "disabling autosave clears its pending countdown");
        check(!schedule.Tick(183, true, 1), "re-enabling autosave starts fresh");
        for (int second = 184; second <= 242; second++) schedule.Tick(second, true, 1);
        check(!schedule.Tick(500, true, 1), "long suspension does not create an immediate catch-up save");
        check(schedule.Tick(501, true, 1), "a long suspension preserves progress already earned before the gap");
    }
}
