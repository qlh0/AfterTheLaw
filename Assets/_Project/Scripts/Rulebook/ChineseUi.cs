using System.Collections.Generic;
using TMPro;
using UnityEngine;

static class ChineseUi
{
    static TMP_FontAsset fallbackFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterFont()
    {
        if (fallbackFont == null)
        {
            Font font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (font != null)
                fallbackFont = TMP_FontAsset.CreateFontAsset(font);
        }

        if (fallbackFont == null)
        {
            Debug.LogError("MYLIFE could not load its Chinese UI font.");
            return;
        }

        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks == null)
            TMP_Settings.fallbackFontAssets = fallbacks = new List<TMP_FontAsset>();

        if (!fallbacks.Contains(fallbackFont))
            fallbacks.Add(fallbackFont);
    }

    // Translate labels for display while keeping the rule engine's original values.
    public static string RuleLabel(string value) => value switch
    {
        "Child" => "孩子",
        "Friend" => "朋友",
        "Enemy" => "敌人",
        "Other" => "其他人",
        "Hungry" => "饥饿",
        "Lonely" => "孤独",
        "Joyful" => "喜悦",
        "Hurt" => "受伤",
        "Dead" => "死亡",
        "Love" => "爱",
        "Violence" => "暴力",
        "Silence" => "沉默",
        "Cooperation" => "合作",
        "None" => "无",
        _ => value
    };
}
