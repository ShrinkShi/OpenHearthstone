import asyncio
import hashlib
import json
import sqlite3
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone

import pytest
from hearthstone.enums import CardSet, CardType

from game.accounts import AccountError, AccountService
from game.accounts.catalog import get_catalog
from game.accounts.service import SESSION_LIFETIME
from game.database import preset, validate_deck
from game.match import Match
from game.server import Server


SHANGHAI = timezone(timedelta(hours=8))
PASSWORD = "correct-horse-battery"


def make_service(tmp_path, name="accounts.sqlite3", **kwargs):
    return AccountService(tmp_path / name, password_iterations=10_000,
                          golden_rate=0, **kwargs)


def register(service, username, display_name=None, request_id=None):
    request = {
        "type": "account_register",
        "request_id": request_id or f"register-{username}",
        "username": username,
        "password": PASSWORD,
        "display_name": display_name or username,
    }
    response, account_id, token = service.process(None, None, request)
    assert response["ok"]
    return response, account_id, token


def call(service, account_id, token, operation, request_id, **fields):
    return service.process(account_id, token, {
        "type": operation, "request_id": request_id, **fields,
    })[0]


def test_shop_names_match_metadata_and_open_the_advertised_expansion(tmp_path):
    service = make_service(tmp_path)
    registered, account_id, token = register(service, "set-labels")
    shop = {int(item["set_id"]): item for item in registered["snapshot"]["shop"]}
    examples = ((CardSet.GANGS, "龙争虎斗加基森"), (CardSet.OG, "上古之神的低语"),
                (CardSet.ICECROWN, "冰封王座的骑士"),
                (CardSet.BLACK_TEMPLE, "外域的灰烬"),
                (CardSet.SCHOLOMANCE, "通灵学园"))
    for expansion, title in examples:
        assert shop[int(expansion)]["name"] == title
        bought = call(service, account_id, token, "account_shop_buy", f"buy-{expansion.name}",
                      set_id=int(expansion), count=1)
        assert bought["ok"]
        opened = call(service, account_id, token, "account_pack_open", f"open-{expansion.name}",
                      set_id=int(expansion))
        assert opened["ok"] and len(opened["opened_cards"]) == 5
        assert all(service.catalog.cards[item["card_id"]].set_id == int(expansion)
                   for item in opened["opened_cards"])
    assert opened["snapshot"]["gold"] == 500


def test_registration_password_hash_sessions_resume_logout_and_starter_decks(tmp_path):
    service = make_service(tmp_path)
    response, account_id, token = register(service, "PlayerOne", "玩家一")
    snapshot = response["snapshot"]
    assert snapshot["username"] == "playerone"
    assert snapshot["display_name"] == "玩家一"
    assert snapshot["gold"] == 1000 and snapshot["dust"] == 0
    assert len(snapshot["cards"]) == 185
    assert len(snapshot["decks"]) == 11
    assert len(snapshot["quests"]) == 8
    assert all(card["free"] > 0 and card["normal"] == card["golden"] == 0
               for card in snapshot["cards"])
    catalog = service.catalog
    cards_by_id = {item["card_id"]: item for item in snapshot["cards"]}
    assert len([card for card in catalog.cards.values() if card.set_id == 2]) == 153
    assert len([card for card in catalog.cards.values() if card.set_id == 1637]) == 32
    assert not [card for card in catalog.cards.values() if card.set_id == 1635]
    assert len([card for card in catalog.cards.values() if card.set_id == 3]) == 240
    assert not any(card.set_id == 3 and card.card_id in cards_by_id for card in catalog.cards.values())
    assert sum(1 for card in catalog.cards.values() if card.set_id == 2
               and card.card_id in catalog.playable_ids) == 143
    assert sum(1 for card in catalog.cards.values() if card.set_id == 1637
               and card.card_id in catalog.playable_ids) == 32
    assert 3 in catalog.pack_sets  # EXPERT1 cards are sold as classic packs, never initial gifts.
    assert not (set(catalog.pack_sets) & {2, 4, 1453, 1586, 1637, 1705})
    assert all(card.card_type in {int(CardType.MINION), int(CardType.SPELL), int(CardType.WEAPON)}
               for cards_in_pack in catalog.pack_by_set.values() for card in cards_in_pack)
    assert all(len(deck["cards"]) == 30 and set(deck["cards"]) <= cards_by_id.keys()
               for deck in snapshot["decks"])
    assert {deck["class_id"] for deck in snapshot["decks"]} == {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 14}
    for deck in snapshot["decks"]:
        validate_deck(deck["class_id"], deck["cards"])
        owned = Counter({card_id: item["free"] for card_id, item in cards_by_id.items()})
        assert all(owned[card_id] >= count for card_id, count in Counter(deck["cards"]).items())
    core_legendary = next(card for card in catalog.cards.values()
                          if card.set_id == 1637 and card.rarity == 5)
    assert cards_by_id[core_legendary.card_id]["free"] == 1
    core_dust = call(service, account_id, token, "account_disenchant", "core-dust",
                     card_id=core_legendary.card_id, golden=False, count=1)
    assert not core_dust["ok"] and "核心系列" in core_dust["error"]
    basic_free = next(card for card in catalog.cards.values() if card.set_id == 2)
    free_dust = call(service, account_id, token, "account_disenchant", "basic-dust",
                     card_id=basic_free.card_id, golden=False, count=1)
    assert not free_dust["ok"] and "不能分解" in free_dust["error"]

    replay, replay_account, replay_token = service.process(None, None, {
        "type": "account_register", "request_id": "register-PlayerOne",
        "username": "PlayerOne", "password": PASSWORD, "display_name": "玩家一",
    })
    assert replay["ok"] and replay_token == token and replay_account == account_id
    assert replay["snapshot"]["revision"] == snapshot["revision"]

    login, login_account, login_token = service.process(None, None, {
        "type": "account_login", "request_id": "login-1",
        "username": "PLAYERONE", "password": PASSWORD,
    })
    assert login["ok"] and login_account == account_id
    assert login["snapshot"]["username"] == "playerone"
    resumed, resumed_account, resumed_token = service.process(None, None, {
        "type": "account_resume", "request_id": "resume-1", "token": token,
    })
    assert resumed["ok"] and resumed_account == account_id and resumed_token == token

    conn = sqlite3.connect(service.store.path)
    try:
        password_salt, password_hash = conn.execute(
            "SELECT password_salt,password_hash FROM accounts WHERE account_id=?", (account_id,)
        ).fetchone()
        token_hash, = conn.execute(
            "SELECT token_hash FROM sessions WHERE token_hash=?", (hashlib.sha256(token.encode()).digest(),)
        ).fetchone()
    finally:
        conn.close()
    assert len(password_salt) == 16 and len(password_hash) == 32
    assert token_hash == hashlib.sha256(token.encode()).digest()

    logout, _, _ = service.process(login_account, login_token, {
        "type": "account_logout", "request_id": "logout-1",
    })
    assert logout["ok"]
    replay_logout, _, _ = service.process(login_account, login_token, {
        "type": "account_logout", "request_id": "logout-1",
    })
    assert replay_logout["ok"]
    with pytest.raises(AccountError, match="登录状态已失效"):
        service.process(None, None, {
            "type": "account_resume", "request_id": "resume-revoked", "token": login_token,
        })

    restarted = make_service(tmp_path)
    after_restart, _, _ = restarted.process(None, None, {
        "type": "account_resume", "request_id": "resume-original", "token": token,
    })
    assert after_restart["ok"] and after_restart["snapshot"]["gold"] == 1000


def test_free_and_core_cards_cannot_be_crafted_or_replayed(tmp_path):
    service = make_service(tmp_path)
    _, account_id, token = register(service, "NoCoreCraft")
    conn = sqlite3.connect(service.store.path)
    try:
        conn.execute("UPDATE accounts SET dust=5000 WHERE account_id=?", (account_id,))
        conn.commit()
    finally:
        conn.close()

    before = call(service, account_id, token, "account_state", "before-core-craft")["snapshot"]
    core = service.catalog.cards["RLK_024"]
    basic = next(card for card in service.catalog.cards.values() if card.set_id == 2)
    assert (core.set_id, core.rarity) == (1637, 1)
    assert basic.rarity == 2
    targets = (("RLK_024", False, "core-normal"),
               ("RLK_024", True, "core-golden"),
               (basic.card_id, False, "free-normal"),
               (basic.card_id, True, "free-golden"))
    for card_id, golden, request_id in targets:
        first = call(service, account_id, token, "account_craft", request_id,
                     card_id=card_id, golden=golden, count=1)
        assert not first["ok"]
        assert "不能制作" in first["error"]
        assert first["snapshot"]["gold"] == before["gold"]
        assert first["snapshot"]["dust"] == before["dust"] == 5000
        assert first["snapshot"]["revision"] == before["revision"]
        assert first["snapshot"]["cards"] == before["cards"]

        replay = call(service, account_id, token, "account_craft", request_id,
                      card_id=card_id, golden=golden, count=1)
        assert replay == first


def test_expired_or_revoked_session_cannot_replay_account_mutation(tmp_path):
    now = [1_800_000_000]
    service = make_service(tmp_path, clock=lambda: now[0])
    _, account_id, token = register(service, "ExpiryUser")
    set_id = service.catalog.pack_sets[0]
    purchase = {"type": "account_shop_buy", "request_id": "expiring-buy",
                "set_id": set_id, "count": 1}
    first, _, _ = service.process(account_id, token, purchase)
    assert first["ok"] and first["snapshot"]["gold"] == 900

    now[0] += SESSION_LIFETIME + 1
    with pytest.raises(AccountError, match="登录状态已失效"):
        service.process(account_id, token, purchase)
    login, current_account, current_token = service.process(None, None, {
        "type": "account_login", "request_id": "fresh-login",
        "username": "expiryuser", "password": PASSWORD,
    })
    assert login["ok"] and current_account == account_id
    state, _, _ = service.process(current_account, current_token, {
        "type": "account_state", "request_id": "after-expiry",
    })
    assert state["snapshot"]["gold"] == 900
    assert state["snapshot"]["packs"] == [{"set_id": set_id, "count": 1}]

    logout, _, _ = service.process(current_account, current_token, {
        "type": "account_logout", "request_id": "fresh-logout",
    })
    assert logout["ok"] and logout["snapshot"] is None
    replay_logout, _, _ = service.process(current_account, current_token, {
        "type": "account_logout", "request_id": "fresh-logout",
    })
    assert replay_logout["ok"] and replay_logout["snapshot"] is None
    with pytest.raises(AccountError, match="登录状态已失效"):
        service.process(current_account, current_token, {
            "type": "account_state", "request_id": "after-logout",
        })


def test_buy_open_craft_disenchant_dedup_and_transaction_rollback(tmp_path, monkeypatch):
    service = make_service(tmp_path, pack_weights={1: 0, 3: 1, 4: 0, 5: 0})
    _, account_id, token = register(service, "EconomyUser")
    set_id = service.catalog.pack_sets[0]
    bought = call(service, account_id, token, "account_shop_buy", "buy-1", set_id=set_id, count=1)
    assert bought["ok"] and bought["snapshot"]["gold"] == 900
    assert bought["snapshot"]["packs"] == [{"set_id": set_id, "count": 1}]
    assert call(service, account_id, token, "account_shop_buy", "buy-1",
                set_id=set_id, count=1) == bought

    opened = call(service, account_id, token, "account_pack_open", "open-1", set_id=set_id)
    assert opened["ok"] and len(opened["opened_cards"]) == 5
    assert any(service.catalog.cards[card["card_id"]].rarity >= 3
               for card in opened["opened_cards"])
    assert len({card["card_id"] for card in opened["opened_cards"]}) == 5
    assert call(service, account_id, token, "account_pack_open", "open-1",
                set_id=set_id) == opened
    assert opened["snapshot"]["packs"] == []

    for index, opened_card in enumerate(opened["opened_cards"][:2]):
        dust_result = call(service, account_id, token, "account_disenchant", f"dust-pack-{index}",
                           card_id=opened_card["card_id"], golden=opened_card["golden"], count=1)
        assert dust_result["ok"]
    assert dust_result["snapshot"]["dust"] == 40

    common = next(card for card in service.catalog.cards.values()
                  if card.rarity == 1 and card.set_id not in (1637,))
    crafted = call(service, account_id, token, "account_craft", "craft-1",
                   card_id=common.card_id, golden=False, count=1)
    assert crafted["ok"] and crafted["snapshot"]["gold"] == 900
    assert crafted["snapshot"]["dust"] == 0
    assert call(service, account_id, token, "account_craft", "craft-1",
                card_id=common.card_id, golden=False, count=1) == crafted
    dusted = call(service, account_id, token, "account_disenchant", "dust-1",
                  card_id=common.card_id, golden=False, count=1)
    assert dusted["ok"] and dusted["snapshot"]["dust"] == 5
    assert all(card["card_id"] != common.card_id for card in dusted["snapshot"]["cards"]
               if not card["normal"] and not card["golden"] and not card["free"])

    before = call(service, account_id, token, "account_state", "state-before-invalid")
    for bad_count in (0, -1, True, 1.5):
        result = call(service, account_id, token, "account_shop_buy", f"bad-count-{bad_count}",
                      set_id=set_id, count=bad_count)
        assert not result["ok"]
    bad_golden = call(service, account_id, token, "account_craft", "bad-golden",
                      card_id=common.card_id, golden=1, count=1)
    assert not bad_golden["ok"]
    after = call(service, account_id, token, "account_state", "state-after-invalid")
    assert after["snapshot"]["gold"] == before["snapshot"]["gold"]
    assert after["snapshot"]["dust"] == before["snapshot"]["dust"]

    # A failure after the first inventory write must roll back both cards and pack count.
    call(service, account_id, token, "account_shop_buy", "buy-2", set_id=set_id, count=1)
    before_rollback = call(service, account_id, token, "account_state", "state-before-rollback")["snapshot"]
    original = service._change_inventory
    calls = 0

    def fail_after_one(conn, owner, card_id, **counts):
        nonlocal calls
        calls += 1
        original(conn, owner, card_id, **counts)
        if calls == 1:
            raise RuntimeError("injected rollback")

    monkeypatch.setattr(service, "_change_inventory", fail_after_one)
    with pytest.raises(RuntimeError, match="injected rollback"):
        service._open_pack(account_id, {"type": "account_pack_open", "request_id": "rollback-open",
                                        "set_id": set_id})
    monkeypatch.setattr(service, "_change_inventory", original)
    after_rollback = call(service, account_id, token, "account_state", "state-after-rollback")["snapshot"]
    assert after_rollback["packs"] == before_rollback["packs"]
    assert after_rollback["cards"] == before_rollback["cards"]


def test_pack_duplicate_protection_can_be_configured(tmp_path):
    class FixedRandom:
        @staticmethod
        def random():
            return 0

        @staticmethod
        def choice(values):
            return values[0]

    service = make_service(tmp_path, "duplicates.sqlite3", pack_weights={1: 0, 3: 1, 4: 0, 5: 0},
                           duplicate_protection=False, rng=FixedRandom())
    _, account_id, token = register(service, "RepeatPack")
    set_id = 3
    assert set_id in service.catalog.pack_sets
    assert call(service, account_id, token, "account_shop_buy", "repeat-buy",
                set_id=set_id, count=1)["ok"]
    opened = call(service, account_id, token, "account_pack_open", "repeat-open", set_id=set_id)
    assert len(opened["opened_cards"]) == 5
    assert len({card["card_id"] for card in opened["opened_cards"]}) == 1


def test_deck_ownership_privacy_revision_and_concurrent_idempotency(tmp_path):
    service = make_service(tmp_path)
    first, account_a, token_a = register(service, "DeckOwner")
    second, account_b, token_b = register(service, "OtherOwner")
    source_deck = first["snapshot"]["decks"][0]
    saved = call(service, account_a, token_a, "account_deck_save", "deck-save-new",
                 deck_id="", name="新建卡组", class_id=source_deck["class_id"], cards=source_deck["cards"])
    assert saved["ok"] and saved["deck_id"]
    assert any(deck["deck_id"] == saved["deck_id"] for deck in saved["snapshot"]["decks"])

    steal = call(service, account_b, token_b, "account_deck_delete", "delete-foreign",
                 deck_id=source_deck["deck_id"])
    assert not steal["ok"]
    other_state = call(service, account_b, token_b, "account_state", "other-state")
    assert any(deck["deck_id"] == source_deck["deck_id"] for deck in first["snapshot"]["decks"])
    assert not any(deck["deck_id"] == source_deck["deck_id"] for deck in other_state["snapshot"]["decks"])

    same_request = {"type": "account_shop_buy", "request_id": "concurrent-buy",
                    "set_id": service.catalog.pack_sets[0], "count": 2}
    with ThreadPoolExecutor(max_workers=8) as pool:
        responses = list(pool.map(lambda _: service.process(account_a, token_a, same_request)[0], range(8)))
    assert all(response == responses[0] and response["ok"] for response in responses)
    account_after = call(service, account_a, token_a, "account_state", "after-concurrent-buy")["snapshot"]
    assert account_after["gold"] == first["snapshot"]["gold"] - 200
    assert account_after["packs"] == [{"set_id": service.catalog.pack_sets[0], "count": 2}]

    with pytest.raises(AccountError, match="请求编号已用于不同内容"):
        service.process(account_a, token_a, {
            "type": "account_shop_buy", "request_id": "concurrent-buy",
            "set_id": service.catalog.pack_sets[0], "count": 1,
        })
    # Request IDs are scoped to the authenticated account, not global.
    other_buy = call(service, account_b, token_b, "account_shop_buy", "concurrent-buy",
                     set_id=service.catalog.pack_sets[0], count=1)
    assert other_buy["ok"]


def test_two_accounts_complete_economy_cycle_and_persist_after_restart(tmp_path):
    service = make_service(tmp_path, pack_weights={1: 80, 3: 20, 4: 0, 5: 0})
    users = [register(service, "CycleOne", "第一账号"),
             register(service, "CycleTwo", "第二账号")]
    common = next(card for card in service.catalog.cards.values()
                  if card.rarity == 1 and card.set_id not in (2, 3, 4, 1637, 1635))
    set_id = common.set_id
    expected = []
    for index, (registration, account_id, token) in enumerate(users):
        starter = registration["snapshot"]["decks"][0]
        buy = call(service, account_id, token, "account_shop_buy", "cycle-buy",
                    set_id=set_id, count=1)
        assert buy["ok"]
        opened = call(service, account_id, token, "account_pack_open", "cycle-open", set_id=set_id)
        assert opened["ok"] and len(opened["opened_cards"]) == 5
        for card_index, opened_card in enumerate(opened["opened_cards"]):
            dusted = call(service, account_id, token, "account_disenchant",
                          f"cycle-dust-{card_index}", card_id=opened_card["card_id"],
                          golden=opened_card["golden"], count=1)
            assert dusted["ok"]
        crafted = call(service, account_id, token, "account_craft", "cycle-craft",
                       card_id=common.card_id, golden=False, count=1)
        assert crafted["ok"]
        deck = call(service, account_id, token, "account_deck_save", "cycle-deck",
                    deck_id="", name=f"账号{index + 1}卡组", class_id=starter["class_id"],
                    cards=starter["cards"])
        assert deck["ok"] and deck["deck_id"]
        service.record_accepted_match(f"cycle-match-{index}", 1, 0, [],
                                      [account_id, None], ["WON", "LOST"])
        service.record_accepted_match(f"cycle-match-{index}", 1, 0, [],
                                      [account_id, None], ["WON", "LOST"])
        state = call(service, account_id, token, "account_state", f"cycle-state-{index}")["snapshot"]
        assert state["gold"] == 1020
        assert next(q for q in state["quests"]
                    if q["period"] == "daily" and q["difficulty"] == "easy")["completed"]
        assert next(q for q in state["quests"]
                    if q["period"] == "daily" and q["difficulty"] == "hard")["completed"]
        expected.append((account_id, token, state, common.card_id))

    restarted = make_service(tmp_path)
    for index, (account_id, token, old_state, crafted_id) in enumerate(expected):
        response, resumed_id, resumed_token = restarted.process(None, None, {
            "type": "account_resume", "request_id": f"cycle-resume-{index}", "token": token,
        })
        assert response["ok"] and resumed_id == account_id and resumed_token == token
        snapshot = response["snapshot"]
        assert snapshot["gold"] == old_state["gold"]
        assert snapshot["dust"] == old_state["dust"]
        assert len(snapshot["decks"]) == 12
        assert snapshot["packs"] == []
        assert next(card for card in snapshot["cards"] if card["card_id"] == crafted_id)["normal"] >= 1


def test_quest_rewards_are_transactional_and_event_deduplicated(tmp_path):
    service = make_service(tmp_path)
    _, account_id, token = register(service, "AllQuests")
    for version in range(1, 31):
        service.record_accepted_match("quest-cards", version, 0,
                                      [{"type": "CardPlay"}], [account_id, None])
    for index in range(3):
        service.record_accepted_match(f"quest-win-{index}", 1, 0, [],
                                      [account_id, None], ["WON", "LOST"])
    for index in range(3, 5):
        service.record_accepted_match(f"quest-loss-{index}", 1, 0, [],
                                      [account_id, None], ["LOST", "WON"])
    # Duplicate delivery of a completing result cannot pay the reward a second time.
    service.record_accepted_match("quest-loss-4", 1, 0, [],
                                  [account_id, None], ["LOST", "WON"])
    service.record_accepted_match("quest-win-2", 1, 0, [],
                                  [account_id, None], ["WON", "LOST"])
    state = call(service, account_id, token, "account_state", "all-quests-state")["snapshot"]
    assert state["gold"] == 1720
    assert len(state["quests"]) == 8
    assert [quest["reward_gold"] for quest in state["quests"]] == [
        40, 60, 80, 60, 80, 120, 160, 120,
    ]
    assert all(quest["completed"] and quest["progress"] == quest["target"]
               for quest in state["quests"])


def test_timezone_quest_refresh_and_progress_only_from_accepted_match_events(tmp_path):
    current = [datetime(2026, 10, 25, 23, 59, 59, tzinfo=SHANGHAI).timestamp()]
    service = make_service(tmp_path, clock=lambda: current[0])
    response, account_id, token = register(service, "QuestUser")
    snapshot = response["snapshot"]
    assert snapshot["daily_reset_at"] == int(datetime(2026, 10, 26, tzinfo=SHANGHAI).timestamp())
    assert snapshot["weekly_reset_at"] == int(datetime(2026, 10, 26, tzinfo=SHANGHAI).timestamp())
    daily = [q for q in snapshot["quests"] if q["period"] == "daily"]
    weekly = [q for q in snapshot["quests"] if q["period"] == "weekly"]
    assert len(daily) == len(weekly) == 4
    assert [q["reward_gold"] for q in daily] == [40, 60, 80, 60]
    assert [q["reward_gold"] for q in weekly] == [80, 120, 160, 120]
    assert daily[-1]["quest_id"].endswith(":fourth")
    assert weekly[-1]["quest_id"].endswith(":fourth")
    assert daily[-1]["difficulty"] == weekly[-1]["difficulty"] == "normal"
    assert daily[-1]["description"] == "完成2场对局"
    assert weekly[-1]["description"] == "完成5场对局"

    # These events come from accepted Match.apply calls; rejected commands do not call the recorder.
    match = Match(["QuestUser", "Guest"], [4, 10], [preset(4), preset(10)], seed=91)
    version = match.version
    for seat in (0, 1):
        assert match.apply(seat, {"kind": "mulligan", "cards": [], "match_id": match.id,
                                  "version": version, "action_id": f"mulligan-{seat}"})
        service.record_accepted_match(match.id, match.version, seat, match.observer.events,
                                      [account_id, None])
    accepted_card_play = False
    for action_index in range(40):
        if match.game.ended:
            break
        seat = match.game.players.index(match.game.current_player)
        moves = match.legal_moves(seat)
        move = next((item for item in moves if item["kind"] == "play"), None)
        if move is None:
            move = next(item for item in moves if item["kind"] == "end_turn")
        command = dict(move, match_id=match.id, version=match.version,
                       action_id=f"action-{action_index}")
        assert match.apply(seat, command)
        service.record_accepted_match(match.id, match.version, seat, match.observer.events,
                                      [account_id, None])
        if seat == 0 and any(event["type"] == "CardPlay" for event in match.observer.events):
            accepted_card_play = True
            break
    assert accepted_card_play
    state = call(service, account_id, token, "account_state", "quest-after-card")["snapshot"]
    daily_normal = next(q for q in state["quests"] if q["period"] == "daily" and q["difficulty"] == "normal")
    assert daily_normal["progress"] == 1

    # Duplicate event delivery and a rejected command cannot award progress twice.
    event = {"type": "CardPlay", "owner": 0}
    service.record_accepted_match("fixed-match", 7, 0, [event], [account_id, None])
    once = call(service, account_id, token, "account_state", "quest-once")["snapshot"]
    service.record_accepted_match("fixed-match", 7, 0, [event], [account_id, None])
    twice = call(service, account_id, token, "account_state", "quest-twice")["snapshot"]
    assert next(q for q in twice["quests"] if q["quest_id"] == daily_normal["quest_id"])["progress"] == \
        next(q for q in once["quests"] if q["quest_id"] == daily_normal["quest_id"])["progress"]

    daily_fourth = next(q for q in twice["quests"] if q["quest_id"].endswith(":fourth")
                        and q["period"] == "daily")
    rerolled_fourth = call(service, account_id, token, "account_quest_reroll", "reroll-fourth",
                           quest_id=daily_fourth["quest_id"])
    changed_fourth = next(q for q in rerolled_fourth["snapshot"]["quests"]
                          if q["quest_id"] == daily_fourth["quest_id"])
    assert rerolled_fourth["ok"]
    assert changed_fourth["difficulty"] == "normal"
    assert changed_fourth["reward_gold"] == 60 and changed_fourth["target"] == 10
    assert not changed_fourth["reroll_available"]

    easy = next(q for q in twice["quests"] if q["period"] == "daily" and q["difficulty"] == "easy")
    reroll = call(service, account_id, token, "account_quest_reroll", "reroll-once",
                  quest_id=easy["quest_id"])
    assert reroll["ok"]
    changed_easy = next(q for q in reroll["snapshot"]["quests"] if q["quest_id"] == easy["quest_id"])
    assert changed_easy["reroll_available"] is False and changed_easy["target"] == 3
    assert call(service, account_id, token, "account_quest_reroll", "reroll-once",
                quest_id=easy["quest_id"]) == reroll
    second_reroll = call(service, account_id, token, "account_quest_reroll", "reroll-twice",
                         quest_id=easy["quest_id"])
    assert not second_reroll["ok"]

    current[0] = datetime(2026, 10, 26, 0, 0, 1, tzinfo=SHANGHAI).timestamp()
    refreshed = call(service, account_id, token, "account_state", "quest-after-reset")["snapshot"]
    refreshed_daily = [q for q in refreshed["quests"] if q["period"] == "daily"]
    refreshed_weekly = [q for q in refreshed["quests"] if q["period"] == "weekly"]
    assert len(refreshed_daily) == len(refreshed_weekly) == 4
    assert {q["quest_id"].split(":")[1] for q in refreshed["quests"] if q["period"] == "daily"} == {"2026-10-26"}
    assert {q["quest_id"].split(":")[1] for q in refreshed["quests"] if q["period"] == "weekly"} == {"2026-10-26"}
    assert all(q["progress"] == 0 and q["reroll_available"]
               for q in refreshed_daily + refreshed_weekly)


def test_full_collection_snapshot_stays_under_client_line_limit(tmp_path):
    service = make_service(tmp_path)
    _, account_id, token = register(service, "LargeCollection")
    conn = service.store.connect()
    try:
        conn.execute("BEGIN IMMEDIATE")
        conn.execute("DELETE FROM collections WHERE account_id=?", (account_id,))
        conn.executemany(
            "INSERT INTO collections(account_id,card_id,normal,golden,free) VALUES(?,?,2,1,0)",
            [(account_id, card_id) for card_id in service.catalog.cards],
        )
        conn.execute("DELETE FROM decks WHERE account_id=?", (account_id,))
        deck_cards = preset(4)
        for index in range(30):
            conn.execute(
                "INSERT INTO decks(deck_id,account_id,name,class_id,cards_json,revision,created_at,updated_at) "
                "VALUES(?,?,?,?,?,1,0,0)",
                (f"stress-deck-{index:02d}", account_id, "压力卡组" * 5, 4,
                 json.dumps(deck_cards, ensure_ascii=False)),
            )
        conn.executemany("INSERT INTO packs(account_id,set_id,count) VALUES(?,?,1000)",
                         [(account_id, set_id) for set_id in service.catalog.pack_sets])
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()
    response = call(service, account_id, token, "account_state", "max-snapshot")
    payload_bytes = len(json.dumps(response, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
    assert len(response["snapshot"]["cards"]) == len(service.catalog.cards)
    assert len(response["snapshot"]["decks"]) == 30
    assert payload_bytes < 1_000_000


def test_accounts_v1_tcp_resume_join_progress_and_restart(tmp_path):
    async def run():
        db_path = tmp_path / "tcp-accounts.sqlite3"
        account_service = make_service(tmp_path, "tcp-accounts.sqlite3")
        server = Server(tmp_path / "tcp-matches", account_service=account_service)
        listener = await asyncio.start_server(server.handle, "127.0.0.1", 0, limit=32768)
        port = listener.sockets[0].getsockname()[1]

        async def connect():
            reader, writer = await asyncio.open_connection("127.0.0.1", port)
            hello = json.loads(await asyncio.wait_for(reader.readline(), 5))
            assert "accounts_v1" in hello["capabilities"]
            assert "account_quests_four_slots_v1" in hello["capabilities"]
            return reader, writer

        async def send(writer, value):
            writer.write((json.dumps(value, ensure_ascii=False) + "\n").encode("utf-8"))
            await writer.drain()

        async def recv(reader):
            return json.loads(await asyncio.wait_for(reader.readline(), 8))

        menu = []
        for username, display in (("tcp-one", "TCP一"), ("tcp-two", "TCP二")):
            reader, writer = await connect()
            menu.append((reader, writer))
            await send(writer, {"type": "account_register", "request_id": f"register-{username}",
                                "username": username, "password": PASSWORD, "display_name": display})
            response = await recv(reader)
            assert response["type"] == "account_result" and response["ok"]
        account_results = []
        for reader, _ in menu:
            # Keep the returned registration result available from a second idempotent request.
            # The menu peer remains authenticated from register, so state also proves account isolation.
            await send(menu[menu.index((reader, _))][1], {
                "type": "account_state", "request_id": f"state-{len(account_results)}"})
            account_results.append(await recv(reader))
        accounts = [result["snapshot"] for result in account_results]
        tokens = []
        for i, snapshot in enumerate(accounts):
            replay_reader, replay_writer = await connect()
            await send(replay_writer, {"type": "account_login", "request_id": f"login-tcp-{i}",
                                       "username": snapshot["username"], "password": PASSWORD})
            login = await recv(replay_reader)
            assert login["ok"]
            tokens.append(login["token"])
            replay_writer.close()
            await replay_writer.wait_closed()

        games = []
        for index, snapshot in enumerate(accounts):
            reader, writer = await connect()
            games.append((reader, writer))
            await send(writer, {"type": "account_resume", "request_id": f"resume-game-{index}",
                                "token": tokens[index]})
            resumed = await recv(reader)
            assert resumed["ok"] and resumed["snapshot"]["account_id"] == snapshot["account_id"]
            await send(writer, {"type": "join", "deck_id": snapshot["decks"][0]["deck_id"],
                                "account_id": "spoofed", "class_id": 10, "deck": []})
            queued = await recv(reader)
            assert queued["type"] == "queued"

        duplicate_reader, duplicate_writer = await connect()
        await send(duplicate_writer, {"type": "account_resume", "request_id": "resume-duplicate",
                                      "token": tokens[0]})
        assert (await recv(duplicate_reader))["ok"]
        await send(duplicate_writer, {"type": "join", "deck_id": accounts[0]["decks"][0]["deck_id"]})
        assert (await recv(duplicate_reader))["type"] == "error"
        duplicate_writer.close()
        await duplicate_writer.wait_closed()

        # The second account join started a match and the first queue response is followed by state.
        states = [await recv(reader) for reader, _ in games]
        assert states[0]["match_id"] == states[1]["match_id"]
        assert [player["name"] for player in states[0]["players"]] == ["TCP一", "TCP二"]
        await send(games[0][1], {"type": "action", "kind": "concede",
                                 "match_id": states[0]["match_id"], "version": states[0]["version"],
                                 "action_id": "tcp-concede"})
        finished = [await recv(reader) for reader, _ in games]
        assert all(state["phase"] == "finished" for state in finished)
        assert [player["result"] for player in finished[0]["players"]] == ["LOST", "WON"]

        updated = []
        for index, (reader, writer) in enumerate(menu):
            await send(writer, {"type": "account_state", "request_id": f"post-match-{index}"})
            updated.append((await recv(reader))["snapshot"])
        assert updated[0]["gold"] == 1040
        assert updated[1]["gold"] == 1120
        assert next(q for q in updated[1]["quests"]
                    if q["period"] == "daily" and q["difficulty"] == "hard")["completed"]

        for _, writer in games:
            writer.close()
            await writer.wait_closed()
        for _, writer in menu:
            writer.close()
            await writer.wait_closed()
        listener.close()
        await listener.wait_closed()

        restarted_server = Server(tmp_path / "tcp-matches-restarted",
                                  account_service=AccountService(db_path, password_iterations=10_000))
        restarted_listener = await asyncio.start_server(restarted_server.handle, "127.0.0.1", 0)
        new_reader, new_writer = await asyncio.open_connection(
            "127.0.0.1", restarted_listener.sockets[0].getsockname()[1])
        await new_reader.readline()
        await send(new_writer, {"type": "account_resume", "request_id": "resume-after-restart",
                                "token": tokens[1]})
        persisted = await recv(new_reader)
        assert persisted["ok"] and persisted["snapshot"]["gold"] == 1120
        new_writer.close()
        await new_writer.wait_closed()
        restarted_listener.close()
        await restarted_listener.wait_closed()

    asyncio.run(run())
