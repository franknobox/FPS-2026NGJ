using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NGJ.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlugWhitebox
{
    // 仅命令行验收启用；普通启动仍进入菜单，战斗数值不受影响。
    public sealed class OutpostVfxPreview : MonoBehaviour
    {
        static bool Has(string flag) => Array.Exists(Environment.GetCommandLineArgs(), a => a == flag);
        string folder;
        readonly List<string> checks = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Has("--vfx-preview") && !Has("--slug-smoke")) return;
            if (!Has("--vfx-preview") && SceneManager.GetActiveScene().name != ClientFlow.ClientUI.LevelScene)
                SceneManager.LoadScene(ClientFlow.ClientUI.LevelScene);
            if (Has("--vfx-preview"))
            {
                var host = new GameObject("特效验收（仅命令行）");
                DontDestroyOnLoad(host); host.AddComponent<OutpostVfxPreview>();
            }
        }
        IEnumerator Start()
        {
            folder = Path.GetFullPath(Path.Combine(Application.dataPath,"..","VFXPreview"));
            Directory.CreateDirectory(folder);
            // 使用真实菜单按钮进入，验证正常启动流程，不绕过开始界面。
            yield return null;
            if(SceneManager.GetActiveScene().name != ClientFlow.ClientUI.MenuScene)
                throw new Exception("启动场景不是主菜单");
            Click("开始游戏"); yield return null;
            Click("进入荒野");
            SlugGame g;
            do { yield return null; g = FindFirstObjectByType<SlugGame>(); } while (!g || !g.HasStarted);
            var scenario = Scenario(g);
            while (true)
            {
                object step = null; bool next;
                try { next = scenario.MoveNext(); if (next) step = scenario.Current; }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(folder,"verification.txt"),"FAIL\n"+string.Join("\n",checks)+"\n"+e);
                    Debug.LogException(e); Application.Quit(1); yield break;
                }
                if (!next) break; yield return step;
            }
            File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS\n"+string.Join("\n",checks));
            Debug.Log("OUTPOST_VFX_RUNTIME_PASS"); Application.Quit(0);
        }
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            checks.Add(message); Debug.Log("VFX_CHECK_OK " + message);
        }
        void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        void Click(string caption)
        {
            foreach(var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if(button.GetComponentInChildren<UnityEngine.UI.Text>().text==caption) { button.onClick.Invoke(); checks.Add("真实菜单按钮："+caption); return; }
            throw new Exception("缺少菜单按钮："+caption);
        }
        IEnumerator Scenario(SlugGame g)
        {
            yield return new WaitForSeconds(2);
            Check(g.Running && g.levelFlow,"实际荒野前哨关卡正常启动");
            g.Pause();
            yield return new WaitForSecondsRealtime(.3f);
            Capture("pause-layout");
            yield return new WaitForEndOfFrame();
            Click("设置");
            yield return new WaitForSecondsRealtime(.3f);
            Capture("settings-layout");
            yield return new WaitForEndOfFrame();
            Click("返回");
            Click("继续任务");
            yield return null;
            // 用实际窗口尺寸验证底部文字，而不是只检查设计坐标。
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.5f);
                Canvas.ForceUpdateCanvases();
                var hud=FindFirstObjectByType<SlugHud>();
                var corners=new Vector3[4];
                foreach(var text in hud.GetComponentsInChildren<UnityEngine.UI.Text>())
                {
                    if(!text.transform.IsChildOf(hud.transform.Find("战斗HUD/底部战斗信息")))continue;
                    text.rectTransform.GetWorldCorners(corners);
                    foreach(var corner in corners)
                    {
                        var screen=RectTransformUtility.WorldToScreenPoint(null,corner);
                        Check(screen.x>=0 && screen.x<=Screen.width && screen.y>=8 && screen.y<=Screen.height,
                            $"HUD {Screen.width}x{Screen.height} {text.name} 边界可见");
                    }
                    if(text.name=="轮盘按键提示")Check(text.preferredHeight<=text.rectTransform.rect.height,"操作提示两行完整容纳");
                }
                Capture($"hud-layout-{Screen.width}x{Screen.height}");
                yield return new WaitForEndOfFrame();
            }
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.5f);
            // 在隔离位置用真实物理碰撞验证：挥空不提示、接受命中才提示、同目标不重复。
            var dummy=new GameObject("命中反馈验收目标"); dummy.layer=CombatLayers.Enemy;
            dummy.transform.position=new Vector3(1000,1000,1000);
            dummy.AddComponent<SphereCollider>().radius=.5f;
            var target=dummy.AddComponent<CombatTarget>(); target.team=1; target.maxNoise=1000;
            int received=0;
            target.HitReceived+=hit=>{received++;g.HitFeedback(hit,!target.IsAlive);};
            Physics.SyncTransforms(); g.hitUntil=0;
            var hitData=new HitInfo {source=g.stats,noise=1,penetration=ArmorGrade.High,skillName="验收技能"};
            var missed=new GameObject("挥空验收").AddComponent<CombatHitbox>();
            missed.transform.position=dummy.transform.position+Vector3.right*10; missed.radius=1; missed.Initialize(hitData);
            Check(received==0 && g.hitUntil==0,"技能挥空不出现命中提示");
            var landed=Instantiate(g.weaponDefinitions[2].skill.hitboxPrefab,dummy.transform.position,Quaternion.identity);
            landed.obstructionMask=0; landed.speed=0; landed.Initialize(hitData);
            Check(received==1 && g.hitUntil>Time.time && g.hitLabel.Contains("验收技能"),"真实技能碰撞触发命中标记和技能名");
            Check(landed.impactEffect && FindObjectsByType<CombatVfxMotion>(FindObjectsSortMode.None).Length>0,"技能命中生成接触特效");
            yield return new WaitForFixedUpdate();
            Check(received==1,"同一技能不会重复结算同一目标");
            target.armor=ArmorGrade.High; hitData.penetration=ArmorGrade.Low;
            target.ReceiveHit(hitData);
            Check(g.hitLabel.Contains("装甲减伤"),"装甲减伤反馈准确");
            target.armor=ArmorGrade.Low; target.maxNoise=target.Noise+1; hitData.noise=2;
            target.ReceiveHit(hitData);
            Check(g.hitLabel.Contains("击杀"),"致死命中显示击杀");
            Destroy(dummy); Destroy(landed.gameObject); Destroy(missed.gameObject);
            g.stats.deathAtNoiseLimit = false;
            var radar = FindFirstObjectByType<UIMinimap>();
            Check(radar != null, "左上角敌情雷达已绑定");
            var probe = new GameObject("雷达验收敌人").AddComponent<SlugEnemy>();
            probe.enabled = false; g.enemies.Add(probe);
            probe.transform.position = g.PlayerPosition + new Vector3(8,0,10);
            Check(radar.TryGetEnemyMarker(probe,out var near,out bool edge) && !edge && near.x>0 && near.y>0, "范围内敌人显示正确方位红点");
            probe.transform.position = g.PlayerPosition + new Vector3(-80,0,60);
            Check(radar.TryGetEnemyMarker(probe,out var far,out edge) && edge && Mathf.Abs(far.magnitude-1)<.001f && far.x<0, "超出量程敌人固定在圆形边缘");
            probe.dead = true;
            Check(!radar.TryGetEnemyMarker(probe,out _,out _), "死亡敌人移除标记");
            probe.dead = false; probe.gameObject.SetActive(false);
            Check(!radar.TryGetEnemyMarker(probe,out _,out _), "隐藏敌人移除标记");
            probe.gameObject.SetActive(true);
            var nearProbe = new GameObject("雷达近敌验收").AddComponent<SlugEnemy>();
            nearProbe.enabled=false; nearProbe.transform.position=g.PlayerPosition+new Vector3(12,0,14);g.enemies.Add(nearProbe);
            yield return new WaitForEndOfFrame(); Capture("radar-enemies");
            yield return null;
            g.enemies.Remove(probe);g.enemies.Remove(nearProbe);Destroy(probe.gameObject);Destroy(nearProbe.gameObject);
            var restPosition=g.view.transform.localPosition;
            var restRotation=g.view.transform.localRotation;
            g.cameraFeedback.Impulse(1,.4f);
            yield return new WaitForEndOfFrame();
            Check(g.cameraFeedback.IsShaking && (g.view.transform.localPosition-restPosition).sqrMagnitude>0.0000001f, "镜头实际产生震动位移");
            yield return new WaitForSeconds(.6f);
            Check(!g.cameraFeedback.IsShaking && Vector3.Distance(g.view.transform.localPosition,restPosition)<.001f && Quaternion.Angle(g.view.transform.localRotation,restRotation)<.02f, "震动结束恢复原始镜头，无累计漂移");
            g.cameraFeedback.Impulse(1);g.Pause();
            yield return new WaitForEndOfFrame();
            Check(!g.cameraFeedback.IsShaking, "暂停立即清除震动");g.Resume();
            g.AimAt(g.PlayerPosition + new Vector3(0,1,15));
            Capture("00-level");
            for (int i=0;i<5;i++)
            {
                g.ConfigureLoadout(i); g.SelectWeapon(i); g.weapons.ResetWeapons();
                g.stats.TryConsumeNoise(g.stats.Noise);
                yield return new WaitForSeconds(.3f);
                Check(g.Fire(),"武器 "+i+" 真实开火");
                Check(g.cameraFeedback.IsShaking,"武器 "+i+" 触发开火震动");
                yield return new WaitForSeconds(.025f);
                Check(FindObjectsByType<CombatVfxMotion>(FindObjectsSortMode.None).Length>0,"武器 "+i+" 生成枪口特效");
                yield return new WaitForEndOfFrame(); Capture("weapon-"+i);
                yield return new WaitForSeconds(.8f);
                if (g.weaponDefinitions[i].skill)
                {
                    g.runner.ResetSkills(); g.stats.AddNoise(90);
                    Check(g.CastWeaponSkill(),"武器 "+i+" 真实技能");
                    yield return new WaitForSeconds(.09f);
                    yield return new WaitForEndOfFrame(); Capture("weapon-skill-"+i);
                    yield return new WaitForSeconds(3);
                    Check(g.runner.transform.GetComponentsInChildren<ParticleSystem>().Length==0,"武器技能 "+i+" 持续粒子已清理");
                }
                yield return new WaitForSeconds(1);
            }
            // 对地实射，验收真正的弹体碰撞和爆炸，而非单独播放展示特效。
            foreach(int index in new[]{3,4})
            {
                g.ConfigureLoadout(index); g.SelectWeapon(index); g.weapons.ResetWeapons();
                g.stats.TryConsumeNoise(g.stats.Noise);
                g.AimAt(g.PlayerPosition + new Vector3(0,-.1f,7));
                Check(g.Fire(),"爆炸武器 "+index+" 对地实射");
                float deadline=Time.time+7; bool exploded=false;
                while(Time.time<deadline)
                {
                    foreach(var fx in FindObjectsByType<CombatVfxMotion>(FindObjectsSortMode.None))
                        if(fx.name.Contains("Explosion")) { exploded=true; break; }
                    if(exploded) break;
                    yield return null;
                }
                Check(exploded,"爆炸武器 "+index+" 碰撞生成爆炸特效");
                yield return new WaitForSeconds(.1f);
                int visibleParticles=0;
                foreach(var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) visibleParticles+=ps.particleCount;
                Check(visibleParticles>0,"爆炸粒子实际发射 "+visibleParticles);
                yield return new WaitForEndOfFrame(); Capture("explosion-"+index);
                yield return new WaitForSeconds(2);
            }
            g.AimAt(g.PlayerPosition + new Vector3(0,1,15));
            for (int i=0;i<3;i++)
            {
                g.runner.ResetSkills(); g.stats.TryConsumeNoise(g.stats.Noise); g.stats.AddGland(2); g.skill=i;
                Check(g.CastSkill(),"声腺技能 "+i+" 真实释放");
                yield return new WaitForSeconds(.13f);
                yield return new WaitForEndOfFrame(); Capture("gland-"+i);
                if(i==2)
                {
                    yield return new WaitForSeconds(2);
                    yield return new WaitForEndOfFrame(); Capture("gland-2-counter");
                }
                yield return new WaitForSeconds(3);
            }
            g.runner.ResetSkills(); g.stats.AddGland(2); g.skill=2;
            Check(g.CastSkill(),"共鸣取消测试启动");
            g.BeginRun(); yield return null; yield return null;
            Check(!g.stats.IsAbsorbing && g.runner.transform.GetComponentsInChildren<ParticleSystem>().Length==0,"重开清理持续共鸣粒子");
            yield return new WaitForSeconds(3);
            Check(FindObjectsByType<CombatVfxMotion>(FindObjectsSortMode.None).Length==0,"瞬时特效全部到期清理");
        }
    }
}
