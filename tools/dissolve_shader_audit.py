"""Record the exact original single-texture dissolve shader and blend state."""
import json

from .asset_lock import atomic_json
from .importer import PROJECT
from .inspect_compiled_shaders import variant_bindings


def main():
    directory = PROJECT / "local/compiled-shader-audit/Effects_AddMulDissolveSingleTex-4286994341503254994"
    parsed = json.loads((directory / "parsed-form.json").read_text(encoding="utf-8"))
    audit = json.loads((directory / "audit.json").read_text(encoding="utf-8"))
    if audit.get("errors"):
        raise ValueError("Original dissolve program audit contains errors")
    programs = {entry["index"]: entry for entry in audit["programs"] if entry["platform"] == 4}
    parameters = {entry["index"]: entry for entry in audit["parameter_records"] if entry["platform"] == 4}
    bindings = variant_bindings(parsed, [])
    for binding in bindings:
        binding["program_sha256"] = programs[binding["blob"]]["sha256"]
        binding["parameter_sha256"] = parameters[binding["parameter_blob"]]["sha256"]
    shader_pass = parsed["m_SubShaders"][0]["m_Passes"][0]
    names = {index: name for name, index in shader_pass["m_NameIndices"]}
    offsets = {}
    for stage in ("progVertex", "progFragment"):
        offsets[stage] = {names[value["m_NameIndex"]]: value["m_Index"]
                          for buffer in shader_pass[stage]["m_CommonParameters"]["m_ConstantBuffers"]
                          if names[buffer["m_NameIndex"]] == "UnityPerMaterial"
                          for value in buffer["m_VectorParams"]}
    atomic_json(PROJECT / "manifests/native-dissolve-shader.json", dict(
        shader={key: audit[key] for key in ("name", "bundle", "cab", "pathid")},
        family=10, bindings=bindings, material_byte_offsets=offsets,
        blend=shader_pass["m_State"]["rtBlend0"],
        notes=["Original red-only mask and vertex-alpha dissolve arithmetic reimplemented.",
               "This audit does not certify full card animation or original scene rendering."]))
    black_directory = directory.parent / "Utility_Black-5031480652861743436"
    black = json.loads((black_directory / "audit.json").read_text(encoding="utf-8"))
    black_parsed = json.loads((black_directory / "parsed-form.json").read_text(encoding="utf-8"))
    if black.get("errors"):
        raise ValueError("Original Black shader audit contains errors")
    black_programs = {entry["index"]: entry for entry in black["programs"] if entry["platform"] == 4}
    black_parameters = {entry["index"]: entry for entry in black["parameter_records"] if entry["platform"] == 4}
    black_bindings = variant_bindings(black_parsed, [])
    for binding in black_bindings:
        binding["program_sha256"] = black_programs[binding["blob"]]["sha256"]
        binding["parameter_sha256"] = black_parameters[binding["parameter_blob"]]["sha256"]
    black_state = black_parsed["m_SubShaders"][0]["m_Passes"][0]["m_State"]
    atomic_json(PROJECT / "manifests/native-black-shader.json", dict(
        shader={key: black[key] for key in ("name", "bundle", "cab", "pathid")},
        family=11, bindings=black_bindings,
        state={key: black_state[key] for key in ("rtBlend0", "zWrite", "zTest", "culling")},
        notes=["Textureless constant black/opaque output; back-face culling and depth write.",
               "Original empty fragment parameter record is type 1, not executable code."]))
    print("DISSOLVE SHADER AUDIT", len(bindings), "original stage bindings")
    print("BLACK SHADER AUDIT", len(black_bindings), "original stage bindings")


if __name__ == "__main__":
    main()
