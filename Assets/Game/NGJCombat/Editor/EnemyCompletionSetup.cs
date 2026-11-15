using System;
using System.IO;
using System.Linq;
using NGJ.Combat;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace NGJ.EditorTools
{
    public static class EnemyCompletionSetup
    {
        const string Root="Assets/Game/NGJCombat/Generated";
        const string Anim="Assets/ART/Animations/Enemies";
        const string Fx="Assets/ART/VFX/EnemySound";
        const string Audio="Assets/ART/Audio/EnemySound";
        [MenuItem("NGJ/美术/补齐敌人动作声波与角色脚步")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Folder(Anim);Folder(Fx);Folder(Audio);
            Tone("蓄声预警",460,.32f);Tone("轻型声波",920,.14f);Tone("中型声波",560,.18f);Tone("重型共振",180,.26f);
            AssetDatabase.Refresh();
            var line=Material("声波线",Color.white);
            foreach(var pair in new[]{(id:"EnemyChanYin",kind:0),(id:"EnemyMiYin",kind:1),(id:"EnemyMiYinPurple",kind:1),(id:"EnemyZhenYin",kind:2)})
            {
                var path=Root+"/Prefabs/"+pair.id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var binding=root.GetComponent<EnemyVisualBinding>();binding.deathDespawnDelay=1.25f;
                    var visual=binding.preparedVisual;var animator=visual.GetComponentInChildren<Animator>(true);
                    if(!animator)animator=visual.gameObject.AddComponent<Animator>();
                    animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var bones=visual.GetComponentInChildren<SkinnedMeshRenderer>(true).bones;
                    var states=new[]{"Idle","Move","Windup","Attack","Stun","Death"};
                    var clips=states.Select(state=>Clip(pair.id,state,pair.kind,animator,bones)).ToArray();
                    animator.runtimeAnimatorController=Controller(pair.id,states,clips);
                    var presentation=root.GetComponent<EnemyPresentation>();if(!presentation)presentation=root.AddComponent<EnemyPresentation>();
                    presentation.soundMaterial=line;
                    presentation.color=pair.kind==0?new Color(.15f,.9f,1):pair.kind==2?new Color(1,.2f,.6f):new Color(1,.7f,.1f);
                    presentation.windupSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+"/蓄声预警.wav");
                    presentation.shotSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+"/"+new[]{"轻型声波","中型声波","重型共振"}[pair.kind]+".wav");
                    var definition=root.GetComponent<EnemyCombatant>().definition;
                    if(pair.kind<2)definition.projectilePrefab=Bolt(pair.kind,presentation.color);
                    EditorUtility.SetDirty(definition);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
            foreach(var path in new[]{SlugWhitebox.Editor.SlugWhiteboxBuilder.ScenePath,Wasteland.Editor.WastelandLevelBuilder.ScenePath})
            {
                EditorSceneManager.OpenScene(path);var game=Object.FindFirstObjectByType<SlugGame>();
                var steps=game.player.GetComponent<PlayerFootsteps>();if(!steps)steps=game.player.gameObject.AddComponent<PlayerFootsteps>();
                steps.game=game;
                steps.clips=Enumerable.Range(1,4).Select(i=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ART/Audio/Footsteps_DirtyGround/Footsteps_DirtyGround_Walk/Footsteps_DirtyGround_Walk_"+i.ToString("00")+".wav")).ToArray();
                if(steps.clips.Any(c=>!c))throw new Exception("Missing imported footsteps");
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            }
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
            Debug.Log("ENEMY_COMPLETION_READY four animated actors, visible sound bolts/beam, four random footsteps");
        }
        static AnimationClip Clip(string id,string state,int kind,Animator animator,Transform[] bones)
        {
            Folder(Anim+"/"+id);string path=Anim+"/"+id+"/"+state+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();clip.frameRate=30;
            float duration=state=="Windup"?.4f:state=="Death"?1f:state=="Move"?kind==2?1.1f:.7f:1f;
            bool moving=state=="Move",death=state=="Death",attack=state=="Attack",stun=state=="Stun",windup=state=="Windup";
            foreach(var bone in bones)
            {
                string name=bone.name;string binding=AnimationUtility.CalculateTransformPath(bone,animator.transform);
                var rest=bone.localRotation;var curves=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();
                int frames=Mathf.CeilToInt(duration*30);
                for(int i=0;i<=frames;i++)
                {
                    float t=(float)i/frames,phase=t*Mathf.PI*2,side=name.EndsWith(".L")?1:-1;
                    Vector3 e=Vector3.zero;
                    if(kind==1)
                    {
                        if(name.Contains("Upper")){float tripod=name.Contains("Middle")?-1:1;e.z=side*(death?55*t:stun?18:Mathf.Sin(phase+side*tripod*Mathf.PI*.5f)*(moving?22:2));}
                        if(name.Contains("Lower"))e.x=death?40*t:moving?Mathf.Max(0,Mathf.Cos(phase+side))*25:0;
                        if(name=="Body")e=new Vector3(windup?-12*t:attack?Mathf.Sin(phase)*8:0,0,death?70*t:stun?Mathf.Sin(phase*6)*7:moving?Mathf.Sin(phase)*3:Mathf.Sin(phase)*1.5f);
                        if(name.StartsWith("Mandible"))e.z=side*(attack?18+Mathf.Sin(phase*3)*10:windup?12*t:0);
                        if(name=="Eye")e.x=windup?-10*t:attack?6:0;
                    }
                    else
                    {
                        if(name.StartsWith("Thigh"))e.x=death?-45*t:moving?Mathf.Sin(phase+side*Mathf.PI*.5f)*(kind==2?20:10):0;
                        if(name.StartsWith("Shin"))e.x=death?65*t:moving?Mathf.Max(0,-Mathf.Sin(phase+side*Mathf.PI*.5f))*28:0;
                        if(name.StartsWith("UpperArm"))e.x=attack?-18:windup?-12*t:moving?-Mathf.Sin(phase+side*Mathf.PI*.5f)*12:0;
                        if(name=="Chest")e=new Vector3(death?35*t:windup?-10*t:attack?Mathf.Sin(phase*3)*7:stun?15:Mathf.Sin(phase)*2,0,moving?Mathf.Sin(phase)*3:0);
                        if(name=="Pelvis"&&death)e.x=65*t;
                        if(name=="Head")e.x=stun?Mathf.Sin(phase*6)*7:windup?-8*t:0;
                        if(name=="Jaw")e.x=attack?20+Mathf.Sin(phase*4)*8:windup?15*t:0;
                        if(name.StartsWith("WingBase"))e.z=side*(death?65*t:stun?45:Mathf.Sin(phase*4)*32);
                        if(name.StartsWith("WingTip"))e.z=side*(death?35*t:stun?30:Mathf.Sin(phase*4-.5f)*18);
                    }
                    var q=rest*Quaternion.Euler(e);float[] values={q.x,q.y,q.z,q.w};
                    for(int j=0;j<4;j++)curves[j].AddKey(duration*t,values[j]);
                }
                string[] axes={"x","y","z","w"};for(int j=0;j<4;j++)clip.SetCurve(binding,typeof(Transform),"localRotation."+axes[j],curves[j]);
                if(name==(kind==1?"Body":"Pelvis"))
                {
                    var restPosition=bone.localPosition;var up=bone.parent.InverseTransformVector(Vector3.up);
                    for(int axis=0;axis<3;axis++)
                    {
                        var curve=new AnimationCurve();for(int i=0;i<=30;i++){float t=i/30f;float offset=death?-.2f*t:Mathf.Sin(t*Mathf.PI*2)*(moving?.025f:.012f);curve.AddKey(duration*t,restPosition[axis]+up[axis]*offset);}
                        clip.SetCurve(binding,typeof(Transform),"localPosition."+new[]{"x","y","z"}[axis],curve);
                    }
                }
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=!death&&!windup;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);return clip;
        }
        static AnimatorController Controller(string id,string[] names,AnimationClip[] clips)
        {
            string path=Anim+"/"+id+"/"+id+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=controller.layers[0].stateMachine;foreach(var state in machine.states)machine.RemoveState(state.state);foreach(var transition in machine.anyStateTransitions)machine.RemoveAnyStateTransition(transition);
            controller.parameters=Array.Empty<AnimatorControllerParameter>();controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            foreach(var parameter in new[]{"Dead","Stunned","WindingUp","Attacking","WeakPointOpen","Airborne"})controller.AddParameter(parameter,AnimatorControllerParameterType.Bool);
            for(int i=0;i<names.Length;i++)
            {
                var state=machine.AddState(names[i],new Vector3(280,i*65));state.motion=clips[i];
                if(i==0)machine.defaultState=state;
                var transition=machine.AddAnyStateTransition(state);transition.hasExitTime=false;transition.duration=.06f;transition.canTransitionToSelf=false;
                if(i==5){transition.AddCondition(AnimatorConditionMode.If,0,"Dead");continue;}
                transition.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
                if(i==4){transition.AddCondition(AnimatorConditionMode.If,0,"Stunned");continue;}
                transition.AddCondition(AnimatorConditionMode.IfNot,0,"Stunned");
                if(i==3){transition.AddCondition(AnimatorConditionMode.If,0,"Attacking");continue;}
                transition.AddCondition(AnimatorConditionMode.IfNot,0,"Attacking");
                if(i==2){transition.AddCondition(AnimatorConditionMode.If,0,"WindingUp");continue;}
                transition.AddCondition(AnimatorConditionMode.IfNot,0,"WindingUp");
                transition.AddCondition(i==0?AnimatorConditionMode.Less:AnimatorConditionMode.Greater,.12f,"Speed");
            }
            EditorUtility.SetDirty(controller);return controller;
        }
        static CombatProjectile Bolt(int kind,Color color)
        {
            string name=kind==0?"颤音声波弹":"觅音声波弹";
            string impactPath=Fx+"/"+name+"命中.prefab";
            var impact=new GameObject(name+"命中");var box=impact.AddComponent<CombatHitbox>();box.radius=.38f;box.lifetime=.03f;box.targetMask=1<<CombatLayers.Player;box.obstructionMask=CombatLayers.WorldMask;
            var impactAsset=PrefabUtility.SaveAsPrefabAsset(impact,impactPath);Object.DestroyImmediate(impact);
            var root=new GameObject(name);var projectile=root.AddComponent<CombatProjectile>();projectile.radius=.13f;projectile.gravity=0;projectile.lifetime=1.5f;projectile.collisionMask=CombatLayers.WorldMask|(1<<CombatLayers.Player);projectile.explosionPrefab=impactAsset.GetComponent<CombatHitbox>();
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(root.transform,false);sphere.transform.localScale=Vector3.one*.2f;Object.DestroyImmediate(sphere.GetComponent<Collider>());sphere.GetComponent<Renderer>().sharedMaterial=Material(name,color);
            var trail=root.AddComponent<TrailRenderer>();trail.sharedMaterial=Material(name,color);trail.time=.18f;trail.startWidth=.15f;trail.endWidth=0;trail.minVertexDistance=.05f;
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
            var asset=PrefabUtility.SaveAsPrefabAsset(root,Fx+"/"+name+".prefab");Object.DestroyImmediate(root);return asset.GetComponent<CombatProjectile>();
        }
        static Material Material(string name,Color color)
        {
            string path=Fx+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);EditorUtility.SetDirty(material);return material;
        }
        static void Tone(string name,float frequency,float duration)
        {
            string path=Audio+"/"+name+".wav";if(File.Exists(path))return;
            int rate=22050,count=(int)(duration*rate);using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++){float t=(float)i/rate,p=(float)i/count;float envelope=Mathf.Sin(Mathf.PI*p)*(.7f+.3f*Mathf.Sin(t*85));float value=Mathf.Sin(2*Mathf.PI*frequency*(t-t*t*.35f))*.23f*envelope;writer.Write((short)(value*short.MaxValue));}
            }
        }
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    }
}
