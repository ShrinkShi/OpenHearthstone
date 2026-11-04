Shader "LocalLab/OriginalActorFrame"
{
    Properties
    {
        _MainTex("Texture",2D)="white"{}
        _LightingBlend("Ambient lighting blend",Float)=0
        _MainLightColor("Source main light input",Color)=(0,0,0,0)
        _GlobalMipBias("Source global mip bias",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry" "Highlight"="true"}
        Cull Off ZWrite On ZTest LEqual Blend One Zero
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "OriginalActorFrame.cginc"
            float3 _LightDirection;float4 _ShadowBias;
            float4 vert(appdata_base v):SV_POSITION
            {
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                float3 normal=mul(v.normal,(float3x3)unity_WorldToObject);
                return OriginalFrameShadow(world,normal,_LightDirection,_ShadowBias.xy,UNITY_MATRIX_VP);
            }
            float4 frag():SV_Target{return 1;}
            ENDCG
        }
        Pass
        {
            Name "ActorFrame"
            Tags {"LightMode"="Always"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "OriginalActorFrame.cginc"
            sampler2D _MainTex;float4 _MainTex_ST,_MainLightColor;
            float _LightingBlend,_GlobalMipBias;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.color=OriginalFrameColor(v.color,float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w),_MainLightColor.rgb,_LightingBlend);
                return o;
            }
            float4 frag(v2f i):SV_Target{return tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias))*i.color;}
            ENDCG
        }
    }
}
