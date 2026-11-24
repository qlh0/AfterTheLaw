using UnityEngine;

public class ConstantValueModule : ControlModuleBase
{
    [Header("Constant Value")]
    public float value = 0f;

    protected override float Evaluate(float time)
    {
        return value;
    }
}