import asyncio
import json
import sqlite3

from game.accounts import AccountService
from game.server import Server


PASSWORD = "correct-horse-battery"


def make_service(tmp_path, name="social.sqlite3"):
    return AccountService(tmp_path / name, password_iterations=10_000, golden_rate=0)


def register(service, username, display_name=None):
    response, account_id, token = service.process(None, None, {
        "type": "account_register",
        "request_id": f"register-{username}",
        "username": username,
        "password": PASSWORD,
        "display_name": display_name or username,
    })
    assert response["ok"]
    return response, account_id, token


def call(service, account, token, operation, request_id, **fields):
    return service.process(account, token, {
        "type": operation, "request_id": request_id, **fields,
    })[0]


def test_friend_requests_authorize_self_duplicate_accept_reject_cancel_and_remove(tmp_path):
    service = make_service(tmp_path)
    first, first_id, first_token = register(service, "friend-first", "第一位")
    second, second_id, second_token = register(service, "friend-second", "第二位")
    _, other_id, other_token = register(service, "friend-other", "旁观者")

    self_add = call(service, first_id, first_token, "account_friend_add", "social-self",
                    username="friend-first")
    assert not self_add["ok"] and "自己" in self_add["error"]
    missing = call(service, first_id, first_token, "account_friend_add", "social-missing",
                   username="not-registered")
    assert not missing["ok"]

    blocked = call(service, second_id, second_token, "account_privacy_update", "social-block-requests",
                    hide_online=False, allow_friend_requests=False)
    assert blocked["ok"] and blocked["snapshot"]["privacy"]["allow_friend_requests"] is False
    denied = call(service, first_id, first_token, "account_friend_add", "social-denied",
                  username="FRIEND-SECOND")
    assert not denied["ok"] and "不接收" in denied["error"]
    assert denied["snapshot"]["outgoing_friend_requests"] == []

    call(service, second_id, second_token, "account_privacy_update", "social-allow-requests",
         hide_online=False, allow_friend_requests=True)
    sent = call(service, first_id, first_token, "account_friend_add", "social-add-second",
                username="friend-second")
    assert sent["ok"]
    friend_request_id = sent["friend_request_id"]
    assert len(friend_request_id) == 32
    assert sent["snapshot"]["outgoing_friend_requests"][0]["request_id"] == friend_request_id
    assert sent["snapshot"]["outgoing_friend_requests"][0]["account_id"] == second_id
    assert sent["snapshot"]["outgoing_friend_requests"][0]["short_id"] == second_id[:8]

    replay = call(service, first_id, first_token, "account_friend_add", "social-add-second",
                  username="friend-second")
    assert replay == sent
    duplicate = call(service, first_id, first_token, "account_friend_add", "social-add-duplicate",
                     username="friend-second")
    assert not duplicate["ok"] and "已发送" in duplicate["error"]
    assert len(duplicate["snapshot"]["outgoing_friend_requests"]) == 1

    incoming = call(service, second_id, second_token, "account_state", "social-incoming")["snapshot"]
    assert incoming["incoming_friend_requests"][0]["request_id"] == friend_request_id
    unauthorized = call(service, other_id, other_token, "account_friend_accept", "social-unauthorized",
                        friend_request_id=friend_request_id)
    assert not unauthorized["ok"] and "无权" in unauthorized["error"]

    accepted = call(service, second_id, second_token, "account_friend_accept", "social-accept",
                     friend_request_id=friend_request_id)
    assert accepted["ok"] and accepted["snapshot"]["incoming_friend_requests"] == []
    assert accepted["snapshot"]["friends"][0]["account_id"] == first_id
    assert accepted["snapshot"]["friends"][0]["display_name"] == "第一位"
    assert call(service, first_id, first_token, "account_state", "social-bilateral-friends")[
        "snapshot"]["friends"][0]["account_id"] == second_id

    unauthorized_remove = call(service, other_id, other_token, "account_friend_remove",
                               "social-remove-unauthorized", account_id=first_id)
    assert not unauthorized_remove["ok"]
    removed = call(service, second_id, second_token, "account_friend_remove", "social-remove",
                   account_id=first_id)
    assert removed["ok"] and removed["snapshot"]["friends"] == []
    assert call(service, first_id, first_token, "account_state", "social-removed-both-sides")[
        "snapshot"]["friends"] == []

    rejected_request = call(service, first_id, first_token, "account_friend_add", "social-add-reject",
                            username="friend-second")
    rejected = call(service, second_id, second_token, "account_friend_reject", "social-reject",
                    friend_request_id=rejected_request["friend_request_id"])
    assert rejected["ok"] and rejected["snapshot"]["incoming_friend_requests"] == []
    assert call(service, first_id, first_token, "account_state", "social-rejected-outgoing")[
        "snapshot"]["outgoing_friend_requests"] == []

    cancelled_request = call(service, first_id, first_token, "account_friend_add", "social-add-cancel",
                             username="friend-second")
    cancelled = call(service, first_id, first_token, "account_friend_cancel", "social-cancel",
                     friend_request_id=cancelled_request["friend_request_id"])
    assert cancelled["ok"] and cancelled["snapshot"]["outgoing_friend_requests"] == []
    unauthorized_cancel = call(service, second_id, second_token, "account_friend_cancel",
                               "social-cancel-unauthorized",
                               friend_request_id=cancelled_request["friend_request_id"])
    assert not unauthorized_cancel["ok"]


def test_friendship_and_privacy_persist_after_service_restart(tmp_path):
    service = make_service(tmp_path)
    _, first_id, first_token = register(service, "persist-first", "保持一")
    _, second_id, second_token = register(service, "persist-second", "保持二")
    added = call(service, first_id, first_token, "account_friend_add", "persist-add",
                 username="persist-second")
    accepted = call(service, second_id, second_token, "account_friend_accept", "persist-accept",
                    friend_request_id=added["friend_request_id"])
    assert accepted["ok"]
    privacy = call(service, second_id, second_token, "account_privacy_update", "persist-privacy",
                   hide_online=False, allow_friend_requests=False)
    assert privacy["ok"]
    service.record_last_seen(first_id)
    original_gold = privacy["snapshot"]["gold"]
    original_deck_count = len(privacy["snapshot"]["decks"])

    restarted = make_service(tmp_path)
    first = restarted.process(None, None, {
        "type": "account_resume", "request_id": "persist-resume-first", "token": first_token,
    })[0]
    second = restarted.process(None, None, {
        "type": "account_resume", "request_id": "persist-resume-second", "token": second_token,
    })[0]
    assert first["ok"] and first["snapshot"]["friends"][0]["account_id"] == second_id
    assert second["ok"] and second["snapshot"]["friends"][0]["account_id"] == first_id
    assert second["snapshot"]["privacy"] == {
        "hide_online": False, "allow_friend_requests": False,
    }
    assert isinstance(second["snapshot"]["friends"][0]["last_seen"], int)
    assert second["snapshot"]["gold"] == original_gold
    assert len(second["snapshot"]["decks"]) == original_deck_count


def test_social_schema_migrates_legacy_account_pack_and_quest_rows(tmp_path):
    database_path = tmp_path / "legacy.sqlite3"
    account_id = "a" * 32
    with sqlite3.connect(database_path) as conn:
        conn.executescript("""
            CREATE TABLE accounts (
                account_id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL, password_salt BLOB NOT NULL,
                password_hash BLOB NOT NULL, gold INTEGER NOT NULL,
                dust INTEGER NOT NULL, revision INTEGER NOT NULL, created_at INTEGER NOT NULL
            );
            CREATE TABLE packs (
                account_id TEXT NOT NULL, set_id INTEGER NOT NULL,
                count INTEGER NOT NULL, PRIMARY KEY (account_id,set_id)
            );
            CREATE TABLE quests (
                account_id TEXT NOT NULL, quest_id TEXT NOT NULL, period TEXT NOT NULL,
                period_key TEXT NOT NULL, difficulty TEXT NOT NULL, quest_type TEXT NOT NULL,
                title TEXT NOT NULL, description TEXT NOT NULL, progress INTEGER NOT NULL,
                target INTEGER NOT NULL, reward_gold INTEGER NOT NULL, completed INTEGER NOT NULL,
                rerolls INTEGER NOT NULL, PRIMARY KEY (account_id,quest_id)
            );
        """)
        conn.execute(
            "INSERT INTO accounts VALUES(?,?,?,?,?,?,?,?,?)",
            (account_id, "legacy-user", "旧账号", bytes(16), bytes(32), 777, 12, 9, 100),
        )
        conn.execute("INSERT INTO packs VALUES(?,?,?)", (account_id, 3, 2))
        conn.execute(
            "INSERT INTO quests VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (account_id, "daily:2026-10-30:easy", "daily", "2026-10-30", "easy",
             "games", "旧任务", "已保存进度", 1, 2, 40, 0, 0),
        )

    make_service(tmp_path, "legacy.sqlite3")
    with sqlite3.connect(database_path) as conn:
        account = conn.execute(
            "SELECT gold,dust,revision,last_seen_at FROM accounts WHERE account_id=?",
            (account_id,),
        ).fetchone()
        pack = conn.execute(
            "SELECT count FROM packs WHERE account_id=? AND set_id=3", (account_id,)
        ).fetchone()
        quest = conn.execute(
            "SELECT progress,target,reward_gold,slot FROM quests WHERE account_id=?",
            (account_id,),
        ).fetchone()
    assert account == (777, 12, 9, None)
    assert pack == (2,)
    assert quest == (1, 2, 40, 0)


def test_tcp_presence_uses_authenticated_peer_reference_count_and_privacy(tmp_path):
    async def run():
        service = make_service(tmp_path)
        server = Server(tmp_path / "social-matches", account_service=service)
        listener = await asyncio.start_server(server.handle, "127.0.0.1", 0, limit=32768)
        port = listener.sockets[0].getsockname()[1]

        async def connect():
            reader, writer = await asyncio.open_connection("127.0.0.1", port)
            hello = json.loads(await asyncio.wait_for(reader.readline(), 5))
            assert "account_social_v1" in hello["capabilities"]
            assert "account_quests_four_slots_v1" in hello["capabilities"]
            return reader, writer

        async def send(writer, value):
            writer.write((json.dumps(value, ensure_ascii=False) + "\n").encode("utf-8"))
            await writer.drain()

        async def receive(reader):
            return json.loads(await asyncio.wait_for(reader.readline(), 5))

        async def account_request(reader, writer, operation, request_id, **fields):
            await send(writer, {"type": operation, "request_id": request_id, **fields})
            response = await receive(reader)
            assert response["type"] == "account_result", response
            return response

        async def wait_until(predicate):
            for _ in range(100):
                if predicate():
                    return
                await asyncio.sleep(0.01)
            assert predicate()

        opened = []
        try:
            first_reader, first_writer = await connect()
            opened.append((first_reader, first_writer))
            first = await account_request(first_reader, first_writer, "account_register", "tcp-presence-a",
                                          username="tcp-presence-a", password=PASSWORD,
                                          display_name="在线甲")
            first_id = first["snapshot"]["account_id"]

            second_reader, second_writer = await connect()
            opened.append((second_reader, second_writer))
            second = await account_request(second_reader, second_writer, "account_register", "tcp-presence-b",
                                           username="tcp-presence-b", password=PASSWORD,
                                           display_name="在线乙")
            second_id = second["snapshot"]["account_id"]

            added = await account_request(first_reader, first_writer, "account_friend_add", "tcp-friend-add",
                                          username="tcp-presence-b")
            accepted = await account_request(second_reader, second_writer, "account_friend_accept",
                                             "tcp-friend-accept",
                                             friend_request_id=added["friend_request_id"])
            assert accepted["snapshot"]["friends"][0]["online"] is True

            second_first_reader, second_first_writer = await connect()
            opened.append((second_first_reader, second_first_writer))
            login = await account_request(second_first_reader, second_first_writer, "account_login",
                                          "tcp-presence-a-login", username="tcp-presence-a",
                                          password=PASSWORD)
            assert login["ok"] and login["snapshot"]["account_id"] == first_id
            assert server.online_accounts[first_id] == 2

            visible = await account_request(second_reader, second_writer, "account_state", "tcp-visible")
            assert visible["snapshot"]["friends"][0]["online"] is True

            hidden = await account_request(second_first_reader, second_first_writer,
                                           "account_privacy_update", "tcp-hide-online",
                                           hide_online=True, allow_friend_requests=True)
            assert hidden["ok"]
            private_view = await account_request(second_reader, second_writer, "account_state", "tcp-hidden")
            assert private_view["snapshot"]["friends"][0]["online"] is False
            assert private_view["snapshot"]["friends"][0]["last_seen"] is None

            logout = await account_request(second_first_reader, second_first_writer, "account_logout",
                                           "tcp-logout-a")
            assert logout["ok"] and server.online_accounts[first_id] == 1
            visible_again = await account_request(first_reader, first_writer,
                                                  "account_privacy_update", "tcp-show-online",
                                                  hide_online=False, allow_friend_requests=True)
            assert visible_again["ok"]
            friend_online = await account_request(second_reader, second_writer,
                                                  "account_state", "tcp-visible-after-logout")
            assert friend_online["snapshot"]["friends"][0]["online"] is True

            first_writer.close()
            await first_writer.wait_closed()
            await wait_until(lambda: first_id not in server.online_accounts)
            friend_offline = await account_request(second_reader, second_writer,
                                                   "account_state", "tcp-offline-last-seen")
            friend = friend_offline["snapshot"]["friends"][0]
            assert friend["account_id"] == first_id
            assert friend["online"] is False
            assert isinstance(friend["last_seen"], int) and friend["last_seen"] > 0
            assert friend_offline["snapshot"]["account_id"] == second_id
        finally:
            for _, writer in opened:
                writer.close()
            for _, writer in opened:
                try:
                    await writer.wait_closed()
                except (ConnectionError, OSError):
                    pass
            listener.close()
            await listener.wait_closed()

    asyncio.run(run())
