# TASK-0008

## 任务名称
原版对阵开场、播报与起手换牌展示顺序。

## 当前状态
DONE，2026-10-27，本地有界范围。r9本地功能、原来源及实际双端已按独立V023给出PASS，总控登记READY_FOR_REVIEW→VERIFIED→DONE。复用TASK-0007原声基础和现有匹配/换牌规则；不提升TASK-0006完整官方视觉状态，原动态Actor等未恢复边界见下文。

最终固定源 `60fd1b8b6717a0a66d92474d8741e9b325eae03e` / r9 DLL `496937AFCF0D5897251656D2BC45AACA9DFBB02FAAB75B017E6116A041D673B8`。r8/r9实际双Player `work/f023-opening-priest-dh-r4` / `r5` 完整PASS：原before→versus→after播报、稳定VS、大英雄姓名/职业、真实Keep与mulligan输入Repaint、START继续播放时换牌可用、进入play后的七表情与投降结果均有时序/截图/Listener。只控制显示，规则服务器不等动画。

最终VS由原prefab的V/S Mesh/UV/材质与三个Transform链，按正面+Y、X/Z投影和深度缓冲渲成透明派生图；1372原三角中绘制962正面，保原atlas和Repeat/Bilinear采样。来源65项/0错误、像素透明区/哈希与引用闭环均可核。保留完整atlas直绘及UV并集失败工件，未冒充官方动画恢复。现有淡入/淡出/换牌衔接为自主重建；原VS_Shadow、shader/light/mip/camera/controller及完整prefab动态不在有界完成声明内。主报告[V023](../verification/V023-opening-audio.md)，失败与实现历史见F023。

## 范围
按用户本轮图二/图四加入棋盘变暗、己方左下/对方右上的大英雄拱框、英雄姓名/职业、VS，以及原版XX对阵XX播报。开场完成后展示起手抽牌与替换/留牌，保留双方英雄开场原声及气泡并安排相位。只控制客户端展示，不让权威服务器等待动画或改先后手/抽牌规则。

## 验收
- 11基础英雄姓名/职业/原画与原声音源可核查，不能合成配音冒充原声。
- 原资源/动画可追溯；未解析的原Actor或运行合同明确标记。
- 两个座位与先后手的VS/播报→起手展示→换牌交互顺序正确，无开场阶段的多余操作/气泡遮挡。
- 旧英雄七表情/投降、正常匹配/断开/结果流程不回归。
- 新Unity构建与实际双端Player截图/日志/Listener，随后独立Verification。

本轮主记录：`docs/verification/F023-feedback-and-opening.md`。

## F023-C 实施进展（未验收）
- 静态 Cecil 仅读原安装 `Assembly-CSharp.dll`（SHA-256 `786376BDDAD544709639D4B3AB60CE8799D4B1C6C7D7FC4C225CE79FE07228C6`）：`MulliganManager.HandleGameStart` 装载 `GameStart_VS_Letters.prefab:3cb2cbed6d44a694eb23fb8791684003` 和 `VO_ANNOUNCER_VERSUS_21.prefab:acc34acb15f07ff4ba08025a57a9a458`；`WaitForHeroesAndStartAnimations` 依次取己方 `AnnouncerLineType.BEFORE_VERSUS`、播放 `versusVo`、取对方 `AFTER_VERSUS`，均等待当前声音结束。三段源路径来自原 `CardDef`；人物姓名/职业来自当前本地 CardDef/职业表。未执行原程序集。
- 新增 `tools.export_voices --base-hero-announcer`，按11个基础英雄的 `m_AnnouncerLineBeforeVersusPath` / `m_AnnouncerLineAfterVersusPath` 导出音频候选、Registry ID 与来源manifest；缺少专用字段时记录使用原 `m_AnnouncerLinePath` 的fallback。通用 VS 片段按确认的prefab GUID读取其 `SoundDef`、primary `m_AudioClip` fallback和所有 `m_RandomClips` AudioClip引用。只读IL确认原播放先按每项 `m_Weight` 加权随机选择一个 `m_RandomClips` clip，随机选择为空时才使用 `m_AudioClip`；自主端复用同一加权算法并一次播放一个候选，manifest保留候选及权重，Player时间线记录实际clip ID。`tools.presentation_assets` 新增原 `VS_letters` 运行素材及 VS prefab 的 source-only provenance。
- 本机只读 asset-index 静态记录：通用 VO prefab 是 `essential_base_global-prefab-0.unity3d` / `CAB-69c88b234dfa427e02d1fcee9337ae34` / PathID `-1978496017990928215`；`GameStart_VS_Letters` 原 prefab 同CAB、PathID `3060361679846831233`；原 `VS_letters` Texture2D 是 `essential_base_global-texture-1.unity3d` / `CAB-e02b69b1bbe893a6736884865127e637` / PathID `-945091578145489584`。播报manifest会记录通用prefab的GUID、CAB和PathID，presentation provenance会记录 VS prefab 和纹理身份。
- `BattleOpening` 在客户端绘制棋盘压暗、双方英雄大图、姓名/职业和原 VS_letters；之后顺序播放原 CardDef 前播报、原 versus clip、对方后播报。三段完成后启动 F022 的 `START` 语音与气泡；VS 淡出后释放 `MulliganPresentation` 的起手换牌输入，不等待 START 全播完。正常棋盘继续绘制 START 气泡并按语音时长自然过期。开场输入、自动操作及可访问按钮仅在 VS 展示期间禁用；不改变Server状态或回合结算。
- 原始 `GameStart_VS_Letters` prefab 只作为 source-only 证据记录；自主开场动画的布局/淡入时长是客户端重建，不能据此声称复原了官方 prefab/Actor 动画合同。原始三段播报顺序及 SoundDef 候选策略已静态确认；缺失引用、无效权重或来源图不完整时拒绝运行绑定并报告候选。actual Player harness 会保存阶段/声音时间戳及 VS、换牌显示、换牌输入截图；音频实际听感、Listener、双端/不同座位行为和正式构建仍待总控串行验证；本Feature没有运行Importer、Unity、Player或测试。
