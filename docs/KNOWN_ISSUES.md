# 当前真实未解决问题

更新：2026-10-27。只记录开放项；解决后写证据并移出本列表，历史保留在 Task/Git。KI-002 局部命中已由V002-r2（213acba）验收；TASK-0003根编排及五轮停止/所有权缺口已由全新V003-r6（59ae909）独立验收，总控登记DONE。以下保留超出该验收范围的真实开放项。

| ID | 问题与证据 | 跟踪 |
| KI-016 | 卡组标准轮换数据和游客跨职业规则尚未实现；标准格式可保存与导入导出但不能开始对战。收藏式组卡的原版逐像素视觉、系统剪贴板、hover预览和真实断线重连尚未完成独立验证。 | TASK-0015；V028 |
| KI-015 | F025用户四项本地修正已独立通过：最终ac95ced/F07640E5，Editor原生153输入/26RPC/25图、同DLL Windows handler八步/14请求/22图。包方向/UV、商店紫底木框与旧3D黑带、4+4任务、收藏分区/上层详情、设置登录退出均有实证；R1–R6退修保留。原Frame_PC为派生复用，完整旧PC Collection相机/Shader/动态/逐像素仍Unknown；OS输入、Standalone真实GUI及双Unity账号整局不在这些证据内。 | TASK-0011/0012；F025与V025-hub |
| KI-011 | F023 H10持续热帧已显著改善，quiet实际P50约4ms，但首次cold约263ms，仍有51/83ms未归因慢帧。PNG采集开销已用无PNG实际GUI对照排除，不能把0.2s逻辑门或热路径中位数当全程流畅保证。 | TASK-0006；V023-rogue-performance |
| KI-012 | 新VS开场本地功能使用原Mesh/UV/Transform/材质/原声自主重建；原VS_Shadow、shader、动态灯光/mip选择/原camera/controller/prefab动画尚未恢复。透明几何图正确不等于官方逐帧1:1。 | TASK-0008有界交付之外；V023-opening-audio |
| KI-013 | F023补跑NativeAudioLoopChecks/NativeSourceAudioChecks.Run全链时，下游NativePropertyChecks仍在“authored float curve reaches actual particle playback speed”失败。SourceAudio17项自身通过并新增独立入口加入聚合，但不把全链失败抹成PASS。 | F023失败工件；原属性曲线后续范围 |
| KI-014 | H04/H09原Start片段进入Listener的波形证据强；单独Loop音轨受重叠/原3D衰减影响，尚无有力独立归因。保原模板，不擅自增益；组件playing/正确位置不冒称人耳可听或硬件loopback。 | TASK-0006；V023-opening-audio |
| KI-010 | TASK-0005首轮R2五项本地修复V005-r2 PASS后，用户复验又发现发射音效听感异常、飞行只有圆球缺参考所示拖尾/粒子，并要求恢复命中闪光及数字伤害显示。5张本技能阶段JPG已固定于work/user-review-2026-10-19-round2并记录哈希；未核原轨/控制器/伤害浮字链之前不得凭视觉描述猜特效实现。版本、帧间隔及原声未知。 | TASK-0005 新F005C/V005 |
| --- | --- | --- |
| KI-009 | R2五项本地修复已由全新V005-r2独立有界PASS，当前实物仍待用户主观复看：Arrival前无提前受击/扣血且Hit各一次，恢复Start/Missile自动R2T主体/Bloom及源投影，共用Actor/技能详情使用原普通战斗白ambient，所有目标入口采用原箭头；暂停/倍率和实际窗口已独立核实。原engineDelta异常根因仍Unknown，单调帧输入已回归。不得把程序/来源通过写成原版完整视觉通过；原运行/官方比较缺项见KI-008，当前截图与接触表见V005-r2报告。 | TASK-0005；本轮程序缺口已解除，主观视觉未确认 |
| KI-001 | 正式公开路径无原历史，raw传输/protobuf/官方GameState/ZoneMgr宿主仍未接入；Task3只验收显式规范化输入的真实TaskHost。五轮程序缺口已由V003-r6解除，历史FAIL不撤销。源目标消失只pending--、不Finish/下一发是已核实原语义，禁止超时冒充成功。见ORIGINAL_SUPER_SPELL与V003-r6。 | 原历史传输后续独立范围 |
| KI-003 | 原场景几何与机位精度仍Unknown。两卡实际所需LookUpdate(time/up/无axis)已通过V003-r6；joint/其他分支未触及而明确拒绝，手牌槽须显式宿主，不能把两卡范围的验收迁移到这些未实现分支。 | TASK-0004 几何；其他分支后续范围 |
| KI-004 | EX1_134（历史误称“刺骨”，实际军情七处特工）旧诊断白色中心及完整 Shader/场景光照/后处理仍未解决。Task3 补齐原 Trail 参数/材质/time 绑定与表现时钟路径点；原引擎内部顶点年龄/细分/纹理铺展及完整 Trail Shader 精度 Unknown。详见 ORIGINAL_SMOKE_SHADERS / ORIGINAL_SUPER_SPELL。 | TASK-0004 |
| KI-005 | V004-r2（ae97e87）独立确认局部工具/来源/Shader GPU通过，但实际关联35候选没有双卡同条件官方序列；八分项仍Unknown，跨来源度量未实现/执行。当前BLOCKED，需填写task-0004-official-input.json后再核真实性/可比性并继续度量/独立验收。本项目28帧及C/K稳定重复、局部RT或后台比赛不代替官方Ground Truth。 | TASK-0004 |
| KI-006 | 无原安装/缓存可完整运行未验证；本地导入仍是当前必要环境，发布资产清单、全部 manifests 公开边界未逐项审计。 | ROADMAP P2 |
| KI-007 | Codex App项目/附件管理的历史调用为Invalid app tool request，UI Project归属未核实。本轮原生独立Feature/Verification已实际执行并提交，不依赖可见UI聊天，不能把该UI接口问题说成无法编排Agent。 | TASK-0001 外部 UI 步骤；不阻塞原生编排 |
| KI-008 | HERO_08bp本轮修复经独立V005-r2本地有界PASS，完整保真仍BLOCKED。原全局光照keyword/灯数组/atten映射、四种主光阴影分支、原运行Camera/layer9 hitbox/遮挡/HDR服务未闭合；31/32无阴影源公式已实现，原普通阶段Board白ambient已恢复，当前主/附加灯0不是原事实。153媒体清点仅两张敌方法师静态按钮参考，无所搜范围本技能动态/音频序列；R2实际颜色/主体改善不等于官方逐帧通过，旧V005-r1亮度/尺寸结论只属旧实现。实际DSP保留超1浮点样本，不证明设备硬裁或官方听感。解除需同版本可比施放/取消/指示器音视频与原运行服务输入，再恢复实际选支并逐项比较。当前DLL1920/1280与15回合自然局独立成功，不是阻塞。 | TASK-0005；完整保真BLOCKED，证据见V005-r2 |

四项大型原源重读在最近回归被排除：这是证据覆盖限制，不据此推断代码失败。旧 README/BUILD 历史数值不作为当前 baseline；详见 PROJECT_STATE 与专题原始记录。
