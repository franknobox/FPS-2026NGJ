using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NGJ.Combat.Editor
{
    // 仅影响编辑器显示，保留原始字段名和资源 GUID；支持 Undo、多选和预制体覆盖记录。
    public class CombatInspector : UnityEditor.Editor
    {
        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "kind", "敌人类型" },
            { "description", "定位与特性" },
            { "quantity", "数量倾向" },
            { "flying", "飞行单位" },
            { "moveSpeed", "移动速度（米／秒）" },
            { "flightHeight", "悬浮高度（米）" },
            { "fallsWhenStunned", "眩晕后坠落" },
            { "exposeDuringAttack", "攻击时暴露弱点" },
            { "exposeDuringWindup", "前摇时暴露弱点" },
            { "exposeDuringStun", "眩晕时暴露弱点" },
            { "attackMode", "攻击模式" },
            { "attackNoise", "每次攻击噪值" },
            { "attackPenetration", "攻击穿透等级" },
            { "attackRange", "攻击距离（米）" },
            { "detectionRange", "发现距离（米）" },
            { "windup", "攻击前摇（秒）" },
            { "attackInterval", "攻击后冷却（秒）" },
            { "attackDuration", "攻击动作时长（秒）" },
            { "beamDuration", "射线持续时间（秒）" },
            { "beamTickInterval", "射线伤害间隔（秒）" },
            { "definition", "敌人类型配置" },
            { "attackTarget", "攻击目标" },
            { "findPlayerAutomatically", "自动寻找玩家" },
            { "attackOrigin", "攻击发射点" },
            { "attackMask", "攻击检测层" },
            { "fallGravity", "坠落重力" },
            { "type", "武器类型" },
            { "displayName", "显示名称" },
            { "fireInterval", "开火间隔（秒）" },
            { "selfNoise", "开火自身噪值" },
            { "targetNoise", "单弹丸伤害／噪值" },
            { "penetration", "穿透等级" },
            { "range", "射线射程（米）" },
            { "pelletCount", "弹丸数量" },
            { "spreadAngle", "散布半角（度）" },
            { "maxTargetsPerPellet", "单弹丸命中上限" },
            { "projectilePrefab", "投射物预制体" },
            { "projectileSpeed", "投射物初速度（米／秒）" },
            { "skill", "武器技能" },
            { "resource", "消耗资源类型" },
            { "cost", "资源消耗量" },
            { "cooldown", "冷却时间（秒）" },
            { "effect", "技能效果类型" },
            { "hitboxPrefab", "命中区域预制体" },
            { "hit", "命中效果" },
            { "duration", "效果持续时间（秒）" },
            { "damageMultiplier", "增伤倍率" },
            { "absorbedNoiseMultiplier", "共鸣反击倍率" },
            { "team", "阵营编号" },
            { "maxNoise", "噪值上限" },
            { "currentNoise", "当前／初始噪值" },
            { "armor", "躯干装甲" },
            { "weakPointArmor", "弱点装甲" },
            { "airborne", "处于空中" },
            { "underPenetrationMultiplier", "穿透不足伤害倍率" },
            { "armorBlocksEffects", "装甲阻挡控制和易伤" },
            { "deathAtNoiseLimit", "达到噪值上限死亡" },
            { "exposeWeakPointWhenStunned", "眩晕时暴露弱点" },
            { "weakPointOpen", "主动暴露弱点" },
            { "maxGlands", "声腺上限" },
            { "glands", "当前／初始声腺" },
            { "glandRegenPerSecond", "每秒声腺回复" },
            { "noiseDecayPerSecond", "每秒自然降噪" },
            { "noiseDecayDelay", "降噪等待时间（秒）" },
            { "shape", "判定形状" },
            { "radius", "判定半径（米）" },
            { "halfExtents", "盒形半尺寸（米）" },
            { "halfAngle", "扇形半角（度）" },
            { "lifetime", "存活时间（秒）" },
            { "speed", "声波速度（米／秒）" },
            { "targetMask", "可命中物理层" },
            { "obstructionMask", "墙体遮挡层" },
            { "maxTargets", "最多命中目标数" },
            { "gravity", "重力加速度" },
            { "sticky", "命中后黏附" },
            { "detonationDelay", "黏附引爆延时（秒）" },
            { "explosionPrefab", "爆炸区域预制体" },
            { "collisionMask", "投射物碰撞层" },
            { "owner", "所属玩家属性" },
            { "castOrigin", "技能瞄准／发射点" },
            { "skills", "技能执行组件" },
            { "fireOrigin", "普攻瞄准／发射点" },
            { "equipped", "当前装备武器" },
            { "hitMask", "普攻射线检测层" },
            { "target", "所属受击目标" },
            { "weakPoint", "是否为弱点" },
            { "source", "攻击来源" },
            { "noise", "基础伤害／噪值" },
            { "stunDuration", "眩晕时间（秒）" },
            { "vulnerabilityDuration", "易伤时间（秒）" },
            { "vulnerabilityMultiplier", "易伤倍率" },
            { "groundOnly", "仅命中地面目标" },
            { "point", "命中位置" },
            { "direction", "命中方向" },
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("直接修改下面的数值即可调参。悬停字段可查看说明。武器／技能／敌人配置为共享资源。", MessageType.Info);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("运行时修改组件会在退出 Play 后恢复；配置资源的修改可能保留。已生成的声波／投射物仍使用生成时的参数。", MessageType.Info);

            // 逐项遍历展开的子字段，让 HitInfo 内的伤害、眩晕、易伤也显示中文。
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                string label = Labels.TryGetValue(property.name, out string translated) ? translated : property.displayName;
                bool readOnly = property.name == "m_Script" ||
                    (Application.isPlaying && (property.name == "currentNoise" || property.name == "glands"));
                using (new EditorGUI.DisabledScope(readOnly))
                {
                    EditorGUI.indentLevel = property.depth;
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), false);
                }
                enterChildren = property.isExpanded && property.hasVisibleChildren && property.propertyType != SerializedPropertyType.ObjectReference;
            }
            EditorGUI.indentLevel = 0;
            serializedObject.ApplyModifiedProperties();

            // 从武器/技能面板直接跳到关联配置或预制体，省去在 Project 中查找。
            foreach (string field in new[] { "definition", "equipped", "skill", "hitboxPrefab", "projectilePrefab", "explosionPrefab" })
            {
                var reference = serializedObject.FindProperty(field);
                if (reference != null && reference.objectReferenceValue &&
                    GUILayout.Button("选中并编辑：" + reference.objectReferenceValue.name))
                {
                    Selection.activeObject = reference.objectReferenceValue;
                    EditorGUIUtility.PingObject(reference.objectReferenceValue);
                }
            }
            if (target is EnemyCombatant && GUILayout.Button("应用敌人配置到组件（不重置当前噪值）"))
            {
                foreach (var item in targets)
                {
                    Undo.RecordObject(item, "应用敌人配置");
                    ((EnemyCombatant)item).ApplyDefinition();
                    EditorUtility.SetDirty(item);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
            }
            if (Application.isPlaying && targets.Length == 1 && target is PlayerCombatStats player)
                EditorGUILayout.LabelField("运行状态", $"存活：{player.IsAlive}  眩晕：{player.IsStunned}  增伤：{player.DamageMultiplier:0.##}  吸收：{player.AbsorbedNoise:0.##}");
        }
    }

    // 分别注册以限定作用范围，避免改变项目中其他系统的 Inspector。
    [CustomEditor(typeof(EnemyDefinition)), CanEditMultipleObjects] public sealed class EnemyDefinitionInspector : CombatInspector { }
    [CustomEditor(typeof(WeaponDefinition)), CanEditMultipleObjects] public sealed class WeaponInspector : CombatInspector { }
    [CustomEditor(typeof(SkillDefinition)), CanEditMultipleObjects] public sealed class SkillInspector : CombatInspector { }
    [CustomEditor(typeof(CombatTarget), true), CanEditMultipleObjects] public sealed class TargetInspector : CombatInspector { }
    [CustomEditor(typeof(CombatWeaponController)), CanEditMultipleObjects] public sealed class WeaponControllerInspector : CombatInspector { }
    [CustomEditor(typeof(SkillRunner)), CanEditMultipleObjects] public sealed class SkillRunnerInspector : CombatInspector { }
    [CustomEditor(typeof(CombatHitbox)), CanEditMultipleObjects] public sealed class HitboxInspector : CombatInspector { }
    [CustomEditor(typeof(CombatProjectile)), CanEditMultipleObjects] public sealed class ProjectileInspector : CombatInspector { }
    [CustomEditor(typeof(CombatHurtbox)), CanEditMultipleObjects] public sealed class HurtboxInspector : CombatInspector { }

    // 菜单提供固定入口，也可直接选中 Project 资源或场景组件使用 Inspector。
    public sealed class CombatTuningWindow : EditorWindow
    {
        Object edited;
        UnityEditor.Editor inspector;
        Vector2 scroll;

        [MenuItem("NGJ/Combat/打开数值调节面板")]
        public static void Open()
        {
            var window = GetWindow<CombatTuningWindow>("战斗数值调节");
            window.minSize = new Vector2(380, 400);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("选择武器／技能配置，或拖入玩家、敌人、Hitbox 组件，即可修改数值。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("武器配置")) ShowFolder("Weapons");
                if (GUILayout.Button("技能配置")) ShowFolder("Skills");
                if (GUILayout.Button("敌人配置")) ShowFolder("Enemies");
                if (GUILayout.Button("命中预制体")) ShowFolder("Prefabs");
            }
            edited = EditorGUILayout.ObjectField("编辑对象", edited, typeof(Object), true);
            if (GUILayout.Button("编辑当前选中对象")) edited = Selection.activeObject;
            // GameObject 转为战斗组件，并允许在同物体上的多个战斗组件之间选择。
            GameObject go = edited as GameObject;
            if (edited is Component component) go = component.gameObject;
            if (go)
            {
                foreach (var candidate in go.GetComponents<MonoBehaviour>())
                    if (candidate && candidate.GetType().Namespace == "NGJ.Combat" && GUILayout.Button(candidate.GetType().Name)) edited = candidate;
                if (edited is GameObject) edited = go.GetComponent<CombatTarget>() as Object ?? go.GetComponent<CombatHitbox>() as Object ?? go.GetComponent<CombatProjectile>();
            }
            if (!edited) return;
            UnityEditor.Editor.CreateCachedEditor(edited, null, ref inspector);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (inspector) inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        static void ShowFolder(string folder)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>("Assets/Game/NGJCombat/Generated/" + folder);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }
        void OnDisable() { if (inspector) DestroyImmediate(inspector); }
    }
}
