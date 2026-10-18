# 山河未尽

正式 Unity 工程位于仓库根目录，包含 `Assets`、`Packages` 和 `ProjectSettings`。

## 第一次打开

1. 克隆本仓库，例如：`git clone https://github.com/luobai6/-.git Game`。
2. 使用 **Unity 2022.3.62f3c1**，在 Unity Hub 中添加刚下载的 `Game` 根目录。
3. 等待 Unity 导入资源和解析依赖，打开 `Assets/Scenes/SampleScene.unity`。

当前提交是基础工程，场景中包含默认相机和方向光。

## 团队协作

- 开工前同步仓库；提交前确认控制台没有编译错误。
- `Assets`、`Packages`、`ProjectSettings` 需要提交；资源与对应的 `.meta` 文件一起提交。
- 不提交 `Library`、`Temp`、`Logs`、`UserSettings` 等本机缓存，忽略规则已配置。
- 场景约定负责人；其他成员优先在各自的场景、脚本或 Prefab 中工作，再由负责人整合。
- 所有人使用上述相同的 Unity 版本，避免反复改写工程设置和资源。

MCP 插件包随工程保存，使用相对路径引用。队友正常使用 Unity 不需要安装 MCP 的 Python 服务；要使用 AI 编辑器连接时，再配置各自电脑的 MCP 客户端。

仓库中原有的 `Mountains And Rivers` 目录和设计文档均保留原样；本次接入的正式工程入口是仓库根目录。
