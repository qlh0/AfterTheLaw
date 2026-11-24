using System;
using System.Collections.Generic;
using System.Globalization;

public enum WorldEventKind { WorldRuleChanged, RelationshipChanged, Conflict, AgentDied }
public enum WorldEventVisibility { Public, Participants }
public enum WorldDeathCause { Age, Health, PopulationLimit, Conflict }

public readonly struct WorldEventActor
{
    public readonly int Id;
    public readonly int Generation;

    public WorldEventActor(AgentData agent)
    {
        Id = agent.id;
        Generation = agent.generation;
    }

    public override string ToString() => $"G{Generation}-{Id}";
}

public sealed class WorldEvent
{
    public string Id { get; internal set; }
    public WorldEventKind Kind { get; }
    public WorldEventVisibility Visibility { get; }
    public int Tick { get; }
    public float Time { get; }
    public int LastTick { get; internal set; }
    public float LastTime { get; internal set; }
    public WorldEventActor? Actor { get; }
    public WorldEventActor? Target { get; }
    public WorldDeathCause? DeathCause { get; internal set; }
    public string RelatedConflictId { get; internal set; }
    public int HitCount { get; internal set; }
    public float Damage { get; internal set; }
    public IReadOnlyList<WorldRuleChange> RuleChanges { get; internal set; } = Array.Empty<WorldRuleChange>();

    readonly List<WorldRelationChange> relations = new List<WorldRelationChange>();
    public IReadOnlyList<WorldRelationChange> RelationChanges => relations;
    internal void AddRelation(WorldRelationChange change) => relations.Add(change);

    internal WorldEvent(WorldEventKind kind, WorldEventVisibility visibility, int tick, float time,
                        WorldEventActor? actor = null, WorldEventActor? target = null)
    {
        Kind = kind;
        Visibility = visibility;
        Tick = LastTick = tick;
        Time = LastTime = time;
        Actor = actor;
        Target = target;
    }
}

public readonly struct WorldRelationChange
{
    public readonly WorldEventActor Observer;
    public readonly WorldEventActor Target;
    public readonly SocialRelationType Before;
    public readonly SocialRelationType After;

    public WorldRelationChange(WorldEventActor observer, WorldEventActor target,
                               SocialRelationType before, SocialRelationType after)
    {
        Observer = observer;
        Target = target;
        Before = before;
        After = after;
    }
}

public readonly struct WorldRuleChange
{
    public readonly string Key;
    public readonly string Before;
    public readonly string After;

    public WorldRuleChange(string key, string before, string after)
    {
        Key = key;
        Before = before;
        After = after;
    }
}

public sealed class WorldEventLog
{
    const int Capacity = 200;
    const int ConflictGapTicks = 2;
    const int RelationStableTicks = 3;
    const float SignificantDamage = 0.10f;

    readonly List<WorldEvent> events = new List<WorldEvent>(Capacity);
    readonly Dictionary<(int, int), WorldEvent> conflicts = new Dictionary<(int, int), WorldEvent>();
    readonly Dictionary<int, (WorldEvent conflict, WorldEventActor attacker)> lethalHits =
        new Dictionary<int, (WorldEvent, WorldEventActor)>();
    readonly Dictionary<(int, int), PendingRelation> relations = new Dictionary<(int, int), PendingRelation>();
    readonly Dictionary<string, (float before, float after)> pendingParameters =
        new Dictionary<string, (float, float)>();
    readonly List<(int, int)> expired = new List<(int, int)>();

    static readonly string[] RuleKeys = { "draw_together", "turn_away", "becomes", "spreads", "behavior_rises" };
    readonly (string, string, string)[] activeRules = new (string, string, string)[RuleKeys.Length];

    int sequence;
    float currentTime;

    public string WorldId { get; } = Guid.NewGuid().ToString("N");
    public int Tick { get; private set; }
    public int Revision { get; private set; }
    public IReadOnlyList<WorldEvent> Events => events;

    readonly struct PendingRelation
    {
        public readonly WorldRelationChange Change;
        public readonly int ChangedTick;
        public readonly WorldEvent Conflict;

        public PendingRelation(WorldRelationChange change, int changedTick, WorldEvent conflict)
        {
            Change = change;
            ChangedTick = changedTick;
            Conflict = conflict;
        }
    }

    public void SetRuleBaseline()
    {
        for (int i = 0; i < RuleKeys.Length; i++)
            activeRules[i] = ActiveRule(i);
    }

    public void BeginTick(float time)
    {
        Tick++;
        currentTime = time;
        List<WorldRuleChange> changes = null;
        for (int i = 0; i < RuleKeys.Length; i++)
        {
            var current = ActiveRule(i);
            if (current == activeRules[i]) continue;
            changes ??= new List<WorldRuleChange>();
            changes.Add(new WorldRuleChange(RuleKeys[i], Encode(activeRules[i]), Encode(current)));
            activeRules[i] = current;
        }

        foreach (var entry in pendingParameters)
        {
            (float before, float after) = entry.Value;
            if (before == after) continue;
            changes ??= new List<WorldRuleChange>();
            changes.Add(new WorldRuleChange(entry.Key, FormatParameter(before), FormatParameter(after)));
        }
        pendingParameters.Clear();

        if (changes != null)
            Publish(new WorldEvent(WorldEventKind.WorldRuleChanged, WorldEventVisibility.Public, Tick, currentTime)
            {
                RuleChanges = changes
            });
    }

    public void QueueParameterChange(string name, float before, float after)
    {
        if (before == after) return;
        if (pendingParameters.TryGetValue(name, out var existing))
            before = existing.before;
        pendingParameters[name] = (before, after);
    }

    public WorldEvent RecordHit(AgentData attacker, AgentData target, float healthBefore)
    {
        float damage = healthBefore - Math.Max(0f, target.hp);
        if (damage <= 0f) return null;

        var key = (Math.Min(attacker.id, target.id), Math.Max(attacker.id, target.id));
        if (!conflicts.TryGetValue(key, out WorldEvent conflict) || Tick - conflict.LastTick > ConflictGapTicks)
        {
            conflict = new WorldEvent(WorldEventKind.Conflict, WorldEventVisibility.Participants,
                Tick, currentTime, new WorldEventActor(attacker), new WorldEventActor(target));
            conflicts[key] = conflict;
        }

        conflict.HitCount++;
        conflict.Damage += damage;
        conflict.LastTick = Tick;
        conflict.LastTime = currentTime;

        bool lethal = target.hp <= 0f;
        bool breaksFriendship = attacker.GetSocial(target.id) == SocialRelationType.Friend ||
                                target.GetSocial(attacker.id) == SocialRelationType.Friend;
        if (conflict.Id != null)
            Revision++;
        else if (lethal || breaksFriendship || conflict.HitCount >= 2 || conflict.Damage >= SignificantDamage)
            Publish(conflict);

        if (lethal)
            lethalHits[target.id] = (conflict, new WorldEventActor(attacker));
        return conflict;
    }

    public void SetRelationship(AgentData observer, AgentData target, SocialRelationType after,
                                WorldEvent conflict = null)
    {
        SocialRelationType before = observer.GetSocial(target.id);
        if (before == after) return;

        observer.SetSocial(target.id, after);
        var key = (observer.id, target.id);
        if (relations.TryGetValue(key, out PendingRelation pending))
            before = pending.Change.Before;

        if (before == after)
            relations.Remove(key);
        else
            relations[key] = new PendingRelation(
                new WorldRelationChange(new WorldEventActor(observer), new WorldEventActor(target), before, after),
                Tick, conflict);
    }

    public void RecordDeath(AgentData agent, WorldDeathCause cause)
    {
        WorldEventActor? attacker = null;
        string conflictId = null;
        if (lethalHits.Remove(agent.id, out var hit) && cause == WorldDeathCause.Health)
        {
            cause = WorldDeathCause.Conflict;
            attacker = hit.attacker;
            conflictId = hit.conflict.Id;
        }

        Publish(new WorldEvent(WorldEventKind.AgentDied, WorldEventVisibility.Participants,
            Tick, currentTime, new WorldEventActor(agent), attacker)
        {
            DeathCause = cause,
            RelatedConflictId = conflictId
        });
    }

    public void EndTick(WorldState world)
    {
        expired.Clear();
        foreach (var pair in relations)
        {
            PendingRelation pending = pair.Value;
            if (!world.agentsById.TryGetValue(pair.Key.Item1, out var observer) ||
                !world.agentsById.TryGetValue(pair.Key.Item2, out var target))
            {
                expired.Add(pair.Key);
                continue;
            }
            if (observer.alive && target.alive && Tick - pending.ChangedTick < RelationStableTicks)
                continue;

            WorldRelationChange change = pending.Change;
            if (observer.GetSocial(target.id) == change.After)
                PublishRelation(change, pending.Conflict);

            expired.Add(pair.Key);
        }
        foreach (var key in expired)
            relations.Remove(key);

        expired.Clear();
        foreach (var pair in conflicts)
        {
            if (Tick - pair.Value.LastTick >= ConflictGapTicks)
                expired.Add(pair.Key);
        }
        foreach (var key in expired)
            conflicts.Remove(key);
    }

    void PublishRelation(WorldRelationChange change, WorldEvent conflict)
    {
        if (conflict?.Id != null && events.Contains(conflict))
        {
            conflict.AddRelation(change);
            Revision++;
            return;
        }

        var entry = new WorldEvent(WorldEventKind.RelationshipChanged, WorldEventVisibility.Participants,
            Tick, currentTime, change.Observer, change.Target);
        entry.AddRelation(change);
        Publish(entry);
    }

    void Publish(WorldEvent entry)
    {
        entry.Id = $"{WorldId}:{++sequence}";
        if (events.Count == Capacity)
            events.RemoveAt(0);
        events.Add(entry);
        Revision++;
    }

    static (string, string, string) ActiveRule(int index)
    {
        switch (index)
        {
            case 0: return IdentityPair(RuleBookState.rule1A, RuleBookState.rule1B, symmetric: true);
            case 1: return IdentityPair(RuleBookState.rule2A, RuleBookState.rule2B, symmetric: true);
            case 2:
                return RuleBookState.becomesFrom != RuleBookState.becomesTo
                    ? IdentityPair(RuleBookState.becomesFrom, RuleBookState.becomesTo, symmetric: false) : default;
            case 3:
                return RulebookParse.Condition(RuleBookState.spreadsCondition) != RuleCondition.None
                    ? (RuleBookState.spreadsCondition, null, null) : default;
            case 4:
                return RulebookParse.Identity(RuleBookState.rule5Identity) != PerceivedIdentity.None &&
                       RulebookParse.Condition(RuleBookState.rule5Condition) != RuleCondition.None &&
                       RulebookParse.Behavior(RuleBookState.rule5Behavior) != RuleBehavior.None
                    ? (RuleBookState.rule5Identity, RuleBookState.rule5Condition, RuleBookState.rule5Behavior)
                    : default;
            default: return default;
        }
    }

    static (string, string, string) IdentityPair(string a, string b, bool symmetric)
    {
        if (RulebookParse.Identity(a) == PerceivedIdentity.None ||
            RulebookParse.Identity(b) == PerceivedIdentity.None)
            return default;

        return symmetric && string.CompareOrdinal(a, b) > 0 ? (b, a, null) : (a, b, null);
    }

    static string Encode((string first, string second, string third) value) =>
        value.first == null ? "" : value.second == null ? value.first :
        value.third == null ? $"{value.first}|{value.second}" : $"{value.first}|{value.second}|{value.third}";

    static string FormatParameter(float value) => value.ToString("G", CultureInfo.InvariantCulture);
}

