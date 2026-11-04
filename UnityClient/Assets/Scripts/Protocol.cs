using System;

[Serializable] public class AssetEntry { public string id, path; }
[Serializable] public class AssetList { public AssetEntry[] entries; }
[Serializable] public class CardDef
{
    public string CardId, Name, Text, FlavorText, ArtistName, ArtAsset, AudioAsset, VisualAsset, ScriptId;
    public string[] RelatedCardIds;
    public int Class, Set, Type, Cost, Attack, Health, Durability, Race, Rarity, Blood, Frost, Unholy;
    public int EliteTag, MercenaryTag;
    public bool Collectible, Playable;
}
[Serializable] public class ClassDef { public int id; public string name, hero; public string[] deck; public bool available; }
[Serializable] public class CardDB { public CardDef[] cards; public ClassDef[] classes; }
[Serializable] public class EntityView
{
    public int entity, cost, attack, health, armor, durability, max_health, max_durability;
    public bool health_buffed,durability_buffed,attack_buffed,combo_ready,asleep,used;
    public int display_damage;
    public bool display_damage_known;
    public int display_heal;
    public bool display_heal_known;
    public string card_id;
    public string[] keywords;
}
[Serializable] public class PlayerView
{
    public string name, result;
    public int seat, mana, max_mana, deck_count, hand_count, secret_count, corpses, corpses_spent;
    public EntityView hero, power, weapon;
    public EntityView[] hand, field, secrets;
}
[Serializable] public class Move
{
    public string kind, choice;
    public int entity, target, index;
    public int[] cards;
}
[Serializable] public class SemanticEvent { public string type, card_id, event_id; public int source, target, card_type; public int owner=-1,index=-1; public float amount; public EntityView visual; }
[Serializable] public class StateView
{
    public string type, message, match_id, phase, public_hash, request_id, event_id, emote_id, hero_card_id;
    public string[] capabilities;
    public bool duplicate;
    public int version, seat, turn, active;
    public PlayerView[] players;
    public Move[] moves;
    public EntityView[] choices;
    public SemanticEvent[] events;
}
[Serializable] public class HeroEmoteMessage
{
    public string type="hero_emote", match_id, emote_id, request_id;
}
[Serializable] public class HeroSpeechCandidate { public string id; public float duration; }
[Serializable] public class HeroSpeechEntry
{
    public string id, card_id, emote_id, game_string_key, text, label, audio_id;
    public float duration;
    public HeroSpeechCandidate[] candidates;
}
[Serializable] public class HeroSpeechLabel { public string id, text, game_string_key; }
[Serializable] public class HeroSpeechSource
{
    public string card_id, field, emote_id, sound_ref, game_string_key, bubble_spell_ref;
    public int emote_type;
}
[Serializable] public class HeroSpeechBubbleSource
{
    public string logical_id, asset_name, asset_id, bundle, cab, pathid, status, actor_link, region_origin, region_method, sha256;
    public int width, height;
    public HeroSpeechRegion[] regions;
}
[Serializable] public class HeroSpeechRegion
{
    public string id;
    public int[] source_rect_px, tail_tip_px, text_rect_px;
    public float[] uv;
    public bool flip_y;
}
[Serializable] public class HeroSpeechIndex
{
    public int schema_version;
    public string locale;
    public HeroSpeechEntry[] entries;
    public HeroSpeechLabel[] labels;
    public HeroSpeechSource[] source_rows;
    public HeroSpeechBubbleSource bubble_art;
}
[Serializable] public class JoinMessage { public string type = "join", name; public int class_id; public string[] deck; }
[Serializable] public class ActionMessage
{
    public string type = "action", kind, choice, action_id, match_id;
    public int entity, target, version, index;
    public int[] cards;
}
[Serializable] public class SavedDeck { public int class_id; public string[] cards; }
[Serializable] public class Profile { public string name, host; public int class_id; public string[] deck; public SavedDeck[] decks; }
[Serializable] public class MeshData { public float[] vertices, uv, uv2, normals, colors, bone_weights, bindposes; public int[] bone_indices; public bool source_normals; public SubMesh[] submeshes; }
[Serializable] public class SubMesh { public int[] triangles; }
[Serializable] public class SceneMaterial
{
    public string name, texture, properties, shader;
    public NativeMaterial native;
    public bool transparent, effect;
    public float intensity=1;
    public UnityEngine.Color tint=UnityEngine.Color.white;
    public UnityEngine.Vector2 texture_scale=UnityEngine.Vector2.one, texture_offset;
}
[Serializable] public class SceneNode
{
    public string name, mesh, particle;
    public int parent, sorting_order;
    public bool active;
    public UnityEngine.Vector3 position, scale;
    public UnityEngine.Quaternion rotation;
    public SceneMaterial[] materials;
    public SceneCamera camera;
}
[Serializable] public class SceneCamera { public float field_of_view, near, far; }
[Serializable] public class SceneActor { public int owner,portrait,portrait_material_index;public string spell_table,animation_file; }
[Serializable] public class SceneData { public SceneNode[] nodes;public SceneActor[] actors; }
[Serializable] public class VoiceCandidate { public string id; public float weight; }
[Serializable] public class VoiceEntry { public string id, card_id, @event, fallback_id, selection_strategy; public float delay; public bool suppress_play_sound_on_summon; public VoiceCandidate[] candidates; }
[Serializable] public class VoiceIndex { public VoiceEntry[] entries; }
