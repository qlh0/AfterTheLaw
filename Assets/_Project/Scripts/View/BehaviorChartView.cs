using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Small live chart (bottom-left) plotting the share of agents in each
/// behavior state plus total population over the last ~40 seconds. When the
/// audience edits a Rule Book rule, the curves bend — long-term evidence that
/// their change rewired the society, complementing the instant attribution
/// visuals in RuleAttributionView.
///
/// Rendered into a small Texture2D each simulation tick; no external assets.
/// </summary>
public class BehaviorChartView : MonoBehaviour
{
    public AgentSpawner spawner;

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(280f, 96f);
    public Vector2 margin = new Vector2(14f, 14f);
    public KeyCode toggleKey = KeyCode.C;
    public bool visibleOnStart = true;

    [Header("Series Colors")]
    public Color loveColor = new Color(0.95f, 0.4f, 0.6f);
    public Color violenceColor = new Color(0.85f, 0.15f, 0.1f);
    public Color silenceColor = new Color(0.35f, 0.45f, 0.65f);
    public Color populationColor = new Color(0.4f, 0.4f, 0.4f);
    public Color backgroundColor = new Color(1f, 1f, 1f, 0.85f);

    const int Samples = 128;
    const int TexWidth = 256;
    const int TexHeight = 72;

    // Ring buffers (fractions 0..1).
    readonly float[] love = new float[Samples];
    readonly float[] violence = new float[Samples];
    readonly float[] silence = new float[Samples];
    readonly float[] population = new float[Samples];
    int head;
    int lastTickIndex = -1;
    int maxPopulationSeen = 1;

    Texture2D texture;
    Color32[] pixels;
    TMP_Text legend;
    GameObject panelRoot;
    bool isVisible;
    bool built;

    void Start()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<AgentSpawner>();
    }

    /// <summary>
    /// Single-screen mode: mount inside the rule bar's ChartMount slot (may
    /// not exist yet on the first frames — retried from Update). Dual-display
    /// exhibition: standalone panel in the world screen's corner.
    /// </summary>
    void TryBuild()
    {
        RectTransform mount = null;
        DisplayModeManager manager = FindFirstObjectByType<DisplayModeManager>();

        if (manager != null && manager.IsSingleScreenMode)
        {
            if (manager.ChartMount == null) return; // bar not built yet — retry next frame
            mount = manager.ChartMount;
        }

        BuildPanel(mount);
        built = true;
        isVisible = visibleOnStart;
        panelRoot.SetActive(isVisible);
    }

    void BuildPanel(RectTransform mount)
    {
        RectTransform parent;

        if (mount == null)
        {
            GameObject canvasGO = new GameObject("BehaviorChartCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 0;
            canvas.sortingOrder = 110;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            parent = canvasGO.GetComponent<RectTransform>();
        }
        else
        {
            parent = mount;
        }

        panelRoot = new GameObject("ChartPanel", typeof(RectTransform), typeof(Image));
        RectTransform panel = panelRoot.GetComponent<RectTransform>();
        panel.SetParent(parent, false);

        if (mount == null)
        {
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = margin;
            panel.sizeDelta = panelSize;
        }
        else
        {
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }

        panelRoot.GetComponent<Image>().color = backgroundColor;

        // Chart texture.
        texture = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        pixels = new Color32[TexWidth * TexHeight];

        GameObject chartGO = new GameObject("Chart", typeof(RectTransform), typeof(RawImage));
        RectTransform chartRect = chartGO.GetComponent<RectTransform>();
        chartRect.SetParent(panel, false);
        chartRect.anchorMin = new Vector2(0f, 0f);
        chartRect.anchorMax = new Vector2(1f, 1f);
        chartRect.offsetMin = new Vector2(6f, 6f);
        chartRect.offsetMax = new Vector2(-6f, -29f);
        chartGO.GetComponent<RawImage>().texture = texture;

        // Legend / current values.
        GameObject legendGO = new GameObject("Legend", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform legendRect = legendGO.GetComponent<RectTransform>();
        legendRect.SetParent(panel, false);
        legendRect.anchorMin = new Vector2(0f, 1f);
        legendRect.anchorMax = new Vector2(1f, 1f);
        legendRect.pivot = new Vector2(0.5f, 1f);
        legendRect.sizeDelta = new Vector2(0f, 24f);
        legendRect.anchoredPosition = new Vector2(0f, -3f);

        legend = legendGO.GetComponent<TextMeshProUGUI>();
        legend.fontSize = 14f;
        legend.alignment = TextAlignmentOptions.Center;
        legend.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        legend.text = "正在等待模拟数据……"; // replaced by live values on the first tick
    }

    bool buildErrorLogged;

    void Update()
    {
        if (!built)
        {
            try
            {
                TryBuild();
            }
            catch (System.Exception e)
            {
                if (!buildErrorLogged)
                {
                    buildErrorLogged = true;
                    Debug.LogError($"BehaviorChartView: build failed — {e}");
                }
                return;
            }
            if (!built) return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            panelRoot.SetActive(isVisible);
        }

        if (!isVisible || spawner == null || spawner.worldState == null) return;

        SimulationStats stats = spawner.worldState.stats;
        if (stats.tickIndex == lastTickIndex) return;
        lastTickIndex = stats.tickIndex;

        PushSample(stats);
        Redraw();
        UpdateLegend(stats);
    }

    void PushSample(SimulationStats stats)
    {
        int alive = Mathf.Max(1, stats.agentsAlive);
        maxPopulationSeen = Mathf.Max(maxPopulationSeen, stats.agentsAlive);

        love[head] = stats.agentsInLove / (float)alive;
        violence[head] = stats.agentsInViolence / (float)alive;
        silence[head] = stats.agentsInSilence / (float)alive;
        population[head] = stats.agentsAlive / (float)maxPopulationSeen;

        head = (head + 1) % Samples;
    }

    void Redraw()
    {
        Color32 bg = new Color32(255, 255, 255, 0);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = bg;

        // Faint mid gridline.
        Color32 grid = new Color32(0, 0, 0, 22);
        int midY = TexHeight / 2;
        for (int x = 0; x < TexWidth; x++)
            pixels[midY * TexWidth + x] = grid;

        PlotSeries(population, populationColor, 90);
        PlotSeries(silence, silenceColor, 255);
        PlotSeries(violence, violenceColor, 255);
        PlotSeries(love, loveColor, 255);

        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    void PlotSeries(float[] series, Color color, byte alpha)
    {
        Color32 c = color;
        c.a = alpha;

        int prevY = -1;

        for (int x = 0; x < TexWidth; x++)
        {
            int sampleIndex = (head + x * Samples / TexWidth) % Samples;
            float value = Mathf.Clamp01(series[sampleIndex]);
            int y = Mathf.Clamp(Mathf.RoundToInt(value * (TexHeight - 3)) + 1, 1, TexHeight - 2);

            if (prevY < 0) prevY = y;

            int yMin = Mathf.Min(y, prevY);
            int yMax = Mathf.Max(y, prevY);
            for (int yy = yMin; yy <= yMax; yy++)
                pixels[yy * TexWidth + x] = c;

            prevY = y;
        }
    }

    void UpdateLegend(SimulationStats stats)
    {
        legend.text =
            $"<color=#{ColorUtility.ToHtmlStringRGB(loveColor)}>爱 {stats.agentsInLove}</color>   " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(violenceColor)}>暴力 {stats.agentsInViolence}</color>   " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(silenceColor)}>沉默 {stats.agentsInSilence}</color>   " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(populationColor)}>人口 {stats.agentsAlive}</color>";
    }
}
