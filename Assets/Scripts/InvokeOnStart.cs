using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MetaAudioSDKDemo
{
    /// <summary>Invokes an event once, a moment after the scene starts (e.g. to switch on an effect).</summary>
    public class InvokeOnStart : MonoBehaviour
    {
        [Tooltip("Seconds to wait after Start. Also guarantees every other component has run its own Start first.")]
        [SerializeField, Min(0f)] float delaySeconds = 1f;
        [SerializeField] UnityEvent onStart = new();

        public UnityEvent OnStart => onStart;

        IEnumerator Start()
        {
            yield return null; // let the targets finish their own Start
            if (delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);
            onStart.Invoke();
        }
    }
}
