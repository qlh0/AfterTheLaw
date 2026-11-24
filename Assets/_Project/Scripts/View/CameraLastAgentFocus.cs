using UnityEngine;

public class CameraLastAgentFocus : MonoBehaviour
{
    [Header("Refs")]
    public AgentSpawner spawner;
    public Camera targetCamera;

    [Header("Enable")]
    public bool enableLastAgentFocus = true;

    [Header("Zoom Sizes")]
    public float defaultOrthographicSize = 5f;
    public float lastAgentOrthographicSize = 2f;

    [Header("Speeds")]
    public float zoomInSpeed = 2.5f;
    public float zoomOutSpeed = 2f;
    public float followSpeed = 3f;

    [Header("Offset")]
    public Vector3 followOffset = new Vector3(0f, 0f, -10f);

    [Header("Return To Full View")]
    public bool returnToInitialPositionWhenNoAgents = true;

    Vector3 initialCameraPosition;
    float initialOrthographicSize;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            initialCameraPosition = targetCamera.transform.position;
            initialOrthographicSize = targetCamera.orthographicSize;
        }
    }

    void Start()
    {
        if (targetCamera != null)
        {
            if (defaultOrthographicSize <= 0f)
                defaultOrthographicSize = initialOrthographicSize;
        }
    }

    void LateUpdate()
    {
        if (!enableLastAgentFocus) return;
        if (spawner == null || spawner.worldState == null || targetCamera == null) return;

        int aliveCount = 0;
        AgentData lastAliveAgent = null;

        var agents = spawner.worldState.agents;
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && agents[i].alive)
            {
                aliveCount++;
                lastAliveAgent = agents[i];

                if (aliveCount > 1)
                    break;
            }
        }

        if (aliveCount == 1 && lastAliveAgent != null)
        {
            FollowLastAgent(lastAliveAgent);
            ZoomTo(lastAgentOrthographicSize, zoomInSpeed);
        }
        else if (aliveCount == 0)
        {
            if (returnToInitialPositionWhenNoAgents)
            {
                targetCamera.transform.position = Vector3.Lerp(
                    targetCamera.transform.position,
                    initialCameraPosition,
                    Time.deltaTime * followSpeed
                );
            }

            ZoomTo(defaultOrthographicSize > 0f ? defaultOrthographicSize : initialOrthographicSize, zoomOutSpeed);
        }
        else
        {
            ZoomTo(defaultOrthographicSize > 0f ? defaultOrthographicSize : initialOrthographicSize, zoomOutSpeed);
        }
    }

    void FollowLastAgent(AgentData agent)
    {
        Vector3 targetPos = new Vector3(agent.position.x, agent.position.y, 0f) + followOffset;

        targetCamera.transform.position = Vector3.Lerp(
            targetCamera.transform.position,
            targetPos,
            Time.deltaTime * followSpeed
        );
    }

    void ZoomTo(float targetSize, float speed)
    {
        if (!targetCamera.orthographic)
            return;

        targetCamera.orthographicSize = Mathf.Lerp(
            targetCamera.orthographicSize,
            targetSize,
            Time.deltaTime * speed
        );
    }

    public void ResetCameraImmediately()
    {
        if (targetCamera == null) return;

        targetCamera.transform.position = initialCameraPosition;
        targetCamera.orthographicSize = initialOrthographicSize;
    }
}