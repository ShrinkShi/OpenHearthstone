# 原网格和材质替换

2026-10-11：恢复四个源 Prefab 内的 18 个 SetMesh / SetMaterial 动作。全卡完整专属动画、完整原 Shader / 脚本仍未完成。

## 来源与提取

只读 Cecil 检查 SetMesh、SetMaterial 和 RendererExtension；程序集哈希与调用在 manifests/original-script-audit.json，IL 留在 local/script-audit，未执行官方程序集。

`python -m tools.native_geometry` 按原 FsmObjectParams 类型导出 Mesh / Material，保留外部引用的 Bundle、CAB、PathID。首次刷新四缓存，重复零更新、零失败、零未解析参数。来源和元数据见 manifests/native-geometry-assets.json、native-geometry-refresh.json。原网格、贴图只留在 local/runtime。

两个 FromCorpse 子图 2ac223d9b583771c9eb24338 / b0cbd0aa93fedbfc28ee3f8e 各有四个网格及四个材质替换；93313b7c2aed67a49667c4c4 / 96d6edb199ef520b5b3430f4 各有一个材质替换。

四原网格来自 essential_base_global-mesh-0.unity3d、CAB-66e7137de3b79380e05eb711ae004556，PathID 为 -1115502772572045766、-4578371029689156038、-808494164974354886、3685253907496844858。没有以默认网格代替。两原材质的定义及贴图引用在清单中。

## 源语义与生命周期

SetMesh 在 OwnerDefault 对象上查找 Renderer 和 MeshFilter，再设置 mesh，不搜索父子对象；空网格清空过滤器。SetMaterial 零槽使用 renderer.material，其他槽先获取实例化 materials，再设置指定槽。越界槽不改动，负槽保留异常语义。

NativeGeometryBindings 保存借用网格和材质数组，倒放、重播、销毁时恢复；实例化材质由客户端登记并释放。

新材质 FX/CommonFX 的 RGBR + Ramp 分支依据原 VS 118 / PS 1206 恢复，透明度和预乘下限使用红通道。保存的 _LookupTex 被绑定，但该 Ramp 字节码只采样 _MainTex。精确关键词及程序/参数哈希见 manifests/native-commonfx-variants.json。现已恢复三个 CommonFX 基础变体，不能称全部分支。

## 验证与限制

全量 Python 779 项通过；实际 Unity 网格/材质检查 73 项通过，覆盖原指针、空值、槽位、纹理绑定、倒放恢复及重播。原 Shader GPU 通道检查 236 项通过，其中新增三组冻结像素样本验证 RGBR。完整 Actor GPU 回归和 Windows 构建均退出 0：work/native-geometry-all-gpu.log、native-geometry-shader-gpu.log、build-native-geometry.log。

这18个源动作使用固定 Mesh / Material 参数；动态对象变量仍明确报告未绑定。原 IMaterialService 注册、停止、池化等行为未完整恢复，客户端资源管理不是该服务的完整复刻。源子图动作测试不是整套入场流程或整卡视觉一致验收。

完整法术程序启用数仍为2，整套RLK入场仍未启用。本轮没有操作桌面或修改官方安装；用户UI校准保持原哈希。

## 双客户端回归

47416端口、geometry_1/2独立后台Player完成比赛7fa76d9f24ae433b94f4dabb8698898a：12回合、version40，Shrink2术士4血获胜，Shrink1法师-3血失败。两端公共hash均为0b6f6da9ba371bd037ffc7188697cb43faa84fb18299507a653b24abe1eee387，队列峰值1，日志无异常或fallback。card_render_count=0，不能用该回归宣称GUI的60FPS、人工比赛或新完整入场效果实战验收。自有PIDs 27292/468/30504已清理。

UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。构建生成的场景ID已归一至历史值，未重写架构或规则。
