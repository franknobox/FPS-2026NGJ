using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NGJ.Combat;
namespace ClientFlow.Editor
{
    public static class MainMenuArtSetup
    {
        const string Folder="Assets/ART/UI/Menu/";
        [MenuItem("循声虫族/客户端/应用主菜单美术")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("请先保存当前场景");
            string original=EditorSceneManager.GetActiveScene().path;
            var scene=EditorSceneManager.OpenScene(ClientSceneSetup.MenuPath);
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenu>();Bind(menu);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        }
        public static void Bind(MainMenu menu)
        {
            menu.backgroundArt=Import("荒野前哨_主菜单背景.jpg",false);
            menu.titleArt=Import("荒野前哨_标题.png",true);
            EditorUtility.SetDirty(menu);
        }
        static Sprite Import(string name,bool alpha)
        {
            string path=Folder+name;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("Missing menu art: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.sRGBTexture=true;
            importer.alphaIsTransparency=alpha;importer.alphaSource=alpha?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=alpha?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
