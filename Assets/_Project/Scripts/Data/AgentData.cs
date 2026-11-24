using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-data agent record — no Unity behaviours; the simulation layer mutates
/// these and the view layer only reads them.
///
/// Relations are stored SPARSELY: the dictionaries only contain non-default
/// entries (kinship ≠ None, social ≠ Other). Everyone an agent has no entry
/// for is implicitly a stranger ("Other"). This keeps memory and iteration
/// costs proportional to actual relationships instead of population², and it
/// lets systems iterate "all my friends/enemies/kin" directly.
///
/// Relations are asymmetric by design: A's perception of B lives in A's
/// dictionaries and can differ from B's perception of A.
/// </summary>
public class AgentData
{
    public int id;
    public int generation = 1;

    public Vector2 position;
    public Vector2 velocity;

    // -------- Relations (sparse: non-default entries only) --------
    public Dictionary<int, KinshipType> kinshipRelations = new Dictionary<int, KinshipType>();
    public Dictionary<int, SocialRelationType> socialRelations = new Dictionary<int, SocialRelationType>();

    // Pair timers, also sparse:
    // closenessTimers — accumulated near-time toward becoming Friend, tracked
    //   only for pairs that have recently been within befriending range.
    // distanceTimers — accumulated far-time toward forgetting a Friend, tracked
    //   only for current Friend pairs.
    public Dictionary<int, float> closenessTimers = new Dictionary<int, float>();
    public Dictionary<int, float> distanceTimers = new Dictionary<int, float>();

    public KinshipType GetKinship(int otherId)
    {
        return kinshipRelations.TryGetValue(otherId, out KinshipType k) ? k : KinshipType.None;
    }

    public SocialRelationType GetSocial(int otherId)
    {
        return socialRelations.TryGetValue(otherId, out SocialRelationType s) ? s : SocialRelationType.Other;
    }

    /// <summary>Sets how this agent perceives <paramref name="otherId"/>.
    /// Default values are removed rather than stored.</summary>
    public void SetSocial(int otherId, SocialRelationType value)
    {
        if (value == SocialRelationType.Other)
            socialRelations.Remove(otherId);
        else
            socialRelations[otherId] = value;
    }

    public void SetKinship(int otherId, KinshipType value)
    {
        if (value == KinshipType.None)
            kinshipRelations.Remove(otherId);
        else
            kinshipRelations[otherId] = value;
    }

    // -------- Conditions (0–1) --------
    public float hungry = 0f;
    public float lonely = 0f;
    public float joyful = 0f;
    public float hurt = 0f;

    // -------- Behavior --------
    public BehaviorType currentBehavior = BehaviorType.None;
    public float behaviorTimer = 0f;

    // -------- Health / Resource --------
    public float hp = 1f;
    public float maxHP = 1f;

    public float food = 1f;
    public float maxFood = 1f;

    // -------- Life --------
    public float age = 0f;
    public float maxAge = 30f;

    public float reproductionCooldown = 0f;

    // -------- State --------
    public bool alive = true;
    public bool deathMarkerSpawned = false;

    // World time at the moment of death. Dead agents linger for a short grace
    // period (so visuals/rules can react), then are removed from the agent list
    // so the per-tick loops stop iterating corpses. -1 = still alive.
    public float deathTime = -1f;
}
