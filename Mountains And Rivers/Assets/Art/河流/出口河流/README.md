# 洪水出口河道资源说明

## 定位

该资源是地图南边界使用的单入口终点河道：水从北侧进入，在地块中下部展开，并从南侧地图边界流出。它不是建在堤坝上的“泄水口”棋子，不参与修改洪水路径规则。

## 源贴图

- `T_River_Outlet_Albedo.png`：河床、岸线、石块和植被的彩色底图。
- `T_River_Outlet_Control.tga`：RGBA 河道控制图，沿用现有河流 Shader 的通道定义。
- `T_River_Outlet_NS_Advance.png`：北侧到南侧的洪水水头推进图，黑色先到、白色后到。

## Unity 资源

- `M_RiverBed_Outlet.mat`：河床材质。
- `M_RiverWater_Outlet.mat`：程序化河水与洪水材质。
- `OutletRiver.prefab`：河床与河水双层 Prefab，默认流向为 `(0, -1)`，关闭反向推进。
- `D_洪水出口.asset`：地块系统使用的视觉定义。

## 创建与验证

在 Unity 菜单执行：

`山河未尽 > 河道工具 > 创建或更新洪水出口资源`

工具会统一贴图导入设置，重建材质和 Prefab，并生成：

`Assets/Scenes/OutletRiverArtValidation3x3.unity`

验证场景中间一列从北到南依次为“直河、直河、洪水出口”。在出口 Prefab 的 `RiverVisualController` 上依次检查：

1. “洪水到达进度”从 `0` 到 `1` 时，水头是否从北侧连续推进到南侧边界。
2. “洪水扩张进度”为 `0`、`0.5`、`1` 时，水面是否保持在控制图范围内。
3. 直河与出口入口是否无明显错位、白边或断层。
4. 出口只用于地图南边界，不旋转为反向入口，不开启“反向推进”。
