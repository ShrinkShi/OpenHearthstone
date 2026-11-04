# 窗口、材质与状态反馈（2026-10-09）

本轮在 master/0b9f97d 延续现有 Unity、Python 规则和 TCP，没有扩卡池、改写服务器或使用 Computer Use。官方安装只读，保存的 UI 校对文件未改写。

## 窗口适配

`DesignViewport` 同时提供相机 viewport、投影和 GUI 矩阵。战斗设计坐标 1920×1080，主菜单沿用 1600×1000，均只做一次等比缩放。一般窗口按高度缩放核心，横向变宽只扩大可见背景，不拉伸各组件或平铺贴图。

背景最大宽高比暂取 2.5，超过时清屏相机留下纯黑侧边。极窄窗口保底容纳战斗 1520 / 菜单 1120 设计单位的核心宽度，必要时上下留边。这些是本项目配置，不是官方实测极限。投影、手牌布局和鼠标逆变换使用同一矩阵。

棋盘和 TableTop 仍是原 3D 网格层级。背景校对偏移按有效 viewport 像素换算；编辑器预览用独立等比 Fit。旧校对数据不迁移或覆盖。

## 菜单与机制

只在 SCENE_MAIN_MENU 为 Unlit_Texture 去掉无关灰色漫反射乘色，对旧 Self-Illumin_Diffuse 使用双倍光照近似。大小写不敏感地禁用未恢复的 Glow 节点，避免 GlowOutline_Quad 的黑色遮罩挡住横幅。完整木盒 Shader、神光和开盒状态机 **未完成**；未更改其他场景曝光或原始图像。

| 用途 | 原资源 | 恢复方式 |
|---|---|---|
| 亡语 | Card_Play_Ally_DeathRattle_Idle / GenFX_Deathrattle_Skull | 原轮廓网格及材质，不能把不透明原图直接贴成方块 |
| 剧毒 | Poisonous 网格与同名纹理 | 原绿色药瓶；原 URP 占位材质替换为真实药瓶纹理 |
| 扳机 | 既有 MODEL_MINION_TRIGGER | 原闪电，实际触发时扩大并叠加金色光晕 |

三类图标按有效机制并排，沉默后移除。编辑器有 minion/deathrattle、minion/trigger、minion/poisonous 独立槽。bundle/CAB/PathID/GUID/缓存见 [来源清单](../manifests/window-feedback-assets.json)。GPU 已检查方向、透明边缘和药瓶绿色内容。

## 入场、交战与关联效果

英雄攻击的直接伤害、反击和武器耐久在同一个命中点更新；真实 Trigger 仍单独顺序播放。服务器立即完成一次动作，时间均在客户端。

回归发现 Fireplace 的普通 Play 只广播 Summon，不向 targeted_action 观察器发入场事件。本项目在原实体已公开入场、战吼前补 Summon/visual/index，使随从先落地并插入正确位置。这只是公开表现元数据，不添加重复召唤或改变规则。

489 张可玩牌均审计 CardDef 效果分支；233 张接入可用主施放关联素材，另保留 7 张已有独立时间线。最初的 234 组中，NEW1_031 仅有不可见初始化材质，排除后标为未完成。法术在 0.72 秒离手后启动；随从/武器在 0.84 秒显牌后入场，再延后 0.2 秒启动关联效果，主施放默认 0.8 秒。伤害、变形、死亡和历史等待各自阶段。上述是本项目调校值，非官方实测。

分类为 projectile、area、buff、heal、draw、transform。素材追踪 CardDef → SpellPath → Material/AudioClip；原 ParticleSystem/AnimationClip 类型树只留本机。去底/加法 Shader、运动、分层与时序为本项目重实现。初始化 RGB/alpha 为零的层不假装可播放，运行时颜色脚本未恢复。状态及失败原因见 [CARD_PRESENTATION_STATUS](CARD_PRESENTATION_STATUS.md) 和 [审计清单](../manifests/card-presentation-audit.json)。**全部现有牌的原专属动画、完整 FSM/Animator/Shader 和条件分支未完成**。

## 实测与截图边界

- Python 规则/网络/管线：599 项通过，最终全套 32.47 秒。
- Unity Windows x64 构建退出码 0，3527 条断言通过，其中新关联素材/时序/采样断言 1294；不是新增 3527 种规则。最终结果见 work/build-window-release.log。
- GPU：69 项原模型、9 项窗口、343 项关联材质检查；后者覆盖 168 个可见唯一图层和 7 个合成样板。
- 盗贼/战士后台原生双客户端：15 回合、v55，Shrink1 胜，英雄 19/0。
- 法师/战士后台原生双客户端：16 回合、v62，Shrink2 胜，英雄 0/7。

两局使用独立 profile 和测试端口 47253/47254，真实匹配与动作，无状态改写；最终双方公共哈希一致。测试不激活窗口，默认服务器和用户窗口不停止。后台统计约 60 FPS、队列峰值 1，但 batchmode 不绘制完整 GUI、card_render_count 为零，**不能作为正式 1080p 渲染性能证据**。

`python -m tools.window_feedback_report` 验证指定 Profile 的终局、同 MatchId/版本/公共哈希和生命归零，结果写 work/window-duel-results.json。仍在运行的旧服务器不会被开发过程强停；结束当前对局后，可用 `Start-LocalDuel.cmd --port 47240` 启动新版本服务器和双客户端，避免默认端口复用旧进程。

截图在 `tests/visual/current/window-feedback`、`interaction-models`、`associated-effects`，均为 GPU 离屏场景/组件。窗口图不含完整 GUI 文字/输入，关联图仅为材质组合，不冒充实机整屏或原动画截图。用户附件分别归档 baseline/window-feedback 与 reference/oracle/window-feedback。

整屏人工比赛、窗口切换时的悬停/拖动、闪电连续帧、全卡牌原动画分支和正式渲染性能验收 **未完成**。
