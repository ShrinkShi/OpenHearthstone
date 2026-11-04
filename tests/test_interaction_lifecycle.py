import pytest

from tools.original_particle_tracks import curve, gradient, tracks
from tools.visual_scenarios import fixture, transition


@pytest.mark.parametrize('card_id', ['EX1_134', 'EX1_133'])
def test_combo_target_choices_are_authoritative_for_minion_and_weapon(card_id):
    match = fixture(0, 1, classes=(7, 10))
    player = match.game.players[0]
    card = player.give(card_id)
    inactive = [m for m in match.legal_moves(0) if m.get('entity') == card.entity_id]
    if card_id == 'EX1_134':
        assert inactive and all(m.get('target', 0) == 0 for m in inactive)
    player.give('GAME_005').play()
    moves = [m for m in match.legal_moves(0) if m.get('entity') == card.entity_id]
    assert moves and all(m.get('target', 0) != 0 for m in moves)
    target = player.opponent.field[0]
    match.observer.events.clear()
    after = transition(match, 'play', lambda m: m.get('entity') == card.entity_id and m.get('target') == target.entity_id)
    assert after['public_hash'] == match.snapshot(1)['public_hash']
    assert any(e['type'] == 'CardPlay' and e['target'] == target.entity_id for e in after['events'])


def test_lethal_spell_has_final_result_without_server_animation_wait():
    match = fixture(0, 0)
    player = match.game.players[0]
    player.opponent.hero.damage = 29
    card = player.give('CS2_029')
    after = transition(match, 'play', lambda m: m.get('entity') == card.entity_id and m.get('target') == player.opponent.hero.entity_id)
    assert after['phase'] == 'finished'
    assert [p['result'] for p in after['players']] == ['WON', 'LOST']
    assert after['public_hash'] == match.snapshot(1)['public_hash']


def test_particle_export_preserves_independent_color_and_alpha_times():
    data = dict(minMaxState=1, maxGradient=dict(m_NumColorKeys=2,m_NumAlphaKeys=2,
        ctime0=0,ctime1=65535,atime0=8000,atime1=40000,
        key0=dict(r=1,g=0,b=0,a=.2),key1=dict(r=0,g=1,b=0,a=.8)))
    result = gradient(data)
    assert result['enabled']
    assert result['colors'][0]['time'] == 0
    assert result['alpha'][0]['time'] == pytest.approx(8000/65535)
    assert result['alpha'][1]['value'] == .8


def test_curve_export_preserves_tangents_and_weighted_mode():
    keys=[dict(time=0,value=1,inSlope=0,outSlope=2,weightedMode=2,inWeight=.3,outWeight=.7)]
    result=curve(dict(minMaxState=1,scalar=3,maxCurve=dict(m_Curve=keys)))
    assert result['keys'] == keys and result['scalar'] == 3 and result['enabled']


def test_disabled_modules_are_not_invented():
    result=tracks(dict(InitialModule={},ColorModule=dict(enabled=False),SizeModule=dict(enabled=False)))
    assert result['restored']
    assert result['color'] is None and result['size'] is None and result['rotation'] is None
