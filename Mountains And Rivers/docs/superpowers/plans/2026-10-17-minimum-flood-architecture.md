# 山河未尽最小洪水架构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有 Unity 2022.3 工程中建立可测试的纯 C# 洪水核心，并跑通“载入关卡—布置堤坝—察水预览—源头推进播放—结算—重试”的最小闭环。

**Architecture:** ScriptableObject 只保存静态关卡配置，经验证后转换为纯 C# 不可变定义；纯 C# 核心基于运行状态与玩家方案生成最终状态和波次化记录；Unity 流程层与表现层只协调输入、播放和重试。第一阶段只实现堤坝，不提前实现泄水口、故道、完整存档或正式关卡。

**Tech Stack:** Unity 2022.3 LTS、C#、URP 14、Unity Test Framework 1.1.33、ScriptableObject、EditMode/PlayMode Tests、现有 `RiverWaterAndFlood.shader`。

**Spec:** `E:/GitHub/山河未尽/山河未尽-技术架构说明.md`

## Global Constraints

- 游戏规则核心不得引用 `UnityEngine`。
- Inspector 字段、Tooltip、菜单、错误信息与团队交接注释必须使用中文。
- 洪水使用整数水量与容量；不得引入真实流体、ECS、第三方依赖注入或完整第三方框架。
- ScriptableObject 不得保存本局当前水量、棋子实例或受损状态。
- 察水预览与正式演算必须调用同一个 `FloodSimulator`。
- GameObject、Prefab、Shader、Collider 与动画不得作为玩法结果来源。
- 第一阶段河网为有向无环图；配置环路必须阻止开局。
- 保留现有 `MapGenerator`、`TileData`、`LevelData`、`RiverVisualController` 和白模资产，不做一次性重写。

## Review Focus

- 两条上游同轮进入同一河段时必须先汇总，结果不能依赖集合遍历顺序；Task 3 的测试固定该行为。
- 被堤坝关闭的连接不得接收水，且无备用路径时必须生成可解释的回积或破坏记录；Task 4 的测试固定该行为。
- 预览后修改方案必须使缓存失效；未修改方案时正式触发必须复用相同结果；Task 5 的测试固定该行为。
- 河网环路、重复编号、缺少出口和无效引用必须阻止关卡启动；Task 2 的测试固定该行为。
- 播放取消或重试不能留下半淹没材质和旧协程；Task 6 与 Task 7 的 PlayMode 测试固定该行为。

---

### Task 1: 建立程序集边界与核心数据模型

**Files:**
- Create: `Assets/Script/Core/Shanhe.Core.asmdef`
- Create: `Assets/Script/Core/Model/LevelDefinition.cs`
- Create: `Assets/Script/Core/Model/RuntimeState.cs`
- Create: `Assets/Script/Core/Model/PlayerPlan.cs`
- Create: `Assets/Script/Core/Results/SimulationStep.cs`
- Create: `Assets/Script/Core/Results/SimulationResult.cs`
- Create: `Assets/Script/Tests/EditMode/Shanhe.Core.Tests.asmdef`
- Create: `Assets/Script/Tests/EditMode/CoreModelTests.cs`

**Interfaces:**
- Produces: `LevelDefinition`, `RiverNodeDefinition`, `RiverConnectionDefinition`, `ProtectionTargetDefinition`, `RuntimeState`, `PlayerPlan`, `SimulationStep`, `SimulationResult`。
- Stable IDs: all public core APIs use non-empty `string` IDs validated before construction.

- [ ] **Step 1: 编写失败测试**

新增 `CoreModelTests`，断言运行状态由定义创建后可以独立修改，而原始 `LevelDefinition` 保持不变；空编号和负容量构造失败。

- [ ] **Step 2: 运行测试并确认失败**

Run: Unity Test Runner → EditMode → `CoreModelTests`
Expected: FAIL，核心类型尚不存在。

- [ ] **Step 3: 实现最小核心类型**

关键签名：

```csharp
public sealed class LevelDefinition
{
    public RuntimeState CreateInitialState();
}

public sealed class RuntimeState
{
    public RuntimeState Clone();
}

public sealed class SimulationResult
{
    public RuntimeState FinalState { get; }
    public IReadOnlyList<SimulationStep> Steps { get; }
}
```

- [ ] **Step 4: 运行测试并确认通过**

Run: Unity Test Runner → EditMode → `CoreModelTests`
Expected: PASS。

- [ ] **Step 5: 提交本任务**

Commit suggestion: `feat: establish deterministic flood core models`

### Task 2: 扩展关卡配置、转换与验证

**Files:**
- Create: `Assets/Script/Level/Shanhe.Level.asmdef`
- Move: `Assets/Script/Map/LevelData.cs` → `Assets/Script/Level/Definitions/LevelData.cs`（连同 `.meta`）
- Move: `Assets/Script/Map/TileData.cs` → `Assets/Script/Level/Definitions/TileData.cs`（连同 `.meta`）
- Move: `Assets/Script/Map/TileDefinition.cs` → `Assets/Script/Level/Definitions/TileDefinition.cs`（连同 `.meta`）
- Create: `Assets/Script/Level/RiverNodeData.cs`
- Create: `Assets/Script/Level/RiverConnectionData.cs`
- Create: `Assets/Script/Level/ProtectionTargetData.cs`
- Create: `Assets/Script/Level/InteractionPointData.cs`
- Create: `Assets/Script/Level/LevelDataConverter.cs`
- Create: `Assets/Script/Level/LevelDataValidator.cs`
- Create: `Assets/Script/Tests/EditMode/LevelDataValidationTests.cs`

**Interfaces:**
- Consumes: Task 1 `LevelDefinition` and node/connection definitions.
- Produces: `LevelValidationResult LevelDataValidator.Validate(LevelData source)` and `LevelDefinition LevelDataConverter.Convert(LevelData source)`。

- [ ] **Step 1: 编写配置验证失败测试**

覆盖重复编号、缺少入口、缺少出口、连接引用不存在节点、负容量、不可达出口和河网环路。

- [ ] **Step 2: 运行测试并确认失败**

Run: Unity Test Runner → EditMode → `LevelDataValidationTests`
Expected: FAIL，验证器尚不存在。

- [ ] **Step 3: 扩展 ScriptableObject 配置并实现验证器**

保留当前地图宽度、长度和地块列表；新增河道节点、连接、目标与交互点列表。所有新增字段添加中文 Tooltip。验证结果区分阻止开局的错误与可继续的警告。

- [ ] **Step 4: 实现数据转换**

`Convert` 只接受通过验证的数据；转换后不得保留 ScriptableObject 或 GameObject 引用。

- [ ] **Step 5: 运行全部 EditMode 测试**

Run: Unity Test Runner → EditMode
Expected: Task 1–2 tests PASS。

- [ ] **Step 6: 提交本任务**

Commit suggestion: `feat: add validated level graph authoring data`

### Task 3: 实现确定性洪水演算与波次记录

**Files:**
- Create: `Assets/Script/Core/Simulation/IFloodRoutingPolicy.cs`
- Create: `Assets/Script/Core/Simulation/FloodSimulator.cs`
- Create: `Assets/Script/Core/Simulation/ExplicitPriorityRoutingPolicy.cs`
- Create: `Assets/Script/Core/Simulation/WaterConservationValidator.cs`
- Create: `Assets/Script/Tests/EditMode/FloodSimulatorTests.cs`

**Interfaces:**
- Consumes: `LevelDefinition`, `RuntimeState`, `PlayerPlan`。
- Produces: `SimulationResult FloodSimulator.Run(LevelDefinition level, RuntimeState initialState, PlayerPlan plan, IFloodRoutingPolicy routingPolicy)`。
- `IFloodRoutingPolicy` produces a stable ordered list of candidate connection IDs; equal scores use stable IDs.

- [ ] **Step 1: 编写最小直线河道失败测试**

入口水量 4、河段容量 4、可达出口；断言最终流出 4、无损坏，并按波次产生入口、河段、出口记录。

- [ ] **Step 2: 编写同轮汇总与守恒失败测试**

两条上游分别输入 2 和 3，共同进入容量 4 的河段；断言先汇总为 5、接收 4、超载 1，且总水量守恒。

- [ ] **Step 3: 运行测试并确认失败**

Run: Unity Test Runner → EditMode → `FloodSimulatorTests`
Expected: FAIL，演算器尚不存在。

- [ ] **Step 4: 实现波次队列演算**

每轮先按目标节点汇总，再按稳定编号处理。不得使用字典枚举顺序作为结果顺序。每条记录写明波次、并行组、前后状态、水量与原因代码。

- [ ] **Step 5: 实现水量守恒验证**

演算结束验证“输入水量＝出口流出＋仍滞留水量＋被明确计入破坏结算的水量”；不满足时返回核心错误而不是播放结果。

- [ ] **Step 6: 运行全部 EditMode 测试**

Run: Unity Test Runner → EditMode
Expected: Task 1–3 tests PASS，重复运行结果顺序一致。

- [ ] **Step 7: 提交本任务**

Commit suggestion: `feat: implement wave-based flood simulation`

### Task 4: 实现堤坝命令与方案重建

**Files:**
- Create: `Assets/Script/Core/Commands/GameCommand.cs`
- Create: `Assets/Script/Core/Commands/PlaceDamCommand.cs`
- Create: `Assets/Script/Core/Commands/RemoveDamCommand.cs`
- Create: `Assets/Script/Core/Commands/CommandResult.cs`
- Create: `Assets/Script/Core/Commands/PlanBuilder.cs`
- Create: `Assets/Script/Tests/EditMode/DamCommandTests.cs`

**Interfaces:**
- Consumes: initial `RuntimeState` and ordered valid commands.
- Produces: `CommandResult PlanBuilder.TryApply(GameCommand command)` and `RuntimeState PlanBuilder.Rebuild()`。

- [ ] **Step 1: 编写命令验证失败测试**

覆盖合法筑堤、非法交互点、棋子不足、重复筑堤、移除后返还棋子，以及被阻断连接不接收水。

- [ ] **Step 2: 运行测试并确认失败**

Run: Unity Test Runner → EditMode → `DamCommandTests`
Expected: FAIL，命令类型尚不存在。

- [ ] **Step 3: 实现原子命令与中文拒绝原因**

失败命令不得留下部分修改。移除命令后，从初始状态重新应用剩余有效命令。

- [ ] **Step 4: 运行全部 EditMode 测试**

Run: Unity Test Runner → EditMode
Expected: PASS。

- [ ] **Step 5: 提交本任务**

Commit suggestion: `feat: add dam planning commands`

### Task 5: 实现关卡会话、察水缓存与快速重试

**Files:**
- Create: `Assets/Script/Gameplay/Shanhe.Gameplay.asmdef`
- Create: `Assets/Script/Gameplay/Session/GamePhase.cs`
- Create: `Assets/Script/Gameplay/Session/GameSession.cs`
- Create: `Assets/Script/Gameplay/Session/PreviewCache.cs`
- Create: `Assets/Script/Gameplay/Retry/RetryService.cs`
- Create: `Assets/Script/Tests/EditMode/GameSessionTests.cs`

**Interfaces:**
- Consumes: validated `LevelDefinition`, `FloodSimulator`, routing policy and player commands.
- Produces: `CommandResult TrySubmit(GameCommand command)`, `SimulationResult Preview()`, `SimulationResult ConfirmAndResolve()`, `void Retry()`。

- [ ] **Step 1: 编写状态与预览失败测试**

断言只有布置阶段接受命令；预览不修改真实状态；方案改变使缓存失效；方案未改变时正式触发返回同一结果内容；演算阶段拒绝命令；重试恢复初始状态。

- [ ] **Step 2: 运行测试并确认失败**

Run: Unity Test Runner → EditMode → `GameSessionTests`
Expected: FAIL，会话尚不存在。

- [ ] **Step 3: 实现四阶段会话**

阶段限定为载入、布置、演算播放、结算。预览指纹由有序命令内容生成，不使用对象引用或运行时哈希码。

- [ ] **Step 4: 实现重试**

重试丢弃运行状态与预览缓存，从不可变定义创建初态；第一阶段默认清空上次方案。

- [ ] **Step 5: 运行全部 EditMode 测试**

Run: Unity Test Runner → EditMode
Expected: PASS。

- [ ] **Step 6: 提交本任务**

Commit suggestion: `feat: add level session preview and retry flow`

### Task 6: 扩展河道 Shader 与波次播放

**Files:**
- Create: `Assets/Script/Presentation/Shanhe.Presentation.asmdef`
- Modify: `Assets/Art/Effects/River/RiverWaterAndFlood.shader`
- Move: `Assets/Art/Effects/River/RiverVisualController.cs` → `Assets/Script/Presentation/River/RiverVisualController.cs`（连同 `.meta`，保持 Prefab 引用）
- Modify: `Assets/Art/Effects/River/README.md`
- Create: `Assets/Script/Presentation/River/RiverSegmentView.cs`
- Create: `Assets/Script/Presentation/River/FloodPlaybackController.cs`
- Create: `Assets/Script/Tests/PlayMode/Shanhe.PlayMode.Tests.asmdef`
- Create: `Assets/Script/Tests/PlayMode/FloodPlaybackTests.cs`

**Interfaces:**
- Consumes: ordered `SimulationStep` list and stable connection-to-view bindings.
- Produces: `IEnumerator Play(IReadOnlyList<SimulationStep> steps)`, `void CancelAndReset()`。
- `RiverVisualController` adds public setters for 到达进度、承压程度、漫溢程度、浑浊程度 and `恢复初始表现()`。

- [ ] **Step 1: 编写播放取消与复位失败测试**

创建两段测试河道，开始播放后调用 `CancelAndReset()`；断言所有段落回到初始材质参数且没有后续步骤继续执行。

- [ ] **Step 2: 运行测试并确认失败**

Run: Unity Test Runner → PlayMode → `FloodPlaybackTests`
Expected: FAIL，播放控制器和新参数尚不存在。

- [ ] **Step 3: 为 Shader 增加水头推进遮罩与到达参数**

保留当前 RGBA 控制图语义；新增独立单通道推进遮罩和中文 Inspector 名称。到达参数只控制洪水状态沿河道纵向出现，漫溢继续由原有范围控制。

- [ ] **Step 4: 实现波次播放控制器**

同波并行播放，等待该波关键动画完成后进入下一波。未找到绑定视图时记录中文错误并中止本次播放，不能静默跳过。

- [ ] **Step 5: 更新河道使用说明**

记录推进遮罩黑白方向、纹理导入设置、直线与转弯验收方法，以及到达／承压／漫溢三个参数的职责。

- [ ] **Step 6: 运行 PlayMode 测试并目视检查当前直线河道**

Expected: 水头从上游推进到下游；取消或重试后完全复位；漫溢仍由河心向两岸扩张。

- [ ] **Step 7: 提交本任务**

Commit suggestion: `feat: play flood simulation from source by waves`

### Task 7: 接入现有白模地图并跑通最小闭环

**Files:**
- Move: `Assets/Script/Map/MapGenerator.cs` → `Assets/Script/Presentation/Map/MapGenerator.cs`（连同 `.meta`）
- Move: `Assets/Script/Map/WhiteboxTileColor.cs` → `Assets/Script/Presentation/Map/WhiteboxTileColor.cs`（连同 `.meta`）
- Move: `Assets/Script/Map/OrthographicMapCameraController.cs` → `Assets/Script/Presentation/Map/OrthographicMapCameraController.cs`（连同 `.meta`）
- Create: `Assets/Script/Editor/Shanhe.Editor.asmdef`
- Move: `Assets/Script/Map/Editor/WhiteboxSampleCreator.cs` → `Assets/Script/Editor/Map/WhiteboxSampleCreator.cs`（连同 `.meta`）
- Move: `Assets/Script/Map/Editor/MapGeneratorEditor.cs` → `Assets/Script/Editor/Map/MapGeneratorEditor.cs`（连同 `.meta`）
- Create: `Assets/Script/Presentation/Session/GameSessionController.cs`
- Create: `Assets/Script/Presentation/Interaction/InteractionPointView.cs`
- Modify: `Assets/Scenes/WhiteboxMapSample.unity`
- Create: `Assets/Script/Tests/PlayMode/MinimumFloodLoopTests.cs`

**Interfaces:**
- Consumes: Tasks 2–6 public APIs.
- Produces: one playable whitebox scene demonstrating load, place dam, preview, confirm, source-progress animation, result and retry.

- [ ] **Step 1: 编写场景烟雾失败测试**

加载 `WhiteboxMapSample`，断言存在关卡会话、入口、出口、至少一处分叉、一个筑堤点和所有逻辑连接的视觉绑定。

- [ ] **Step 2: 运行测试并确认失败**

Run: Unity Test Runner → PlayMode → `MinimumFloodLoopTests`
Expected: FAIL，场景尚未接入会话。

- [ ] **Step 3: 接入会话与交互点**

移动现有脚本时必须连同 `.meta` 文件一起移动，以保留场景和 Prefab 引用；暂时保留原命名空间，避免无关序列化迁移。`MapGenerator` 继续生成地块和视觉内容；逻辑状态由 `GameSession` 持有。`InteractionPointView` 只提交稳定编号，命令成功后才更新棋子表现。

- [ ] **Step 4: 建立最小分叉测试数据**

包含一个入口、至少三段河道、一处分叉、一个出口、一个保护点和一个筑堤点；数值只用于验证架构，不作为最终第一关数值。

- [ ] **Step 5: 跑通完整流程**

手动验收：放置堤坝 → 察水 → 确认 → 水头从源头推进 → 改道或破坏 → 结算 → 重试复位。

- [ ] **Step 6: 运行全部 EditMode 与 PlayMode 测试**

Expected: 全部通过；Console 无新增错误；中文诊断能定位所有故意制造的非法配置。

- [ ] **Step 7: 提交本任务**

Commit suggestion: `feat: complete minimum flood prototype loop`

## Deferred Follow-up Plans

以下内容不属于本计划，需在最小闭环验收后分别规划：

1. 路径权重候选对比与最终规则；
2. 泄水口命令和自动泄水；
3. 故道发现、挖掘与整体激活；
4. 白模／正式表现方案资产化与河道转弯、分叉Prefab；
5. 玩家JSON进度、选关和剧情跳过；
6. 完整五关、正式美术、音效和剧情演出。
