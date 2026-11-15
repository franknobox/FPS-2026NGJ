using System;
using System.Collections.Generic;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ArtTools
{
    public static class SoundGlandDropSetup
    {
        const string Models="Assets/ART/Models/Pickups";
        const string Mats="Assets/ART/Materials/Pickups";
        public const string PrefabPath="Assets/ART/Prefabs/Pickups/声腺掉落.prefab";
        [MenuItem("美术工具/掉落物/制作并接入声腺")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("请先保存当前场景");
            string original=EditorSceneManager.GetActiveScene().path;
            Folder(Models);Folder(Mats);Folder("Assets/ART/Prefabs/Pickups");
            var shell=Material("声腺甲壳",new Color(.66f,.48f,.24f));
            var core=Material("声腺核心",new Color(.66f,.105f,.055f));
            var gold=Material("声腺共鸣环",new Color(1,.64f,.15f));
            var ink=Material("声腺孔隙",new Color(.035f,.027f,.022f));
            var gland=GlandMesh();var plate=PlateMesh();var ring=RingMesh();
            var root=new GameObject("声腺掉落");
            try
            {
                Part(root.transform,"暗红声腺",gland,core,Vector3.zero,Quaternion.identity);
                for(int i=0;i<3;i++)
                {
                    var rotation=Quaternion.Euler(0,i*120,0);
                    Part(root.transform,"外壳瓣 "+i,plate,shell,Vector3.zero,rotation);
                    for(int j=0;j<2;j++)
                    {
                        var vent=GameObject.CreatePrimitive(PrimitiveType.Cube);vent.name="金色声纹";
                        vent.transform.SetParent(root.transform,false);vent.transform.localPosition=rotation*new Vector3(0,-.015f-j*.033f,.157f);
                        vent.transform.localRotation=rotation;vent.transform.localScale=new Vector3(.035f,.009f,.009f);
                        Object.DestroyImmediate(vent.GetComponent<Collider>());vent.GetComponent<Renderer>().sharedMaterial=gold;
                    }
                }
                Part(root.transform,"顶部共鸣环",ring,gold,new Vector3(0,.143f,0),Quaternion.identity);
                var opening=GameObject.CreatePrimitive(PrimitiveType.Cylinder);opening.name="共鸣孔";opening.transform.SetParent(root.transform,false);
                opening.transform.localPosition=new Vector3(0,.132f,0);opening.transform.localScale=new Vector3(.11f,.009f,.11f);
                Object.DestroyImmediate(opening.GetComponent<Collider>());opening.GetComponent<Renderer>().sharedMaterial=ink;
                Combine(root);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("Interactable");
                foreach(var r in root.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;}
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach(var path in new[]{Wasteland.Editor.WastelandLevelBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);var game=Object.FindFirstObjectByType<SlugGame>();
                game.glandDropPrefab=prefab;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();ArtShowcaseSetup.UpdateShowcase();
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        }
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int index=path.LastIndexOf('/');Folder(path.Substring(0,index));AssetDatabase.CreateFolder(path.Substring(0,index),path.Substring(index+1));
        }
        static void Combine(GameObject root)
        {
            var parts=root.GetComponentsInChildren<MeshFilter>();
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var part in parts)
            {
                var material=part.GetComponent<Renderer>().sharedMaterial;
                if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups.Add(material,list);}
                list.Add(new CombineInstance{mesh=part.sharedMesh,transform=root.transform.worldToLocalMatrix*part.transform.localToWorldMatrix});
            }
            var meshes=new List<Mesh>();var materials=new List<Material>();var instances=new List<CombineInstance>();
            foreach(var group in groups){var mesh=new Mesh();mesh.CombineMeshes(group.Value.ToArray(),true,true);meshes.Add(mesh);materials.Add(group.Key);instances.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});}
            var combined=new Mesh();combined.name="声腺掉落合并";combined.CombineMeshes(instances.ToArray(),false,true);
            string path=Models+"/声腺掉落合并.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(combined,saved);Object.DestroyImmediate(combined);}else{saved=combined;AssetDatabase.CreateAsset(saved,path);}
            foreach(var mesh in meshes)Object.DestroyImmediate(mesh);
            while(root.transform.childCount>0)Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            root.AddComponent<MeshFilter>().sharedMesh=saved;root.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
        }
        static Material Material(string name,Color color)
        {
            string path=Mats+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("ART/Comic/Toon Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_AmbientFloor",.24f);mat.SetFloat("_AmbientStrength",.8f);mat.SetFloat("_Smoothness",.2f);mat.SetFloat("_HatchStrength",.02f);
            EditorUtility.SetDirty(mat);return mat;
        }
        static void Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Quaternion rotation)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        static Mesh Save(string name,List<Vector3> vertices)
        {
            string path=Models+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name=name;mesh.SetVertices(vertices);var indices=new int[vertices.Count];for(int i=0;i<indices.Length;i++)indices[i]=i;
            mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
        static void Triangle(List<Vector3> v,Vector3 a,Vector3 b,Vector3 c){v.Add(a);v.Add(b);v.Add(c);}
        static Mesh GlandMesh()
        {
            var v=new List<Vector3>();float[] ys={-.16f,-.10f,0,.105f,.17f},rs={0,.078f,.105f,.075f,0};
            for(int row=0;row<4;row++)for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;
                var p=new Vector3(Mathf.Cos(a)*rs[row],ys[row],Mathf.Sin(a)*rs[row]);
                var q=new Vector3(Mathf.Cos(b)*rs[row],ys[row],Mathf.Sin(b)*rs[row]);
                var r=new Vector3(Mathf.Cos(a)*rs[row+1],ys[row+1],Mathf.Sin(a)*rs[row+1]);
                var s=new Vector3(Mathf.Cos(b)*rs[row+1],ys[row+1],Mathf.Sin(b)*rs[row+1]);
                if(rs[row]>0)Triangle(v,p,r,q);if(rs[row+1]>0)Triangle(v,q,r,s);
            }
            return Save("声腺核心",v);
        }
        static Mesh PlateMesh()
        {
            var v=new List<Vector3>();var p=new[]{new Vector3(0,-.17f,.06f),new Vector3(.075f,-.055f,.10f),new Vector3(0,.09f,.08f),new Vector3(-.075f,-.055f,.10f)};
            var ridge=new Vector3(0,-.04f,.16f);
            for(int i=0;i<4;i++){Triangle(v,p[i],p[(i+1)%4],ridge);Triangle(v,p[(i+1)%4],p[i],new Vector3(0,-.04f,.075f));}
            return Save("声腺甲壳瓣",v);
        }
        static Mesh RingMesh()
        {
            var v=new List<Vector3>();
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Triangle(v,p*.058f,p*.037f,q*.058f);Triangle(v,q*.058f,p*.037f,q*.037f);
            }
            return Save("声腺共鸣环",v);
        }
    }
}
