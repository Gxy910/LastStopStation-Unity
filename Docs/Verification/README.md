# 验证证据与复现记录

本目录归档根 README 引用的自动化检查输出，目的是让「42 次爆头 → 45 次爆头」「第二只精英不再重叠」这类结论可以当场核对，而不是只给一句结论。

## 来源与环境

- 时间：2026-11-15 至 2026-11-17
- 引擎：Unity 2022.3.62f2c1，与主工程一致
- 运行方式：Unity Editor 批处理（无图形界面）
- 日志：11 份完整 Unity Editor 日志，共约 1.1 MB
- 测试副本 `PatrolPrototype` 位于本仓库之外的工作目录，未入库（原因见文末「局限」）
- `scripts/`：测试副本中检查脚本的原样拷贝，见 [scripts/README.md](scripts/README.md)

## 复现命令

每个检查类暴露一个静态 `Run()`，用批处理启动：

```bat
E:\2022.3.62f2c1\Editor\Unity.exe ^
  -batchmode -nographics ^
  -projectPath E:\codex111\2026-11-11\new-chat\work\PatrolPrototype ^
  -executeMethod PatrolHitCoverageChecks.Run ^
  -logFile E:\codex111\2026-11-15\e-codex111-2026-11-11-new\work\PatrolHitCoverageChecks-resume-final.log
```

`Run()` 打开 `Assets/Scenes/OldStation.unity`、置 SessionState 标记并进入 Play；`Poll()` 在 `EditorApplication.update` 里等场景就绪后推进检查。每条断言输出 `PASS <描述>`，断言失败直接抛异常中断，所以「日志跑到最后的 `CHECKS PASSED` 且没有异常」即为整组通过。把 `-executeMethod` 换成上表任一检查类的 `Run` 即可单独重跑。

## 日志怎么读

编辑器日志前 340 行左右是 License、场景加载和 Play 启动，检查结论集中在中段（约 L350–L440），文件尾部是 Profiler 与 MemoryLeaks 统计，与结论无关。在 GitHub 或编辑器里搜关键词即可：

## 证据清单

| 日志 | 行数 | 对应结论 | 检索关键词 |
| --- | --- | --- | --- |
| [head_before_edges.log](logs/head_before_edges.log) | 1662 | 案例一 修复前：42 爆头 / 1 身体 / 2 未中 | `HEAD REPRO SUMMARY` |
| [head_after_full.log](logs/head_after_full.log) | 1669 | 案例一 修复后：45/45 爆头 | `HEAD REPRO SUMMARY` |
| [PatrolHitCoverageChecks-resume-final.log](logs/PatrolHitCoverageChecks-resume-final.log) | 1472 | 受击区随动画更新：动画头部伤害与血迹 | `COVERAGE PASS` |
| [spawn_safety_before.log](logs/spawn_safety_before.log) | 1226 | 案例二 修复前：两只精英生成点重合 | `SPAWN REPRO` |
| [spawn_safety_after.log](logs/spawn_safety_after.log) | 1210 | 案例二 修复后：间距 13 米、不重叠 | `SPAWN REPRO` |
| [PatrolSpawnSafetyChecksResume-resume-final.log](logs/PatrolSpawnSafetyChecksResume-resume-final.log) | 1230 | 出生安全续测：双入口被挡等待、入口切换、不提前清波 | `SPAWN SAFETY` |
| [PatrolEndgameChecks-resume-final.log](logs/PatrolEndgameChecks-resume-final.log) | 2082 | 51/61/71/81 波血量与双精英节奏 | `ENDGAME PASS` |
| [PatrolRouteChecks-resume-final.log](logs/PatrolRouteChecks-resume-final.log) | 1353 | 委托锁定/解锁 → 简报 → 首波 → 强化 → 绿灯门撤离结算 | `ROUTE PASS` |
| [PatrolMeleeChecks-resume-final.log](logs/PatrolMeleeChecks-resume-final.log) | 1613 | 短棍近战命中、血迹残留、三击击倒 | `MELEE PASS` |
| [PatrolChargeResumeChecks-resume-final.log](logs/PatrolChargeResumeChecks-resume-final.log) | 1278 | 精英 windup→charge、20 波扣 1 格 / 25 波扣 2 格 | `CHARGE RESUME PASS` |
| [PatrolSideResumeChecks-resume-final.log](logs/PatrolSideResumeChecks-resume-final.log) | 1239 | 侧门预警、限流、入口回退 | `SIDE RESUME PASS` |

## 结论汇总

- 案例一（头部命中）：相同 45 组条件下，修复前 42 次爆头 / 1 次身体 / 2 次未中（`pass=42 fail=3`），修复后 45/45 次爆头（`pass=45 fail=0`）。
- 案例二（精英出生）：修复前两只精英生成点重合（`bossDistance=0.0000 overlap=True`），修复后间距 13 米、不重叠（`bossDistance=13.0000 overlap=False`）。
- 其余 7 份日志覆盖受击区随动画更新、51/61/71/81 波血量与精英节奏、委托与撤离主流程、近战命中与血迹、精英冲撞、侧门入场。

## 局限

- 测试副本未入库：它带着被改过的场景布局、调试入口和与本机路径绑定的依赖，直接搬进主工程会污染仓库；这里归档的是结论日志与检查脚本代码。
- `scripts/` 不能直接放进主工程 `Assets/Editor` 编译：脚本用反射读写私有成员，依赖测试副本里被改过的场景与对象名，属于一次性验证工具。
- 自动回归验证的是判定结果，不是手感：连续移动瞄准、头颈交界、多敌人混战、光照与音效仍以人工试玩为准（README 已写明这一边界）。
