using System.Collections;
using UnityEngine;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// A door that leads to the next scene: pressing it (via its button) swings the leaf open towards the
    /// room, reveals a warm glow in the doorway, and loads the next scene while the door is still opening.
    /// </summary>
    public class SceneDoor : MonoBehaviour
    {
        [SerializeField] SceneLoader loader;
        [Tooltip("The door leaf. Its pivot is the hinge line.")]
        [SerializeField] Transform leaf;
        [Tooltip("Shown behind the leaf while the door opens.")]
        [SerializeField] Renderer doorway;
        [Tooltip("Direction from the wall into the room, in this object's local space.")]
        [SerializeField] Vector3 outward = Vector3.forward;
        [SerializeField, Range(30f, 120f)] float openAngle = 100f;
        [SerializeField, Min(0.1f)] float openSeconds = 1.2f;
        [SerializeField, Min(0f)] float loadAfterSeconds = 0.6f;

        bool opening;

        public SceneLoader Loader => loader;

        void Awake()
        {
            if (doorway != null) doorway.enabled = false;
        }

        public void Open()
        {
            if (opening || loader == null) return;
            opening = true;
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            if (doorway != null) doorway.enabled = true;
            var start = leaf != null ? leaf.localRotation : Quaternion.identity;
            var axis = leaf != null && leaf.parent != null ? leaf.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            float sign = OpenSign();
            bool loading = false;
            for (float t = 0f; t < openSeconds; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / openSeconds);
                float eased = 1f - (1f - u) * (1f - u) * (1f - u); // quick start, soft stop
                if (leaf != null) leaf.localRotation = Quaternion.AngleAxis(sign * openAngle * eased, axis) * start;
                if (!loading && t >= loadAfterSeconds) { loading = true; loader.LoadScene(); }
                yield return null;
            }
            if (!loading) loader.LoadScene();
        }

        /// <summary>+1 or -1: whichever way swings the free edge of the leaf into the room.</summary>
        float OpenSign()
        {
            if (leaf == null) return 1f;
            var r = leaf.GetComponent<Renderer>();
            var pivot = leaf.position;
            var edge = (r != null ? r.bounds.center : pivot + leaf.right) - pivot;
            var swung = Quaternion.AngleAxis(10f, Vector3.up) * edge;
            return Vector3.Dot(swung - edge, transform.TransformDirection(outward)) > 0f ? 1f : -1f;
        }
    }
}
