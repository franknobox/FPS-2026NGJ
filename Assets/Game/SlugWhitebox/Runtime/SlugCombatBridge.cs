using NGJ.Combat;
using UnityEngine;
namespace SlugWhitebox
{
    // 白盒负责输入和表现，NGJCombat 统一处理资源、攻击与伤害。
    public partial class SlugGame
    {
        [Header("战斗配置：步枪、狙击、霰弹、榴弹、火箭筒；界面名称读取配置")]
        public WeaponDefinition[] weaponDefinitions;
        [Tooltip("出战索引：0步枪、1狙击、2霰弹、3榴弹、4火箭筒。默认霰弹和火箭筒。")]
        public int[] weaponLoadout = { 2, 4 };

        public bool ConfigureLoadout(int first, int second)
        {
            if (weaponDefinitions == null || first == second || first < 0 || second < 0 ||
                first >= weaponDefinitions.Length || second >= weaponDefinitions.Length ||
                !weaponDefinitions[first] || !weaponDefinitions[second]) return false;
            weaponLoadout = new[] { first, second };
            if (weapons) SelectWeapon(first);
            return true;
        }

        public bool SelectWeaponSlot(int slot)
        {
            return weaponLoadout != null && slot >= 0 && slot < weaponLoadout.Length && slot < 2 && SelectWeapon(weaponLoadout[slot]);
        }
        [Tooltip("嘶鸣、撼地、共鸣。")]
        public SkillDefinition[] glandSkills;
        [Tooltip("颤音虫、觅音虫、震音虫。")]
        public EnemyCombatant[] enemyPrefabs;
        [Tooltip("觅音虫外观变体，共用觅音虫定义；按出生顺序轮换。")]
        public EnemyCombatant[] miYinVariants;
        public PlayerCombatStats stats;
        public CombatWeaponController weapons;
        public SkillRunner runner;
        void InitializeCombat()
        {
            stats=player.GetComponent<PlayerCombatStats>();runner=player.GetComponent<SkillRunner>();weapons=player.GetComponent<CombatWeaponController>();
            CombatLayers.SetActorLayer(player.gameObject,CombatLayers.Player);
            weapons.hitMask=CombatLayers.PlayerAttackMask;
            stats.HitReceived += hit => {damageUntil=Time.time+.3f;};
            stats.Died += () => {EndRun("噪值过载 / 按 R 重新开始");view.transform.localPosition=Vector3.up*.5f;};
            runner.CastStarted += definition => {Pulse(player.position+Vector3.up*.2f,8,Color.cyan,.5f);Play(skillSounds[Mathf.Clamp(skill,0,2)],.5f);Message("释放："+definition.displayName,2);};
        }
        void ClearEffects()
        {
            foreach(var effect in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))Destroy(effect.gameObject);
            foreach(var effect in FindObjectsByType<CombatHitbox>(FindObjectsSortMode.None))Destroy(effect.gameObject);
        }
        void ResetNewCombat(){ClearEffects();stats.enabled=true;stats.ResetCombat();runner.ResetSkills();weapons.ResetWeapons();}
        public bool CastWeaponSkill()
        {
            if(!Running)return false;
            if(weapons.equipped && !weapons.equipped.skill){Message("这把武器没有武器技能",1.5f);return false;}
            bool ok=weapons.TryWeaponSkill();if(!ok)Message("噪值不足、技能冷却或共鸣尚未结束",2);return ok;
        }
    }
}
