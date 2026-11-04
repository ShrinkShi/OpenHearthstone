# 原 FSM 纹理参数与绑定恢复

2026-10-10 节点，继续全卡原动画/Shader/脚本目标；全卡恢复未完成。没有使用 Computer Use，没有修改官方安装或用户 UI 校准。

## 原始证据

实际动作名是 `HutongGames.PlayMaker.Actions.SetMaterialTexture`，静态 IL 在忽略缓存 `local/script-audit/Blizzard.T5.PlayMakerCustomActions.Runtime.dll.SetMaterialTexture.il.txt`。原 OnEnter 设置纹理后立即 Finish，不等待服务器。空纹理名称回退 `_MainTex`；空材质选择目标 Renderer 的 materialIndex。绑定不重设 UV ST。

原 ActionData 类型 32（FsmMaterial）和 33（FsmTexture）使用 `fsmObjectParams`，而不是假想的独立材质/纹理表。保留显式 null、变量名、精确 CAB/PathID/Bundle/Type 和导出路径；缺失引用或未知类型明确 unresolved。动态变量默认值的成功解析不等于变量运行时已绑定。

BT_035 英雄分支的三个 SetMaterialTexture 使用固定原纹理：`essential_base_global-texture-2.unity3d`，CAB-572cbaceb2612afe464bd92ebbf0736a，PathID -8039451771726812956 和 3506897862555594978。前两处使用同一纹理，第三处使用另一纹理。它们不是动态卡牌原画；随从分支没有这三个动作。没有凭印象用卡牌原画替代。

## 实现与覆盖

`python -m tools.native_effects --refresh-asset-cache` 按源 FSM 的状态、启用动作顺序、参数身份逐一核对，再只替换类型 32/33。实际刷新 **97 个 Prefab、693 个参数、5 个唯一资源、0 unresolved、0 失败**；重复运行 **0 更新、0 失败**。来源见 [参数审计](../manifests/native-texture-parameter-refresh.json)。没有修改启用索引、几何、动画或布局。

运行时目前只实现 **空显式材质 + Renderer 索引 + 固定 Texture2D 或 null**。不支持的显式材质、动态变量、RenderTexture/Cubemap 标记为 UnboundTextureActions，不猜测资产。Texture2D 通过 AssetRegistry 缓存，原 Shader 未声明的属性不绑定。绑定后更新可见状态，离屏节点不直接画到棋盘。

实例在原 secondary-camera 绑定完成后保存初始纹理和可见纹理标记。回退时间时恢复初始纹理，包括 RenderTexture，再重放 FSM；纹理 ST 不变。服务器保持立即规则结算，客户端独立播放。

## 验证及限制

- Python **656 项通过**；Windows 构建成功，原有 C# **4101**、原资源数据与运行时 **76985** 项检查通过。新增 7 项实际 BT_035 英雄分支检查，核对三处纹理、原导出对象、回放与 ST。
- GPU **28 项检查 / 72 帧**通过；新增 BT_035-Hero 6 帧场景，实际绑定 3、未绑定 0。原有其他 GPU 专项套件本节点未重跑。
- 本节点新构建的真实 Windows 双客户端后台自动完成一局，Profile `texture_1/2`、端口 47342；11 回合/v35，Match f333346081f54351958e38d6b0cb1580，双方公开哈希 `1809b7bc7839213a4903ca05708522ee9de4a8f0e969b21b84bd47ac30bec2dd` 相同。Shrink2 术士 2HP 胜，Shrink1 法师 -4HP 负。batchmode 不绘制正常 GUI，不算人工比赛或正式渲染性能验收。
- 英雄分支可见 4/6 样本，最大 1420 像素、黑色 alpha 186；查看 0.30 秒截图仍是很小的绿色英雄轮廓，距离完整原特效差异明显，**没有启用该特效**。这不是全卡动画完成或整屏人工验收。
- 原 `GetCardPortraitTextureAction` 还涉及 Actor、CardDef 与 premium 判定，其运行时恢复未完成；没有把静态纹理解析冒充该动作恢复。完整目标绑定、全生命周期、全 CommonFX 变体和原后处理仍未完成。

截图：`tests/visual/current/native-effects/BT_035-Hero-0.30.png`；GPU 报告同目录 `render-report.json`；构建 `work/build-native-texture.log`。
