using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The curated control surface that "Speak to the World" exposes to the LLM:
/// a fixed list of named simulation parameters, each with a legal range and a
/// one-line description. The model can only touch what's listed here, and
/// every write is clamped — capability design instead of raw access.
/// </summary>
public class WorldParameterRegistry
{
    public struct Param
    {
        public string name;
        public string description;
        public float min;
        public float max;
        public Func<float> get;
        public Action<float> set;
    }

    public readonly List<Param> parameters = new List<Param>();

    public WorldParameterRegistry(AgentSystem agents, AgentSystem_FoodHungry food,
                                  AgentSystem_MovementSystem movement, SimulationEngine engine)
    {
        if (food != null)
        {
            Add("foodRespawnInterval", "seconds between food spawns — lower = richer world", 0.5f, 20f,
                () => food.foodRespawnInterval, v => food.foodRespawnInterval = v);
            Add("foodRespawnAmount", "how many foods spawn each time", 0f, 10f,
                () => food.foodRespawnAmount, v => food.foodRespawnAmount = Mathf.RoundToInt(v));
            Add("foodDecayPerTick", "how fast agents burn food — higher = hungrier world", 0f, 0.05f,
                () => food.foodDecayPerTick, v => food.foodDecayPerTick = v);
            Add("starvingHpLossPerTick", "how deadly starvation is", 0f, 0.1f,
                () => food.starvingHpLossPerTick, v => food.starvingHpLossPerTick = v);
        }

        if (agents != null)
        {
            Add("violenceThreshold", "hunger level that triggers Violence — lower = more fights", 0.05f, 1f,
                () => agents.violenceThreshold, v => agents.violenceThreshold = v);
            Add("silenceThreshold", "loneliness level that triggers Silence", 0.05f, 1f,
                () => agents.silenceThreshold, v => agents.silenceThreshold = v);
            Add("loveThreshold", "joy level that triggers Love — lower = more love", 0.05f, 1f,
                () => agents.loveThreshold, v => agents.loveThreshold = v);
            Add("attackDamage", "damage per hit", 0f, 1f,
                () => agents.attackDamage, v => agents.attackDamage = v);
            Add("attackDistance", "reach of an attack", 0.2f, 3f,
                () => agents.attackDistance, v => agents.attackDistance = v);
            Add("birthFoodCost", "food each parent spends on a birth", 0f, 1f,
                () => agents.birthFoodCost, v => agents.birthFoodCost = v);
            Add("reproductionCooldownTime", "seconds between births per agent — lower = baby boom", 0f, 30f,
                () => agents.reproductionCooldownTime, v => agents.reproductionCooldownTime = v);
            Add("maxAliveAgents", "population cap", 10f, 2000f,
                () => agents.maxAliveAgents, v => agents.maxAliveAgents = Mathf.RoundToInt(v));
            Add("lonelinessCheckRadius", "how close company must be to cure loneliness", 0.5f, 6f,
                () => agents.lonelinessCheckRadius, v => agents.lonelinessCheckRadius = v);
            Add("joyfulRisePerTick", "how quickly wellbeing turns to joy", 0f, 0.2f,
                () => agents.joyfulRisePerTick, v => agents.joyfulRisePerTick = v);
            Add("lonelyChurchSpawnChance", "chance a deeply lonely agent founds a church", 0f, 1f,
                () => agents.lonelyChurchSpawnChance, v => agents.lonelyChurchSpawnChance = v);
        }

        if (movement != null)
        {
            Add("moveSpeed", "how fast everyone moves", 0.2f, 8f,
                () => movement.moveSpeed, v => movement.moveSpeed = v);
            Add("loveForceBonus", "extra pull between agents in Love", 0f, 2f,
                () => movement.loveForceBonus, v => movement.loveForceBonus = v);
            Add("violenceForceBonus", "extra push of violent agents toward others", 0f, 2f,
                () => movement.violenceForceBonus, v => movement.violenceForceBonus = v);
            Add("silenceForceMultiplier", "how much silent agents still move (0 = frozen)", 0f, 1f,
                () => movement.silenceForceMultiplier, v => movement.silenceForceMultiplier = v);
        }

        if (engine != null)
        {
            Add("gameSecondsPerRealSecond", "speed of time itself", 0.1f, 10f,
                () => engine.gameSecondsPerRealSecond, v => engine.gameSecondsPerRealSecond = v);
        }
    }

    void Add(string name, string description, float min, float max, Func<float> get, Action<float> set)
    {
        parameters.Add(new Param { name = name, description = description, min = min, max = max, get = get, set = set });
    }

    /// <summary>Clamped write; returns false for unknown names.</summary>
    public bool TrySet(string name, float value, out float before, out float after)
    {
        before = after = 0f;
        if (float.IsNaN(value) || float.IsInfinity(value)) return false;

        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].name == name)
            {
                Param parameter = parameters[i];
                before = parameter.get();
                parameter.set(Mathf.Clamp(value, parameter.min, parameter.max));
                after = parameter.get();
                return true;
            }
        }

        return false;
    }

    /// <summary>One line per parameter: name = current (min..max) — description.</summary>
    public string DescribeForPrompt()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            sb.AppendLine($"{p.name} = {p.get():0.###} (range {p.min:0.###}..{p.max:0.###}) — {p.description}");
        }
        return sb.ToString();
    }
}
