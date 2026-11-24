using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MYLIFE's single-screen interface: a row of folder-style drawers hanging
/// from the top edge. Each drawer slides independently; its tab rides on the
/// drawer's bottom edge like a physical file folder.
///
///   SYSTEM     — pause / restart / quit
///   GUIDE      — how to read the world + how the Rule Book works
///   AGENT      — live inspector for the selected agent (auto-opens on click)
///   RULE BOOK  — the five editable rules + live behavior chart
///
/// Rules are written to the plain RuleBookState, which the simulation reads
/// once per tick. The mouse cursor becomes a crosshair over the world —
/// you aim at an agent to inspect it.
/// </summary>
public class DisplayModeManager : MonoBehaviour
{
    [Header("Style")]
    public Color panelColor = new Color(0.85f, 0.85f, 0.85f, 0.97f);
    public Color tabColor = new Color(0.72f, 0.72f, 0.72f, 0.97f);
    public Color tabOpenColor = new Color(0.85f, 0.85f, 0.85f, 0.97f);
    public Color blankColor = new Color(1f, 1f, 1f, 1f);
    public Color blankFilledColor = new Color(0.95f, 0.92f, 0.55f, 1f);
    public Color textColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    public Color buttonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public float fontSize = 18f;
    public float rowHeight = 32f;

    [Header("Drawer Layout (1920-wide reference)")]
    public float systemWidth = 210f;
    public float guideWidth = 470f;
    public float agentWidth = 300f;
    public float rulebookWidth = 950f; // anchored to the RIGHT edge; slightly overlaps the AGENT drawer so the tab strip never shows a seam
    public float rulesColumnWidth = 560f;
    public float chartWidth = 300f;
    public float tabHeight = 30f;
    public KeyCode rulebookToggleKey = KeyCode.Tab;

    [Header("Onboarding")]
    public string subtitle = "改写这个世界的法则";
    public string firstHint = "← 点击空白处";
    public Color hintPulseColor = new Color(0.98f, 0.85f, 0.35f, 1f);

    [Header("Crosshair Cursor")]
    public bool useCrosshairCursor = true;
    public Color crosshairColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);

    public bool IsSingleScreenMode => true; // legacy accessor kept for the chart

    static readonly string[] IdentityOptions = { "Child", "Friend", "Enemy", "Other" };
    static readonly string[] ConditionOptions = { "Hungry", "Lonely", "Joyful", "Hurt", "Dead" };
    static readonly string[] BehaviorOptions = { "Love", "Violence", "Silence" };
    const string ClearOption = "清除";
    const string EmptyBlank = "—";

    /// <summary>Slot inside the RULE BOOK drawer where BehaviorChartView mounts.</summary>
    public RectTransform ChartMount { get; private set; }

    class Drawer
    {
        public RectTransform panel;
        public Image tabImage;
        public bool open;
        public bool bottom;         // rises from the bottom edge, tab on top
        public float t;             // 0 closed, 1 open
        public float fallbackHeight;
    }

    readonly List<Drawer> drawers = new List<Drawer>();
    Drawer systemDrawer, guideDrawer, agentDrawer, rulebookDrawer, chartDrawer, speakDrawer, eventsDrawer;

    [Header("Bottom Drawers")]
    public float speakPanelWidth = 560f;
    public int maxChatEntries = 40;

    [Header("Title Screen")]
    public string titleText = "法则之后";
    public string beginText = "开始模拟";
    [Tooltip("Tiny backdrop world behind the title screen.")]
    public int introAgentCount = 5;
    public int introFoodCount = 10;

    [Header("Time Controls")]
    [SerializeField, Range(0.1f, 1f)] float normalTimeScale = 1f;

    RectTransform mainUIRoot;
    GameObject titleRoot;
    bool simulationStarted;
    int fullAgentCount, fullFoodCount;
    float resumeTimeScale = 1f;
    readonly List<(float speed, Image circle)> speedButtons = new List<(float, Image)>();

    Canvas uiCanvas;
    RectTransform dropdownPanel;
    GameObject clickCatcher;
    Image firstBlankImage;
    Func<string> firstBlankValue;
    bool anyRuleEverSet;
    int sampleRuleIndex;
    int lastSelectedAgentId = -1;

    readonly List<Action> refreshers = new List<Action>();

    AgentSpawner statsSource;
    AgentSystem agentSystem;
    WorldView worldView;
    TMP_Text pauseLabel;
    GameObject guideWorldBlock, guideRulesBlock;
    ClaudeRuleCompiler compiler;
    TMP_InputField speakInput;

    void Start()
    {
        RuleBookState.Clear();
        statsSource = FindFirstObjectByType<AgentSpawner>();
        agentSystem = FindFirstObjectByType<AgentSystem>();
        worldView = FindFirstObjectByType<WorldView>();
        compiler = gameObject.AddComponent<ClaudeRuleCompiler>();

        BuildCanvas();

        // Everything except the title screen lives under this root so the
        // whole interface can wait behind BEGIN SIMULATION.
        GameObject rootGO = new GameObject("MainUI", typeof(RectTransform));
        mainUIRoot = rootGO.GetComponent<RectTransform>();
        mainUIRoot.SetParent(uiCanvas.transform, false);
        mainUIRoot.anchorMin = Vector2.zero;
        mainUIRoot.anchorMax = Vector2.one;
        mainUIRoot.offsetMin = Vector2.zero;
        mainUIRoot.offsetMax = Vector2.zero;

        // All drawers start closed — the world speaks first.
        float x = 0f;
        systemDrawer = BuildDrawer("系统", x, systemWidth, 148f, BuildSystemContent, startOpen: false, stretchToRightEdge: false); x += systemWidth;
        guideDrawer = BuildDrawer("指南", x, guideWidth, 360f, BuildGuideContent, startOpen: false, stretchToRightEdge: false); x += guideWidth;
        agentDrawer = BuildDrawer("角色", x, agentWidth, 410f, BuildInspectorContent, startOpen: false, stretchToRightEdge: false); x += agentWidth;

        // The Rule Book stretches from the AGENT drawer's right edge to the
        // screen's right edge — seamless tab strip at ANY window aspect.
        rulebookDrawer = BuildDrawer("规则手册", x, 0f, 248f, BuildRulebookContent, startOpen: false, stretchToRightEdge: true);

        // Bottom drawers, mirrored: tabs peek at the screen's bottom edge.
        chartDrawer = BuildDrawer("图表", 0f, chartWidth + 28f, 240f, BuildChartContent,
            startOpen: false, stretchToRightEdge: false, bottom: true);
        speakDrawer = BuildDrawer("对话", chartWidth + 28f, speakPanelWidth, 250f, BuildChatContent,
            startOpen: false, stretchToRightEdge: false, bottom: true);

        eventsDrawer = BuildDrawer("世界事件", chartWidth + 28f + speakPanelWidth, 430f, 350f, BuildEventsContent,
            startOpen: false, stretchToRightEdge: false, bottom: true);
        BuildSpeedControls();
        BuildTitleScreen();
        SetupIntroWorld();

        mainUIRoot.gameObject.SetActive(false);

        if (useCrosshairCursor)
            ApplyCrosshairCursor();
    }

    // ------------------------------------------------------ title screen

    void SetupIntroWorld()
    {
        if (statsSource == null) return;

        // Remember the real world size, then shrink to a quiet backdrop:
        // a few trees, a few wanderers.
        fullAgentCount = statsSource.initialCount;
        fullFoodCount = statsSource.initialFoodCount;

        statsSource.initialCount = introAgentCount;
        statsSource.initialFoodCount = introFoodCount;
        statsSource.ResetWorld();

        if (agentSystem != null) agentSystem.ResetRuntimeState();
        if (worldView != null) worldView.ResetView();
    }

    void BuildTitleScreen()
    {
        titleRoot = new GameObject("TitleScreen", typeof(RectTransform));
        RectTransform root = titleRoot.GetComponent<RectTransform>();
        root.SetParent(uiCanvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // Just words over the living world — no backdrop.
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.SetParent(root, false);
        titleRect.anchorMin = new Vector2(0.5f, 0.58f);
        titleRect.anchorMax = new Vector2(0.5f, 0.58f);
        titleRect.sizeDelta = new Vector2(1200f, 130f);

        TextMeshProUGUI title = titleGO.GetComponent<TextMeshProUGUI>();
        title.text = titleText;
        title.fontSize = 96f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 18f;
        title.color = textColor;
        title.alignment = TextAlignmentOptions.Center;

        AddTitleLabel(root, 0.50f, 800f, 36f, "After the Law", 24f, FontStyles.Normal, 0.85f);

        GameObject beginGO = new GameObject("Begin", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Button));
        RectTransform beginRect = beginGO.GetComponent<RectTransform>();
        beginRect.SetParent(root, false);
        beginRect.anchorMin = new Vector2(0.5f, 0.42f);
        beginRect.anchorMax = new Vector2(0.5f, 0.42f);
        beginRect.sizeDelta = new Vector2(600f, 60f);

        TextMeshProUGUI begin = beginGO.GetComponent<TextMeshProUGUI>();
        begin.text = beginText;
        begin.fontSize = 28f;
        begin.characterSpacing = 8f;
        begin.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        begin.alignment = TextAlignmentOptions.Center;

        beginGO.GetComponent<Button>().onClick.AddListener(BeginSimulation);

        // Gentle breathing on the begin text.
        StartCoroutine(BreatheBeginText(begin));

        BuildTitleApiSection(root);
        BuildTitleCredits(root);
    }

    void BuildTitleApiSection(RectTransform root)
    {
        AddTitleLabel(root, 0.30f, 560f, 28f,
            "可选：对话和角色故事需要 DeepSeek API 密钥",
            15f, FontStyles.Normal, 0.72f);

        // Key input (masked) + save button.
        GameObject rowGO = new GameObject("ApiRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rowRect = rowGO.GetComponent<RectTransform>();
        rowRect.SetParent(root, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.255f);
        rowRect.anchorMax = new Vector2(0.5f, 0.255f);

        HorizontalLayoutGroup h = rowGO.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        ContentSizeFitter fit = rowGO.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject inputGO = new GameObject("KeyInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        inputGO.transform.SetParent(rowRect, false);
        inputGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);

        LayoutElement inputLE = inputGO.GetComponent<LayoutElement>();
        inputLE.minWidth = 380f;
        inputLE.preferredWidth = 380f;
        inputLE.minHeight = rowHeight;

        GameObject viewportGO = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        viewport.SetParent(inputGO.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 2f);
        viewport.offsetMax = new Vector2(-8f, -2f);

        TextMeshProUGUI placeholder = CreateInputText(viewport,
            ClaudeRuleCompiler.HasStoredKey ? "此设备已保存密钥" : "粘贴你的 DeepSeek API 密钥");
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.68f);

        TextMeshProUGUI inputText = CreateInputText(viewport, "");

        TMP_InputField keyInput = inputGO.GetComponent<TMP_InputField>();
        keyInput.textViewport = viewport;
        keyInput.textComponent = inputText;
        keyInput.placeholder = placeholder;
        keyInput.lineType = TMP_InputField.LineType.SingleLine;
        keyInput.contentType = TMP_InputField.ContentType.Password; // never shown on screen

        TMP_Text statusLabel = null;

        AddPanelButton(rowRect, "保存", 70f, () =>
        {
            string status = ClaudeRuleCompiler.SaveUserKey(keyInput.text);
            keyInput.text = "";
            if (statusLabel != null) statusLabel.text = status;
            placeholder.text = ClaudeRuleCompiler.HasStoredKey ? "此设备已保存密钥" : "粘贴你的 DeepSeek API 密钥";
        });

        statusLabel = AddTitleLabel(root, 0.215f, 560f, 24f, "", 14f, FontStyles.Italic, 0.6f);

        AddTitleLabel(root, 0.175f, 760f, 46f,
            "密钥保存在此设备上，用于验证发往 api.deepseek.com 的 HTTPS 请求。\n" +
            "API 用量由你的 DeepSeek 账户计费。",
            13f, FontStyles.Italic, 0.6f);
    }

    void BuildTitleCredits(RectTransform root)
    {
        AddTitleLabel(root, 0.075f, 900f, 62f,
            "作者：<b>Yichuan (River) Wang</b>\n" +
            "yichuan.wang.work@gmail.com   ·   yichuanwangriver.com\n" +
            "© 2026 Yichuan Wang 版权所有，按 MIT 许可证开源",
            13f, FontStyles.Normal, 0.65f);
    }

    TMP_Text AddTitleLabel(RectTransform root, float anchorY, float width, float height,
                           string text, float size, FontStyles style, float alpha)
    {
        GameObject go = new GameObject("TitleLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    System.Collections.IEnumerator BreatheBeginText(TextMeshProUGUI begin)
    {
        while (begin != null && titleRoot != null)
        {
            float a = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 2f);
            begin.color = new Color(textColor.r, textColor.g, textColor.b, a);
            yield return null;
        }
    }

    void BeginSimulation()
    {
        if (simulationStarted) return;
        simulationStarted = true;
        RuleBookState.Clear();

        if (statsSource != null)
        {
            statsSource.initialCount = fullAgentCount;
            statsSource.initialFoodCount = fullFoodCount;
            statsSource.ResetWorld();
        }

        if (agentSystem != null) agentSystem.ResetRuntimeState();
        if (worldView != null) worldView.ResetView();
        SetSimulationSpeed(normalTimeScale);

        Destroy(titleRoot);
        titleRoot = null;
        mainUIRoot.gameObject.SetActive(true);
    }

    // ---------------------------------------------------- speed controls

    void BuildSpeedControls()
    {
        GameObject column = new GameObject("SpeedControls", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rect = column.GetComponent<RectTransform>();
        rect.SetParent(mainUIRoot, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(12f, 0f);

        VerticalLayoutGroup v = column.GetComponent<VerticalLayoutGroup>();
        v.spacing = 10f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;

        ContentSizeFitter fit = column.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddSpeedButton(rect, "II", 0f);
        AddSpeedButton(rect, "1×", normalTimeScale);
        AddSpeedButton(rect, "2×", normalTimeScale * 2f);
        AddSpeedButton(rect, "4×", normalTimeScale * 4f);
    }

    void AddSpeedButton(RectTransform parent, string label, float speed)
    {
        GameObject go = new GameObject("Speed" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        Image circle = go.GetComponent<Image>();
        circle.sprite = GetCircleSprite();
        circle.type = Image.Type.Simple;
        circle.color = tabColor;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minWidth = 44f;
        le.minHeight = 44f;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.SetParent(go.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 17f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;

        go.GetComponent<Button>().onClick.AddListener(() => SetSimulationSpeed(speed));
        speedButtons.Add((speed, circle));
    }

    void SetSimulationSpeed(float speed)
    {
        if (speed > 0f)
            resumeTimeScale = speed;

        Time.timeScale = speed;
    }

    Sprite circleSprite;

    Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 48;
        float r = size / 2f - 1f;
        Vector2 c = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
            for (int x2 = 0; x2 < size; x2++)
            {
                float dist = Vector2.Distance(new Vector2(x2, y), c);
                byte alpha = dist <= r ? (byte)255 : dist <= r + 1.5f ? (byte)Mathf.Clamp((int)((r + 1.5f - dist) / 1.5f * 255), 0, 255) : (byte)0;
                pixels[y * size + x2] = new Color32(255, 255, 255, alpha);
            }

        tex.SetPixels32(pixels);
        tex.Apply(false);
        tex.filterMode = FilterMode.Bilinear;

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    void Update()
    {
        // Don't steal Tab while the user is typing a wish.
        bool typing = speakInput != null && speakInput.isFocused;
        if (!typing && Input.GetKeyDown(rulebookToggleKey))
            ToggleDrawer(rulebookDrawer);

        foreach (var d in drawers)
        {
            float target = d.open ? 1f : 0f;
            d.t = Mathf.MoveTowards(d.t, target, Time.unscaledDeltaTime * 6f);
            float eased = 1f - (1f - d.t) * (1f - d.t);
            float height = d.panel.rect.height > 1f ? d.panel.rect.height : d.fallbackHeight;
            float hiddenY = d.bottom ? -height : height; // top drawers slide up, bottom drawers slide down
            d.panel.anchoredPosition = new Vector2(d.panel.anchoredPosition.x, Mathf.Lerp(hiddenY, 0f, eased));

            Color target_c = d.open ? tabOpenColor : tabColor;
            if (d.tabImage != null && d.tabImage.color != target_c)
                d.tabImage.color = Color.Lerp(d.tabImage.color, target_c, Time.unscaledDeltaTime * 10f);
        }

        for (int i = 0; i < refreshers.Count; i++)
            refreshers[i]();

        UpdateFirstRunHint();
        if (simulationStarted)
            PollAgentSelection();

        // Highlight the speed button matching the current time scale.
        for (int i = 0; i < speedButtons.Count; i++)
        {
            bool active = Mathf.Approximately(Time.timeScale, speedButtons[i].speed);
            Color want = active ? blankFilledColor : tabColor;
            if (speedButtons[i].circle.color != want)
                speedButtons[i].circle.color = want;
        }

        // Snap chat history to the newest entry after layout has rebuilt.
        if (chatScrollPending && chatScroll != null)
        {
            chatScrollPending = false;
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    void ToggleDrawer(Drawer d)
    {
        d.open = !d.open;
        CloseDropdown();
    }

    void PollAgentSelection()
    {
        if (worldView == null) return;
        if (worldView.SelectedAgentId == lastSelectedAgentId) return;

        lastSelectedAgentId = worldView.SelectedAgentId;
        HideAgentStory(); // a new agent means a new story

        if (lastSelectedAgentId >= 0 && !agentDrawer.open)
            agentDrawer.open = true; // aim, click, inspect
    }

    // ---------------------------------------------------------- first-run

    void UpdateFirstRunHint()
    {
        if (anyRuleEverSet || firstBlankImage == null) return;

        if (AnyRuleSet())
        {
            anyRuleEverSet = true;
            if (string.IsNullOrEmpty(firstBlankValue()))
                firstBlankImage.color = blankColor;
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
        firstBlankImage.color = Color.Lerp(blankColor, hintPulseColor, pulse * 0.6f);
    }

    static bool AnyRuleSet()
    {
        return RuleBookState.rule1A != "" || RuleBookState.rule1B != "" ||
               RuleBookState.rule2A != "" || RuleBookState.rule2B != "" ||
               RuleBookState.becomesFrom != "" || RuleBookState.becomesTo != "" ||
               RuleBookState.spreadsCondition != "" ||
               RuleBookState.rule5Identity != "" || RuleBookState.rule5Condition != "" ||
               RuleBookState.rule5Behavior != "";
    }

    // ------------------------------------------------------------ canvas

    void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("DrawerCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        uiCanvas = canvasGO.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.targetDisplay = 0;
        uiCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    // ------------------------------------------------------------ drawers

    Drawer BuildDrawer(string label, float x, float width, float fallbackHeight,
                       Action<RectTransform> buildContent, bool startOpen, bool stretchToRightEdge,
                       bool bottom = false)
    {
        var d = new Drawer { fallbackHeight = fallbackHeight, open = startOpen, t = startOpen ? 1f : 0f, bottom = bottom };

        // Panel hangs from the top edge (or rises from the bottom) at its x offset.
        GameObject panelGO = new GameObject(label + "Panel",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        d.panel = panelGO.GetComponent<RectTransform>();
        d.panel.SetParent(mainUIRoot, false);

        float edgeY = bottom ? 0f : 1f;

        if (stretchToRightEdge)
        {
            // Left edge fixed at x, right edge glued to the screen's right —
            // width adapts to the window, so no seam and no overflow, ever.
            d.panel.anchorMin = new Vector2(0f, edgeY);
            d.panel.anchorMax = new Vector2(1f, edgeY);
            d.panel.pivot = new Vector2(0f, edgeY);
            d.panel.offsetMin = new Vector2(x, 0f);
            d.panel.offsetMax = new Vector2(0f, 0f);
        }
        else
        {
            d.panel.anchorMin = new Vector2(0f, edgeY);
            d.panel.anchorMax = new Vector2(0f, edgeY);
            d.panel.pivot = new Vector2(0f, edgeY);
            d.panel.anchoredPosition = new Vector2(x, 0f);
            d.panel.sizeDelta = new Vector2(width, 0f);
        }

        panelGO.GetComponent<Image>().color = panelColor;

        VerticalLayoutGroup v = panelGO.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 8, 8);
        v.spacing = 3f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = false;
        v.childControlHeight = false;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;

        panelGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        buildContent(d.panel);

        // Tab: a child of the panel, hanging below its bottom-left corner so it
        // rides along as the drawer slides (an upside-down file folder: tab on
        // the bottom edge, still peeking at the screen top when closed).
        // ignoreLayout is essential — without it the panel's VerticalLayoutGroup
        // would swallow the tab into the content flow.
        GameObject tabGO = new GameObject(label + "Tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        tabGO.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform tabRect = tabGO.GetComponent<RectTransform>();
        tabRect.SetParent(d.panel, false);

        // Full panel width: closed drawers read as one continuous strip.
        // Top drawers: tab hangs BELOW the panel; bottom drawers: tab sits
        // ABOVE it — either way it peeks at the screen edge when closed.
        float tabEdgeY = bottom ? 1f : 0f;
        tabRect.anchorMin = new Vector2(0f, tabEdgeY);
        tabRect.anchorMax = new Vector2(1f, tabEdgeY);
        tabRect.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
        tabRect.anchoredPosition = new Vector2(0f, 0f);
        tabRect.sizeDelta = new Vector2(0f, tabHeight);

        d.tabImage = tabGO.GetComponent<Image>();
        d.tabImage.sprite = GetTabSprite(roundedTop: bottom); // corners away from the panel
        d.tabImage.type = Image.Type.Sliced;
        d.tabImage.color = startOpen ? tabOpenColor : tabColor;
        tabGO.GetComponent<Button>().onClick.AddListener(() => ToggleDrawer(d));

        GameObject tabLabelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform tabLabelRect = tabLabelGO.GetComponent<RectTransform>();
        tabLabelRect.SetParent(tabRect, false);
        tabLabelRect.anchorMin = Vector2.zero;
        tabLabelRect.anchorMax = Vector2.one;
        tabLabelRect.offsetMin = Vector2.zero;
        tabLabelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tabText = tabLabelGO.GetComponent<TextMeshProUGUI>();
        tabText.text = label;
        tabText.fontSize = fontSize;
        tabText.fontStyle = FontStyles.Bold;
        tabText.color = textColor;
        tabText.alignment = TextAlignmentOptions.Center;

        drawers.Add(d);
        return d;
    }

    // --------------------------------------------------- SYSTEM drawer

    void BuildSystemContent(RectTransform parent)
    {
        pauseLabel = AddPanelButton(parent, "暂停", 170f,
            () => SetSimulationSpeed(Time.timeScale == 0f ? resumeTimeScale : 0f));

        // The circular speed buttons and the system drawer share the same speed.
        refreshers.Add(() =>
        {
            string want = Time.timeScale == 0f ? "继续" : "暂停";
            if (pauseLabel != null && pauseLabel.text != want)
                pauseLabel.text = want;
        });

        AddPanelButton(parent, "重启世界", 170f, () =>
        {
            SetSimulationSpeed(normalTimeScale);

            if (agentSystem != null) agentSystem.ResetRuntimeState();
            if (statsSource != null) statsSource.ResetWorld();
            if (worldView != null) worldView.ResetView();
        });

        AddPanelButton(parent, "返回标题", 170f, QuitToTitle);

        AddPanelButton(parent, "退出游戏", 170f, () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    void QuitToTitle()
    {
        simulationStarted = false;
        Time.timeScale = 1f;

        foreach (var d in drawers)
        {
            d.open = false;
            d.t = 0f;
        }

        HideAgentStory();
        CloseDropdown();
        lastSelectedAgentId = -1;

        mainUIRoot.gameObject.SetActive(false);
        SetupIntroWorld();
        BuildTitleScreen();
    }

    TMP_Text AddPanelButton(RectTransform parent, string label, float width, Action onClick)
    {
        GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = buttonColor;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minWidth = width;
        le.minHeight = rowHeight;
        ((RectTransform)go.transform).sizeDelta = new Vector2(width, rowHeight);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.SetParent(go.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize * 0.8f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
        return tmp;
    }

    // ---------------------------------------------------- GUIDE drawer

    void BuildGuideContent(RectTransform parent)
    {
        // Sub-tabs: WORLD | RULES
        GameObject row = new GameObject("SubTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.SetParent(parent, false);

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        ContentSizeFitter fit = row.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddPanelButton(rowRect, "认识世界", 110f, () => ShowGuide(true));
        AddPanelButton(rowRect, "了解规则", 110f, () => ShowGuide(false));

        guideWorldBlock = BuildGuideBlock(parent, new[]
        {
            "<b>如何观察这个世界</b>",
            "每个小人都是独立行动的角色。",
            "<b>G2-14</b> 表示第 2 代、世界中出生的第 14 个角色。",
            "角色会漫游、从树木获取食物、交友、相爱、争斗和死亡。",
            "<b>房屋</b>标记着孩子出生的地方。",
            "<b>十字架</b>是墓碑，死者仍会被记住。",
            "有些规则会让他们继续影响活着的角色。",
            "<b>尖塔</b>是由极度孤独的角色建起的教堂。",
            "教堂会吸引其他孤独的角色。",
            "喜悦、孤独、受伤和相爱都会改变角色的外形。",
            "<i>将准星对准角色并点击，</i>",
            "<i>就能在“角色”面板查看它的状态。</i>",
        });

        guideRulesBlock = BuildGuideBlock(parent, new[]
        {
            "<b>如何使用规则手册</b>",
            "规则手册里的五句话是这个社会的法则。",
            "填上空白处，规则就会立即生效。",
            "你改变的是角色生活的条件。",
            "身份取决于角色的<i>主观认知</i>。",
            "同一个人可能是某人的朋友，也是另一个人的敌人。",
            "<color=#33BF66>——</color>  两个角色相互靠近（规则 1）",
            "<color=#E64033>——</color>  两个角色相互远离（规则 2）",
            "<color=#8C59E6>◯</color>  一种状态传播给此角色（规则 4）",
            "<color=#FF9926>◯</color>  此角色的行为倾向增强（规则 5）",
            "每条规则旁的数字显示它影响了多少角色。",
            "<i>图表记录爱、暴力、沉默和人口。</i>",
            "<i>修改规则后，可以观察曲线如何变化。</i>",
        });

        ShowGuide(true);
    }

    GameObject BuildGuideBlock(RectTransform parent, string[] lines)
    {
        GameObject block = new GameObject("GuideBlock", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rect = block.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        VerticalLayoutGroup v = block.GetComponent<VerticalLayoutGroup>();
        v.spacing = 1f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;

        ContentSizeFitter fit = block.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (string line in lines)
        {
            GameObject go = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(rect, false);

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = line;
            tmp.fontSize = fontSize * 0.78f;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            go.GetComponent<LayoutElement>().minHeight = rowHeight * 0.62f;
            go.GetComponent<LayoutElement>().minWidth = 420f;
        }

        return block;
    }

    void ShowGuide(bool world)
    {
        if (guideWorldBlock != null) guideWorldBlock.SetActive(world);
        if (guideRulesBlock != null) guideRulesBlock.SetActive(!world);
    }

    // ------------------------------------------------ AGENT inspector

    void BuildInspectorContent(RectTransform parent)
    {
        GameObject go = new GameObject("AgentInfo", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        // The drawer layout doesn't control child sizes — size the text rect
        // explicitly or it stays at the 100×100 default and the info overflows.
        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(agentWidth - 28f, 286f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize * 0.85f;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.text = "请选择一个角色";

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minWidth = agentWidth - 28f;
        le.minHeight = 286f;
        le.preferredHeight = 286f;

        // WorldView keeps writing the inspector text; point it at this drawer
        // and retire the old top-left overlay text.
        if (worldView != null)
        {
            if (worldView.agentInfoText != null)
                worldView.agentInfoText.gameObject.SetActive(false);

            worldView.agentInfoText = tmp;
        }

        // DETAIL: ask the agent for its own story, in its own voice.
        AddPanelButton(parent, "角色故事", 170f, RequestAgentStory);

        // Story lives in a fixed-height scroll view (wheel + right scrollbar)
        // so long stories can't spill past the drawer edge.
        float storyWidth = agentWidth - 28f;
        GameObject scrollGO = new GameObject("StoryScroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        scrollGO.transform.SetParent(parent, false);
        ((RectTransform)scrollGO.transform).sizeDelta = new Vector2(storyWidth, 216f);

        LayoutElement scrollLE = scrollGO.GetComponent<LayoutElement>();
        scrollLE.minWidth = storyWidth;
        scrollLE.minHeight = 216f;

        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        viewport.SetParent(scrollGO.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(-12f, 0f); // leave room for the scrollbar
        viewportGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);

        GameObject textGO = new GameObject("StoryText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.SetParent(viewport, false);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        agentStoryText = textGO.GetComponent<TextMeshProUGUI>();
        agentStoryText.fontSize = fontSize * 0.78f;
        agentStoryText.fontStyle = FontStyles.Italic;
        agentStoryText.color = new Color(textColor.r, textColor.g, textColor.b, 0.85f);
        agentStoryText.alignment = TextAlignmentOptions.TopLeft;

        // Scrollbar (right side).
        GameObject sbGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform sbRect = sbGO.GetComponent<RectTransform>();
        sbRect.SetParent(scrollGO.transform, false);
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.sizeDelta = new Vector2(8f, 0f);
        sbGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

        GameObject handleGO2 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        RectTransform handleRect = handleGO2.GetComponent<RectTransform>();
        handleRect.SetParent(sbRect, false);
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        handleGO2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);

        Scrollbar sb = sbGO.GetComponent<Scrollbar>();
        sb.handleRect = handleRect;
        sb.direction = Scrollbar.Direction.BottomToTop;

        agentStoryScroll = scrollGO.GetComponent<ScrollRect>();
        agentStoryScroll.viewport = viewport;
        agentStoryScroll.content = textRect;
        agentStoryScroll.horizontal = false;
        agentStoryScroll.vertical = true;
        agentStoryScroll.movementType = ScrollRect.MovementType.Clamped;
        agentStoryScroll.scrollSensitivity = 16f;
        agentStoryScroll.verticalScrollbar = sb;

        agentStoryRoot = scrollGO;
        scrollGO.SetActive(false);
    }

    TMP_Text agentStoryText;
    ScrollRect agentStoryScroll;
    GameObject agentStoryRoot;

    void RequestAgentStory()
    {
        if (compiler == null || worldView == null || statsSource == null) return;
        if (worldView.SelectedAgentId < 0) return;
        if (!statsSource.worldState.agentsById.TryGetValue(worldView.SelectedAgentId, out AgentData agent)) return;

        ShowAgentStory("<i>正在回忆……</i>");

        int askedFor = agent.id;
        compiler.TellAgentStory(BuildAgentContext(agent), (success, text) =>
        {
            // The user may have clicked another agent while the story was being
            // written — don't put one agent's memories in another's mouth.
            if (worldView.SelectedAgentId != askedFor) return;
            ShowAgentStory(text);
        });
    }

    void ShowAgentStory(string text)
    {
        if (agentStoryRoot == null) return;
        agentStoryRoot.SetActive(true);
        agentStoryText.text = text;

        // Size the content to the wrapped text and start at the top.
        float width = agentWidth - 28f - 12f;
        float height = agentStoryText.GetPreferredValues(text, width, 0f).y + 8f;
        ((RectTransform)agentStoryText.transform).sizeDelta = new Vector2(0f, height);
        agentStoryScroll.verticalNormalizedPosition = 1f;
    }

    void HideAgentStory()
    {
        if (agentStoryRoot != null)
            agentStoryRoot.SetActive(false);
    }

    /// <summary>Everything true about this agent right now, for the story.</summary>
    string BuildAgentContext(AgentData agent)
    {
        var world = statsSource.worldState;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"you are agent {agent.id}, generation {agent.generation}.");
        sb.AppendLine(agent.alive
            ? $"age {agent.age:0} of a lifespan of {agent.maxAge:0} — {(agent.age / Mathf.Max(1f, agent.maxAge)):P0} of your life is behind you."
            : "you are dead; you speak from beneath a small cross.");
        sb.AppendLine($"health {agent.hp:0.00}, food {agent.food:0.00} of {agent.maxFood:0.00}.");
        sb.AppendLine($"feelings now (0..1): hungry {agent.hungry:0.00}, lonely {agent.lonely:0.00}, joyful {agent.joyful:0.00}, hurt {agent.hurt:0.00}.");
        sb.AppendLine($"current behavior: {agent.currentBehavior}.");

        int parents = 0, children = 0, deadKin = 0;
        foreach (var pair in agent.kinshipRelations)
        {
            bool kinAlive = world.agentsById.TryGetValue(pair.Key, out AgentData kin) && kin.alive;
            if (!kinAlive) deadKin++;
            if (pair.Value == KinshipType.Parent) parents++;
            else if (pair.Value == KinshipType.Child) children++;
        }

        int friends = 0, enemies = 0;
        foreach (var pair in agent.socialRelations)
        {
            if (pair.Value == SocialRelationType.Friend) friends++;
            else if (pair.Value == SocialRelationType.Enemy) enemies++;
        }

        sb.AppendLine($"kin: {parents} parents, {children} children ({deadKin} of your kin are dead).");
        sb.AppendLine($"bonds as you perceive them: {friends} friends, {enemies} enemies.");
        sb.AppendLine($"the laws of your world right now: {ClaudeRuleCompiler.DescribeActiveRules()}.");
        sb.AppendLine($"around you: {world.stats.agentsAlive} live in this world; {world.deathMarkers.Count} graves; {world.churches.Count} churches.");

        return sb.ToString();
    }

    // ---------------------------------------------- RULE BOOK drawer

    void BuildRulebookContent(RectTransform parent)
    {
        // Left: header + five sentences, in the normal layout flow.
        // Right: the chart is anchored directly to the panel's top-right corner
        // (outside the layout flow) so it can never be pushed off-screen.
        GameObject rulesGO = new GameObject("Rules", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        RectTransform rulesColumn = rulesGO.GetComponent<RectTransform>();
        rulesColumn.SetParent(parent, false);
        rulesColumn.sizeDelta = new Vector2(rulesColumnWidth, 0f);

        LayoutElement rulesLE = rulesGO.GetComponent<LayoutElement>();
        rulesLE.minWidth = rulesColumnWidth;
        rulesLE.preferredWidth = rulesColumnWidth;

        VerticalLayoutGroup v = rulesGO.GetComponent<VerticalLayoutGroup>();
        v.spacing = 3f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = false;
        v.childControlHeight = false;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;

        rulesGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildRulebookHeader(rulesColumn);

        BuildSentence(rulesColumn, "1", new SentencePart[]
        {
            Blank(() => RuleBookState.rule1A, v2 => RuleBookState.rule1A = v2, IdentityOptions),
            Text("与"),
            Blank(() => RuleBookState.rule1B, v2 => RuleBookState.rule1B = v2, IdentityOptions),
            Text("相互靠近"),
        }, s => s.rule1PairsMatched > 0 ? $"{s.rule1PairsMatched} 对"
              : (!anyRuleEverSet ? firstHint : ""));

        BuildSentence(rulesColumn, "2", new SentencePart[]
        {
            Blank(() => RuleBookState.rule2A, v2 => RuleBookState.rule2A = v2, IdentityOptions),
            Text("与"),
            Blank(() => RuleBookState.rule2B, v2 => RuleBookState.rule2B = v2, IdentityOptions),
            Text("相互远离"),
        }, s => s.rule2PairsMatched > 0 ? $"{s.rule2PairsMatched} 对" : "");

        BuildSentence(rulesColumn, "3", new SentencePart[]
        {
            Blank(() => RuleBookState.becomesFrom, v2 => RuleBookState.becomesFrom = v2, IdentityOptions),
            Text("被视为"),
            Blank(() => RuleBookState.becomesTo, v2 => RuleBookState.becomesTo = v2, IdentityOptions),
        }, null);

        BuildSentence(rulesColumn, "4", new SentencePart[]
        {
            Blank(() => RuleBookState.spreadsCondition, v2 => RuleBookState.spreadsCondition = v2, ConditionOptions),
            Text("会传播"),
        }, s => s.rule4AgentsAffected > 0 ? $"{s.rule4AgentsAffected} 人" : "");

        BuildSentence(rulesColumn, "5", new SentencePart[]
        {
            Text("当"),
            Blank(() => RuleBookState.rule5Identity, v2 => RuleBookState.rule5Identity = v2, IdentityOptions),
            Text("处于"),
            Blank(() => RuleBookState.rule5Condition, v2 => RuleBookState.rule5Condition = v2, ConditionOptions),
            Text("时，"),
            Blank(() => RuleBookState.rule5Behavior, v2 => RuleBookState.rule5Behavior = v2, BehaviorOptions),
            Text("增强"),
        }, s => s.rule5AgentsAffected > 0 ? $"{s.rule5AgentsAffected} 人" : "");
    }

    // ------------------------------------------------- CHART drawer (bottom)

    void BuildChartContent(RectTransform parent)
    {
        GameObject mountGO = new GameObject("ChartMount", typeof(RectTransform), typeof(LayoutElement));
        mountGO.transform.SetParent(parent, false);

        ChartMount = mountGO.GetComponent<RectTransform>();
        ChartMount.sizeDelta = new Vector2(chartWidth, 216f);

        LayoutElement le = mountGO.GetComponent<LayoutElement>();
        le.minWidth = chartWidth;
        le.minHeight = 216f;
    }

    // ------------------------------- SPEAK drawer (bottom): chat with history

    RectTransform chatContent;
    ScrollRect chatScroll;
    TMP_Text pendingWorldEntry;
    bool chatScrollPending;

    void BuildChatContent(RectTransform parent)
    {
        // History: a scroll view of past exchanges.
        GameObject scrollGO = new GameObject("History", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        scrollGO.transform.SetParent(parent, false);
        ((RectTransform)scrollGO.transform).sizeDelta = new Vector2(speakPanelWidth - 28f, 184f);

        LayoutElement scrollLE = scrollGO.GetComponent<LayoutElement>();
        scrollLE.minWidth = speakPanelWidth - 28f;
        scrollLE.minHeight = 184f;

        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        viewport.SetParent(scrollGO.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewportGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f); // raycast target for wheel scroll

        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        chatContent = contentGO.GetComponent<RectTransform>();
        chatContent.SetParent(viewport, false);
        chatContent.anchorMin = new Vector2(0f, 1f);
        chatContent.anchorMax = new Vector2(1f, 1f);
        chatContent.pivot = new Vector2(0.5f, 1f);
        chatContent.offsetMin = Vector2.zero;
        chatContent.offsetMax = Vector2.zero;

        VerticalLayoutGroup v = contentGO.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        chatScroll = scrollGO.GetComponent<ScrollRect>();
        chatScroll.viewport = viewport;
        chatScroll.content = chatContent;
        chatScroll.horizontal = false;
        chatScroll.vertical = true;
        chatScroll.movementType = ScrollRect.MovementType.Clamped;
        chatScroll.scrollSensitivity = 18f;

        AddChatEntry("你可以询问世界的现状，也可以告诉它你希望改变什么。", 0.78f);

        // Input row below the history.
        BuildSpeakRow(parent);
    }

    void BuildEventsContent(RectTransform parent)
    {
        var panel = parent.gameObject.AddComponent<WorldEventPanel>();
        panel.Build(parent, 402f, fontSize, textColor);

        refreshers.Add(() =>
        {
            if (eventsDrawer.open && statsSource != null)
                panel.Refresh(statsSource.worldState.EventLog);
        });
    }

    void AddChatEntry(string richText, float alpha)
    {
        if (chatContent == null) return;

        GameObject go = new GameObject("Entry", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(chatContent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = richText;
        tmp.fontSize = fontSize;
        tmp.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        tmp.alignment = TextAlignmentOptions.TopLeft;

        pendingWorldEntry = tmp; // most recent entry (used to fill in replies)

        while (chatContent.childCount > maxChatEntries)
            DestroyImmediate(chatContent.GetChild(0).gameObject);

        chatScrollPending = true;
    }

    void BuildSpeakRow(RectTransform parent)
    {
        GameObject row = new GameObject("SpeakRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.SetParent(parent, false);

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        ContentSizeFitter fit = row.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Input field (TMP_InputField built by hand: bg → viewport → text + placeholder).
        GameObject inputGO = new GameObject("WishInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        inputGO.transform.SetParent(rowRect, false);
        inputGO.GetComponent<Image>().color = blankColor;

        LayoutElement inputLE = inputGO.GetComponent<LayoutElement>();
        inputLE.minWidth = speakPanelWidth - 28f - 88f; // panel minus padding & button
        inputLE.preferredWidth = speakPanelWidth - 28f - 88f;
        inputLE.minHeight = rowHeight - 2f;

        GameObject viewportGO = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = viewportGO.GetComponent<RectTransform>();
        viewport.SetParent(inputGO.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 2f);
        viewport.offsetMax = new Vector2(-8f, -2f);

        TextMeshProUGUI placeholder = CreateInputText(viewport, "和世界对话，例如：让孤独可以传播");
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.68f);

        TextMeshProUGUI text = CreateInputText(viewport, "");

        speakInput = inputGO.GetComponent<TMP_InputField>();
        speakInput.textViewport = viewport;
        speakInput.textComponent = text;
        speakInput.placeholder = placeholder;
        speakInput.lineType = TMP_InputField.LineType.SingleLine;
        speakInput.onSubmit.AddListener(_ => SubmitWish());

        AddPanelButton(rowRect, "对话", 80f, SubmitWish);
    }

    TextMeshProUGUI CreateInputText(RectTransform viewport, string content)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(viewport, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize * 0.85f;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    void SubmitWish()
    {
        if (compiler == null || speakInput == null) return;

        string wish = speakInput.text != null ? speakInput.text.Trim() : "";
        if (wish.Length == 0 || compiler.IsBusy) return;

        AddChatEntry($"<b>你</b>：{wish}", 0.8f);
        AddChatEntry("<i>世界正在聆听……</i>", 0.45f);
        TMP_Text replyEntry = pendingWorldEntry;

        speakInput.text = "";
        speakInput.ActivateInputField(); // keep the keyboard flow going

        compiler.Compile(wish, (success, message) =>
        {
            if (replyEntry != null)
            {
                replyEntry.text = $"<b>世界</b>：{message}";
                replyEntry.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
            }
            chatScrollPending = true;
        });
    }

    void BuildRulebookHeader(RectTransform parent)
    {
        GameObject row = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.SetParent(parent, false);

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        ContentSizeFitter fit = row.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject go = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(rowRect, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = subtitle;
        tmp.fontSize = fontSize * 0.85f;
        tmp.fontStyle = FontStyles.Italic;
        tmp.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        go.GetComponent<LayoutElement>().minHeight = rowHeight;

        AddPanelButton(rowRect, "试用规则", 110f, ApplySampleRule);
    }

    /// <summary>Curated sample rules with dramatic, quickly visible effects.</summary>
    void ApplySampleRule()
    {
        switch (sampleRuleIndex % 5)
        {
            case 0: RuleBookState.rule1A = "Enemy"; RuleBookState.rule1B = "Friend"; break;
            case 1: RuleBookState.spreadsCondition = "Lonely"; break;
            case 2: RuleBookState.rule5Identity = "Friend"; RuleBookState.rule5Condition = "Hungry"; RuleBookState.rule5Behavior = "Violence"; break;
            case 3: RuleBookState.becomesFrom = "Enemy"; RuleBookState.becomesTo = "Friend"; break;
            case 4: RuleBookState.rule2A = "Friend"; RuleBookState.rule2B = "Friend"; break;
        }

        sampleRuleIndex++;
    }

    // -------------------------------------------------------- sentences

    struct SentencePart
    {
        public bool isBlank;
        public string text;
        public Func<string> getValue;
        public Action<string> setValue;
        public string[] options;
    }

    static SentencePart Text(string t) => new SentencePart { isBlank = false, text = t };

    static SentencePart Blank(Func<string> getValue, Action<string> setValue, string[] options) =>
        new SentencePart { isBlank = true, getValue = getValue, setValue = setValue, options = options };

    void BuildSentence(RectTransform parent, string number, SentencePart[] parts, Func<SimulationStats, string> counter)
    {
        GameObject row = new GameObject("Rule" + number, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.SetParent(parent, false);

        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        ContentSizeFitter fit = row.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddLabel(rowRect, number + ".", 0.45f);

        foreach (var part in parts)
        {
            if (!part.isBlank)
                AddLabel(rowRect, part.text, 1f);
            else
                AddBlankButton(rowRect, part);
        }

        if (counter != null)
            AddCounterLabel(rowRect, counter);
    }

    void AddLabel(RectTransform parent, string text, float alpha)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        go.GetComponent<LayoutElement>().minHeight = rowHeight;
    }

    void AddCounterLabel(RectTransform parent, Func<SimulationStats, string> counter)
    {
        GameObject go = new GameObject("Counter", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize * 0.8f;
        tmp.fontStyle = FontStyles.Italic;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);

        LayoutElement counterLE = go.GetComponent<LayoutElement>();
        counterLE.minHeight = rowHeight;
        counterLE.minWidth = 80f;
        counterLE.preferredWidth = 150f; // cap so long texts can't stretch the row

        string lastText = null;
        refreshers.Add(() =>
        {
            SimulationStats stats = statsSource != null && statsSource.worldState != null
                ? statsSource.worldState.stats : null;
            string text = stats != null ? counter(stats) : "";
            if (text == lastText) return;
            lastText = text;
            tmp.text = text;
        });
    }

    void AddBlankButton(RectTransform parent, SentencePart part)
    {
        GameObject go = new GameObject("Blank", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.color = blankColor;

        if (firstBlankImage == null)
        {
            firstBlankImage = bg;
            firstBlankValue = part.getValue;
        }

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minWidth = 92f;
        le.minHeight = rowHeight - 4f;

        GameObject labelGO = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.SetParent(go.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        TextMeshProUGUI valueText = labelGO.GetComponent<TextMeshProUGUI>();
        valueText.fontSize = fontSize;
        valueText.fontStyle = FontStyles.Bold;
        valueText.color = textColor;
        valueText.alignment = TextAlignmentOptions.Center;

        string lastValue = null;
        refreshers.Add(() =>
        {
            string v = part.getValue();
            if (v == lastValue) return;
            lastValue = v;

            valueText.text = string.IsNullOrEmpty(v) ? EmptyBlank : ChineseUi.RuleLabel(v);
            bg.color = string.IsNullOrEmpty(v) ? blankColor : blankFilledColor;
        });

        go.GetComponent<Button>().onClick.AddListener(() => OpenDropdown((RectTransform)go.transform, part));
    }

    // ---------------------------------------------------------- dropdown

    void OpenDropdown(RectTransform blank, SentencePart part)
    {
        CloseDropdown();

        clickCatcher = new GameObject("DropdownCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform catcherRect = clickCatcher.GetComponent<RectTransform>();
        catcherRect.SetParent(uiCanvas.transform, false);
        catcherRect.anchorMin = Vector2.zero;
        catcherRect.anchorMax = Vector2.one;
        catcherRect.offsetMin = Vector2.zero;
        catcherRect.offsetMax = Vector2.zero;
        clickCatcher.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        clickCatcher.GetComponent<Button>().onClick.AddListener(CloseDropdown);

        GameObject panelGO = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        dropdownPanel = panelGO.GetComponent<RectTransform>();
        dropdownPanel.SetParent(uiCanvas.transform, false);

        panelGO.GetComponent<Image>().color = new Color(0.97f, 0.97f, 0.97f, 1f);

        VerticalLayoutGroup v = panelGO.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(4, 4, 4, 4);
        v.spacing = 2f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        panelGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelGO.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (string option in part.options)
            AddDropdownOption(part, ChineseUi.RuleLabel(option), option, false);

        AddDropdownOption(part, ClearOption, "", true);

        dropdownPanel.pivot = new Vector2(0f, 1f);
        Vector3[] corners = new Vector3[4];
        blank.GetWorldCorners(corners); // 0 = bottom-left
        dropdownPanel.position = corners[0];
    }

    void AddDropdownOption(SentencePart part, string label, string value, bool isClear)
    {
        GameObject go = new GameObject("Option", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(dropdownPanel, false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        go.GetComponent<LayoutElement>().minWidth = 120f;
        go.GetComponent<LayoutElement>().minHeight = rowHeight - 4f;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.SetParent(go.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = isClear ? FontStyles.Italic : FontStyles.Normal;
        tmp.color = isClear ? new Color(textColor.r, textColor.g, textColor.b, 0.5f) : textColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        go.GetComponent<Button>().onClick.AddListener(() =>
        {
            part.setValue(value);
            CloseDropdown();
        });
    }

    void CloseDropdown()
    {
        if (dropdownPanel != null) Destroy(dropdownPanel.gameObject);
        if (clickCatcher != null) Destroy(clickCatcher);
        dropdownPanel = null;
        clickCatcher = null;
    }

    // ---------------------------------------------------------- tab sprite

    Sprite tabSpriteBottomRounded;
    Sprite tabSpriteTopRounded;

    /// <summary>9-sliced white sprite with two rounded corners — bottom pair
    /// for top drawers, top pair for bottom drawers. Tinted via Image.color.</summary>
    Sprite GetTabSprite(bool roundedTop)
    {
        Sprite cached = roundedTop ? tabSpriteTopRounded : tabSpriteBottomRounded;
        if (cached != null) return cached;

        const int size = 24;
        const int radius = 8;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        Color32 solid = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(255, 255, 255, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x2 = 0; x2 < size; x2++)
            {
                bool inside = true;

                int yy = roundedTop ? (size - 1 - y) : y; // mirror for top-rounded
                if (yy < radius)
                {
                    if (x2 < radius)
                        inside = (x2 - radius) * (x2 - radius) + (yy - radius) * (yy - radius) <= radius * radius;
                    else if (x2 >= size - radius)
                        inside = (x2 - (size - 1 - radius)) * (x2 - (size - 1 - radius)) + (yy - radius) * (yy - radius) <= radius * radius;
                }

                pixels[y * size + x2] = inside ? solid : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
        tex.filterMode = FilterMode.Bilinear;

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));

        if (roundedTop) tabSpriteTopRounded = sprite;
        else tabSpriteBottomRounded = sprite;

        return sprite;
    }

    // ---------------------------------------------------------- cursor

    /// <summary>A small crosshair cursor — you aim at agents to inspect them.</summary>
    void ApplyCrosshairCursor()
    {
        const int size = 33;
        const int mid = size / 2;
        const int gap = 3;
        const int arm = 10;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 c = crosshairColor;

        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int o = gap; o <= gap + arm; o++)
        {
            SetCursorPixel(pixels, size, mid + o, mid, c);
            SetCursorPixel(pixels, size, mid - o, mid, c);
            SetCursorPixel(pixels, size, mid, mid + o, c);
            SetCursorPixel(pixels, size, mid, mid - o, c);
        }

        SetCursorPixel(pixels, size, mid, mid, c); // center dot

        tex.SetPixels32(pixels);
        tex.Apply(false);

        Cursor.SetCursor(tex, new Vector2(mid, mid), CursorMode.ForceSoftware);
    }

    static void SetCursorPixel(Color32[] pixels, int size, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        pixels[y * size + x] = c;
    }
}
