using UnityEngine;

public static class IdentityRelationTable
{
    public static float GetRelation(IdentityType a, IdentityType b)
    {
        if (a == IdentityType.Parent && b == IdentityType.Child) return 0.8f;
        if (a == IdentityType.Child && b == IdentityType.Parent) return 0.8f;

        if (a == IdentityType.Friend && b == IdentityType.Friend) return 0.6f;

        if (a == IdentityType.Other && b == IdentityType.Other) return 0.3f;

        if (a == IdentityType.Enemy && b == IdentityType.Enemy) return -0.4f;

        if ((a == IdentityType.Enemy && b == IdentityType.Friend) ||
            (a == IdentityType.Friend && b == IdentityType.Enemy))
            return -0.6f;

        return 0.1f; // 默认弱吸引
    }
}