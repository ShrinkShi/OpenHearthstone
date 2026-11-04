# TASK-0009

英雄对话校对、随从登场语音与通用装备音效。DONE（本地有界交付），F024，2026-10-27。独立V024报告提交8bf73a8912c56d2694ddf8d4c0dfa99f1967f489通过后，由总控登记READY_FOR_REVIEW→VERIFIED→DONE；不扩为完整游戏或全部原版视听还原。

英雄实际对话泡和右键七选项加入F6 UI调试器，复用实际渲染及移动/缩放/旋转/文字/保存/重载。登场语音在真正Summon时触发，包含技能召唤CS2_101t报告兵，不能CardPlay提前播。所有武器实际装备有通用原声；已有Rogue专用source不可双播。

保持F023 source/战斗结算语义；检查真实事件实体/时机/去重。音频经Importer/Registry，原安装和用户校对不覆盖。新Editor、双Player、Listener及保存重载后独立V024。共享local/Unity/Player仅root串行。

## 实现候选（2026-10-27，等待串行验收）

- 英雄对话泡与七选项复用 `HeroSpeech` / `HeroEmoteMenu` 的原气泡渲染；F6 新增 `battle.speech`、`battle.emotes`，显示同一渲染器的预览，并把气泡、文字与每个选项分别注册到 `UICalibration`。校准回归使用临时文件检查七项布局、气泡文字、保存与重载，不写用户配置。
- 随从语音路由由 `NativeMinionSummonVoices` 按真实 `Summon` 实体绑定。优先使用当时的 `Summon.visual.entity/card_id`，可正确保留同批变形/死亡/离场前的原实体；缺少 `visual` 时仅接受新增场上实体。源 CardDef 的 `m_PlayEffectDef.m_SoundSpellPaths` 与 `m_SuppressPlaySoundsOnSummon` 由 `tools/export_voices.py --summon-minions` 导出，涵盖当前 CardDef 范围并强制包含 `CS2_101t`；自定义召唤/生成路径留作来源元数据，不当作音频。
- 通用武器装备源音频将 `NativeBattleWeapons` 的 41/42 槽扩展到任意新装备的武器实体；Rogue 既有耐久/损坏 7/5 时机仍保留。Editor 回归夹具包括 `CS2_106` 与 `EX1_567`。
- 上述为实现候选历史，实际验收已完成：449条当前随从/衍生PLAY来源有界导出、真实F6两页面捕获及位置/旋转/文字样式保存重载；r14完整Unity/GPU/构建通过，Battle32、Weapon142。Paladin双Player两端各一次报告兵PLAY；源码相同的R9回放及原始clip/SHA/Listener总混音关联见F024记录。CS2_106/EX1_567四对8Player（含基本攻击对照）在R14实际新跑，装备41/42、耐久7、损坏5与攻击前快照归因通过。Rogue重复装备与耐久序列有Editor回归，未把它扩为本轮Rogue-specific Player全量实测。

独立复核见 `docs/verification/V024-hub.md`。账号PC界面的自动点击尚未通过属于TASK-0011，不以本任务局部通过覆盖。
