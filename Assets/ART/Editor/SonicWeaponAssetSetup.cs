using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArtTools
{
    // Asset preparation only: no changes to gameplay scenes or weapon logic.
    public static class SonicWeaponAssetSetup
    {
        const string Models = "Assets/ART/Models/Weapons/SonicWeapon/";
        const string Textures = "Assets/ART/Textures/Weapons/SonicWeapon/";
        const string Materials = "Assets/ART/Materials/Weapons";
        const string Prefabs = "Assets/ART/Prefabs/Weapons";

        [MenuItem("美术工具/武器/整理两把声波武器")]
        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EnsureFolder(Materials);
                EnsureFolder(Prefabs);
                var color = PrepareTexture("BaseColor", false, true);
                var normal = PrepareTexture("Normal", true, false);
                var metallic = PrepareTexture("MetallicSmoothness", false, false);
                string materialPath = Materials + "/SonicWeapon_Surface.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (!shader) throw new Exception("URP Lit shader not found");
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetTexture("_BaseMap", color);
                material.SetTexture("_BumpMap", normal);
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetColor("_BaseColor", Color.white);
                if (material.shader.name != "ART/Comic/Toon Lit") material.SetFloat("_BumpScale", 1);
                material.SetFloat("_Metallic", 1);
                material.SetFloat("_Smoothness", 1);
                material.SetFloat("_SmoothnessTextureChannel", 0);
                material.SetFloat("_Cull", 0);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(material);

                var results = new System.Collections.Generic.List<object>();
                foreach (string name in new[] { "霰弹枪", "爪型枪" })
                {
                    string path = Models + name + ".fbx";
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (!importer) throw new Exception("Missing FBX importer: " + path);
                    importer.importAnimation = false;
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.globalScale = 1;
                    importer.useFileScale = true;
                    importer.bakeAxisConversion = true;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "SonicWeapon_Body"), material);
                    importer.SaveAndReimport();

                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var filter = source.GetComponentsInChildren<MeshFilter>(true).Single();
                    var sourceMuzzle = source.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Muzzle");
                    // Include the imported model root's FBX axis conversion.
                    Matrix4x4 bodyMatrix = filter.transform.localToWorldMatrix;
                    Vector3 muzzlePosition = sourceMuzzle.position;
                    if (muzzlePosition.z <= 0) throw new Exception("Muzzle must face Unity +Z: " + name + " " + muzzlePosition);
                    string assetName = name == "霰弹枪" ? "步枪" : name;
                    var root = new GameObject(assetName);
                    try
                    {
                        var body = new GameObject("Body");
                        body.transform.SetParent(root.transform, false);
                        body.transform.localPosition = bodyMatrix.GetColumn(3);
                        body.transform.localRotation = bodyMatrix.rotation;
                        body.transform.localScale = bodyMatrix.lossyScale;
                        body.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        var renderer = body.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        var grip = new GameObject("GripSocket");
                        grip.transform.SetParent(root.transform, false);
                        var muzzle = new GameObject("Muzzle");
                        muzzle.transform.SetParent(root.transform, false);
                        muzzle.transform.localPosition = muzzlePosition;
                        int layer = LayerMask.NameToLayer("Weapons");
                        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer < 0 ? 0 : layer;
                        string prefabPath = Prefabs + "/" + assetName + ".prefab";
                        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        if (!saved) throw new Exception("Could not save " + prefabPath);
                        if (saved.GetComponentsInChildren<MeshRenderer>(true).Length != 1) throw new Exception("Unexpected renderer count");
                        if (saved.GetComponentsInChildren<MonoBehaviour>(true).Any()) throw new Exception("Unexpected gameplay script");
                        if (saved.transform.Find("GripSocket").localPosition != Vector3.zero) throw new Exception("Grip origin changed");
                        results.Add(new { name, prefabPath, triangles = filter.sharedMesh.GetIndexCount(0) / 3,
                            vertices = filter.sharedMesh.vertexCount, muzzle = new[] { muzzlePosition.x, muzzlePosition.y, muzzlePosition.z },
                            material = AssetDatabase.GetAssetPath(material), meshGuid = AssetDatabase.AssetPathToGUID(path) });
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(".utmp/weapon-preparation");
                File.WriteAllText(".utmp/weapon-preparation/unity-validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));
                Debug.Log("ART_WEAPON_PREFABS_READY");
                if (Application.isBatchMode && !ArtResourceImport.IsImporting) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static Texture2D PrepareTexture(string suffix, bool normal, bool srgb)
        {
            string path = Textures + "SonicWeapon_" + suffix + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new Exception("Missing texture: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
