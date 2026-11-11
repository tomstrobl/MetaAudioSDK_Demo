using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>Renders still images of the open scene to Temp/&lt;folder&gt; for quick visual checks.</summary>
    public static class ScenePreviewRenderer
    {
        public readonly struct View
        {
            public readonly string Name;
            public readonly Vector3 Eye, Target;
            public readonly System.Action BeforeRender;

            public View(string name, Vector3 eye, Vector3 target, System.Action beforeRender = null)
            {
                Name = name; Eye = eye; Target = target; BeforeRender = beforeRender;
            }
        }

        public static string Render(string folder, params View[] views) => Render(folder, null, views);

        /// <param name="qualityLevel">Quality level to render with (e.g. "Mobile" for Quest), or null for the current one.</param>
        public static string Render(string folder, string qualityLevel, params View[] views)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", folder);
            Directory.CreateDirectory(dir);

            int previousQuality = QualitySettings.GetQualityLevel();
            int quality = qualityLevel == null ? -1 : System.Array.IndexOf(QualitySettings.names, qualityLevel);
            if (quality >= 0 && quality != previousQuality) QualitySettings.SetQualityLevel(quality, true);

            var camGo = new GameObject("PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                var cam = camGo.AddComponent<Camera>();
                var main = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "CenterEyeAnchor");
                if (main != null) cam.CopyFrom(main);
                cam.stereoTargetEye = StereoTargetEyeMask.None;
                cam.fieldOfView = 70f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 1000f;
                cam.targetTexture = rt;
                foreach (var v in views)
                {
                    v.BeforeRender?.Invoke();
                    cam.transform.SetPositionAndRotation(v.Eye, Quaternion.LookRotation(v.Target - v.Eye));
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, v.Name + ".png"), tex.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = null;
                if (QualitySettings.GetQualityLevel() != previousQuality) QualitySettings.SetQualityLevel(previousQuality, true);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            Debug.Log($"[Preview] Renders written to {dir}");
            return dir;
        }
    }
}
