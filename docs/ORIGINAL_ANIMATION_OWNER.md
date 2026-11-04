# 原动画自身目标与停止动作

2026-10-10，接续 c4b2806。全卡原专属动画、完整原 Shader/脚本仍未完成。

## 实际错误与源证据

原 FsmOwnerDefault 的 ownerOption=0 选择 FSM 所在 GameObject。解析器先正确解析自身节点，随后继续解析未使用的 gameObject.value；空指针覆盖了自身节点，变成 -1。此错误会让旧版动画 Play/Speed/Stop 丢失原目标，不是单纯的缺失素材。

现按原选择分支直接保留自身对象，不读取未使用的显式对象。两个 Python 回归分别使用空指针和过时非空指针，确认 Reader 只解析真实自身目标。显式对象、外部对象和动态变量没有改成猜测的自身对象。

原 StopAnimation 静态 IL：Blizzard.T5.PlayMaker.Runtime.dll / HutongGames.PlayMaker.Actions.StopAnimation，DoStopAnimation 的 IL_0022 判断 IsNullOrEmpty；空名称在 IL_002f 调用 Animation.Stop()，其他名称在 IL_0046 调用 Animation.Stop(string)。OnEnter 调用后立即 Finish。原程序集只做 Cecil 静态调查，没有加载或执行。

## 可重复刷新与实现

`.venv\Scripts\python.exe -m tools.native_effects --refresh-animation-cache` 按原 reference 重建层级并核对节点名称、父级、路径及 FSM 状态/动作/参数序列。只刷新 AnimationPlaythroughAction、PlayAnimation、SetAnimationSpeed、StopAnimation 的原参数；不重新导出整个池、不自动启用效果。

首次更新 111 个 Prefab、220 个动作、1214 个参数，零失败；重复运行零缓存更新。manifests/native-animation-refresh.json 保存原 reference、FSM source Bundle/CAB/PathID、目标节点和未解析参数。-1 外部节点仍没有被绑定，unresolved 参数计数为零不表示全部目标可用。

NativeEffectInstance 按指定名称或空名称停止本节点旧版 Clip；在精确动作时间采样最后一帧后移除时钟，停止动作立即完成。其他节点、其他名称和 Animator 分支不被误停，回退重建可以再次播放。动态名称或未绑定节点计入 UnboundAnimationActions，不冒充完成。

直接使用本机 Unity Animation/AnimationClip 的对照样本验证：Stop 不存在的名称保留当前动画，Stop 当前名称后 IsPlaying=false，单独停止并 Sample 保留已采样姿态。该验证使用自建曲线，没有执行官方脚本。完整 CrossFade、PlayMode、多层混合、StopOnExit、自动停用与 LoopEvent 仍未恢复。

## 原月火术与实际验证

EX1_173 的 ReuseFX_Lunar Prefab：reference 和源 FSM 身份见刷新清单。三个 StopAnimation 原目标恢复为节点 1；Birth/Death 的 PlayAnimation 目标为 1、11、20。实际 Death 分支没有未绑定停止动作或 FSM 超限。

- 674 Python 测试通过，43.69 秒。
- 77200 原资源/运行时检查、4101 既有 C# 检查通过。
- 34 Prefab GPU 检查通过，108 帧；Windows 构建成功。
- 日志 work/native-animation-owner-prefab-checks.log、work/build-native-animation-owner.log。

新构建后台双 Windows Player ownerstop_1/2、端口 47364 完成 12 回合/v38：Shrink1 法师 3HP 胜，Shrink2 术士 0HP 负。Match 90e14dc6821c4ce3997931fb1de25b1d；双方公开哈希 a3c18a9bb61a5daad855b90372b3bd96f62c54b1774c00398512666e19316669 一致，队列峰值 1。后台不绘制普通 GUI，不作为人工比赛或正式 FPS 验收。

已查看 EX1_173-0.30.png 和 EX1_173-Death-0.30.png：Birth 几乎不可见，Death 是小蓝白球。两分支各四个采样可见、最大 1174/1611 像素，但目标场景、尺寸及完整效果未完成，月火术保持禁用。

现有两个启用槽已重拍并核对，参数刷新改变缓存字节 SHA：CS2_025 为 bb4a2d2c9dea10b9a69a13ee9e38c2fdc210221b2a5bce796316ec7769af4aca；CS2_062 为 796d5f415567af7681a1e9632b3e2987661b148f8bea4e2a0a8bf22b43151e54。通过原 review 的 GPU/SHA 验证重新登记，仍为 1.2 秒 replace 与 .35 秒伤害/2.1 秒 supplement；没有新增启用槽。

全池现有 489 张牌、352 效果槽仍只有两个正式启用。完整原动画/脚本、全局后处理、原版同状态连续帧和正式 FPS 未完成；地狱烈焰中央过曝仍存在。

未使用 Computer Use；官方安装、用户 UI 校对文件与原服务器/规则架构未修改。
