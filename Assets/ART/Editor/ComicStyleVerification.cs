using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArtTools
{
    public static class ComicStyleVerification
    {
        static int ticks;
        const string AutoProbe = "Assets/ART/Materials/ComicAutoImportProbe.mat";

        public static void Run()
        {
            try
            {
                ArtResourceImport.ExitAfterImport = false;
                try { ArtResourceImport.ImportAll(); }
                finally { ArtResourceImport.ExitAfterImport = true; }
                ComicStyleSetup.ApplyAll();
                var testMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ART/Materials/Weapons/SonicWeapon_Surface.mat");
                Selection.activeObject=testMaterial;
                ComicStyleSetup.RestoreSelected();
                if(testMaterial.shader.name!="Universal Render Pipeline/Lit") throw new Exception("Material restoration failed");
                if(!ComicStyleSetup.GetSettings().autoStyleExclusions.Contains(testMaterial)) throw new Exception("Restored material is not protected from auto conversion");
                ComicStyleSetup.ApplyMaterials(new[]{AssetDatabase.GetAssetPath(testMaterial)});
                var settings=ComicStyleSetup.GetSettings();
                ComicStyleSetup.RestoreRenderers();
                foreach(var binding in settings.rendererBindings)
                {
                    var data=new SerializedObject(binding.pipeline).FindProperty("m_RendererDataList");
                    for(int i=0;i<binding.originalRenderers.Length;i++)
                        if(data.GetArrayElementAtIndex(i).objectReferenceValue!=binding.originalRenderers[i]) throw new Exception("Renderer restoration failed");
                }
                settings.enableInk=true;
                ComicStyleSetup.ApplyMaterials(Array.Empty<string>());
                if(settings.autoStyleNewArtMaterials)
                {
                    if(AssetDatabase.LoadAssetAtPath<Material>(AutoProbe)) throw new Exception("Unexpected pre-existing auto-import probe");
                    AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),AutoProbe);
                    AssetDatabase.ImportAsset(AutoProbe,ImportAssetOptions.ForceSynchronousImport);
                }
                ShaderUtil.allowAsyncCompilation = false;
                ticks = 0;
                EditorApplication.update += WaitForPipeline;
            }
            catch (Exception error) { Fail(error); }
        }

        static void WaitForPipeline()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || ++ticks < 10) return;
            EditorApplication.update -= WaitForPipeline;
            try { CaptureAndCheck(); }
            catch (Exception error) { Fail(error); }
        }

        static void CaptureAndCheck()
        {
            const string output = ".utmp/comic-style";
            Directory.CreateDirectory(output);
            if(ComicStyleSetup.GetSettings().autoStyleNewArtMaterials)
            {
                var probe=AssetDatabase.LoadAssetAtPath<Material>(AutoProbe);
                if(!probe || probe.shader.name!="ART/Comic/Toon Lit") throw new Exception("Automatic ART material conversion did not run");
                probe.EnableKeyword("_ALPHATEST_ON");
                for(int pass=0;pass<probe.passCount;pass++) ShaderUtil.CompilePass(probe,pass,true);
                string backup="Assets/ART/Settings/OriginalMaterials/"+AssetDatabase.AssetPathToGUID(AutoProbe)+".mat";
                AssetDatabase.DeleteAsset(AutoProbe);
                AssetDatabase.DeleteAsset(backup);
            }
            ArtShowcaseSetup.UpdateShowcase();
            var scene = EditorSceneManager.OpenScene(ArtShowcaseSetup.ScenePath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First();
            var actors = scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true))
                .Where(t=>PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && t.GetComponentInChildren<SkinnedMeshRenderer>())
                .Select(t=>t.gameObject).ToArray();
            foreach(var actor in actors)
                if(actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Head").position.y<=actor.transform.position.y)
                    throw new Exception("Preview model upside down: "+actor.name);
            var spatial=actors
                .Select(o=>new{name=o.name,rootY=o.transform.position.y,
                    headY=o.GetComponentsInChildren<Transform>().First(t=>t.name=="Head").position.y,
                    headViewportY=camera.WorldToViewportPoint(o.GetComponentsInChildren<Transform>().First(t=>t.name=="Head").position).y,
                    rootViewportY=camera.WorldToViewportPoint(o.transform.position).y}).ToArray();
            File.WriteAllText(output+"/spatial.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{spatial,graphicsUVStartsAtTop=SystemInfo.graphicsUVStartsAtTop,cameraUp=new[]{camera.transform.up.x,camera.transform.up.y,camera.transform.up.z}},Newtonsoft.Json.Formatting.Indented));
            var materials = AssetDatabase.FindAssets("t:Material",new[]{"Assets/ART/Materials/Enemies","Assets/ART/Materials/Weapons"})
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
            if (materials.Length < 5 || materials.Any(m => m.shader.name != "ART/Comic/Toon Lit")) throw new Exception("Some ART materials are not using the comic shader");
            foreach (var material in materials)
                for(int pass=0;pass<material.passCount;pass++) ShaderUtil.CompilePass(material,pass,true);
            var originalPipeline = QualitySettings.renderPipeline;
            var results = new System.Collections.Generic.List<object>();
            try
            {
                foreach(string name in new[]{"PC","Mobile"})
                {
                    var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/"+name+"_RPAsset.asset");
                    QualitySettings.renderPipeline = pipeline;
                    var settings = new SerializedObject(pipeline);
                    var data = settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
                    var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().Single(f => f.name == "Comic Ink");
                    feature.SetActive(true);
                    Render(camera,null); // warm up renderer state and shader variants
                    var ink = Render(camera,output+"/comic-"+name+".png");
                    feature.SetActive(false);
                    Color32[] plain;
                    try { plain = Render(camera,output+"/without-ink-"+name+".png"); }
                    finally { feature.SetActive(true); }
                    int changed=0,magenta=0;
                    for(int i=0;i<ink.Length;i++)
                    {
                        if(Math.Abs((int)ink[i].r-plain[i].r)+Math.Abs((int)ink[i].g-plain[i].g)+Math.Abs((int)ink[i].b-plain[i].b)>18) changed++;
                        if(ink[i].r>230 && ink[i].b>230 && ink[i].g<50) magenta++;
                    }
                    if(changed<100) throw new Exception("Ink pass did not affect the "+name+" render: "+changed);
                    if(magenta>ink.Length/200) throw new Exception("Possible shader error pixels in "+name);
                    results.Add(new{name,changedPixels=changed,magentaPixels=magenta,renderer=AssetDatabase.GetAssetPath(data),features=data.rendererFeatures.Select(f=>f.name).ToArray()});
                }
            }
            finally { QualitySettings.renderPipeline = originalPipeline; }
            foreach(string shaderName in new[]{"ART/Comic/Toon Lit","Hidden/ART/Comic/Screen Ink"})
            {
                var shader=Shader.Find(shaderName);
                var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray();
                if(errors.Length>0) throw new Exception(shaderName+": "+string.Join(";",errors.Select(e=>e.message)));
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(output+"/verification.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{materials=materials.Select(m=>AssetDatabase.GetAssetPath(m)).ToArray(),results},Newtonsoft.Json.Formatting.Indented));
            Debug.Log("COMIC_RENDER_VERIFIED");
            EditorApplication.Exit(0);
        }

        static Color32[] Render(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;
            var image=new Texture2D(1600,900,TextureFormat.RGBA32,false,false);
            try
            {
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1600,900),0,0);
                image.Apply();
                if(path!=null) File.WriteAllBytes(path,image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void Fail(Exception error)
        {
            Debug.LogException(error);
            EditorApplication.update-=WaitForPipeline;
            EditorApplication.Exit(1);
        }
    }
}
