from types import SimpleNamespace

import pytest
from fireplace import cards
from hearthstone.enums import GameTag

from game import database
from game.accounts import AccountError, AccountService


@pytest.fixture
def account(tmp_path):
    path = tmp_path / "account.sqlite"
    service = AccountService(path, password_iterations=10000)
    registered, account_id, token = service.process(None, None, {
        "type": "account_register", "request_id": "register", "username": "partialtest",
        "password": "correct-horse-battery", "display_name": "Tester"})
    assert registered["ok"]
    return path, service, account_id, token, registered["snapshot"]


def request(service, account_id, token, request_id, **fields):
    return service.process(account_id, token, {
        "type": "account_deck_save", "request_id": request_id, **fields})[0]


def test_empty_and_partial_decks_survive_restart_and_cannot_join(account):
    path, service, account_id, token, snapshot = account
    source = next(deck for deck in snapshot["decks"] if deck["class_id"] == 4)
    saved = request(service, account_id, token, "empty", deck_id="", name="Empty",
                    class_id=4, cards=[])
    assert saved["ok"]
    empty_id = saved["deck_id"]
    partial = source["cards"][:3]
    saved = request(service, account_id, token, "partial", deck_id="", name="Partial",
                    class_id=4, cards=partial, format="wild")
    assert saved["ok"]
    partial_id = saved["deck_id"]
    service = AccountService(path, password_iterations=10000)
    state = service.process(account_id, token, {"type": "account_state", "request_id": "state"})[0]
    decks = {deck["deck_id"]: deck for deck in state["snapshot"]["decks"]}
    assert decks[empty_id]["cards"] == []
    assert decks[partial_id]["cards"] == partial
    for deck_id in (empty_id, partial_id):
        with pytest.raises(AccountError, match="30"):
            service.load_match_deck(account_id, token, deck_id)
    assert service.load_match_deck(account_id, token, source["deck_id"])["deck"] == source["cards"]
    updated = request(service, account_id, token, "update", deck_id=partial_id, name="Complete",
                      class_id=4, cards=source["cards"])
    assert updated["ok"]
    assert service.load_match_deck(account_id, token, partial_id)["deck"] == source["cards"]


def test_partial_save_rejects_invalid_cards_class_copy_limits_and_ownership(account):
    _, service, account_id, token, snapshot = account
    source = next(deck for deck in snapshot["decks"] if deck["class_id"] == 4)
    owned = {row["card_id"] for row in snapshot["cards"]}
    eligible = [cards.db[card_id] for card_id in database.scope()]
    wrong_class = next(card.id for card in eligible if int(card.card_class) not in (4, 12))
    legendary = next(card.id for card in eligible if int(card.card_class) in (4, 12)
                     and int(card.rarity) == 5)
    unowned = next(card.id for card in eligible if int(card.card_class) in (4, 12)
                   and card.id not in owned and card.id != legendary)
    with service.store.transaction() as conn:
        for card_id, copies in ((legendary, 2), (source["cards"][0], 3)):
            conn.execute("INSERT INTO collections(account_id,card_id,normal,golden,free) VALUES(?,?,?,0,0) "
                         "ON CONFLICT(account_id,card_id) DO UPDATE SET normal=excluded.normal, golden=0, free=0",
                         (account_id, card_id, copies))
    examples = [
        ["NOT_A_CARD"], [wrong_class], [source["cards"][0]] * 3,
        [legendary] * 2, [unowned], [source["cards"][0]] * 31, [None],
    ]
    for index, deck in enumerate(examples):
        response = request(service, account_id, token, f"bad-{index}", deck_id="", name="Bad",
                           class_id=4, cards=deck)
        assert not response["ok"], deck
    ownership = request(service, account_id, token, "unowned-detail", deck_id="", name="Bad",
                        class_id=4, cards=[unowned])
    assert "尚未拥有" in ownership["error"]
    invalid_class = request(service, account_id, token, "bad-class", deck_id="", name="Bad",
                            class_id=999, cards=[])
    assert not invalid_class["ok"]


def test_partial_death_knight_runes_still_checked(monkeypatch):
    fake_cards = {
        "blood": SimpleNamespace(card_class=1, rarity=1, tags={GameTag.COST_BLOOD: 3}),
        "frost": SimpleNamespace(card_class=1, rarity=1, tags={GameTag.COST_FROST: 1}),
    }
    monkeypatch.setattr(database, "cards", SimpleNamespace(db=fake_cards))
    monkeypatch.setattr(database, "scope", lambda: list(fake_cards))
    database.validate_deck(1, ["blood"], require_complete=False)
    with pytest.raises(ValueError, match="runes"):
        database.validate_deck(1, ["blood", "frost"], require_complete=False)
    with pytest.raises(ValueError, match="exactly 30"):
        database.validate_deck(1, ["blood"])
