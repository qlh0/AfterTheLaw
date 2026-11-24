using UnityEngine;

public class NoiseModule : AudioModuleBase
{
    public enum NoiseType
    {
        White
    }

    [Header("Noise")]
    public NoiseType noiseType = NoiseType.White;
    [Range(0f, 1f)] public float amplitude = 0.05f;

    [Header("Character")]
    [Range(0f, 1f)] public float smoothing = 0.85f;

    [Header("Control Inputs")]
    public ControlModuleBase amplitudeControl;
    public float amplitudeControlAmount = 0f;

    float lastSampleL = 0f;
    float lastSampleR = 0f;

    protected override float EvaluateSample(double dspTime, int channel)
    {
        float currentAmplitude = amplitude;
        float t = (float)dspTime;

        if (amplitudeControl != null)
            currentAmplitude += amplitudeControl.GetValue(t) * amplitudeControlAmount;

        currentAmplitude = Mathf.Clamp01(currentAmplitude);

        float raw = GenerateNoiseSample();
        float smoothed = ApplySmoothing(raw, channel);

        return smoothed * currentAmplitude;
    }

    float GenerateNoiseSample()
    {
        switch (noiseType)
        {
            case NoiseType.White:
                return Random.Range(-1f, 1f);

            default:
                return 0f;
        }
    }

    float ApplySmoothing(float input, int channel)
    {
        float s = Mathf.Clamp01(smoothing);

        if (channel == 0)
        {
            lastSampleL = Mathf.Lerp(input, lastSampleL, s);
            return lastSampleL;
        }
        else
        {
            lastSampleR = Mathf.Lerp(input, lastSampleR, s);
            return lastSampleR;
        }
    }

    public override void ResetState()
    {
        lastSampleL = 0f;
        lastSampleR = 0f;
    }

    protected override void ValidateModule()
    {
        base.ValidateModule();
        amplitude = Mathf.Clamp01(amplitude);
        smoothing = Mathf.Clamp01(smoothing);
    }
}