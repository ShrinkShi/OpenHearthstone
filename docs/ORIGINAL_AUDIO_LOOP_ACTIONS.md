# 原循环音源启动与结束

2026-10-14。全卡专属动画、完整原 Shader/脚本未完成。

## 原脚本与实现

静态核对 AudioLoopingStartAction、AudioLoopingEndAction、SoundManager.PlayClip、LoadClipForPlayback、FinishSource/FinishGeneratedSource。启动先获取自身 SoundDef/AudioSource，开启 loop 和 enabled；内部 startVolume=0，但 OnEnter 不调用 SetVolume(0)，先按当前代码音量播放。随后使用游戏时间淡入，保存结束时间、每帧读取时间除数和目标值，完成写精确目标并 Finish。零时长也先播放，首个 OnUpdate 才完成。

结束取得代码音量、使用游戏时间淡出至0，完成读取当前 m_stop 决定是否停声。FadeOutTime<=Epsilon 直接 Finish，不静音或停声；存在对象但没有 AudioSource 时仍计时，GetVolume(null)=1，SetVolume/Stop(null)不生效。空对象直接 Finish。状态退出不继续更新这两个状态动作。

默认 dontDestroySource=false 调用 PlayClip(args,false)，直接使用 SoundDef 对象自身音源并登记为 generated source；Stop 后 FinishGeneratedSource 销毁该对象。适配器为支持确定性回退保留池化节点，但隐藏其子树、令 ResolveObject 的原引用失效并释放物理声部，回退恢复。dontDestroySource=true 分支不登记该销毁。真实 Unity OnDestroy、所有原组件销毁回调和完整 SoundManager 注销尚未恢复；不将池化行为称为完整引擎脚本一致性。

随机起播按原选项传给真实 AudioSource.time，样本独立于选音、随机音量和音高。当前12个原启动动作的 randomizeStartPoint 与 dontDestroySource 均为false，额外SDK检查覆盖true分支。

真实原Prefab测试首先失败：已有 Resolve 拒绝 SoundDef 固定主音频。按 LoadClipForPlayback 修复为非空随机列表优先，其返回null才使用 m_AudioClip；空列表不消耗选音随机样本。缺失主音频缓存仍拒绝，直接 AudioSource.clip 指针分支和完整异步加载未恢复。没有用另一随机音频替换原主音频。

## 来源与实测

`python -m tools.refresh_audio_loop` 回读12程序的12启动/24结束动作，共132参数，0未解析、0失败。

`python -m tools.recover_fade_audio --loop` 复用完整程序层级恢复28个音源/声音定义节点、24条音频引用，0音频导出错误；包含共享引用，不能称为24种独立声音。来源保存在 native-audio-loop-refresh.json 和 native-audio-loop-bindings.json。正式整卡启用数量未因此增加。

`tests/test_audio_loop_source.py` 精确重放原层级/typed参数、原音源关联及默认选项，通过1项/2.95秒。Python常规1181 passed、4 deselected、28 warnings，77.57秒；四项专属定位/区域/启动/RGBA大型来源重读未重跑。

`work/native-audio-loop-final.log`：33新增Unity检查及已有执行检查链通过，退出0。覆盖实际物理音源初始/淡入音量、FINISHED等待、淡出停声、源引用失效、物理释放、确定性回退、保留源、stop=false、源移除后的Playthrough完成事件、随机起播SDK时间、固定原音频及选音优先级。全部12个原启动程序逐个加载原Prefab并执行原动作、发出原音频cue，无未绑定淡入动作。测试仍不是全部原状态/整卡动画一致性或听感验收。

官方安装只读，没有Computer Use，原程序集没有执行。完整原SoundManager、原销毁回调、全卡动画和完整Shader/脚本仍未完成。

`work/native-audio-loop-gpu.log` 41项GPU检查通过并退出0，`work/build-native-audio-loop.log` Windows构建退出0。原混音bundle9项常量核对通过。UI校准 SHA256 仍为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

最终后台双客户端 audio_loop_1/2 在 localhost:47462 完成一局。match_id=6470ae5428ba4c5bb264a868ec6e369d；双方 finished、回合13、版本47、英雄生命0/10，最终public_hash均为931d87752ee9e54477416d95837c13edf36b000909b91aa92cd408105c3e3740。双方及服务器日志没有Exception/fallback/Error。后台自动比赛不证明人工画面、完整听感或60 FPS验收；正式原整卡启用仍两项，没有新增整卡一致性通过场景。
