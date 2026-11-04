# 原 SuperSpell 区域子效果

TASK-0002 复用 RegisterClone/NativeAreaInstance 增加了 [命中独立端点](ORIGINAL_SUPER_IMPACT.md)。命中工厂显式选择原初始空 FSM 入口，防止主动发送 None 关闭克隆；本页区域端点仍沿用既有默认入口。命中完成/None 释放的幂等与故障保护限定于新端点，未声称恢复完整根控制器。

2026-10-12。全卡原专属动画与完整 Shader/脚本未完成。正式客户端仍只启用原先审核的两个效果；本节点未切换正式播放器。

## 来源与发现

只读静态核对 SuperSpell.DetermineAreaEffectInfo、SpawnAreaEffect、CloneSpell、WaitAndSpawnAreaEffect、OnCloneFinished、OnCloneStateStarted 及 SpellAreaEffectInfo。未执行官方程序集。

DetermineAreaEffectInfo 按来源控制方选择一个可用区域配置；来源或控制方缺失时优先可用 friendly，再取 opponent。可用要求配置存在、Enabled 且 Prefab 非空。地狱烈焰的友方和对手 Prefab 各自已包含双方视觉，不能同时实例化两个配置。

`python -m tools.super_area_source` 导出 232 个程序的原根区域配置，保留原始组件、两侧参数及 Prefab CAB/PathID/Bundle/GUID 来源。公开来源为 manifests/original-super-area-config.json。

`python -m tools.export_super_area_prefabs` 独立导出奥术爆炸和地狱烈焰的三个区域 Prefab，复用既有 UnityFS、层级、材质、动画、FSM 和音频导出器。输出 `.area-spell.json` 与 `.area-audio.json`，不覆盖已审核 `.native-effect.json`。每个 Prefab 的音频导出错误为 0，但未人工听音认证。

## 实际执行

NativeSuperArea 恢复原选择、克隆、注册回调、挂接、设置来源/任务、定位与激活顺序。原 IsMakingClones 始终为 true，不按序列化 m_MakeClones 误用共享实例。原随机延时采样后仅正值进入 scaled 等待；宿主没有等待服务时明确失败。

UseSuperLocation 覆盖世界位置；其他非零 Location 才覆盖配置并 UpdatePosition，非零 Facing 独立更新朝向。原定位字段 setter 不重新置 dirty。定位参数复制到每个实例，避免污染共享程序。

NativeAreaInstance 用真实原 Prefab 创建独立 Spell，从 None 完成设置后 Activate。根原 FSM 的状态开始回调可明确启用；完成通知与进入 None 后释放是两个独立步骤。回调逆序执行，沿用原列表清除规则。禁止倒退采样，避免 Effect 重建时丢失克隆来源与定位；重新播放必须创建新实例。

## 已验证与未完成

两个 Python 来源检查通过：全部 232 配置重读一致，以及三个独立 Prefab/音频/FSM 身份核对。真实克隆的友方/对手共四个场景完成设置、播放、可见像素与完成回调检查。

诊断截图位于 tests/visual/current/native-super-area/，使用独立正交捕获；不是正式比赛画面，也不是官方同状态对比。奥术爆炸可见，地狱烈焰仍有亮部饱和及正交投影差异。未把原截图当背景。

未完成：完整 SuperSpell.DoAction、任务/历史/目标自动绑定、SpellManager 对象池、源 Actor 与战场坐标连接、原全局 Shader/后处理、正式播放器切换、全卡专属效果认证。现有 SetSource 保存显式来源身份，不能替代原 Card/Controller 服务。配置覆盖数不代表完整效果覆盖数。

没有使用 Computer Use，没有修改官方安装或用户 UI 校准。

最终检查：work/native-super-area-final2.log 新增 32 项 Unity 检查及既有检查链通过，退出 0。新增倒退采样测试初次错误地假定所有 Prefab 在 0.3 秒仍 pending，已改为核对采样前后 pending/完成数不变；原图的真实完成时间未改变。work/build-native-super-area.log Windows 构建退出 0，实际 Mixer Bundle 九个编译常量匹配。

Python：1169 passed/1 deselected/18 warnings/292.85 秒。排除项为上一节点已经通过的全部 254 程序/715 定位组件重读检查，其代码和原缓存本次未变；两项新区域来源检查也单独通过。不宣称同一次全量命令绿色。

双客户端后台比赛：端口 47442、super_area_1/2 独立 Profile，比赛 e4a0dc89f6804d928da41365c39f05db，17 回合/version60。Shrink2 法师 11 血胜，Shrink1 法师 -1 血负；两端 public_hash 都为 e4430d34e38f98e96a0fc4918f0ea07799174979438f2cf3ecbbc1b8607c1b7a，日志无 Exception/fallback/Error。后台不绘制 GUI，本结果证明既有玩法回归，不能证明新区域入口在正式画面通过。核对命令行后只清理自有 PID 18988/3904/32704。

用户校准 SHA256 为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，保持不变。
