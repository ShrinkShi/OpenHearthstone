# 原 ScaleFrom 动画

2026-10-11。全卡专属动画、完整原Shader/脚本：**未完成**。

原资源 `Paw_OverrideSpawn_Random.prefab:110e3d741c15e0341a4c58d37c9828b5` 的Action Reveal含iTweenScaleFrom：Actor变量、起始0.1倍、0.4秒、easeOutBack(27)、无loop。缓存 `076d511f7b25ad78f327937e.native-effect.json`。参数从原Bundle精确刷新，40份iTween缓存中1份更新，复跑0更新0失败；原两个启用缓存指纹保持不变。来源记录在 `manifests/native-itween-refresh.json`。

静态核对 `Blizzard.T5.PlayMakerCustomActions.Runtime.dll.iTweenScaleFrom.il.txt`（SHA256 `c22830689b7a695f0761f1451053e29c3987da541260a2fefa8e7084567b8dac`）和已审核的Blizzard.T5.iTween.dll原ScaleFrom IL，没有执行官方程序集。

恢复原语义：先捕获目标对象当前缩放，立即写入vectorScale加transformScale的起始值，再启动延迟ScaleTo回到捕获值。time/speed、缓动、停止、独立finishEvent复用现有原时间轴；借用对象倒放和释放恢复初值。未支持loop继续拒绝，不在拒绝时修改模型。

SpellInitActorVariables原实现从FSM Owner及父级找Actor/Card，不能以事件source替代。当前Owner归属未接通，新增UnboundActorInitializationActions诊断与调查报告字段，不猜测RootObjectMesh。全Spawn控制器、原Actor部件初始化、附属Spell仍未完成；该效果没有启用。

## 验证

新增14检查，原资源/运行时78132、现有客户端4106通过，Windows构建成功；Python688（20.63秒）通过。覆盖原0.1/0.4/Back曲线、延迟前即缩小、speed换算、倒放、释放、非法loop和初始化缺口。

GPU8检查/4帧：使用原动作和显式测试Actor供给，真实MODEL_MINION/CS2_179在0、0.1、0.2、0.4秒按曲线变化；截图 `tests/visual/current/native-scalefrom/CS2_179-*.png`，0.2秒超调帧已检查。这是绑定fixture，不是原完整入场序列。正式启用数仍2，全卡原Shader没有在本节点新增恢复。

最终GPU复跑：原Actor18、生命周期7、显隐4、标准GPU41检查/146帧通过；调查40Prefab/188动作/133生命周期/798帧0异常。46空白场景保留，8个场景明确记录未绑定Actor初始化，不把它们计为完整还原。

日志：`work/build-native-scalefrom-final.log`、`work/native-scalefrom-final-gpu.log`。没有Computer Use；后台GPU和自动对局不证明正式GUI性能或人工体验。

最终构建双客户端 `nativescalefrom_1/2` 自动完成11回合/v34，Shrink1法师13HP胜、Shrink2术士-1HP负。match `9c3d1367413e432183e0f99fb34796b1`，两端public_hash `fa91aabfe2fce9e7cca2f1dbb1be231ed934e0aeec790eeaca8505da528ac38d` 相同，队列峰值1。只清理47387测试进程，用户37240服务器和UI校准保留。
