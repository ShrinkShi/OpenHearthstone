# F021 全职业原版截图反馈落实

2026-10-24 至 2026-10-25，TASK-0006，IN_PROGRESS。

## 当前交付进展（2026-10-25）

本轮已实现并实际回放萨满、德鲁伊、战士、盗贼、术士、牧师、圣骑士、猎人与恶魔猎手，不是仅修改死亡骑士。11职业共用原版技能按钮Actor并绑定各自原画、费用与描述；下列证据属于本地实际运行与用户参考图对照，不声明官方逐帧/原声全等。

| 职业/范围 | 已落实行为 | 实际证据 |
| --- | --- | --- |
| 萨满 | 按钮符文和真实召唤点图腾/闪电分层；源特效结束后交接完整Actor，之前隐藏原画、框、攻血、嘲讽和睡眠 | `work/f021-shaman-r1-1920` 双端PASS；19项细分检查，source活跃期间输入已开，不等同于source结束后检查 |
| 德鲁伊 | 真实Impact clone的獠牙按按钮尺寸捕获；攻击与首次护甲反馈同批开始；可见施法结束后不再被清理尾段锁住 | `work/f021-druid-r1-1920` 双端及analyzer PASS，source后解锁/合法后续操作/hash同步 |
| 战士/通用护甲 | 按钮原光圈保留源世界比例；首获护甲闪光、已有护甲仅更新数值；按吸收量选原护甲受击声，徽章随英雄运动 | 最终`work/f021-warrior-r3-1920` 双端及analyzer PASS；已查看实际外圈关键帧。GPU早期core 42×42、后续ring 133×146，相机/clamp未改 |
| 盗贼/武器 | 按钮原黑烟、原WeaponSummon贴图/RGB Hermite曲线及已核验加法base；攻血同时结算，耐久与损毁各有随后原音效 | 最终`work/f021-rogue-weapons-r3-1920` 双端及analyzer PASS；已查看白蓝equip关键帧，无黑底矩形；两次真实攻击/2→1→销毁链通过 |
| 牧师 | 选择Start、目标Impact、实际Heal分阶段；取消/过期拒绝无残留；居中大号白字黑边，无+0或受击抖动 | 最终`work/f021-priest-r4-1280` 双端及analyzer PASS；实际参考帧中+2位于英雄中央且字号已修正 |
| 术士 | 自伤不触发英雄受击震动；紫色原source效果；伤害星形与数字在命中时直接可读，移除虚构的一秒放大 | `work/f021-warlock-r2-1920` 双端及analyzer PASS，source后解锁/hash同步 |
| 圣骑士 | 原金光位于英雄头像，招募与视觉尾段解锁分离 | `work/f021-paladin-r1-1920` 双端及analyzer PASS，旧尾段活跃时合法结束回合且新回合hash收敛 |
| 猎人 | 唯一原爆炸音绑定命中，记录实际voice属性与Listener混音 | `work/f021-hunter-r2-1920` 双端/analyzer PASS；原Explode_03在Hit后33.953ms，混音相关性0.76442、projection gain约0.69628；未做设备loopback |
| 恶魔猎手 | Flow独立Start/Impact按自身时钟推进；绿拱/符文按实际头像与原静态mesh比例贴合；清除重复模板播放 | `work/f021-demon-hunter-r5-1920` 双端及analyzer PASS；source约2.037秒释放，0活跃clone/未处理动作，source后解锁并合法结束回合 |
| 法师回归 | 共用表现修改后复验选择/取消/过期请求、唯一命中与完整受击窗口 | r12的`work/f021-mage-r2-1920` PASS 32/3；追加`work/f021-mage-review-r1-1920` PASS 50/10，analyzer通过、两端唯一Hit、窗口wall约0.5474/0.5523秒。通用报告displayHash仍空，不宣称双方source后解锁 |
| 死亡骑士（保留前轮修复） | 原按钮层和破土召唤保持现有来源路线，共用按钮原画/翻面纳入本轮统一 | r11继续通过36 disabled-route及19 positive-source检查；本轮没有新增一次H11正式Player回放，不冒充重新验收 |

最终`work/f021-all-build-r11.log` 已Build Successful并自然exit 0：H10 113、H02 43+5、H09 82、H03 57、H07 540、Presentation 290、Weapon 122及其它已挂接来源检查通过。r11包含上述三处收尾视觉修正。DLL SHA-256=`9237BCA2CDF55A4AB2B22619D717234D1CCD5D1B879D570C938773A701922ACA`；`Main.unity`仅还原构建生成的fileID，前后SHA-256=`D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`。

随后法师回归r1暴露同一个harness遗留：接收真实`ServerActionRejected`的观察分支仍有Priest-only条件，Mage分支继续读取会被下一state覆盖的status。服务器journal确实拒绝了旧version，source也完成取消，r1仍按FAIL保留。现统一step5 transport观察，保留状态未变/Completed/0clone/pending清理等全部断言；没有修改产品规则、特效或取消逻辑。针对该调整26项Fireblast/Priest检查通过，`work/f021-all-build-r12.log` Build Successful、exit0。当前可执行DLL SHA-256=`DE0AEA31741C5DE9F956142D3F7C313A54722D7D9149C518C95D34BD00CA73FB`；上表Warrior/Rogue/Priest收尾回放明确属于r11，不能混成r12重跑。

Mage默认模式没有timeline，另暴露analyzer局部`frames`未赋值；现每role初始化空列表，实际r2索引生成成功。追加review模式有真实timeline：driver/observer为342/681帧、各唯一Hit，完整窗口0.547407/0.552280秒，wall/batch最大漂移0.004308/0.000938秒，末帧authority/display health均27。driver末帧InputLocked=false但source仍未释放；observer末帧仍在反应窗口且InputLocked=true。这是受击窗口和driver反应后输入切片，不等于双方source后解锁/终态display hash验证。

日常双开入口为项目根目录`Start-LocalDuel.cmd`，自动复用或启动服务器并给两个客户端独立profile/名字。`build/client/HearthstoneClone.exe`仅是客户端，直接打开前需另有`game.server`监听默认`127.0.0.1:37240`。本轮代码/资产实现和本地检查完成；TASK-0006整体仍IN_PROGRESS，保留源快照差异、完整Actor变换/生命周期、同机位官方连续帧/原声A/B与设备音频输出等未闭合项。Rogue/Priest的Listener float PCM有超1样本（独立V记录具体计数），不能声称所有混音无削波。

2026-10-25交付入口补正：用户直接打开exe后匹配提示连接被积极拒绝。实际检查两个exe均无命令行参数、player1保存host=127.0.0.1，默认37240没有监听且没有本项目server进程。已后台补启动loopback服务器，独立重复验证协议hello握手成功，日志`work/server-user-recovery-20261025.out.log`。保留用户窗口，不自动再开双客户端；当前窗口返回卡组选择后重新匹配即可。此前仅提供exe直链的启动说明不完整，已改为上述launcher入口；未改客户端或规则代码。

最新focused pytest为188 passed。此前全量1381 passed/12 failed，经独立复核有一项旧Paladin文本合同已修正，余11项包括缓存覆盖增加和真实Shader/PPtr来源差异；没有刷新快照掩盖失败，分类见 [V021](V021-source-cache-audit.md)。

## 本轮新增可复用经验

- Prefab内定义的Start/Impact模板与Flow真正生成的实例必须区分；同时启动会重复音画。实例渲染必须取自身`PlaybackTime`，父级施法年龄会倒放实例、重建FSM，造成已播完仍锁操作。
- 粒子Renderer的bounds可能包含预估发射轨迹；首帧自动取景不等于按钮实际可见范围。必须对照最终棋盘关键帧和有效像素范围，不能只检查非空RGB。
- 静态mesh原始比例适合固定头像效果的宽高；动画第一帧的变形背景不适合当长期比例依据。H10此前绿拱过窄由此产生。
- 老缓存的`shape.radius`对象必须显式、无损地迁移到运行时标量；本轮仅迁移H02/H01所选root，保留备份，未全目录扫描改动法师等既有效果。
- 纹理alpha全为1不代表应source-over；黑底发光图会盖黑棋盘。必须在非黑背景上做GPU断言，验证黑边不改背景、核心实际增亮；原shader未恢复时记录合成边界。
- 网络拒绝应观察实际transport事件；同版本状态可能立即覆盖临时status文字。取消测试必须从收到拒绝计时并验证Completed、0clone与状态未变。
- 音频文件名和回调不证明命中听感。Hunter逐条SoundDef溯源排除了Build_02误判，再从Listener PCM检出真正Explode_03；仍未做设备loopback或原版原声A/B。
- 回放同一Unity帧可有不同实体的多次采样。验证解锁需绑定目标实体/版本/状态，不能假设frame号全局唯一。

### 收尾关键帧暴露的问题（保留失败）

- H01 r8严格门控失败：JsonUtility把源`particle:null`物化为空DTO，取景错误地纳入未创建ParticleSystem的容器；现在只取`restored`且实际存在的粒子。r9有效像素检查继续失败，亮斑只有14×14，证明只修边界不够。
- r10排除单纯相机模式假设：Editor相机本来就是orthographic=true，实际startSize/currentSize正常，仍仅14×14和43×48。真正错误是把整套9 world-unit爆炸拟合进122px按钮，改变了来源的世界到像素比例。最终H01改为保留源相机、源world位置和原renderer clamp，只捕获owner1子树并在全棋盘坐标合成；不把整个爆炸塞入按钮。额外尝试的通用投影切换已撤回，不扩大共用捕获行为。
- H01源`scaling=2`为Shape模式，只缩放发射形状、不缩放粒子尺寸；固定取景按此语义计算。[Unity API](https://docs.unity3d.com/6000.0/ScriptReference/ParticleSystem.MainModule-scalingMode.html)
- H03旧“未知shader”已定位为导出读错字段：真实名字在`m_ParsedForm.m_Name`而顶层`m_Name`为空。目标CAB与既有audit不同，独立比较全部P4程序/参数SHA、bindings、render states与常量偏移后精确匹配`Hero/Additive/Additive` family16；并非凭相同pathID认定等价。
- H03 v2现在保存两张512²全不透明贴图、源材质及编译核验记录。在空keywords、单位MainTex ST、LightingBlend/GlowIntensity/GlowAddMul均0的严格平面条件下，原`2*vertexRGBA*TintColor`与`SrcAlpha One`可折叠为`4*textureRGB*TintRGB*TintAlpha`，复用既有emission pass；原RGB曲线不改。完整Actor局部transform和整体生命周期仍不在这一平面合成的证明范围。
- 最新focused pytest为188 passed；专项weapon exporter测试3 passed。`test_native_additive`仍保留旧引用快照一项失败，其余原源stage/material绑定检查通过；未刷新全局manifest。

用户要求继续落实已经提供的全部职业参考图；不能只推进H11，也不能把缺少官方连续帧/原声A/B当成已明确的位置、大小、时序问题的实现阻塞。官方等价等级与本地修复证据仍分别记录。

本轮17张用户原版参考图已从指定Temp附件路径逐个校验并原样复制到 `work/f021-reference-inputs/`，按priest-select/target/heal、warlock-cast/end、druid、paladin、rogue、shaman、warrior-button/armor-gain/armor-loss、demon-hunter-cast/end、death-knight、hunter-arrival/impact命名。没有重绘、调色或修改参考图。对照对象是这些用户提供的原始参考，不能把本地运行截图当作原版证据。

## 执行隔离

- 当前脏工作区仅生成临时Git树快照 `d691f85872592a78891572247214b77d9fecd762`，主分支/索引不变，保留既有改动。
- F021-A：`agents/f021-druid-armor`，德鲁伊/战士及通用护甲、输入时序。
- F021-B：`agents/f021-shaman`，萨满按钮/召唤点双层、召唤可见性。
- F021-C：`agents/f021-demon-hunter`，恶魔猎手头像/按钮分层、严格路由启用；同文件小修盗贼按钮黑烟位置。
- 子Agent并行修改独立worktree；共享Unity、导入缓存、Player构建与回放由总控串行执行。DevSpace打开两次均返回Internal error，使用本地工具继续。

## 继续队列

1. 整合三条实现，执行实际GPU/Editor检查及Player回放，失败回传执行者。
2. 盗贼装备白色高亮及攻血交互→耐久→损毁反馈。
3. 牧师选择/命中/实际治疗三阶段、术士按钮紫色效果与自伤不震动、圣骑士英雄头像金光逐项核对。
4. 猎人命中声音已存在本地播放证据，仍需检查箭体和爆炸峰值与用户图的实际差异。

本记录仅登记本轮真实实施与验证；未验证的项目不声明完成。

## 已整合与验证进度

- 共用原版按钮：`Card_Play_HeroPower` / `History_HeroPower` 是共用Actor来源，旧NativePowerVisual硬绑定Mage原画且ClientApp只对Mage启用。现在按当前CardDef绑定11种原画，基础技能统一使用原翻面/复原、高亮、费用与描述。GPU `FireblastVisualChecks.Run` 47 checks通过（含11技能分别核对贴图及Ready/Used像素差），文本检查28通过；`work/f021-common-ui-build-r1.log` Build Success。按构建前快照恢复Main.unity仅生成的fileID变化。
- H10/盗贼源点：整合 `a543c12`、`874a7fd`，root重导H10成功（registry 1357/errors0，production index enabled=true）。启用profile逻辑原来已存在，本次Importer仅新增源层级校验，旧禁播是缓存未重导。root审查又发现world节点已投影到按钮，再平移DrawRect造成二次位移；整合 `15e2862` 改全屏合成、源/目标world锚点及GPU像素断言。
- H02：整合 `b05d6da`、`edbc820`、`5c49e9f`。主按钮按实际按钮bounds绘制，Owner3独立512²图腾FX捕获、真实Actor源绑定与整套UI隐藏交接；Editor 35 runtime + 5 disabled-route检查在r3通过。root审查发现的out参数遗漏已修；初始化顺序调整为Seek后显式保持位置，不把UpdateOriginalPosition误记成必抛初始化异常。
- 护甲/数字：整合 `8818978`、`fbd642a`，通用Hero/Minion Hit复用独立DamageSplat，排除武器耐久；首获护甲闪光，已有护甲只更新徽章；吸收护甲不重复飘数。静态PPtr确认原SoundDef→Armor_Splat_Damage，root已通过presentation_assets正规导入，registry1358/errors0，51项来源导出无error。牧师零治疗不画+0，治疗数字改白色、原黑描边保留。

## 保留的失败与待复验

- routes-r1：Rogue新增加对手席位夹具仍硬编码source=2，修正为动态power/owner后r2整体通过。不是放宽来源合同。
- routes-r3：H10新GPU37、H02runtime35通过，但PresentationChecks仍期待旧平移矩形，root已同步为统一全屏合成并保留真实节点定位检查。
- routes-r4：A新增Editor存在局部config未定义及weaponBefore/After重名，编译失败已退回Feature。H06新button路径另被root发现误读thin parent为完整source，正在改取真实Flow clone并补实际非空渲染检查。尚不能登记本轮综合通过。
- Priest正式双端 `work/f021-priest-native-ui-r1-1280/` 在stale拒绝夹具失败：收到拒绝后cancel实际仅推进1.990974秒，夹具从请求发送起硬等wall2s后立即断言；取消/权威状态证据保留。F021-D正在按实际拒绝及source时钟等待并记录Completed/ActiveClones/Pending，未以无故延时掩盖泄漏。
- Rogue装备位原白蓝光及源RGB动画曲线已找到，F021-C继续实际接线，尚未整合。

## 后续整合与失败修正

- routes-r6 综合通过：H09 82、H03 57、H10 37、H11 36 disabled + 19 source、H02 35 runtime + 5 disabled、Presentation 289；H01/H06来源检查及真正按钮GPU绘制同时通过。H06父级thin root原位置为SourceHero，实际獠牙Impact才是SourceHeroPower；修正的是取用对象，未把父级位置伪改成按钮。
- H02增加固定1.33正交取景：按原quad半边长0.06849、动画峰值scale18.725及3.5%余量推导，输出按实际按钮1.55倍。原TotemQuad初始disabled，必须保留并由FX_TotemFadeIn开启；不能仅绘制尾段MoteBurst。
- 综合r7失败为新增Editor局部变量重名，已修。r8严格H02校验发现导出shape.radius是含value/mode/spread/speed的对象，但NativeShape仍按float读取，实际为0；正在按数据边界修复，未删除门控掩盖。
- H03装备白蓝光接线已整合：原WeaponSummonBlue/Red贴图、四个RGB Hermite关键帧与0.683333秒来源时长；网络Observe仅登记，实际Summon Cue启动声音/光效。修正JsonUtility数组字段和缺省嵌套对象，`work/f021-weapons-r3.log` 114 checks通过。原shader名称未知、当前槽内GUI合成未重建原Actor局部transform，因此不声明完全源空间等价。
- Priest stale夹具已改为分别等待真实拒绝响应及Source取消完成，而不是从请求发送计时；实际取消必须Completed、Pending/ActiveClones为0、状态不变。20项Priest/Paladin/weapon/H10合同检查通过，正式Player待新构建。
- 全量Python结果1381 passed / 12 failed，保留失败并独立分类旧来源快照/缓存范围与真实回归；不得把全量登记为通过。Paladin一项旧SourceBusy文本断言已同步等价的新控制流。
