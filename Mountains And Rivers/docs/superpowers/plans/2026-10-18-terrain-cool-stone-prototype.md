# 浅石青连续地块小样 Implementation Plan

> **状态：已被替代。** 2026-10-18 已确认舍弃圆缓坡肩，并改用统一手绘顶面纹理、赭石侧壁和窄幅手绘边缘带。后续实施请以 `docs/superpowers/plans/2026-10-18-unified-painted-terrain-skin.md` 为准；本文件仅保留旧实验过程。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立一个可反复调色的 3×3 Unity 地块小样，验证浅石青地面、晕染缓坡与现有山体能否自然连续。

**Architecture:** 保留现有 `MapGenerator` 的逻辑方格和高度数据，不修改玩法代码。通过世界坐标采样材质隐藏格缝，在独立验证场景中增加只负责视觉的缓坡覆盖网格和现有山体参照；河水仅用验证占位，不接入正式河道系统。

**Tech Stack:** Unity 2022.3、URP ShaderLab/HLSL、C# Editor 工具、Python 3 + Pillow/NumPy 离线贴图生成与预览。

**Spec:** 本轮确认的美术基准：C「浅石青缓坡」、暖石色河岸、A「晕染缓坡」、成品隐藏逻辑格缝；具体色值是工作基线，必须允许实机后调整。

## Global Constraints

- 不修改正式河道出入口、河流 Prefab 或河道逻辑。
- 不修改 `MapGenerator` 的坐标、高度、Collider 和玩法语义。
- Shader Inspector 字段、Tooltip、菜单与交付说明使用中文。
- 使用现有山体 Prefab 作色彩与接地参照。
- 本轮不批量扩展地块库，只交付 3×3/小范围验证样例。

## Review Focus

- 同高度相邻地块不能出现九宫格边框或纹理接缝。
- 缓坡覆盖层不能改变逻辑地块 Collider 或点击行为。
- 低频青灰晕染不能形成可识别的重复蝴蝶图案。
- 山脚不能明显悬空，地面也不能比山体更抢眼。
- 临时水面必须明显标注为验证占位，不能与正式河道资产混淆。

---

### Task 1: 重做浅石青连续地表贴图

**Files:**
- Modify: `Tools/TerrainSkin/gen_terrain_skin.py`
- Modify: `Assets/Art/Map/Terrain/Textures/T_Terrain_Top_Ink_A.png`
- Modify: `Assets/Art/Map/Terrain/Textures/T_Terrain_Side_Earth_A.png`
- Modify: `Tools/TerrainSkin/out/preview_before_after.png`

**Interfaces:**
- Consumes: 当前 `TerrainTileSkin` 的世界坐标镜像采样与 `_WorldScale = 4`。
- Produces: 顶面浅石青低频罩染贴图、侧面柔和青灰贴图、同机位离线预览。

- [x] **Step 1: 把生成器色板改为山体提取色**

使用 `#B8B28F` 作为地面基色、`#7A8D7F` 作为青灰罩染、`#4C6969` 作为少量深色、`#C3B38E` 作为暖石过渡色。

- [x] **Step 2: 降低顶面纹理频率与反差**

移除可辨识砂砾和草笔，改为跨 1–4 个世界单位的柔和水墨晕染，目标整体明度标准差不高于 `0.045`。

- [x] **Step 3: 把侧壁规则层理改为连续坡脚色带**

减少水平层线，让上缘承接暖石色、下部逐渐转入青灰，目标整体明度标准差不高于 `0.035`。

- [x] **Step 4: 运行生成器并检查统计**

Run: `python Tools/TerrainSkin/gen_terrain_skin.py`

Expected: 生成两张 2048² PNG 与 `preview_before_after.png`；脚本输出顶面和侧面统计且无异常。

- [x] **Step 5: 目视检查离线预览**

Expected: 前两排同高度地面连续；地面没有密集绿点；高差侧面不再像规则切片。

### Task 2: 更新带山体与缓坡的验证场景生成器

**Files:**
- Modify: `Assets/Script/Map/Editor/TerrainArtValidationCreator.cs`
- Modify: `Assets/Art/Map/Terrain/README.md`
- Regenerate: `Assets/Scenes/TerrainArtValidation3x3.unity`

**Interfaces:**
- Consumes: `M_Terrain_Base_A`、`Miuntain.prefab`、`L_普通地块3x3验证.asset`。
- Produces: 菜单 `山河未尽/美术验证/创建或更新浅石青地块3×3验证场景` 与可保存的独立验证场景。

- [x] **Step 1: 在验证场景中创建视觉缓坡覆盖网格**

为高度 0→1 与 0→2 的展示位置创建无 Collider 的楔形 Mesh，复用 `M_Terrain_Base_A`，不修改底层逻辑方块。

- [x] **Step 2: 加入现有山体参照**

实例化 `Assets/Art/山/山1/Miuntain.prefab` 到平地区域，保持其原材质与内部层次，仅调整验证场景中的位置、旋转和整体比例。

- [x] **Step 3: 加入明确命名的临时水面与暖岸色条**

对象名包含“仅供配色验证”，不引用或改动正式河道资源；不添加 Collider。

- [x] **Step 4: 更新相机取景与中文说明**

相机同时看见连续平地、两级缓坡、山体和临时水面；README 明确色值为可调基线以及三项验收标准。

- [ ] **Step 5: Unity 编译与场景验证**

Expected: 菜单可执行、场景保存成功、Console 无新增编译或 Shader 错误；Game 视图可同时评估山脚贴地、格缝与主次关系。

