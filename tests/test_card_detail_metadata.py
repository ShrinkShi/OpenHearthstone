from game import database
from hearthstone.enums import GameTag


def test_card_detail_metadata_uses_simplified_chinese_flavor_and_artist():
    local = {
        'TLC_817': {
            'm_flavorText': {'m_locValues': [''] * 12 + ['第6天。']},
            'm_artistName': 'BOSi Studio',
        }
    }

    detail = database.card_detail_metadata('TLC_817', local, {})

    assert detail == {
        'FlavorText': '第6天。',
        'ArtistName': 'BOSi Studio',
        'RelatedCardIds': [],
    }


def test_card_detail_metadata_defaults_for_old_or_missing_local_records():
    assert database.card_detail_metadata('MISSING', {}, {}) == {
        'FlavorText': '',
        'ArtistName': '',
        'RelatedCardIds': [],
    }
    assert database.localized_text({'m_flavorText': {'m_locValues': ['en']}}, 'm_flavorText') == ''


def test_local_relationships_maps_only_direct_quest_and_collection_edges():
    local = {
        'TLC_817': {'m_ID': 117544},
        'TLC_817t3': {'m_ID': 118551},
        'TLC_817t5': {'m_ID': 119879},
    }
    tags = [
        {'m_cardId': 117544, 'm_tagId': int(GameTag.QUEST_REWARD_DATABASE_ID), 'm_tagValue': 119879},
        {'m_cardId': 118551, 'm_tagId': int(GameTag.COLLECTION_RELATED_CARD_DATABASE_ID), 'm_tagValue': 119879},
        {'m_cardId': 117544, 'm_tagId': int(GameTag.CARD_SET), 'm_tagValue': 3},
        {'m_cardId': 117544, 'm_tagId': int(GameTag.QUEST_REWARD_DATABASE_ID), 'm_tagValue': 999999},
    ]

    assert database.local_relationships(local, tags) == {
        'TLC_817': ['TLC_817t5'],
        'TLC_817t3': ['TLC_817t5'],
    }
