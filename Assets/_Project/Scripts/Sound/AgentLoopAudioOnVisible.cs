using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AgentLoopAudioOnVisible : MonoBehaviour
{
    [Header("Loop Audio")]
    public AudioClip loopClip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool playOnEnable = true;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
    }

    void OnEnable()
    {
        if (!playOnEnable) return;
        PlayLoop();
    }

    void OnDisable()
    {
        StopLoop();
    }

    void OnDestroy()
    {
        StopLoop();
    }

    void PlayLoop()
    {
        if (audioSource == null) return;
        if (loopClip == null) return;

        audioSource.volume = volume;

        if (audioSource.clip != loopClip)
            audioSource.clip = loopClip;

        if (!audioSource.isPlaying)
        {
            audioSource.time = Random.Range(0f, loopClip.length);
            audioSource.Play();
        }
    }

    void StopLoop()
    {
        if (audioSource == null) return;

        if (audioSource.isPlaying)
            audioSource.Stop();
    }
}