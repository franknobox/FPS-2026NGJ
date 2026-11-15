using UnityEditor;
using UnityEditor.SceneManagement;

namespace NGJ.EditorTools
{
    // 从任何关卡点击 Play 都先进入菜单，停止后仍返回原来的编辑场景。
    [InitializeOnLoad]
    public static class OutpostPlayEntry
    {
        const string Menu = "Assets/Game/Client/Scenes/客户端菜单.unity";
        static OutpostPlayEntry() { EditorApplication.delayCall += Configure; }
        public static void Configure()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Menu);
        }
    }
}
