using System.Collections.Generic;
using UnityEngine;

public class TreeBirdBurstManager : MonoBehaviour
{
    [Header("Timing")]
    public bool autoTrigger = true;
    public float baseInterval = 15f;
    public float intervalJitter = 3f;

    [Header("Burst Count")]
    public int minTreesPerBurst = 1;
    public int maxTreesPerBurst = 3;

    [Header("Visibility")]
    public bool onlyTriggerVisibleTrees = true;

    [Header("Tree Type Weights")]
    public float weightA = 1f;
    public float weightB = 1f;
    public float weightC = 1f;

    float timer;

    void Start()
    {
        ResetTimer();
    }

    void Update()
    {
        if (!autoTrigger) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        TriggerRandomTrees();
        ResetTimer();
    }

    void ResetTimer()
    {
        timer = baseInterval + Random.Range(-intervalJitter, intervalJitter);
        timer = Mathf.Max(0.1f, timer);
    }

    void TriggerRandomTrees()
    {
        TreeBirdBurstTrigger[] allTrees = FindObjectsByType<TreeBirdBurstTrigger>(FindObjectsSortMode.None);
        if (allTrees == null || allTrees.Length == 0) return;

        List<TreeBirdBurstTrigger> candidates = new List<TreeBirdBurstTrigger>();

        for (int i = 0; i < allTrees.Length; i++)
        {
            if (allTrees[i] == null) continue;

            if (onlyTriggerVisibleTrees && !IsVisibleOnScreen(allTrees[i].transform.position))
                continue;

            candidates.Add(allTrees[i]);
        }

        if (candidates.Count == 0) return;

        int burstCount = Random.Range(minTreesPerBurst, maxTreesPerBurst + 1);
        burstCount = Mathf.Clamp(burstCount, 1, candidates.Count);

        for (int i = 0; i < burstCount; i++)
        {
            TreeBirdBurstTrigger chosen = PickWeightedTree(candidates);
            if (chosen == null) break;

            chosen.TriggerBurst();
            candidates.Remove(chosen);

            if (candidates.Count == 0) break;
        }
    }

    TreeBirdBurstTrigger PickWeightedTree(List<TreeBirdBurstTrigger> candidates)
    {
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += GetTreeWeight(candidates[i]);
        }

        if (totalWeight <= 0f)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        float roll = Random.Range(0f, totalWeight);
        float running = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            running += GetTreeWeight(candidates[i]);
            if (roll <= running)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    float GetTreeWeight(TreeBirdBurstTrigger tree)
    {
        switch (tree.treeType)
        {
            case BirdBurstAudioManager.BirdTreeType.A:
                return Mathf.Max(0f, weightA);
            case BirdBurstAudioManager.BirdTreeType.B:
                return Mathf.Max(0f, weightB);
            case BirdBurstAudioManager.BirdTreeType.C:
                return Mathf.Max(0f, weightC);
        }

        return 1f;
    }

    bool IsVisibleOnScreen(Vector3 worldPosition)
    {
        if (Camera.main == null) return true;

        Vector3 viewport = Camera.main.WorldToViewportPoint(worldPosition);

        return viewport.z > 0f &&
               viewport.x >= 0f && viewport.x <= 1f &&
               viewport.y >= 0f && viewport.y <= 1f;
    }
}