# 原随从高光 Shader 与状态来源

后续动态轮廓/原高光平面UV1与UNorm8修复见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)。以下保留本Shader节点时的验证范围。

2026-10-11。完整原高光脚本、全卡专属动画与全部原 Shader 未完成。

从原 `Custom/Selection/Highlight` 的 D3D11 顶点/片元程序恢复轮廓采样、UV0/UV1 与 ST 混合、顶点色红通道、原 `_Seed + _ShaderTime`、移动 flare 采样、非线性冷热颜色/强度和 RGBA/Colorize 运算。原 `dp2` 重复标量得到的系数 2 保留，不用简单绿色边框代替。状态为 Cull Off、ZWrite Off、LEqual、SrcAlpha/OneMinusSrcAlpha。

源 Shader 位于 `essential_base_global-material-0.unity3d`，PathID `-1327719733717699995`；Shader 审计缓存保存两段 DXBC。`original-actor-highlight-shader.json` 记录实际程序/参数 SHA256、常量偏移和源状态；`original-minion-highlight.json` 记录实际 Actor 的 HighlightState、RenderPlane 来源、21 个状态槽位（20 个原材质及一个空槽）、材质参数和原 flare 纹理。`python -X utf8 -m tools.actor_highlight` 可重建两份记录与运行时状态缓存，来源解析错误会中止。

原 HighlightState 静态 IL 证明：ChangeState 复制状态材质属性、绑定 Seed，再生成轮廓；有FSM时发送Birth/Idle/Death转场，无FSM时直接切换Renderer。后续组件调查确认普通随从属于无FSM分支，详见后续动态轮廓文档。普通随从 `m_StaticSilouetteTexture` 为空，必须使用 HighlightRender 生成动态轮廓。此历史节点尚未恢复该管线，所以**原高光尚未在正式战场启用**。不能把此节点描述成游戏内可攻击/选中/受击原高光已完成。

Compiled Shader 解析器增加现代 D3D 参数记录 kind=5：原高光顶点参数 blob 0 先前被误当作零代码 GPU 程序；片元参数 blob 2 为 kind=3。修复后两个参数的字节和 SHA256 保留。新增参数边界用例，不跨记录读取。脚本 IL 审计保留已有条目，只更新读取的类型；没有加载或执行原游戏 DLL。

GPU 检查使用浮点 RenderTexture 和独立 CPU 原公式，覆盖 54 组非白顶点色、不同第二 UV 权重、原 seed/time、原 mask 红通道、四色通道 alpha blend；另检查 Shader 支持、21 槽/20 材质数量与真实源 flare。运行 `NativeHighlightMaterialChecks.RunGpu`，不依赖伪造的生产轮廓。完整原 ShaderTime 服务、动态轮廓/状态转场、与原场景空间组合仍未完成。

源状态枚举名与 HighlightRender 的根 Transform、轮廓比例和实际 Shader 依赖也已导出。继续恢复需处理 `HighlightMultiSample`、`HighlightMultiSampleBlend`、`HighlightMaskBlend` 与白/灰/黑替换渲染；源 IL 中的 256 像素轮廓和四级采样常量已保存。重建前先运行 `tools/export_script_il.ps1 -ExtraTypes @('HighlightState','HighlightRender','ActorStateType','HighlightStateType')`；增量审计验证保留全部 154 条既有记录，新增这四种源类型，不删除旧 iTween/Core 来源。

## 本节点实测

705 项 Python 通过（46.34 秒），77 项原高光 Shader/材质 GPU 检查通过；既有43材质/19静态比较/105双方召唤与完整GPU/iTween回归保持通过，iTween 798 帧零异常。Windows 构建及既有客户端、78172原运行时检查通过。日志 `work/native-highlight-final-gpu.log`、`work/build-native-highlight.log`。

两真实 Windows Player 自动比赛 13 回合/v39 完成：Shrink2 术士 9 生命胜，Shrink1 法师 -2 生命负；match_id `b9bce05b29744999b958ae5833595e37`，公开哈希均为 `f59938a9fadfe02786a6f674824824914c844ab558dafb7d4e36dd3201153afd`，队列峰值1。后台 Player 不绘制完整 GUI，此结果验证可玩性回归，不证明新高光已在实战显示、完整GUI 60FPS或人工视觉验收。

按命令行核对清理测试客户端6616/31988与服务器35696，用户服务器10536保留。用户校准 SHA256 保持 `E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`；未使用 Computer Use、未修改官方安装、未执行官方 DLL。
