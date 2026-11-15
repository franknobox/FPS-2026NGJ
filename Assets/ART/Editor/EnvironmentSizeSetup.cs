using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArtTools
{
    public static class EnvironmentSizeSetup
    {
        public const string SettingsPath = "Assets/ART/Settings/EnvironmentSizes.asset";
        const string Folder = "Assets/ART/Prefabs/Environment/";
        const string Geometry = "GeometryScale";
        sealed class Capture
        {
            public GameObject instance;
            public string path;
            public Vector3 position, worldScale, targetSize;
            public Quaternion rotation;
            public Dictionary<string, Matrix4x4> meshMatrices;
        }

        [MenuItem("美术工具/场景素材/将展示尺寸设为默认")]
        public static void ApplyFromShowcase()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ArtShowcaseSetup.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new Exception("请在停止运行的资源展示场景中同步尺寸。");
            var captures = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                .Select(t => new { t, path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) })
                .Where(x => x.path.StartsWith(Folder)).Select(x =>
                {
                    var matrix = x.t.localToWorldMatrix;
                    var a = ((Vector3)matrix.GetColumn(0)).normalized;
                    var b = ((Vector3)matrix.GetColumn(1)).normalized;
                    var c = ((Vector3)matrix.GetColumn(2)).normalized;
                    if (Mathf.Abs(Vector3.Dot(a,b)) > .0001f || Mathf.Abs(Vector3.Dot(a,c)) > .0001f || Mathf.Abs(Vector3.Dot(b,c)) > .0001f)
                        throw new Exception("父级缩放产生剪切，请先整理层级：" + x.t.name);
                    var scale = x.t.lossyScale;
                    if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) throw new Exception("场景素材缩放必须为正：" + x.t.name);
                    return new Capture { instance = x.t.gameObject, path = x.path, position = x.t.position,
                        rotation = x.t.rotation, worldScale = scale, targetSize = Vector3.Scale(ContentBounds(x.t.gameObject).size, scale),
                        meshMatrices = x.t.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.sharedMesh.name, f => f.transform.localToWorldMatrix) };
                }).ToArray();
            if (captures.Length != 14 || captures.Select(c => c.path).Distinct().Count() != 14)
                throw new Exception("需要资源展示中的14个独立场景素材，未修改资源。");
            var settings = AssetDatabase.LoadAssetAtPath<EnvironmentSizeProfile>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<EnvironmentSizeProfile>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            // Capture every instance before changing any source prefab, including unsaved edits.
            foreach (var capture in captures)
            {
                string guid = AssetDatabase.AssetPathToGUID(capture.path);
                var entry = settings.entries.FirstOrDefault(e => e.prefabGuid == guid);
                if (entry == null) { entry = new EnvironmentSizeProfile.Entry { prefabGuid = guid }; settings.entries.Add(entry); }
                entry.label = capture.instance.name;
                entry.size = capture.targetSize;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            foreach (var capture in captures)
            {
                var prefab = PrefabUtility.LoadPrefabContents(capture.path);
                try { ApplyToPrefab(prefab, capture.path); PrefabUtility.SaveAsPrefabAsset(prefab, capture.path); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                AssetDatabase.ImportAsset(capture.path, ImportAssetOptions.ForceSynchronousImport);
                var transform = capture.instance.transform;
                transform.SetPositionAndRotation(capture.position, capture.rotation);
                Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
                transform.localScale = new Vector3(1 / parentScale.x, 1 / parentScale.y, 1 / parentScale.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                foreach (var filter in capture.instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    Matrix4x4 before = capture.meshMatrices[filter.sharedMesh.name];
                    Matrix4x4 after = filter.transform.localToWorldMatrix;
                    for (int i = 0; i < 16; i++)
                        if (Mathf.Abs(before[i] - after[i]) > .001f) throw new Exception("展示姿态发生变化：" + capture.instance.name);
                }
                if (Vector3.Distance(ContentBounds(capture.instance).size, capture.targetSize) > .002f)
                    throw new Exception("实例未继承新的默认尺寸：" + capture.instance.name);
            }
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ENVIRONMENT_SIZES_APPLIED prefabs=14 worldMatricesPreserved=true PlayMode=" + EditorApplication.isPlaying);
        }

        // Also used by FBX reimport so regenerated prefabs retain the agreed default size.
        public static void ApplyToPrefab(GameObject root, string assetPath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<EnvironmentSizeProfile>(SettingsPath);
            var entry = settings ? settings.entries.FirstOrDefault(e => e.prefabGuid == AssetDatabase.AssetPathToGUID(assetPath)) : null;
            if (entry == null) return;
            var geometry = root.transform.Find(Geometry);
            if (!geometry)
            {
                var children = root.transform.Cast<Transform>().ToArray();
                geometry = new GameObject(Geometry).transform;
                geometry.SetParent(root.transform, false);
                foreach (var child in children) child.SetParent(geometry, false);
            }
            geometry.localPosition = Vector3.zero;
            geometry.localRotation = Quaternion.identity;
            geometry.localScale = Vector3.one;
            root.transform.localScale = Vector3.one;
            var raw = ContentBounds(root).size;
            if (raw.x <= 0 || raw.y <= 0 || raw.z <= 0) throw new Exception("Empty environment geometry: " + assetPath);
            geometry.localScale = new Vector3(entry.size.x / raw.x, entry.size.y / raw.y, entry.size.z / raw.z);
            var lod = root.GetComponent<LODGroup>();
            if (lod) lod.RecalculateBounds();
        }

        public static Bounds ContentBounds(GameObject root)
        {
            Bounds bounds = default;
            bool started = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var local = filter.sharedMesh.bounds;
                for (int x=-1; x<=1; x+=2) for (int y=-1; y<=1; y+=2) for (int z=-1; z<=1; z+=2)
                {
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,new Vector3(x,y,z)));
                    if (!started) { bounds = new Bounds(point,Vector3.zero); started = true; } else bounds.Encapsulate(point);
                }
            }
            if (!started) throw new Exception("Missing environment meshes: " + root.name);
            return bounds;
        }

        public static void VerifyDefaults()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EnvironmentSizeProfile>(SettingsPath);
            if (!settings || settings.entries.Count != 14) throw new Exception("Missing environment size settings");
            foreach (var entry in settings.entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.prefabGuid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (Vector3.Distance(root.transform.localScale,Vector3.one) > .0001f || Vector3.Distance(ContentBounds(root).size,entry.size) > .002f)
                        throw new Exception("Default size mismatch: " + entry.label);
                    if (root.GetComponent<LODGroup>().lodCount != 3 || root.GetComponentsInChildren<MeshCollider>(true).Length != 1)
                        throw new Exception("LOD/collision mismatch: " + entry.label);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("ENVIRONMENT_DEFAULT_SIZES_VERIFIED prefabs=14");
        }
    }
}
