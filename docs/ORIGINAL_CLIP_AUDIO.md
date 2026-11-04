# 原 AudioPlayClipAction 与声音引用

2026-10-11：从183个当前源效果程序恢复347个AudioPlayClipAction的类型化SoundDef引用，导出130份SoundDef及132段原AudioClip。完整全卡动画、Shader / 脚本仍未完成。

## 来源与提取

只读原AudioPlayClipAction、其Delay迭代器、SoundManager.PlayClip / GenerateAudioSource / LoadClipForPlayback及SoundUtils的IL；不执行官方程序集。调用及程序集哈希在manifests/original-script-audit.json。

`python -m tools.native_clip_audio` 从原FsmObjectParams恢复SoundDef MonoBehaviour、Bundle / CAB / PathID，导出主音频和随机列表的精确引用，并保存SoundDef和原AudioSource组件值。音频使用现有Importer/解包路径，不按模糊名称猜测。PCM留在local/runtime；manifests/native-clip-audio-assets.json记录每段音频的来源、SHA256、采样数、声道、位宽和采样率。`--audit-only`只核对已有导出并更新清单。

两个FromCorpse子Prefab引用的原Cast为ReuseFX_Necrotic_DeathKnight_CustomSpawn_FromCorpse_Cast_Sound.wav:d1e85cea921b1764fb1d7ae7800edbd4，Impact为ReuseFX_Necrotic_DeathKnight_CustomSpawn_FromCorpse_Impact_Sound.wav:7adb795c51925f044abdc17b4e2746cb。Random子图两动作volume0.8，普通子图两动作volume0.7；pitch1、spatialBlend0、category1相同，Impact均延迟0.8秒。没有将普通版音量改成Random版。

## 源脚本行为

OnEnter在m_Delay>0时启动Delay协程，否则立即Play。协程先检查剩余值，再减Time.deltaTime并yield null，下一次迭代发现剩余<=0才Play。NativeClipAudioDelay保留检查、减法、yield的顺序；同一采样时刻的重复Update不允许提前结束等待。

Play通过SoundPlayClipArgs设置非None的volume / pitch / spatialBlend，以及非零category；调用SoundManager.PlayClip(generateNewSource=true)，然后Finish，不能等WAV整段播放完才推进FSM。源随机列表优先于主音频；空随机列表才回退m_AudioClip。音量和音高覆盖值再乘SoundDef随机范围。

NativeEffectInstance现在保留独立音频动作等待，读取原SoundDef与精确PCM，通过已有NativeAudioPlayback播放桥发送音频cue。倒放重建cue，但已经交付的回调不重复播放。服务器不等待音效。

## 明确未恢复的边界

这不是完整SoundManager的复刻。全局SoundConfig模板、3D路由、分类混音、ducking、语音/实体标签抑制、持久声音、实例限制仍未完整恢复。当前动作只消费已解析的显式2D参数、空模板、无VO/实例限制的分支；其他分支明确计入UnboundAudioActions，不把通用音效当成原声音。随机抽样采用现有客户端可重复序列适配，不宣称原全局Unity随机序列一致。

提取347个动作不等于347个动作所有运行分支已验收，也不等于183个整卡效果完成。两个FromCorpse完整入场仍未启用；完整法术启用数保持2。没有操作桌面、播放用于验证的可听音频或改官方安装/用户校准。

## 实际验证（2026-10-12）

全量Python测试1132项通过。实际Unity ClipAudio检查32项通过，覆盖两份源动作的精确PCM加载、0.7/0.8音量、延迟协程完成、FINISHED转场和倒放重播；完整GPU回归退出0。Windows构建Result: Success、退出0。日志为work/native-clip-audio-gpu-3.log、work/native-clip-audio-all-gpu.log和work/build-native-clip-audio.log。音频播放桥在测试中静音；没有声称听觉验收。

183个程序首次源刷新183更新、0失败；同一进程重复刷新零更新、零失败。独立资产复核再次确认183程序、130 SoundDef、132音频。参数调查发现235个动作有非零spatialBlend、19个有动作实例限制、2个有非空模板，这些统计可重叠，不是已恢复数量。

47418端口、clipaudio_1/2独立后台客户端完成比赛863e4083ff734e92b1cadfbd5da3d76d：13回合、version40，Shrink1法师13血获胜，Shrink2术士-1血失败，双方公共哈希均为7e9d17f8a46574028d90a0df9706e84bb445b38742b21c98774a993514df921b。日志无异常或fallback。后台card_render_count=0，不能用此比赛证明新动画已视觉验收、人工比赛或1080p性能。

用户UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。完整全卡动画、原Shader、原脚本及全局声音服务：未完成。

后续已恢复默认播放模板及空间参数，并隔离运行全部源声音动作；最新验证与尚未恢复的20动作见[ORIGINAL_AUDIO_TEMPLATE](ORIGINAL_AUDIO_TEMPLATE.md)。上述2D限制是该初始提交的历史边界，不能作为当前已完成全部声音服务的声明。
