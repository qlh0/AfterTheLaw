using UnityEngine;

public class TreeBirdBurstTrigger : MonoBehaviour
{
    [Header("Refs")]
    public ParticleSystem birdBurstEffect;

    [Header("Tree Type")]
    public BirdBurstAudioManager.BirdTreeType treeType;

    void Start()
    {
        if (birdBurstEffect != null)
        {
            var main = birdBurstEffect.main;
            main.playOnAwake = false;
            birdBurstEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public void TriggerBurst()
    {
        if (birdBurstEffect != null)
        {
            birdBurstEffect.Play();
        }

        if (BirdBurstAudioManager.Instance != null)
        {
            BirdBurstAudioManager.Instance.PlayBirdBurstAt(transform.position, treeType);
        }
    }
}