# 原通用 Actor 模板与初始化

2026-10-11。全卡专属动画、完整原Shader/脚本：**未完成**。

通过catalog GUID确认通用模板，避免按同名对象取到内部网格或专属效果副本：

| 模板 | GUID | GameObject PathID | Actor PathID | 节点/网格 |
| --- | --- | --- | --- | --- |
| Card_Play_Ally | 23b7de16184fa8042bf6b734e7ca4d60 | 7379369862269225637 | 7287244737665747621 | 96/47 |
| Card_Play_Hero | 42cbbd2c4969afb46b3887bb628de19d | -2507336562026496370 | 3078986562978823822 | 61/12 |

二者源Bundle均为essential_base_global-prefab-0.unity3d，CAB-69c88b234dfa427e02d1fcee9337ae34。`python -m tools.actor_templates`独立导出MODEL_ACTOR_MINION/HERO，保留Actor组件来源、肖像GameObject索引、肖像材质索引与原SpellTable GUID。旧MODEL_MINION/HERO及用户校准没有替换。来源与缺口记录 `manifests/original-actor-templates.json`。

## 原脚本恢复

静态审核加入GameObjectUtils，共146类型；其IL SHA256 `6629f54e8fe641af2a99d83eda061ea7039ce0bdb6420e633cf3e60cf9857145`。原FindChildBySubstring包含自身，OrdinalIgnoreCase、按子级顺序深度优先。Actor.AssignRootObject以此找到RootObject，AssignMeshRenderers选第一个名为Mesh的Renderer，肖像按原m_portraitMesh引用取得。NativeActorIdentity保存这些真实对象引用，独立于旧显示适配器。

AssetRegistry仅在场景有源Actor元数据时创建该绑定。SpellInitActorVariables现在从精确FSM Owner及其父级找NativeActorIdentity，按原IsNone分别写入Actor、RootObject、RootObjectMesh；GetMeshRenderer(true)保留premium==2的肖像选择分支，没有Mesh时不覆盖Mesh变量。不存在父级Actor时继续报缺口，不拿source卡替代。

**未完成**：Card.GetActor后备路径、正式Spell父级挂接、完整Actor生命周期、AssignMaterials/材质切换、金色模板及动画、ProjectedShadow、MinionShake、附属Spell和SpellTable调度、全部原Shader。模板仍用现有AssetSurface兼容材质，正式客户端未改用新模板。英雄模板Quad引用Unity内建资源，当前1个网格缺失；没有伪造完成。旧GenFX模型仍用于普通比赛，效果启用数仍2。

## 验证

新增31项C#检查，原资源/运行时78172、现有客户端4106通过；Python693（20.66秒）、Windows构建通过。新Python回归证明同名对象必须按catalog GUID的CAB/PathID选择；原RootObject也接入已有显隐边界，继续报告未恢复Actor副作用。

新增GPU12检查/6帧：原通用英雄/随从的真实Actor父级、原初始化动作、原ScaleFrom动作连接，0/0.2/0.4秒捕获。这里只显示原主要Mesh并按原材质索引放入肖像，其他Renderer关闭，是对象绑定fixture，不是完整原Actor或正式GUI截图。截图 `tests/visual/current/native-templates/MODEL_ACTOR_*-*.png`。首次截图使用旧GenFX预览旋转导致整体倒置，已去掉该旋转，按通用模板自身姿态捕获。

日志：`work/build-native-templates-final.log`、`work/native-template-final-gpu.log`。未使用Computer Use、未执行官方程序集、官方安装只读；后台捕获不证明人工表现或60FPS。

GPU完整复跑：原Actor18、生命周期7、显隐4、ScaleFrom8、标准GPU41检查/146帧通过；40Prefab/188动作/133生命周期/798帧调查0异常，46空白场景保留。新增模板截图已查看，方向正确。

最终Windows构建双客户端 `nativetemplate_1/2` 自动完成10回合/v32，Shrink1法师13HP胜、Shrink2术士-1HP负。match `e72ddbceffd24ec4bb6a1c6853f4417d`，两端public_hash `5ddc1bfd3e74fa9e2c55f2b2cbf0b7d3375223ec7abc06d9df1737956e924993` 一致，队列峰值1。只清理47389测试进程，用户37240服务器保留。用户校准SHA256仍为 `e85ce40298c0fd45916d73fb1cc547ad9c46d1e3e124d2e0465f976c6b547da6`。

补齐原RootObject显隐边界后的构建，`nativetemplatefinal_1/2` 双端完成12回合/v39：Shrink2术士25HP胜、Shrink1法师-2HP负；match `a732cd23721749fcab9fb6c2f291b368`，两端public_hash `f2633344902428d34e909af8e20411943b1e6c0090ab06af51595297612a1a35` 一致，队列峰值1，日志无异常。最终构建原资源检查78172通过，测试47390进程已清理，用户服务器保留。
