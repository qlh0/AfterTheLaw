using UnityEngine;

public class SimulationEngine : MonoBehaviour
{
    public AgentSpawner spawner;
    public AgentSystem agentSystem;

    [Header("Simulation Tick（模拟步进）")]
    public float ticksPerSecond = 3f; // 每秒运行多少次tick（模拟刷新频率）

    [Header("Game Time Scale（游戏时间倍率）")]
    public float gameSecondsPerRealSecond = 1f; // 现实1秒 = 游戏多少秒（时间加速倍率）

    [Header("World Scale（世界尺度）")]
    public float metersPerUnit = 1f; // 1个Unity单位 = 多少米
    public float pixelsPerUnit = 100f; // 1个Unity单位 = 多少像素（一般Sprite默认100）

    [Header("Pixel Scale（像素尺度）")]
    public float metersPerPixel = 0.01f; // 1像素 = 多少米（最终统一尺度）

    float timer = 0f;

    float TickInterval
    {
        get
        {
            return 1f / Mathf.Max(0.0001f, ticksPerSecond);
        }
    }

    float GameDeltaTime
    {
        get
        {
            return TickInterval * gameSecondsPerRealSecond;
        }
    }

    void Awake()
    {
        // 自动同步：metersPerUnit = 像素 * 每像素米数
        metersPerUnit = pixelsPerUnit * metersPerPixel;
    }

    public float UnitsToMeters(float units)
    {
        return units * metersPerUnit;
    }

    public float MetersToUnits(float meters)
    {
        return meters / Mathf.Max(0.0001f, metersPerUnit);
    }

    public float PixelsToMeters(float pixels)
    {
        return pixels * metersPerPixel;
    }

    public float MetersToPixels(float meters)
    {
        return meters / Mathf.Max(0.0001f, metersPerPixel);
    }

    void Update()
    {
        timer += Time.deltaTime;

        int maxTicksPerFrame = 1;
        int ticksRun = 0;

        while (timer >= TickInterval && ticksRun < maxTicksPerFrame)
        {
            RunTick();
            timer -= TickInterval;
            ticksRun++;
        }
    }

    void RunTick()
    {
        if (spawner != null && spawner.worldState != null)
        {
            spawner.worldState.worldTime += GameDeltaTime;
        }

        if (agentSystem != null)
        {
            agentSystem.tickInterval = GameDeltaTime;

            if (agentSystem.foodHungrySystem != null && spawner != null && spawner.worldState != null)
            {
                agentSystem.foodHungrySystem.UpdateFoodGrowth(
                    spawner.worldState.foods,
                    GameDeltaTime
                );
            }

            agentSystem.RunSimulationStep();
        }
    }
}