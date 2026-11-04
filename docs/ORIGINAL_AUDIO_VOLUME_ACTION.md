# 原正在播放音源的音量动作

2026-10-14。全卡原专属动画、完整原Shader/脚本未完成。

## 原行为

静态读取Assembly-CSharp.dll.AudioSetVolumeAction.il.txt。OnEnter立即UpdateVolume，非EveryFrame则Finish；OnUpdate继续更新。先解析OwnerDefault对象及其自身AudioSource，空对象或没有AudioSource直接返回；m_Volume.IsNone时不写入，否则调用SoundManager.SetVolume。没有退出恢复。

此前此动作没有执行，部分原曲线仅在变量中变化，已播放声音保持旧音量。现NativeEffectInstance按原顺序，在AnimateFloat等变量更新之后更新自有音源的代码音量；EveryFrame保持动作未完成，状态退出停止更新，None保留旧值。无法绑定的外部源、缺失None元数据等仍明确计入UnboundAudioVolumeActions。

Playthrough物理voice持有原选择时的sourceVolume×randomVolume基数，并每帧读取源代码音量更新实际AudioSource.volume，不重新选择随机声音、不重启声音或分配新声部。完整分类混音、ducking、外部Actor服务等仍未恢复；只对已有自有源路径生效。

## 来源恢复

`python -m tools.refresh_audio_volume` 回读46普通特效程序、105动作；`--extra-only`另外回读11启动/Actor程序、21动作，共57程序126动作。使用真实源层级重放节点指针和FsmFloat None/变量标记；原启动效果和Actor语音程序没有漏掉。两次提取均0失败。

refresh_scale_cache增加可选patterns，默认仍只处理普通效果，既有调用范围不变。原来源CAB/PathID/Bundle、参数与节点关联记录于native-audio-volume-refresh及extra-refresh。普通引用的unknown_actions/未解析计数已同步；未因此启用更多正式整卡效果。

## 实测

work/native-audio-volume.log：6新增实际SDK检查及已有完整执行链通过，退出0。真实源actor-spell程序在内存中构造每帧曲线测试：.25秒正在播放volume=.5625、.5秒volume=.375，只有一次声音启动，None保持.375，空目标不写，未知None标记诊断。没有修改官方资源或用户校准。

work/native-audio-volume-gpu.log：41效果GPU检查通过，退出0。Python常规1177 passed/4 deselected/25 warnings/183.05秒，含57程序原字段/层级重放。四项此前未改的专属定位/区域/启动/RGBA来源重读未在本命令重跑。将总动作断言收紧为精确126后来源测试再过1项/22.05秒。

work/build-native-audio-volume.log Windows构建退出0。没有ComputerUse，官方安装只读。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

新构建后台双客户端audio_volume_1/2通过localhost:47458完成一局，match_id=21f0e7ef0de2488f90cf9df428260960。双方finished、回合13、版本50、英雄生命-2/1，最终public_hash均为9c6ca14c75558ecc84df82d5f63fe11bed04f565190c58cce773ad088b18261b；双方及服务器日志没有Exception/fallback/Error。此为后台自动比赛，不代表人工画面、听感或60 FPS验收。native_mixer --verify-bundle的9项常量核对通过。

## 未完成

完整外部Actor/原SoundManager、原听感、完整原Shader/脚本和正式全卡音画仍未完成。正式启用仍两项，未新增整卡一致性通过场景。
