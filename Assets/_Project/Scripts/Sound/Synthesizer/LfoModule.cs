using UnityEngine;

public class LfoModule : ControlModuleBase
{
    public enum Waveform
    {
        Sine,
        Square,
        Saw,
        Triangle
    }

    [Header("LFO")]
    public Waveform waveform = Waveform.Sine;
    [Min(0.01f)] public float frequency = 1f;
    public float phaseOffset = 0f;
    public float amplitude = 1f;
    public float bias = 0f;

    protected override float Evaluate(float time)
    {
        float phase = time * frequency + phaseOffset;
        phase = phase - Mathf.Floor(phase);

        float sample = GenerateWaveSample(phase);
        return sample * amplitude + bias;
    }

    float GenerateWaveSample(float phase)
    {
        switch (waveform)
        {
            case Waveform.Sine:
                return Mathf.Sin(phase * 2f * Mathf.PI);

            case Waveform.Square:
                return phase < 0.5f ? 1f : -1f;

            case Waveform.Saw:
                return phase * 2f - 1f;

            case Waveform.Triangle:
                return 1f - 4f * Mathf.Abs(phase - 0.5f);

            default:
                return 0f;
        }
    }

    protected override void ValidateModule()
    {
        base.ValidateModule();
        frequency = Mathf.Max(0.01f, frequency);
    }
}