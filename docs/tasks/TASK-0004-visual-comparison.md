# TASK-0004

既定双卡：EX1_129刀扇、EX1_134军情七处特工。历史资料将后者误称“刺骨”，实际刺骨为EX1_124；本任务按既定ID/Prefab建立比较，不把EX1_134证据当成EX1_124。

## 任务名称
双卡关键帧对比与 Shader 逆向方法闭环。

## 背景
现有固定相机/GPU/自动截图有基础，但完整官方双卡可比序列尚未建立，局部着色器数学不能证明整卡一致。

## 当前已知事实
Confirmed：tests/visual/README 与 ORIGINAL_MISSILE_PATH/ORIGINAL_SMOKE_SHADERS 保存已有验证边界。Observed：旧本项目 native-effects/EX1_134-0.30.png 白色中心，卡为军情七处特工，不是EX1_124刺骨或官方图。Unknown：完整官方逐帧差异。

## 目标
建立可复现原版/复刻 Birth/Early/Mid/Arrival/Hit/Death 对比；验证 Shader 编译来源→等价行为→视觉输出的方法；形成正式“视觉效果逆向标准工作流”。

## 非目标
不批量迁移其他卡，不宣称取回原开发期源码，不将内部参考充当官方，不做大型 Shader 修复/Bridge。

## 相关文件
visual/SHADER_REVERSE_ENGINEERING.md、VISUAL_COMPATIBILITY.md；tests/visual/README.md；tools/visual_scenarios.py、visual_capture.py、inspect_compiled_shaders.py；manifests/native-*；hearth_visual_db。

## 实施约束
遵守不使用 Computer Use 的当前约束；可使用用户已有官方证据，若缺少可比源序列写 BLOCKED，不自行操纵原客户端。整卡评估依赖 TASK-0003，证据清点可先行。修复发现另建 Task。

## 验收标准
保存来源版本/hash、相机/分辨率/状态/时钟/种子、官方与复刻关键帧索引；解释位置/旋转/时间/颜色/Alpha/粒子/Trail/声音差异；机器可读比较结果及分项等级，未验证项 Unknown；一个 Shader 案例完整审计并独立 V004；标准工作流明确成功与失败分支，不以单相似度代表完成。

## 当前状态
BLOCKED（2026-10-17，总控登记V004-r2局部PASS与阻塞确认，验收ae97e8721f82109883796eda9b09bb3fba102c78）。依赖TASK-0003已DONE；重放/索引/单侧遥测、Shader局部审计及流程已独立复核，未发现可修局部失败。实际关联目录没有既定双卡可比官方序列，来源/版本/机位/状态/时标等缺失；跨来源度量尚未实现/执行，不能进入整TaskVERIFIED/DONE。当前有界队列在此资源阻塞处停止，报告docs/verification/V004-r2.md。

## 实施记录
1. 在 master/fdadec5 干净工作区启动，按AGENTS→PROJECT_STATE→Task→专题读取。只修改Task4采集/工具/测试/专题/DB，TASK-0003生产代码、原探针与独立报告未改。
2. 先实际清点 E:\HS 关联资料：18官方UI静态图、17资源工具/规则仓库图；初始1386本项目生成图。官方18图全部经contact sheet检查，均非双卡效果序列；具体文件/hash/尺寸和排除目录入 task-0004-reference-inventory.json。无其他目录扩搜、原客户端操作或原DLL执行。旧白中心身份已核实，不迁移卡ID。
3. 新 Task4VisualEvidence 编辑器入口和PowerShell runner：真实Match.apply双卡×2视角、1/240秒、固定种子/生产根，按真实事件采集28主帧+C/K+生产固定灰背景合成，输入/相机/时钟/seed/音源请求/图像/hash/位置/旋转/粒子/Trail与资源释放入机器清单；4张contact sheet只标本项目。Death为真实激活，Arrival同帧/先于Hit均保留。
4. family24实例完成只读原bundle→两个材质→两stage编译程序/参数/hash/反汇编→原状态/常量/采样器→公式→实际GPU浮点输出的审计。直接恢复/行为重建/未知分开，产品版本Unknown，局部case不提升整卡。
5. 正式标准工作流包含成功/失败/缺资料分支；官方输入契约及4场景×8项Unknown比较状态已生成。没有合格官方输入的跨来源度量未实现/未执行，不能伪填差异。详见 docs/visual/TASK4_EVIDENCE.md。

## 测试结果
- 最终 `tools/Invoke-Task4VisualEvidence.ps1` exit0：隐藏串行Unity新进程4156/26052各exit0（2026-10-16 14:57:53Z～14:58:25Z），4场景/28主帧，第二进程28/28主图字节及遥测相同；根完成且所拥有克隆/相机输出/C/K释放。
- 7个16×16 ARGBFloat实际GPU样本通过，最大通道误差5.960464477539063e-8，阈值.003；两stage/两材质原源重读与fresh D3DDisassemble匹配。原素材只读；完整源码/hash/状态在 manifests/task-0004-shader-audit.json。
- `pytest tests/test_task4_visual_evidence.py tests/test_particle_alpha_shader.py -q`：7 passed / 3 UnityVersionFallbackWarning / exit0。包含缺官方/错误卡/内部来源不通过契约，以及原程序/材质/8原纹理raw/mip/sampler。
- 两Unity日志均含既有Licensing access-token unavailable信息，但实际exit0；没有业务异常或编译失败。UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
- 未重跑无变化常规1187、五轮停止探针、新Player/正式整局；未跑官方比较、音频波形/实际混音听感或人工整体视觉。当前军情七处特工诊断效果很小很暗，不能称官方一致。

## 最终结果
整Task未完成；BLOCKED，非Verified/Done。局部工具/来源/GPU证据可由总控另派独立V004复核，该局部复核不得自动解除官方资料阻塞。整体V2 provisional与whole_effect_verified=false保持。

2026-10-17独立V004-r2：**局部交付PASS，整体BLOCKED**。全新Unity PID8716/18448各exit0，28/28主帧及完整报告重复，另56 C/K重复、生产C+D*K逐像素重构误差≤2/255；根与独立对象/相机/浮点RT观察全清。fresh来源/完整Shader状态/2stage参数与反汇编、两材质、7浮点GPU样本及7项Python通过；完整伪官方字段仍Unknown，错卡/内部来源缺失，像素/step/绝对时间篡改分别拒绝。原r1的10-16证据保留且未当本轮通过；临时Editor入口清理，未改生产/原清单。官方八分项全Unknown，跨来源度量未实现/未执行，Task继续BLOCKED，不进VERIFIED/DONE。报告 `docs/verification/V004-r2.md`，本轮隔离证据 `work/v004-r2`；提交用该报告git log定位。

## Commit
实现 `16b18048dd3d3eec74e1c802b9c239826e5d1910`（中文提交），交接见 docs/handoffs/F004-task-0004.md；后续文档提交不伪造自引用哈希。

## 遗留问题
最小缺资料为固定EX1_129/EX1_134双视角原始官方连续序列或带时标帧及来源媒体SHA/版本、机位/分辨率、source/targets/连击状态、事件锚点/绝对时间/时钟、随机、色彩/后处理及声音环境。契约 manifests/task-0004-official-input.json；无法取得字段保持Unknown，并显式建立可比性。补资料后仍需正式跨来源度量和独立V004，不能仅凭字段齐全升级。完整绝对artifact路径/继续命令见交接。
