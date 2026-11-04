# 原高光动态轮廓链路

后续原全局ShaderTime时间/gyro与关闭行为已恢复并在客户端接入，见 [ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。以下保留轮廓节点当时的验证范围。

2026-10-11。全卡专属动画、完整原 Shader／脚本未完成；本节点不宣称正式战场高光已启用。

原 HighlightRender 普通 Actor 路径现能从实际 Mesh 生成动态轮廓，再使用上一节点的源 Highlight 材质渲染。此轮恢复三个采样/合成 Shader：MultiSample 的四角红通道平均、MultiSampleBlend 与中心 BlendTex 红通道取 max、MaskBlend 四角差乘0.3并 saturate，输出均复制到RGBA。保留源TEXCOORD1输入和MainTex的texel偏移，额外UV0不能代替该通道；NativeHighlightFilters用显式UV1全屏Mesh绘制，不依赖Built-in Blit是否提供第二UV。

五个常量替换 Shader 在官方 `Hearthstone_Data/resources.assets` 内找到，不能只扫描外部Bundle。原 Grey/LightGrey/DarkGrey/White/BlackOverlay 分别输出0.5/0.75/0.25/1/0灰度和alpha1；Cull Off、ZWrite Off、ZTest Always、One/Zero已按源状态保留。两个灰度Shader也在外部bundle找到同源版本。`python -X utf8 -m tools.highlight_filter_audit` 生成 `manifests/original-highlight-filters.json`，记录八个Shader的16个实际程序/参数SHA256、常量偏移、原状态及源HighlightRender程序集来源。

NativeHighlightSilhouette恢复根对象世界缩放/旋转归一化、Highlight标签对应子Mesh筛选、原视图/正交矩阵、最长256像素比例、16位深度/R8目标；按原IL执行0.3/0.5/1/0.92四种分辨率的五次灰度绘制与五次filter合成，偏移/blur强度保留。生成后用finally恢复根的parent/localPosition/localRotation/localScale并释放临时RT。原render/clip比例当前为源模板的1；premium/override mesh、include/exclude特殊渲染和完整可见性缓存未恢复。正常对局尚未替换旧高光。

## 普通随从原状态分支

进一步读取源GameObject组件证明：Highlight_Card_Plane只有Transform、MeshFilter、MeshRenderer、HighlightRender，没有PlayMakerFSM。TryInitRenderers是在这个平面上取FSM；普通模板因此走ChangeState的无FSM分支，不能把Birth/Idle/Death当作该模板必需转场。Awake由父级ActorStateMgr把序列化highlightType=0解析为1。上述组件、父脚本、实际类型与序列化HistoryTranslation进入来源清单。

NativeHighlightState恢复这一分支：Setup随机seed/idle隐藏；相同状态直接返回；0/1/29隐藏；匹配状态复制原材质、绑定seed和动态mask/队列、应用原offset并按Hide控制显示；历史状态28使用序列化(0,-0.25,0)，不能误用构造器(0,-0.1,0)；未知状态返回false并重置idle。Hide保留期望可见状态，Show只恢复已请求的高光。释放时恢复旧材质、激活/可见状态和位置。控制器尚未接入正式交互，完整原全局ShaderTime和特殊轮廓服务未完成；没有添加假想的普通随从FSM或淡入动画。

实际渲染揭示此前高光平面Mesh缺少第二UV，且UNorm8顶点红色255误作浮点255，造成过曝白圈。`tools.actor_highlight`现从源MeshVertexData format=2读取并归一化颜色、导出第二UV，源Mesh/CAB/PathID和顶点数进入状态来源记录。修复只作用于这张原高光平面，不用猜测所有旧Mesh的颜色格式，也不修改用户校准。

## 验证与截图

NativeHighlightSilhouetteChecks通过837项GPU检查：三种filter×四种blur×64像素（768项），非一致UV、不同尺寸BlendTex、超1浮点红值和RGBA饱和验证；三个真实卡牌各验证原高光Mesh的UV1/UNorm8、根变换恢复及非矩形轮廓（12项），并覆盖无FSM状态切换/隐藏/历史位移/同状态/未知状态/资源恢复（57项）。基于实际原Actor、源flare/颜色和生成的轮廓保存12图：三个mask以及各自白/绿/黄状态，目录 `tests/visual/current/native-highlight-silhouette/`。

CS2_182-state-6.png为实际离屏原资源验证图，**不是正式战场截图，也不是原版截图对比**。mask诊断PNG来自R8读回，Shader只读取红通道，不能把PNG的G/B默认读回颜色当作界面颜色。已目视检查修复前过曝白圈及修复后绿色轮廓，完整原场景逐帧比对仍未完成。

有FSM的其他模板的Birth/Idle/Death、全局ShaderTime服务和正式手牌/英雄/随从交互仍需接入。普通模板的无FSM状态切换已恢复并独立验证。旧正常对局高光仍保留；此节点验证原生成链路，没有用自己画的椭圆轮廓冒充原实现。

705项Python测试通过（47.37秒）。完整GPU回归通过：837轮廓/状态、77高光材质、43原画/边框、19静态与动态比较、105双方召唤；iTween798帧零异常。日志 `work/native-highlight-state-final-gpu.log`。Windows构建 `work/build-native-highlight-state.log` 成功；原运行时78172检查保持通过。正式启用专属法术程序仍2个，不把普通召唤或高光检查计作全卡专属效果完成。

本节点第一次Windows双客户端回归：`silhouetteactor_`，17回合/v52，Shrink2术士17生命胜，Shrink1法师0生命负；match_id `55cb1ba0638e491b8cf0e2a92673f874`，公开状态哈希双方相同 `00901bbab2473452777bf4d134700a6fa0ef0f1169e9b8a4eeb05ae8de4d1b3c`。背景Player不绘制完整GUI，不能用于宣称完整GUI 60FPS或人工视觉验收。

重建状态控制器后的最终完整回归：`statefinal_`，10回合/v36，Shrink2术士14生命胜，Shrink1法师-2生命负；match_id `53435f646e994ea9986ee49c9af4ea40`，公开哈希双方一致 `f0f91a6628917f320d370219169f1abafbf924ac4481e76147cf599fda5bf99d`，队列峰值1。中间`stateactor_`的性能JSON损坏、测试进程已不在，未将其计为有效最终验收；确认进程停止后使用新端口/新Profile完成上述重测。
