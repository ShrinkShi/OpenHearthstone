"""单线程权威比赛循环；网络连接只是命令来源，不执行客户端逻辑。"""
from __future__ import annotations

import argparse
import asyncio
import json
import logging
import time
from pathlib import Path

from .accounts import AccountError, AccountService
from .database import initialize, validate_deck
from .hero_emotes import HeroEmoteSession
from .match import Match


class Peer:
    def __init__(self, writer):
        self.writer = writer
        self.name = ''
        self.class_id = 0
        self.deck = []
        self.match = None
        self.seat = -1
        self.account_id = None
        self.account_token = None
        self.online_account_id = None

    async def send(self, payload):
        self.writer.write((json.dumps(payload, ensure_ascii=False) + '\n').encode('utf-8'))
        await self.writer.drain()


class Server:
    def __init__(self, logdir, account_service=None):
        initialize()
        self.queue = []
        self.rooms = {}
        self.hero_emotes = {}
        self.logdir = Path(logdir)
        self.logdir.mkdir(parents=True, exist_ok=True)
        self.account_service = account_service
        self.online_accounts = {}
        self.peers = set()
        if self.account_service is not None:
            self.account_service.set_presence_provider(
                lambda account_id: self.online_accounts.get(account_id, 0) > 0)

    def _set_peer_presence(self, peer, account_id):
        previous = peer.online_account_id
        if previous == account_id:
            return
        if previous:
            remaining = self.online_accounts.get(previous, 0) - 1
            if remaining > 0:
                self.online_accounts[previous] = remaining
            else:
                self.online_accounts.pop(previous, None)
                try:
                    self.account_service.record_last_seen(previous)
                except Exception:
                    logging.exception('Could not persist account last-seen time')
        peer.online_account_id = account_id
        if account_id:
            self.online_accounts[account_id] = self.online_accounts.get(account_id, 0) + 1

    def _deauthenticate_token(self, token):
        if not isinstance(token, str):
            return
        for other in tuple(self.peers):
            if other.account_token == token:
                self._set_peer_presence(other, None)

    def _drop_expired_peer_presence(self, peer):
        if (self.account_service is not None and peer.online_account_id
                and peer.account_token
                and not self.account_service.session_is_active(peer.account_id, peer.account_token)):
            self._set_peer_presence(peer, None)

    def _account_seated(self, account_id, peer):
        if not account_id:
            return False
        for other in self.queue:
            if other is not peer and other.account_id == account_id:
                return True
        for room in self.rooms.values():
            for other in room:
                if (other is not peer and other.account_id == account_id
                        and other.match is not None and not other.match.game.ended):
                    return True
        return False

    def _record_account_progress(self, match, actor_seat):
        if self.account_service is None:
            return
        room = self.rooms.get(match.id, [])
        account_ids = [None, None]
        for peer in room:
            if peer.match is match and peer.seat in (0, 1):
                account_ids[peer.seat] = peer.account_id
        if not any(account_ids):
            return
        results = [player.playstate.name for player in match.game.players] if match.game.ended else None
        try:
            self.account_service.record_accepted_match(
                match.id, match.version, actor_seat, match.observer.events,
                account_ids, results)
        except Exception:
            # Match 权威结算不等待任务进度或客户端动画。
            logging.exception('Account quest progress failed')

    def record(self, match, command, seat, error=''):
        entry = {'time': time.time(), 'seat': seat, 'command': command, 'error': error,
                 'version': match.version, 'hash': match.snapshot(seat)['public_hash']}
        with (self.logdir / (match.id + '.jsonl')).open('a', encoding='utf-8') as stream:
            stream.write(json.dumps(entry, ensure_ascii=False) + '\n')
        (self.logdir / (match.id + '-state.json')).write_text(json.dumps(match.dump(), ensure_ascii=False), encoding='utf-8')

    async def broadcast(self, match):
        for peer in self.rooms.get(match.id, []):
            if not peer.writer.is_closing():
                try:
                    await peer.send(match.snapshot(peer.seat))
                except (ConnectionError, OSError):
                    pass

    async def handle(self, reader, writer):
        peer = Peer(writer)
        self.peers.add(peer)
        try:
            await peer.send({'type': 'hello', 'protocol': 1,
                             'capabilities': ['hero_emotes_v1'] +
                             (['accounts_v1', 'account_quests_four_slots_v1']
                              + ['account_social_v1', 'account_deck_codes_v1',
                                 'account_deck_format_metadata_v1']
                              if self.account_service is not None else [])})
            while True:
                raw = await reader.readline()
                if not raw:
                    break
                msg = {'type': 'malformed'}
                try:
                    msg = json.loads(raw)
                    if not isinstance(msg, dict):
                        raise ValueError('message must be an object')
                    if isinstance(msg.get('type'), str) and msg['type'].startswith('account_'):
                        if self.account_service is None:
                            raise AccountError('账号服务未启用')
                        operation = msg['type']
                        if (operation in ('account_register', 'account_login', 'account_resume', 'account_logout')
                                and (peer.match or peer in self.queue)):
                            raise AccountError('排队或对局中不能切换账号状态')
                        try:
                            old_token = peer.account_token
                            response, account_id, token = self.account_service.process(
                                peer.account_id, peer.account_token, msg)
                            if response.get('ok'):
                                peer.account_id, peer.account_token = account_id, token
                                if operation == 'account_logout':
                                    self._deauthenticate_token(old_token)
                                elif (operation in ('account_register', 'account_login')
                                      and not self.account_service.session_is_active(account_id, token)):
                                    self._set_peer_presence(peer, None)
                                else:
                                    self._set_peer_presence(peer, account_id)
                        except AccountError as exc:
                            self._drop_expired_peer_presence(peer)
                            response = self.account_service.error_response(
                                operation, msg.get('request_id'), str(exc))
                        except Exception:
                            logging.exception('Account request failed')
                            response = self.account_service.error_response(
                                operation, msg.get('request_id'), '账号服务暂时不可用')
                        await peer.send(response)
                    elif msg.get('type') == 'join':
                        if peer.match or peer in self.queue:
                            raise ValueError('already queued or in match')
                        if self.account_service is not None:
                            if not peer.account_id or not peer.account_token:
                                raise ValueError('resume account before joining')
                            if self._account_seated(peer.account_id, peer):
                                raise ValueError('account already queued or in a match')
                            saved = self.account_service.load_match_deck(
                                peer.account_id, peer.account_token, msg.get('deck_id'))
                            name, deck, class_id = saved['name'], saved['deck'], saved['class_id']
                        else:
                            deck_format = msg.get('format', 'wild')
                            if deck_format != 'wild':
                                raise ValueError('standard format is not configured')
                            name = msg.get('name')
                            if not isinstance(name, str) or not 1 <= len(name) <= 32:
                                raise ValueError('name must be 1..32 characters')
                            deck = msg.get('deck', [])
                            if not isinstance(deck, list) or any(not isinstance(c, str) for c in deck):
                                raise ValueError('invalid deck')
                            class_id = msg.get('class_id')
                            validate_deck(class_id, deck)
                        peer.name, peer.deck, peer.class_id = name, deck, class_id
                        self.queue.append(peer)
                        await peer.send({'type': 'queued'})
                        if len(self.queue) >= 2:
                            pair, self.queue = self.queue[:2], self.queue[2:]
                            match = Match([p.name for p in pair], [p.class_id for p in pair], [p.deck for p in pair])
                            self.rooms[match.id] = pair
                            for seat, p in enumerate(pair):
                                p.match, p.seat = match, seat
                            self.record(match, {'kind': 'GAME_START'}, -1)
                            await self.broadcast(match)
                    elif msg.get('type') == 'action':
                        if not peer.match:
                            raise ValueError('not in match')
                        changed = peer.match.apply(peer.seat, msg)
                        if changed:
                            self._record_account_progress(peer.match, peer.seat)
                        self.record(peer.match, msg, peer.seat)
                        if changed:
                            await self.broadcast(peer.match)
                        else:
                            await peer.send(peer.match.snapshot(peer.seat))
                    elif msg.get('type') == 'hero_emote':
                        request_id = msg.get('request_id', '') if isinstance(msg.get('request_id', ''), str) else ''
                        try:
                            match = peer.match
                            if (match is None or peer not in self.rooms.get(match.id, [])
                                    or peer.seat not in (0, 1)):
                                raise ValueError('not in matching session')
                            session = self.hero_emotes.setdefault(match.id, HeroEmoteSession(match.id))
                            event, duplicate = session.accept(match, peer.seat, msg)
                            await peer.send({'type': 'hero_emote_ack', 'request_id': request_id,
                                             'event_id': event['event_id'], 'duplicate': duplicate})
                            if not duplicate:
                                for recipient in self.rooms.get(match.id, []):
                                    if (recipient.match is match and recipient.seat in (0, 1)
                                            and not recipient.writer.is_closing()):
                                        await recipient.send(event)
                        except ValueError as exc:
                            await peer.send({'type': 'hero_emote_error', 'request_id': request_id,
                                             'message': str(exc)[:300]})
                    elif msg.get('type') == 'cancel':
                        if peer in self.queue:
                            self.queue.remove(peer)
                        if peer.match and not peer.match.game.ended:
                            raise ValueError('concede before leaving live match')
                        if peer.match:
                            room = self.rooms.get(peer.match.id, [])
                            if peer in room:
                                room.remove(peer)
                            if not room:
                                self.rooms.pop(peer.match.id, None)
                                self.hero_emotes.pop(peer.match.id, None)
                        peer.match, peer.seat = None, -1
                        await peer.send({'type': 'menu'})
                    elif msg.get('type') == 'ping':
                        await peer.send({'type': 'pong'})
                    else:
                        raise ValueError('unknown message type')
                except Exception as exc:
                    logging.exception('Rejected command')
                    await peer.send({'type': 'error', 'message': str(exc)[:300],
                                     'version': peer.match.version if peer.match else 0})
                    if peer.match:
                        self.record(peer.match, msg, peer.seat, str(exc))
                        await peer.send(peer.match.snapshot(peer.seat))
        except (ConnectionError, OSError, ValueError):
            pass
        finally:
            if peer in self.queue:
                self.queue.remove(peer)
            if peer.match:
                match = peer.match
                if not match.game.ended:
                    command = {'kind': 'concede', 'action_id': f'disconnect-{peer.seat}',
                               'version': match.version, 'match_id': match.id}
                    match.apply(peer.seat, command, cause='disconnect')
                    self._record_account_progress(match, peer.seat)
                    self.record(match, command, peer.seat)
                    await self.broadcast(match)
                room = self.rooms.get(match.id, [])
                if peer in room:
                    room.remove(peer)
                if not room:
                    self.rooms.pop(match.id, None)
                    self.hero_emotes.pop(match.id, None)
            self._set_peer_presence(peer, None)
            self.peers.discard(peer)
            writer.close()
            try:
                await writer.wait_closed()
            except (ConnectionError, OSError):
                # Windows Proactor 在对方进程被关闭后会再次报告连接重置。
                pass


async def main(host, port, logdir, account_db='local/accounts.sqlite3',
               pack_weights=None, golden_rate=0.05, duplicate_protection=True):
    server = Server(logdir, account_service=AccountService(
        account_db, pack_weights=pack_weights, golden_rate=golden_rate,
        duplicate_protection=duplicate_protection))
    listener = await asyncio.start_server(server.handle, host, port, limit=32768)
    print(f'Authoritative server listening on {host}:{port}', flush=True)
    async with listener:
        await listener.serve_forever()


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--host', default='127.0.0.1')
    p.add_argument('--port', type=int, default=37240)
    p.add_argument('--logdir', default='local/matches')
    p.add_argument('--account-db', default='local/accounts.sqlite3')
    p.add_argument('--common-weight', type=float, default=70)
    p.add_argument('--rare-weight', type=float, default=22)
    p.add_argument('--epic-weight', type=float, default=7)
    p.add_argument('--legendary-weight', type=float, default=1)
    p.add_argument('--golden-rate', type=float, default=0.05)
    p.add_argument('--no-duplicate-protection', action='store_true')
    args = p.parse_args()
    logging.basicConfig(level=logging.ERROR)
    pack_weights = {1: args.common_weight, 3: args.rare_weight,
                    4: args.epic_weight, 5: args.legendary_weight}
    asyncio.run(main(args.host, args.port, args.logdir, args.account_db,
                     pack_weights, args.golden_rate,
                     not args.no_duplicate_protection))
