# F022-B4：H04 最终合成边界复核

后续静态来源更新：[B5](F022-B5-H04-runtime-static-contracts.md)已从当前原版 `OffScreenRenderFX.SetupCamera` 确认 `allowHDR(false)`，与本地一致；本文关于原离屏HDR的Unknown仅保留为当时调查状态。Alpha动作本地时钟与参考图相位仍未配对，不能把不同阶段的窄淡差异直接判为Shader欠亮。

## 范围与结论

本记录复核圣骑士 H04 的原始阶段、r4 双端画面，以及 r3 Editor GPU 分层读回，判断淡 halo 是否来自遗漏的 Glow/Bloom/HDR pass。现有证据没有支持缺失 pass：`GlowTransparent` 在实际 H04 实例中的输出为空，且 `_GlowIntensity`、`_GlowAddMul` 都为 0。r4 中 halo 已从头像亮块恢复为沿英雄边缘的金色效果；相对参考图仍偏窄、偏淡，具体差距仍为 **Unknown**。本次没有改业务渲染或源资源。

## 原始阶段与 Player 画面

r4 driver 和 observer 都按运行中 NativeEffect 的年龄采样，不按墙钟猜阶段。源年龄、FSM 状态和粒子数来自各端 `report.json`：

| 视角 | 阶段 | 源年龄 | FSM 状态 | 粒子数 |
|---|---|---:|---|---:|
| Driver | early | 0.154s | ActionState2 | 226 |
| Driver | mid | 0.445s | ActionState2 | 267 |
| Driver | tail | 1.152s | ActionFadeOut | 294 |
| Observer | early | 0.154s | ActionState2 | 226 |
| Observer | mid | 0.443s | ActionState2 | 264 |
| Observer | tail | 1.152s | ActionFadeOut | 292 |

实际截图位于 `work/f022-paladin-r4/{driver,observer}/reference-paladin-{early,mid,tail}.png`。只读对照裁图 `work/f022-paladin-r4/qa/paladin-hero-crops.jpg` 将原参考、双方 ready 和 mid 并排：方形亮块已消失；mid 可见头像边缘金光，tail 衰减。和 `work/f021-reference-inputs/paladin.png` 相比，r4 的外围金光更窄、更淡，且背景外溢较少。该对照确认效果已进入 Player 最终画面，但不能单凭截图确定剩余差异来自哪个原版运行时环节。

## Source 与 GPU 读回

- 原始 H04 `OffScreenRenderFX` 记录只有 HolyOffscreen 的离屏捕获配置（256×256、bounds 与 clip 设置）；该组件没有记录 Bloom 强度、阈值、模糊或 keyword 参数。对应 prefab JSON 中没有额外 capture 配置。
- HolyOffscreen 的 RenderPlane 使用 `TempBuffer 1`，r3 GPU 读回的离屏 RT 为 256×256、G 均值 12.6、A=255；完整 affine 图在 1024×576 中有 6453 个有效像素，bounds 为 `(464,98)-(561,198)`。这证明常规 H04 source 合成有实际输出。
- `Card_Hero` 材质有 `GlowTransparent` pass，但该实例读回 `_GlowIntensity=0`、`_GlowAddMul=0`；隔离该 pass 的图像为 0 像素。因而当前实例不存在可接入而被漏掉的正向 GlowTransparent 输出。
- r3 读回记录 main camera HDR 开启、离屏 capture camera HDR 关闭。H04 `OffScreenRenderFX` 序列化数据没有 HDR 字段；本次没有原版运行时相机实现证据能确定原版 HDR 状态，因此不能据此更改本地 HDR 设置。
- Mage 的 `RenderToTexture` Bloom 路径有独立组件及显式 bloom 参数，H04 的 `OffScreenRenderFX` 没有相同数据合同。把 Mage 路径接到 H04 会超出当前来源证据。

诊断原件：`work/native-paladin-source-diagnostic/diagnostic.txt`，由 root 串行运行，Editor 进程 exit 0。对应的 H04 vertex-color 归一化来源与修改见 `docs/verification/F022-B1-H04-vertex-color-normalization.md`。

## 尚未证实的边界

当前导出的 prefab/FSM 没有找到给 `Card_Hero` 动态绑定英雄头像纹理或 Actor 材质的动作；该分支读回的输入仍是原预制默认的 `GenFX_white32x32` 贴图。此结果只描述现有导出资源与重建 runtime，不能证明原游戏运行时从未通过外部 Actor 注入纹理或材质。原版外部 Actor binder 与全局后处理是否参与这张效果仍为 **Unknown**。

因此，当前证据既没有证实被遗漏的 H04 Glow/Bloom pass，也没有证实 HDR 或外部材质注入是剩余淡差距的原因。结束本次窄查，不添加空 pass、不提亮、不扩大搜索范围；剩余视觉差异保留 Unknown。

## 验证边界

- Player 阶段图与源年龄来自 r4 双端实际回放；只读 QA 裁图没有改写原截图。
- r3 GPU 诊断由 root 执行并报告 exit 0；本 worktree 未运行 Unity、Player 或 Importer。
- 本记录不声称已达到参考图像素级一致，也不把运行时未知解释成已确认的原版合同。
