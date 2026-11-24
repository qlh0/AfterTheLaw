using System.Collections.Generic;
using UnityEngine;

public class AgentAudioManager : MonoBehaviour
{
    public AgentSpawner spawner;

    [Header("Audio Prefabs")]
    public GameObject defaultAudioPrefab;
    public GameObject loveAudioPrefab;
    public GameObject joyfulAudioPrefab;
    public GameObject lonelyAudioPrefab;
    public GameObject hurtAudioPrefab;

    [Header("Condition Thresholds")]
    public float joyfulThreshold = 0.15f;
    public float lonelyThreshold = 0.15f;
    public float hurtThreshold = 0.15f;

    [Header("Parent")]
    public Transform audioParent;

    Dictionary<int, GameObject> audioObjectsById = new Dictionary<int, GameObject>();
    Dictionary<int, AgentAudioState> currentAudioStateById = new Dictionary<int, AgentAudioState>();

    enum AgentAudioState
    {
        Default,
        Love,
        Joyful,
        Lonely,
        Hurt
    }

    void Update()
    {
        if (spawner == null || spawner.worldState == null) return;

        RebuildMissingAudioObjects();
        UpdateAudioObjects();
        CleanupDeadOrMissingAgents();
    }

    void RebuildMissingAudioObjects()
    {
        foreach (var agent in spawner.worldState.agents)
        {
            if (!agent.alive) continue;
            if (audioObjectsById.ContainsKey(agent.id)) continue;

            AgentAudioState state = GetAudioState(agent);
            GameObject prefab = GetPrefabForState(state);
            if (prefab == null) continue;

            GameObject obj = Instantiate(prefab, agent.position, Quaternion.identity);

            if (audioParent != null)
                obj.transform.SetParent(audioParent);

            audioObjectsById[agent.id] = obj;
            currentAudioStateById[agent.id] = state;
        }
    }

    void UpdateAudioObjects()
    {
        foreach (var agent in spawner.worldState.agents)
        {
            if (!audioObjectsById.ContainsKey(agent.id)) continue;

            GameObject obj = audioObjectsById[agent.id];

            if (!agent.alive)
            {
                obj.SetActive(false);
                continue;
            }

            obj.SetActive(true);
            obj.transform.position = agent.position;

            AgentAudioState targetState = GetAudioState(agent);

            if (targetState != currentAudioStateById[agent.id])
            {
                Vector3 pos = obj.transform.position;
                Destroy(obj);

                GameObject prefab = GetPrefabForState(targetState);
                if (prefab == null)
                {
                    audioObjectsById.Remove(agent.id);
                    currentAudioStateById.Remove(agent.id);
                    continue;
                }

                GameObject newObj = Instantiate(prefab, pos, Quaternion.identity);

                if (audioParent != null)
                    newObj.transform.SetParent(audioParent);

                audioObjectsById[agent.id] = newObj;
                currentAudioStateById[agent.id] = targetState;
            }
        }
    }

    void CleanupDeadOrMissingAgents()
    {
        HashSet<int> liveIds = new HashSet<int>();

        foreach (var agent in spawner.worldState.agents)
        {
            if (agent.alive)
                liveIds.Add(agent.id);
        }

        List<int> removeIds = new List<int>();

        foreach (var pair in audioObjectsById)
        {
            if (!liveIds.Contains(pair.Key))
            {
                if (pair.Value != null)
                    Destroy(pair.Value);

                removeIds.Add(pair.Key);
            }
        }

        foreach (int id in removeIds)
        {
            audioObjectsById.Remove(id);
            currentAudioStateById.Remove(id);
        }
    }

    AgentAudioState GetAudioState(AgentData agent)
    {
        if (agent.hurt >= hurtThreshold && hurtAudioPrefab != null)
            return AgentAudioState.Hurt;

        if (agent.lonely >= lonelyThreshold && lonelyAudioPrefab != null)
            return AgentAudioState.Lonely;

        if (agent.joyful >= joyfulThreshold && joyfulAudioPrefab != null)
            return AgentAudioState.Joyful;

        if (agent.currentBehavior == BehaviorType.Love && loveAudioPrefab != null)
            return AgentAudioState.Love;

        return AgentAudioState.Default;
    }

    GameObject GetPrefabForState(AgentAudioState state)
    {
        switch (state)
        {
            case AgentAudioState.Hurt:
                return hurtAudioPrefab != null ? hurtAudioPrefab : defaultAudioPrefab;
            case AgentAudioState.Lonely:
                return lonelyAudioPrefab != null ? lonelyAudioPrefab : defaultAudioPrefab;
            case AgentAudioState.Joyful:
                return joyfulAudioPrefab != null ? joyfulAudioPrefab : defaultAudioPrefab;
            case AgentAudioState.Love:
                return loveAudioPrefab != null ? loveAudioPrefab : defaultAudioPrefab;
            default:
                return defaultAudioPrefab;
        }
    }
}