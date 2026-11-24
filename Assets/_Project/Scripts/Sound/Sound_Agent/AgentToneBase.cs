using UnityEngine;

public abstract class AgentToneBase : MonoBehaviour
{
    public abstract float GenerateSample(double dspTime, int sampleRate);
}