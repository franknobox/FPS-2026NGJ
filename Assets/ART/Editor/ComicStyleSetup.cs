using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArtTools
{
    public static class ComicStyleSetup
    {
        public const string SettingsPath = "Assets/ART/Settings/ComicStyle.asset";
        const string RenderFolder = "Assets/ART/Settings/Rendering";
        const string BackupFolder = "Assets/ART/Settings/OriginalMaterials";
        const string ToonName = "ART/Comic/Toon Lit";
        const string InkName = "Hidden/ART/Comic/Screen Ink";
        public static bool IsApplying { get; private set; }

        public static ComicStyleSettings GetSettings(bool create = false)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ComicStyleSettings>(SettingsPath);
            if (settings || !create) return settings;
            EnsureFolder("Assets/ART/Settings");
            settings = ScriptableObject.CreateInstance<ComicStyleSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        [MenuItem("美术工具/漫画渲染/应用到全部ART材质")]
        public static void ApplyAll()
        {
            ApplyMaterials(AssetDatabase.FindAssets("t:Material", new[] { "Assets/ART/Materials" })
                .Select(AssetDatabase.GUIDToAssetPath));
        }

        [MenuItem("美术工具/漫画渲染/应用到选中资源")]
        public static void ApplySelected() { ApplyMaterials(SelectedMaterials()); }

        [MenuItem("美术工具/漫画渲染/打开风格设置")]
        public static void SelectSettings() { Selection.activeObject = GetSettings(true); }

        public static void ApplyMaterials(IEnumerable<string> paths)
        {
            if (IsApplying) return;
            IsApplying = true;
            try
            {
                var settings = GetSettings(true);
                var shader = Shader.Find(ToonName);
                if (!shader) throw new Exception("Comic shader has not imported yet");
                CheckShader(shader);
                int changed = 0;
                foreach (string path in paths.Distinct())
                {
                    if (!path.StartsWith("Assets/") || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || path.StartsWith(BackupFolder)) continue;
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material) settings.autoStyleExclusions.Remove(material);
                    if (material && ConvertMaterial(material, path, settings, shader)) changed++;
                }
                InstallRenderers(settings);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                Debug.Log("COMIC_STYLE_APPLIED materials=" + changed);
            }
            finally { IsApplying = false; }
        }

        static bool ConvertMaterial(Material material, string path, ComicStyleSettings settings, Shader shader)
        {
            string oldShader = material.shader ? material.shader.name : "";
            bool supported = oldShader == ToonName || oldShader == "Universal Render Pipeline/Lit" ||
                oldShader == "Universal Render Pipeline/Simple Lit" || oldShader == "Universal Render Pipeline/Unlit" ||
                oldShader == "Standard" || oldShader == "Standard (Specular setup)" || oldShader == "Unlit/WorldSpace";
            if (!supported || material.renderQueue >= 3000 || (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f)) return false;
            if (oldShader != ToonName)
            {
                EnsureFolder(BackupFolder);
                string backup = BackupFolder + "/" + AssetDatabase.AssetPathToGUID(path) + ".mat";
                if (!AssetDatabase.LoadAssetAtPath<Material>(backup)) AssetDatabase.CopyAsset(path, backup);
                bool legacy = oldShader.StartsWith("Standard") || oldShader == "Unlit/WorldSpace";
                string mapProperty = legacy ? "_MainTex" : "_BaseMap";
                string colorProperty = legacy ? "_Color" : "_BaseColor";
                var texture = material.HasProperty(mapProperty) ? material.GetTexture(mapProperty) : null;
                Vector2 tiling = material.HasProperty(mapProperty) ? material.GetTextureScale(mapProperty) : Vector2.one;
                Vector2 offset = material.HasProperty(mapProperty) ? material.GetTextureOffset(mapProperty) : Vector2.zero;
                Color color = material.HasProperty(colorProperty) ? material.GetColor(colorProperty) : Color.white;
                bool clip = material.IsKeywordEnabled("_ALPHATEST_ON") || (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > .5f);
                float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") :
                    material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : .5f;
                float cull = material.HasProperty("_Cull") ? material.GetFloat("_Cull") : 2;
                int queue = material.renderQueue;
                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
                material.SetTextureOffset("_BaseMap", offset);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Cull", cull);
                material.SetFloat("_AlphaClip", clip ? 1 : 0);
                material.SetFloat("_WorldSpaceUV", oldShader == "Unlit/WorldSpace" ? 1 : 0);
                if (clip) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
                material.renderQueue = clip ? (int)RenderQueue.AlphaTest : queue;
            }
            material.SetColor("_ShadowColor", settings.shadowColor);
            material.SetColor("_MidColor", settings.midColor);
            material.SetFloat("_ShadowThreshold", settings.shadowThreshold);
            material.SetFloat("_LightThreshold", settings.lightThreshold);
            material.SetFloat("_BandSoftness", settings.bandSoftness);
            material.SetFloat("_AmbientStrength", settings.ambientStrength);
            material.SetFloat("_AmbientFloor", settings.ambientFloor);
            material.SetFloat("_SpecularStrength", settings.specularStrength);
            material.SetFloat("_SpecularThreshold", settings.specularThreshold);
            material.SetFloat("_Saturation", settings.saturation);
            material.SetFloat("_Contrast", settings.contrast);
            material.SetFloat("_ColorLevels", settings.colorLevels);
            material.SetFloat("_BumpScale", settings.normalStrength);
            material.SetFloat("_HatchStrength", settings.hatchStrength);
            material.SetFloat("_HatchScale", settings.hatchScale);
            SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap"));
            SetKeyword(material, "_METALLICSPECGLOSSMAP", material.GetTexture("_MetallicGlossMap"));
            SetKeyword(material, "_OCCLUSIONMAP", material.GetTexture("_OcclusionMap"));
            if (material.GetColor("_EmissionColor").maxColorComponent > 0) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return true;
        }

        static void SetKeyword(Material material, string keyword, Texture texture)
        {
            if (texture) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        static IEnumerable<string> SelectedMaterials(bool extractModels = true)
        {
            var paths = new HashSet<string>();
            foreach (var selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (AssetDatabase.IsValidFolder(path))
                    foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { path })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                else if (!string.IsNullOrEmpty(path))
                {
                    foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    {
                        if (dependency.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) paths.Add(dependency);
                        if (extractModels && AssetImporter.GetAtPath(dependency) is ModelImporter)
                            foreach (string extracted in ExtractModelMaterials(dependency)) paths.Add(extracted);
                    }
                }
                if (selected is GameObject go)
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            if (material)
                            {
                                string materialPath = AssetDatabase.GetAssetPath(material);
                                if (materialPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) paths.Add(materialPath);
                                else if (extractModels && AssetImporter.GetAtPath(materialPath) is ModelImporter)
                                    foreach (string extracted in ExtractModelMaterials(materialPath)) paths.Add(extracted);
                            }
            }
            return paths;
        }

        static IEnumerable<string> ExtractModelMaterials(string modelPath)
        {
            if (!modelPath.StartsWith("Assets/")) return Array.Empty<string>();
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (!importer) return Array.Empty<string>();
            string folder = "Assets/ART/Materials/Imported/" + Path.GetFileNameWithoutExtension(modelPath) + "_" + AssetDatabase.AssetPathToGUID(modelPath).Substring(0,8);
            var results = new List<string>();
            bool changed = false;
            var remaps = importer.GetExternalObjectMap();
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(material);
                if (remaps.TryGetValue(identifier, out var existing) && existing is Material)
                {
                    results.Add(AssetDatabase.GetAssetPath(existing));
                    continue;
                }
                EnsureFolder(folder);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long id);
                string safeName = string.Concat(material.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string path = folder + "/" + safeName + "_" + id + ".mat";
                var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!copy) { copy = new Material(material); AssetDatabase.CreateAsset(copy, path); }
                importer.AddRemap(identifier, copy);
                results.Add(path);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            return results;
        }

        [MenuItem("美术工具/漫画渲染/恢复选中资源的原材质")]
        public static void RestoreSelected()
        {
            var settings = GetSettings(true);
            foreach (string path in SelectedMaterials(false))
            {
                string backup = BackupFolder + "/" + AssetDatabase.AssetPathToGUID(path) + ".mat";
                var original = AssetDatabase.LoadAssetAtPath<Material>(backup);
                var target = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!original || !target) continue;
                string name = target.name;
                EditorUtility.CopySerialized(original, target);
                target.name = name;
                if (!settings.autoStyleExclusions.Contains(target)) settings.autoStyleExclusions.Add(target);
                EditorUtility.SetDirty(target);
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        static void InstallRenderers(ComicStyleSettings settings)
        {
            if (!settings.enableInk) { RestoreRenderers(); return; }
            EnsureFolder(RenderFolder);
            var inkShader = Shader.Find(InkName);
            if (!inkShader) throw new Exception("Ink shader missing");
            CheckShader(inkShader);
            string materialPath = RenderFolder + "/ComicInk.mat";
            var ink = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!ink) { ink = new Material(inkShader); AssetDatabase.CreateAsset(ink, materialPath); }
            ink.SetColor("_InkColor", settings.inkColor);
            ink.SetFloat("_InkWidth", settings.inkWidth);
            ink.SetFloat("_DepthThreshold", settings.depthThreshold);
            ink.SetFloat("_NormalThreshold", settings.normalThreshold);
            ink.SetFloat("_NormalStrength", settings.normalStrengthInk);
            ink.SetFloat("_NormalFadeDistance", settings.normalFadeDistance);
            EditorUtility.SetDirty(ink);
            foreach (string pipelinePath in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
                if (!pipeline) continue;
                var serialized = new SerializedObject(pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                if (!settings.rendererBindings.Any(binding => binding.pipeline == pipeline))
                {
                    var originals = new ScriptableRendererData[list.arraySize];
                    for (int i = 0; i < originals.Length; i++) originals[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                    settings.rendererBindings.Add(new ComicPipelineBinding { pipeline = pipeline, originalRenderers = originals });
                }
                for (int i = 0; i < list.arraySize; i++)
                {
                    var original = list.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                    if (!original) continue;
                    string originalPath = AssetDatabase.GetAssetPath(original);
                    UniversalRendererData renderer;
                    if (originalPath.StartsWith(RenderFolder)) renderer = original;
                    else
                    {
                        string path = RenderFolder + "/Comic_" + Path.GetFileName(originalPath);
                        renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                        if (!renderer)
                        {
                            if (!AssetDatabase.CopyAsset(originalPath, path)) throw new Exception("Cannot copy renderer " + originalPath);
                            renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                        }
                        list.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
                    }
                    var feature = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(item => item.name == "Comic Ink");
                    if (!feature)
                    {
                        feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                        feature.name = "Comic Ink";
                        AssetDatabase.AddObjectToAsset(feature, renderer);
                        renderer.rendererFeatures.Add(feature);
                    }
                    feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                    feature.fetchColorBuffer = true;
                    feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
                    feature.passMaterial = ink;
                    feature.passIndex = 0;
                    feature.SetActive(true);
                    feature.Create();
                    renderer.SetDirty();
                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(renderer);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }
        }

        [MenuItem("美术工具/漫画渲染/关闭描边并恢复原渲染器")]
        public static void RestoreRenderers()
        {
            var settings = GetSettings();
            if (!settings) return;
            settings.enableInk = false;
            EditorUtility.SetDirty(settings);
            foreach (var binding in settings.rendererBindings)
            {
                if (!binding.pipeline) continue;
                var serialized = new SerializedObject(binding.pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                list.arraySize = binding.originalRenderers.Length;
                for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = binding.originalRenderers[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(binding.pipeline);
            }
            AssetDatabase.SaveAssets();
        }

        static void CheckShader(Shader shader)
        {
            var errors = ShaderUtil.GetShaderMessages(shader).Where(message => message.severity.ToString() == "Error").ToArray();
            if (errors.Length > 0) throw new Exception(shader.name + ": " + string.Join("; ", errors.Select(message => message.message)));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }

    public sealed class ComicMaterialImport : AssetPostprocessor
    {
        static readonly HashSet<string> Pending = new HashSet<string>();
        static bool queued;
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (ComicStyleSetup.IsApplying) return;
            foreach (string path in imported)
                if (path.StartsWith("Assets/ART/Materials/") && path.EndsWith(".mat")) Pending.Add(path);
            if (Pending.Count == 0 || queued) return;
            queued = true;
            EditorApplication.delayCall += ApplyPending;
        }
        static void ApplyPending()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ApplyPending;
                return;
            }
            queued = false;
            var settings = ComicStyleSetup.GetSettings();
            var paths = Pending.ToArray();
            Pending.Clear();
            if (!settings || !settings.autoStyleNewArtMaterials) return;
            paths = paths.Where(path =>
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                return material && !settings.autoStyleExclusions.Contains(material) && material.shader && material.shader.name != "ART/Comic/Toon Lit";
            }).ToArray();
            if (paths.Length > 0) ComicStyleSetup.ApplyMaterials(paths);
        }
    }
}
