#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace BeastSoccer.EditorTools
{
    /// <summary>
    /// Import/build helpers for the temporary Leo sprite test. Existing user-made
    /// Leo_Run_Side / Leo_Run_3Q .anim clips are never deleted or recreated.
    /// FIX19 only auto-builds the NEW front-run, shoot, and ult clips from the PNGs
    /// shipped under Assets/Art/Leo/TempAuto.
    /// </summary>
    public static class SpriteArtTools
    {
        private const float ExistingLeoRunPivotY = 0.10f;
        private const float ExistingLeoRunPPU = 250f;  // existing 500x500 art
        private const float TempLeoPivotY = 0.10f;
        private const float TempLeoPPU = 750f;         // new 1500x1500 art -> same world scale
        private const float VoltRunPPU = 375f;          // 750x750 Volt art -> similar world scale
        private const float VoltRunPivotY = 0.10f;

        [MenuItem("Beast Soccer/Sprites/Prepare Leo Run Test Assets")]
        public static void PrepareLeoRunTestAssets()
        {
            PrepareFolder("Assets/Art/Leo/Run", ExistingLeoRunPPU, ExistingLeoRunPivotY, false);
        }

        public static void PrepareLeoTempAnimationAssets()
        {
            const string root = "Assets/Art/Leo/TempAuto";
            if (!AssetDatabase.IsValidFolder(root)) return;

            PrepareFolder(root, TempLeoPPU, TempLeoPivotY, true);
            AssetDatabase.Refresh();

            BuildClipFromFolder(
                "Assets/Art/Leo/TempAuto/RunFront",
                "Assets/Generated/Leo_Run_Front.anim",
                12f,
                true);

            BuildClipFromFolder(
                "Assets/Art/Leo/TempAuto/ShootFront3Q",
                "Assets/Generated/Leo_Shoot_Temp.anim",
                12f,
                false);

            BuildClipFromFolder(
                "Assets/Art/Leo/TempAuto/Ult",
                "Assets/Generated/Leo_Ult_Temp.anim",
                10f,
                false);
        }

        public static void PrepareVoltRunAssets()
        {
            const string root = "Assets/Art/Volt/RunAuto";
            if (!AssetDatabase.IsValidFolder(root)) return;
            PrepareFolder(root, VoltRunPPU, VoltRunPivotY, true);
            AssetDatabase.Refresh();

            BuildClipFromFolder(root + "/Side", "Assets/Generated/Volt_Run_Side.anim", 12f, true);
            BuildClipFromFolder(root + "/Front3Q", "Assets/Generated/Volt_Run_Front3Q.anim", 12f, true);
            BuildClipFromFolder(root + "/Front", "Assets/Generated/Volt_Run_Front.anim", 12f, true);
            BuildClipFromFolder(root + "/Back3Q", "Assets/Generated/Volt_Run_Back3Q.anim", 12f, true);
            BuildClipFromFolder(root + "/Back", "Assets/Generated/Volt_Run_Back.anim", 12f, true);
        }

        public static void PrepareUIAndPitchAssets()
        {
            const string uiRoot = "Assets/Art/UI";
            if (AssetDatabase.IsValidFolder(uiRoot))
                PrepareFolder(uiRoot, 100f, 0.5f, true);

            const string pitchPath = "Assets/Art/Pitch/Pitch_Cropped.jpg";
            var pitchImporter = AssetImporter.GetAtPath(pitchPath) as TextureImporter;
            if (pitchImporter != null)
            {
                pitchImporter.textureType = TextureImporterType.Default;
                pitchImporter.mipmapEnabled = false;
                pitchImporter.textureCompression = TextureImporterCompression.Uncompressed;
                pitchImporter.wrapMode = TextureWrapMode.Clamp;
                pitchImporter.filterMode = FilterMode.Bilinear;
                pitchImporter.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static void PrepareFolder(string root, float ppu, float pivotY, bool forceNoMipMaps)
        {
            if (!AssetDatabase.IsValidFolder(root)) return;

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (Mathf.Abs(importer.spritePixelsPerUnit - ppu) > .01f) { importer.spritePixelsPerUnit = ppu; dirty = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (forceNoMipMaps && importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Vector2 wantedPivot = new Vector2(.5f, pivotY);
                if (settings.spriteAlignment != (int)SpriteAlignment.Custom) { settings.spriteAlignment = (int)SpriteAlignment.Custom; dirty = true; }
                if ((settings.spritePivot - wantedPivot).sqrMagnitude > .000001f) { settings.spritePivot = wantedPivot; dirty = true; }
                if (settings.spriteMeshType != SpriteMeshType.FullRect) { settings.spriteMeshType = SpriteMeshType.FullRect; dirty = true; }

                if (dirty)
                {
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            if (changed > 0)
                Debug.Log($"[Beast Soccer] Prepared {changed} Leo sprite PNG(s) under {root}. Existing user .anim clips were preserved.");
        }

        private static AnimationClip BuildClipFromFolder(string folder, string clipPath, float fps, bool loop)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            EnsureGeneratedFolder();

            var spritePaths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    spritePaths.Add(path);
            }
            spritePaths.Sort(System.StringComparer.OrdinalIgnoreCase);
            if (spritePaths.Count == 0) return null;

            var sprites = new List<Sprite>();
            foreach (string path in spritePaths)
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) sprites.Add(s);
            }
            if (sprites.Count == 0) return null;

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                clip.name = Path.GetFileNameWithoutExtension(clipPath);
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.ClearCurves();
            clip.frameRate = fps;
            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = sprites[i]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return clip;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
        }

        [MenuItem("Beast Soccer/Sprites/Copy Active Sprite Pivot To Selected PNGs")]
        public static void CopyActivePivotToSelected()
        {
            string refPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            var reference = AssetImporter.GetAtPath(refPath) as TextureImporter;
            if (reference == null || reference.textureType != TextureImporterType.Sprite)
            {
                Debug.LogError("[Beast Soccer] Make the correctly-pivoted sprite the active selected PNG first.");
                return;
            }

            var referenceSettings = new TextureImporterSettings();
            reference.ReadTextureSettings(referenceSettings);
            Vector2 pivot = referenceSettings.spritePivot;
            int changed = 0;
            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }
            Debug.Log($"[Beast Soccer] Copied pivot ({pivot.x:0.###}, {pivot.y:0.###}) to {changed} selected sprite(s). Animation clips were preserved.");
        }
    }
}
#endif
