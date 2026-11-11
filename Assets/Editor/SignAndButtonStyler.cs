using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// Restyles the wooden sign text and the world-space "next scene" buttons of the active scene.
    /// The words stay the same; only typography, colours and button shape change. Safe to re-run.
    /// </summary>
    public static class SignAndButtonStyler
    {
        const string Folder = "Assets/Fonts";
        const string PillSpritePath = "Assets/Art/UI/Pill.png";
        const int PillTextureSize = 128;
        const float ButtonHeight = 56f;           // canvas units
        const float ButtonHorizontalPadding = 36f;
        const float ButtonRimWidth = 4f;          // canvas units
        const string ButtonFillName = "Fill";

        /// <summary>Typography and colours for one look.</summary>
        public sealed class Theme
        {
            public string Name;
            public string SignFontTtf, SignFontAssetPath, SignMaterialPath;
            public Color SignInk;
            public Color UnderlayColor;
            public Vector2 UnderlayOffset;
            public float UnderlayDilate, UnderlaySoftness, FaceDilate, SignLineSpacing;
            /// <summary>Largest font size per unit of text-area height; autosize shrinks it to fit.</summary>
            public float SignSizeFactor;
            /// <summary>A TMP font asset path, or null to use the sign font for the button label too.</summary>
            public string ButtonFontPath;
            public Color ButtonNormal, ButtonHover, ButtonPressed, ButtonLabel;
            /// <summary>Alpha 0 means no rim.</summary>
            public Color ButtonRim;
        }

        /// <summary>Painted cream lettering and a dark pill button with Meta-blue hover.</summary>
        public static readonly Theme Default = new()
        {
            Name = "Default",
            SignFontTtf = Folder + "/ZillaSlab-Bold.ttf",
            SignFontAssetPath = Folder + "/ZillaSlab-Bold SDF.asset",
            SignMaterialPath = Folder + "/ZillaSlab-Bold SDF - Painted.mat",
            SignInk = new Color(0.97f, 0.91f, 0.78f), // warm cream paint
            UnderlayColor = new Color(0.08f, 0.05f, 0.03f, 0.75f), // a soft, close shadow so the paint sits on the wood
            UnderlayOffset = new Vector2(0.35f, -0.45f),
            UnderlayDilate = 0.15f, UnderlaySoftness = 0.35f, FaceDilate = 0.08f, SignLineSpacing = -6f,
            SignSizeFactor = 1.55f, // 4-5 lines on the Ambisonics welcome sign
            ButtonFontPath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Fonts/Roboto-Medium SDF.asset",
            ButtonNormal = new Color(0.11f, 0.12f, 0.14f, 1f),
            ButtonHover = new Color(0.10f, 0.45f, 0.95f, 1f), // Meta blue
            ButtonPressed = new Color(0.06f, 0.30f, 0.70f, 1f),
            ButtonLabel = new Color(0.96f, 0.97f, 0.98f),
            ButtonRim = Color.clear,
        };

        /// <summary>Dark ink lettering for light paper (posters), with the default button.</summary>
        public static readonly Theme Workshop = new()
        {
            Name = "Workshop",
            SignFontTtf = Default.SignFontTtf,
            SignFontAssetPath = Default.SignFontAssetPath,
            SignMaterialPath = Folder + "/ZillaSlab-Bold SDF - Ink.mat",
            SignInk = new Color(0.13f, 0.11f, 0.10f), // printed black ink, slightly warm
            UnderlayColor = new Color(0f, 0f, 0f, 0f),
            UnderlayOffset = Vector2.zero,
            UnderlayDilate = 0f, UnderlaySoftness = 0f, FaceDilate = 0.05f, SignLineSpacing = -6f,
            SignSizeFactor = 1.55f,
            ButtonFontPath = Default.ButtonFontPath,
            ButtonNormal = Default.ButtonNormal, ButtonHover = Default.ButtonHover, ButtonPressed = Default.ButtonPressed,
            ButtonLabel = Default.ButtonLabel, ButtonRim = Color.clear,
        };

        /// <summary>Glowing ember blackletter, and an obsidian button with a smouldering rim that flares blood red.</summary>
        public static readonly Theme Demonic = new()
        {
            Name = "Demonic",
            SignFontTtf = Folder + "/PirataOne-Regular.ttf",
            SignFontAssetPath = Folder + "/PirataOne-Regular SDF.asset",
            SignMaterialPath = Folder + "/PirataOne-Regular SDF - Ember.mat",
            SignInk = new Color(1f, 0.36f, 0.12f), // TMP is unlit, so the letters glow in the dark scene
            UnderlayColor = new Color(0.7f, 0.04f, 0f, 0.6f), // a soft red halo instead of a shadow
            UnderlayOffset = Vector2.zero,
            UnderlayDilate = 0.45f, UnderlaySoftness = 0.85f, FaceDilate = 0.05f, SignLineSpacing = -14f,
            SignSizeFactor = 2.6f, // a short instruction: fill the panel so it reads from the player's start
            ButtonFontPath = null,
            ButtonNormal = new Color(0.07f, 0.02f, 0.02f, 1f),
            ButtonHover = new Color(0.62f, 0.05f, 0.03f, 1f),
            ButtonPressed = new Color(0.36f, 0.02f, 0.01f, 1f),
            ButtonLabel = new Color(0.98f, 0.86f, 0.72f), // bone
            ButtonRim = new Color(1f, 0.32f, 0.08f, 0.9f),
        };

        /// <summary>
        /// A Roman inscription on sandstone: Cinzel capitals in dark red-brown pigment, with a pale lower edge so
        /// the letters read as cut into the stone. The default button.
        /// </summary>
        public static readonly Theme Temple = new()
        {
            Name = "Temple",
            SignFontTtf = Folder + "/Cinzel-Bold.ttf",
            SignFontAssetPath = Folder + "/Cinzel-Bold SDF.asset",
            SignMaterialPath = Folder + "/Cinzel-Bold SDF - Inscribed.mat",
            SignInk = new Color(0.32f, 0.13f, 0.08f), // iron-oxide pigment filling the cut letters
            UnderlayColor = new Color(1f, 0.95f, 0.84f, 0.55f), // light catching the lower edge of each cut
            UnderlayOffset = new Vector2(0.15f, -0.4f),
            UnderlayDilate = 0.1f, UnderlaySoftness = 0.25f, FaceDilate = 0.05f, SignLineSpacing = 4f,
            SignSizeFactor = 1.3f, // a heading and a few lines filling the stele's panel
            ButtonFontPath = Default.ButtonFontPath,
            ButtonNormal = Default.ButtonNormal, ButtonHover = Default.ButtonHover, ButtonPressed = Default.ButtonPressed,
            ButtonLabel = Default.ButtonLabel, ButtonRim = Color.clear,
        };

        [MenuItem("Tools/MetaAudioSDK Demo/Style Sign And Button")]
        public static void Apply() => Apply(Default);

        public static void Apply(Theme theme)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var signs = roots.SelectMany(r => r.GetComponentsInChildren<TextMeshPro>(true)).ToList();
            var buttons = roots.SelectMany(r => r.GetComponentsInChildren<Button>(true))
                .Where(b => b.GetComponentInChildren<TextMeshProUGUI>(true) != null)
                .Where(b => b.GetComponentInParent<SceneDoor>() == null).ToList(); // a door is its own button (RoomAcoustics)
            if (signs.Count == 0 && buttons.Count == 0)
            {
                Debug.LogWarning($"[SignAndButtonStyler] Nothing to style in scene '{scene.name}'.");
                return;
            }

            var views = new List<ScenePreviewRenderer.View>();
            AddViews(views, "before", signs, buttons);
            ScenePreviewRenderer.Render("StylePreview", views.ToArray());

            var signFont = GetOrCreateSignFont(theme);
            var signMaterial = GetOrCreateSignMaterial(theme, signFont);
            foreach (var sign in signs) StyleSign(theme, sign, signFont, signMaterial);

            var buttonFont = theme.ButtonFontPath == null ? signFont : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(theme.ButtonFontPath);
            if (buttonFont == null) Debug.LogError($"[SignAndButtonStyler] Button font not found at {theme.ButtonFontPath}.");
            var pill = GetOrCreatePillSprite();
            foreach (var button in buttons) StyleButton(theme, button, buttonFont, pill);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[SignAndButtonStyler] Styled {signs.Count} sign text(s) and {buttons.Count} button(s) ({theme.Name} theme). Save the scene to keep it.");

            views.Clear();
            AddViews(views, "after", signs, buttons);
            ScenePreviewRenderer.Render("StylePreview", views.ToArray());
        }

        // ---------- sign ----------

        static void StyleSign(Theme theme, TextMeshPro tmp, TMP_FontAsset font, Material material)
        {
            Undo.RecordObject(tmp, "Style Sign");
            tmp.font = font;
            tmp.fontSharedMaterial = material;
            tmp.color = theme.SignInk;
            tmp.fontStyle = FontStyles.Normal; // the font itself is bold, no faux bold
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = 0f;
            tmp.wordSpacing = 0f;
            tmp.lineSpacing = theme.SignLineSpacing;
            // Size from the sign's own text area (rect minus margins), so any board scale works.
            var area = tmp.rectTransform.rect.height - tmp.margin.y - tmp.margin.w;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = theme.SignSizeFactor * area;
            tmp.fontSizeMin = 0.35f * tmp.fontSizeMax;
            tmp.text = FormatSignText(tmp.text);
            tmp.ForceMeshUpdate();
            EditorUtility.SetDirty(tmp);
        }

        /// <summary>
        /// Puts the first sentence on its own, larger line and keeps a trailing smiley attached to the
        /// last word. The words themselves are unchanged.
        /// </summary>
        static string FormatSignText(string text)
        {
            if (text.Contains("<size=")) return text; // already formatted
            text = text.Replace(" :)", " :)");
            int split = text.IndexOfAny(new[] { '!', '.', '?' });
            if (split < 0 || split == text.Length - 1) return text;
            string head = text.Substring(0, split + 1).Trim();
            string body = text.Substring(split + 1).Trim();
            return $"<size=125%>{head}</size>\n<size=40%> </size>\n{body}";
        }

        static TMP_FontAsset GetOrCreateSignFont(Theme theme)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(theme.SignFontAssetPath);
            if (existing != null) return existing;

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(theme.SignFontTtf);
            var asset = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            asset.name = Path.GetFileNameWithoutExtension(theme.SignFontAssetPath);
            // Bake printable ASCII plus typographic punctuation, then freeze the atlas.
            var chars = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + " ‘’“”–—…äöüÄÖÜßé";
            asset.TryAddCharacters(chars, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[SignAndButtonStyler] Font is missing: {missing}");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(asset, theme.SignFontAssetPath);
            asset.atlasTexture.name = asset.name + " Atlas";
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static Material GetOrCreateSignMaterial(Theme theme, TMP_FontAsset font)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(theme.SignMaterialPath);
            if (mat == null)
            {
                mat = new Material(font.material) { name = Path.GetFileNameWithoutExtension(theme.SignMaterialPath) };
                AssetDatabase.CreateAsset(mat, theme.SignMaterialPath);
            }
            mat.shader = font.material.shader;
            mat.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, theme.FaceDilate);
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, theme.UnderlayColor);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, theme.UnderlayOffset.x);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, theme.UnderlayOffset.y);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, theme.UnderlayDilate);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, theme.UnderlaySoftness);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        // ---------- button ----------

        static void StyleButton(Theme theme, Button button, TMP_FontAsset font, Sprite pill)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            var rect = (RectTransform)button.transform;

            Undo.RecordObjects(new Object[] { button, label, label.rectTransform, rect, image }, "Style Button");

            if (font != null) label.font = font;
            label.fontSharedMaterial = label.font.material;
            label.color = theme.ButtonLabel;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = Vector4.zero;
            label.characterSpacing = 0f;
            // Keep any 180 degree flip (the label is read from the canvas' back), but drop odd squashing.
            var s = label.rectTransform.localScale;
            label.rectTransform.localScale = new Vector3(Mathf.Sign(s.x), Mathf.Sign(s.y), 1f);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.rectTransform.anchoredPosition = Vector2.zero;

            float width = label.GetPreferredValues(label.text, Mathf.Infinity, ButtonHeight).x + 2f * ButtonHorizontalPadding;
            rect.sizeDelta = new Vector2(width, ButtonHeight);

            SetPill(image, pill, ButtonHeight);
            image.color = Color.white;

            // With a rim, the button's own image is the (untinted) rim and a slightly smaller "Fill" child is
            // what the hover tint colours; tinting the rim itself would darken it along with the fill.
            var fillRect = rect.Find(ButtonFillName) as RectTransform;
            Graphic tintTarget = image;
            if (theme.ButtonRim.a > 0f)
            {
                if (fillRect == null)
                {
                    var go = new GameObject(ButtonFillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(go, "Style Button");
                    fillRect = (RectTransform)go.transform;
                    fillRect.SetParent(rect, false);
                    go.layer = button.gameObject.layer;
                }
                fillRect.SetAsFirstSibling(); // behind the label
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.one * ButtonRimWidth;
                fillRect.offsetMax = -Vector2.one * ButtonRimWidth;
                fillRect.localScale = Vector3.one;
                var fill = fillRect.GetComponent<Image>();
                SetPill(fill, pill, ButtonHeight - 2f * ButtonRimWidth);
                fill.color = Color.white;
                fill.raycastTarget = false;
                image.color = theme.ButtonRim;
                tintTarget = fill;
                EditorUtility.SetDirty(fill);
            }
            else if (fillRect != null) Undo.DestroyObjectImmediate(fillRect.gameObject);
            button.targetGraphic = tintTarget;
            // Clear any tint the button left on the rim while it was the target (not serialized, but it shows in the editor).
            image.canvasRenderer.SetColor(Color.white);
            var oldOutline = image.GetComponent<Outline>(); // earlier rim attempt; the tint darkened it
            if (oldOutline != null) Undo.DestroyObjectImmediate(oldOutline);

            var colors = button.colors;
            colors.normalColor = theme.ButtonNormal;
            colors.highlightedColor = theme.ButtonHover;
            colors.pressedColor = theme.ButtonPressed;
            colors.selectedColor = theme.ButtonNormal; // don't stay lit after a click
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            FitInteractionCollider(button, rect);

            foreach (var o in new Object[] { button, label, image, rect }) EditorUtility.SetDirty(o);
        }

        static void SetPill(Image image, Sprite pill, float height)
        {
            image.sprite = pill;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            // Corner radius = half the height, so the ends are perfect semicircles.
            image.pixelsPerUnitMultiplier = (PillTextureSize * 0.5f) / (height * 0.5f) * (100f / pill.pixelsPerUnit);
        }

        /// <summary>
        /// The Interaction SDK ray hits a box collider sized to the button; keep it matching the new size.
        /// </summary>
        static void FitInteractionCollider(Button button, RectTransform rect)
        {
            var canvas = button.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            if (canvas.GetComponentsInChildren<Button>(true).Length != 1) return; // ambiguous, leave alone
            var box = canvas.GetComponentsInChildren<BoxCollider>(true)
                .FirstOrDefault(c => c.transform.parent == canvas.transform && c.size == Vector3.one);
            if (box == null) return;
            Undo.RecordObject(box.transform, "Fit Button Collider");
            var local = canvas.transform.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            var t = box.transform;
            t.localPosition = new Vector3(local.x, local.y, t.localPosition.z);
            t.localScale = new Vector3(rect.rect.width, rect.rect.height, t.localScale.z);
            EditorUtility.SetDirty(t);
        }

        static Sprite GetOrCreatePillSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PillSpritePath);
            if (sprite != null) return sprite;

            Directory.CreateDirectory(Path.GetDirectoryName(PillSpritePath)!);
            int n = PillTextureSize;
            float r = n * 0.5f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f); // 1 px antialiased edge
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(PillSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PillSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PillSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(r - 1, r - 1, r - 1, r - 1);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(PillSpritePath);
        }

        // ---------- previews ----------

        static void AddViews(List<ScenePreviewRenderer.View> views, string prefix, List<TextMeshPro> signs, List<Button> buttons)
        {
            foreach (var sign in signs)
            {
                var b = sign.GetComponent<Renderer>().bounds;
                // 3D text is read looking along its +Z axis.
                views.Add(new ScenePreviewRenderer.View($"{prefix}_sign_close", b.center - sign.transform.forward * 2.2f, b.center));
                views.Add(new ScenePreviewRenderer.View($"{prefix}_sign_player", b.center - sign.transform.forward * 5f + Vector3.up * 0.2f, b.center));
            }
            int i = 0;
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                var c = rect.TransformPoint(rect.rect.center);
                // Read the label from the side its text faces.
                var readDir = label.transform.forward;
                var img = button.targetGraphic;
                var hover = button.colors.highlightedColor;
                var normal = button.colors.normalColor;
                views.Add(new ScenePreviewRenderer.View($"{prefix}_button{i}_close", c - readDir * 3f, c,
                    () => img.canvasRenderer.SetColor(normal)));
                views.Add(new ScenePreviewRenderer.View($"{prefix}_button{i}_hover", c - readDir * 3f, c,
                    () => img.canvasRenderer.SetColor(hover)));
                views.Add(new ScenePreviewRenderer.View($"{prefix}_button{i}_player", c - readDir * 6f + Vector3.up * 0.9f, c,
                    () => img.canvasRenderer.SetColor(normal)));
                i++;
            }
        }
    }
}
