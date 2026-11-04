# 原声音实例限制

2026-10-12：恢复AudioPlayClipAction的SoundOptions，以及原SoundManager按生成音源名称计数和释放额度的行为。完整全卡动画、Shader和脚本仍未完成。

## 原行为与实现

静态IL证据为SoundManager.PlayImpl、FinishFirstGeneratedSourceByName、EnableInstanceLimitedSound迭代器及AudioPlayClipAction.Play；不执行官方程序集。程序集哈希和新增类型见manifests/original-script-audit.json。

AudioPlayClipAction始终传入独立SoundOptions，即使其InstanceLimited=false，也不能错误地回退到SoundDef的限制值。NativeEffectInstance将原限制开关、时长、最大数量和满额策略随cue传给客户端共享NativeAudioPlayback。NativeSoundLimits保存计数与独立等待，不让服务器等音频或动画。

计数键为生成对象名Audio Object - 加原SoundDef.m_AudioClip的无扩展文件名，不能按导出WAV文件名或实体ID分别限流。首次计数为1；已有计数时最大值小于1按1处理。满额策略0拒绝播放；策略1调用原生成源逆序匹配删除分支，不增加已有计数；未知策略拒绝。原函数名称虽然包含First，源码实际从列表尾部开始查找，不能改成删除最老声音。当前原19限制动作均为策略0。

原等待时长大于0时用该值，否则取AudioClip.length，不按pitch换算。协程先检查剩余时长，再减Time.deltaTime并yield，下次迭代才释放；计数减1，小于等于0移除。音源停止、被删除或播放完并不会直接释放该计数。客户端每帧只推进一次共享等待，避免同帧多次Play把时间扣多次。服务Dispose才清理剩余额度。

播放桥对拒绝的新音源立即回收，是本项目的资源清理适配；原SoundManager生成池/Destroy调度与全部服务生命周期仍未完整恢复。策略1的IL分支及计数经独立检查，当前卡牌未使用该策略，不宣称其全部声音生命周期逐帧一致。

## 原资源审计

`python -m tools.native_sound_limits`重新读取原FSM并核对19限制动作的参数；全部最大数量1，限制时长为0.1 / 0.2 / 0.3 / 0.5秒。审计可重复一致，结果在manifests/native-sound-limits.json。

剩余两个显式AudioSource模板都指向原L_SFX AudioMixerGroupController，PathID8739943912118694949，连接AudioMixerController -8058976051137792987。提取了6份关联混音器对象的原元数据和Bundle / CAB / PathID。它们是源依赖数据，尚未重建为运行时Unity混音器；两个动作继续明确未恢复，不拿通用音源替代。19限制动作中一个还依赖该模板，所以此次恢复18个此前被拒绝的声音动作。

## 实际验证

1137项Python测试通过；66项实际Unity音频检查通过，含额度边界、原等待顺序、最大数量、PCM时长回退、满额分支、独立名称和共享播放桥的重复抑制。全GPU回归work/native-audio-limits-all-gpu-2.log退出0，包含既有Shader / Timeline / 几何 / 粒子检查。

183程序的声音动作隔离运行得到345启动、345完成、345播放请求：342音源实际启动，3次重复请求被限制规则拒绝，2动作明确未恢复。work/native-audio-source-census.json区分请求与播放数量；没有将被抑制的声音计为播放成功。隔离保留原声音参数和节点层级，其他FSM/渲染状态隔离，不证明183整卡动画完成。所有音频检查静音，未进行可听验证。

Windows构建work/build-native-audio-limits.log为Success、退出0。首次Unity编译因新脚本meta GUID多一位失败，修正为32位后完整回归成功，原失败日志保留；没有忽略编译失败或绕过测试。

完整效果启用数保持2。两个显式模板、完整Mixer / ducking / 分类服务 / VO抑制、其他原Shader和脚本，以及全卡视觉和听觉验收：未完成。

47420端口、limitedaudio_1/2后台客户端完成比赛94332be6a9034fcabcb1043f7083bdf3：14回合、version45，Shrink2术士7血获胜、Shrink1法师-3血失败，双方公共hash均为27e43dc518637dd9d037b05c98ef3ab1ffc494e3425d0373e83ae6a8c762b0c5。日志无异常或fallback；这是后台自动对局，未宣称人工、整卡视觉或1080p性能验收。自有28148/32256/11844清理。

用户UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，生成场景ID已归一；未使用Computer Use或改官方安装。

后续已恢复两个显式模板及原混音器编译拓扑，最新347声音动作统计见[ORIGINAL_MIXER](ORIGINAL_MIXER.md)。此文345/2为实例限制节点的历史边界，完整全局声音服务仍未完成。
