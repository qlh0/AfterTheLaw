using System.Collections.Generic;
using UnityEngine;

public class TreeAudioManager : MonoBehaviour
{
    public enum TreeType
    {
        A,
        B,
        C
    }

    [System.Serializable]
    public class TreeAudioChannel
    {
        public TreeType treeType;
        public AudioSource audioSource;

        [Header("Volume Mapping")]
        public float baseVolume = 0f;
        public float volumePerTree = 0.03f;
        public float maxVolume = 1f;

        [Header("Stereo Pan Mapping")]
        public bool useStereoPan = true;
        public float panStrength = 1f;

        [HideInInspector] public int count;
        [HideInInspector] public Vector3 averagePosition;
        [HideInInspector] public float averageScale;
    }

    [Header("Where spawned trees live")]
    public Transform foodsParent;

    [Header("3 global speakers")]
    public TreeAudioChannel channelA;
    public TreeAudioChannel channelB;
    public TreeAudioChannel channelC;

    readonly List<TreeAudioMarker> markersBuffer = new List<TreeAudioMarker>();

    void Update()
    {
        CollectTreeStats();
        ApplyAudio(channelA);
        ApplyAudio(channelB);
        ApplyAudio(channelC);
    }

    void CollectTreeStats()
    {
        ResetChannel(channelA);
        ResetChannel(channelB);
        ResetChannel(channelC);

        markersBuffer.Clear();

        if (foodsParent == null)
            return;

        foodsParent.GetComponentsInChildren(true, markersBuffer);

        foreach (var marker in markersBuffer)
        {
            if (marker == null) continue;
            if (!marker.gameObject.activeInHierarchy) continue;

            TreeAudioChannel channel = GetChannel(marker.treeType);
            if (channel == null) continue;

            channel.count++;
            channel.averagePosition += marker.transform.position;
            channel.averageScale += GetWorldScale(marker.transform);
        }

        FinalizeChannel(channelA);
        FinalizeChannel(channelB);
        FinalizeChannel(channelC);
    }

    void ResetChannel(TreeAudioChannel channel)
    {
        if (channel == null) return;

        channel.count = 0;
        channel.averagePosition = Vector3.zero;
        channel.averageScale = 0f;
    }

    void FinalizeChannel(TreeAudioChannel channel)
    {
        if (channel == null) return;
        if (channel.count <= 0) return;

        channel.averagePosition /= channel.count;
        channel.averageScale /= channel.count;
    }

    void ApplyAudio(TreeAudioChannel channel)
    {
        if (channel == null || channel.audioSource == null) return;

        float targetVolume = Mathf.Clamp(
            channel.baseVolume + channel.count * channel.volumePerTree,
            0f,
            channel.maxVolume
        );

        channel.audioSource.volume = targetVolume;

        if (channel.useStereoPan)
        {
            float pan = 0f;

            if (channel.count > 0 && Camera.main != null)
            {
                Vector3 viewport = Camera.main.WorldToViewportPoint(channel.averagePosition);
                pan = Mathf.Clamp((viewport.x - 0.5f) * 2f * channel.panStrength, -1f, 1f);
            }

            channel.audioSource.panStereo = pan;
        }
    }

    TreeAudioChannel GetChannel(TreeType type)
    {
        switch (type)
        {
            case TreeType.A: return channelA;
            case TreeType.B: return channelB;
            case TreeType.C: return channelC;
        }

        return null;
    }

    float GetWorldScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return (s.x + s.y + s.z) / 3f;
    }
}