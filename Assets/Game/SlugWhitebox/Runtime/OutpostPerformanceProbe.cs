using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlugWhitebox
{
    // 仅 --outpost-perf 启用：固定分辨率、关闭限帧，预热后采样实际渲染帧间隔。
    public sealed class OutpostPerformanceProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), x => x == "--outpost-perf")) return;
            var host = new GameObject("性能采样（仅命令行）");
            DontDestroyOnLoad(host); host.AddComponent<OutpostPerformanceProbe>();
            if (SceneManager.GetActiveScene().name != ClientFlow.ClientUI.LevelScene)
                SceneManager.LoadScene(ClientFlow.ClientUI.LevelScene);
        }

        IEnumerator Start()
        {
            SlugGame game;
            do { yield return null; game = FindFirstObjectByType<SlugGame>(); }
            while (!game || !game.HasStarted);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Screen.SetResolution(1280, 720, false);
            game.stats.deathAtNoiseLimit = false;
            yield return new WaitForSecondsRealtime(5);
            var times = new List<double>(4096);
            double start = Time.realtimeSinceStartupAsDouble, previous = start;
            while (Time.realtimeSinceStartupAsDouble - start < 12)
            {
                // 固定地点匀速转身，包含世界渲染、敌人逻辑和雷达更新。
                game.player.rotation = Quaternion.Euler(0, (float)(Time.realtimeSinceStartupAsDouble-start)*12, 0);
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                times.Add((now-previous)*1000); previous = now;
            }
            times.Sort();
            string report = $"1280x720; uncapped; rotating at level spawn; frames={times.Count}; " +
                $"median_ms={times[times.Count/2]:F2}; p95_ms={times[(int)(times.Count*.95)]:F2}; " +
                $"average_fps={times.Count/(previous-start):F1}; running={game.Running}";
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"..","VFXPreview"));
            Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,"performance.txt"), report);
            Debug.Log("OUTPOST_PERF " + report); Application.Quit();
        }
    }
}
