from tools import launch_duel


OLD_ACCOUNT_HELLO = {
    "type": "hello", "protocol": 1,
    "capabilities": ["hero_emotes_v1", "accounts_v1"],
}
FOUR_SLOT_ACCOUNT_HELLO = {
    "type": "hello", "protocol": 1,
    "capabilities": [
        "hero_emotes_v1", "accounts_v1", "account_quests_four_slots_v1",
    ],
}
SOCIAL_ACCOUNT_HELLO = {
    "type": "hello", "protocol": 1,
    "capabilities": [
        "hero_emotes_v1", "accounts_v1", "account_quests_four_slots_v1",
        "account_social_v1",
    ],
}
GUEST_HELLO = {
    "type": "hello", "protocol": 1,
    "capabilities": ["hero_emotes_v1"],
}
DECK_ACCOUNT_HELLO = {
    "type": "hello", "protocol": 1,
    "capabilities": SOCIAL_ACCOUNT_HELLO["capabilities"] + [
        "account_deck_codes_v1", "account_deck_format_metadata_v1",
    ],
}


def test_old_account_server_is_skipped_for_next_available_port(monkeypatch):
    responses = {37240: OLD_ACCOUNT_HELLO, 37241: None}
    monkeypatch.setattr(launch_duel, "server_info", responses.get)

    assert launch_duel.choose_account_server(37240) == (37241, True)


def test_four_slot_server_without_social_is_skipped(monkeypatch):
    responses = {37240: FOUR_SLOT_ACCOUNT_HELLO, 37241: None}
    monkeypatch.setattr(launch_duel, "server_info", responses.get)

    assert launch_duel.choose_account_server(37240) == (37241, True)


def test_older_social_server_is_skipped(monkeypatch):
    responses = {37240: SOCIAL_ACCOUNT_HELLO, 37241: None}
    monkeypatch.setattr(launch_duel, "server_info", responses.get)

    assert launch_duel.choose_account_server(37240) == (37241, True)


def test_deck_capable_server_is_reused(monkeypatch):
    monkeypatch.setattr(launch_duel, "server_info", lambda port: DECK_ACCOUNT_HELLO)

    assert launch_duel.choose_account_server(37240) == (37240, False)


def test_empty_port_starts_account_server(monkeypatch):
    monkeypatch.setattr(launch_duel, "server_info", lambda port: None)

    assert launch_duel.choose_account_server(37240) == (37240, True)


def test_startup_readiness_requires_deck_capabilities_but_preserves_guest_mode():
    assert not launch_duel.server_matches_mode(OLD_ACCOUNT_HELLO, autoplay=False)
    assert not launch_duel.server_matches_mode(FOUR_SLOT_ACCOUNT_HELLO, autoplay=False)
    assert not launch_duel.server_matches_mode(SOCIAL_ACCOUNT_HELLO, autoplay=False)
    assert launch_duel.server_matches_mode(DECK_ACCOUNT_HELLO, autoplay=False)
    assert launch_duel.server_matches_mode(GUEST_HELLO, autoplay=True)
    assert not launch_duel.server_matches_mode(SOCIAL_ACCOUNT_HELLO, autoplay=True)
