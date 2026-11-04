import copy
import pytest

from tools.native_animation_wrap_refresh import apply_modes


def fixture():
    program={'animations':[{'id':'clip','name':'Original','loop':False,'tracks':[{'keys':[1,2]}]}],
             'nodes':[{'name':'Root','materials':[{'shader':'Original'}]}]}
    source={'clip':{'name':'Original','wrap_known':True,'wrap_mode':4,'source':{'pathid':'42'}}}
    settings={'wrap_known':True,'wrap_mode':0,'default_clip':'clip','source':{'pathid':'43'}}
    return program,source,settings


def test_mode_refresh_preserves_curves_materials_and_legacy_loop_flag():
    program,source,settings=fixture();apply_modes(program,source,[(0,settings)])
    assert program['animations'][0]=={'id':'clip','name':'Original','loop':False,'tracks':[{'keys':[1,2]}],
                                      'wrap_known':True,'wrap_mode':4,'source':{'pathid':'42'}}
    assert program['nodes'][0]['materials']==[{'shader':'Original'}]
    assert program['nodes'][0]['legacy_animation']==settings


@pytest.mark.parametrize('fault',['missing','renamed','owner','default'])
def test_invalid_source_metadata_leaves_entire_program_unchanged(fault):
    program,source,settings=fixture();before=copy.deepcopy(program);node=0
    if fault=='missing':source={}
    if fault=='renamed':source['clip']['name']='Other'
    if fault=='owner':node=-1
    if fault=='default':settings['default_clip']='missing'
    with pytest.raises(ValueError):apply_modes(program,source,[(node,settings)])
    assert program==before
