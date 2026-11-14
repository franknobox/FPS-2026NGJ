using UnityEngine;

namespace SlugWhitebox
{
    [CreateAssetMenu(menuName = "循声虫族/白盒参数")]
    public class SlugTuning : ScriptableObject
    {
        [Header("移动与生存（无跳跃/冲刺）")]
        public float moveSpeed = 6.5f;
        public float mouseSensitivity = .095f;
        public float health = 120;
        [Header("声腺：数量、回复、每只敌人掉落")]
        public float glandMax = 100;
        public float glandRegen = 2.5f;
        public int glandDrop = 18;
        public int biomassDrop = 10;
        public float pickupRadius = 4;
        [Header("技能：裂甲脉冲 / 迟滞共振 / 震荡爆鸣")]
        public int[] skillCosts = { 25, 35, 45 };
        public float vulnerabilityMultiplier = 2;
        public float vulnerabilityDuration = 5;
        public float slowDuration = 6;
        public float staggerDuration = 2.5f;
        public float skillRange = 16;
        [Header("武器：中频步枪 / 高频穿刺 / 低频霰弹 / 骨刃")]
        public float[] weaponDamage = { 18, 75, 12, 48 };
        public float[] fireIntervals = { .115f, .8f, .7f, .45f };
        public float[] heatPerShot = { 10, 38, 34, 0 };
        public float heatCoolRate = 23;
        public float overheatRecovery = 30;
        [Header("敌人：疾行虫 / 甲壳虫 / 吐蚀虫")]
        public float[] enemyHealth = { 78, 185, 100 };
        public float[] enemySpeed = { 3.6f, 2.25f, 2.7f };
        public int[] sectorCounts = { 8, 11, 14 };
        public float enemyDamage = 12;
    }
}
