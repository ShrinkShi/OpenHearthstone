"""Read original objects without Importer, cache mutation or original DLL execution."""
import hashlib
import json
from pathlib import Path
import sqlite3
from tools.vendor.unity import UnityReader

PROJECT = Path(__file__).resolve().parents[2]


class ReadOnlyStore:
    def __init__(self):
        self.db = sqlite3.connect((PROJECT / "local/asset-index.sqlite").as_uri() + "?mode=ro", uri=True)
        self.db.row_factory = sqlite3.Row

    def get_meta(self, key):
        row = self.db.execute("SELECT value FROM meta WHERE key=?", (key,)).fetchone()
        return json.loads(row[0]) if row else None

    def set_meta(self, *args):
        raise RuntimeError("V005-r2 source reader is read-only")


def main():
    reader = UnityReader(Path(r"D:\Program Files (x86)\Hearthstone"), ReadOnlyStore())
    runtime = PROJECT / "local/runtime"
    output = PROJECT / "work/v005-r2-source/independent-source.json"
    result = {"checks": [], "files": {}, "captures": [], "portraits": [], "official_fidelity_verified": False}

    def check(value, label):
        assert value, label
        result["checks"].append(label)

    def record(obj):
        bundle = reader.cabs[obj.assets_file.name.lower()]
        source = reader.source_paths()[bundle]
        result["files"][bundle] = dict(path=str(source), sha256=hashlib.sha256(source.read_bytes()).hexdigest())
        return dict(bundle=bundle, cab=obj.assets_file.name, pathid=str(obj.path_id))

    config = json.loads((runtime / "native-fireblast.json").read_text(encoding="utf-8"))
    card = reader.definition("HERO_08bp")
    check(card["m_PlayEffectDef"]["m_SpellPath"] == config["programs"][0]["reference"], "real HERO_08bp CardDef binds this SuperSpell")
    root, _, _ = reader.resolve(config["programs"][0]["reference"])
    supers = []
    for obj, tree in reader.walk(root, include_visual=False):
        if obj.type.name != "MonoBehaviour":
            continue
        tree = tree or obj.read_typetree()
        script = reader.pointer(obj, tree["m_Script"])
        if script and script.read_typetree()["m_ClassName"] == "SuperSpell":
            supers.append(tree)
            check(str(obj.path_id) == config["programs"][0]["source"]["pathid"], "real SuperSpell component identity")
            record(obj)
    check(len(supers) == 1 and any(v == config["programs"][0]["missile"] for v in supers[0].values()), "complete real missile definition matches cache")
    for entry in config["prefabs"]:
        original, _, _ = reader.resolve(entry["reference"])
        graph = json.loads((runtime / entry["file"]).read_text(encoding="utf-8"))
        captures = []
        for obj, tree in reader.walk(original, include_visual=False):
            if obj.type.name != "MonoBehaviour":
                continue
            tree = tree or obj.read_typetree()
            script = reader.pointer(obj, tree["m_Script"])
            if script and script.read_typetree()["m_ClassName"] == "RenderToTexture":
                captures.append((obj, tree))
        check(len(captures) == len(graph["captures"]), "all real capture components covered " + entry["reference"])
        for (obj, raw), capture in zip(captures, graph["captures"]):
            for field in ("width", "height", "resolution"):
                check(capture[field] == raw["m_" + field.title()], "capture source " + field)
            for cache, source in (("near", "m_NearClip"), ("far", "m_FarClip"), ("position", "m_PositionOffset"),
                                  ("camera_offset", "m_CameraOffset"), ("clear", "m_ClearColor")):
                check(capture[cache] == raw[source], "capture source " + cache)
            check(capture["target"] == -1 and raw["m_RenderToObject"]["m_PathID"] == 0, "original automatic target is genuinely absent")
            for name, value in capture["original_parameters"].items():
                check(value == raw[name], "exact original capture parameter " + name)
            check(raw["m_CreateRenderPlane"] == 1 and raw["m_RenderMaterial"] == 5 and raw["m_BloomBlend"] == 0,
                  "source chooses generated alpha-clip and additive bloom")
            result["captures"].append(dict(source=record(obj), size=[raw["m_Width"], raw["m_Height"]],
                                           resolution=raw["m_Resolution"], parameters=capture["original_parameters"]))
    from PIL import Image
    registry = json.loads((runtime / "registry.json").read_text(encoding="utf-8"))
    for card_id in ("HERO_08bp", "CS2_119"):
        definition = reader.definition(card_id)
        texture, _, _ = reader.resolve(definition["m_PortraitTexturePath"])
        png = runtime / next(v["path"] for v in registry["entries"] if v["id"] == "CARD_" + card_id)
        decoded = texture.read().image.convert("RGBA")
        cached = Image.open(png).convert("RGBA")
        check(cached.size == decoded.size and cached.tobytes() == decoded.tobytes(),
              "runtime PNG equals freshly decoded original portrait " + card_id)
        result["portraits"].append(dict(card_id=card_id, source=record(texture), png_sha256=hashlib.sha256(png.read_bytes()).hexdigest(),
                                        size=cached.size, color_space=texture.read_typetree()["m_ColorSpace"]))
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print("V005_R2_SOURCE", len(result["checks"]), "checks;", len(result["captures"]), "real captures;", output)


if __name__ == "__main__":
    main()
