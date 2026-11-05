# OpenHearthstone

本机资源驱动的 Unity Windows 客户端与 Python 权威对战服务器。开发中的本地项目；十一职业均已接入，可运行同机双客户端。完整卡池核验与高还原视觉验收仍未完成。

官方安装只读；原版资源、数据库和导出缓存都位于忽略的 `local/`，不会提交 Git。

## 仓库目录

- `game/`：Python 对战规则、服务器与账号逻辑。
- `UnityClient/`：Unity 客户端、编辑器和构建脚本。
- `tools/`：资源索引、导出和验证工具。
- `manifests/`、`data/`：项目所需的映射与数据清单。
- `tests/`：自动测试和视觉验证入口。
- `docs/`：构建、资产管线、设计记录和任务状态。
- `local/`、`build/`、`work/`：本机资源与生成物，不包含在仓库中。

## 首次准备

项目当前面向 Windows，使用 Python 3.11 和 Unity 2022.3.60f1c1。克隆后在仓库根目录执行：

```powershell
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe -X utf8 -m pytest tests -q
```

客户端程序和本机资源缓存不随源码分发。双击启动之前，请按 [首次构建步骤](docs/BUILD.md) 设置游戏安装目录、导出资源并构建 Unity 客户端；仅创建 Python 环境还不能启动游戏。研究清单包含资源映射、曲线及组件数据；部分研究、视觉验证和资源导出依赖本机客户端及已有缓存，干净克隆不保证直接通过所有测试或生成完整画面。

源代码使用 [AGPL-3.0-or-later](LICENSE)；第三方组件的许可见 `licenses/`。游戏名称、原始素材及其权利属于各自权利人，原素材不在本仓库的许可范围内。

## 技术栈

- Unity 2022.3.60f1c1，安装路径因电脑而异
- Python 3.11 虚拟环境 `.venv`
- UnityPy 1.25.2、SQLite 元数据索引、原始 Mesh/Transform/Material 转换
- Fireplace 固定版本规则 DSL、TCP UTF-8 JSON Lines 权威服务器
- pytest 规则与真实 TCP 连接测试
- 可选研究用参考仓库独立于本项目

## 在当前机器运行

双击根目录 `Start-LocalDuel.cmd`，自动复用或启动本机服务器，并打开名字独立的 Shrink1 / Shrink2。每个职业的自定义套牌分别保存在各自 profile 中。

也可在自己的项目根目录打开 PowerShell，分别启动服务器和两个客户端：

```powershell
.\.venv\Scripts\python.exe -X utf8 -m game.server
.\build\client\HearthstoneClone.exe --profile player1 --name Shrink1
.\build\client\HearthstoneClone.exe --profile player2 --name Shrink2
```

点击“对战模式”，选择职业与 30 张牌卡组，再进入 LAN 匹配。IP 默认 `127.0.0.1`；跨电脑时服务器使用 `--host 0.0.0.0`，客户端填服务器的局域网 IP。客户端通过 `--data` 指向各自本机 `local` 数据目录。`F12` 截图，`Esc` 取消目标或打开游戏菜单；`F3` 开发 HUD（默认关闭），开发模式 `F4` 动画倍速、`F5` 跳过。

各 profile 独立保存于 `%LOCALAPPDATA%\HearthstoneClone\profiles\<profile>`。原始资产需要每台机器自行提取，不包含在源码中。

人工校对 UI：双击 `Start-UIEditor.cmd`，或游戏中按 `F6`。分别编辑随从牌、法术牌、武器牌、战场随从、英雄、技能、棋盘/双方区域、手牌、换牌和宝石；支持拖动/旋转/缩放、原画翻转、数值输入、撤销和 JSON 导入导出。`Ctrl+S` 保存到本地 `local/ui-calibration.json`，两个客户端共用；另一已启动实例按 `F7` 重读。详见 [UI 编辑器操作说明](docs/UI_EDITOR.md)。

```powershell
.\.venv\Scripts\python.exe -X utf8 -m pytest tests -q
.\.venv\Scripts\python.exe -X utf8 -m tools.importer report
```

截至 2026-10-08：本项目 577 项测试通过，上游专项测试 281 项通过。489 张目标牌有逐 ID 基础执行测试，20 个抉择分支单独测试；已修复死亡计数、眼棱目标/流放减费、法力燃烧及部分恶魔猎手触发对象。两个 Unity 实例通过正常动作协议完成了法师/战士及死亡骑士/恶魔猎手完整比赛，并通过实际按钮验证了菜单、卡组选择、匹配和起手换牌。基础执行通过不等于每张牌的全部行为已 verified。

本地目标池共 489 张，逐 ID 状态记录在实现状态文档。显示数据库保留历史快照全部 9,603 条定义，供随机生成牌显示名称、类型与规则文字；可组牌仍仅有 489 张。已修复卡牌倒置、串图、英雄与法术/武器 submesh 原画错配、卡背图集 UV、宝石 alpha 用法。衍生牌缺少缓存时原生客户端会在后台只读提取原画。

主菜单使用原版木盒、圆盘、按钮模型及相机；支持悬停反馈与原版 UI 音效。卡牌事件音频按 CardDef GUID 按需提取；十一职业预设提取了 297 条原版音轨（零错误），合并额外测试牌后共 304 条、涉及 89 张牌。客户端使用每个事件的首个默认候选，其余候选及原始时间证据保留在本地报告。并非每张法术都包含 CardDef 事件音轨；完整语音覆盖与混音仍在完善。

真实窗口操作已验证：替换两张起手牌、拖动战斗邪犬入场、施放幸运币、死亡骑士技能召唤食尸鬼并拖箭头攻击、死亡增加尸体、恶魔猎手技能与英雄攻击触发邪犬成长。未通过控制台修改比赛状态。

音频实机比赛 `3667b1b9272a4a9487ddf6db9f428cbc` 在 17 回合、版本 81 正常结束；两实例公共哈希均为 `dcc3bff28aab76375bf66cd6f92a21344e5f8dcf673a90ab86071a2a8affab27`。已验证出牌/攻击/死亡 WAV 成功加载播放，胜负窗口分别正常返回木盒主界面。死亡音轨相对本项目攻击冲撞延后 0.4 秒；这属于当前表现调校，尚未声称与官方时序一致。

最终构建的法师/术士比赛 `86fe2d7760b24fcf9316edca42631fb4` 正常运行 18 回合、版本 64；双方公共哈希 `fc4bc90a78eedc85da37047231fd903232f122a02f0ebfcefe746f6aaea57352` 一致，日志没有命令拒绝或运行异常。另通过主动中断本项目服务器验证原生断线提示与返回菜单，未提供原比赛重连。

源码采用 AGPL-3.0-or-later，以兼容所使用的 Fireplace。Resource Lab 的 MIT 许可单独保留。Blizzard 资源不属于本项目许可证。

本轮 Presentation Sprint 保留规则/LAN 架构，改进 1080p 棋盘/桌面、换牌状态、动态手牌、随从排布、攻击/死亡队列、原版字库和事件音效。重新通过 577 项测试，构建执行 245 条表现断言，32 个实际 Windows 场景完成截图检查；双客户端仍能完整比赛。官方战斗 Oracle、五个 Gold Standard 的官方核验、完整粒子和本轮人工完整局**未完成**。见 [表现系统](docs/PRESENTATION_SYSTEM.md)、[差异与验收](docs/VISUAL_DIFF.md)、[回归命令](tests/visual/README.md)。

进度与限制见 [ROADMAP](docs/ROADMAP.md)、[卡池](docs/CARD_SCOPE.md)、[实现状态](docs/CARD_IMPLEMENTATION_STATUS.md)。开发入口见 [构建说明](docs/BUILD.md)、[资产管线](docs/ASSET_PIPELINE.md)、[网络协议](docs/NETWORK_PROTOCOL.md)。
