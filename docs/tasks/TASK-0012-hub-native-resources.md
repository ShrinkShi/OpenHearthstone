# TASK-0012

2026-10-31：F026子任务0013独立V026 b7830d0有界通过，原生齿轮/钱包/好友/选项、红纸重抽与购包九切片来源归档。开包Repeat源设置已确认，局部neutral及木框裁切属于静态兼容，原灯光/SH与完整控制器仍Unknown；整0012保持IN_PROGRESS。固定0f379fa/DLL4255CA07及失败/最终实图见F026与V026报告。

F025最终ac95ced已构建复图：限定Shop Sprite退化UV恢复、16包-Y/-Z轴、PC木框/紫底和旧3D乘法阴影处理，收藏原Frame_PC棕木框与白纸完成态、原静态重复控件/题签已清理。GUI-r7与同DLL Windows direct-r7通过，独立V025 98035d3/bbbd950已核准本轮有界范围；来源、失败、像素对照和Unknown见F025/V025。原Frame_PC是派生复用，不冒称旧Collection原Camera；完整旧版保真整体IN_PROGRESS。

2026-10-30用户以原版实图指出商店包倒置/黑边、收藏背景和构图错误，F025退修IN_PROGRESS。优先校正原网格/相机与页面坐标组合，恢复已导出的背景与托盘，禁止截图铺底；候选须实际复图再独立V025。见`docs/verification/F025-hub-reference-corrections.md`。

PC收藏、开包、商店、任务原生界面资源。IN_PROGRESS，F024，2026-10-29更新。

r24/6dcbfab最终可测试版已实际复图：源PackCount/ShopNotice/托盘根Renderer清理后白块/空badge/宽条消失，开包/收藏入口仍走正常按钮；纸页内八卡完整、深棕规则字清楚，未改用户校准。Editor GUI-r7和Standalone direct-r9均有当前DLL绑定。原资源接入与具体显示修正有界核准，原版Collection源相机、完整Shader/动态及旧PC精确构图仍开放，不能将本地可用提升为完整保真DONE。

r22实际Editor GUI-r5与独立V024确认原mesh/UV页面可以与完整账号功能连用。主菜单原托盘默认inactive，现显式激活；不支持的Shop_Coin_shadow Multiply附属阴影已按具体节点屏蔽，收藏原Done/Crafting控件与GUI重复已清理。新图仍露出PackCount_mesh计数白块/空badge、托盘根宽条，9f1f6d0新增可恢复的节点/Renderer清理，待r23复图。单页纸投影宽约659设计单位，原GUI四列817宽超页，Hub局部排版另修。来源、Shader与派生相机边界保留，不把用户参考截图铺背景，不把实际功能通过升为官方完整视觉。

为TASK-0011提供原资源，复用现有Importer/RuntimeExport/Registry/Unity渲染；不把用户截图当整屏产品背景，不把atlas整图当可直接显示成品。原版安装只读，精确原GameObject/mesh/UV/材质/Transform/camera及派生项有来源记录。原脚本不执行。共享local导出与Unity串行由root执行。

已索引起点：CollectionManager `8544bc9fa860abd6e2cec119` 位于cardasset_base_qualityhigh_cm_global-prefab-0.unity3d；PackOpening `decaf1d75932344b990a5902` 位于initial_base_qualityhigh_global-9a595510-prefab-0.unity3d；普通QuestLog `969742ecd0ac7380df8ab36c` 位于essential_base_qualityhigh_global-content-0.unity3d。PackOpeningTray、SidePackOpeningTray、QuestBoard_Atlas、QuestTile_Daily/Weekly、questReroll和多扩展Pack贴图已索引。上述只是候选名，未凭名字认定最终外观。

F024 Battle Agent完成0009实现后接续研究/导出工具源码；Hub Agent负责实际页面与新NativeHubScene渲染桥接，避免同文件。视觉须以实际新Player验证，原Scene缺失/现代客户端与旧参考差异明确保留。主界面四按钮/收藏/开包/商店/任务都能操作后再独立V024，不提前宣称完整还原。

现已串行导出9Scene+3state variant及16包模型、币/尘，错误0；23包面研究来源保留但只16真包进入商店。实际GPU-r1/r2及Player-r4核实了mesh/UV组合；去掉默认hover/glow、教程箭头、无纹理DepthReserve裁切面和未布局DeckContentsBackground，不把整atlas当背景。通用金属按钮取原DoneButton的Bracket/Button/UV/材质，文字与命中区由现有GUI处理；source相机按bounds派生，原相机未知不声称恢复。97e0e71修源按钮几何ratio取景后铺到目标Rect，缓存normal/hover避免逐按钮重复RT分配。

实际direct-r4截图发现切页首帧因activeScene晚更新而回退整atlas，58f3ae9已改显式Draw同步ActivateScene，HubPages也禁止Collection裸atlas fallback；该修复待下个实际新Player截图复验。页面完整旧PC视觉及交互验收仍IN_PROGRESS。
