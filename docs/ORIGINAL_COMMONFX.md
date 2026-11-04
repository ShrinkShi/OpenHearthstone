# 原 CommonFX 编译变体与混合恢复

本节点继续全卡原动画/Shader/脚本目标，未完成全卡认证。没有使用 Computer Use，没有修改官方安装或用户校对。

## F025 页面与状态排错经验（2026-10-30）

- 出现整块近黑时先检查网格UV分布，再检查纹理、材质和光照。Shop两个Sprite的导出UV全0，中心像素恰好等于纹理左上暗色；只调亮不能恢复其紫色纹饰或木边。限定源节点/材质/完整裁切尺寸，在独立clone上按Sprite几何恢复UV并随页面销毁；不修改共享网格或全局色彩。包面正反与上下则由原网格、UV和相机front/up一起验证；本轮16个原包为-Y/-Z，7个派生映射不套用未知轴。
- 原组分激活后不等于还原排版。纸页、托盘、托盘内容背景的父子变换各异；R2背景越界压第四列，不能只用合并Renderer bounds再缩小GUI。按实际可见纸/托盘分别派生布局，并从同一相机映射回GUI坐标；原静态控件与新交互只保留一份。外框裁片派生须标为行为重建，不能声称原版相机/完整动态恢复。
- 协议snapshot:null不保证Unity运行时引用为null。R4实际JsonUtility日志为account_id空、revision0；旧session revision较高时会误进stale分支。注销必须按操作语义结束snapshot生命周期，未认证状态不能重排认证刷新；回归必须让实际JSON先过JsonUtility，再验证队列和迟到响应。不能只用手造null对象断言协议正确。
- 透明底板会暴露预设的中间态。BasePage的.552941灰Tint与同PageA纹理的inactive PageTurnFront白Tint均有原资源证据；静态完成态只派生这一个纸面实例，不全局调亮。Shop右侧窄黑带在背板拟合后仍存在，源Shop_Box_Shadow的Multiply贴图边缘×0.3与底色逐点匹配，最终只隐藏此旧3D组分。RGB很暗本身不能证明相机漏底，必须由原纹理/UV/混合或层级隔离解释。
- 每轮以固定源码、程序集和runtime元数据哈希绑定真实截图/输入收据。R1滚动截图后目标未刷新、R2注销等待、R3编译错误均保留；handler回放、Editor原生GameView输入和OS鼠标是不同证据。详见F025/V025记录。

## F023 可复用排错经验（2026-10-27）

- 表现依赖必须对应源图实际存在的阶段。盗贼 H03 是 source-only root、没有 parent Flow/Arrival；换武器同批的旧武器 Death 不得无条件依赖技能命中。旧代码真实 Editor 用例推进9秒仍 Busy/InputLocked/queue1；只解除被替换旧武器的错误依赖，保留 Death 的 fired/applied、声音与新武器召唤，不造虚假 Hit/Arrival，也不泛改法师/猎人。完整原对局hash/状态不能由最后一帧倒推；源回归与实际双端另记。
- 检查音效要看物理 AudioSource/Listener 和原波形，不能只看播放回调。源模板的3D音源若跟随暂存区，会距离听者约30000单位。用 Nodes 对象身份识别源克隆；共享可变 staging frame 随显式重锚更新，重父后仍正确，外部 Actor 保留原世界位置。隐藏视觉祖先不等于声音所有者自身停用；只按 owner.activeSelf 的源语义停音。H09 首次修正固定原点后仍出现额外10000单位偏差，独立 Listener 验收才找出，必须保留这次失败。
- 性能先分开生产播放和取证开销。H10 每帧完整 Supports 文件读取/反序列化造成热 prepare 中位约34ms，按播放器生命周期校验并缓存后约0.12ms。同龄重复 Seek 跳过前仍处理 source attack wait，不能截断真实延迟动作。PNG CaptureScreenshot 每张约100ms，需用无PNG但保留真实GUI/合法后续动作/Listener的 quiet 模式对照；目前 quiet 多数Update间隔约4ms，首次冷加载约0.26秒、少量未归因慢帧仍开放，不能把逻辑0.2秒门当实际帧流畅度。
- 原 Texture2D 不等于可直接画出的独立 sprite。VS_letters 是全不透明图集，直接整图渲染会带出暗红底和箭头杂区。f2887da尝试把两原字母1372个三角的UV并集当alpha mask，实际导出仍覆盖整图，已FAIL；3D模型侧/背面的UV也会采样图集，UV覆盖不等于屏幕轮廓。最终6b6a119/14c0d75按原三段Transform链、正面962三角、+Y视角X/Z正交投影与深度缓冲绘制透明图，并使用原材质UV/tint及TextureSettings的Bilinear/Repeat；UV有1.00384越界，不能静默Clamp。r8/r9实际Player中红底和箭头均消失，仍不宣称恢复原shader/灯光/VS_Shadow/动态Actor。失败图/来源保留于work/f023-vs-uv-union-r1。Atlas外框尺寸也不等于气泡主体尺寸，菜单须按alpha主体/英雄比例和真实稳定帧对照；前三行共用y锚，右第四追加，检查实际Artwork/HitRect，透明sprite矩形互相覆盖不等于可见内容重叠。原资源身份、程序回归、正式Player、官方逐帧仍是不同证据。

实际工件、失败历史和独立结论持续记录于 [F023](verification/F023-feedback-and-opening.md)、V023相关报告，不能仅据本经验段标记全职业完成。

## 原始证据

BT_035/175 的原主体材质使用 `FX/CommonFX`，不是上一轮已恢复的 Particle/AddMultiply 家族。原 Shader：`essential_base_global-shader-0.unity3d` / CAB-c394926ba464822396a05cbfae08a8b3 / PathID -402334618889270515。

原 `m_ValidKeywords` 确认两组具体分支：共同 `_COLORING_RAMP`、`_DISSOLVE_NONE`、`_SECONDTEXTURETYPE_RGBR`、`_TEXTURETYPE_RGBA`，分别加 `_SECONDTEXTURE_NOISEUV` 或 `_SECONDTEXTURE_MASK`。旧 `_CLAMPUV_ON` 只在 InvalidKeywords 中，不当作有效变体。原 pass/state 和参数记录关联见 [native-commonfx-variants.json](../manifests/native-commonfx-variants.json)。

| 路径 | 原 VS Blob | 原 PS Blob | 参数 Blob |
| --- | --- | --- | --- |
| NoiseUV 基础 | 126 | 1214 | PS 1154 |
| Mask 基础 | 139 | 1227 | PS 1158 |
| 共同 GlowTransparent | 2240 | 2242 | 按原阶段掩码查询 |

完整原编译 bytes/反汇编保留在忽略的 `local/compiled-shader-audit/FX_CommonFX--402334618889270515`，公开审计只保存身份与 SHA256。没有执行原程序集。

### 修正编译程序审计

新 Unity player blob 将类型 3/4 的参数记录放入同一个条目表。旧 UnityPy ShaderProgram 对参数块按代码结构读取，会越过条目边界，把后面的 DXBC 当作该参数记录的程序。因此首次报告的 2243 个程序/2146 DXBC 不可信。

新 `BoundedShaderProgram` 按原 offset/length 严格截取 Windows D3D 条目，参数块独立保存，保留 BlobIndex。正确结果为 **2110 个 GPU 程序/2110 个 DXBC + 133 个参数记录**。这是静态解析数量，不是恢复 2110 个程序。其他平台解析及多 segment 支持未完成；未假定所有历史 Shader 审计都已重新检查。同期 `Custom/FX/FX_AreaEffect_Render` 审计从旧 6/5 修正为 4 个 GPU 程序/4 个 DXBC。

## 实际恢复

两个基础分支按原 VS/PS 运算实现：

- VS 主/第二纹理 ST、UV swap；第二纹理按原 Time.z（2 倍秒数）滚动；原顶点色的高亮项与 `_AlphaMul` 同时影响 RGB 和 alpha。
- NoiseUV 按第二纹理 RG 转为 -1~1 后乘原 noise force，再位移主 UV；Mask 按第二纹理 R 与 `_Color2.a` 生成倍率。
- 原 RGB additive、premultiplied alpha floor、以绿色通道为输入的三点 ramp、原五倍过渡、RGB ColorMul 和原输出 alpha。未添加原程序中没有的 Intensity 倍率。
- 同一原 GlowTransparent VS/PS 与已恢复 Glow 程序一致，新增 CommonFX 绑定；Glow 使用原顶点色及未扭曲主 UV，忽略基础 pass 的 AlphaMul。输出独立 HDR 纹理，尚未完成原全局 Bloom/Glow 后处理。
- 原动态 Blend 引用 `_SrcBlendOutline`/`_DstBlendOutline`，旧导出拿到了占位 0/0。现按原材质/Shader 默认属性解析，本样本为 One/OneMinusSrcAlpha。属性缺失显式审计。

### H11 第二纹理 Alpha 溶解子集

只新增 H11 原生效果实际使用的两组精确 `FX/CommonFX` 关键词组合，不据此推断其他 CommonFX 变体已恢复：

- family 44：`_COLORING_NONE,_DISSOLVE_SECONDALPHA,_SECONDTEXTURETYPE_RGBR,_SECONDTEXTURE_MASK,_TEXTURETYPE_RGBA`；第二纹理取 R。
- family 45：`_COLORING_NONE,_DISSOLVE_SECONDALPHA,_SECONDTEXTURETYPE_RGBA,_SECONDTEXTURE_MASK,_TEXTURETYPE_RGBA`；第二纹理取 A。

证据来自原 Shader 同一个 VS（Blob 271/260，程序哈希相同）及两个精确 PS：RGBR `local/compiled-shader-audit/FX_CommonFX--402334618889270515/4-1359.asm.txt`、RGBA `.../4-1348.asm.txt`；VS 为 `.../4-271.asm.txt`。这些反汇编显示顶点 dissolve 值为 `max(1 - vertexAlpha - _IgnoreVertexAlpha, _MinAlpha*1.01) * (1 + 2*_DissolveSmoothness) - _DissolveSmoothness`，PS 再按对应 mask 通道计算 `f = pow(saturate(mask - (2*(vertexDissolve + _DissolveFactor) - 1)), 10*_DissolveSmoothness)`。主体 RGB、Alpha、颜色边缘按 DXBC 的 `ColorAdd`、premultiplied floor、`ColorMul`、`Color2`、`ColorMul2` 与 `COLOR1.a` 分别输出。H11 实际材质与节点可在 `local/runtime/40ac8d77701fdbccc4033483.native-effect.json` 核对；其余关键词组合遵循已有映射或兼容路径，未纳入本次新增恢复。

代码实现分别使用 family 44/45，并公开原有动画会写入的 `_DissolveFactor`、`_DissolveSmoothness`、`_MinAlpha`、`_IgnoreVertexAlpha` 与 `_ColorMul2` 属性。新增的实际材质映射断言及合成纹理 GPU 像素断言覆盖两种通道和溶解阈值；它们在本轮 Unity Editor 执行前不计入验证通过数。此项是对 H11 所用两个材质变体的有界恢复，**不是 CommonFX 全面恢复，也不代表 H11 已达到原版视觉等价**。

### H11 初始裂纹 ADD 子集

双端 Player 的 1920×1080 帧中，破土区域曾出现旋转的土色方块。来源是 `Mesh_Initial_Crack` 的精确关键词组合 `_COLORING_NONE,_DISSOLVE_NONE,_SECONDTEXTURETYPE_RGBR,_SECONDTEXTURE_ADD,_TEXTURETYPE_RGBA` 未匹配 family；通用回退忽略了 `_SecondTex` 和 `_AlphaMul`，且透明主贴图像素含隐藏土色 RGB。原材质使用 One / OneMinusSrcAlpha，因此 alpha=0 的 RGB 仍被加到画面。

原 shader 对应 VS blob 130、PS blob 1218（相同关键词组合）。family 46 仅映射该精确组合：VS 的 COLOR0 保留 `v.color * _Color`、高亮项与 `_AlphaMul` 公式，COLOR1 依 `v.color * _Color2`、`_ColorMul2` 生成第二层调制；PS 使用 `SecondTex.rgbx`（RGBR 的 alpha 来自 R），先以 `max(Main.a,_PremultipliedRGBTexture)` 预乘主 RGB，再加第二层并乘 COLOR0。H11 材质未启用 UV 旋转/交换/滚动，因此恢复路径与其实际参数一致。

验证：family46 精确映射与 1×1 GPU 像素断言覆盖第二层合成和透明 RGB 不泄漏；Unity `NativeEffectChecks` 为 80,546，`ORIGINAL SHADER GPU CHECKS` 为 308，Glow 120、Affine 491、Native GPU 41 均通过。1920×1080 双 Player 回放/analyzer 通过；新帧 `work/f020-h11-commonfx-add-r3-1920/driver/source-eruption-0.55s.png` 与 `.../source-eruption-1.0s.png` 中方形已消失，碎石、烟尘、Ghoul 与按钮青绿色特效都可见。此结果修复了方块来源并验证本地回放，不宣称已经通过逐帧官方画面对照。

通过 exact CAB/PathID 刷新 **51 个 Prefab、569 次材质引用、151 个唯一材质**，50 个缓存更新、0 失败。重复运行为 0 更新/0 失败。仅刷新原关键词与混合值，未重写几何、纹理、Clip、启用标记和用户布局。追踪见 [native-commonfx-refresh.json](../manifests/native-commonfx-refresh.json)。

两条基础分支在现有缓存匹配 **9 个效果槽**：BT_035、BT_175、BT_490、BT_512、BT_752、BT_801、BT_142、BT_351 的 TriggerEffectDefs:0、BT_495。槽内还可能有其他未恢复材质/动作，不能称 9 张牌完成；其他 78 组 CommonFX 关键词组合未恢复。

## 截图与差异

离屏真实原 Prefab 不是原版截图铺底。BT_035/175 亮像素从上一轮 308 增为 893，黑 alpha 从 808 降至 95，绿色主体轮廓开始出现。0.30 秒和 1.00 秒图已查看：

- `tests/visual/current/native-effects/BT_035-0.30.png`
- `tests/visual/current/native-effects/BT_035-1.00.png`

仍缺原 SpellSetMaterialTexture 等目标纹理/姿态绑定、完整生命周期、原动画/脚本、全部 CommonFX 关键词变体和原版连续帧认证，**继续禁用**。新的数值阈值通过不等于外观完成；没有新增完成卡数。BT_801 仍有 695 黑 alpha，继续禁用。139 次旧零轨道 Clip 尚未恢复。

## 验证与复现

```powershell
.\.venv\Scripts\python.exe -m tools.inspect_compiled_shaders --pattern '%CommonFX%' --card BT_035
.\.venv\Scripts\python.exe -m tools.commonfx_variant_audit
.\.venv\Scripts\python.exe -m tools.native_effects --refresh-commonfx-cache
.\.venv\Scripts\python.exe -m pytest -q
```

`work/native-commonfx-uv-math.log`：108 条原 Shader GPU 数值断言，含 40 条新增 CommonFX 检查；覆盖 RGBA、AlphaMul、premultiplied floor、Mask、无原 Intensity、两组 ST、NoiseUV、Time.z 和 swap。`work/native-commonfx-glow.log`：68 条 Glow GPU，含 8 条新增 CommonFX/AlphaMul 排除检查。原 Prefab GPU 为 26 条/66 帧，不等于全卡逐帧验收。

最终 Windows 构建 `work/build-native-commonfx-final.log` Result: Success，4101 条既有 C#、76978 条原数据/行为断言另计。新增 2 条真实原 Prefab 行为检查，确认原动画 AlphaMul 到达恢复程序、原关键词和 named Blend 到达材质。本文后续附最终 Python/对局结果。

最终 Python 全量 **651 项通过（18.71 秒）**，最后收窄 Windows 参数块识别后，8 条相关单测再次通过。测试范围包含表格越界/截断、参数记录不能跨条目读取、GPU 变体阶段 keyword mask/参数身份、named Blend 材质值与缺失审计。

端口 47338 第一次构建：11 回合/v36，MatchId 9c139c1a1cd847eabe1b35a795a10599，Shrink1 法师 0HP 败，Shrink2 术士 25HP 胜，公共哈希 3be688f6bb21a35f8372f9c184978eee213d65e3bd1c1168daacf7f3602d0650。

端口 47340 最终构建：16 回合/v52，MatchId 076c354419b14a329c0d5dd671ed5c4c，Shrink1 法师 -4HP 败，Shrink2 术士 22HP 胜，双方公共哈希 e0029945f5678e249b9dabf088eb4e26a9fa20ce87da17986f2c26211f03f3c6。队列峰值 1，日志无运行异常。都是后台 Windows 自动对局，不冒充人工整局或正式渲染性能。

`work/native-commonfx-final-prefab.log` 最终原 Prefab 检查 26 条/66 帧。BT_035/175 缓存 SHA256 5ce45e5662507fbe0c05bad13d3d24dde45b6c27e878fa83eaf427ed6b5b8064；奥术爆炸/地狱烈焰缓存身份保持原值，启用标记未改变。未重跑本轮未涉及的其他历史 GPU 套件，不把历史断言算本轮结果。

原 37240 服务器保留；测试进程按命令行核对后关闭。用户校对 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。下一步原目标纹理/FSM 绑定与旧零轨道 Clip，完整目标继续保持 active。

## 2026-10-26 F022 可复用的排查经验

- 先核对原 Mesh 通道格式，再判断 Shader 是否错误。H04 两个头像遮罩的 Color 是 UNorm8；缓存中的 0/255 直接赋给 float `Mesh.colors` 会产生实心过曝。当前只对已核实的 H04 实例 clone 除以255，共享 Mesh 不改；不能把这条修复盲目应用到全部资源。来源、GPU 前后图及 PPtr 见 `verification/F022-B1-H04-vertex-color-normalization.md`。
- 放置点与效果渲染必须用同一个源相机。H04/H07 旧通用正交视野跨度32，原 Main Camera 在棋盘面的跨度约20.1；前后不一致会让效果缩小约1.59倍。复用已有原相机和屏幕点投射，再测两视角像素误差；不要先叠经验缩放。
- 分开验证原节点、离屏 RT、最终合成和主相机。H11 的独立 layer30 被主相机再次看见，产生远离技能按钮的第二份效果。H04 layer21 白平面虽然需要隔离，但后续 GPU 证明它不是实心过曝的主因；假设必须随实际读回结果修正。
- 输入门、权威状态、可见尾效、实际声音结束各用自己的生命周期。首次资源加载的长帧会让理论 source age 早于实际声音结束；H11 因此改等原 token cue 的实际播放结束再回收，短输入门不随音尾延长。验收要在尾效仍活跃时真正提交合法动作，并检查最终双方 hash，不能只检查布尔解锁。
- 音频回调存在不代表混音存在。H05 缺少独立 Hero HealthSplat 原音层；补接后用实际 Listener PCM 验证。高频素材的低采样率相关性会误报，原48k双奇偶格搜索、重叠声源条件拟合与隔离 Player 的完整逐样本检查可互相印证；软件 Listener 不冒充设备录音。
- 保留阶段和失败工件。满血治疗要有基础 Impact，但不造 Heal/+0；召唤、护甲、声音等语义以真实 authority 事件和原 FSM 为准。先证明来源及单条实际链，再抽象给其它卡牌复用。

本轮最终构建、实际 Player 和独立复核事实集中于 `verification/F022-hero-feedback-and-speech.md`；本文经验不提升任何卡牌的官方逐帧兼容等级。
