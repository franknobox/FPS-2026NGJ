using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NGJ.Combat;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wasteland;
using Object = UnityEngine.Object;

namespace Wasteland.Editor
{
    public static class WastelandLevelBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/荒野前哨.unity";
        const string Root = "Assets/Game/Wasteland";
        const string Environment = "Assets/ART/Prefabs/Environment/";
        static readonly Vector3[] Centers = { Vector3.zero, new Vector3(10,0,56), new Vector3(-6,0,118) };
        static readonly Vector2[] Sizes = { new Vector2(44,40), new Vector2(48,44), new Vector2(52,48) };
        static readonly string[] Names = { "01 / 前哨外围", "02 / 能源回收场", "03 / 指挥楼外院" };
        static readonly Vector3[] Entrances = { new Vector3(0,0,-20), new Vector3(20,0,34), new Vector3(-14,0,94) };
        static readonly Vector3[] Exits = { new Vector3(10,0,20), new Vector3(2,0,78), new Vector3(-6,0,142) };
        static readonly List<NavMeshBuildSource> Sources = new List<NavMeshBuildSource>();
        static readonly List<Bounds> Floors = new List<Bounds>();
        static readonly List<Bounds> Obstacles = new List<Bounds>();
        static Transform generated, floorRoot, frameRoot, propRoot, decorationRoot, spawnRoot;
        static Material ground, rock, metal, amber, open, closed;
        static System.Random random;

        [MenuItem("循声虫族/正式关卡/打开荒野前哨")]
        public static void Open() { EditorSceneManager.OpenScene(ScenePath); }
        [MenuItem("循声虫族/正式关卡/生成荒野前哨（默认种子）")]
        public static void Build() { BuildSeed(20261115); }
        [MenuItem("循声虫族/正式关卡/更换内部布局种子并重建")]
        public static void BuildNextSeed()
        {
            var current = Object.FindFirstObjectByType<WastelandRunDirector>();
            BuildSeed(current ? current.seed + 1 : 20261116);
        }

        public static void BuildSeed(int seed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请先停止运行");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Root + "/Settings"); EnsureFolder(Root + "/Navigation");
            EnsureFolder("Assets/ART/Materials/Environment/Wasteland"); EnsureFolder("Assets/ART/Textures/Environment/Wasteland");
            Sources.Clear(); Floors.Clear(); Obstacles.Clear(); random = new System.Random(seed);
            var scene = EditorSceneManager.OpenScene("Assets/Game/SlugWhitebox/Scenes/SlugWhitebox.unity");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var game = Object.FindFirstObjectByType<SlugGame>();
            var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);
            var playerRoot = game.player.root.gameObject;
            foreach (var root in scene.GetRootGameObjects())
                if (root != game.gameObject && root != playerRoot && root != light.gameObject) Object.DestroyImmediate(root);
            game.gameObject.name = "荒野前哨 · 战斗与流程";
            game.spawns = Array.Empty<SlugSpawn>();
            var tuning = AssetDatabase.LoadAssetAtPath<SlugTuning>(Root + "/Settings/WastelandTuning.asset");
            if (!tuning) { tuning = Object.Instantiate(game.tuning); AssetDatabase.CreateAsset(tuning,Root + "/Settings/WastelandTuning.asset"); }
            game.tuning = tuning;
            generated = new GameObject("关卡框架 · 固定外轮廓").transform;
            floorRoot = Group("可行走地面碰撞", generated);
            frameRoot = Group("围墙与缓冲", generated);
            propRoot = Group("内部布局 · 种子 " + seed, generated);
            decorationRoot = Group("荒野地表与碎石", generated);
            spawnRoot = Group("已验证出生点", generated);
            PrepareMaterials();
            var director = game.gameObject.AddComponent<WastelandRunDirector>();
            director.seed = seed; director.spawnMaterial = game.lineMaterial;
            director.areas = new WastelandArea[3]; game.levelFlow = director;
            for (int i=0;i<3;i++)
            {
                var size = Sizes[i]; var center = Centers[i];
                Floor(Names[i], center, new Vector2(size.x,size.y));
                var area = new WastelandArea { displayName = Names[i], bounds = new Bounds(center+Vector3.up*4,new Vector3(size.x,12,size.y)),
                    firstWave = new[]{4,5,7}[i], secondWave = new[]{4,6,7}[i] };
                area.entrance = Gate("入口 " + (i+1), Entrances[i], false, (i+1).ToString("00"));
                area.exit = Gate("出口 " + (i+1), Exits[i], true, i==2?"EXIT":"CLEAR");
                director.areas[i] = area;
                float x0=center.x-size.x*.5f,x1=center.x+size.x*.5f,z0=center.z-size.y*.5f,z1=center.z+size.y*.5f;
                WallWithOpening(new Vector3(x0,0,z0),new Vector3(x1,0,z0),Entrances[i],8);
                WallLine(new Vector3(x1,0,z0),new Vector3(x1,0,z1));
                WallWithOpening(new Vector3(x1,0,z1),new Vector3(x0,0,z1),Exits[i],8);
                WallLine(new Vector3(x0,0,z1),new Vector3(x0,0,z0));
            }
            // The same wide floor continues through both doglegs and the starting/exit pockets.
            Floor("起点准备区",new Vector3(0,0,-28),new Vector2(10,16));
            WallLine(new Vector3(-5,0,-20),new Vector3(-5,0,-36));
            WallLine(new Vector3(-5,0,-36),new Vector3(5,0,-36));
            WallLine(new Vector3(5,0,-36),new Vector3(5,0,-20));
            Connector(Exits[0],Entrances[1]); Connector(Exits[1],Entrances[2]);
            Floor("撤离平台",new Vector3(-6,0,148),new Vector2(10,12));
            WallLine(new Vector3(-11,0,154),new Vector3(-11,0,142));
            WallLine(new Vector3(-1,0,142),new Vector3(-1,0,154));
            WallLine(new Vector3(-1,0,154),new Vector3(-11,0,154));
            var end = new GameObject("撤离点 EXIT").transform; end.SetParent(generated); end.position=new Vector3(-6,0,151); game.exit=end;
            Text("EXIT",new Vector3(-6,3,153.5f),.075f,frameRoot);
            PlaceLandmarks(); PlaceInterior(seed);
            Terrain(); EdgeRocks(seed); Lighting(light,game.view);
            game.player.position = director.StartPosition; game.player.rotation=Quaternion.identity;
            game.view.transform.localRotation=Quaternion.identity;
            Physics.SyncTransforms();
            BakeNavigation();
            for(int i=0;i<3;i++) BuildSpawns(director.areas[i],i);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            buildScenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s =>
                new EditorBuildSettingsScene(s.path, s.path == "Assets/Game/SlugWhitebox/Scenes/SlugWhitebox.unity" ? false : s.enabled)));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            // Saving/reimporting a replaced NavMesh asset can invalidate its live editor instance.
            var navigation = generated.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            navigation.RemoveData(); navigation.AddData();
            Verify();
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,55),Quaternion.Euler(62,0,0),95,true,true);
            Debug.Log("WASTELAND_BUILT seed="+seed+" rooms=44x40/48x44/52x48 buffers=2");
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent,false); return go.transform;
        }
        static void Floor(string name,Vector3 center,Vector2 size)
        {
            var bounds=new Bounds(center+Vector3.down*.3f,new Vector3(size.x,.6f,size.y));
            var go=new GameObject(name);go.transform.SetParent(floorRoot);go.transform.position=bounds.center;go.layer=LayerMask.NameToLayer("Ground");
            go.AddComponent<BoxCollider>().size=bounds.size;
            Sources.Add(BoxSource(bounds.center,bounds.size,Quaternion.identity,0));
            Floors.Add(new Bounds(center,new Vector3(size.x,1,size.y)));
        }
        static NavMeshBuildSource BoxSource(Vector3 center,Vector3 size,Quaternion rotation,int area)
        {
            return new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(center,rotation,Vector3.one),size=size,area=area };
        }
        static void WallWithOpening(Vector3 a,Vector3 b,Vector3 door,float width)
        {
            Vector3 direction=(b-a).normalized;
            WallLine(a,door-direction*width*.5f); WallLine(door+direction*width*.5f,b);
        }
        static void WallLine(Vector3 a,Vector3 b)
        {
            var delta=b-a;float length=delta.magnitude;if(length<.1f)return;
            float yaw=-Mathf.Atan2(delta.z,delta.x)*Mathf.Rad2Deg;
            int count=Mathf.CeilToInt(length/4.5f);
            for(int i=0;i<count;i++)
            {
                string asset=i%3==0?"墙体01":"墙体03";
                Prop(asset,Vector3.Lerp(a,b,(i+.5f)/count),yaw,frameRoot,false);
            }
            var seam=new GameObject("墙脚连续碰撞");seam.transform.SetParent(frameRoot);seam.transform.position=(a+b)*.5f+Vector3.up*.78f;
            seam.transform.rotation=Quaternion.Euler(0,yaw,0);seam.layer=LayerMask.NameToLayer("Ground");
            seam.AddComponent<BoxCollider>().size=new Vector3(length,1.56f,.55f);
        }
        static void Connector(Vector3 a,Vector3 b)
        {
            float middle=(a.z+b.z)*.5f;
            Vector3 p=new Vector3(a.x,0,middle),q=new Vector3(b.x,0,middle);
            foreach(var edge in new[]{(a,p),(p,q),(q,b)})
                Floor("缓冲通路",(edge.Item1+edge.Item2)*.5f,new Vector2(Mathf.Abs(edge.Item1.x-edge.Item2.x)+8,Mathf.Abs(edge.Item1.z-edge.Item2.z)+8));
            // Orthogonal union of the three floor strips; start and end edges remain open.
            bool right=b.x>a.x;float s=right?1:-1;
            Vector3[] polygon={a+Vector3.left*4*s,a+Vector3.right*4*s,
                new Vector3(a.x+4*s,0,middle-4),new Vector3(b.x+4*s,0,middle-4),b+Vector3.right*4*s,b+Vector3.left*4*s,
                new Vector3(b.x-4*s,0,middle+4),new Vector3(a.x-4*s,0,middle+4)};
            for(int i=1;i<polygon.Length;i++)if(i!=4)WallLine(polygon[i],polygon[(i+1)%polygon.Length]);
            // A small quiet landing, without forced healing or enemy activation.
            var pad=Cube("缓冲区导向标",(p+q)*.5f+Vector3.up*.015f,new Vector3(2.5f,.025f,2),amber,decorationRoot,false);
        }
        static GameObject Prop(string name,Vector3 position,float yaw,Transform parent,bool obstacle=true)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Environment+name+".prefab");
            if(!prefab)throw new Exception("Missing prop "+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            instance.transform.position=position;instance.transform.rotation=Quaternion.Euler(0,yaw,0);instance.transform.localScale=Vector3.one;
            var local=ArtTools.EnvironmentSizeSetup.ContentBounds(prefab);
            var center=instance.transform.TransformPoint(local.center);
            // Explicit box build sources avoid reading non-readable imported art meshes at runtime.
            var size=local.size+new Vector3(.12f,0,.12f);
            Sources.Add(BoxSource(center,size,instance.transform.rotation,1));
            if(obstacle)
            {
                var bounds=new Bounds(center,new Vector3(Mathf.Abs(Mathf.Cos(yaw*Mathf.Deg2Rad))*size.x+Mathf.Abs(Mathf.Sin(yaw*Mathf.Deg2Rad))*size.z,size.y,
                    Mathf.Abs(Mathf.Sin(yaw*Mathf.Deg2Rad))*size.x+Mathf.Abs(Mathf.Cos(yaw*Mathf.Deg2Rad))*size.z));
                Obstacles.Add(bounds);
            }
            return instance;
        }
        static void PlaceLandmarks()
        {
            Prop("塔",new Vector3(-15,0,9),0,propRoot);
            Prop("桥",new Vector3(-15,0,-9),90,propRoot);
            Prop("集装箱",new Vector3(8,0,5),90,propRoot);
            Prop("集装箱",new Vector3(14,0,-7),0,propRoot);
            Prop("墙体02_转角",new Vector3(14,0,12),180,propRoot);
            Prop("能源站",new Vector3(-3,0,54),0,propRoot);
            Prop("集装箱",new Vector3(24,0,52),0,propRoot);
            Prop("集装箱",new Vector3(20,0,67),90,propRoot);
            Prop("墙体02_转角",new Vector3(-6,0,68),90,propRoot);
            Prop("前哨指挥楼",new Vector3(8,0,128),180,propRoot);
            Prop("集装箱",new Vector3(-20,0,119),90,propRoot);
            Prop("集装箱",new Vector3(-6,0,129),0,propRoot);
            Prop("墙体02_转角",new Vector3(12,0,105),0,propRoot);
        }
        static void PlaceInterior(int seed)
        {
            // Slots keep the central traversal spine clear; the seed changes only internal pieces and offsets.
            Vector2[][] slots={
                new[]{new Vector2(-7,-8),new Vector2(12,-13),new Vector2(-6,8),new Vector2(5,13),new Vector2(17,3),new Vector2(-15,16)},
                new[]{new Vector2(4,42),new Vector2(28,42),new Vector2(12,51),new Vector2(8,66),new Vector2(27,72),new Vector2(-9,45)},
                new[]{new Vector2(-24,102),new Vector2(4,105),new Vector2(-11,111),new Vector2(-20,131),new Vector2(10,137),new Vector2(0,116)}};
            for(int room=0;room<3;room++)for(int i=0;i<slots[room].Length;i++)
            {
                var slot=slots[room][i];var position=new Vector3(slot.x+(float)(random.NextDouble()-.5)*1.1f,0,slot.y+(float)(random.NextDouble()-.5)*1.1f);
                Prop("路障"+(1+(i+room+random.Next(3))%6).ToString("00"),position,random.Next(4)*90,propRoot);
            }
        }
        static WastelandGate Gate(string name,Vector3 position,bool locked,string label)
        {
            var root=new GameObject(name);root.transform.SetParent(frameRoot);root.transform.position=position;
            var gate=root.AddComponent<WastelandGate>();
            foreach(float side in new[]{-1f,1f})
            {
                Cube("门柱",position+new Vector3(side*4.4f,1.7f,0),new Vector3(.7f,3.4f,1),metal,root.transform,true);
                Sources.Add(BoxSource(position+new Vector3(side*4.4f,1.7f,0),new Vector3(.7f,3.4f,1),Quaternion.identity,1));
                Cube("琥珀标识",position+new Vector3(side*4.4f,2.3f,-.53f),new Vector3(.28f,.9f,.06f),amber,root.transform,false);
            }
            Cube("门楣",position+Vector3.up*3.3f,new Vector3(9.3f,.35f,1),metal,root.transform,false);
            var barrier=new GameObject("战斗封锁");barrier.transform.SetParent(root.transform);barrier.transform.position=position;
            var panel=Cube("封锁挡板",position+Vector3.up*1.25f,new Vector3(8,2.5f,.3f),metal,barrier.transform,true);
            panel.layer=LayerMask.NameToLayer("Interactable");
            for(int i=-3;i<=3;i++)Cube("封锁斜纹",position+new Vector3(i,1.25f,-.17f),new Vector3(.12f,2.2f,.025f),closed,barrier.transform,false).transform.rotation=Quaternion.Euler(0,0,-22);
            gate.barrier=barrier;
            gate.indicator=Cube("入口状态灯",position+new Vector3(0,3.3f,-.53f),new Vector3(1.6f,.13f,.05f),open,root.transform,false).GetComponent<Renderer>();
            gate.openMaterial=open;gate.closedMaterial=closed;gate.SetClosed(locked);
            Text(label,position+new Vector3(0,3.9f,0),.06f,root.transform);
            return gate;
        }
        static GameObject Cube(string name,Vector3 position,Vector3 size,Material material,Transform parent,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static void Text(string text,Vector3 position,float size,Transform parent)
        {
            var go=new GameObject(text);go.transform.SetParent(parent);go.transform.position=position;go.transform.rotation=Quaternion.identity;
            var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=96;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(1,.86f,.5f);
        }
        static Material Mat(string name,Color color,float emission=0)
        {
            string path="Assets/ART/Materials/Environment/Wasteland/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("ART/Comic/Toon Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.05f);
            material.SetFloat("_HatchStrength",0);material.SetFloat("_SpecularStrength",.05f);material.SetFloat("_Cull",2);
            if(emission>0)material.SetColor("_EmissionColor",color*emission);
            EditorUtility.SetDirty(material);return material;
        }
        static void PrepareMaterials()
        {
            ground=Mat("黄色荒野地表",Color.white);ground.SetFloat("_VertexTint",1);ground.SetFloat("_WorldSpaceUV",1);ground.SetFloat("_UVs",3);
            ground.SetColor("_ShadowColor",new Color(.52f,.46f,.35f));ground.SetColor("_MidColor",new Color(.87f,.83f,.73f));ground.SetFloat("_AmbientFloor",.35f);
            rock=Mat("砂岩",new Color(.48f,.38f,.24f));metal=Mat("深灰金属",new Color(.15f,.18f,.19f));
            amber=Mat("琥珀导向",new Color(.95f,.59f,.16f),.15f);open=Mat("通路开放",new Color(.1f,.8f,.62f),.5f);closed=Mat("战斗封锁",new Color(1,.2f,.2f),.35f);
            string path="Assets/ART/Textures/Environment/Wasteland/SandDetail.png";
            // Rebuild the same seamless detail texture deterministically.
            {
                int size=512;var texture=new Texture2D(size,size,TextureFormat.RGB24,false);
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=(float)x/(size-1),v=(float)y/(size-1);
                    float noise=Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(u*8,v*8),Mathf.PerlinNoise(u*8-8,v*8),u),Mathf.Lerp(Mathf.PerlinNoise(u*8,v*8-8),Mathf.PerlinNoise(u*8-8,v*8-8),u),v);
                    uint hash=unchecked((uint)(x%511)*374761393u+(uint)(y%511)*668265263u);
                    hash=unchecked((hash^(hash>>13))*1274126177u);
                    float grain=((hash^(hash>>16))&65535)/65535f-.5f;
                    pixels[y*size+x]=Color.white*(.84f+noise*.12f+grain*.02f);
                }
                texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            }
            ground.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        static void Terrain()
        {
            const int nx=89,nz=153;var vertices=new Vector3[nx*nz];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[(nx-1)*(nz-1)*6];
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                float px=-66+x*1.5f,pz=-48+z*1.5f;float distance=float.MaxValue;
                foreach(var floor in Floors)
                {
                    float dx=Mathf.Max(floor.min.x-px,0,px-floor.max.x),dz=Mathf.Max(floor.min.z-pz,0,pz-floor.max.z);
                    distance=Mathf.Min(distance,Mathf.Sqrt(dx*dx+dz*dz));
                }
                float height=Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-.4f)/6))*(.7f+Mathf.PerlinNoise(px*.075f+7,pz*.075f+19)*2.3f);
                vertices[z*nx+x]=new Vector3(px,height,pz);uv[z*nx+x]=new Vector2(px/3,pz/3);
                float patch=Mathf.PerlinNoise(px*.045f+14,pz*.045f+31)*.7f+Mathf.PerlinNoise(px*.15f+3,pz*.15f+11)*.3f;
                colors[z*nx+x]=Color.Lerp(new Color(.56f,.41f,.22f),new Color(.9f,.74f,.43f),patch);
                if(x<nx-1&&z<nz-1)
                {
                    int k=(z*(nx-1)+x)*6,i=z*nx+x;triangles[k]=i;triangles[k+1]=i+nx;triangles[k+2]=i+1;triangles[k+3]=i+1;triangles[k+4]=i+nx;triangles[k+5]=i+nx+1;
                }
            }
            var mesh=new Mesh{name="WastelandSurface"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.colors=colors;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            const string path="Assets/ART/Models/Environment/WastelandGround.asset";
            var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;}else AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject("黄色荒野 · 连续地表");go.transform.SetParent(decorationRoot);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=ground;
        }
        static void EdgeRocks(int seed)
        {
            var rng=new System.Random(seed+871);
            for(int room=0;room<3;room++)for(int i=0;i<16;i++)
            {
                float side=i%2==0?-1:1;
                var pos=Centers[room]+new Vector3(side*(Sizes[room].x*.5f+2+(float)rng.NextDouble()*4),0,((float)rng.NextDouble()-.5f)*Sizes[room].y);
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="外围风蚀岩";go.transform.SetParent(decorationRoot);go.transform.position=pos;
                go.transform.localScale=new Vector3(2+(float)rng.NextDouble()*3,1+(float)rng.NextDouble()*2,2+(float)rng.NextDouble()*3);
                go.GetComponent<Renderer>().sharedMaterial=rock;Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }
        static void Lighting(Light sun,Camera camera)
        {
            sun.name="荒野暖阳";sun.intensity=1.3f;sun.color=new Color(1,.9f,.72f);sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.shadows=LightShadows.Soft;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.56f,.62f,.68f);RenderSettings.ambientEquatorColor=new Color(.42f,.39f,.3f);RenderSettings.ambientGroundColor=new Color(.29f,.24f,.17f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.69f,.62f,.48f);RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=185;
            var shader=Shader.Find("Skybox/Procedural");
            if(shader)
            {
                string path="Assets/ART/Materials/Environment/Wasteland/荒野天空.mat";
                var sky=AssetDatabase.LoadAssetAtPath<Material>(path);if(!sky){sky=new Material(shader);AssetDatabase.CreateAsset(sky,path);}
                sky.SetColor("_SkyTint",new Color(.63f,.72f,.82f));sky.SetColor("_GroundColor",new Color(.51f,.42f,.26f));sky.SetFloat("_Exposure",1.1f);RenderSettings.skybox=sky;
                camera.clearFlags=CameraClearFlags.Skybox;
            }
            camera.backgroundColor=RenderSettings.fogColor;camera.farClipPlane=230;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        }
        static void BakeNavigation()
        {
            var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.7f;settings.agentHeight=2.2f;settings.agentClimb=.2f;settings.overrideVoxelSize=true;settings.voxelSize=.16f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,Sources,new Bounds(new Vector3(0,4,60),new Vector3(100,30,220)),Vector3.zero,Quaternion.identity);
            if(!data)throw new Exception("Navigation build failed");
            string path=Root+"/Navigation/WastelandNavMesh.asset";
            var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if(existing)AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data,path);
            var surface=generated.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();surface.navMeshData=data;surface.AddData();
        }
        static void BuildSpawns(WastelandArea area,int room)
        {
            var root=Group(area.displayName+" 出生点",spawnRoot);var points=new List<Transform>();var air=new List<Transform>();
            for(int z=0;z<5;z++)for(int x=0;x<6;x++)
            {
                var position=area.bounds.center+new Vector3((x-2.5f)*6,0,(z-1.5f)*6);position.y=0;
                if(!area.Contains(position,4) || Vector3.Distance(position,Entrances[room])<11 || Obstacles.Any(b=>ExpandedXZ(b,1.4f).Contains(new Vector3(position.x,b.center.y,position.z))))continue;
                if(!NavMesh.SamplePosition(position,out var hit,1,NavMesh.AllAreas)||Mathf.Abs(hit.position.y)>.2f)continue;
                var marker=new GameObject("地面出生点 "+points.Count).transform;marker.SetParent(root);marker.position=hit.position;points.Add(marker);
                var flyPosition=hit.position+Vector3.up*3;
                if(!Physics.CheckCapsule(flyPosition+Vector3.up*.4f,flyPosition+Vector3.up*1.4f,.55f,CombatLayers.WorldMask,QueryTriggerInteraction.Ignore))
                {
                    var flying=new GameObject("飞行出生点 "+air.Count).transform;flying.SetParent(root);flying.position=flyPosition;air.Add(flying);
                }
            }
            if(points.Count<8||air.Count<8)throw new Exception("Not enough valid spawns: "+area.displayName+" "+points.Count+"/"+air.Count);
            area.groundSpawns=points.ToArray();area.airSpawns=air.ToArray();
        }
        static Bounds ExpandedXZ(Bounds bounds,float amount){bounds.Expand(new Vector3(amount*2,0,amount*2));return bounds;}
        public static void Verify()
        {
            var director=Object.FindFirstObjectByType<WastelandRunDirector>();
            if(!director||director.areas.Length!=3)throw new Exception("Missing formal level director");
            var waypoints=new[]{director.StartPosition,Entrances[0],new Vector3(0,0,0),Exits[0],new Vector3(10,0,27),new Vector3(20,0,27),Entrances[1],new Vector3(10,0,56),Exits[1],new Vector3(2,0,86),new Vector3(-14,0,86),Entrances[2],new Vector3(-6,0,118),Exits[2],new Vector3(-6,0,151)};
            for(int i=1;i<waypoints.Length;i++)
            {
                if(!NavMesh.SamplePosition(waypoints[i-1],out var a,3,NavMesh.AllAreas)||!NavMesh.SamplePosition(waypoints[i],out var b,3,NavMesh.AllAreas))throw new Exception("Navigation endpoint missing "+i);
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Navigation disconnected at "+i);
            }
            foreach(var area in director.areas)foreach(var marker in area.groundSpawns)
            {
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(marker.position,area.entrance.transform.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Unreachable spawn "+marker.name);
            }
            foreach(var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if(PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject)&&AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject)).StartsWith(Environment)&&Vector3.Distance(transform.localScale,Vector3.one)>.0001f)
                    throw new Exception("Prop scale changed "+transform.name);
            if(UnityEditor.ShaderUtil.ShaderHasError(Shader.Find("ART/Comic/Toon Lit")))throw new Exception("Comic terrain shader error");
            Debug.Log("WASTELAND_VERIFIED connectedSegments=14 areas=3 spawns="+string.Join("/",director.areas.Select(a=>a.groundSpawns.Length+"g+"+a.airSpawns.Length+"a"))+" PlayMode="+EditorApplication.isPlaying);
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');EnsureFolder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));
        }
    }
}
