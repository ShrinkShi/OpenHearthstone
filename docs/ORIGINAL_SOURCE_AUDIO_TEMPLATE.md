# 原发声节点音源模板

2026-10-13。全卡原专属动画、完整原Shader/脚本未完成。

## 真实缺失与原证据

AudioPlaythrough的普通SoundDef分支使用已有AudioSource。原SoundManager.PlayImpl非空source分支直接注册和播放该音源；SetVolume只把值写入SourceExtension.m_codeVolume并UpdateVolume，不能把原音源音量直接替换掉。本节点保持既有原音量×代码音量×随机音量公式。

原AudioSource全部字段此前已导出，但NativeAudio.Resolve只传递音量/音高/loop/mute，未传模板。NativeAudioPlayback又无条件写spatialBlend=cue.spatialBlend，默认为0，丢失源空间曲线、距离、优先级、声像及Doppler等设置。

现在普通Resolve携带原template，选择完整Apply路径，不套用PlayClip的Copy模板删减规则。inheritSpatialCurve避免再覆盖原自定义空间曲线；显式ClipAudio式空间覆盖保留原行为。最终代码音量和音高产品仍应用，测试静音也仍可用。

## 位置与生命周期

已有原发声对象应随对象移动，不能当作PlayClip的单次位置快照。Playthrough cue保留对应本项目节点和离屏stagingRoot；Playback更新时追踪该节点，并剥离本项目离屏平移。原节点销毁后停止对应voice并清理记录，StopAll也清理跟踪数据。

本项目仍复制独立物理voice，没有完整恢复原既有音源重复播放/声部策略。剥离staging平移不代表原世界/监听器/棋盘声场已完成；完整SoundManager、分类混音、ducking、VO条件、直接clip/覆盖SoundDef分支仍未完成。

## 验证

work/native-source-audio.log：10新增实际SDK检查及已有完整执行链通过，退出0。原来源为92c50d3e8ca46833c7a5dc25.native-audio.json；在内存中修改模板字段区分SDK默认值，不改官方资源或缓存。检查真实AudioSource的优先级/距离、pan/Doppler/bypass、衰减/空间曲线、最终volume/pitch、位置跟随、源销毁停止及显式空间覆盖。

work/native-source-audio-gpu.log：41效果GPU检查通过，退出0。Python常规1176 passed/4 deselected/24 warnings/132.32秒，四项此前未改的专属定位/区域/启动/RGBA来源重读未在本命令重跑。

没有ComputerUse，没有原版听感对照或桌面人工验收。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。正式原生效果启用仍两项，完整全卡未完成。

work/build-native-source-audio.log Windows构建退出0，原Mixer九编译常量一致。最终构建后台source_audio_1/2在47456完成c8421bc0f2b342359a0961392fc363d3，12回合/version33。Shrink1法师27血胜、Shrink2法师-3血负，两端public_hash均dbd6c90d40ecd3786de01a690ba49af4b03e2296a1d462e9c29701b0cf18797b，日志无Exception/fallback/Error。终局与命令行确认后只清理自有15324/30372/30536；后台不绘制GUI，不证明原听感或人工整卡验收。
