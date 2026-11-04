"""生成逐 ID 状态；基础出牌通路通过不等于全部效果已核验。"""
from .importer import PROJECT
from game.database import initialize, scope
from game.death_knight import DK_IDS
from fireplace import cards


def main():
    initialize()
    lines = ['# 卡牌实现状态', '',
             '`[x] verified` = 有充分行为核验；`[ ] incomplete` = 明确有缺失；`[?] uncertain` = 已接入但仍有待核验行为。', '',
             '当前目标池 489 张：原九职业与恶魔猎手历史池 431 张，死亡骑士上市池 58 张。', '',
             '目标 489 张均通过服务器合法动作执行的基础测试；20 个抉择分支另行逐一执行。另有伤害、尸体、符文、技能、冻结、吸血、武器、持续伤害等专项。基础执行测试不能证明所有随机池、触发顺序和边界正确。', '',
             '上游 Classic/Mechanics/Hero Powers/Secrets 共 281 项测试通过。本项目另有 577 项规则、TCP、视觉比较测试。尚未将每张历史牌的全部数值与行为矩阵独立补齐，因此不能把所有牌标为 verified。', '',
             '历史脚本修正已验证：BT_427 死亡计数；BT_753 法力锁定与叠加/过期；BT_801 目标、吸血和双方手牌两端流放减费；BT_142 英雄增益与过期；BT_351 自身成长；BT_510 被攻击触发。修正保留其他上游脚本属性。', '',
             '已知未完成：RLK_122 天灾军团目前随机池只包含本项目注册的死亡骑士亡灵，尚未扩展并核验完整的 2022 历史生成池。其他发现/随机生成池也需要跨历史卡池语义审计。', '',
             '| 状态 | CardId | 名称 | 证据与限制 |', '|---|---|---|---|']
    for card_id in scope():
        c = cards.db[card_id]
        status = '[ ] incomplete' if card_id == 'RLK_122' else '[?] uncertain'
        evidence = '基础执行通路已测，效果边界仍待核验' if card_id in DK_IDS else '出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补'
        if card_id == 'RLK_122':
            evidence = '可执行；完整历史随机亡灵池尚未实现'
        lines.append(f'| {status} | {card_id} | {c.name} | {evidence} |')
    (PROJECT / 'docs/CARD_IMPLEMENTATION_STATUS.md').write_text('\n'.join(lines)+'\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
