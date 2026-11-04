# Windows 构建

当前 Unity 2022.3.60f1c1 已安装并激活。关闭正在运行的测试客户端，再从正式项目根目录执行：

```powershell
& 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe' -batchmode -quit -projectPath E:\HS\HearthstoneClone\UnityClient -executeMethod BuildClient.Build -logFile E:\HS\HearthstoneClone\work\unity-build.log
```

生成 `build/client/HearthstoneClone.exe` 与配套目录。原版 Unity 6000 bundle 不直接加入 Unity 2022 Assets；本地导出数据位于项目 `local`，启动可通过 `--data` 指定另一台机器自己的缓存路径。

2026-10-15 核实：当前 BuildClient.Build 在构建前执行 NativeEffectChecks，其中包含 Camera.Render，不能使用 `-nographics`。TASK-0002 的旧命令在该调用处 SIGSEGV / 退出 1，证据 `work/task-0002-build.log`；保留图形批处理且隐藏进程，不需要操作桌面。

首次 Python 环境：Python 3.11 创建 `.venv`，安装 `requirements.txt`。资产扫描与导出命令见 ASSET_PIPELINE。参考仓库独立保留，无需修改。原资源不包含在 Git 或本项目许可证中。

资产导出前运行 `python -m tools.prepare_data` 准备并校验 2022 死亡骑士历史定义。`python -m tools.card_status` 生成逐 ID 状态。固定视觉测试入口见 `tests/visual/README.md`。

战斗交互版本另外需要 `python -m tools.interaction_assets`，在已有扫描索引/RuntimeExport 缓存上增量恢复技能详情、历史框、护甲和耐久。本机本轮已执行，不必重新全量解包；其他电脑应使用其自己的合法客户端提取。新版本沿用已有 `local/ui-calibration.json`，不会覆盖人工校对。

构建执行 245 条表现、21 条校对、33 条交互断言。资源 GPU 离屏检查另用 `-executeMethod InteractionRenderChecks.Run`，移除 `-nographics`；完整 GUI 截图须正常可渲染的 Player，后台黑帧不计入通过。
