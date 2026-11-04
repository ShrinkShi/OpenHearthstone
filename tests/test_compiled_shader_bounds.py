import struct

import pytest

from tools.inspect_compiled_shaders import segment_entries, BoundedShaderProgram, variant_bindings


@pytest.mark.parametrize('kind',[1,2,3,4,5])
def test_parameter_record_cannot_read_through_to_following_shader(kind):
    header=struct.pack('<I3I',1,16,12,0)
    parameter=struct.pack('<III',202012090,kind,9999999)
    program=BoundedShaderProgram(header+parameter,(2022,3))
    assert program.m_SubPrograms==[None]
    assert program.parameter_entries==[(0,kind,parameter)]


@pytest.mark.parametrize('raw',[
    b'\x01',struct.pack('<I',500000),struct.pack('<I3I',1,0,8,0)+b'\x00'*8,
    struct.pack('<I3I',1,16,500000,0)+b'\x00'*8,
    struct.pack('<I3I',1,16,8,1)+b'\x00'*8])
def test_invalid_or_unrestored_shader_segment_is_rejected(raw):
    with pytest.raises(ValueError):list(segment_entries(raw,(2022,3)))


def test_entry_identity_and_size_are_kept_exact():
    raw=struct.pack('<I3I3I',2,28,8,0,36,12,0)+b'A'*8+b'B'*12
    assert list(segment_entries(raw,(2022,3)))==[(0,b'A'*8),(1,b'B'*12)]


def test_variant_selection_respects_stage_mask_and_parameter_identity():
    program=dict(m_PlayerSubPrograms=[[],[],[],[dict(m_BlobIndex=126,m_GpuProgramType=15,m_KeywordIndices=[0]),dict(m_BlobIndex=127,m_GpuProgramType=15,m_KeywordIndices=[1])]],
        m_ParameterBlobIndices=[[],[],[],[1154,1158]],m_SerializedKeywordStateMask=[0,1])
    parsed=dict(m_KeywordNames=['noise','mask','pixel_only'],m_SubShaders=[dict(m_Passes=[dict(progVertex=program,m_State=dict(m_Name='FORWARD'))])])
    result=variant_bindings(parsed,['noise','pixel_only'])
    assert len(result)==1 and result[0]['blob']==126 and result[0]['parameter_blob']==1154
