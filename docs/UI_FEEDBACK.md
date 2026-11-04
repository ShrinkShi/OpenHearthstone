# 卡牌操作、文字与原版反馈素材

本轮继续现有 Unity/Python 权威对战，未扩卡池。官方安装及参考仓库只读，未使用 Computer Use，用户保存的 29 个布局元素原文件保留，SHA256 为 `05A3B53B95C57D93203D09A7AA9BFEAC64880E86D2ACFAFE2E6323332318B488`。

## 实际变化

- 战斗悬停用原音轨 `card_mouse_over`，拾牌用 `card_mouse_away`，菜单仍用菜单音轨。留牌点选改用原 `card_turn_over_normal` Foley，未证实它就是原版留牌按钮的准确调用；此次无人工试听，不能声称该项完全一致。
- 烈焰冲击沿 `HERO_08bp` 的 Fireball Small Prefab 引用确认 `Shared_Fire_Start_1`、`Shared_Fire_Cast_1`、`Shared_Fire_Impact_Small`。起手/发射/命中分别进入客户端 cue，不再播放菜单点击声。
- 生命/耐久传递当前上限和增益标志，满值白、受损红、增益后满值绿。损伤优先于增益；沉默重算上限。手牌、详情、场上英雄/随从和武器共用数字绘制。
- 校对工具支持背景取景、文字填充/描边/阴影、RGBA 调色板、字号和粗斜体，保留描述原有粗体。样式为可选字段，旧布局继续兼容。
- 扳机符号是原 `Trigger` 网格及纹理；实际监听回调发送 `Trigger`，视觉队列短暂提亮，沉默后不显示。普通广播或空战吼不冒充扳机。
- 点击松开后仍持牌，按住拖动也持牌。随从/武器/非指向法术必须战场释放或二次点击战场。拖回手牌、手牌区二次点击、右键或 Esc 取消。指向法术用箭头；指向战吼先落位再选合法目标，保留插入位置。

## 原版效果与重实现边界

`python -m tools.feedback_assets` 增量导出 18 个逻辑资源，当前 registry 984 项，零导出错误。来源见 [feedback-assets.json](../manifests/feedback-assets.json)。

| 卡牌 | 确认的原始关联 | 当前使用 |
|---|---|---|
| 烈焰冲击 | Fireball Small、Fireball_Start、14 个粒子系统、6 条音轨 | 三阶段声音、原火焰/火花纹理，重实现弹道/扩散 |
| 火球术 | Fireball Medium、Fireball_Start、13 个粒子系统、5 条音轨 | 起手/施放/命中声音、原火焰/火花纹理 |
| 寒冰箭 | Ice_FX、Fireball_Start、14 个粒子系统、5 条音轨 | 冰霜声音、冰弹和冰片纹理，不再仅给火球贴蓝色 |
| 奥术智慧 | ArcaneIntellect_FX、3 个粒子系统、Mage_ArcaneIntellect_Cast_1 | 原音轨、奥术符号及符文纹理，抽牌阶段叠放 |

`local/runtime/effect-research.json` 记录源 Prefab GUID、CAB/PathID/bundle 和动画/粒子原参数缓存；`effect-index.json` 映射使用的音轨和纹理。`Fireball_Start` 的根节点浮动曲线约 15 秒，并非 0.4 秒弹道，未强行替代弹道。原 PlayMaker 脚本、专用 Shader、完整 ParticleSystem/AnimationClip 重放**未完成**。当前轨迹、大小、淡出、发射时间和闪电提亮由本项目重实现，原音轨/纹理/网格来自本机。粒子用加法混合 Shader，保留透明 alpha，黑色素材背景不遮挡棋盘。

## 本轮验证

- 起始规则 baseline：577 passed；新增六项表现字段/扳机测试后 **583 passed（18.33 秒）**。
- Windows 构建：245 条表现、21 条校对、33 条交互、26 条反馈、11 条客户端输入/TCP 请求断言，合计 **336 条通过**。输入重放不操作桌面，检查短点击无出牌请求、拖回取消、战场释放、敌方英雄目标、战吼落位/选目标和右键取消。
- GPU 离屏：11 个模型/轮廓图、6 个粒子合成图及 6 项黑角透明检查，共 **23 项通过**。图在 `tests/visual/current/interaction-models`，报告 `render-report.json`；组件图不能冒充整屏截图。
- 首次后台双 Windows Player 局 `8dad083c2df74eea934887af7a211e95`：18 回合/v64，Shrink2 战士 WON（27 HP）、Shrink1 法师 LOST（-4 HP）；双方公共哈希 `23ee577709ad93eac95aad19383561b4774952af0b1afa0560d83d677a0c38b8`。专用 Profile/端口，未动玩家 Profile。
- 最终构建局 `91d3281367d648dda89db50e98586ff6`：15 回合/v54，Shrink1 LOST（-2 HP）、Shrink2 WON（21 HP）。两个客户端 Profile 的 state.json 都为 finished/v54，公共哈希 `379dcca2f4416e5677c6614c236b3105a0513a2ea2246a14b4cc3faac6e8435f`，与服务器一致；两份 Player 日志和服务器日志无异常。后台进程在检查结束后关闭，仅清理本轮专用进程。

未完成：新字体面板与交互的整屏人工核验、留牌点击准确原轨核定、所有卡牌专属 VFX。后台自动对局不是人工完整比赛；批处理 Player 不执行完整 OnGUI，其约 60 FPS 数据不能作为 1080p GUI 性能验收。
