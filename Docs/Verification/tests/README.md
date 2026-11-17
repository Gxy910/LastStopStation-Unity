# EditMode 自动化测试

这一层是为了把「数值 / 波次 / 武器公式」从场景里剥出来，做成不打开场景也能跑的回归测试。之前这些数字全靠试玩，现在改动后一条命令就能验证没有回退。

## 程序集分层

| 程序集 | 位置 | 说明 |
|---|---|---|
| `YuDian.Runtime` | `Assets/Scripts/YuDian.Runtime.asmdef` | 全部玩法代码。**不引用 UnityEditor**，运行时只通过 `EditorHooks` 回调编辑器能力 |
| `YuDian.Editor` | `Assets/Editor/YuDian.Editor.asmdef` | 只在 Editor 编译：关卡菜单、调试绘制、`EditorHooksInstaller` 注入 |
| `YuDian.Tests.EditMode` | `Assets/Tests/EditMode/YuDian.Tests.EditMode.asmdef` | EditMode 测试，引用 NUnit 与两个 TestRunner 程序集 |

解耦方式：`Assets/Scripts/EditorHooks.cs` 定义 `ExitPlayMode` / `DebugDrawEnabled` / `DrawSceneLabel` 三个静态委托，Editor 侧的 `EditorHooksInstaller`（`[InitializeOnLoad]`）在编辑器启动时注入实现。这样 Runtime 程序集里不再出现 `UnityEditor` 调用，打包也不会把编辑器代码带进去。

测试要读内部的数学函数，所以 `Assets/Scripts/AssemblyInfo.cs` 用 `InternalsVisibleTo` 对 Editor 与测试程序集开放，相关函数改为 `internal static`：`PatrolBalance.Health/EliteHealth`、`PatrolMission.WaveComposition/IsDoubleEliteWave`、`PatrolWeapon.HeadshotDamage/RifleShotPitch`。

## 覆盖范围（13 个用例）

- **YuDian.Tests.PatrolBalanceTests**（5）：`EliteHealth_HitsInterpolationAnchors`、`Health_NeverDecreases_FromWave1To150`、`InvalidWave_ClampsToFirstWave`、`LateWaves_UsePublishedStageTable`、`WaveOne_MatchesOpeningTable`
- **YuDian.Tests.PatrolWavePlanTests**（5）：`ChargerWaves_AreEveryFifthWave`、`DoubleEliteStartsAtWave80_Every25Waves`、`OpeningWaves_FollowTutorialTable`、`Wave51_Has28Enemies_AsAdvertised`、`WavesAfter51_AddOneEveryFour`
- **YuDian.Tests.WeaponMathTests**（3）：`HeadshotDamage_AlwaysExceedsBodyDamage`、`HeadshotDamage_IsOnePointSevenFiveTimes_RoundedUp`、`RifleShotPitch_RisesOverFinalFiveRounds`

对应 README 里的公开数字：51 波 28 只、第 80 波起每 25 波双精英、爆头伤害 `(Damage*7+3)/4`、1–150 波血量不回退。

## 运行方式

Unity 里：`Window > General > Test Runner` → `EditMode` → `Run All`。

命令行（把路径换成自己机器的 Unity 安装位置）：

```
Unity.exe -batchmode -nographics -projectPath <工程根目录> ^
  -runTests -testPlatform EditMode ^
  -testResults Docs/Verification/tests/EditMode-results.xml
```

注意：命令行跑测试要求这个工程没有被 Unity 编辑器实例占用（否则会报 `another Unity instance is running with this project open`）。本次结果是在一份独立的工程副本上跑的，代码与配置和仓库一致，目的是不打断正在打开的工程。

## 最近一次结果

2026-11-17，**13/13 通过**（耗时约 0.08 秒）。

- [EditMode-results.xml](EditMode-results.xml)：NUnit 结果 XML，`<test-run ... passed="13" failed="0">`
- [EditMode-run.log](EditMode-run.log)：Unity 批处理日志，可搜 `Executing tests with settings: test mode = EditMode`

## 局限

只覆盖纯计算逻辑。场景引用、动画事件、出生时序、手感和性能不在这一层，仍由 `Docs/Verification/logs` 里的自动检查日志和人工试玩负责。测试也不能替代「改动后在 Unity 里点一遍」。
