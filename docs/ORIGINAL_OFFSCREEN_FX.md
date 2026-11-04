# 原 OffScreenRenderFX 捕获

2026-10-12。完整全卡原专属动画、Shader/脚本未完成。

## 真实组件与来源

原神圣启动Prefab ReuseFX_Holy_Idle_01 使用 OffScreenRenderFX，不是字段以m_开头的旧RenderToTexture。旧导出器只识别后一种，因此缺失神圣图的捕获链。原MonoScript/typetree及静态IL已核对，未执行官方程序集。

export_program 增加 offscreen_fx 原配置导出，记录源CAB/PathID/Bundle与完整raw，按实际组件类名识别。目标为owner节点9 RenderPlane，源为节点2 HolyOffscreen；源分辨率256×256，AboveClip/BelowClip均1，UseBounds=true、ForceSize=0、CameraRect覆盖整图。启动Prefab已通过既有prefabs-only入口重导，普通已审核效果cache未改变。

## 恢复的执行

按原Start行为为源物体加全局X250及Y250起每实例加10的偏移。正交size使用owner.localScale.x × Mesh.bounds.size.x /2，ForceSize非零时直接使用原值。相机从源物体姿态旋转局部(90,180,0)，保持相对于owner的姿态；原裁剪为-AboveClip到BelowClip，黑色清屏、不启用HDR、原CameraRect。24位深度的临时纹理绑定owner的mainTexture。相机由客户端捕获宿主手动驱动，不注册原UniversalInputManager，也不冒充原全部场景管理。

图像捕获、Glow与Affine入口均更新原offscreen纹理。重建时重新应用一次偏移，不重复累积；释放实例时释放自有相机及临时RenderTexture。隔离场景用本项目层30，完整原分层/IgnoreLayer21/全局相机服务未恢复。

每个正常启用的原组件创建一次，禁用组件不绑定/分配新捕获。原运行时后续启用脚本的完整生命周期仍未恢复。

## 实际问题与验证

最初命令缓冲捕获黑屏，换实际相机后仍无图。用诊断Cube区分镜头/粒子问题后查出JSON x/y/width/height无法直接读入Unity Rect内部字段，造成零视口；改为显式viewport DTO转换，检查viewport宽高均1。诊断Cube仅用于定位，最终测试已移除，没有用其替代原效果。

work/native-offscreen-fx-final.log：10新增Unity检查与所有既有链通过，退出0。覆盖原尺寸/mesh bounds/纹理绑定/源隔离/视口/真实捕获与合成可见、采样与重建偏移不重复。

原源纹理 source.png 现在显示完整放射状粒子；composed.png显示真正由原源纹理组成的外层效果。两图位于 tests/visual/current/native-offscreen-fx/，已查看。颜色仍红，目标原画与原神圣光芒/Shader仍未恢复，不作为整卡视觉一致性通过。

Python常规组1171 passed/4 deselected/19 warnings/124.96秒，新增原OffScreenRenderFX字段/指针/组件身份核对通过；排除四项此前通过且专属来源恢复函数未改的全层级定位/全部区域/启动/RGBA重读。未宣称本次全量同命令绿色。

## 未完成

旧RenderToTexture的25属性、TrailRenderer70属性、原完整Shader/颜色/目标Card、全局服务、原完整帧序与正式全卡播放仍未完成。本节点没有因捕获可见就开启更多卡牌；正式审核启用仍2。

没有ComputerUse，官方安装只读，用户UI校准未改。

后续恢复了原 FX_HolyRender 的 R/G 金色合成，先前红色源纹理是内部通道数据。最终图已变金色，详见 [ORIGINAL_HOLY_SHADER.md](ORIGINAL_HOLY_SHADER.md)，完整整卡仍未完成。

work/native-offscreen-fx-gpu.log的41标准GPU检查通过，退出0；work/build-native-offscreen-fx.log Windows构建退出0，实际Mixer九编译常量一致。UI校准SHA256为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，保持不变。

最终构建后台offscreen_fx_1/2在47448完成5dbd9e0b0b6f45d8aa5e0d530bcfbf5a，16回合/version54。Shrink2法师12血胜、Shrink1法师-2血负，两端public_hash均2b17c8e2251eb09b194831ded119b20177bc0858fb2845c3326764623bdfbdc1，日志无Exception/fallback/Error。终局命令行核对后仅清理自有28360/37816/8024。后台不绘制GUI，不证明原神圣效果在正式比赛已完成。
