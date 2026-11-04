# 视觉参考

2026-10-10 原 Prefab 增量：`current/native-effects` 保存八张候选牌 48 帧原数据渲染及三张 1920×1080 棋盘组件合成图。来源为本机安装中的层级/粒子/动画/FSM/材质，不是把官方截图作背景。20 项 GPU 检查通过，奥术爆炸与地狱烈焰启用；冰霜新星只有灰色阴影，画面核验拒绝启用。未使用 Computer Use，未新增官方连续帧或正常 GUI 整屏截图。完整原 Shader/脚本及全卡逐帧一致仍未完成，见 [NATIVE_EFFECTS](NATIVE_EFFECTS.md)。

2026-10-10：本轮三张附件均为复刻反馈，归档 baseline/interaction-lifecycle，不是官方 Oracle。current/lifecycle 新增 49 个 GPU 组件采样，含不同窗口的手牌/完整箭头、英雄入场/破碎/结算和原粒子参数。没有 Computer Use，没有操作官方或用户窗口；完整 GUI 截图、官方连续帧与人工比赛未完成。依据和验证见 [INTERACTION_LIFECYCLE](INTERACTION_LIFECYCLE.md)。

2026-10-09：最新暗菜单附件是复刻 baseline/window-feedback/menu-dark.png，宽/窄附件是官方 reference/oracle/window-feedback/menu-wide.png、menu-narrow.png。未使用 Computer Use。1920×1080、1220×1110、2335×515 的离屏原场景/组件在 current/window-feedback，不含完整 GUI。剧毒确认使用原绿色药瓶、亡语使用原骷髅网格。关联材质组合在 current/associated-effects，不代替官方连续帧；见 [WINDOW_FEEDBACK](WINDOW_FEEDBACK.md)。

2026-10-09 手牌/嘲讽/连击三张附件作为复刻反馈基线保存于 `reference/internal/combat-feedback`，不当作官方同状态 Oracle。原嘲讽模型和盗贼效果关联来自安装资源；`current/interaction-models/taunt-composite.png`、`combo-yellow-components.png`、`hand10-mana-components.png` 是 GPU 离屏组件组合，未绘制整套 GUI 文本/输入，不冒充完整客户端截图。本轮未使用 Computer Use，未操作正在运行的游戏。

2026-10-09 装备/菜单反馈：六张新附件中图一/二为复刻问题基线（`tests/visual/baseline/pacing`），图三～六为原版圆形武器、技能背面/详情和插槽牌库（`reference/oracle/pacing`）。本轮未操作桌面或官方窗口。原模型/材质关联用于判断显示状态，51 项 GPU 采样位于 current/interaction-models；这些不是完整 GUI 截图。刀扇时轴为本项目调校值，原连续帧及音画时间点测量未完成，见 [BATTLE_PACING](BATTLE_PACING.md)。

2026-10-09 字体/拾牌反馈开发未使用桌面工具，沿用用户提供的正版截图。源音轨通过 CardDef/Prefab 引用核对；`tests/visual/current/interaction-models` 新增扳机模型和六张粒子合成图，仅是组件基准。新字体面板、持牌输入与整屏声音时序的人工核对未完成；不把 GPU 组件图当作官方整屏参考，见 [UI_FEEDBACK](UI_FEEDBACK.md)。

优先寻找本地菜单、棋盘、卡框、英雄框、原画、按钮和音频。现有 AssetStudio 仅作为抽样工具。

官方客户端仅正常启动、观察、截图；登录由用户完成。不得注入、修改或绕过认证。

参数：固定 1920×1080，手牌扇形/倾斜、Hover 抬升、透视与灯光、拖动箭头、抽牌飞行、攻击冲撞、受击和死亡。已有官方主菜单原生图，官方战斗 Reference 未完成，不能声称像素级还原。

2026-10-08 实机发现并修复：卡牌模型 Z 方向与相机朝向导致上下颠倒；延迟销毁的渲染对象导致后续卡面串图；英雄 submesh 纹理分配反向；空武器 JSON 在 Unity 中变为空对象导致界面异常。修复后已通过原生窗口截图和两窗口点击换牌检查。

已追加修正：原始宝石网格覆盖度、牌库卡背 UV、英雄技能框/原画、法术与武器原画 submesh、卡名位置与描边。固定 gallery 同时检查法术/随从/武器/传说/死亡骑士。仍待改善菜单按钮、光照、关键字覆盖层及完整 VFX。当前自写表现支持抽牌移动、手牌扇形、Hover、攻击冲撞、受击、死亡和原版火球纹理弹道，但还没有达到高质量统一视觉验收。

`--screenshot-dir <path>` 在进入换牌/正常游戏时截图，结束时等表现队列播放完；`F12` 保存当前画面。`--visual-test <scene>` / `--visual-state` / `--visual-gallery` 支持固定场景与过程帧；像素差/dHash 入口见 `tests/visual/README.md`。内部基线与官方 Oracle 分目录保存，原图/导出均忽略。

主菜单不再用整棵 Prefab 的包围盒缩放：`BlackFillQuad` 的巨大尺寸会把木盒缩成很小的模型。现按原始根缩放与 `CameraClosed` 位姿显示，视角取自 `TheBoxCamera`（20.7 度）。按钮命中区域从原版网格投影计算，悬停/按下改变材质亮度；卡组选择使用原版英雄框。菜单阴影按原 Shader 的 Multiply 混合，Additive 按加法混合，避免把灰度贴图画成不透明遮罩。专用 BoxRing、神光动画、灯光和完整开盒状态机仍待恢复。`--visual-menu` 自动保存菜单基线。

本轮通过官方 Beta Launcher/Battle.net 正常启动，游戏内设为 1080p，用 Print 保存 reference/oracle/main_menu.png。未修改、注入或绕过登录。战斗参考受到连接/教程及桌面工具窗口错误阻挡，尚无官方换牌/攻击/胜负图。调校值见 [PRESENTATION_SYSTEM](PRESENTATION_SYSTEM.md)，不得视为官方实测；差异与验收见 [VISUAL_DIFF](VISUAL_DIFF.md)。

## 用户提供的战斗参考（2026-10-09）

后续四张官方局部参考（双方各自法力、牌库提示、手牌提示）位于 `reference/oracle/board-ui/01~04.png`。第五张是复刻编辑器重叠的基线，位于 `reference/internal/ui-editor/sidebar-overlap.png`，不当作正版 Oracle。本轮未使用 Computer Use；离屏模型图及恢复边界见 [BOARD_UI](BOARD_UI.md)。

用户本轮提供七张正版截图，原样复制到忽略目录 `tests/visual/reference/oracle/interaction/`：换牌、无动作回合按钮、技能就绪、技能详情、随从详情、历史图标、历史详情。前三张为 1924×1127，包含窗口边框；后四张为不同尺寸的局部截图。它们用于位置、轮廓、图标和交互关系参考，不能与 1920×1080 的客户端整图直接算同状态像素差。棋盘主题及卡牌状态亦不同。

换牌绿光/悬停白光、绿色回合按钮、技能卷轴、随从左右侧详情、历史小图标以这些真实图为依据。没有将整张参考图铺到游戏背景；详情依靠真实模型、原画、字体和布局组成。用户要求本轮不使用 Computer Use，未操作官方窗口、未录制新时序。

当前最终模型/图标有十张 Unity GPU 离屏采样，见 `current/interaction-models/`；这是资源方向与组合的证据，不是完整对战 UI 验收。后台 Player 整图采样产生黑帧，已拒绝。最终画面手动核对未完成，仍需在正常窗口评估比例及白绿光强度。

原 Multiply 节点查看 current/native-effects/CS2_026-2.00.png、CS2_062-0.30.png；均为本项目真实原 Prefab GPU 离屏帧，非正版截图或整场认证。原目标与最终合成仍有差异，继续以已有正版参考核对，不操作用户桌面，见 [ORIGINAL_MULTIPLY_SHADER](ORIGINAL_MULTIPLY_SHADER.md)。
