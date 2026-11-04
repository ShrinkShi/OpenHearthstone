# TASK-0005 第二轮用户复验 Feature 交接

日期：2026-10-20。状态：`READY_FOR_REVIEW`；等待全新独立V005-r3。本文记录Feature实现和本地候选证据，不代表独立验收，也不代表完整官方一致。

## 实现范围

- missile粒子simulation-space由来源默认enum映射调整为显式可选policy。只在 `NativeSuperSpellPlayback` 确认的HERO_08bp missile分支，把五个Small missile来源粒子节点传World policy；Start、Impact、Frost Nova及通用Particle继续旧映射。`NativeEffectData.NativeParticle.Create` 对没有policy的调用维持旧行为。
- 伤害数字沿来源 `DamageSplat` → `ActorSplats` 的target Actor路径，在权威Hit处单次触发。通过 `Presentation` 接入DamageSplat绘制；`ClientApp` 使用来源数字字体/轮廓样式。没有额外加一层Presentation文字；hero/minion都沿Actor持有者和源样式显示。
- 粒子生命周期仍通过当前missile对象持有的来源节点执行Begin、Arrival/Impact和dispose；不修改服务端立即结算和权威伤害规则。Review粒子检查读取来源粒子startColor/startSize、实际world坐标/剩余寿命与计数。该字段采样不等同于完整over-lifetime GPU属性验证。
- 双端正式GUI test harness补齐文件原子发布、observer完成marker、authority display gate和连接关闭后再ResetMenu；这些同步/清理行为属于review harness。生产ResetMenu路径未因静态按钮view诊断被改写。

## 最终构建和正式GUI证据

- BuildClient.Build：`work/f005c-build-r15.log`，成功。DLL：`build/client/HearthstoneClone_Data/Managed/Assembly-CSharp.dll`，SHA256 `68BAD1BC92679F3A646229FEE0B5ADE10653369630C5D16ADDA804B26232FBDF`。
- 最终双端随从run：`work/f005c-formal-minion-r10/`，1280×720；driver/observer结果均为`PASS bounded formal GUI/input/DSP; official fidelity unverified`，自然exit 0，42/10项，双方publicHash `643e3941ba082bf423065179d0274568b6d8954ec9d0a71a0bca1834d88c2359`。observer完整Hit窗口4.103216s，driver 4.100756s；两侧Hit callback各1次，友方随从权威生命值7→6。退出时 `powerViews=0`、`nativeVoices=0`。
- 实际友方随从命中画面：`work/f005c-formal-minion-r10/observer-cast-contact-sheet.png`；driver对照：`driver-cast-contact-sheet.png`；双方全阶段：`contact-sheet.png`；机器索引：`evidence-index.json`。observer接触表中的实际画面显示命中爆裂和唯一`-1`；单帧路径/哈希列在`evidence-index.json`。用户参考是阶段截图而非时序gold，不能由其倒推出特效时刻。
- r10的350ms全局接触表未采到0.252s飞行窗口内帧；为补实际棋盘取证，新增仅review driver使用的两张定点截图（没有改变游戏表现/正式采样间隔），新build见`work/f005c-build-flight-r16.log`，BuildClient成功exit0。独立补充run `work/f005c-formal-flight-r1/` 1280×720，42项PASS，driver/observer自然exit0且publicHash与r10相同；该DLL SHA256 `da11e1a29c736715d15c1e0aa394d616dc6cfbf31b4758e16a45b2149f08a05b`，只多出review采样代码。Timeline记录MissileBirth在flow time 1.253640（frame5955，realtime 27.449947s），Arrival在1.505708（frame5997，realtime27.672844s）。`driver/review-flight-mid.png`（frame5965，realtime27.496130s，Arrival尚未发生，SHA256 `af257eae5adf39df51b60f97fd76fbc1648fbcedced4b549d11e556f706c4f1b`）和`driver/review-flight-late.png`（frame5982，realtime27.594810s，Arrival尚未发生，SHA256 `6ed451a8b6e71f31f6f0d75f45d7c8c6e4ec2422964cb34bfc070c403687a354`）均为实际1280×720棋盘GUI；后图可见火球与来源粒子尾迹跨棋盘移动。该补充run的DSP有168个float样本绝对值>1，只用于飞行画面补证，不覆盖r10无clipping的声音结果。
- 实际英雄正式run此前在`work/f005c-formal-gui-r1/`，完整本轮证据另见该目录。新粒子源GPU/时间序列检查在`work/f005c-r2-gpu/`：`flight-*.png`、`missile-body-*.png`、`selected-*.png`、`screen-metrics.json`。Small来源没有TrailRenderer；实际飞行尾迹由来源粒子组成。隔离黑底flight图里尾迹细暗，因此不把隔离图作为通过/失败的唯一标准，也没有人为加亮或放大。

## 声音：来源事件与混音边界

- 同一来源clip：`local/runtime/0e9c3a038b94535def357f71.wav`，SHA256 `7eb2f306314419553eb7623a42bff55d3f6d4cef9121d3127e8cbe41ccccb985`，44.1kHz mono PCM16，91243 frames（2.069002s），source peak .9928894，clipped source samples 0。
- 原Prefab来源记录：`local/runtime/6fbec4e4494122ed6e1902d0.fireblast-start-audio.json` 的 `AudioPlaythroughAction` volume .5，运行链实际volume .4；`local/runtime/96a84893c7b182959b4ccb1f.fireblast-missile-audio.json` 的 `AudioPlayAction` volume .5。二者是两个独立来源action，均各播放一次同一clip，run中时刻27.521982s和27.540022s，起音差18.04ms，近乎全长重叠。UI的 `hero_power_icon_flip_off.wav` 另在27.531649s播放一次，volume .75；命中cue `b1f63568710c974b9314ccdd.wav` 在27.777750s播放。
- 最终scene Listener capture：`work/f005c-formal-minion-r10/driver/scene-mix.wav`，SHA256 `d0ff94f4b606357c355a45465baa39080f95f8b932c8499a3d2a6e859f4cf6b4`，48kHz、双声道float32、916480 frames（19.093333s），peak .9420093、RMS .0858298、clipped samples 0。事件时间/音源/volume/pitch记录于`driver/timeline.json`，混音10ms peak/RMS于`driver/scene-mix-energy.csv`，PCM摘要在`evidence-index.json`。混音峰值未削波不证明设备输出或主观听感正常。没有用户所说版本的原游戏录音，无法完成音频A/B；由用户试听判断异常是否解除。当前不删除已证实原Prefab action、不臆改volume。

## 保留的失败/边界

- 早期r1报告共享/partial write、observer Hit完整窗口只差约30ms，r2-r5的Busy/authority display/窗口时序同步失败，r7完成文件marker/退出握手失败，r8因scene-exit残余 `PowerViews=1` 断言失败，r9确认ResetMenu实际清零后网络排队状态又重建静态合法技能按钮view。r10改为先关闭正式Connection再reset并通过；不把旧夹具FAIL覆盖为PASS。
- r6运行前提因实际桌面1680×1050不符合原1920请求而失败；采用项目正式支持的1280×720完成r10，没有改系统分辨率。
- `RenderMultipleBillboards` SIGSEGV的具体归因Unknown；后续`-nographics`试验违反项目正式Build入口约束，应作为无效build实验，不能将崩溃归因全局映射或该实验。此前NativeEffectChecks源码未被该实验改动。
- 本轮局部新源检查/新build/双端候选不覆盖独立V005；首轮V005-r2的PASS只属于d4fc538固定旧范围。用户参考客户端版本/时序/原音未知；运行相机、遮挡和完整全局粒子/光照配置也未闭合。整Task保留官方完整一致性未知，不得标DONE或宣称完整一致。

## V005-r3建议独立复验入口

在本Feature提交后，以新上下文检查本交接、Task新增要求和代码diff；独立检查来源调用范围/旧simulation-space路径回归、DamageSplat与权威Hit单次绑定、BuildClient成功，并重新执行双方正式GUI和实际AudioListener。不得把本Feature候选结果当V005通过；报告需分别给局部客观PASS、用户听审项和无法比对官方动态表现的未知项。Unity/local/build保持串行独占。
