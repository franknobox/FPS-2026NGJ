using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NGJ.Combat;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NGJ.Progression.Editor
{
    public static class EvolutionVerification
    {
        static int checks;
        static void Require(bool value,string label){if(!value)throw new Exception(label);checks++;}
        [MenuItem("NGJ/成长/编辑模式核验")]
        public static string Run()
        {
            if(EditorApplication.isPlaying)throw new Exception("Only edit mode");checks=0;
            var config=AssetDatabase.LoadAssetAtPath<ProgressionSettings>(ProgressionSetup.SettingsPath);
            Require(config && config.options.Length==24,"完整强化配置");
            var state=new EvolutionState(config,43);state.AddBiomass(180);
            Require(state.Level==4&&state.Points==3&&state.Experience==30,"多级升级保留经验溢出");
            var ids=state.Offers(WeaponFamily.Sniper,false).Select(x=>x.id).ToArray();
            state.Offers(WeaponFamily.Rifle,false);Require(ids.SequenceEqual(state.Offers(WeaponFamily.Sniper,false).Select(x=>x.id)),"面板切换不刷新选项");
            Require(!state.Buy(WeaponFamily.Sniper,false,"不存在")&&state.Points==3,"拒绝非法扣点");
            Require(state.Offers(WeaponFamily.Player,false).Length==3,"玩家普通强化三选一");
            foreach(WeaponFamily family in Enum.GetValues(typeof(WeaponFamily)))
            {
                state=new EvolutionState(config,17);state.AddBiomass(10000);
                while(state.PendingSpecial(family)==0){var option=state.Offers(family,false)[0];int before=state.Points;Require(state.Buy(family,false,option.id)&&state.Points==before-1,"普通强化扣一进化点");}
                int points=state.Points;var options=state.Offers(family,true);
                Require(options.Length==(family==WeaponFamily.Player?1:2),"真实特殊池数量");
                string id=options[0].id;Require(state.Buy(family,true,id)&&state.Points==points,"特殊强化免费");
                Require(!state.Buy(family,true,id),"拒绝重复特殊强化");
            }
            var preview=EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/荒野前哨.unity");
            GameObject sample=null;WeaponDefinition a=null,b=null;
            try
            {
                var game=preview.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SlugGame>(true)).Single();
                var original=game.weaponDefinitions.ToArray();float baseDamage=original[2].targetNoise,baseCost=original[2].skill.cost;
                var run=game.gameObject.AddComponent<RunProgression>();game.progression=run;run.Initialize(game,config);run.BeginRun();
                foreach(var option in config.options)
                {
                    var family=Enum.GetValues(typeof(WeaponFamily)).Cast<WeaponFamily>().First(option.Supports);
                    foreach(var w in run.State.weapons)w.owned.Clear();run.State.weapons[(int)family].owned.Add(new UpgradeStack{id=option.id,count=1});run.State.AddBiomass(1);
                    if(family!=WeaponFamily.Player){var weapon=run.Weapon(family);Require(weapon!=null,"运行时武器存在");Require((weapon.evolution&option.effect)==option.effect,"特殊效果标记");}
                }
                Require(original[2].targetNoise==baseDamage&&original[2].skill.cost==baseCost,"强化不污染共享基础资源");
                run.BeginRun();Require(run.State.Level==1&&run.State.Points==0&&run.State.weapons.All(x=>x.owned.Count==0),"重开清空成长");
                Require(game.weaponDefinitions[2].targetNoise==baseDamage&&game.stats.baseNoise==0&&game.moveSpeedMultiplier==1,"重开恢复属性");
                sample=new GameObject("编辑模式受击核验");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sample,preview);
                var player=sample.AddComponent<PlayerCombatStats>();player.team=0;player.maxNoise=1000;player.deathAtNoiseLimit=false;
                var controller=sample.AddComponent<CombatWeaponController>();controller.owner=player;controller.hitMask=0;
                a=ScriptableObject.CreateInstance<WeaponDefinition>();b=ScriptableObject.CreateInstance<WeaponDefinition>();
                player.ApplyDamageBuff(1.5f,2,a,false);Require(player.DamageMultiplierFor(a)==1.5f&&player.DamageMultiplierFor(b)==1,"步枪增伤默认仅本武器");
                player.ApplyDamageBuff(1.5f,2,a,true);Require(player.DamageMultiplierFor(b)==1.5f,"共振频率跨武器生效");
                var targetObject=new GameObject("目标");targetObject.transform.SetParent(sample.transform);var target=targetObject.AddComponent<CombatTarget>();target.team=1;target.maxNoise=1000;target.deathAtNoiseLimit=false;
                target.ReceiveHit(new HitInfo{source=player,stunDuration=2,penetration=ArmorGrade.High});target.ReceiveHit(new HitInfo{source=player,noise=10,stunnedMultiplier=1.5f,penetration=ArmorGrade.High});
                Require(target.Noise==15,"刺耳回音对已眩晕敌人增伤");target.ResetCombat();
                target.ReceiveHit(new HitInfo{source=player,noise=10,directTarget=target,directMultiplier=1.5f,penetration=ArmorGrade.High});Require(target.Noise==15,"钻声弹头直击加成");
                a.evolution=EvolutionEffect.BioAmplify;var hit=controller.DecorateHit(new HitInfo{source=player,noise=10,penetration=ArmorGrade.High},a);
                target.ReceiveHit(hit);target.ReceiveHit(hit);
                var stacks=(Dictionary<WeaponDefinition,int>)typeof(CombatWeaponController).GetField("nextShotStacks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
                Require(stacks[a]==1,"同一次攻击对同一敌人仅叠一层生体增幅");
                controller.equipped=a;typeof(CombatWeaponController).GetMethod("FireOne",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[]{a});Require(stacks[a]==0,"下一次射击消耗生体增幅");
                Require(game.progressionSettings==config,"正式关卡已绑定成长配置");
            }
            finally{if(sample)Object.DestroyImmediate(sample);if(a)Object.DestroyImmediate(a);if(b)Object.DestroyImmediate(b);EditorSceneManager.ClosePreviewScene(preview);}
            string result=$"EVOLUTION_EDITOR_CHECKS_PASS {checks}; PlayMode={EditorApplication.isPlaying}";
            System.IO.Directory.CreateDirectory(".utmp/progression");System.IO.File.WriteAllText(".utmp/progression/verification.txt",result);return result;
        }
    }
}
