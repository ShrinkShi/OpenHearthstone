# 原 Clip 和 Animation 组件模式导出

2026-10-11。前一节点真实 Unity 播放已验证 Once/Loop/ClampForever 的完成边界。本节点保留源序列化信息，为替换固定动画完成时长提供依据。

## 变更

`tools/native_effects.py` 新导出的 AnimationClip 保留 `wrap_mode` 和 `wrap_known`。0 是真实的 Default/继承模式；缺字段必须标记未知。保留旧 `loop` 字段兼容现有读取方，但不再用它代表完整 WrapMode。

新导出的节点另含 `legacy_animation`：Animation 组件的精确 Bundle/CAB/PathID、WrapMode、默认 Clip ID、自动播放、物理更新和剔除模式。默认 Clip 按指针解析，不按同名查找。

`python -m tools.native_animation_wrap_audit` 只读核对已有 Prefab 的原依赖与 Clip。旧缓存缺少 Clip source 时按精确 ID 从原依赖树恢复；不能只按动画名称认定。输出 `manifests/native-animation-wrap-audit.json`，同时记录失败。没有刷新原运行缓存，没有改变启用标志或素材哈希。

## 验证与限制

新增检查覆盖 0/1/2/4/8/未知数值、缺字段、PingPong 不丢失，以及组件覆盖/精确默认 Clip。目标测试 53 项通过。

完整 Python 测试 683 项通过，34.66 秒。

实际原资源调查：346 处动画引用、205 个唯一 Clip 全部按精确 ID 找到。模式分布：Default(0) 197、Once(1) 2、Loop(2) 3、PingPong(4) 1、ClampForever(8) 2。379 处 Animation 组件引用的 WrapMode 均为 Default(0)。

调查命令退出码 1：59 组依赖树缺少 `unity default resources`，没有 Clip 查找失败。审计保留全部错误，不把内建依赖缺失写成通过。所有 Clip 已定位不代表整个 Prefab 完整；默认模式实际继承值仍需独立 Unity 验证。

非默认样本含 PhysicalTargettedBirthAnimation / FireBall_Missle_CANCEL（8）、FX_ReticleIdle1（4）、FX_Lightning_Idle_01 / Take 001 / Fireball_Start（2）、BabblingBook_openAnim / HighInquisitorWhitemane_CustomSpawn（1）。各自精确 Bundle/CAB/PathID 在审计中，不能仅据名称认为完整卡牌特效可用。

正式播放器尚未读取新模式和覆盖关系，仍按固定时长完成 PlayAnimation。组件与节点匹配、默认模式的有效值、动态完成/自定义事件、负速/循环边界和完整全卡原动画仍未完成。本节点不可计为新启用卡数或已完成原脚本。

未使用 Computer Use，未修改官方安装或用户校对文件。本节点不改 Unity 正式代码，未重新构建 Windows 或重复双客户端比赛。
