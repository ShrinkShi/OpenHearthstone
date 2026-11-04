# 原特效指定音源停止

2026-10-14。全卡专属动画、完整原 Shader/脚本未完成。

静态读取 `local/script-audit/Assembly-CSharp.dll.AudioStopAction.il.txt`：OnEnter 解析 OwnerDefault，仅获取该对象自身 AudioSource；对象或音源为空时跳过，存在时调用 SoundManager.Stop，随后 Finish。不递归停止子对象，不销毁音源，不恢复音量，也不取消 PlayMakerFSM 上已经启动的延迟播放协程。

此前执行器遗漏此动作，取消、死亡和结束状态中的指定停声没有发生。现在 NativeEffectInstance 对真实自有音源记录停止版本；每次播放绑定当前版本，NativeAudioPlayback 停止过期播放并保留原音源，后续播放复用同一 SDK 对象。尚未到时的协程在实际 Play 时绑定新版本，因此原 Stop 不会误取消其后播放。外部音源或未解析绑定计入 UnboundAudioStopActions；完整 SoundManager 的分类、限额及外部 Actor 路径仍未恢复。

## 来源与验证

`python -m tools.refresh_audio_stop` 从原 Bundle 重读 10 个普通/Actor 程序的 16 个动作、真实 OwnerDefault 节点，0 失败。来源 CAB、PathID、Bundle 和节点保存在 `manifests/native-audio-stop-refresh.json`；普通卡牌审计同步更新，不因此启用新的正式整卡效果。

`tests/test_audio_stop_source.py` 重放原层级与 typed 参数并比较完整缓存，核对精确 16 动作。Unity `NativeAudioStopChecks.Run` 的 8 项检查通过：停止现有播放、保留源、再次播放、无重复分配、延迟协程继续、空目标跳过、未知目标诊断；同时已有音量、音源、脚本执行检查链通过。日志 `work/native-audio-stop.log`。

常规 Python 测试 1178 passed、4 deselected、26 warnings，181.22 秒。未重跑四项此前未改动的专属定位/区域/启动/RGBA 大型来源重读。后台音源检查使用静音，不代表原听感验收。

`work/native-audio-stop-gpu.log` 41 项 GPU 检查通过，原脚本检查、GPU 和 Windows 构建均以 return code 0 退出。`tools.native_mixer --verify-bundle` 核对 9 项原常量通过。

新构建后台双客户端 audio_stop_1/2 在 localhost:47459 完成一局，match_id=6535d606d6c54a5fbe5e3fbc39d6b8bb。双方 finished、回合 13、版本 43、英雄生命 -3/5，public_hash 均为 8667858684d81df8228e18e2bfc4bc1cec4684a8ef6abaea31f46481a5b1082d；双方和服务器日志没有 Exception/fallback/Error。此为自动后台比赛，不代表人工画面、听感或 60 FPS 验收。

UI 校准 SHA256 为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，与修改前一致。

没有使用 Computer Use，没有修改官方安装或用户 UI 校准。正式原整卡效果仍只启用两项；本节点不宣称全卡动画、Shader 或原音画一致性完成。
