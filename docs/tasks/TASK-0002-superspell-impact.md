# TASK-0002

2026-10-16 卡名勘误：本任务既定双卡ID为EX1_129刀扇和EX1_134军情七处特工。历史“EX1_134 刺骨”是名称误标；刺骨ID为EX1_124。本任务已通过的原配置/Prefab/运行证据均仍对应原指定ID，不因此改变DONE状态或迁移证据。核对来源见PROJECT_STATE。

## 任务名称
SuperSpell 命中分派与生命周期契约。

## 背景
根投射物/命中缺口阻碍完整双卡编排。暂停时已有 NativeSuperImpact 草稿，应验证并完善，而非丢弃重写或直接宣称成功。

## 启动时已知事实
- Confirmed：original-script-audit.json 静态记录 SuperSpell/<WaitAndSpawnImpact>d__89 及源程序集 hash，原 IL 缓存位于 local/script-audit；未执行原 DLL。
- Confirmed：NativeSuperImpact 草稿表达 spawn wait / offset / metadata gate / 异步 CompleteFrom / 目标检查 / 克隆定位与回收服务，但未编译验收；服务契约尚不完整。
- Confirmed：NativeSuperArea.RegisterClone、NativeAreaInstance、NativeMissileFlight 已存在可复用边界；根双卡正式比赛仍未接通。
- Unknown：草稿所有边界是否与原完整控制器一致；不能仅凭草稿作为原版事实。

## 目标
将命中子系统做成可执行、来源明确、失败可诊断的端点，覆盖两张样板卡真实配置和克隆完成/资源释放；记录其与 metadata 宿主的严格契约，为 TASK-0003 提供接入条件。

## 非目标
不完成全部根 SuperSpell；不扩展其他卡；不恢复完整原 GameState/ZoneMgr；不伪造原 PowerTaskList；不做 Bridge 或无证据 Shader/动画替代。

## 相关文件
UnityClient/Assets/Scripts/NativeSuperImpact.cs、NativeSuperArea.cs、NativeAreaInstance.cs、NativeMissileFlight.cs、NativePowerExecution.cs；manifests/original-script-audit.json、original-super-missile-config.json；docs/ORIGINAL_MISSILE_PATH.md、ORIGINAL_POWER_EXECUTION.md、ORIGINAL_SUPER_AREA.md；visual/VISUAL_REVERSE_ENGINEERING.md；hearth_visual_db/effects.json。

## 实施约束
遵守 AGENTS/DEC-001~004；静态核实真实迭代器编号与 IL。源 wait 与 game wait 分开；Server 不依赖动画。真实原历史缺失时拒绝或暴露未绑定服务，不以 SemanticEvent 冒充原 metadata。修改前核实 checkpoint 与当前代码，不改变 UI 校准。

## 验收标准
1. 对照 IL 逐项写明 Spawn/Pending、随机采样、0 秒仍 yield、offset、CompleteUntil/From 先后关系、目标失效、damage range 选择、location/rotation、激活与完成责任。
2. 有意义的边界验证：重叠/空隙/越界范围与 null prefab、失效目标、同步/异步完成、缺失依赖、异常/取消、重复完成与资源释放；不只镜像实现。
3. 两个样板真实原配置通过来源重读，实际克隆至少完成命中生命周期 GPU/回调验证；明确局部与整卡差别。
4. 编译与适当规则/表现回归通过；保存命令/退出码/版本/证据；依赖不足写 BLOCKED 和原因，不造占位完成。
5. 更新 Task/数据库/专题，中文提交，READY_FOR_REVIEW；V002 独立复验后才 VERIFIED。

## 当前状态
DONE（2026-10-16 V002-r2 独立固定 `752f909543afd0ffc5904b2825b8ed0f6d6fa804` 验收通过，报告提交 `213acba8e35964d622c454aad28bdb276960694c`；总控据报告登记 VERIFIED→DONE）。串行沿用 master / E:\HS\HearthstoneClone；首次实施起点 c2686fa，启动时工作区干净、只有本工作树，草稿与 da081b6 一致。未开并行写线程，未推送。

2026-10-16 新编排指令已替代此前“同聊天补查后等待”约束：首轮独立上下文 V002 固定验收提交 `6838ddfe88b4a036faf099b6583a8dc9f13355d1`，失败后派 F002B 修复；最新实现提交及执行者证据见下文。新回合验证通过才由总控登记 VERIFIED→DONE 并自动派发 F003。当前总控不承担 V002；Agent/回合/验收报告见 AUTOMATION_RUN，规则见 DEC-005/WORKFLOW。

## 实施记录
1. 原 IL 重读确认 d__89、Spawn/HasImpact、范围选择、CloneSpell、SpellManager 和 metadata d__126/129/128；程序集 SHA256 为 `3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728`。原客户端产品版本 Unknown；只静态读取，未执行原 DLL。逐项行为/偏移与严格服务契约见 [ORIGINAL_SUPER_IMPACT](../ORIGINAL_SUPER_IMPACT.md)。
2. 保留草稿主流程，将 WaitAndSpawn 私有化，增加调度前验证、每请求计数票据、受管嵌套协程、同步/异步故障诊断、取消、重复 finished/None 防护、释放失败保留/重试。保留 spawn/game 独立采样与等待、0 秒 yield、offset、Until→异步 From→目标检查、位置/旋转/激活顺序。无 ranges 不调用 DamageInfo。
3. 新增 `NativeImpactFactory`、双卡来源恢复工具与 manifest，复用 NativeAreaInstance/RegisterClone 和现有 Importer/Registry。原根图、服务器规则/网络语义、原安装和 UI 校准未改。
4. 真实 GPU 检查发现原克隆起点被主动 None FSM 关闭。核实 Spell.Awake 不发送 None、GetSpell(Spell) 直接 Instantiate、两源 FSM start 均空后，为命中工厂增加显式 startAtFsmEntry；既有 area/missile 默认模式保留。没有删源动作、延长时间或添加替代视觉。
5. metadata 只保留严格显式服务：没有映射时宿主明确返回 -1；有映射缺 Until/From 等依赖即失败。未把 SemanticEvent 变成原历史。真实克隆 GPU 场景明确无 metadata 映射；metadata 边界用受控调度检查，不伪称原完整 GameState/ZoneMgr。
6. 已更新专题、视觉数据库、PROJECT_STATE/KNOWN_ISSUES/INDEX。验收发现旧构建命令 -nographics 与现有 Camera.Render 前置检查冲突；修正 AGENTS/BUILD 复现命令。构建生成的 Main.unity 随机 fileID 噪声已仅撤销本轮改动，语义不变。

## 测试结果
以下为首次交接（00b80f2）的执行者检查；后续补查另列，不混用版本。Unity 2022.3.60f1c1 / revision 469b28c2de88，GPU 不加 -nographics，后台隐藏进程，跟踪自建 PID/退出码；未使用 Computer Use。

| 检查 | 结果与本地证据 |
| --- | --- |
| `tools/export_script_il.ps1 -ExtraTypes`（SpellManager、d__89、d__126/129/128、SpellUtils） | 138 types，退出 0；source hash/缓存索引在 original-script-audit.json。 |
| `python -m tools.super_impact_source` | 两根/两独立命中 Prefab，退出 0；work/task-0002-source-export.log、manifests/original-super-impact.json。 |
| 新来源检查 + 既有投射物来源检查（独立 Python 进程） | 4 passed / 4 warnings / 55.68s，退出 0；work/task-0002-source-final.log。包括 232 根投射物配置重读、两个命中原根类型树及 Prefab/FSM/音频身份。 |
| 规则/表现常规回归 | 1187 passed / 4 deselected / 33 warnings / 820.44s，退出 0；work/task-0002-regression.log。与前行分进程，不写单次 1191 全量通过。 |
| `NativeSuperImpactChecks.RunRegression` | 154 命中检查、既有 27 路径及音源/相机/目标/攻击/78194 效果运行时等检查链，标准 GPU 41 项 / 146 帧通过，退出 0；work/task-0002-final-gpu.log。 |
| `NativeSuperImpactChecks.Run`（最终释放断言） | 154 项通过，退出 0；work/task-0002-final-impact-resources.log。另直接检查克隆/隔离根/相机、RenderTexture 与原图临时材质均实际销毁；此轮仅增强 Editor 断言，Player 业务代码与上次成功构建相同。 |
| `NativeSuperActionChecks.Run` | 51 SuperAction、32 SuperArea、61 PowerExecution 及区域/定位等既有链通过，退出 0；work/task-0002-related-gpu.log。覆盖 NativeAreaInstance 默认模式回归。 |
| `BuildClient.Build`（图形 batchmode） | Build Finished, Result: Success，退出 0；work/task-0002-build-gpu.log。Player EXE SHA256：47C6D9B4B0B26E0DD7CA8BD02A49859A8C338CE8FE8F1FCAC00743AECD5D1CE7。 |
| UI 校准 | SHA256 仍为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。 |

Python 复现命令（仓库根目录）：

```powershell
.\tools\export_script_il.ps1 -ExtraTypes @('SpellManager','SuperSpell/<CompleteTasksUntilMetaData>d__126','SuperSpell/<CompleteTasksFromMetaData>d__129','SuperSpell/<WaitForTasks>d__128','SuperSpell/<WaitAndSpawnImpact>d__89','SpellUtils')
.\.venv\Scripts\python.exe -X utf8 -m tools.super_impact_source
.\.venv\Scripts\python.exe -X utf8 -m pytest tests/test_super_impact_source.py tests/test_super_missile_source.py -q
.\.venv\Scripts\python.exe -X utf8 -m pytest tests -q --ignore=tests/test_super_missile_source.py --ignore=tests/test_super_impact_source.py -k 'not test_original_spell_transform_catalog_matches_all_actual_sources and not test_area_configuration_and_prefab_identity_replay_original_components and not test_start_configuration_replays_actual_original_components and not test_rgba_parameter_refresh_matches_original_fields_and_none_flags'
```

Unity 复现：使用 AGENTS 的隐藏 Start-Process 方式，参数 `-batchmode -quit -projectPath E:\HS\HearthstoneClone\UnityClient -executeMethod <入口> -logFile <日志绝对路径>`；入口/日志按上表逐个运行，不并发占用同一 Unity 项目。Build 也保留图形设备，不能加 -nographics。

四个 deselected 是沿用隔离策略的既有大型定位/区域/启动/RGBA 源重读，本轮未跑，不隐去覆盖限制。Python warnings 为现有 UnityPy 版本 fallback；Unity 仍有既有递归字段序列化深度提示。

命中检查覆盖：重叠首选、闭区间/空隙/上下界/极值并列/null prefab；缺失 DamageInfo 服务与真实 null 区别；初始/等待后目标失效；精确等待/offset/随机次序；同步/异步 Until/From、From 在视觉结束后故障；缺失/无效调度器；嵌套异常、取消/Stop/Dispose、重复完成与释放重试；同步 Activate 完成；多个实际独立克隆取消与物理资源回收。

GPU 证据 `tests/visual/current/native-super-impact/`：固定 512×512 隔离相机、白 ambient、固定最小 delay/采样时刻；EX1_129/EX1_134 各 8 帧及 lifecycle JSON。最大可见像素分别 168/140；各 finishes=1、pending=0、released=true、whole_effect_verified=false。图像仅证明局部可见性和命中生命周期，不证明官方画面/整卡效果或人工听音。本轮没有新正式对局接入或官方逐帧验收。

失败记录：首轮新增检查脚本 CS0819 已修正；GPU-2 缺真实 Facing=6 宿主，GPU-3/4 为初始化 None 关闭根导致黑帧，GPU-5 通过后最终 RunRegression 再通过。旧 -nographics Build 在既有 NativeEffectChecks.Camera.Render 处 SIGSEGV / 退出 1（work/task-0002-build.log），去掉该参数后成功。不以失败/中间版本结果为最终验收背书。

## 同聊天补查（2026-10-15～16）

用户选择“在当前聊天补查 TASK-0002，保持 READY_FOR_REVIEW”；本轮不创建 V002 聊天，不做独立验收，不推进 TASK-0003。

Observed：`NativeSuperImpactChecks.RunSupplemental` 在修复前复现六项失败（work/task-0002-supplemental-before.log，退出 1）：诊断回调抛错中断多请求取消；Dispose 通知期间仍能入队；GetSpell 在 Dispose 期间返回后仍被激活；Source/克隆设置中取消后继续后续写入；回收回调重入导致重复 Release。另复现取消后回收错误被已停止分支吞掉（work/task-0002-supplemental-release-before.log，退出 1）。不把首次 154 项通过当成这些边界的既有证据。

修复限定 NativeSuperImpact：隔离诊断观察者错误并提供 DiagnosticFailures/LastDiagnosticError；Dispose 先置关闭标志；回收增加进行中标志；在外部服务返回后阻止已取消请求继续设置/激活；晚返回克隆立即交释放服务；已取消请求的后续回收异常仍可诊断/重试。原等待/metadata/位置/激活顺序在未取消路径保持不变，未改规则、原资源或工厂协议。

- `NativeSuperImpactChecks.RunRegression`：172 命中检查、既有路径/表现检查链及标准 GPU 41 项/146 帧通过，退出 0；work/task-0002-supplemental-final-gpu.log。含双卡真实工厂晚返回克隆没有 Source/Target/Activate、隔离根/相机/RT 实际销毁的新增检查。
- `python -m pytest tests/test_match.py tests/test_presentation_metadata.py -q`：24 passed / 8.63s，退出 0；work/task-0002-supplemental-python.log。使用仓库 .venv Python，保留 -X utf8。
- `BuildClient.Build`：Windows Build Finished, Result: Success，退出 0；work/task-0002-supplemental-build.log。自主 Player 业务程序集 `build/client/HearthstoneClone_Data/Managed/Assembly-CSharp.dll` SHA256 为 `682169A7EAB9EA2FF6AF05D5A662F492CC3614E9F91285AC8C31D483EFD93F26`；EXE 为 Unity 启动壳，其 hash 与首次构建相同，不能单独表示业务代码版本。
- 两卡可见像素仍 168/140、finished 各一次、pending=0、released=true；UI 校准 hash 仍 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。构建的 Main.unity 随机 fileID 噪声已仅恢复本轮生成改动。
- 先前 1187 项及四项来源检查是首次交接证据；Python/原资源未改，本轮没有再次全量重读，不冒充新一轮全量通过。新增故意抛错的诊断观察者产生两条预期警告，由 DiagnosticFailures 独立记录。

## 独立验收 V002-r1（2026-10-16）

固定业务目标 `6838ddfe88b4a036faf099b6583a8dc9f13355d1`；独立 Agent `/root/v002_r1` 未继承实施推理。开始 HEAD 132ee1b，较目标只有编排文档差异，工作区干净。完整独立证据、命令与范围见 [V002-r1 报告](../verification/V002-r1.md)。

**FAIL → IN_PROGRESS**：取消时 Release 第一次抛错并保留克隆，随后晚到 None 回调的第二次 Release 抛新错，被 `Fail` 的 stopped 早退吞掉。独立探针两次新 Unity 进程（16952、11440）均退出 1：attempts=2、errorsBefore=2、errorsAfter=2、LastError 仍 OperationCanceledException；第三次显式 CancelAll 可清理、pending=0、cancelled=1、finishes=0。因此限定为诊断丢失，不声称永久泄漏。失败验收标准 2 及“失败可诊断”目标；复现材料 `tools/verification/v002_r1_late_none.cs`、`work/v002-r1-late-none*.log`。未改生产实现，修复交给 Feature Agent，随后由全新 V002 回合验证。

本轮已独立完成：8 类型 Cecil 原 IL 逐行重读两次通过；4 项双卡/投射物来源测试通过（50.66s）；命中 172、路径 27、标准 GPU 41 项/146 帧及既有链通过；SuperAction 51 / SuperArea 32 / PowerExecution 61 与既有链通过；Windows 构建 Success；Python 常规回归 1187 passed / 4 deselected / 33 warnings / 651.84s，均退出 0。来源测试为独立进程，四项既有大型来源检查未运行，不写单次 1191 项全量通过。真实双卡最大像素 168/140，finish 各 1、pending=0、实际 GameObject/隔离根/相机/RT/临时材质释放；晚返回克隆未设置/激活并已释放。新证据日志均为 `work/v002-r1-*`，不能以这些正常结果覆盖独立失败。

未新增整卡/官方视觉/真实 metadata 宿主认证，数据库独立验收标记保持 false。UI 校准 hash 保持原值，自建 Unity 均退出，临时 Editor 入口及本轮构建噪声已清理；原安装只读，没有 Computer Use、推送或发布。

解除条件：补齐已停止请求的晚到 None 回收错误诊断，增加该最小序列回归，保持计数/完成/回收幂等和显式重试；新独立验收通过后才可 VERIFIED。报告/探针/Task 的中文验收提交用 `git log -1 --oneline -- docs/verification/V002-r1.md` 定位。

## F002B：独立失败 F1 修复（2026-10-16）

执行者为原生独立 Agent `/root/f002b`，仅接收 Task、失败材料和范围约束，未继承之前实施或验收推理。依序读取仓库规程、状态、Task、V002-r1、命中/区域/metadata/路径专题并核对代码。开始 master / HEAD `82ba8e6`，工作区干净、仅一个工作树；相对原业务 `6838ddf` 只有编排/验收材料差异。没有改 V002-r1 报告、tools 下独立探针或验收标准。

Observed：先原样复制独立探针至临时 Editor 入口，SHA256 均为 `7578B30228B223C301046BD29497AAF55E8B62C9DBD2E8ABFDEFAA09D4BC6E87`。未修业务的新进程 PID 20644 / 退出 1，`work/f002b-late-none-before.log:272`：attempts=2、errors=2→2、LastError=OperationCanceledException、retained=1；第三次显式重试成功。另先增加回归再运行旧业务，PID 32896 / 退出 1，`work/f002b-regression-before.log:306` 分别复现 cancel/dispose/fault 停止后晚到 None 的诊断失败。

最小业务修复仅修改 `NativeSuperImpact.Fail` 的 stopped 分支：仍调用 Report，再返回；不重复 Settle、增加 Faulted/Cancelled、关闭协程或主动重入 Release。由此晚到 None 的新回收错误进入 Error/LastError，同时保留失败克隆供显式重试。既有释放进行中保护、原等待/metadata/定位/激活顺序不变。新增回归覆盖取消、Dispose、故障停止后的同一序列；第二次 Release 中重入 CancelAll/None、诊断观察者抛错以及成功重试后的重复回调均有断言。

本轮 Unity 为 `2022.3.60f1c1 (469b28c2de88)`，所有进程隐藏启动并读取 Process.Handle 后 WaitForExit，保留图形设备，不加 `-nographics`，串行独占 Unity/local/build。

| 检查 | 本轮结果 / PID / 退出码 / 证据 |
| --- | --- |
| 原独立 `V002R1LateNoneProbe.Run` 修复后 | PASS / 25200 / 0；`work/f002b-late-none-after.log:274`。errors=2→3，LastError=release-attempt-2；retained=1，随后显式第三次重试成功，pending=0、cancelled=1、finishes=0。 |
| `NativeSuperImpactChecks.Run` | 205 项（原 172 + 本轮 33）/ 8000 / 0；`work/f002b-impact-lifecycle.log:560`。覆盖全部命中边界、新增三类停止路径、真实双卡生命周期与物理资源销毁。 |
| `pytest tests/test_match.py tests/test_presentation_metadata.py -q` | 24 passed / 11.54s / 0；`work/f002b-python-targeted.log`。 |
| `BuildClient.Build` | Windows Build Finished, Result: Success / 4992 / 0；`work/f002b-build.log:3262`。包含既有表现、交互、校准、NativeEffect 前置检查与 Mixer 构建。 |

自主 Player 业务程序集 `build/client/HearthstoneClone_Data/Managed/Assembly-CSharp.dll` SHA256：`01198BBA172359FC228168464E18B16281B6C6C89D91288359906C264C1252F8`。双卡峰值像素仍 168/140，各 finishes=1、pending=0、released=true，检查直接断言 GameObject、隔离根/相机/RT/临时材质已销毁，也覆盖多克隆取消及工厂晚返回未设置/激活。故意抛错的诊断观察者共三条预期警告，含新增晚到诊断隔离；不作为运行故障。

可重复命令（显式仓库工作目录）：

```powershell
& .\.venv\Scripts\python.exe -X utf8 -m pytest tests/test_match.py tests/test_presentation_metadata.py -q *> work/f002b-python-targeted.log
$f002bUnityProcess = Start-Process -FilePath 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe' -ArgumentList @('-batchmode','-quit','-projectPath','E:\HS\HearthstoneClone\UnityClient','-executeMethod','NativeSuperImpactChecks.Run','-logFile','E:\HS\HearthstoneClone\work\f002b-impact-lifecycle.log') -WorkingDirectory 'E:\HS\HearthstoneClone' -WindowStyle Hidden -PassThru
$null = $f002bUnityProcess.Handle
Write-Output ('PID=' + $f002bUnityProcess.Id)
$f002bUnityProcess.WaitForExit()
Write-Output ('EXIT=' + $f002bUnityProcess.ExitCode)
```

原探针复验按 V002-r1 的临时复制步骤，入口改为 `V002R1LateNoneProbe.Run`，日志 `work/f002b-late-none-after.log`；构建入口为 `BuildClient.Build`、日志 `work/f002b-build.log`。本轮仅改自主诊断分支，未重跑 V002-r1 刚完成的 1187 项常规回归、4 项来源/8 类型 IL 或标准 GPU 41 项/146 帧，不将旧证据冒充本轮结果；本轮真实命中 GPU/资源与针对性规则检查已重新执行。没有新增正式比赛、官方逐帧或完整 metadata 宿主认证。

所有自建 Unity 已退出；临时 Editor 探针和同名 meta 已清理，tools 原探针 hash 未变。构建仅生成 Main.unity 随机 fileID 差异，逐项核对后用 apply_patch 恢复本轮噪声；mixer audit 无差异。UI 校准 SHA256 保持 `E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`。原安装只读，未执行原 DLL，没有 Computer Use、抢焦点、推送或发布。

F002B 执行者检查解除已观察的 F1；独立验收尚未进行。状态仅 READY_FOR_REVIEW，交由总控派全新 V002 固定下述修复提交。

## 独立验收 V002-r2（2026-10-16）

**PASS → VERIFIED**。独立 Agent `/root/v002_r2` 空历史上下文启动，固定业务目标 `752f909543afd0ffc5904b2825b8ed0f6d6fa804`，开始 master / HEAD `eb56e42`、工作区干净；目标之后仅文档/编排差异。重新读取原 IL、Task/专题、运行代码和测试，未把 F002B 的执行者日志当独立通过。完整命令、证据和范围见 [V002-r2 报告](../verification/V002-r2.md)。

- 原 `v002_r1_late_none.cs` 原样复制，SHA256 仍 `7578B30228B223C301046BD29497AAF55E8B62C9DBD2E8ABFDEFAA09D4BC6E87`；新 Unity PID 36008 / 退出 0。errors=2→3、LastError=release-attempt-2，克隆保留后第三次显式重试成功，pending=0、cancelled=1、finishes=0。前轮失败材料保持原样。
- 自主新增 `tools/verification/v002_r2_stopped.cs`，19 场景通过，PID 28452 / 退出 0：三种首次停止、诊断内显式重试/观察者抛错、finished/None 双顺序及晚到错误观察者同步取消另一个活动请求；target/stage/异常身份正确，无重复 pending/停止计数/完成/回收。
- 本轮实际重跑 8 类型 Cecil 静态 IL 匹配；4 项来源测试 passed / 119.73s；`NativeSuperImpactChecks.RunRegression` 为 205 命中、27 路径、既有表现链及标准 GPU 41 项/146 帧，PID 11352 / 退出 0；针对性 Python 24 passed / 13.25s；Windows 构建 Success，PID 21816 / 退出 0。日志均为 `work/v002-r2-*`。
- 双卡真实命中 GPU 各 8 帧，独立像素读取峰值 168/140，各 finishes=1、pending=0、released=true；运行断言确认 GameObject、隔离根/相机、RT、临时材质物理销毁，并覆盖双克隆取消及工厂晚返回未设置/激活后释放。证据另存 `work/v002-r2-native-super-impact/`，只代表局部生命周期。
- 独立构建 Player 业务 DLL SHA256 为 `01198BBA172359FC228168464E18B16281B6C6C89D91288359906C264C1252F8`。Unity 2022.3.60f1c1，四个隐藏图形进程均先读 Handle 再 WaitForExit，退出均 0。UI 校准 hash 不变；临时入口/meta 与本轮场景 fileID 噪声已清理，生产代码未改，未推送。
- 常规 Python 1187/4 deselected 与单独 SuperAction 51 / SuperArea 32 / PowerExecution 61 使用 V002-r1 刚完成的独立未变基线，未冒称本轮重跑；四项大型定位/区域/启动/RGBA 来源重读仍未跑。范围选择理由见报告。

验收标准逐项满足于局部端点/严格显式宿主契约范围。未认证完整根调度、真实 metadata 历史宿主、正式比赛整卡、官方逐帧或人工视觉/听音；数据库整体视觉级别不提升。总控负责将 VERIFIED 登记 DONE 及后续状态/数据库索引，不由验收者接着实施 TASK-0003。

## 最终结果
本 Task 的局部命中端点、来源、工厂和宿主契约通过 V002-r2 独立验收，F002B 修复已解除 V002-r1 的 F1。总控已登记 DONE，并自动转入 TASK-0003。仅 controller.impact_independently_verified 升为 true；根整卡/官方视觉与 metadata 宿主未验收，full_root_integration/metadata_host_bound 和整体 independently_verified 仍 false，整体视觉等级不变。

## Commit
历史端点起点 0329198；草稿 checkpoint da081b6；首次实现 `00b80f2`，首次交接 `35113ad`。同聊天补查包含后续业务代码修复，V002 应固定最新补查实现，而非仅复验 00b80f2。用 `git log -1 --oneline -- UnityClient/Assets/Scripts/NativeSuperImpact.cs` 获取当前补查实现提交，用 `git log --oneline -- docs/tasks/TASK-0002-superspell-impact.md` 查看证据/交接历史；不填写自引用哈希。

F002B 实现与新增回归：`752f909543afd0ffc5904b2825b8ed0f6d6fa804`（修复：上报命中取消后晚到None的回收异常）。本节文档提交位于其后，新独立验收须固定此业务提交并核对后续差异。

V002-r2 中文验收提交用 `git log -1 --oneline -- docs/verification/V002-r2.md` 定位，报告/Task/本轮探针同提交，不填写自引用哈希。

## 遗留问题
F1 已经 V002-r2 固定修复提交独立复验解除，本 Task 无剩余实质失败。TASK-0003 负责真实 metadata 历史/任务回调、根共享 pending/故障聚合、调度和正式比赛接入；按专题契约调用 CancelAll/Dispose 和回调栈退出后的 FlushReleased。待总控登记本 Task DONE 后方可按依赖启动。TASK-0004 负责官方关键帧/Shader 方法；本轮不继续这两个任务。
