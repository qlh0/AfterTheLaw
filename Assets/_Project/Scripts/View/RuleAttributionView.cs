using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes Rule Book cause-and-effect visible in the world (attribution layer):
///
/// - Rule 1 (Draw Together): a soft line between every matched pair
/// - Rule 2 (Turn Away): same, in the repulsion color
/// - Rule 4 (Condition Spreads): a pulsing ring around receiving agents
/// - Rule 5 (Behavior Rises): a pulsing ring around affected observers
///
/// Reads SimulationStats each frame (filled by the simulation each tick) and
/// renders with pooled LineRenderers — nothing here writes simulation state.
/// Exhibition feedback motivated this layer: rules did change the world, but
/// the audience couldn't attribute the change to their edit.
/// </summary>
public class RuleAttributionView : MonoBehaviour
{
    public AgentSpawner spawner;

    [Header("Pair Lines (Rules 1 & 2)")]
    public bool showPairLines = true;
    public Color drawTogetherColor = new Color(0.2f, 0.75f, 0.4f, 0.24f);
    public Color turnAwayColor = new Color(0.9f, 0.25f, 0.2f, 0.27f);
    public float lineWidth = 0.027f;
    public int maxLines = 96;

    [Header("Effect Rings (Rules 4 & 5)")]
    public bool showRings = true;
    public Color spreadColor = new Color(0.55f, 0.35f, 0.9f, 0.6f);
    public Color behaviorRiseColor = new Color(1f, 0.6f, 0.15f, 0.6f);
    public float ringRadius = 0.32f;
    public float ringWidth = 0.025f;
    public int maxRings = 96;
    public float pulseSpeed = 3f;

    [Header("Rendering")]
    public int sortingOrder = -5; // behind agents

    const int RingSegments = 22;

    readonly List<LineRenderer> linePool = new List<LineRenderer>();
    readonly List<LineRenderer> ringPool = new List<LineRenderer>();
    Material sharedMaterial;

    void Start()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<AgentSpawner>();

        Shader shader = Shader.Find("Sprites/Default");
        sharedMaterial = new Material(shader);
    }

    void Update()
    {
        if (spawner == null || spawner.worldState == null) return;

        WorldState world = spawner.worldState;
        SimulationStats stats = world.stats;

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseSpeed);

        int linesUsed = 0;
        if (showPairLines)
        {
            linesUsed = DrawPairLines(world, stats.rule1Pairs, drawTogetherColor, pulse, linesUsed);
            linesUsed = DrawPairLines(world, stats.rule2Pairs, turnAwayColor, pulse, linesUsed);
        }
        for (int i = linesUsed; i < linePool.Count; i++)
            linePool[i].gameObject.SetActive(false);

        int ringsUsed = 0;
        if (showRings)
        {
            ringsUsed = DrawRings(world, stats.spreadReceiverIds, spreadColor, pulse, ringsUsed);
            ringsUsed = DrawRings(world, stats.rule5ObserverIds, behaviorRiseColor, pulse, ringsUsed);
        }
        for (int i = ringsUsed; i < ringPool.Count; i++)
            ringPool[i].gameObject.SetActive(false);
    }

    int DrawPairLines(WorldState world, List<long> pairs, Color color, float pulse, int used)
    {
        Color c = color;
        c.a = color.a * pulse;

        for (int i = 0; i < pairs.Count && used < maxLines; i++)
        {
            SimulationStats.UnpackPair(pairs[i], out int idA, out int idB);

            if (!world.agentsById.TryGetValue(idA, out AgentData a) || !a.alive) continue;
            if (!world.agentsById.TryGetValue(idB, out AgentData b) || !b.alive) continue;

            LineRenderer line = GetLine(used++);
            line.gameObject.SetActive(true);
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(a.position.x, a.position.y, 0f));
            line.SetPosition(1, new Vector3(b.position.x, b.position.y, 0f));
            line.startColor = c;
            line.endColor = c;
        }

        return used;
    }

    int DrawRings(WorldState world, List<int> agentIds, Color color, float pulse, int used)
    {
        Color c = color;
        c.a = color.a * pulse;
        float radius = ringRadius * (0.9f + 0.15f * pulse);

        for (int i = 0; i < agentIds.Count && used < maxRings; i++)
        {
            if (!world.agentsById.TryGetValue(agentIds[i], out AgentData agent) || !agent.alive) continue;

            LineRenderer ring = GetRing(used++);
            ring.gameObject.SetActive(true);

            for (int s = 0; s <= RingSegments; s++)
            {
                float angle = s / (float)RingSegments * Mathf.PI * 2f;
                ring.SetPosition(s, new Vector3(
                    agent.position.x + Mathf.Cos(angle) * radius,
                    agent.position.y + Mathf.Sin(angle) * radius,
                    0f));
            }

            ring.startColor = c;
            ring.endColor = c;
        }

        return used;
    }

    LineRenderer GetLine(int index)
    {
        while (linePool.Count <= index)
            linePool.Add(CreateRenderer("PairLine", 2));
        return linePool[index];
    }

    LineRenderer GetRing(int index)
    {
        while (ringPool.Count <= index)
            ringPool.Add(CreateRenderer("EffectRing", RingSegments + 1));
        return ringPool[index];
    }

    LineRenderer CreateRenderer(string name, int positions)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.material = sharedMaterial;
        line.positionCount = positions;
        line.startWidth = name == "PairLine" ? lineWidth : ringWidth;
        line.endWidth = line.startWidth;
        line.useWorldSpace = true;
        line.sortingOrder = sortingOrder;
        line.numCapVertices = 2;
        go.SetActive(false);

        return line;
    }
}
