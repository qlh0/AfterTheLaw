using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class WorldView : MonoBehaviour
{
    public AgentSpawner spawner;

    [Header("Agent Prefabs")]
    public GameObject agentPrefab;
    public GameObject loveAgentPrefab;
    public GameObject joyfulAgentPrefab;
    public GameObject lonelyAgentPrefab;
    public GameObject hurtAgentPrefab;

    [Header("Condition Thresholds For Visual Swap")]
    public float joyfulVisualThreshold = 0.15f;
    public float lonelyVisualThreshold = 0.15f;
    public float hurtVisualThreshold = 0.15f;

    [Header("Other Prefabs")]
    public GameObject homePrefab;
    public GameObject churchPrefab;

    [Header("Relation Markers")]
    public GameObject friendMarkerPrefab;
    public GameObject enemyMarkerPrefab;
    public float relationMarkerDistance = 0.28f;
    public float relationMarkerMaxDistance = 3.5f;
    public float relationMarkerAngleOffset = -90f; // 三角形默认顶点朝上，用这个偏移对齐方向

    [Header("Food Prefabs (Weighted)")]
    public GameObject foodPrefabA;
    public GameObject foodPrefabB;
    public GameObject foodPrefabC;

    [Range(0f, 1f)] public float foodPrefabAWeight = 0.7f;
    [Range(0f, 1f)] public float foodPrefabBWeight = 0.15f;
    [Range(0f, 1f)] public float foodPrefabCWeight = 0.15f;

    public GameObject combatMarkerPrefab;
    public GameObject deathMarkerPrefab;

    public Transform agentsParent;
    public Transform homesParent;
    public Transform churchesParent;
    public Transform foodsParent;
    public Transform combatMarkersParent;
    public Transform deathMarkersParent;

    public TMP_Text agentInfoText;

    [Header("Rendering")]
    public float visualLerpSpeed = 12f;
    public int targetFrameRate = 30;

    int selectedAgentId = -1;

    /// <summary>Currently inspected agent id (-1 = none). The drawer UI polls
    /// this to auto-open the AGENT inspector drawer on selection.</summary>
    public int SelectedAgentId => selectedAgentId;

    Dictionary<int, GameObject> visualsById = new Dictionary<int, GameObject>();
    Dictionary<int, AgentVisualState> currentVisualStateById = new Dictionary<int, AgentVisualState>();

    // Reused buffers for detecting agents that were removed from the simulation
    // (corpse cleanup) so their visuals get destroyed instead of leaking.
    readonly HashSet<int> agentIdsInState = new HashSet<int>();
    readonly List<int> orphanedVisualIds = new List<int>();

    Dictionary<int, GameObject> homeVisualsById = new Dictionary<int, GameObject>();
    Dictionary<int, GameObject> churchVisualsById = new Dictionary<int, GameObject>();
    Dictionary<int, GameObject> foodVisualsById = new Dictionary<int, GameObject>();
    Dictionary<int, Vector3> foodBaseScaleById = new Dictionary<int, Vector3>();
    Dictionary<int, GameObject> combatMarkerVisualsById = new Dictionary<int, GameObject>();
    Dictionary<int, GameObject> deathMarkerVisualsById = new Dictionary<int, GameObject>();

    enum AgentVisualState
    {
        Default,
        Love,
        Joyful,
        Lonely,
        Hurt
    }

    void Start()
    {
        Application.targetFrameRate = targetFrameRate;
        RebuildAllVisualsFromCurrentWorld();
    }

    void Update()
    {
        RebuildMissingAgentVisuals();
        RebuildMissingHomes();
        RebuildMissingChurches();
        RebuildMissingFoods();
        RebuildMissingCombatMarkers();
        RebuildMissingDeathMarkers();

        DestroyVisualsForRemovedAgents();
        UpdateAgentVisuals();
        UpdateRelationMarkers();
        UpdateHomeVisuals();
        UpdateChurchVisuals();
        UpdateFoodVisuals();
        UpdateCombatMarkerVisuals();
        UpdateDeathMarkerVisuals();

        if (Input.GetMouseButtonDown(0))
            CheckClickAgent();

        if (selectedAgentId >= 0)
            RefreshSelectedAgentInfo();
    }

    public void ResetView()
    {
        ClearAllVisuals();
        selectedAgentId = -1;

        if (agentInfoText != null)
            agentInfoText.text = "请选择一个角色";

        RebuildAllVisualsFromCurrentWorld();
    }

    void RebuildAllVisualsFromCurrentWorld()
    {
        RebuildMissingAgentVisuals();
        RebuildMissingHomes();
        RebuildMissingChurches();
        RebuildMissingFoods();
        RebuildMissingCombatMarkers();
        RebuildMissingDeathMarkers();

        if (agentInfoText != null)
            agentInfoText.text = "请选择一个角色";
    }

    void ClearAllVisuals()
    {
        DestroyAllInDictionary(visualsById);
        DestroyAllInDictionary(homeVisualsById);
        DestroyAllInDictionary(churchVisualsById);
        DestroyAllInDictionary(foodVisualsById);
        DestroyAllInDictionary(combatMarkerVisualsById);
        DestroyAllInDictionary(deathMarkerVisualsById);

        visualsById.Clear();
        currentVisualStateById.Clear();

        homeVisualsById.Clear();
        churchVisualsById.Clear();
        foodVisualsById.Clear();
        foodBaseScaleById.Clear();
        combatMarkerVisualsById.Clear();
        deathMarkerVisualsById.Clear();
    }

    void DestroyAllInDictionary(Dictionary<int, GameObject> dict)
    {
        foreach (var pair in dict)
        {
            if (pair.Value != null)
                Destroy(pair.Value);
        }
    }

    GameObject GetRandomFoodPrefab()
    {
        float total = foodPrefabAWeight + foodPrefabBWeight + foodPrefabCWeight;
        if (total <= 0f) return foodPrefabA;

        float roll = Random.value * total;

        if (roll < foodPrefabAWeight) return foodPrefabA;
        if (roll < foodPrefabAWeight + foodPrefabBWeight) return foodPrefabB;
        return foodPrefabC;
    }

    void RebuildMissingAgentVisuals()
    {
        foreach (var agent in spawner.worldState.agents)
        {
            if (visualsById.ContainsKey(agent.id)) continue;

            AgentVisualState state = GetVisualState(agent);
            GameObject prefabToUse = GetPrefabForState(state);

            GameObject obj = Instantiate(prefabToUse, agent.position, Quaternion.identity);
            obj.transform.SetParent(agentsParent);

            AgentVisual_Number visual = obj.GetComponent<AgentVisual_Number>();
            if (visual == null)
                visual = obj.GetComponentInChildren<AgentVisual_Number>();

            if (visual != null)
                visual.SetAgentData(agent.id, agent.generation);

            EnsureRelationMarkers(obj);

            visualsById[agent.id] = obj;
            currentVisualStateById[agent.id] = state;
        }
    }

    void UpdateAgentVisuals()
    {
        foreach (var agent in spawner.worldState.agents)
        {
            if (!visualsById.ContainsKey(agent.id)) continue;

            GameObject obj = visualsById[agent.id];

            if (!agent.alive)
            {
                obj.SetActive(false);
                continue;
            }

            obj.SetActive(true);

            AgentVisualState targetState = GetVisualState(agent);

            if (targetState != currentVisualStateById[agent.id])
            {
                Vector3 pos = obj.transform.position;

                Destroy(obj);

                GameObject prefabToUse = GetPrefabForState(targetState);
                GameObject newObj = Instantiate(prefabToUse, pos, Quaternion.identity);
                newObj.transform.SetParent(agentsParent);

                AgentVisual_Number visual = newObj.GetComponent<AgentVisual_Number>();
                if (visual == null)
                    visual = newObj.GetComponentInChildren<AgentVisual_Number>();

                if (visual != null)
                    visual.SetAgentData(agent.id, agent.generation);

                EnsureRelationMarkers(newObj);

                visualsById[agent.id] = newObj;
                currentVisualStateById[agent.id] = targetState;

                obj = newObj;
            }

            obj.transform.position = Vector3.Lerp(
                obj.transform.position,
                agent.position,
                Time.deltaTime * visualLerpSpeed
            );
        }
    }

    /// <summary>
    /// The simulation removes long-dead agents from the world list; destroy
    /// their visuals too, otherwise inactive GameObjects pile up over a full
    /// exhibition day. Also clears the info panel if the selected agent is gone.
    /// </summary>
    void DestroyVisualsForRemovedAgents()
    {
        agentIdsInState.Clear();
        foreach (var agent in spawner.worldState.agents)
            agentIdsInState.Add(agent.id);

        orphanedVisualIds.Clear();
        foreach (var pair in visualsById)
        {
            if (!agentIdsInState.Contains(pair.Key))
                orphanedVisualIds.Add(pair.Key);
        }

        for (int i = 0; i < orphanedVisualIds.Count; i++)
        {
            int id = orphanedVisualIds[i];

            if (visualsById.TryGetValue(id, out GameObject obj) && obj != null)
                Destroy(obj);

            visualsById.Remove(id);
            currentVisualStateById.Remove(id);
        }

        if (selectedAgentId >= 0 && !agentIdsInState.Contains(selectedAgentId))
        {
            selectedAgentId = -1;
            if (agentInfoText != null)
                agentInfoText.text = "请选择一个角色";
        }
    }

    void EnsureRelationMarkers(GameObject obj)
    {
        AgentRelationMarkers markers = obj.GetComponent<AgentRelationMarkers>();
        if (markers == null)
            markers = obj.AddComponent<AgentRelationMarkers>();

        if (markers.friendMarker == null && friendMarkerPrefab != null)
        {
            GameObject m = Instantiate(friendMarkerPrefab, obj.transform);
            m.name = "FriendMarker";
            markers.friendMarker = m.transform;
        }

        if (markers.enemyMarker == null && enemyMarkerPrefab != null)
        {
            GameObject m = Instantiate(enemyMarkerPrefab, obj.transform);
            m.name = "EnemyMarker";
            markers.enemyMarker = m.transform;
        }
    }

    void UpdateRelationMarkers()
    {
        var world = spawner.worldState;

        foreach (var self in world.agents)
        {
            if (self == null || !self.alive) continue;
            if (!visualsById.TryGetValue(self.id, out GameObject obj) || obj == null) continue;

            AgentRelationMarkers markers = obj.GetComponent<AgentRelationMarkers>();
            if (markers == null) continue;

            AgentData nearestFriend = null;
            AgentData nearestEnemy = null;
            float nearestFriendDist = float.MaxValue;
            float nearestEnemyDist = float.MaxValue;

            // Relations are sparse — iterate the agent's actual relationships
            // instead of scanning the whole population (was O(n²) per frame).
            foreach (var pair in self.socialRelations)
            {
                if (!world.agentsById.TryGetValue(pair.Key, out AgentData other)) continue;
                if (other == null || !other.alive) continue;

                float dist = Vector2.Distance(self.position, other.position);
                if (dist > relationMarkerMaxDistance) continue;

                if (pair.Value == SocialRelationType.Friend && dist < nearestFriendDist)
                {
                    nearestFriendDist = dist;
                    nearestFriend = other;
                }
                else if (pair.Value == SocialRelationType.Enemy && dist < nearestEnemyDist)
                {
                    nearestEnemyDist = dist;
                    nearestEnemy = other;
                }
            }

            UpdateSingleMarker(markers.friendMarker, self, nearestFriend);
            UpdateSingleMarker(markers.enemyMarker, self, nearestEnemy);
        }
    }

    void UpdateSingleMarker(Transform marker, AgentData self, AgentData target)
    {
        if (marker == null) return;

        if (target == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);

        Vector2 dir = (target.position - self.position).normalized;

        // 沿着以 agent 为圆心的固定半径轨道移动
        Vector3 localPos = new Vector3(dir.x, dir.y, 0f) * relationMarkerDistance;
        marker.localPosition = localPos;

        // 三角形顶点始终朝向目标；如果素材默认朝上，就加 -90 偏移
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + relationMarkerAngleOffset;
        marker.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    AgentVisualState GetVisualState(AgentData agent)
    {
        if (agent.hurt >= hurtVisualThreshold && hurtAgentPrefab != null)
            return AgentVisualState.Hurt;

        if (agent.lonely >= lonelyVisualThreshold && lonelyAgentPrefab != null)
            return AgentVisualState.Lonely;

        if (agent.joyful >= joyfulVisualThreshold && joyfulAgentPrefab != null)
            return AgentVisualState.Joyful;

        if (agent.currentBehavior == BehaviorType.Love && loveAgentPrefab != null)
            return AgentVisualState.Love;

        return AgentVisualState.Default;
    }

    GameObject GetPrefabForState(AgentVisualState state)
    {
        switch (state)
        {
            case AgentVisualState.Hurt:
                if (hurtAgentPrefab != null) return hurtAgentPrefab;
                break;
            case AgentVisualState.Lonely:
                if (lonelyAgentPrefab != null) return lonelyAgentPrefab;
                break;
            case AgentVisualState.Joyful:
                if (joyfulAgentPrefab != null) return joyfulAgentPrefab;
                break;
            case AgentVisualState.Love:
                if (loveAgentPrefab != null) return loveAgentPrefab;
                break;
        }

        return agentPrefab;
    }

    void RebuildMissingHomes()
    {
        if (homePrefab == null || homesParent == null) return;

        foreach (var home in spawner.worldState.homes)
        {
            if (homeVisualsById.ContainsKey(home.id)) continue;

            GameObject obj = Instantiate(homePrefab, home.position, Quaternion.identity);
            obj.transform.SetParent(homesParent);

            homeVisualsById[home.id] = obj;
        }
    }

    void UpdateHomeVisuals()
    {
        DestroyOrphanedVisuals(homeVisualsById, spawner.worldState.homes, h => h.id);

        foreach (var home in spawner.worldState.homes)
        {
            if (!homeVisualsById.ContainsKey(home.id)) continue;

            GameObject obj = homeVisualsById[home.id];

            obj.transform.position = Vector3.Lerp(
                obj.transform.position,
                home.position,
                Time.deltaTime * visualLerpSpeed
            );
        }
    }

    void RebuildMissingChurches()
    {
        if (churchPrefab == null || churchesParent == null) return;

        foreach (var church in spawner.worldState.churches)
        {
            if (churchVisualsById.ContainsKey(church.id)) continue;

            GameObject obj = Instantiate(churchPrefab, church.position, Quaternion.identity);
            obj.transform.SetParent(churchesParent);

            churchVisualsById[church.id] = obj;
        }
    }

    void UpdateChurchVisuals()
    {
        foreach (var church in spawner.worldState.churches)
        {
            if (!churchVisualsById.ContainsKey(church.id)) continue;

            GameObject obj = churchVisualsById[church.id];

            obj.transform.position = Vector3.Lerp(
                obj.transform.position,
                church.position,
                Time.deltaTime * visualLerpSpeed
            );
        }
    }

    void RebuildMissingFoods()
    {
        if (foodsParent == null) return;

        foreach (var food in spawner.worldState.foods)
        {
            if (foodVisualsById.ContainsKey(food.id)) continue;

            GameObject prefabToUse = GetRandomFoodPrefab();
            if (prefabToUse == null) continue;

            GameObject obj = Instantiate(prefabToUse, food.position, Quaternion.identity);
            obj.transform.SetParent(foodsParent);

            foodVisualsById[food.id] = obj;
            foodBaseScaleById[food.id] = obj.transform.localScale;
        }
    }

    void UpdateFoodVisuals()
    {
        // Consumed foods are now removed from the world list — destroy their
        // visuals (previously they were only hidden and piled up over time).
        agentIdsInState.Clear(); // reuse the id buffer
        foreach (var food in spawner.worldState.foods)
            agentIdsInState.Add(food.id);

        orphanedVisualIds.Clear();
        foreach (var pair in foodVisualsById)
        {
            if (!agentIdsInState.Contains(pair.Key))
                orphanedVisualIds.Add(pair.Key);
        }

        for (int i = 0; i < orphanedVisualIds.Count; i++)
        {
            int id = orphanedVisualIds[i];

            if (foodVisualsById.TryGetValue(id, out GameObject foodObj) && foodObj != null)
                Destroy(foodObj);

            foodVisualsById.Remove(id);
            foodBaseScaleById.Remove(id);
        }

        foreach (var food in spawner.worldState.foods)
        {
            if (!foodVisualsById.ContainsKey(food.id)) continue;

            GameObject obj = foodVisualsById[food.id];

            obj.transform.position = Vector3.Lerp(
                obj.transform.position,
                food.position,
                Time.deltaTime * visualLerpSpeed
            );

            obj.SetActive(!food.consumed);

            if (foodBaseScaleById.TryGetValue(food.id, out Vector3 baseScale))
            {
                float growthScale = food.amount / Mathf.Max(0.0001f, food.baseAmount);
                obj.transform.localScale = baseScale * growthScale;
            }
        }
    }

    void RebuildMissingCombatMarkers()
    {
        if (combatMarkerPrefab == null || combatMarkersParent == null) return;

        foreach (var marker in spawner.worldState.combatMarkers)
        {
            if (combatMarkerVisualsById.ContainsKey(marker.id)) continue;

            GameObject obj = Instantiate(combatMarkerPrefab, marker.position, Quaternion.identity);
            obj.transform.SetParent(combatMarkersParent);

            combatMarkerVisualsById[marker.id] = obj;
        }
    }

    void UpdateCombatMarkerVisuals()
    {
        HashSet<int> liveIds = new HashSet<int>();

        foreach (var marker in spawner.worldState.combatMarkers)
        {
            liveIds.Add(marker.id);

            if (combatMarkerVisualsById.TryGetValue(marker.id, out GameObject obj))
            {
                obj.transform.position = Vector3.Lerp(
                    obj.transform.position,
                    marker.position,
                    Time.deltaTime * visualLerpSpeed
                );

                obj.SetActive(true);
            }
        }

        foreach (var pair in combatMarkerVisualsById)
        {
            if (!liveIds.Contains(pair.Key) && pair.Value != null)
                pair.Value.SetActive(false);
        }
    }

    void RebuildMissingDeathMarkers()
    {
        if (deathMarkerPrefab == null || deathMarkersParent == null) return;

        foreach (var marker in spawner.worldState.deathMarkers)
        {
            if (deathMarkerVisualsById.ContainsKey(marker.id)) continue;

            GameObject obj = Instantiate(deathMarkerPrefab, marker.position, Quaternion.identity);
            obj.transform.SetParent(deathMarkersParent);

            deathMarkerVisualsById[marker.id] = obj;
        }
    }

    void UpdateDeathMarkerVisuals()
    {
        DestroyOrphanedVisuals(deathMarkerVisualsById, spawner.worldState.deathMarkers, m => m.id);

        foreach (var marker in spawner.worldState.deathMarkers)
        {
            if (!deathMarkerVisualsById.ContainsKey(marker.id)) continue;

            GameObject obj = deathMarkerVisualsById[marker.id];

            obj.transform.position = Vector3.Lerp(
                obj.transform.position,
                marker.position,
                Time.deltaTime * visualLerpSpeed
            );

            obj.SetActive(true);
        }
    }

    /// <summary>Destroys visuals whose backing entity left the world state
    /// (entity caps / consumption / corpse removal). Reuses shared buffers.</summary>
    void DestroyOrphanedVisuals<T>(Dictionary<int, GameObject> visuals, List<T> entities, System.Func<T, int> idOf)
    {
        agentIdsInState.Clear();
        for (int i = 0; i < entities.Count; i++)
            agentIdsInState.Add(idOf(entities[i]));

        orphanedVisualIds.Clear();
        foreach (var pair in visuals)
        {
            if (!agentIdsInState.Contains(pair.Key))
                orphanedVisualIds.Add(pair.Key);
        }

        for (int i = 0; i < orphanedVisualIds.Count; i++)
        {
            int id = orphanedVisualIds[i];

            if (visuals.TryGetValue(id, out GameObject obj) && obj != null)
                Destroy(obj);

            visuals.Remove(id);
        }
    }

    void CheckClickAgent()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        int agentId = FindAgentAt(camera.ScreenToWorldPoint(Input.mousePosition));
        if (agentId < 0) return;

        selectedAgentId = agentId;
        RefreshSelectedAgentInfo();
    }

    int FindAgentAt(Vector2 point)
    {
        // Visual positions are updated in Update, including when physics is paused.
        Physics2D.SyncTransforms();

        int closestAgentId = -1;
        float closestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
        {
            AgentVisual_Number visual = hit.GetComponentInParent<AgentVisual_Number>();
            if (visual == null ||
                !visualsById.TryGetValue(visual.agentId, out GameObject currentVisual) ||
                currentVisual == null || !hit.transform.IsChildOf(currentVisual.transform))
                continue;

            float distance = ((Vector2)hit.bounds.center - point).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestAgentId = visual.agentId;
            }
        }

        return closestAgentId;
    }

    void RefreshSelectedAgentInfo()
    {
        AgentData agent = spawner.worldState.agents.Find(a => a.id == selectedAgentId);
        if (agent == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"角色 {agent.id}");
        sb.AppendLine($"世代：第 {agent.generation} 代");
        sb.AppendLine($"状态：{(agent.alive ? "存活" : "已死亡")}");
        sb.AppendLine($"年龄：{agent.age:F1} / {agent.maxAge:F1}");
        sb.AppendLine($"生命值：{agent.hp:F2}");
        sb.AppendLine($"食物：{agent.food:F2} / {agent.maxFood:F2}");
        sb.AppendLine($"饥饿：{agent.hungry:F2}");
        sb.AppendLine($"孤独：{agent.lonely:F2}");
        sb.AppendLine($"喜悦：{agent.joyful:F2}");
        sb.AppendLine($"受伤：{agent.hurt:F2}");
        sb.AppendLine($"行为：{ChineseUi.RuleLabel(agent.currentBehavior.ToString())}");

        if (agentInfoText != null)
            agentInfoText.text = sb.ToString();
    }
}