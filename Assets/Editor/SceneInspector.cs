using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// Read-only health check of the active scene: shaders that don't render in URP (pink), missing
    /// scripts/meshes/materials, coplanar surfaces that z-fight, lighting state, the Meta room acoustics box
    /// versus the actual room, where the player stands and what the UI looks like from there.
    /// Writes Temp/SceneReport.txt and renders previews to Temp/SceneInspector/&lt;scene&gt;.
    /// </summary>
    public static class SceneInspector
    {
        [MenuItem("Tools/MetaAudioSDK Demo/Inspect Active Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            sb.AppendLine($"Scene: {scene.path}");
            var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToList();

            // ---- shaders / materials ----
            sb.AppendLine("\n== Materials (by shader) ==");
            foreach (var g in renderers.SelectMany(r => r.sharedMaterials.Select(m => (r, m))).GroupBy(x => x.m))
            {
                var m = g.Key;
                string shader = m == null ? "<null material>" : m.shader == null ? "<null shader>" : m.shader.name;
                bool bad = m == null || m.shader == null || !m.shader.isSupported || shader.StartsWith("Hidden/InternalErrorShader")
                           || shader == "Standard" || shader.StartsWith("Legacy Shaders") || shader.StartsWith("Nature/") || shader.StartsWith("Standard (");
                if (!bad && !(m != null && m.shader.name.StartsWith("Shader Graphs/"))) continue;
                sb.AppendLine($"{(bad ? "BROKEN " : "")}{(m ? m.name : "null")} [{shader}] used by {g.Count()}: {string.Join(", ", g.Select(x => Path(x.r.transform)).Distinct().Take(6))}");
            }

            // ---- missing things ----
            sb.AppendLine("\n== Missing references ==");
            foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0) sb.AppendLine($"Missing script x{missing}: {Path(t)}");
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null) sb.AppendLine($"MeshFilter without mesh: {Path(t)}");
                var mr = t.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterials.Any(x => x == null)) sb.AppendLine($"Empty material slot: {Path(t)}");
            }

            // ---- big renderers and coplanar floors ----
            sb.AppendLine("\n== Large renderers ==");
            foreach (var r in renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.magnitude > 3f).OrderByDescending(r => r.bounds.size.magnitude).Take(25))
                sb.AppendLine($"{Path(r.transform)}: centre {r.bounds.center:F2} size {r.bounds.size:F2} mats [{string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "null"))}]");
            sb.AppendLine("\n== Possible z-fighting (horizontal surfaces at the same height overlapping) ==");
            var flats = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.y < 0.05f && r.bounds.size.x > 1f && r.bounds.size.z > 1f).ToList();
            for (int i = 0; i < flats.Count; i++)
            for (int j = i + 1; j < flats.Count; j++)
            {
                var a = flats[i].bounds; var b = flats[j].bounds;
                if (Mathf.Abs(a.center.y - b.center.y) < 0.02f && a.Intersects(b))
                    sb.AppendLine($"{Path(flats[i].transform)} and {Path(flats[j].transform)} both at y={a.center.y:F3}");
            }

            // ---- lighting ----
            sb.AppendLine("\n== Lighting ==");
            sb.AppendLine($"Quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} ambient={RenderSettings.ambientMode} sky={RenderSettings.skybox?.name} fog={RenderSettings.fog} reflection={RenderSettings.defaultReflectionMode}x{RenderSettings.reflectionIntensity}");
            sb.AppendLine($"Lightmaps={LightmapSettings.lightmaps.Length} probes={LightmapSettings.lightProbes?.count ?? 0} settings={(Lightmapping.TryGetLightingSettings(out var ls) && ls ? ls.name : "none")}");
            foreach (var l in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
                sb.AppendLine($"Light {Path(l.transform)}: {l.type} {l.lightmapBakeType} enabled={l.enabled && l.gameObject.activeInHierarchy} int={l.intensity} range={l.range} shadows={l.shadows} colour={l.color} pos={l.transform.position:F2} rot={l.transform.eulerAngles:F0}");
            foreach (var p in roots.SelectMany(r => r.GetComponentsInChildren<ReflectionProbe>(true)))
                sb.AppendLine($"ReflectionProbe {Path(p.transform)}: enabled={p.enabled} mode={p.mode} size={p.size} intensity={p.intensity} baked={(p.bakedTexture ? p.bakedTexture.name : "none")}");
            int lightmapped = renderers.Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < 0xFFFE);
            sb.AppendLine($"Renderers using a lightmap: {lightmapped}/{renderers.Count}; static ContributeGI: {renderers.Count(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI))}");

            // ---- room and acoustics ----
            sb.AppendLine("\n== Room ==");
            var room = RoomBounds(renderers);
            sb.AppendLine($"Room geometry (renderers inside the largest prefab): centre {room.center:F2} size {room.size:F2}");
            foreach (var mb in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null && m.GetType().Name.Contains("RoomAcoustic")))
            {
                var so = new SerializedObject(mb);
                string F(string n) { var p = so.FindProperty(n); return p == null ? "?" : p.propertyType == SerializedPropertyType.Float ? p.floatValue.ToString("F2") : p.propertyType == SerializedPropertyType.Enum ? p.enumDisplayNames[p.enumValueIndex] : p.propertyType == SerializedPropertyType.Boolean ? p.boolValue.ToString() : p.intValue.ToString(); }
                sb.AppendLine($"{mb.GetType().Name} on {Path(mb.transform)} at {mb.transform.position:F2}: width {F("width")} height {F("height")} depth {F("depth")} lockToListener {F("lockPositionToListener")} " +
                              $"walls L {F("leftMaterial")} R {F("rightMaterial")} front {F("frontMaterial")} back {F("backMaterial")} ceiling {F("ceilingMaterial")} floor {F("floorMaterial")} clutter {F("clutterFactor")}");
            }

            // ---- player and UI ----
            sb.AppendLine("\n== Player and UI ==");
            var rig = PlayerRigUtility.FindRig();
            var eye = (rig ? rig.transform.position : Vector3.zero) + Vector3.up * 1.6f;
            if (rig != null)
            {
                var ground = PlayerRigUtility.GroundBelow(rig);
                sb.AppendLine($"Rig at {rig.transform.position:F2} facing {rig.transform.forward:F2}; ground below at y={(ground.HasValue ? ground.Value.ToString("F2") : "NONE")}" +
                              (ground.HasValue && Mathf.Abs(rig.transform.position.y - ground.Value) > 0.05f ? "  <-- rig is not on the floor (floor-level tracking)" : ""));
            }
            foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<TMPro.TMP_Text>(true)))
                sb.AppendLine($"Text '{Short(t.text)}' on {Path(t.transform)} at {t.transform.position:F2}, {Vector3.Distance(eye, t.transform.position):F1} m from the eyes, size {t.fontSize:F2} auto={t.enableAutoSizing}");
            foreach (var a in roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)))
                sb.AppendLine($"Audio {Path(a.transform)} at {a.transform.position:F2}: clip={(a.clip ? a.clip.name : "none")} loop={a.loop} playOnAwake={a.playOnAwake} spatialize={a.spatialize}");

            var dir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Temp");
            File.WriteAllText(System.IO.Path.Combine(dir, $"SceneReport_{scene.name}.txt"), sb.ToString());
            Debug.Log($"[SceneInspector] Report written to Temp/SceneReport_{scene.name}.txt");

            RenderPreviews(scene.name, eye, rig ? rig.transform.forward : Vector3.forward, room, renderers);
        }

        static Bounds RoomBounds(List<Renderer> renderers)
        {
            // The room is the biggest prefab instance that isn't the XR rig or a ground plane.
            var groups = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .GroupBy(r => PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject))
                .Where(g => g.Key != null && !g.Key.name.Contains("BuildingBlock"))
                .Select(g => { var b = g.First().bounds; foreach (var r in g) b.Encapsulate(r.bounds); return b; })
                .OrderByDescending(b => b.size.magnitude).ToList();
            return groups.Count > 0 ? groups[0] : new Bounds(Vector3.zero, Vector3.one * 6f);
        }

        static void RenderPreviews(string sceneName, Vector3 eye, Vector3 forward, Bounds room, List<Renderer> renderers)
        {
            var flat = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (flat == Vector3.zero) flat = Vector3.forward;
            var views = new List<ScenePreviewRenderer.View>();
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0, 90 * i, 0) * flat;
                views.Add(new ScenePreviewRenderer.View($"player_{new[] { "ahead", "right", "behind", "left" }[i]}", eye, eye + d * 5f - Vector3.up * 0.4f));
            }
            views.Add(new ScenePreviewRenderer.View("player_down", eye, eye + flat * 1.2f - Vector3.up * 1.6f));
            // Each sound source as seen from the spawn, and from closer up.
            var rig = PlayerRigUtility.FindRig();
            foreach (var src in renderers.Select(r => r.GetComponent<AudioSource>()).Where(a => a != null && (rig == null || !a.transform.IsChildOf(rig.transform))).Distinct())
            {
                var p = src.transform.position;
                views.Add(new ScenePreviewRenderer.View($"source_{src.name}", eye, p));
                var back = Vector3.ProjectOnPlane(eye - p, Vector3.up).normalized;
                views.Add(new ScenePreviewRenderer.View($"source_{src.name}_close", p + back * 2.5f + Vector3.up * 0.3f, p));
            }
            views.Add(new ScenePreviewRenderer.View("overview", eye + Vector3.up * 6f - flat * 4f, eye + flat * 10f));

            // Cutaway: hide everything above head height so the whole room is visible from above.
            float cut = room.min.y + 2.1f;
            var hidden = renderers.Where(r => r.enabled && r.bounds.min.y > cut - 0.3f && r.bounds.size.y < 3f).ToList();
            var c = room.center;
            views.Add(new ScenePreviewRenderer.View("cutaway_top", c + new Vector3(0.01f, Mathf.Max(room.size.x, room.size.z) * 0.9f + 2f, -0.01f), c,
                () => hidden.ForEach(r => r.enabled = false)));
            views.Add(new ScenePreviewRenderer.View("cutaway_angle", c + new Vector3(-room.size.x * 0.7f, room.size.y + 3f, -room.size.z * 0.7f), c));
            views.Add(new ScenePreviewRenderer.View("outside", c + new Vector3(room.size.x * 1.4f, 4f, room.size.z * 1.6f), c,
                () => hidden.ForEach(r => r.enabled = true)));
            try
            {
                ScenePreviewRenderer.Render($"SceneInspector/{sceneName}", "Mobile", views.ToArray());
            }
            finally
            {
                hidden.ForEach(r => r.enabled = true);
            }
        }

        static string Short(string s) => s.Length > 50 ? s.Substring(0, 50).Replace("\n", " ") + "..." : s.Replace("\n", " ");

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
