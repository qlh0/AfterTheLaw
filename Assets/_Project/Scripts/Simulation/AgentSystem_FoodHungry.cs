using System.Collections.Generic;
using UnityEngine;

public class AgentSystem_FoodHungry : MonoBehaviour
{
    [Header("Food Seeking")]
    public float lowFoodSeekThreshold = 0.7f;
    public float foodSeekForce = 3.5f;
    public float hungrySeekBonus = 2.0f;

    [Header("Food Respawn")]
    public float foodRespawnInterval = 3f;
    public int foodRespawnAmount = 2;
    float foodRespawnTimer = 0f;

    [Header("Food Growth (Inspector Controlled)")]
    public float foodBaseAmount = 1f;
    public float foodMaxAmount = 1.2f;
    public float foodGrowthRate = 0.02f;

    [Header("Food")]
    public float foodDecayPerTick = 0.01f;
    public float hungerFromLowFoodPerTick = 0.03f;
    public float starvingHpLossPerTick = 0.02f;
    public float lowFoodThreshold = 0.3f;

    [Header("Food Pickup")]
    public float foodPickupDistance = 0.6f;
    public float hungryReliefOnEat = 0.35f;

    public void UpdateFoodRespawn(WorldState world, float tickInterval, Vector2 boundsMin, Vector2 boundsMax)
    {
        foodRespawnTimer += tickInterval;

        if (foodRespawnTimer < foodRespawnInterval) return;

        foodRespawnTimer = 0f;

        for (int i = 0; i < foodRespawnAmount; i++)
        {
            FoodData food = new FoodData(
                world.nextFoodId++,
                new Vector2(
                    Random.Range(boundsMin.x, boundsMax.x),
                    Random.Range(boundsMin.y, boundsMax.y)
                ),
                foodBaseAmount,
                foodMaxAmount,
                foodGrowthRate
            );

            world.foods.Add(food);
        }
    }

    // ⭐ 关键：用 tickInterval 控制成长速度（否则不同TPS会不一致）
    public void UpdateFoodGrowth(List<FoodData> foods, float tickInterval)
    {
        foreach (var food in foods)
        {
            if (food.consumed) continue;

            if (food.amount < food.maxAmount)
            {
                food.amount += food.growthRate * tickInterval; // ✅ 核心改动
                food.amount = Mathf.Min(food.amount, food.maxAmount);
            }
        }
    }

    public void UpdateFood(List<AgentData> agents)
    {
        foreach (var agent in agents)
        {
            if (!agent.alive) continue;

            agent.food -= foodDecayPerTick;
            agent.food = Mathf.Clamp(agent.food, 0f, agent.maxFood);

            if (agent.food <= lowFoodThreshold)
                agent.hungry += hungerFromLowFoodPerTick;

            if (agent.food <= 0f)
                agent.hp -= starvingHpLossPerTick;
        }
    }

    public void ProcessFoodPickup(List<AgentData> agents, List<FoodData> foods)
    {
        foreach (var agent in agents)
        {
            if (!agent.alive) continue;

            for (int i = 0; i < foods.Count; i++)
            {
                FoodData food = foods[i];
                if (food.consumed) continue;

                float dist = Vector2.Distance(agent.position, food.position);
                if (dist > foodPickupDistance) continue;

                agent.food += food.amount;
                agent.food = Mathf.Clamp(agent.food, 0f, agent.maxFood);

                agent.hungry -= hungryReliefOnEat;
                agent.hungry = Mathf.Clamp01(agent.hungry);

                food.consumed = true;
                break;
            }
        }

        // Remove consumed foods from the world list. They used to stay forever
        // with a consumed flag, so food scans (pickup, nearest-food seeking)
        // slowed down permanently over a long exhibition run.
        foods.RemoveAll(IsConsumed);
    }

    static bool IsConsumed(FoodData food) => food.consumed;

    public FoodData FindNearestAvailableFood(Vector2 position, List<FoodData> foods)
    {
        FoodData nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var food in foods)
        {
            if (food.consumed) continue;

            float dist = Vector2.Distance(position, food.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = food;
            }
        }

        return nearest;
    }
}