using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// How loud an AudioSource is right now (0..1), read from its clip's samples at the playback position and
    /// normalised against a slowly decaying peak, so quiet passages still read as quiet. Needs the clip's
    /// Load Type to be "Decompress On Load". Follows clip changes.
    /// </summary>
    public sealed class ClipLevelMeter
    {
        public float WindowSeconds = 0.05f;
        public float PeakDecaySeconds = 8f;

        float[] samples;
        int channels, frames, frequency;
        float peak = 1e-6f;
        AudioClip loaded;
        bool warned;

        public float Measure(AudioSource source, float dt)
        {
            if (source == null || !source.isPlaying || source.clip == null) return 0f;
            if (source.clip != loaded && !Load(source.clip)) return 0f;
            int window = Mathf.Max(1, Mathf.RoundToInt(WindowSeconds * frequency));
            int end = source.timeSamples;
            double sum = 0;
            for (int i = 0; i < window; i++)
            {
                int f = end - i;
                if (f < 0) f += frames; // the clip loops
                int o = f * channels;
                for (int c = 0; c < channels; c++) sum += samples[o + c] * samples[o + c];
            }
            float energy = (float)(sum / (window * channels));
            peak = Mathf.Max(energy, peak * Mathf.Exp(-dt / PeakDecaySeconds));
            return Mathf.Clamp01(Mathf.Sqrt(energy / Mathf.Max(peak, 1e-9f)));
        }

        bool Load(AudioClip clip)
        {
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                if (!warned) Debug.LogWarning($"[ClipLevelMeter] '{clip.name}' needs Load Type 'Decompress On Load' to be measured.");
                warned = true;
                return false;
            }
            if (clip.loadState != AudioDataLoadState.Loaded) { clip.LoadAudioData(); return false; }
            channels = clip.channels;
            frames = clip.samples;
            frequency = clip.frequency;
            samples = new float[frames * channels];
            if (!clip.GetData(samples, 0)) { samples = null; return false; }
            loaded = clip;
            peak = 1e-6f;
            return true;
        }
    }
}
