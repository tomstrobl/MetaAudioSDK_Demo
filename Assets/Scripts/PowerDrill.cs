using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// A cordless drill that runs in bursts: the trigger pulls in, the motor spins up, runs for a while
    /// (slightly bogging down as if under load) and winds down again. One motor speed drives everything,
    /// so the pitch and volume of the looping motor sound always match the spinning chuck.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PowerDrill : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("Spins with the motor. Its pivot lies on the drill's axis.")]
        [SerializeField] Transform chuck;
        [Tooltip("Marks the tip of the bit; with the chuck pivot it defines the spin axis.")]
        [SerializeField] Transform bitTip;
        [SerializeField] Transform trigger;
        [Tooltip("Shaken slightly while the motor runs (the drill model's root).")]
        [SerializeField] Transform body;

        [Header("Rhythm")]
        [SerializeField] Vector2 runSeconds = new(1.2f, 2.8f);
        [SerializeField] Vector2 pauseSeconds = new(0.8f, 2f);
        [SerializeField, Min(0.01f)] float spinUpSeconds = 0.25f;
        [SerializeField, Min(0.01f)] float spinDownSeconds = 0.7f;

        [Header("Sound")]
        [Tooltip("Pitch of the motor loop when barely turning; full speed plays it at pitch 1.")]
        [SerializeField, Range(0.05f, 1f)] float idlePitch = 0.3f;
        [SerializeField, Min(0f)] float pitchRandomizationSemitones = 2f;
        [Tooltip("How much the motor speed wavers while running, as if drilling into something.")]
        [SerializeField, Range(0f, 0.2f)] float loadWobble = 0.04f;

        [Header("Look")]
        [Tooltip("Visible spin at full speed. Kept low: a real 25 rev/s would strobe on a 72-90 Hz display.")]
        [SerializeField, Min(0f)] float visualRevsPerSecond = 7f;
        [SerializeField, Min(0f)] float triggerTravel = 0.006f;
        [SerializeField, Min(0f)] float vibration = 0.0004f;

        AudioSource source;
        Vector3 spinAxis, triggerRest, triggerDir, bodyRest;
        bool partsResolved, running;
        float speed, timer, burstPitch = 1f, triggerPull, noiseSeed;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            ResolveParts();
            timer = 0.6f;
            noiseSeed = Random.value * 100f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            timer -= dt;
            if (timer <= 0f)
            {
                running = !running;
                timer = running ? Random.Range(runSeconds.x, runSeconds.y) : Random.Range(pauseSeconds.x, pauseSeconds.y);
                if (running) burstPitch = Mathf.Pow(2f, Random.Range(-pitchRandomizationSemitones, pitchRandomizationSemitones) * 0.5f / 12f);
            }

            // Spin up quickly and roughly linearly; wind down exponentially, like a motor coasting to a stop.
            if (running) speed = Mathf.Min(1f, speed + dt / spinUpSeconds);
            else speed = Mathf.Max(0f, speed * Mathf.Exp(-dt * 4f / spinDownSeconds) - dt * 0.05f);

            float load = running ? 1f + loadWobble * (Mathf.PerlinNoise(noiseSeed, Time.time * 2.5f) - 0.5f) * 2f : 1f;
            ApplyAudio(speed * load);
            Pose(speed, running, dt);
        }

        void ApplyAudio(float s)
        {
            if (s < 0.005f)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            source.pitch = Mathf.Lerp(idlePitch, 1f, Mathf.Clamp01(s)) * burstPitch;
            source.volume = Mathf.Pow(Mathf.Clamp01(s), 0.6f);
            if (!source.isPlaying) source.Play();
        }

        void Pose(float s, bool pulled, float dt)
        {
            if (chuck != null) chuck.localRotation = Quaternion.AngleAxis(360f * visualRevsPerSecond * s * dt, spinAxis) * chuck.localRotation;
            triggerPull = Mathf.MoveTowards(triggerPull, pulled ? 1f : 0f, dt / 0.06f);
            if (trigger != null) trigger.localPosition = triggerRest + triggerDir * (triggerTravel * triggerPull);
            if (body != null) body.localPosition = bodyRest + Random.insideUnitSphere * (vibration * s);
        }

        /// <summary>Editor previews: show the drill running (trigger pulled, chuck turned) or at rest.</summary>
        public void PreviewPose(bool runningPose, float chuckDegrees)
        {
            ResolveParts();
            if (chuck != null) chuck.localRotation = Quaternion.AngleAxis(chuckDegrees, spinAxis) * chuck.localRotation;
            if (trigger != null) trigger.localPosition = triggerRest + triggerDir * (runningPose ? triggerTravel : 0f);
        }

        public void ClearPreview(Quaternion chuckRotation)
        {
            if (chuck != null) chuck.localRotation = chuckRotation;
            if (trigger != null && partsResolved) trigger.localPosition = triggerRest;
        }

        void ResolveParts()
        {
            if (partsResolved) return;
            partsResolved = true;
            if (chuck != null)
            {
                var axisWorld = bitTip != null ? bitTip.position - chuck.position : chuck.right;
                spinAxis = (chuck.parent != null ? chuck.parent.InverseTransformDirection(axisWorld) : axisWorld).normalized;
            }
            if (trigger != null)
            {
                triggerRest = trigger.localPosition;
                // The trigger is pulled back along the drill's axis, towards the grip.
                triggerDir = trigger.parent != null ? trigger.parent.InverseTransformDirection(-(bitTip != null && chuck != null ? (bitTip.position - chuck.position).normalized : Vector3.right)) : Vector3.left;
            }
            if (body != null) bodyRest = body.localPosition;
        }
    }
}
