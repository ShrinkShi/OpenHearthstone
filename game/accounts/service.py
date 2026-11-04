"""Authoritative account and economy operations backed by SQLite."""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import math
import re
import secrets
import sqlite3
import time
import uuid
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Callable

from ..database import HEROES, validate_deck
from .catalog import CardCatalog, get_catalog
from .economy import (
    DEFAULT_PACK_WEIGHTS,
    GOLDEN_CRAFT_COST,
    GOLDEN_DUST_VALUE,
    NORMAL_CRAFT_COST,
    NORMAL_DUST_VALUE,
    PACK_PRICE,
    PACK_SIZE,
    QUESTS,
    QUEST_REWARDS,
    QUEST_SLOTS,
)
from .persistence import SqliteStore
from .deck_code import FORMATS, decode_deck, encode_deck


SHANGHAI = timezone(timedelta(hours=8), name="UTC+08:00")
SESSION_LIFETIME = 30 * 24 * 60 * 60
MAX_REQUEST_ID_LENGTH = 128
MAX_MUTATION_COUNT = 1000
MAX_DECKS = 30
PASSWORD_ITERATIONS = 310_000
_USERNAME_CHARS = frozenset("_.-")
_REQUEST_ID = re.compile(r"^[A-Za-z0-9._:-]{1,128}$")


class AccountError(ValueError):
    """A safe, user-facing account error without credentials or internals."""


class AccountService:
    def __init__(
        self,
        database_path: str | Path,
        *,
        clock: Callable[[], float] | None = None,
        pack_weights: dict[int, float] | None = None,
        golden_rate: float = 0.05,
        duplicate_protection: bool = True,
        password_iterations: int = PASSWORD_ITERATIONS,
        catalog: CardCatalog | None = None,
        rng=None,
    ):
        self.store = SqliteStore(database_path)
        self.catalog = catalog or get_catalog()
        self.clock = clock or time.time
        self.rng = rng or secrets.SystemRandom()
        self.pack_weights = self._validate_weights(
            DEFAULT_PACK_WEIGHTS if pack_weights is None else pack_weights)
        if isinstance(golden_rate, bool) or not isinstance(golden_rate, (int, float)):
            raise ValueError("golden_rate must be a number")
        if not math.isfinite(golden_rate) or not 0 <= golden_rate <= 1:
            raise ValueError("golden_rate must be between 0 and 1")
        self.golden_rate = float(golden_rate)
        if type(duplicate_protection) is not bool:
            raise ValueError("duplicate_protection must be bool")
        self.duplicate_protection = duplicate_protection
        if type(password_iterations) is not int or password_iterations < 10_000:
            raise ValueError("password_iterations must be at least 10000")
        self.password_iterations = password_iterations
        self.token_key = self._load_token_key(database_path)
        self._is_online: Callable[[str], bool] = lambda account_id: False

    def set_presence_provider(self, provider: Callable[[str], bool]) -> None:
        self._is_online = provider

    def record_last_seen(self, account_id: str) -> None:
        with self.store.transaction() as conn:
            conn.execute("UPDATE accounts SET last_seen_at=? WHERE account_id=?",
                         (self._now(), account_id))

    def session_is_active(self, account_id: str, token: str) -> bool:
        try:
            with self.store.transaction() as conn:
                self._active_account_in(conn, account_id, token)
            return True
        except AccountError:
            return False

    @staticmethod
    def _validate_weights(weights: dict[int, float]) -> dict[int, float]:
        if not isinstance(weights, dict) or not weights:
            raise ValueError("pack_weights must be a non-empty mapping")
        cleaned = {}
        for rarity, weight in weights.items():
            if type(rarity) is not int or rarity not in (1, 3, 4, 5):
                raise ValueError("pack rarity weights must use rarity ids 1, 3, 4, or 5")
            if isinstance(weight, bool) or not isinstance(weight, (int, float)):
                raise ValueError("pack weights must be numbers")
            if not math.isfinite(weight) or weight < 0:
                raise ValueError("pack weights must be finite and non-negative")
            cleaned[rarity] = float(weight)
        if not any(weight > 0 for weight in cleaned.values()):
            raise ValueError("at least one pack rarity weight must be positive")
        if not any(cleaned.get(rarity, 0) > 0 for rarity in (3, 4, 5)):
            raise ValueError("pack weights must allow a rare or higher card")
        return cleaned

    @staticmethod
    def _load_token_key(database_path: str | Path) -> bytes:
        key_path = Path(str(database_path) + ".token-key")
        key_path.parent.mkdir(parents=True, exist_ok=True)
        if not key_path.exists():
            conn = sqlite3.connect(str(database_path))
            try:
                sessions = conn.execute("SELECT count(*) FROM sessions").fetchone()[0]
            finally:
                conn.close()
            if sessions:
                raise ValueError("account token key is missing for an existing database")
        try:
            with key_path.open("xb") as stream:
                stream.write(secrets.token_bytes(32))
        except FileExistsError:
            pass
        except OSError:
            raise ValueError("account token key cannot be created") from None
        if not key_path.exists():
            raise ValueError("account token key is missing")
        key = key_path.read_bytes()
        if len(key) != 32:
            raise ValueError("account token key has an invalid length")
        return key

    def _now(self) -> int:
        return int(self.clock())

    @staticmethod
    def _safe_request_id(request: dict) -> str:
        value = request.get("request_id")
        if not isinstance(value, str) or not _REQUEST_ID.fullmatch(value):
            raise AccountError("请求编号格式无效")
        return value

    @staticmethod
    def _fingerprint(request: dict) -> str:
        try:
            canonical = json.dumps(request, ensure_ascii=False, sort_keys=True,
                                   separators=(",", ":"), allow_nan=False)
        except (TypeError, ValueError):
            raise AccountError("请求格式无效") from None
        return hashlib.sha256(canonical.encode("utf-8")).hexdigest()

    @staticmethod
    def _username(raw: object) -> str:
        if not isinstance(raw, str):
            raise AccountError("账号名格式无效")
        value = raw.strip().casefold()
        if not 3 <= len(value) <= 24 or not any(ch.isalnum() for ch in value):
            raise AccountError("账号名需为3到24个字母、数字或常用符号")
        if any(not (ch.isalnum() or ch in _USERNAME_CHARS) for ch in value):
            raise AccountError("账号名需为3到24个字母、数字或常用符号")
        return value

    @staticmethod
    def _display_name(raw: object, fallback: str) -> str:
        value = fallback if raw is None else raw
        if (not isinstance(value, str) or not 1 <= len(value.strip()) <= 32
                or any(not ch.isprintable() for ch in value)):
            raise AccountError("显示名称需为1到32个可见字符")
        return value.strip()

    @staticmethod
    def _password(raw: object) -> str:
        if not isinstance(raw, str) or not 8 <= len(raw) <= 128:
            raise AccountError("密码长度需为8到128个字符")
        return raw

    @staticmethod
    def _count(raw: object, *, field: str = "数量", maximum: int = MAX_MUTATION_COUNT) -> int:
        if type(raw) is not int or not 1 <= raw <= maximum:
            raise AccountError(f"{field}必须是1到{maximum}之间的整数")
        return raw

    @staticmethod
    def _account_key(account_id: str) -> str:
        return "account:" + account_id

    @staticmethod
    def _preauth_key(username: str) -> str:
        digest = hashlib.sha256(username.encode("utf-8")).hexdigest()
        return "preauth:" + digest

    def _request_token(self, account_id: str, operation: str, request_id: str,
                       fingerprint: str) -> str:
        material = "\0".join((account_id, operation, request_id, fingerprint)).encode("utf-8")
        digest = hmac.new(self.token_key, b"account-session\0" + material, hashlib.sha256).digest()
        return base64.urlsafe_b64encode(digest).decode("ascii").rstrip("=")

    @staticmethod
    def _token_hash(token: str) -> bytes:
        return hashlib.sha256(token.encode("utf-8")).digest()

    @staticmethod
    def _result(operation: str, request_id: str, *, ok: bool, error: str = "",
                snapshot: dict | None = None, token: str | None = None,
                opened_cards: list | None = None, open_id: str | None = None,
                **extra) -> dict:
        result = {
            "type": "account_result", "operation": operation,
            "request_id": request_id, "ok": ok, "error": error,
            "snapshot": snapshot,
        }
        if token is not None:
            result["token"] = token
        if opened_cards is not None:
            result["opened_cards"] = opened_cards
            result["open_id"] = open_id or ""
        result.update(extra)
        return result

    def error_response(self, operation: str, request_id: object, error: str) -> dict:
        safe_operation = operation if isinstance(operation, str) and operation.startswith("account_") else "account_unknown"
        safe_request_id = request_id if isinstance(request_id, str) and _REQUEST_ID.fullmatch(request_id) else ""
        return self._result(safe_operation, safe_request_id, ok=False, error=error,
                            opened_cards=[] if safe_operation == "account_pack_open" else None,
                            open_id="" if safe_operation == "account_pack_open" else None)

    @staticmethod
    def _dedup_result(conn: sqlite3.Connection, account_key: str, operation: str,
                      request_id: str, fingerprint: str) -> dict | None:
        row = conn.execute(
            "SELECT fingerprint, response_json FROM request_dedup "
            "WHERE account_key=? AND operation=? AND request_id=?",
            (account_key, operation, request_id),
        ).fetchone()
        if row is None:
            return None
        if not hmac.compare_digest(row["fingerprint"], fingerprint):
            raise AccountError("该请求编号已用于不同内容，请重新提交")
        return json.loads(row["response_json"])

    def _save_dedup(self, conn: sqlite3.Connection, account_key: str, operation: str,
                    request_id: str, fingerprint: str, response: dict) -> None:
        stored = dict(response)
        stored.pop("token", None)
        conn.execute(
            "INSERT INTO request_dedup(account_key,operation,request_id,fingerprint,response_json,created_at) "
            "VALUES(?,?,?,?,?,?)",
            (account_key, operation, request_id, fingerprint,
             json.dumps(stored, ensure_ascii=False, separators=(",", ":")), self._now()),
        )

    def _run_preauth_mutation(self, operation: str, request: dict, username_key: str,
                              work: Callable[[sqlite3.Connection, str, str], tuple[dict, str | None]]) -> tuple[dict, str | None, str | None]:
        request_id = self._safe_request_id(request)
        fingerprint = self._fingerprint(request)
        account_key = self._preauth_key(username_key)
        with self.store.transaction() as conn:
            previous = self._dedup_result(conn, account_key, operation, request_id, fingerprint)
            if previous is not None:
                token = None
                account_id = (previous.get("snapshot") or {}).get("account_id")
                if previous.get("ok") and account_id:
                    token = self._request_token(account_id, operation, request_id, fingerprint)
                    previous["token"] = token
                return previous, account_id, token
            conn.execute("SAVEPOINT account_operation")
            account_id = None
            token = None
            try:
                extra, account_id = work(conn, request_id, fingerprint)
                if account_id:
                    token = self._request_token(account_id, operation, request_id, fingerprint)
                response = self._result(operation, request_id, ok=True,
                                        snapshot=self._snapshot(conn, account_id) if account_id else None,
                                        token=token, **extra)
                conn.execute("RELEASE SAVEPOINT account_operation")
            except AccountError as exc:
                conn.execute("ROLLBACK TO SAVEPOINT account_operation")
                conn.execute("RELEASE SAVEPOINT account_operation")
                response = self._result(operation, request_id, ok=False, error=str(exc))
                account_id = None
                token = None
            self._save_dedup(conn, account_key, operation, request_id, fingerprint, response)
            return response, account_id, token

    def _run_account_mutation(self, account_id: str, operation: str, request: dict,
                              work: Callable[[sqlite3.Connection], dict],
                              token: str | None = None) -> dict:
        request_id = self._safe_request_id(request)
        fingerprint = self._fingerprint(request)
        account_key = self._account_key(account_id)
        with self.store.transaction() as conn:
            if token is not None:
                try:
                    self._active_account_in(conn, account_id, token)
                except AccountError:
                    previous = self._dedup_result(conn, account_key, operation, request_id, fingerprint)
                    if operation == "account_logout" and previous is not None and previous.get("ok"):
                        # A revoked token may retry only its logout acknowledgement, never account data.
                        return self._result(operation, request_id, ok=True)
                    raise
            previous = self._dedup_result(conn, account_key, operation, request_id, fingerprint)
            if previous is not None:
                return previous
            conn.execute("SAVEPOINT account_operation")
            opened_cards = None
            open_id = None
            try:
                extra = work(conn)
                opened_cards = extra.pop("opened_cards", None)
                open_id = extra.pop("open_id", None)
                response = self._result(operation, request_id, ok=True,
                                        snapshot=None if operation == "account_logout"
                                        else self._snapshot(conn, account_id),
                                        opened_cards=opened_cards, open_id=open_id, **extra)
                conn.execute("RELEASE SAVEPOINT account_operation")
            except AccountError as exc:
                conn.execute("ROLLBACK TO SAVEPOINT account_operation")
                conn.execute("RELEASE SAVEPOINT account_operation")
                response = self._result(operation, request_id, ok=False, error=str(exc),
                                        snapshot=None if operation == "account_logout"
                                        else self._snapshot(conn, account_id),
                                        opened_cards=[] if operation == "account_pack_open" else None,
                                        open_id="" if operation == "account_pack_open" else None)
            self._save_dedup(conn, account_key, operation, request_id, fingerprint, response)
            return response

    def _derive_password_hash(self, password: str, salt: bytes) -> bytes:
        return hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt,
                                   self.password_iterations, dklen=32)

    def _new_session(self, conn: sqlite3.Connection, account_id: str, token: str) -> None:
        now = self._now()
        conn.execute(
            "INSERT INTO sessions(session_id,account_id,token_hash,created_at,expires_at,revoked_at) "
            "VALUES(?,?,?,?,?,NULL)",
            (uuid.uuid4().hex, account_id, self._token_hash(token), now, now + SESSION_LIFETIME),
        )

    def _register(self, request: dict) -> tuple[dict, str | None, str | None]:
        try:
            username = self._username(request.get("username"))
        except AccountError:
            username = "invalid"

        def create(conn: sqlite3.Connection, request_id: str, fingerprint: str):
            value = self._username(request.get("username"))
            password = self._password(request.get("password"))
            display_name = self._display_name(request.get("display_name"), value)
            if conn.execute("SELECT 1 FROM accounts WHERE username=?", (value,)).fetchone():
                raise AccountError("该账号名已被使用")
            account_id = uuid.uuid4().hex
            salt = secrets.token_bytes(16)
            password_hash = self._derive_password_hash(password, salt)
            conn.execute(
                "INSERT INTO accounts(account_id,username,display_name,password_salt,password_hash,gold,dust,revision,created_at) "
                "VALUES(?,?,?,?,?,1000,0,0,?)",
                (account_id, value, display_name, salt, password_hash, self._now()),
            )
            for card_id, count in self.catalog.starter_grants().items():
                conn.execute(
                    "INSERT INTO collections(account_id,card_id,free) VALUES(?,?,?)",
                    (account_id, card_id, count),
                )
            for class_id, deck in self.catalog.starter_decks().items():
                deck_id = uuid.uuid4().hex
                deck_name = f"{self.catalog.class_names[class_id]}预组"
                conn.execute(
                    "INSERT INTO decks(deck_id,account_id,name,class_id,cards_json,revision,created_at,updated_at) "
                    "VALUES(?,?,?,?,?,1,?,?)",
                    (deck_id, account_id, deck_name, class_id,
                     json.dumps(deck, ensure_ascii=False), self._now(), self._now()),
                )
            token = self._request_token(account_id, "account_register", request_id, fingerprint)
            self._new_session(conn, account_id, token)
            self._bump_revision(conn, account_id)
            return {}, account_id

        response, account_id, token = self._run_preauth_mutation(
            "account_register", request, username, create)
        if response.get("ok") and account_id and token:
            response["token"] = token
        return response, account_id, token

    def _login(self, request: dict) -> tuple[dict, str | None, str | None]:
        try:
            username = self._username(request.get("username"))
        except AccountError:
            username = "invalid"

        def authenticate(conn: sqlite3.Connection, request_id: str, fingerprint: str):
            value = self._username(request.get("username"))
            password = self._password(request.get("password"))
            row = conn.execute(
                "SELECT account_id,password_salt,password_hash FROM accounts WHERE username=?",
                (value,),
            ).fetchone()
            salt = row["password_salt"] if row else bytes(16)
            expected = row["password_hash"] if row else bytes(32)
            actual = self._derive_password_hash(password, salt)
            if row is None or not hmac.compare_digest(actual, expected):
                raise AccountError("账号名或密码错误")
            account_id = row["account_id"]
            token = self._request_token(account_id, "account_login", request_id, fingerprint)
            self._new_session(conn, account_id, token)
            self._bump_revision(conn, account_id)
            return {}, account_id

        response, account_id, token = self._run_preauth_mutation(
            "account_login", request, username, authenticate)
        if response.get("ok") and account_id and token:
            response["token"] = token
        return response, account_id, token

    def _active_account_in(self, conn: sqlite3.Connection, account_id: str | None,
                           token: object) -> str:
        if not isinstance(token, str) or not 20 <= len(token) <= 256:
            raise AccountError("登录状态已失效，请重新登录")
        session = conn.execute(
            "SELECT account_id,expires_at,revoked_at FROM sessions WHERE token_hash=?",
            (self._token_hash(token),),
        ).fetchone()
        if (session is None or session["revoked_at"] is not None
                or session["expires_at"] <= self._now()
                or (account_id is not None and not hmac.compare_digest(session["account_id"], account_id))):
            raise AccountError("登录状态已失效，请重新登录")
        return session["account_id"]

    def _snapshot(self, conn: sqlite3.Connection, account_id: str) -> dict:
        account = conn.execute(
            "SELECT account_id,username,display_name,gold,dust,revision FROM accounts WHERE account_id=?",
            (account_id,),
        ).fetchone()
        if account is None:
            raise AccountError("账号不存在")
        social = self._social_snapshot(conn, account_id)
        now = self._now()
        self._ensure_quests(conn, account_id, now)
        local_now = datetime.fromtimestamp(now, SHANGHAI)
        next_day = (local_now.date() + timedelta(days=1))
        next_week = local_now.date() + timedelta(days=7 - local_now.weekday())
        daily_reset = int(datetime.combine(next_day, datetime.min.time(), SHANGHAI).timestamp())
        weekly_reset = int(datetime.combine(next_week, datetime.min.time(), SHANGHAI).timestamp())
        card_rows = conn.execute(
            "SELECT card_id,normal,golden,free FROM collections "
            "WHERE account_id=? AND (normal>0 OR golden>0 OR free>0) ORDER BY card_id",
            (account_id,),
        ).fetchall()
        deck_rows = conn.execute(
            "SELECT deck_id,name,class_id,cards_json,revision,format FROM decks "
            "WHERE account_id=? ORDER BY updated_at DESC,deck_id",
            (account_id,),
        ).fetchall()
        pack_rows = conn.execute(
            "SELECT set_id,count FROM packs WHERE account_id=? AND count>0 ORDER BY set_id",
            (account_id,),
        ).fetchall()
        quest_rows = conn.execute(
            "SELECT quest_id,period,slot,difficulty,title,description,progress,target,reward_gold,completed,rerolls "
            "FROM quests WHERE account_id=? AND ((period='daily' AND period_key=?) "
            "OR (period='weekly' AND period_key=?)) "
            "ORDER BY CASE period WHEN 'daily' THEN 0 ELSE 1 END,slot",
            (account_id, self._period_key("daily", now), self._period_key("weekly", now)),
        ).fetchall()
        return {
            "account_id": account["account_id"],
            "username": account["username"],
            "display_name": account["display_name"],
            "gold": account["gold"],
            "dust": account["dust"],
            "revision": account["revision"],
            "cards": [{"card_id": row["card_id"], "normal": row["normal"],
                       "golden": row["golden"], "free": row["free"]} for row in card_rows],
            "decks": [{"deck_id": row["deck_id"], "name": row["name"],
                       "class_id": row["class_id"], "cards": json.loads(row["cards_json"]),
                       "revision": row["revision"], "format": row["format"]} for row in deck_rows],
            "packs": [{"set_id": row["set_id"], "count": row["count"]} for row in pack_rows],
            "quests": [{"quest_id": row["quest_id"], "period": row["period"],
                        "difficulty": row["difficulty"], "title": row["title"],
                        "description": row["description"], "progress": row["progress"],
                        "target": row["target"], "reward_gold": row["reward_gold"],
                        "completed": bool(row["completed"]),
                        "reroll_available": bool(not row["completed"] and row["progress"] == 0
                                                  and row["rerolls"] == 0)}
                       for row in quest_rows],
            "shop": [{"set_id": set_id, "name": self.catalog.set_name(set_id),
                      "price": PACK_PRICE} for set_id in self.catalog.pack_sets],
            **social,
            "daily_reset_at": daily_reset,
            "weekly_reset_at": weekly_reset,
        }

    @staticmethod
    def _account_privacy(conn: sqlite3.Connection, account_id: str) -> dict:
        conn.execute("INSERT OR IGNORE INTO account_privacy(account_id) VALUES(?)", (account_id,))
        row = conn.execute(
            "SELECT hide_online,allow_friend_requests FROM account_privacy WHERE account_id=?",
            (account_id,),
        ).fetchone()
        return {"hide_online": bool(row["hide_online"]),
                "allow_friend_requests": bool(row["allow_friend_requests"])}

    def _social_snapshot(self, conn: sqlite3.Connection, account_id: str) -> dict:
        privacy = self._account_privacy(conn, account_id)
        friend_rows = conn.execute(
            "SELECT a.account_id,a.username,a.display_name,a.last_seen_at,"
            "COALESCE(p.hide_online,0) AS hide_online "
            "FROM friendships f "
            "JOIN accounts a ON a.account_id=CASE WHEN f.account_low=? "
            "THEN f.account_high ELSE f.account_low END "
            "LEFT JOIN account_privacy p ON p.account_id=a.account_id "
            "WHERE f.account_low=? OR f.account_high=? "
            "ORDER BY a.username,a.account_id",
            (account_id, account_id, account_id),
        ).fetchall()

        friends = []
        for row in friend_rows:
            hidden = bool(row["hide_online"])
            online = bool(self._is_online(row["account_id"])) and not hidden
            friends.append({
                "account_id": row["account_id"],
                "short_id": row["account_id"][:8],
                "username": row["username"],
                "display_name": row["display_name"],
                "online": online,
                "last_seen": None if hidden or online else row["last_seen_at"],
            })

        incoming_rows = conn.execute(
            "SELECT r.request_id,a.account_id,a.username,a.display_name,r.created_at "
            "FROM friend_requests r JOIN accounts a ON a.account_id=r.sender_id "
            "WHERE r.recipient_id=? AND r.state='pending' "
            "ORDER BY r.created_at,r.request_id", (account_id,),
        ).fetchall()
        outgoing_rows = conn.execute(
            "SELECT r.request_id,a.account_id,a.username,a.display_name,r.created_at "
            "FROM friend_requests r JOIN accounts a ON a.account_id=r.recipient_id "
            "WHERE r.sender_id=? AND r.state='pending' "
            "ORDER BY r.created_at,r.request_id", (account_id,),
        ).fetchall()

        def request_items(rows):
            return [{"request_id": row["request_id"], "account_id": row["account_id"],
                     "short_id": row["account_id"][:8], "username": row["username"],
                     "display_name": row["display_name"], "created_at": row["created_at"]}
                    for row in rows]

        return {
            "friends": friends,
            "incoming_friend_requests": request_items(incoming_rows),
            "outgoing_friend_requests": request_items(outgoing_rows),
            "privacy": privacy,
        }

    @staticmethod
    def _social_request_id(request: dict) -> str:
        value = request.get("friend_request_id")
        if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{32}", value):
            raise AccountError("好友申请编号无效")
        return value

    def _friend_add(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            username = self._username(request.get("username"))
            target = conn.execute(
                "SELECT account_id FROM accounts WHERE username=?", (username,)
            ).fetchone()
            if target is None:
                raise AccountError("未找到该账号")
            target_id = target["account_id"]
            if target_id == account_id:
                raise AccountError("不能添加自己为好友")
            low, high = sorted((account_id, target_id))
            if conn.execute(
                    "SELECT 1 FROM friendships WHERE account_low=? AND account_high=?",
                    (low, high)).fetchone():
                raise AccountError("对方已是好友")
            target_privacy = conn.execute(
                "SELECT allow_friend_requests FROM account_privacy WHERE account_id=?",
                (target_id,),
            ).fetchone()
            if target_privacy is not None and not target_privacy["allow_friend_requests"]:
                raise AccountError("对方当前不接收好友申请")
            if conn.execute(
                    "SELECT 1 FROM friend_requests WHERE sender_id=? AND recipient_id=? "
                    "AND state='pending'", (account_id, target_id)).fetchone():
                raise AccountError("好友申请已发送")
            if conn.execute(
                    "SELECT 1 FROM friend_requests WHERE sender_id=? AND recipient_id=? "
                    "AND state='pending'", (target_id, account_id)).fetchone():
                raise AccountError("对方已向你发送好友申请，请先处理")
            now = self._now()
            friend_request_id = uuid.uuid4().hex
            conn.execute(
                "INSERT INTO friend_requests(request_id,sender_id,recipient_id,state,created_at,updated_at) "
                "VALUES(?,?,?,'pending',?,?)",
                (friend_request_id, account_id, target_id, now, now),
            )
            self._bump_social_revisions(conn, account_id, target_id)
            return {"friend_request_id": friend_request_id}

        return self._run_account_mutation(account_id, "account_friend_add", request, work, token=token)

    def _friend_accept(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            friend_request_id = self._social_request_id(request)
            pending = conn.execute(
                "SELECT sender_id,recipient_id,state FROM friend_requests WHERE request_id=?",
                (friend_request_id,),
            ).fetchone()
            if pending is None or pending["recipient_id"] != account_id:
                raise AccountError("好友申请不存在或无权处理")
            if pending["state"] != "pending":
                raise AccountError("好友申请已处理")
            low, high = sorted((account_id, pending["sender_id"]))
            conn.execute(
                "INSERT INTO friendships(account_low,account_high,created_at) VALUES(?,?,?)",
                (low, high, self._now()),
            )
            conn.execute(
                "UPDATE friend_requests SET state='accepted',updated_at=? "
                "WHERE request_id=? AND recipient_id=? AND state='pending'",
                (self._now(), friend_request_id, account_id),
            )
            self._bump_social_revisions(conn, account_id, pending["sender_id"])
            return {}

        return self._run_account_mutation(account_id, "account_friend_accept", request, work, token=token)

    def _friend_reject(self, account_id: str, request: dict, token: str | None = None) -> dict:
        return self._friend_request_finish(account_id, request, "reject", token)

    def _friend_cancel(self, account_id: str, request: dict, token: str | None = None) -> dict:
        return self._friend_request_finish(account_id, request, "cancel", token)

    def _friend_request_finish(self, account_id: str, request: dict, action: str,
                               token: str | None) -> dict:
        operation = "account_friend_reject" if action == "reject" else "account_friend_cancel"

        def work(conn: sqlite3.Connection):
            friend_request_id = self._social_request_id(request)
            row = conn.execute(
                "SELECT sender_id,recipient_id,state FROM friend_requests WHERE request_id=?",
                (friend_request_id,),
            ).fetchone()
            expected_owner = row["recipient_id"] if row and action == "reject" else (
                row["sender_id"] if row else None)
            if row is None or expected_owner != account_id:
                raise AccountError("好友申请不存在或无权处理")
            if row["state"] != "pending":
                raise AccountError("好友申请已处理")
            new_state = "rejected" if action == "reject" else "cancelled"
            conn.execute(
                "UPDATE friend_requests SET state=?,updated_at=? "
                "WHERE request_id=? AND state='pending'",
                (new_state, self._now(), friend_request_id),
            )
            other_id = row["sender_id"] if action == "reject" else row["recipient_id"]
            self._bump_social_revisions(conn, account_id, other_id)
            return {}

        return self._run_account_mutation(account_id, operation, request, work, token=token)

    def _friend_remove(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            target_id = request.get("account_id")
            if not isinstance(target_id, str) or len(target_id) != 32 or target_id == account_id:
                raise AccountError("好友账号编号无效")
            low, high = sorted((account_id, target_id))
            removed = conn.execute(
                "DELETE FROM friendships WHERE account_low=? AND account_high=?",
                (low, high),
            )
            if removed.rowcount != 1:
                raise AccountError("好友关系不存在")
            self._bump_social_revisions(conn, account_id, target_id)
            return {}

        return self._run_account_mutation(account_id, "account_friend_remove", request, work, token=token)

    def _privacy_update(self, account_id: str, request: dict,
                        token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            hide_online = request.get("hide_online")
            allow_requests = request.get("allow_friend_requests")
            if type(hide_online) is not bool or type(allow_requests) is not bool:
                raise AccountError("隐私设置格式无效")
            conn.execute(
                "INSERT INTO account_privacy(account_id,hide_online,allow_friend_requests) "
                "VALUES(?,?,?) ON CONFLICT(account_id) DO UPDATE SET "
                "hide_online=excluded.hide_online,allow_friend_requests=excluded.allow_friend_requests",
                (account_id, int(hide_online), int(allow_requests)),
            )
            self._bump_social_revisions(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_privacy_update", request, work, token=token)

    @staticmethod
    def _period_key(period: str, now: int) -> str:
        local_date = datetime.fromtimestamp(now, SHANGHAI).date()
        if period == "daily":
            return local_date.isoformat()
        monday = local_date - timedelta(days=local_date.weekday())
        return monday.isoformat()

    def _ensure_quests(self, conn: sqlite3.Connection, account_id: str, now: int) -> None:
        for period in ("daily", "weekly"):
            period_key = self._period_key(period, now)
            for slot_index, slot in enumerate(QUEST_SLOTS[period]):
                quest_id = f"{period}:{period_key}:{slot.quest_id_suffix}"
                template = slot.template
                conn.execute(
                    "INSERT OR IGNORE INTO quests(account_id,quest_id,period,period_key,slot,difficulty,quest_type,"
                    "title,description,progress,target,reward_gold,completed,rerolls) "
                    "VALUES(?,?,?,?,?,?,?,?,?,0,?,?,0,0)",
                    (account_id, quest_id, period, period_key, slot_index, slot.difficulty,
                     template.quest_type, template.title, template.description,
                     template.target, QUEST_REWARDS[(period, slot.difficulty)]),
                )

    @staticmethod
    def _bump_revision(conn: sqlite3.Connection, account_id: str, amount: int = 1) -> None:
        conn.execute("UPDATE accounts SET revision=revision+? WHERE account_id=?", (amount, account_id))

    def _bump_social_revisions(self, conn: sqlite3.Connection, account_id: str,
                               *related_account_ids: str) -> None:
        affected = {account_id, *related_account_ids}
        rows = conn.execute(
            "SELECT CASE WHEN account_low=? THEN account_high ELSE account_low END AS friend_id "
            "FROM friendships WHERE account_low=? OR account_high=?",
            (account_id, account_id, account_id),
        ).fetchall()
        affected.update(row["friend_id"] for row in rows)
        for affected_id in affected:
            self._bump_revision(conn, affected_id)

    def _validate_collection_deck(self, conn: sqlite3.Connection, account_id: str,
                                  class_id: object, deck: object, *,
                                  require_complete: bool = True) -> tuple[int, list[str]]:
        if type(class_id) is not int or class_id not in HEROES:
            raise AccountError("职业无效")
        if (not isinstance(deck, list) or len(deck) > 30
                or (require_complete and len(deck) != 30)
                or any(not isinstance(card_id, str) for card_id in deck)):
            raise AccountError("卡组必须包含30张有效卡牌" if require_complete else "卡组最多包含30张有效卡牌")
        try:
            validate_deck(class_id, deck, require_complete=require_complete)
        except (TypeError, ValueError, KeyError):
            raise AccountError("卡组不符合当前对战规则") from None
        required = Counter(deck)
        rows = conn.execute(
            "SELECT card_id,normal,golden,free FROM collections WHERE account_id=?",
            (account_id,),
        ).fetchall()
        available = {row["card_id"]: row["normal"] + row["golden"] + row["free"] for row in rows}
        if any(available.get(card_id, 0) < count for card_id, count in required.items()):
            raise AccountError("卡组包含尚未拥有的卡牌")
        return class_id, list(deck)

    @staticmethod
    def _validate_card(card_id: object, catalog: CardCatalog):
        if not isinstance(card_id, str) or not 1 <= len(card_id) <= 128:
            raise AccountError("卡牌编号无效")
        card = catalog.cards.get(card_id)
        if card is None:
            raise AccountError("该卡牌不在可收藏卡库中")
        return card

    def _change_inventory(self, conn: sqlite3.Connection, account_id: str, card_id: str,
                          *, normal: int = 0, golden: int = 0, free: int = 0) -> None:
        row = conn.execute(
            "SELECT normal,golden,free FROM collections WHERE account_id=? AND card_id=?",
            (account_id, card_id),
        ).fetchone()
        before = (row["normal"], row["golden"], row["free"]) if row else (0, 0, 0)
        after = (before[0] + normal, before[1] + golden, before[2] + free)
        if min(after) < 0:
            raise AccountError("收藏数量不足")
        if any(value > 2_147_483_647 for value in after):
            raise AccountError("收藏数量超过上限")
        if not any(after):
            conn.execute("DELETE FROM collections WHERE account_id=? AND card_id=?", (account_id, card_id))
        else:
            conn.execute(
                "INSERT INTO collections(account_id,card_id,normal,golden,free) VALUES(?,?,?,?,?) "
                "ON CONFLICT(account_id,card_id) DO UPDATE SET normal=excluded.normal,"
                "golden=excluded.golden,free=excluded.free",
                (account_id, card_id, *after),
            )

    def _save_deck(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            class_id, deck = self._validate_collection_deck(
                conn, account_id, request.get("class_id"), request.get("cards"),
                require_complete=False)
            name = request.get("name")
            if (not isinstance(name, str) or not 1 <= len(name.strip()) <= 32
                    or any(not ch.isprintable() for ch in name)):
                raise AccountError("卡组名称需为1到32个可见字符")
            name = name.strip()
            deck_id = request.get("deck_id", "")
            if not isinstance(deck_id, str) or len(deck_id) > 64:
                raise AccountError("卡组编号无效")
            if deck_id:
                row = conn.execute(
                    "SELECT revision,format FROM decks WHERE deck_id=? AND account_id=?",
                    (deck_id, account_id),
                ).fetchone()
                if row is None:
                    raise AccountError("卡组不存在")
                deck_format = request.get("format", row["format"])
                if not isinstance(deck_format, str) or deck_format not in FORMATS:
                    raise AccountError("卡组格式无效")
                revision = row["revision"] + 1
                conn.execute(
                    "UPDATE decks SET name=?,class_id=?,cards_json=?,format=?,revision=?,updated_at=? "
                    "WHERE deck_id=? AND account_id=?",
                    (name, class_id, json.dumps(deck, ensure_ascii=False), deck_format, revision,
                     self._now(), deck_id, account_id),
                )
            else:
                deck_format = request.get("format", "wild")
                if not isinstance(deck_format, str) or deck_format not in FORMATS:
                    raise AccountError("卡组格式无效")
                count = conn.execute("SELECT count(*) FROM decks WHERE account_id=?", (account_id,)).fetchone()[0]
                if count >= MAX_DECKS:
                    raise AccountError("卡组数量已达上限")
                deck_id = uuid.uuid4().hex
                now = self._now()
                conn.execute(
                    "INSERT INTO decks(deck_id,account_id,name,class_id,cards_json,format,revision,created_at,updated_at) "
                    "VALUES(?,?,?,?,?,?,1,?,?)",
                    (deck_id, account_id, name, class_id,
                     json.dumps(deck, ensure_ascii=False), deck_format, now, now),
                )
            self._bump_revision(conn, account_id)
            return {"deck_id": deck_id}

        return self._run_account_mutation(account_id, "account_deck_save", request, work, token=token)

    def _delete_deck(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            deck_id = request.get("deck_id")
            if not isinstance(deck_id, str) or not 1 <= len(deck_id) <= 64:
                raise AccountError("卡组编号无效")
            cursor = conn.execute("DELETE FROM decks WHERE deck_id=? AND account_id=?", (deck_id, account_id))
            if cursor.rowcount != 1:
                raise AccountError("卡组不存在")
            self._bump_revision(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_deck_delete", request, work, token=token)

    def _craft(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            card = self._validate_card(request.get("card_id"), self.catalog)
            if card.rarity == 2 or card.set_id == 1637:
                raise AccountError("免费卡和核心系列卡牌不能制作")
            golden = request.get("golden", False)
            if type(golden) is not bool:
                raise AccountError("金色选项无效")
            count = self._count(request.get("count"))
            costs = GOLDEN_CRAFT_COST if golden else NORMAL_CRAFT_COST
            cost = costs.get(card.rarity)
            if cost is None:
                raise AccountError("该卡牌不能制作")
            account = conn.execute("SELECT dust FROM accounts WHERE account_id=?", (account_id,)).fetchone()
            total = cost * count
            if account["dust"] < total:
                raise AccountError("奥术之尘不足")
            conn.execute("UPDATE accounts SET dust=dust-? WHERE account_id=?", (total, account_id))
            self._change_inventory(conn, account_id, card.card_id,
                                   golden=count if golden else 0,
                                   normal=0 if golden else count)
            self._bump_revision(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_craft", request, work, token=token)

    def _disenchant(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            card = self._validate_card(request.get("card_id"), self.catalog)
            golden = request.get("golden", False)
            if type(golden) is not bool:
                raise AccountError("金色选项无效")
            count = self._count(request.get("count"))
            if card.rarity == 2 or card.set_id == 1637:
                raise AccountError("免费卡和核心系列卡牌不能分解")
            values = GOLDEN_DUST_VALUE if golden else NORMAL_DUST_VALUE
            dust = values.get(card.rarity)
            if dust is None:
                raise AccountError("该卡牌不能分解")
            row = conn.execute(
                "SELECT normal,golden FROM collections WHERE account_id=? AND card_id=?",
                (account_id, card.card_id),
            ).fetchone()
            owned = row["golden" if golden else "normal"] if row else 0
            if owned < count:
                raise AccountError("可分解数量不足")
            conn.execute("UPDATE accounts SET dust=dust+? WHERE account_id=?", (dust * count, account_id))
            self._change_inventory(conn, account_id, card.card_id,
                                   golden=-count if golden else 0,
                                   normal=0 if golden else -count)
            self._bump_revision(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_disenchant", request, work, token=token)

    def _shop_buy(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            set_id = request.get("set_id")
            if type(set_id) is not int or set_id not in self.catalog.pack_sets:
                raise AccountError("扩展包编号无效")
            count = self._count(request.get("count"), maximum=1000)
            cost = PACK_PRICE * count
            account = conn.execute("SELECT gold FROM accounts WHERE account_id=?", (account_id,)).fetchone()
            if account["gold"] < cost:
                raise AccountError("金币不足")
            conn.execute("UPDATE accounts SET gold=gold-? WHERE account_id=?", (cost, account_id))
            conn.execute(
                "INSERT INTO packs(account_id,set_id,count) VALUES(?,?,?) "
                "ON CONFLICT(account_id,set_id) DO UPDATE SET count=count+excluded.count",
                (account_id, set_id, count),
            )
            self._bump_revision(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_shop_buy", request, work, token=token)

    def _weighted_rarity(self, rarities: tuple[int, ...]) -> int:
        values = [(rarity, self.pack_weights.get(rarity, 0)) for rarity in rarities]
        values = [(rarity, weight) for rarity, weight in values if weight > 0]
        if not values:
            raise AccountError("该扩展包暂无可抽取的卡牌")
        total = sum(weight for _, weight in values)
        pick = self.rng.random() * total
        for rarity, weight in values:
            pick -= weight
            if pick < 0:
                return rarity
        return values[-1][0]

    def _open_pack(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            set_id = request.get("set_id")
            if type(set_id) is not int or set_id not in self.catalog.pack_sets:
                raise AccountError("扩展包编号无效")
            pack = conn.execute(
                "SELECT count FROM packs WHERE account_id=? AND set_id=?",
                (account_id, set_id),
            ).fetchone()
            if pack is None or pack["count"] < 1:
                raise AccountError("该扩展包数量不足")
            cards = self.catalog.pack_by_set.get(set_id, ())
            owned_rows = conn.execute(
                "SELECT card_id,normal,golden,free FROM collections WHERE account_id=?",
                (account_id,),
            ).fetchall()
            owned = {row["card_id"] for row in owned_rows
                     if row["normal"] or row["golden"] or row["free"]}
            owned_counts = {row["card_id"]: (row["normal"], row["golden"], row["free"])
                            for row in owned_rows}
            drawn = set()
            opened = []
            for slot in range(PACK_SIZE):
                forced_rare = slot == 0
                available_rarities = (3, 4, 5) if forced_rare else (1, 3, 4, 5)
                rarity = self._weighted_rarity(available_rarities)
                candidates = [card for card in cards if card.rarity == rarity]
                if not candidates:
                    candidates = [card for card in cards if card.rarity in available_rarities]
                if not candidates:
                    raise AccountError("该扩展包暂无可抽取的卡牌")
                if self.duplicate_protection:
                    fresh = [card for card in candidates if card.card_id not in owned and card.card_id not in drawn]
                    if fresh:
                        candidates = fresh
                    else:
                        fresh_any = [card for card in cards
                                     if card.rarity in available_rarities
                                     and card.card_id not in owned and card.card_id not in drawn]
                        if fresh_any:
                            candidates = fresh_any
                card = self.rng.choice(candidates)
                is_golden = self.rng.random() < self.golden_rate
                counts = owned_counts.get(card.card_id, (0, 0, 0))
                is_new = not any(counts)
                opened.append({"card_id": card.card_id, "golden": is_golden, "new_card": is_new})
                self._change_inventory(conn, account_id, card.card_id,
                                       golden=1 if is_golden else 0,
                                       normal=0 if is_golden else 1)
                owned.add(card.card_id)
                old = owned_counts.get(card.card_id, (0, 0, 0))
                owned_counts[card.card_id] = (
                    old[0] + (0 if is_golden else 1),
                    old[1] + (1 if is_golden else 0), old[2])
                drawn.add(card.card_id)
            conn.execute(
                "UPDATE packs SET count=count-1 WHERE account_id=? AND set_id=?",
                (account_id, set_id),
            )
            self._bump_revision(conn, account_id)
            return {"opened_cards": opened, "open_id": uuid.uuid4().hex}

        return self._run_account_mutation(account_id, "account_pack_open", request, work, token=token)

    def _reroll_quest(self, account_id: str, request: dict, token: str | None = None) -> dict:
        def work(conn: sqlite3.Connection):
            quest_id = request.get("quest_id")
            if not isinstance(quest_id, str) or not 1 <= len(quest_id) <= 80:
                raise AccountError("任务编号无效")
            row = conn.execute(
                "SELECT period,period_key,difficulty,quest_type,progress,completed,rerolls "
                "FROM quests WHERE account_id=? AND quest_id=?",
                (account_id, quest_id),
            ).fetchone()
            if row is None:
                raise AccountError("任务不存在")
            if row["period_key"] != self._period_key(row["period"], self._now()):
                raise AccountError("任务已刷新")
            if row["completed"] or row["progress"] or row["rerolls"]:
                raise AccountError("该任务当前不能重置")
            options = QUESTS[(row["period"], row["difficulty"])]
            replacement = next((item for item in options if item.quest_type != row["quest_type"]), None)
            if replacement is None:
                raise AccountError("当前任务没有可替换目标")
            updated = conn.execute(
                "UPDATE quests SET quest_type=?,title=?,description=?,target=?,rerolls=rerolls+1 "
                "WHERE account_id=? AND quest_id=? AND period_key=? "
                "AND completed=0 AND progress=0 AND rerolls=0",
                (replacement.quest_type, replacement.title, replacement.description,
                 replacement.target, account_id, quest_id, row["period_key"]),
            )
            if updated.rowcount != 1:
                raise AccountError("任务状态已变化")
            self._bump_revision(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_quest_reroll", request, work, token=token)

    def _logout(self, account_id: str, request: dict, token: str) -> dict:
        def work(conn: sqlite3.Connection):
            cursor = conn.execute(
                "UPDATE sessions SET revoked_at=? WHERE account_id=? AND token_hash=? "
                "AND revoked_at IS NULL AND expires_at>?",
                (self._now(), account_id, self._token_hash(token), self._now()),
            )
            if cursor.rowcount != 1:
                raise AccountError("登录状态已失效，请重新登录")
            self._bump_revision(conn, account_id)
            return {}

        return self._run_account_mutation(account_id, "account_logout", request, work, token=token)

    def process(self, peer_account_id: str | None, peer_token: str | None,
                request: dict) -> tuple[dict, str | None, str | None]:
        """Handle one account request and return response plus updated peer identity."""
        if not isinstance(request, dict):
            raise AccountError("请求格式无效")
        operation = request.get("type")
        if not isinstance(operation, str) or not operation.startswith("account_"):
            raise AccountError("账号请求类型无效")
        request_id = self._safe_request_id(request)
        if operation == "account_register":
            response, account_id, token = self._register(request)
            return response, account_id if response.get("ok") else peer_account_id, token if response.get("ok") else peer_token
        if operation == "account_login":
            response, account_id, token = self._login(request)
            return response, account_id if response.get("ok") else peer_account_id, token if response.get("ok") else peer_token
        if operation == "account_resume":
            token = request.get("token")
            with self.store.transaction() as conn:
                account_id = self._active_account_in(conn, None, token)
                response = self._result(operation, request_id, ok=True,
                                        snapshot=self._snapshot(conn, account_id), token=token)
            return response, account_id, token

        if operation == "account_state":
            with self.store.transaction() as conn:
                account_id = self._active_account_in(conn, peer_account_id, peer_token)
                response = self._result(operation, request_id, ok=True,
                                        snapshot=self._snapshot(conn, account_id))
            return response, account_id, peer_token
        if operation in ("account_deck_encode", "account_deck_decode"):
            with self.store.transaction() as conn:
                account_id = self._active_account_in(conn, peer_account_id, peer_token)
                try:
                    if operation == "account_deck_encode":
                        extra = {"deck_code": encode_deck(request.get("class_id"),
                                 request.get("cards"), request.get("format"))}
                    else:
                        extra = decode_deck(request.get("deck_code"))
                    response = self._result(operation, request_id, ok=True, **extra)
                except ValueError as exc:
                    response = self._result(operation, request_id, ok=False, error=str(exc))
            return response, account_id, peer_token
        if operation == "account_logout":
            if not peer_account_id or not isinstance(peer_token, str):
                raise AccountError("登录状态已失效，请重新登录")
            response = self._logout(peer_account_id, request, peer_token)
            return response, peer_account_id, peer_token
        handlers = {
            "account_deck_save": self._save_deck,
            "account_deck_delete": self._delete_deck,
            "account_craft": self._craft,
            "account_disenchant": self._disenchant,
            "account_shop_buy": self._shop_buy,
            "account_pack_open": self._open_pack,
            "account_quest_reroll": self._reroll_quest,
            "account_friend_add": self._friend_add,
            "account_friend_accept": self._friend_accept,
            "account_friend_reject": self._friend_reject,
            "account_friend_cancel": self._friend_cancel,
            "account_friend_remove": self._friend_remove,
            "account_privacy_update": self._privacy_update,
        }
        handler = handlers.get(operation)
        if handler is None:
            raise AccountError("不支持该账号操作")
        if not peer_account_id or not isinstance(peer_token, str):
            raise AccountError("请先登录")
        account_id = peer_account_id
        response = handler(account_id, request, token=peer_token)
        return response, account_id, peer_token

    def load_match_deck(self, account_id: str | None, token: str | None,
                        deck_id: object) -> dict:
        if not account_id:
            raise AccountError("请先登录")
        if not isinstance(deck_id, str) or not 1 <= len(deck_id) <= 64:
            raise AccountError("卡组编号无效")
        with self.store.transaction() as conn:
            account_id = self._active_account_in(conn, account_id, token)
            row = conn.execute(
                "SELECT name,class_id,cards_json,format FROM decks WHERE deck_id=? AND account_id=?",
                (deck_id, account_id),
            ).fetchone()
            if row is None:
                raise AccountError("卡组不存在")
            if row["format"] == "standard":
                raise AccountError("标准模式卡池尚未配置，暂不能开始对战")
            cards = json.loads(row["cards_json"])
            class_id, cards = self._validate_collection_deck(
                conn, account_id, row["class_id"], cards)
            account = conn.execute(
                "SELECT display_name FROM accounts WHERE account_id=?", (account_id,)
            ).fetchone()
        return {"account_id": account_id, "name": account["display_name"],
                "deck_name": row["name"], "class_id": class_id, "deck": cards,
                "format": row["format"]}

    def record_accepted_match(self, match_id: str, version: int, actor_seat: int,
                              events: list[dict], account_ids: list[str | None],
                              results: list[str] | None = None) -> None:
        """Apply progress only from a Match state that has accepted a new action."""
        if (not isinstance(match_id, str) or type(version) is not int or version < 1
                or type(actor_seat) is not int or actor_seat not in (0, 1)
                or len(account_ids) != 2):
            raise ValueError("invalid accepted match identity")
        now = self._now()
        work_events = []
        for index, event in enumerate(events):
            if not isinstance(event, dict):
                continue
            kind = {"CardPlay": "card_play", "Attack": "attack"}.get(event.get("type"))
            if kind is None:
                continue
            owner = event.get("owner")
            if type(owner) is not int or owner not in (0, 1):
                owner = actor_seat
            account_id = account_ids[owner]
            if account_id:
                work_events.append((account_id, f"{match_id}:v{version}:event{index}", kind))
        if results is not None:
            for seat, result in enumerate(results):
                account_id = account_ids[seat]
                if account_id:
                    work_events.append((account_id, f"{match_id}:result:seat{seat}", "match_complete"))
                    if result == "WON":
                        work_events.append((account_id, f"{match_id}:result:win{seat}", "match_win"))
        if not work_events:
            return
        changed_accounts: set[str] = set()
        with self.store.transaction() as conn:
            for account_id, event_id, event_type in work_events:
                inserted = conn.execute(
                    "INSERT OR IGNORE INTO quest_events(account_id,event_id,created_at) VALUES(?,?,?)",
                    (account_id, event_id, now),
                ).rowcount
                if not inserted:
                    continue
                self._ensure_quests(conn, account_id, now)
                quest_rows = conn.execute(
                    "SELECT quest_id,progress,target,reward_gold FROM quests WHERE account_id=? "
                    "AND quest_type=? AND completed=0 AND ((period='daily' AND period_key=?) "
                    "OR (period='weekly' AND period_key=?))",
                    (account_id, event_type, self._period_key("daily", now),
                     self._period_key("weekly", now)),
                ).fetchall()
                for quest in quest_rows:
                    progress = min(quest["target"], quest["progress"] + 1)
                    completed = int(progress >= quest["target"])
                    updated = conn.execute(
                        "UPDATE quests SET progress=?,completed=? "
                        "WHERE account_id=? AND quest_id=? AND completed=0",
                        (progress, completed, account_id, quest["quest_id"]),
                    )
                    if updated.rowcount != 1:
                        continue
                    if completed:
                        conn.execute("UPDATE accounts SET gold=gold+? WHERE account_id=?",
                                     (quest["reward_gold"], account_id))
                    changed_accounts.add(account_id)
            for account_id in changed_accounts:
                self._bump_revision(conn, account_id)
