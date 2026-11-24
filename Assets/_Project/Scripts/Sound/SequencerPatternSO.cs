using UnityEngine;

[CreateAssetMenu(fileName = "NewSequencerPattern", menuName = "Audio/Sequencer Pattern")]
public class SequencerPatternSO : ScriptableObject
{
    [Header("Tempo")]
    [Range(10f, 200f)] public float bpm = 72f;
    [Range(1, 8)] public int stepsPerBeat = 1;

    [Header("Pitch Range")]
    [Range(36, 84)] public int minMidiNote = 48; // C3
    [Range(36, 84)] public int maxMidiNote = 72; // C5

    [Header("Pattern")]
    public StepData[] steps = new StepData[16];

    void OnEnable()
    {
        if (steps == null || steps.Length != 16)
            steps = new StepData[16];

        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == null)
                steps[i] = new StepData();
        }
    }
}

[System.Serializable]
public class StepData
{
    public bool enabled = true;
    [Range(36, 84)] public int midiNote = 60;
    [Range(0f, 1f)] public float velocity = 1f;
}