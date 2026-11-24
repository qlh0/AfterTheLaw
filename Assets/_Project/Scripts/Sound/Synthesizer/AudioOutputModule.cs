using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioOutputModule : MonoBehaviour
{
    [Header("Audio Graph Input")]
    public AudioModuleBase sourceModule;

    [Header("Output")]
    [Range(0f, 1f)] public float outputGain = 0.2f;

    double dspSampleTime;
    double sampleRate;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        dspSampleTime = 0.0;
    }

    void OnValidate()
    {
        outputGain = Mathf.Clamp01(outputGain);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (sourceModule == null)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = 0f;
            return;
        }

        for (int i = 0; i < data.Length; i += channels)
        {
            double currentDspTime = dspSampleTime / sampleRate;
            float sample = sourceModule.GetSample(currentDspTime, 0) * outputGain;

            for (int ch = 0; ch < channels; ch++)
            {
                data[i + ch] = sample;
            }

            dspSampleTime += 1.0;
        }
    }
}