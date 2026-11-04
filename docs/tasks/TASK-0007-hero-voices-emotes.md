# TASK-0007

## 任务名称
英雄开场、投降、攻击及七种表情的原版语音和气泡。

## 背景与目标
2026-10-25用户明确要求新增英雄语音/气泡，包括开场、投降，以及右键己方英雄可发送的感谢、称赞、问候、惊叹、失误、威胁和抱歉。惊叹与旧版抱歉同时存在，覆盖当前11职业基础英雄。

## 已知事实
- Confirmed：实施前的server只有join/action/cancel/ping；F022已新增专用hero_emote协议和气泡UI，表情不改规则状态。
- Confirmed：英雄表情来源、99条原音频/字幕及原气泡贴图已核查并正常导出；完整原气泡Actor绑定仍Unknown。
- Confirmed：复用当前Python权威server、Unity客户端和Importer/Registry，不重建架构。

## 实施约束
原资源只读，原音源/原字幕必须保留来源与候选。未找到的历史抱歉原声不得伪装为已恢复，不合成配音冒充原声。表情广播由服务器验证对局/发送者并限频，不改游戏状态版本或合法动作；双方正确映射自己的/对手英雄气泡。不得执行原DLL。

## 验收标准
1. 11个基础英雄可正确触发开场和显式投降语音/气泡，每次只播一次，双方可见可听。
2. 右键己方英雄显示七项；点击发送、点击外部/取消关闭；对手同步收到，不能代发对手表情。
3. 限频及非法请求测试，表情不改变规则/hash/version；回放/重发不重复播。
4. 原资源来源/字幕/候选清单；实际Unity编译和双端Player行为/气泡截图/Listener音频证据。

## 当前状态
DONE（2026-10-27，本地有界范围）。r9综合构建及最终双端七气泡/开场/Keep/投降通过，独立V023已给本地有界PASS，总控登记READY_FOR_REVIEW→VERIFIED→DONE。F022原99条开场/七表情/投降的V022有界DONE保留。原Actor绑定和官方逐帧视觉不在完成声明内，完整职业技能见TASK-0006。

F023最终候选r9 DLL `496937AFCF0D5897251656D2BC45AACA9DFBB02FAAB75B017E6116A041D673B8`，固定源码 `60fd1b8b6717a0a66d92474d8741e9b325eae03e`。`work/f023-hero-attacks-all-r1` 的24个r6实际Player覆盖11基础英雄各双端与Rogue合法CS2_082对照；新分析器只读重验与独立原PCM/来源核查通过。无武器测试在测试fixture中临时+1攻击，真实Match接受Attack；每个事件只触发一次映射原声。15个原clip哈希匹配，本轮动态选中各英雄_0，未宣称全部随机变体覆盖。r9相对r8仅HeroEmote与其检查器的布局差异，最终 `work/f023-opening-priest-dh-r5` 两端自然exit0/errors空，前三行左右齐、右第四抱歉、7发送与14广播、原字幕/气泡/投降均完成。报告见[V023](../verification/V023-opening-audio.md)。

F023原攻击语音已从11基础英雄CardDef导出15个clip，绑定已接受英雄Attack事件；新菜单使用原atlas左三右四。r4新构建与牧师/DH双端开场、七项发送/接收/投降通过，稳定菜单截图与11英雄实际合法Attack/Listener仍待追加。对局VS/原播报另见TASK-0008。主记录为[F023](../verification/F023-feedback-and-opening.md)，独立V023已启动，不预先宣称新范围通过。

2026-10-26 集成实测：r7真实22个Player自然exit0，日志确认11英雄每人9种语义，99个不同hero/emote键；覆盖原START、七表情、每英雄显式CONCEDE。六组r2加五组反向r3的22个报告和Listener float32 WAV逐样本重验通过，统一工件`work/f022-speech-all-22-revalidation.json`。旧r2摘要保留初版validator把首次THANKS截图与末态SORRY比较产生的FAIL；修正仅分析范围，不回填历史报告。当前截图已经是与实际己方广播及已Repaint语义匹配的真实表情，原字幕/文本/英雄绑定也被核对。新协议房间移除seat0后的列表索引问题已经独立TCP复现、修复及重验。独立报告见[V022-hero-speech](../verification/V022-hero-speech.md)，总控据此登记 VERIFIED→DONE。物理鼠标/官方Actor动态/设备扬声器听感未冒称已验。

r12新DLL `E1E9329E34BDBEC05D4F949A2AAFB2047983AC0797E7CCCC123208CEC2329DBD`上的最终smoke在`work/f022-speech-r4-final`：Mage/Warrior两真实Player自然exit0、summary.passed=true，开场、七项菜单/双方广播/语音/实际气泡和投降均通过。此为同一语音实现的后续集成复核；没有以两英雄smoke替代前述11英雄99条的独立证据。正常阶段自动交接，继续TASK-0006，不创建未授权的新功能范围。

## 本轮实现与源证据（F022-C）
- 源检查确认11个基础英雄CardDef各有9个`m_EmoteDefs`：问候、称赞、失误、威胁、感谢、旧版抱歉、投降、开场及WOW；WOW(type 33)与旧版抱歉(type 6)保留为不同语义/来源。中文`Strings/zhCN/GAMEPLAY_AUDIO.txt`的99个字幕键均存在，七个菜单标签来自`Strings/zhCN/GAMEPLAY.txt`。
- 只读resolver检查确认这99条语音引用均能解析到zhCN AudioClip候选；气泡贴图`Emote_Bubbles`是原版512×512纹理。标准英雄CardDef的`m_emoteSpellPath`为空，因此当前接入直接使用原版贴图并重建文本框，原版气泡Actor绑定仍Unknown，不宣称恢复Actor。
- 新增`tools/export_hero_speech.py`串行导入源音频、字幕/候选/来源manifest和气泡贴图；客户端从`hero-speech-index.json`加载，不嵌入安装路径。正式导出11英雄/99条候选且errors=[]，SHA-256、atlas region和metadata-only来源清单已落在`manifests/original-hero-speech.json`。
- server hello公布`hero_emotes_v1`；独立表情消息仅从当前配对peer/seat推导英雄，白名单七项，做逐席限频与有界request_id去重。广播不发送state，也不改match version/hash/legal moves。旧server缺capability时客户端保留本地开场表现并提示表情不可用。
- 客户端己方英雄右键菜单有七项；表情事件和显式、已接受的Concede语义事件按event_id去重，双方各播一次对应英雄原声/字幕。开场在新match仅触发一次，固定seat 0后seat 1串行等待音长；跨match、菜单重置、投降会递增表现generation并使异步加载/旧开场失效。独立的每seat AudioSource只管理英雄语音，不触碰技能FX/SFX；显式投降气泡在结果画面保留。
- 气泡直接使用原版512×512 `Emote_Bubbles` atlas；清单记录上下半区UV、原像素尾尖锚点及正文矩形。own气泡在己方英雄上方且尾巴锚到英雄上沿；opponent气泡在对手下方并垂直翻转后尾尖锚到英雄下沿。原版Actor绑定仍Unknown。
- 同时按F022-B要求，成功的`HERO_09bp`非零合法目标施放新增权威`HeroPowerTarget`事件，仅记录真实power实体、已校验目标和seat，不造Heal、不修改规则状态/hash；旧事件处理仍按原路径执行。

独立记录、源定位、导出命令、协议合同、测试和缺项见[`F022-C-hero-speech.md`](../verification/F022-C-hero-speech.md)。

## 实施/测试/提交
Focused Python测试覆盖协议、导出及实际报告/Listener校验；Unity `HeroSpeechChecks.Run`已随r13聚合构建通过11英雄/99条和七菜单项。`tools.hero_speech_player`使用独立临时端口启动双Player，覆盖开场、与真实Event adapter共用的右键菜单处理、双方广播、投降气泡/结局及Listener录音；正式22个Player及最终smoke均由总控串行执行，独立V022复核原工件。Replay不是人工物理鼠标测试，软件Listener不是设备loopback，原Actor绑定未知边界保留。

## F023-C 扩展（审核中）
2026-10-26 增加11个基础英雄的已接受 `Attack` 原声和头像两侧的七气泡菜单布局，保留此任务中F022的99条语音/字幕、投降、开场对白及协议。实现提交为 `20f7116` 与菜单绘制helper `cbac120`；当前F023 Task8实现进度及正式构建/Player待验收见[`F023-feedback-and-opening.md`](../verification/F023-feedback-and-opening.md)。
