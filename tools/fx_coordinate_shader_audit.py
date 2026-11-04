"""Record original FX coordinate programs and their named constant offsets."""
import json

from .asset_lock import atomic_json
from .importer import PROJECT
from .inspect_compiled_shaders import variant_bindings


def main():
    shaders = []
    for suffix, family in (("World-4422613114386012427", 8),
                           ("Scrolling--8682043857861559096", 9),
                           ("ScrollingManual--2418971604062054018", 12),
                           ("Rotation-3752617471019072207", 13),
                           ("Scrolling2Layer--8673478984178380409", 14)):
        directory = PROJECT / "local/compiled-shader-audit" / ("Hero_AddMultiply_AddMultiplyFX" + suffix)
        parsed = json.loads((directory / "parsed-form.json").read_text(encoding="utf-8"))
        audit = json.loads((directory / "audit.json").read_text(encoding="utf-8"))
        if audit.get("errors"):
            raise ValueError("Original shader audit contains errors")
        programs = {p["index"]: p for p in audit["programs"] if p["platform"] == 4}
        parameters = {p["index"]: p for p in audit["parameter_records"] if p["platform"] == 4}
        bindings = variant_bindings(parsed, [])
        for binding in bindings:
            binding["program_sha256"] = programs[binding["blob"]]["sha256"]
            binding["parameter_sha256"] = parameters[binding["parameter_blob"]]["sha256"]
        pass_layouts = []
        for shader_pass in parsed["m_SubShaders"][0]["m_Passes"]:
            names = {index: name for name, index in shader_pass["m_NameIndices"]}
            layout = {}
            for stage in ("progVertex", "progFragment"):
                layout[stage] = {
                    names[buffer["m_NameIndex"]]: {
                        names[value["m_NameIndex"]]: value["m_Index"]
                        for value in buffer["m_VectorParams"]
                    }
                    for buffer in shader_pass[stage]["m_CommonParameters"]["m_ConstantBuffers"]
                }
            pass_layouts.append(layout)
        shaders.append(dict(shader={key: audit[key] for key in ("name", "bundle", "cab", "pathid")},
                            family=family, bindings=bindings, constant_byte_offsets=pass_layouts[0],
                            pass_constant_byte_offsets=pass_layouts,
                            restored_passes=[0,1] if family in (9,14) else [0], unrestored_passes=[]))
    atomic_json(PROJECT / "manifests/native-fx-coordinate-shaders.json", dict(
        shaders=shaders,
        notes=["Base DXBC arithmetic reimplemented; original binaries are not executed.",
               "Scrolling and TwoLayer Glow share verified original arithmetic. No additional card slot enabled.",
               "Source/target ownership, global Bloom and full original scenes remain incomplete."]))
    print("FX COORDINATE SHADER AUDIT", len(shaders), "base families")


if __name__ == "__main__":
    main()
