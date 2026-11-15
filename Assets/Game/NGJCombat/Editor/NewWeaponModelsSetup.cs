using System;
using System.Linq;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace NGJ.EditorTools
{
    public static class NewWeaponModelsSetup
    {
        const string Art="Assets/ART/Prefabs/Weapons/";
        const string Views="Assets/Game/Prefabs/Weapons/";
        [MenuItem("NGJ/美术/接入原创霰弹与狙击模型")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("Only edit mode");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("请先保存当前场景");
            var original=EditorSceneManager.GetActiveScene().path;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // 先复制原先的自制霰弹外观，之后两者互不覆盖。
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Art+"步枪.prefab"))
            {
                var old=PrefabUtility.LoadPrefabContents(Art+"霰弹枪.prefab");
                try{old.name="步枪";PrefabUtility.SaveAsPrefabAsset(old,Art+"步枪.prefab");}
                finally{PrefabUtility.UnloadPrefabContents(old);}
            }
            BuildArt("OrganicShotgun","霰弹枪");BuildArt("OrganicSniper","狙击枪");
            BuildView("Rifle","步枪","SiMing","Rifle",.12f,.018f);
            BuildView("Shotgun","霰弹枪","HanYin","Shotgun",.45f,.055f);
            BuildView("Sniper","狙击枪","JianXiao","Rifle",.4f,.065f);
            foreach(var path in new[]{SlugWhitebox.Editor.SlugWhiteboxBuilder.ScenePath,Wasteland.Editor.WastelandLevelBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);var g=Object.FindFirstObjectByType<SlugGame>();
                foreach(var pair in new[]{(0,"Rifle"),(1,"Sniper"),(2,"Shotgun")})
                {
                    if(g.weaponModels[pair.Item1])Object.DestroyImmediate(g.weaponModels[pair.Item1]);
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Views+pair.Item2+".prefab"),g.weaponPivot);
                    go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.SetActive(pair.Item1==2);
                    g.weaponModels[pair.Item1]=go;g.fireSounds[pair.Item1]=go.GetComponent<SlugWeaponView>().feedbackSource.audioSFX.shooting[0];
                }
                g.ConfigureLoadout(2,4,0,1);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();ArtTools.ArtShowcaseSetup.UpdateShowcase();
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        }
        static void BuildArt(string id,string label)
        {
            string folder="Assets/ART/Textures/Weapons/"+id+"/";
            var maps=new Texture2D[3];string[] roles={"BaseColor","Normal","MetallicSmoothness"};
            for(int i=0;i<3;i++)
            {
                string path=folder+id+"_"+roles[i]+".png";
                var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=i==1?TextureImporterType.NormalMap:TextureImporterType.Default;
                ti.sRGBTexture=i==0;ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.SaveAndReimport();maps[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            string matPath="Assets/ART/Materials/Weapons/"+id+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.SetTexture("_BaseMap",maps[0]);mat.SetTexture("_BumpMap",maps[1]);mat.SetTexture("_MetallicGlossMap",maps[2]);
            mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);
            mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(mat);
            ArtTools.ComicStyleSetup.ApplyMaterials(new[]{matPath});
            string modelPath="Assets/ART/Models/Weapons/"+id+"/"+id+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;importer.globalScale=1;importer.bakeAxisConversion=true;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),id+"_Body"),mat);importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var filter=model.GetComponentsInChildren<MeshFilter>(true).Single();
            var muzzle=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Muzzle");
            if(muzzle.position.z<=0)throw new Exception("Incorrect weapon forward "+id);
            var root=new GameObject(label);
            try
            {
                var body=new GameObject("Body");body.transform.SetParent(root.transform,false);
                var matrix=filter.transform.localToWorldMatrix;body.transform.localPosition=matrix.GetColumn(3);body.transform.localRotation=matrix.rotation;body.transform.localScale=matrix.lossyScale;
                body.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var renderer=body.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                new GameObject("GripSocket").transform.SetParent(root.transform,false);
                var socket=new GameObject("Muzzle").transform;socket.SetParent(root.transform,false);socket.localPosition=muzzle.position;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("Weapons");
                PrefabUtility.SaveAsPrefabAsset(root,Art+label+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void BuildView(string name,string art,string definition,string feedback,float duration,float recoil)
        {
            var root=new GameObject(name);
            try
            {
                var motion=new GameObject("原厂开火运动").transform;motion.SetParent(root.transform,false);
                var animated=new GameObject("枪体运动").transform;animated.SetParent(motion,false);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+art+".prefab"),animated);
                visual.name="Visual";visual.transform.localPosition=new Vector3(.20f,-.24f,.36f);
                var view=root.AddComponent<SlugWeaponView>();view.rig=motion.gameObject.AddComponent<Animator>();view.rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                view.feedbackSource=AssetDatabase.LoadAssetAtPath<cowsins.Weapon_SO>("Assets/FPS Engine/ScriptableObjects/Weapons/"+feedback+".asset");
                view.feedbackWeapon=AssetDatabase.LoadAssetAtPath<NGJ.Combat.WeaponDefinition>("Assets/Game/NGJCombat/Generated/Weapons/"+definition+".asset");
                view.muzzle=visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Muzzle");
                view.idleClip=Clip(name+"BodyIdle",1,0);view.fireClip=Clip(name+"BodyFire",duration,recoil);
                PrefabUtility.SaveAsPrefabAsset(root,Views+name+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static AnimationClip Clip(string name,float duration,float recoil)
        {
            string path="Assets/ART/Animations/FPSWeapons/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();
            foreach(var axis in new[]{"x","y","z"})clip.SetCurve("枪体运动",typeof(Transform),"localPosition."+axis,
                axis=="z"?new AnimationCurve(new Keyframe(0,0),new Keyframe(duration*.14f,-recoil),new Keyframe(duration,0)):AnimationCurve.Constant(0,duration,0));
            EditorUtility.SetDirty(clip);return clip;
        }
    }
}
