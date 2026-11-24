using UnityEngine;

public class LoveTone : MonoBehaviour
{
    [Header("Tempo / 12-TET")]
    [Range(40f, 180f)] public float bpm = 72f;
    [Range(1, 8)] public int stepsPerBeat = 2;
    [Range(36, 84)] public int rootMidiNote = 60; // C4 = 60

    [Header("Melody / Harmony")]
    [Range(0f, 1f)] public float melodyAmount = 0.75f;
    [Range(0f, 1f)] public float harmonyAmount = 0.45f;
    [Range(0f, 1f)] public float subAmount = 0.12f;

    [Header("Tone Color")]
    [Range(0f, 1f)] public float sineAmount = 0.72f;
    [Range(0f, 1f)] public float softHarmonicAmount = 0.22f;
    [Range(0f, 1f)] public float airyNoiseAmount = 0.035f;
    [Range(0f, 1f)] public float vocalFormantAmount = 0.18f;

    [Header("Expression")]
    [Range(0f, 0.05f)] public float vibratoDepth = 0.008f;
    [Range(0.1f, 8f)] public float vibratoRate = 4.8f;
    [Range(0f, 0.02f)] public float driftAmount = 0.004f;
    [Range(0.01f, 2f)] public float driftRate = 0.25f;
    [Range(0f, 0.5f)] public float tremoloDepth = 0.05f;
    [Range(0.05f, 8f)] public float tremoloRate = 0.7f;

    [Header("Note Envelope")]
    [Range(0.001f, 0.2f)] public float attackTime = 0.015f;
    [Range(0.01f, 0.6f)] public float releaseTime = 0.18f;
    [Range(0.1f, 1f)] public float sustainLevel = 0.78f;
    [Range(0f, 1f)] public float noteGap = 0.08f;

    [Header("Soft Clip")]
    [Range(0.1f, 4f)] public float softClipDrive = 1.35f;

    double phaseMelody;
    double phaseHarmony;
    double phaseSub;
    float noiseState;

    // 4-chord loop in semitones relative to root
    readonly int[] chordRoots = { 0, 9, 5, 7 }; // I - vi - IV - V
    // chord tones relative to chord root
    readonly int[][] chordToneSets =
    {
        new[] { 0, 4, 7, 11 }, // maj7
        new[] { 0, 3, 7, 10 }, // min7
        new[] { 0, 4, 7, 11 }, // maj7
        new[] { 0, 2, 7, 10 }  // sus2 7
    };

    // step melody choices within bar, in chord-tone indices
    readonly int[] melodyPattern = { 0, 2, 1, 3, 2, 1, 0, 1 };
    readonly int[] harmonyPattern = { 2, 1, 3, 2, 1, 0, 2, 3 };

    public float GenerateSample(double dspTime, int sampleRate)
    {
        float t = (float)dspTime;

        float beatsPerSecond = bpm / 60f;
        float stepsPerSecond = beatsPerSecond * Mathf.Max(1, stepsPerBeat);
        float stepDuration = 1f / stepsPerSecond;

        int globalStep = Mathf.FloorToInt(t / stepDuration);
        float stepPhase = (t % stepDuration) / stepDuration; // 0..1 within current step

        int stepsPerBar = 4 * Mathf.Max(1, stepsPerBeat);
        int barIndex = Mathf.FloorToInt(globalStep / (float)stepsPerBar) % chordRoots.Length;
        int stepInBar = globalStep % stepsPerBar;

        int[] chordSet = chordToneSets[barIndex];
        int chordRoot = chordRoots[barIndex];

        int melodyToneIndex = melodyPattern[stepInBar % melodyPattern.Length] % chordSet.Length;
        int harmonyToneIndex = harmonyPattern[stepInBar % harmonyPattern.Length] % chordSet.Length;

        int melodyMidi = rootMidiNote + chordRoot + chordSet[melodyToneIndex];
        int harmonyMidi = rootMidiNote + chordRoot + chordSet[harmonyToneIndex] - 12;
        int subMidi = melodyMidi - 12;

        float drift = (Mathf.PerlinNoise(13.7f, t * driftRate) * 2f - 1f) * driftAmount;
        float vibrato = Mathf.Sin(t * vibratoRate * Mathf.PI * 2f) * vibratoDepth;

        float melodyFreq = MidiToFrequency(melodyMidi) * (1f + drift + vibrato);
        float harmonyFreq = MidiToFrequency(harmonyMidi) * (1f + drift * 0.6f + vibrato * 0.7f);
        float subFreq = MidiToFrequency(subMidi) * (1f + drift * 0.4f);

        phaseMelody += melodyFreq * Mathf.PI * 2.0 / sampleRate;
        phaseHarmony += harmonyFreq * Mathf.PI * 2.0 / sampleRate;
        phaseSub += subFreq * Mathf.PI * 2.0 / sampleRate;

        if (phaseMelody > Mathf.PI * 2.0) phaseMelody -= Mathf.PI * 2.0;
        if (phaseHarmony > Mathf.PI * 2.0) phaseHarmony -= Mathf.PI * 2.0;
        if (phaseSub > Mathf.PI * 2.0) phaseSub -= Mathf.PI * 2.0;

        float env = StepEnvelope(stepPhase);

        float melSine = Mathf.Sin((float)phaseMelody);
        float melSoft = Mathf.Sin((float)(phaseMelody * 2.0)) * 0.4f;
        float harmSine = Mathf.Sin((float)phaseHarmony);
        float subSine = Mathf.Sin((float)phaseSub);

        // very light "human-ish" vowel/formant feel
        float formant1 = Mathf.Sin((float)(phaseMelody * 3.1)) * 0.22f;
        float formant2 = Mathf.Sin((float)(phaseMelody * 5.2)) * 0.12f;
        float vocalColor = (formant1 + formant2);

        float white = Random.Range(-1f, 1f);
        noiseState = Mathf.Lerp(noiseState, white, 0.06f);
        float airy = noiseState;

        float tremolo = 1f - tremoloDepth + tremoloDepth * (0.5f + 0.5f * Mathf.Sin(t * tremoloRate * Mathf.PI * 2f));

        float melodyVoice =
            melSine * sineAmount +
            melSoft * softHarmonicAmount +
            vocalColor * vocalFormantAmount;

        float harmonyVoice =
            harmSine * (sineAmount * 0.85f) +
            Mathf.Sin((float)(phaseHarmony * 2.0)) * (softHarmonicAmount * 0.35f);

        float sample =
            melodyVoice * melodyAmount +
            harmonyVoice * harmonyAmount +
            subSine * subAmount +
            airy * airyNoiseAmount;

        sample *= env * tremolo;
        sample = (float)System.Math.Tanh(sample * softClipDrive);

        return sample;
    }

    float StepEnvelope(float stepPhase)
    {
        float playableEnd = 1f - noteGap;
        if (stepPhase >= playableEnd) return 0f;

        float local = stepPhase / Mathf.Max(0.0001f, playableEnd);

        if (local < attackTime)
            return Mathf.Clamp01(local / Mathf.Max(0.0001f, attackTime));

        if (local > 1f - releaseTime)
        {
            float relT = (local - (1f - releaseTime)) / Mathf.Max(0.0001f, releaseTime);
            return Mathf.Lerp(sustainLevel, 0f, relT);
        }

        return sustainLevel;
    }

    float MidiToFrequency(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}