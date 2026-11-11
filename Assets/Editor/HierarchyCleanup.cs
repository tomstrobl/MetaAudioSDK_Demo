using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// One-time pass that gives every scene a hierarchy a student can read: the sound source first, named for what
    /// it demonstrates, then the signs and the next-scene control, then "Lighting" and "Environment" groups for
    /// everything else. Only names, grouping and order change (world positions are kept, so the baked lighting
    /// stays valid). Meta's [BuildingBlock] objects and the camera rig keep their standard names. Safe to re-run.
    /// </summary>
    public static class HierarchyCleanup
    {
        const string RigName = "[BuildingBlock] Camera Rig";
        static readonly List<string> Problems = new();

        [MenuItem("Tools/MetaAudioSDK Demo/Rename Scene Objects For Students")]
        public static void Run()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[Hierarchy] Wait for the running bake to finish first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Problems.Clear();
            var report = new StringBuilder();
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                switch (scene.name)
                {
                    case "Ambisonics": Ambisonics(); break;
                    case "BasicPointSource": BasicPointSource(); break;
                    case "RoomAcoustics": RoomAcoustics(); break;
                    case "SourceDirectivity": SourceDirectivity(); break;
                    default: continue;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine($"######## {scene.name}");
                foreach (var root in scene.GetRootGameObjects()) Describe(root.transform, 0, report);
                report.AppendLine();
            }
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Temp", "Hierarchy");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "hierarchy.txt"), report.ToString());
            File.WriteAllLines(Path.Combine(dir, "problems.txt"), Problems);
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path, OpenSceneMode.Single);
            Debug.Log($"[Hierarchy] Done, {Problems.Count} problem(s). Report in {dir}.");
            foreach (var p in Problems) Debug.LogWarning("[Hierarchy] " + p);
        }

        // ---------- per scene ----------

        static void Ambisonics()
        {
            Rename("AmbisonicAmbience", "Ambisonic Sound Source");
            NextSceneButton("Canvas", "Next Scene Button", "Load BasicPointSource", "BasicPointSource");
            Rename("WoodenBoard", "Welcome Sign");
            Group("Lighting", "Directional Light>Sun", "Light Probes");
            Group("Environment", "Terrain", "Plane>Far Ground", "Boundary Fence", "Boundarys>Invisible Walls");
            Walls();
            Order(RigName, "Ambisonic Sound Source", "Welcome Sign", "Next Scene Button", "Lighting", "Environment", "EventSystem");
        }

        static void BasicPointSource()
        {
            Rename("Sound", "Point Sound Source");
            Rename("Board01", "Instruction Sign");
            Rename("Instruction Sign/Slab04", "Sign Base");
            NextSceneButton("Canvas", "Next Scene Button", "Load RoomAcoustics", "RoomAcoustics");
            // The altar prefab instance had no name override and showed up as "(prefab)".
            var altar = Roots().FirstOrDefault(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabSource(g) == "DemonicAltar")
                        ?? Find("Demonic Altar");
            if (altar != null) SetName(altar, "Demonic Altar");
            var particles = Find("Particles") ?? Find("Altar Fire And Dust");
            if (particles != null)
            {
                SetName(particles, "Altar Fire And Dust");
                int fire = 0, dust = 0;
                foreach (Transform child in particles.transform)
                {
                    var source = PrefabSource(child.gameObject);
                    if (source == "Fire") SetName(child.gameObject, ++fire == 1 ? "Fire" : $"Fire {fire}");
                    else if (source == "Dust") SetName(child.gameObject, ++dust == 1 ? "Dust" : $"Dust {dust}");
                }
            }
            Rename("Terrain PoinSource", "Terrain");
            // This scene already had a "Lighting" group (altar glow and blood sun); the probes join it.
            Group("Lighting", "Light Probes");
            Group("Environment", "Terrain", "Demonic Altar", "Altar Fire And Dust");
            Order(RigName, "Point Sound Source", "Instruction Sign", "Next Scene Button", "Lighting", "Environment", "EventSystem");
        }

        static void RoomAcoustics()
        {
            Rename("Sound", "Point Sound Source (Power Drill)");
            Rename("RoomAcoustics", "Room Acoustic Properties");
            Rename("Poster", "Instruction Poster");
            Rename("Workshop Door", "Next Scene Door");
            Group("Lighting", "Directional Light>Sun", "Point Light>Garage Lamp", "Reflection Probe>Garage Reflection Probe", "Light Probes");
            Group("Environment", "Garage", "Plane>Ground", "Boundarys>Invisible Walls");
            Walls();
            Order(RigName, "Point Sound Source (Power Drill)", "Room Acoustic Properties", "Instruction Poster", "Next Scene Door",
                "Lighting", "Environment", "EventSystem");
        }

        static void SourceDirectivity()
        {
            // Two stray Interaction SDK sample cubes sat at the world origin, buried inside the temple platform.
            foreach (var cube in Roots().Where(g => g.name == "[BuildingBlock] Cube").ToList())
            {
                Problems.Add($"SourceDirectivity: deleted a stray '[BuildingBlock] Cube' at {cube.transform.position} (hidden inside the platform).");
                Undo.DestroyObjectImmediate(cube);
            }

            // Each voice belongs to the statue it stands in.
            var statues = new Dictionary<string, string>
            {
                ["Henry"] = "EA_Characters_Statue_Clean_1f",
                ["Egan"] = "EA_Characters_Statue_Clean_1a",
                ["Brandon"] = "EA_Characters_Statue_Hooded_Moss_1a",
            };
            var speaking = new List<string>();
            foreach (var (voice, statue) in statues)
            {
                var source = Find(voice) ?? Find($"{voice} (Voice Source)");
                if (source != null)
                {
                    SetName(source, $"{voice} (Voice Source)");
                    var marker = source.transform.Find("Cube");
                    if (marker != null) SetName(marker.gameObject, "Facing Direction Marker");
                }
                var mesh = Find(statue) ?? Find($"{voice} (Statue)");
                if (mesh != null) SetName(mesh, $"{voice} (Statue)");
                speaking.Add($"{voice} (Voice Source)");
                speaking.Add($"{voice} (Statue)");
            }
            Group("Speaking Statues", speaking.ToArray());
            NextSceneButton("Canvas", "Back To Start Button", "Load Ambisonics", "Ambisonics");
            Rename("Inscription Stele", "Closing Sign (Stele)");
            Group("Lighting", "Directional Light>Sun", "Light Probes");
            Group("Temple Ruins", "Entrance", "Arches", "Platform", "Altar", "Flag>Banner 1", "Flag (1)>Banner 2");
            Group("Environment", "Temple Ruins", "Tree", "Terrain", "Plane>Far Ground", "Stars", "Boundary Fence", "Boundarys>Invisible Walls");
            Walls();
            Order(RigName, "Speaking Statues", "Closing Sign (Stele)", "Back To Start Button", "Lighting", "Environment", "EventSystem");
        }

        // ---------- shared pieces ----------

        /// <summary>The world-space canvas with the scene loader and the poke/ray button.</summary>
        static void NextSceneButton(string oldName, string newName, string loaderName, string target)
        {
            var canvas = Find(oldName) ?? Find(newName);
            if (canvas == null) return;
            SetName(canvas, newName);
            var loader = canvas.transform.Find(loaderName) ?? canvas.transform.Find($"Scene Loader (to {target})");
            if (loader != null) SetName(loader.gameObject, $"Scene Loader (to {target})");
            else Problems.Add($"{newName}: no '{loaderName}' child.");
            var button = canvas.transform.Find("TeleportButton") ?? canvas.transform.Find("Button");
            if (button == null) { Problems.Add($"{newName}: no 'TeleportButton' child."); return; }
            SetName(button.gameObject, "Button");
            var label = button.Find("Text (TMP)") ?? button.Find("Label");
            if (label != null) SetName(label.gameObject, "Label");
        }

        /// <summary>The four invisible boundary walls: "B (left)" etc. become "Wall Left" etc.</summary>
        static void Walls()
        {
            var walls = Find("Environment/Invisible Walls");
            if (walls == null) return;
            foreach (Transform w in walls.transform)
            {
                var side = w.name.Replace("B (", "").Replace(")", "").Replace("Wall ", "").Trim();
                SetName(w.gameObject, "Wall " + char.ToUpper(side[0]) + side.Substring(1).ToLower());
            }
        }

        // ---------- helpers ----------

        static GameObject[] Roots() => EditorSceneManager.GetActiveScene().GetRootGameObjects();

        /// <summary>A root object, or a "Root/Child/..." path below one.</summary>
        static GameObject Find(string path)
        {
            var parts = path.Split('/');
            var root = Roots().FirstOrDefault(g => g.name == parts[0]);
            if (root == null) return null;
            var t = root.transform;
            foreach (var part in parts.Skip(1))
            {
                t = t.Cast<Transform>().FirstOrDefault(c => c.name == part);
                if (t == null) return null;
            }
            return t.gameObject;
        }

        /// <summary>Renames a root (or path) object; already renamed is fine, missing is reported.</summary>
        static void Rename(string path, string newName)
        {
            var go = Find(path);
            if (go == null)
            {
                var parent = path.Contains('/') ? path.Substring(0, path.LastIndexOf('/') + 1) : "";
                if (Find(parent + newName) == null) Problems.Add($"{EditorSceneManager.GetActiveScene().name}: '{path}' not found.");
                return;
            }
            SetName(go, newName);
        }

        static void SetName(GameObject go, string name)
        {
            if (go.name == name) return;
            Undo.RecordObject(go, "Rename");
            go.name = name;
        }

        /// <summary>
        /// Puts root objects under a root group (created if needed, at the origin). "Old>New" also renames.
        /// World transforms are kept.
        /// </summary>
        static void Group(string groupName, params string[] members)
        {
            var group = Roots().FirstOrDefault(g => g.name == groupName);
            if (group == null)
            {
                group = new GameObject(groupName);
                Undo.RegisterCreatedObjectUndo(group, "Group");
            }
            foreach (var m in members)
            {
                var parts = m.Split('>');
                string oldName = parts[0], newName = parts.Length > 1 ? parts[1] : parts[0];
                var go = Roots().FirstOrDefault(g => g != group && (g.name == oldName || g.name == newName))
                         ?? group.transform.Cast<Transform>().FirstOrDefault(c => c.name == oldName || c.name == newName)?.gameObject;
                if (go == null) { Problems.Add($"{EditorSceneManager.GetActiveScene().name}: '{oldName}' not found for group '{groupName}'."); continue; }
                SetName(go, newName);
                if (go.transform.parent != group.transform) Undo.SetTransformParent(go.transform, group.transform, true, "Group");
            }
        }

        static void Order(params string[] names)
        {
            int index = 0;
            foreach (var n in names)
            {
                var go = Roots().FirstOrDefault(g => g.name == n);
                if (go == null) { Problems.Add($"{EditorSceneManager.GetActiveScene().name}: '{n}' not found for ordering."); continue; }
                go.transform.SetSiblingIndex(index++);
            }
        }

        static string PrefabSource(GameObject go)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) return null;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
        }

        static void Describe(Transform t, int depth, StringBuilder sb)
        {
            sb.AppendLine(new string(' ', depth * 2) + t.name);
            if (t.name.StartsWith("[BuildingBlock]") || t.name == "Boundary Fence" || depth >= 3) return;
            foreach (Transform c in t) Describe(c, depth + 1, sb);
        }
    }
}
