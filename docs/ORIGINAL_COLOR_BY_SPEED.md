# 原 ColorBySpeed 粒子颜色

2026-10-11：恢复 7 个源 Prefab、12 个 ParticleSystem 的 ColorBySpeedModule。它使用 Unity 原生粒子模块，并非新写一个替代 Shader。完整全卡专属动画、原 Shader 和脚本仍未完成，正式完整法术程序启用数量仍为 2。

## 源数据和恢复范围

`manifests/native-color-speed-refresh.json` 保留每个节点的源 Bundle、CAB、PathID、原始模块及缓存映射。6 个节点为单渐变，6 个为双渐变；速度范围分别为增益效果 0~1、入场烟雾 0~4、弹幕效果 0~20。RGB/Alpha 键时间按原 ushort / 65535 还原，保留原渐变模式。本批源 colorSpace 均为 -1；不宣称恢复其他显式颜色空间分支。

| 缓存 ID | 原节点 | 用途 |
| --- | --- | --- |
| 0f0ec46ae18fad7f2fa1dad7 | 6、12 | Necrotic BuffImpact Small |
| 2ac223d9b583771c9eb24338 | 31 | FromCorpse Super Random 烟雾 |
| b0cbd0aa93fedbfc28ee3f8e | 31 | FromCorpse Super 烟雾 |
| 3b53c6edac0ba99e03be30be | 28、33、38 | Frost Flurry WithIdle |
| 74398d37bea0bf0116c207f0 | 28、33、38 | Frost Flurry NoIdle |
| 4d8519098584bdd2988a4511 | 17 | Decay Slash WithIdle |
| d554fc3ef71c90388d48ea9e | 23 | Blood Slash WithIdle |

`python -m tools.native_color_speed` 在资产锁内按精确源 PPtr 重新提取，只回填此模块并移除对应缺口，不覆盖 Timeline、Actor、材质或其他粒子参数。首次更新 7 个缓存，重复运行更新 0 个、失败 0 个。新导出由 native_effects 同时保留该字段。官方安装未修改。

## 验证和限制

746 项 Python 测试通过。NativeColorSpeedChecks 对实际生产 NativeEffectInstance 创建的粒子系统另建原始源字段引擎对照，检查 12 个节点、5 档速度的颜色及透明度，共 156 项通过。使用 SetParticles 初始化单粒子以隔离此模块，不把受控单粒子检查视为整卡视觉完成。

完整 GPU 回归通过，含 1034 项 Timeline、16 项原粒子时钟检查，以及 iTween 40 程序 / 188 动作 / 133 生命周期 / 798 帧，0 异常。离屏源图仍只显示很小效果，完整 RLK 入场仍未启用。

本批恢复前缓存中其余缺口为 CollisionModule 74、SubModule 32、TrailModule 2；本轮未恢复这些模块。原材质、Actor/Elite、CameraShaker 及其他脚本缺口继续沿源证据推进。没有桌面操作、人工比赛或 1080p/60FPS 验收。

Windows 构建成功（work/build-native-color-speed-final.log）。端口 47413、独立 colorspeed_1/2 Profile 后台自动比赛 ed38780a08e44d37a934bee60e16bd75 完成：第17回合、状态版本61，Shrink1法师2血获胜，Shrink2术士-3血失败，双方 public_hash 相同，队列峰值1、日志无异常。card_render_count=0，不用此数据宣称实际 GUI 帧率或新效果实战展示。测试自有进程已清理，用户 UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
