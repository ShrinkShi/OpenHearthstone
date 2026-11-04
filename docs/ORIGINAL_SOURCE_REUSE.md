# 原既有音源复用与代码音量

2026-10-13。全卡原专属动画、完整原Shader/脚本未完成。

## 原证据

静态核对SoundManager.PlayImpl：非空source使用其自身AudioSource，UpdateSource之后最终直接source.Play；原发声节点不会因音频结束就销毁。RegisterExtension按AudioSource身份取得既有SourceExtension。SetVolume保存m_codeVolume；AudioPlaythrough在m_VolumeScale.IsNone时不调用SetVolume，因此保留前一次代码音量，而不是重置为1。

## 修正

Playthrough按真实自有节点InstanceID提供稳定restartKey和源名称，明确keepSource。NativeAudioPlayback复用同一SDK AudioSource，空闲时仍保留在活着的发声节点上；源销毁、StopAll或实例退出释放。一般生成声部保持先前音频结束即释放的行为，没有把所有声音改成常驻。

NativeEffectInstance保存每个自有节点的代码音量，显式值更新，None沿用已有值或初次默认1；重建时清理。既有原音源音量×代码音量×随机音量公式保持不变，延迟时读取当前值仍保留。

有限缓存/声部预算仍是本项目边界，完整原同名实例配额、混音、ducking、VO、覆盖SoundDef及原场景监听器未恢复；不能把普通非限制音源复用宣称完整SoundManager。

## 实测

work/native-source-reuse.log：6新增实际SDK/源FSM检查及已有执行链通过，退出0。真实92c50d3e8ca46833c7a5dc25.actor-spell.json在内存中替换测试FSM：同一源先代码音量.4，再None，最终两次cue音量均.3；稳定源身份、重复触发只有一个SDK对象、空闲复用、销毁释放均验证。

work/native-source-reuse-gpu.log：41效果GPU检查通过，退出0。Python常规1176 passed/4 deselected/24 warnings/172.08秒；四项此前未改的专属定位/区域/启动/RGBA来源重读未在本命令重跑。

没有ComputerUse，官方安装只读，没有原听感或人工整卡验收。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。正式审核启用仍两项，完整全卡未完成。

work/build-native-source-reuse.log Windows构建退出0，原Mixer九编译常量一致。最终构建后台source_reuse_1/2在47457完成48ba5d33041d48d5aaaf43d8d17ba4a5，11回合/version34。Shrink1法师17血胜、Shrink2法师0血负，两端public_hash均70f5119094c3003520370d473738a85e4d389f55404815b4238cc765002f7601，日志无Exception/fallback/Error。终局与命令行确认后仅清理自有30904/29804/28508；后台不绘制GUI，不代表听感或人工原动画验收。
