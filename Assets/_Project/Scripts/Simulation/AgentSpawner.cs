using UnityEngine;

public class AgentSpawner : MonoBehaviour
{
    public int initialCount = 10;
    public int initialFoodCount = 12;

    public float minMaxAge = 20f;
    public float maxMaxAge = 40f;

    public float spawnPadding = 0.5f;

    public WorldState worldState = new WorldState();

    void Start()
    {
        InitializeWorld();
    }

    public void ResetWorld()
    {
        worldState = new WorldState();
        InitializeWorld();
    }

    void InitializeWorld()
    {
        worldState.EventLog.SetRuleBaseline();
        for (int i = 0; i < initialCount; i++)
        {
            AgentData agent = CreateBaseAgent(GetRandomPositionInCamera());
            agent.generation = 1;
            worldState.AddAgent(agent);
        }

        // Seed random social relations between the founding agents. Relations
        // are stored sparsely: "Other" (the default) is simply not stored, so
        // only the rolled Friend/Enemy entries take memory.
        for (int i = 0; i < worldState.agents.Count; i++)
        {
            AgentData self = worldState.agents[i];

            for (int j = 0; j < worldState.agents.Count; j++)
            {
                if (i == j) continue;

                self.SetSocial(worldState.agents[j].id, GetRandomInitialSocialRelation());
            }
        }

        SpawnInitialFoods();
    }

    public AgentData CreateBaseAgent(Vector2 position)
    {
        AgentData agent = new AgentData();

        agent.id = worldState.nextAgentId++;
        agent.generation = 1;
        agent.position = ClampToCamera(position);
        agent.velocity = Vector2.zero;
        agent.age = 0f;
        agent.maxAge = Random.Range(minMaxAge, maxMaxAge);
        agent.alive = true;
        agent.reproductionCooldown = 0f;

        agent.maxFood = 1f;
        agent.food = 1f;

        return agent;
    }

    void SpawnInitialFoods()
    {
        for (int i = 0; i < initialFoodCount; i++)
        {
            FoodData food = new FoodData(
                worldState.nextFoodId++,
                GetRandomPositionInCamera(),
                1f,
                1.2f,
                0.02f
            );

            worldState.foods.Add(food);
        }
    }

    Vector2 GetRandomPositionInCamera()
    {
        Camera cam = Camera.main;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        float minX = -halfWidth + spawnPadding;
        float maxX = halfWidth - spawnPadding;
        float minY = -halfHeight + spawnPadding;
        float maxY = halfHeight - spawnPadding;

        return new Vector2(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY)
        );
    }

    Vector2 ClampToCamera(Vector2 pos)
    {
        Camera cam = Camera.main;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        pos.x = Mathf.Clamp(pos.x, -halfWidth + spawnPadding, halfWidth - spawnPadding);
        pos.y = Mathf.Clamp(pos.y, -halfHeight + spawnPadding, halfHeight - spawnPadding);

        return pos;
    }

    SocialRelationType GetRandomInitialSocialRelation()
    {
        float roll = Random.value;

        if (roll < 0.15f)
            return SocialRelationType.Enemy;

        if (roll < 0.40f)
            return SocialRelationType.Friend;

        return SocialRelationType.Other;
    }
}