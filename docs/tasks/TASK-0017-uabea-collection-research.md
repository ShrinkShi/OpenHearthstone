# TASK-0017：使用 UABEA 研究收藏界面

- 状态：IN_PROGRESS；本轮完成一组只读静态调查，尚未独立复核或恢复完整场景行为。
- 目标：将原场景的序列化引用与交互方法调用关联，为后续修正收藏页提供可复现依据。
- 工具：[UABEA 官方 v8](https://github.com/nesrak1/UABEA/releases/tag/v8) 的 `AssetsTools.NET.dll`、`classdata.tpk` 和 `Mono.Cecil.dll`。官方 Windows zip SHA-256 为 `0623AB1DC099A6397C3A7E72782B91405E60B286C6825FB6FA21E553FF2EA580`。未操作 UABEA 图形界面；通过其随附库读取资源，通过 Cecil 静态读取 IL，没有运行原游戏程序集。
- 来源：`D:/Program Files (x86)/Hearthstone`；资源包 `cardasset_base_qualityhigh_cm_global-prefab-0.unity3d` SHA-256 `3CA7509D555981EDBE331958F9ADF1264F559B36663289143370CB01C261D725`；`Assembly-CSharp.dll` SHA-256 `2408C5EAE3D5AB083B8B290A7278147159E79A3C6E72E1D922EF44028ED16AFB`。另有 F 盘版本，哈希不同，不得混用证据。具体产品版本 Unknown。
- 复现：将官方 v8 Windows zip 解压至 `work/uabea-v8/files`，然后在仓库执行 `pwsh -NoProfile -File tools/audit_collection_uabea.ps1`。路径可通过参数覆盖；生成的本机报告和 48 个方法的 IL 位于 `local/collection-uabea-audit/`，不纳入产品或公开资源。

## 已确认的静态事实

- UABEA 读取的 `CollectionManager` GameObject path ID 为 `-6398912150071876638`；根节点有 Transform、`CollectionManagerDisplay` 和 `CollectionInputMgr`。`CollectionManagerDisplay` 中 `m_bookBack` 指向 `BookBack_mesh`，另有 `CollectionPageManager`、`ManaTabManager`、`SetFilterContainer` 和 `CraftingModeButton` 的同文件引用；`m_filterButton` 为零引用，不能据此认定按钮不存在或其实际激活路径。
- `CollectionDeckTray.SaveCurrentDeckAndEnterDeckListMode` 先调用 `SaveCurrentDeck`；普通分支随后切换托盘模式、调用 `CollectionManager.DoneEditing` 及 `CollectionManagerDisplay.OnDoneEditingDeck`。存在乱斗等分支；静态调用顺序不证明服务器响应时序。
- `CollectionPageManager.FilterByManaCost` 调用 `CollectibleCardFilter.FilterManaCost`、更新结果并请求页面过渡；`ShowCraftingModeCards` 使用拥有与可制作性等筛选。`PageLeft`/`PageRight` 含死亡骑士符文专门分支，不能当作简单整数翻页的完整证明。
- `CollectionManagerScene.Awake` 使用 `m_CollectionManagerPrefab` 的移动端覆盖值请求实例化；自走棋使用另一引用。`BookPageManager.Awake` 注册左右翻页区域回调，实例化两个页面并保持本地 Transform；`Start` 等待页面加载。该证据支持继续追踪场景加载，但尚未确定背景及相机。
- `CollectionManagerDisplay.OnSetFilterButtonPressed` 的方法体更新活动记录并关闭筛选按钮发光；此方法本身没有显示 `SetFilterTray` 的调用。打开操作由下述单独注册的回调处理。
- 后续静态追踪：`SetFilterTray.Awake` 在桌面给切换按钮和外部区域分别注册打开、关闭回调；`CollectionManagerDisplay.Start` 从嵌套 prefab 获取同一托盘，并向其按钮追加更新活动记录的回调。`Show` 的进、出时长分别是 0.35 和 0.25 秒，音源引用分别为 `66491d3d01ed663429ab80daf6a5e880` 和 `3139d09eb94899d41b9bf612649f47bf`。本地复刻已接入对应声音与托盘位移动画。
- `BookPageManager.TransitionPageWhenReady` 对 RIGHT 使用 `07282310dd70fee4ca2dfdb37c545acc`，对 LEFT 使用 `371e496e1cd371144abfec472e72d9a9`；两个 SoundDef 各有 4 个等权音频、0.7–1.0 随机音调与 0.4 音量。复刻接入了方向对应的四个等权音频、0.4 音量和随机音调。
- `PageTurn` 初始化时从 `Box` 获取外框及底栏用于离屏渲染；这个离屏相机服务翻页贴图，不能认定为收藏主相机。原 prefab 的左右动画速度为 2 和 1.6，左翻页定位延迟 0.44 秒；`PageTurnLeft` / `PageTurnRight` 各有 8 条位置、旋转和缩放曲线，复刻版已接入网格及曲线，原版离屏流程仍未完整移植。
- `Box.TranslateSceneModeToBoxState` 对收藏模式走默认 `OPEN`，`Box.InitializeStateConfigs` 将其相机状态设为 `OPENED`；PC 分支使用 `CameraOpened` 的世界位置。已从原 TheBox 场景取该骨骼旋转、位置以及相机视场和裁剪面，并在复刻版使用原始收藏 prefab 的局部变换及 TheBox 外框、底栏。手机分支是宽高比相关的另一条路径，本次没有将它当作 PC 分支。
- `PageTurnFront` 和 `PageTurnBack` 是共用九根骨骼的蒙皮网格；两段动画各有八组位置、旋转、缩放曲线。`python -m tools.audit_collection_page_turn` 输出真实骨骼顺序、曲线及来源哈希到本机研究目录。片段末键分别为 0.667 秒和 0.467 秒，不能直接等同播放时长：原逻辑还应用左右不同速度并依赖离屏页面捕获。
- 编辑套牌增加卡牌涉及 `DeckTrayCardListContent.AddCard`、规则和游客数量检查；本次只记录静态调用，未重建完整规则和运行时事件顺序。

## 未完成与下一步

- Editor 回放中，原版 PC 透视相机与 TheBox 木框恢复了页面周围背景；现有材质仍有兼容重建，尚未作官方逐帧对比，也未实际验证其他窗口比例。
- 跟踪 `CollectionInputMgr`、筛选按钮事件绑定、页签和 `BookPageManager` 的翻页动画与音源引用；逐项关联 prefab 的序列化字段。需在目标版本下单独验证，不能用静态调用等同于运行时观察。
- 筛选托盘使用从隐藏骨骼左向滑入的位移方向、原方法的 0.35/0.25 秒和 easeOutCubic；现有列表控件的宽度与位移距离是复刻版重建。翻页音效补入 0.7–1.0 随机音调，分别使用独立音源避免改变其他声音。
- 2026-11-04 F032：复刻版的翻页按钮已接入源蒙皮、九骨骼、左右位置/旋转/缩放曲线及原播放速度；用 GPU 内存截图捕获当前页面并贴在变形网格上，期间阻止重复操作，结束后释放临时贴图。UI 截取和页面裁剪是兼容重建，源 `Custom/PageTurn` 着色器与原离屏流程未完整移植，仍需原版逐帧比对和实际 Player 验收。资源不存在或捕获失败时直接切页。
- 重新生成本机翻页资源：`.venv/Scripts/python.exe -X utf8 -m tools.audit_collection_page_turn --publish-runtime`；原游戏安装只读，资源生成在忽略的本机 `local/runtime`，不向 Git 提交。
- 验证：`refs/verification/f032-r8` 的 Editor GameView 回放通过，39 次成功请求、1 次预期拒绝，实际采集左右翻页图并确认页码和动画结束；`f032-gpu-r5` 检查源骨骼两方向的网格形变及资源缺失检测；62 项定向 Python 测试通过；`f032-build-r8` Unity 构建退出码 0。回放未验证扬声器实际听感、原版视觉或独立 Player 输入。
