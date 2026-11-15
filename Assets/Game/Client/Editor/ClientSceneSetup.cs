using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NGJ.Combat;
namespace ClientFlow.Editor
{
    public static class ClientSceneSetup
    {
        public const string MenuPath="Assets/Game/Client/Scenes/客户端菜单.unity";
        [MenuItem("循声虫族/客户端/创建菜单场景")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("请先停止运行");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if(!AssetDatabase.IsValidFolder("Assets/Game/Client/Scenes"))AssetDatabase.CreateFolder("Assets/Game/Client","Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("菜单相机",typeof(Camera),typeof(AudioListener));camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color(.1f,.1f,.09f);
            var menu=new GameObject("客户端主菜单").AddComponent<MainMenu>();
            if(System.IO.File.Exists("Assets/ART/UI/Menu/荒野前哨_标题.png"))MainMenuArtSetup.Bind(menu);
            EditorSceneManager.SaveScene(scene,MenuPath);
            string level=Wasteland.Editor.WastelandLevelBuilder.ScenePath;
            var rest=EditorBuildSettings.scenes.Where(s=>s.path!=MenuPath&&s.path!=level).ToArray();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MenuPath,true),new EditorBuildSettingsScene(level,true)}.Concat(rest).ToArray();
            AssetDatabase.SaveAssets();
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        }
        [MenuItem("循声虫族/客户端/打开菜单场景")]
        public static void Open(){EditorSceneManager.OpenScene(MenuPath);}
    }
}
