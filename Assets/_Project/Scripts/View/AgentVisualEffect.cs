using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AgentVisualEffect : MonoBehaviour
{
    [Header("References")]
    public AgentVisual_Number numberVisual;
    public Transform effectTarget;

    [Header("Breathing")]
    public bool enableBreathing = false;
    public float breathAmplitude = 0.08f;
    public float breathSpeed = 2f;

    [Header("Shake")]
    public bool enableShake = false;
    public float shakeAmount = 0.03f;
    public float shakeSpeed = 30f;

    [Header("Trail")]
    public bool enableTrail = false;
    public float trailSpawnInterval = 0.06f;
    public int trailLength = 8;
    public float trailLifetime = 0.35f;
    [Range(0f, 1f)] public float trailAlpha = 0.35f;
    public float trailMinMoveDistance = 0.03f;

    [Header("Imprint")]
    public bool enableImprint = false;
    public float imprintSpawnInterval = 0.12f;
    public float imprintMinMoveDistance = 0.06f;
    [Range(0f, 1f)] public float imprintAlpha = 0.6f;
    public float imprintLifetime = 0f; // 0 = 永久留下

    TMP_Text tmp;
    Vector3 baseLocalPosition;
    Vector3 baseLocalScale;

    float trailTimer = 0f;
    float imprintTimer = 0f;

    Vector3 lastTrailSpawnPos;
    Vector3 lastImprintSpawnPos;
    bool hasTrailSpawned = false;
    bool hasImprintSpawned = false;

    readonly Queue<GameObject> trailGhosts = new Queue<GameObject>();

    void Awake()
    {
        if (numberVisual == null)
            numberVisual = GetComponent<AgentVisual_Number>();

        if (numberVisual != null)
            tmp = numberVisual.GetText();

        if (tmp == null)
            tmp = GetComponentInChildren<TMP_Text>();

        // 关键修复：effectTarget 永远只允许是文字对象，不允许是根物体
        if (effectTarget == null && tmp != null)
            effectTarget = tmp.transform;

        if (effectTarget == null)
        {
            enabled = false;
            return;
        }

        if (effectTarget == transform)
        {
            Debug.LogWarning("AgentVisualEffect: effectTarget 不能是 prefab 根物体，请指向 TextMeshPro 子物体。已自动禁用。", this);
            enabled = false;
            return;
        }

        baseLocalPosition = effectTarget.localPosition;
        baseLocalScale = effectTarget.localScale;
    }

    void Update()
    {
        if (effectTarget == null || tmp == null) return;

        ApplyBreathAndShake();
        UpdateTrail();
        UpdateImprint();
    }

    void ApplyBreathAndShake()
    {
        Vector3 pos = baseLocalPosition;
        Vector3 scale = baseLocalScale;

        if (enableBreathing)
        {
            float wave = 1f + Mathf.Sin(Time.time * breathSpeed) * breathAmplitude;
            scale = baseLocalScale * wave;
        }

        if (enableShake)
        {
            float x = (Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) - 0.5f) * 2f * shakeAmount;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeSpeed) - 0.5f) * 2f * shakeAmount;
            pos += new Vector3(x, y, 0f);
        }

        effectTarget.localPosition = pos;
        effectTarget.localScale = scale;
    }

    void UpdateTrail()
    {
        if (!enableTrail) return;

        trailTimer += Time.deltaTime;

        if (trailTimer < trailSpawnInterval) return;
        trailTimer = 0f;

        Vector3 currentPos = effectTarget.position;

        if (hasTrailSpawned && Vector3.Distance(currentPos, lastTrailSpawnPos) < trailMinMoveDistance)
            return;

        lastTrailSpawnPos = currentPos;
        hasTrailSpawned = true;

        GameObject ghost = CreateGhost(currentPos, trailAlpha, trailLifetime, false);
        trailGhosts.Enqueue(ghost);

        while (trailGhosts.Count > trailLength)
        {
            GameObject oldest = trailGhosts.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    void UpdateImprint()
    {
        if (!enableImprint) return;

        imprintTimer += Time.deltaTime;

        if (imprintTimer < imprintSpawnInterval) return;
        imprintTimer = 0f;

        Vector3 currentPos = effectTarget.position;

        if (hasImprintSpawned && Vector3.Distance(currentPos, lastImprintSpawnPos) < imprintMinMoveDistance)
            return;

        lastImprintSpawnPos = currentPos;
        hasImprintSpawned = true;

        CreateGhost(currentPos, imprintAlpha, imprintLifetime, true);
    }

    GameObject CreateGhost(Vector3 worldPos, float alpha, float lifetime, bool keepForever)
    {
        GameObject ghost = new GameObject("TextGhost");
        ghost.transform.position = worldPos;
        ghost.transform.rotation = effectTarget.rotation;
        ghost.transform.localScale = effectTarget.lossyScale;

        TextMeshPro ghostText = ghost.AddComponent<TextMeshPro>();
        CopyTextStyle(tmp, ghostText, alpha);

        if (!keepForever && lifetime > 0f)
        {
            GhostFade fade = ghost.AddComponent<GhostFade>();
            fade.Init(ghostText, lifetime);
        }

        return ghost;
    }

    void CopyTextStyle(TMP_Text source, TMP_Text target, float alpha)
    {
        target.text = source.text;
        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize;
        target.alignment = source.alignment;
        target.characterSpacing = source.characterSpacing;
        target.wordSpacing = source.wordSpacing;
        target.lineSpacing = source.lineSpacing;
        target.paragraphSpacing = source.paragraphSpacing;
        target.enableWordWrapping = source.enableWordWrapping;
        target.overflowMode = source.overflowMode;
        target.rectTransform.sizeDelta = source.rectTransform.sizeDelta;

        Color c = source.color;
        c.a = alpha;
        target.color = c;

        MeshRenderer srcRenderer = source.GetComponent<MeshRenderer>();
        MeshRenderer dstRenderer = target.GetComponent<MeshRenderer>();

        if (srcRenderer != null && dstRenderer != null)
        {
            dstRenderer.sortingLayerID = srcRenderer.sortingLayerID;
            dstRenderer.sortingOrder = srcRenderer.sortingOrder - 1;
        }
    }
}

public class GhostFade : MonoBehaviour
{
    TMP_Text textRef;
    float lifetime;
    float timer;
    Color startColor;

    public void Init(TMP_Text textComponent, float fadeLifetime)
    {
        textRef = textComponent;
        lifetime = fadeLifetime;
        timer = 0f;

        if (textRef != null)
            startColor = textRef.color;
    }

    void Update()
    {
        if (textRef == null)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / Mathf.Max(0.0001f, lifetime));
        Color c = startColor;
        c.a = Mathf.Lerp(startColor.a, 0f, t);
        textRef.color = c;

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}