# 原版手牌世界姿态计算

2026-10-12。恢复范围是 ZoneHand 的位置、旋转、缩放及 Hover 位置计算。全卡原版专属动画和完整 Shader/脚本未完成，完整效果启用仍为 2。

来源为只读导出的 `local/script-audit/Assembly-CSharp.dll.ZoneHand.il.txt`，程序集身份见 `manifests/original-script-audit.json`。没有加载或执行官方程序集。

`NativeHandGeometry` 保留原世界坐标轴，而不是直接套用屏幕像素坐标。桌面间距为 1.270804；宽度限制按 count 而非 count-1 除。收拢条件是原视觉数量 > 3，然后己方扣除 TurnStartManager 待抽牌数量；敌方不扣除。原旋转 Y 的范围为己方 40+count*2 度、敌方 40 度；卡片自身 Z 旋转为 352.5 度。曲面深度还包含单侧旋转补偿，揭示的敌方手牌额外旋转 180 度、深度偏移 -0.2。

放大的目标选择模式先从最大宽度扣除英雄宽度；偶数牌分开左右半英雄宽度，奇数牌保留原不对称分支。预留槽位按原集合顺序逐个插入，不排序或去重。手机模式的放大间距、缩放与 Hover 小手牌偏移由宿主明确提供，不猜测序列化值。Hover 使用原 MouseOverCardHeight 世界 Z 标记与 SelectCardOffsetZ，Y 为手牌中心 +1，X 保持卡片位置。

当前正式客户端仍使用已校准的 HandPresentation；本节点没有改变玩家画面。未完成原场景中心/宽度/高度标记导入、相机投影、完整 ZoneHand.UpdateLayout 的 iTween 时长/曲线、拖牌及聚焦状态机，也未据此声称视觉与原版一致。未修改用户 UI 校准，未使用 Computer Use，未修改官方安装。

检查入口为 `NativeHandGeometryChecks.Run`；覆盖 1~10 张牌、抽牌前后收拢阈值、敌方揭示、目标选择奇偶布局、预留槽位、桌面/手机缩放与 Hover。该入口继续运行已有区域布局、任务执行、动画与召唤检查链。

`work/native-hand-geometry-final.log`：91 项新增检查通过，Unity 正常退出；既有检查链通过。`work/build-native-hand-geometry.log`：Windows 构建退出 0；实际 Mixer Bundle 九个编译常量匹配。本节点没有规则/网络改动，未重复全量 Python 或双客户端比赛；最近 baseline 为 1165 Python 通过、47438 端口 12 回合比赛两端状态一致，记录见 ORIGINAL_ZONE_LAYOUT.md。这些 baseline 不冒充本节点重新运行结果。

UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
