# 视觉架构与迁移边界

路线依据 DEC-001/003。当前已有系统继续使用，详见 [架构](../ARCHITECTURE.md)。

1. Level 1：在本客户端恢复等价数学、生命周期、控制器、状态机、粒子、声音与渲染语义。
2. Level 2：OriginalAsset → Importer → Asset Registry → Open Client Runtime。保留来源身份、版本/hash 和参数，原数据只放本机缓存。
3. Level 3：Original Runtime Bridge 为可选且未实现后端，不能使游戏逻辑依赖原版进程。

当前可用组合点：NativeEffectPlayer、NativeEffectInstance、NativeAreaPlayback、NativeSuperArea/Action、NativeMissilePath/Flight、NativeSpellContext/Transform/Targets、NativeAudio、NativeEffect.shader。状态和控制系统比单个 Prefab 更重要：找出谁创建/初始化/设置来源目标/进入 Birth/发射/旋转/命中/结束/回收及谁同步历史任务。

完整 SuperSpell 仍是缺口，不能将各子图直接启动当作根行为。先使用刀扇/刺骨收敛工厂、事件与时钟，再抽取确实共享的接口。现有服务器权威结算不改变；元数据播放桥只能消费可信历史，不伪造原任务。

资源渲染、行为生命周期、时序、Shader、声音分别验收；数据驱动不等于“所有未知动作自动通过”。不支持项必须保留诊断与证据。
