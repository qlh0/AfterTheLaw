using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Force-based movement.
///
/// Original design: every agent attracts/repels every other agent according to
/// its perceived relation (kinship 0.8, friend 0.6, stranger 0.3, enemy −0.6),
/// modified by behaviors and Rule Book pair rules — an O(n²) loop whose cost
/// quadrupled with every population doubling.
///
/// Optimized design ("shared field + sparse corrections"): almost all pairs are
/// mutual strangers with the same default coefficient, so the pair force law is
/// decomposed into
///   force_i = M_i × [ baseCoef_i × Σ_j dir/dist  +  Σ_related (rel − baseCoef) × dir/dist ]
/// The first term is a shared field over all (non-silent) agents: computed
/// EXACTLY from grid cells near the agent, and approximated for far cells by
/// "cell count × direction to cell centroid" (Barnes-Hut-style, single level).
/// The second term touches only the agent's actual relationships (sparse).
/// Silence: silent agents exert no force (excluded from field aggregates and
/// skipped in corrections) and move at silenceForceMultiplier of the force
/// they feel — same rules as the original loop.
///
/// Set useFarFieldApproximation = false to fall back to the exact O(n²) loop
/// (for A/B behavior comparison; relations are sparse in both paths).
/// </summary>
public class AgentSystem_MovementSystem : MonoBehaviour
{
    public AgentSystem owner;

    [Header("Movement")]
    public float attractionStrength = 1f;
    public float moveSpeed = 2f;
    public float velocityDamping = 0.9f;

    [Header("Optimization")]
    [Tooltip("Use grid far-field approximation. Off = exact all-pairs loop (slow at large populations, useful for A/B testing).")]
    public bool useFarFieldApproximation = true;
    [Tooltip("Within this radius pair forces are exact; beyond it, whole grid cells act as single points at their centroid.")]
    public float nearFieldRadius = 3.5f;
    [Tooltip("Interaction horizon: agents beyond this distance exert no force. Covers a whole screen in the installation (no behavioral change there); bounds far-field cost in larger worlds.")]
    public float farFieldRange = 20f;

    [Header("World Bounds")]
    public bool useWorldBounds = true;
    public float boundsPadding = 0.1f;
    public float wallBounceDamping = 0.2f;

    [Tooltip("When set, world bounds come from these values instead of the camera. Used by the benchmark to test constant-density scaling; leave off in the installation.")]
    public bool overrideWorldBounds = false;
    public Vector2 worldMinOverride = new Vector2(-10f, -6f);
    public Vector2 worldMaxOverride = new Vector2(10f, 6f);

    [Header("Behavior Movement Effects")]
    public float loveForceBonus = 0.6f;
    public float silenceForceMultiplier = 0.2f;
    public float violenceForceBonus = 0.3f;

    [Header("Church Attraction")]
    public bool enableChurchAttraction = true;
    public float lonelyChurchAttractionThreshold = 0.5f;
    public float churchAttractionForce = 1.2f;
    public float churchAttractionDistanceFalloff = 1f;

    const float DefaultRelation = 0.3f; // SocialRelationTable value for "Other"
    const float KinshipRelation = 0.8f;

    public void UpdateMovement(List<AgentData> agents, WorldState world, float tickInterval, SpatialGrid grid)
    {
        AgentSystem_Rulebook rulebook = owner != null ? owner.rulebookSystem : null;
        bool pairRulesActive = false;
        float defaultPairModifier = 0f;

        if (rulebook != null)
        {
            rulebook.PrepareTick();
            pairRulesActive = rulebook.HasActivePairRules;
            defaultPairModifier = rulebook.GetDefaultPairModifier();
        }

        List<FoodData> foods = world.foods;
        List<ChurchData> churches = world.churches;

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData self = agents[i];
            if (!self.alive) continue;

            float behaviorBonus = 0f;
            if (self.currentBehavior == BehaviorType.Love) behaviorBonus = loveForceBonus;
            else if (self.currentBehavior == BehaviorType.Violence) behaviorBonus = violenceForceBonus;

            // Coefficient every stranger pair shares (incl. active Rule 1/2
            // effects on default pairs).
            float baseCoef = DefaultRelation + defaultPairModifier + behaviorBonus;

            Vector2 force = useFarFieldApproximation
                ? ComputePairForcesGrid(agents, world, grid, i, self, baseCoef, behaviorBonus, rulebook, pairRulesActive)
                : ComputePairForcesExact(agents, i, self, behaviorBonus, rulebook, pairRulesActive);

            if (self.currentBehavior == BehaviorType.Silence)
                force *= silenceForceMultiplier;

            force += ComputeFoodForce(self, foods);
            force += ComputeChurchForce(self, churches);

            self.velocity += force * attractionStrength;
            self.velocity *= velocityDamping;
        }

        foreach (var agent in agents)
        {
            if (!agent.alive) continue;

            agent.position += agent.velocity * moveSpeed * tickInterval;
            ClampAgentToWorldBounds(agent);
        }
    }

    // ---------------- shared field + sparse corrections ----------------

    Vector2 ComputePairForcesGrid(List<AgentData> agents, WorldState world, SpatialGrid grid,
                                  int selfIndex, AgentData self, float baseCoef, float behaviorBonus,
                                  AgentSystem_Rulebook rulebook, bool pairRulesActive)
    {
        Vector2 force = Vector2.zero;

        int selfCellX = grid.CellXOf(self.position.x);
        int selfCellY = grid.CellYOf(self.position.y);
        int nearCells = Mathf.Max(1, Mathf.CeilToInt(nearFieldRadius / grid.cellSize));

        // --- near cells: exact per-agent forces (full relation, rules, silence) ---
        int minX = Mathf.Max(0, selfCellX - nearCells);
        int maxX = Mathf.Min(grid.Cols - 1, selfCellX + nearCells);
        int minY = Mathf.Max(0, selfCellY - nearCells);
        int maxY = Mathf.Min(grid.Rows - 1, selfCellY + nearCells);

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                List<int> members = grid.MembersOfCell(cx, cy);

                for (int m = 0; m < members.Count; m++)
                {
                    int j = members[m];
                    if (j == selfIndex) continue;

                    AgentData other = agents[j];
                    if (other.currentBehavior == BehaviorType.Silence) continue;

                    Vector2 dir = other.position - self.position;
                    float dist = dir.magnitude;
                    if (dist <= 0.01f) continue;

                    float relation = RelationOf(self, other) + behaviorBonus;

                    if (pairRulesActive)
                        relation += rulebook.GetPairRelationModifier(self, other);

                    force += dir.normalized * relation / dist;
                }
            }
        }

        // --- far cells: cell aggregates at the shared default coefficient ---
        // Only cells within the interaction horizon — otherwise the far loop
        // itself becomes O(total cells) per agent and re-introduces quadratic
        // cost when the world grows with the population.
        int farMinX = Mathf.Max(0, grid.CellXOf(self.position.x - farFieldRange));
        int farMaxX = Mathf.Min(grid.Cols - 1, grid.CellXOf(self.position.x + farFieldRange));
        int farMinY = Mathf.Max(0, grid.CellYOf(self.position.y - farFieldRange));
        int farMaxY = Mathf.Min(grid.Rows - 1, grid.CellYOf(self.position.y + farFieldRange));

        for (int cy = farMinY; cy <= farMaxY; cy++)
        {
            for (int cx = farMinX; cx <= farMaxX; cx++)
            {
                if (cx >= minX && cx <= maxX && cy >= minY && cy <= maxY) continue;

                if (!grid.GetCellAggregate(cx, cy, out int count, out Vector2 centroid)) continue;

                Vector2 delta = centroid - self.position;
                float sqrDist = delta.sqrMagnitude;
                if (sqrDist <= 0.0001f) continue;

                // Σ dir/dist over cell members ≈ count × delta / |delta|²
                force += delta * (count * baseCoef / sqrDist);
            }
        }

        // --- sparse corrections: my actual relationships beyond the near zone ---
        // (near-zone related agents were already handled exactly above; far ones
        // were counted in the field at baseCoef, so add the difference.)
        foreach (var pair in self.kinshipRelations)
            force += FarCorrection(world, grid, self, pair.Key, baseCoef, behaviorBonus,
                                   rulebook, pairRulesActive, minX, maxX, minY, maxY);

        foreach (var pair in self.socialRelations)
        {
            if (self.kinshipRelations.ContainsKey(pair.Key)) continue; // already corrected
            force += FarCorrection(world, grid, self, pair.Key, baseCoef, behaviorBonus,
                                   rulebook, pairRulesActive, minX, maxX, minY, maxY);
        }

        return force;
    }

    Vector2 FarCorrection(WorldState world, SpatialGrid grid, AgentData self, int otherId,
                          float baseCoef, float behaviorBonus,
                          AgentSystem_Rulebook rulebook, bool pairRulesActive,
                          int minX, int maxX, int minY, int maxY)
    {
        if (!world.agentsById.TryGetValue(otherId, out AgentData other)) return Vector2.zero;
        if (!other.alive) return Vector2.zero;
        if (other.currentBehavior == BehaviorType.Silence) return Vector2.zero; // exerts nothing anywhere

        // Skip if the target sits in the near zone — handled exactly there.
        int cellX = grid.CellXOf(other.position.x);
        int cellY = grid.CellYOf(other.position.y);
        if (cellX >= minX && cellX <= maxX && cellY >= minY && cellY <= maxY) return Vector2.zero;

        Vector2 dir = other.position - self.position;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return Vector2.zero;
        if (dist > farFieldRange) return Vector2.zero; // beyond the interaction horizon

        float relation = RelationOf(self, other) + behaviorBonus;

        if (pairRulesActive)
            relation += rulebook.GetPairRelationModifier(self, other);

        // Field already applied baseCoef to this agent (as part of its cell);
        // add the difference to make the related pair exact.
        return dir.normalized * (relation - baseCoef) / dist;
    }

    // ---------------- exact fallback (A/B testing) ----------------

    Vector2 ComputePairForcesExact(List<AgentData> agents, int selfIndex, AgentData self,
                                   float behaviorBonus, AgentSystem_Rulebook rulebook, bool pairRulesActive)
    {
        Vector2 force = Vector2.zero;

        for (int j = 0; j < agents.Count; j++)
        {
            if (j == selfIndex) continue;

            AgentData other = agents[j];
            if (!other.alive) continue;
            if (other.currentBehavior == BehaviorType.Silence) continue;

            Vector2 dir = other.position - self.position;
            float dist = dir.magnitude;
            if (dist <= 0.01f) continue;

            float relation = RelationOf(self, other) + behaviorBonus;

            if (pairRulesActive)
                relation += rulebook.GetPairRelationModifier(self, other);

            force += dir.normalized * relation / dist;
        }

        return force;
    }

    static float RelationOf(AgentData self, AgentData other)
    {
        KinshipType kinship = self.GetKinship(other.id);
        if (kinship == KinshipType.Parent || kinship == KinshipType.Child)
            return KinshipRelation;

        return SocialRelationTable.GetRelation(self.GetSocial(other.id));
    }

    // ---------------- environment forces ----------------

    Vector2 ComputeFoodForce(AgentData self, List<FoodData> foods)
    {
        if (owner == null || owner.foodHungrySystem == null) return Vector2.zero;
        if (self.food > owner.foodHungrySystem.lowFoodSeekThreshold) return Vector2.zero;

        FoodData nearestFood = owner.foodHungrySystem.FindNearestAvailableFood(self.position, foods);
        if (nearestFood == null) return Vector2.zero;

        Vector2 foodDir = nearestFood.position - self.position;
        if (foodDir.magnitude <= 0.01f) return Vector2.zero;

        float hungerFactor = Mathf.Lerp(1f, 1f + owner.foodHungrySystem.hungrySeekBonus, self.hungry);
        return foodDir.normalized * owner.foodHungrySystem.foodSeekForce * hungerFactor;
    }

    Vector2 ComputeChurchForce(AgentData self, List<ChurchData> churches)
    {
        if (!enableChurchAttraction) return Vector2.zero;
        if (churches == null || churches.Count == 0) return Vector2.zero;
        if (self.lonely < lonelyChurchAttractionThreshold) return Vector2.zero;

        ChurchData nearestChurch = FindNearestChurch(self.position, churches);
        if (nearestChurch == null) return Vector2.zero;

        Vector2 churchDir = nearestChurch.position - self.position;
        float churchDist = churchDir.magnitude;
        if (churchDist <= 0.01f) return Vector2.zero;

        float lonelyFactor = Mathf.InverseLerp(lonelyChurchAttractionThreshold, 1f, self.lonely);
        float falloff = 1f / Mathf.Max(0.01f, churchDist * churchAttractionDistanceFalloff);
        return churchDir.normalized * churchAttractionForce * lonelyFactor * falloff;
    }

    ChurchData FindNearestChurch(Vector2 fromPos, List<ChurchData> churches)
    {
        ChurchData nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < churches.Count; i++)
        {
            float dist = Vector2.Distance(fromPos, churches[i].position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = churches[i];
            }
        }

        return nearest;
    }

    // ---------------- bounds ----------------

    public void GetWorldBounds(out Vector2 min, out Vector2 max)
    {
        if (overrideWorldBounds)
        {
            min = worldMinOverride;
            max = worldMaxOverride;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            min = new Vector2(-10f, -6f);
            max = new Vector2(10f, 6f);
            return;
        }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        min = new Vector2(-halfWidth, -halfHeight);
        max = new Vector2(halfWidth, halfHeight);
    }

    void GetPaddedBounds(out Vector2 min, out Vector2 max)
    {
        GetWorldBounds(out min, out max);
        min.x += boundsPadding;
        min.y += boundsPadding;
        max.x -= boundsPadding;
        max.y -= boundsPadding;
    }

    public Vector2 ClampToWorldBounds(Vector2 pos)
    {
        if (!useWorldBounds) return pos;

        GetPaddedBounds(out Vector2 min, out Vector2 max);

        pos.x = Mathf.Clamp(pos.x, min.x, max.x);
        pos.y = Mathf.Clamp(pos.y, min.y, max.y);

        return pos;
    }

    void ClampAgentToWorldBounds(AgentData agent)
    {
        if (!useWorldBounds) return;

        GetPaddedBounds(out Vector2 boundsMin, out Vector2 boundsMax);
        float halfWidth = boundsMax.x, halfHeight = boundsMax.y;
        float negHalfWidth = boundsMin.x, negHalfHeight = boundsMin.y;

        Vector2 pos = agent.position;
        Vector2 vel = agent.velocity;

        if (pos.x < negHalfWidth)
        {
            pos.x = negHalfWidth;
            if (vel.x < 0f) vel.x *= -wallBounceDamping;
        }
        else if (pos.x > halfWidth)
        {
            pos.x = halfWidth;
            if (vel.x > 0f) vel.x *= -wallBounceDamping;
        }

        if (pos.y < negHalfHeight)
        {
            pos.y = negHalfHeight;
            if (vel.y < 0f) vel.y *= -wallBounceDamping;
        }
        else if (pos.y > halfHeight)
        {
            pos.y = halfHeight;
            if (vel.y > 0f) vel.y *= -wallBounceDamping;
        }

        agent.position = pos;
        agent.velocity = vel;
    }
}
