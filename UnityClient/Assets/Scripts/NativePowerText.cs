using System;
using UnityEngine;

[Serializable] public sealed class NativePowerTextSettings
{
    public int m_FontSize,m_RichText,m_Outline,m_Anchor,m_Alignment,m_RenderQueue,m_ResizeToFit;
    public float m_CharacterSize,m_LineSpacing,m_OutlineSize,m_Width,m_Height,m_MinCharacterSize;
    public Color m_TextColor,m_OutlineColor;
    public int m_RenderToTexture,m_Resolution,m_AntiAlias;public float m_AntiAliasEdge,m_AntiAliasAmount;public Color m_GradientUpperColor,m_GradientLowerColor;
}
[Serializable] public sealed class NativePowerFontDefinition
{
    public string file;public float font_size_modifier,line_space_modifier,character_size_modifier=1,single_line_adjustment;
}

// 原UberText的已审阅TextMesh分支，动态字图仍由Unity字体服务生成。
public sealed class NativePowerText:IDisposable
{
    readonly TextMesh text;readonly MeshRenderer renderer;readonly Material material,boldMaterial;readonly Font font;
    readonly NativePowerTextSettings source;readonly NativePowerFontDefinition definition;readonly bool body;
    readonly NativePowerTextCapture capture;
    public TextMesh Mesh{get{return text;}}
    public NativePowerText(NativeEffectInstance effect,GameObject owner,NativePowerTextSettings source,Font font,NativePowerFontDefinition definition,bool body)
    {
        this.source=source;this.font=font;this.body=body;this.definition=definition;
        if(source.m_Anchor!=1||source.m_Alignment!=1)throw new NotSupportedException("Unreviewed original power text anchor");
        var go=new GameObject("Original power TextMesh");go.layer=30;go.transform.SetParent(owner.transform,false);
        if(body)go.transform.localPosition=new Vector3(0,-.05f,0);
        text=go.AddComponent<TextMesh>();renderer=go.GetComponent<MeshRenderer>();
        text.font=font;text.fontSize=(int)(definition.font_size_modifier*1f*(float)source.m_FontSize);text.characterSize=source.m_CharacterSize*definition.character_size_modifier*.01f;
        text.lineSpacing=source.m_LineSpacing*definition.line_space_modifier;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.richText=source.m_RichText!=0;text.color=source.m_TextColor;
        var materialDefinition=new NativeMaterial{name="Original power text",shader=source.m_Outline==0?"Hero/Text_Unlit":source.m_RichText==0?"Hidden/TextOutline_Unlit_NoVertColor":"Hidden/TextOutline_Unlit",
            src=5,dst=10,src_alpha=5,dst_alpha=10,render_queue=3000+source.m_RenderQueue,
            properties=new[]{"_MainTex","_Color","_OutlineColor","_OutlineOffsetX","_OutlineOffsetY"},
            floats=new NativeFloat[0],colors=new NativeColor[0],textures=new NativeTexture[0]};
        material=effect.ReplaceVisualMaterial(renderer,materialDefinition);material.SetColor("_Color",source.m_RichText==0?source.m_TextColor:Color.white);material.SetColor("_OutlineColor",source.m_OutlineColor);
        if(source.m_RichText!=0)
        {
            var boldDefinition=JsonUtility.FromJson<NativeMaterial>(JsonUtility.ToJson(materialDefinition));boldDefinition.shader=source.m_Outline==0?"Hidden/Text_Bold":"Hidden/TextBoldOutline_Unlit";
            boldMaterial=effect.CreateVisualMaterial(boldDefinition);boldMaterial.SetColor("_Color",Color.white);boldMaterial.SetColor("_OutlineColor",source.m_OutlineColor);renderer.sharedMaterials=new[]{material,boldMaterial};
        }
        renderer.sortingOrder=source.m_RenderQueue;
        if(source.m_RenderToTexture!=0)
        {
            if(source.m_AntiAlias==0)throw new NotSupportedException("Unreviewed non-antialiased title RTT");
            capture=new NativePowerTextCapture(effect,owner,renderer,source);
        }
    }
    public void Set(string value)
    {
        string processed=source.m_RichText!=0?"<material=1></material>"+value.Replace("<b>","<material=1>").Replace("</b>","</material>"):value;
        if(text.text!=processed)
        {
            text.text=processed;text.characterSize=source.m_CharacterSize*definition.character_size_modifier*.01f;
            text.lineSpacing=value.Contains("\n")?source.m_LineSpacing*definition.line_space_modifier:source.m_LineSpacing+definition.single_line_adjustment;
            if(source.m_ResizeToFit!=0)Fit();
        }
        RefreshTexture();
    }
    void Fit()
    {
        var transform=text.transform;var parent=transform.parent;var position=transform.localPosition;var rotation=transform.localRotation;var scale=transform.localScale;
        try
        {
            transform.SetParent(null,false);transform.position=Vector3.zero;transform.rotation=Quaternion.identity;transform.localScale=Vector3.one;
            for(int step=0;step<40;step++)
            {
                var size=renderer.bounds.size;
                if(size.x<=source.m_Width&&size.y<=source.m_Height)break;
                text.characterSize=Mathf.Max(source.m_MinCharacterSize*.01f,text.characterSize*.95f);
            }
        }
        finally{transform.SetParent(parent,false);transform.localPosition=position;transform.localRotation=rotation;transform.localScale=scale;}
    }
    public void SetColor(Color color)
    {text.color=color;material.SetColor("_Color",source.m_RichText==0?color:Color.white);}
    public void RefreshTexture()
    {
        var texture=font.material.mainTexture;if(texture==null)throw new InvalidOperationException("Original power dynamic font atlas unavailable");
        material.mainTexture=texture;material.SetFloat("_OutlineOffsetX",source.m_OutlineSize/texture.width);material.SetFloat("_OutlineOffsetY",source.m_OutlineSize/texture.height);
        if(boldMaterial!=null)
        {
            float offset=source.m_Outline!=0?source.m_OutlineSize+1.08f*.75f:1.08f;
            boldMaterial.mainTexture=texture;boldMaterial.SetFloat("_OutlineOffsetX",offset/texture.width);boldMaterial.SetFloat("_OutlineOffsetY",offset/texture.height);
        }
        capture?.Render();
    }
    public void Dispose(){capture?.Dispose();}
}
