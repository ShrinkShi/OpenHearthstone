# 原 iTween 完整曲线分发与完成姿态

2026-10-12。恢复原库所有 35 个已声明 EaseType 的曲线分发，以及单独的 Punch 标量函数；并非完整 iTween、ZoneHand、全卡专属动画或完整 Shader/脚本完成。完整效果启用仍为 2。

来源为只读静态 IL：`local/script-audit/Blizzard.T5.iTween.dll.iTween.il.txt`，SHA256 为 097FD7AC19D17E913AC92D3A26C394D41747475A8E72BDBA8C60443CEE24D057。对应枚举为同目录 iTween.EaseType 文件。没有执行或加载官方程序集。原 ZoneHand.FocusCard 使用枚举 16（指数缓出）、15（指数缓入）、5（三次缓入缓出）；现有运行时此前只覆盖 10 种已扫描到的效果曲线，现在已覆盖整个声明枚举范围。

## 保留原数值行为

- 补齐 Cubic/Quart/Quint、Sine、Expo、Circ、Spring、Bounce、Back、Elastic 与两种专有曲线。保留原负向增量、过冲、浮点系数以及 Elastic 的 0.3 周期；不使用另一套通用 easing 枚举代替。
- 原 Expo 没有特殊端点补偿：easeInExpo 在 0 时有 1/1024 余量；easeOutExpo 在 1 时为 1023/1024；InOutExpo 端点有 1/2048 余量。这些原函数值原样保留。
- 原 GetEasingFunction 对 enum 32（punch）实际返回 linear。单独 Punch(amplitude,time) 是围绕零衰减的函数，端点为零；没有把这两条路径混同。
- 原 easeInSineOutExpo 比较 time 与 start/2，而不是固定 0.5；原分支及不连续边界保留。未知或未声明枚举仍拒绝执行，不能用原默认 linear 掩盖资源未解析。
- NativeEffectInstance 的 ScaleTo/FadeTo 在完成帧按原 ApplyScaleToTargets/ApplyColorToTargets 精确写入目标值，然后派发完成回调。以前直接保留曲线采样值；新增 Expo 支持会暴露其端点余量，因此需要同步恢复这条完成路径。
- 手牌深度计算的除数改为原 float 6*count，避免大整数乘法与源 IL 不同。正式 HandPresentation 未切换，用户校准未修改。

## 实际验证

`work/native-complete-itween-third.log`：460 项新增 Unity 检查通过，退出 0；包括全部枚举的三个固定采样、负向增量、归一时钟边界、专有分支和 Punch。每种枚举均通过真实原准星图的 ScaleTo、材质 alpha、精确完成与倒放重播验证。控制测试仅替换曲线/时长并保留原 SetScale 初始动作，不作为原卡视觉一致性证据。测试代码最初使用不存在的 AssetRegistry.Dispose 导致编译失败，已修复；第二次发现测试误去掉原 SetScale 初态，也已纠正。成功证据以第三次日志为准。

既有 91 手牌姿态、16 区域布局、24 区域列表、61 任务执行、33 历史集合、27 目标集合、35 对象选择、29 定位、57 攻击等待/音频及原动画/召唤检查链继续通过。

`work/native-complete-itween-survey.log`：40 原程序、188 个声明动作、133 生命周期场景、798 帧、0 异常。35 场景全程没有可见像素；源准星 Birth 的 0.30 秒截图仍只有中央小亮点，不能宣称整卡效果已经恢复。诊断图片在 tests/visual/current/native-itween；本次没有官方参考截图对齐、人工比赛、听音或 FPS 验收。

Python 全量 1165 通过，15 个 UnityPy 版本回退警告，95.27 秒。`work/build-native-complete-itween.log` Windows 构建退出 0；实际 Mixer Bundle 九个编译常量匹配。

后台 complete_itween_1/2 在端口 47439 完成比赛 898df7d35edb4084adba59d67d927b5c：15 回合/version49，Shrink2 术士 10 血胜、Shrink1 法师 -3 血负。两端 hash 均为 294dbed0446a5e8f8b01171121962189102ff8260a047ef9a6eeafac4fb016af；日志无 Exception/fallback/Error。终局核对命令行后仅清理自有 PID 21764/2292/14512。后台 card_render_count 为 0，不作为 GUI 动画完整播放的证据。

UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未使用 Computer Use，未修改官方安装。

## 未完成

原 iTween 完整组件的路径/移动/旋转/循环、Rigidbody、Update 调度与所有回调；ZoneHand.FocusCard 完整状态机/场景参数/投影/布局运动；正式攻击 Actor、原历史与 Controller 连接；其余 Shader 变体、后处理和所有专属效果的整卡合成。不能因曲线支持完整就扩大原效果启用名单。
