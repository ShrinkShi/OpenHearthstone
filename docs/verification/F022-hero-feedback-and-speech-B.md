# F022 来源特效与声音调查 B

2026-10-26，TASK-0006，IN_PROGRESS。当前分支只提交来源导出与实例化修正；源缓存重导和实际 GPU/Player 回放由总控串行执行。

## 圣骑士 H04：头像方块来源

**Confirmed**：用户提供的原参考 `work/f021-reference-inputs/paladin.png` 显示头像周围薄金光；本轮最新截图显示头像下半部被明亮矩形覆盖。来源 prefab `ReuseFX_Holy_Untargeted_01.prefab:e928ccd16a8d24a4dad18ce6e3ee3b10` 来自 CAB `CAB-8e3755b5c7f289df2f238a71420d2eae`、bundle `initial_base_global-41caf0cf-prefab-3.unity3d`，源 SHA-256 为 `fd1fad849ceb3439a380097fba970e9cd0831840b85667305a2318d3beca7e1`。

源节点 `Card_Hero`（node 11）启用且可见，使用 `Hero/Additive/Additive` 与白色源贴图，GameObject layer 为 21。它不是 node 2 的 OffScreenRenderFX 捕获源子树；最终 NativeEffectCamera 只渲染 layer 30。旧 exporter 丢弃了 `GameObject.m_Layer`，实例化时又把所有节点改为 layer 30，因此将 Card_Hero producer 直接叠到最终图像，形成矩形亮块。无需隐藏或删除源 Renderer；其 layer 21 使它避开最终特效相机，仍保留源可见性供捕获逻辑使用。

本次修改由 `tools/native_effects.py` 导出 `source_layer`，NativeNode DTO 接收它，NativeEffectInstance 仅保留源 layer 21，其余节点仍使用专用 layer 30。Paladin Editor 检查要求 node 11 仍启用、仍在 layer 21，并断言 PreviewCamera 排除 layer 21、包含 layer 30。原缓存还未由本分支修改；总控合并后应正规重导 `HERO_04bp`，再做 GPU 和 Player 对照。本轮没有 GPU/Player 结果，不能宣称视觉已通过。

## 术士 H07：前中段半径缓存

**Confirmed**：`local/runtime/9a9e85e32683f6bc789c18c3.hero-power-source.json` 的粒子 `shape.radius` 有 16 个 `{value, mode, spread, speed}` 对象，而 NativeShape DTO 将半径读为 `float`。已存在的显式迁移工具只把可证明无损的 constant wrapper 化为 `value`，不改其他字段。

Root FSM 的 `Play FX` 依次激活 node 3 `Offscreen Particles` 与 node 8 `RenderToTexture SmokeRingPuff`，播放 node 4 及子节点 5–7 的粒子，再播放原音频并启动 node 9 及子节点 10–11 粒子，最后等待 4 秒。node 4–7 的半径分别为 1.0、0.2、0.2、1.5；特别是两个吸入 wisps 的半径在旧 JSON DTO 路径会归零。node 12 及子节点 13–20 源初始 inactive，且此 root FSM 未激活它们，不能以任意打开它们替代修复。

只读 dry-run 精确选中上述 HERO_07bp 文件，报告 `converted=16, already_scalar=0`。总控应只运行 `python -m tools.migrate_shape_radius_cache --card-ids HERO_07bp`，随后正规重导 HERO_07bp，并核验 Early/Mid/Tail。Dry-run 不修改共享 `local/`。这解释了前中段发射形状塌成点的可能机制，但迁移后的实际效果尚未回放验证。术士自伤不震动由表现层修复负责，本分支未改 Presentation。

## 猎人 H05：爆炸音可听性

**Confirmed**：H05 Impact 的 DeathState2 action 10 是 0 延迟的 `AudioPlayRandomClipAction`，三个等权 SoundDef 都是 Targeted Explode；正确 Explode_03 clip 为 `84debef801d83ef877fed63e.wav`，`d47...` 是 Build_02。动作 volume/pitch 为 1、spatialBlend 为 1、无 template override。原生播放克隆全局 source 模板（volume=1、MinDistance=100、MaxDistance=500、linear rolloff），随后应用 cue volume=1；命中附近 listener 距离在 MinDistance 内时，距离曲线不应衰减。该动作没有配套 Stop/Fade。ClientApp 对 H05 仍播放通用 `SFX_ATTACK_IMPACT`，所以现有路由包含通用 Hit 与来源爆炸两层。

F021 Listener PCM 的相关性和投影增益只证明声音进入 Unity Listener mix；没有设备 loopback，无法证明系统输出或用户听感。当前证据未定位到可安全修复的 source 音量、音高或截断错误，故本轮没有盲目放大或替换音源。现有 FireblastReplay 的 `WrapAudio` 在 `NativeAudioPlayback.Play` 后通过 `Note` 记录实际 AudioSource：clip、`isPlaying`、volume、mute、spatialBlend、min/maxDistance、voice/listener位置和距离。总控可直接复用 `--hunter` Player 回放；该记录暂不包含 `outputAudioMixerGroup` 和 `rolloffMode`。如实例值正常但听感仍低，再补录这些字段并采集设备 loopback；猎人听感仍标记未解决。

## 定向验证

- `python -m pytest tests/test_native_effects.py tests/test_priest_player_replay_contract.py -q`：81 passed。
- `python -m py_compile tools/native_effects.py`：通过。
- `git diff --check`：通过。
- H07 迁移工具 `--dry-run --card-ids HERO_07bp`：精确命中单文件，16 converted，0 already_scalar；只读。
- Unity Build、GPU、正式 Player 与设备音频 loopback：本分支未运行。
