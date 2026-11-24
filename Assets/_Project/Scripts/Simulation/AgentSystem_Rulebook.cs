using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the five audience-editable Rule Book rules to the simulation.
///
/// The Rule Book UI writes card values into singleton bridges; this system
/// reads them once per tick (PrepareTick) into enum snapshots so the per-pair
/// hot loops never touch strings or singletons.
///
/// Rule 1 (Draw Together) and Rule 2 (Turn Away) are PER-PAIR forces: when the
/// two agents of a pair perceive each other as the slotted identity pair, their
/// mutual attraction is raised/lowered. (Historical note: the original
/// implementation passed the observer as its own pair partner, which made the
/// perceived identity empty and the match always fail — these two rules were
/// silently dead in the exhibited build.)
/// </summary>
public class AgentSystem_Rulebook : MonoBehaviour
{
    [Header("Rule 1: Draw Together")]
    public float drawTogetherPairBonus = 0.35f;

    [Header("Rule 2: Turn Away")]
    public float turnAwayPairPenalty = 0.35f;

    [Header("Rule 4: Condition Spreads")]
    public float spreadsRadius = 2.5f;
    public float spreadsAmountPerSource = 0.03f;
    public float deadSpreadsLonelyAmountPerSource = 0.04f;
    public float deadSpreadsHurtAmountPerSource = 0.015f;

    [Header("Rule 5: When Identity is Condition, Behavior Rises")]
    public float behaviorRiseAmountPerMatch = 0.08f;

    // -------- Per-tick rule snapshot (parsed once, read by hot loops) --------

    bool rule1Valid, rule2Valid, becomesValid;
    PerceivedIdentity rule1A, rule1B, rule2A, rule2B;
    PerceivedIdentity becomesFrom, becomesTo;

    /// <summary>True if Rule 1 or Rule 2 is active — movement only pays the
    /// per-pair rule cost when this is set.</summary>
    public bool HasActivePairRules => rule1Valid || rule2Valid;

    /// <summary>
    /// The identity two strangers see each other as: "Other", unless the
    /// Becomes rule remaps Other to something else. The movement far-field
    /// treats all non-related pairs as this identity.
    /// </summary>
    public PerceivedIdentity DefaultPerceivedIdentity =>
        (becomesValid && becomesFrom == PerceivedIdentity.Other) ? becomesTo : PerceivedIdentity.Other;

    /// <summary>
    /// Rule 1/2 modifier for a default pair (two mutual strangers). Folded into
    /// the movement far-field coefficient so per-pair rules also act at scale.
    /// </summary>
    public float GetDefaultPairModifier()
    {
        if (!rule1Valid && !rule2Valid) return 0f;

        PerceivedIdentity d = DefaultPerceivedIdentity;
        float modifier = 0f;

        if (rule1Valid && PairMatches(d, d, rule1A, rule1B))
            modifier += drawTogetherPairBonus;

        if (rule2Valid && PairMatches(d, d, rule2A, rule2B))
            modifier -= turnAwayPairPenalty;

        return modifier;
    }

    // Reused buffers (avoid per-tick allocation).
    readonly List<int> countsBuffer = new List<int>();
    readonly List<int> queryBuffer = new List<int>();

    /// <summary>Set by AgentSystem each tick; rule systems record activity here
    /// so the view can visualize what each rule is doing.</summary>
    [System.NonSerialized] public SimulationStats stats;

    /// <summary>Parse the current RuleBookState values into enum snapshots.
    /// Call once per tick before any rule queries.</summary>
    public void PrepareTick()
    {
        becomesFrom = RulebookParse.Identity(RuleBookState.becomesFrom);
        becomesTo = RulebookParse.Identity(RuleBookState.becomesTo);
        becomesValid = becomesFrom != PerceivedIdentity.None && becomesTo != PerceivedIdentity.None;

        rule1A = RulebookParse.Identity(RuleBookState.rule1A);
        rule1B = RulebookParse.Identity(RuleBookState.rule1B);
        rule1Valid = rule1A != PerceivedIdentity.None && rule1B != PerceivedIdentity.None;

        rule2A = RulebookParse.Identity(RuleBookState.rule2A);
        rule2B = RulebookParse.Identity(RuleBookState.rule2B);
        rule2Valid = rule2A != PerceivedIdentity.None && rule2B != PerceivedIdentity.None;
    }

    // ---------------- Rule 3: Becomes (perception substitution) ----------------

    /// <summary>
    /// How <paramref name="observer"/> currently perceives <paramref name="target"/>:
    /// own child → Child, else the social relation (Friend/Enemy/Other), then
    /// filtered through the "Becomes" substitution rule if active.
    /// </summary>
    public PerceivedIdentity GetPerceivedIdentity(AgentData observer, AgentData target)
    {
        if (observer == null || target == null) return PerceivedIdentity.None;

        PerceivedIdentity identity;

        if (observer.kinshipRelations.TryGetValue(target.id, out KinshipType kinship) &&
            kinship == KinshipType.Child)
        {
            identity = PerceivedIdentity.Child;
        }
        else if (observer.socialRelations.TryGetValue(target.id, out SocialRelationType social))
        {
            switch (social)
            {
                case SocialRelationType.Friend: identity = PerceivedIdentity.Friend; break;
                case SocialRelationType.Enemy: identity = PerceivedIdentity.Enemy; break;
                default: identity = PerceivedIdentity.Other; break;
            }
        }
        else
        {
            identity = PerceivedIdentity.Other;
        }

        if (becomesValid && identity == becomesFrom)
            identity = becomesTo;

        return identity;
    }

    // ---------------- Rules 1 + 2: per-pair attraction modifier ----------------

    /// <summary>
    /// Extra attraction (+) or repulsion (−) between a specific pair. A pair
    /// matches when the two agents' mutual perceived identities equal the
    /// slotted identity pair (in either order).
    /// </summary>
    public float GetPairRelationModifier(AgentData a, AgentData b)
    {
        if (!rule1Valid && !rule2Valid) return 0f;

        PerceivedIdentity ab = GetPerceivedIdentity(a, b); // how a sees b
        PerceivedIdentity ba = GetPerceivedIdentity(b, a); // how b sees a

        float modifier = 0f;

        if (rule1Valid && PairMatches(ab, ba, rule1A, rule1B))
        {
            modifier += drawTogetherPairBonus;
            RecordRule1Match(a.id, b.id);
        }

        if (rule2Valid && PairMatches(ab, ba, rule2A, rule2B))
        {
            modifier -= turnAwayPairPenalty;
            RecordRule2Match(a.id, b.id);
        }

        return modifier;
    }

    // Pair rules are evaluated from both directions ((a,b) and (b,a)); record
    // only the a<b direction so each pair counts once per tick.
    void RecordRule1Match(int idA, int idB)
    {
        if (stats == null || idA >= idB) return;

        stats.rule1PairsMatched++;
        if (stats.rule1Pairs.Count < SimulationStats.MaxAttributionEntries)
            stats.rule1Pairs.Add(SimulationStats.PackPair(idA, idB));
    }

    void RecordRule2Match(int idA, int idB)
    {
        if (stats == null || idA >= idB) return;

        stats.rule2PairsMatched++;
        if (stats.rule2Pairs.Count < SimulationStats.MaxAttributionEntries)
            stats.rule2Pairs.Add(SimulationStats.PackPair(idA, idB));
    }

    static bool PairMatches(PerceivedIdentity x, PerceivedIdentity y,
                            PerceivedIdentity slotA, PerceivedIdentity slotB)
    {
        return (x == slotA && y == slotB) || (x == slotB && y == slotA);
    }

    // ---------------- Rule 4: Condition Spreads ----------------

    public void ApplyConditionSpreads(List<AgentData> agents, WorldState world, SpatialGrid grid)
    {
        RuleCondition condition = RulebookParse.Condition(RuleBookState.spreadsCondition);
        if (condition == RuleCondition.None) return;

        countsBuffer.Clear();
        for (int i = 0; i < agents.Count; i++)
            countsBuffer.Add(0);

        if (condition == RuleCondition.Dead)
        {
            // Dead spreads from graves (death markers persist forever, so the
            // effect accumulates over the life of the world — corpse cleanup in
            // AgentSystem does not change this rule's behavior).
            for (int i = 0; i < agents.Count; i++)
            {
                AgentData receiver = agents[i];
                if (receiver == null || !receiver.alive) continue;

                int count = 0;
                var markers = world.deathMarkers;
                for (int j = 0; j < markers.Count; j++)
                {
                    if (Vector2.Distance(receiver.position, markers[j].position) <= spreadsRadius)
                        count++;
                }
                countsBuffer[i] = count;
            }
        }
        else
        {
            // Contagion is local (spreadsRadius) — use the spatial grid instead
            // of scanning every agent against every other agent.
            for (int i = 0; i < agents.Count; i++)
            {
                AgentData receiver = agents[i];
                if (receiver == null || !receiver.alive) continue;

                grid.QueryCircle(agents, receiver.position, spreadsRadius, queryBuffer);

                int count = 0;
                for (int q = 0; q < queryBuffer.Count; q++)
                {
                    int j = queryBuffer[q];
                    if (j == i) continue;

                    if (AgentMatchesCondition(agents[j], condition))
                        count++;
                }
                countsBuffer[i] = count;
            }
        }

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData receiver = agents[i];
            if (receiver == null || !receiver.alive) continue;

            int sourceCount = countsBuffer[i];
            if (sourceCount <= 0) continue;

            ApplySpreadToReceiver(receiver, condition, sourceCount);

            if (stats != null)
            {
                stats.rule4AgentsAffected++;
                if (stats.spreadReceiverIds.Count < SimulationStats.MaxAttributionEntries)
                    stats.spreadReceiverIds.Add(receiver.id);
            }
        }
    }

    static bool AgentMatchesCondition(AgentData agent, RuleCondition condition)
    {
        switch (condition)
        {
            case RuleCondition.Hungry: return agent.alive && agent.hungry > 0f;
            case RuleCondition.Lonely: return agent.alive && agent.lonely > 0f;
            case RuleCondition.Hurt: return agent.alive && agent.hurt > 0f;
            case RuleCondition.Joyful: return agent.alive && agent.joyful > 0f;
            case RuleCondition.Dead: return !agent.alive;
            default: return false;
        }
    }

    void ApplySpreadToReceiver(AgentData receiver, RuleCondition condition, int sourceCount)
    {
        float amount = spreadsAmountPerSource * sourceCount;

        switch (condition)
        {
            case RuleCondition.Hungry:
                receiver.hungry = Mathf.Clamp01(receiver.hungry + amount);
                break;

            case RuleCondition.Lonely:
                receiver.lonely = Mathf.Clamp01(receiver.lonely + amount);
                break;

            case RuleCondition.Hurt:
                receiver.hurt = Mathf.Clamp01(receiver.hurt + amount);
                break;

            case RuleCondition.Joyful:
                receiver.joyful = Mathf.Clamp01(receiver.joyful + amount);
                break;

            case RuleCondition.Dead:
                receiver.lonely = Mathf.Clamp01(receiver.lonely + deadSpreadsLonelyAmountPerSource * sourceCount);
                receiver.hurt = Mathf.Clamp01(receiver.hurt + deadSpreadsHurtAmountPerSource * sourceCount);
                break;
        }
    }

    // ------- Rule 5: When Identity is Condition, Behavior Rises -------

    public void ApplyIdentityConditionBehaviorRises(List<AgentData> agents, WorldState world)
    {
        PerceivedIdentity targetIdentity = RulebookParse.Identity(RuleBookState.rule5Identity);
        RuleCondition targetCondition = RulebookParse.Condition(RuleBookState.rule5Condition);
        RuleBehavior risingBehavior = RulebookParse.Behavior(RuleBookState.rule5Behavior);

        if (targetIdentity == PerceivedIdentity.None ||
            targetCondition == RuleCondition.None ||
            risingBehavior == RuleBehavior.None)
            return;

        // This rule matches world-wide (no radius), so a naive version is
        // O(n²). Decomposition: an observer perceives almost everyone as the
        // default identity — only its sparse relation entries differ. So:
        //   X == default: count = (all matching agents) − self − related
        //                 matching agents perceived as something else
        //   X != default: count = matching agents among the observer's own
        //                 relation entries perceived as X
        // Either way each observer costs O(its relation count), not O(n).
        PerceivedIdentity defaultIdentity = DefaultPerceivedIdentity;

        int totalMatching = 0;
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && AgentMatchesCondition(agents[i], targetCondition))
                totalMatching++;
        }

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData observer = agents[i];
            if (observer == null || !observer.alive) continue;

            int count;

            if (targetIdentity == defaultIdentity)
            {
                count = totalMatching;

                if (AgentMatchesCondition(observer, targetCondition))
                    count--; // observers never count themselves

                foreach (var pair in RelatedTargets(observer, world))
                {
                    if (AgentMatchesCondition(pair, targetCondition) &&
                        GetPerceivedIdentity(observer, pair) != defaultIdentity)
                        count--;
                }
            }
            else
            {
                count = 0;

                foreach (var pair in RelatedTargets(observer, world))
                {
                    if (AgentMatchesCondition(pair, targetCondition) &&
                        GetPerceivedIdentity(observer, pair) == targetIdentity)
                        count++;
                }
            }

            if (count > 0)
            {
                ApplyBehaviorRise(observer, risingBehavior, count);

                if (stats != null)
                {
                    stats.rule5AgentsAffected++;
                    if (stats.rule5ObserverIds.Count < SimulationStats.MaxAttributionEntries)
                        stats.rule5ObserverIds.Add(observer.id);
                }
            }
        }
    }

    /// <summary>
    /// Enumerates the agents this observer has any explicit relation to
    /// (children by kinship + everyone in its social table), deduplicated.
    /// </summary>
    static IEnumerable<AgentData> RelatedTargets(AgentData observer, WorldState world)
    {
        foreach (var pair in observer.kinshipRelations)
        {
            if (pair.Value != KinshipType.Child) continue;
            if (world.agentsById.TryGetValue(pair.Key, out AgentData target))
                yield return target;
        }

        foreach (var pair in observer.socialRelations)
        {
            // Skip ids already yielded via the kinship-Child pass.
            if (observer.GetKinship(pair.Key) == KinshipType.Child) continue;
            if (world.agentsById.TryGetValue(pair.Key, out AgentData target))
                yield return target;
        }
    }

    void ApplyBehaviorRise(AgentData observer, RuleBehavior behavior, int matchCount)
    {
        float amount = behaviorRiseAmountPerMatch * matchCount;

        switch (behavior)
        {
            case RuleBehavior.Love:
                observer.joyful = Mathf.Clamp01(observer.joyful + amount);
                break;

            case RuleBehavior.Violence:
                // hurt feeds the Violence trigger threshold in AgentSystem
                observer.hurt = Mathf.Clamp01(observer.hurt + amount);
                break;

            case RuleBehavior.Silence:
                observer.lonely = Mathf.Clamp01(observer.lonely + amount);
                break;
        }
    }
}
