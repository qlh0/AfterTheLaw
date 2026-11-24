public static class SocialRelationTable
{
    public static float GetRelation(SocialRelationType relation)
    {
        switch (relation)
        {
            case SocialRelationType.Friend:
                return 0.6f;

            case SocialRelationType.Enemy:
                return -0.6f;

            case SocialRelationType.Other:
            default:
                return 0.3f;
        }
    }
}