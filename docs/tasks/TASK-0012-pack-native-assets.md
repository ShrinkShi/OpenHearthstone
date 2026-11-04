# TASK-0012 子项：卡包与钱包原生资源

状态：READY_FOR_REVIEW。范围仅包括 `tools/export_pack_assets.py`、`manifests/pack-native-assets.json`、本测试及本记录；Unity 场景、页面和共享导出执行由主任务负责。

## 已完成

- manifest 保留 23 个源映射作为研究记录，其中 16 个是普通扩展包模型；NAXX/BRM/LOE/KARA（12/14/20/23）使用对应原冒险商店书籍模型派生展示，1403 是 Year of the Dragon 主题模型，1463 是恶魔猎手职业包，1869 是 Return of the Lich King 故事书主题展示。这 7 项均不是当前可售的普通卡包。
- 所有 23 个 GameObject、Mesh、Material、Texture 和金币/奥术之尘来源均以 asset ID、bundle、CAB、path ID、GUID（索引中无 GUID 的对象明确为 null）登记。只读比较 `local/asset-index.sqlite` 的 94 条引用后未发现差异；没有运行 Importer scan/export，也没有写共享 `local`。
- `tools.export_pack_assets` 以 `get_catalog().pack_sets` 为当前商店白名单，从 23 条研究映射中仅导出匹配项，并拒绝非 `official_pack_model` presentation。由此当前商店预期 16 个 set 时，运行时只生成 16 个 `MODEL_PACK_<set_id>` SceneData 和对应 `UI_PACK_<set_id>` 同路径 Registry alias；精确按资产 ID 导出 `UI_GOLD`（`large_coin_0` 游戏金币 Sprite）及 `UI_DUST`（`DustJar_Icon` 奥术之尘 Texture2D）。不使用真钱购买资产。
- 执行时会逐项核验现有索引中的 asset ID、bundle、CAB、path ID、名称、类型和可用 GUID；导出后再检查每个 `primary_node_path` 在 SceneData 中确实落到带 mesh 的节点。缺源或层级不符会写明错误、保存当前 registry/metadata 并以失败结束，不会静默跳过。
- 一次 `RuntimeExport.save()` 后，运行时视觉契约写入 `local/runtime/pack-visuals.json`。字段使用 snake_case，逐项给出 `presentation_kind`、`source_asset_id`、主 mesh 层级路径、排除节点、camera 建议和导出状态；Hub 可按 `primary_node_path` 过滤 SceneData。
- 场景建议正交 fit primary mesh bounds，并忽略源世界坐标；运行时 `front_axis` 为 `unknown`，并附 `front_axis_note` 要 Hub 按主 mesh UV 判断正面。原始资源没有已确认的 UI camera 或正面轴向；轻微 yaw 可展示厚度。该设置属于派生展示建议，不是官方相机恢复。`presentation_note` 是英文审计说明，不是玩家文案。

## 验证

- `E:\HS\HearthstoneClone\.venv\Scripts\python.exe -X utf8 -m pytest tests/test_pack_assets_export.py -q`：6 passed。聚焦测试分别验证 23 条研究清单、当前商店 16 项子集、派生商品拒绝、缺源显式失败和 runtime metadata 筛选。
- 当前商店 16 项：`(3, 13, 15, 21, 25, 27, 1001, 1004, 1125, 1127, 1129, 1130, 1158, 1347, 1414, 1443)`；被排除的 7 个研究来源不会进入运行时列表。
- 本子项没有实际执行 `python -m tools.export_pack_assets`；该命令将写入共享 `local/runtime`，由 root 串行执行并检查 Unity/GPU 展示。

## 交接

运行入口：`python -m tools.export_pack_assets`。Unity 读取 `local/runtime/pack-visuals.json` 中的 `model_id`/`scene_file` 与 `primary_node_path`；`ui_id` 是 scene Registry 别名，不是平面封面图片。未完成 Unity/GPU 视觉验收前，本子项不宣称官方相机方向或成品视觉通过。
