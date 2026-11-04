import asyncio
import json
import pytest

from game.database import preset
from game.server import Server


def test_two_tcp_peers_complete_match(tmp_path):
    async def run():
        server = Server(tmp_path)
        listener = await asyncio.start_server(server.handle, '127.0.0.1', 0, limit=32768)
        port = listener.sockets[0].getsockname()[1]
        r0, w0 = await asyncio.open_connection('127.0.0.1', port)
        r1, w1 = await asyncio.open_connection('127.0.0.1', port)
        async def recv(r):
            return json.loads(await asyncio.wait_for(r.readline(), 8))
        async def send(w, x):
            w.write((json.dumps(x) + '\n').encode())
            await w.drain()
        assert (await recv(r0))['type'] == 'hello'
        assert (await recv(r1))['type'] == 'hello'
        await send(w0, {'type': 'join', 'name': 'Shrink1', 'class_id': 4, 'deck': preset(4)})
        assert (await recv(r0))['type'] == 'queued'
        await send(w1, {'type': 'join', 'name': 'Shrink2', 'class_id': 10, 'deck': preset(10)})
        assert (await recv(r1))['type'] == 'queued'
        states = [await recv(r0), await recv(r1)]
        assert states[0]['match_id'] == states[1]['match_id']
        await send(w0, {'type': 'action', 'kind': 'end_turn', 'match_id': states[0]['match_id'], 'version': 0, 'action_id': 'bad'})
        assert (await recv(r0))['type'] == 'error'
        states[0] = await recv(r0)
        for i in range(1500):
            if states[0]['phase'] == 'finished':
                break
            seat = next((s for s in (0, 1) if states[s]['moves']), None)
            assert seat is not None
            moves = states[seat]['moves']
            move = next((m for kind in ('mulligan', 'choose', 'attack', 'play', 'power', 'end_turn')
                         for m in moves if m['kind'] == kind), None)
            await send((w0, w1)[seat], dict(move, type='action', action_id=str(i),
                match_id=states[seat]['match_id'], version=states[seat]['version']))
            states = [await recv(r0), await recv(r1)]
            assert states[0]['version'] == states[1]['version']
            assert states[0]['public_hash'] == states[1]['public_hash']
            assert not states[0]['players'][1]['hand']
        assert states[0]['phase'] == 'finished'
        assert sorted(p['result'] for p in states[0]['players']) == ['LOST', 'WON']
        await send(w0, {'type': 'cancel'})
        assert (await recv(r0))['type'] == 'menu'
        w0.close()
        w1.close()
        await w0.wait_closed()
        await w1.wait_closed()
        listener.close()
        await listener.wait_closed()
    asyncio.run(run())


@pytest.mark.parametrize('payload', [b'{bad\n', b'[]\n', b'null\n', b'{"type":"unknown"}\n'])
def test_malformed_protocol_does_not_poison_connection(tmp_path, payload):
    async def run():
        server = Server(tmp_path)
        listener = await asyncio.start_server(server.handle, '127.0.0.1', 0)
        reader, writer = await asyncio.open_connection('127.0.0.1', listener.sockets[0].getsockname()[1])
        await reader.readline()
        writer.write(payload)
        await writer.drain()
        reply = json.loads(await asyncio.wait_for(reader.readline(), 5))
        assert reply['type'] == 'error' and not server.queue
        writer.write(b'{"type":"ping"}\n')
        await writer.drain()
        assert json.loads(await asyncio.wait_for(reader.readline(), 5))['type'] == 'pong'
        writer.close()
        await writer.wait_closed()
        listener.close()
        await listener.wait_closed()
    asyncio.run(run())


@pytest.mark.parametrize('abrupt', [False, True])
def test_disconnect_ends_match_and_cleans_room(tmp_path, abrupt):
    async def run():
        server = Server(tmp_path)
        listener = await asyncio.start_server(server.handle, '127.0.0.1', 0)
        peers = []
        for name, cls in [('DK', 1), ('DH', 14)]:
            r, w = await asyncio.open_connection('127.0.0.1', listener.sockets[0].getsockname()[1])
            peers.append((r, w))
            await r.readline()
            w.write((json.dumps(dict(type='join', name=name, class_id=cls, deck=preset(cls)))+'\n').encode())
            await w.drain()
            assert json.loads(await r.readline())['type'] == 'queued'
        states = [json.loads(await r.readline()) for r, _ in peers]
        if abrupt:
            peers[1][1].transport.abort()
        else:
            peers[1][1].close()
        await peers[1][1].wait_closed()
        result = json.loads(await asyncio.wait_for(peers[0][0].readline(), 5))
        assert result['phase'] == 'finished'
        assert result['players'][0]['result'] == 'WON'
        assert result['players'][1]['result'] == 'LOST'
        assert result['version'] == states[0]['version']+1
        peers[0][1].close()
        await peers[0][1].wait_closed()
        for _ in range(100):
            if not server.rooms:
                break
            await asyncio.sleep(.01)
        assert not server.rooms and not server.queue
        listener.close()
        await listener.wait_closed()
    asyncio.run(run())


def test_windows_reset_during_close_does_not_escape_cleanup(tmp_path):
    class ResetWriter:
        def __init__(self):
            self.closed = False

        def write(self, data):
            pass

        async def drain(self):
            pass

        def close(self):
            self.closed = True

        async def wait_closed(self):
            raise ConnectionResetError(64, 'The specified network name is no longer available')

    async def run():
        server = Server(tmp_path)
        reader = asyncio.StreamReader()
        reader.feed_eof()
        writer = ResetWriter()
        await server.handle(reader, writer)
        assert writer.closed and not server.queue and not server.rooms

    asyncio.run(run())


def test_simultaneous_mulligans_accept_same_initial_version(tmp_path):
    async def run():
        server = Server(tmp_path)
        listener = await asyncio.start_server(server.handle, '127.0.0.1', 0)
        peers = []
        for name, cls in [('Shrink1', 4), ('Shrink2', 10)]:
            reader, writer = await asyncio.open_connection('127.0.0.1', listener.sockets[0].getsockname()[1])
            peers.append((reader, writer))
            await reader.readline()
            writer.write((json.dumps(dict(type='join', name=name, class_id=cls, deck=preset(cls)))+'\n').encode())
            await writer.drain()
            assert json.loads(await reader.readline())['type'] == 'queued'
        states = [json.loads(await reader.readline()) for reader, _ in peers]
        assert states[0]['version'] == states[1]['version'] == 0
        for seat, (_, writer) in enumerate(peers):
            selected = [states[seat]['players'][seat]['hand'][0]['entity']]
            command = dict(type='action', kind='mulligan', cards=selected,
                           match_id=states[seat]['match_id'], version=0, action_id=f'same-version-{seat}')
            writer.write((json.dumps(command)+'\n').encode())
        for _, writer in peers:
            await writer.drain()
        final = []
        for reader, _ in peers:
            first = json.loads(await asyncio.wait_for(reader.readline(), 5))
            second = json.loads(await asyncio.wait_for(reader.readline(), 5))
            assert first['type'] == second['type'] == 'state'
            assert first['version'] == 1 and second['version'] == 2
            assert second['phase'] == 'play'
            final.append(second)
        assert final[0]['public_hash'] == final[1]['public_hash']
        for _, writer in peers:
            writer.close()
            await writer.wait_closed()
        for _ in range(100):
            if not server.rooms:
                break
            await asyncio.sleep(.01)
        assert not server.rooms
        listener.close()
        await listener.wait_closed()
    asyncio.run(run())
