Shader "LocalLab/OriginalActorHighlight"
{
    Properties
    {
        _Color("Base Color",Color)=(.5,.5,.5,1)
        _HiColor("Hot Color",Color)=(.5,.5,.5,1)
        _Colorize("Colorize",Color)=(1,1,1,1)
        _ColorIntensity("Color Intensity",Float)=1
        _Intensity("Intensity",Float)=1
        _FlareTex("Flare Texture",2D)="white"{}
        _FlareIntensity("Flare Intensity",Float)=1
        _VflareSpeed("Flare V Speed",Float)=1
        _UflareScale("Flare U Scale",Float)=1
        _VflareScale("Flare V Scale",Float)=1
        _ColorAlphaWeight("Color Alpha Weight",Float)=1
        _TexCoordTwoWeight("2nd Texture Coord Weight",Float)=1
        _MainTex("Silhouette",2D)="black"{}
        _Seed("Seed",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="true"}
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_FlareTex;float4 _MainTex_ST,_Color,_HiColor,_Colorize;
            float _ColorIntensity,_Intensity,_FlareIntensity,_VflareSpeed,_UflareScale,_VflareScale,_ColorAlphaWeight,_TexCoordTwoWeight,_Seed;
            float _ShaderTime,_GlobalMipBias;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 uv2:TEXCOORD1;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 data:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.data.xy=lerp(o.uv,TRANSFORM_TEX(v.uv2,_MainTex),_TexCoordTwoWeight);
                o.data.z=v.color.r;o.data.w=_Seed+_ShaderTime;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float mask=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias)).r*_Intensity*1.2;
                float2 flareUV=float2(i.data.x*_UflareScale+i.data.w*.05,mask*_VflareScale+i.data.w*_VflareSpeed);
                float flare=tex2Dbias(_FlareTex,float4(flareUV,0,_GlobalMipBias)).r;
                // DXBC dp2 duplicates both scalar operands; this factor must remain 2.
                float strength=2*(flare*i.data.z+mask)*mask*_FlareIntensity;
                float3 rgb=(_Color.rgb+strength*(strength*_HiColor.rgb-_Color.rgb))*_ColorIntensity;
                float alpha=(1+_ColorAlphaWeight*(i.data.z-1))*strength;
                return float4(rgb,alpha)*_Colorize;
            }
            ENDCG
        }
    }
}
