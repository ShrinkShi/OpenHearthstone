# 手牌、目标选择与比赛转场（2026-10-10）

本轮延续 master / 8ddf319；规则、服务器和网络协议没有重设计。用户校对文件保留，SHA256 为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

## 修复的交互

| 用户反馈 | 当前实现 | 验证范围 |
|---|---|---|
| 手牌向一侧倾斜、窗口缩放时错位 | 按每张牌自己的设计坐标枢轴后乘旋转；保留正常扇形、用户区域偏移/缩放 | 1/2/3/4/5/7/10 张，1920×1080、1440×1024、2335×515 GPU 采样；1220×1110 几何检查 |
| 箭头线段与箭头头部分离 | 所有线段、曲线切向、端点和尖端使用同一设计坐标；输入从窗口像素转换一次 | 缩放后枢轴、端点和逆命中断言，离屏箭头截图 |
| 选择英雄攻击时不抬升 | 44 设计像素抬升、1.11 倍缩放，取消平滑返回；附件跟随区域，名字独立 | 抬升和回落断言 |
| 随从/武器目标指示缺失 | 合法 moves 有目标时提供箭头；先放置，再选目标；武器使用圆形装备预览 | 客户端真实 TCP 输入重放；军情七处特工和毁灭之刃规则用例 |
| 名字和齿轮因比例裁切消失 | 名字锚定窗口左侧，齿轮锚定窗口右侧；核心布局继续等比缩放 | 四种窗口比例边缘位置断言 |

旋转根因与 [Unity 2022.3 GUIUtility 源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Modules/IMGUI/GUIUtility.cs) 的矩阵组合一致：旧调用会先清空 GUI.matrix，再以屏幕轴组合旋转。项目改用 parent × localRotation，卡牌中心不再随角度漂移。

## 比赛生命周期

`BattleLifecycle` 管理表现时钟，服务器仍立即结算。

- 新比赛：双方英雄沿独立入场轨迹落入英雄区，1.25 秒后开始起手发牌；原换牌状态继续负责交互和交换。
- 终局：等待表现队列追上最终权威快照，败方英雄分成 20 个片段，1.65 秒内飞散、脱色并淡出；认输的英雄生命可能大于零，也按 LOST 结果播放。
- 结算：头像、原胜利皇冠和原木制横幅进入；胜负文字使用统一描边字体，按钮在淡入后启用。返回菜单清空生命周期，下一局不会继承旧时钟。
- 英雄裂纹、玻璃破碎垫音和结算进入音与时钟同步；原 victory/defeat jingle 保留。
- F5 开发跳过同时推进表现队列和生命周期；F4 速度继续适用于转场。默认不显示开发 HUD。

20 个片段通过单一 `HeroFragment` 材质绘制，不创建临时 GameObject。原英雄画像和原裂纹贴图来自本机，破碎 Shader、分片运动和转场时间是重实现，非完整原版死亡 Prefab 脚本。

## 继续恢复原粒子通道

新增 `tools.original_particle_tracks`，通过 ParticleSystemRenderer → Material 和同一 GameObject 的 ParticleSystem 关联原模块，不按名称猜测贴图。

已给 EX1_134、CS2_025、CS2_028、CS2_032、CS1_112、EX1_593 共 6 张牌、11 个素材层接入 startColor、ColorModule、SizeModule、RotationModule。颜色键和 alpha 键保留独立时间；标量曲线保留切线、权重和 weightedMode。原始类型树和身份保留在本地缓存、来源清单。

这是模块恢复，生命周期归一化、确定性取最大曲线以及现有二维运动仍有差异。发射形状、碰撞、完整脚本 FSM、Animator 和全部原 Shader **未完成**。全部 489 张现有牌的原版专属动画 **未完成**，前轮 233 组关联素材和 7 张独立时间线不因此变成逐帧还原。

Unity JsonUtility 对缺省嵌套对象的行为会产生默认参数对象，因此 `restored`/`enabled` 是显式开关。旧素材配置走原有路径，不会因空参数变黑或消失。

## 可重放命令

在 E:\HS\HearthstoneClone 下执行：

```powershell
.\.venv\Scripts\python.exe -m tools.lifecycle_assets
.\.venv\Scripts\python.exe -m tools.original_particle_tracks
.\.venv\Scripts\python.exe -m tools.repair_audio_cache
.\.venv\Scripts\python.exe -m tools.visual_scenarios
.\.venv\Scripts\python.exe -m pytest -q
```

Unity 隐藏批处理调用 BuildClient.Build 构建，VisualRegressionChecks.Run 执行完整 GPU 组件检查。GPU 调用需要省略 -nographics。

新增 `--visual-test battle_entrance`、`hero_attack_select`、`attack_arrow`、`combo_target_select`、`weapon_target_select`、`hero_death`。所有测试快照由真实 Match/合法动作生成；正式 LAN 不读取这些文件。

素材来源见 [lifecycle-assets](../manifests/lifecycle-assets.json)、[original-particle-tracks](../manifests/original-particle-tracks.json)。原模型/纹理/音频仅放本地，不提交 Git，也不修改官方安装。

## 实测记录与边界

- 608 项 Python 测试通过，18.94 秒；基线为 599。
- Windows x64 最终构建成功，4101 条 C# 断言，其中输入/TCP 21、交互/生命周期 571。断言数不等于独立卡牌规则数量。
- 完整 GPU 回归 470 项（69+9+343+49），新增 49 项。帧包含正常扇形、完整箭头、英雄入场/死亡多个阶段、结算组件，以及原粒子通道。粒子末段允许按原 alpha 曲线淡出，另检查中段实际可见。
- 初次验证两局：盗贼/战士 14 回合、v58，Shrink1 胜（4 / -3）；法师/战士 16 回合、v61，Shrink2 胜（-3 / 9）。
- 最终构建两局：盗贼/战士 12 回合、v45，Shrink1 胜（28 / 0）；法师/战士 10 回合、v33，Shrink2 胜（-2 / 28）。报告为 `work/interaction-final-results.json`。
- 音频缓存修复后再一局：盗贼/战士 13 回合、v54，Shrink2 胜（-4 / 8），两个完整日志无 FMOD 解码错误。报告为 `work/interaction-audio-results.json`。

双客户端是实际 Windows Player，使用独立 profile、47255–47259 端口，共 5 局后台自动比赛，没有改写终局状态；双方 MatchId、版本和公开哈希由报告工具再核对。仅清理本轮测试进程，用户默认 37240 服务未停止。

音频审计检查 447 个本地导出：原 `ReuseFX_DemonHunter_Glaive_Medium_Impact_Sound` 的运行时 WAV 曾被存为全零缓冲，前轮日志已有相同错误。修复了 1 个缓存，坏文件保留在本地 `invalid-audio`，来源及前后哈希见 [audio-cache-repairs](../manifests/audio-cache-repairs.json)。Importer、常规导出和关联效果导出现在校验容器、PCM 帧完整性后发布；重复审计为 0 修复。新生命周期音频均为有效 PCM WAV。

用户要求不使用 Computer Use，本轮未操作桌面、官方客户端或人工输入。`tests/visual/current/lifecycle` 是正式组件的 GPU 离屏截图，**不是完整 GUI 实机截图**；后台 batchmode 不绘制 GUI，不能证明正式 1080p 60 FPS。

整屏人工比赛、鼠标手感复核、全部专属动画/原 Shader、官方逐帧对照以及正式渲染性能验收 **未完成**。
