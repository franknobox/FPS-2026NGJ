# NGJ 战斗模块

依据《ngj fps 敌人 技能 武器(1).xls》。战斗模块已通过适配器接入现有 SlugWhitebox 白盒场景，包含四种武器、七个技能、三类敌人、玩家噪值/声腺、掉落和结算。保留原地图；FPS Engine 的其他场景不在此次接入范围。

## 直接试玩

- 场景：`Assets/Game/SlugWhitebox/Scenes/SlugWhitebox.unity`，Unity 打开后点击 Play。
- Windows 程序：`Builds/SlugWhitebox/SlugWhitebox.exe`。
- WASD 移动，鼠标瞄准，左键普攻，右键释放当前武器技能。
- 1/2 或滚轮切换两把出战武器（默认霰弹/火箭筒）；Z/X/C 选择嘶鸣/撼地/共鸣，Q 释放。
- R 重新开始，Esc 暂停/继续。向前进入各区，清场后继续推进，第三个区清场后走到 EXIT 结算。
- 普攻有效命中敌人和受击会增加自身噪值，空放、打墙和空爆不增加；同一次普攻的多弹丸、穿透和群体爆炸仅结算一次，达到上限死亡；拾取青色掉落补充声腺与生物质。

菜单 **循声虫族 → 接入新战斗模块（保留当前白盒）** 可重新接线。执行前会在项目外层 `Verification/SceneBackups` 保存场景备份；它不重建地图。

## 文件与入口

- `PlayerCombatStats`：玩家噪值、声腺、增伤、共鸣吸收及资源事件。
- `CombatTarget`：统一受击接口、噪值上限、装甲、眩晕、易伤与死亡事件。普通受击目标可挂此组件，team 设为 1；使用 EnemyCombatant 时不要重复挂载。
- `CombatHurtbox`：子碰撞体转发给同一个 CombatTarget，可标记弱点。
- `EnemyDefinition` / `EnemyCombatant`：三类敌人配置、基础追击、前摇、攻击、眩晕打断、飞行坠落和弱点窗口。
- `WeaponDefinition` / `SkillDefinition`：可配置 ScriptableObject。
- `CombatWeaponController`：四种武器普攻、装备接口、武器技能入口。
- `SkillRunner`：四个武器技能与嘶鸣、撼地、共鸣；资源检查、冷却、取消。
- `CombatHitbox`：球/盒查询、扇形限制、移动声波、墙体遮挡、同目标去重。
- `CombatProjectile`：抛物线榴弹、黏附延时爆炸；SphereCast 避免高速穿透。
- `Editor/CombatAssetBuilder`：菜单 **NGJ → Combat → 生成缺少的配置和预制体**，不会覆盖已有配置。

生成目录 `Generated` 包含 4 个武器配置、7 个技能配置、7 个 Hitbox、2 个投射物预制体、一个玩家战斗组件预制体，另有 3 个敌人配置和 3 个敌人预制体。Hitbox 使用 Physics 主动查询，不依赖 Rigidbody/Trigger；白盒通过 `SlugCombatEffect` 为投射物和技能添加测试可视效果，Scene 视图也可选中查看范围。

## 最小接入

### 不打开代码修改数值

这里的调参入口是 Unity 的 **Inspector（检视面板）**；Console（控制台）用于查看日志。

1. 菜单 **NGJ → Combat → 打开数值调节面板** 可打开独立调参窗口。点击“武器配置”“技能配置”“敌人配置”“命中预制体”定位资源，再拖入“编辑对象”或点击“编辑当前选中对象”。
2. 也可以直接在 Project 选中 `Generated/Weapons`、`Generated/Skills`、`Generated/Enemies` 中的 `.asset`，或打开 `Generated/Prefabs` 中的预制体，在 Inspector 修改中文字段。悬停字段可以看到含义和单位。
3. 武器配置修改伤害、射速、自身噪值、射程；技能配置修改消耗、冷却、眩晕、易伤；Hitbox 预制体修改半径、角度、移动速度；玩家组件修改噪值上限、声腺上限和回复速度。
4. 面板下方“选中并编辑”按钮可直接跳到关联技能或预制体。修改支持 Undo，多选 Inspector 支持批量编辑。
5. 建议在停止 Play 后修改并保存。Play 时组件改动退出后恢复，配置资源的修改可能保留；已生成的攻击实例继续使用生成时的参数。当前噪值和声腺在 Play 中只读，避免绕过战斗事件；停止 Play 后可设置初始值。

脚本已补充中文字段说明、参数提示和主要方法注释，字段名及配置资源引用保持兼容。

### 连接场景组件

1. 在玩家根物体挂 `PlayerCombatStats`、`SkillRunner`、`CombatWeaponController`，玩家 team 设为 0。三个组件的 owner 指向同一个玩家属性组件。或者参考 `PlayerCombatRig` 预制体挂载。
2. `fireOrigin` 和 `castOrigin` 指向相机或独立瞄准点，正 Z 为发射方向。示例预制体的 CastOrigin 需跟随你的相机俯仰。
3. 将武器配置赋给 `equipped`。输入按住射击时每帧调用 `TryFire()`，按技能时调用 `TryWeaponSkill()`；声腺技能通过 `SkillRunner.TryCast(skill)` 调用。
4. 直接拖入 `EnemyChanYin`、`EnemyMiYin` 或 `EnemyZhenYin` 预制体。它们已挂 `EnemyCombatant`、CharacterController 和弱点；不要再挂第二个 `CombatTarget`，也不要同时启用旧 AI 驱动移动和攻击。具体用法见下文。
5. 玩家及其碰撞体放在 Player 层，敌人与弱点放在 Enemy 层。CombatLayers 统一目标和环境遮挡范围，环境可使用 Default、Ground、Object 等环境层；角色不进入遮挡集合。targetMask 包含 Player/Enemy，玩家普攻检测包含环境与 Enemy，投射物和敌人攻击包含环境与双方角色。
6. 敌人的攻击调用 `playerStats.ReceiveHit(new HitInfo { source = enemyTarget, noise = 10, penetration = ArmorGrade.Medium })`，才能触发共鸣吸收。白盒敌人已经通过此入口攻击；`SlugGame.Hurt` 已转接噪值，避免新旧系统重复扣血。FPS Engine 的 IDamageable 在其他场景仍需自行适配。
7. UI 订阅 `NoiseChanged`、`GlandsChanged`、`Died`，或读取 `Noise/Glands` 与 `CooldownRemaining(skill)`；敌人死亡掉落由其他系统订阅 `Died`，拾取调用 `AddGland`。

```csharp
// 由你现有的输入组件调用，不绑定固定按键或额外依赖。
weapons.Equip(sniperDefinition);
weapons.TryFire();
weapons.TryWeaponSkill();
skills.TryCast(gongMingDefinition);
```

## 暂定规则与调参

表格仅给了高/中/低，没有具体数值。生成的伤害、射速、射程、技能消耗噪值、冷却、持续时间、散布、爆炸半径全部是测试值。声腺技能消耗为表格规定的 1。不要将测试值视为设计定稿。

- 玩家噪值到上限死亡：`deathAtNoiseLimit` 可关闭；敌人同样可配置。
- 通用玩家预制体自然降噪默认关闭，声腺每秒恢复 0.1。当前白盒玩家测试值：上限 100，停止增加噪值 2 秒后每秒降噪 8，声腺初始值/上限 2，每 10 秒恢复 1，击杀掉落额外补充 1。在场景 Player 的 PlayerCombatStats 修改，不需要改代码。
- 穿透不低于装甲时全额结算；不足时伤害乘 `underPenetrationMultiplier`，并默认阻挡眩晕/易伤。此公式是可替换的测试规则，非表格定稿。
- 仅标记为弱点的 Collider 且目标 `WeakPointExposed` 时使用弱点装甲。默认眩晕会暴露；攻击期间的暴露由敌人 AI 设置 `weakPointOpen`。弱点放在躯干外可被射线命中的位置。
- 易伤影响后续伤害，重复刷新时间；不与自身叠乘。霰弹按弹丸计伤，同一弹丸/Hitbox 对同一 CombatTarget 只结算一次。
- 步枪增伤影响输出，不提高命中结算的自身噪值。撼音体技能为零伤害、中穿透眩晕。
- 撼地仅影响 `airborne=false` 的目标，不模拟地形波传播；地面高度限制可在其 Hitbox 上改用 Box 形状。
- 共鸣期间吞掉敌方命中及其附带效果，按来袭原始噪值累计，结束后反击。取消/禁用/死亡不反击、不返还消耗；正常反击使用结束时的瞄准方向。
- 已接受的普攻即使自身噪值触顶也会射出。技能释放不是普通开火，不额外增加自身噪值。
- 榴弹普攻命中即爆、超时也爆；黏质炸弹命中后附着，延时爆炸施加易伤。榴弹弹道、黏附时间和易伤倍率都是暂定参数。

通用模块提供基础追击；当前白盒适配器已补上地面 NavMesh 寻路、分区刷怪、三类敌人外观与弱点颜色、HUD、输入、两武器切换、掉落和测试音效。正式虫体模型、动画和对象池尚未制作。白盒允许两武器切换以便验收，不强制实现两武器背包。

## 敌人配置与特性

依据指定 `(1).xls` 的“敌人”表 A2:H4：

| 配置 / 预制体 | 已实现的特性 |
| --- | --- |
| ChanYin / EnemyChanYin（颤音虫） | 低噪值上限、低甲、快速飞行、高频低伤间隙攻击；眩晕时停止攻击并受重力下落，接地后可被地面技能命中，眩晕结束恢复飞行 |
| MiYin / EnemyMiYin（觅音虫） | 中噪值上限、中甲、适中地面移速和攻击节奏；攻击动作和眩晕期间暴露低甲弱点 |
| ZhenYin / EnemyZhenYin（震音虫） | 高噪值上限、重甲、较慢地面移速、低频高伤持续射线；攻击和眩晕期间暴露低甲弱点 |

1. 配置在 `Generated/Enemies`，预制体在 `Generated/Prefabs`。拖入场景后，攻击目标可手动指定玩家的 `PlayerCombatStats`；默认每秒自动寻找该组件。玩家必须有 Collider，才能被物理射线命中。生成的玩家战斗预制体仅负责属性，需要接到你现有的角色碰撞体。
2. 修改敌人 `.asset` 的中文字段即可调整类型、噪值上限、装甲、速度、悬浮高度、发现/攻击距离、前摇、冷却、伤害和持续射线跳数。类型枚举仅作标识，更换类型时请指定整套配置资源。
3. 上限、装甲、弱点装甲和眩晕暴露开关在 Awake/ResetCombat 时从配置复制；停止 Play 后调参并重新运行即可生效。也可选中敌人组件点击“应用敌人配置到组件”，它不会重置当前噪值；仅运行时调用 `ResetCombat()` 会重置战斗状态。移动与攻击参数直接读取配置。
4. 弱点位于子物体 `WeakPoint`，带 SphereCollider 和 CombatHurtbox，摆在躯干前侧；可自行调整位置和半径。只有命中这个碰撞体且处于暴露窗口，才使用低甲。中重甲在前摇和攻击期间暴露，攻击后冷却不暴露，眩晕会打断前摇/持续射线。
5. 攻击会被第一个非自身碰撞体阻挡；`攻击检测层` 应包含环境及玩家。通过玩家 `ReceiveHit` 结算，支持现有共鸣吸收。`AttackStarted`、`AttackStopped`、`ShotFired` 可接动画、射线视觉和音效，`Died` 可接掉落与回收。

表格没有提供具体数值，35/100/240 上限及攻击时长、速度等均为测试值；通用模块将数量倾向“较多/适中/较少”保存为元数据；白盒实际数量与种类由原场景 SlugSpawn 和 SlugTuning 决定。前两类的攻击形状未指定，目前用单次射线作为测试实现；震音虫用分跳射线，伤害字段表示每一跳的噪值。颤音虫的定性“难命中”由飞行与较快速度体现，未添加表格未明确的闪避概率。

敌人预制体提供碰撞与逻辑。当前白盒由 `SlugEnemy` 添加青色飞虫、橙色觅音虫和紫色震音虫外观、攻击曳光、弱点显示及死亡掉落。地面敌人由 NavMeshAgent 驱动，飞虫保留 CharacterController 飞行与眩晕坠落。新战斗模块负责统一伤害和敌人特性。

## 验证

敌人脚本、数值调节窗口、中文 Inspector 和白盒接线已通过 Unity 6000.3.28f1 编译，并成功构建 Windows 试玩程序。

菜单 **NGJ → Combat → 验证核心结算** 在临时场景中验证资源、穿透、眩晕、地面过滤、命中去重、共鸣和冷却；运行结束关闭临时场景。菜单 NGJ → Combat → 准备美术接入配置 可设置现有场景的基线；编辑器入口 ArtIntegrationSetup.ApplyAndVerify 校验层、遮挡、前摇打断、出战组合和模型挂点，无需进入 Play Mode。

已在 Unity 6000.3.28f1 的独立测试项目中通过编译与 46 项运行时断言，包括高速榴弹、黏附跟随、敌人攻击弱点窗口、持续射线、眩晕打断、墙体遮挡，以及颤音虫眩晕坠落、落地受击与恢复飞行。Generated 中的 14 个配置和 13 个预制体已生成，并检查了内部 GUID 引用。已在现有白盒接线；完整流程另通过 `--slug-smoke` 实际射击测试，报告见 `Builds/Verification/combat-smoke.txt`，以报告 PASS/FAIL 为准。

正式资源接入：参见项目开发指南。四个敌人逻辑预制体共用三种策划定义，黄/紫眼虫对外均为觅音虫；霰弹显示撼音体，爪型枪视图对应火箭筒。RocketLauncher 为追加枚举项，不改变旧枚举编号；火箭筒使用独立白板弹体与遮挡爆炸，暂无武器技能。统一素材场景为 Assets/ART/Scenes/资源展示.unity。
