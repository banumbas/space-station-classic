using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> NPCMaxUpdates =
        CVarDef.Create("npc.max_updates", 128);

    public static readonly CVarDef<bool> NPCEnabled = CVarDef.Create("npc.enabled", true);

    /// <summary>
    ///     Should NPCs pathfind when steering. For debug purposes.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathfinding = CVarDef.Create("npc.pathfinding", true);

    // Starlight start
    /// <summary>
    ///     Per-tick wall-clock budget (seconds) the HTN planner is allowed to spend running plan jobs.
    ///     Lower it to cap NPC planning cost under load. Was a hardcoded 0.004 (4ms) in HTNSystem.
    /// </summary>
    public static readonly CVarDef<float> NPCHTNPlanBudget =
        CVarDef.Create("npc.htn_plan_budget", 0.004f, CVar.SERVERONLY);
    // Starlight end
}
