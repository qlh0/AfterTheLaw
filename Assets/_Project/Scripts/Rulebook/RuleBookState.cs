/// <summary>
/// The authoritative store of the five audience-editable rules — plain data,
/// no UI coupling. The rule bar (DisplayModeManager) writes card values here;
/// the simulation (AgentSystem_Rulebook) reads them once per tick.
///
/// Historical note: rules used to flow UI → DropSlot components → singleton
/// bridge MonoBehaviours → simulation, because the exhibition build had a
/// drag-and-drop Rule Book on a second display. When the project moved to a
/// single screen, that indirection was retired for this plain state object.
/// </summary>
public static class RuleBookState
{
    // Rule 1: [A] and [B] draw together
    public static string rule1A = "";
    public static string rule1B = "";

    // Rule 2: [A] and [B] turn away
    public static string rule2A = "";
    public static string rule2B = "";

    // Rule 3: [from] becomes [to] (perceived identity substitution)
    public static string becomesFrom = "";
    public static string becomesTo = "";

    // Rule 4: [condition] spreads
    public static string spreadsCondition = "";

    // Rule 5: when [identity] is [condition], [behavior] rises
    public static string rule5Identity = "";
    public static string rule5Condition = "";
    public static string rule5Behavior = "";

    /// <summary>Reset all rules (called on startup so editor domain-reload
    /// settings can't leak rules between play sessions).</summary>
    public static void Clear()
    {
        rule1A = rule1B = "";
        rule2A = rule2B = "";
        becomesFrom = becomesTo = "";
        spreadsCondition = "";
        rule5Identity = rule5Condition = rule5Behavior = "";
    }
}
