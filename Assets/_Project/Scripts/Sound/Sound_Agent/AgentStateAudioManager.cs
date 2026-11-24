using System.Collections.Generic;
using UnityEngine;

public class AgentStateAudioManager : MonoBehaviour
{
    public AgentSpawner spawner;

    public enum AgentAudioState
    {
        Default,
        Joyful,
        Love,
        Lonely,
        Hurt
    }

    [System.Serializable]
    public class StateAudioChannel
    {
        public AgentAudioState state;

        [Header("4 Speakers")]
        public AudioSource speakerA;
        public AudioSource speakerB;
        public AudioSource speakerC;
        public AudioSource speakerD;

        [Header("Volume Mapping")]
        public float baseVolume = 0f;
        public float volumePerAgent = 0.02f;
        public float maxVolumePerSpeaker = 1f;

        [Header("Stereo Pan Mapping")]
        public bool useStereoPan = true;
        public float panStrength = 1f;
        public float speakerPanSpread = 0.2f;

        [Header("Pitch Mapping")]
        public bool usePitchMapping = false;
        public float minPitch = 0.95f;
        public float maxPitch = 1.05f;
        public float speakerPitchSpread = 0.03f;

        [HideInInspector] public int count;
        [HideInInspector] public Vector3 averagePosition;
        [HideInInspector] public float averageIntensity;
    }

    [Header("5 state groups × 4 speakers")]
    public StateAudioChannel defaultChannel;
    public StateAudioChannel joyfulChannel;
    public StateAudioChannel loveChannel;
    public StateAudioChannel lonelyChannel;
    public StateAudioChannel hurtChannel;

    [Header("Thresholds")]
    public float joyfulThreshold = 0.15f;
    public float lonelyThreshold = 0.15f;
    public float hurtThreshold = 0.15f;

    readonly Dictionary<AudioSource, bool> speakerActiveStates = new Dictionary<AudioSource, bool>();

    void Update()
    {
        if (spawner == null || spawner.worldState == null) return;

        ResetChannel(defaultChannel);
        ResetChannel(joyfulChannel);
        ResetChannel(loveChannel);
        ResetChannel(lonelyChannel);
        ResetChannel(hurtChannel);

        List<AgentData> agents = spawner.worldState.agents;

        foreach (var agent in agents)
        {
            if (agent == null || !agent.alive) continue;

            AgentAudioState state = GetState(agent);
            StateAudioChannel channel = GetChannel(state);
            if (channel == null) continue;

            channel.count++;
            channel.averagePosition += (Vector3)agent.position;
            channel.averageIntensity += GetIntensity(agent, state);
        }

        FinalizeChannel(defaultChannel);
        FinalizeChannel(joyfulChannel);
        FinalizeChannel(loveChannel);
        FinalizeChannel(lonelyChannel);
        FinalizeChannel(hurtChannel);

        ApplyAudio(defaultChannel);
        ApplyAudio(joyfulChannel);
        ApplyAudio(loveChannel);
        ApplyAudio(lonelyChannel);
        ApplyAudio(hurtChannel);
    }

    void ResetChannel(StateAudioChannel channel)
    {
        if (channel == null) return;

        channel.count = 0;
        channel.averagePosition = Vector3.zero;
        channel.averageIntensity = 0f;
    }

    void FinalizeChannel(StateAudioChannel channel)
    {
        if (channel == null) return;
        if (channel.count <= 0) return;

        channel.averagePosition /= channel.count;
        channel.averageIntensity /= channel.count;
    }

    void ApplyAudio(StateAudioChannel channel)
    {
        if (channel == null) return;

        AudioSource[] speakers = GetSpeakers(channel);

        int validSpeakerCount = 0;
        for (int i = 0; i < speakers.Length; i++)
        {
            if (speakers[i] != null)
                validSpeakerCount++;
        }

        if (validSpeakerCount == 0) return;

        bool shouldPlayThisState = channel.count > 0;

        float totalVolume = 0f;
        float perSpeakerVolume = 0f;

        if (shouldPlayThisState)
        {
            totalVolume = Mathf.Clamp(
                channel.baseVolume + channel.count * channel.volumePerAgent,
                0f,
                channel.maxVolumePerSpeaker * validSpeakerCount
            );

            perSpeakerVolume = validSpeakerCount > 0 ? totalVolume / validSpeakerCount : 0f;
            perSpeakerVolume = Mathf.Clamp(perSpeakerVolume, 0f, channel.maxVolumePerSpeaker);
        }

        float basePan = 0f;
        if (channel.useStereoPan && shouldPlayThisState && Camera.main != null)
        {
            Vector3 viewport = Camera.main.WorldToViewportPoint(channel.averagePosition);
            basePan = Mathf.Clamp((viewport.x - 0.5f) * 2f * channel.panStrength, -1f, 1f);
        }

        float basePitch = 1f;
        if (channel.usePitchMapping && shouldPlayThisState)
        {
            float t = Mathf.Clamp01(channel.averageIntensity);
            basePitch = Mathf.Lerp(channel.minPitch, channel.maxPitch, t);
        }

        ApplyToSpeaker(speakers[0], shouldPlayThisState, perSpeakerVolume, basePan - channel.speakerPanSpread * 1.5f, basePitch - channel.speakerPitchSpread * 1.5f);
        ApplyToSpeaker(speakers[1], shouldPlayThisState, perSpeakerVolume, basePan - channel.speakerPanSpread * 0.5f, basePitch - channel.speakerPitchSpread * 0.5f);
        ApplyToSpeaker(speakers[2], shouldPlayThisState, perSpeakerVolume, basePan + channel.speakerPanSpread * 0.5f, basePitch + channel.speakerPitchSpread * 0.5f);
        ApplyToSpeaker(speakers[3], shouldPlayThisState, perSpeakerVolume, basePan + channel.speakerPanSpread * 1.5f, basePitch + channel.speakerPitchSpread * 1.5f);
    }

    void ApplyToSpeaker(AudioSource source, bool shouldPlay, float volume, float pan, float pitch)
    {
        if (source == null) return;

        source.volume = shouldPlay ? volume : 0f;
        source.panStereo = Mathf.Clamp(pan, -1f, 1f);
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);

        AlphabetAudioPlayer alphabetPlayer = source.GetComponent<AlphabetAudioPlayer>();

        bool wasActive = false;
        speakerActiveStates.TryGetValue(source, out wasActive);

        if (shouldPlay)
        {
            if (!wasActive)
            {
                if (alphabetPlayer != null)
                    alphabetPlayer.PlayInspectorText();
                else if (!source.isPlaying)
                    source.Play();

                speakerActiveStates[source] = true;
            }
        }
        else
        {
            if (wasActive)
            {
                if (alphabetPlayer != null)
                    alphabetPlayer.StopText();
                else if (source.isPlaying)
                    source.Stop();

                speakerActiveStates[source] = false;
            }
        }
    }

    AudioSource[] GetSpeakers(StateAudioChannel channel)
    {
        return new AudioSource[]
        {
            channel.speakerA,
            channel.speakerB,
            channel.speakerC,
            channel.speakerD
        };
    }

    AgentAudioState GetState(AgentData agent)
    {
        if (agent.hurt >= hurtThreshold)
            return AgentAudioState.Hurt;

        if (agent.lonely >= lonelyThreshold)
            return AgentAudioState.Lonely;

        if (agent.joyful >= joyfulThreshold)
            return AgentAudioState.Joyful;

        if (agent.currentBehavior == BehaviorType.Love)
            return AgentAudioState.Love;

        return AgentAudioState.Default;
    }

    float GetIntensity(AgentData agent, AgentAudioState state)
    {
        switch (state)
        {
            case AgentAudioState.Joyful:
                return agent.joyful;
            case AgentAudioState.Love:
                return agent.joyful;
            case AgentAudioState.Lonely:
                return agent.lonely;
            case AgentAudioState.Hurt:
                return agent.hurt;
            default:
                return 0.5f;
        }
    }

    StateAudioChannel GetChannel(AgentAudioState state)
    {
        switch (state)
        {
            case AgentAudioState.Default: return defaultChannel;
            case AgentAudioState.Joyful: return joyfulChannel;
            case AgentAudioState.Love: return loveChannel;
            case AgentAudioState.Lonely: return lonelyChannel;
            case AgentAudioState.Hurt: return hurtChannel;
        }

        return null;
    }
}