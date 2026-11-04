"""Validate isolated hero-emote messages without touching match rules."""
from __future__ import annotations

import re
import time


EMOTE_IDS = frozenset({
    'THANKS', 'WELL_PLAYED', 'GREETINGS', 'WOW', 'OOPS', 'THREATEN', 'SORRY',
})
_REQUEST_ID = re.compile(r'^[A-Za-z0-9_-]{8,80}$')


class HeroEmoteSession:
    """Per-match replay and rate-limit state for client emote messages."""

    def __init__(self, match_id, cooldown=1.0, max_requests=4096):
        self.match_id = match_id
        self.cooldown = cooldown
        self.max_requests = max_requests
        self.last_sent = {}
        self.accepted = {}

    def accept(self, match, seat, command, now=None):
        if match is None or match.id != self.match_id:
            raise ValueError('match mismatch')
        if type(seat) is not int or seat not in (0, 1):
            raise ValueError('invalid match seat')
        if not isinstance(command, dict) or command.get('type') != 'hero_emote':
            raise ValueError('invalid hero emote message')
        if command.get('match_id') != self.match_id:
            raise ValueError('match mismatch')
        emote_id = command.get('emote_id')
        if not isinstance(emote_id, str) or emote_id not in EMOTE_IDS:
            raise ValueError('invalid hero emote')
        request_id = command.get('request_id')
        if not isinstance(request_id, str) or not _REQUEST_ID.fullmatch(request_id):
            raise ValueError('invalid hero emote request_id')
        if match.game.ended:
            raise ValueError('match already ended')

        key = (seat, request_id)
        fingerprint = (self.match_id, emote_id)
        previous = self.accepted.get(key)
        if previous:
            if previous['fingerprint'] != fingerprint:
                raise ValueError('request_id reused with different emote')
            return previous['event'], True
        if len(self.accepted) >= self.max_requests:
            raise ValueError('hero emote request limit reached')

        now = time.monotonic() if now is None else now
        last = self.last_sent.get(seat)
        if last is not None and (now < last or now - last < self.cooldown):
            raise ValueError('hero emote rate limit')

        hero_id = match.game.players[seat].hero.id
        event = {
            'type': 'hero_emote_event',
            'match_id': self.match_id,
            'event_id': f'{self.match_id}:{seat}:{request_id}',
            'request_id': request_id,
            'seat': seat,
            'hero_card_id': hero_id,
            'emote_id': emote_id,
        }
        self.accepted[key] = {'fingerprint': fingerprint, 'event': event}
        self.last_sent[seat] = now
        return event, False
