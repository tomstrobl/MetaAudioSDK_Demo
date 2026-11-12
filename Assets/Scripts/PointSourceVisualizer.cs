using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// "Ember soul" look for a grabbable point source: a glowing core that pulses with how loud the
    /// source is right now, a halo, two orbiting rune rings, drifting embers, and ripples that radiate
    /// out from where the sound is emitted. Brightens while a hand hovers over or holds it.
    /// The core doubles as the grab handle (see <see cref="FitGrabCollider"/>).
    /// Everything is unlit and additive, so it costs no realtime lights.
    /// </summary>
    public class PointSourceVisualizer : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Tooltip("The point source. Its clip must use Load Type 'Decompress On Load' so its loudness can be read.")]
        [SerializeField] AudioSource source;

        [Header("Parts")]
        [SerializeField] Renderer core;
        [SerializeField] Renderer halo;
        [SerializeField] Transform innerRing, outerRing;
        [SerializeField] ParticleSystem embers;
        [SerializeField] Mesh rippleMesh;
        [SerializeField] Material rippleMaterial;

        [Header("Loudness")]
        [SerializeField, Range(0.01f, 0.2f)] float windowSeconds = 0.05f;
        [SerializeField, Min(0f)] float attackRate = 25f;
        [SerializeField, Min(0f)] float releaseRate = 4f;

        [Header("Response")]
        [Tooltip("Quest renders without HDR, so values much above 1 clip to flat white and lose the red rim.")]
        [SerializeField] float coreIdle = 0.55f, coreLoud = 1.0f, coreHover = 0.25f;
        [SerializeField] float haloIdle = 0.35f, haloLoud = 1.2f;
        [Tooltip("How much the core swells at full loudness (fraction of its size).")]
        [SerializeField, Range(0f, 1f)] float corePulse = 0.25f;
        [SerializeField] Vector2 ringSpeedDegrees = new(40f, 160f);
        [SerializeField] Vector2 emberRate = new(3f, 30f);

        [Header("Ripples")]
        [SerializeField, Min(0.1f)] float rippleLifetime = 1.6f;
        [SerializeField, Min(0.01f)] float rippleMaxRadius = 1.1f;
        [Tooltip("Minimum time between ripples.")]
        [SerializeField, Min(0.05f)] float rippleInterval = 0.45f;
        [Tooltip("A ripple is emitted when the loudness rises this far above its recent average.")]
        [SerializeField, Range(0f, 1f)] float rippleOnset = 0.12f;
        [SerializeField, Range(0f, 1f)] float rippleMinLevel = 0.2f;
        [SerializeField, Min(0f)] float rippleIntensity = 1.6f;

        readonly List<IInteractableView> interactables = new();
        readonly Dictionary<IInteractableView, InteractableState> states = new();
        readonly List<Ripple> ripples = new();
        MaterialPropertyBlock block;
        readonly ClipLevelMeter meter = new();
        float level, average, interaction, sinceRipple;
        Vector3 coreScale;
        Transform rippleRoot;

        class Ripple
        {
            public Transform Transform;
            public Renderer Renderer;
            public float Age = float.MaxValue, Strength;
        }

        public AudioSource Source { get => source; set => source = value; }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (core != null)
            {
                coreScale = core.transform.localScale;
                FitGrabCollider();
            }
        }

        // The core is what you grab: a sphere collider on it (part of the source's rigidbody) is fitted to its
        // mesh, so the grab volume matches what you see and pulses along with it.
        void FitGrabCollider()
        {
            var collider = core.GetComponent<SphereCollider>();
            var mesh = core.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (collider == null || mesh == null) return;
            var bounds = mesh.bounds;
            collider.center = bounds.center;
            collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        void OnEnable()
        {
            // The grab interactables live on the point source object (or its children).
            var owner = source != null ? source.transform : transform;
            foreach (var view in owner.GetComponentsInChildren<IInteractableView>(true))
            {
                interactables.Add(view);
                states[view] = view.State;
                view.WhenStateChanged += OnStateChanged;
            }
        }

        void OnDisable()
        {
            foreach (var view in interactables) view.WhenStateChanged -= OnStateChanged;
            interactables.Clear();
            states.Clear();
        }

        void OnDestroy()
        {
            if (rippleRoot != null) Destroy(rippleRoot.gameObject);
        }

        void OnStateChanged(InteractableStateChangeArgs args)
        {
            foreach (var view in interactables) states[view] = view.State;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            meter.WindowSeconds = windowSeconds;
            float target = meter.Measure(source, dt);
            level += (target - level) * (1f - Mathf.Exp(-(target > level ? attackRate : releaseRate) * dt));

            float interactionTarget = 0f;
            foreach (var s in states.Values)
                interactionTarget = Mathf.Max(interactionTarget, s == InteractableState.Select ? 1f : s == InteractableState.Hover ? 0.6f : 0f);
            interaction += (interactionTarget - interaction) * (1f - Mathf.Exp(-12f * dt));

            Apply(level, interaction, dt);

            // Ripples on onsets (the loudness jumping above its recent average), rate-limited.
            sinceRipple += dt;
            average += (level - average) * (1f - Mathf.Exp(-2f * dt));
            if (sinceRipple >= rippleInterval && level > rippleMinLevel && level - average > rippleOnset)
            {
                EmitRipple(level);
                sinceRipple = 0f;
            }
            UpdateRipples(dt);
        }

        /// <summary>Shows a representative active state without playing audio (for editor previews).</summary>
        public void Preview(float previewLevel)
        {
            block ??= new MaterialPropertyBlock();
            if (core != null && coreScale == Vector3.zero) coreScale = core.transform.localScale;
            Apply(previewLevel, 0f, 0f);
            if (embers != null) embers.Simulate(2f, true, true);
        }

        /// <summary>Restores the authored state after <see cref="Preview"/>.</summary>
        public void ClearPreview()
        {
            foreach (var r in new[] { core, halo }) if (r != null) r.SetPropertyBlock(null);
            if (core != null && coreScale != Vector3.zero) core.transform.localScale = coreScale;
            if (embers != null)
            {
                embers.Clear(true);
                var emission = embers.emission;
                emission.rateOverTime = emberRate.x;
            }
        }

        void Apply(float l, float hover, float dt)
        {
            if (core != null)
            {
                block.Clear();
                block.SetFloat(IntensityId, Mathf.Lerp(coreIdle, coreLoud, l) + coreHover * hover);
                core.SetPropertyBlock(block);
                core.transform.localScale = coreScale * (1f + corePulse * l + 0.1f * hover);
            }
            if (halo != null)
            {
                block.Clear();
                block.SetFloat(IntensityId, Mathf.Lerp(haloIdle, haloLoud, l) + 0.5f * hover);
                halo.SetPropertyBlock(block);
            }
            float speed = Mathf.Lerp(ringSpeedDegrees.x, ringSpeedDegrees.y, l) * dt;
            if (innerRing != null) innerRing.Rotate(0f, speed, 0f, Space.Self);
            if (outerRing != null) outerRing.Rotate(speed * 0.7f, -speed * 0.45f, 0f, Space.Self);
            if (embers != null)
            {
                var emission = embers.emission;
                emission.rateOverTime = Mathf.Lerp(emberRate.x, emberRate.y, l);
            }
        }

        // ---------- ripples ----------

        void EmitRipple(float strength)
        {
            if (rippleMesh == null || rippleMaterial == null) return;
            if (rippleRoot == null)
            {
                // Ripples stay where they were emitted, even when the source is carried or thrown.
                rippleRoot = new GameObject($"{name} Ripples").transform;
            }
            Ripple r = null;
            foreach (var x in ripples) if (x.Age >= rippleLifetime) { r = x; break; }
            if (r == null)
            {
                if (ripples.Count >= 4) return;
                var go = new GameObject("Ripple");
                go.transform.SetParent(rippleRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = rippleMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rippleMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r = new Ripple { Transform = go.transform, Renderer = mr };
                ripples.Add(r);
            }
            r.Age = 0f;
            r.Strength = strength;
            r.Transform.position = transform.position;
            r.Renderer.enabled = true;
        }

        void UpdateRipples(float dt)
        {
            foreach (var r in ripples)
            {
                if (r.Age >= rippleLifetime) { if (r.Renderer.enabled) r.Renderer.enabled = false; continue; }
                r.Age += dt;
                float t = Mathf.Clamp01(r.Age / rippleLifetime);
                float radius = Mathf.Lerp(0.08f, rippleMaxRadius, 1f - (1f - t) * (1f - t)); // fast, then easing out
                r.Transform.localScale = Vector3.one * (radius / Mathf.Max(rippleMesh.bounds.extents.x, 1e-4f)); // any sphere mesh
                block.Clear();
                block.SetFloat(IntensityId, rippleIntensity * r.Strength * (1f - t) * (1f - t));
                r.Renderer.SetPropertyBlock(block);
            }
        }
    }
}
