using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NGJ.Combat;
using UnityEngine;
namespace SlugWhitebox
{
    // 仅 --slug-smoke 启用。射击测试临时移除玩家噪值，普通试玩不受影响。
    public static class SlugSmoke
    {
        static readonly List<string> checks=new List<string>();
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Verification"));
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);Debug.Log("SLUG_CHECK_OK "+name);}
        public static IEnumerator Run(SlugGame game)
        {
            Directory.CreateDirectory(Folder);var scenario=Scenario(game);
            while(true)
            {
                object current=null;bool next;
                try{next=scenario.MoveNext();if(next)current=scenario.Current;}
                catch(Exception error){File.WriteAllText(Path.Combine(Folder,"combat-smoke.txt"),"FAIL\n"+string.Join("\n",checks)+"\n"+error);Debug.LogException(error);Application.Quit(1);yield break;}
                if(!next)break;yield return current;
            }
            File.WriteAllText(Path.Combine(Folder,"combat-smoke.txt"),"PASS\n"+string.Join("\n",checks));Application.Quit(0);
        }
        static IEnumerator Scenario(SlugGame g)
        {
            yield return null;
            Check(g.Running && g.stats.team==0,"启动、玩家阵营和新组件接线");
            Check(g.weaponDefinitions.Length==4 && g.glandSkills.Length==3 && g.enemyPrefabs.Length==3,"4武器、7技能、3敌人配置齐全");
            for(int i=0;i<4;i++)
            {
                g.SelectWeapon(i);g.AimAt(g.PlayerPosition+Vector3.forward*15+Vector3.up*3);
                Check(g.Fire(),"武器 "+i+" 真实普攻");g.stats.AddNoise(50);Check(g.CastWeaponSkill(),"武器 "+i+" 噪值技能");
                g.stats.TryConsumeNoise(g.stats.Noise);yield return new WaitForSeconds(.2f);
            }
            yield return new WaitForSeconds(1);
            for(int i=0;i<3;i++){g.runner.ResetSkills();g.stats.AddGland(2);g.skill=i;Check(g.CastSkill(),"声腺技能 "+i);yield return new WaitForSeconds(2.5f);}
            g.BeginRun();yield return null;
            g.stats.deathAtNoiseLimit=false;
            int total=0;
            for(int sector=0;sector<3;sector++)
            {
                g.ActivateSector(sector);total+=g.tuning.sectorCounts[sector];yield return new WaitForSeconds(.5f);
                if(sector==0)
                {
                    g.Warp(new Vector3(0,.15f,5));g.AimAt(new Vector3(0,3,18));
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"combat-whitebox.png"));
                }
                float deadline=Time.realtimeSinceStartup+90;
                int attempts=0;
                while(g.Remaining>0 && Time.realtimeSinceStartup<deadline)
                {
                    var e=g.enemies.Find(enemy=>enemy && !enemy.dead);if(!e)break;
                    // 移到目标近旁，测试真实射线命中；路径移动由独立 NavMesh 检查覆盖。
                    Vector3 spot=e.transform.position-e.transform.forward*3;spot.y=.15f;
                    if(UnityEngine.AI.NavMesh.SamplePosition(spot,out var sample,5,UnityEngine.AI.NavMesh.AllAreas))g.Warp(sample.position+Vector3.up*.1f);
                    g.SelectWeapon(1);g.AimAt(e.AimPoint);g.stats.TryConsumeNoise(g.stats.Noise);g.Fire();
                    if(++attempts%10==0)Debug.Log($"SLUG_PROGRESS sector={sector} remaining={g.Remaining} target={e.kind} hp={e.hp} mode={g.mode}");
                    yield return new WaitForSecondsRealtime(.95f);
                }
                Check(g.Remaining==0,"区域 "+sector+" 真实射击清场");
            }
            Check(g.kills==total,"全部敌人死亡、计数和掉落事件");
            int biomassBefore=g.runBiomass;g.Collect(1,10);Check(g.stats.Glands>0 && g.runBiomass==biomassBefore+10,"拾取接入声腺和生物质");
            g.Warp(g.exit.position);yield return null;yield return null;Check(g.mode==SlugGame.Mode.Result,"EXIT 通关结算");
            g.BeginRun();yield return null;Check(g.kills==0 && g.stats.Noise==0 && g.stats.Glands==2,"重开清理关卡与资源");
            g.stats.deathAtNoiseLimit=true;g.Hurt(100);Check(g.mode==SlugGame.Mode.Result,"噪值过载死亡");
            g.BeginRun();yield return null;Check(g.Running && g.stats.IsAlive,"死亡后可重开");
        }
    }
}
