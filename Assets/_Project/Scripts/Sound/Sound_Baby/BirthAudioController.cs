using UnityEngine;

public class BirthAudioController : MonoBehaviour
{
    public AgentSpawner spawner;

    [Header("4 Birth Speakers")]
    public AudioSource speakerA;
    public AudioSource speakerB;
    public AudioSource speakerC;
    public AudioSource speakerD;

    [Header("Stereo")]
    public bool usePanFromScreenPosition = true;
    [Range(0f, 1f)] public float panStrength = 1f;

    int lastAgentCount = 0;
    int lastCheckedMaxId = -1;
    int nextSpeakerIndex = 0;
    bool hasInitialized = false;

    void Start()
    {
        PrepareSpeaker(speakerA);
        PrepareSpeaker(speakerB);
        PrepareSpeaker(speakerC);
        PrepareSpeaker(speakerD);
    }

    void Update()
    {
        if (spawner == null || spawner.worldState == null) return;

        // 第一次只建立基线，不播放出生音
        if (!hasInitialized)
        {
            lastAgentCount = spawner.worldState.agents.Count;
            lastCheckedMaxId = GetMaxAgentId();
            hasInitialized = true;
            return;
        }

        // Birth detection is id-based (agent ids are monotonically increasing),
        // NOT count-based: the simulation now removes expired corpses from the
        // list, so list count can shrink even while births happen.
        {
            AgentData newestAgent = GetNewestAgent();

            if (newestAgent != null)
            {
                AudioSource speaker = GetNextSpeaker();
                if (speaker != null)
                {
                    float pan = 0f;

                    if (usePanFromScreenPosition && Camera.main != null)
                    {
                        Vector3 viewport = Camera.main.WorldToViewportPoint(newestAgent.position);
                        pan = Mathf.Clamp((viewport.x - 0.5f) * 2f * panStrength, -1f, 1f);
                    }

                    speaker.panStereo = pan;
                    speaker.Play();

                    lastCheckedMaxId = Mathf.Max(lastCheckedMaxId, newestAgent.id);
                }
            }
        }

        lastAgentCount = spawner.worldState.agents.Count;
    }

    void PrepareSpeaker(AudioSource source)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = false;
    }

    AudioSource GetNextSpeaker()
    {
        AudioSource[] speakers = new AudioSource[] { speakerA, speakerB, speakerC, speakerD };

        for (int i = 0; i < speakers.Length; i++)
        {
            int index = (nextSpeakerIndex + i) % speakers.Length;
            if (speakers[index] != null)
            {
                nextSpeakerIndex = (index + 1) % speakers.Length;
                return speakers[index];
            }
        }

        return null;
    }

    AgentData GetNewestAgent()
    {
        AgentData newest = null;
        int highestId = lastCheckedMaxId;

        foreach (var agent in spawner.worldState.agents)
        {
            if (agent == null) continue;

            if (agent.id > highestId)
            {
                highestId = agent.id;
                newest = agent;
            }
        }

        return newest;
    }

    int GetMaxAgentId()
    {
        int maxId = -1;

        foreach (var agent in spawner.worldState.agents)
        {
            if (agent == null) continue;
            if (agent.id > maxId) maxId = agent.id;
        }

        return maxId;
    }
}