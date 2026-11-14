using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace SlugWhitebox
{
    // Opt-in executable smoke test. Exercises the real combat and movement methods without player saves.
    public static class SlugSmoke
    {
        [Serializable] class Report { public bool passed; public string[] checks; public int kills; public int biomass; public string note; }
        static readonly List<string> checks=new List<string>();
        static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Verification"));
        static void Check(bool condition,string name)
        {
            if(!condition)throw new Exception("SLUG_SMOKE_FAILED: "+name);
            checks.Add(name);Debug.Log("SLUG_CHECK_OK "+name);
        }

        public static IEnumerator Run(SlugGame game)
        {
            Directory.CreateDirectory(Folder);
            // Coroutine exception handling is delegated to a wrapper so failures emit a report and exit.
            var routine=Scenario(game);
            while(true)
            {
                object current=null;bool next=false;
                try{next=routine.MoveNext();if(next)current=routine.Current;}
                catch(Exception error)
                {
                    Debug.LogException(error);
                    File.WriteAllText(Path.Combine(Folder,"smoke-report.json"),JsonUtility.ToJson(new Report{passed=false,checks=checks.ToArray(),note=error.ToString()},true));
                    Application.Quit(1);yield break;
                }
                if(!next)break;
                yield return current;
            }
        }

        static IEnumerator Scenario(SlugGame g)
        {
            yield return null;
            Check(g.Running,"启动直接进入试玩，无开始菜单");
            yield return new WaitForSeconds(.5f);
            Check(Array.TrueForAll(g.fireSounds,c=>c&&c.samples>0)&&g.pickupSound&&g.hitSound&&g.killSound,"四种武器及拾取命中音效可用");
            Check(Array.TrueForAll(g.weaponModels,m=>m&&Array.Exists(m.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.sharedMesh&&r.sharedMesh.vertexCount>1000)),"四种武器使用素材包真实网格");
            for(int i=0;i<4;i++)
            {
                g.SelectWeapon(i);yield return new WaitForSeconds(.15f);
                Check(Array.FindAll(g.weaponModels,m=>m.activeSelf).Length==1,"换枪只显示当前武器 "+(i+1));
                yield return Capture("weapon-live-"+(i+1)+".png");
                g.Fire(true);yield return new WaitForSeconds(.06f);
                Check(g.weaponModels[i].GetComponent<SlugWeaponView>().PlayingShot,"素材包开火或挥刀动画播放 "+(i+1));
                yield return Capture("weapon-fire-"+(i+1)+".png");
            }
            g.BeginRun();yield return null;
            var labels=g.hud.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            Check(labels.Length==1&&g.hud.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length==0
                &&g.hud.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==0,"画面只有一个计数器，无面板、进度条、按钮");
            Check(g.maxGlands==g.tuning.glandMax && g.glands==g.maxGlands,"声腺初始化与上限");
            g.skill=0;float before=g.glands;Check(g.CastSkill(),"技能可施放");
            Check(Mathf.Approximately(g.glands,before-g.tuning.skillCosts[0]),"技能真实扣除声腺");
            g.hud.Refresh();Check(g.hud.counter.text=="声腺：75 / 100","计数器显示真实技能扣费后的数值");
            float after=g.glands;g.TickResources(1);Check(g.glands>after,"战斗中缓慢回复");
            g.glands=0;Check(!g.CastSkill() && g.glands==0,"声腺不足拒绝施放且不扣费");
            g.glands=g.maxGlands-1;g.Collect(18,10);
            Check(g.glands==g.maxGlands && g.runBiomass==10,"声腺拾取封顶且溢出不转生物质");
            var enemyObject=new GameObject("验证目标");enemyObject.transform.position=new Vector3(0,0,7);
            var target=enemyObject.AddComponent<SlugEnemy>();target.Initialize(g,0);g.enemies.Add(target);
            g.AimAt(target.AimPoint);g.glands=g.maxGlands;g.skill=0;g.CastSkill();
            float first=target.vulnerableUntil;target.vulnerableUntil=Time.time+.1f;g.skill=1;g.CastSkill();
            Check(target.VulnerableLeft>0&&target.SlowLeft>0&&target.StaggerLeft>0,"易伤/减速/硬直标签共存");
            g.glands=g.maxGlands;g.skill=0;g.CastSkill();Check(target.vulnerableUntil>=first,"同类标签刷新时长");
            yield return new WaitForSeconds(.95f);
            Check(target.StaggerLeft==0&&target.SlowLeft>0&&target.VulnerableLeft>0,"标签按各自时间到期");
            // Actual raycast weapon fire -> damage -> death -> physical drop -> magnetic pickup.
            g.AimAt(target.AimPoint);g.SelectWeapon(0);g.Fire(true);g.Fire(true);g.Fire(true);
            yield return null;
            Check(!target||target.dead,"真实射线武器命中易伤目标并击杀");
            Check(g.drops.Count==1,"击杀生成声腺与生物质掉落");
            var drop=g.drops[0];g.glands=40;g.Warp(drop.body.position+Vector3.back*2-Vector3.up*.4f);
            yield return new WaitForSeconds(.65f);
            Check(g.runBiomass==20&&g.drops.Count==0&&g.glands>58,"吸附拾取实际增加声腺和生物质");
            g.AimAt(g.view.transform.position+Vector3.up*30);g.SelectWeapon(0);g.heat[0]=0;
            for(int i=0;i<10;i++)g.Fire(true);
            Check(g.locked[0]&&!g.Fire(true),"武器到达100热量锁定开火");
            g.SelectWeapon(1);Check(g.Fire(true)&&g.heat[1]>0,"过热可切枪且热量独立");
            g.TickResources(3.1f);Check(!g.locked[0]&&g.heat[0]<=g.tuning.overheatRecovery,"后台武器冷却到阈值解锁");
            g.SelectWeapon(3);g.Fire(true);Check(g.heat[3]==0,"近战不积累热量");
            g.SpawnSpit(g.PlayerPosition+Vector3.up*5,g.PlayerPosition+Vector3.forward*20);
            g.EndRun("验证结束");g.BeginRun();
            Check(g.maxGlands==g.tuning.glandMax&&g.glands==g.maxGlands&&g.kills==0,"重新试玩重置声腺、热量与击杀");
            yield return null;yield return null;
            Check(!GameObject.Find("酸液弹"),"上一局酸液弹不会带入下一局");
            // Full run: follow the baked path, aim and fire using the real combat methods.
            float deadline=Time.realtimeSinceStartup+150;
            bool screenshot=false;
            g.Warp(new Vector3(0,.1f,3.5f));yield return null;
            while(g.Running&&Time.realtimeSinceStartup<deadline)
            {
                if(g.Remaining>0)
                {
                    SlugEnemy closest=null;float best=float.MaxValue;
                    foreach(var e in g.enemies)
                    {
                        if(!e||e.dead||!g.ClearLine(g.view.transform.position,e.AimPoint))continue;
                        float distance=Vector3.Distance(e.transform.position,g.PlayerPosition);
                        if(distance<best){best=distance;closest=e;}
                    }
                    if(closest)
                    {
                        g.AimAt(closest.AimPoint);g.SelectWeapon(g.locked[0]?1:0);
                        if(closest.VulnerableLeft<=0&&g.glands>=g.tuning.skillCosts[0]){g.skill=0;g.CastSkill();}
                        g.Fire();
                        if(!screenshot && g.kills>=2)
                        {
                            screenshot=true;
                            yield return Capture("whitebox-combat.png");
                        }
                    }
                    else MoveToward(g,new Vector3(0,0,g.sector*30+13));
                    // Scripted combat verification maintains health to cover all three sectors deterministically.
                    g.health=g.maxHealth;
                }
                else
                {
                    if(g.drops.Count>0)MoveToward(g,g.drops[0].body.position);
                    else MoveToward(g,g.sector<2?new Vector3(0,0,(g.sector+1)*30+4):g.exit.position);
                }
                yield return null;
            }
            int total=0;foreach(int n in g.tuning.sectorCounts)total+=n;
            Check(g.mode==SlugGame.Mode.Result&&g.kills==total,"三片区域真实射击清场并到达终点");
            Check(g.runBiomass==total*g.tuning.biomassDrop,"每种敌人掉落一致且可全数回收");
            yield return Capture("whitebox-exit.png");
            File.WriteAllText(Path.Combine(Folder,"smoke-report.json"),JsonUtility.ToJson(new Report{passed=true,checks=checks.ToArray(),kills=g.kills,biomass=g.runBiomass,note="脚本直接调用实际技能/开火/角色移动方法。完整流程验证阶段持续恢复玩家生命，不评价难度或手动鼠标手感；隔离玩家存档。"},true));
            Debug.Log("SLUG_SMOKE_OK checks="+checks.Count);Application.Quit(0);
        }

        static void MoveToward(SlugGame g,Vector3 destination)
        {
            var path=new NavMeshPath();Vector3 flat=g.PlayerPosition;flat.y=0;
            if(!NavMesh.CalculatePath(flat,destination,NavMesh.AllAreas,path)||path.corners.Length<2)return;
            Vector3 target=path.corners[1];Vector3 delta=target-g.PlayerPosition;delta.y=0;
            g.player.GetComponent<CharacterController>().Move(delta.normalized*g.tuning.moveSpeed*Time.deltaTime);
            if(delta.sqrMagnitude>.1f)g.AimAt(g.view.transform.position+delta.normalized*10);
        }
        static IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(!texture)throw new Exception("没有渲染帧，不能验证截图");
            File.WriteAllBytes(Path.Combine(Folder,filename),texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
        }
    }
}
