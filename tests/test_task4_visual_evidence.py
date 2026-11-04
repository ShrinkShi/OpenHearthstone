from tools.task4_visual_evidence import CARDS, PHASES, official_gaps


def test_missing_reference_never_becomes_zero_difference_or_pass():
    gaps = official_gaps({'sequences': []})
    assert len(gaps) == 4
    assert all(row['status'] == 'MISSING' and 'source_kind' in row['missing'] for row in gaps)
    assert all('frame:' + phase in row['missing'] for row in gaps for phase in PHASES)


def test_wrong_card_or_internal_sequence_does_not_satisfy_official_contract():
    row = {'card': 'EX1_124', 'seat': 0, 'source_kind': 'official_client_capture'}
    assert all(r['status'] == 'MISSING' for r in official_gaps({'sequences': [row]}))
    row = dict(row, card=CARDS[1], source_kind='project_generated')
    assert 'source_kind' in official_gaps({'sequences': [row]})[2]['missing']


def test_complete_metadata_still_requires_provenance_and_comparability_review():
    row = {key: 'recorded' for key in ('source_media', 'source_sha256', 'product_version', 'capture_origin',
           'camera', 'resolution', 'state', 'clock', 'seed', 'color_pipeline', 'audio_environment')}
    row.update(card=CARDS[0], seat=0, source_kind='official_client_capture',
               frames=[dict(phase=p, file='frame.png', sha256='hash', absolute_time=0, event_alignment='event') for p in PHASES])
    result = official_gaps({'sequences': [row]})
    assert not result[0]['missing']
    assert result[0]['status'] == 'REQUIRES_PROVENANCE_AND_COMPARABILITY_REVIEW'
    assert result[1]['status'] == 'MISSING'
