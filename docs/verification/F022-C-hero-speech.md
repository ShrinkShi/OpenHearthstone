# F022-C：英雄语音与七种表情

日期：2026-10-26。实现交接记录；TASK-0007仍为IN_PROGRESS，等待总控重跑更新后的manifest导出、Unity编译及双端Player验收。

## 目标和边界

覆盖11个基础英雄的开场和投降语音/字幕/气泡，以及己方英雄右键发送感谢、称赞、问候、惊叹、失误、威胁、抱歉。惊叹与旧版抱歉分别映射WOW(type 33)和SORRY(type 6)。资源导出器保留原卡定义引用、字幕键、音频候选及bundle/CAB/path ID；不合成配音，不补写缺失字幕。

## 原版来源

- **Confirmed，CardDef：** 11张基础`HERO_*`英雄卡各有9个`m_EmoteDefs`，对应GREETINGS=1、WELL_PLAYED=2、OOPS=3、THREATEN=4、THANKS=5、SORRY=6、CONCEDE=7、START=8、WOW=33。WOW和旧版SORRY为不同定义。
- **Confirmed，字幕：** 99个英雄/语义字幕键均存在于原版`Strings/zhCN/GAMEPLAY_AUDIO.txt`；七个右键菜单标签来自原版`Strings/zhCN/GAMEPLAY.txt`。示例HERO_08的START字幕是`这可是你\n自找的！`，WOW为`太棒了。`，SORRY为`抱歉。`。导出器缺失时保留空值并报告，不造文本。
- **Confirmed，声音：** 对全部99条`m_emoteSoundSpellPath`进行zhCN resolver遍历，每条均找到AudioClip候选；候选随引用记录locale、bundle、CAB、path ID和时长。静态检查定位至原版`essential_base_zhcn-content-0.unity3d`中的AudioClip资源。
- **Confirmed，气泡图：** 原版`Emote_Bubbles` Texture2D，512×512，bundle `initial_base_global-8411aad9-texture-18.unity3d`，CAB `CAB-9700719c6895aa16719c40184051ca34`，path ID `-4582338155894409427`；研究临时解出的PNG SHA-256：`ab7f3735df678b24fc9619567737cfa184f5dc2320efed245b9efc148ae0f7a6`。atlas包含上下两个气泡；上半尾尖核心像素(420,209)，下半(121,455)，分别以source rect [0,0,512,256]/[0,256,512,256]及对应UV保存。正文矩形留在主体内部，opponent半图垂直翻转，尾尖对齐各自英雄边缘。
- **Unknown，原版Actor绑定：** 基础英雄CardDef的标准`m_emoteSpellPath`为空。已发现的通用聊天框资源不足以证明其就是标准英雄Emote Actor。本实现直接重用已确认的`Emote_Bubbles` atlas并重建文字框，manifest明确标记Actor绑定未确认；不宣称Actor动画/布局等价。

以上为只读资源索引/resolver追踪结果。没有向`local`写入音频、PNG、索引或缓存；由总控在共享工作区串行运行正式导出。

## 协议与客户端行为

- hello发布`hero_emotes_v1`。客户端缺少该capability时不发未知消息，右键菜单仍显示且点击后给发送者兼容提示。
- 客户端只发`{type, match_id, emote_id, request_id}`；没有seat或hero字段。服务端要求peer仍在当前match room的其座位，英雄CardID从服务端match状态推导；只接受七个稳定ID，按seat限频，使用有界request_id表去重。重放只返回ack，不重复广播；无效/限频错误只回发送者，不经规则state/error路径。
- 接受的emote单独广播给配对双方；不递增Match.version，不改变public hash或legal moves。客户端以match/event ID去重，不改变pending/Presentation输入锁。
- 新match的两名英雄从seat 0起依次触发START，按实际clip长度串行播放并显示字幕气泡。开场Coroutine和异步音频加载携带generation，匹配重置/断线/菜单返回/Concede会取消旧工作；两席用独立AudioSource，不停技能FX/SFX。显式Concede只有在权威`Match.apply`成功后新增语义事件；disconnect路径不标成主动投降。双方收到Concede后以权威owner/card/event ID播放一次；气泡锚在投降者英雄处并保留到结果截图/画面。
- H09交叉协议：成功`HERO_09bp`目标施法的`PlayHeroPower`执行后记录`HeroPowerTarget`。测试区分满血目标与受伤目标，不伪造Heal、不改health/hash，并覆盖重复action和stale拒绝。

## 实现与验收入口

- `tools/export_hero_speech.py`：默认导出11英雄、99音频候选/字幕、菜单标签、来源清单与原版气泡纹理；`--locale`支持zhcn/enus/zhtw/kokr/jajp，`--heroes`可限卡。由仓库根运行：

  ```powershell
  & '.venv\Scripts\python.exe' -X utf8 -m tools.export_hero_speech --locale zhcn
  ```

  更新后的Exporter也写入tracked metadata-only `manifests/original-hero-speech.json`，记录CardDef type/string key、source ref、CAB/path ID、候选SHA-256与bubble atlas区域，不包含完整字幕文本或音频二进制。根控此前的99项缓存来自旧Exporter版本，需用上述命令重新导出以生成新metadata。

- Unity Editor的`HeroSpeechChecks.Run`检查card database全部11英雄、每英雄9个字幕/原声候选、7标签、WOW/SORRY源类型、导出文件和气泡atlas来源，并要求Actor未知保持显式。
- 独立服务端临时localhost端口测试覆盖capability、两peer TCP广播、服务器推导seat/hero、request重放、非法ID、限频、state/hash不变；pytest还覆盖H09满血/受伤/重复/stale与主动投降/disconnect差异。
- Unity Editor `HeroSpeechChecks.Run`还验证11×9来源条目、clip/字幕、WOW与旧SORRY独立、气泡atlas区域/锚点，以及七项菜单Replay接口。双Player入口`tools.hero_speech_player`在正式构建后启动私有服务端/两个独立profile，不接触已运行的37240服务；各自输出JSON报告和菜单、气泡、结局截图，显式核对Concede bubble和双方原声次数。Replay仍不替代真实鼠标/人工听感检查。
- 按总控对H11 ghost问题的交叉修复，`ClientApp`主PresentationCamera也排除了layer 30；离屏staging仍由既有隔离相机采样，主场景不再重复直画。此修复未由本分支运行Player验证。

## 本分支验证与限制

- Python定向命令：`& 'E:\HS\HearthstoneClone\.venv\Scripts\python.exe' -X utf8 -m pytest tests\test_hero_emotes.py tests\test_hero_speech_export.py -q`。
- `git diff --check`用于提交前检查。
- root在checkpoint `edffed2`后报告已正式导出11英雄/99候选，且基础server/Exporter/MATCH定向测试通过；本分支因增加clip SHA、bubble regions和tracked metadata写入，需要root重新运行导出。此分支只执行Python定向pytest和diff检查，未运行Unity Editor/Build或Player双端对局/音频Listener/气泡截图。因此运行时音频实际播放、Unity接线、视觉锚点及Concede结果保留仍待root验收；Actor链接仍Unknown。
