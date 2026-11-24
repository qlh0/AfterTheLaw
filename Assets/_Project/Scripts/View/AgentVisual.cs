using TMPro;
using UnityEngine;

public class AgentVisual : MonoBehaviour
{
    public int agentId;

    TextMeshPro text3D;

    void Awake()
    {
        text3D = GetComponent<TextMeshPro>();

        if (text3D == null)
            text3D = GetComponentInChildren<TextMeshPro>();
    }

    public void SetId(int id)
    {
        agentId = id;

        if (text3D != null)
            text3D.text = agentId.ToString();
    }
}