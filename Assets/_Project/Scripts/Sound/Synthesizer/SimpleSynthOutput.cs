using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleSynthOutput : MonoBehaviour
{
    public enum Waveform
    {
        Sine,
        Square,
        Saw,
        Triangle,
        Noise
    }

    [Header("Oscillator")]
    public Waveform waveform = Waveform.Sine;
    [Min(0.01f)] public float frequency = 440f;

    [Header("Gain")]
    [Range(0f, 1f)] public float gain = 0.1f;

    double phase;
    double sampleRate;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    void OnValidate()
    {
        frequency = Mathf.Max(0.01f, frequency);
        gain = Mathf.Clamp01(gain);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        double phaseIncrement = frequency / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = GenerateSample();
            sample *= gain;

            for (int ch = 0; ch < channels; ch++)
            {
                data[i + ch] = sample;
            }

            phase += phaseIncrement;
            if (phase >= 1.0)
                phase -= 1.0;
        }
    }

    float GenerateSample()
    {
        switch (waveform)
        {
            case Waveform.Sine:
                return Mathf.Sin((float)(phase * 2.0 * Mathf.PI));

            case Waveform.Square:
                return phase < 0.5 ? 1f : -1f;

            case Waveform.Saw:
                return (float)(phase * 2.0 - 1.0);

            case Waveform.Triangle:
                return 1f - 4f * Mathf.Abs((float)phase - 0.5f);

            case Waveform.Noise:
                return Random.Range(-1f, 1f);

            default:
                return 0f;
        }
    }
}