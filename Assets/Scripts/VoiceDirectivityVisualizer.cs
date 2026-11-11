using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// Shows which way a voice faces: glowing motes stream out of the mouth while the source speaks, their
    /// directions drawn from a human voice's directivity pattern (most go forward, fewer to the sides, very few
    /// backwards, and those start dimmer). Motion reads as direction from any viewing angle, even straight on.
    /// A small glow marks the mouth. Unlit and additive: no realtime lights.
    /// </summary>
    [ExecuteAlways]
    public class VoiceDirectivityVisualizer : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] AudioSource source;
        [Tooltip("World-space particle system the motes are emitted into (its own emission is off).")]
        [SerializeField] ParticleSystem particles;
        [SerializeField] Renderer mouthGlow;

        [Header("Directivity")]
        [Tooltip("Relative level straight behind the speaker (a real voice is quieter behind you, not silent).")]
        [SerializeField, Range(0f, 1f)] float behind = 0.12f;
        [Tooltip("Higher values concentrate the motes more towards the front.")]
        [SerializeField, Range(0.5f, 8f)] float focus = 3f;

        [Header("Motes")]
        [SerializeField] Vector2 motesPerSecond = new(1.5f, 40f);
        [SerializeField] Vector2 speed = new(0.6f, 1.1f);
        [SerializeField] Vector2 lifetime = new(1.2f, 1.8f);
        [SerializeField] Vector2 size = new(0.02f, 0.045f);
        [SerializeField] Color color = new(0.6f, 0.85f, 1f, 1f);

        [Header("Response")]
        [SerializeField] float glowIdle = 0.25f, glowLoud = 1.2f;
        [SerializeField, Min(0f)] float attackRate = 20f, releaseRate = 3f;
        [Tooltip("Loudness shown in the editor (not playing).")]
        [SerializeField, Range(0f, 1f)] float editorLevel = 0.6f;

        readonly ClipLevelMeter meter = new();
        MaterialPropertyBlock block;
        float level, pending;

        public AudioSource Source { get => source; set => source = value; }

        void Update()
        {
            if (!Application.isPlaying) { ApplyGlow(editorLevel); return; }
            float dt = Time.deltaTime;
            float target = meter.Measure(source, dt);
            level += (target - level) * (1f - Mathf.Exp(-(target > level ? attackRate : releaseRate) * dt));
            ApplyGlow(level);
            Emit(level, dt);
        }

        /// <summary>Shows the motes and glow at a given loudness (for editor previews): simulates a moment of speech.</summary>
        public void Preview(float previewLevel)
        {
            ApplyGlow(previewLevel);
            if (particles == null) return;
            particles.Clear(true);
            const float step = 1f / 30f;
            for (int i = 0; i < 60; i++)
            {
                Emit(previewLevel, step);
                particles.Simulate(step, true, false, false);
            }
        }

        public void ClearPreview()
        {
            if (particles != null) particles.Clear(true);
        }

        void Emit(float l, float dt)
        {
            if (particles == null) return;
            pending += Mathf.Lerp(motesPerSecond.x, motesPerSecond.y, l) * dt;
            var origin = mouthGlow != null ? mouthGlow.transform.position : transform.position;
            while (pending >= 1f)
            {
                pending -= 1f;
                var local = SampleDirection(out float p);
                var e = new ParticleSystem.EmitParams
                {
                    position = origin + Random.insideUnitSphere * 0.02f,
                    velocity = transform.TransformDirection(local) * Random.Range(speed.x, speed.y),
                    startLifetime = Random.Range(lifetime.x, lifetime.y),
                    startSize = Random.Range(size.x, size.y),
                    // Motes going where the voice is weak start dimmer.
                    startColor = new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(behind, 1f, p))),
                };
                particles.Emit(e, 1);
            }
        }

        /// <summary>A direction in local space with probability proportional to the directivity pattern (rejection sampling).</summary>
        Vector3 SampleDirection(out float p)
        {
            for (int i = 0; i < 32; i++)
            {
                var d = Random.onUnitSphere;
                p = Pattern(Mathf.Acos(Mathf.Clamp(d.z, -1f, 1f)));
                if (Random.value <= p) return d;
            }
            p = 1f;
            return Vector3.forward;
        }

        /// <summary>Relative level in a direction at angle theta from straight ahead (local +Z).</summary>
        float Pattern(float theta)
        {
            float cardioid = 0.5f + 0.5f * Mathf.Cos(theta);
            return behind + (1f - behind) * Mathf.Pow(cardioid, focus);
        }

        void ApplyGlow(float l)
        {
            if (mouthGlow == null) return;
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetFloat(IntensityId, Mathf.Lerp(glowIdle, glowLoud, l));
            mouthGlow.SetPropertyBlock(block);
        }
    }
}
