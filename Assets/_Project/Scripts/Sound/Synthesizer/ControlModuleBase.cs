using UnityEngine;

public abstract class ControlModuleBase : SynthModuleBase
{
    [Header("Control Output")]
    public bool clampOutput = false;
    public float outputMin = -1f;
    public float outputMax = 1f;

    public float GetValue(float time)
    {
        if (bypass) return 0f;

        float value = Evaluate(time);

        if (clampOutput)
            value = Mathf.Clamp(value, outputMin, outputMax);

        return value;
    }

    protected abstract float Evaluate(float time);

    protected override void ValidateModule()
    {
        if (outputMax < outputMin)
            outputMax = outputMin;
    }
}