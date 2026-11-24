using System.Collections.Generic;

/// <summary>
/// Per-tick simulation activity counters, reset at the start of every tick and
/// filled by the simulation systems. The view layer reads these to drive the
/// live behavior chart (L3) and the rule-attribution effects (L2) — making
/// "the rule you just placed is changing the world" visible.
/// </summary>
public class SimulationStats
{
    public int tickIndex;

    // -------- world events this tick --------
    public int births;
    public int attacks;          // individual hits landed
    public int friendshipsFormed;
    public int friendshipsForgotten;
    public int deaths;

    // -------- behavior census this tick (agents currently in each state) --------
    public int agentsInLove;
    public int agentsInViolence;
    public int agentsInSilence;
    public int agentsAlive;

    // -------- Rule Book activity this tick --------
    public int rule1PairsMatched;    // Draw Together
    public int rule2PairsMatched;    // Turn Away
    public int rule4AgentsAffected;  // Condition Spreads receivers
    public int rule5AgentsAffected;  // Behavior Rises observers

    // -------- attribution lists (L2 visuals; capped, reused) --------
    public const int MaxAttributionEntries = 256;

    /// <summary>Packed id pairs (idA &lt;&lt; 32 | idB) matched by Rule 1 this tick.</summary>
    public readonly List<long> rule1Pairs = new List<long>(64);
    /// <summary>Packed id pairs matched by Rule 2 this tick.</summary>
    public readonly List<long> rule2Pairs = new List<long>(64);
    /// <summary>Agents that received a spread condition this tick.</summary>
    public readonly List<int> spreadReceiverIds = new List<int>(64);
    /// <summary>Agents whose behavior pressure rose from Rule 5 this tick.</summary>
    public readonly List<int> rule5ObserverIds = new List<int>(64);

    public void ResetTick(int newTickIndex)
    {
        tickIndex = newTickIndex;

        births = 0;
        attacks = 0;
        friendshipsFormed = 0;
        friendshipsForgotten = 0;
        deaths = 0;

        agentsInLove = 0;
        agentsInViolence = 0;
        agentsInSilence = 0;
        agentsAlive = 0;

        rule1PairsMatched = 0;
        rule2PairsMatched = 0;
        rule4AgentsAffected = 0;
        rule5AgentsAffected = 0;

        rule1Pairs.Clear();
        rule2Pairs.Clear();
        spreadReceiverIds.Clear();
        rule5ObserverIds.Clear();
    }

    public static long PackPair(int a, int b)
    {
        return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }

    public static void UnpackPair(long key, out int a, out int b)
    {
        a = (int)(key >> 32);
        b = (int)(key & 0xFFFFFFFF);
    }
}
