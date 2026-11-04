# V012 独立验收：H05 TrailRenderer 点龄映射边界

日期：2026-10-21

验收范围：`TASK-0006` 中 H05 TrailRenderer 点龄映射诊断切片

验证者：V012（独立于H05执行者上下文）

## 结论

**本次源码/API边界审计切片 PASS，可标记 VERIFIED。** 这只确认记录中的实现差异、官方文档依据和未知范围准确。H05完整视觉仍为 **FAIL**；本报告不代表Hunter技能或TASK-0006整体完成。

## 验收项

| 项目 | 状态 | 独立结论 |
| --- | --- | --- |
| 本地点龄是否被传入TrailRenderer | **PASS** | `NativeTrailPlayback.Sample`按本地`Point.time`裁剪点，可追加当前位置，随后无条件调用`Renderer.Clear()`，再把仅含位置的数组传给`AddPositions()`。本地点龄未传回TrailRenderer，不能保留在本地列表里的逐点年龄。该证据支持“时间戳映射不等价”，不证明Unity清空后批量点的确切内部年龄。 |
| Unity 2022.3 API引用措辞 | **PASS** | [AddPositions API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TrailRenderer.AddPositions.html)说明Trail点有出生时间戳、平滑消失需要唯一递增时间戳；当在同一current time添加多个点，文档描述Unity会在最新现存点与当前时间间调整时间戳。该API输入位置数据，不含本地点龄参数。[Clear API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TrailRenderer.Clear.html)说明它移除全部Trail点。 |
| Unknown与H05视觉状态边界 | **PASS** | 记录把清空后Unity内部赋时方式和最终像素影响保留为Unknown，未将时间戳差异说成弱窄轨迹的已证实根因。正式Player同帧采样证明渲染链有输出，但不能单独归因于点龄、材质或合成；正常施放截图中箭体仍不可辨，H05维持视觉FAIL合理。 |
| 定向pytest与空白检查 | **PASS** | `tests/test_hunter_trail_diagnostic_contract.py`含两个源码合同检查。独立禁用pytest缓存与字节码运行，结果2 passed。Task与来源矩阵限定`git diff --check`退出0，仅有LF/CRLF提示。 |

## 限制

本轮未编辑代码，未运行Unity、Importer、Build或Player。验收不证明Trail内部点年龄或视觉因果，也不接受增量`AddPosition`作为修复。H05候选若进入实现，仍需比较固定presentation age下的点迹、宽度、RT、GUI和屏幕输出，并覆盖暂停、变速与倒退seek；正式Player和完整技能状态尚未通过。
