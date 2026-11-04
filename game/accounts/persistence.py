"""SQLite schema and transaction handling for account state."""
from __future__ import annotations

import sqlite3
from contextlib import contextmanager
from pathlib import Path


SCHEMA = """
CREATE TABLE IF NOT EXISTS accounts (
    account_id TEXT PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    password_salt BLOB NOT NULL,
    password_hash BLOB NOT NULL,
    gold INTEGER NOT NULL CHECK (gold >= 0),
    dust INTEGER NOT NULL CHECK (dust >= 0),
    revision INTEGER NOT NULL CHECK (revision >= 0),
    created_at INTEGER NOT NULL,
    last_seen_at INTEGER
);
CREATE TABLE IF NOT EXISTS sessions (
    session_id TEXT PRIMARY KEY,
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    token_hash BLOB NOT NULL UNIQUE,
    created_at INTEGER NOT NULL,
    expires_at INTEGER NOT NULL,
    revoked_at INTEGER
);
CREATE INDEX IF NOT EXISTS sessions_account_id ON sessions(account_id);
CREATE TABLE IF NOT EXISTS collections (
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    card_id TEXT NOT NULL,
    normal INTEGER NOT NULL DEFAULT 0 CHECK (normal >= 0),
    golden INTEGER NOT NULL DEFAULT 0 CHECK (golden >= 0),
    free INTEGER NOT NULL DEFAULT 0 CHECK (free >= 0),
    PRIMARY KEY (account_id, card_id)
);
CREATE TABLE IF NOT EXISTS decks (
    deck_id TEXT PRIMARY KEY,
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    class_id INTEGER NOT NULL,
    cards_json TEXT NOT NULL,
    format TEXT NOT NULL DEFAULT 'wild' CHECK (format IN ('wild', 'standard')),
    revision INTEGER NOT NULL CHECK (revision >= 1),
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS decks_account_id ON decks(account_id);
CREATE TABLE IF NOT EXISTS packs (
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    set_id INTEGER NOT NULL,
    count INTEGER NOT NULL CHECK (count >= 0),
    PRIMARY KEY (account_id, set_id)
);
CREATE TABLE IF NOT EXISTS quests (
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    quest_id TEXT NOT NULL,
    period TEXT NOT NULL CHECK (period IN ('daily', 'weekly')),
    period_key TEXT NOT NULL,
    slot INTEGER NOT NULL DEFAULT 0 CHECK (slot BETWEEN 0 AND 3),
    difficulty TEXT NOT NULL,
    quest_type TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    progress INTEGER NOT NULL DEFAULT 0 CHECK (progress >= 0),
    target INTEGER NOT NULL CHECK (target > 0),
    reward_gold INTEGER NOT NULL CHECK (reward_gold >= 0),
    completed INTEGER NOT NULL DEFAULT 0 CHECK (completed IN (0, 1)),
    rerolls INTEGER NOT NULL DEFAULT 0 CHECK (rerolls >= 0),
    PRIMARY KEY (account_id, quest_id)
);
CREATE INDEX IF NOT EXISTS quests_period ON quests(account_id, period, period_key);
CREATE TABLE IF NOT EXISTS quest_events (
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    event_id TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    PRIMARY KEY (account_id, event_id)
);
CREATE TABLE IF NOT EXISTS account_privacy (
    account_id TEXT PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
    hide_online INTEGER NOT NULL DEFAULT 0 CHECK (hide_online IN (0, 1)),
    allow_friend_requests INTEGER NOT NULL DEFAULT 1 CHECK (allow_friend_requests IN (0, 1))
);
CREATE TABLE IF NOT EXISTS friend_requests (
    request_id TEXT PRIMARY KEY,
    sender_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    recipient_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    state TEXT NOT NULL CHECK (state IN ('pending', 'accepted', 'rejected', 'cancelled')),
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    CHECK (sender_id <> recipient_id)
);
CREATE INDEX IF NOT EXISTS friend_requests_incoming
    ON friend_requests(recipient_id, state, created_at, request_id);
CREATE INDEX IF NOT EXISTS friend_requests_outgoing
    ON friend_requests(sender_id, state, created_at, request_id);
CREATE UNIQUE INDEX IF NOT EXISTS friend_requests_pending_pair
    ON friend_requests(sender_id, recipient_id) WHERE state = 'pending';
CREATE TABLE IF NOT EXISTS friendships (
    account_low TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    account_high TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    created_at INTEGER NOT NULL,
    PRIMARY KEY (account_low, account_high),
    CHECK (account_low < account_high)
);
CREATE TABLE IF NOT EXISTS request_dedup (
    account_key TEXT NOT NULL,
    operation TEXT NOT NULL,
    request_id TEXT NOT NULL,
    fingerprint TEXT NOT NULL,
    response_json TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    PRIMARY KEY (account_key, operation, request_id)
);
"""


class SqliteStore:
    def __init__(self, path: str | Path):
        self.path = str(path)
        if self.path == ":memory:":
            raise ValueError("Account storage requires a persistent SQLite file")
        Path(self.path).parent.mkdir(parents=True, exist_ok=True)
        self.initialize()

    def connect(self) -> sqlite3.Connection:
        conn = sqlite3.connect(self.path, timeout=15, isolation_level=None)
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA foreign_keys = ON")
        conn.execute("PRAGMA busy_timeout = 15000")
        return conn

    def initialize(self) -> None:
        conn = self.connect()
        try:
            conn.execute("PRAGMA journal_mode = WAL")
            conn.executescript(SCHEMA)
            self._migrate_quest_slots(conn)
            self._migrate_social(conn)
            self._migrate_deck_format(conn)
        finally:
            conn.close()

    @staticmethod
    def _migrate_deck_format(conn: sqlite3.Connection) -> None:
        conn.execute("BEGIN IMMEDIATE")
        try:
            columns = {row["name"] for row in conn.execute("PRAGMA table_info(decks)")}
            if "format" not in columns:
                conn.execute("ALTER TABLE decks ADD COLUMN format TEXT NOT NULL DEFAULT 'wild' "
                             "CHECK (format IN ('wild', 'standard'))")
            conn.commit()
        except Exception:
            conn.rollback()
            raise

    @staticmethod
    def _migrate_quest_slots(conn: sqlite3.Connection) -> None:
        """Add explicit quest slots while preserving IDs from the three-slot schema."""
        conn.execute("BEGIN IMMEDIATE")
        try:
            columns = {row["name"] for row in conn.execute("PRAGMA table_info(quests)")}
            if "slot" not in columns:
                conn.execute(
                    "ALTER TABLE quests ADD COLUMN slot INTEGER NOT NULL DEFAULT 0 "
                    "CHECK (slot BETWEEN 0 AND 3)"
                )
            conn.execute(
                "UPDATE quests SET slot=CASE "
                "WHEN quest_id=period || ':' || period_key || ':easy' THEN 0 "
                "WHEN quest_id=period || ':' || period_key || ':normal' THEN 1 "
                "WHEN quest_id=period || ':' || period_key || ':hard' THEN 2 "
                "WHEN quest_id=period || ':' || period_key || ':fourth' THEN 3 "
                "ELSE slot END "
                "WHERE quest_id IN (period || ':' || period_key || ':easy', "
                "period || ':' || period_key || ':normal', "
                "period || ':' || period_key || ':hard', "
                "period || ':' || period_key || ':fourth')"
            )
            conn.execute(
                "CREATE UNIQUE INDEX IF NOT EXISTS quests_account_period_slot "
                "ON quests(account_id,period,period_key,slot)"
            )
            conn.commit()
        except Exception:
            conn.rollback()
            raise

    @staticmethod
    def _migrate_social(conn: sqlite3.Connection) -> None:
        """Add social presence metadata without touching existing account state."""
        conn.execute("BEGIN IMMEDIATE")
        try:
            columns = {row[1] for row in conn.execute("PRAGMA table_info(accounts)")}
            if "last_seen_at" not in columns:
                conn.execute("ALTER TABLE accounts ADD COLUMN last_seen_at INTEGER")
            conn.commit()
        except Exception:
            conn.rollback()
            raise

    @contextmanager
    def transaction(self):
        conn = self.connect()
        try:
            conn.execute("BEGIN IMMEDIATE")
            yield conn
            conn.commit()
        except Exception:
            conn.rollback()
            raise
        finally:
            conn.close()
