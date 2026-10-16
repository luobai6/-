# 白模地图与正交相机实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Unity 编辑状态生成可调整高度的白模地块，并在运行时移动和缩放固定角度正交相机。

**Architecture:** `LevelData` 持有地图与地块数据，`TileDefinition` 持有白模颜色和内容预制体引用；`MapGenerator` 验证数据后只重建自身的 `GeneratedMap`。相机控制组件独立处理旧输入系统的键盘、中键和滚轮。

**Tech Stack:** Unity 2022.3.62f1c1、C#、URP、Unity Editor API。

**Spec:** `docs/superpowers/specs/2026-10-16-whitebox-map-camera-design.md`

## Global Constraints

- Inspector 字段、Tooltip、按钮与团队交接注释使用中文。
- 不固定高度档数，不实现水流寻路、坡河或正式侧壁。
- 不修改现有 `DiKuaiChangShi` 场景或用户制作的河流预制体。
- 生成器不得清除自身 `GeneratedMap` 之外的对象。

## Review Focus

- 无效生成参数：保留旧地图并明确报错。
- 重复与越界坐标：跳过并给出坐标和统计。
- 缺失定义或内容预制体：仍生成可见底座。
- 多个生成器：各自清除自己的结果。
- 非正交相机：只警告一次，不改变投影设置。

---

### Task 1: 数据与生成

**Files:**
- Create: `Assets/Script/Map/TileData.cs`
- Create: `Assets/Script/Map/LevelData.cs`
- Create: `Assets/Script/Map/TileDefinition.cs`
- Create: `Assets/Script/Map/MapGenerator.cs`
- Create: `Assets/Script/Map/Editor/MapGeneratorEditor.cs`

**Interfaces:**
- `TileData`: `Vector2Int 网格坐标`, `int 高度等级`, `TileDefinition 地块定义`。
- `MapGenerator.重新生成(): bool`、`MapGenerator.清除生成结果(): void`。
- `MapGenerator.计算地块顶面高度(int): float` 与 `MapGenerator.计算地块中心(Vector2Int): Vector3` 供编辑器验证。

- [x] 按规格创建可序列化数据、ScriptableObject 与中文 Inspector 字段。
- [x] 完成输入预检、坐标去重、越界与高度检查。
- [x] 生成底座及 `PrimaryContent`，使用 `MaterialPropertyBlock` 应用颜色。
- [x] 通过 Unity Editor 按钮生成与清除；支持 Undo 和场景脏标记。
- [x] 验证无效参数不删除原结果，多个生成器互不影响。

### Task 2: 正交相机

**Files:**
- Create: `Assets/Script/Map/OrthographicMapCameraController.cs`

**Interfaces:**
- `OrthographicMapCameraController.限制缩放(float): float`。
- XZ 平移、中键拖动、滚轮缩放、可选边界。

- [x] 实现正交模式校验与中文单次警告。
- [x] 实现键盘移动、中键拖动与滚轮缩放，保持旋转不变。
- [x] 验证缩放上下限、边界和非正交模式。

### Task 3: 示例与交接

**Files:**
- Create: `Assets/Art/MapWhitebox/` 下的白模材质与示例数据资产。
- Create: `Assets/Script/Map/Editor/WhiteboxSampleCreator.cs`。
- Create: `Assets/Script/Map/README.md`。

**Interfaces:**
- 菜单 `山河未尽/白模地图/创建示例场景`，生成独立测试场景、两档高度、普通地块与 `StraightRiver` 地块。

- [x] 准备示例定义和关卡数据；保留当前场景。
- [x] 创建独立示例场景并配置生成器和正交相机。
- [x] 用 Unity 导入日志与示例场景检查资源、脚本和 Shader；完成键盘移动与滚轮缩放的 Play Mode 验证。
