using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlugWhitebox.Editor
{
    public static class SlugWeaponAssets
    {
        public static readonly string[] Sources = { "RifleRig", "Revolver", "Shotgun", "KatanaRig" };
        static readonly string[] Names = { "步枪", "左轮", "霰弹枪", "武士刀" };

        [MenuItem("循声虫族/替换为素材包武器")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(SlugWhiteboxBuilder.ScenePath);
            var game = UnityEngine.Object.FindFirstObjectByType<SlugGame>();
            if (!game) throw new Exception("Whitebox game missing");
            var pivot = game.weaponPivot;
            while (pivot.childCount > 0) UnityEngine.Object.DestroyImmediate(pivot.GetChild(0).gameObject);
            pivot.name = "素材包武器 · 1步枪 2左轮 3霰弹 4刀";
            pivot.localPosition = Vector3.zero; pivot.localRotation = Quaternion.identity;
            game.weaponModels = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                game.weaponModels[i] = Create(i, pivot);
                game.weaponModels[i].SetActive(i == 0);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Preview(game);
            Debug.Log("SLUG_WEAPONS_INSTALLED 4 real FPS Engine weapon rigs");
        }

        public static void InstallAndBuild() { Install(); SlugWhiteboxBuilder.Build(); }

        public static GameObject Create(int index, Transform parent)
        {
            string path = "Assets/FPS Engine/Models/Weapons/" + Sources[index] + ".fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source) throw new Exception("Missing recovered weapon: " + path);
            var holder = new GameObject(Names[index] + " · FPS Engine");
            holder.transform.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(source, holder.transform, false);
            model.name = Sources[index];
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToArray();
            var idle = clips.First(c => c.name.ToLowerInvariant().Contains("idle"));
            var fire = clips.First(c => c.name.Contains("Shoot") || c.name.Contains("Fire") || c.name == "Slash_Anim");
            idle.SampleAnimation(model, 0);
            // Source rigs are authored around a 1 m chest height, facing +Z.
            model.transform.localPosition = new Vector3(.10f, -1.18f, .12f);
            model.transform.localRotation = Quaternion.identity;
            var view = holder.AddComponent<SlugWeaponView>();
            view.rig = model.GetComponent<Animator>();
            if (!view.rig) view.rig = model.AddComponent<Animator>();
            view.rig.runtimeAnimatorController = null;
            view.rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            view.idleClip = idle; view.fireClip = fire;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            holder.layer = 2;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                bool arms = r.name == "Arms_Mesh" || r.name == "Mesh";
                int count = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh.subMeshCount : r.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                var materials = new Material[count];
                for (int j = 0; j < count; j++) materials[j] = MaterialFor(arms ? "手臂" : "枪体" + j % 3, arms ? .53f : .23f + (j % 3) * .12f);
                r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            var muzzle = new GameObject("枪口").transform;
            muzzle.SetParent(holder.transform, false);
            muzzle.localPosition = index == 1 ? new Vector3(.17f, -.14f, .66f) : new Vector3(.17f, -.14f, .88f);
            view.muzzle = muzzle;
            return holder;
        }

        static Material MaterialFor(string name, float gray)
        {
            string path = SlugWhiteboxBuilder.Root + "/Materials/素材武器_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = new Color(gray, gray, gray);
            material.SetFloat("_Smoothness", .25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Preview(SlugGame game)
        {
            Directory.CreateDirectory("Builds/Verification");
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                game.view.targetTexture = target;
                for (int i = 0; i < 4; i++)
                {
                    foreach (var model in game.weaponModels) model.SetActive(false);
                    game.weaponModels[i].SetActive(true);
                    var view = game.weaponModels[i].GetComponent<SlugWeaponView>();
                    view.idleClip.SampleAnimation(view.rig.gameObject, 0);
                    game.view.Render(); RenderTexture.active = target;
                    var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                    File.WriteAllBytes("Builds/Verification/weapon-" + (i + 1) + ".png", image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            finally
            {
                game.view.targetTexture = null; RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                for (int i = 0; i < 4; i++) game.weaponModels[i].SetActive(i == 0);
            }
        }
        public static void Inspect()
        {
            var report = new System.Text.StringBuilder();
            foreach (string name in Sources)
            {
                string path="Assets/FPS Engine/Models/Weapons/"+name+".fbx";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!source)throw new Exception("Missing recovered model: "+path);
                var go=UnityEngine.Object.Instantiate(source);
                var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
                var idle=clips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("idle"));
                if(idle)idle.SampleAnimation(go,0);
                report.AppendLine(name+" clips="+string.Join(",",clips.Select(c=>c.name)));
                foreach(var r in go.GetComponentsInChildren<Renderer>(true))
                    report.AppendLine("  "+r.name+" bounds="+r.bounds+" local="+r.transform.localPosition+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null")));
                foreach(var t in go.GetComponentsInChildren<Transform>(true))report.AppendLine("    "+t.name+" "+t.localPosition+" "+t.localEulerAngles);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/weapon-inspect.txt",report.ToString());
            Debug.Log("SLUG_WEAPON_INSPECT_OK");
        }
    }
}
