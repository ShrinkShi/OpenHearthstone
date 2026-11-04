using System;
using System.Collections.Generic;

[Serializable] public sealed class NativeActorSpellEntry
{public int index,type;public string reference,file,error;public NativeAssetSource inline_source;}
[Serializable] public sealed class NativeActorSpellDefinition
{public string reference;public NativeActorSpellEntry[] entries;}
[Serializable] public sealed class NativeActorSpellCatalog
{public NativeActorSpellDefinition[] tables;public bool complete;}

// Original SpellTable lookup/cache behavior. Factories own SpellManager creation,
// SetSpellType and attachment; this does not claim a complete SpellController.
public sealed class NativeActorSpellTable<T> where T:class
{
    readonly Dictionary<int,NativeActorSpellEntry> entries=new Dictionary<int,NativeActorSpellEntry>();
    readonly Dictionary<int,T> loaded=new Dictionary<int,T>();
    readonly Func<NativeActorSpellEntry,T> create;
    readonly Action<T,int> setType;
    readonly Action<T> attachLocal,release;
    readonly Func<T,bool> alive;
    readonly Action<T> show,hide;
    public int DuplicateEntries{get;private set;}
    public NativeActorSpellTable(NativeActorSpellDefinition definition,Func<NativeActorSpellEntry,T> factory,Action<T,int> setSpellType,Action<T> attach,Action<T> releaseSpell,Func<T,bool> isAlive=null,Action<T> showSpell=null,Action<T> hideSpell=null)
    {
        create=factory;setType=setSpellType;attachLocal=attach;release=releaseSpell;alive=isAlive??(value=>value!=null);
        show=showSpell;hide=hideSpell;
        foreach(var entry in definition.entries??new NativeActorSpellEntry[0])
        {if(entries.ContainsKey(entry.type))DuplicateEntries++;else entries.Add(entry.type,entry);}
    }
    public NativeActorSpellEntry Entry(int type){NativeActorSpellEntry entry;return entries.TryGetValue(type,out entry)?entry:null;}
    public T GetLoaded(int type){T value;return loaded.TryGetValue(type,out value)&&alive(value)?value:null;}
    public T GetSpell(int type,bool local)
    {
        var entry=Entry(type);if(entry==null)return null;
        var previous=GetLoaded(type);if(local&&previous!=null)return previous;
        // JsonUtility materializes JSON null nested objects as empty instances.
        if(entry.inline_source!=null&&!String.IsNullOrEmpty(entry.inline_source.pathid)||String.IsNullOrEmpty(entry.reference)||!String.IsNullOrEmpty(entry.error))return null;
        var value=create(entry);if(value==null||!alive(value))return null;
        setType(value,type);
        if(local){loaded[type]=value;attachLocal(value);}
        return value;
    }
    public void ReleaseAll()
    {
        foreach(var value in loaded.Values)if(alive(value))release(value);
        loaded.Clear();
    }
    public void Show()
    {
        if(show==null)throw new InvalidOperationException("Original Spell.Show callback is not bound");
        foreach(var pair in loaded)if(pair.Key!=0&&alive(pair.Value))show(pair.Value);
    }
    public void Hide()
    {
        if(hide==null)throw new InvalidOperationException("Original Spell.Hide callback is not bound");
        foreach(var value in loaded.Values)if(alive(value))hide(value);
    }
}
