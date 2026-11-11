using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>Draws [SceneName] strings as a scene asset picker and warns when the scene isn't in the build.</summary>
    [CustomPropertyDrawer(typeof(SceneNameAttribute))]
    public class SceneNameDrawer : PropertyDrawer
    {
        const float HelpHeight = 38f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight + (Problem(property) != null ? HelpHeight + 2f : 0f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[SceneName] only works on strings.");
                return;
            }

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginProperty(line, label, property);
            var current = FindScene(property.stringValue);
            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUI.ObjectField(line, label, current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck()) property.stringValue = picked != null ? picked.name : string.Empty;
            EditorGUI.EndProperty();

            var problem = Problem(property);
            if (problem != null)
                EditorGUI.HelpBox(new Rect(position.x, line.yMax + 2f, position.width, HelpHeight), problem, MessageType.Warning);
        }

        static string Problem(SerializedProperty property)
        {
            var name = property.stringValue;
            if (string.IsNullOrEmpty(name)) return "No scene selected.";
            if (FindScene(name) == null) return $"No scene named '{name}' exists in the project.";
            bool inBuild = EditorBuildSettings.scenes.Any(s => s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == name);
            return inBuild ? null : $"'{name}' is not enabled in File > Build Profiles, so it can't be loaded.";
        }

        static SceneAsset FindScene(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // Prefer the build list, then search the project.
            var path = EditorBuildSettings.scenes.Select(s => s.path)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
            path ??= AssetDatabase.FindAssets($"t:SceneAsset {name}").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
            return path != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null;
        }
    }
}
