using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class CardEffect
{
    public string card_id,start,cast,hit,projectile,impact,kind;
    public float cast_duration;
    public CardEffectSprite[] visuals;
}
[Serializable] public sealed class CardEffectSprite
{public string texture;public Color tint=Color.white,background;public bool soft_mask,key_background;public OriginalParticleTracks native;
 public bool HasNative{get{return native!=null&&native.restored;}}}
[Serializable] public sealed class CardEffectIndex {public CardEffect[] entries;}

// 数据来自本机 CardDef 的效果引用；播放节奏由客户端表现队列决定。
public static class CardEffectRegistry
{
    static readonly Dictionary<string,CardEffect> effects=new Dictionary<string,CardEffect>();
    static readonly Dictionary<string,CardEffectSprite> sprites=new Dictionary<string,CardEffectSprite>();
    public static void Load(string folder)
    {
        effects.Clear();sprites.Clear();string path=Path.Combine(folder,"effect-index.json");
        if(File.Exists(path))foreach(var entry in JsonUtility.FromJson<CardEffectIndex>(File.ReadAllText(path)).entries)
        {effects[entry.card_id]=entry;foreach(var sprite in entry.visuals??new CardEffectSprite[0])sprites[sprite.texture]=sprite;}
    }
    public static CardEffect Find(string cardId){CardEffect effect;return effects.TryGetValue(cardId??"",out effect)?effect:null;}
    public static CardEffectSprite Sprite(string id){CardEffectSprite sprite;return sprites.TryGetValue(id??"",out sprite)?sprite:null;}
    public static IEnumerable<string> AudioIds{get{return effects.Values.SelectMany(e=>new[]{e.start,e.cast,e.hit}).Where(s=>!String.IsNullOrEmpty(s)).Distinct();}}
}
