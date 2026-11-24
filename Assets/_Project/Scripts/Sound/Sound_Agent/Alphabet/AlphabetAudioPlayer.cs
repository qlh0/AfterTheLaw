using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AlphabetAudioPlayer : MonoBehaviour
{
    [System.Serializable]
    public class LetterClip
    {
        public string letter;   // A-Z
        public AudioClip clip;
    }

    [Header("A-Z Clips")]
    public List<LetterClip> letterClips = new List<LetterClip>();

    [Header("Playback Text")]
    [TextArea(2, 5)]
    public string inputText = "HELLO";
    public bool playOnStart = false;
    public bool loopText = false;
    public bool ignoreUnknownCharacters = true;

    [Header("Playback Speed")]
    [Range(1f, 10f)] public float playbackSpeed = 3f;
    [Range(0f, 0.08f)] public float pitchJitter = 0.01f;

    [Header("Letter Gaps")]
    [Range(0f, 0.25f)] public float vowelGap = 0.01f;
    [Range(0f, 0.25f)] public float consonantGap = 0.0f;
    [Range(0f, 0.5f)] public float spaceGap = 0.08f;
    [Range(0f, 0.3f)] public float punctuationGap = 0.04f;

    [Header("Softness")]
    [Range(0.001f, 0.2f)] public float fadeInTime = 0.01f;
    [Range(0.001f, 0.2f)] public float fadeOutTime = 0.03f;

    [Header("Distant Feel")]
    public bool useDistantTone = true;
    [Range(500f, 22000f)] public float lowPassCutoff = 3800f;
    [Range(-10000f, 0f)] public float reverbDryLevel = -1200f;
    [Range(-10000f, 0f)] public float reverbRoom = -1600f;
    [Range(-10000f, 0f)] public float reverbRoomHF = -3200f;
    [Range(0.1f, 20f)] public float reverbDecayTime = 1.8f;
    [Range(0.1f, 2f)] public float reverbDecayHFRatio = 0.6f;
    [Range(-10000f, 1000f)] public float reverbLevel = -900f;
    [Range(0f, 0.3f)] public float reverbDelay = 0.04f;
    [Range(0f, 100f)] public float diffusion = 85f;
    [Range(0f, 100f)] public float density = 70f;

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 1f;

    AudioSource source;
    AudioLowPassFilter lowPassFilter;
    AudioReverbFilter reverbFilter;
    Coroutine playRoutine;

    readonly Dictionary<char, AudioClip> clipMap = new Dictionary<char, AudioClip>();

    void Awake()
    {
        source = GetComponent<AudioSource>();
        ConfigureSource(source);

        lowPassFilter = GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null)
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();

        reverbFilter = GetComponent<AudioReverbFilter>();
        if (reverbFilter == null)
            reverbFilter = gameObject.AddComponent<AudioReverbFilter>();

        ApplyDistanceFilters();
        RebuildMap();
    }

    void Start()
    {
        if (playOnStart)
            PlayText(inputText);
    }

    void OnValidate()
    {
        AudioSource src = GetComponent<AudioSource>();
        if (src != null)
        {
            src.playOnAwake = false;
            src.loop = false;
        }

        lowPassFilter = GetComponent<AudioLowPassFilter>();
        reverbFilter = GetComponent<AudioReverbFilter>();

        ApplyDistanceFilters();
    }

    void ConfigureSource(AudioSource src)
    {
        if (src == null) return;

        src.playOnAwake = false;
        src.loop = false;
        src.volume = 0f;
        src.pitch = 1f;
        src.dopplerLevel = 0f;
        src.spatialBlend = 0f;
    }

    void ApplyDistanceFilters()
    {
        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = useDistantTone;
            lowPassFilter.cutoffFrequency = lowPassCutoff;
            lowPassFilter.lowpassResonanceQ = 1f;
        }

        if (reverbFilter != null)
        {
            reverbFilter.enabled = useDistantTone;
            reverbFilter.dryLevel = reverbDryLevel;
            reverbFilter.room = reverbRoom;
            reverbFilter.roomHF = reverbRoomHF;
            reverbFilter.decayTime = reverbDecayTime;
            reverbFilter.decayHFRatio = reverbDecayHFRatio;
            reverbFilter.reverbLevel = reverbLevel;
            reverbFilter.reverbDelay = reverbDelay;
            reverbFilter.diffusion = diffusion;
            reverbFilter.density = density;
        }
    }

    public void RebuildMap()
    {
        clipMap.Clear();

        foreach (var entry in letterClips)
        {
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.letter)) continue;
            if (entry.clip == null) continue;

            char c = char.ToUpperInvariant(entry.letter.Trim()[0]);
            if (c < 'A' || c > 'Z') continue;

            clipMap[c] = entry.clip;
        }
    }

    public void PlayText(string text)
    {
        StopText();
        playRoutine = StartCoroutine(PlayTextRoutine(text));
    }

    public void PlayInspectorText()
    {
        PlayText(inputText);
    }

    public void StopText()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (source != null)
        {
            source.Stop();
            source.volume = 0f;
            source.pitch = 1f;
        }
    }

    IEnumerator PlayTextRoutine(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        RebuildMap();
        ApplyDistanceFilters();

        do
        {
            for (int i = 0; i < text.Length; i++)
            {
                char originalChar = text[i];
                char lookupChar = char.ToUpperInvariant(originalChar);

                if (originalChar == ' ')
                {
                    yield return new WaitForSeconds(spaceGap);
                    continue;
                }

                if (!IsSupportedLetter(lookupChar))
                {
                    if (!ignoreUnknownCharacters)
                        Debug.LogWarning($"AlphabetAudioPlayer: unsupported character '{originalChar}' on {name}");

                    yield return new WaitForSeconds(punctuationGap);
                    continue;
                }

                if (!clipMap.TryGetValue(lookupChar, out AudioClip clip) || clip == null)
                {
                    if (!ignoreUnknownCharacters)
                        Debug.LogWarning($"AlphabetAudioPlayer: no clip assigned for '{lookupChar}' on {name}");

                    yield return new WaitForSeconds(consonantGap);
                    continue;
                }

                float speed = Mathf.Max(0.01f, playbackSpeed + Random.Range(-pitchJitter, pitchJitter));
                float duration = clip.length / speed;

                source.Stop();
                source.clip = clip;
                source.pitch = speed;
                source.volume = 0f;
                source.time = 0f;
                source.Play();

                yield return StartCoroutine(FadeSource(source, 0f, volume, fadeInTime));

                float mainPlayTime = Mathf.Max(0f, duration - fadeInTime - fadeOutTime);
                yield return new WaitForSeconds(mainPlayTime);

                yield return StartCoroutine(FadeSource(source, source.volume, 0f, fadeOutTime));

                source.Stop();

                float gap = IsVowel(lookupChar) ? vowelGap : consonantGap;
                if (gap > 0f)
                    yield return new WaitForSeconds(gap);
            }
        }
        while (loopText);

        playRoutine = null;
    }

    IEnumerator FadeSource(AudioSource src, float from, float to, float duration)
    {
        if (src == null) yield break;

        if (duration <= 0f)
        {
            src.volume = to;
            yield break;
        }

        float t = 0f;
        src.volume = from;

        while (t < duration && src != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            src.volume = Mathf.Lerp(from, to, k);
            yield return null;
        }

        if (src != null)
            src.volume = to;
    }

    bool IsSupportedLetter(char c)
    {
        return c >= 'A' && c <= 'Z';
    }

    bool IsVowel(char c)
    {
        return c == 'A' || c == 'E' || c == 'I' || c == 'O' || c == 'U';
    }
}