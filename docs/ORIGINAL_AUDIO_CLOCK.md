# 原音频动作时钟与PCM播放

后续已将本节点回调用于正式友方原SUMMON_IN召唤链，见 [ORIGINAL_BATTLE_SUMMON](ORIGINAL_BATTLE_SUMMON.md)。以下保留音频时钟节点当时记录；完整原SoundManager仍未完成。

2026-10-11。全卡原专属动画、完整原Shader/脚本**未完成**。本节点只恢复已核对的原召唤AudioPlaythrough分支，不扩大原效果启用名单（仍为2）。

## 来源与执行

沿用 [原召唤音频来源](ORIGINAL_SUMMON_AUDIO.md) 的精确SoundDef、AudioSource和FX_MinionSummon_Drop WAV。`python -X utf8 -m tools.native_audio --bind-actor-spell` 将独立音频文件关联至该源 `.actor-spell.json`，按原CAB/PathID核对身份，精确刷新AudioPlaythrough的Object参数；不改视觉节点、曲线、材质或已有目标节点，不改已启用 `.native-effect.json`。

FsmObject现在明确区分null与外部未绑定对象。原AudioPlaythrough的OneShotSound/OneShotClip均为空时，使用目标同物体的源SoundDef。VO和动作InstanceLimited分支显式拒绝并记录UnboundAudioActions；没有已导出音频定义也会记录缺口，不猜测音效。

`NativeAudio.Select` 保留原随机权重累计 `sample <= cumulative` 和末项后备，包括零权重边界。随机音量/音高保留原范围；本地固定seed便于重放验证，未复刻原全局随机数状态。音量按sourceVolume × codeVolume × defVolume，音高按sourcePitch × defPitch；原类别/ducking/codePitch服务仍未恢复。

NativeEffectInstance在表现时间轴排音频动作，原入场等待结束约0.5秒时发出落地声回调。m_Delay另行保留；m_FinishedEvent在发起播放时发出，不等1.198667秒WAV结束。零延迟动作在FINISHED转移前发出。回退重建随机决定和逻辑cue，物理回调按机器/状态/动作/进入时间去重。退出状态取消未到时的cue；实例释放清除待发cue，释放后Seek拒绝。

## 实际播放桥接与限制

`NativeAudioPlayback` 按缓存文件加载PCM16单/双声道WAV，保留采样率、采样数和PCM数据；缓存最多32个Clip，同时最多16个独立AudioSource，容量不足报错，不隐式改选音。每个voice独立音高/音量，正常帧回收结束voice。离开比赛停止voice，客户端销毁释放Clip。NativeEffectPlayer的回调已连接ClientApp播放桥接。

这是自行重实现的本地2D播放桥接，**不是完整原SoundManager**。原3D空间衰减、混音器、ducking、原实例限流/池、持久声音、SummonVO/KeywordVO、非空OneShotSound、直接AudioSource Clip、更多SoundDef派生类型均**未完成**。正在播放的声音归播放桥接管理，单个视觉实例释放不会截断已发出的声音。

真实原Actor/效果表的正式比赛路由**未完成**，所以本节点不会使普通比赛突然改用原召唤声；现有音效不受替换。不能把源fixture的回调/播放验证当全卡已接入。

## 验证

29项新Unity检查：原权重边界、真实召唤0.5秒时序、随机音高、回退去重、大步Seek、显式VO缺口、释放、延迟完成事件、瞬时转移，以及真实WAV到Unity AudioClip的48000Hz/单声道/57536采样、非静音PCM、缓存复用、独立voice配置和清理。非空直接Clip与主SoundDef字符串分支拒绝，不错误改选随机音效。实际AudioSource测试强制mute；**未完成听感验收**，不播放声音打扰用户。

最终704项Python通过（39.76秒），包含源绑定身份/视觉参数保留检查。Windows构建和既有4106项客户端、78172项原运行时、34项效果表、26项Actor动画检查通过。完整GPU回归也通过，iTween调查40程序/188动作/133生命周期/798帧，0异常；标准GPU41检查/146帧，Actor18、生命周期7、显隐4、ScaleFrom8、模板12、效果表13检查保持通过。最后两个拒绝分支检查随后再次Windows构建通过，视觉代码未变。

双Windows Player后台自动对局：nativeaudio_1/2，端口47394，16回合/v51；Shrink1法师4生命胜、Shrink2术士-1生命负。match_id `f6c86b52c9bc4c759b0b4b1c888eb3ef`，双方public_hash `b0f99f655abfaafa51d3614ed112ee850c7bd33bafaf17099a14750f4fbf8e5c` 一致，队列峰值1。后台验证不代表人工比赛、听感或完整GUI 60FPS验收；最后两个音频拒绝分支不影响正式现有两个启用效果。

日志：`work/build-native-audio-final.log`、`work/native-audio-clock-gpu.log`、`work/native-audio-checks.json`、`work/window-duel-results.json`。测试客户端20068/13572和服务器33192按PID/命令行核对后清理，用户服务器10536保留。不使用Computer Use；官方安装只读，原DLL只作静态IL读取。用户校对文件SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
