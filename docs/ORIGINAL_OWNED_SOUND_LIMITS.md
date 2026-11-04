# 原物体音源限流与拒绝重播

本节点继续恢复现有原声音脚本，不新增整卡效果启用，不代表完整原 SoundManager 或完整全卡 Shader／脚本完成。

## 原脚本依据

`Assembly-CSharp.dll.SoundManager.il.txt` 的 `PlayImpl` 在 RegisterExtension/UpdateSource 后检查按物体名称计数的 activeLimitedSounds，拒绝时返回null，尚未调用 AudioSource.Play。因此已存在音源及原播放位置应保留，不能先Stop或删除物体。`FinishFirstGeneratedSourceByName` 只倒序扫描 m_generatedSources，不包含原场景物体音源；达到上限的替换分支不递增计数，并独立启动释放协程。物体相同名称不等于相同实例。

NativeAudioPlayback现在先完成配置与原名称配额判断，再在接受时重播；拒绝时保留具restartKey的原物体音源，只有被拒绝的生成音源被回收。替换扫描排除物体音源；生成音源和物体音源共享名称计数，但保留各自身份。重复播放相同PCM不再重复赋值clip，避免无意重置旧播放位置。

原 AudioPlaythrough 的两个 Play 分支都会提供动作 SoundOptions，因此动作限流关闭时也覆盖 SoundDef 的限流。原物体限流动作不再一概拒绝；默认 SoundDef 解析仍携带其自己的限流设置，供没有动作覆盖的调用使用。计数释放沿用原逐帧协程时钟，不以Stop或PCM播放完毕替代释放。

## 来源中缺声音的蛇陷阱

9add73a48bda0f94d51e2988.native-effect.json 的 Beast_Untargeted_SnakeTrap_FX 源物体有AudioSource、没有SoundDef。现代序列化中有m_Resource指针，不能据此杜撰一个 SoundDef。原 LoadClipForPlayback 在没有SoundDef时仍走失败返回；即使有SoundDef，直接clip也只在随机引用与主引用都为空的条件内使用。当前保留这一项诊断，不强制播放猜测音效。全局原占位声音服务仍未恢复。

## 验证范围

新增原物体限流检查覆盖：首次配额、拒绝保留实例及播放位置、替换保留物体、原计数、关闭动作限流覆盖、生成/物体同名时仅回收生成音源、新物体首次被拒绝时保持独立身份、原FSM动作选项传递。静音实际AudioSource检查不代表听感验收。

173原程序280动作隔离运行全部完成，279个cue、278个实际声音启动（其中一个被原限流抑制），只剩蛇陷阱1项缺源SoundDef诊断。此数字是声音动作数量，不是整卡完成数量；完整视觉状态机、场景混音、ducking/全局限流与全卡原脚本仍未完成。

`work/native-owned-sound-final.log`：11项新增检查通过，并执行既有指定音源、相机、区域、历史与角色检查链，退出0。`work/native-owned-sound-census.log`隔离源声音运行退出0。`work/native-owned-sound-gpu.log`：41项GPU回归通过，退出0。`work/build-native-owned-sound.log`：Result: Success，退出0。原混音bundle9项常量一致。

新构建 owned_sound_1/2 在localhost:47468后台完成一局自动比赛：20回合、version73，Shrink1以16生命胜，Shrink2以0生命败。双方match_id=37b7d998781d4a45a54e54640743bf3c，public_hash=8b4507ab3113073bd77bcc38813a35f681bdfa91ab4b1cb83db5d2b7d83bf29b；客户端日志未检出Exception/fallback/Error。此项不代替人工、GUI性能或全卡视觉验收。

未使用Computer Use、未执行官方DLL、官方安装只读。UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。整卡原效果审核仍2项，目标未完成。

最终常规测试 `work/pytest-owned-sound.log`：1183 passed、4 deselected、30 warnings，431.78秒；四项大型定位/区域/启动/RGBA源重读未重跑。自动比赛结束后按PID及命令行清理本轮自建后台客户端和服务器，未操作用户游戏。
