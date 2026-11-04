# 原 Actor 显隐动作

2026-10-11。全卡原专属动画、完整原 Shader/脚本：**未完成**。

静态审核 Actor IL（SHA256 `e66f681ec75c3b53083a4b0b9f8389a09981d6c2e7677325022076d9c7d95ef1`），没有加载或执行官方程序集。脚本审核共145类型，仍不标为完整恢复。

`--refresh-actor-visibility-cache` 精确刷新11份原Prefab、26个动作、156参数；复跑0更新/0失败。来源Bundle、CAB、PathID记录在 `manifests/native-actor-visibility-refresh.json`。

## 运行时行为

ActorSetVisibilityRecursiveAction区分普通Renderer与Actor。恢复父级遍历、子级遍历、Actor边界停止递归，以及resetOnExit保存/恢复。普通Renderer仅修改enabled，不改变对象active；借用外部Renderer在重播及释放时恢复初值。内部缺失纹理仍保留原有安全门，不冒充原Renderer分支完全一致。

客户端MODEL_HERO/MODEL_MINION挂接明确的NativeActorPresentation，以模型源层级根作为VisualRoot；这不是官方Actor组件。根对象显隐同步影响离屏模型和附属数值可见性，重播及效果释放恢复借用前状态。ActorRootVisibilityBindings统计根绑定，UnrestoredActorVisibilitySideEffects明确报告剩余缺口。

**未完成**：官方Actor的m_rootObject/m_meshRenderer初始化、owned Spell表、ignoreSpells完整语义、护甲效果、状态管理器、ProjectedShadow、Ghost、DiamondRT、Highlight回调、完整SpellController调度。不得用根对象SetActive等同原Actor.Show/Hide。

## 验证

新增8项显隐检查，原资源/运行时78118、现有客户端4106、Python688（21.01秒）通过，最终Windows构建成功。覆盖原Patchwerk普通Renderer层级隐藏、原GetActor对象变量、模型根显隐、退出恢复、倒放、缺对象诊断、释放不销毁模型。

新增GPU4检查/2帧，`tests/visual/current/native-visibility/CS2_179-hidden.png`与`CS2_179-restored.png`验证真实MODEL_MINION输出与HUD可见状态；恢复截图已检查。此用原动作组成的绑定fixture不是原卡完整动画。Actor GPU18、生命周期GPU7、标准GPU41/146帧通过；40Prefab/133生命周期/798帧调查0异常。仍有46空白场景，启用效果仍2，两个启用缓存SHA不变。

日志：`work/build-native-visibility-final.log`、`work/native-visibility-final-gpu.log`。调查报告新增显隐缺对象、根绑定和未恢复副作用计数。没有Computer Use，未修改官方安装；后台检查不证明人工GUI表现及60FPS。

最终Windows构建双客户端 `nativeactorvisibility_1/2` 自动完成17回合/v57：Shrink2术士11HP胜、Shrink1法师-2HP负。match `e44618b6a90a490bbae366a542ffefd4`，两端public_hash `b64ab33200e61836919e291673cb752f77c45a7db518dbf628fffee69bf7c4ee` 一致、队列峰值1。只清理本轮47386端口测试进程，用户37240服务器保留。用户UI校准SHA256仍为 `e85ce40298c0fd45916d73fb1cc547ad9c46d1e3e124d2e0465f976c6b547da6`。
