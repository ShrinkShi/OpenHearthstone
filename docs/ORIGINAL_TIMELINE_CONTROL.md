# 原 ControlRandomized 粒子轨道

本文记录 03a8ddc 节点的历史验证。后续已恢复 node31 的原 ColorBySpeed，见 [ORIGINAL_COLOR_BY_SPEED](ORIGINAL_COLOR_BY_SPEED.md)；完整入场仍未完成。

后续 CameraShaker2 轨道已恢复，见 [ORIGINAL_TIMELINE_SHAKER](ORIGINAL_TIMELINE_SHAKER.md)。本文旧镜头缺口描述属于历史节点。

恢复 RLK_060 / RLK_061 两个子 Prefab 的两个 ControlRandomizedTrack，接续 [ORIGINAL_TIMELINE](ORIGINAL_TIMELINE.md)。完整专属入场及全卡原 Shader / 脚本仍未完成，不扩大正式启用名单。

## 静态源证据

官方 DLL 仅由 Cecil 读取 IL。`manifests/original-script-audit.json` 记录 Assembly-CSharp.dll / Unity.Timeline.dll 的 SHA256、类、调用及忽略目录的 IL 文件。重点类型：ControlRandomizedPlayableAsset、ControlRandomizedBehaviour、ParticleSystemWithData、ControlRandomizedParticleControlPlayable、ControlRandomizedHelper、ActivationControlPlayable。

源自定义 ParticleControl 与 Unity 默认 ParticleControl 不同。Initialize 停止并清空系统及孩子，systemTime=0。OnBehaviourPlay / Pause 仅将 lastTime=-1。PrepareFrame 不推进 inactiveInHierarchy 系统；相同时间不重复模拟。按 startDelay.Evaluate(randomSeed)、duration、fixedDeltaTime/2、maximumParticleDeltaTime 决定重启或增量；跨周期使用源余数与修正；Simulate 使用 withChildren=true 和默认 fixedTimeStep=true。NativeRandomizedParticleClock 保留这些分支，未换成绝对时间自造 Tween。

源 ControlRandomizedPlayableAsset 的 GetParticleSystemRoots 在遇到 ParticleSystem 后停止向下寻找，不能把全部后代分别当根模拟。sourceGameObject 通过 Director ExposedReference 解析；本节点 source node31 和 node34 是同名但不同 PPtr 的 FX_Smoke，各自根为31/34，拥有子粒子32/33和35/36。原轨道区间分别 0~2.0833333333333335、0.9666666666666667~4.516666666666667。

## 集成和边界

源 Track.active=true 控制激活；轨道退出暂停时钟并停用目标；销毁恢复 postPlayback（实际两个源均Restore=2）。原行为获取默认 GetComponentsInChildren 列表、保存 auto seed 标志，OnPlay 停止/切手动种子，仅在列表非全存活时随机化，销毁恢复标志。范围为 Random.Range(1,9999)，不包含9999。

客户端粒子创建已有确定性适配。本节点继续以实例 System.Random(117) 提供相同整数范围，使倒放和视觉测试可重现；这不是复原官方全局 Unity.Random 的具体随机序列。恢复当前实例拥有的 auto seed 快照，不宣称恢复所有资源原始 auto seed 导入字段。

轨道控制的整个粒子子树从既有 ParticlePlayAction 采样循环排除，避免父根带孩子模拟后，孩子又被独立推进。重入释放旧控制后再捕获新状态，倒放/Dispose 恢复拥有对象；服务器不等待动画。

源缓存带 control 节点、根集合、active/post/search/update 标志和兼容审计；原始字段与精确来源仍在两个 `manifests/original-timeline-*.json`。本阶段仅接受本源图无 prefab 实例化、无混合、无 extrapolation 的分支。源层级扫描确认这两个目标没有要同步的子 Director / MonoBehaviour；这不等于已实现 updateDirector / ITimeControl 通用分支。未知子脚本/Director、动态实例化、混合等明确拒绝。

## 实际验证

730 Python 通过。16 项新增粒子时钟检查对照实际 Unity 粒子引擎的初始/增量/重复/倒放/跳跃/含delay采样，并验证 inactive、Play/Pause 重启和种子所有权。现有 Timeline 专项增加至1034项，覆盖两个实际原图的粒子生成、源区间激活、退出、倒放种子重现，原183曲线仍通过。

受控 FSM 的4张实际离屏图在 `tests/visual/current/native-timeline-control`，0.20 / 1.20 秒、64×64。本阶段1.20图仅见很小青色效果，不能作为整套入场视觉一致的证据；未恢复的 Actor/Elite、材质/Shader及镜头仍限制完整画面。不是人工比赛、1080p完整GUI或60FPS验收。官方客户端未修改，未操作桌面。

未完成：CameraShaker2、音频轨通用执行、子Director / ITimeControl、原Prefab递归实例化保护、完整随机种子导入/全局状态、Timeline混合/RootMotion、ActorElite及非传奇Mesh/Material、完整Shader、专属事件路由。继续沿源脚本推进，全卡目标保持。

已确认源node31还有既有未恢复的 ColorBySpeedModule；32~36当前unsupported模块列表为空。这一模块缺口保留，不以成功生成粒子宣称每个原粒子参数已完整实现。全GPU回归 `work/native-timeline-control-final-gpu.log` 退出0，原40程序/133生命周期/798帧调查0异常；Windows构建 `work/build-native-timeline-control-final.log` Result: Success。UI校准SHA256保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

最终构建独立Profile timelinecontrol_1/2、端口47412完成后台自动比赛：12回合/version41，MatchId=0dc0e578b35d405781c688d197fa263d，Shrink1法师0血负、Shrink2术士22血胜；双方public_hash=3dfd9915ce2fd242258c2e530eee2a0a5301026f91a1188e76884e7e9bd77296一致，表现队列峰1，日志未见异常或原效果fallback。后台card_render_count=0，不能据此宣称正式GUI/FPS或人工视觉验收。源图重复导出SHA256一致，缓存幂等。
