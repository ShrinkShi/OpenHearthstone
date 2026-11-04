# 资产审计

2026-10-07：官方安装存在，`Data/Win` 包含现代 UnityFS、基础与 zhcn Manifest；`Data/hsb` 是独立索引存储。Player 还包含 resources.assets、globalgamemanagers、level 文件，扫描不能遗漏它们。

官方 UnityPlayer 版本 6000.3.11.3145967。AssetStudioGUI 0.16.0 可启动；pythonnet 调用同版本 AssetsManager 加载现代菜单纹理样本得到 0 files / 0 objects，同一样本 UnityPy 可以读取，因此采用脚本管线作为长期方案。该结论仅针对实测样本，不声称所有 AssetStudio 格式都失效。

2026-10-08 全量元数据索引：5,137 个来源文件成功解析，无失败 bundle，当前索引没有对象元数据读取错误。来源包括 5,072 个 Data/Win bundle 和 65 个 Player 资源文件；未进行全客户端资源导出。

| 类型 | 数量 |
|---|---:|
| Texture2D | 70,227 |
| Sprite | 910 |
| AudioClip | 71,883 |
| Material | 91,485 |
| Shader | 2,532 |
| Mesh | 22,590 |
| GameObject | 888,678 |
| AnimationClip | 12,177 |
| Animator | 10,407 |
| ParticleSystem | 143,751 |
| TextAsset | 3 |
| Font | 14 |
| AssetBundle | 5,072 |

Prefab 在本安装中表现为 GameObject/Transform/组件层级，不应虚构一个额外 Prefab 对象计数。Scene 也以 Player level 文件与组件组织，不与 GameObject 重复计数。完整类型、来源、PathID、GUID、语言和纹理/音频参数保存在本地 SQLite 与 `local/asset-audit.json`。

按需集合已导出菜单 226 个层级节点、64 个网格，荆棘谷棋盘 124 个节点、33 个网格，以及卡牌、英雄、技能、宝石、卡背、卡框图集、BGM 和基础音效。棋盘新计数包含两项 SkinnedMeshRenderer 静态几何。最近一次导出记在 `local/runtime/export-report.json`，新增的连续审计记录写入 `export-history.jsonl`；首次菜单导出的默认 Unity 内置 Popup/PopupShadow 网格指针仍未恢复，后续声音导出零错误不代表该问题已解决。

事件声音沿 CardDef GUID 按需提取；十一职业预设此次为 297 条、零错误，合并额外测试牌后共 304 条。报告保留语言、CAB、PathID、SoundDef 引用与静态时间证据。并未导出全部 71,883 段客户端音频。
