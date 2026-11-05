# Windows 构建

## 新克隆首次准备

仓库只包含源码和研究清单，`.venv`、`local/` 和 `build/` 均不会随 Git 克隆。需要 Windows、Python 3.11、Git、Unity 2022.3.60f1c1 和本机的炉石传说安装。以下命令在**自己的仓库根目录**的 PowerShell 中执行，路径请按实际安装位置替换：

```powershell
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
$env:HEARTHSTONE_GAME_ROOT = 'D:\Games\Hearthstone' # 改为实际安装目录
$env:UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\2022.3.60f1c1\Editor\Unity.exe' # 改为实际路径
Test-Path (Join-Path $env:HEARTHSTONE_GAME_ROOT 'Data\Win\asset_manifest.unity3d')
Test-Path $env:UNITY_EDITOR
```

两个 `Test-Path` 都应返回 `True`。`HEARTHSTONE_GAME_ROOT` 对当前 PowerShell 会话及其子进程生效；新窗口可重新设置或自行配置为用户环境变量。成功扫描后，导入器也会尝试复用本机 `local/asset-audit.json` 中仍然存在的安装路径。游戏目录始终只读。然后准备元数据和基础资源：

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.prepare_data
.\.venv\Scripts\python.exe -X utf8 -m tools.importer scan
.\.venv\Scripts\python.exe -X utf8 -m tools.export_runtime
.\.venv\Scripts\python.exe -X utf8 -m tools.export_ui
.\.venv\Scripts\python.exe -X utf8 -m tools.presentation_assets
.\.venv\Scripts\python.exe -X utf8 -m tools.fireblast_ui_assets
```

扫描和导出可能耗时，且依赖本机游戏版本；请检查输出的错误和 `local/runtime/export-report.json`，缺失资源时不能认为导出完整。其他界面和特效的导出入口见 [资产管线](ASSET_PIPELINE.md)，仅运行上述基础步骤不保证全部画面还原。确认 `local/runtime/registry.json` 和五个字体文件存在后构建：

```powershell
New-Item -ItemType Directory -Force work | Out-Null
& $env:UNITY_EDITOR -batchmode -quit -projectPath (Join-Path (Get-Location) 'UnityClient') -executeMethod BuildClient.Build -logFile (Join-Path (Get-Location) 'work\unity-build.log')
```

检查进程退出码及 `work/unity-build.log`。成功后双击 `Start-LocalDuel.cmd`；UI 编辑器使用 `Start-UIEditor.cmd`。安装位置、资源版本或 Unity 环境不同可能需要调整；本项目尚未验证所有版本的干净机器完整导出。

`BuildClient.Build` 只构建客户端；需要原开发环境完整回放数据的回归检查可使用 `BuildClient.BuildValidated`，不能把普通构建成功当作这些检查已通过。

## 本机历史记录

当前 Unity 2022.3.60f1c1 已安装并激活。关闭正在运行的测试客户端，再从正式项目根目录执行：

```powershell
& 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe' -batchmode -quit -projectPath E:\HS\HearthstoneClone\UnityClient -executeMethod BuildClient.Build -logFile E:\HS\HearthstoneClone\work\unity-build.log
```

生成 `build/client/HearthstoneClone.exe` 与配套目录。原版 Unity 6000 bundle 不直接加入 Unity 2022 Assets；本地导出数据位于项目 `local`，启动可通过 `--data` 指定另一台机器自己的缓存路径。

2026-10-15 历史核实：当时 BuildClient.Build 在构建前执行 NativeEffectChecks，其中包含 Camera.Render，不能使用 `-nographics`。检查现已移至 BuildClient.BuildValidated。TASK-0002 的旧命令在该调用处 SIGSEGV / 退出 1，证据 `work/task-0002-build.log`；完整验收仍需图形批处理且隐藏进程。

首次 Python 环境：Python 3.11 创建 `.venv`，安装 `requirements.txt`。资产扫描与导出命令见 ASSET_PIPELINE。参考仓库独立保留，无需修改。原资源不包含在 Git 或本项目许可证中。

资产导出前运行 `python -m tools.prepare_data` 准备并校验 2022 死亡骑士历史定义。`python -m tools.card_status` 生成逐 ID 状态。固定视觉测试入口见 `tests/visual/README.md`。

战斗交互版本另外需要 `python -m tools.interaction_assets`，在已有扫描索引/RuntimeExport 缓存上增量恢复技能详情、历史框、护甲和耐久。本机本轮已执行，不必重新全量解包；其他电脑应使用其自己的合法客户端提取。新版本沿用已有 `local/ui-calibration.json`，不会覆盖人工校对。

构建执行 245 条表现、21 条校对、33 条交互断言。资源 GPU 离屏检查另用 `-executeMethod InteractionRenderChecks.Run`，移除 `-nographics`；完整 GUI 截图须正常可渲染的 Player，后台黑帧不计入通过。
