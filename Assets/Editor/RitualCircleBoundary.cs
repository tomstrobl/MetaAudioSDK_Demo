using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// BasicPointSource had no boundary at all. This builds a ritual circle around the demonic altar: obsidian
    /// spikes with ember-glowing tips, joined by sagging rusted chains (SourceArt/RitualCircle.blend), plus one
    /// invisible wall per chain so the player cannot leave, like the walls in the other scenes. The ring encloses
    /// the spawn, the instruction sign and the next-scene button with room to spare. Then the scene is rebaked
    /// so the spikes cast shadows and their glowing tips light the ground. Safe to re-run.
    /// </summary>
    public static class RitualCircleBoundary
    {
        const string ScenePath = "Assets/Scenes/BasicPointSource.unity";
        const string FbxPath = "Assets/Art/RitualCircle/RitualCircle.fbx";
        const string MaterialFolder = "Assets/Art/RitualCircle";
        const string StoneGrainPath = "Assets/Art/Temple/Textures/Stone_Grain.png";

        // Must match SourceArt/RitualCircle.blend: the chain mesh is modelled for exactly this chord.
        const float Radius = 15.5f;
        const int Segments = 39;
        const float ChainHeight = 1.0f;   // the spikes' iron band
        const float SinkSpikes = 0.06f;   // set into the ground, never floating on a slope
        const float WallHeight = 6f, WallThickness = 0.2f;
        static readonly float[] ProbeHeights = { 0.5f, 1.7f, 3.5f };

        const string RootName = "Ritual Circle";
        const string WallsName = "Invisible Walls";
        const string EnvironmentName = "Environment";

        [MenuItem("Tools/MetaAudioSDK Demo/Build Ritual Circle Boundary And Bake")]
        public static void Run()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[RitualCircle] A bake is already running."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var roots = scene.GetRootGameObjects();
            var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).FirstOrDefault();
            if (terrain == null) { Debug.LogError("[RitualCircle] No terrain in the scene."); return; }
            var altar = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Demonic Altar");
            var centre = altar != null ? altar.position : Vector3.zero;

            var spikes = new[] { "RitualSpike0", "RitualSpike1", "RitualSpike2" }.Select(LoadMesh).ToArray();
            var chain = LoadMesh("RitualChain");
            if (spikes.Any(m => m == null) || chain == null) return;
            float chord = 2f * Radius * Mathf.Sin(Mathf.PI / Segments);
            float modelled = chain.bounds.size.x;
            if (Mathf.Abs(modelled - chord) > 0.15f)
                Debug.LogWarning($"[RitualCircle] The chain mesh is {modelled:F3} m long, the ring needs {chord:F3} m; links will be stretched.");

            var obsidian = Obsidian();
            var iron = Iron();
            var ember = Ember();
            CheckTerrainCovers(terrain, centre);

            var environment = roots.FirstOrDefault(r => r.name == EnvironmentName);
            if (environment == null)
            {
                environment = new GameObject(EnvironmentName);
                Undo.RegisterCreatedObjectUndo(environment, "Ritual circle");
            }
            foreach (var old in environment.transform.Cast<Transform>().Where(t => t.name is RootName or WallsName).ToList())
                Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Ritual circle");
            root.transform.SetParent(environment.transform, false);
            var walls = new GameObject(WallsName);
            Undo.RegisterCreatedObjectUndo(walls, "Ritual circle");
            walls.transform.SetParent(environment.transform, false);

            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(root, flags);

            var ground = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = 2f * Mathf.PI * i / Segments;
                ground[i] = Ground(terrain, centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Radius);
            }

            var probes = new List<Vector3>();
            for (int i = 0; i < Segments; i++)
            {
                Vector3 p = ground[i], q = ground[(i + 1) % Segments];

                var spike = Spawn($"Spike {i + 1}", spikes[Hash(i) % spikes.Length], new[] { obsidian, iron, ember }, root.transform,
                    p + Vector3.down * SinkSpikes, Quaternion.Euler(0f, Hash(i + 101) % 360, 0f), Vector3.one);

                // The chain hangs from band to band; its sag is modelled in, so only length (barely) and slope change.
                Vector3 c0 = p + Vector3.up * (ChainHeight - SinkSpikes), c1 = q + Vector3.up * (ChainHeight - SinkSpikes);
                Vector3 span = c1 - c0;
                var rot = Quaternion.LookRotation(span, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);
                Spawn($"Chain {i + 1}", chain, new[] { iron }, spike.transform.parent, (c0 + c1) * 0.5f, rot,
                    new Vector3(span.magnitude / modelled, 1f, 1f));

                // One invisible wall per chain, slightly longer so the corners close.
                var flat = new Vector3(span.x, 0f, span.z);
                var wall = new GameObject($"Wall {i + 1}");
                wall.transform.SetParent(walls.transform, false);
                wall.transform.SetPositionAndRotation((p + q) * 0.5f + Vector3.up * (WallHeight * 0.5f), Quaternion.LookRotation(flat));
                var box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(WallThickness, WallHeight, flat.magnitude + 0.4f);

                // Probes inside and outside the ring at every other spike, so the spikes and chains are lit by
                // the same baked light as the ground they stand on.
                if (i % 2 == 0)
                {
                    var outward = new Vector3(p.x - centre.x, 0f, p.z - centre.z).normalized;
                    foreach (var offset in new[] { -1.2f, 1.2f })
                    {
                        var g = Ground(terrain, new Vector3(p.x, 0f, p.z) + outward * offset);
                        probes.AddRange(ProbeHeights.Select(h => g + Vector3.up * h));
                    }
                }
            }
            var probeGroup = new GameObject("Ritual Circle Probes");
            probeGroup.transform.SetParent(root.transform, false);
            probeGroup.AddComponent<LightProbeGroup>().probePositions = probes.ToArray();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RitualCircle] {Segments} spikes, {Segments} chains and walls on a {Radius} m ring around {centre:F1}, " +
                      $"{probes.Count} probes. Baking, this takes a few minutes...");

            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
            if (!Lightmapping.BakeAsync())
            {
                Lightmapping.bakeCompleted -= OnBakeCompleted;
                Debug.LogError("[RitualCircle] Could not start the bake.");
            }
        }

        static void OnBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RitualCircle] Bake finished: {LightmapSettings.lightmaps.Length} lightmap(s). Scene saved.");
            RenderPreviews(scene);
        }

        static void RenderPreviews(UnityEngine.SceneManagement.Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).First();
            var altar = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Demonic Altar");
            var centre = altar != null ? altar.position : Vector3.zero;
            var rig = PlayerRigUtility.FindRig();
            var eye = (rig != null ? rig.transform.position : centre + Vector3.forward * 10f) + Vector3.up * 1.7f;
            var forward = rig != null ? rig.transform.forward : Vector3.back;
            var edge = Ground(terrain, centre + new Vector3(0.6f, 0f, -0.8f) * Radius);
            ScenePreviewRenderer.Render("RitualCirclePreview",
                new ScenePreviewRenderer.View("player_ahead", eye, eye + forward * 10f - Vector3.up * 0.8f),
                new ScenePreviewRenderer.View("player_behind", eye, eye - forward * 10f - Vector3.up * 0.8f),
                new ScenePreviewRenderer.View("player_left", eye, eye + Quaternion.Euler(0f, -90f, 0f) * forward * 10f - Vector3.up * 0.8f),
                new ScenePreviewRenderer.View("overview", centre + new Vector3(-24f, 22f, -24f), centre),
                new ScenePreviewRenderer.View("close", edge + (centre - edge).normalized * 3.2f + Vector3.up * 1.6f, edge + Vector3.up * 0.9f));
        }

        static void CheckTerrainCovers(Terrain terrain, Vector3 centre)
        {
            var min = terrain.transform.position;
            var max = min + terrain.terrainData.size;
            if (centre.x - Radius - 2f < min.x || centre.x + Radius + 2f > max.x || centre.z - Radius - 2f < min.z || centre.z + Radius + 2f > max.z)
                Debug.LogWarning($"[RitualCircle] The ring reaches past the terrain (x {min.x:F1}..{max.x:F1}, z {min.z:F1}..{max.z:F1}); " +
                                 "spikes there stand on the far ground height.");
        }

        static Vector3 Ground(Terrain terrain, Vector3 p)
        {
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        static int Hash(int i)
        {
            unchecked
            {
                uint h = (uint)(i + 17) * 2654435761u;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        static GameObject Spawn(string name, Mesh mesh, Material[] mats, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.receiveGI = ReceiveGI.LightProbes;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            mr.shadowCastingMode = ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(parent.gameObject));
            return go;
        }

        static Mesh LoadMesh(string name)
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
            {
                Debug.LogError($"[RitualCircle] Missing model at {FbxPath}.");
                return null;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
            var mesh = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            if (mesh == null) Debug.LogError($"[RitualCircle] Mesh '{name}' not found in {FbxPath}.");
            return mesh;
        }

        static Material Lit(string name)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_BaseMap", null);
            return mat;
        }

        static Material Save(Material mat)
        {
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        /// <summary>Black volcanic glass: glossy, so the blood-red sun glints along its edges.</summary>
        static Material Obsidian()
        {
            var mat = Lit("Ritual_Obsidian");
            mat.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.05f));
            mat.SetFloat("_Smoothness", 0.8f);
            return Save(mat);
        }

        /// <summary>Rusted iron for the bands and chains, mottled with the temple's stone grain.</summary>
        static Material Iron()
        {
            var mat = Lit("Ritual_RustedIron");
            mat.SetColor("_BaseColor", new Color(0.36f, 0.19f, 0.1f));
            mat.SetFloat("_Metallic", 0.55f);
            mat.SetFloat("_Smoothness", 0.3f);
            var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(StoneGrainPath);
            mat.SetTexture("_DetailAlbedoMap", grain);
            mat.SetTextureScale("_DetailAlbedoMap", Vector2.one * 3f);
            mat.SetFloat("_DetailAlbedoMapScale", 1f);
            if (grain != null) mat.EnableKeyword("_DETAIL_MULX2"); else mat.DisableKeyword("_DETAIL_MULX2");
            return Save(mat);
        }

        /// <summary>Smouldering ember tips. Baked emissive, so each tip casts a faint red glow on the ground.</summary>
        static Material Ember()
        {
            var mat = Lit("Ritual_Ember");
            mat.SetColor("_BaseColor", new Color(0.9f, 0.28f, 0.05f));
            mat.SetFloat("_Smoothness", 0.4f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.26f, 0.04f) * 3f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            return Save(mat);
        }
    }
}
