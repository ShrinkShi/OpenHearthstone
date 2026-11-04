# 原 EaseFloat / EaseColor 恢复

2026-10-10，继续全卡原动画、Shader、脚本目标。本节点恢复当前资源实际使用的缓动分支，不代表完整原脚本或全卡动画完成。

## 来源与参数

仅静态读取本机 `Blizzard.T5.PlayMaker.Runtime.dll` 的 `EaseFsmAction`、`EaseFloat`、`EaseColor` IL，不加载或执行官方程序集。原方法记录位于 `local/script-audit/`，类型审计仍为 restored=false。

`python -m tools.native_effects --refresh-ease-cache` 使用既有源 FSM 刷新工具，核对状态、启用动作顺序和参数身份；34 个 Prefab、122 个动作（70 Float / 52 Color）、1220 个参数，0 unresolved、0 失败，重复零更新。CAB / PathID / 源 Bundle 见 [native-ease-refresh.json](../manifests/native-ease-refresh.json)。不改层级、几何、用户校对或启用列表。

当前 easeType：21=77、1=26、2=6、0=4、4=4、19=4、3=1。speed 全部 None，reverse 全 false，realTime 全 false；delay 有 None 和 0 / .15 / .2 / .25 / .3 / .5 / 1 / 2 秒。None speed 的原值是 1，不能用序列化占位 0。

## 运行时行为

- 原枚举 0/1/2：二次入/出/入出，3/4：三次入/出，19：圆形出，21：Unity Mathf.Lerp。按静态 IL 运算及分支实现，替换旧统一线性插值。
- OnEnter 捕获 from/to 并立即写初值，reverse 写 to；四个 RGBA 分量独立计算。每个动作拥有独立时钟。
- 原 Ease 的延迟先减到负值，随后一帧开始；延迟期间 runningTime 不推进。这与 AnimateFsmAction 的时钟不同，不能共用错误的时间语义。
- 到达终点时先写最终值，下一步 Finish / finishEvent；普通 FINISHED 等待所有已绑定缓动结束，显式事件使用实际采样时刻进入下一状态。
- SetMaterialFloat/Color 持续读取原变量；Seek 回退重建动画、初值和状态。
- 未恢复的 easing 类型、缺失参数/变量、实时墙钟和动态控制值明确计入 UnboundEaseActions，阻止凭错误时长提前 FINISHED。当前源样本不使用这些控制分支。

其余 22 种原 switch 分支、动态控制值/墙钟、完整 PlayMaker 与 Animator、全原 Shader/后处理及逐卡目标/生命周期仍未完成。

## 验证与视觉边界

检查包括全部 122 个动作参数绑定、七种非线性采样、真实冰霜新星两秒延迟/三次淡出进入节点 29 的原 _Color、终点及回退；真实暗影效果圆形 EaseFloat 进入节点 6 的 _AlphaIntensity。另检查反向播放、下一步完成和原 finishEvent 转场。

离屏 GPU 的 CS2_026-2.00.png 仍是局部蓝白碎片；目标场景、其它 Shader 与后处理未完整恢复，冰霜新星未启用。当前仅既有奥术爆炸与地狱烈焰两个槽正式启用，未新增原动画认证。

后台双 Windows Player 自动对局使用独立 ease_1/2、47350 端口，14 回合/v47，Match `02e0c28cd39e430892fcb943535d5455`；Shrink2 术士 18HP 胜、Shrink1 法师 0HP 负，公开哈希 `c69f3282fd6da7bfbbb873db46baa75e57e206c1808563f06397a7373f230fc6` 相同。不是人工比赛，batchmode 不绘制普通 GUI，帧率不能作为正式 1080p 验收。

没有使用 Computer Use，没有操作官方客户端，也没有修改 `local/ui-calibration.json`。

验证：664 项 Python 全套、后续 37 项资产工具测试、4101 项既有 C#、77148 项原资源/运行时检查通过（新增 144 项，其中 122 项是参数覆盖，其余含运算与真实材质/转场行为）。最终 Windows 构建成功，日志 `work/build-native-ease-final.log`。GPU 30 项 / 84 帧通过，日志 `work/gpu-native-ease-final.log`；专门 CommonFX/Glow GPU 套件本节点未重跑。两项已启用程序 SHA 和用户校对 SHA 与开工一致。
