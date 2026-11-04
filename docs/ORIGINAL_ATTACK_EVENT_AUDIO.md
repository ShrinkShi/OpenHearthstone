# 原攻击事件声音与等待协程

后续节点恢复显式对象绑定下的原UpdateTransform，四攻击实例transform_gaps为0；以下保留历史结果。正式对象选择/控制器仍未完成，见[ORIGINAL_SPELL_TRANSFORM](ORIGINAL_SPELL_TRANSFORM.md)。

2026-10-12。全卡专属动画、完整原 Shader/脚本未完成；完整效果启用数仍为2。本节点恢复离线源攻击实例的声音事件和等待分支，未启用正式对战攻击附属实例。

## 来源与边界

静态读取 SelfAnimUtils.PlayDefaultSound、SoundManager.Play/PlayImpl/RegisterExtension、ActorAttackSpell 及其 WaitThenDoAction 迭代器的原 IL，不执行官方程序集。PlayDefaultSound 使用接收节点自身 AudioSource/SoundDef，不使用 AnimationEvent.objectReferenceParameter；部分事件对象参数实际是 AudioClip，不能将其统一当作 SoundDef。原始身份保存在 manifests/original-script-audit.json 和 original-animation-event-audio.json。

`python -m tools.animation_event_audio` 从8源 Prefab 恢复5音频节点、5片段引用，0提取错误。四个 ACTION_STATE 的默认声音是 FX_Minion_AttackLaunch.wav:84aa8fcefb2bd49459fc5649299b8d17，PCM缓存2670b50038652a0dec74cfb8.wav；第五节点是 GameObjectContainer/SFX_Shatter 的 Ice_FrozenMinion_Impact_Sound.wav，保持其原引用，不改成攻击声。

23d38186 来源为 initial_base_global-2ebe64b8-prefab-4.unity3d，CAB-824a57d31e8915d6162164fd62a4305d，AudioSource PathID3355549892729273198、SoundDef PathID1244363960665799534。清单保留其他来源和完整参数。

## 播放与等待

NativeAudio.ResolveExistingSource 按同节点 SoundDef 的随机列表或主片段选择声音，再乘原源音量/音高；目前仅接受直接clip为空、非持久、无限额且2D的源。播放器在该源节点下创建受其激活状态控制的子 AudioSource，同一源键重复播放重启单个声音，不叠加声音。PCM缓存复用；节点隐藏、实例释放时清理声音。这里没有直接加载或执行原 MonoBehaviour。

ActorAttackSpell 的 Birth 重置等待标记；Action 启动等待请求，原 StopWaitingToAct 事件解除标记。已yield的请求在下一客户端表现帧恢复，并保留当初的 previousState；若已经解除等待，则立即开始。组件协程可跨状态存在，多次Action保留多请求，不强行合并。服务器不等待这些帧。

原 Spell.OnAction 的 UpdateTransform 仍未恢复。SourceBaseAction 记录实际开始时刻和前态，并增加 UnrestoredSourceActionSideEffects，不能将此计数视为原移动/目标任务已执行。完整 SoundManager 的分类、ducking、重复源extension服务、原控制器任务及合成仍未完成。

## 验证

work/native-attack-event-audio-verified.log 退出0：53声音/等待检查，四真实Prefab均 audio=1、action_callbacks=1、unbound_events=0、transform_gaps=1。测试解码实际PCM并核对非零样本，静音播放验证节点所有权、单声音重启和隐藏/释放无泄漏；未进行人工听音验收。

既有57动画事件检查、78源状态检查、运行时和四原入场状态链通过。1163 Python测试通过，14 UnityPy版本回退警告，46.95秒。新增检查从原安装重提取八源音频与缓存逐项比较，未以清单自我比较替代原文件验证。

work/build-native-attack-event-audio.log 构建退出0，实际Mixer Bundle九编译常量与原数据一致。后台attack_audio_1/2在47430端口完成比赛1bc743856cef48cea086d593b75ead22：9回合/version31，Shrink2术士13血胜、Shrink1法师0血负；两端最终hash均为e85c51d53ab423e4421bbb82d6b040718208d57b165f76f1d2510273f5154bd0，客户端及服务器日志无异常/fallback。核对命令行后仅清理本次三进程。

UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。后台回归不能代替新增动画画面、人工比赛或1080p/60FPS验收。未使用Computer Use，未修改官方安装。
