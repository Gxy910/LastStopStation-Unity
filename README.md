# 末日车站

《末日车站》是一款用 Unity 和 C# 制作的单人第一人称叙事生存 Demo。玩家从灾后的旧客运站出发，通过对话、寻找道具和站外接送事件推进四章主线。接送事件中的选择和行动会改变回站后的角色位置、对话和场景内容。

修好阿岚的通讯装置后，玩家可在第一、第二和第四章到车站的路哨岗亭接取货场清障委托。第三章从站外接送点继续接送剧情。进入货场后，可以清理感染者、在波次间强化装备，也可以在战斗中返回绿灯门撤离。委托结束后返回出发地点，继续当前主线。剧情中获得的武士刀可带入货场，按 `3` 装备。

**技术展示**　章节与任务状态、分批刷怪及撤离结算、动画与物理命中判定。

## 运行与试玩

1. 用 **Unity 2022.3.62f2c1** 打开仓库根目录，也就是包含 `Assets`、`Packages` 和 `ProjectSettings` 的目录。
2. 在 Project 面板打开 [Assets/Scenes/OldStation.unity](Assets/Scenes/OldStation.unity)，点击 Play。开场字幕可按 `Space` 跳过。
3. 从旧票房的阿岚开始体验主线。想试玩货场时，先找回工具包并帮阿岚修好通讯装置，再去路哨岗亭看向清障值勤板，按 `E` 接单。

| 场景 | 操作 |
| --- | --- |
| 通用 | `WASD` 移动，鼠标观察，`Shift` 奔跑，`E` 交互，`E` / `Enter` 继续对话，`Esc` 暂停 |
| 站外接送 | `1` / `2` 选择付费接送或自行撤离。领到装备后，鼠标左键攻击，`H` 使用恢复补给 |
| 货场战斗 | `Space` 开始第一波，鼠标左键攻击，`1` / `2` 切换枪械与短棍，`R` 换弹，`F` 使用血包 |
| 波次间与撤离 | `7` / `8` / `9` 选择强化，`Space` 开始下一波。靠近绿灯门按 `E` 可撤离，结算已清空的波次 |

## 技术亮点

### 1. 章节状态驱动任务、战斗与场景变化

`StationStory` 用 `CurrentStep` 记录进度，按当前步骤判断交互对象是否能推进任务，并提供对应的目标提示。比如玩家带工具包回来交给阿岚后，任务目标会转为前往出站门，车站控制器也会刷新道具和对话。进入第三章时，`StationPrototype` 在同一个 Unity 场景里切换车站和接送区域，再把玩家移动到对应出生点。

接送事件自己管理付费、领装备、战斗和逃离阶段。事件结束后，通过回调把结果交给主线，再由 `EpilogueStage` 按结局配置回站后的角色与场景。玩家因此能在第四章看到第三章选择带来的变化。

[任务交互](Assets/Scripts/StationStory.cs#L131) · [章节切换](Assets/Scripts/StationPrototype.cs#L128) · [接送事件](Assets/Scripts/EscortEncounter.cs#L159) · [结局场景](Assets/Scripts/EpilogueStage.cs#L73)

### 2. 货场波次调度与撤离结算

每波可以安排多批敌人。货场维护常规入口与侧门的待入场队列，并把待入场的第二只精英算进未完成数量。场内最多有 **6 只存活敌人**，有空位且入口安全时再放怪。清波需要同时满足「场内已无活敌」和「待入场人数为零」，避免把两批敌人之间的空档算成清波。精英蓄力时会显示冲撞方向，玩家可以侧移躲开，再利用恢复时间反击。

任务分别记录当前波次与已清空波次。比如清完第 4 波、在第 5 波中途撤离，这次按第 4 波结算。成功撤离时通过 `PlayerPrefs` 更新最佳成绩，再回到车站。重新接单时从基础装备开始。

[刷怪和清波](Assets/Scripts/Patrol/PatrolMission.cs#L138) · [精英冲撞](Assets/Scripts/Patrol/PatrolEnemy.cs#L222) · [撤离结算](Assets/Scripts/Patrol/PatrolMission.cs#L413)

### 3. 用动画姿势更新头部受击区

感染者的移动、动画和受击分别由角色控制器、骨骼动画和受击区处理。头部动画采样完成后，代码把绑定在头骨坐标下的偏移换算成世界坐标，更新头部受击球。武器从相机画面中心发出射线，查询前同步物理变换，再按最近命中和实体遮挡决定结果。这让头部跟随可见动作，同时保留身体命中和障碍物挡弹。

[动画采样](Assets/Scripts/AssetAnimationPlayer.cs#L50) · [头部受击区](Assets/Scripts/Patrol/PatrolEnemy.cs#L115) · [射击与遮挡判定](Assets/Scripts/Patrol/PatrolWeapon.cs#L251)

## 问题排查记录

### 案例一　快怪奔跑时，瞄准头部却判成身体命中或打空

1. **现象**　玩家追瞄奔跑中的快怪，部分瞄向头部的射击没有判成爆头。
2. **复现与定位**　在测试副本中采样快怪跑步动画的 5 个时刻，从 2、6、12 米三个距离分别向 3 个头部参考点发射射线。记录每枪的判定，以及瞄点和头部受击球中心之间的距离。相同姿势下，三个距离各出现一次漏判。
3. **确认原因**　旧版头部受击球固定在敌人根对象下。跑步动画移动的是头骨，受击球没有随头骨的摆动更新。这解释了复现中出现的身体命中和打空。
4. **修复方法**　初始化时记录球心在 `Head` 骨骼坐标系中的偏移。每次动画采样后，通过 `PoseUpdated` 用最新姿势更新球心，射线查询前调用 `Physics.SyncTransforms()`。这样可以保留受击区原有的尺寸与定位，同时让它跟上动画。
5. **验证结果**　相同的 45 组条件下，修改前有 **42 次爆头判定、1 次身体命中、2 次未命中**，修改后 **45 次均判为爆头**。另做了身体伤害、玩家位移后射击、墙体遮挡和前方敌人遮挡的自动回归。这些结果对应指定动画姿势和参考点。连续移动瞄准、头颈交界以及多敌人实战中的手感仍需人工试玩。

验证记录：[修复前 45 组](Docs/Verification/logs/head_before_edges.log) · [修复后 45 组](Docs/Verification/logs/head_after_full.log) · [动画姿势下的头部受击区回归](Docs/Verification/logs/PatrolHitCoverageChecks-resume-final.log)，关键词 `HEAD REPRO SUMMARY`、`COVERAGE PASS`。

### 案例二　第二只精英可能与第一只重叠出生

1. **现象与复现**　在测试副本跳到双精英波，让第一只精英停在原装卸口，再触发第二只进场。修复前，两只精英的生成位置重合。
2. **定位与原因**　进场逻辑已经检查同屏名额，但当时的入口选择没有检测出生空间是否被占用。即使场内只剩一只敌人，它也可能正挡在出生点。
3. **修复方法**　生成前先检查出生点与玩家的距离，再用胶囊体重叠查询检查出生体积。原入口被挡时检查另一个入口，两边都被挡时保留待入场状态，等空间腾出再生成。
4. **验证结果**　同样布置下复测，第二只精英从可用的原装卸口进入，两只不再重叠。另验证了双入口被挡时继续等待、等待期间不提前清波，以及生成点与玩家保持距离。

验证记录：[修复前重叠](Docs/Verification/logs/spawn_safety_before.log) · [修复后分离](Docs/Verification/logs/spawn_safety_after.log) · [出生安全与入口切换回归](Docs/Verification/logs/PatrolSpawnSafetyChecksResume-resume-final.log)，关键词 `SPAWN REPRO`、`SPAWN SAFETY`。

[出生空间检查与入口切换](Assets/Scripts/Patrol/PatrolMission.cs#L314) · [第二只精英的入场逻辑](Assets/Scripts/Patrol/PatrolMission.cs#L346)

## 验证范围与资源

上述玩法与两项修复均已接入主工程。复现、跳波和自动回归使用单独的 Unity 测试副本；README 中的验证数字来自该副本的测试记录。手动体验可从仓库中的 `OldStation` 场景按上面的路线进入货场。

11 份原始 Unity Editor 日志（共约 1.1 MB）和检查脚本收录在 [Docs/Verification](Docs/Verification/README.md)：那里写了复现命令、每份日志对应哪条结论，以及关键词速查。

其余自动回归覆盖：51/61/71/81 波血量与双精英节奏（`PatrolEndgameChecks`）、委托-首波-强化-撤离主流程（`PatrolRouteChecks`）、近战命中与血迹（`PatrolMeleeChecks`）、精英冲撞与扣血（`PatrolChargeResumeChecks`）、侧门预警与回退（`PatrolSideResumeChecks`）。

`Docs/Verification/scripts/` 是测试副本里检查脚本的原样拷贝，仅作参考，不参与主工程编译。

角色模型来源见 [第三方模型来源](第三方模型来源.txt)。
