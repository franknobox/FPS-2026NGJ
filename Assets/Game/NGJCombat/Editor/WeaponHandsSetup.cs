using System;
using System.Linq;
using SlugWhitebox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace NGJ.EditorTools
{
    public static class WeaponHandsSetup
    {
        const string Anim="Assets/ART/Animations/FPSWeapons/";
        public static void Attach(GameObject root,string id)
        {
            var view=root.GetComponent<SlugWeaponView>();
            var old=root.transform.Find("原厂手臂");if(old)Object.DestroyImmediate(old.gameObject);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS Engine/Models/Weapons/"+id+".fbx"),root.transform);
            model.name="原厂手臂";model.transform.localPosition=new Vector3(.1f,-1.18f,.12f);model.transform.localRotation=Quaternion.identity;
            var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/FPS Engine/Models/Weapons/"+id+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            var idle=CopyClip(clips.First(c=>c.name.Contains("Idle")),id+"ArmsIdle",true);
            var fire=CopyClip(clips.First(c=>c.name.Contains("Fire")),id+"ArmsFire",false);
            idle.SampleAnimation(model,0);
            var authoredArms=view.feedbackSource.weaponObject.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Mesh");
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled=renderer.name=="Mesh";
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                if(renderer.enabled)renderer.sharedMaterials=(authoredArms?authoredArms.sharedMaterials:renderer.sharedMaterials).Select(ArmMaterial).ToArray();
            }
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("Weapons");
            foreach(var col in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(col);
            var visual=view.muzzle.GetComponentsInParent<Transform>().First(t=>t.name=="Visual");
            var oldMotion=root.transform.Find("原厂开火运动");
            if(oldMotion)
            {
                var unusedAnimator=oldMotion.GetComponent<Animator>();if(unusedAnimator)Object.DestroyImmediate(unusedAnimator);
                oldMotion.localPosition=Vector3.zero;oldMotion.localRotation=Quaternion.identity;
                var motion=oldMotion.Find("枪体运动");if(motion){motion.localPosition=Vector3.zero;motion.localRotation=Quaternion.identity;}
            }
            var transforms=model.GetComponentsInChildren<Transform>();
            var bone=transforms.First(t=>t.name==(id=="Shotgun"?"body":"Weapon 1"));
            var palm=transforms.First(t=>t.name=="Middle0.R");
            var finger=transforms.First(t=>t.name=="Middle2.R");
            var desired=(palm.position+finger.position)*.5f;
            var binding=root.GetComponent<WeaponHandBinding>();if(!binding)binding=root.AddComponent<WeaponHandBinding>();
            binding.gun=visual;binding.gripBone=bone;
            binding.positionOffset=Quaternion.Inverse(bone.rotation)*(desired-bone.position);
            binding.rotationOffset=Quaternion.Inverse(bone.rotation)*root.transform.rotation;
            binding.leftShoulder=transforms.First(t=>t.name=="Shoulder.L");binding.leftElbow=transforms.First(t=>t.name=="Elbow.L");binding.leftHand=transforms.First(t=>t.name=="Hand.L");
            binding.leftSupport=id=="Shotgun"?new Vector3(-.025f,.07f,.20f):new Vector3(-.055f,.01f,.12f);
            binding.ApplyPose();
            view.rig=model.GetComponent<Animator>();if(!view.rig)view.rig=model.AddComponent<Animator>();
            view.rig.runtimeAnimatorController=null;view.rig.applyRootMotion=false;view.rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            view.idleClip=idle;view.fireClip=fire;
        }
        static AnimationClip CopyClip(AnimationClip source,string name,bool loop)
        {
            var path=Anim+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=Object.Instantiate(source);AssetDatabase.CreateAsset(clip,path);}else EditorUtility.CopySerialized(source,clip);
            clip.name=name;clip.events=Array.Empty<AnimationEvent>();
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
        }
        static Material ArmMaterial(Material source)
        {
            string path=Anim+"原厂手臂_"+(source?source.name:"默认手套")+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material)return material;
            material=new Material(Shader.Find("ART/Comic/Toon Lit"));
            if(source&&source.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));else if(source&&source.HasProperty("_MainTex"))material.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
            material.SetColor("_BaseColor",source?(source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.color:Color.white):new Color(.24f,.28f,.3f));
            material.SetFloat("_HatchStrength",.02f);AssetDatabase.CreateAsset(material,path);return material;
        }
    }
}
