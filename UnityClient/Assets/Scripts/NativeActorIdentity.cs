using System;
using UnityEngine;

// Source Actor object references, separate from the existing display-only adapter.
public sealed class NativeActorIdentity : MonoBehaviour
{
    public GameObject RootObject{get;private set;}
    public MeshRenderer MeshRenderer{get;private set;}
    public MeshRenderer PortraitRenderer{get;private set;}
    public string SpellTable{get;private set;}
    public NativeActorSpellDefinition SpellDefinition{get;internal set;}
    public NativeProgram AnimationProgram{get;internal set;}
    public int PremiumType{get;set;}
    public bool IsDiamondViewer{get;set;}
    bool hasDefinition,hasEntity;
    int definitionType,definitionElite,definitionMercenary,entityType,entityElite,entityMercenary;
    public void ConfigureDefinitionTags(int type,int elite,int mercenary=0)
    {hasDefinition=true;definitionType=type;definitionElite=elite;definitionMercenary=mercenary;}
    public void ConfigureEntityTags(int type,int elite,int mercenary=0)
    {hasEntity=true;entityType=type;entityElite=elite;entityMercenary=mercenary;}
    // Source Actor prefers EntityDef tags, excludes Lettuce, then falls back
    // to Entity tags and finally the separate diamond-viewer flag.
    public bool IsElite()
    {
        int type=hasDefinition?definitionType:hasEntity?entityType:0;
        int mercenary=hasDefinition?definitionMercenary:hasEntity?entityMercenary:0;
        if(type==23||mercenary>0)return false;
        if(hasDefinition)return definitionElite>0;
        if(hasEntity)return entityElite>0;
        return IsDiamondViewer;
    }
    Texture normalPortrait,diamondPortrait;
    public bool HasCardDef{get;private set;}
    public void ConfigurePortrait(Texture normal,Texture diamond=null)
    {HasCardDef=true;normalPortrait=normal;diamondPortrait=diamond;}
    // Source action forces every premium except Diamond (3) to the normal static portrait.
    public Texture GetStaticPortraitTexture(){return PremiumType==3?diamondPortrait:normalPortrait;}
    public int PortraitMaterialIndex{get;private set;}
    public void Configure(GameObject portrait,string spellTable,int portraitMaterialIndex=0)
    {
        RootObject=FindChildBySubstring(gameObject,"RootObject");
        foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            if(String.Equals(renderer.gameObject.name,"Mesh",StringComparison.OrdinalIgnoreCase)){MeshRenderer=renderer;break;}
        PortraitRenderer=portrait==null?null:portrait.GetComponent<MeshRenderer>();SpellTable=spellTable;PortraitMaterialIndex=portraitMaterialIndex;
    }
    public MeshRenderer GetMeshRenderer(bool portrait){return PremiumType==2&&portrait?PortraitRenderer:MeshRenderer;}
    public static GameObject FindChildBySubstring(GameObject owner,string text)
    {
        if(owner.name.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)return owner;
        foreach(Transform child in owner.transform){var found=FindChildBySubstring(child.gameObject,text);if(found!=null)return found;}
        return null;
    }
    public static NativeActorIdentity FindInThisOrParents(GameObject owner)
    {
        if(owner==null)return null;
        for(var t=owner.transform;t!=null;t=t.parent){var actor=t.GetComponent<NativeActorIdentity>();if(actor!=null)return actor;}
        return null;
    }
}
