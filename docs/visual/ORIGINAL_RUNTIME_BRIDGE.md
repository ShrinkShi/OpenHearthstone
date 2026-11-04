# Original Runtime Bridge（可选研究，未实现）

当前没有已验收 Bridge，也未引入原客户端运行依赖。自主客户端/权威规则应始终可独立工作；当前资源缓存依赖和未来运行时 Fallback 是两件事。

仅在某少数效果成本异常高、明确未逆向且有真实特殊运行时依赖时，提出单独研究 Task 和决策。先评估版本、资源、可重复性、生命周期/错误回收，再决定 IPC：Named Pipe、Socket、Shared Memory 均为候选，尚未选型。

概念边界：Open Client → Visual Command → Bridge API → IPC → Original Runtime Process。可能命令 PlayEffect / SpawnEffect / TriggerSequence / PlayAnimation / SetMaterialParameter / PlayAudio 仅为接口候选，不是原版已存在 API。内部类/字段/地址和版本适配必须隔离，不得泄漏到规则与业务代码。

禁止未经验证设计 LoadLibrary(Hearthstone.exe)。研究模式的官方 Ground Truth、导入本机资源、可选 Fallback 分别计量。将来每个 F 项记录原因/启用开关/失败行为/替换计划；自主还原后持续减少 F 数量。默认不启动或操纵用户原版客户端。
