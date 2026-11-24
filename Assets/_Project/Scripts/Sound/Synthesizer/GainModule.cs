using UnityEngine;

public class GainModule : AudioModuleBase
{
    [Header("Audio Input")]
    public AudioModuleBase audioInput;

    [Header("Gain")]
    public float gain = 1f;

    [Header("Control Inputs")]
    public ControlModuleBase gainControl;
    public float gainControlAmount = 0f;

    protected override float EvaluateSample(double dspTime, int channel)
    {
        if (audioInput == null)
            return 0f;

        float currentGain = gain;
        float t = (float)dspTime;

        if (gainControl != null)
            currentGain += gainControl.GetValue(t) * gainControlAmount;

        float inputSample = audioInput.GetSample(dspTime, channel);
        return inputSample * currentGain;
    }

    protected override void ValidateModule()
    {
        base.ValidateModule();
    }
}