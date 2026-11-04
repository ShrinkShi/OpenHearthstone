# V017：H11双分辨率本地Player切片独立验收

日期：2026-10-21。对象：H11 `HERO_11bp` 的1280×720与1920×1080双端本地Player证据。结论：**PASS（仅限本地双分辨率Player切片；不代表TASK-0006整体完成或达到原版音画保真）。**

## 核验结果

- `work/h11-player-1280-f017-r3-run/analysis.json`与`work/h11-player-1920-f017-r1/analysis.json`均记录PASS。独立对两目录的driver和observer报告执行只读`analyze_player`断言，并检查运行器外层状态；四份报告均通过。`tests/test_death_knight_player_replay_contract.py`定向合同测试为4 passed。为避免覆盖既有证据，没有运行会重写`analysis.json`的完整分析器命令。
- 两轮使用相同Player DLL，SHA-256=`40E052EB550F4D385887BE303F74A122BBFE21AC492D45676B5279E4763FEDAE`。每轮双方cast public hash均为`701849CDC24184D65E074B7EBE818D361BDC6F1F27BE7F38084C0AB5916595705`，回合末hash均为`D10749FF2885884EF1B2103F4D07D18D55B0222CA9C7771FEBEBCD9E5460CD7C`。
- 唯一`PlayHeroPower`与唯一`Summon`正确配对：技能实体5、召唤来源5、目标实体71。真实`HERO_11bpt`为1/1 Charge；己方回合结束后，Death事件的source/target均为71，随从离场，双方权威、peer与display hash一致。
- 两端均观察到H11 source启动与释放；CustomSpawn初始化、side-event分派、Timeline启动及Timeline声音启动各1次。通用召唤fallback为false，`unboundActions`、`unhandledActions`、`unsupportedTimelineTracks`均为0，动作记录中`Wait`为4次。观测到的source持续时间：1280 driver/observer为4.4538/4.4727秒；1920 driver/observer为4.3843/4.4181秒。
- driver通过技能按钮的实际GUI矩形计算坐标，并由测试组件注入`EvidencePointer`/`EvidencePressed`后调用`HandleBattleInput(MouseDown/MouseUp)`；提交计数与pending状态随即变化。该证据覆盖游戏GUI输入处理路径，属于程序注入，不是操作系统原始鼠标事件。source释放后，driver与observer均采到`Busy=false`、`InputLocked=false`、`pending=false`和display hash匹配；source活动期间未观察到提前解锁。driver采到合法`end_turn`后继续完成回合末死亡快照。
- 两条不同的原生声音文件分别记录为Button与Timeline Token cue。每份报告记录一个有效Listener、48 kHz stereo、422–423次DSP回调、overflow为0、约432k–433k sample frames、RMS约0.114。1280 observer的float PCM有3个绝对值超过1的样本，peak为1.0042；另外三份捕获的该计数为0。float WAV保留超出1.0的值，此计数不表示写文件时被硬截平。该证据确认本地Listener混音有信号及两条声音cue被触发，不确认原版听感或每条cue的单独可听贡献。
- 两种分辨率的ready、cast、source-active、source-released及after-end-turn截图均存在，analyzer确认PNG尺寸符合运行分辨率。截图检查只验证文件与尺寸；cast截图在收到权威事件后立即请求捕获，没有等待该时刻的display hash收敛，因此不把它作为逐帧视觉同步或画面保真证明。

## 隔离与运行器行为

- 两轮使用测试runtime全量副本。记录的生产index SHA-256前后不变，均为`53428008669363FA0D70957F415D4AF5EA6F20E7A3904EAF70180F61625A0DF5`；生产H11运行前后均disabled。隔离index SHA-256=`64A4F9F1019B0907F26535D3DDD6EB1C46F2A75A2A824D6F55520DD53D55DF96`，两轮临时runtime路径已清理，且复核时路径均不存在。
- 两轮driver和observer均exit 0。运行器在Player结束后主动terminate本地authority server；因此`run.json`中的server exit为1，属于运行器关闭记录，不应解释为Player失败。匹配日志在报告采集的版本5回合末快照之后，还记录了observer断连触发的Concede版本6；本验收只依据此前已匹配的回合末Death快照，不声称整局正常结束。

## 分析器边界与未覆盖项

- 未发现等待超时或缺失字段默认值将本次运行误判为PASS的路径：等待超时会抛错，缺失Player报告产生FAIL，缺失的source时长、解锁采样、PCM信号或状态hash会被后续断言拒绝。
- analyzer没有把`clippedSamples==0`设为PASS条件，也不直接验证`Wait`动作次数或`unhandledActions`、`unsupportedTimelineTracks`各字段；生产Player合同以聚合`unboundActions==0`拒绝异常，本轮报告对应字段实际为0且`Wait`记录为4。source持续时间检查只接受4–7秒，不能证明严格等于来源标称的5秒。
- 没有原版同机位画面对照或原版音频对照，也没有做原声听感、卡面文字/按钮完整性或逐帧表现验收。两轮结果只能作为H11本地有界运行链的双分辨率PASS；TASK-0006其他英雄技能与整体完成状态不在本报告范围，任务整体仍为`IN_PROGRESS`。

## 证据

- 1280×720：`work/h11-player-1280-f017-r3-run/analysis.json`、`run.json`、`runtime-overlay.json`、`driver/report.json`、`observer/report.json`、`driver/progress.json`、`observer/progress.json`、`matches/08f3c12d404848c5aa152a0c5316c269.jsonl`及双方阶段截图。
- 1920×1080：`work/h11-player-1920-f017-r1/analysis.json`、`run.json`、`runtime-overlay.json`、`driver/report.json`、`observer/report.json`、`driver/progress.json`、`observer/progress.json`、`matches/1ab99f30cb9a46d3b57dbeac833b0fbb.jsonl`及双方阶段截图。
- 合同与采样逻辑：`tools/death_knight_player_evidence.py`、`tests/test_death_knight_player_replay_contract.py`、`UnityClient/Assets/Scripts/H11PlayerReplay.cs`、`UnityClient/Assets/Scripts/FireblastPcmTap.cs`及H11相关工作树diff。
