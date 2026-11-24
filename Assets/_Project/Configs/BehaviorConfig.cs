using UnityEngine;

public static class BehaviorConfig
{
    public static float GetDuration(BehaviorType type)
    {
        switch (type)
        {
            case BehaviorType.Love:
                return 3f;      // medium

            case BehaviorType.Violence:
                return 1.2f;    // short

            case BehaviorType.Silence:
                return 6f;      // long

            default:
                return 0f;
        }
    }
}