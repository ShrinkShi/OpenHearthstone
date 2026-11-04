# 原双层粒子与潜行烟雾 Shader

本节点恢复两个编译Shader分支及其静态纹理采样，不是完整全卡原效果、全部Shader或全部脚本完成，也不扩大现有整卡启用名单。

## 原程序来源与公式

只读提取工具为 `python -m tools.inspect_compiled_shaders --card EX1_129`。D3DDisassemble读取原DXBC字节，不执行原游戏程序。源编译程序、参数块、材质来源和Stage绑定分别记录于 native-particle-alpha-shader/source-bindings 与 native-sneaky-smoke-shader/source-bindings 清单。

`Hero/Particle/FX Particle Additive Alpha 2Layer`：35个效果程序引用、8个独立源材质绑定。VS应用MainTex ST，按原SH L0 gamma及主光颜色影响顶点RGB，保留顶点alpha。PS使用插值后的顶点红色R与alpha A：

```
noiseUV = (mainUV + R - A * (USpeed, VSpeed)) * Scale
RGB = Color.rgb
alpha = AlphaNoise(noiseUV).r * MainTex(mainUV).a * AlphaIntensity * A
```

原程序不读取时间、AlphaNoise ST、Color.a、主纹理RGB或噪声GBA，也不saturate输出。旧兼容路径按时间滚动主纹理并相乘，不等价。原混合为RGBA均SrcAlpha/One。

`Custom/FX/FX_SneakySmoke`：5个效果程序引用、2个独立源材质绑定。VS只乘顶点alpha与Color.a，不应用光照。PS为：

```
mask = MainTex(mainUV).r * vertexAlpha * Color.a
RGB = lerp(Color.rgb, SecondColor.rgb, mask) * Intensity
alpha = mask * Intensity
```

纹理GBA、顶点RGB、SecondColor.a和时间均未使用。插值不截断，超过1会外推。原RGBA混合均SrcAlpha/OneMinusSrcAlpha，与离屏仿射捕获的透明传输表示分开。

## 原纹理与采样

`python -m tools.smoke_source_textures` 恢复8张纹理、17个材质属性绑定；保留原RGBA32/DXT1/DXT5字节、完整mip链、颜色空间、filter、aniso、mip bias与UVW wrap。一张纹理有7级mip，全部源纹理为Repeat；原PNG路径统一Clamp且无mip，不等价。

AssetRegistry给这两类材质使用独立raw缓存，沿用已有高亮纹理的原始加载步骤，不改卡牌/UI的PNG路径。原材质ST仍按来源设置；噪声ST是否参与运算由原编译程序决定。缺少经过核对的原纹理时明确报错，不默默用PNG代替。动态脚本换图与全部其他Shader的源纹理采样尚未全面恢复。

## 验证与限制

新增潜行烟雾21项GPU检查、双层粒子58项GPU/纹理检查通过，日志 `work/native-smoke-raw-textures.log` 退出0。覆盖R/A坐标驱动、ST、时钟不参与、忽略字段、HDR/alpha不截断、原混合、真实Prefab材质绑定、8纹理原字节/mip/采样与缓存复用。首轮粒子红色坐标测试期望值写错，GPU实际.08与源公式一致，已修正期望并重跑；另一轮检查类名误用导致编译失败，改为现有NativeEffectIndex后通过，均未计为成功。

4项Python源验证通过（4.01秒），检查两个原Shader的程序/参数SHA256、源材质Stage身份、常量偏移、采样器、原混合，以及8纹理原数据和采样属性。原场景光照、排序、后处理、动态脚本、整卡连续对局及官方效果逐帧一致性仍未完成。

首轮常规1185项通过（400.51秒）发生在后续原纹理接入前，不能作为最终版本通过记录。最终回归、构建和双客户端结果见下方后续记录。原整卡审核仍2项。

没有Computer Use，未执行原DLL，官方安装只读，保留用户UI校准。

## 最终版本验收记录

`work/native-smoke-final-effects.log` 41项标准GPU效果检查、146帧采样通过，退出0。`work/build-native-smoke-final.log` Windows构建Result: Success，退出0。原混音bundle9项常量一致。用户UI校准SHA256仍为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

读取了实际离屏PNG：刀扇0.30秒与改前快照文件哈希一致，刀扇1.00秒只剩少量刀刃；刺骨0.30秒仍为白色中心效果。这些帧不能证明完整原版烟雾已在整卡状态机中按正确时机呈现，更不能视为整卡一致性通过。改前对照留在work/particle-alpha-before/，当前帧位于tests/visual/current/native-effects/。奥术爆炸/地狱烈焰采样仍分别max_pixels=57949/72915，black_alpha_pixels均0；正式审核依旧2项。

新构建 smoke_shader_1/2 在localhost:47470完成一局后台自动比赛：12回合/version47，Shrink1以7生命胜，Shrink2以-2生命败。双方match_id=5718fad00f3f4a6dbeeddc70889a2124，public_hash=8e2e26a432169b149c99f93753735a94fdfbc2624c05255bf8acef0348ffcfd0；两客户端日志未检出Exception/fallback/Error。后台不绘制正常战场，不能替代全卡视觉、人工或60FPS验收。

最终版本常规回归 `work/pytest-smoke-final.log`：1187 passed、4 deselected、33 warnings，385.83秒，退出0。四项大型定位/区域/启动/RGBA源重读未重跑。按PID和命令行仅清理本轮自建比赛进程。全部目标仍未完成，保留当前两项整卡审核，不因Shader源验证放开全卡效果。
