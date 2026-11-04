# 原 CameraShaker2 时间轴

2026-10-11：恢复 RLK_060 / RLK_061 两个嵌套入场子图的 CameraShaker2Track。完整全卡专属动画、原 Shader 和脚本仍未完成，整套 RLK 效果仍未启用。

## 静态源证据

官方 DLL 只由 Cecil 读取，不执行。`manifests/original-script-audit.json` 记录 CameraShaker2Asset / Behaviour / Track、ShakerBehaviour、ShakerHelper、TimelineEffectHelper 和 TimelineEffectBehaviour 的程序集哈希、方法及调用。IL 在忽略目录 local/script-audit。此轨不是旧 CameraShakeMgr。

原 CameraShaker2Behaviour 取得主相机；ShakerBehaviour 把 m_Amount、m_IntensityCurve、m_interval 传给 Helper。源 Helper 生成30个位置：首角度 Random.Range(0,6.283)，后续角度在上一个角度+1.745与+4.538之间；XY以cos/sin乘绝对幅度，Z按前值正负交替取范围。客户端使用 Unity.Random.Range，不换成另一套抖动噪声。具体随机序列依 Unity 全局状态，倒放不承诺逐像素重复同一个随机序列。

UpdateEffect 将时间/Duration夹到0~1，接近0或1时复位。中间采样的周期数是 Duration/interval，interval<=0时为500；小数相位经过两次余弦插值，循环访问30个位置，再乘原强度曲线。结果按目标right/up/forward加到原世界位置。Option 110 是 SCREEN_SHAKE_ENABLED；TimelineShakeEnabled 控制对应中间采样分支，端点仍复位。未新增设置菜单选项。

原 Helper 同类型实例共享原位置，并在 Kill/退出/销毁时复位；本适配保留重叠实例的原位置同步。不是累加多个独立抖动偏移。

## 精确源参数与集成

两个轨道均不静音，无 SceneBinding，目标是主相机。起点2/3秒、长度5/6秒；幅度各轴0.10000000149011612，间隔0.07500000298023224，7个强度曲线键。键值、斜率、权重、weightedMode和前后wrap保留在两个 original-timeline manifests 及运行缓存。

Timeline提取新增 shaker 字段；当前源无混合、外推、clip-in或变速。未知分支明确拒绝，未宣称支持所有版本。客户端 NativeEffectPlayer / NativeBattleSummons 绑定实际战斗 cameraView，表现实例不会用自身离屏特效相机代替它。无相机时记录 UnboundTimelineCameras；缺口与未知轨道分别记录。退出区间、倒放、重入及Dispose释放借用的相机姿态；服务器不等待动画。

## 验证与未完成项

757项Python测试通过；新增11项精确模板和不兼容分支检查。104项实际Unity检查包含独立源公式对照、旋转相机轴、负幅度取绝对值、正常/零/负interval、位置循环、禁用分支、端点、重叠、退出复位，以及两个原子图实际驱动绑定相机。全GPU回归通过：1034 Timeline、156 ColorBySpeed、16原粒子时钟；iTween 40程序/188动作/133生命周期/798帧，0异常。

两个源图的未静音Animation/ControlRandomized/CameraShaker2轨现可识别；这不等于整个原FSM或整卡完成。Actor/Elite、其他材质/Shader等缺口仍限制完整入场。源已静音的Activation/Sound轨没有擅自开启。未操作桌面，也没有本轮完整GUI人工截图或1080p/60FPS验收。

Windows构建 `work/build-native-timeline-shaker-final.log` 成功并退出0。端口47414、独立timelineshaker_1/2后台Player完成比赛8b99e344186f4f92a3bf9a4d4399a2cf：10回合、version32，Shrink1法师-3血失败、Shrink2术士20血获胜；双方public_hash一致，日志无异常/原效果fallback。完整GPU日志为 `work/native-timeline-shaker-final-gpu.log`。测试自有进程已清理；用户UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。batchmode的card_render_count=0，不据此宣称GUI性能或原动画人工验收。
