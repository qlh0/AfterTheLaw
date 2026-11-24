using UnityEngine;

public abstract class AudioModuleBase : SynthModuleBase
{
    [Header("Audio Output")]
    public bool clampOutput = true;
    public float outputMin = -1f;
    public float outputMax = 1f;

    public float GetSample(double dspTime, int channel)
    {
        if (bypass) return 0f;

        float sample = EvaluateSample(dspTime, channel);

        if (clampOutput)
            sample = Mathf.Clamp(sample, outputMin, outputMax);

        return sample;
    }

    protected abstract float EvaluateSample(double dspTime, int channel);

    protected override void ValidateModule()
    {
        if (outputMax < outputMin)
            outputMax = outputMin;
    }
}