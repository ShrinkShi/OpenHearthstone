import pytest

from game import database


def test_generated_expansion_card_has_metadata_but_cannot_enter_deck(tmp_path, monkeypatch):
    (tmp_path/'local').mkdir()
    monkeypatch.setattr(database, 'ROOT', tmp_path)
    output = database.build_database()
    definitions = {c['CardId']: c for c in output['cards']}
    generated = definitions['UNG_941']
    assert generated['Cost'] == 2 and generated['Class'] == 4 and generated['Type'] == 5
    assert generated['Name'] and generated['Text'] and not generated['Playable']
    assert sum(c['Playable'] for c in output['cards']) == 489
    deck = database.preset(4)
    deck[0] = 'UNG_941'
    with pytest.raises(ValueError, match='outside playable scope'):
        database.validate_deck(4, deck)
