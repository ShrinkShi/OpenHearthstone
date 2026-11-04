# 原 AudioPlaythrough 指定 SoundDef 路径

全卡专属动画、完整原 Shader／脚本未完成。本节点补齐原 `AudioPlaythroughAction.Play` 的非空 `m_OneShotSound` 分支，不扩大整卡效果启用名单。

静态来源为 `local/script-audit/Assembly-CSharp.dll.AudioPlaythroughAction.il.txt`、原 Delay 迭代器、`SoundManager.PlayClip/GenerateAudioSource/PlayImpl` 和 `SoundUtils.CopyAudioSource`。官方 DLL 只读取 IL，不执行。原 Play 忽略遗留 `m_OneShotClip`；取得 AudioSource 后，有指定 SoundDef 时通过 PlayClip(true) 生成独立音源，并在发起播放时发完成事件，不等待 WAV 结束。延迟使用既有逐帧协程适配，保留跨状态续跑和动态音量读取。

指定音源复制原接收物体的模板字段；显式 VolumeScale 替换模板音量，不按自身 SoundDef 路径的 codeVolume 再乘模板音量。None 保留模板音量，音高保留模板因子，声音类别来自指定 SoundDef。空随机列表不消耗抽样次数；随机选择为空才回退主引用。生成声音快照父物体位置，不错误共享源物体的 restartKey、Stop、loop-owner 或实时音量回调。动作始终提供 SoundOptions，即便限流关闭，也覆盖 SoundDef 的限流设置。生成音源的动作限流接入已有原实例计数适配；原物体上的限流分支仍明确拒绝。

`python -m tools.refresh_playthrough_audio` 回读173程序280动作，89动作带显式 SoundDef；动作参数与声音依赖分别记录于 `manifests/native-playthrough-audio-refresh.json` 和 `native-playthrough-audio-assets.json`。声音层按完整源节点索引导出，保留已存在的 fade-audio 文件绑定；不把多个同路径物体错误关联。旧路径调用仅在路径唯一时兼容。173音频根的提取为0 clip错误。

源码重放测试核对全部280动作、节点布局、89个指定 SoundDef 的源类型树与PCM文件。隔离运行测试只运行声音动作，不能证明整卡视觉状态机、实际比赛路由、听觉一致或性能。

未恢复：完整 VO/实体标签策略、原物体上的限流、完整 ducking/SourceExtension/混音路由、既有声音先前随机化后模板音量的所有交互、官方全局随机状态与完整场景监听器。完整原 Shader、全部原脚本服务、全卡正常比赛的逐卡表现验收仍未完成。原法术正式启用仍2项。

## 验证记录

源参数重放1项通过，80.51秒。首轮无GPU检查中声音10项与相机13项通过，随后错误串入渲染检查导致Unity SIGSEGV；改用GPU执行，声音及后续检查链退出0。首轮常规测试发现旧缓存名称绑定及审计未解析数过期，两项失败已用于修正导出工具，不能计为通过。GPU回归 `work/native-playthrough-render-fixed.log` 41项通过，退出0。最终数量见后续验收记录。

本节点未使用Computer Use，未改官方安装，未执行原DLL，未播放人工听感验收音频；静音PCM播放检查不代表听感通过。

最终验证：`work/native-playthrough-sound-final.log` 新增12项检查及相机/区域/历史/角色检查链通过，退出0；`work/native-playthrough-census-final.log` 对173源程序的280声音动作全部完成，271个实际进入静音PCM播放，9个明确拒绝。8个为原物体限流分支，另1个蛇陷阱源AudioSource没有SoundDef（见 census JSON 中7个程序的清单）。不能将271音效计为271整卡效果完成。

最终常规Python `work/pytest-playthrough-final.log`：1183 passed、4 deselected、30 warnings，186.56秒。四项大型定位/区域/启动/RGBA源重读未重跑。Windows构建 `work/build-native-playthrough.log` Result: Success、退出0；原混音器9项常量一致。UI校准SHA256仍为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

修复旧绑定阶段的第二次完整解码进程在后续检查时已不存在、未产生终态报告；未将其计为成功。随后以 `--rebind-existing` 复用第一轮已成功提取的音频缓存，逐一核对根reference/source与节点index/path，保留旧文件名并同步审计；173根全部核对通过，未伪造解码报告。

新构建双客户端 playthrough_1/2 在localhost:47466完成自动比赛：13回合、version41，Shrink1以22生命获胜，Shrink2以-1生命失败。双方match_id为67e03cd96c5e456d932d25522ee7155f、public_hash为99896a03fdd2224a1ae820ca5e3de22db2b4addb25f14fb885b448aec8169f0e，两日志未检出Exception/fallback/Error。后台自动比赛不代替人工、听觉或全卡视觉验收。

后续恢复了原物体音源限流，279声音cue可解析，1项蛇陷阱诊断保留；这里271/9是本节点当时结果，当前实现见 [ORIGINAL_OWNED_SOUND_LIMITS.md](ORIGINAL_OWNED_SOUND_LIMITS.md)。
