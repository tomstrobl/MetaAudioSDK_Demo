using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// Makes the editor render like the Quest: selects the "Mobile" quality level (the one Android uses)
    /// and disables SSAO on the PC renderer, which is costly in VR and darkens distant cut-out foliage.
    /// </summary>
    public static class QuestEditorPreview
    {
        [MenuItem("Tools/MetaAudioSDK Demo/Match Editor To Quest Rendering")]
        public static void Apply()
        {
            int mobile = System.Array.IndexOf(QualitySettings.names, "Mobile");
            if (mobile >= 0) QualitySettings.SetQualityLevel(mobile, true);

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var feature in data.rendererFeatures.Where(f => f != null && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion")))
                {
                    if (!feature.isActive) continue;
                    feature.SetActive(false);
                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(data);
                    Debug.Log($"[QuestEditorPreview] Disabled {feature.name} on {data.name}.");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestEditorPreview] Editor quality level is now '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'.");
        }
    }
}
