using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class HeroSpeechChecks
{
    [Serializable] sealed class HeroAttackManifest
    {public HeroAttackHero[] heroes;public HeroAttackVoice[] voices;}
    [Serializable] sealed class HeroAttackHero
    {public int class_id;public string card_id;}
    [Serializable] sealed class HeroAttackVoice
    {public string id,card_id,@event,asset_id,bundle,cab,pathid,file,sha256;}
    [Serializable] sealed class HeroAnnouncerManifest
    {public HeroAnnouncerHero[] heroes;public HeroAnnouncerVoice[] voices;public HeroAnnouncerVersus versus;}
    [Serializable] sealed class HeroAnnouncerHero
    {public int class_id;public string card_id;public HeroAnnouncerLine[] lines;}
    [Serializable] sealed class HeroAnnouncerLine
    {public string @event,field,reference;public HeroAnnouncerVoice[] voices;}
    [Serializable] sealed class HeroAnnouncerVersus
    {public string prefab_reference,asset_name;public string[] call_order;public HeroAnnouncerVoice[] voices;public HeroAnnouncerPrefab prefab;}
    [Serializable] sealed class HeroAnnouncerPrefab
    {
        public string asset_id,name,bundle,cab,pathid,type,guid,mapping_status,selection_strategy;
        public HeroAnnouncerSource[] sound_defs;
        public HeroAnnouncerCandidate[] clip_candidates,runtime_candidates;
        public HeroAnnouncerCandidate fallback_candidate;
        public HeroAnnouncerSelectionContract selection_contract;
    }
    [Serializable] sealed class HeroAnnouncerSource
    {public string name,bundle,cab,pathid,type,asset_id;}
    [Serializable] sealed class HeroAnnouncerCandidate
    {
        public string selection_role,sound_field,sound_reference,clip_locale,resolved_bundle,asset_id,runtime_id,file,sha256;
        public int candidate_index;
        public float weight,duration;
        public HeroAnnouncerSource sound_def,clip_source;
    }
    [Serializable] sealed class HeroAnnouncerSelectionContract
    {public string selector,caller,random_field,weight_field,selection,fallback_field,fallback_when;public string[] il_sources;}
    [Serializable] sealed class HeroAnnouncerVoice
    {public string id,card_id,@event,asset_id,bundle,cab,pathid,file,sha256,fallback_id,selection_strategy;public VoiceCandidate[] candidates;}
    [Serializable] sealed class PresentationProvenance
    {public PresentationAsset[] assets;}
    [Serializable] sealed class PresentationTextureSettings
    {public int m_FilterMode,m_Aniso,m_WrapU,m_WrapV,m_WrapW;public float m_MipBias;}
    [Serializable] sealed class PresentationAsset
    {public string id,asset_id,bundle,cab,pathid,usage,type,cache,derived_from,sha256,mesh_id,main_texture_id,parent_transform_id,projection;public string[] guids,material_ids,source_assets,transform_ids;public float[] local_position,local_rotation,local_scale;public int source_triangle_count,vertex_count,triangle_count;public PresentationTextureSettings texture_settings;}

    static readonly string[] Emotes={"GREETINGS","WELL_PLAYED","OOPS","THREATEN","THANKS","SORRY","CONCEDE","START","WOW"};
    static readonly string[] Menu={"THANKS","WELL_PLAYED","GREETINGS","WOW","OOPS","THREATEN","SORRY"};

    public static void Run()
    {
        string local=Path.GetFullPath("../local"),runtime=Path.Combine(local,"runtime");
        string indexPath=Path.Combine(runtime,"hero-speech-index.json");
        if(!File.Exists(indexPath))throw new FileNotFoundException("Export hero speech before running checks",indexPath);
        var index=JsonUtility.FromJson<HeroSpeechIndex>(File.ReadAllText(indexPath));
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText(Path.Combine(local,"card-database.json")));
        var heroes=(database.classes??new ClassDef[0]).Select(c=>c.hero).Where(id=>!String.IsNullOrEmpty(id)).Distinct().OrderBy(id=>id).ToArray();
        Require(heroes.Length==11,"expected 11 base heroes, got "+heroes.Length);
        Require(index.entries!=null&&index.entries.Length==99,"expected 99 localized hero entries");
        Require(index.labels!=null&&Menu.All(id=>index.labels.Any(row=>row.id==id&&!String.IsNullOrWhiteSpace(row.text))),"one or more original emote menu labels are missing");

        var registry=JsonUtility.FromJson<AssetList>(File.ReadAllText(Path.Combine(runtime,"registry.json")));
        foreach(string hero in heroes)
        foreach(string emote in Emotes)
        {
            var entry=index.entries.SingleOrDefault(row=>row.card_id==hero&&row.emote_id==emote);
            Require(entry!=null,"missing "+hero+"/"+emote);
            Require(!String.IsNullOrWhiteSpace(entry.text),"missing original subtitle for "+hero+"/"+emote);
            Require(entry.candidates!=null&&entry.candidates.Length>0,"missing original AudioClip candidate for "+hero+"/"+emote);
            foreach(var candidate in entry.candidates)
            {
                var asset=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id==candidate.id);
                Require(asset!=null&&File.Exists(Path.Combine(runtime,asset.path)),"missing exported clip "+candidate.id);
            }
        }
        string voiceIndexPath=Path.Combine(runtime,"voice-index.json");
        string attackManifestPath=Path.Combine(Directory.GetParent(local).FullName,"manifests","original-hero-attack-voices.json");
        Require(File.Exists(voiceIndexPath),"export original base-hero attack voices before running checks");
        Require(File.Exists(attackManifestPath),"export the original base-hero attack source manifest before running checks");
        var voiceIndex=JsonUtility.FromJson<VoiceIndex>(File.ReadAllText(voiceIndexPath));
        var attackManifest=JsonUtility.FromJson<HeroAttackManifest>(File.ReadAllText(attackManifestPath));
        Require(attackManifest.heroes!=null&&attackManifest.heroes.Select(row=>row.card_id).Distinct().Count()==heroes.Length,
            "hero attack source manifest does not cover all 11 base heroes");
        Require(attackManifest.voices!=null&&attackManifest.voices.Length>0,"hero attack source manifest has no original clips");
        foreach(string hero in heroes)
        {
            var voices=(voiceIndex.entries??new VoiceEntry[0]).Where(row=>row.card_id==hero&&row.@event=="ATTACK").ToArray();
            var sources=(attackManifest.voices??new HeroAttackVoice[0]).Where(row=>row.card_id==hero&&row.@event=="ATTACK").ToArray();
            Require(voices.Length>0&&sources.Length>0,"missing original Attack clip for "+hero);
            foreach(var voice in voices)
            {
                var asset=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id==voice.id);
                Require(asset!=null&&File.Exists(Path.Combine(runtime,asset.path)),"missing registered hero attack clip "+voice.id);
            }
        }

        string announcerManifestPath=Path.Combine(Directory.GetParent(local).FullName,"manifests","original-hero-announcer-opening.json");
        Require(File.Exists(announcerManifestPath),"export original hero announcer and versus source manifest before running checks");
        var announcerManifest=JsonUtility.FromJson<HeroAnnouncerManifest>(File.ReadAllText(announcerManifestPath));
        Require(announcerManifest.heroes!=null&&announcerManifest.heroes.Select(row=>row.card_id).Distinct().Count()==heroes.Length,
            "announcer source manifest does not cover all 11 base heroes");
        foreach(string hero in heroes)
        foreach(string eventName in new[]{"ANNOUNCER_BEFORE_VERSUS","ANNOUNCER_AFTER_VERSUS"})
        {
            var voiceRows=(voiceIndex.entries??new VoiceEntry[0]).Where(row=>row.card_id==hero&&row.@event==eventName).ToArray();
            var sources=(announcerManifest.voices??new HeroAnnouncerVoice[0]).Where(row=>row.card_id==hero&&row.@event==eventName).ToArray();
            Require(voiceRows.Length>0&&sources.Length>0,"missing original "+eventName+" voice for "+hero);
            foreach(var voice in voiceRows)
            {
                var asset=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id==voice.id);
                Require(asset!=null&&File.Exists(Path.Combine(runtime,asset.path)),"missing registered announcer clip "+voice.id);
            }
        }
        Require(announcerManifest.versus!=null&&announcerManifest.versus.call_order!=null&&announcerManifest.versus.asset_name=="VO_ANNOUNCER_VERSUS_21"&&
            announcerManifest.versus.call_order.SequenceEqual(new[]{"ANNOUNCER_BEFORE_VERSUS","VERSUS","ANNOUNCER_AFTER_VERSUS"}),
            "original announcer source order is missing or changed");
        Require(announcerManifest.versus.prefab!=null&&announcerManifest.versus.prefab.guid=="acc34acb15f07ff4ba08025a57a9a458"&&
            !String.IsNullOrWhiteSpace(announcerManifest.versus.prefab.cab)&&!String.IsNullOrWhiteSpace(announcerManifest.versus.prefab.pathid),
            "original versus prefab CAB/PathID provenance is missing");
        var versusVoices=(voiceIndex.entries??new VoiceEntry[0]).Where(row=>row.card_id=="GLOBAL"&&row.@event=="VERSUS").ToArray();
        Require(versusVoices.Length==1&&announcerManifest.versus.voices!=null&&announcerManifest.versus.voices.Length==1,
            "expected one GLOBAL:VERSUS event with a weighted candidate list");
        var versusVoice=versusVoices[0];
        var versusSource=announcerManifest.versus.prefab;
        Require(versusSource.mapping_status=="weighted_random_sounddef_candidates"&&
            versusSource.selection_strategy=="weighted_random_clips_then_m_AudioClip_fallback"&&
            versusSource.sound_defs!=null&&versusSource.sound_defs.Length==1,
            "versus SoundDef mapping or original weighted selection strategy is unresolved");
        Require(versusSource.selection_contract!=null&&
            versusSource.selection_contract.selector=="SoundUtils.GetRandomClipFromDef(SoundDef)"&&
            versusSource.selection_contract.random_field=="SoundDef.m_RandomClips"&&
            versusSource.selection_contract.weight_field=="RandomAudioClip.m_Weight"&&
            versusSource.selection_contract.fallback_field=="SoundDef.m_AudioClip"&&
            versusSource.selection_contract.selection=="UnityEngine.Random.Range(0, sum(weights)); cumulative-weight first match; last item fallback",
            "original SoundDef weighted random/fallback IL contract is missing");
        var randomCandidates=(versusSource.clip_candidates??new HeroAnnouncerCandidate[0])
            .Where(candidate=>candidate.selection_role=="weighted_random").OrderBy(candidate=>candidate.candidate_index).ToArray();
        var runtimeCandidates=versusVoice.candidates??new VoiceCandidate[0];
        Require(randomCandidates.Length==3&&runtimeCandidates.Length==randomCandidates.Length&&
            randomCandidates.Select((candidate,index)=>candidate!=null&&index==candidate.candidate_index&&
                !String.IsNullOrWhiteSpace(candidate.runtime_id)&&candidate.weight>=0&&
                !Single.IsNaN(candidate.weight)&&!Single.IsInfinity(candidate.weight)&&
                candidate.clip_source!=null&&candidate.clip_source.type=="AudioClip"&&
                candidate.sound_def!=null&&candidate.sound_def.type=="MonoBehaviour"&&
                runtimeCandidates[index]!=null&&runtimeCandidates[index].id==candidate.runtime_id&&
                runtimeCandidates[index].weight==candidate.weight).All(valid=>valid),
            "all original weighted random candidates must be registered with their source weights");
        var fallbackCandidate=versusSource.fallback_candidate;
        Require(fallbackCandidate!=null&&fallbackCandidate.selection_role=="fallback"&&
            fallbackCandidate.sound_field=="m_AudioClip"&&fallbackCandidate.clip_source!=null&&fallbackCandidate.clip_source.type=="AudioClip"&&
            fallbackCandidate.runtime_id==versusVoice.fallback_id,
            "SoundDef m_AudioClip fallback mapping is missing");
        foreach(var candidate in runtimeCandidates.Concat(new[]{new VoiceCandidate{id=versusVoice.fallback_id,weight=0}}))
        {
            var asset=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id==candidate.id);
            Require(asset!=null&&File.Exists(Path.Combine(runtime,asset.path)),"missing registered versus clip "+candidate.id);
        }
        foreach(var voice in versusVoices)
        {
            var asset=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id==voice.id);
            Require(asset!=null&&File.Exists(Path.Combine(runtime,asset.path)),"missing registered versus clip "+voice.id);
        }
        var versusLetters=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id=="UI_VS_LETTERS");
        Require(versusLetters!=null&&File.Exists(Path.Combine(runtime,versusLetters.path)),"geometry-projected original VS logo is not exported");
        var versusAtlas=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id=="UI_VS_LETTERS_ATLAS");
        Require(versusAtlas!=null&&File.Exists(Path.Combine(runtime,versusAtlas.path)),"original VS_letters atlas is not preserved");
        string presentationProvenancePath=Path.Combine(runtime,"presentation-provenance.json");
        Require(File.Exists(presentationProvenancePath),"presentation asset provenance is missing");
        var presentation=JsonUtility.FromJson<PresentationProvenance>(File.ReadAllText(presentationProvenancePath));
        Require((presentation.assets??new PresentationAsset[0]).Any(row=>row.id=="SOURCE_GAMESTART_VS_LETTERS"&&row.usage=="source_only"&&
            (row.guids??new string[0]).Contains("3cb2cbed6d44a694eb23fb8791684003")),
            "GameStart_VS_Letters source prefab provenance is missing");
        Require((presentation.assets??new PresentationAsset[0]).Any(row=>row.id=="UI_VS_LETTERS_ATLAS"&&row.usage=="inspected_only"&&
            row.cache==versusAtlas.path&&
            !String.IsNullOrWhiteSpace(row.cab)&&!String.IsNullOrWhiteSpace(row.pathid)&&
            row.texture_settings!=null&&row.texture_settings.m_FilterMode==1&&row.texture_settings.m_Aniso==1&&
            row.texture_settings.m_MipBias==0&&row.texture_settings.m_WrapU==0&&row.texture_settings.m_WrapV==0&&row.texture_settings.m_WrapW==0),
            "original VS_letters Texture2D source identity is missing");
        Require((presentation.assets??new PresentationAsset[0]).Any(row=>row.id=="UI_VS_LETTERS"&&row.usage=="runtime"&&
            row.derived_from=="UI_VS_LETTERS_ATLAS"&&row.cache==versusLetters.path&&!String.IsNullOrWhiteSpace(row.sha256)&&
            row.texture_settings!=null&&row.texture_settings.m_FilterMode==1&&row.texture_settings.m_Aniso==1&&
            row.texture_settings.m_MipBias==0&&row.texture_settings.m_WrapU==0&&row.texture_settings.m_WrapV==0&&row.texture_settings.m_WrapW==0),
            "derived VS logo is not tied to the original atlas and cached output");
        var vsSources=presentation.assets??new PresentationAsset[0];
        string[] vsSourceIds={"SOURCE_VS_LETTER_V_MESH","SOURCE_VS_LETTER_S_MESH",
            "SOURCE_VS_LETTER_V_MESH_FILTER","SOURCE_VS_LETTER_S_MESH_FILTER",
            "SOURCE_VS_LETTER_V_MESH_RENDERER","SOURCE_VS_LETTER_S_MESH_RENDERER","SOURCE_VS_LETTERS_MATERIAL",
            "SOURCE_VS_LETTER_V_TRANSFORM","SOURCE_VS_LETTER_S_TRANSFORM",
            "SOURCE_VS_LETTERS_GROUP_TRANSFORM","SOURCE_GAMESTART_VS_LETTERS_TRANSFORM"};
        Require(vsSourceIds.All(id=>vsSources.Any(row=>row.id==id&&row.usage=="source_only"&&
            !String.IsNullOrWhiteSpace(row.cab)&&!String.IsNullOrWhiteSpace(row.pathid))),
            "VS source MeshFilter/Material identities are missing");
        Require(vsSources.Any(row=>row.id=="SOURCE_VS_LETTER_V_MESH_FILTER"&&row.mesh_id=="SOURCE_VS_LETTER_V_MESH")&&
            vsSources.Any(row=>row.id=="SOURCE_VS_LETTER_S_MESH_FILTER"&&row.mesh_id=="SOURCE_VS_LETTER_S_MESH")&&
            vsSources.Any(row=>row.id=="SOURCE_VS_LETTER_V_MESH_RENDERER"&&
                (row.material_ids??new string[0]).Contains("SOURCE_VS_LETTERS_MATERIAL"))&&
            vsSources.Any(row=>row.id=="SOURCE_VS_LETTER_S_MESH_RENDERER"&&
                (row.material_ids??new string[0]).Contains("SOURCE_VS_LETTERS_MATERIAL"))&&
            vsSources.Any(row=>row.id=="SOURCE_VS_LETTERS_MATERIAL"&&row.main_texture_id=="UI_VS_LETTERS_ATLAS"),
            "VS source prefab mesh/material/texture reference edges are incomplete");
        var vsVTransform=vsSources.SingleOrDefault(row=>row.id=="SOURCE_VS_LETTER_V_TRANSFORM");
        var vsSTransform=vsSources.SingleOrDefault(row=>row.id=="SOURCE_VS_LETTER_S_TRANSFORM");
        var vsGroupTransform=vsSources.SingleOrDefault(row=>row.id=="SOURCE_VS_LETTERS_GROUP_TRANSFORM");
        var vsRootTransform=vsSources.SingleOrDefault(row=>row.id=="SOURCE_GAMESTART_VS_LETTERS_TRANSFORM");
        var vsVMesh=vsSources.SingleOrDefault(row=>row.id=="SOURCE_VS_LETTER_V_MESH");
        var vsSMesh=vsSources.SingleOrDefault(row=>row.id=="SOURCE_VS_LETTER_S_MESH");
        Require(vsVTransform!=null&&vsSTransform!=null&&vsGroupTransform!=null&&vsRootTransform!=null&&
            vsVMesh!=null&&vsSMesh!=null&&
            vsVTransform.parent_transform_id==vsGroupTransform.id&&vsSTransform.parent_transform_id==vsGroupTransform.id&&
            vsGroupTransform.parent_transform_id==vsRootTransform.id&&String.IsNullOrEmpty(vsRootTransform.parent_transform_id)&&
            TransformDataValid(vsVTransform)&&TransformDataValid(vsSTransform)&&TransformDataValid(vsGroupTransform)&&TransformDataValid(vsRootTransform)&&
            (vsVMesh.transform_ids??new string[0]).SequenceEqual(new[]{vsVTransform.id,vsGroupTransform.id,vsRootTransform.id})&&
            (vsSMesh.transform_ids??new string[0]).SequenceEqual(new[]{vsSTransform.id,vsGroupTransform.id,vsRootTransform.id})&&
            vsVMesh.vertex_count==347&&vsVMesh.triangle_count==416&&vsSMesh.vertex_count==728&&vsSMesh.triangle_count==956&&
            VectorNear(vsGroupTransform.local_rotation,new[]{0f,1f,0f,0f})&&
            VectorNear(vsGroupTransform.local_position,new[]{0f,0f,0f})&&
            VectorNear(vsGroupTransform.local_scale,new[]{2.5127165f,2.5127165f,2.5127165f})&&
            VectorNear(vsVTransform.local_position,new[]{.15392494f,.01409948f,-.18214296f})&&
            VectorNear(vsSTransform.local_position,new[]{-.12746f,.01409948f,.07530691f})&&
            VectorNear(vsVTransform.local_rotation,new[]{-.7071068f,0f,0f,.7071068f})&&
            VectorNear(vsSTransform.local_rotation,new[]{-.7071068f,0f,0f,.7071068f})&&
            VectorNear(vsVTransform.local_scale,new[]{1f,1f,1f})&&VectorNear(vsSTransform.local_scale,new[]{1f,1f,1f})&&
            VectorNear(vsRootTransform.local_rotation,new[]{0f,0f,0f,1f})&&VectorNear(vsRootTransform.local_scale,new[]{1f,1f,1f}),
            "original VS letter/group transform hierarchy or +Y projection pose changed");
        var derivedVs=vsSources.SingleOrDefault(row=>row.id=="UI_VS_LETTERS");
        string[] derivedSources={"SOURCE_GAMESTART_VS_LETTERS","SOURCE_VS_LETTER_V_MESH","SOURCE_VS_LETTER_S_MESH",
            "SOURCE_VS_LETTER_V_MESH_FILTER","SOURCE_VS_LETTER_S_MESH_FILTER",
            "SOURCE_VS_LETTER_V_MESH_RENDERER","SOURCE_VS_LETTER_S_MESH_RENDERER","SOURCE_VS_LETTERS_MATERIAL",
            "SOURCE_VS_LETTER_V_TRANSFORM","SOURCE_VS_LETTER_S_TRANSFORM",
            "SOURCE_VS_LETTERS_GROUP_TRANSFORM","SOURCE_GAMESTART_VS_LETTERS_TRANSFORM"};
        Require(derivedVs!=null&&derivedVs.projection=="depth_tested_orthographic_world_xz_from_positive_y"&&derivedVs.source_triangle_count==962&&
            derivedSources.All(id=>(derivedVs.source_assets??new string[0]).Contains(id)),
            "derived VS logo provenance does not retain its complete original source graph");

        Require(BattleOpening.LayoutFits(1920,1080)&&BattleOpening.LayoutFits(1440,1080)&&BattleOpening.LayoutFits(1280,720),
            "versus opening layout clips at a supported viewport");
        var openingRects=BattleOpening.LayoutRects;
        for(int i=0;i<openingRects.Length;i++)for(int j=0;j<i;j++)
            Require(!openingRects[i].Overlaps(openingRects[j]),"versus opening portrait/text rects overlap: "+i+" / "+j);

        foreach(string hero in heroes)
        {
            var wow=index.source_rows.SingleOrDefault(row=>row.card_id==hero&&row.emote_id=="WOW");
            var sorry=index.source_rows.SingleOrDefault(row=>row.card_id==hero&&row.emote_id=="SORRY");
            Require(wow!=null&&sorry!=null&&wow.emote_type==33&&sorry.emote_type==6,"WOW and legacy SORRY must retain separate source definitions for "+hero);
        }
        Require(index.bubble_art!=null&&index.bubble_art.asset_name=="Emote_Bubbles"&&index.bubble_art.width==512&&index.bubble_art.height==512,
            "original Emote_Bubbles atlas provenance missing");
        var upper=(index.bubble_art.regions??new HeroSpeechRegion[0]).SingleOrDefault(row=>row.id=="upper_right_tail");
        var lower=(index.bubble_art.regions??new HeroSpeechRegion[0]).SingleOrDefault(row=>row.id=="lower_left_tail");
        Require(upper!=null&&upper.source_rect_px.SequenceEqual(new[]{0,0,512,256})&&upper.tail_tip_px.SequenceEqual(new[]{420,209})&&
            upper.text_rect_px.SequenceEqual(new[]{90,40,340,98})&&!upper.flip_y,"upper bubble source region/tail anchor/body rect mismatch");
        Require(lower!=null&&lower.source_rect_px.SequenceEqual(new[]{0,256,512,256})&&lower.tail_tip_px.SequenceEqual(new[]{121,455})&&
            lower.text_rect_px.SequenceEqual(new[]{88,282,336,80})&&lower.flip_y,"lower bubble source region/tail anchor/body rect mismatch");
        var bubble=(registry.entries??new AssetEntry[0]).SingleOrDefault(row=>row.id=="UI_EMOTE_BUBBLES");
        Require(bubble!=null&&File.Exists(Path.Combine(runtime,bubble.path)),"original emote bubble atlas is not exported");
        Require(index.bubble_art.actor_link!=null&&index.bubble_art.actor_link.Contains("not claimed"),"unknown original actor binding must remain explicit");
        var menu=new HeroEmoteMenu();var ownHero=new Rect(865,700,190,200);int sent=0;
        Require(!menu.ReplayRightClick(new Vector2(960,288),ownHero,"speech-check"),"right-click outside own hero opened menu");
        foreach(string emote in Menu)
        {
            string diagnostic;
            Require(menu.ReplayRightClick(ownHero.center,ownHero,"speech-check",out diagnostic),
                "right-click did not open the seven-option menu: "+diagnostic);
            if(emote==Menu[0])CheckMenuLayout(menu,ownHero);
            Require(menu.ReplaySelect(emote,id=>{Require(id==emote,"menu replay changed selected emote");sent++;}),"menu option did not select "+emote);
        }
        bool consumed;
        Require(menu.ReplayRightClick(ownHero.center,ownHero,"speech-check"),"outside-click check could not open menu");
        menu.ReplayInput(EventType.MouseDown,0,KeyCode.None,new Vector2(100,100),ownHero,"speech-check",out consumed);
        Require(consumed&&!menu.IsOpen,"clicking outside did not close the menu cleanly");
        Require(menu.ReplayRightClick(ownHero.center,ownHero,"speech-check"),"escape check could not open menu");
        menu.ReplayInput(EventType.KeyDown,0,KeyCode.Escape,ownHero.center,ownHero,"speech-check",out consumed);
        Require(consumed&&!menu.IsOpen,"Escape did not close the menu");
        Require(sent==7&&!menu.IsOpen,"menu replay did not send exactly seven options");
        Debug.Log("HERO SPEECH CHECKS passed: 11 heroes, 99 localized clips/subtitles, Attack and announcer sources, original VS letters, seven menu labels, separate WOW/SORRY, original bubble atlas; actor binding remains unverified");
    }

    static void CheckMenuLayout(HeroEmoteMenu menu,Rect hero)
    {
        for(int i=0;i<Menu.Length;i++)
        {
            var image=menu.OptionRect(Menu[i]);var art=menu.OptionArtworkRect(Menu[i]);var hit=menu.OptionHitRect(Menu[i]);
            Require(image.width>0&&image.height>0&&hit.width>0&&hit.height>0,"menu bubble has empty geometry: "+Menu[i]);
            Require(!art.Overlaps(hero),"opaque menu bubble art overlaps the hero: "+Menu[i]);
            Require(i<3?hit.xMax<=hero.xMin:hit.xMin>=hero.xMax,"menu click target overlaps the hero: "+Menu[i]);
            for(int j=0;j<i;j++)
            {
                Require(!art.Overlaps(menu.OptionArtworkRect(Menu[j])),"opaque menu artwork overlaps: "+Menu[j]+" / "+Menu[i]);
                Require(!hit.Overlaps(menu.OptionHitRect(Menu[j])),"menu click targets overlap: "+Menu[j]+" / "+Menu[i]);
            }
        }
        for(int i=0;i<3;i++)
        {
            var leftBody=menu.OptionArtworkRect(Menu[i]);
            var rightBody=menu.OptionArtworkRect(Menu[i+3]);
            Require(Mathf.Abs(leftBody.yMin-rightBody.yMin)<.01f,
                "left/right menu body rows do not align: "+Menu[i]+" / "+Menu[i+3]);
        }
        var lastRightBody=menu.OptionArtworkRect("SORRY");
        var previousRightBody=menu.OptionArtworkRect("THREATEN");
        Require(!lastRightBody.Overlaps(previousRightBody)&&
            !menu.OptionHitRect("SORRY").Overlaps(menu.OptionHitRect("THREATEN")),
            "fourth right menu bubble overlaps the third right bubble");
        foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1440,1080),new Vector2(1280,720)})
        {
            var viewport=new DesignViewport(size.x,size.y,1920,1080,1520);
            foreach(string id in Menu)
            {
                var rect=menu.OptionRect(id);var matrix=viewport.Matrix;
                var a=matrix.MultiplyPoint3x4(new Vector3(rect.xMin,rect.yMin,0));
                var b=matrix.MultiplyPoint3x4(new Vector3(rect.xMax,rect.yMax,0));
                Require(a.x>=0&&a.y>=0&&b.x<=size.x&&b.y<=size.y,"menu bubble clips at "+size.x+"x"+size.y+": "+id);
            }
        }
    }

    static void Require(bool condition,string message)
    {if(!condition)throw new InvalidDataException(message);}

    static bool TransformDataValid(PresentationAsset row)
    {
        return row.usage=="source_only"&&row.local_position!=null&&row.local_position.Length==3&&
            row.local_rotation!=null&&row.local_rotation.Length==4&&row.local_scale!=null&&row.local_scale.Length==3;
    }

    static bool VectorNear(float[] actual,float[] expected)
    {
        return actual!=null&&actual.Length==expected.Length&&actual.Select((value,index)=>Mathf.Abs(value-expected[index])<.001f).All(value=>value);
    }
}
