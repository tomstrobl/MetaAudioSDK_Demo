using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// Every scene's camera rig uses floor-level tracking, so the rig must stand on the ground; several
    /// stood 1 m above it, making players float. Snaps each build scene's rig to the ground and renders
    /// inspector previews of each scene. Safe to re-run.
    /// </summary>
    public static class SnapPlayerRigs
    {
        [MenuItem("Tools/MetaAudioSDK Demo/Snap Player Rigs To Floor (All Scenes)")]
        public static void Run()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[Rigs] Wait for the running bake to finish first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string last = null;
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                var rig = PlayerRigUtility.FindRig();
                var ground = rig ? PlayerRigUtility.GroundBelow(rig) : null;
                if (ground == null) { Debug.LogError($"[Rigs] {scene.name}: no rig or no ground below it."); continue; }
                var p = rig.transform.position;
                if (Mathf.Abs(p.y - ground.Value) > 0.005f)
                {
                    Undo.RecordObject(rig.transform, "Snap rig");
                    rig.transform.position = new Vector3(p.x, ground.Value, p.z);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[Rigs] {scene.name}: rig moved from y={p.y:F2} down to the ground at y={ground.Value:F2}.");
                }
                else Debug.Log($"[Rigs] {scene.name}: rig already on the ground.");

                SceneInspector.Run();
                last = s.path;
            }
            if (last != null) EditorSceneManager.OpenScene(last, OpenSceneMode.Single);
            Debug.Log("[Rigs] Done.");
        }
    }
}
