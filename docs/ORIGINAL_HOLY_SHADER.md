# 原神圣捕获合成 Shader

2026-10-12。全卡原专属动画、完整原 Shader/脚本未完成。

## 来源与恢复

ReuseFX_Holy_Idle_01 的内部 GodRays 原材质确实保存了红色参数；该纹理不是最终可见颜色。RenderPlane 使用 Custom/FX/FX_HolyRender，把捕获 R/G 通道解码为两个原材质金色。此前兼容路径错误地直接乘 RGBA tint，造成最终红色。

从 essential_base_global-shader-0.unity3d、CAB-c394926ba464822396a05cbfae08a8b3、PathID -1376513395945010790 读取两个 D3D 编译阶段与参数记录。只静态反汇编，不执行官方程序集，不修改官方安装。

原顶点程序使用 MainTex ST，不读取顶点颜色。原像素公式：

```
RGB = (2*R*Color.rgb*Color.a + G*Color2.rgb*Color2.a)*Intensity*Alpha
A   = 2*R*Intensity*Alpha*Alpha
```

纹理 B/A、序列化 Clip 不参与此程序；无 saturation。恢复为 NativeEffect family 20，按原材质名称绑定，不改原 GodRays 颜色。玩家 UI 校准未改。

`python -m tools.holy_shader_audit` 可重复生成两个来源 manifest，覆盖 27 缓存程序、8 独立材质，逐一回读官方 Shader 指针及编译阶段/参数哈希；`tests/test_native_holy_shader.py` 重放这些关联。并非 27 张卡牌已完成原版动画。

## 验证与限制

work/native-holy-shader-gpu.log：268 Shader GPU 通道检查通过，含新增 12 个通道检查；56 原 Additive 检查也通过，退出 0。检查 R/G 解码、忽略顶点颜色和捕获 alpha、Clip 未使用及 Alpha 为零的行为。

实际原 Prefab 的 OffScreenRenderFX 捕获与最终合成已生成 `tests/visual/current/native-offscreen-fx/source.png`、`composed.png`；内部数据仍红，合成已为金色光芒。该图是独立真实 Prefab 捕获，不是完整原卡牌/战场视觉验收。目标英雄原画、HeroMask、VertexGlow、原世界光照与完整源场景未恢复；正式审核启用仍只有既有两项。

Python 常规组 1172 passed、4 deselected、20 warnings、156.69 秒。四项此前专属来源重读未修改，未在本命令重跑：全部效果定位、全部区域、全部启动配置和全部 RGBA 参数。

work/build-native-holy.log Windows 构建退出 0。原 Mixer 九项编译常量核对一致。无 Computer Use。

work/native-holy-compose-final.log：12 个实际 OffScreenRenderFX 检查及既有执行链通过，退出0；新增实际材质 family20 绑定及合成金色检查。work/native-holy-effects-gpu.log：41 项效果 GPU 检查通过，退出0。

最终构建后台 holy_shader_1/2 在47449完成比赛 f1882d394b814d4ea599fb7bd7c9c193，15回合/version52。Shrink2法师17血胜、Shrink1法师-1血负；两端 public_hash 均4638b877c70404f194997b833d104e868c009f1c780f7812369204f263c906f3。日志无 Exception/fallback/Error。核对命令行与终局后仅清理自有5204/22756/28688。后台不绘制GUI，不证明神圣效果正式比赛或人工操作已验收。

UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
