# TASK-0004 关键帧与 Shader 证据

2026-10-16，执行者 F004。整体结论 **BLOCKED：缺少可归属到既定双卡、具备条件记录的官方序列**。本页的本项目重放与 Shader 数值检查是可独立复核的交付，不是官方视觉通过，也不是独立 V004。TASK-0003 的生产代码和运行语义未修改。

## 官方输入清点

先用 `rg --files` 检查资料入口，再由 `tools.task4_visual_evidence inventory` 对明确关联的 `E:\HS` 做有界媒体清点。包含仓库全部截图/工作目录、`E:\HS\references` 和 AssetStudio 目录；不搜索用户其他目录。排除 `.git/.venv/Library/Temp/Obj/node_modules`、原素材 `local/runtime/local/cache`、StreamingAssets 和 Player 构建目录，具体绝对路径见清单。这些排除目录是导入资产/依赖，不是官方截图来源。

清单 [task-0004-reference-inventory.json](../../manifests/task-0004-reference-inventory.json) 保存命中的路径、SHA256、长度、尺寸和排除范围。实际核对 `oracle-inventory.png` 的全部 18 张图：菜单 3 张、棋盘 UI 4 张、交互 7 张、节奏相关武器/技能/牌库 4 张；没有既定双卡的效果序列。`references` 的 17 张图是资源工具界面/图标和规则仓库 logo。第一次采集前另有 1386 张本项目生成图；后续清点数量包含本任务新图，不能视为新增官方资料。所搜范围未发现视频或未分类媒体。

背景“刺骨 .30 秒白色中心”已纠正：实际存在 `tests/visual/current/native-effects/EX1_134-0.30.png`，本轮读取可见白色中心；相邻 `render-report.json` 为 `offscreen-native-prefab`，该卡报告程序 SHA256 为 `3a9f994500259b54eae6c4a4154938f5987aed93ed5d803628d37defc550e8bb`。PNG 与报告身份均入清单。它是旧摊平图诊断，不能代表当前自主根，也不是官方帧。EX1_134 是军情七处特工；EX1_124 才是刺骨，不能移植该证据。

原安装仅只读用于 Shader/材质/纹理源重读，路径由现有 Importer 指定 `D:\Program Files (x86)\Hearthstone`。没有操纵原客户端、执行原 DLL 或 Computer Use。原产品版本无确实证据，仍为 Unknown；UnityPy 的 `6000.3.11f1` fallback 警告不是产品版本证据。

## 本项目可重放序列

运行：

```powershell
# E:\HS\HearthstoneClone，独占 UnityClient/local/build
.\tools\Invoke-Task4VisualEvidence.ps1
.\.venv\Scripts\python.exe -X utf8 -m pytest tests/test_task4_visual_evidence.py tests/test_particle_alpha_shader.py -q
```

脚本依次生成真实规则输入、重读 Shader 来源、隐藏启动并串行等待两个图形批处理 Unity。先读取进程 Handle，保存 PID/UTC/真实 ExitCode 到 `work/task4/process-a.json`、`process-b.json`。GPU 不能加 `-nographics`。默认 Unity 为 `F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe`，可用 `-Unity` 覆盖。

输入由既有 `fixture`（规则种子42）和 `transition/Match.apply` 产生，初始化测试棋盘后实际执行合法出牌。EX1_129 对敌方两随从，其中包含真实受伤触发/护甲；EX1_134 先合法打出幸运币，再连击命中敌方英雄。固定 CardID/原 Prefab，均保存 seat0/seat1 的 before/after、公开哈希和完整事件，不伪造原历史或 metadata。生产 `NativeEffectPlayer.PrepareSource → NativeSuperSpellBoard → NativeSuperSpellPlayback` 实际运行。

输出根目录 `E:\HS\HearthstoneClone\work\task4`：

- `scenes/`：8 个规则输入 JSON。
- `capture-a/`、`capture-b/`：各 `capture.json`、28 张事件主帧、56 张分离 C/K 图和7张 Shader 示意 PNG。
- `capture-a/EX1_129-seat0-contact.png` 等4张 contact sheet，明确标 PROJECT ONLY。
- `oracle-inventory.png`：已有官方 UI 图总览。
- `gpu-a.log`、`gpu-b.log`、`replay-final.log`：本轮日志，进程身份另存 JSON。

机器入口 [task-0004-visual-evidence.json](../../manifests/task-0004-visual-evidence.json) 保存输入/源码/相关缓存和清单/音源/图像哈希、相机实际字段、GPU/API、每帧遥测及比较状态。规则输入全部本地重建；原资产和 PNG 不提交。新工具源码在首次采集时未提交，源码哈希是执行身份；`git_base` 是工作树基点，不声称基点已包含新采集器。后续固定实现提交见 Task/交接。

相机：1024×576，正交 size16，local `(0,100,0)`，向下、屏幕上方为世界 +Z，near .1/far200，culling layer30；生产 GUI 投影以1920×1080计算。实际 Unity2022.3.60f1c1、RTX4080 Laptop、D3D11、Gamma，记录自当前运行。时钟显式1/240秒递增，事件定位误差至多一采样步；记录绝对秒数和 step，不强制归一化节奏。根随机为 `System.Random(117)` 均匀抽样，粒子117+node，音源/随机等待/Timeline117；这些是本项目复现种子，不声称等于原客户端随机流。

主帧实际调用生产 `RenderAffine`，按 `C+D*K` 合到固定 `D=RGBA(.18,.18,.18,1)` 背景；另存 C/K 的8位 PNG。无棋盘/Actor整体合成、Glow或后处理，不是完整 GUI。纹理源仍是原缓存；未知原场景条件不会从这些图反推。Shader 的 HDR/负值以浮点读取数值为准，PNG裁切显示不能证明浮点范围。

阶段按真实事件采集：Birth=首次 MissileBirth（包括发射前等待，可能不可见），Early/Mid=首枚投射物绝对路径时长的10%/50%，Arrival=首个到达回调，Hit=实际 ImpactState5，Death=MissileDeath 激活；另存 Cleanup。Death 可能与 Arrival 同一步，且先于延迟 Hit，保留真实事件顺序，不人为移开截图。旧采集器以 MissileNone 命名 Death 的做法不是本轮定义。

当前两视角时间一致；精确值见 JSON：

| 卡 | Birth | Early | Mid | Arrival / Death | Hit | Cleanup |
| --- | --- | --- | --- | --- | --- | --- |
| EX1_129 | .004167 | .016667 | .050000 | .091667 | .129167 | 3.500000 |
| EX1_134 | .004167 | .466667 | .629167 | .829167 | .833333 | 3.833333 |

本轮两个新进程28/28主图字节及阶段遥测相同，全部根完成且拥有的图对象/相机输出/C/K纹理释放。固定灰合成图中刀扇可见小型绿色刀刃，军情七处特工主要为很暗的小型效果；不能以重复一致消除这一外观限制。未进行官方偏差计算。

## 分项结果和解除条件

官方 `position/rotation/timing/color/alpha/particles/trail/audio` 八项均 `grade=Unknown/difference=Unknown`，Overall Unknown，`wholeEffectVerified=false`、不升级 V5/V6。现有 Overall V2 provisional 保持。JSON 只保存本项目位置/旋转/数量/Trail时间与点数，以及音源请求路径、时间/音量/音高；没有原版对应数据，也没有音频波形/实际混音听感。像素均值/bbox/Alpha范围是单侧诊断量，不是准确率。

[task-0004-official-input.json](../../manifests/task-0004-official-input.json) 是缺失输入契约。最小补充：两张固定卡、两种视角的同条件官方原始连续序列或带时标帧，媒体 SHA/来源/产品版本，分辨率、机位/投影、棋盘及 source/targets/连击状态、开始事件、绝对时间、采样时钟/倍速/掉帧、随机信息、色彩与后处理、声音环境。无法取到的字段保持 Unknown，并说明如何以锚点/遮罩/容差取得可比性；不能自行填常见值。收到完整字段也只进入“需要来源与可比性复核”，不自动通过。

继续入口：将资料放入本机忽略的 `tests/visual/reference/oracle/task4/`，填写上述契约，再运行 `assemble --official <manifest>` 复核缺失字段。当前工具只做输入清点/索引/缺口报告，尚未实现或执行有合格官方输入的跨来源差异计算；即使字段齐全，也必须按[标准工作流](VISUAL_REVERSE_ENGINEERING_WORKFLOW.md)验证来源、建立匹配/度量并由独立 V004 复核，不把字段齐全当验收成功。

## Shader 案例：Custom/FX/FX_SneakySmoke

[task-0004-shader-audit.json](../../manifests/task-0004-shader-audit.json) 完整连接两份只读 bundle SHA、Shader CAB/PathID、两个材质的原属性/ST/纹理指针、变体/常量布局/采样器、原状态、DXBC/参数/反汇编缓存 SHA 与本轮 GPU 数值。

- 【直接恢复 / Confirmed】Shader `CAB-c394926ba464822396a05cbfae08a8b3/-1607209025186432915`；Pass0/无关键词，D3D11 VS blob1 / PS blob3。VS程序 SHA `6c33f16b6a15c51061110a36dc11d1a5edea32756009f31743fdcd5a96142f2b`；PS `3d90e10bb86c7a5e2a2b606ab92b0b1b9960a463dbe81b8a2791aa09e06a328f`。编译参数独立有哈希。不是开发期作者源码。
- 【直接恢复 / Confirmed】原 SubShader tags 为 QUEUE Transparent、IGNOREPROJECTOR true、UniversalPipeline。材质 custom queue、完整 Stencil 状态原样记录；Cull0=Off，ZWrite0，ZTest4=LEqual，RGBA SrcAlpha5/OneMinusSrcAlpha10，BlendOp0=Add，ColorMask15。未把原 URP 全局环境等同于本项目 Built-in。
- 【行为重建 / Inferred→局部Observed】VS使用 MainTex ST，`o1.w=vertex.a*Color.a`；PS `sample_b`读取t0/s0，`mul r1.w,r1.x,v1.w` 形成 `mask`，`mad`做不截断 RGB插值，末尾统一乘Intensity。公式为 `mask=MainTex.r*vertexAlpha*Color.a`，`RGB=(Color.rgb+mask*(SecondColor.rgb-Color.rgb))*Intensity`，`A=mask*Intensity`。不读纹理GBA、顶点RGB、SecondColor.a或时钟；没有 saturate/alpha clip/discard。实际 family24/材质接线复用原实现，无 Shader 业务改动。
- 【Observed】新增7个16×16 ARGBFloat/Linear真实GPU样本覆盖mask、忽略字段/时钟、零alpha、HDR不截断、RGBA原混合、零强度、负mask外推。原混合样本输出约 `(.184,.328,.472,.36)`，alpha也是 `As*As+Ad*(1-As)`。最大通道误差 `5.960464477539063e-8`，阈值.003；不是与CPU实现自比的截图相似分。额外来源测试4项重读原程序/材质与8原纹理raw/mip/sampler，3个工具契约测试通过。
- 【Unknown】原场景动态输入、捕获与后处理、全部材质/Shader及整卡官方外观。此局部案例尚待独立 V004，不能标整卡V4/V5或作者源码已恢复。未读的其他变体不能由本分支外推。

验收范围：本轮7 Python通过/3条 UnityVersionFallbackWarning；两次独立Unity过程真实exit0。Unity日志均有既有 Licensing access-token unavailable 信息，未导致退出失败。未重跑无变化的1187常规、五轮原停止探针、新Player构建或正式整局；未进行官方逐帧/音频/人工视觉确认。TASK-0003原独立V003-r6仍是其生产语义基线。
