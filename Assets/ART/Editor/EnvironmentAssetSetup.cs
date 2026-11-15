using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ArtTools
{
    /// <summary>Build repeatable, static environment prefabs from the Blender manifest.</summary>
    public static class EnvironmentAssetSetup
    {
        const string Root = "Assets/ART";
        const string ManifestPath = Root + "/Models/Environment/EnvironmentManifest.json";
        const string MaterialFolder = Root + "/Materials/Environment";
        const string PrefabFolder = Root + "/Prefabs/Environment";
        public const string PreviewPath = ArtShowcaseSetup.ScenePath;

        [Serializable] class Manifest { public int version; public Entry[] assets; }
        [Serializable] class Entry
        {
            public string id, label, group, fbx, materialName;
            public int originalTriangles;
            public int[] triangles;
            public float[] dimensions;
        }

        [MenuItem("美术工具/场景素材/整理FBX与场景预制体")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            Require(manifest.version == 1 && manifest.assets.Length == 14, "Unexpected environment manifest");
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            var materials = new Dictionary<string, Material>();
            var results = new List<object>();
            foreach (var entry in manifest.assets)
            {
                if (!materials.TryGetValue(entry.group, out var material))
                {
                    material = PrepareMaterial(entry.group);
                    materials.Add(entry.group, material);
                }
                var importer = AssetImporter.GetAtPath(entry.fbx) as ModelImporter;
                Require(importer != null, "Missing FBX: " + entry.fbx);
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importBlendShapes = false;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.generateSecondaryUV = true;
                importer.isReadable = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.materialName), material);
                importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbx);
                Require(source, "Model did not import: " + entry.id);
                var filters = source.GetComponentsInChildren<MeshFilter>(true);
                Require(filters.Length == 3, "Expected three LOD meshes: " + entry.id);
                var prefab = new GameObject(entry.label);
                try
                {
                    var renderers = new MeshRenderer[3];
                    var counts = new int[3];
                    for (int level = 0; level < 3; level++)
                    {
                        var filter = filters.Single(f => f.name == "LOD" + level);
                        var mesh = filter.sharedMesh;
                        Require(mesh && mesh.HasVertexAttribute(VertexAttribute.TexCoord0) && mesh.HasVertexAttribute(VertexAttribute.Normal),
                            "Missing geometry, UV or normals: " + entry.id);
                        counts[level] = Enumerable.Range(0, mesh.subMeshCount).Sum(s => (int)(mesh.GetIndexCount(s) / 3));
                        Require(counts[level] == entry.triangles[level], "Triangle count changed: " + entry.id + " LOD" + level);
                        var body = new GameObject("LOD" + level);
                        body.transform.SetParent(prefab.transform, false);
                        CopyTransform(filter.transform.localToWorldMatrix, body.transform);
                        body.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = body.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                        renderers[level] = renderer;
                    }
                    var lods = prefab.AddComponent<LODGroup>();
                    lods.fadeMode = LODFadeMode.None;
                    lods.SetLODs(new[] { new LOD(.35f, new Renderer[] { renderers[0] }),
                        new LOD(.12f, new Renderer[] { renderers[1] }), new LOD(.005f, new Renderer[] { renderers[2] }) });
                    lods.RecalculateBounds();
                    var collision = new GameObject("Collision_LOD2");
                    collision.transform.SetParent(prefab.transform, false);
                    CopyTransform(filters.Single(f => f.name == "LOD2").transform.localToWorldMatrix, collision.transform);
                    var collider = collision.AddComponent<MeshCollider>();
                    collider.sharedMesh = filters.Single(f => f.name == "LOD2").sharedMesh;
                    collider.convex = false;
                    int ground = LayerMask.NameToLayer("Ground");
                    foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        transform.gameObject.layer = ground >= 0 ? ground : 0;
                        GameObjectUtility.SetStaticEditorFlags(transform.gameObject,
                            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI |
                            StaticEditorFlags.ReflectionProbeStatic);
                    }
                    var bounds = renderers[0].bounds;
                    var expected = new Vector3(entry.dimensions[0], entry.dimensions[2], entry.dimensions[1]);
                    Require(Vector3.Distance(bounds.size, expected) < Mathf.Max(.01f, expected.magnitude * .002f),
                        "FBX scale or axis changed: " + entry.id + " " + bounds.size + " != " + expected);
                    Require(Mathf.Abs(bounds.min.y) < .04f, "Ground origin changed: " + entry.id);
                    string path = PrefabFolder + "/" + entry.label + ".prefab";
                    EnvironmentSizeSetup.ApplyToPrefab(prefab, path);
                    bounds = renderers[0].bounds;
                    var saved = PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    Require(saved && saved.GetComponent<LODGroup>().lodCount == 3, "Invalid prefab: " + entry.id);
                    Require(saved.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Unexpected gameplay scripts");
                    results.Add(new { entry.id, entry.label, prefab = path, entry.originalTriangles, triangles = counts,
                        size = new[] { bounds.size.x, bounds.size.y, bounds.size.z },
                        material = AssetDatabase.GetAssetPath(material), collisionTriangles = counts[2] });
                }
                finally { UnityEngine.Object.DestroyImmediate(prefab); }
            }
            ComicStyleSetup.ApplyMaterials(materials.Values.Select(AssetDatabase.GetAssetPath));
            foreach (var material in materials.Values)
                Require(material.shader.name == "ART/Comic/Toon Lit" && !ShaderUtil.ShaderHasError(material.shader), "Comic material error");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(".utmp/environment-preparation");
            File.WriteAllText(".utmp/environment-preparation/unity-validation.json",
                Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));
            Debug.Log("ART_ENVIRONMENT_READY prefabs=" + results.Count + " materials=" + materials.Count + " playMode=" + EditorApplication.isPlaying);
        }

        [MenuItem("美术工具/场景素材/更新资源展示")]
        public static void CreatePreview() { ArtShowcaseSetup.UpdateShowcase(); }

        public static void BuildAndPreview() { Build(); CreatePreview(); }

        static Material PrepareMaterial(string group)
        {
            string path = MaterialFolder + "/" + group + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture(group, "BaseColor", false, true));
            material.SetTexture("_BumpMap", Texture(group, "Normal", true, false));
            material.SetTexture("_MetallicGlossMap", Texture(group, "MetallicSmoothness", false, false));
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetFloat("_Cull", 0);
            if (material.shader.name != "ART/Comic/Toon Lit") material.SetFloat("_BumpScale", 1);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D Texture(string group, string suffix, bool normal, bool srgb)
        {
            string path = Root + "/Textures/Environment/" + group + "/" + group + "_" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Missing texture: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void CopyTransform(Matrix4x4 matrix, Transform target)
        {
            target.localPosition = matrix.GetColumn(3);
            target.localRotation = matrix.rotation;
            target.localScale = matrix.lossyScale;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
