# V022 H11 Token FX 与播放尾部独立复核

日期：2026-10-26。独立只读复核者：`v022_fx_timing`。范围为 H11 Token 音频在真实 Player Listener 混音中的可识别性、回放映射与释放时序、合法 EndTurn，以及 r5/r6 对应阶段图。未启动 Unity、Player 或 Importer，未改业务源码。

## 结论范围

- r6 的 1920×1080 双 Player 切片通过本次独立复核：两端混音均包含可识别的 Token 源；source age 映射无报告违例，播放结束后有界释放；driver 在输入解锁后于同一 Update 样本提交 EndTurn，权威 Death 与双方终态一致。
- r5 的真实阶段截图保留一个视觉回归：同一位置同时显示两个重叠的 Ghoul 表现，前层为 1/0。状态报告只有一个 Ghoul 实体且已收到 Death；这是重复表现层，不是两次服务器召唤。r6 对应阶段仅见一个表现层。r5 不因 r6 修复而改写为通过。
- 本结论只覆盖下文列出的两个 Player 与已检查截图阶段，不代表全任务完成、官方客户端画面/声音保真或物理扬声器听感。

## 固定运行与证据

根控记录的源码验收快照为 `5b60276d35571f2e67472df98dd967514bdff31b`；实际 r6 Player DLL SHA-256 为 `F3F6A4F616F8D18C335858031A427DF24D8F3943C380E8045D02B26AB689FE04`。`work/f022-death-knight-r6/run.json` 与 `analysis.json` 均记录双 Player PASS；driver、observer 均 exit 0。运行器记录的 authority server exit 1，不能据此改写 Player 退出状态。

- `run.json` SHA-256：`1e282ed9893a6e204ac4845b663722d5f2ccbd0d4c09904ec073aa9baa282d18`
- `analysis.json` SHA-256：`e1bef84a661a6c8acf52971a6e4d742fadfe1b8b735c576ed46a70011e958d0a`
- driver / observer 报告 SHA-256：`8e19a4afeaff252722d76bfc0f58e2004736cebaabbcad0a1eef2096ba9a8574` / `89ad6a13c3a8233365832b2153a75fb58d69dd35e112830896e2fa908234267a`
- driver / observer `scene-mix.wav` SHA-256：`76c7fbdb57dd1ad1f9bf86322434c8aff7209238aa47c8699bb4d578fe64dca2` / `bbe4977641ac0c4749cb6709c513dc09214c43b15ac3e319364e82213e9953a8`
- 两端 Token 源均为 48 kHz、双声道 PCM16、192634 frames（4.013208 s），SHA-256 `1779b7584a04a87a2d208c5f8007257ca8d1fbd0aef2068d8331c2736f0fad01`。

本次原始 48 kHz 复算结果保存在 `work/v022-final-h11-token-audit-r6.json`；复算器为 `work/v022-final-h11-token-audit-r4.py`，SHA-256 `41bb11a66419324becfbeb0931caa4995646ec48d67df3c70125ae28b8d22522`。重放命令：

```powershell
./.venv/Scripts/python.exe -X utf8 work/v022-final-h11-token-audit-r4.py --folder work/f022-death-knight-r6 --output work/v022-final-h11-token-audit-r6.json
```

## 实现与验收器边界

- `NativeDeathKnightHeroPowerPlayback.Supports` 校验 H11 时长为 5 s、唯一 Token timeline sound track、起点 0、播放倍率 1、无 clip-in/pre/post，声源非 Loop，并解析到标称 192634/48000 s 的 WAV（容差小于一个 48 kHz sample）。因此该路径播放的是完整非循环 WAV；首次载入 PCM 会延迟 `AudioSource.Play`，不按当前视觉 age 跳过文件开头。r6 Listener onset 与整段模板拟合符合这一点。
- Token 播放期间用真实 `AudioSource.isPlaying` 保持时钟；Prepare 与 Draw 复用同帧同 source-age 的映射样本。identity age 为 timeline 起点 `1/6 s` + WAV 时长 + `GetDSPBufferSize()` 缓冲总时长。active 音频超过 identity age 时，映射最多停在 4.999 s；下一次 Repaint 只在该 Token cue 已停止后移除该 source。非循环声源约束与 r6 实测 release 限定了正常路径，但实现没有独立 watchdog：若 Unity 永远报告同一 voice `isPlaying=true`，source 会继续被保留。
- Player 合同要求 mapped age 与当前音频状态下的公式相符且不倒退；还要求实际 cue active 超过 identity、结束回调不少于 WAV 时长（1 ms 容差）、source release 不早于音频结束加实测 Update 帧容差。未发现会把帧间正常释放误算成截音的本轮反例；本轮观测的结束延迟约 64–66 ms，release 延迟在双帧容差内。Listener/source cue 计数和最终未使用通用 Summon fallback 也有断言。
- `SourceBusy` 的视觉时长门仍以 H11 `duration=5 s` 为上界；音频 identity end 约 4.265 s，实测音频约 4.079 s 回调后结束，均早于 5 s。短输入门允许 0.2 s 后在 source 活跃时提交；H11 被保留为 native visual tail，source 最终结束后从下一次 Repaint 清理。r6 实证 `NativeVisualTailCount` 在权威响应后清零。这里的 5 s 是视觉保活上限，不替代音频实际播放结束判定。

## Token PCM 与尾部时序

分析器以两声道算术均值形成 mono；候选定位后直接在原生 48 kHz PCM 上分别搜索偶数、奇数帧相位，不重采样。每端用 128 ms 局部模板定位，并将已知按钮音与 Power UI 音作为 nuisance sources 做条件最小二乘；整段源投影用于交叉检查。

| 角色 | onset | 局部相关 / 增益 | 全段相关 / 增益 | 条件 Token 增益 | 加入 Token 后 nuisance 残差下降 |
|---|---:|---:|---:|---:|---:|
| driver | 0.618667 s | 0.90146 / 0.77961 | 0.83954 / 0.80325 | 0.80308 | 71.88% |
| observer | 0.618667 s | 0.89415 / 0.76059 | 0.83754 / 0.80356 | 0.80165 | 71.50% |

两个候选相位均在 1 个 48 kHz sample 内；局部相关约 0.89–0.90，条件拟合在加入 Token 后分别减少 3601.52 / 3589.35 SSE。该证据能区分 Token 对全场混音的贡献；它不把同一时间窗内各层的主观可听度量化。

Listener 是 48 kHz 双声道 IEEE float32 混音，各 236544 frames（4.928 s）。文件中存在绝对值大于 1 的浮点样本（driver peak 1.0836、24 个；observer peak 1.0477、7 个）；这是软件 Listener 捕获的数字范围数据，不是设备输出或硬件削波测量。Token 原 WAV 末 250 ms 的 RMS 仅 0.000310、相关约 0.022，因此无法单凭该近静音尾段证明每个尾部采样均可从混音中独立识别。

时序日志在两端都覆盖 Token identity 至 age 4.265208 s；未发现映射合同违例或身份结束后的 false→true。回调至首个 active 观测为 34.6–36.7 ms；回调至实测结束约 4.0776–4.0787 s，比来源时长多 64.4–65.5 ms。Token 结束至 source release 为 19.8 ms / 16.7 ms，均在记录的合并帧容差 47.0 ms / 42.8 ms 内。由此确认释放有界，短尾/更新采样容差不会把 source 无限持有；也不将约 35 ms 的首次观测间隔表述成原始音频被截断。

## 输入、EndTurn 与画面

r6 driver 在 input-unlocked 采样后同一 Update 提交合法 EndTurn；实测提交时间 0.424681 s，验收上限 0.623392 s（0.2 s 输入门 + 实测 Update 帧容差 0.418392 s + 5 ms）。权威响应与提交匹配，且在 source route 活跃时返回。此实测冷帧延迟高于 0.2 s；0.2 s 是门值，不是这次端到端提交耗时。报告中的 `sourceTailActiveAtSubmit=false` 也意味着该次证据证明的是 route/busy 阶段可提交，不证明提交瞬间 `NativeVisualTailCount` 必为正。

该次 EndTurn 后同一 Ghoul entity 71 收到 Death、生命为 0 并离场；两端的终态 hash 相同。r6 两端各十张报告截图均存在且为 1920×1080，包括 source-active、0.12/0.28 s button、input-unlocked、0.55/1.0 s eruption、source-released 与 after-end-turn。人工检查的 eruption 阶段只有一个 Ghoul 表现层，仍可见原破土/地面粒子；释放与回合末图中该随从已不在场，未见原先报告的远离召唤点的左下角幽影。该结论限于这些捕获时刻，不宣称逐帧无重影。

对照保留 r5：`work/f022-death-knight-r5/observer/source-eruption-0.55s.png` 显示召唤点两个重叠卡面，其中前层为 1/0；同一份状态报告仅有 entity 71 且 Death 已发生。r6 同名阶段截图只有一个表示。r5 独立 PCM/clock 复算留在 `work/v022-final-h11-token-audit-r5.json`，没有覆盖或重写旧证据。

## 相邻语音 smoke

`work/f022-speech-r4-final/summary.json` 的两端 smoke 汇总为 `passed=true`，两 Player 自然 exit 0，错误数组为空；每端 7 项表情，双方收到 14 次广播、记录 17 个气泡/语音播放。该项只是最新 smoke 交叉检查；完整 22 Player / 99 键审查仍以 `docs/verification/V022-hero-speech.md` 为准，本报告未重跑全量 Player。
