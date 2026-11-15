using System;
using UnityEngine;
namespace NGJ.Progression
{
    public enum WeaponFamily { Sniper, Rifle, Shotgun, Explosive, Player }
    [Flags] public enum EvolutionEffect { None=0, Echo=1, BioAmplify=2, StunAmplify=4, Wail=8, SharedBuff=16, Burst=32, SharpNoise=64, DoubleShot=128, MicroWave=256, DirectWarhead=512, HeavyWarhead=1024 }
    [Serializable] public sealed class EvolutionOption
    {
        public string id,title;
        [TextArea] public string description;
        public bool special;
        public int families=15,normalCredits=1;
        public float damage=1,selfNoise=1,fireRate=1,skillCost=1,cooldown=1,skillRange=1;
        public EvolutionEffect effect;
        public float noiseCapacity=1,moveSpeed=1,glandInterval=1;
        public float baseNoise,incomingReduction,decayBonus;
        public int extraGland;
        public bool Supports(WeaponFamily family)=>(families & (1<<(int)family))!=0;
    }
    [CreateAssetMenu(menuName="NGJ/成长/局内成长配置")]
    public sealed class ProgressionSettings : ScriptableObject
    {
        [Min(1)] public int firstLevelBiomass=40;
        [Min(0)] public int biomassGrowthPerLevel=10;
        [Min(1)] public int pointsPerLevel=1;
        [Min(1)] public int normalUpgradesPerSpecial=3;
        [Min(0)] public int biomassPerEnemy=10;
        [Min(.1f)] public float intermissionDelay=1.2f;
        [Min(.1f)] public float wailRadius=4;
        public EvolutionOption[] options=Defaults();
        public static EvolutionOption[] Defaults()=>new[]{
            new EvolutionOption{id="noise_down",title="降噪",description="射击命中后积累的自身噪值降低 10%。",selfNoise=.9f},
            new EvolutionOption{id="excite",title="激发",description="射击伤害与积累的自身噪值均增加 10%。",damage=1.1f,selfNoise=1.1f},
            new EvolutionOption{id="cadence",title="增频",description="射速增加 15%。",fireRate=1.15f},
            new EvolutionOption{id="evolve",title="进化",description="本次计为两次普通强化，更快解锁特殊强化。",normalCredits=2},
            new EvolutionOption{id="sensitive",title="易感",description="武器技能噪值消耗降低 20%，冷却缩短 15%。",skillCost=.8f,cooldown=.85f},
            new EvolutionOption{id="dull",title="顿感",description="武器技能噪值消耗增加 15%，一次释放可排出更多噪值。",skillCost=1.15f},
            new EvolutionOption{id="echo",title="回响",special=true,families=5,description="武器技能额外触发一次；消耗与冷却均增加 35%。",skillCost=1.35f,cooldown=1.35f,effect=EvolutionEffect.Echo},
            new EvolutionOption{id="bio_amp",title="生体增幅",special=true,families=9,description="每次攻击每命中一名不同敌人，为下一次射击叠加 10% 伤害；下一枪用尽。",effect=EvolutionEffect.BioAmplify},
            new EvolutionOption{id="stun_amp",title="刺耳回音",special=true,families=7,description="对已眩晕敌人造成的噪值增加 50%。",effect=EvolutionEffect.StunAmplify},
            new EvolutionOption{id="wail",title="哀嚎",special=true,families=9,description="击杀时向附近敌人传导本次命中伤害的 40%；受墙体遮挡，不连锁触发哀嚎。",effect=EvolutionEffect.Wail},
            new EvolutionOption{id="shared_buff",title="共振频率",special=true,families=2,description="步枪技能的临时增伤也可作用于其他出战武器。",effect=EvolutionEffect.SharedBuff},
            new EvolutionOption{id="burst",title="频振",special=true,families=2,description="改为按次触发的三连发，伤害 +30%；连发内射速为 200%，组间隔为原间隔的 150%。",damage=1.3f,effect=EvolutionEffect.Burst},
            new EvolutionOption{id="sharp_noise",title="尖锐噪鸣",special=true,families=2,description="每发额外造成射击时自身当前噪值 ×25% 的伤害。",effect=EvolutionEffect.SharpNoise},
            new EvolutionOption{id="double_shot",title="双声共鸣",special=true,families=4,description="8 枚弹丸每颗伤害翻倍（基础 8×10），射击积累噪值增加 50%。",damage=2,selfNoise=1.5f,effect=EvolutionEffect.DoubleShot},
            new EvolutionOption{id="micro_wave",title="微声震荡",special=true,families=4,description="武器技能冷却缩短 60%，噪值消耗降低 40%，眩晕范围减半。",skillCost=.6f,cooldown=.4f,skillRange=.5f,effect=EvolutionEffect.MicroWave},
            new EvolutionOption{id="direct_warhead",title="钻声弹头",special=true,families=8,description="爆炸弹体直接命中的敌人额外受到 50% 伤害；其他爆炸目标不受此加成。",effect=EvolutionEffect.DirectWarhead},
            new EvolutionOption{id="heavy_warhead",title="重型弹头",special=true,families=8,description="爆炸弹体变为受重力影响的抛物线，伤害增加 30%。",damage=1.3f,effect=EvolutionEffect.HeavyWarhead},
            new EvolutionOption{id="inner_ear",title="扩展内耳",families=16,description="玩家噪值上限增加 10%。",noiseCapacity=1.1f},
            new EvolutionOption{id="base_noise",title="底噪",families=16,description="基础噪值 +10；低于基础噪值时，停止活动 1 秒后每秒回复 5 噪值。",baseNoise=10},
            new EvolutionOption{id="incoming_down",title="降噪",families=16,description="每次受到的攻击噪值减少 1，最低为 0。",incomingReduction=1},
            new EvolutionOption{id="decay_up",title="缓冲",families=16,description="每秒自然衰减的噪值增加 1。",decayBonus=1},
            new EvolutionOption{id="speed_up",title="迅捷",families=16,description="移动速度增加 15%。",moveSpeed=1.15f},
            new EvolutionOption{id="regen_up",title="再生",families=16,description="声腺自然回复所需时间缩短 20%。",glandInterval=.8f},
            new EvolutionOption{id="extra_gland",title="额外声腺",special=true,families=16,description="声腺上限 +1，并补充这枚额外声腺。",extraGland=1}
        };
    }
}
