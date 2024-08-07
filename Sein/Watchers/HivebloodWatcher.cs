﻿using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;

namespace Sein.Watchers;

internal class HivebloodWatcher
{
    public delegate void SkinToggled(bool on);
    public static event SkinToggled? OnSkinToggled;

    public static void Hook()
    {
        ItemChanger.Events.AddFsmEdit(new("Health", "Hive Health Regen"), WatchHiveblood);
        ItemChanger.Events.AddFsmEdit(new("Blue Health Hive", "blue_health_display"), WatchJoniblood);
        ItemChanger.Events.AddFsmEdit(new("Blue Health Hive(Clone)", "blue_health_display"), WatchJoniblood);
    }

    public static bool HivebloodHealing { get; private set; }

    private static void WatchHiveblood(PlayMakerFSM fsm)
    {
        fsm.GetState("Start Recovery").AddFirstAction(new Lambda(() => HivebloodHealing = true));

        foreach (string state in new string[] { "Init", "Idle", "Recover", "Cancel Recovery" })
            fsm.GetState(state).AddFirstAction(new Lambda(() => HivebloodHealing = false));
    }

    private static void WatchJoniblood(PlayMakerFSM fsm)
    {
        fsm.GetState("Regen 1").AddFirstAction(new Lambda(() => HivebloodHealing = true));

        foreach (string state in new string[] { "Destroy Self", "Heal", })
            fsm.GetState(state).AddFirstAction(new Lambda(() => HivebloodHealing = false));
    }
}
