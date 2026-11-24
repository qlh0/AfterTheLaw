using UnityEngine;

public class BirdBurstAudioManager : MonoBehaviour
{
    public static BirdBurstAudioManager Instance;

    public enum BirdTreeType
    {
        A,
        B,
        C
    }

    [Header("3 Bird Clips For 3 Tree Types")]
    public AudioClip birdClipA;
    public AudioClip birdClipB;
    public AudioClip birdClipC;

    [Header("3 Scene Speakers")]
    public AudioSource speakerA;
    public AudioSource speakerB;
    public AudioSource speakerC;

    [Header("Playback")]
    [Range(0f, 1f)] public float baseVolume = 0.8f;
    [Range(0f, 1f)] public float randomVolumeJitter = 0.1f;
    [Range(0.5f, 2f)] public float basePitch = 1f;
    [Range(0f, 0.3f)] public float randomPitchJitter = 0.08f;

    [Header("Stereo")]
    public bool usePanFromScreenPosition = true;
    [Range(0f, 1f)] public float panStrength = 1f;

    int nextSpeakerIndex = 0;

    void Awake()
    {
        Instance = this;
    }

    public void PlayBirdBurstAt(Vector3 worldPosition, BirdTreeType treeType)
    {
        AudioClip clip = GetClipForTreeType(treeType);
        if (clip == null) return;

        AudioSource speaker = GetNextSpeaker();
        if (speaker == null) return;

        float pan = 0f;

        if (usePanFromScreenPosition && Camera.main != null)
        {
            Vector3 viewport = Camera.main.WorldToViewportPoint(worldPosition);
            pan = Mathf.Clamp((viewport.x - 0.5f) * 2f * panStrength, -1f, 1f);
        }

        speaker.Stop();
        speaker.clip = clip;
        speaker.panStereo = pan;
        speaker.volume = Mathf.Clamp01(baseVolume + Random.Range(-randomVolumeJitter, randomVolumeJitter));
        speaker.pitch = Mathf.Max(0.01f, basePitch + Random.Range(-randomPitchJitter, randomPitchJitter));
        speaker.loop = false;
        speaker.Play();
    }

    AudioClip GetClipForTreeType(BirdTreeType treeType)
    {
        switch (treeType)
        {
            case BirdTreeType.A: return birdClipA;
            case BirdTreeType.B: return birdClipB;
            case BirdTreeType.C: return birdClipC;
        }

        return null;
    }

    AudioSource GetNextSpeaker()
    {
        AudioSource[] speakers = new AudioSource[] { speakerA, speakerB, speakerC };

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
}