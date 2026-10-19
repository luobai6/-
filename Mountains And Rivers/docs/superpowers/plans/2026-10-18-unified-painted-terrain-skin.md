# 统一手绘普通地块皮肤 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改动玩法数据和河道资源的前提下，完成“统一青绿顶面纹理 + 赭石侧壁 + 窄幅手绘边缘带”的 3×3 普通地块验证版本，并彻底停止使用圆缓坡肩。

**Architecture:** 保留 `MapGenerator` 生成的方正 Cube 和现有逻辑高度，正式材质通过世界坐标连续采样统一顶面纹理，并按模型侧壁高度绘制上深下浅的赭石渐变与窄幅青赭边缘带。验证场景生成器只创建地块、山体综合色调参照和临时河岸/水面对照，不再生成坡肩网格；河道、瀑布和山体原件均不修改。

**Tech Stack:** Unity 2022.3.62f1c1、URP 14.0.12、ShaderLab/HLSL、C# Editor 工具、Unity Test Framework 1.1.33。

**Spec:** `Assets/Art/Map/Terrain/README.md`

**实施状态（2026-10-18）：** Shader、正式材质、无坡肩场景生成器、资源检查工具和两张 2048×2048 首版纹理已经写入项目。由于项目正被另一个 Unity 编辑器实例占用，场景重建、Shader 实编译和 Game 视图目视验收仍待在该实例中完成。

## Global Constraints

- 当前阶段所有关卡共用一套统一地面纹理，不制作关卡专属地景总图。
- 普通地块保留平整顶面、垂直侧面和清晰阶梯；不创建或使用圆缓坡肩。
- 顶面、侧面和边缘变化来自手绘纹理与确定性 Shader 混合，不使用程序噪声。
- 不修改 `MapGenerator` 的坐标、高度、Collider 和玩法语义。
- 不修改正式河道、源头瀑布、山体 Prefab 或其他对话正在制作的资源。
- Shader Inspector 字段、Tooltip、菜单、错误信息和交接说明使用中文。
- 地块皮肤只负责表现，不参与洪水容量、路径、胜负和点击规则。
- 本轮只完成 3×3 验证版本；5×5 是本轮通过后的下一阶段。

## Review Focus

- 相邻同高度 Cube 必须使用相同世界坐标取样，接缝处不能各自重启 UV。
- 一级和二级高差都必须保持“顶部深赭、向下变浅”，且不能因世界绝对高度不同而颠倒。
- 边缘带只能出现在侧壁顶端，宽度约占侧壁高度 6%～12%，不能污染整个侧面或重新形成坡面。
- 地块定义的白模染色不能把统一青绿与赭石配色染成九宫格色块。
- 验证场景重建后不得生成坡肩对象、坡肩 Mesh 或引用圆缓坡肩材质。

---

### Task 1: 建立可验证的地块皮肤 Shader 接口

**Files:**
- Modify: `Assets/Art/Map/Terrain/Shaders/TerrainTileSkin.shader`
- Modify: `Assets/Art/Map/Terrain/Materials/M_Terrain_Base_A.mat`
- Create: `Assets/Tests/Editor/TerrainTileSkinContractTests.cs`
- Create: `Assets/Tests/Editor/ShanheWeijin.Terrain.EditorTests.asmdef`

**Interfaces:**
- Consumes: `_TopMap`、`_SideMap`、世界坐标 XZ、Cube 模型空间 Y、现有 `_WorldScale`。
- Produces: Shader 属性 `_SideTopTint`、`_SideBottomTint`、`_EdgeTint`、`_EdgeWidth`、`_SideGradientStrength`；材质继续由 `MapGenerator.默认白模材质` 直接使用。

- [ ] **Step 1: 写 Shader 契约编辑器测试**

测试 `Shader.Find("ShanheWeijin/TerrainTileSkin")` 非空，并断言材质存在上述五个中文用途对应属性；同时断言不再需要 `_TopBlendStart` 与 `_TopBlendEnd` 承担坡肩混色。

- [ ] **Step 2: 运行测试并确认旧 Shader 不满足新契约**

Run: Unity EditMode test `TerrainTileSkinContractTests`

Expected: FAIL，缺少侧壁上下色、边缘色和边缘宽度属性。

- [ ] **Step 3: 重构 `TerrainTileSkin.shader`**

顶面继续按 `positionWS.xz / _WorldScale` 统一取样；侧面纹理按朝向投影。把模型空间侧壁高度从 `-0.5..0.5` 归一化为 `0..1`，以此混合 `_SideBottomTint` 与 `_SideTopTint`；仅在侧壁顶部 `_EdgeWidth` 范围混入 `_EdgeTint`。顶面与侧面由法线阈值硬分类，不再模拟圆缓坡面。

- [ ] **Step 4: 更新正式材质工作值**

`M_Terrain_Base_A` 设置青绿顶面 `#748A79`、边缘青赭 `#91846A`、侧面上部深赭 `#8E6F50`、侧面下部浅赭 `#C3A77B`；`_EdgeWidth` 初值为 `0.09`，地块定义染色强度先降为 `0`。

- [ ] **Step 5: 运行 Shader 契约测试**

Expected: PASS；材质引用仍指向 `TerrainTileSkin`，所有正式字段可由 Inspector 调整。

### Task 2: 移除验证场景中的圆缓坡肩生成路径

**Files:**
- Modify: `Assets/Script/Map/Editor/TerrainArtValidationCreator.cs`
- Create: `Assets/Tests/Editor/TerrainArtValidationCreatorTests.cs`
- Regenerate: `Assets/Scenes/TerrainArtValidation3x3.unity`

**Interfaces:**
- Consumes: `D_普通地块_A_静地`、`M_Terrain_Base_A`、`L_普通地块3x3验证.asset`、现有山体 Prefab 只读参照。
- Produces: 菜单 `山河未尽/美术验证/创建或更新普通地块统一皮肤3×3验证场景`，场景中只保留方正阶梯、山体参照与明确标注的临时水岸参照。

- [ ] **Step 1: 写验证场景结构测试**

执行场景创建入口后，断言场景存在 9 个地块根节点和 0/1/2 三种高度；断言对象名不包含“坡肩”，Mesh 资产路径不包含 `Mesh_圆缓坡肩`，地图底座使用 `M_Terrain_Base_A`。

- [ ] **Step 2: 运行测试并确认旧生成器失败**

Expected: FAIL，旧生成器仍创建两个圆缓坡肩对象并使用圆缓坡肩实验材质。

- [ ] **Step 3: 精简 `TerrainArtValidationCreator`**

删除坡肩 Mesh 创建方法和相关路径常量；把默认地块材质改为 `Assets/Art/Map/Terrain/Materials/M_Terrain_Base_A.mat`；保留山体参照、暖岸和临时水面，但不修改它们的原始资源。

- [ ] **Step 4: 更新菜单、对象名与中文日志**

所有名称改为“普通地块统一皮肤3×3验证”，日志明确当前使用统一纹理、无圆缓坡肩、色值仍可调整。

- [ ] **Step 5: 重建并运行场景结构测试**

Expected: PASS；场景可保存，未创建任何坡肩对象，未改动河道与山体 Prefab。

### Task 3: 制作统一手绘纹理首版并完成静态检查

**Files:**
- Modify: `Assets/Art/Map/Terrain/Textures/T_Terrain_Top_Ink_A.png`
- Modify: `Assets/Art/Map/Terrain/Textures/T_Terrain_Side_Earth_A.png`
- Modify: corresponding `.meta` files only when Unity import settings require it
- Modify: `Assets/Art/Map/Terrain/README.md`

**Interfaces:**
- Consumes: 用户提供的青绿山水阶梯参考图，仅作为色彩、笔触尺度和顶侧关系参考。
- Produces: 2048×2048 统一青绿顶面纹理、2048×2048 赭石侧壁纹理；两张图均不包含可识别的大型独立物体。

- [ ] **Step 1: 建立顶面纹理首版**

使用大尺度青绿、浅赭留白、低对比度皴擦与纸本质感；不生成密集噪点，不绘制树木、房屋、完整山峰、巨石或水潭。

- [ ] **Step 2: 建立侧面纹理首版**

使用稀疏纵向冲刷、矿物色痕和淡墨裂纹；贴图本身保持低频，主要的上深下浅关系由 Shader 保证。

- [ ] **Step 3: 检查尺寸、颜色模式和 Alpha**

Expected: 两张图均为 2048×2048 RGBA 或 RGB PNG；没有空白透明区；Unity 导入后不产生白边。

- [ ] **Step 4: 更新交接说明中的实测参数**

记录最终 `_WorldScale`、边缘宽度、材质色值和纹理来源；候选值不能写成已确认结果。

### Task 4: Unity 3×3 实机验收

**Files:**
- Verify: `Assets/Scenes/TerrainArtValidation3x3.unity`
- Verify: `Assets/Art/Map/Terrain/README.md`

**Interfaces:**
- Consumes: Tasks 1–3 的 Shader、材质、纹理和验证场景。
- Produces: 一份可供用户目视调色的稳定 3×3 样例；不自动扩展正式关卡。

- [ ] **Step 1: 等待 Unity 完成编译与资源导入**

Expected: Console 无新增 C# 编译错误、Shader 错误或材质丢失。

- [ ] **Step 2: 运行 EditMode 测试**

Expected: `TerrainTileSkinContractTests` 与 `TerrainArtValidationCreatorTests` 全部通过。

- [ ] **Step 3: 打开 3×3 验证场景检查同高度连续性**

Expected: 前两排不呈现九宫格重复边框；相邻地块取样方向一致。

- [ ] **Step 4: 检查一级与二级高差**

Expected: 方正阶梯清楚；侧壁顶部较深、下部较浅；窄边缘带可见但不像坡面。

- [ ] **Step 5: 与现有山体和临时河岸并看**

Expected: 地面不抢山体，青绿与山体同属一套综合色；临时河岸与水面仍明确标注为配色参照。

- [ ] **Step 6: 停在用户调色门槛**

把 Game 视图结果交给用户判断颜色与笔触，不自动进入 5×5、正式关卡或每关专属地景总图。
