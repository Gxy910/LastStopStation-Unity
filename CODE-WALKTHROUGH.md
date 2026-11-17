# 《末日车站》代码讲解卡

《末日车站》（仓库展示名《末日车站》）是一个 Unity / C# 单人第一人称叙事生存 Demo。玩家在灾后车站通过对话和道具推进主线，陪阿岚前往站外接送点，并根据选择进入不同结局。修好通讯装置后，可以在车站路哨岗亭接取货场清障委托，使用枪械和近战武器挑战无尽波次，再带着已清空的波次撤离。

工程使用 Unity 2022.3.62f2c1、C#、Unity 物理查询与角色控制器。Build Settings 中启用的入口是 [OldStation.unity](Assets/Scenes/OldStation.unity)。在 Unity 中打开项目与该场景后点击 Play，完整操作和主线入口见 [README](README.md)。

这份文档按玩家能走到的流程讲解当前主工程。代码片段截取自仓库，部分省略与问题无关的语句。记录中的自动检查在独立测试副本运行，测试工具和日志位于开发工作区，当前没有随主工程仓库提交。自动检查覆盖指定条件，实机手感仍需玩家确认。

## 讲解卡索引

| 编号  | 模块         | 对应代码                                                                                                                                                                     | 面试价值               |
| --- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------ |
| 001 | 移动中的头部漏判   | [PatrolEnemy](Assets/Scripts/Patrol/PatrolEnemy.cs)、[AssetAnimationPlayer](Assets/Scripts/AssetAnimationPlayer.cs)、[PatrolWeapon](Assets/Scripts/Patrol/PatrolWeapon.cs) | 复现、骨骼坐标、更新时序与物理查询  |
| 002 | 无尽波次与安全刷怪  | [PatrolMission](Assets/Scripts/Patrol/PatrolMission.cs)、[PatrolYard](Assets/Scripts/Patrol/PatrolYard.cs)                                                                | 队列调度、边界条件和不变量      |
| 003 | 精英冲撞与战斗预警  | [PatrolEnemy](Assets/Scripts/Patrol/PatrolEnemy.cs)、[PatrolMission](Assets/Scripts/Patrol/PatrolMission.cs)                                                              | 状态阶段、碰撞与玩家可读性      |
| 004 | 剧情任务与场景切换  | [StationStory](Assets/Scripts/StationStory.cs)、[StationPrototype](Assets/Scripts/StationPrototype.cs)、[EscortEncounter](Assets/Scripts/EscortEncounter.cs)               | C# 状态管理、回调、玩法与主线衔接 |
| 005 | 战斗状态、撤离与纪录 | [PatrolMission](Assets/Scripts/Patrol/PatrolMission.cs)、[PatrolWeapon](Assets/Scripts/Patrol/PatrolWeapon.cs)                                                            | 生命周期、临时状态和持久化边界    |

---

## 001 · 移动中的头部漏判

### 一、解决什么问题

玩家瞄准奔跑感染者的头部开枪时，部分射击被判成身体命中或打空。问题会影响伤害、爆头提示，也会影响第 51 波后按爆头击倒次数发放的补给。

**复现条件。** 测试副本加载货场，从 2、6、12 米三个距离，分别对快怪跑步动画的 5 个固定采样时刻、每个时刻 3 个头部参考点发射射线，共 45 组。测试脚本对动画手动采样并让相机朝向参考点，属于可重复的自动检查。

**观察到的证据。** 旧版本在这 45 组里有 42 次爆头、1 次身体命中、2 次未命中。失败均出现在采样时刻 0.45 秒、横向偏移 0.18 的参考点，记录的该参考点到受击球中心距离为 0.428。修改后同一组条件记录为 45 次爆头。

**确认的原因。** 当时头部受击球相对敌人根对象固定，跑步动画却在移动头骨。模型头部移动时，球心没有跟随动作，导致某些参考点落在旧受击区之外。该证据能解释这组复现，不能据此断言游戏里每一次没打中都由同一个原因造成。

### 二、关键决策

#### 1. 记录球心相对头骨的偏移，动画更新后重算位置

[PatrolEnemy.Initialize](Assets/Scripts/Patrol/PatrolEnemy.cs) 找到名为 `Head` 的骨骼，先把球心转成头骨局部坐标。动画姿势更新时再转换回世界坐标：

```csharp
headBoneOffset = headBone.InverseTransformPoint(head.transform.position);
animationPlayer.PoseUpdated += SyncHeadHitBox;

// SyncHeadHitBox
headHitBox.transform.position = headBone.TransformPoint(headBoneOffset);
```

可以把 `headBoneOffset` 理解成“球心相对头骨的位置”。头骨摆动后，这个局部偏移仍指向相同的附着位置。这样保留原有受击区的设置，同时让球心跟随动画。

代价是依赖模型存在名称为 `Head` 的骨骼。找不到骨骼时，代码保留初始化时的受击球位置，不会获得跟随效果。更换模型时必须检查骨骼名、缩放和球体覆盖范围。

#### 2. 把动画姿势与物理查询衔接起来

[AssetAnimationPlayer](Assets/Scripts/AssetAnimationPlayer.cs) 采样动画后触发 `PoseUpdated`，`PatrolEnemy` 在订阅回调里更新球的位置。它的 `DefaultExecutionOrder(-100)` 让该组件较早更新。武器查询前调用 `Physics.SyncTransforms()`，让物理查询读取同步后的变换。

```csharp
current.SampleAnimation(model, time);
model.transform.localRotation = Quaternion.Euler(lean, 0, 0) * baseRotation;
PoseUpdated?.Invoke();
```

物理同步有成本，当前没有针对不同设备给出耗时结论。敌人的动画由各自组件更新，而 `PatrolMission.Tick` 也会驱动敌人状态，修改调用顺序时需要重新检查“采样、移动、射击”之间的关系。

#### 3. 准星从相机中心发射，按实际遮挡决定命中

[PatrolWeapon.Attack](Assets/Scripts/Patrol/PatrolWeapon.cs) 使用相机画面中心建立射线，枪械用 `RaycastAll`，再选最近的有效碰撞体。身体和头部使用不同受击区，只有命中该敌人的 `headHitBox` 才算爆头。

```csharp
Ray aim = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
Physics.SyncTransforms();
RaycastHit[] hits = Physics.RaycastAll(
    aim, 65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
```

筛选最近碰撞时，代码跳过玩家自身以及无效的触发器，墙、货箱和前方敌人可以挡住射线。对于受击触发区伸到墙外的情况，代码还向受击区中心方向做一次实体遮挡查询。

爆头伤害按 `(Damage * 7 + 3) / 4` 的整数形式实现 1.75 倍向上取整。血浆位置使用本次查询的 `nearest.point`。射线在本次调用 `AddRecoil` 之前创建，后坐力对相机姿势的后续影响仍需结合实际手感验证。

### 三、代码地图

```text
玩家左键
  → StationPrototype.Update → PatrolMission.Tick
  → PatrolWeapon.Attack
      → 相机中心射线 → Physics.SyncTransforms → 查找最近有效命中
      → 检查实体遮挡 → PatrolEnemy.IsHeadHit
      → 计算伤害 → TakeHit → 血浆 / 命中与爆头反馈
动画更新
  → AssetAnimationPlayer.Update / Play → SampleAnimation → PoseUpdated
  → PatrolEnemy.SyncHeadHitBox → 根据 Head 骨骼更新球心
```

### 四、面试会问

**Q：为什么要保存局部偏移？**  
答：初始化时先记住球心在头骨坐标系里的位置。动画改变头骨位置和旋转后，用同一个局部偏移换回世界坐标，可以保持球心相对头骨的位置。

**Q：为什么命中检测还要处理遮挡？**  
答：一条射线可能碰到多个碰撞体，必须先判断最近的有效对象。头部触发球也可能部分伸进墙里，所以代码还检查受击区中心方向上是否有实体障碍。

**Q：怎么判断没有把肩膀大量算成头部？**  
答：查看运行时红色头部受击球与模型的相对位置，针对脖子、肩膀和两者交界处，从不同方向分别射击，记录应当得到身体命中还是爆头。现有自动记录检查了躯干身体伤害，但没有完整覆盖肩部边界，因此我会把肩部误判列为需要补测的项目。

**Q：玩家移动时也测过吗？**  
答：自动回归在移动玩家位置后进行了一次头部射击，并检查伤害。它不能代替玩家持续移动、追瞄和开枪的人工测试。

### 五、验证与已知不足

- 测试副本的 `PatrolHeadMotionRepro` 保存了修复前后结果，固定采样分别为 42/45 和 45/45。该工具以头骨位置及偏移建立参考点，不能单独证明可见网格上的每个像素都被准确覆盖。
- `PatrolHitCoverageChecks` 在普通怪行走的 4 个固定时刻、3 个距离检查头部伤害与血浆，并检查一次身体命中、移动后射击、墙体遮挡和前方敌人遮挡，共记录 28 项通过。
- 需要人工继续检查快怪连续跑动、精英蓄力和冲撞、肩颈交界、玩家与目标同时移动，以及部分遮挡时的实际体验。无法从这些日志推出所有方向均无误判。
- 当前主工程已包含跟随、同步和遮挡代码。上述记录来自测试副本的历史运行，本文编写时没有重新运行这些测试。

### 六、改需求题

> 如果更换感染者模型后找不到 `Head` 骨骼，你会如何定位并支持新模型？

<details>

<summary>展开参考答案</summary>

先在模型层级检查实际头骨名称和动画播放时的 Transform，确认 `CreateAnimatedModel` 返回的模型与搜索范围。给模型配置可验证的骨骼映射，并在缺失时提示开发者，避免静默认为已经跟随。

然后核对初始化球心的局部偏移与缩放，在静止、行走、奔跑和精英动作采样下检查红色球体。最后重复身体、肩颈边界和障碍遮挡测试。修改绑定方式后要重新审视事件订阅与销毁时的解除订阅。

</details>

---

## 002 · 无尽波次与安全刷怪

### 一、解决什么问题

一波敌人可能分批从两个原装卸口和侧门进入。玩家把当前几只击倒后，如果只检查“场上没有怪”，就可能在下一批还没到时错误结算。高波还有双精英，出生点被玩家或敌人占住时，直接生成会造成重叠或突然贴脸。

### 二、关键决策

[PatrolMission](Assets/Scripts/Patrol/PatrolMission.cs) 同时维护普通入口队列 `pending`、侧门队列 `sidePending` 和第二只精英的待入场标记。三个来源共用 `MaxAlive = 6`。波次结束必须同时满足：

```csharp
if (alive == 0 && PendingCount == 0)
{
    cleared = wave;
    betweenWaves = true;
    upgradeChosen = false;
    int supplied = weapon.Restock(wave);
    // 播放清波反馈并显示下一波
}
```

`PendingCount` 把第二只待入场精英也算进去，防止第一只提前死亡时漏算。第 51 波为 28 只，第 52 波起按代码公式继续增长，双精英从第 80 波起每 25 波出现一次。侧门从第 11 波起参与部分波次，预警后才尝试入场。

`IsSafeSpawn` 先检查与玩家的水平距离，再用 `Physics.OverlapCapsule` 检查出生体积。原入口受阻时 `SafeOriginalLane` 检查另一入口，两边都不可用就保留队列等待。侧门受阻时会预告改从原装卸口增援。

这套方案让“本波计划总数”和“当前同屏数量”分开。代价是入口持续被占用时，敌人会持续等待，玩家需要看提示并腾出位置。

### 三、代码地图

```text
空格开始 → PatrolMission.StartWave
  → WaveComposition 算总数和种类 → 放入队列 / 标记第二只精英
StationPrototype.Update → PatrolMission.Tick
  → Tick 已入场敌人 → AliveCount → 空位和预警检查
  → SpawnNext / SpawnSide / SpawnSecondElite
      → IsSafeSpawn → Spawn → PatrolEnemy.Initialize
  → 场内无活敌且所有待入场数量为 0 → 清波、补给、强化
```

### 四、面试会问

**Q：为什么同时需要队列和存活敌人列表？**  
答：队列记录尚未进入的敌人，列表记录已经生成的对象。清波和同屏限制要用到的数量不同，不能混成一个值。

**Q：先出队再检查入口可以吗？**  
答：先出队后如果没有安全位置，还得可靠地把敌人放回去。现在先 `Peek` 检查，确认安全才 `Dequeue`，等待时计划不会丢失。

**Q：如果两只精英同时在场，会不会超过 6 只？**  
答：它们也计入 `AliveCount`，下一次调度只有在存活数小于上限时才生成。第二只精英等待时也计入 `PendingCount`。

### 五、验证与已知不足

测试副本的高波检查记录了第 80、105 波的待入场精英、同屏上限和同时冲撞条件。出生安全回归记录了双入口阻塞后等待、等待时不清波、可用原入口生成、精英不重叠以及避开玩家附近。检查通过不代表所有货箱布局、极端玩家位置和高波性能都已穷举。

当前使用 `new GameObject` 创建敌人，`ClearEnemies` 中销毁对象。是否需要对象池应先通过 Profiler 实测；现有代码不能说明已经使用对象池或取得了某个性能提升。

### 六、改需求题

> 精英和普通怪改用不同的同屏配额，但总数仍不能超过 6 只，应改哪里？

<details>

<summary>展开参考答案</summary>

在 `PatrolMission` 统计活着的精英和普通 / 快怪数量，并在三个入场路径共用的调度条件中检查对应配额与总上限。保留 `PendingCount` 和“活敌为零且等待为零”的清波条件。

还需设计一种可复现的阻塞场景，例如精英配额满但普通怪可入场时，避免队首精英永久挡住后续普通怪。可分队列或显式选择可入场项，并测试双精英、侧门、所有入口受阻及波次总数守恒。

</details>

---

## 003 · 精英冲撞与战斗预警

### 一、解决什么问题

精英需要让玩家看懂“什么时候该躲，什么时候可以反击”。后期一波可能有两只精英，伤害和预警应由各自状态驱动，同时受同屏规则约束。

### 二、关键决策

[PatrolEnemy](Assets/Scripts/Patrol/PatrolEnemy.cs) 使用 `ChargePhase` 表达 `Approach → Windup → Charge → Recover`。接近玩家且路线可用后，记录冲撞方向并进入蓄力，地面显示带箭头的红线。蓄力结束后隐藏红线，沿记录的方向移动，结束或受阻后进入恢复。

```csharp
chargeDirection = delta.sqrMagnitude > .01f
    ? delta.normalized : transform.forward;
chargePhase = ChargePhase.Windup;
chargeTimer = 1.5f;
chargeMarker.SetActive(true);
```

冲撞期间代码检查从上一位置到当前位置的线段是否接近玩家，并检查精英与玩家之间的实体遮挡。这样能处理一帧移动一段距离的情况。命中后回调 `PatrolMission.EnemyDamagePlayer`，由任务统一处理防弹衣、生命和短暂无敌时间。

`PatrolMission.Draw` 为场内每只精英画生命条，`DrawOffscreenWarnings` 根据视口位置在屏幕左右提示视野外蓄力。两只精英各自维护状态，因此可以同时蓄力和冲撞。

### 三、代码地图

```text
StartWave / Spawn → PatrolEnemy.Initialize
  → 保存伤害回调、建立角色控制器与红色地面预警
PatrolMission.Tick → 每只 PatrolEnemy.Tick → TickCharger
  → 接近 → 蓄力与红线 → 冲撞与路径检查 → 恢复
  → damagePlayer(attackDamage) → EnemyDamagePlayer → 护甲 / 生命 / 受伤反馈
PatrolMission.Draw → 精英生命条与视野外方向提示
```

### 四、面试会问

**Q：为什么冲撞时要检查一段线，而只判断当前位置可能遗漏什么？**  
答：精英一帧可能从玩家一侧移动到另一侧。只检查终点距离可能跳过中间经过的位置，线段接近判断可以覆盖这段移动轨迹。

**Q：为什么要单独检查遮挡？**  
答：平面距离接近不能说明中间没有墙。代码还用实体射线检查通路，避免隔着障碍伤害玩家。

**Q：两只精英同一时间命中怎么办？**  
答：每只精英都可以发起冲撞，但玩家受伤逻辑有共享的短暂无敌时间，首次伤害之后立即到来的伤害会被忽略。防弹衣抵挡的是一次完整伤害。

### 五、验证与已知不足

测试副本的高波检查记录了两只精英共用上限、允许同时冲撞、第 5 波与第 25 波伤害、防弹衣以及无敌时间。红线、屏幕边缘提示在不同视角下是否清楚，以及双精英和货箱交汇时的可玩性，需要人工试玩确认。

当前屏幕外提示按左右方向合并，不给每只精英单独编号。场上精英数量增加时，提示信息密度需要重新设计。

### 六、改需求题

> 如果冲撞开始前玩家躲到墙后，怎样保证精英不会穿墙伤人，也不会一直卡在冲撞状态？

<details>

<summary>展开参考答案</summary>

先检查 `TickCharger` 的 `CharacterController.Move`、实际位移和 `blockedTime`，再检查 `ChargePathClear` 是否正确忽略自身和目标，同时保留墙体检测。阻塞达到阈值后进入 `Recover`，保证状态能继续推进。

测试时分别覆盖贴墙、隔墙、斜向擦墙和两只精英同时靠墙。还要确认红线关闭、恢复动作开始、玩家生命不被错误扣减。

</details>

---

## 004 · 剧情任务与场景切换

### 一、解决什么问题

玩家从修通讯、取道具、签接送合同走到站外，最后根据接送事件结果回到车站。不同阶段看同一个角色，得到的目标和对白不同。剧情还要允许玩家在合适时机进入货场，返回后继续当前主线。

### 二、关键决策

[StationStory](Assets/Scripts/StationStory.cs) 用 `Step` 枚举与 `CurrentStep` 表达任务进度，另保存章节、通讯是否修好、接送结局和武士刀等状态。`Interact(StationTarget)` 按交互对象分发，例如交回工具后设置：

```csharp
DeviceRepaired = true;
CurrentStep = Step.ContinueToNextDay;
```

`CanTakePatrol` 根据通讯状态、章节和任务完成状态计算可否接单。当前规则允许修好通讯后在第一、第二和第四章从车站岗亭接取，第三章是接送点剧情。

[StationPrototype](Assets/Scripts/StationPrototype.cs) 负责 Unity 输入、交互焦点、对白和场景对象切换。它在 `BeginChapter` 中按章节切换车站与接送区域，并调用 `FirstPersonWalker.MoveTo` 移动同一个玩家。第三章由 [EscortEncounter](Assets/Scripts/EscortEncounter.cs) 管理局部事件，结束时通过回调交回 `EscortEnding`，再由 `StationStory.FinishEscort` 更新主线和奖励。

这样可以单独理解“剧情到了哪一步”和“这一帧如何接收输入”。代价是剧情文本和条件分支目前集中在代码里，添加大量任务时会增加维护成本。

### 三、代码地图

```text
玩家看向对象按 E
  → StationPrototype.UpdateFocus / InteractFocused
  → StationStory.Interact(目标) → 条件分支更新 CurrentStep → 返回对白
  → RefreshWorld / BeginChapter → 更新交互物、区域和玩家位置
第三章会合牌 → 对话推进 → EscortEncounter.Begin / Tick
  → EscortEncounter.Finish(ending) → StationPrototype.OnEscortFinished
  → StationStory.FinishEscort → BeginChapter → EpilogueStage 展示结局
```

### 四、面试会问

**Q：为什么任务步骤要单独保存？**  
答：同一个 NPC 在不同任务阶段应该提供不同内容，`CurrentStep` 让交互逻辑可以根据当前进度决定能否推进，并给出对应对白。

**Q：接送事件如何通知剧情已经结束？**  
答：控制器配置事件时传入回调，接送事件结束时带着 `EscortEnding` 调用它。控制器再调用主线的 `FinishEscort` 并切换章节。

**Q：货场是否会把剧情状态重建？**  
答：接单把现有 `story.HasKatana` 传给武器，并由货场保存返回位置。正常退出会恢复车站与位置；任务流程继续使用同一个 `StationStory` 实例。

### 五、验证与已知不足

测试副本的 `KatanaFoleyStationOnlyCheck` 以代码驱动剧情交互，检查了修复通讯前后的接单、第二章可接单、第三章接送点、接送后第四章获得武士刀、带刀接单和返回位置。它验证了脚本驱动的路径，不能代替从开场到第四章的完整人工游玩，也没有覆盖每个对白分支。

### 六、改需求题

> 新增“送灯之前先询问暖棚领班”的任务步骤，要改哪些地方？

<details>

<summary>展开参考答案</summary>

在 `StationStory.Step` 增加步骤，调整阿岚、领班和送灯交互的前置条件与状态转换，再检查当前目标文本、交互提示和对象显隐是否依赖旧步骤。`StationPrototype` 应继续负责展示和输入，把剧情推进规则放在 `StationStory`。

验证正常顺序、提前找孙姨、反复与领班对话，以及中途接货场委托并返回后进度仍在。更新对话和存档兼容策略时，还要考虑是否已有需要保留的持久化任务数据。

</details>

---

## 005 · 战斗状态、撤离与纪录

### 一、解决什么问题

货场允许玩家在波次中途撤离。假如清完第 4 波、在第 5 波撤离，应该记录第 4 波。失败则保留之前成功撤离的最佳成绩。每次接单的枪械升级、血包和护甲属于本次出勤。

### 二、关键决策

`PatrolMission` 分别维护正在进行的 `wave` 与已经清空的 `cleared`。`TryExit` 检查玩家是否靠近绿灯门，再调用 `Finish(true)`。只有成功撤离且 `cleared` 超过旧纪录时，才写入 `PlayerPrefs`：

```csharp
if (evacuated && canRecord && cleared > Best)
{
    PlayerPrefs.SetInt(RecordKey, cleared);
    PlayerPrefs.Save();
}
```

结束时清理敌人、武器显示和本次护甲等状态，恢复车站光照、相机背景与入场前的玩家位置。下次 `Begin` 会重新初始化手枪、生命、波次和临时补给，并根据剧情是否获得武士刀决定该次能否使用它。

第 51 波起，`HandleShot` 在枪械爆头击倒时累计次数，每满 5 次调用 `weapon.AddReserve(4)`，用返回的实际到账数量显示反馈。备用弹上限与波末补给仍由武器逻辑处理。

### 三、代码地图

```text
接委托 → PatrolMission.Begin
  → 保存位置 / 隐藏车站 / 启用货场 → PatrolWeapon.Begin → 教程等待
战斗 → PatrolWeapon.Attack → PatrolMission.HandleShot → 命中反馈 / 爆头补给
清波 → cleared = wave → 武器补给 → 选择强化
靠近绿灯门按 E → TryExit → Finish(true) → 按 cleared 更新最佳纪录
生命耗尽 → 倒地计时 → Finish(false) → 返回车站
```

### 四、面试会问

**Q：为什么不能用当前波数结算？**  
答：当前波可能还没有完成。单独维护 `cleared`，在全部敌人和等待队列清空时更新，紧急撤离就能使用已完成的成绩。

**Q：失败和撤离怎么区分？**  
答：两者都走 `Finish` 清理临时状态，但只有 `evacuated` 为真且成绩更高时才写最佳纪录。测试跳波还用 `debugNoRecord` 防止污染正常纪录。

**Q：为什么补给接口返回实际到账量？**  
答：备用弹有上限。如果只告诉玩家“增加 4 发”，接近上限时显示会不准确。接口返回实际增加数量，界面可以显示真实结果。

### 五、验证与已知不足

测试副本的 `PatrolRouteChecks` 记录了清第一波后正常撤离、第二波紧急撤离按第一波结算、重新接单恢复基础手枪，以及失败保留旧纪录。高波检查还覆盖爆头次数跨波、备弹上限和调试退出不写纪录。

这些检查通过脚本推进状态并触发方法，不能完全代表手动走位到撤离门、受击或断电退出时的体验。当前成绩使用本地 `PlayerPrefs`，代码没有提供跨设备同步或防篡改机制。

### 六、改需求题

> 要增加“成功撤离的最近一次波数”，同时保留最高纪录，该怎么做？

<details>

<summary>展开参考答案</summary>

在 `Finish(true)` 中单独保存最近一次的 `cleared`，最高纪录仍只在新成绩更高时更新。失败不覆盖成功撤离的最近成绩。分别定义两个键、读取与展示路径，并考虑调试跳波时两者都不写入。

验证第一次撤离、低于最高纪录的再次撤离、中途撤离、失败、重新启动和调试模式。需要跨设备同步时，应另外设计持久化方案。

</details>

---

## 证据与阅读顺序

仓库中可以直接阅读上面各卡片链接的主工程代码。开发工作区保存了 `head_before_edges.log`、`head_after_full.log`、`PatrolHitCoverageChecks-resume-final.log`、`PatrolSpawnSafetyChecksResume-resume-final.log`、`PatrolEndgameChecks-resume-final.log`、`PatrolRouteChecks-resume-final.log` 和 `katana-foley-station-only.log`。这些是测试副本的历史记录，未随仓库提交。

建议先读卡 001，跟着实际代码画出“动画采样 → 受击球 → 射线 → 遮挡 → 伤害”的路径。然后读卡 002 和 005，用第 5 波紧急撤离举例，说明敌人队列、已清空波数和最佳纪录分别由谁维护。最后结合卡 003 和 004 讲清精英状态与剧情事件回调。
