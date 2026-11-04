# 原 Prefab 特效恢复（2026-10-10）

当前增量：全缓存352槽/254 Prefab刷新（96捕获组件）、原Glow/环境光数学和取色动作，623 Python / 619 GPU / 124类型静态审计见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。Glow后处理、其他家族和全卡原专属分支仍未完成；未新增启用卡数。

最新增量：三个原 Shader 家族基础数学与混合状态、621 Python / 538 GPU / 119类型静态审计见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。全卡专属动画、完整原 Shader/脚本仍未完成；本文下方旧计数为先前节点记录。

后续增量：原 RenderToTexture 捕获链路、8 个 Shader 的 35 个 DXBC 程序审计及 620 Python / 76925 原数据断言见 [RENDER_TEXTURE_RESTORATION](RENDER_TEXTURE_RESTORATION.md)。下文是上一稳定节点记录；冰霜新星现已能画出主体且黑块消除，但外观不合格，仍未启用。完整原 Shader/脚本仍未完成。

本轮在现有客户端表现队列中增加原 Prefab 数据播放器，没有改写服务器、卡牌规则或网络。全程未使用 Computer Use，没有修改官方安装、参考仓库或用户保存的 UI 校对。

## 已恢复的数据与当前接入

现有 489 张可组牌卡中，319 张具有已导出的特效引用。共导出 **352 个效果槽、254 个不同 Prefab、1879 个 ParticleSystem 对象**，包括主施放、扳机、子法术和选项引用。352 是效果槽数量，不是已完成 352 张牌。导出任务失败为零；不同 Prefab 的依赖解析仍记录 **313 条错误**，主要是包外 Unity 内置资源，不能把导出成功等同于依赖完整。

来源与恢复边界见 [native-effect-audit.json](../manifests/native-effect-audit.json)。每项保存 CardId、效果槽、GUID 引用、Bundle、CAB、PathID、缓存路径、粒子/动画数量、动作频次、未知动作、未解析参数和依赖错误。原纹理、网格、动画/FSM 参数均保存在忽略的 `local/runtime`。

| 卡牌 | 当前运行方式 | 仍未恢复 |
|---|---|---|
| 奥术爆炸 CS2_025 | 原环形层级、网格、粒子和材质动画；替代旧施放图层 | 原混合 Shader 的精确输出 |
| 冰霜新星 CS2_026 | 原数据仅作诊断，播放器禁用；保留现有表现 | 当前只能画出地面阴影，冰霜主体及内部遮罩/RenderTexture 流程未恢复 |
| 地狱烈焰 CS2_062 | 原橙色冲击波叠加现有表现 | 原地面捕获、溶解和完整 Shader |

奥术爆炸、地狱烈焰的播放区间均为 1.2 秒，是本项目调校值，不是已测得的官方逐帧时长。其余 350 个效果槽保持禁用，继续使用现有出牌/效果表现，不会因为提取到数据就替换运行画面。冰霜新星曾用 2.4 秒候选区间测试，但最终画面核验拒绝启用。声音继续通过既有 CardEffectRegistry 与 AudioEventRegistry；原 FSM 内 AudioPlaythroughAction 尚未执行。

## 客户端执行边界

`NativeEffectInstance` 重建原局部层级、独立动画节点、网格和真实 Unity ParticleSystem。已接入发射/爆发、形状、主参数、颜色、速度、力、尺寸、旋转、噪声、限速、继承速度、按速度变化和纹理序列。保留 MinMaxCurve 的双常量/双曲线及关键帧斜率、权重；兼容旧版打包参数和新版 FSM 参数数组。

基础 FSM 支持有界 FINISHED 跳转、等待、可见性、粒子播放/停止、动画播放/速度、材质浮点和颜色。动画绑定使用原 Clip ID，避免同名片段误绑定。AnimationPlaythroughAction 的进入阶段立即 Finish，依据原 IL 调用行为；PlayAnimation 可等待片段结束。AnimateFloat 使用秒时间。RandomWait 暂取区间中点，EaseFloat/EaseColor 暂用线性插值，不能称为完整原脚本。

每个播放器至多保留 4 个活跃实例、32 个程序缓存。离屏相机与独立 layer 隔离比赛实体，RenderTexture 合成沿用 GUI 设计坐标矩阵。实例退场释放材质、相机、粒子和 RenderTexture，网格/纹理由现有资产缓存复用；错误时回退既有表现。队列可以正常速度、2 倍和跳过，服务器不等待任何动画。

原 Prefab 中 Offscreen 子树用于内部遮罩，当前明确隔离，避免黑色矩形覆盖棋盘。外部实体/棋盘绑定、二级相机与动态 RenderTexture、完整外部事件生命周期仍未恢复。Collision、ColorBySpeed、SubEmitter、Trail 模块仍未执行。

### 卡牌专属效果的复用边界

HERO_08bp 的火球飞行证明，原效果未必依赖 `TrailRenderer`：该技能的可见尾迹来自原Prefab中五个世界空间粒子节点。映射应在已确认的该技能Small missile调用点显式启用，不能因此全局改变ParticleSystem的simulation-space映射或假设其他卡也采用相同结构。逐卡来源与实际GUI帧证据见[ORIGINAL_FIREBLAST](ORIGINAL_FIREBLAST.md)。

同一案例还暴露了表现队列的边界：如果一个Hit Cue同时承担受击反馈、长寿命DamageSplat和全局 `Busy`，装饰停留时间就会锁住所有用户输入。新增特效时应把权威应用时刻、输入锁窗口和纯装饰生命周期分别建模；缩短Cue前先确认其渲染回调是否也依赖该Cue存活。

## Shader 与原脚本

`NativeEffect.shader` 和 `NativeEffectComposite.shader` 是本项目的兼容实现，**不是完整原 Shader**。它们读取原纹理、材质声明、UV 参数、浮点/颜色动画及混合参数，处理双层滚动、透明捕获和预乘合成。仅启用原 Shader 实际声明的属性，避免材质残留 `_TintColor` 导致地狱烈焰变蓝。Intensity 淡出同时影响捕获 alpha，避免淡出后留下黑色圆盘。原平台编译代码不能作为可直接编译的 ShaderLab 源码使用；深度、灯光、遮罩和各 Shader 家族的精确语义尚未恢复。

`tools/export_script_il.ps1` 用 Unity 自带 Mono.Cecil 静态读取官方 Managed 程序集，导出 113 个相关类型的方法 IL、调用关系和程序集 SHA256。**没有执行官方 DLL，没有将官方程序集加载进比赛客户端**。本机完整 IL 位于忽略的 `local/script-audit`，审计见 [original-script-audit.json](../manifests/original-script-audit.json)。IL 导出用于解释行为，不代表已经还原完整 C# 脚本及其引擎依赖。

## 可重复检查

在项目根目录运行：

```powershell
.venv/Scripts/python.exe -m tools.native_effects --all
powershell -NoProfile -ExecutionPolicy Bypass -File tools/export_script_il.ps1
.venv/Scripts/python.exe -m pytest -q
```

原数据导出默认禁用对应槽。Unity 执行 `NativeEffectRenderChecks.Run` 后，人工核对生成图，再运行：

```powershell
.venv/Scripts/python.exe -m tools.verify_native_effects --review manifests/native-effect-review.json
```

启用校验要求可见阶段、黑遮罩上限和程序 SHA256 与 GPU 报告一致。报告写入 `tests/visual/current/native-effects/render-report.json`，三张棋盘组合图为 `*-board-components.png`。这些是实际 GPU 离屏组件渲染，**不是正常 GUI 整屏截图或人工比赛录像**。

八张候选牌共采样 48 帧；20 项 GPU 断言验证转移上限、三个候选效果的可见性/黑遮罩、组合帧非空、合成矩阵和泄漏边界。冰霜新星的组合帧从空白的 0.6 秒改为可见的 0.3 秒，但只有灰色地面影子，最终画面核验拒绝启用。可见性通过不等于外观恢复。其余候选仍有不可见或遮罩问题，不启用。76916 条 C# 原数据断言主要检查全部导出节点、参数和索引，不能冒充独立规则用例。另有真实发射/重放、材质曲线、零时刻遮罩隔离、队列与快进检查。

性能报告记录每个特效 30 帧的离屏 Seek/Render 耗时；它只覆盖特效路径，不证明完整 1080p 游戏稳定 60 FPS 或长期无泄漏。

## 实际验收记录

- Python：最终全量 618 项测试通过，48.93 秒；本轮基线重新运行为 608 项通过。启用清单修改后亦重复执行十项专属测试通过。
- C#：原有 4101 条断言保持通过，新增 76916 条以原数据完整性为主的断言单独统计。
- GPU：69 + 9 + 343 + 49 + 20 = 490 项检查通过；48 帧为候选诊断采样，不能累计成通过数量。
- Windows x64 构建成功；测试和导出时用户校对文件 SHA256 始终为 `E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`。
- 独立端口 47310 后台双 Windows 客户端：Shrink1 法师胜（5 HP），Shrink2 战士败（-2 HP），12 回合 / v49，比赛 `cc466261e2f34c7281d165615cd4c75e`。双方公共哈希 `e22476f7ddc88f60ea45eebbe0a347b579f509a291ca1115628527f698611cf7`，两个日志无运行异常。自动输入通过正常权威服务器完成，不是人工局或整屏动画验收。
- 最终构建独立端口 47312：Shrink1 术士败（-1 HP），Shrink2 战士胜（14 HP），11 回合 / v36，比赛 `6a9990f218c94fea878cd4a24d310a9e`。双方公共哈希 `4356a649a29dab080f16a731992a8531d6f6c2244954215450a14fe483620c0a`，两个日志无运行异常。测试进程按 PID/端口/Profile 核对后退出；原 37240 服务器保持运行。

前两局使用最终二进制及三项候选配置（冰霜新星后来取消启用）。第三局使用最终两项启用配置，独立端口 47314：Shrink1 法师败（-2 HP），Shrink2 术士胜（4 HP），14 回合 / v45，比赛 `fd2863ec8d184fe1b1772135a853a8b0`。双方公共哈希 `a55618330c8a435f575d2a5a60f781121f98299d54fbc9657a59e8ed6c5654de`，两个日志无运行异常；仅测试进程按 Profile/端口核对后退出。

后台 batchmode 对局验证规则/协议/队列追赶，不执行正常 GUI 特效绘制；原特效画面由上述 GPU 离屏检查验证，不能混淆两种证据。

**未完成：全部现有卡牌原版专属动画、完整原 Shader/脚本、条件分支、音频 FSM、原内部渲染链、整屏人工视觉验收与长期性能验收。**

## TASK-0006：表现批次与输入锁分离

F006修复了法师命中后把长装饰尾段误当全局输入锁的问题。`Presentation.Busy`仍表示当前/排队的表现批次，`Presentation.InputLocked`表示何时必须禁止合法游戏操作；用户输入路径用`InputLocked`，结果/选择/其他生命周期等待保留各自的`Busy`条件。HERO_08bp短Hit反馈在原生Arrival后更新画面；独立DamageDecoration保留原DamageSplat贴图/数字曲线，在4.1秒对象寿命内自行绘制和过期；Reset/Skip清理装饰。权威服务器仍即时裁定。

`FireblastReviewChecks.RunInputLockChecks`覆盖Feedback窗口、原生清理仍活跃时`Busy=true && InputLocked=false`、排队顺序和独立装饰过期。F006正式双端GUI r3实际在`Busy=false && DamageDecorationCount=1`窗口接受后续End Turn，随后装饰消失、双方hash一致。见[执行者交接](handoffs/F006-task-0006.md)与[任务](tasks/TASK-0006-all-hero-powers.md)。这只验证本轮锁修复；其他10个基础英雄技能的逐卡原生FX索引入口及`VOICE_*`/`VFX_*`运行注册映射仍缺，逐项规则/画面/Listener结论见[基础英雄技能矩阵](HERO_POWERS_SOURCE_MATRIX.md)，不能从通用程序绘制推导原生技能表现。
