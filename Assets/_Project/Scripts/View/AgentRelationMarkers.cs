using UnityEngine;

public class AgentRelationMarkers : MonoBehaviour
{
    public Transform friendMarker;
    public Transform enemyMarker;

    public void SetMarkers(Transform friend, Transform enemy)
    {
        friendMarker = friend;
        enemyMarker = enemy;
    }
}