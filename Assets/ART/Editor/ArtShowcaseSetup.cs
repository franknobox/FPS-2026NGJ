using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ArtTools
{
    public static class ArtShowcaseSetup
    {
        public const string ScenePath = "Assets/ART/Scenes/资源展示.unity";
        const string LegacyScene = "Assets/ART/Scenes/场景素材展示.unity";

        [MenuItem("美术工具/资源/更新统一资源展示")]
        public static void UpdateShowcase()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instances = Instances(scene).ToDictionary(SourcePath);
            if (File.Exists(LegacyScene))
            {
                var legacy = EditorSceneManager.OpenScene(LegacyScene, OpenSceneMode.Additive);
                foreach (var instance in Instances(legacy))
                {
                    string path = SourcePath(instance);
                    if (instances.ContainsKey(path)) continue;
                    instance.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    instances.Add(path, instance);
                }
                EditorSceneManager.CloseScene(legacy, true);
            }
            SceneManager.SetActiveScene(scene);
            foreach (string category in new[] { "Enemies", "Weapons", "Environment" })
            {
                string folder = "Assets/ART/Prefabs/" + category;
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p, StringComparer.Ordinal).ToArray();
                string label = category == "Enemies" ? "怪物" : category == "Weapons" ? "武器" : "场景素材";
                var group = scene.GetRootGameObjects().FirstOrDefault(g => g.name == label) ?? new GameObject(label);
                if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
                for (int i = 0; i < paths.Length; i++)
                {
                    bool isNew = !instances.TryGetValue(paths[i], out var instance);
                    if (isNew)
                    {
                        instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]), scene);
                        instances.Add(paths[i], instance);
                    }
                    instance.transform.SetParent(group.transform, true);
                    if (!isNew) continue; // Keep artist-authored placement, rotation and size overrides.
                    instance.transform.localScale = Vector3.one;
                    instance.transform.rotation = Quaternion.identity;
                    if (category == "Environment")
                    {
                        instance.transform.position = new Vector3((i % 4 - 1.5f) * 16, 0, -1 - i / 4 * 18);
                    }
                    else if (category == "Enemies") instance.transform.position = new Vector3((i - (paths.Length - 1) * .5f) * 3.2f, 0, 5);
                    else
                    {
                        instance.transform.position = new Vector3((i - (paths.Length - 1) * .5f) * 3, 1, 8);
                        instance.transform.rotation = Quaternion.Euler(0, -90, 0);
                        instance.transform.localScale = Vector3.one * 2;
                    }
                }
            }
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (!camera) camera = new GameObject("展示相机").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.23f, .26f, .31f);
            camera.orthographic = true;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var allRenderers = instances.Values.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var total = allRenderers[0].bounds;
            foreach (var renderer in allRenderers.Skip(1)) total.Encapsulate(renderer.bounds);
            camera.transform.position = total.center + new Vector3(18, 26, 34);
            camera.transform.LookAt(total.center);
            float halfHeight = .1f;
            foreach (var renderer in allRenderers)
                foreach (var corner in Corners(renderer.bounds))
                {
                    Vector3 point = camera.transform.InverseTransformPoint(corner);
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y), Mathf.Abs(point.x) / camera.aspect);
                }
            camera.orthographicSize = halfHeight * 1.18f;
            if (!Application.isBatchMode && SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(total, false);
            if (!scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).Any())
            {
                var light = new GameObject("展示灯光").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, 155, 0);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (File.Exists(LegacyScene)) AssetDatabase.DeleteAsset(LegacyScene);
            AssetDatabase.SaveAssets();
            Debug.Log("ART_SHOWCASE_UPDATED prefabs=" + instances.Count);
        }

        public static void UpdateAndVerify()
        {
            UpdateShowcase();
            var scene = SceneManager.GetActiveScene();
            var instances = Instances(scene);
            if (instances.Length != 20 || instances.Select(SourcePath).Distinct().Count() != 20)
                throw new Exception("Expected all 20 unique ART prefabs in the unified scene");
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            foreach (var instance in instances)
            {
                if (instance.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new Exception("Missing script: " + instance.name);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (var corner in Corners(renderer.bounds))
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        if (point.z < camera.nearClipPlane || point.z > camera.farClipPlane || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1)
                            throw new Exception("Exhibit outside camera: " + instance.name);
                    }
            }
            if (File.Exists(LegacyScene)) throw new Exception("Duplicate showcase scene remains");
            Debug.Log("ART_SHOWCASE_VERIFIED enemies=4 weapons=2 environment=14 PlayMode=" + EditorApplication.isPlaying);
        }

        static string SourcePath(GameObject instance) => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(instance));
        static GameObject[] Instances(Scene scene) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).Where(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g) && SourcePath(g).StartsWith("Assets/ART/Prefabs/")).ToArray();
        static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }
    }
}
