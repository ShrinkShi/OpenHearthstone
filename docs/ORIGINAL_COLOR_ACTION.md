# 原 AnimateColor 四通道曲线恢复

2026-10-10，7baa026 后继续全卡原专属动画/Shader/脚本目标。目标未完成；没有 Computer Use，没有修改官方安装或 UI 校准。

## 原脚本证据

原 `AnimateColor` 从 `AnimateFsmAction` 继承时钟、延迟、曲线长度、计算模式及结束逻辑。静态 IL 在 `local/script-audit/Blizzard.T5.PlayMaker.Runtime.dll.AnimateColor.il.txt` 和 `AnimateFsmAction.il.txt`，另有 `PlayMaker.dll.FsmUtility.il.txt` 证明 packed Color 为 RGBA 四个 float、UseVariable 字节、剩余 UTF-8 名称。新增基类审计后为 131 个类型；各 restored=false，不把恢复部分分支当作整个类型完成。

当前缓存共有 **59 个 AnimateColor**，计算参数 236 个：232 个模式 1（直接曲线）、3 个模式 0（保留进入时值）、1 个模式 3（进入值减曲线）。53 个 time=None，6 个 time=2；speed/delay 全部 None，realTime 全 false，finishEvent 全为空。没有为凑覆盖编造未出现分支。

## 实际实现

- 原 RGBA 四条曲线独立采样，保留切线/权重和初始颜色；计算 0/1/2/3 按原 IL 分别保留/直接/初值加/初值减。4~7 分支尚未恢复，遇到明确计为未绑定。
- None speed 为 1，不能把原序列化的 0 当速度 0；time=None 使用各曲线原长度，显式 time 按各通道终点缩放采样时间。ignoreCurveOffset 使用原末个非空通道决定的初始时刻。
- 原 Init 立即计算结果，delay 绝对值小于 .01 时首次写入变量；Update 中延迟检测、时间推进、采样和最终写入后下一步 Finish 分开。延迟中原基类仍推进 currentTime，没有擅自改成暂停时钟。
- 每个客户端 FSM 保存独立颜色动画时钟。重复进入或 Seek 回退重新捕获初值并重放；状态转场时不重复推进同一采样区间。持续 SetMaterialColor 读更新后的原变量。
- 实时墙钟、负速度、动态控制值变化及部分计算模式未完成。当前 59 个动作不使用这些分支；全原脚本恢复仍未完成。非空 finishEvent 路径已接线，但本轮没有原样本逐项验收。
- 既有 EaseFloat/EaseColor 仍含线性兼容路径，本节点没有将其宣称为原 easing 恢复。

## 关键枚举验证

Unity 曲线的序列化 m_PreInfinity/m_PostInfinity 与公共 WrapMode 数字不同。`NativeCurveEncodingChecks` 用本机 Unity 2022.3.60f1c1 创建真实曲线、EditorJsonUtility 导出并与运行时采样对照：**0=PingPong，1=Loop，2=Clamp，3=Default**。Once 与 ClampForever 同样保存为 2。默认/Loop/往返/钳制共 5 种实际曲线通过采样验证，记录 `work/native-curve-encoding.txt`。

首次验证中错误地硬写 Default 越界样本应为 1，检查失败；改为直接与 Unity 原曲线实际 Evaluate 对照，保留枚举及采样双重检查。没有降低断言精度。

## 提取与验证

`python -m tools.native_effects --refresh-color-cache` 逐一核对源状态、启用动作顺序及参数身份，仅刷新 AnimateColor。**25 个 Prefab / 59 个动作 / 885 个参数 / 236 条曲线，0 unresolved、0 失败**，重复零更新。原曲线均为序列化 2/2（钳制）。审计见 [native-color-refresh.json](../manifests/native-color-refresh.json)。已有两项正式启用特效的程序 SHA 仍匹配；没有启用新特效。

- **664 Python** 全套通过；Windows 构建成功，**4101** 既有 C#、**77004** 原资源/运行时检查通过。新增 8 项颜色检查及 5 种 Unity 枚举采样验证，包含真实智慧祝福曲线进入材质、回退、减法、循环不结束与显式长度。
- GPU **30 项 / 84 帧**，新 Holy-Wisdom 原 Birth 分支诊断 6 帧：5/6 可见，最多 1847 像素、638 粒子，黑色 alpha 0、未绑定颜色动作 0。
- 查看 0.60 秒图仍是微小的红色局部光点，原目标大小/位置、Shader 家族、生命周期和后处理差异明显，**未启用**。不能当作正常比赛的原智慧祝福动画完成。
- 其他专门 Shader/Glow GPU 套件本节点未重跑。batchmode 双客户端只验证比赛和同步，不绘制正常 GUI。
- 本节点 Windows 后台双客户端完成一局：`color_1/2`、端口 47348，Match ee6cf9b67f074d0c949bac2c193ffcdf，14 回合/v48；双方公开哈希 `79f5b6d184564c096e9d003074e3e952f9420860bd9c5fd86b0501e08c823902` 相同。Shrink2 术士 9HP 胜，Shrink1 法师 -4HP 负。不是人工比赛或正式渲染 FPS 验收。

日志 `work/build-native-color-final.log`、`work/gpu-native-color.log`；截图 `tests/visual/current/native-effects/Holy-Wisdom-0.60.png`。动态 Actor/原画、全 Shader 变体、完整 PlayMaker/Animator、全卡逐场景验收仍未完成。
