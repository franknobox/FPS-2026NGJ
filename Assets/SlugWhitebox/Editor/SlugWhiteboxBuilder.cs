using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

namespace SlugWhitebox.Editor
{
    public static class SlugWhiteboxBuilder
    {
        public const string Root="Assets/SlugWhitebox";
        public const string ScenePath=Root+"/Scenes/SlugWhitebox.unity";
        static Transform geometry;
        static Material wall,floor,cover,ink,white;
        [MenuItem("循声虫族/打开白盒场景")]
        public static void Open() {EditorSceneManager.OpenScene(ScenePath);}
        [MenuItem("循声虫族/重新生成白盒（覆盖生成场景）")]
        public static void Generate()
        {
            Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Settings");
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            wall=Mat("白盒墙体",.85f);floor=Mat("白盒地面",.58f);cover=Mat("白盒掩体",.73f);
            ink=Mat("白盒深灰",.22f);white=Mat("白盒占位",.95f);
            var line=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/声波线.mat");
            if(!line)
            {
                line=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(line,Root+"/Materials/声波线.mat");
            }
            var config=AssetDatabase.LoadAssetAtPath<SlugTuning>(Root+"/Settings/WhiteboxTuning.asset");
            if(!config){config=ScriptableObject.CreateInstance<SlugTuning>();AssetDatabase.CreateAsset(config,Root+"/Settings/WhiteboxTuning.asset");}
            var world=new GameObject("白盒几何 · 单位为米");geometry=world.transform;
            Box("地面",new Vector3(0,-.3f,43),new Vector3(30,.6f,102),floor);
            Box("左墙",new Vector3(-15,2.5f,43),new Vector3(1,5,102),wall);
            Box("右墙",new Vector3(15,2.5f,43),new Vector3(1,5,102),wall);
            Box("起点墙",new Vector3(0,2.5f,-8),new Vector3(31,5,1),wall);
            Box("终点墙",new Vector3(0,2.5f,94),new Vector3(31,5,1),wall);
            // A 2 m floor grid supplies an editable scale reference.
            for(int x=-14;x<=14;x+=2)Box("2m网格",new Vector3(x,.006f,43),new Vector3(.025f,.01f,101),cover,false);
            for(int z=-6;z<94;z+=2)Box("2m网格",new Vector3(0,.006f,z),new Vector3(29,.01f,.025f),cover,false);
            for(int sector=0;sector<3;sector++)
            {
                float z=sector*30;
                if(sector==0)
                {
                    Cover(-6,12,3,1.3f,3);Cover(6,12,3,1.3f,3);
                    Cover(-10,21,3,2.4f,3);Cover(10,21,3,2.4f,3);
                }
                else if(sector==1)
                {
                    Cover(0,z+12,5,2.4f,4);
                    Cover(-7,z+20,4,1.3f,2);Cover(7,z+20,4,1.3f,2);
                }
                else
                {
                    Cover(-6,z+10,3,2.4f,4);Cover(6,z+19,3,2.4f,4);
                    Cover(0,z+24,4,1.3f,2);
                }
                if(sector<2)
                {
                    Box("分区墙左",new Vector3(-9.5f,2.5f,z+29),new Vector3(11,5,1),wall);
                    Box("分区墙右",new Vector3(9.5f,2.5f,z+29),new Vector3(11,5,1),wall);
                    Box("门洞 · 宽8m",new Vector3(0,4.5f,z+29),new Vector3(8,1,1),wall);
                }
                WorldText("0"+(sector+1),new Vector3(-.6f,3.3f,z+28.4f),.7f);
            }
            var endpoint=new GameObject("终点");endpoint.transform.position=new Vector3(0,0,90);
            Box("终点范围",new Vector3(0,.02f,90),new Vector3(6,.04f,4),white,false);
            WorldText("EXIT",new Vector3(-1.1f,2.7f,93.4f),.6f);
            var sun=new GameObject("中性光").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;
            sun.color=Color.white;sun.transform.rotation=Quaternion.Euler(55,-25,0);sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.fog=false;
            var surface=world.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;
            surface.layerMask=1<<0;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
            string navPath=Root+"/Scenes/WhiteboxNavMesh.asset";var data=surface.navMeshData;
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(navPath))AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(data,navPath);surface.navMeshData=data;
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(Vector3.zero,new Vector3(0,0,90),NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                throw new Exception("Whitebox exit is not reachable");
            var spawnRoot=new GameObject("敌人出生点 · sector 0到2 / kind 0到2");
            for(int sector=0;sector<3;sector++)for(int i=0;i<14;i++)
            {
                float x=new[]{-10f,-3f,3f,10f}[i%4],z=sector*30+12+(i/4)*3.5f;
                if(!NavMesh.SamplePosition(new Vector3(x,0,z),out var sample,5,NavMesh.AllAreas))throw new Exception("Invalid spawn");
                var go=new GameObject("区域"+(sector+1)+"_出生点"+(i+1));go.transform.SetParent(spawnRoot.transform);go.transform.position=sample.position;
                var marker=go.AddComponent<SlugSpawn>();marker.sector=sector;marker.kind=i%5==0?1:i%4==0?2:0;
            }
            var player=new GameObject("玩家 · 高1.8m");player.layer=2;player.transform.position=new Vector3(0,.15f,-2);
            var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.4f;cc.center=Vector3.up*.9f;cc.stepOffset=.25f;
            var camGo=new GameObject("FPS Camera");camGo.tag="MainCamera";camGo.transform.SetParent(player.transform,false);camGo.transform.localPosition=new Vector3(0,1.62f,0);
            var camera=camGo.AddComponent<Camera>();camera.nearClipPlane=.035f;camera.farClipPlane=160;camera.fieldOfView=80;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.72f,.72f);
            camGo.AddComponent<AudioListener>();camGo.AddComponent<UniversalAdditionalCameraData>();
            var pivot=new GameObject("素材包武器").transform;pivot.SetParent(camGo.transform,false);
            var models=new GameObject[4];for(int i=0;i<4;i++){models[i]=Weapon(i,pivot);models[i].SetActive(i==0);}
            var manager=new GameObject("白盒入口 · 启动即玩").AddComponent<SlugGame>();
            manager.tuning=config;manager.surfaceMaterial=wall;manager.cyanMaterial=white;manager.orangeMaterial=ink;manager.lineMaterial=line;
            manager.player=player.transform;manager.view=camera;manager.weaponPivot=pivot;manager.weaponModels=models;manager.exit=endpoint.transform;
            manager.spawns=spawnRoot.GetComponentsInChildren<SlugSpawn>();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("SLUG_SCENE_OK · primitive blockout · 2m grid · connected exit route");
        }
        [MenuItem("循声虫族/构建 Windows 白盒")]
        public static void Build()
        {
            if(!File.Exists(ScenePath))Generate();Directory.CreateDirectory("Builds/SlugWhitebox");
            PlayerSettings.productName="循声虫族 · 声腺白盒";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/SlugWhitebox/SlugWhitebox.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("SLUG_BUILD_FAILED "+report.summary.result);
            Debug.Log("SLUG_BUILD_OK bytes="+report.summary.totalSize);
        }
        public static void GenerateAndBuild(){Generate();Build();}
        static Material Mat(string name,float value)
        {
            string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.color=new Color(value,value,value);material.SetFloat("_Smoothness",0);material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);return material;
        }
        static void Cover(float x,float z,float w,float h,float d)=>Box("掩体 · "+h+"m",new Vector3(x,h/2,z),new Vector3(w,h,d),cover);
        static GameObject Box(string name,Vector3 pos,Vector3 size,Material material,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(geometry);go.transform.position=pos;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;go.isStatic=true;
            if(!solid){UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.layer=2;}return go;
        }
        static void WorldText(string value,Vector3 pos,float scale)
        {
            var go=new GameObject(value);go.layer=2;go.transform.SetParent(geometry);go.transform.position=pos;go.transform.localScale=Vector3.one*scale;
            var text=go.AddComponent<TextMesh>();text.text=value;text.fontSize=48;text.characterSize=.3f;text.color=new Color(.15f,.15f,.15f);
        }
        static GameObject Weapon(int index,Transform parent)
        {
            return SlugWeaponAssets.Create(index,parent);
        }
    }
}
