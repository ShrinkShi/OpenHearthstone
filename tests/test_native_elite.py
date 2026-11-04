import json
from pathlib import Path

from game.database import initialize
from fireplace import cards
from hearthstone.enums import GameTag


ROOT=Path(__file__).parents[1]


def test_local_display_database_preserves_entity_elite_tag_instead_of_deriving_rarity():
    initialize()
    database=json.loads((ROOT/'local/card-database.json').read_text(encoding='utf-8'))
    for card in database['cards']:
        original=cards.db[card['CardId']]
        assert card['EliteTag']==int(original.tags.get(GameTag.ELITE,0))
        assert card['MercenaryTag']==int(original.tags.get(1665,0))


def test_both_original_elite_action_owner_and_events_are_refreshed():
    audit=json.loads((ROOT/'manifests/native-actor-elite-refresh.json').read_text(encoding='utf-8'))
    assert len(audit['programs'])==2 and not audit['failures']
    for record in audit['programs']:
        assert record['actions']==1 and record['unresolved']==0
        source=json.loads((ROOT/'local/runtime'/record['file']).read_text(encoding='utf-8'))
        actions=[a for m in source['machines'] for s in m['states'] for a in s['actions'] if a['name']=='ActorEliteEventAction']
        assert len(actions)==1
        parameters={p['name']:p for p in actions[0]['params']}
        assert parameters['m_ActorObject']['variable']=='Actor'
        assert parameters['m_EliteEvent']['text']=='On'
        assert parameters['m_NonEliteEvent']['text']=='Off'
