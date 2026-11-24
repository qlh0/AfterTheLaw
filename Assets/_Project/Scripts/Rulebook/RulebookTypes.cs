/// <summary>
/// Enum forms of the Rule Book card values. The drag-and-drop UI works with
/// strings (card labels); the simulation parses them once per tick into these
/// enums so hot loops never compare strings.
/// </summary>
public enum PerceivedIdentity
{
    None,
    Child,
    Friend,
    Enemy,
    Other
}

public enum RuleCondition
{
    None,
    Hungry,
    Lonely,
    Hurt,
    Joyful,
    Dead
}

public enum RuleBehavior
{
    None,
    Love,
    Violence,
    Silence
}

public static class RulebookParse
{
    public static PerceivedIdentity Identity(string value)
    {
        switch (value)
        {
            case "Child": return PerceivedIdentity.Child;
            case "Friend": return PerceivedIdentity.Friend;
            case "Enemy": return PerceivedIdentity.Enemy;
            case "Other": return PerceivedIdentity.Other;
            default: return PerceivedIdentity.None;
        }
    }

    public static RuleCondition Condition(string value)
    {
        switch (value)
        {
            case "Hungry": return RuleCondition.Hungry;
            case "Lonely": return RuleCondition.Lonely;
            case "Hurt": return RuleCondition.Hurt;
            case "Joyful": return RuleCondition.Joyful;
            case "Dead": return RuleCondition.Dead;
            default: return RuleCondition.None;
        }
    }

    public static RuleBehavior Behavior(string value)
    {
        switch (value)
        {
            case "Love": return RuleBehavior.Love;
            case "Violence": return RuleBehavior.Violence;
            case "Silence": return RuleBehavior.Silence;
            default: return RuleBehavior.None;
        }
    }
}
