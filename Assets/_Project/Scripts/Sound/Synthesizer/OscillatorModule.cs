using UnityEngine;

public class OscillatorModule : AudioModuleBase
{
    public enum Waveform
    {
        Sine,
        Square,
        Saw,
        Triangle
    }

    [Header("Oscillator")]
    public Waveform waveform = Waveform.Sine;
    [Min(0.01f)] public float frequency = 440f;
    [Range(0f, 1f)] public float amplitude = 0.2f;

    [Header("Control Inputs")]
    public ControlModuleBase frequencyControl;
    public float frequencyControlAmount = 0f;

    public ControlModuleBase amplitudeControl;
    public float amplitudeControlAmount = 0f;

    protected override float EvaluateSample(double dspTime, int channel)
    {
        float currentFrequency = frequency;
        float currentAmplitude = amplitude;

        float t = (float)dspTime;

        if (frequencyControl != null)
            currentFrequency += frequencyControl.GetValue(t) * frequencyControlAmount;

        if (amplitudeControl != null)
            currentAmplitude += amplitudeControl.GetValue(t) * amplitudeControlAmount;

        currentFrequency = Mathf.Max(0.01f, currentFrequency);
        currentAmplitude = Mathf.Clamp01(currentAmplitude);

        double phase = dspTime * currentFrequency;
        phase -= System.Math.Floor(phase); // keep 0~1

        float sample = GenerateWaveSample(phase);
        return sample * currentAmplitude;
    }

    float GenerateWaveSample(double phase)
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

            default:
                return 0f;
        }
    }

    protected override void ValidateModule()
    {
        base.ValidateModule();

        frequency = Mathf.Max(0.01f, frequency);
        amplitude = Mathf.Clamp01(amplitude);
    }
}