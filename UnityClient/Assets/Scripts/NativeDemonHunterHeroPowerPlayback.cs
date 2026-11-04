using System;
using System.IO;
using System.Linq;
using UnityEngine;

// This route is limited to the observed H10 one-buff batch and its imported source graph.
public static class NativeDemonHunterHeroPowerPlayback
{
    public const string HeroPowerId = "HERO_10bp";
    public const string HeroId = "HERO_10";
    public const int TargetRootNode = 12;
    public const string RootReference = "ReuseFX_BuffImpact_DemonHunter_Small_Super.prefab:7d6b21e193de5cc44a280f817f088120";
    public const string StartReference = "ReuseFX_Idle_DemonHunter_Small.prefab:52ef5d6ab84e8d947ad46489243e4e9a";
    public const string ImpactReference = "ReuseFX_BuffImpact_DemonHunter_Small.prefab:b300463bc19840848a171d18c17dcb3e";
    public const string RuntimeProfile = "source-backed HERO_10bp self-hero Buff route; no-History Impact is immediate per source IL";
    public const float PlaybackSpeed = 1.15f;
    const string RootFile = "77a947e5d6ef3ec34b9f2f93.hero-power-source.json";
    const string StartFile = "d7f540fad79d4d4b8410ee69.hero-power-prefab.json";
    const string ImpactFile = "3b1718c0436a04bebd5bacbf.hero-power-prefab.json";
    static readonly int[] ExpectedParticleNodes = { 7, 8, 10, 18, 19, 21, 27, 28, 29, 30, 32, 38, 39 };

    public sealed class Pair
    {
        public SemanticEvent spendMana, playHeroPower, buff;
        public EntityView power, hero;
        public int ownerIndex, ownerSeat;
    }

    public static float PlaybackAge(float age)
    {
        if(age<0||float.IsNaN(age)||float.IsInfinity(age))throw new ArgumentOutOfRangeException("age");
        return age*PlaybackSpeed;
    }

    [Serializable] sealed class SourcePath { public string pathid; }
    [Serializable] sealed class RawPrefab { public SourcePath prefab_source; }
    [Serializable] sealed class RawStart { public RawPrefab prefab; public SourcePath prefab_source; }
    [Serializable] sealed class RawImpact { public RawPrefab prefab; }
    [Serializable] sealed class RawCard { public string card_id, file, reference; public RawStart start; public RawImpact impact; }
    [Serializable] sealed class RawCatalog { public RawCard[] programs; }
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs; public NativeSuperSpellAsset[] prefabs; }

    // NativeParam.property_data recursively contains more NativeParam values.
    // H10 does not execute SetProperty, so parse its direct parameters through
    // this non-recursive view and leave the imported source file untouched.
    [Serializable] sealed class RuntimeProgram
    {
        public string reference, audio_file, event_audio_file;
        public NativeNode[] nodes;
        public NativeAnimation[] animations;
        public RuntimeMachine[] machines;
        public NativeCapture[] captures;
        public NativeOffscreenFXData[] offscreen_fx;
        public NativeTimeline[] timelines;
        public NativeSourceSpellState[] spell_states;
        public NativeAnimationEventReceiver[] animation_event_receivers;
        public NativeSourceSpellTransform[] spell_transforms;
        public string[] unknown_actions;
    }
    [Serializable] sealed class RuntimeMachine
    {
        public int owner;
        public bool owner_known;
        public string start;
        public RuntimeState[] states;
        public NativeTransition[] events;
        public NativeFloat[] floats;
        public NativeColor[] colors;
        public NativeVector[] vectors;
        public NativeTextureVariable[] textures;
    }
    [Serializable] sealed class RuntimeState
    {
        public string name;
        public RuntimeAction[] actions;
        public NativeTransition[] transitions;
    }
    [Serializable] sealed class RuntimeAction
    {
        public string name;
        public RuntimeParam[] @params;
    }
    [Serializable] sealed class RuntimeParam
    {
        public string name, text, variable, asset_type, asset_path, array_type;
        public NativeAssetSource asset_source;
        public bool object_null, none_known, is_none, wrap_known, integer_known, array_known, unresolved;
        public int integer, kind, node, pre_wrap, post_wrap, array_count;
        public float number;
        public Vector3 vector;
        public Color color;
        public NativeKey[] curve;
    }
    [Serializable] sealed class RuntimeParticleProgram { public RuntimeParticleNode[] nodes; }
    [Serializable] sealed class RuntimeParticleNode { public RuntimeParticle particle; }
    [Serializable] sealed class RuntimeParticle
    {
        public NativeParticleCollision collision;
        public bool empty_sub_emitters, restored, loop, awake, size3d, rotation3d;
        public int space, scaling, max_particles;
        public float duration, speed;
        public NativeCurve delay, life, velocity, size, size_y, size_z, rotation, rotation_x, rotation_y, gravity;
        public NativeGradient start_color, color;
        public NativeColorBySpeed color_speed;
        public NativeEmission emission;
        public NativeShape shape;
        public NativeModule[] modules;
        public NativeUV uv;
        public NativeParticleRender render;
        public string[] unsupported;
    }

    static bool SameEvent(SemanticEvent left, SemanticEvent right)
    {
        return left != null && right != null && left.type == right.type && left.card_id == right.card_id &&
            left.source == right.source && left.target == right.target && left.owner == right.owner &&
            Mathf.Approximately(left.amount, right.amount);
    }

    static bool HasNoRecursivePropertyData(string path)
    {
        return File.ReadAllText(path).IndexOf("\"property_data\"", StringComparison.Ordinal) < 0;
    }

    // JsonUtility materializes empty nested classes for JSON null fields, so keep explicit source presence separately.
    static int SkipJsonWhitespace(string json, int position)
    {
        while (position < json.Length && Char.IsWhiteSpace(json[position])) position++;
        return position;
    }

    static int SkipJsonString(string json, int start)
    {
        if (start >= json.Length || json[start] != '"') throw new InvalidDataException("Expected JSON string");
        for (int i = start + 1; i < json.Length; i++)
        {
            if (json[i] == '\\') { i++; continue; }
            if (json[i] == '"') return i + 1;
        }
        throw new InvalidDataException("Unterminated JSON string");
    }

    static int SkipJsonValue(string json, int start)
    {
        int position = SkipJsonWhitespace(json, start);
        if (position >= json.Length) throw new InvalidDataException("Missing JSON value");
        char first = json[position];
        if (first == '"') return SkipJsonString(json, position);
        if (first != '{' && first != '[')
        {
            int end = position;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']' && !Char.IsWhiteSpace(json[end])) end++;
            if (end == position) throw new InvalidDataException("Empty JSON value");
            return end;
        }

        var closers = new System.Collections.Generic.List<char> { first == '{' ? '}' : ']' };
        for (int i = position + 1; i < json.Length; i++)
        {
            char current = json[i];
            if (current == '"') { i = SkipJsonString(json, i) - 1; continue; }
            if (current == '{' || current == '[') closers.Add(current == '{' ? '}' : ']');
            else if (current == '}' || current == ']')
            {
                if (closers.Count == 0 || closers[closers.Count - 1] != current)
                    throw new InvalidDataException("Mismatched JSON container");
                closers.RemoveAt(closers.Count - 1);
                if (closers.Count == 0) return i + 1;
            }
        }
        throw new InvalidDataException("Unterminated JSON container");
    }

    static bool TryGetJsonPropertyRange(string json, int objectStart, string property, out int valueStart, out int valueEnd)
    {
        valueStart = -1; valueEnd = -1;
        int position = SkipJsonWhitespace(json, objectStart);
        if (position >= json.Length || json[position++] != '{') throw new InvalidDataException("Expected JSON object");
        while (true)
        {
            position = SkipJsonWhitespace(json, position);
            if (position >= json.Length) throw new InvalidDataException("Unterminated JSON object");
            if (json[position] == '}') return valueStart >= 0;
            int keyStart = position;
            int keyEnd = SkipJsonString(json, keyStart);
            string key = json.Substring(keyStart + 1, keyEnd - keyStart - 2);
            if (key.IndexOf('\\') >= 0) throw new InvalidDataException("Escaped H10 source object key is unsupported");
            position = SkipJsonWhitespace(json, keyEnd);
            if (position >= json.Length || json[position++] != ':') throw new InvalidDataException("Expected JSON property separator");
            int currentStart = SkipJsonWhitespace(json, position);
            int currentEnd = SkipJsonValue(json, currentStart);
            if (key == property)
            {
                if (valueStart >= 0) throw new InvalidDataException("Duplicate JSON property " + property);
                valueStart = currentStart; valueEnd = currentEnd;
            }
            position = SkipJsonWhitespace(json, currentEnd);
            if (position >= json.Length) throw new InvalidDataException("Unterminated JSON object");
            if (json[position] == ',') { position++; continue; }
            if (json[position] == '}') return valueStart >= 0;
            throw new InvalidDataException("Expected JSON object delimiter");
        }
    }

    static bool[] ReadParticlePresence(string json)
    {
        int rootStart = SkipJsonWhitespace(json, 0), nodesStart, nodesEnd;
        if (!TryGetJsonPropertyRange(json, rootStart, "nodes", out nodesStart, out nodesEnd) ||
            nodesStart >= json.Length || json[nodesStart] != '[')
            throw new InvalidDataException("HERO_10bp nodes array is missing");
        var presence = new System.Collections.Generic.List<bool>();
        int position = nodesStart + 1;
        while (true)
        {
            position = SkipJsonWhitespace(json, position);
            if (position >= nodesEnd) throw new InvalidDataException("Unterminated HERO_10bp nodes array");
            if (json[position] == ']')
            {
                if (position + 1 != nodesEnd) throw new InvalidDataException("Unexpected data after HERO_10bp nodes array");
                return presence.ToArray();
            }
            if (json[position] != '{') throw new InvalidDataException("HERO_10bp node is not an object");
            int nodeStart = position, particleStart, particleEnd;
            int nodeEnd = SkipJsonValue(json, nodeStart);
            if (!TryGetJsonPropertyRange(json, nodeStart, "particle", out particleStart, out particleEnd))
                throw new InvalidDataException("HERO_10bp node omits explicit particle presence");
            if (particleStart < json.Length && json[particleStart] == '{') presence.Add(true);
            else if (particleEnd - particleStart == 4 && String.CompareOrdinal(json, particleStart, "null", 0, 4) == 0) presence.Add(false);
            else throw new InvalidDataException("HERO_10bp particle is neither object nor null");
            position = SkipJsonWhitespace(json, nodeEnd);
            if (position >= nodesEnd) throw new InvalidDataException("Unterminated HERO_10bp nodes array");
            if (json[position] == ',') { position++; continue; }
            if (json[position] == ']')
            {
                if (position + 1 != nodesEnd) throw new InvalidDataException("Unexpected data after HERO_10bp nodes array");
                return presence.ToArray();
            }
            throw new InvalidDataException("Expected HERO_10bp node delimiter");
        }
    }

    public static NativeProgram ReadProgram(string path)
    {
        string json = File.ReadAllText(path);
        bool[] particlePresence = ReadParticlePresence(json);
        var view = JsonUtility.FromJson<RuntimeProgram>(json);
        if (view == null) throw new InvalidDataException("HERO_10bp runtime program is empty: " + path);
        var particleView = JsonUtility.FromJson<RuntimeParticleProgram>(json);
        if (particleView == null || view.nodes == null || particleView.nodes == null ||
            view.nodes.Length != particleView.nodes.Length || view.nodes.Length != particlePresence.Length || view.nodes.Any(node => node == null))
            throw new InvalidDataException("HERO_10bp node/particle arrays do not align: " + path);
        var result = new NativeProgram
        {
            reference = view.reference,
            audio_file = view.audio_file,
            event_audio_file = view.event_audio_file,
            nodes = view.nodes,
            animations = view.animations,
            machines = (view.machines ?? new RuntimeMachine[0]).Select(machine => new NativeMachine
            {
                owner = machine.owner,
                owner_known = machine.owner_known,
                start = machine.start,
                states = (machine.states ?? new RuntimeState[0]).Select(state => new NativeState
                {
                    name = state.name,
                    actions = (state.actions ?? new RuntimeAction[0]).Select(action => new NativeAction
                    {
                        name = action.name,
                        @params = (action.@params ?? new RuntimeParam[0]).Select(parameter => new NativeParam
                        {
                            name = parameter.name, text = parameter.text, variable = parameter.variable,
                            asset_type = parameter.asset_type, asset_path = parameter.asset_path,
                            array_type = parameter.array_type, asset_source = parameter.asset_source,
                            object_null = parameter.object_null, none_known = parameter.none_known,
                            is_none = parameter.is_none, wrap_known = parameter.wrap_known,
                            integer_known = parameter.integer_known, array_known = parameter.array_known,
                            unresolved = parameter.unresolved, integer = parameter.integer,
                            kind = parameter.kind, node = parameter.node, pre_wrap = parameter.pre_wrap,
                            post_wrap = parameter.post_wrap, array_count = parameter.array_count,
                            number = parameter.number, vector = parameter.vector, color = parameter.color,
                            curve = parameter.curve
                        }).ToArray()
                    }).ToArray(),
                    transitions = state.transitions
                }).ToArray(),
                events = machine.events,
                floats = machine.floats,
                colors = machine.colors,
                vectors = machine.vectors,
                textures = machine.textures
            }).ToArray(),
            captures = view.captures,
            offscreen_fx = view.offscreen_fx,
            timelines = view.timelines,
            spell_states = view.spell_states,
            animation_event_receivers = view.animation_event_receivers,
            spell_transforms = view.spell_transforms,
            unknown_actions = view.unknown_actions
        };
        for (int i = 0; i < result.nodes.Length; i++)
        {
            var source = particleView.nodes[i] == null ? null : particleView.nodes[i].particle;
            if (!particlePresence[i]) { result.nodes[i].particle = null; continue; }
            if (source == null) throw new InvalidDataException("HERO_10bp particle object was not mapped at node " + i);
            result.nodes[i].particle = new NativeParticle
            {
                collision = source.collision, empty_sub_emitters = source.empty_sub_emitters,
                restored = source.restored, loop = source.loop, awake = source.awake,
                size3d = source.size3d, rotation3d = source.rotation3d,
                space = source.space, scaling = source.scaling, max_particles = source.max_particles,
                duration = source.duration, speed = source.speed,
                delay = source.delay, life = source.life, velocity = source.velocity, size = source.size,
                size_y = source.size_y, size_z = source.size_z, rotation = source.rotation,
                rotation_x = source.rotation_x, rotation_y = source.rotation_y, gravity = source.gravity,
                start_color = source.start_color, color = source.color, color_speed = source.color_speed,
                emission = source.emission, shape = source.shape, modules = source.modules,
                uv = source.uv, render = source.render, unsupported = source.unsupported
            };
        }
        return result;
    }

    static bool FindEntities(StateView state, int entity, out int ownerIndex, out int ownerSeat,
        out EntityView power, out EntityView hero)
    {
        ownerIndex = -1; ownerSeat = -1; power = null; hero = null;
        if (state == null || state.players == null) return false;
        for (int i = 0; i < state.players.Length; i++)
        {
            var player = state.players[i];
            if (player == null || player.power == null || player.power.entity != entity) continue;
            if (ownerIndex >= 0) return false;
            ownerIndex = i; ownerSeat = player.seat; power = player.power; hero = player.hero;
        }
        return ownerIndex >= 0 && hero != null;
    }

    public static bool TryGetPair(SemanticEvent powerEvent, StateView before, StateView after, out Pair pair)
    {
        pair = null;
        if (powerEvent == null || powerEvent.type != "PlayHeroPower" || before == null || after == null ||
            before.match_id != after.match_id || after.version <= before.version || before.players == null || after.players == null)
            return false;
        var events = after.events ?? new SemanticEvent[0];
        if (events.Length != 3 || !events.Select(item => item == null ? "" : item.type)
                .SequenceEqual(new[] { "SpendMana", "PlayHeroPower", "Buff" })) return false;
        var spend = events[0]; var play = events[1]; var buff = events[2];
        if (!SameEvent(play, powerEvent) || play.target <= 0 || play.source <= 0 ||
            spend.source != play.target || spend.target != play.source || !Mathf.Approximately(spend.amount, 1) ||
            spend.owner < 0 || play.owner != spend.owner || buff.owner != spend.owner ||
            buff.source != play.target || buff.target <= 0) return false;

        int beforeOwner, beforeSeat, afterOwner, afterSeat;
        EntityView oldPower, oldHero, currentPower, currentHero;
        if (!FindEntities(before, play.target, out beforeOwner, out beforeSeat, out oldPower, out oldHero) ||
            !FindEntities(after, play.target, out afterOwner, out afterSeat, out currentPower, out currentHero) ||
            beforeOwner != afterOwner || beforeSeat != afterSeat || beforeOwner != spend.owner ||
            oldPower.card_id != HeroPowerId || currentPower.card_id != HeroPowerId ||
            oldHero.card_id != HeroId || currentHero.card_id != HeroId ||
            !String.IsNullOrEmpty(play.card_id) && play.card_id != HeroPowerId ||
            oldHero.entity != buff.target || currentHero.entity != buff.target ||
            oldHero.attack != 0 || currentHero.attack != 1 || !currentHero.attack_buffed ||
            oldHero.health != currentHero.health || buff.visual == null || buff.visual.entity != currentHero.entity ||
            buff.visual.card_id != HeroId || buff.visual.attack != 1 || !buff.visual.attack_buffed ||
            before.players[beforeOwner].mana - after.players[afterOwner].mana != 1 ||
            !currentPower.used) return false;

        pair = new Pair { spendMana = spend, playHeroPower = play, buff = buff, power = currentPower,
            hero = currentHero, ownerIndex = afterOwner, ownerSeat = afterSeat };
        return true;
    }

    public static bool TryGetPairForPowerCue(SemanticEvent cue, StateView before, StateView after, out Pair pair)
    {
        pair = null;
        if (cue == null || cue.type != "SpellEffect" || cue.card_id != HeroPowerId || cue.source <= 0 || cue.target <= 0)
            return false;
        var powers = (after == null ? new SemanticEvent[0] : after.events ?? new SemanticEvent[0])
            .Where(item => item != null && item.type == "PlayHeroPower" && item.target == cue.source).ToArray();
        if (powers.Length != 1 || !TryGetPair(powers[0], before, after, out pair) || pair.hero.entity != cue.target ||
            cue.owner >= 0 && cue.owner != pair.ownerIndex)
        { pair = null; return false; }
        return true;
    }

    public static bool SamePair(Pair left, Pair right)
    {
        return left != null && right != null && left.ownerIndex == right.ownerIndex && left.ownerSeat == right.ownerSeat &&
            SameEvent(left.spendMana, right.spendMana) && SameEvent(left.playHeroPower, right.playHeroPower) &&
            SameEvent(left.buff, right.buff) && left.hero.entity == right.hero.entity;
    }

    public static bool SupportsConfig(NativeSuperSpellConfig config)
    {
        return config != null && config.card_id == HeroPowerId && config.file == RootFile && config.reference == RootReference &&
            config.location == 2 && config.target_behavior == 0 && config.source_root_profile == RuntimeProfile &&
            config.start_enabled && !config.start_config_null && config.start != null && config.start.enabled &&
            !config.start.prefab_null && config.start.prefab_reference == StartReference && config.start.use_super_location &&
            config.start.death_after_missiles && !config.start.adjust_rotation && config.start.rotation == Vector3.zero &&
            config.missile != null && config.missile.m_Enabled == 0 && String.IsNullOrEmpty(config.missile_reference) &&
            config.missile.m_Prefab != null && config.missile.m_Prefab.m_FileID == 0 && config.missile.m_Prefab.m_PathID == 0 &&
            config.missile.m_ReversePrefab != null && config.missile.m_ReversePrefab.m_FileID == 0 && config.missile.m_ReversePrefab.m_PathID == 0 &&
            config.impact != null && config.impact.enabled && config.impact.prefab != null && !config.impact.prefab.prefab_null &&
            config.impact.prefab.prefab_reference == ImpactReference && config.impact.prefab.prefab_node == 12 &&
            config.impact.ranges != null && config.impact.ranges.Length == 0 && !config.impact.use_super_location &&
            config.impact.location == 6 && !config.impact.parent_to_location && config.impact.delay_min == 0 &&
            config.impact.delay_max == 0 && config.impact.spawn_offset == 0 &&
            Mathf.Approximately(config.impact.game_delay_min, .5f) && Mathf.Approximately(config.impact.game_delay_max, .5f) &&
            !config.impact.adjust_rotation && config.impact.rotation == Vector3.zero &&
            config.area != null && config.area.friendly != null && config.area.opponent != null &&
            config.area.friendly.enabled && config.area.friendly.prefab_null && String.IsNullOrEmpty(config.area.friendly.prefab_reference) &&
            config.area.opponent.enabled && config.area.opponent.prefab_null && String.IsNullOrEmpty(config.area.opponent.prefab_reference) &&
            config.chain_enabled && config.chain_prefab_null && String.IsNullOrEmpty(config.chain_reference) &&
            !config.chain_available && !config.reverse_available;
    }

    public static bool Supports(AssetRegistry assets, NativeEffectEntry entry)
    {
        try
        {
            if (assets == null || entry == null || entry.card_id != HeroPowerId || !entry.enabled ||
                !Mathf.Approximately(entry.duration, .1f) || entry.replace || entry.file != RootFile) return false;
            var rootPath = Path.Combine(assets.Root, RootFile);
            var catalogPath = Path.Combine(assets.Root, "native-super-spell.json");
            if (!File.Exists(rootPath) || !File.Exists(catalogPath) || !HasNoRecursivePropertyData(rootPath)) return false;
            var root = ReadProgram(rootPath);
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(catalogPath));
            var config = catalog.programs.Single(row => row.card_id == HeroPowerId && row.file == RootFile && row.reference == RootReference);
            if (!SupportsConfig(config) || !SupportsRootProgram(root)) return false;
            var rawCard = ReadRawCard(catalogPath, HeroPowerId);
            if (rawCard == null || rawCard.file != RootFile || rawCard.reference != RootReference ||
                rawCard.start == null || rawCard.start.prefab_source == null ||
                rawCard.start.prefab_source.pathid != "-7316641151859032915" || rawCard.impact == null ||
                rawCard.impact.prefab == null || rawCard.impact.prefab.prefab_source == null ||
                rawCard.impact.prefab.prefab_source.pathid != "-618183129397526064") return false;
            if (String.IsNullOrEmpty(root.audio_file) || !File.Exists(Path.Combine(assets.Root, root.audio_file))) return false;
            var startAsset = catalog.prefabs.Single(asset => asset.reference == StartReference && asset.file == StartFile);
            var impactAsset = catalog.prefabs.Single(asset => asset.reference == ImpactReference && asset.file == ImpactFile);
            return PrefabShape(assets, startAsset, 11, 3, 0, 1) && PrefabShape(assets, impactAsset, 30, 10, 3, 2);
        }
        catch { return false; }
    }

    static RawCard ReadRawCard(string path, string cardId)
    {
        var catalog = JsonUtility.FromJson<RawCatalog>(File.ReadAllText(path));
        return catalog.programs.SingleOrDefault(row => row.card_id == cardId);
    }

    public static bool SupportsRootProgram(NativeProgram root)
    {
        return String.IsNullOrEmpty(RootProgramFailure(root));
    }

    public static string ParticleProfileSummary(NativeProgram root)
    {
        if (root == null) return "program=null";
        var nodes = root.nodes ?? new NativeNode[0];
        int nullNodes = nodes.Count(node => node == null);
        var rows = nodes.Select((node, index) => new { node, index })
            .Where(row => row.node != null && row.node.particle != null)
            .Select(row => row.index + ":" + row.node.name + "=" + row.node.particle.restored).ToArray();
        return "nodes=" + (root.nodes == null ? "null" : root.nodes.Length.ToString()) +
            "; nullNodes=" + nullNodes + "; particleCount=" + rows.Length + "; particles=" + String.Join(",", rows);
    }

    public static string RootProgramFailure(NativeProgram root)
    {
        try
        {
            if (root == null) return "program is null";
            if (root.reference != RootReference) return "reference=" + root.reference;
            if (root.nodes == null || root.nodes.Length != 42) return "nodes length=" + (root.nodes == null ? "null" : root.nodes.Length.ToString());
            if (root.nodes[0] == null || root.nodes[0].parent != -1 || root.nodes[0].name != "ReuseFX_Fel_DemonHunter_BuffImpact_Small_Super_WithIdle" ||
                root.nodes[1] == null || root.nodes[1].parent != -1 || root.nodes[1].name != "ReuseFX_Idle_DemonHunter_Small" ||
                root.nodes[TargetRootNode] == null || root.nodes[TargetRootNode].parent != -1 ||
                root.nodes[TargetRootNode].name != "ReuseFX_BuffImpact_DemonHunter_Small")
                return "source/target layer roots changed";
            if (root.machines == null || root.machines.Length != 2) return "machines length=" + (root.machines == null ? "null" : root.machines.Length.ToString());
            if (root.animations == null || root.animations.Length != 3) return "animations length=" + (root.animations == null ? "null" : root.animations.Length.ToString());
            if (root.captures == null || root.captures.Length != 3) return "captures length=" + (root.captures == null ? "null" : root.captures.Length.ToString());
            if (root.unknown_actions == null || !root.unknown_actions.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(new[] {
                "AnimatorPlayAction", "AudioPlayClipAction", "AudioPlaythroughAction", "SetMaterialTexture", "SetScale" }))
                return "unknown_actions=" + (root.unknown_actions == null ? "null" : String.Join(",", root.unknown_actions));
            int nullNodeCount = root.nodes.Count(node => node == null);
            string particleSummary = ParticleProfileSummary(root);
            if (nullNodeCount != 0) return "node entries null=" + nullNodeCount + "; " + particleSummary;
            var particleRows = root.nodes.Select((node, index) => new { node, index })
                .Where(row => row.node.particle != null)
                .Select(row => row.index + ":" + row.node.name + "=" + row.node.particle.restored).ToArray();
            var particleIndices = root.nodes.Select((node, index) => new { node, index })
                .Where(row => row.node.particle != null).Select(row => row.index).ToArray();
            if (!particleIndices.SequenceEqual(ExpectedParticleNodes))
                return "node particle indices differ; expected=" + String.Join(",", ExpectedParticleNodes) + "; " + particleSummary;
            var unrestored = particleRows.Where(row => row.EndsWith("=False", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (unrestored.Length != 0)
                return "node particles not restored=" + String.Join(",", unrestored) + "; " + particleSummary;
            var source = root.machines.SingleOrDefault(machine => machine != null && machine.owner_known && machine.owner == 1);
            var target = root.machines.SingleOrDefault(machine => machine != null && machine.owner_known && machine.owner == 12);
            if (source == null) return "owner=1 machine missing or owner_known was not parsed";
            if (target == null) return "owner=12 machine missing or owner_known was not parsed";
            string failure = MachineFailure(source, 1, 10);
            if (!String.IsNullOrEmpty(failure)) return failure;
            failure = MachineFailure(target, 12, 7);
            if (!String.IsNullOrEmpty(failure)) return failure;
            if (!TextureParameterShape(target)) return TextureParameterFailure(target);
            if (!ScaleParameterShape(source, "Minion", new[] { 6, 10 },
                new[] { new Vector3(.7f, .85f, .85f), new Vector3(.65f, .75f, .75f) }))
                return ScaleParameterFailure(source, "Minion", new[] { 6, 10 },
                    new[] { new Vector3(.7f, .85f, .85f), new Vector3(.65f, .75f, .75f) });
            if (!ScaleParameterShape(source, "Weapon/HP", new[] { 6, 10 },
                new[] { new Vector3(.7f, .7f, .7f), new Vector3(.65f, .65f, .65f) }))
                return ScaleParameterFailure(source, "Weapon/HP", new[] { 6, 10 },
                    new[] { new Vector3(.7f, .7f, .7f), new Vector3(.65f, .65f, .65f) });
            if (!ScaleParameterShape(target, "Weapon/HP", new[] { 14 }, new[] { new Vector3(1.05f, 1.05f, .75f) }))
                return ScaleParameterFailure(target, "Weapon/HP", new[] { 14 }, new[] { new Vector3(1.05f, 1.05f, .75f) });
            return null;
        }
        catch (Exception error) { return "profile diagnostic exception: " + error.GetBaseException().Message; }
    }

    static bool DirectParams(NativeAction action, params string[] names)
    {
        return action != null && action.@params != null && !action.@params.Any(parameter => parameter == null) &&
            action.@params.Select(parameter => parameter.name).SequenceEqual(names) &&
            action.@params.All(parameter => !parameter.unresolved);
    }

    static NativeParam Param(NativeAction action, string name)
    {
        return action.@params.Single(parameter => parameter.name == name);
    }

    static bool TextureParameterShape(NativeMachine target)
    {
        return String.IsNullOrEmpty(TextureParameterFailure(target));
    }

    static string TextureParameterFailure(NativeMachine target)
    {
        var actions = target.states.Single(state => state.name == "Hero").actions
            .Where(action => action.name == "SetMaterialTexture").ToArray();
        int[] nodes = { 24, 25, 26 };
        string[] paths = { "cd7ba7885a2d12ab34d1dabf.png", "cd7ba7885a2d12ab34d1dabf.png", "db598f55f4e4c96b7e32d722.png" };
        string[] pathIds = { "-8039451771726812956", "-8039451771726812956", "3506897862555594978" };
        if (actions.Length != nodes.Length) return "owner12.Hero SetMaterialTexture count=" + actions.Length;
        for (int i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            string failure = TextureActionFailure(action, i, nodes[i], paths[i], pathIds[i]);
            if (!String.IsNullOrEmpty(failure)) return failure;
        }
        return null;
    }

    static string TextureActionFailure(NativeAction action, int indexInState, int node, string path, string pathId)
    {
            if (!DirectParams(action, "gameObject", "materialIndex", "material", "namedTexture", "texture"))
            {
                string names = action == null || action.@params == null ? "null" : String.Join(",", action.@params.Where(parameter => parameter != null).Select(parameter => parameter.name));
                return "owner12.Hero SetMaterialTexture[" + indexInState + "] params=" + names;
            }
            var gameObject = Param(action, "gameObject"); var index = Param(action, "materialIndex");
            var material = Param(action, "material"); var named = Param(action, "namedTexture"); var texture = Param(action, "texture");
            if (gameObject.kind != 20 || gameObject.node != node || !gameObject.none_known || gameObject.is_none ||
                !String.IsNullOrEmpty(gameObject.variable)) return "owner12.Hero SetMaterialTexture[" + indexInState + "] gameObject=" + gameObject.kind + "/" + gameObject.node;
            if (index.kind != 16 || !Mathf.Approximately(index.number, 0) || !String.IsNullOrEmpty(index.variable))
                return "owner12.Hero SetMaterialTexture[" + indexInState + "] materialIndex=" + index.kind + "/" + index.number;
            if (material.kind != 32 || !material.object_null || material.asset_type != "Material" || !String.IsNullOrEmpty(material.variable))
                return "owner12.Hero SetMaterialTexture[" + indexInState + "] material=" + material.kind + "/" + material.asset_type + "/null=" + material.object_null;
            if (named.kind != 18 || named.text != "_MainTex" || !String.IsNullOrEmpty(named.variable))
                return "owner12.Hero SetMaterialTexture[" + indexInState + "] namedTexture=" + named.kind + "/" + named.text;
            var source = texture.asset_source;
            if (texture.kind != 33 || texture.asset_type != "Texture" || texture.object_null ||
                !String.IsNullOrEmpty(texture.variable) || texture.asset_path != path || source == null ||
                source.type != "Texture2D" || source.pathid != pathId ||
                source.cab != "CAB-572cbaceb2612afe464bd92ebbf0736a" ||
                source.bundle != "essential_base_global-texture-2.unity3d")
                return "owner12.Hero SetMaterialTexture[" + indexInState + "] texture=" + texture.kind + "/" + texture.asset_type +
                    "/" + texture.asset_path + "/" + (source == null ? "null" : source.type + "/" + source.pathid + "/" + source.cab + "/" + source.bundle);
            return null;
    }

    static bool ScaleParameterShape(NativeMachine machine, string stateName, int[] nodes, Vector3[] scales)
    {
        return String.IsNullOrEmpty(ScaleParameterFailure(machine, stateName, nodes, scales));
    }

    static string ScaleParameterFailure(NativeMachine machine, string stateName, int[] nodes, Vector3[] scales)
    {
        var actions = machine.states.Single(state => state.name == stateName).actions
            .Where(action => action.name == "SetScale").ToArray();
        if (actions.Length != nodes.Length || scales.Length != nodes.Length)
            return "owner" + machine.owner + "." + stateName + " SetScale count=" + actions.Length;
        for (int i = 0; i < actions.Length; i++)
        {
            string failure = ScaleActionFailure(actions[i], machine.owner, stateName, i, nodes[i], scales[i]);
            if (!String.IsNullOrEmpty(failure)) return failure;
        }
        return null;
    }

    static string ScaleActionFailure(NativeAction action, int owner, string stateName, int index, int node, Vector3 scale)
    {
        string prefix = "owner" + owner + "." + stateName + " SetScale[" + index + "] ";
        if (!DirectParams(action, "gameObject", "vector", "x", "y", "z", "everyFrame", "lateUpdate"))
        {
            string names = action == null || action.@params == null ? "null" : String.Join(",", action.@params.Where(parameter => parameter != null).Select(parameter => parameter.name));
            return prefix + "params=" + names;
        }
        var gameObject = Param(action, "gameObject"); var vector = Param(action, "vector");
        var x = Param(action, "x"); var y = Param(action, "y"); var z = Param(action, "z");
        var everyFrame = Param(action, "everyFrame"); var lateUpdate = Param(action, "lateUpdate");
        if (gameObject.kind != 20 || gameObject.node != node || !gameObject.none_known || gameObject.is_none ||
            !String.IsNullOrEmpty(gameObject.variable)) return prefix + "gameObject=" + gameObject.kind + "/" + gameObject.node;
        if (vector.kind != 28 || !vector.none_known || !vector.is_none || !String.IsNullOrEmpty(vector.variable))
            return prefix + "vector=" + vector.kind + "/" + vector.none_known + "/" + vector.is_none;
        if (!ScaleComponent(x, scale.x) || !ScaleComponent(y, scale.y) || !ScaleComponent(z, scale.z))
            return prefix + "xyz=" + x.number + "," + y.number + "," + z.number + " expected=" + scale;
        if (everyFrame.kind != 1 || !Mathf.Approximately(everyFrame.number, 0) || !String.IsNullOrEmpty(everyFrame.variable) ||
            lateUpdate.kind != 1 || !Mathf.Approximately(lateUpdate.number, 0) || !String.IsNullOrEmpty(lateUpdate.variable))
            return prefix + "everyFrame/lateUpdate=" + everyFrame.kind + "/" + everyFrame.number + "/" + lateUpdate.kind + "/" + lateUpdate.number;
        return null;
    }

    static bool ScaleComponent(NativeParam parameter, float expected)
    {
        return parameter.kind == 15 && parameter.none_known && !parameter.is_none &&
            String.IsNullOrEmpty(parameter.variable) && Mathf.Approximately(parameter.number, expected);
    }

    static string MachineFailure(NativeMachine machine, int owner, int states)
    {
        if (machine == null) return "owner" + owner + " machine missing";
        if (!machine.owner_known || machine.owner != owner) return "owner machine identity not parsed: expected=" + owner + " actual=" + machine.owner + "/" + machine.owner_known;
        if (machine.start != "StartState") return "owner" + owner + " FSM start=" + machine.start;
        if (machine.states == null || machine.states.Length != states) return "owner" + owner + " states count=" + (machine.states == null ? "null" : machine.states.Length.ToString());
        if (machine.events == null) return "owner" + owner + " events=null";
        foreach (var state in machine.states)
        {
            if (state == null) return "owner" + owner + " contains null state";
            if (state.actions == null) return "owner" + owner + "." + state.name + " actions=null";
            if (state.transitions == null) return "owner" + owner + "." + state.name + " transitions=null";
            foreach (var action in state.actions)
            {
                if (action == null) return "owner" + owner + "." + state.name + " contains null action";
                if (action.@params == null) return "owner" + owner + "." + state.name + "." + action.name + " params=null";
                int badIndex = Array.FindIndex(action.@params, parameter => parameter == null || parameter.unresolved);
                if (badIndex >= 0) return "owner" + owner + "." + state.name + "." + action.name + " has null/unresolved param " +
                    (action.@params[badIndex] == null ? "<null>" : action.@params[badIndex].name);
            }
        }
        string expectedEvents = owner == 1 ? "Action>ActionState;Birth>Get Target;Cancel>Cancel;Death>Death;Idle>Idle;None>NoneState" : "Death>Get Target;None>NoneState";
        if (!Transitions(machine.events, expectedEvents)) return "owner" + owner + " event transitions differ: " +
            String.Join(";", machine.events.Select(row => row == null ? "null" : row.@event + ">" + row.state));
        string[][] shapeRows = owner == 1 ? new[] {
            new[] { "StartState", "", "" }, new[] { "NoneState", "SpellStateAction|ActivateGameObject", "" },
            new[] { "ActionState", "SpellStateAction|SpellFinishAction", "FINISHED>Death" },
            new[] { "Init FX", "SpellStateAction|ActivateGameObject|ParticlePlayAction|ParticlePlayAction|AudioPlayClipAction|Wait", "FINISHED>Idle" },
            new[] { "Death", "SpellStateAction|SpellFinishAction|ParticleStopAction|ParticleStopAction|AudioVolumeFadeAction|Wait", "FINISHED>NoneState" },
            new[] { "Cancel", "SpellStateAction|SpellFinishAction|ParticleStopAction|ParticleStopAction|AudioVolumeFadeAction|AudioPlayClipAction|Wait", "FINISHED>NoneState" },
            new[] { "Idle", "SpellStateAction|AudioVolumeFadeAction|AudioSetVolumeAction|AudioPlaythroughAction", "" },
            new[] { "Get Target", "SpellStateAction|SpellCardTypeAction", "Hero>Init FX;Minion>Minion;Weapon>Weapon/HP" },
            new[] { "Minion", "SetScale|SetScale", "FINISHED>Init FX" }, new[] { "Weapon/HP", "SetScale|SetScale", "FINISHED>Init FX" },
        } : new[] {
            new[] { "StartState", "", "" }, new[] { "Spell Finish", "SpellFinishAction|AudioPlayClipAction|Wait", "FINISHED>NoneState" },
            new[] { "NoneState", "SpellStateAction", "" },
            new[] { "Get Target", "SpellStateAction|SpellCardTypeAction", "Hero>Hero;Minion>Minion;Weapon>Weapon/HP" },
            new[] { "Minion", "SetVisibilityRecursiveAction|AnimatorPlayAction|ActivateGameObject|ParticlePlayAction", "FINISHED>Spell Finish" },
            new[] { "Hero", "SetVisibilityRecursiveAction|SetMaterialTexture|SetMaterialTexture|SetMaterialTexture|AnimatorPlayAction|ActivateGameObject|ParticlePlayAction", "FINISHED>Spell Finish" },
            new[] { "Weapon/HP", "SetScale|SetVisibilityRecursiveAction|AnimatorPlayAction|ActivateGameObject|ParticlePlayAction", "FINISHED>Spell Finish" },
        };
        foreach (var shape in shapeRows)
            if (!StateShape(machine, shape[0], shape[1], shape[2]))
            {
                var actual = machine.states.SingleOrDefault(state => state.name == shape[0]);
                return "owner" + owner + "." + shape[0] + " FSM mismatch; actions=" +
                    (actual == null || actual.actions == null ? "null" : String.Join("|", actual.actions.Select(action => action.name))) +
                    " transitions=" + (actual == null || actual.transitions == null ? "null" : String.Join("|", actual.transitions.Select(row => row.@event + ">" + row.state)));
            }
        return null;
    }

    static bool StateShape(NativeMachine machine, string name, string actions, string transitions)
    {
        var state = machine.states.SingleOrDefault(row => row.name == name);
        var expectedActions = String.IsNullOrEmpty(actions) ? new string[0] : actions.Split('|');
        return state != null && state.actions.Select(action => action.name).SequenceEqual(expectedActions) &&
            Transitions(state.transitions, transitions);
    }

    static bool Transitions(NativeTransition[] actual, string expected)
    {
        var expectedRows = String.IsNullOrEmpty(expected) ? new string[0] : expected.Split(';');
        return actual != null && actual.Select(row => row.@event + ">" + row.state).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(expectedRows.OrderBy(value => value, StringComparer.Ordinal));
    }

    static bool PrefabShape(AssetRegistry assets, NativeSuperSpellAsset asset, int nodes, int particles, int animations, int captures)
    {
        string path = Path.Combine(assets.Root, asset.file);
        if (!File.Exists(path) || !HasNoRecursivePropertyData(path)) return false;
        var program = ReadProgram(path);
        string audioFile = asset.file.Replace(".hero-power-prefab.json", ".hero-power-audio.json");
        var audioPath = Path.Combine(assets.Root, audioFile);
        return program != null && program.reference == asset.reference && program.nodes != null && program.nodes.Length == nodes &&
            program.nodes.Count(node => node.particle != null) == particles && program.animations != null &&
            program.animations.Length == animations && program.captures != null && program.captures.Length == captures &&
            File.Exists(audioPath) && (String.IsNullOrEmpty(program.audio_file) || program.audio_file == audioFile);
    }
}
