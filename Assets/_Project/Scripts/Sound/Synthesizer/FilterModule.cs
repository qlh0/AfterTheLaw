using UnityEngine;

public class FilterModule : AudioModuleBase
{
    public enum FilterType
    {
        LowPass,
        HighPass
    }

    [Header("Audio Input")]
    public AudioModuleBase audioInput;

    [Header("Filter")]
    public FilterType filterType = FilterType.LowPass;
    [Min(1f)] public float cutoff = 1000f;
    [Range(1, 4)] public int stages = 2;

    [Header("Control Inputs")]
    public ControlModuleBase cutoffControl;
    public float cutoffControlAmount = 0f;

    float[] lowStateL;
    float[] lowStateR;

    float[] highInputL;
    float[] highInputR;
    float[] highOutputL;
    float[] highOutputR;

    protected override void OnModuleInit()
    {
        EnsureStateArrays();
    }

    protected override float EvaluateSample(double dspTime, int channel)
    {
        if (audioInput == null)
            return 0f;

        EnsureStateArrays();

        float input = audioInput.GetSample(dspTime, channel);

        float currentCutoff = cutoff;
        float t = (float)dspTime;

        if (cutoffControl != null)
            currentCutoff += cutoffControl.GetValue(t) * cutoffControlAmount;

        float sampleRate = AudioSettings.outputSampleRate;
        currentCutoff = Mathf.Clamp(currentCutoff, 1f, sampleRate * 0.45f);

        float dt = 1f / sampleRate;
        float rc = 1f / (2f * Mathf.PI * currentCutoff);
        float alphaLow = dt / (rc + dt);
        float alphaHigh = rc / (rc + dt);

        if (channel == 0)
        {
            if (filterType == FilterType.LowPass)
                return ProcessLowPass(input, alphaLow, lowStateL);
            else
                return ProcessHighPass(input, alphaHigh, highInputL, highOutputL);
        }
        else
        {
            if (filterType == FilterType.LowPass)
                return ProcessLowPass(input, alphaLow, lowStateR);
            else
                return ProcessHighPass(input, alphaHigh, highInputR, highOutputR);
        }
    }

    float ProcessLowPass(float input, float alpha, float[] state)
    {
        float x = input;

        for (int i = 0; i < stages; i++)
        {
            state[i] = state[i] + alpha * (x - state[i]);
            x = state[i];
        }

        return x;
    }

    float ProcessHighPass(float input, float alpha, float[] prevInput, float[] prevOutput)
    {
        float x = input;

        for (int i = 0; i < stages; i++)
        {
            float y = alpha * (prevOutput[i] + x - prevInput[i]);
            prevInput[i] = x;
            prevOutput[i] = y;
            x = y;
        }

        return x;
    }

    void EnsureStateArrays()
    {
        int size = Mathf.Clamp(stages, 1, 4);

        if (lowStateL == null || lowStateL.Length != size) lowStateL = new float[size];
        if (lowStateR == null || lowStateR.Length != size) lowStateR = new float[size];

        if (highInputL == null || highInputL.Length != size) highInputL = new float[size];
        if (highInputR == null || highInputR.Length != size) highInputR = new float[size];

        if (highOutputL == null || highOutputL.Length != size) highOutputL = new float[size];
        if (highOutputR == null || highOutputR.Length != size) highOutputR = new float[size];
    }

    public override void ResetState()
    {
        EnsureStateArrays();

        for (int i = 0; i < lowStateL.Length; i++) lowStateL[i] = 0f;
        for (int i = 0; i < lowStateR.Length; i++) lowStateR[i] = 0f;

        for (int i = 0; i < highInputL.Length; i++) highInputL[i] = 0f;
        for (int i = 0; i < highInputR.Length; i++) highInputR[i] = 0f;

        for (int i = 0; i < highOutputL.Length; i++) highOutputL[i] = 0f;
        for (int i = 0; i < highOutputR.Length; i++) highOutputR[i] = 0f;
    }

    protected override void ValidateModule()
    {
        base.ValidateModule();
        cutoff = Mathf.Max(1f, cutoff);
        stages = Mathf.Clamp(stages, 1, 4);
        EnsureStateArrays();
    }
}