using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uniform spatial grid over the (camera-derived) world bounds.
///
/// Rebuilt once per simulation tick from the alive agents, then shared by every
/// system that needs neighborhood queries (loneliness, combat, love bonds,
/// befriending, condition spreads) — replacing their former O(n²) all-pairs
/// scans with O(n · k) cell lookups.
///
/// Each cell also stores an aggregate (count + position sum) of its NON-SILENT
/// agents, which the movement system uses as a far-field approximation:
/// distant cells act on an agent as "count × direction-to-centroid / dist²"
/// instead of per-agent forces (silent agents exert no force, hence the
/// non-silent aggregate).
/// </summary>
public class SpatialGrid
{
    public float cellSize { get; private set; } = 1f;

    Vector2 min;
    int cols, rows;

    // Per-cell member lists hold agent indices into the agents list used at
    // Rebuild time. Lists are pooled and reused across rebuilds.
    List<int>[] cells = new List<int>[0];

    // Far-field aggregates per cell (non-silent alive agents only).
    int[] countNonSilent = new int[0];
    Vector2[] positionSumNonSilent = new Vector2[0];

    public int Cols => cols;
    public int Rows => rows;

    public void Rebuild(List<AgentData> agents, float cellSize, Vector2 worldMin, Vector2 worldMax)
    {
        this.cellSize = Mathf.Max(0.1f, cellSize);
        min = worldMin;

        cols = Mathf.Max(1, Mathf.CeilToInt((worldMax.x - worldMin.x) / this.cellSize));
        rows = Mathf.Max(1, Mathf.CeilToInt((worldMax.y - worldMin.y) / this.cellSize));

        int cellCount = cols * rows;

        if (cells.Length < cellCount)
        {
            var newCells = new List<int>[cellCount];
            for (int i = 0; i < cells.Length; i++) newCells[i] = cells[i];
            for (int i = cells.Length; i < cellCount; i++) newCells[i] = new List<int>();
            cells = newCells;

            countNonSilent = new int[cellCount];
            positionSumNonSilent = new Vector2[cellCount];
        }

        for (int i = 0; i < cellCount; i++)
        {
            cells[i].Clear();
            countNonSilent[i] = 0;
            positionSumNonSilent[i] = Vector2.zero;
        }

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData agent = agents[i];
            if (agent == null || !agent.alive) continue;

            int cell = CellIndexOf(agent.position);
            cells[cell].Add(i);

            if (agent.currentBehavior != BehaviorType.Silence)
            {
                countNonSilent[cell]++;
                positionSumNonSilent[cell] += agent.position;
            }
        }
    }

    public int CellXOf(float x) => Mathf.Clamp((int)((x - min.x) / cellSize), 0, cols - 1);
    public int CellYOf(float y) => Mathf.Clamp((int)((y - min.y) / cellSize), 0, rows - 1);
    public int CellIndexOf(Vector2 pos) => CellXOf(pos.x) + CellYOf(pos.y) * cols;

    /// <summary>
    /// Collects indices (into the Rebuild agent list) of alive agents within
    /// <paramref name="radius"/> of <paramref name="pos"/>. The caller's list is
    /// cleared first. May include the querying agent itself — callers filter.
    /// </summary>
    public void QueryCircle(List<AgentData> agents, Vector2 pos, float radius, List<int> results)
    {
        results.Clear();

        int minX = CellXOf(pos.x - radius);
        int maxX = CellXOf(pos.x + radius);
        int minY = CellYOf(pos.y - radius);
        int maxY = CellYOf(pos.y + radius);

        float sqrRadius = radius * radius;

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                List<int> members = cells[cx + cy * cols];

                for (int m = 0; m < members.Count; m++)
                {
                    int index = members[m];
                    if ((agents[index].position - pos).sqrMagnitude <= sqrRadius)
                        results.Add(index);
                }
            }
        }
    }

    public List<int> MembersOfCell(int cx, int cy) => cells[cx + cy * cols];

    /// <summary>
    /// True if any OTHER alive agent is within radius. Early-exits on the first
    /// hit — at high densities this is dramatically cheaper than collecting
    /// every neighbor when the caller only needs "am I alone?".
    /// </summary>
    public bool HasOtherWithin(List<AgentData> agents, int selfIndex, Vector2 pos, float radius)
    {
        int minX = CellXOf(pos.x - radius);
        int maxX = CellXOf(pos.x + radius);
        int minY = CellYOf(pos.y - radius);
        int maxY = CellYOf(pos.y + radius);

        float sqrRadius = radius * radius;

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                List<int> members = cells[cx + cy * cols];

                for (int m = 0; m < members.Count; m++)
                {
                    int index = members[m];
                    if (index == selfIndex) continue;
                    if ((agents[index].position - pos).sqrMagnitude <= sqrRadius)
                        return true;
                }
            }
        }

        return false;
    }

    public bool GetCellAggregate(int cx, int cy, out int count, out Vector2 centroid)
    {
        int index = cx + cy * cols;
        count = countNonSilent[index];

        if (count <= 0)
        {
            centroid = Vector2.zero;
            return false;
        }

        centroid = positionSumNonSilent[index] / count;
        return true;
    }
}
