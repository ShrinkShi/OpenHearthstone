# 原召唤落地音频来源

后续原音频动作时钟、权重选择器和PCM播放桥接已接入，见 [ORIGINAL_AUDIO_CLOCK](ORIGINAL_AUDIO_CLOCK.md)。以下记录来源恢复节点当时状态；完整原SoundManager和正式Actor表比赛路由仍未完成。

2026-10-11。全卡原专属动画、完整原Shader/脚本仍**未完成**；本节点找回真实声音和源参数，尚未接入原FSM音频播放。

源 `Card_Play_Ally_SummonIn.prefab:786da6b44c25c46d39459e873d237292` 的AudioSource PathID `8380335505722186880`，SoundDef PathID `3867332086282067072`；二者在同一个GameObject。AudioSource直接Clip指针是null。SoundDef的m_AudioClip字符串为空，m_RandomClips中唯一引用：`FX_MinionSummon_Drop.wav:2630d970c5e7b3f4ebd2bd88723877ed`，权重1；随机音高0.9至1.1、随机音量1至1，类别1。原AudioSource音量0.75、pitch1。最终播放音量仍受动作参数/SoundManager类别和ducking影响，不能直接把0.75当最终值。

确切AudioClip位于essential_base_global-audio-0.unity3d，CAB `CAB-c491983cea83897756f11fa097177509`，PathID `2307216968499716871`。按GUID解析，然后以CAB/PathID唯一资产索引导出，避免同名挑错。解码文件 `local/runtime/bc42c1af01f7c7c5aef6f81d.wav`：48000Hz、单声道、1.198666667秒，SHA256 `6be1cf862da3ee8a9e0b76b4befc6d75a088e3ced6f796d0c3003708d88bb649`。已检查WAV结构，没有播放声音打扰用户。

命令 `python -X utf8 -m tools.native_audio`，也可指定 `--reference`。保存原AudioSource/SoundDef全部序列化字段与引用、精确Clip缓存来源及错误。默认召唤节点1组组件/1Clip/0导出错误。记录 `manifests/original-summon-audio.json` 和独立 `.native-audio.json`，不改两个原效果启用缓存。

原静态IL增加SoundManager、SoundDef、SoundOptions、SoundPlayClipArgs、SoundUtils，共154类型。GetDefFromSource从AudioSource同物体取得SoundDef；原AudioPlaythroughAction的OneShotSound为null时调用SoundManager.Play(source,null,null,options)，而非没有声音。GetRandomClipFromDef对原权重求和，用Random.Range(0,sum)，累计比较为sample<=累计权重，最后项作后备，不将零权重/边界逻辑改成别的算法。尚未实现该选择器。

原AudioPlaythroughAction按m_Delay推迟Play，发起声音后发送FinishedEvent并Finish；不是等待声音长度。InstanceLimit、SummonVO/KeywordVO过滤、SoundManager音量/音高/ducking/音频池均未恢复。**未完成**：原音频动作调度与实际回调播放、完整SoundManager、所有效果音频、听感和混音验收。导出不代表这些声音已经在比赛中自动播放。

701项Python测试通过（38.04秒），新增检查覆盖空AudioSource Clip时保留同物体SoundDef、原随机范围、精确引用、缺失Clip错误和错类型拒绝。未修改Unity客户端/服务器，本节点不声称新增Windows或GPU验收。未使用Computer Use，官方安装只读，原程序集只作静态读取。
