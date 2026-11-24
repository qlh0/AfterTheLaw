using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldEventPanel : MonoBehaviour
{
    readonly StringBuilder lines = new StringBuilder();
    WorldEventLog displayedLog;
    int displayedRevision = -1;
    TMP_Text text;
    ScrollRect scroll;

    public void Build(RectTransform parent, float width, float fontSize, Color textColor)
    {
        var scrollObject = new GameObject("EventHistory", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        var scrollRect = (RectTransform)scrollObject.transform;
        scrollRect.SetParent(parent, false);
        scrollRect.sizeDelta = new Vector2(width, 322f);
        var size = scrollObject.GetComponent<LayoutElement>();
        size.preferredWidth = width;
        size.preferredHeight = 322f;

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        var viewport = (RectTransform)viewportObject.transform;
        viewport.SetParent(scrollRect, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

        var textObject = new GameObject("EventList", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        var content = (RectTransform)textObject.transform;
        content.SetParent(viewport, false);
        content.anchorMin = Vector2.up;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(-16f, 0f);
        content.anchoredPosition = Vector2.zero;

        text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = "暂无重要事件";
        textObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22f;
    }

    public void Refresh(WorldEventLog log)
    {
        bool newWorld = displayedLog != log;
        if (!newWorld && displayedRevision == log.Revision) return;

        displayedLog = log;
        displayedRevision = log.Revision;
        lines.Clear();
        for (int i = log.Events.Count - 1; i >= 0; i--)
        {
            WorldEvent entry = log.Events[i];
            if (lines.Length > 0) lines.AppendLine().AppendLine();
            lines.Append("<b>").Append(entry.Time.ToString("0.0")).Append(" 秒</b>　");
            AppendSummary(entry);
        }
        text.text = lines.Length == 0 ? "暂无重要事件" : lines.ToString();
        if (newWorld)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    void AppendSummary(WorldEvent entry)
    {
        switch (entry.Kind)
        {
            case WorldEventKind.WorldRuleChanged:
                lines.Append("<color=#A92C26>你修改了世界的规则</color>（");
                for (int i = 0; i < entry.RuleChanges.Count; i++)
                {
                    if (i > 0) lines.Append("、");
                    lines.Append(RuleName(entry.RuleChanges[i].Key));
                }
                lines.Append('）');
                break;
            case WorldEventKind.RelationshipChanged:
                lines.Append("关系变化：");
                AppendRelation(entry.RelationChanges[0]);
                break;
            case WorldEventKind.Conflict:
                lines.Append(entry.Actor).Append(" 与 ").Append(entry.Target).Append(" 爆发冲突");
                int friendshipsBroken = 0;
                foreach (var change in entry.RelationChanges)
                    if (change.Before == SocialRelationType.Friend && change.After == SocialRelationType.Enemy)
                        friendshipsBroken++;
                if (friendshipsBroken >= 2)
                    lines.Append("，双方反目");
                else if (friendshipsBroken == 1)
                    lines.Append("，一方由朋友转为敌人");
                break;
            case WorldEventKind.AgentDied:
                lines.Append(entry.Actor).Append(" 死亡（").Append(DeathLabel(entry.DeathCause)).Append('）');
                break;
        }
    }

    void AppendRelation(WorldRelationChange change) =>
        lines.Append(change.Observer).Append(" 对 ").Append(change.Target).Append("：")
             .Append(ChineseUi.RuleLabel(change.Before.ToString())).Append(" → ")
             .Append(ChineseUi.RuleLabel(change.After.ToString()));

    static string DeathLabel(WorldDeathCause? cause) => cause switch
    {
        WorldDeathCause.Age => "寿命结束",
        WorldDeathCause.Conflict => "冲突致命伤",
        WorldDeathCause.PopulationLimit => "人口限制",
        _ => "生命值耗尽",
    };

    static string RuleName(string key) => key switch
    {
        "draw_together" => "相互靠近",
        "turn_away" => "相互远离",
        "becomes" => "被视为",
        "spreads" => "状态传播",
        "behavior_rises" => "行为增强",
        "foodRespawnInterval" => "食物刷新间隔",
        "foodRespawnAmount" => "每次刷新食物数量",
        "foodDecayPerTick" => "食物消耗速度",
        "starvingHpLossPerTick" => "饥饿伤害",
        "violenceThreshold" => "暴力触发阈值",
        "silenceThreshold" => "沉默触发阈值",
        "loveThreshold" => "爱触发阈值",
        "attackDamage" => "攻击伤害",
        "attackDistance" => "攻击距离",
        "birthFoodCost" => "生育食物消耗",
        "reproductionCooldownTime" => "生育间隔",
        "maxAliveAgents" => "人口上限",
        "lonelinessCheckRadius" => "陪伴感知范围",
        "joyfulRisePerTick" => "喜悦增长速度",
        "lonelyChurchSpawnChance" => "教堂生成概率",
        "moveSpeed" => "移动速度",
        "loveForceBonus" => "爱产生的吸引力",
        "violenceForceBonus" => "暴力产生的排斥力",
        "silenceForceMultiplier" => "沉默移动倍率",
        "gameSecondsPerRealSecond" => "世界时间倍率",
        _ => key,
    };
}

