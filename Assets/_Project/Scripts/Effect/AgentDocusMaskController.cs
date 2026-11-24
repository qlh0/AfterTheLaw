using System.Collections.Generic;
using UnityEngine;

public class AgentFocusMaskController : MonoBehaviour
{
    public AgentSpawner spawner;
    public Material focusMaterial;

    [Header("Mask Texture")]
    public int textureWidth = 512;
    public int textureHeight = 288;
    public FilterMode filterMode = FilterMode.Bilinear;

    [Header("Performance")]
    public int frameSkip = 2;
    int frameCounter = 0;

    [Header("Focus Circles")]
    [Range(0.001f, 0.5f)] public float focusRadius = 0.08f;
    [Range(0.001f, 0.5f)] public float focusSoftness = 0.05f;
    [Range(0f, 1f)] public float circleStrength = 1f;
    [Min(0.01f)] public float falloffPower = 1.5f;

    [Header("No Agent Fade")]
    public float noAgentFadeDuration = 2f;
    float noAgentFadeProgress = 0f;

    Texture2D maskTexture;
    Color[] clearPixels;
    Color[] workPixels;

    void Start()
    {
        CreateMaskTexture();
    }

    void LateUpdate()
    {
        if (spawner == null || spawner.worldState == null) return;
        if (focusMaterial == null) return;
        if (Camera.main == null) return;

        if (maskTexture == null ||
            maskTexture.width != textureWidth ||
            maskTexture.height != textureHeight)
        {
            CreateMaskTexture();
        }

        frameCounter++;
        if (frameSkip > 1 && frameCounter % frameSkip != 0)
        {
            focusMaterial.SetTexture("_FocusMask", maskTexture);
            return;
        }

        System.Array.Copy(clearPixels, workPixels, clearPixels.Length);

        List<AgentData> agents = spawner.worldState.agents;
        Camera cam = Camera.main;

        int aliveCount = 0;

        for (int i = 0; i < agents.Count; i++)
        {
            AgentData agent = agents[i];
            if (!agent.alive) continue;

            aliveCount++;

            Vector3 viewport = cam.WorldToViewportPoint(agent.position);
            if (viewport.z < 0f) continue;
            if (viewport.x < -0.2f || viewport.x > 1.2f || viewport.y < -0.2f || viewport.y > 1.2f) continue;

            DrawSoftCircle(
                viewport.x,
                viewport.y,
                focusRadius,
                focusSoftness,
                circleStrength
            );
        }

        if (aliveCount == 0)
        {
            noAgentFadeProgress += Time.deltaTime / Mathf.Max(0.0001f, noAgentFadeDuration);
            noAgentFadeProgress = Mathf.Clamp01(noAgentFadeProgress);

            FillMaskToWhite(noAgentFadeProgress);
        }
        else
        {
            noAgentFadeProgress = 0f;
        }

        maskTexture.SetPixels(workPixels);
        maskTexture.Apply(false, false);

        focusMaterial.SetTexture("_FocusMask", maskTexture);
    }

    void CreateMaskTexture()
    {
        if (maskTexture != null)
        {
            Destroy(maskTexture);
        }

        maskTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true);
        maskTexture.wrapMode = TextureWrapMode.Clamp;
        maskTexture.filterMode = filterMode;

        clearPixels = new Color[textureWidth * textureHeight];
        workPixels = new Color[textureWidth * textureHeight];

        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = Color.black;
            workPixels[i] = Color.black;
        }

        maskTexture.SetPixels(clearPixels);
        maskTexture.Apply(false, false);
    }

    void FillMaskToWhite(float amount)
    {
        for (int i = 0; i < workPixels.Length; i++)
        {
            float current = workPixels[i].r;
            float result = Mathf.Max(current, amount);
            workPixels[i] = new Color(result, result, result, 1f);
        }
    }

    void DrawSoftCircle(float centerX, float centerY, float radius, float softness, float strength)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt((centerX - radius - softness) * textureWidth), 0, textureWidth - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((centerX + radius + softness) * textureWidth), 0, textureWidth - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((centerY - radius - softness) * textureHeight), 0, textureHeight - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((centerY + radius + softness) * textureHeight), 0, textureHeight - 1);

        float outerRadius = radius + softness;

        for (int y = minY; y <= maxY; y++)
        {
            float v = (y + 0.5f) / textureHeight;

            for (int x = minX; x <= maxX; x++)
            {
                float u = (x + 0.5f) / textureWidth;
                float dist = Vector2.Distance(new Vector2(u, v), new Vector2(centerX, centerY));

                float value = 0f;

                if (dist <= radius)
                {
                    value = 1f;
                }
                else if (dist <= outerRadius)
                {
                    float t = Mathf.InverseLerp(outerRadius, radius, dist);
                    t = Mathf.Clamp01(t);
                    t = Mathf.Pow(t, falloffPower);
                    value = Mathf.SmoothStep(0f, 1f, t);
                }

                value *= strength;

                int index = y * textureWidth + x;
                float current = workPixels[index].r;
                float result = Mathf.Max(current, value);

                workPixels[index] = new Color(result, result, result, 1f);
            }
        }
    }

    void OnDestroy()
    {
        if (maskTexture != null)
        {
            Destroy(maskTexture);
        }
    }
}