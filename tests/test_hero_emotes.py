import asyncio
import json
from types import SimpleNamespace

import pytest

from game.database import preset
from game.hero_emotes import HeroEmoteSession
from game.match import Match
from game.server import Server


def fake_match(match_id='match-1'):
    players = [SimpleNamespace(hero=SimpleNamespace(id='HERO_08')),
               SimpleNamespace(hero=SimpleNamespace(id='HERO_01'))]
    return SimpleNamespace(id=match_id, game=SimpleNamespace(ended=False, players=players))


def test_hero_emote_session_validates_source_replay_and_rate_limit():
    match = fake_match()
    session = HeroEmoteSession(match.id, cooldown=1.0, max_requests=2)
    command = {'type': 'hero_emote', 'match_id': match.id, 'request_id': 'request-01',
               'emote_id': 'WOW', 'seat': 1, 'hero_card_id': 'HERO_01'}

    event, duplicate = session.accept(match, 0, command, now=10.0)
    assert not duplicate
    assert event['seat'] == 0 and event['hero_card_id'] == 'HERO_08'
    assert event['emote_id'] == 'WOW'

    replay, duplicate = session.accept(match, 0, command, now=10.1)
    assert duplicate and replay == event
    with pytest.raises(ValueError, match='different emote'):
        session.accept(match, 0, dict(command, emote_id='SORRY'), now=10.2)
    with pytest.raises(ValueError, match='rate limit'):
        session.accept(match, 0, dict(command, request_id='request-02'), now=10.9)

    with pytest.raises(ValueError, match='invalid hero emote'):
        session.accept(match, 0, dict(command, request_id='request-02', emote_id='CONCEDE'), now=11.0)
    with pytest.raises(ValueError, match='match mismatch'):
        session.accept(match, 0, dict(command, request_id='request-02', match_id='other'), now=11.0)
    with pytest.raises(ValueError, match='invalid match seat'):
        session.accept(match, 2, command, now=11.0)

    accepted, duplicate = session.accept(match, 0, dict(command, request_id='request-02'), now=11.0)
    assert not duplicate and accepted['request_id'] == 'request-02'
    with pytest.raises(ValueError, match='request limit'):
        session.accept(match, 0, dict(command, request_id='request-03'), now=12.0)


@pytest.mark.parametrize('damage,has_heal', [(0, False), (4, True)])
def test_priest_hero_power_target_is_authoritative_for_full_and_injured_hero(damage, has_heal):
    match = Match(['PriestA', 'PriestB'], [6, 6], [preset(6), preset(6)], seed=7)
    for player in match.game.players:
        player.choice.choose()
    priest = match.game.current_player
    seat = match.game.players.index(priest)
    priest.max_mana = 10
    priest.used_mana = 0
    priest.hero.damage = damage
    target_id = priest.hero.entity_id
    power = priest.hero.power
    move = next(m for m in match.legal_moves(seat)
                if m['kind'] == 'power' and m['entity'] == power.entity_id and m['target'] == target_id)
    command = dict(move, action_id='priest-target-0001', match_id=match.id, version=match.version)
    assert match.apply(seat, command)

    snapshot = match.snapshot(seat)
    targets = [event for event in snapshot['events'] if event['type'] == 'HeroPowerTarget']
    assert targets == [{'type': 'HeroPowerTarget', 'source': power.entity_id, 'target': target_id,
                        'owner': seat, 'card_id': 'HERO_09bp', 'event_id': 'priest-target-0001'}]
    assert any(event['type'] == 'Heal' for event in snapshot['events']) is has_heal
    assert priest.hero.health == 30 - max(damage - (2 if damage else 0), 0)

    version = match.version
    assert not match.apply(seat, command)
    assert match.version == version
    assert len([event for event in match.snapshot(seat)['events'] if event['type'] == 'HeroPowerTarget']) == 1
    stale = dict(command, action_id='priest-target-stale', version=version - 1)
    with pytest.raises(ValueError, match='stale state version'):
        match.apply(seat, stale)
    assert len([event for event in match.snapshot(seat)['events'] if event['type'] == 'HeroPowerTarget']) == 1

    expected_hash = match.snapshot(seat)['public_hash']
    match.observer.events.clear()
    assert match.snapshot(seat)['public_hash'] == expected_hash


@pytest.mark.parametrize('cause,expected', [('player', True), ('disconnect', False)])
def test_concede_speech_event_is_explicit_and_deduplicated(cause, expected):
    match = Match(['Mage', 'Warrior'], [4, 10], [preset(4), preset(10)], seed=9)
    command = {'kind': 'concede', 'action_id': 'player-concede-0001',
               'match_id': match.id, 'version': match.version}
    assert match.apply(0, command, cause=cause)
    events = [event for event in match.snapshot(0)['events'] if event['type'] == 'Concede']
    assert bool(events) is expected
    if expected:
        assert events == [{'type': 'Concede', 'source': match.game.players[0].hero.entity_id,
                           'target': match.game.players[0].hero.entity_id, 'owner': 0,
                           'card_id': 'HERO_08', 'event_id': 'player-concede-0001'}]
    version = match.version
    assert not match.apply(0, command, cause=cause)
    assert match.version == version
    assert len([event for event in match.snapshot(0)['events'] if event['type'] == 'Concede']) == int(expected)


def test_hero_emote_tcp_broadcast_deduplicates_and_does_not_change_match_state(tmp_path):
    async def run():
        server = Server(tmp_path / 'logs')
        listener = await asyncio.start_server(server.handle, '127.0.0.1', 0, limit=32768)
        peers = []

        async def recv(reader):
            return json.loads(await asyncio.wait_for(reader.readline(), 5))

        async def send(writer, value):
            writer.write((json.dumps(value) + '\n').encode())
            await writer.drain()

        try:
            port = listener.sockets[0].getsockname()[1]
            for name, class_id in [('Mage', 4), ('Warrior', 10)]:
                reader, writer = await asyncio.open_connection('127.0.0.1', port)
                peers.append((reader, writer))
                hello = await recv(reader)
                assert 'hero_emotes_v1' in hello['capabilities']
                await send(writer, {'type': 'join', 'name': name, 'class_id': class_id,
                                    'deck': preset(class_id)})
                assert (await recv(reader))['type'] == 'queued'

            states = [await recv(reader) for reader, _ in peers]
            match_id = states[0]['match_id']
            assert states[0]['public_hash'] == states[1]['public_hash']
            match = server.rooms[match_id][0].match
            version = match.version
            before = match.snapshot(0)
            command = {'type': 'hero_emote', 'match_id': match_id, 'request_id': 'network-0001',
                       'emote_id': 'THANKS', 'seat': 1, 'hero_card_id': 'HERO_01', 'version': -1}
            await send(peers[0][1], command)
            ack, local_event = await recv(peers[0][0]), await recv(peers[0][0])
            remote_event = await recv(peers[1][0])
            assert ack['type'] == 'hero_emote_ack' and not ack['duplicate']
            assert local_event == remote_event
            assert local_event['seat'] == 0 and local_event['hero_card_id'] == 'HERO_08'
            assert local_event['emote_id'] == 'THANKS'
            assert match.version == version
            after = match.snapshot(0)
            assert after['version'] == before['version']
            assert after['public_hash'] == before['public_hash']
            assert after['moves'] == before['moves']

            await send(peers[0][1], command)
            replay_ack = await recv(peers[0][0])
            assert replay_ack['type'] == 'hero_emote_ack' and replay_ack['duplicate']
            with pytest.raises(asyncio.TimeoutError):
                await asyncio.wait_for(peers[1][0].readline(), .05)

            invalid = dict(command, request_id='network-0002', emote_id='CONCEDE')
            await send(peers[0][1], invalid)
            rejected = await recv(peers[0][0])
            assert rejected['type'] == 'hero_emote_error'
            with pytest.raises(asyncio.TimeoutError):
                await asyncio.wait_for(peers[1][0].readline(), .05)

            wrong_match = dict(command, request_id='network-0003', match_id='other-match')
            await send(peers[0][1], wrong_match)
            assert (await recv(peers[0][0]))['type'] == 'hero_emote_error'
            assert match.version == version
            await send(peers[0][1], {'type': 'action', 'kind': 'concede',
                                    'match_id': match_id, 'version': version,
                                    'action_id': 'seat-zero-concede-001'})
            assert (await recv(peers[0][0]))['phase'] == 'finished'
            assert (await recv(peers[1][0]))['phase'] == 'finished'
            await send(peers[0][1], {'type': 'cancel'})
            assert (await recv(peers[0][0]))['type'] == 'menu'
            assert len(server.rooms[match_id]) == 1 and server.rooms[match_id][0].seat == 1
            await send(peers[1][1], dict(command, request_id='remaining-seat-001', emote_id='WOW'))
            finished_error = await recv(peers[1][0])
            assert finished_error['type'] == 'hero_emote_error'
            assert 'list index' not in finished_error['message']
            with pytest.raises(asyncio.TimeoutError):
                await asyncio.wait_for(peers[1][0].readline(), .05)
        finally:
            for _, writer in peers:
                writer.close()
            for _, writer in peers:
                await writer.wait_closed()
            listener.close()
            await listener.wait_closed()
            for _ in range(100):
                if not server.rooms:
                    break
                await asyncio.sleep(.01)

    asyncio.run(run())
