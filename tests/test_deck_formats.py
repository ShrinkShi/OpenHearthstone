import sqlite3
from collections import Counter

import pytest
from hearthstone.deckstrings import Deck
from hearthstone.enums import FormatType

from game.accounts import AccountError, AccountService
from game.accounts.deck_code import decode_deck, encode_deck
from game.accounts.persistence import SCHEMA, SqliteStore
from game.database import preset


def test_old_deck_schema_migrates_without_changing_existing_values(tmp_path):
    path = tmp_path / "old.sqlite"
    conn = sqlite3.connect(path)
    conn.executescript(SCHEMA.replace(
        "    format TEXT NOT NULL DEFAULT 'wild' CHECK (format IN ('wild', 'standard')),\n", ""))
    conn.execute("INSERT INTO accounts(account_id,username,display_name,password_salt,password_hash,"
                 "gold,dust,revision,created_at) VALUES('a','user','name',x'01',x'02',0,0,1,1)")
    conn.execute("INSERT INTO decks VALUES('d','a','旧卡组',4,'[\"CS2_029\"]',7,10,20)")
    conn.commit()
    conn.close()
    SqliteStore(path)
    SqliteStore(path)
    conn = sqlite3.connect(path)
    row = conn.execute("SELECT deck_id,name,class_id,cards_json,revision,created_at,updated_at,format FROM decks").fetchone()
    conn.close()
    assert row == ('d', '旧卡组', 4, '["CS2_029"]', 7, 10, 20, 'wild')


@pytest.mark.parametrize("deck_format,wire_format", [("wild", FormatType.FT_WILD),
                                                      ("standard", FormatType.FT_STANDARD)])
def test_real_deckstring_roundtrip(deck_format, wire_format):
    card_ids = preset(4)
    encoded = encode_deck(4, card_ids, deck_format)
    wire = Deck.from_deckstring(encoded)
    assert wire.format == wire_format
    decoded = decode_deck(encoded)
    assert decoded["class_id"] == 4
    assert decoded["format"] == deck_format
    assert Counter(decoded["cards"]) == Counter(card_ids)


def test_malformed_unknown_and_sideboard_are_rejected():
    valid = Deck.from_deckstring(encode_deck(4, preset(4), "wild"))
    for value in (None, "bad!", "A" * 2049, "AA=="):
        with pytest.raises(ValueError):
            decode_deck(value)
    valid.cards = [(987654321, 1)]
    with pytest.raises(ValueError, match="未知"):
        decode_deck(valid.as_deckstring)
    valid.cards = [(1, 1)]
    valid.sideboards = [(1, 1, 1)]
    with pytest.raises(ValueError, match="备牌"):
        decode_deck(valid.as_deckstring)


def test_account_formats_and_read_only_code_api(tmp_path):
    path = tmp_path / "accounts.sqlite"
    service = AccountService(path, password_iterations=10000)
    registered, account_id, token = service.process(None, None, {
        "type": "account_register", "request_id": "r", "username": "deckuser",
        "password": "correct-horse-battery", "display_name": "Tester"})
    source = next(deck for deck in registered["snapshot"]["decks"] if deck["class_id"] == 4)
    assert source["format"] == "wild"

    def request(kind, request_id, **fields):
        return service.process(account_id, token, {"type": kind, "request_id": request_id, **fields})[0]

    saved = request("account_deck_save", "save", deck_id="", name="Standard",
                    class_id=source["class_id"], cards=source["cards"], format="standard")
    assert saved["ok"]
    deck_id = saved["deck_id"]
    with pytest.raises(AccountError, match="标准"):
        service.load_match_deck(account_id, token, deck_id)
    renamed = request("account_deck_save", "rename", deck_id=deck_id, name="Renamed",
                      class_id=source["class_id"], cards=source["cards"])
    assert next(row for row in renamed["snapshot"]["decks"] if row["deck_id"] == deck_id)["format"] == "standard"
    revision = renamed["snapshot"]["revision"]
    encoded = request("account_deck_encode", "encode", class_id=source["class_id"],
                      cards=source["cards"], format="standard")
    assert encoded["ok"]
    decoded = request("account_deck_decode", "decode", deck_code=encoded["deck_code"])
    assert decoded["ok"] and decoded["format"] == "standard"
    assert Counter(decoded["cards"]) == Counter(source["cards"])
    state = request("account_state", "state")
    assert state["snapshot"]["revision"] == revision
    assert request("account_deck_delete", "delete", deck_id=deck_id)["ok"]
    service = AccountService(path, password_iterations=10000)
    state = request("account_state", "after-restart")
    assert all(row["deck_id"] != deck_id for row in state["snapshot"]["decks"])
