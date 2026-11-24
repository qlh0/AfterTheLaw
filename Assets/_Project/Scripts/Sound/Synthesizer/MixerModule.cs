using System.Collections.Generic;
using UnityEngine;

public class MixerModule : AudioModuleBase
{
    [System.Serializable]
    public class MixerInput
    {
        public AudioModuleBase source;
        [Range(0f, 1f)] public float level = 0.5f;
        public bool mute = false;
        public bool solo = false;
    }

    [Header("Mixer Inputs")]
    public List<MixerInput> inputs = new List<MixerInput>();

    [Header("Master")]
    [Range(0f, 2f)] public float masterGain = 1f;

    [Header("Mix Behavior")]
    public bool normalizeByActiveInputs = true;
    public bool useHeadroom = true;
    [Range(0.1f, 2f)] public float headroomScale = 0.8f;
    public bool softClipOutput = true;

    [Header("Control Inputs")]
    public ControlModuleBase masterGainControl;
    public float masterGainControlAmount = 0f;

    protected override float EvaluateSample(double dspTime, int channel)
    {
        if (inputs == null || inputs.Count == 0)
            return 0f;

        bool hasSolo = HasSoloInput();

        float mixed = 0f;
        int activeCount = 0;
        float levelSum = 0f;

        for (int i = 0; i < inputs.Count; i++)
        {
            MixerInput input = inputs[i];
            if (input == null || input.source == null) continue;
            if (input.mute) continue;
            if (hasSolo && !input.solo) continue;

            float level = Mathf.Clamp01(input.level);
            if (level <= 0f) continue;

            mixed += input.source.GetSample(dspTime, channel) * level;
            levelSum += level;
            activeCount++;
        }

        if (activeCount == 0)
            return 0f;

        if (normalizeByActiveInputs)
        {
            float divisor = Mathf.Max(1f, levelSum);
            mixed /= divisor;
        }

        if (useHeadroom)
            mixed *= headroomScale;

        float currentMasterGain = Mathf.Max(0f, masterGain);
        float t = (float)dspTime;

        if (masterGainControl != null)
            currentMasterGain += masterGainControl.GetValue(t) * masterGainControlAmount;

        currentMasterGain = Mathf.Max(0f, currentMasterGain);
        mixed *= currentMasterGain;

        if (softClipOutput)
            mixed = SoftClip(mixed);

        return mixed;
    }

    bool HasSoloInput()
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            MixerInput input = inputs[i];
            if (input != null && input.source != null && input.solo)
                return true;
        }

        return false;
    }

    float SoftClip(float x)
    {
        return x / (1f + Mathf.Abs(x));
    }

    protected override void ValidateModule()
    {
        base.ValidateModule();

        masterGain = Mathf.Clamp(masterGain, 0f, 2f);
        headroomScale = Mathf.Clamp(headroomScale, 0.1f, 2f);

        if (inputs == null) return;

        for (int i = 0; i < inputs.Count; i++)
        {
            MixerInput input = inputs[i];
            if (input == null) continue;

            input.level = Mathf.Clamp01(input.level);
        }
    }
}