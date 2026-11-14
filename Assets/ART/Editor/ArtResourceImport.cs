using System;
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
    public static class ArtResourceImport
    {
        public static bool IsImporting { get; private set; }
        public static bool ExitAfterImport { get; set; } = true;

        [MenuItem("美术工具/资源/整理怪物与武器")]
        public static void ImportAll()
        {
            try
            {
                IsImporting = true;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                SonicWeaponAssetSetup.Build();
                EnsureFolder("Assets/ART/Materials/Enemies");
                EnsureFolder("Assets/ART/Prefabs/Enemies");
                var records = new System.Collections.Generic.List<object>();
                foreach (var item in new[] {
                    new { Source = "CarapaceEnemy", Name = "甲壳怪", Height = 2.2f },
                    new { Source = "WingedEnemy", Name = "翼怪", Height = 1.8f },
                    new { Source = "YellowEyeBug", Name = "黄色眼虫", Height = .75f },
                    new { Source = "PurpleEyeBug", Name = "紫色眼虫", Height = .75f } })
                {
                    string modelPath = "Assets/ART/Models/Enemies/" + item.Source + "/" + item.Source + ".fbx";
                    string texturePrefix = "Assets/ART/Textures/Enemies/" + item.Source + "/" + item.Source;
                    var color = Texture(texturePrefix + "_BaseColor.png", false, true);
                    var normal = Texture(texturePrefix + "_Normal.png", true, false);
                    var metallic = Texture(texturePrefix + "_MetallicSmoothness.png", false, false);
                    string materialPath = "Assets/ART/Materials/Enemies/" + item.Name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (!material)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.SetTexture("_BaseMap", color);
                    material.SetTexture("_BumpMap", normal);
                    material.SetTexture("_MetallicGlossMap", metallic);
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Metallic", 1);
                    material.SetFloat("_Smoothness", 1);
                    material.SetFloat("_SmoothnessTextureChannel", 0);
                    if (material.shader.name != "ART/Comic/Toon Lit") material.SetFloat("_BumpScale", 1);
                    material.SetFloat("_Cull", 0);
                    material.EnableKeyword("_NORMALMAP");
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    EditorUtility.SetDirty(material);

                    var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    if (!importer) throw new Exception("Missing model: " + modelPath);
                    importer.globalScale = 1;
                    importer.useFileScale = true;
                    importer.bakeAxisConversion = true;
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = true;
                    importer.optimizeGameObjects = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), item.Source + "_Body"), material);
                    importer.SaveAndReimport();

                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    var root = new GameObject(item.Name);
                    try
                    {
                        var facing = new GameObject("VisualRoot").transform;
                        facing.SetParent(root.transform, false);
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, facing);
                        model.name = "Model";
                        var transforms = model.GetComponentsInChildren<Transform>(true);
                        var head = transforms.Single(t => t.name == "Head");
                        var socket = transforms.Single(t => t.name == "SoundSocket");
                        Vector3 direction = Vector3.ProjectOnPlane(socket.position - head.position, Vector3.up);
                        if (direction.sqrMagnitude < .00001f) throw new Exception("Cannot identify enemy facing: " + item.Name);
                        // FromToRotation is ambiguous at 180 degrees and can flip up/down.
                        facing.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(direction.normalized, Vector3.forward, Vector3.up), Vector3.up);
                        var renderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                        renderer.sharedMaterial = material;
                        renderer.quality = SkinQuality.Bone4;
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                        var animator = model.GetComponentInChildren<Animator>(true);
                        if (!animator || !animator.avatar || !animator.avatar.isValid) throw new Exception("Missing valid Generic avatar: " + item.Name);
                        animator.applyRootMotion = false;
                        // Imported skin bounds can include a conservative animation margin.
                        // Measure baked rest-pose vertices instead of resizing to that AABB.
                        var baked = new Mesh();
                        Bounds geometryBounds;
                        try
                        {
                            renderer.BakeMesh(baked);
                            var vertices = baked.vertices;
                            if (vertices.Length == 0) throw new Exception("Empty skinned mesh");
                            geometryBounds = new Bounds(renderer.transform.TransformPoint(vertices[0]), Vector3.zero);
                            foreach (var vertex in vertices) geometryBounds.Encapsulate(renderer.transform.TransformPoint(vertex));
                        }
                        finally { UnityEngine.Object.DestroyImmediate(baked); }
                        if (Mathf.Abs(geometryBounds.size.y - item.Height) > .02f) throw new Exception("Unexpected baked enemy height: " + geometryBounds.size);
                        if (geometryBounds.min.y < -.02f || head.position.y <= root.transform.position.y) throw new Exception("Enemy is not upright: " + item.Name);
                        if (!renderer.sharedMesh || renderer.bones.Any(b => !b)) throw new Exception("Missing mesh or bone references");
                        int layer = LayerMask.NameToLayer("Enemy");
                        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer < 0 ? 0 : layer;
                        string prefabPath = "Assets/ART/Prefabs/Enemies/" + item.Name + ".prefab";
                        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        if (!saved) throw new Exception("Prefab save failed: " + prefabPath);
                        if (saved.GetComponentsInChildren<MonoBehaviour>(true).Any()) throw new Exception("Unexpected gameplay script");
                        records.Add(new { name = item.Name, prefab = prefabPath, triangles = renderer.sharedMesh.GetIndexCount(0) / 3,
                            bones = renderer.bones.Length, height = geometryBounds.size.y, avatar = animator.avatar.name,
                            facing = new[] { (socket.position-head.position).normalized.x, (socket.position-head.position).normalized.y, (socket.position-head.position).normalized.z }, material = materialPath });
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                CreatePreview();
                AddEyeBugsToPreview();
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(".utmp/art-import");
                File.WriteAllText(".utmp/art-import/validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(records, Newtonsoft.Json.Formatting.Indented));
                Debug.Log("ART_RESOURCES_READY");
                if (Application.isBatchMode && ExitAfterImport) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
            finally { IsImporting = false; }
        }

        static void CreatePreview()
        {
            const string path = "Assets/ART/Scenes/资源展示.unity";
            // Preserve a preview scene once an artist has edited it.
            if (File.Exists(path)) return;
            if (!Application.isBatchMode)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var open = SceneManager.GetSceneAt(i);
                    if (string.IsNullOrEmpty(open.path) && open.isDirty) return;
                }
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                string[] paths = { "Enemies/甲壳怪", "Enemies/翼怪", "Weapons/霰弹枪", "Weapons/爪型枪" };
                for (int i = 0; i < paths.Length; i++)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ART/Prefabs/" + paths[i] + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                    instance.transform.position = new Vector3(-3 + i * 2, i >= 2 ? 1 : 0, 0);
                    if (i >= 2) instance.transform.rotation = Quaternion.Euler(0, -40, 0);
                }
                var lightObject = new GameObject("展示灯光");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                var cameraObject = new GameObject("展示相机");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(3, 2.8f, 9);
                camera.transform.LookAt(new Vector3(0, 1, 0));
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 50;
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                EditorSceneManager.SaveScene(scene, path);
            }
            finally { if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true); }
        }

        static Texture2D Texture(string path, bool normal, bool srgb)
        {
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

        static void AddEyeBugsToPreview()
        {
            const string path = "Assets/ART/Scenes/资源展示.unity";
            if (!File.Exists(path)) return;
            if (!Application.isBatchMode)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var open = SceneManager.GetSceneAt(i);
                    if (string.IsNullOrEmpty(open.path) && open.isDirty) return;
                }
            }
            var scene = EditorSceneManager.OpenScene(path,
                Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            try
            {
                string[] names = { "黄色眼虫", "紫色眼虫" };
                for (int i = 0; i < names.Length; i++)
                {
                    string prefabPath = "Assets/ART/Prefabs/Enemies/" + names[i] + ".prefab";
                    bool exists = scene.GetRootGameObjects().Any(root =>
                        AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)) == prefabPath);
                    if (exists) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.transform.position = new Vector3(i == 0 ? -1.4f : 1.4f, 0, 2.25f);
                }
                EditorSceneManager.SaveScene(scene, path);
            }
            finally { if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true); }
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
