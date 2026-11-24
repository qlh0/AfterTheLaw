using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

/// <summary>
/// Headless simulation benchmark — runs the full simulation step loop in the
/// editor (no play mode) at increasing agent counts and writes a report to
/// Benchmarks/ in the project root.
///
/// Usage: open World.unity (a MainCamera must exist in the scene), then
/// menu MYLIFE → Run Simulation Benchmark.
/// </summary>
public static class SimulationBenchmark
{
    // Agent counts to test. The population cap is set to each N so births
    // never push the world above the tested size.
    static readonly int[] AgentCounts = { 50, 120, 250, 500, 1000, 2000 };

    const int WarmupTicks = 30;
    const int MeasuredTicks = 150;
    const float TickInterval = 1f / 3f; // matches SimulationEngine default (3 ticks/sec)
    const int RandomSeed = 12345;       // fixed seed so runs are comparable

    // Keep the population constant during measurement: agents get near-infinite
    // lifespan and HP so aging/starvation/combat can't shrink the world mid-run.
    // Without this, everything dies of old age (~20-40 game-seconds) before the
    // run ends and the later ticks measure an empty world.
    const bool ImmortalAgents = true;

    // Reference population for constant-density mode: the world area is scaled
    // so every run has the same agents-per-unit² as 120 agents on one screen
    // (the tuned exhibition density).
    const int ReferencePopulation = 120;

    [MenuItem("MYLIFE/Run Simulation Benchmark")]
    public static void Run()
    {
        if (Camera.main == null)
        {
            EditorUtility.DisplayDialog(
                "Simulation Benchmark",
                "No MainCamera found. Open World.unity first — the simulation derives world bounds from the camera.",
                "OK");
            return;
        }

        var fixedWorld = new List<Result>();
        var constantDensity = new List<Result>();
        GameObject rig = null;
        int totalRuns = AgentCounts.Length * 2;

        try
        {
            for (int c = 0; c < AgentCounts.Length; c++)
            {
                int n = AgentCounts[c];

                // Mode 1: fixed world (the real installation: one screen).
                EditorUtility.DisplayProgressBar(
                    "Simulation Benchmark",
                    $"Fixed world: {n} agents ({c + 1}/{AgentCounts.Length})...",
                    (float)c / totalRuns);

                rig = BuildRig(n, false, out AgentSystem agentSystem, out AgentSpawner spawner);
                fixedWorld.Add(Measure(n, agentSystem, spawner));
                UnityEngine.Object.DestroyImmediate(rig);
                rig = null;

                // Mode 2: constant density (world area grows with population —
                // measures engine scaling without the design-density ceiling).
                EditorUtility.DisplayProgressBar(
                    "Simulation Benchmark",
                    $"Constant density: {n} agents ({c + 1}/{AgentCounts.Length})...",
                    (float)(AgentCounts.Length + c) / totalRuns);

                rig = BuildRig(n, true, out agentSystem, out spawner);
                constantDensity.Add(Measure(n, agentSystem, spawner));
                UnityEngine.Object.DestroyImmediate(rig);
                rig = null;
            }
        }
        finally
        {
            if (rig != null)
                UnityEngine.Object.DestroyImmediate(rig);
            EditorUtility.ClearProgressBar();
        }

        string report = WriteReport(fixedWorld, constantDensity);
        Debug.Log($"SimulationBenchmark: done. Report written to {report}\n" +
                  $"— Fixed world —\n{FormatConsoleTable(fixedWorld)}" +
                  $"— Constant density —\n{FormatConsoleTable(constantDensity)}");
        EditorUtility.RevealInFinder(report);
    }

    // ---------------------------------------------------------------- rig

    static GameObject BuildRig(int agentCount, bool constantDensity, out AgentSystem agentSystem, out AgentSpawner spawner)
    {
        var go = new GameObject("__SimulationBenchmark");
        go.hideFlags = HideFlags.HideAndDontSave;

        spawner = go.AddComponent<AgentSpawner>();
        agentSystem = go.AddComponent<AgentSystem>();
        var rulebook = go.AddComponent<AgentSystem_Rulebook>();
        var food = go.AddComponent<AgentSystem_FoodHungry>();
        var movement = go.AddComponent<AgentSystem_MovementSystem>();

        // Wire manually — Awake() is not called in edit mode.
        agentSystem.spawner = spawner;
        agentSystem.rulebookSystem = rulebook;
        agentSystem.foodHungrySystem = food;
        agentSystem.movementSystem = movement;
        movement.owner = agentSystem;

        agentSystem.tickInterval = TickInterval;
        agentSystem.usePopulationCap = true;
        agentSystem.maxAliveAgents = agentCount;

        spawner.initialCount = agentCount;
        spawner.initialFoodCount = Mathf.Max(12, agentCount / 8);

        Random.InitState(RandomSeed);
        spawner.ResetWorld();
        agentSystem.ResetRuntimeState();

        if (constantDensity)
        {
            // Scale the world so agents-per-area matches ReferencePopulation on
            // one camera screen, then scatter agents/foods over the new bounds.
            Camera cam = Camera.main;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float scale = Mathf.Sqrt((float)agentCount / ReferencePopulation);

            movement.overrideWorldBounds = true;
            movement.worldMinOverride = new Vector2(-halfWidth * scale, -halfHeight * scale);
            movement.worldMaxOverride = new Vector2(halfWidth * scale, halfHeight * scale);

            foreach (var agent in spawner.worldState.agents)
                agent.position = new Vector2(
                    Random.Range(movement.worldMinOverride.x, movement.worldMaxOverride.x),
                    Random.Range(movement.worldMinOverride.y, movement.worldMaxOverride.y));

            foreach (var foodItem in spawner.worldState.foods)
                foodItem.position = new Vector2(
                    Random.Range(movement.worldMinOverride.x, movement.worldMaxOverride.x),
                    Random.Range(movement.worldMinOverride.y, movement.worldMaxOverride.y));
        }

        if (ImmortalAgents)
        {
            foreach (var agent in spawner.worldState.agents)
            {
                agent.maxAge = float.MaxValue;
                agent.maxHP = 1e9f;
                agent.hp = 1e9f;
            }
        }

        return go;
    }

    // ------------------------------------------------------------ measure

    struct Result
    {
        public int requestedAgents;
        public float avgMsPerTick;
        public float maxMsPerTick;
        public float p95MsPerTick;
        public int gcCollections;
        public long allocBytesPerTick;
        public int aliveAtStart;
        public int aliveAtEnd;
        public int totalListSizeAtEnd; // includes dead agents kept in the list
    }

    static Result Measure(int n, AgentSystem agentSystem, AgentSpawner spawner)
    {
        for (int i = 0; i < WarmupTicks; i++)
            RunOneTick(agentSystem, spawner);

        int aliveAtStart = CountAlive(spawner.worldState.agents);

        var tickMs = new float[MeasuredTicks];
        var sw = new Stopwatch();

        int gcBefore = GC.CollectionCount(0);
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < MeasuredTicks; i++)
        {
            sw.Restart();
            RunOneTick(agentSystem, spawner);
            sw.Stop();
            tickMs[i] = (float)sw.Elapsed.TotalMilliseconds;
        }

        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        int gcAfter = GC.CollectionCount(0);

        Array.Sort(tickMs);
        float sum = 0f;
        for (int i = 0; i < tickMs.Length; i++) sum += tickMs[i];

        return new Result
        {
            requestedAgents = n,
            avgMsPerTick = sum / MeasuredTicks,
            maxMsPerTick = tickMs[tickMs.Length - 1],
            p95MsPerTick = tickMs[Mathf.FloorToInt(tickMs.Length * 0.95f)],
            gcCollections = gcAfter - gcBefore,
            allocBytesPerTick = (allocAfter - allocBefore) / MeasuredTicks,
            aliveAtStart = aliveAtStart,
            aliveAtEnd = CountAlive(spawner.worldState.agents),
            totalListSizeAtEnd = spawner.worldState.agents.Count
        };
    }

    static void RunOneTick(AgentSystem agentSystem, AgentSpawner spawner)
    {
        // Mirrors SimulationEngine.RunTick()
        spawner.worldState.worldTime += TickInterval;
        agentSystem.foodHungrySystem.UpdateFoodGrowth(spawner.worldState.foods, TickInterval);
        agentSystem.RunSimulationStep();
    }

    static int CountAlive(List<AgentData> agents)
    {
        int count = 0;
        for (int i = 0; i < agents.Count; i++)
            if (agents[i] != null && agents[i].alive) count++;
        return count;
    }

    // ------------------------------------------------------------- report

    static string WriteReport(List<Result> fixedWorld, List<Result> constantDensity)
    {
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Benchmarks");
        Directory.CreateDirectory(dir);

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        string mdPath = Path.Combine(dir, $"simulation_benchmark_{stamp}.md");
        string csvPath = Path.Combine(dir, $"simulation_benchmark_{stamp}.csv");

        var md = new StringBuilder();
        md.AppendLine("# MYLIFE Simulation Benchmark");
        md.AppendLine();
        md.AppendLine($"- Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
        md.AppendLine($"- Machine: {SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB RAM");
        md.AppendLine($"- Unity: {Application.unityVersion}");
        md.AppendLine($"- Ticks: {WarmupTicks} warmup + {MeasuredTicks} measured @ {TickInterval * 1000f:F0} ms/tick budget " +
                      $"(target: 3 ticks/sec → every tick must stay well under 333 ms; " +
                      $"for 30 fps rendering headroom, ideally under ~10 ms)");
        md.AppendLine($"- Random seed: {RandomSeed}, population cap = requested agent count" +
                      (ImmortalAgents ? ", immortal-agent mode (constant population)" : ""));
        md.AppendLine();

        var csv = new StringBuilder();
        csv.AppendLine("mode,requested_agents,alive_start,alive_end,list_size_end,avg_ms_per_tick,p95_ms_per_tick,max_ms_per_tick,alloc_bytes_per_tick,gc_gen0_collections");

        md.AppendLine("## Fixed world (one screen — the real installation)");
        md.AppendLine();
        AppendTable(md, csv, fixedWorld, "fixed_world");

        md.AppendLine();
        md.AppendLine($"## Constant density (world area scales with population; density pinned to {ReferencePopulation} agents/screen)");
        md.AppendLine();
        AppendTable(md, csv, constantDensity, "constant_density");

        md.AppendLine();
        md.AppendLine("Notes: measures the pure simulation step (no rendering). " +
                      "The fixed-world mode packs every population onto one screen, so interaction radii cover " +
                      "ever-larger fractions of the population — costs there reflect the DESIGN's density ceiling. " +
                      "The constant-density mode scales world area with population and isolates the engine's scaling behavior.");

        File.WriteAllText(mdPath, md.ToString());
        File.WriteAllText(csvPath, csv.ToString());
        return mdPath;
    }

    static void AppendTable(StringBuilder md, StringBuilder csv, List<Result> results, string modeName)
    {
        md.AppendLine("| Agents (requested) | Alive start→end | List size (incl. dead) | Avg ms/tick | p95 ms/tick | Max ms/tick | Alloc/tick | GC gen0 collections |");
        md.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var r in results)
        {
            md.AppendLine($"| {r.requestedAgents} | {r.aliveAtStart}→{r.aliveAtEnd} | {r.totalListSizeAtEnd} " +
                          $"| {r.avgMsPerTick:F2} | {r.p95MsPerTick:F2} | {r.maxMsPerTick:F2} " +
                          $"| {FormatBytes(r.allocBytesPerTick)} | {r.gcCollections} |");

            csv.AppendLine($"{modeName},{r.requestedAgents},{r.aliveAtStart},{r.aliveAtEnd},{r.totalListSizeAtEnd}," +
                           $"{r.avgMsPerTick:F3},{r.p95MsPerTick:F3},{r.maxMsPerTick:F3},{r.allocBytesPerTick},{r.gcCollections}");
        }
    }

    static string FormatConsoleTable(List<Result> results)
    {
        var sb = new StringBuilder();
        foreach (var r in results)
            sb.AppendLine($"  {r.requestedAgents,5} agents: avg {r.avgMsPerTick,8:F2} ms/tick, " +
                          $"p95 {r.p95MsPerTick,8:F2} ms, alloc {FormatBytes(r.allocBytesPerTick),10}/tick, " +
                          $"alive {r.aliveAtStart}→{r.aliveAtEnd} (list {r.totalListSizeAtEnd})");
        return sb.ToString();
    }

    static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes} B";
    }
}
