using UnityEngine;
using System.Linq;
namespace ClientFlow
{
    public static class ClientSettings
    {
        const string VolumeKey="Client.MasterVolume", SensitivityKey="Client.MouseSensitivity", FullscreenKey="Client.Fullscreen", WidthKey="Client.Width", HeightKey="Client.Height";
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));
        public static float Sensitivity => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey,1),.25f,2);
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey,Screen.fullScreen?1:0)==1;
        public static void SetVolume(float value){PlayerPrefs.SetFloat(VolumeKey,Mathf.Clamp01(value));AudioListener.volume=Volume;PlayerPrefs.Save();}
        public static void SetSensitivity(float value){PlayerPrefs.SetFloat(SensitivityKey,Mathf.Clamp(value,.25f,2));PlayerPrefs.Save();}
        public static void SetFullscreen(bool value){PlayerPrefs.SetInt(FullscreenKey,value?1:0);Screen.fullScreen=value;PlayerPrefs.Save();}
        public static Vector2Int[] Resolutions()=>Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
        public static Vector2Int Resolution=>new Vector2Int(PlayerPrefs.GetInt(WidthKey,Screen.width),PlayerPrefs.GetInt(HeightKey,Screen.height));
        public static void NextResolution()
        {
            var options=Resolutions();if(options.Length==0)return;var current=Resolution;int index=System.Array.FindIndex(options,r=>r==current);
            var next=options[(index+1)%options.Length];PlayerPrefs.SetInt(WidthKey,next.x);PlayerPrefs.SetInt(HeightKey,next.y);Screen.SetResolution(next.x,next.y,Screen.fullScreenMode);PlayerPrefs.Save();
        }
        public static void Apply()
        {
            AudioListener.volume=Volume;if(PlayerPrefs.HasKey(FullscreenKey))Screen.fullScreen=Fullscreen;
            if(PlayerPrefs.HasKey(WidthKey)&&PlayerPrefs.HasKey(HeightKey)){var resolution=Resolution;if(Resolutions().Contains(resolution))Screen.SetResolution(resolution.x,resolution.y,Screen.fullScreenMode);}
        }
    }
}
