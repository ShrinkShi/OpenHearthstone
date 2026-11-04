using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AssetRegistry
{
    public static void ReleaseTransient(UnityEngine.Object value)
    {
        if(value==null)return;
        var texture=value as RenderTexture;if(texture!=null){if(RenderTexture.active==texture)RenderTexture.active=null;texture.Release();}
        if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);
    }
    public readonly string Root;
    public Func<Color?> BattleAmbient;
    readonly Dictionary<string,string> entries = new Dictionary<string,string>();
    readonly Dictionary<string,Texture2D> textures = new Dictionary<string,Texture2D>();
    readonly Dictionary<string,Mesh> meshes = new Dictionary<string,Mesh>();
    [Serializable] sealed class HighlightTexture{public string png,raw,name;public int width,height,format,mips,color_space,filter,aniso,wrap_u,wrap_v,wrap_w;public float mip_bias;}
    [Serializable] sealed class HighlightTextures{public HighlightTexture[] textures;}
    HighlightTextures highlightTextures;
    HighlightTextures smokeTextures;
    HighlightTextures superSpellTextures;
    HighlightTextures fireblastTextures;
    HighlightTextures fireblastUITextures;
    public Texture2D FireblastTextureFile(string png)
    {
        string key="native-fireblast:"+png;Texture2D texture;if(textures.TryGetValue(key,out texture))return texture;
        if(fireblastUITextures==null&&File.Exists(Path.Combine(Root,"native-fireblast-ui-textures.json")))fireblastUITextures=JsonUtility.FromJson<HighlightTextures>(File.ReadAllText(Path.Combine(Root,"native-fireblast-ui-textures.json")));
        if(fireblastTextures==null&&File.Exists(Path.Combine(Root,"native-fireblast-textures.json")))fireblastTextures=JsonUtility.FromJson<HighlightTextures>(File.ReadAllText(Path.Combine(Root,"native-fireblast-textures.json")));
        var definition=fireblastUITextures?.textures.FirstOrDefault(t=>t.png==png)??fireblastTextures?.textures.FirstOrDefault(t=>t.png==png);
        if(definition==null)return TextureFile(png);
        if((definition.format!=3&&definition.format!=4&&definition.format!=10&&definition.format!=12)||definition.mips<1)throw new InvalidDataException("Unreviewed fireblast raw texture");
        return LoadRawTexture(key,definition);
    }
    public Texture2D SmokeTextureFile(string png)
    {
        string key="native-smoke:"+png;Texture2D texture;if(textures.TryGetValue(key,out texture))return texture;
        if(smokeTextures==null)smokeTextures=JsonUtility.FromJson<HighlightTextures>(File.ReadAllText(Path.Combine(Root,"native-smoke-source-textures.json")));
        var definition=Array.Find(smokeTextures.textures,t=>t.png==png);
        if(definition==null&&File.Exists(Path.Combine(Root,"native-super-spell-textures.json")))
        {
            if(superSpellTextures==null)superSpellTextures=JsonUtility.FromJson<HighlightTextures>(File.ReadAllText(Path.Combine(Root,"native-super-spell-textures.json")));
            definition=Array.Find(superSpellTextures.textures,t=>t.png==png);
        }
        if(definition==null&&File.Exists(Path.Combine(Root,"native-fireblast-textures.json")))
        {
            if(fireblastTextures==null)fireblastTextures=JsonUtility.FromJson<HighlightTextures>(File.ReadAllText(Path.Combine(Root,"native-fireblast-textures.json")));
            definition=Array.Find(fireblastTextures.textures,t=>t.png==png);
        }
        if(definition==null||(definition.format!=4&&definition.format!=10&&definition.format!=12)||definition.mips<1||(definition.color_space!=0&&definition.color_space!=1))
            throw new InvalidDataException("Unverified original smoke texture");
        return LoadRawTexture(key,definition);
    }
    public Texture2D HighlightTextureFile(string png)
    {
        string key="native-highlight:"+png;Texture2D texture;if(textures.TryGetValue(key,out texture))return texture;
        if(highlightTextures==null)highlightTextures=JsonUtility.FromJson<HighlightTextures>(File.ReadAllText(Path.Combine(Root,"original-highlight-flare.json")));
        var definition=Array.Find(highlightTextures.textures,t=>t.png==png);
        if(definition==null||(definition.format!=3&&definition.format!=10)||definition.mips!=1||(definition.color_space!=0&&definition.color_space!=1))
            throw new InvalidDataException("Unverified original highlight texture");
        return LoadRawTexture(key,definition);
    }
    Texture2D LoadRawTexture(string key,HighlightTexture definition)
    {
        var texture=new Texture2D(definition.width,definition.height,(TextureFormat)definition.format,definition.mips,definition.color_space==0);
        try
        {
            texture.name=definition.name;texture.LoadRawTextureData(File.ReadAllBytes(Path.Combine(Root,definition.raw)));texture.Apply(false,false);
            texture.filterMode=(FilterMode)definition.filter;texture.anisoLevel=definition.aniso;texture.mipMapBias=definition.mip_bias;
            texture.wrapModeU=(TextureWrapMode)definition.wrap_u;texture.wrapModeV=(TextureWrapMode)definition.wrap_v;texture.wrapModeW=(TextureWrapMode)definition.wrap_w;
            textures.Add(key,texture);return texture;
        }
        catch{ReleaseTransient(texture);throw;}
    }
    Dictionary<string,NativeActorSpellDefinition> actorSpellTables;
    public NativeActorSpellDefinition ActorSpellTable(string reference)
    {
        if(actorSpellTables==null)
        {
            actorSpellTables=new Dictionary<string,NativeActorSpellDefinition>();var path=Path.Combine(Root,"actor-spell-tables.json");
            if(File.Exists(path))foreach(var table in JsonUtility.FromJson<NativeActorSpellCatalog>(File.ReadAllText(path)).tables)actorSpellTables.Add(table.reference,table);
        }
        NativeActorSpellDefinition result;return reference!=null&&actorSpellTables.TryGetValue(reference,out result)?result:null;
    }
    public int TextureCount{get{return textures.Count;}}
    public Action<string> MissingPortrait;
    public AssetRegistry(string root)
    {
        Root = Path.Combine(root, "runtime");
        Refresh();
    }
    public void Refresh(){foreach(var e in JsonUtility.FromJson<AssetList>(File.ReadAllText(Path.Combine(Root,"registry.json"))).entries)entries[e.id]=e.path;}
    public string PathFor(string id) { return entries.ContainsKey(id) ? Path.Combine(Root,entries[id]) : ""; }
    public Texture2D Texture(string id)
    {
        var texture=TextureFile(entries.ContainsKey(id) ? entries[id] : id);
        if(texture==null&&id.StartsWith("CARD_")&&MissingPortrait!=null)MissingPortrait(id.Substring(5));
        return texture;
    }
    public Texture2D TextureFile(string filename)
    {
        if (String.IsNullOrEmpty(filename)) return null;
        if (textures.ContainsKey(filename)) return textures[filename];
        string path = Path.Combine(Root,filename);
        if (!File.Exists(path)) return null;
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
        texture.LoadImage(File.ReadAllBytes(path));
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        textures[filename] = texture;
        return texture;
    }
    public IEnumerator Audio(string id, Action<AudioClip> ready)
    {
        var path = PathFor(id);
        if (path == "") yield break;
        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.WAV))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) ready(DownloadHandlerAudioClip.GetContent(request));
            else Debug.LogWarning(id + ": " + request.error);
        }
    }
    public Mesh MeshFile(string filename)
    {
        if (meshes.ContainsKey(filename)) return meshes[filename];
        var data = JsonUtility.FromJson<MeshData>(File.ReadAllText(Path.Combine(Root,filename)));
        var mesh = new Mesh {name = filename, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32};
        var vertices = new Vector3[data.vertices.Length/3];
        for (int i=0;i<vertices.Length;i++) vertices[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
        mesh.vertices=vertices;
        bool hasBoneWeights=data.bone_weights!=null&&data.bone_weights.Length>0;
        bool hasBoneIndices=data.bone_indices!=null&&data.bone_indices.Length>0;
        bool hasBindposes=data.bindposes!=null&&data.bindposes.Length>0;
        // 导出器用 null 表示静态网格；JsonUtility 可能将其解析为空数组。
        if(hasBoneWeights||hasBoneIndices||hasBindposes)
        {
            if(data.bone_weights==null||data.bone_indices==null||data.bindposes==null||
               data.bone_weights.Length!=vertices.Length*4||data.bone_indices.Length!=vertices.Length*4||
               data.bindposes.Length==0||data.bindposes.Length%16!=0)
                throw new InvalidDataException("Skinned mesh data is incomplete: "+filename);
            var weights=new BoneWeight[vertices.Length];
            for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{
                boneIndex0=data.bone_indices[i*4],boneIndex1=data.bone_indices[i*4+1],
                boneIndex2=data.bone_indices[i*4+2],boneIndex3=data.bone_indices[i*4+3],
                weight0=data.bone_weights[i*4],weight1=data.bone_weights[i*4+1],
                weight2=data.bone_weights[i*4+2],weight3=data.bone_weights[i*4+3]};
            mesh.boneWeights=weights;
            var bindposes=new Matrix4x4[data.bindposes.Length/16];
            for(int i=0;i<bindposes.Length;i++)for(int row=0;row<4;row++)for(int column=0;column<4;column++)
                bindposes[i][row,column]=data.bindposes[i*16+row*4+column];
            mesh.bindposes=bindposes;
        }
        if(data.colors!=null && data.colors.Length==vertices.Length*4)
        {
            var colors=new Color[vertices.Length];
            for(int i=0;i<colors.Length;i++) colors[i]=new Color(data.colors[i*4],data.colors[i*4+1],data.colors[i*4+2],data.colors[i*4+3]);
            mesh.colors=colors;
        }
        if (data.uv.Length == vertices.Length*2)
        {
            var uv = new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++) uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);
            mesh.uv=uv;
        }
        if(data.uv2!=null&&data.uv2.Length==vertices.Length*2)
        {
            var uv2=new Vector2[vertices.Length];
            for(int i=0;i<uv2.Length;i++)uv2[i]=new Vector2(data.uv2[i*2],data.uv2[i*2+1]);
            mesh.uv2=uv2;
        }
        mesh.subMeshCount=data.submeshes.Length;
        for(int i=0;i<data.submeshes.Length;i++) mesh.SetTriangles(data.submeshes[i].triangles,i);
        if(data.source_normals&&data.normals!=null&&data.normals.Length==vertices.Length*3)
        {
            var normals=new Vector3[vertices.Length];
            for(int i=0;i<normals.Length;i++)normals[i]=new Vector3(data.normals[i*3],data.normals[i*3+1],data.normals[i*3+2]);
            mesh.normals=normals;
        }
        else mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshes[filename]=mesh;
        return mesh;
    }
    public GameObject Scene(string id)
    {
        var path=PathFor(id);
        if(path=="" || !File.Exists(path)) return new GameObject(id+"_Missing");
        var data=JsonUtility.FromJson<SceneData>(File.ReadAllText(path));
        var root=new GameObject(id);
        var objects=new GameObject[data.nodes.Length];
        for(int i=0;i<data.nodes.Length;i++)
        {
            var n=data.nodes[i]; var go=new GameObject(n.name); objects[i]=go;
            go.transform.SetParent(n.parent<0?root.transform:objects[n.parent].transform,false);
            go.transform.localPosition=n.position; go.transform.localRotation=n.rotation; go.transform.localScale=n.scale;
            if(!String.IsNullOrEmpty(n.mesh))
            {
                go.AddComponent<MeshFilter>().sharedMesh=MeshFile(n.mesh);
                var renderer=go.AddComponent<MeshRenderer>();
                var materials=new Material[n.materials.Length];
                for(int j=0;j<materials.Length;j++)
                {
                    var m=n.materials[j];
                    if(id=="MODEL_ACTOR_MINION"&&m.native!=null&&m.native.shader==m.shader&&(m.shader=="Custom/Card/Unlit_Desaturate_2Texture2uv"||m.shader=="Custom/Card/UnlitTexture"))
                    {materials[j]=NativeActorMaterial.Create(m.native,this);continue;}
                    var mat=new Material(Shader.Find("LocalLab/AssetSurface"));
                    mat.name=m.name;
                    mat.mainTexture=TextureFile(m.texture);
                    mat.SetFloat("_UseAlpha",m.transparent?1:0);
                    mat.mainTextureScale=m.texture_scale;mat.mainTextureOffset=m.texture_offset;
                    var tint=m.tint;
                    // 原菜单专用 Shader 未移植；Unlit_Texture 不做漫反射乘色，旧自发光近似补偿双倍光照。
                    // 此兼容只作用于木盒场景，不改变用户校对过的卡牌、图标和棋盘材质。
                    if(id=="SCENE_MAIN_MENU"&&m.shader=="Hero/Unlit/Unlit_Texture")tint=new Color(1,1,1,tint.a);
                    else if(id=="SCENE_MAIN_MENU"&&m.shader=="Hero/Self-Illumin_Diffuse")tint=new Color(tint.r*2,tint.g*2,tint.b*2,tint.a);
                    if(m.effect)tint.a*=m.intensity;mat.SetColor("_Tint",tint);
                    mat.SetFloat("_VertexTint",m.effect?1:0);
                    mat.SetFloat("_Effect",m.effect?1:0);
                    string shader=m.shader??"";
                    if(shader.Contains("Multiply"))
                    {
                        mat.SetFloat("_Multiply",1);mat.SetInt("_SrcBlend",2);mat.SetInt("_DstBlend",0);
                    }
                    else if(shader.Contains("Additive"))
                    {
                        mat.SetInt("_SrcBlend",5);mat.SetInt("_DstBlend",1);mat.SetFloat("_Effect",1);
                    }
                    mat.renderQueue=m.transparent?3000:2000;
                    mat.SetInt("_ZWrite",m.transparent?0:1);
                    materials[j]=mat;
                }
                renderer.sharedMaterials=materials;
                renderer.sortingOrder=n.sorting_order;
                if(id=="MODEL_ACTOR_MINION"&&Array.Exists(n.materials,m=>m.native!=null&&m.shader=="Custom/Card/Unlit_Desaturate_2Texture2uv"))
                    NativeActorMaterial.BindAmbient(renderer,this);
            }
            go.SetActive(n.active || n.parent<0);
        }
        foreach(var actor in data.actors??new SceneActor[0])
        {
            if(actor.owner<0||actor.owner>=objects.Length||actor.portrait<-1||actor.portrait>=objects.Length)
                throw new InvalidDataException("Original Actor reference outside scene hierarchy");
            var identity=objects[actor.owner].AddComponent<NativeActorIdentity>();
            identity.Configure(actor.portrait<0?null:objects[actor.portrait],actor.spell_table,actor.portrait_material_index);
            identity.SpellDefinition=ActorSpellTable(actor.spell_table);
            if(!String.IsNullOrEmpty(actor.animation_file))identity.AnimationProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(Root,actor.animation_file)));
            if(identity.RootObject!=null)objects[actor.owner].AddComponent<NativeActorPresentation>().Configure(identity.RootObject);
        }
        return root;
    }
    public static void Fit(GameObject go, float width)
    {
        var renderers=go.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0) return;
        Bounds b=renderers[0].bounds;
        foreach(var r in renderers) b.Encapsulate(r.bounds);
        float factor=width/Mathf.Max(b.size.x,b.size.z,0.001f);
        go.transform.localScale*=factor;
        go.transform.position-=b.center*factor;
    }
}
