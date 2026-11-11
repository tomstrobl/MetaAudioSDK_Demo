using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// Visualizes where sound is coming from in a first-order ambisonic (ambiX: ACN channel order,
    /// SN3D normalization) clip. A sphere of virtual directional microphones is "pointed" at the
    /// sound field every frame; each vertex bulges out and glows by how much energy arrives
    /// from its direction, so the globe leans towards the brook, flashes where a bird calls, etc.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AmbisonicSoundfieldVisualizer : MonoBehaviour
    {
        [Tooltip("The AudioSource playing the ambisonic clip. Its rotation defines the orientation of the sound field.")]
        [SerializeField] AudioSource source;

        [Header("Shape")]
        [SerializeField, Range(1, 5)] int subdivisions = 4;
        [SerializeField, Min(0.01f)] float radius = 0.4f;
        [Tooltip("Extra radius, as a fraction of the base radius, in the loudest direction.")]
        [SerializeField, Range(0f, 2f)] float bulge = 0.75f;

        [Header("Analysis")]
        [Tooltip("Length of the analysis window in seconds.")]
        [SerializeField, Range(0.01f, 0.2f)] float windowSeconds = 0.05f;
        [Tooltip("Virtual microphone pattern: 0 = omni, 0.5 = cardioid, 0.75 = hypercardioid, 1 = figure-8.")]
        [SerializeField, Range(0f, 1f)] float directivity = 0.75f;
        [Tooltip("Higher values make the loudest directions stand out more against the rest.")]
        [SerializeField, Range(0.5f, 6f)] float contrast = 1.5f;
        [SerializeField, Min(0f)] float attackRate = 30f;
        [SerializeField, Min(0f)] float releaseRate = 5f;

        [Header("Look")]
        [SerializeField] Gradient colors = DefaultGradient();
        [SerializeField, Range(0f, 1f)] float idleAlpha = 0.03f;
        [SerializeField, Range(0f, 1f)] float peakAlpha = 0.9f;

        Mesh mesh;
        Vector3[] directions; // unit vectors in this transform's local space
        Vector3[] vertices;
        Color[] vertexColors;
        float[] smoothed;     // 0..1 per vertex
        float[] samples;      // interleaved 4-channel clip data
        int channels;
        float levelPeak = 1e-6f;
        bool unusableClip; // set once a clip turns out to be unreadable, so the warning isn't repeated every frame

        public AudioSource Source { get => source; set => source = value; }

        void OnEnable()
        {
            BuildMesh();
            if (Application.isPlaying) TryLoadSamples();
        }

        void OnDisable()
        {
            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                mesh = null;
            }
        }

        // Inspector changes can't touch other components from OnValidate, so rebuild on the next Update.
        void OnValidate() => rebuildPending = true;
        bool rebuildPending;

        void Update()
        {
            if (mesh == null || rebuildPending) { rebuildPending = false; BuildMesh(); }
            if (!Application.isPlaying)
            {
                ApplyIdle();
                return;
            }
            if (samples == null && (unusableClip || !TryLoadSamples())) { ApplyIdle(); return; }

            bool playing = source != null && source.isPlaying;
            Analyze(playing ? source.timeSamples : -1, Time.deltaTime);
        }

        /// <summary>
        /// Shows the sound field at a given clip time without smoothing. Used for editor previews.
        /// </summary>
        public void PreviewAt(float seconds)
        {
            if (mesh == null) BuildMesh();
            if (samples == null && !TryLoadSamples()) return;
            var clip = source.clip;
            int frame = Mathf.Clamp(Mathf.RoundToInt(seconds * clip.frequency), 0, clip.samples - 1);
            levelPeak = 1e-6f;
            // Prime the loudness tracker with the whole clip so the preview uses realistic levels.
            for (int f = 0; f < clip.samples; f += clip.frequency / 20)
                levelPeak = Mathf.Max(levelPeak, WindowCovariance(f, clip.frequency)[0]);
            Analyze(frame, -1f);
        }

        bool TryLoadSamples()
        {
            samples = null;
            if (source == null) source = GetComponentInParent<AudioSource>();
            var clip = source != null ? source.clip : null;
            if (clip == null) return false;
            if (clip.channels < 4)
            {
                Debug.LogWarning($"[AmbisonicSoundfieldVisualizer] '{clip.name}' has {clip.channels} channels; a first-order ambisonic clip needs 4.", this);
                unusableClip = true;
                return false;
            }
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                Debug.LogWarning($"[AmbisonicSoundfieldVisualizer] '{clip.name}' must use Load Type 'Decompress On Load' so its samples can be read.", this);
                unusableClip = true;
                return false;
            }
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return false; // still loading, try again next frame
            channels = clip.channels;
            samples = new float[clip.samples * channels];
            if (!clip.GetData(samples, 0)) { samples = null; return false; }
            return true;
        }

        /// <summary>Covariance of the W, X, Y, Z channels over a window ending at endFrame.</summary>
        /// <returns>Upper triangle: WW, WX, WY, WZ, XX, XY, XZ, YY, YZ, ZZ (mean per frame).</returns>
        float[] WindowCovariance(int endFrame, int frequency)
        {
            var c = new float[10];
            int frames = samples.Length / channels;
            int window = Mathf.Max(1, Mathf.RoundToInt(windowSeconds * frequency));
            for (int i = 0; i < window; i++)
            {
                int f = endFrame - i;
                if (f < 0) f += frames; // the clip loops
                int o = f * channels;
                // ambiX / ACN order: 0 = W, 1 = Y (left), 2 = Z (up), 3 = X (front)
                float w = samples[o], y = samples[o + 1], z = samples[o + 2], x = samples[o + 3];
                c[0] += w * w; c[1] += w * x; c[2] += w * y; c[3] += w * z;
                c[4] += x * x; c[5] += x * y; c[6] += x * z;
                c[7] += y * y; c[8] += y * z;
                c[9] += z * z;
            }
            for (int k = 0; k < 10; k++) c[k] /= window;
            return c;
        }

        void Analyze(int frame, float dt)
        {
            int n = directions.Length;
            var targets = new float[n];
            float level = 0f;

            if (frame >= 0)
            {
                var c = WindowCovariance(frame, source.clip.frequency);

                // Overall loudness with a slowly decaying peak, so quiet passages still read as quiet.
                if (dt > 0f) levelPeak = Mathf.Max(c[0], levelPeak * Mathf.Exp(-dt / 8f));
                level = Mathf.Sqrt(c[0] / Mathf.Max(levelPeak, 1e-9f));

                // Rotation from this globe's local space into the sound field's (the source's) space.
                Quaternion toField = Quaternion.Inverse(source.transform.rotation) * transform.rotation;
                float a = 1f - directivity, b = directivity;
                float max = 1e-12f, min = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    Vector3 d = toField * directions[i];
                    // Unity (x right, y up, z forward) to ambisonic (X front, Y left, Z up).
                    float dx = d.z, dy = -d.x, dz = d.y;
                    float gw = a, gx = b * dx, gy = b * dy, gz = b * dz;
                    float e = gw * gw * c[0] + gx * gx * c[4] + gy * gy * c[7] + gz * gz * c[9]
                            + 2f * (gw * gx * c[1] + gw * gy * c[2] + gw * gz * c[3]
                                  + gx * gy * c[5] + gx * gz * c[6] + gy * gz * c[8]);
                    targets[i] = Mathf.Max(e, 0f);
                    if (targets[i] > max) max = targets[i];
                    if (targets[i] < min) min = targets[i];
                }
                // Natural ambiences arrive from almost everywhere, so show how much each direction stands
                // out above the quietest one. The floor on the range keeps a truly diffuse field calm
                // instead of amplifying tiny differences into noise.
                float range = Mathf.Max(max - min, 0.35f * max);
                float gain = Mathf.Sqrt(level);
                for (int i = 0; i < n; i++)
                    targets[i] = Mathf.Pow(Mathf.Clamp01((targets[i] - min) / range), contrast) * gain;
            }

            for (int i = 0; i < n; i++)
            {
                float t = targets[i], s = smoothed[i];
                if (dt < 0f) s = t; // preview: no smoothing
                else s += (t - s) * (1f - Mathf.Exp(-(t > s ? attackRate : releaseRate) * dt));
                smoothed[i] = s;
                vertices[i] = directions[i] * (radius * (1f + bulge * s));
                var col = colors.Evaluate(s);
                col.a = Mathf.Lerp(idleAlpha, peakAlpha, s);
                vertexColors[i] = col;
            }
            mesh.vertices = vertices;
            mesh.colors = vertexColors;
            mesh.RecalculateBounds();
        }

        void ApplyIdle()
        {
            for (int i = 0; i < directions.Length; i++)
            {
                smoothed[i] = 0f;
                vertices[i] = directions[i] * radius;
                var col = colors.Evaluate(0f);
                col.a = Mathf.Max(idleAlpha, 0.15f); // keep it visible while editing
                vertexColors[i] = col;
            }
            mesh.vertices = vertices;
            mesh.colors = vertexColors;
            mesh.RecalculateBounds();
        }

        void BuildMesh()
        {
            Icosphere.Create(subdivisions, out directions, out var triangles);
            int n = directions.Length;
            vertices = new Vector3[n];
            vertexColors = new Color[n];
            smoothed = new float[n];
            for (int i = 0; i < n; i++) vertices[i] = directions[i] * radius;

            if (mesh == null)
            {
                mesh = new Mesh { name = "Soundfield Globe", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = vertexColors;
            mesh.triangles = triangles;
            mesh.normals = directions;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            ApplyIdle();
        }

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.10f, 0.35f, 0.90f), 0f),
                    new GradientColorKey(new Color(0.10f, 0.85f, 0.95f), 0.45f),
                    new GradientColorKey(new Color(1.00f, 0.85f, 0.30f), 0.8f),
                    new GradientColorKey(new Color(1.00f, 0.45f, 0.20f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        /// <summary>Minimal icosphere generator with shared vertices.</summary>
        static class Icosphere
        {
            public static void Create(int subdivisions, out Vector3[] verts, out int[] tris)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var v = new System.Collections.Generic.List<Vector3>
                {
                    new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                    new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                    new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
                };
                for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
                var f = new System.Collections.Generic.List<int>
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                    1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                    4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                var cache = new System.Collections.Generic.Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                }
                for (int s = 0; s < subdivisions; s++)
                {
                    var next = new System.Collections.Generic.List<int>(f.Count * 4);
                    for (int i = 0; i < f.Count; i += 3)
                    {
                        int a = f[i], b = f[i + 1], c = f[i + 2];
                        int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                        next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                    }
                    f = next;
                }
                verts = v.ToArray();
                tris = f.ToArray();
            }
        }
    }
}
