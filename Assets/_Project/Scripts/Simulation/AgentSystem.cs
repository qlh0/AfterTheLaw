using System.Collections.Generic;
using UnityEngine;

public class AgentSystem : MonoBehaviour
{
    public AgentSpawner spawner;
    public AgentSystem_Rulebook rulebookSystem;
    public AgentSystem_FoodHungry foodHungrySystem;
    public AgentSystem_MovementSystem movementSystem;

    [Header("Relation Change")]
    public float tickInterval = 0.1f;
    public float friendDistance = 2.0f;
    public float forgetDistance = 4.0f;
    public float befriendTime = 2.0f;
    public float forgetTime = 3.0f;

    [Header("Population / Birth Safety")]
    public bool useBirthRateLimit = true;
    public int maxBirthsPerTick = 3;
    public bool usePopulationCap = true;
    public int maxAliveAgents = 120;
    public bool useOverflowCull = false;
    public int targetAliveAfterCull = 100;

    [Header("Church Spawn")]
    public bool enableChurchSpawn = true;
    [Range(0f, 1f)] public float lonelyChurchSpawnChance = 0.03f;
    public float lonelyChurchThreshold = 0.85f;
    public float minDistanceBetweenChurches = 2.0f;
    public bool oneChurchPerFounder = true;

    [Header("Church Lonely Spread")]
    public bool enableChurchLonelySpread = true;
    public float churchLonelySpreadRadius = 3.0f;
    public float churchLonelySpreadPerTick = 0.03f;

    [Header("Love Bond")]
    public float loveBondDistance = 1.2f;
    public float loveBondTime = 1.0f;

    [Header("Birth")]
    public float birthDistance = 0.9f;
    public float birthLoveTime = 2.0f;
    public float reproductionCooldownTime = 6.0f;
    public float birthMinFoodRequired = 0.75f;
    public float birthFoodCost = 0.35f;

    [Header("Condition Check Radius")]
    public float lonelinessCheckRadius = 2.5f;

    [Header("Joyful Condition")]
    public float joyfulFoodThreshold = 0.75f;
    public float joyfulMaxHurtThreshold = 0.2f;
    public float joyfulRisePerTick = 0.03f;
    public float joyfulDecayPerTick = 0.02f;

    [Header("Behavior Thresholds")]
    public float violenceThreshold = 0.3f;
    public float silenceThreshold = 0.5f;
    public float loveThreshold = 0.6f;
    public float hurtViolenceThreshold = 0.5f;

    [Header("Combat")]
    public float attackDistance = 0.8f;
    public float attackDamage = 0.15f;

    [Header("Combat Escape")]
    public bool enableEscape = true;
    [Range(0f, 1f)] public float escapeChance = 0.65f;
    public float escapeVelocityBoost = 2.5f;
    public float escapeMinDirectionDistance = 0.01f;

    [Header("World Entity Caps")]
    [Tooltip("Oldest homes/graves are removed beyond these caps. Prevents the unbounded sprite pileup that tanked framerate during birth explosions on long exhibition runs. Note: 'Dead spreads' sources from graves, so the cap also bounds that rule's accumulation.")]
    public bool capWorldEntities = true;
    public int maxHomes = 250;
    public int maxDeathMarkers = 300;

    [Header("Corpse Cleanup")]
    [Tooltip("Dead agents stay in the list this many game-seconds (so visuals and death rules can react), then are removed so simulation loops stop iterating corpses. Death markers (graves) persist forever regardless.")]
    public float corpseLingerSeconds = 5f;

    [Header("Spatial Grid")]
    [Tooltip("Cell size of the shared neighbor-query grid. Should be >= the largest local query radius (loneliness / spreads = 2.5).")]
    public float gridCellSize = 2.5f;

    // Pair timers are keyed by a packed long (low id in high bits) instead of
    // strings — avoids per-tick string allocation in hot loops.
    Dictionary<long, float> pairLoveTimers = new Dictionary<long, float>();
    HashSet<int> churchFounderIds = new HashSet<int>();

    // Shared spatial grid, rebuilt each tick; all local-neighborhood systems
    // query this instead of scanning all agents.
    public SpatialGrid Grid { get; } = new SpatialGrid();

    // Reused per-tick collections (avoid GC pressure).
    readonly HashSet<long> markedCombatPairs = new HashSet<long>();
    readonly List<int> removedAgentIdsThisTick = new List<int>();
    readonly List<long> staleLoveTimerKeys = new List<long>();
    readonly List<int> queryBuffer = new List<int>();
    readonly HashSet<int> nearIdsBuffer = new HashSet<int>();
    readonly List<int> timerEditBuffer = new List<int>();
    readonly List<int> forgetBuffer = new List<int>();

    void Awake()
    {
        if (rulebookSystem == null)
            rulebookSystem = GetComponent<AgentSystem_Rulebook>();

        if (foodHungrySystem == null)
            foodHungrySystem = GetComponent<AgentSystem_FoodHungry>();

        if (movementSystem == null)
            movementSystem = GetComponent<AgentSystem_MovementSystem>();

        if (movementSystem != null)
            movementSystem.owner = this;
    }

    public void ResetRuntimeState()
    {
        pairLoveTimers.Clear();
        churchFounderIds.Clear();
    }

    int tickCounter = 0;

    public void RunSimulationStep()
    {
        var world = spawner.worldState;
        var agents = world.agents;

        world.EventLog.BeginTick(world.worldTime);
        world.stats.ResetTick(++tickCounter);
        if (rulebookSystem != null)
            rulebookSystem.stats = world.stats;

        world.combatMarkers.Clear();

        UpdateAgingAndDeath(agents, world);
        RemoveExpiredCorpses(agents, world);
        RebuildGrid(agents);
        UpdateCooldowns(agents);
        ResetTransientHurtState(agents);

        if (foodHungrySystem != null)
        {
            Vector2 foodMin = new Vector2(-10f, -6f);
            Vector2 foodMax = new Vector2(10f, 6f);

            if (movementSystem != null)
            {
                movementSystem.GetWorldBounds(out foodMin, out foodMax);
                foodMin.x += movementSystem.boundsPadding;
                foodMin.y += movementSystem.boundsPadding;
                foodMax.x -= movementSystem.boundsPadding;
                foodMax.y -= movementSystem.boundsPadding;
            }

            foodHungrySystem.UpdateFoodRespawn(world, tickInterval, foodMin, foodMax);
            foodHungrySystem.UpdateFood(agents);
            foodHungrySystem.ProcessFoodPickup(agents, world.foods);
        }

        UpdateConditions(agents);
        ProcessChurchSpawning(agents, world);
        ApplyChurchLonelySpread(agents, world);

        if (rulebookSystem != null)
        {
            rulebookSystem.PrepareTick(); // parse rule slots once per tick
            rulebookSystem.ApplyConditionSpreads(agents, world, Grid);
            rulebookSystem.ApplyIdentityConditionBehaviorRises(agents, world);
        }

        UpdateBehaviors(agents);
        ProcessViolence(agents, world);
        ProcessLoveBond(agents);
        ProcessBirth(agents);

        if (useOverflowCull)
            ApplyPopulationOverflowCull(agents, world);

        if (capWorldEntities)
        {
            while (world.homes.Count > maxHomes)
                world.homes.RemoveAt(0); // oldest first
            while (world.deathMarkers.Count > maxDeathMarkers)
                world.deathMarkers.RemoveAt(0);
        }

        UpdateRelationChanges(agents, world);

        // Rebuild once more so the movement far-field aggregates see the
        // behaviors (Silence) decided THIS tick, not last tick's.
        RebuildGrid(agents);

        if (movementSystem != null)
            movementSystem.UpdateMovement(agents, world, tickInterval, Grid);

        world.EventLog.EndTick(world);
    }

    void RebuildGrid(List<AgentData> agents)
    {
        Vector2 boundsMin = new Vector2(-10f, -6f);
        Vector2 boundsMax = new Vector2(10f, 6f);

        if (movementSystem != null)
            movementSystem.GetWorldBounds(out boundsMin, out boundsMax);

        Grid.Rebuild(agents, gridCellSize, boundsMin, boundsMax);
    }

    /// <summary>
    /// Removes agents that have been dead longer than corpseLingerSeconds.
    /// Without this, every O(n²) loop keeps iterating corpses forever — over a
    /// full exhibition day the agent list grows unboundedly and the simulation
    /// slows to a crawl. Graves (death markers) persist; only the agent entries go.
    /// </summary>
    void RemoveExpiredCorpses(List<AgentData> agents, WorldState world)
    {
        removedAgentIdsThisTick.Clear();

        for (int i = agents.Count - 1; i >= 0; i--)
        {
            AgentData agent = agents[i];
            if (agent == null || agent.alive) continue;
            if (world.worldTime - agent.deathTime < corpseLingerSeconds) continue;

            removedAgentIdsThisTick.Add(agent.id);
            world.RemoveAgentAt(i);
        }

        if (removedAgentIdsThisTick.Count == 0) return;

        // Drop references to removed agents from everyone's relation tables
        // and from pair timers, so lookups stay clean and memory doesn't leak.
        for (int i = 0; i < agents.Count; i++)
        {
            AgentData agent = agents[i];

            for (int r = 0; r < removedAgentIdsThisTick.Count; r++)
            {
                int removedId = removedAgentIdsThisTick[r];
                agent.kinshipRelations.Remove(removedId);
                agent.socialRelations.Remove(removedId);
                agent.closenessTimers.Remove(removedId);
                agent.distanceTimers.Remove(removedId);
            }
        }

        staleLoveTimerKeys.Clear();
        foreach (var pair in pairLoveTimers)
        {
            int idA = (int)(pair.Key >> 32);
            int idB = (int)(pair.Key & 0xFFFFFFFF);

            for (int r = 0; r < removedAgentIdsThisTick.Count; r++)
            {
                if (removedAgentIdsThisTick[r] == idA || removedAgentIdsThisTick[r] == idB)
                {
                    staleLoveTimerKeys.Add(pair.Key);
                    break;
                }
            }
        }

        for (int i = 0; i < staleLoveTimerKeys.Count; i++)
            pairLoveTimers.Remove(staleLoveTimerKeys[i]);
    }

    void ResetTransientHurtState(List<AgentData> agents)
    {
        foreach (var agent in agents)
        {
            if (!agent.alive) continue;
            agent.hurt = 0f;
        }
    }

    void UpdateCooldowns(List<AgentData> agents)
    {
        foreach (var agent in agents)
        {
            if (!agent.alive) continue;
            if (agent.reproductionCooldown > 0f)
                agent.reproductionCooldown -= tickInterval;
        }
    }

    void UpdateAgingAndDeath(List<AgentData> agents, WorldState world)
    {
        foreach (var agent in agents)
        {
            if (!agent.alive) continue;

            agent.age += tickInterval;

            if (agent.age >= agent.maxAge || agent.hp <= 0f)
                KillAgent(agent, world, agent.hp <= 0f ? WorldDeathCause.Health : WorldDeathCause.Age);
        }
    }

    void UpdateConditions(List<AgentData> agents)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            AgentData self = agents[i];
            if (!self.alive) continue;

            // Loneliness only needs "is anyone near me?" — early-exit grid probe
            // instead of collecting every neighbor (huge win at high density).
            bool hasCompany = Grid.HasOtherWithin(agents, i, self.position, lonelinessCheckRadius);

            if (!hasCompany)
                self.lonely += ConditionSpeedConfig.MediumRise;
            else
                self.lonely -= ConditionSpeedConfig.MediumDecay;

            bool isFed = self.food >= joyfulFoodThreshold;
            bool isSafe = self.hurt <= joyfulMaxHurtThreshold;

            if (isFed && isSafe)
                self.joyful += joyfulRisePerTick;
            else
                self.joyful -= joyfulDecayPerTick;

            self.hungry -= ConditionSpeedConfig.SlowDecay;

            self.hungry = Mathf.Clamp01(self.hungry);
            self.lonely = Mathf.Clamp01(self.lonely);
            self.joyful = Mathf.Clamp01(self.joyful);
            self.hurt = Mathf.Clamp01(self.hurt);
        }
    }

    void ProcessChurchSpawning(List<AgentData> agents, WorldState world)
    {
        if (!enableChurchSpawn) return;

        if (world.nextAgentId == 0)
            churchFounderIds.Clear();

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData agent = agents[i];
            if (agent == null || !agent.alive) continue;
            if (agent.lonely < lonelyChurchThreshold) continue;

            if (oneChurchPerFounder && churchFounderIds.Contains(agent.id))
                continue;

            if (Random.value > lonelyChurchSpawnChance)
                continue;

            Vector2 churchPos = agent.position;
            if (movementSystem != null)
                churchPos = movementSystem.ClampToWorldBounds(churchPos);

            if (IsTooCloseToExistingChurch(churchPos, world))
                continue;

            ChurchData church = new ChurchData
            {
                id = world.nextChurchId++,
                position = churchPos,
                founderAgentId = agent.id
            };

            world.churches.Add(church);
            churchFounderIds.Add(agent.id);
        }
    }

    void ApplyChurchLonelySpread(List<AgentData> agents, WorldState world)
    {
        if (!enableChurchLonelySpread) return;
        if (world.churches == null || world.churches.Count == 0) return;

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData agent = agents[i];
            if (agent == null || !agent.alive) continue;

            float addedLonely = 0f;

            for (int j = 0; j < world.churches.Count; j++)
            {
                ChurchData church = world.churches[j];
                float dist = Vector2.Distance(agent.position, church.position);

                if (dist <= churchLonelySpreadRadius)
                    addedLonely += churchLonelySpreadPerTick;
            }

            if (addedLonely > 0f)
                agent.lonely = Mathf.Clamp01(agent.lonely + addedLonely);
        }
    }

    bool IsTooCloseToExistingChurch(Vector2 pos, WorldState world)
    {
        for (int i = 0; i < world.churches.Count; i++)
        {
            float dist = Vector2.Distance(pos, world.churches[i].position);
            if (dist < minDistanceBetweenChurches)
                return true;
        }

        return false;
    }

    void UpdateBehaviors(List<AgentData> agents)
    {
        SimulationStats stats = spawner.worldState.stats;

        foreach (var agent in agents)
        {
            if (!agent.alive)
            {
                agent.currentBehavior = BehaviorType.None;
                continue;
            }

            stats.agentsAlive++;

            if (agent.behaviorTimer > 0f)
            {
                agent.behaviorTimer -= tickInterval;
                continue;
            }

            BehaviorType newBehavior = BehaviorType.None;

            if (agent.hungry >= violenceThreshold)
                newBehavior = BehaviorType.Violence;

            if (agent.hurt >= hurtViolenceThreshold)
                newBehavior = BehaviorType.Violence;

            if (agent.lonely >= silenceThreshold)
                newBehavior = BehaviorType.Silence;

            if (agent.joyful >= loveThreshold)
                newBehavior = BehaviorType.Love;

            if (newBehavior != BehaviorType.None)
            {
                agent.currentBehavior = newBehavior;
                agent.behaviorTimer = BehaviorConfig.GetDuration(newBehavior);
            }
        }

        // Behavior census (after this tick's assignments).
        foreach (var agent in agents)
        {
            if (!agent.alive) continue;

            switch (agent.currentBehavior)
            {
                case BehaviorType.Love: stats.agentsInLove++; break;
                case BehaviorType.Violence: stats.agentsInViolence++; break;
                case BehaviorType.Silence: stats.agentsInSilence++; break;
            }
        }
    }

    void ProcessViolence(List<AgentData> agents, WorldState world)
    {
        markedCombatPairs.Clear();

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData attacker = agents[i];

            if (!attacker.alive) continue;
            if (attacker.currentBehavior != BehaviorType.Violence) continue;

            Grid.QueryCircle(agents, attacker.position, attackDistance, queryBuffer);

            for (int q = 0; q < queryBuffer.Count; q++)
            {
                int j = queryBuffer[q];
                if (j == i) continue;

                AgentData target = agents[j];
                if (target.currentBehavior == BehaviorType.Silence) continue;

                float healthBefore = target.hp;
                target.hp -= attackDamage;
                target.hurt = 1f;
                world.stats.attacks++;

                WorldEvent conflict = world.EventLog.RecordHit(attacker, target, healthBefore);
                world.EventLog.SetRelationship(attacker, target, SocialRelationType.Enemy, conflict);
                world.EventLog.SetRelationship(target, attacker, SocialRelationType.Enemy, conflict);

                if (enableEscape)
                {
                    Vector2 awayDir = target.position - attacker.position;

                    if (awayDir.magnitude > escapeMinDirectionDistance && Random.value < escapeChance)
                        target.velocity += awayDir.normalized * escapeVelocityBoost;
                }

                long pairKey = GetPairKey(attacker.id, target.id);
                if (!markedCombatPairs.Contains(pairKey))
                {
                    markedCombatPairs.Add(pairKey);

                    world.combatMarkers.Add(new CombatMarkerData
                    {
                        id = world.nextCombatMarkerId++,
                        position = (attacker.position + target.position) * 0.5f,
                        agentAId = attacker.id,
                        agentBId = target.id
                    });
                }
            }
        }
    }

    void ProcessLoveBond(List<AgentData> agents)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            AgentData self = agents[i];

            if (!self.alive) continue;
            if (self.currentBehavior != BehaviorType.Love) continue;

            Grid.QueryCircle(agents, self.position, loveBondDistance, queryBuffer);

            for (int q = 0; q < queryBuffer.Count; q++)
            {
                int j = queryBuffer[q];
                if (j == i) continue;

                AgentData other = agents[j];
                if (other.currentBehavior == BehaviorType.Silence) continue;

                if (!self.closenessTimers.ContainsKey(other.id))
                    self.closenessTimers[other.id] = 0f;

                self.closenessTimers[other.id] += tickInterval;

                if (self.GetKinship(other.id) != KinshipType.None)
                    continue;

                if (self.closenessTimers[other.id] >= loveBondTime &&
                    self.GetSocial(other.id) != SocialRelationType.Friend)
                {
                    spawner.worldState.EventLog.SetRelationship(self, other, SocialRelationType.Friend);
                    spawner.worldState.stats.friendshipsFormed++;
                }
            }
        }
    }

    void ProcessBirth(List<AgentData> agents)
    {
        int birthsThisTick = 0;

        // Count alive agents ONCE per tick and maintain the count incrementally.
        // The previous version re-counted the whole list inside the pair loop,
        // making birth processing effectively O(n³) at scale.
        int aliveCount = CountAliveAgents(agents);

        for (int i = 0; i < agents.Count; i++)
        {
            if (useBirthRateLimit && birthsThisTick >= maxBirthsPerTick)
                break;

            AgentData a = agents[i];
            if (!a.alive) continue;
            if (a.currentBehavior != BehaviorType.Love) continue;
            if (a.reproductionCooldown > 0f) continue;
            if (a.food < birthMinFoodRequired) continue;

            for (int j = i + 1; j < agents.Count; j++)
            {
                if (useBirthRateLimit && birthsThisTick >= maxBirthsPerTick)
                    break;

                if (usePopulationCap && aliveCount >= maxAliveAgents)
                    return;

                AgentData b = agents[j];
                if (!b.alive) continue;
                if (b.currentBehavior != BehaviorType.Love) continue;
                if (b.reproductionCooldown > 0f) continue;
                if (b.food < birthMinFoodRequired) continue;

                if (a.GetSocial(b.id) != SocialRelationType.Friend) continue;
                if (b.GetSocial(a.id) != SocialRelationType.Friend) continue;

                float dist = Vector2.Distance(a.position, b.position);
                if (dist > birthDistance) continue;

                long pairKey = GetPairKey(a.id, b.id);

                if (!pairLoveTimers.ContainsKey(pairKey))
                    pairLoveTimers[pairKey] = 0f;

                pairLoveTimers[pairKey] += tickInterval;

                if (pairLoveTimers[pairKey] >= birthLoveTime)
                {
                    a.food = Mathf.Max(0f, a.food - birthFoodCost);
                    b.food = Mathf.Max(0f, b.food - birthFoodCost);

                    if (!usePopulationCap || aliveCount < maxAliveAgents)
                    {
                        SpawnChild(a, b);
                        birthsThisTick++;
                        aliveCount++;
                    }

                    pairLoveTimers[pairKey] = 0f;
                    a.reproductionCooldown = reproductionCooldownTime;
                    b.reproductionCooldown = reproductionCooldownTime;
                }
            }
        }
    }

    void SpawnChild(AgentData parentA, AgentData parentB)
    {
        Vector2 birthPos = (parentA.position + parentB.position) * 0.5f;

        if (movementSystem != null)
            birthPos = movementSystem.ClampToWorldBounds(birthPos);

        AgentData child = spawner.CreateBaseAgent(birthPos);

        child.generation = Mathf.Max(parentA.generation, parentB.generation) + 1;
        child.maxAge = Random.Range(20f, 40f);
        child.hp = 1f;
        child.maxHP = 1f;
        child.food = 1f;
        child.maxFood = 1f;

        // Relations are sparse — a newborn only needs entries for its parents;
        // everyone else is implicitly a stranger. (The old version initialized
        // default entries against every agent in the world: O(n) per birth.)
        child.SetKinship(parentA.id, KinshipType.Parent);
        child.SetKinship(parentB.id, KinshipType.Parent);

        parentA.SetKinship(child.id, KinshipType.Child);
        parentB.SetKinship(child.id, KinshipType.Child);

        spawner.worldState.AddAgent(child);
        WorldEventLog events = spawner.worldState.EventLog;
        events.SetRelationship(child, parentA, SocialRelationType.Friend);
        events.SetRelationship(child, parentB, SocialRelationType.Friend);
        events.SetRelationship(parentA, child, SocialRelationType.Friend);
        events.SetRelationship(parentB, child, SocialRelationType.Friend);
        spawner.worldState.stats.births++;

        HomeData home = new HomeData
        {
            id = spawner.worldState.nextHomeId++,
            position = birthPos,
            parentAId = parentA.id,
            parentBId = parentB.id,
            childId = child.id
        };

        spawner.worldState.homes.Add(home);
    }

    int CountAliveAgents(List<AgentData> agents)
    {
        int count = 0;

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && agents[i].alive)
                count++;
        }

        return count;
    }

    void ApplyPopulationOverflowCull(List<AgentData> agents, WorldState world)
    {
        if (!usePopulationCap) return;

        int aliveCount = CountAliveAgents(agents);
        int targetCount = Mathf.Min(targetAliveAfterCull, maxAliveAgents);

        if (aliveCount <= maxAliveAgents) return;

        List<AgentData> aliveAgents = new List<AgentData>();
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && agents[i].alive)
                aliveAgents.Add(agents[i]);
        }

        while (aliveCount > targetCount && aliveAgents.Count > 0)
        {
            int randomIndex = Random.Range(0, aliveAgents.Count);
            AgentData victim = aliveAgents[randomIndex];
            aliveAgents.RemoveAt(randomIndex);

            if (victim != null && victim.alive)
            {
                KillAgent(victim, world, WorldDeathCause.PopulationLimit);
                aliveCount--;
            }
        }
    }

    void KillAgent(AgentData agent, WorldState world, WorldDeathCause cause)
    {
        if (agent == null || !agent.alive) return;

        agent.alive = false;
        agent.deathTime = world.worldTime;
        agent.velocity = Vector2.zero;
        agent.currentBehavior = BehaviorType.None;
        world.stats.deaths++;
        world.EventLog.RecordDeath(agent, cause);

        if (!agent.deathMarkerSpawned)
        {
            world.deathMarkers.Add(new DeathMarkerData
            {
                id = world.nextDeathMarkerId++,
                position = agent.position,
                agentId = agent.id
            });

            agent.deathMarkerSpawned = true;
        }
    }

    /// <summary>Order-independent pair key packed into a long — no string allocation.</summary>
    static long GetPairKey(int a, int b)
    {
        return a < b
            ? ((long)a << 32) | (uint)b
            : ((long)b << 32) | (uint)a;
    }

    /// <summary>
    /// Proximity-driven relation dynamics, restructured from an O(n²) all-pairs
    /// scan into three sparse passes per agent:
    ///   A) grid query within friendDistance — accumulate closeness, befriend
    ///   B) sweep tracked closeness timers — reset pairs that drifted beyond
    ///      forgetDistance, freeze pairs in the neutral zone (original semantics)
    ///   C) sweep current Friend entries — accumulate far-time, forget
    /// Cost scales with actual neighbors + actual friendships, not population².
    /// </summary>
    void UpdateRelationChanges(List<AgentData> agents, WorldState world)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            AgentData self = agents[i];
            if (!self.alive) continue;
            if (self.currentBehavior == BehaviorType.Silence) continue;

            // ---- Pass A: neighbors within befriending range ----
            nearIdsBuffer.Clear();
            Grid.QueryCircle(agents, self.position, friendDistance, queryBuffer);

            for (int q = 0; q < queryBuffer.Count; q++)
            {
                int j = queryBuffer[q];
                if (j == i) continue;

                AgentData other = agents[j];
                if (other.currentBehavior == BehaviorType.Silence) continue;

                nearIdsBuffer.Add(other.id);

                if (self.GetKinship(other.id) != KinshipType.None) continue;

                self.closenessTimers.TryGetValue(other.id, out float closeness);
                closeness += tickInterval;
                self.closenessTimers[other.id] = closeness;
                self.distanceTimers.Remove(other.id); // near again → far-timer resets

                if (closeness >= befriendTime &&
                    self.GetSocial(other.id) != SocialRelationType.Friend)
                {
                    spawner.worldState.EventLog.SetRelationship(self, other, SocialRelationType.Friend);
                    world.stats.friendshipsFormed++;
                }
            }

            // ---- Pass B: tracked closeness pairs that are NOT near right now ----
            // Original semantics: closeness resets only when the pair drifts
            // beyond forgetDistance; in between it freezes. Also drop timers
            // whose target no longer exists.
            timerEditBuffer.Clear();
            foreach (var pair in self.closenessTimers)
            {
                if (nearIdsBuffer.Contains(pair.Key)) continue;

                if (!world.agentsById.TryGetValue(pair.Key, out AgentData other) || !other.alive)
                {
                    timerEditBuffer.Add(pair.Key);
                    continue;
                }

                if (other.currentBehavior == BehaviorType.Silence) continue; // freeze

                if (Vector2.Distance(self.position, other.position) >= forgetDistance)
                    timerEditBuffer.Add(pair.Key); // reset (= remove sparse entry)
            }

            for (int t = 0; t < timerEditBuffer.Count; t++)
                self.closenessTimers.Remove(timerEditBuffer[t]);

            // ---- Pass C: forget distant friends ----
            forgetBuffer.Clear();
            foreach (var pair in self.socialRelations)
            {
                if (pair.Value != SocialRelationType.Friend) continue;
                if (self.GetKinship(pair.Key) != KinshipType.None) continue; // kin never drift
                if (nearIdsBuffer.Contains(pair.Key)) continue;              // near → handled in A

                if (!world.agentsById.TryGetValue(pair.Key, out AgentData other) || !other.alive)
                    continue; // keep the relation; entry is cleaned on corpse removal

                if (other.currentBehavior == BehaviorType.Silence) continue; // freeze

                float dist = Vector2.Distance(self.position, other.position);
                if (dist < forgetDistance)
                    continue; // neutral zone → freeze (reset happens only in Pass A when truly near)

                self.distanceTimers.TryGetValue(pair.Key, out float farTime);
                farTime += tickInterval;
                self.distanceTimers[pair.Key] = farTime;

                if (farTime >= forgetTime)
                    forgetBuffer.Add(pair.Key);
            }

            for (int f = 0; f < forgetBuffer.Count; f++)
            {
                AgentData formerFriend = world.agentsById[forgetBuffer[f]];
                world.EventLog.SetRelationship(self, formerFriend, SocialRelationType.Other);
                self.distanceTimers.Remove(forgetBuffer[f]);
                world.stats.friendshipsForgotten++;
            }
        }
    }
}