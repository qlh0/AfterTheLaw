using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AgentSynthVoice : MonoBehaviour
{
    [Header("Tone Source")]
    public LoveTone toneSource;

    [Header("Master")]
    [Range(0f, 1f)] public float masterVolume = 0.2f;
    public bool playOnEnable = true;

    [Header("Spatial")]
    [Range(0f, 1f)] public float spatialBlend = 0f;
    public float minDistance = 1.5f;
    public float maxDistance = 12f;

    AudioSource audioSource;
    AudioClip synthClip;
    int sampleRate;
    bool initialized = false;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        if (sampleRate <= 0) sampleRate = 48000;

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = spatialBlend;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.volume = 1f;
        audioSource.mute = false;

        synthClip = AudioClip.Create(
            "AgentSynth_" + GetInstanceID(),
            sampleRate,   // 1秒长度
            1,            // mono
            sampleRate,
            true,         // streaming
            OnAudioRead
        );

        audioSource.clip = synthClip;
        initialized = true;

        if (playOnEnable)
            audioSource.Play();
    }

    void OnEnable()
    {
        if (initialized && playOnEnable && audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    void OnDisable()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    void OnValidate()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.spatialBlend = spatialBlend;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = 0f;
        }
    }

    void OnAudioRead(float[] data)
    {
        if (toneSource == null || !toneSource.enabled)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = 0f;
            return;
        }

        double dspTime = AudioSettings.dspTime;
        double dt = 1.0 / sampleRate;

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = toneSource.GenerateSample(dspTime, sampleRate) * masterVolume;
            dspTime += dt;
        }
    }

    void OnDestroy()
    {
        if (synthClip != null)
            Destroy(synthClip);
    }
}