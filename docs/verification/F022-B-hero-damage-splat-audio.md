# F022-B Hero DamageSplat 健康伤害音源

## 原版来源

只读安装 `D:/Program Files (x86)/Hearthstone` 中 `Card_Play_Hero_DamageSplat.prefab:2ecc84d6a2db7461799ad55067409307`。Health 状态的 `AudioPlayAction` 使用根对象 AudioSource，并通过 `m_OneShotSound` 明确指向 `Audio_Container/SFX_BloodSplatHealth` 的 SoundDef PathID `8997213827551090078`。SoundDef 指向 `Blood_Splat_Damage.wav:4dbf1353322e92443997499cf169c06e`，固定随机音量 `0.75`、pitch `1`、category `1`；根 AudioSource 音量和 pitch 均为 `1`，动作 `VolumeScale=1`、`Delay=0`。

因此客户端需要复现“根 AudioSource + Health SoundDef”的绑定。只播放 `FX_Minion_AttackImpact` 不能覆盖该层；Blood Splat 与 Hunter `Explode_03` 是独立音源。

## 实现

- 新 `tools.native_hero_damage_audio` 从原FSM action解析SoundDef指针，导出原 clip、Health SoundDef、根 AudioSource模板和动作参数到 `local/runtime/native-hero-damage-splat-audio.json`，同时写可审阅清单 `manifests/native-hero-damage-splat-audio.json`。
- `NativeHeroDamageAudio` 只为 authority `Hit`、实际目标实体为 Hero 且 `amount > ArmorAbsorbed` 播源 Health 层。使用 source SoundDef 的固定 `0.75`，没有再乘客户端全局 `sfx.volume` 或修改 pitch。纯护甲命中仍走原 `SFX_ARMOR_HIT` 路由；有穿透的部分护甲命中保留护甲层并增加 Health 层。
- `HERO_07bp` 技能源打中该玩家自己的 Hero 时跳过通用 Health 层，沿原 Life Tap 专用音频路由。Presentation cue 的单次 fired 状态及 adapter 对同一事件对象的有界去重防止重复播放。

导出命令（root 串行执行，避免与共享 `local/` 导入同时写入）：

```powershell
E:/HS/HearthstoneClone/.venv/Scripts/python.exe -X utf8 -m tools.native_hero_damage_audio
```

## 定向验证

`PresentationChecks` 增加：无护甲 Hero Hit 播放、部分护甲但仍有健康伤害时播放、纯护甲时不播 Health、非 Hero 命中不播、Life Tap 自伤不添加 Health 层，以及同一 Authority Hit 对象至多派发一次；同时验证生成 cue 的 clip 绑定、`.75` 音量、pitch `1`、category `1` 和 AudioSource 模板。

本 worktree 未运行 Unity/Player，也未导出或写入共享 `local/`。合并后需由 root 执行上方导出命令，再串行运行 `PresentationChecks`、构建与 Hunter 实机回放；听感和完整混音尚未验证。
