using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NGJ.Combat;
using SlugWhitebox;
using UnityEngine;
namespace NGJ.Progression
{
    public sealed class RunProgression : MonoBehaviour
    {
        public EvolutionState State{get;private set;}
        public bool IntermissionPending{get;private set;}
        public ProgressionSettings Settings{get;private set;}
        public event Action Changed;
        SlugGame game;
        WeaponDefinition[] originals,runtime;
        WeaponBaseline[] baselines;
        float maxNoise,maxGlands,regen,decay;
        sealed class WeaponBaseline
        {
            public float damage,noise,interval,cost,cooldown;
            public WeaponBaseline(WeaponDefinition w){damage=w.targetNoise;noise=w.selfNoise;interval=w.fireInterval;cost=w.skill?w.skill.cost:0;cooldown=w.skill?w.skill.cooldown:0;}
            public void Reset(WeaponDefinition w)
            {
                w.targetNoise=damage;w.selfNoise=noise;w.fireInterval=interval;w.evolution=0;
                if(w.skill){w.skill.cost=cost;w.skill.cooldown=cooldown;w.skill.extraTriggers=0;w.skill.rangeMultiplier=1;}
            }
        }
        public void Initialize(SlugGame owner,ProgressionSettings settings)
        {
            game=owner;Settings=settings;if(!Settings){Settings=ScriptableObject.CreateInstance<ProgressionSettings>();Settings.hideFlags=HideFlags.DontSave;}
            originals=game.weaponDefinitions;runtime=new WeaponDefinition[originals.Length];baselines=new WeaponBaseline[originals.Length];
            for(int i=0;i<originals.Length;i++)
            {
                if(!originals[i])continue;var w=Instantiate(originals[i]);w.name=originals[i].name+"_局内";w.hideFlags=HideFlags.DontSave;
                if(w.skill){w.skill=Instantiate(w.skill);w.skill.hideFlags=HideFlags.DontSave;w.skill.sourceWeapon=w;}
                runtime[i]=w;baselines[i]=new WeaponBaseline(w);
            }
            game.weaponDefinitions=runtime;
            maxNoise=game.stats.maxNoise;maxGlands=game.stats.maxGlands;regen=game.stats.glandRegenPerSecond;decay=game.stats.noiseDecayPerSecond;
        }
        public void BeginRun()
        {
            CancelPending();if(State!=null)State.Changed-=UpdateBuild;
            State=new EvolutionState(Settings,Environment.TickCount^GetInstanceID());State.Changed+=UpdateBuild;UpdateBuild();
        }
        public static WeaponFamily Family(WeaponDefinition weapon)
        {
            return weapon.type==WeaponType.JianXiaoSniper?WeaponFamily.Sniper:weapon.type==WeaponType.SiMingRifle?WeaponFamily.Rifle:
                weapon.type==WeaponType.HanYinShotgun?WeaponFamily.Shotgun:WeaponFamily.Explosive;
        }
        public WeaponDefinition Weapon(WeaponFamily family)=>runtime?.FirstOrDefault(w=>w&&Family(w)==family&&(family!=WeaponFamily.Explosive||w.type==WeaponType.RocketLauncher));
        public void AddBiomass(int amount)
        {
            int gained=State.AddBiomass(amount);if(gained>0)game.Message($"Lv.{State.Level} · 进化点 +{gained*Settings.pointsPerLevel}",2.5f);
        }
        void UpdateBuild()
        {
            if(State==null)return;
            for(int i=0;i<runtime.Length;i++)
            {
                var weapon=runtime[i];if(!weapon)continue;baselines[i].Reset(weapon);
                foreach(var owned in State.weapons[(int)Family(weapon)].owned)
                {
                    var option=Array.Find(Settings.options,x=>x.id==owned.id);if(option==null)continue;
                    int n=owned.count;
                    weapon.targetNoise*=Mathf.Pow(option.damage,n);weapon.selfNoise*=Mathf.Pow(option.selfNoise,n);weapon.fireInterval/=Mathf.Pow(option.fireRate,n);weapon.evolution|=option.effect;
                    if(weapon.skill){weapon.skill.cost*=Mathf.Pow(option.skillCost,n);weapon.skill.cooldown*=Mathf.Pow(option.cooldown,n);weapon.skill.rangeMultiplier*=Mathf.Pow(option.skillRange,n);}
                }
                weapon.wailRadius=Settings.wailRadius;
                if(weapon.skill)weapon.skill.extraTriggers=(weapon.evolution&EvolutionEffect.Echo)!=0?1:0;
            }
            float capacity=maxNoise,glands=maxGlands,rate=regen,speed=1,baseNoise=0,reduction=0,decayRate=decay;
            foreach(var owned in State.weapons[(int)WeaponFamily.Player].owned)
            {
                var option=Array.Find(Settings.options,x=>x.id==owned.id);if(option==null)continue;int n=owned.count;
                capacity*=Mathf.Pow(option.noiseCapacity,n);glands+=option.extraGland*n;rate/=Mathf.Pow(option.glandInterval,n);speed*=Mathf.Pow(option.moveSpeed,n);
                baseNoise+=option.baseNoise*n;reduction+=option.incomingReduction*n;decayRate+=option.decayBonus*n;
            }
            float previousGlands=game.stats.maxGlands;
            game.stats.maxNoise=capacity;game.stats.maxGlands=glands;game.stats.glandRegenPerSecond=rate;game.stats.noiseDecayPerSecond=decayRate;
            game.stats.baseNoise=baseNoise;game.stats.incomingNoiseReduction=reduction;game.moveSpeedMultiplier=speed;
            if(glands>previousGlands)game.stats.AddGland(glands-previousGlands);
            Changed?.Invoke();
        }
        public void AreaCleared(bool hasNext)
        {
            if(IntermissionPending)return;IntermissionPending=true;StartCoroutine(Checkpoint(hasNext));
        }
        IEnumerator Checkpoint(bool hasNext)
        {
            yield return new WaitForSeconds(Settings.intermissionDelay);
            if(!game||!game.stats.IsAlive){IntermissionPending=false;yield break;}
            game.CollectUnclaimedDrops();
            if(hasNext)game.EnterEvolution();else{IntermissionPending=false;game.Message("区域全部肃清 · 前往 EXIT",3);}
        }
        public void Continue(){IntermissionPending=false;game.Resume();}
        public void CancelPending(){StopAllCoroutines();IntermissionPending=false;}
        public string Summary()=>State==null?"":$"Lv.{State.Level}  ·  生物质 {State.TotalBiomass}  ·  普通强化 {State.weapons.Sum(w=>w.normalPurchases)}  ·  特殊强化 {State.weapons.Sum(w=>w.specialPurchases)}";
        public string Attributes(WeaponFamily family)
        {
            if(family==WeaponFamily.Player)return $"噪值上限 {game.stats.maxNoise:0.#}     移速 {game.tuning.moveSpeed*game.moveSpeedMultiplier:0.##} 米/秒\n自然降噪 {game.stats.noiseDecayPerSecond:0.#}/秒     声腺 {game.stats.maxGlands:0} 枚 · {1/Mathf.Max(.001f,game.stats.glandRegenPerSecond):0.#}秒/枚\n底噪 {game.stats.baseNoise:0}     受击减噪 {game.stats.incomingNoiseReduction:0}";
            var w=Weapon(family);if(!w)return "未配置";
            string damage=w.pelletCount>1?$"{w.targetNoise:0.#} × {w.pelletCount}":w.targetNoise.ToString("0.#");
            return $"伤害 {damage}     命中自身噪值 +{w.selfNoise:0.#}\n射击间隔 {w.fireInterval:0.###}秒"+(w.skill?$"     技能消耗 {w.skill.cost:0.#} · 冷却 {w.skill.cooldown:0.#}秒":"");
        }
        void OnDestroy()
        {
            if(State!=null)State.Changed-=UpdateBuild;
            if(runtime!=null)foreach(var w in runtime)if(w){if(w.skill)Release(w.skill);Release(w);}
            if(game)game.weaponDefinitions=originals;
        }
        static void Release(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
