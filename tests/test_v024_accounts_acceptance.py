import asyncio
import json
import sqlite3
from concurrent.futures import ThreadPoolExecutor

import pytest
from fireplace import cards
from hearthstone.enums import CardSet, CardType

from game.accounts import AccountError, AccountService
from game.accounts.catalog import get_catalog
from game.accounts.economy import NORMAL_DUST_VALUE
from game.server import Server


TEST_PASSWORD = "acceptance-test-password"


def test_all_shop_titles_match_cardset_enum_and_metadata_card_ids():
    catalog = get_catalog()
    expected_names = {
        CardSet.EXPERT1: "经典卡包",
        CardSet.NAXX: "纳克萨玛斯的诅咒",
        CardSet.GVG: "地精大战侏儒",
        CardSet.BRM: "黑石山的火焰",
        CardSet.TGT: "冠军的试炼",
        CardSet.LOE: "探险者协会",
        CardSet.OG: "上古之神的低语",
        CardSet.KARA: "卡拉赞之夜",
        CardSet.GANGS: "龙争虎斗加基森",
        CardSet.UNGORO: "勇闯安戈洛",
        CardSet.ICECROWN: "冰封王座的骑士",
        CardSet.LOOTAPALOOZA: "狗头人与地下世界",
        CardSet.GILNEAS: "女巫森林",
        CardSet.BOOMSDAY: "砰砰计划",
        CardSet.TROLL: "拉斯塔哈的大乱斗",
        CardSet.DALARAN: "暗影崛起",
        CardSet.ULDUM: "奥丹姆奇兵",
        CardSet.DRAGONS: "巨龙降临",
        CardSet.YEAR_OF_THE_DRAGON: "迦拉克隆的觉醒",
        CardSet.BLACK_TEMPLE: "外域的灰烬",
        CardSet.SCHOLOMANCE: "通灵学园",
        CardSet.DEMON_HUNTER_INITIATE: "恶魔猎手新兵",
        CardSet.PATH_OF_ARTHAS: "阿尔萨斯之路",
    }
    example_cards = {
        CardSet.EXPERT1: "CS1_069",
        CardSet.NAXX: "FP1_001",
        CardSet.GVG: "GVG_001",
        CardSet.BRM: "BRM_001",
        CardSet.TGT: "AT_001",
        CardSet.LOE: "LOE_002",
        CardSet.OG: "OG_006",
        CardSet.KARA: "KAR_004",
        CardSet.GANGS: "CFM_020",
        CardSet.UNGORO: "UNG_001",
        CardSet.ICECROWN: "ICC_018",
        CardSet.LOOTAPALOOZA: "LOOT_008",
        CardSet.GILNEAS: "GIL_113",
        CardSet.BOOMSDAY: "BOT_020",
        CardSet.TROLL: "TRL_010",
        CardSet.DALARAN: "DAL_007",
        CardSet.ULDUM: "ULD_003",
        CardSet.DRAGONS: "DRG_006",
        CardSet.YEAR_OF_THE_DRAGON: "YOD_001",
        CardSet.BLACK_TEMPLE: "BT_002",
        CardSet.SCHOLOMANCE: "SCH_199",
        CardSet.DEMON_HUNTER_INITIATE: "BT_173",
        CardSet.PATH_OF_ARTHAS: "RLK_015",
    }

    assert len(expected_names) == len(example_cards) == 23
    for card_set, title in expected_names.items():
        set_id = int(card_set)
        assert CardSet(set_id).name == card_set.name
        assert catalog.set_name(set_id) == title
        sample_id = example_cards[card_set]
        assert catalog.cards[sample_id].set_id == set_id
        assert int(cards.db[sample_id].card_set) == set_id


def test_shop_contains_only_actual_random_pack_expansions():
    catalog = get_catalog()
    actual_pack_sets = {
        CardSet.EXPERT1, CardSet.GVG, CardSet.TGT, CardSet.OG, CardSet.GANGS,
        CardSet.UNGORO, CardSet.ICECROWN, CardSet.LOOTAPALOOZA, CardSet.GILNEAS,
        CardSet.BOOMSDAY, CardSet.TROLL, CardSet.DALARAN, CardSet.ULDUM,
        CardSet.DRAGONS, CardSet.BLACK_TEMPLE, CardSet.SCHOLOMANCE,
    }
    special_acquisition_sets = {
        CardSet.NAXX, CardSet.BRM, CardSet.LOE, CardSet.KARA,
        CardSet.YEAR_OF_THE_DRAGON, CardSet.DEMON_HUNTER_INITIATE,
        CardSet.PATH_OF_ARTHAS,
    }

    assert set(catalog.pack_sets) == {int(card_set) for card_set in actual_pack_sets}
    assert len(catalog.pack_sets) == 16
    assert not ({int(card_set) for card_set in special_acquisition_sets}
                & set(catalog.pack_sets))


def make_service(tmp_path, filename="accounts.sqlite3", **kwargs):
    return AccountService(tmp_path / filename, password_iterations=10_000,
                          golden_rate=0, **kwargs)


def register(service, username):
    request = {
        "type": "account_register",
        "request_id": f"register-{username}",
        "username": username,
        "password": TEST_PASSWORD,
        "display_name": username,
    }
    return request, service.process(None, None, request)


def test_legacy_three_slot_quests_migrate_without_resetting_or_repaying(tmp_path):
    filename = "legacy-quests.sqlite3"
    service = make_service(tmp_path, filename)
    _, (registration, account_id, token) = register(service, "LegacyQuestOwner")
    period_keys = {
        period: next(quest["quest_id"].split(":")[1]
                     for quest in registration["snapshot"]["quests"]
                     if quest["period"] == period)
        for period in ("daily", "weekly")
    }
    legacy_values = {
        ("daily", "easy"): ("match_complete", "完成一场对局", "完成1场对局", 1, 1, 40, 1, 0),
        ("daily", "normal"): ("attack", "攻击对手", "进行10次攻击", 4, 10, 60, 0, 1),
        ("daily", "hard"): ("match_win", "赢得对局", "赢得1场对局", 1, 1, 80, 1, 0),
        ("weekly", "easy"): ("match_complete", "完成对局", "完成3场对局", 2, 3, 80, 0, 0),
        ("weekly", "normal"): ("card_play", "打出卡牌", "打出30张卡牌", 30, 30, 120, 1, 0),
        ("weekly", "hard"): ("match_win", "赢得对局", "赢得3场对局", 1, 3, 160, 0, 0),
    }
    legacy_columns = (
        "quest_id,period,period_key,difficulty,quest_type,title,description,"
        "progress,target,reward_gold,completed,rerolls"
    )
    conn = service.store.connect()
    try:
        conn.execute("DELETE FROM quests WHERE account_id=? AND quest_id LIKE '%:fourth'",
                     (account_id,))
        for (period, difficulty), values in legacy_values.items():
            quest_id = f"{period}:{period_keys[period]}:{difficulty}"
            conn.execute(
                "UPDATE quests SET quest_type=?,title=?,description=?,progress=?,target=?,"
                "reward_gold=?,completed=?,rerolls=? WHERE account_id=? AND quest_id=?",
                (*values, account_id, quest_id),
            )
        conn.execute("UPDATE accounts SET gold=1240 WHERE account_id=?", (account_id,))
        conn.execute("DROP INDEX quests_account_period_slot")
        conn.execute("ALTER TABLE quests DROP COLUMN slot")
        old_rows = [tuple(row) for row in conn.execute(
            f"SELECT {legacy_columns} FROM quests WHERE account_id=? ORDER BY period,quest_id",
            (account_id,),
        )]
    finally:
        conn.close()

    upgraded = make_service(tmp_path, filename)
    resumed, resumed_id, resumed_token = upgraded.process(None, None, {
        "type": "account_resume", "request_id": "legacy-quest-resume", "token": token,
    })
    assert resumed["ok"] and resumed_id == account_id and resumed_token == token
    snapshot = resumed["snapshot"]
    assert snapshot["gold"] == 1240
    assert len(snapshot["quests"]) == 8
    assert [sum(quest["period"] == period for quest in snapshot["quests"])
            for period in ("daily", "weekly")] == [4, 4]
    conn = upgraded.store.connect()
    try:
        upgraded_rows = [tuple(row) for row in conn.execute(
            f"SELECT {legacy_columns} FROM quests WHERE account_id=? "
            "AND quest_id NOT LIKE '%:fourth' ORDER BY period,quest_id",
            (account_id,),
        )]
    finally:
        conn.close()
    assert upgraded_rows == old_rows

    daily_fourth = next(q for q in snapshot["quests"]
                        if q["period"] == "daily" and q["quest_id"].endswith(":fourth"))
    weekly_fourth = next(q for q in snapshot["quests"]
                         if q["period"] == "weekly" and q["quest_id"].endswith(":fourth"))
    assert (daily_fourth["difficulty"], daily_fourth["target"], daily_fourth["reward_gold"],
            daily_fourth["progress"], daily_fourth["completed"], daily_fourth["reroll_available"]) == (
                "normal", 2, 60, 0, False, True)
    assert (weekly_fourth["difficulty"], weekly_fourth["target"], weekly_fourth["reward_gold"],
            weekly_fourth["progress"], weekly_fourth["completed"], weekly_fourth["reroll_available"]) == (
                "normal", 5, 120, 0, False, True)

    reopened = make_service(tmp_path, filename)
    repeated, repeated_id, repeated_token = reopened.process(None, None, {
        "type": "account_resume", "request_id": "legacy-quest-resume-again", "token": token,
    })
    assert repeated["ok"] and repeated_id == account_id and repeated_token == token
    assert repeated["snapshot"]["gold"] == 1240
    assert repeated["snapshot"]["quests"] == snapshot["quests"]
    conn = reopened.store.connect()
    try:
        rows = conn.execute(
            "SELECT period,slot,quest_id FROM quests WHERE account_id=? "
            "AND period_key IN (?,?) ORDER BY period,slot",
            (account_id, period_keys["daily"], period_keys["weekly"]),
        ).fetchall()
    finally:
        conn.close()
    assert len(rows) == 8
    assert [(row["period"], row["slot"]) for row in rows] == [
        ("daily", 0), ("daily", 1), ("daily", 2), ("daily", 3),
        ("weekly", 0), ("weekly", 1), ("weekly", 2), ("weekly", 3),
    ]


def test_preauth_replay_does_not_revive_revoked_session_or_missing_key(tmp_path):
    service = make_service(tmp_path)
    request, (registration, account_id, token) = register(service, "ReplayOwner")
    assert registration["ok"]
    logout, _, _ = service.process(account_id, token, {
        "type": "account_logout", "request_id": "logout-replay-check",
    })
    assert logout["ok"] and logout["snapshot"] is None

    replay, replay_account, replay_token = service.process(None, None, request)
    assert replay["ok"] and replay_account == account_id and replay_token == token
    with pytest.raises(AccountError, match="登录状态已失效"):
        service.process(replay_account, replay_token, {
            "type": "account_state", "request_id": "state-after-register-replay",
        })
    with pytest.raises(AccountError, match="登录状态已失效"):
        service.process(None, None, {
            "type": "account_resume", "request_id": "resume-after-register-replay",
            "token": replay_token,
        })

    conn = sqlite3.connect(service.store.path)
    try:
        session_count, revoked_count = conn.execute(
            "SELECT count(*),sum(revoked_at IS NOT NULL) FROM sessions WHERE account_id=?",
            (account_id,),
        ).fetchone()
    finally:
        conn.close()
    assert session_count == revoked_count == 1

    (tmp_path / "accounts.sqlite3.token-key").unlink()
    with pytest.raises(ValueError, match="token key is missing"):
        AccountService(service.store.path, password_iterations=10_000)


def test_concurrent_economy_requests_preserve_wallet_pack_and_inventory_invariants(tmp_path):
    service = make_service(tmp_path)
    _, (registration, account_id, token) = register(service, "ConcurrentOwner")
    assert registration["ok"]
    set_id = service.catalog.pack_sets[0]

    def request(operation, request_id, **fields):
        return service.process(account_id, token, {
            "type": operation, "request_id": request_id, **fields,
        })[0]

    with ThreadPoolExecutor(max_workers=16) as pool:
        purchases = list(pool.map(
            lambda index: request("account_shop_buy", f"parallel-buy-{index}",
                                  set_id=set_id, count=1), range(16)))
    assert sum(result["ok"] for result in purchases) == 10
    state = request("account_state", "parallel-buy-state")["snapshot"]
    assert state["gold"] == 0
    assert state["packs"] == [{"set_id": set_id, "count": 10}]

    with ThreadPoolExecutor(max_workers=16) as pool:
        openings = list(pool.map(
            lambda index: request("account_pack_open", f"parallel-open-{index}", set_id=set_id),
            range(16)))
    successful_openings = [result for result in openings if result["ok"]]
    assert len(successful_openings) == 10
    assert all(len(result["opened_cards"]) == 5 for result in successful_openings)
    state = request("account_state", "parallel-open-state")["snapshot"]
    assert state["packs"] == []
    assert sum(card["normal"] + card["golden"] for card in state["cards"]) == 50

    target = successful_openings[0]["opened_cards"][0]
    dust_before = state["dust"]
    with ThreadPoolExecutor(max_workers=12) as pool:
        disenchant_results = list(pool.map(
            lambda index: request("account_disenchant", f"parallel-disenchant-{index}",
                                  card_id=target["card_id"], golden=target["golden"], count=1),
            range(12)))
    assert sum(result["ok"] for result in disenchant_results) == 1
    state = request("account_state", "parallel-disenchant-state")["snapshot"]
    rarity = service.catalog.cards[target["card_id"]].rarity
    assert state["dust"] == dust_before + NORMAL_DUST_VALUE[rarity]

    # Seed an isolated test wallet so concurrent crafting reaches the debit path.
    craft_target = next(card for card in service.catalog.cards.values()
                        if card.rarity == 1 and card.set_id not in (2, 1635, 1637)
                        and card.card_id not in {item["card_id"]
                                                 for result in successful_openings
                                                 for item in result["opened_cards"]})
    conn = service.store.connect()
    try:
        conn.execute("UPDATE accounts SET dust=200 WHERE account_id=?", (account_id,))
    finally:
        conn.close()
    with ThreadPoolExecutor(max_workers=16) as pool:
        crafts = list(pool.map(
            lambda index: request("account_craft", f"parallel-craft-{index}",
                                  card_id=craft_target.card_id, golden=False, count=1),
            range(16)))
    assert sum(result["ok"] for result in crafts) == 5
    state = request("account_state", "parallel-craft-state")["snapshot"]
    crafted = next(card for card in state["cards"] if card["card_id"] == craft_target.card_id)
    assert crafted["normal"] == 5
    assert state["dust"] == 0


def test_core_cards_cannot_be_crafted_from_free_inventory(tmp_path):
    service = make_service(tmp_path)
    _, (registration, account_id, token) = register(service, "CoreCraftOwner")
    assert registration["ok"]

    core = next(card for card in service.catalog.cards.values()
                if card.set_id == int(CardSet.CORE) and card.rarity == 1)
    granted = next(item for item in registration["snapshot"]["cards"]
                   if item["card_id"] == core.card_id)
    assert granted["free"] > 0

    conn = service.store.connect()
    try:
        conn.execute("UPDATE accounts SET dust=1000 WHERE account_id=?", (account_id,))
    finally:
        conn.close()

    before = service.process(account_id, token, {
        "type": "account_state", "request_id": "core-craft-state-before",
    })[0]["snapshot"]
    results = [service.process(account_id, token, {
        "type": "account_craft", "request_id": f"core-craft-{golden}",
        "card_id": core.card_id, "golden": golden, "count": 1,
    })[0] for golden in (False, True)]
    after = service.process(account_id, token, {
        "type": "account_state", "request_id": "core-craft-state-after",
    })[0]["snapshot"]

    assert all(not result["ok"] for result in results) and (
        after["gold"] == before["gold"] and after["dust"] == before["dust"] and
        after["revision"] == before["revision"] and after["cards"] == before["cards"]
    ), ("normal and golden crafting of an already-granted CORE card must be rejected "
        "without changing wallet, revision, or inventory; "
        f"results={[result.get('ok') for result in results]}, "
        f"before=({before['gold']},{before['dust']},{before['revision']}), "
        f"after=({after['gold']},{after['dust']},{after['revision']})")


def test_saved_deck_requires_owned_in_scope_cards_and_exact_size(tmp_path):
    service = make_service(tmp_path)
    registration, account_id, token = register(service, "DeckVerifier")[1]
    assert registration["ok"]
    starter = registration["snapshot"]["decks"][0]
    owned_ids = {item["card_id"] for item in registration["snapshot"]["cards"]}
    legal_deck = list(starter["cards"])

    def save(request_id, class_id, cards):
        return service.process(account_id, token, {
            "type": "account_deck_save", "request_id": request_id,
            "deck_id": "", "name": "验收卡组", "class_id": class_id, "cards": cards,
        })[0]

    valid = save("save-valid-starter", starter["class_id"], legal_deck)
    assert valid["ok"] and valid["deck_id"]
    loaded = service.load_match_deck(account_id, token, valid["deck_id"])
    assert loaded["class_id"] == starter["class_id"] and loaded["deck"] == legal_deck

    assert not save("save-29-cards", starter["class_id"], legal_deck[:-1])["ok"]

    unowned = next(card for card in service.catalog.cards.values()
                   if card.card_id in service.catalog.playable_ids
                   and card.class_id in (12, starter["class_id"])
                   and card.card_id not in owned_ids and card.card_id not in legal_deck)
    unowned_deck = list(legal_deck)
    unowned_deck[0] = unowned.card_id
    assert not save("save-unowned-card", starter["class_id"], unowned_deck)["ok"]

    out_of_scope = next(card for card in service.catalog.cards.values()
                        if card.card_id not in service.catalog.playable_ids
                        and card.class_id in (12, starter["class_id"])
                        and card.card_type in {int(CardType.MINION), int(CardType.SPELL), int(CardType.WEAPON)}
                        and card.card_id not in legal_deck)
    out_of_scope_deck = list(legal_deck)
    out_of_scope_deck[0] = out_of_scope.card_id
    assert not save("save-out-of-scope-card", starter["class_id"], out_of_scope_deck)["ok"]


def test_tcp_auth_is_token_bound_and_bad_json_does_not_kill_server(tmp_path):
    async def run():
        account_service = make_service(tmp_path)
        server = Server(tmp_path / "matches", account_service=account_service)
        listener = await asyncio.start_server(server.handle, "127.0.0.1", 0, limit=32768)
        port = listener.sockets[0].getsockname()[1]
        writers = []

        async def connect():
            reader, writer = await asyncio.open_connection("127.0.0.1", port)
            writers.append(writer)
            hello = json.loads(await asyncio.wait_for(reader.readline(), 5))
            assert "accounts_v1" in hello["capabilities"]
            return reader, writer

        async def send(writer, value):
            writer.write((json.dumps(value, ensure_ascii=False) + "\n").encode("utf-8"))
            await writer.drain()

        async def recv(reader):
            return json.loads(await asyncio.wait_for(reader.readline(), 5))

        try:
            accounts = []
            menus = []
            for username in ("tcp-owner-a", "tcp-owner-b"):
                reader, writer = await connect()
                menus.append((reader, writer))
                await send(writer, {
                    "type": "account_register", "request_id": f"register-{username}",
                    "username": username, "password": TEST_PASSWORD, "display_name": username,
                })
                accounts.append(await recv(reader))
            assert all(result["ok"] for result in accounts)
            owner_a, owner_b = (result["snapshot"] for result in accounts)
            assert all(type(item["set_id"]) is int for item in owner_a["shop"])

            unauth_reader, unauth_writer = await connect()
            await send(unauth_writer, {
                "type": "account_shop_buy", "request_id": "forged-buy",
                "account_id": owner_b["account_id"], "set_id": owner_b["shop"][0]["set_id"],
                "count": 1,
            })
            assert not (await recv(unauth_reader))["ok"]
            await send(unauth_writer, {
                "type": "join", "account_id": owner_b["account_id"],
                "deck_id": owner_b["decks"][0]["deck_id"],
            })
            assert (await recv(unauth_reader))["type"] == "error"

            await send(menus[1][1], {"type": "account_state", "request_id": "owner-b-after-forgery"})
            assert (await recv(menus[1][0]))["snapshot"]["gold"] == 1000

            unauth_writer.write(b'{"type":"account_register","request_id":"nan-register",'
                                b'"username":"bad-json-user","password":"acceptance-test-password",'
                                b'"display_name":"bad-json","extra":NaN}\n')
            await unauth_writer.drain()
            nan_result = await recv(unauth_reader)
            assert nan_result["type"] == "account_result" and not nan_result["ok"]

            deeply_nested = ("[[" * 1200 + "0" + "]]" * 1200 + "\n").encode("ascii")
            unauth_writer.write(deeply_nested)
            await unauth_writer.drain()
            assert (await recv(unauth_reader))["type"] == "error"
            await send(unauth_writer, {"type": "ping"})
            assert (await recv(unauth_reader))["type"] == "pong"

            resumed_reader, resumed_writer = await connect()
            await send(resumed_writer, {
                "type": "account_resume", "request_id": "resume-owner-a",
                "token": accounts[0]["token"],
            })
            assert (await recv(resumed_reader))["ok"]
            await send(resumed_writer, {
                "type": "account_state", "request_id": "spoof-state",
                "account_id": owner_b["account_id"],
            })
            assert (await recv(resumed_reader))["snapshot"]["account_id"] == owner_a["account_id"]
            await send(resumed_writer, {
                "type": "account_deck_delete", "request_id": "delete-foreign-deck",
                "deck_id": owner_b["decks"][0]["deck_id"],
                "account_id": owner_b["account_id"],
            })
            assert not (await recv(resumed_reader))["ok"]
            for request_id, invalid_set_id in (("string-set-id", str(owner_a["shop"][0]["set_id"])),
                                                ("boolean-set-id", True)):
                await send(resumed_writer, {
                    "type": "account_shop_buy", "request_id": request_id,
                    "set_id": invalid_set_id, "count": 1,
                })
                invalid_set_result = await recv(resumed_reader)
                assert not invalid_set_result["ok"]
                assert invalid_set_result["error"] == "扩展包编号无效"
            await send(resumed_writer, {
                "type": "account_shop_buy", "request_id": "strict-bool-count",
                "set_id": owner_a["shop"][0]["set_id"], "count": True,
            })
            assert not (await recv(resumed_reader))["ok"]
            await send(resumed_writer, {
                "type": "account_shop_buy", "request_id": "oversized-count",
                "set_id": owner_a["shop"][0]["set_id"], "count": 1001,
            })
            assert not (await recv(resumed_reader))["ok"]
            await send(resumed_writer, {
                "type": "account_shop_buy", "request_id": "negative-count",
                "set_id": owner_a["shop"][0]["set_id"], "count": -1,
            })
            assert not (await recv(resumed_reader))["ok"]
            await send(resumed_writer, {
                "type": "join", "deck_id": owner_b["decks"][0]["deck_id"],
            })
            assert (await recv(resumed_reader))["type"] == "error"

            await send(menus[1][1], {"type": "account_state", "request_id": "owner-b-after-cross-write"})
            state_b = (await recv(menus[1][0]))["snapshot"]
            assert state_b["gold"] == 1000
            assert len(state_b["decks"]) == 11

            await send(menus[0][1], {"type": "account_logout", "request_id": "logout-from-menu"})
            assert (await recv(menus[0][0]))["ok"]
            await send(resumed_writer, {"type": "join", "deck_id": owner_a["decks"][0]["deck_id"]})
            assert (await recv(resumed_reader))["type"] == "error"
            await send(resumed_writer, {"type": "account_state", "request_id": "revoked-state"})
            assert not (await recv(resumed_reader))["ok"]
        finally:
            for writer in writers:
                writer.close()
            for writer in writers:
                try:
                    await writer.wait_closed()
                except (ConnectionError, OSError):
                    pass
            listener.close()
            await listener.wait_closed()

    asyncio.run(run())


def test_tcp_disconnect_concedes_broadcasts_and_rewards_authoritative_accounts(tmp_path):
    async def run():
        account_service = make_service(tmp_path)
        server = Server(tmp_path / "matches", account_service=account_service)
        listener = await asyncio.start_server(server.handle, "127.0.0.1", 0, limit=32768)
        port = listener.sockets[0].getsockname()[1]
        writers = []

        async def connect():
            reader, writer = await asyncio.open_connection("127.0.0.1", port)
            writers.append(writer)
            await asyncio.wait_for(reader.readline(), 5)
            return reader, writer

        async def send(writer, value):
            writer.write((json.dumps(value, ensure_ascii=False) + "\n").encode("utf-8"))
            await writer.drain()

        async def recv(reader):
            return json.loads(await asyncio.wait_for(reader.readline(), 8))

        try:
            menus = []
            registrations = []
            for username in ("disconnect-a", "disconnect-b"):
                reader, writer = await connect()
                menus.append((reader, writer))
                await send(writer, {
                    "type": "account_register", "request_id": f"register-{username}",
                    "username": username, "password": TEST_PASSWORD, "display_name": username,
                })
                registrations.append(await recv(reader))

            games = []
            for index, registration in enumerate(registrations):
                reader, writer = await connect()
                games.append((reader, writer))
                snapshot = registration["snapshot"]
                await send(writer, {
                    "type": "account_resume", "request_id": f"resume-game-{index}",
                    "token": registration["token"],
                })
                assert (await recv(reader))["snapshot"]["account_id"] == snapshot["account_id"]
                await send(writer, {"type": "join", "deck_id": snapshot["decks"][0]["deck_id"]})
                assert (await recv(reader))["type"] == "queued"

            initial = [await recv(reader) for reader, _ in games]
            assert initial[0]["match_id"] == initial[1]["match_id"]

            # Closing the socket without an action must take the server's disconnect-concede path.
            games[0][1].close()
            await games[0][1].wait_closed()
            finished = await recv(games[1][0])
            assert finished["phase"] == "finished"
            assert [player["result"] for player in finished["players"]] == ["LOST", "WON"]

            snapshots = []
            for index, (reader, writer) in enumerate(menus):
                await send(writer, {"type": "account_state", "request_id": f"after-disconnect-{index}"})
                snapshots.append((await recv(reader))["snapshot"])
            assert [snapshot["gold"] for snapshot in snapshots] == [1040, 1120]
            assert all(next(q for q in snapshot["quests"]
                            if q["period"] == "daily" and q["difficulty"] == "easy")["completed"]
                       for snapshot in snapshots)
            assert next(q for q in snapshots[1]["quests"]
                        if q["period"] == "daily" and q["difficulty"] == "hard")["completed"]
            assert not next(q for q in snapshots[0]["quests"]
                            if q["period"] == "daily" and q["difficulty"] == "hard")["completed"]
        finally:
            for writer in writers:
                writer.close()
            for writer in writers:
                try:
                    await writer.wait_closed()
                except (ConnectionError, OSError):
                    pass
            listener.close()
            await listener.wait_closed()

    asyncio.run(run())
