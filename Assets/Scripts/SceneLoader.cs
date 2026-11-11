using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaAudioSDKDemo
{
    /// <summary>
    /// Loads a scene, e.g. from a UI Button's OnClick. Pick the scene in the Inspector; it must be
    /// enabled in File > Build Profiles (the Inspector warns if it isn't).
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [Tooltip("Scene to load. Must be enabled in File > Build Profiles.")]
        [SerializeField, SceneName] string sceneName;

        bool loading;

        public string SceneName
        {
            get => sceneName;
            set => sceneName = value;
        }

        /// <summary>Starts loading the scene. Extra calls while it is loading are ignored.</summary>
        public void LoadScene()
        {
            if (loading) return;
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[SceneLoader] No scene set on '{name}'.", this);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not enabled in File > Build Profiles.", this);
                return;
            }
            loading = true;
            SceneManager.LoadSceneAsync(sceneName);
        }
    }

    /// <summary>Shows a string field as a scene picker in the Inspector.</summary>
    public class SceneNameAttribute : PropertyAttribute { }
}
