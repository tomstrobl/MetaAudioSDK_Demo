using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MetaAudioSDKDemo.Tests
{
    /// <summary>
    /// Walks the demo like a player: every build scene must have exactly one scene button that can be
    /// hit by the Interaction SDK ray, and pressing it must load the next build scene (the last one
    /// loops back to the first).
    /// </summary>
    public class SceneLoopTests
    {
        const float LoadTimeoutSeconds = 30f;

        [UnityTest]
        public IEnumerator EverySceneButtonLoadsTheNextSceneInTheLoop()
        {
            // Without a headset the XR plugins log errors; they are unrelated to scene loading.
            LogAssert.ignoreFailingMessages = true;

            // The test runner appends its own InitTestScene to the build list while tests run; skip it.
            var scenes = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
                .Select(SceneUtility.GetScenePathByBuildIndex)
                .Where(p => !Path.GetFileName(p).StartsWith("InitTestScene")).ToList();
            Assert.That(scenes.Count, Is.GreaterThan(1), "Need at least two scenes in the build.");

            for (int i = 0; i < scenes.Count; i++)
            {
                string scenePath = scenes[i];
                string expected = Path.GetFileNameWithoutExtension(scenes[(i + 1) % scenes.Count]);

                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
                yield return null;

                var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(b => LoaderOf(b) != null).ToList();
                Assert.That(buttons.Count, Is.EqualTo(1), $"{scenePath}: expected exactly one button wired to a SceneLoader or SceneDoor.");

                var button = buttons[0];
                Assert.That(button.isActiveAndEnabled && button.interactable, Is.True, $"{scenePath}: the scene button is not active and interactable.");
                AssertRayCanHit(button, scenePath);

                var loader = LoaderOf(button);
                Assert.That(loader.SceneName, Is.EqualTo(expected), $"{scenePath}: button loads '{loader.SceneName}', expected '{expected}'.");

                button.onClick.Invoke();
                float waited = 0f;
                while (SceneManager.GetActiveScene().name != expected && waited < LoadTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(expected), $"{scenePath}: pressing the button did not load '{expected}'.");
            }
        }

        /// <summary>The scene loader a button triggers, directly or through a <see cref="SceneDoor"/>.</summary>
        static SceneLoader LoaderOf(Button b)
        {
            for (int k = 0; k < b.onClick.GetPersistentEventCount(); k++)
            {
                var target = b.onClick.GetPersistentTarget(k);
                if (target is SceneLoader loader) return loader;
                if (target is SceneDoor door) return door.Loader;
            }
            return null;
        }

        /// <summary>The Interaction SDK ray hits a collider on the canvas; it must cover the button.</summary>
        static void AssertRayCanHit(Button button, string scenePath)
        {
            var canvas = button.GetComponentInParent<Canvas>()?.rootCanvas;
            Assert.That(canvas, Is.Not.Null, $"{scenePath}: the scene button is not on a canvas.");
            var rect = (RectTransform)button.transform;
            var centre = rect.TransformPoint(rect.rect.center);
            bool covered = canvas.GetComponentsInChildren<BoxCollider>(true).Any(c =>
            {
                var local = c.transform.InverseTransformPoint(centre) - c.center;
                var half = c.size * 0.5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
            });
            Assert.That(covered, Is.True, $"{scenePath}: no canvas collider covers the button, so the controller ray can't press it.");
        }
    }
}
