# 原音源淡入淡出动作

2026-10-14。全卡专属动画、完整原 Shader/脚本未完成。

## 原脚本行为

静态核对 `Assembly-CSharp.dll.AudioVolumeFadeAction.il.txt` 与 `SoundManager.GetVolume/SetVolume`。显式 m_AudioSource 非空时优先使用该组件，否则获取 OwnerDefault 对象自身 AudioSource。空对象、无音源或 FadeTime<=Mathf.Epsilon 时 Finish，不写入目标值也不停止声音。正常进入保存原代码音量、起始时间及结束时间，并原值写回；每帧 Lerp，完成写入精确目标值并按当前 StopWhenDone 停声。退出不恢复音量，不再继续更新此状态内的淡出动作。

目标音量与时间除数每帧读取，结束时间在进入时固定。原实时分支将 RealtimeSinceStartup-startTime 写入 currentTime，但仍与绝对 endTime 比较、再次减 startTime；按实际 IL 保留此行为，没有改成常见的相对时间计时。现有 168 个动作均使用游戏时间分支。

## 实现

NativeAudioFade 保存原计时和当前增益；NativeEffectInstance 按原动作顺序更新自有源代码音量，淡出完成参与 FSM 的 FINISHED 等待。NativeAudioPlayback 继续使用同一个实际 AudioSource，更新 volumeBase×codeVolume，不重新选择原声音、不重启声部。StopWhenDone 使用已有指定音源停止路径。

FsmObject 的 AudioSource 指针解析为真实所属 GameObject 节点，并保留组件 CAB/PathID；绑定时核对恢复音源的实际组件身份。原 Raw 音源来源记录由 NativeAudioSourceData.source 读取。无法恢复的外部组件保持 UnboundAudioFadeActions，不用相邻或默认源替代。

实际运行时调查发现此前这 61 个程序均没有 audio_file，原 recover 仅访问主 Prefab 子树，还漏掉原程序引用的独立 Prefab。`tools.recover_fade_audio` 现在复用原提取器并按完整程序层级访问所有恢复对象：61 程序、175 个音源/声音定义节点、114 条音频引用，0 导出错误。引用数包含程序之间共享音频，不等于 114 种独立声音。

多个独立 Prefab 可以拥有相同路径。新音源数据记录 node/node_known，运行时按真实程序索引查找；旧的单树缓存仍按路径读取。不能在新索引没有匹配项时回退到另一同名对象。12 个显式组件指针均核对到实际接入音源数据的 CAB/PathID。

## 验证状态

61 程序、168 动作、870 参数已回读，0 提取失败、0 未解析参数。其中 12 个显式组件指针按原 CAB/PathID 和所属 GameObject 恢复；18 个显式参数为空，其余 138 个旧版动作没有该字段，按原默认空引用选择 OwnerDefault。第二次节点回读发现两份旧缓存仍未更新，重放失败后再次回读纠正，未放宽来源比较。

`NativeAudioFadeChecks.Run` 的最终 24 项 Unity 检查通过并退出0，覆盖实际 SDK 渐弱、状态完成等待、完成停声、提前退出、不重复分配、显式组件优先与错误组件拒绝、零时长、动态目标/除数/完成标志、原实时分支边界。新增重名独立 Prefab 的索引查找、旧缓存兼容及真实原特效对象上的音源淡出执行。既有停止、音量、音源与脚本检查链同时通过。数据解析完整不代表所有原场景发声或整卡动画已经验证。日志 `work/native-audio-fade-indexed.log`。

`tests/test_audio_fade_source.py` 的两项来源测试通过，19.77 秒；保持原完整缓存重放相等，并核对全部 61 程序的音源挂接和 12 个显式组件身份。

完整程序层级接入后的 41 项 GPU 检查通过并退出0，日志 `work/native-audio-fade-indexed-gpu.log`。最终 Python 常规测试 1180 passed、4 deselected、27 warnings，183.87 秒。四项专属定位/区域/启动/RGBA 大型来源重读未重跑，未将本命令称为全部测试。

`work/build-native-audio-fade-indexed.log` 最终 Windows 构建退出0。`tools.native_mixer --verify-bundle` 的9项原常量核对通过。UI校准 SHA256 仍为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

最终构建后台双客户端 audio_fade_final_1/2 在 localhost:47461 完成一局。match_id=13b3fa20ac1043ceba664154f421a244；双方 finished、回合13、版本43、英雄生命15/0，最终 public_hash 均为681bdc4ab1608764238016ec9ed5c6ec25cf6832cb612182147fc56e383c7948。双方及服务器日志没有 Exception/fallback/Error。后台比赛不等于人工画面、完整听感或60 FPS验收。

原程序集未执行，官方安装只读，没有 Computer Use。完整 SoundManager、全卡动画、Shader 和正式整卡一致性仍未完成。

正式原整卡效果仍只启用两项，本节点未增加全卡一致性通过数量。
