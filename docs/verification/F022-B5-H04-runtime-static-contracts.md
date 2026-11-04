# F022-B5：H04 原版运行时静态契约

## 结论

对当前原版程序集做 metadata-only Cecil 读取后，H04 的 `OffScreenRenderFX` 自建相机明确关闭 HDR；本地 r3 读回同样为 HDR 关闭，离屏 HDR 不构成两边的已证实差异。程序集里存在通用 Actor portrait 更新和子树绑定代码，但没有发现将 Actor portrait 指向 H04 `Card_Hero` 材质的专用调用。发现的通用子树绑定路径只处理材质名包含 `portrait` 的 Renderer；H04 材质名 `GenFX_HolyHeroRender` 不满足这个筛选。

H04 的 `CardHeroAlpha` 原曲线在动作本地 0.5 秒到 0.5 峰值，然后按 Loop 重复。r4 记录的是 NativeEffect 实例年龄，没有记录 `AnimateFloat` 自己的时钟起点，因此实例年龄不能精确替代曲线时间。用户原参考图的 source age 也未知；剩余亮度差异仍不能归因给 Shader 欠亮或漏掉的 Actor 纹理注入。

## 程序集与方法证据

只读来源为本机当前原版程序集，Cecil `AssemblyDefinition.ReadAssembly` 读取元数据和 IL，未加载或执行官方代码：

- `D:/Program Files (x86)/Hearthstone/Hearthstone_Data/Managed/Assembly-CSharp.dll`，SHA-256 `786376BDDAD544709639D4B3AB60CE8799D4B1C6C7D7FC4C225CE79FE07228C6`。
- `D:/Program Files (x86)/Hearthstone/Hearthstone_Data/Managed/Blizzard.T5.PlayMaker.Runtime.dll`，SHA-256 `6737A12CC53A30AE028474DD9ACB5C8B8C21856E9B14958EA38E5EC75CA02BB9`。
- H04 导出 `work/f022-assets-before/paladin-root.json`，SHA-256 `75A90A6013CD59F44B1CE579CCFC4AE1C4A5010FCB81E51F227D9BA4359EBC0B`。

### OffScreenRenderFX

当前 Assembly-CSharp 的 `OffScreenRenderFX` 字段只有源对象、bounds/尺寸/clip、CameraRect 与私有相机/RT状态，没有 Actor、portrait 或 Glow 字段。

- `Start` 顺序调用 `PositionObjectToRender`、`CreateCamera`、`CreateRenderTexture`、`SetupCamera`、`SetupMaterial`。
- `SetupCamera` 的 IL `0x00B6–0x00B7` 调用 `Camera.set_allowHDR(false)`。它同时设置正交投影、裁剪面、目标 RT、黑色清屏、CameraRect 和深度。
- `CreateRenderTexture` 调用 `RenderTexture.GetTemporary(RenderResolutionX, RenderResolutionY, 24)`，未显式传入 HDR 格式参数。
- `SetupMaterial` 将临时 RT 设置到 OffScreenRenderFX 所属 Renderer 的 `mainTexture`。该路径是捕获结果回写 RenderPlane，不是给 `Card_Hero` 绑定 Actor 纹理。

这与 r3 GPU 诊断记录的 `offscreenCameraHDR=False` 一致；主相机 `HDR=True` 仍是另一条渲染路径，不能推翻原 OffScreenRenderFX 相机的明确设置。B4 中“原相机 HDR 未知”的结论由本次当前程序集 IL 复核修正为 **Confirmed false**。

### Actor portrait 与 H04 材质

当前 Assembly-CSharp 的 `Actor.UpdatePortraitTexture` 优先读取 `m_portraitTextureOverride`；没有 override 时从 `CardDef.GetPortraitTexture` 取 Actor 原画，最后经 `Actor.SetPortraitTexture` 写入该 Actor 自己的 `GetPortraitMaterial().mainTexture`。`Actor.SetPortraitTextureOverride` 的调用者为 `TempleArt`、`MineCartRushArt`、`BigCardEnchantmentPanel` 和 `Actor.SetHistoryItem`，未发现 H04 `OffScreenRenderFX` 或 `Card_Hero` 的直接引用。

另有通用 `HutongGames.PlayMaker.Actions.AssignActorPortraitToChildrenAction.OnEnter`：它从来源 Actor 的 `m_portraitMesh` 与 `m_portraitMatIdx` 取得 portrait texture，遍历目标子树 Renderer；仅当目标 `Material.name.Contains("portrait")` 时才写入 `Material.mainTexture`。同程序集的 `AssignActorPortraitToChildren` 组件采用相同材质名筛选。当前 H04 来源材质名是 `GenFX_HolyHeroRender`，不符合该谓词；该通用代码本身不能解释 H04 的白色 `GenFX_white32x32` 输入被 Actor portrait 替换。是否存在其他外部运行时路由仍为 Unknown。

此前来源记录确认 H04 的 `GenFX_HolyHeroRender` 原始 `_MainTex` 指向 32×32 全白 `GenFX_white32x32`；详见 `docs/ORIGINAL_LOCAL_VISIBILITY.md`。本次没有再解析 Prefab FSM，也没有把“未找到此程序集路径”扩展成“原客户端绝不会注入”。

### Glow 参数调用点

本轮对当前 Assembly-CSharp 的 `_GlowIntensity` / `_GlowAddMul` 字符串常量作定点扫描，命中调用如下：

- `Hearthstone.Timeline.CameraOverlayHelper.UpdateTargetCommonFX` 把 overlay material 的值从 `m_valuesForCommonFX[22]`、`[23]` 传给 `TrySetMatFloat`。
- `LightningAnimator.Start` 读取已有材质 `_GlowIntensity` 到 `m_matGlowIntensity`；其 `AnimateMaterial` 迭代器更新该材质的强度并在结束时写 0。

两条都是通用 Timeline/材质动画路径；本次在 `OffScreenRenderFX` 与 Actor portrait 方法里没有找到给 H04 `Card_Hero` 写 `_GlowIntensity` 或 `_GlowAddMul` 的调用。H04 实例的这两个属性读回为 0、`GlowTransparent` pass 输出为 0 像素，GPU 证据见 `work/native-paladin-source-diagnostic/diagnostic.txt` 与 [B4](F022-B4-H04-final-render-boundary.md)。当前来源不支持给 H04 接入额外 Glow pass 或调高亮度。

## Alpha 曲线与采样相位

原 H04 `ActionState2` 的 `AnimateFloat(CardHeroAlpha)` 曲线关键帧为 `(0, 0)`、`(0.5, 0.5)`，`realTime=true`，`post_wrap=2`；`ActionFadeOut` 使用 `(0, 0.5)`、`(0.5, 0)`。同一资源中 `_TintColor.a` 每帧接收 `CardHeroAlpha`。

当前 `Blizzard.T5.PlayMaker.Runtime.dll` 的 `AnimateFloat.OnEnter` 记录 `FsmTime.RealtimeSinceStartup` 并将 `currentTime` 置 0；`OnUpdate` 对 `realTime=true` 计算 `RealtimeSinceStartup - startTime`，再传给 `AnimationCurve.Evaluate`。`ActionHelpers.IsLoopingWrapMode` 对 WrapMode `2` 或 `4` 返回 true；因此 `ActionState2` 的 0.5 秒尾端是本地曲线峰值，并循环重现，而不是在 0.154 秒之前已结束的一次性峰值。

r4 的 early/mid 捕获分别记录 NativeEffect `sourceAge=0.154s/0.445s`；mid 材质 tint alpha 约 0.38，接近本地 0.5 秒曲线峰值。采样器没有同时记录 AnimateFloat 的 `startTime/currentTime`，因此不能将实例年龄直接认定为精确曲线位置。原曲线不支持“峰值早于 0.154 秒”的判断，本次不要求早峰 GPU 帧；用户参考静帧的 source age 未知，剩余差距仍按相位 Unknown 记录。

## 验证边界

- 当前 Assembly-CSharp 与 PlayMaker runtime 都是本机安装版本的只读 Cecil 元数据/IL 查询；没有执行 DLL、运行游戏、Unity、Player 或 Importer。
- 本轮没有修改业务代码、原版安装、local 缓存或共享资产。
- 确认了原 OffScreenRenderFX 相机 HDR 关闭、通用 Actor portrait binder 的材质筛选合同，以及 H04 Alpha 曲线的动作本地峰值；没有确认 H04 专属外部 Actor binder 或原版全局后处理链。
