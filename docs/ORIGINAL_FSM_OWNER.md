# 原 FSM Owner 归属

2026-10-11。全卡原专属动画、完整原Shader/脚本：**未完成**。

原SpellInitActorVariables从Owner及其父级查找Actor，缺失时查找Card.GetActor。当前事件source/target不能证明该父级归属；因此不把事件source替代Owner。

## 已恢复

导出器在原PlayMaker组件的m_GameObject中解析Owner，按CAB与PathID匹配原节点索引，保存owner/owner_known。缓存补齐命令：`python -m tools.native_effects --refresh-actor-owner-cache`。17份含初始化动作的原Prefab、25个FSM的Owner均精确解析，复跑0更新/0失败。来源Bundle、CAB、PathID和节点记录在 `manifests/native-actor-owner-refresh.json`。

NativeEffectInstance.MachineOwner只返回已经证明的原节点；旧缓存默认0、越界、缺字段都不返回Root。初始化诊断新增UnboundMachineOwnerActions，区分缺Owner与缺完整父级Actor。此节点没有执行官方脚本，也没有新增启用效果。

## 原 Actor 调查

当前MODEL_MINION来源GenFX_Card_InPlay_Ally_v2（essential_base_global-mesh-0.unity3d，PathID885833137594050249），只有网格层级，没有Actor。17份含SpellInitActorVariables的效果引用图也未发现Actor组件，需原运行时挂接。

找到带真实Actor的Card_Play_Ally(Clone)：Bundle `cardasset_base_global-0516e06c-prefab-0.unity3d`，CAB `CAB-04f1bba1ec8908c41092010259e5da28`，GameObject PathID8952999692579001933。它位于REVFX_MurlocHolmes_CustomSummon中，是特定效果内的实例，**不能冒充全卡通用模板**。包含Actor、SelfAnimUtils、MinionShake、ProjectedShadow、ProjectedShadowController；Actor.m_portraitMesh引用PathID8251267242834286157。

已核对原Actor.AssignRootObject通过FindChildBySubstring查找RootObject；AssignMeshRenderers寻找名为Mesh的MeshRenderer，并从m_portraitMesh取得肖像Renderer。原GetMeshRenderer(true)根据premium类型选择。完整Actor实例与普通/金色模板、查找顺序、材质设置、Spell父级挂接仍需恢复，当前没有把上述调查实例替换正式模型。

## 验证

新增4项Python Owner检查（包含不同CAB同PathID和空引用），Python692通过（20.67秒）；新增9项C# Owner检查，原资源/运行时78141、现有客户端4106通过。检查真实Paw Prefab的FSM属于节点1、Owner缺失/越界拒绝、已知Owner仍报告未恢复Actor初始化。

日志 `work/build-native-owner.log`。两个已启用缓存SHA不变，用户校准保留；本节点未声称完整原Actor初始化已完成。

Windows构建成功；GPU复跑原Actor18、生命周期7、显隐4、ScaleFrom8、标准GPU41检查/146帧通过，40Prefab/188动作/133生命周期/798帧调查0异常、46空白保留，日志 `work/native-owner-gpu.log`。没有Computer Use，不把离屏测试当作正式GUI/FPS验收。

最终构建双端 `nativeowner_1/2` 自动完成12回合/v34：Shrink1法师19HP胜、Shrink2术士0HP负。match `0ee46085aafd49e3b342c7401d7708ab`，两端public_hash `f4f5854dcc534311a9ad5fa588c8cdaa753484c9ff47fb76e3870f9ec2508a40` 一致，队列峰值1。只清理47388测试进程，用户37240服务器保留。UI校准SHA256仍为 `e85ce40298c0fd45916d73fb1cc547ad9c46d1e3e124d2e0465f976c6b547da6`。
