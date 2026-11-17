# 检查脚本（测试副本原样拷贝）

这些文件来自测试副本 `PatrolPrototype/Assets/Editor`，用于生成上一级 [logs](../logs) 中的证据，只作为参考资料保留。**不要**直接放进主工程 `Assets/Editor`：脚本通过反射访问私有字段，依赖测试副本的场景布局与调试入口，主工程编译会报错或行为不同。

## 入口

每个类都是 `[InitializeOnLoad]` 的 Editor 静态类，`public static void Run()` 负责打开 `OldStation` 场景、置 `SessionState` 标记并进入 Play；`Poll()` 在 `EditorApplication.update` 里等待场景就绪后执行检查。批处理命令见 [上级 README](../README.md#复现命令)。

## 脚本与检查内容

| 脚本 | 检查内容 |
| --- | --- |
| [PatrolHeadMotionRepro.cs](PatrolHeadMotionRepro.cs) | 案例一：5 个跑步动画时刻 × 3 个距离 × 3 个头部参考点的 45 组射击，输出 `HEAD REPRO` 与 `HEAD REPRO SUMMARY` |
| [PatrolHitCoverageChecks.cs](PatrolHitCoverageChecks.cs) | 受击球是否跟着动画头部走：`COVERAGE PASS animated head damage`、`COVERAGE PASS blood at animated head` |
| [PatrolSpawnSafetyRepro.cs](PatrolSpawnSafetyRepro.cs) | 案例二：制造出生点重叠，输出 `SPAWN REPRO bossDistance=... overlap=...` |
| [PatrolSpawnSafetyChecks.cs](PatrolSpawnSafetyChecks.cs) | 出生安全主回归（入口选择、出生体积检查） |
| [PatrolSpawnSafetyChecksResume.cs](PatrolSpawnSafetyChecksResume.cs) | 出生安全续测：双入口被挡时等待、入口切换、等待期间不提前清波、生成点与玩家距离 |
| [PatrolEndgameChecks.cs](PatrolEndgameChecks.cs) | 无尽货场：51/61/71/81 波血量规则与双精英节奏 |
| [PatrolRouteChecks.cs](PatrolRouteChecks.cs) | 委托锁定与解锁、简报锁首波、强化选择、绿灯门撤离与已清波次结算 |
| [PatrolMeleeChecks.cs](PatrolMeleeChecks.cs) | 短棍近战命中、血迹残留、三击击倒 |
| [PatrolChargeResumeChecks.cs](PatrolChargeResumeChecks.cs) | 精英 `Windup → Charge` 状态切换与 20/25 波扣血 |
| [PatrolSideResumeChecks.cs](PatrolSideResumeChecks.cs) | 侧门预警、限流、入口被堵时的回退 |
| [PatrolProfilerResumeChecks.cs](PatrolProfilerResumeChecks.cs) | Profiler 采样（性能记录，不参与上面任何结论） |
| [PatrolDebugWindow.cs](PatrolDebugWindow.cs) | 调试窗口：跳波、查看波次/待入场状态（手动复现用，不是自动检查） |

## 与主工程的关系

这些检查直接调用主工程 `Assets/Scripts` 里的同一套运行时逻辑（`PatrolMission`、`PatrolEnemy`、`PatrolWeapon` 等），测试副本只是在场景与调试入口上做了改动，所以结论对主工程成立。
