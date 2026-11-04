# 原场景相机数据与效果捕获入口

2026-10-12，接续 aa07d0d。全卡专属动画、完整 Shader/脚本未完成；正式审核效果仍两项。

`python -m tools.battle_camera_source` 只读官方安装 @player/level30 中四个 Camera：Main Camera、TooltipCamera、MainFullscreenFXCamera、IgnoreFullScreenEffectsCamera。数据包含 CAB/PathID/文件、全部原 Camera typetree、逐级 Transform 身份及原位置/旋转/缩放。记录为 manifests/original-battle-cameras.json，运行缓存为 local/runtime/original-battle-cameras.json；不执行原程序集，不改变官方安装。

主相机的序列化投影 mode1、透视 FOV 34.870445251464844、near1/far200，世界位置 (1092.5,32,6.099999904632568)，原旋转 quaternion (0.6870788335800171,0,0,0.7265828847885132)。其他三相机有自己的 clip/fov，继承原主相机 Transform 链。没有把正交 size100 当作透视画面的尺度。

NativeSourceCamera 按原 TRS 父链计算世界位置，按父链旋转计算世界朝向，设置普通投影/裁剪及输出 aspect。源与目标坐标原点由宿主明确提供；捕获 pass 继续拥有层过滤、背景与目标纹理。原物理/自定义投影模式尚未恢复，明确拒绝而非当作普通透视。绑定同时清除旧自定义投影与旧自定义 view 矩阵。

NativeEffectInstance.BindCaptureView 是真实原 Prefab 捕获视图入口，只允许播放前绑定。相机从 Spell/Actor 层级分离，保持场景世界位置；实例销毁显式释放独立自有相机，未销毁外部 Timeline/Main Camera。正式 NativeEffectPlayer 尚未自动使用它，原场景 Board 原点、实体坐标、运行时 viewport/FOV 适配、分相机层及完整 postprocessing 尚未恢复。源相机的静态配置不能冒充原游戏运行时最终视图。

GPU 截图 tests/visual/current/native-camera/arcane-perspective.png 使用真实原奥术爆炸图及相机姿态/FOV。为了隔离投影验证，诊断原点取原光轴前方32单位的点，使效果处于视图中心；该点不是已经恢复的原战场中心，截图也不是官方同状态参考或正式整局 UI。没有把原截图作为背景，没有修改用户 UI 校准。

原数据 Python 重读核对 1 项通过；work/native-source-camera-owned.log 20 项新增 Unity 检查通过，退出 0：四相机父链、真实参数、相机层保留、光轴/边界投影、当地 Unity mode1 的序列化映射、旧矩阵清除、真实原效果可见、播放后绑定拒绝、镜头不跟随 Spell 移动及独立相机释放。既有 11 定位、460 曲线及所有原动画/召唤检查链一起通过。work/build-native-source-camera-final.log Windows 构建退出 0；Mixer 九个实际编译常量匹配。

Python 复测 1167 passed/1 deselected/16 warnings/124.68 秒，跳过的唯一项是此前完整完成的 254 程序/715 组件来源核对；其代码和原定位缓存未改变。当前唯一 1168 项覆盖通过，仍按来源检查与常规组分开记录，不冒充同一次全量绿色。

后台 source_camera_1/2 在端口 47441 完成 f801f0e2d34b40e3b6f7385689332972：13 回合/version43，Shrink2 术士 19 血胜、Shrink1 法师 0 血负；两端 hash 均 bde087dbdb84604f165d13fb2ccf8e47d0f3085275cbf9d339ca57ea260edd36。日志无 Exception/fallback/Error，终局核对后清理自有 PID 26040/31728/32904。相机入口在正常客户端默认未开启；GUI card_render_count=0，不能作为原相机在正式比赛正常播放或 FPS 验收。

UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

未使用 Computer Use。完整目标继续包含子 Spell 路由、原战场/相机合成和全部专属 Shader/脚本，不能因新增捕获入口而扩大启用名单。
