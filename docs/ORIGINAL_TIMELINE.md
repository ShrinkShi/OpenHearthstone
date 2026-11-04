# 原 Timeline 动画轨道恢复

本文记录 f3737dc 动画轨道节点的验证状态。后续两个 ControlRandomized 轨道已恢复粒子/激活执行，见 [ORIGINAL_TIMELINE_CONTROL](ORIGINAL_TIMELINE_CONTROL.md)；下文仅执行动画轨、3条控制/镜头未恢复的描述属于该历史节点，当前仍未恢复镜头及通用子Director等分支。

恢复两个 RLK_060 / RLK_061 自定义入场子 Prefab 的 PlayableDirector 图、AnimationTrack 与 TimelinePlay 动作。整套 RLK 仍未启用，全卡专属动画、完整原 Shader / 脚本未完成。

## 源数据

官方 UnityFS 只读，官方 DLL 仅静态读 IL。两个缓存 ID：`2ac223d9b583771c9eb24338`、`b0cbd0aa93fedbfc28ee3f8e`。Source Bundle / CAB / PathID、原轨道及 Clip 参数见同名 `manifests/original-timeline-*.json`。

共同动画 `ReuseFX_Necrotic_DeathKnight_CustomSpawn_FromCorpse` 来自 `essential_base_global-prefab-0.unity3d`，CAB `CAB-69c88b234dfa427e02d1fcee9337ae34`，PathID `-7404933130905737980`。旧缓存漏掉嵌套独立根的 Animator 绑定，曲线曾为 0。现恢复 Director=node1、Animator=node2；同名 FX_Smoke 兄弟通过源 PPtr 和子级顺序精确区分。

183 条曲线已恢复，其中 26 条为 Renderer `m_Enabled` 的精确 CRC32 绑定；该 Clip 未解析绑定为 0。这不代表整个 Prefab / Shader 完成。Clip 长度 2 秒，TimelineClip 开始于 1/12 秒、长度 4.433333333333334 秒，整图 6 秒，不混为自定等待。

源图 7 轨：AnimationTrack、两条 ControlRandomizedTrack、ActivationTrack、CameraShaker2Track、两条 SoundTimelineTrack。Activation 和两条 Sound 源 muted=true。当前执行 AnimationTrack；3 条未静音控制/镜头轨记 UnsupportedTimelineTracks。两个 `unity default resources` 内建依赖警告保留。

## 执行边界

源 TimelinePlay 获取指定 Owner 的 Director 并 Play；finishOnTimelineStopped=false 立即 Finish，true 等 stopped。客户端复用现有 FSM 时钟，不增加服务器等待；既有 FSM 最小步为 0.0001 秒。

当前支持源图使用的 None wrap、零姿态偏移/无有效 AvatarMask、速度、clip-in、pre/post Hold、源非循环曲线和 stopped 时间。混合、非零偏移、有效 AvatarMask、未知 extrapolation / loop 覆盖拒绝并计数。源 Clip 无 Animator 根姿态曲线，不宣称通用 humanoid RootMotion、FootIK、RemoveStartOffset 已恢复。

动画退出 FSM 状态后继续；倒放重建；停止后不持续覆盖后续姿态。同一 Director 重入替换旧时钟，数量有界。原层级/曲线用现有采样器，不以 Tween 替代。

## 提取和验证

```powershell
.venv\Scripts\python.exe -X utf8 -m tools.timeline_source --refresh-actions
.venv\Scripts\python.exe -X utf8 -m tools.timeline_source --file b0cbd0aa93fedbfc28ee3f8e
.venv\Scripts\python.exe -m pytest -q
```

TimelinePlay 精确刷新 21 缓存、0 映射失败；源图审计额外保存在忽略目录 `local/timeline-source-audit`。`--file` 只接受 24 位缓存 ID。

728 Python 通过；新增 1008 Unity/GPU 检查覆盖两个实际源图的开始前/中间/末尾/停止、位置缩放曲线、动作独立完成、明确等待、倒放和未知轨道。使用实际 Clip / 层级和受控测试 FSM，不冒充整套源 FSM 已完整运行。全 GPU 和 Windows 构建退出 0：`work/native-timeline-final-gpu.log`、`work/build-native-timeline-final.log`。

未完成：ControlRandomized 粒子/子 Director、CameraShaker2、音频轨通用执行、混合/RootMotion、ActorEliteEventAction、非传奇 SetMesh/SetMaterial、其余 Shader 和正式专属事件路由。原专属法术程序启用数仍 2；共享片段不计为全卡完成。未操作桌面或做人工 GUI / 60 FPS 验收。

最终构建后台双客户端：独立Profile `timeline_1`/`timeline_2`，端口47411，14回合/version48完成。MatchId `cc92ae3f374d4df2b3bebaaa8b83bbc5`；Shrink1法师-1血负、Shrink2术士23血胜。双方public_hash `d67787ebfd58d4a724998a41f77e27fc1805ff247e005249f4cf576504f30c60` 一致，表现队列峰1；日志未见异常/原效果fallback。后台card_render_count=0，不能用约60的后台帧率证明正式GUI 1080p性能。源参数重复刷新21缓存/0更新/0失败；用户校准SHA256保持 `E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`。
