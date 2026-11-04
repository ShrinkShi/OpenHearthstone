# 原卡牌特效的 Spell 定位数据与根生命周期

2026-10-12。全卡原专属动画、完整 Shader/脚本未完成；正式审核启用效果仍为 2，这两项的本地审核也不是原版逐帧一致性认证。

## 发现与来源

此前定位导出器仅处理 205 个 Actor Spell 根，普通 `.native-effect.json` 未保存原根 Spell/SuperSpell 的 m_Location、m_Facing、命名骨骼、父子关系和旋转选项。火球术与准星原根均为 SuperSpell，SourceAuto(location2)、Facing None、无父子附着。奥术爆炸的原 SuperSpell 还引用友方区域子 Prefab；地狱烈焰分别引用双方区域子 Prefab，不能将这些实例默认全部放到单个中心点。

`python -m tools.spell_transform_source --kind effects` 现在导出全部普通效果的根、嵌套以及独立引用 Prefab 内的 Spell 组件。缓存保留精确节点号、原类型、全部定位字段、CAB/PathID/Bundle 和原始组件 typetree；公开追踪为 manifests/original-effect-transform-source.json。原 Actor 目录及默认命令保持独立。

工具复用普通导出器的精确原层级遍历，同时逐节点核对 name/parent/path；不同层级直接失败，不按名字猜节点。共享遍历也用于既有 Scale/Actor 参数刷新，避免两个映射算法分叉。普通 export_program 已接入同一定位序列化，重新生成效果缓存不会再丢掉这组字段。导出写入使用项目 asset_lock 与 atomic_json；官方安装只读，原组件原始数据只保存在忽略的 local 缓存及来源 manifest。

## 执行恢复与界限

明确调用 NativeEffectInstance.SelectSourceSpellState 时，SuperSpell 的 Birth/Action 现在进入根定位过程，使用已有精确 NativeSpellTransform。普通 location2 清 dirty 后不会在 Action 擅自追踪移动的源；原 SuperSpell.OnAction 则只对 location18 强制位置 dirty、facing9 强制朝向 dirty。重播重建 dirty 并重新解析源对象。

原 IL 为 SuperSpell.OnBirth/OnAction 与 Spell.OnBirth/OnAction。SuperSpell 的 UpdateTargets、子 Spell Spawn、路径/Impact/回调与完整状态处理不因根定位成功就算完成；UnrestoredSourceActionSideEffects 仍记录这些缺口。本节点未把普通所有 FSM 的 SpellStateAction 直接转发到根，因为它必须按原 FindComponentInThisOrParents 解析实际 Spell，子组件可拥有不同的实例和生命周期。

普通客户端仍使用既有已审核路径，没有据此启用未完成的火球术或准星。根/子 Spell 的实际路由、共用战场坐标、相机与完整 SuperSpell Controller 尚未完成；新数据是后续连接的原始依据，不是整卡画面还原通过。

## 已运行验证

work/native-effect-placement.log：11 项新增 Unity 检查通过，真实火球术/准星模型的 SourceAuto、普通 dirty 行为、重播与特殊动态选择器已验证；继续执行 460 完整曲线及既有姿态/区域/历史/动画/召唤检查链。64 个效果与 Actor 表 Python 检查通过。work/build-native-effect-placement.log Windows 构建退出 0；实际 Mixer Bundle 九个编译常量匹配。

后台 effect_placement_1/2 在端口 47440 完成 dbb9d83bd94b460a88b577cef22b7d7f：13 回合/version43，Shrink2 战士 25 血胜、Shrink1 盗贼 -4 血负；双方 hash 均 b80f22e95fce7792d306b11290cc5dc784efa840e7866f5820bf98dc99f13ce3。日志无 Exception/fallback/Error，终局核对命令行后仅清理自有 PID 20816/30652/7228。后台不绘制 GUI，不当作新定位已经在正常比赛完整播放的证据。

UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未 Computer Use、未改官方安装、未执行原程序集。

原相机只读调查确认 @player/level30 的 Main Camera 是透视投影，序列化 FOV 为 34.870445251464844；当前诊断特效捕获为正交 size16。当前空白调查包含 16 Cancel、13 Action、2 Birth、4 Death，不能简单认定所有空白都是故障，也不能用把捕获区域放大来证明原表现恢复。尚未恢复运行时相机适配与原同状态视觉参考。

完整定位导出已完成：254 程序、715 Spell 组件（254 根、461 子/独立引用组件），12 种类。包含 476 Spell、204 SuperSpell，以及 Brawl、Jaraxxus、OverrideCustomSpawn、SpawnToHand 等派生组件。原脚本的完整执行仍未恢复，不能用组件枚举代替脚本运行认证。

新增定位字段使两个原启用 cache 的 SHA 改变，初次全量检查报告旧审核指纹不匹配。work/native-effect-placement-gpu.log 重拍后 41 GPU 检查通过，查看奥术爆炸与地狱烈焰组件图后，通过原 native-effect-review.json 审核入口更新这两个指纹，未新增启用效果；原指纹测试单项复测通过。地狱烈焰中央过曝仍存在，当前图片不是完整正常客户端截图。

原 40 程序/188 动作/133 生命周期/798 帧调查复测为 0 异常、35 空白场景，可见性没有变化。

全来源核对已完成：完整 pytest 原会话结果为 1166 passed/1 failed/17 warnings/1964.35 秒，唯一失败是更新审核前已经记录的旧指纹检查；新的全部 254 程序/715 定位组件原资源重读一致性检查通过。审核修复后复测其余 1166 项为全部通过（1 项全来源核对排除，16 warnings，135.23 秒），指纹单项也通过。因此当前 1167 个唯一测试均已覆盖通过，来源检查与常规组分两次确认，不伪称同一次全量命令绿色。
