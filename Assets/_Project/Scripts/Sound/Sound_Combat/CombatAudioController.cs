using UnityEngine;

public class CombatAudioController : MonoBehaviour
{
    public AgentSpawner spawner;

    [Header("3 Combat Speakers")]
    public AudioSource speakerA;
    public AudioSource speakerB;
    public AudioSource speakerC;

    [Header("Thresholds")]
    public int level2Threshold = 5;
    public int level3Threshold = 20;

    [Header("Volume Mapping")]
    public float baseVolume = 0.08f;
    public float volumePerPair = 0.02f;
    public float maxVolumePerSpeaker = 0.5f;

    [Header("Smoothing")]
    public float fadeSpeed = 6f;

    [Header("Layer Offsets")]
    public float speakerBStartOffset = 0.18f;
    public float speakerCStartOffset = 0.33f;

    bool speakerAStarted = false;
    bool speakerBStarted = false;
    bool speakerCStarted = false;

    void Start()
    {
        PrepareSpeaker(speakerA);
        PrepareSpeaker(speakerB);
        PrepareSpeaker(speakerC);
    }

    void Update()
    {
        if (spawner == null || spawner.worldState == null) return;

        int combatCount = spawner.worldState.combatMarkers.Count;

        bool useA = combatCount > 0;
        bool useB = combatCount >= level2Threshold;
        bool useC = combatCount >= level3Threshold;

        float totalVolume = 0f;
        if (combatCount > 0)
        {
            totalVolume = Mathf.Clamp(
                baseVolume + combatCount * volumePerPair,
                0f,
                maxVolumePerSpeaker * 3f
            );
        }

        int activeLayers = 0;
        if (useA) activeLayers++;
        if (useB) activeLayers++;
        if (useC) activeLayers++;

        float perLayerVolume = activeLayers > 0 ? totalVolume / activeLayers : 0f;
        perLayerVolume = Mathf.Clamp(perLayerVolume, 0f, maxVolumePerSpeaker);

        HandleSpeaker(speakerA, useA, perLayerVolume, ref speakerAStarted, 0f);
        HandleSpeaker(speakerB, useB, perLayerVolume, ref speakerBStarted, speakerBStartOffset);
        HandleSpeaker(speakerC, useC, perLayerVolume, ref speakerCStarted, speakerCStartOffset);
    }

    void PrepareSpeaker(AudioSource source)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
    }

    void HandleSpeaker(AudioSource source, bool shouldPlay, float targetVolume, ref bool started, float startOffset)
    {
        if (source == null) return;

        if (shouldPlay)
        {
            if (!started)
            {
                if (source.clip != null && source.clip.length > 0f)
                    source.time = Mathf.Clamp(startOffset, 0f, source.clip.length - 0.01f);

                source.Play();
                started = true;
            }

            source.volume = Mathf.Lerp(source.volume, targetVolume, Time.deltaTime * fadeSpeed);
        }
        else
        {
            source.volume = Mathf.Lerp(source.volume, 0f, Time.deltaTime * fadeSpeed);

            if (started && source.volume < 0.01f)
            {
                source.Stop();
                source.volume = 0f;
                started = false;
            }
        }
    }
}