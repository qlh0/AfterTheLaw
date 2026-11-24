using System.Collections.Generic;
using UnityEngine;

public class ChurchData
{
    public int id;
    public Vector2 position;
    public int founderAgentId;
}

public class WorldState
{
    public List<AgentData> agents = new List<AgentData>();

    /// <summary>O(1) agent lookup by id — kept in sync with the agents list via
    /// AddAgent/RemoveAgentAt. Sparse relation entries store ids, and systems
    /// resolve them through this map instead of scanning the list.</summary>
    public Dictionary<int, AgentData> agentsById = new Dictionary<int, AgentData>();

    public void AddAgent(AgentData agent)
    {
        agents.Add(agent);
        agentsById[agent.id] = agent;
    }

    public void RemoveAgentAt(int index)
    {
        agentsById.Remove(agents[index].id);
        agents.RemoveAt(index);
    }
    public List<HomeData> homes = new List<HomeData>();
    public List<FoodData> foods = new List<FoodData>();
    public List<ChurchData> churches = new List<ChurchData>();

    public List<CombatMarkerData> combatMarkers = new List<CombatMarkerData>();
    public List<DeathMarkerData> deathMarkers = new List<DeathMarkerData>();

    public float worldTime = 0f;
    public WorldEventLog EventLog { get; } = new WorldEventLog();

    /// <summary>Per-tick activity counters for the live chart and rule
    /// attribution visuals. Reset by AgentSystem each tick.</summary>
    public SimulationStats stats = new SimulationStats();

    public int nextAgentId = 0;
    public int nextHomeId = 0;
    public int nextFoodId = 0;
    public int nextChurchId = 0;
    public int nextCombatMarkerId = 0;
    public int nextDeathMarkerId = 0;
}