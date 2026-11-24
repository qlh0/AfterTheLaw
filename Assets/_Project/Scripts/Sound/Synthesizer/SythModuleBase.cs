using UnityEngine;

public abstract class SynthModuleBase : MonoBehaviour
{
    [Header("Module Base")]
    public bool bypass = false;

    [SerializeField] protected string moduleId = "Module";

    public virtual string ModuleId
    {
        get => moduleId;
    }

    public virtual bool IsBypassed
    {
        get => bypass;
    }

    protected virtual void Awake()
    {
        OnModuleInit();
    }

    protected virtual void OnEnable()
    {
        OnModuleEnable();
    }

    protected virtual void OnDisable()
    {
        OnModuleDisable();
    }

    protected virtual void OnValidate()
    {
        ValidateModule();
    }

    protected virtual void OnModuleInit() { }

    protected virtual void OnModuleEnable() { }

    protected virtual void OnModuleDisable() { }

    protected virtual void ValidateModule() { }

    public virtual void ResetState() { }
}